using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>STL geometry only: the format has no units, assemblies or joints.</summary>
public static class StlReader
{
    public const int MaxTriangles = 200000;
    public const int MaxBytes = 32 * 1024 * 1024;
    public static Vector3[] Read(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaxBytes) throw new InvalidDataException("STL missing or larger than 32 MB.");
        return Parse(File.ReadAllBytes(path));
    }
    public static Vector3[] Parse(byte[] data)
    {
        if (data.Length > MaxBytes) throw new InvalidDataException("STL exceeds 32 MB.");
        // A binary STL header can begin with 'solid'. Use the exact record length instead.
        if (data.Length >= 84)
        {
            uint count = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(80, 4));
            if (84L + 50L * count == data.Length)
            {
                if (count == 0 || count > MaxTriangles) throw new InvalidDataException("STL needs 1–200,000 triangles.");
                var vertices = new Vector3[checked((int)count * 3)];
                using var reader = new BinaryReader(new MemoryStream(data));
                reader.BaseStream.Position = 84;
                for (int triangle = 0; triangle < count; triangle++)
                {
                    reader.BaseStream.Position += 12; // Recompute normals instead of trusting exported normals.
                    for (int v = 0; v < 3; v++) vertices[triangle * 3 + v] = Check(new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                    reader.ReadUInt16();
                }
                ValidateTriangles(vertices);
                return vertices;
            }
        }
        using var text = new StringReader(new UTF8Encoding(false, true).GetString(data));
        var result = new List<Vector3>();
        bool solid = false, facet = false, loop = false;
        int facetVertices = 0, facets = 0;
        string line;
        while ((line = text.ReadLine()) != null)
        {
            var tokens = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;
            switch (tokens[0].ToLowerInvariant())
            {
                case "solid": if (solid || facet) throw Bad(); solid = true; break;
                case "facet": if (!solid || facet || tokens.Length != 5 || tokens[1] != "normal") throw Bad(); facet = true; facetVertices = 0; break;
                case "outer": if (!facet || loop || tokens.Length != 2 || tokens[1] != "loop") throw Bad(); loop = true; break;
                case "vertex":
                    if (!loop || tokens.Length != 4 || facetVertices >= 3 || result.Count >= MaxTriangles * 3) throw Bad();
                    result.Add(Check(new(Number(tokens[1]), Number(tokens[2]), Number(tokens[3])))); facetVertices++; break;
                case "endloop": if (!loop || facetVertices != 3) throw Bad(); loop = false; break;
                case "endfacet": if (!facet || loop || facetVertices != 3) throw Bad(); facet = false; facets++; break;
                case "endsolid": if (!solid || facet || loop) throw Bad(); solid = false; break;
                default: throw Bad();
            }
        }
        if (solid || facet || loop || facets == 0 || result.Count != facets * 3) throw Bad();
        var points = result.ToArray(); ValidateTriangles(points); return points;
    }
    private static float Number(string value) => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static Vector3 Check(Vector3 p)
    {
        if (!p.IsFinite() || Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z))) > 10000000)
            throw new InvalidDataException("STL has invalid coordinates.");
        return p;
    }
    private static void ValidateTriangles(Vector3[] points)
    {
        bool valid = false;
        for (int i = 0; i < points.Length; i += 3)
            if ((points[i + 1] - points[i]).Cross(points[i + 2] - points[i]).LengthSquared() > 0) { valid = true; break; }
        if (!valid) throw new InvalidDataException("STL has no non-degenerate triangles.");
    }
    private static InvalidDataException Bad() => new("Invalid or incomplete ASCII STL (max 200,000 triangles).");
}
