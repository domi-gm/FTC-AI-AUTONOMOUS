using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

public sealed class ImportedPart
{
    public string Name { get; set; } = "Rigid group";
    public string Asset { get; set; } = "";
    public string BodyId { get; set; } = "chassis";
    public ImportedPart Copy() => (ImportedPart)MemberwiseClone();
}

public sealed class ImportedRobotDefinition
{
    public List<ImportedPart> Parts { get; set; } = new();
    public string Units { get; set; } = "mm";
    public string UpAxis { get; set; } = "Z";
    public int QuarterTurns { get; set; }
    public string SourceFormat { get; set; } = "stl";
    public List<string> ImportNotes { get; set; } = new();
    public List<RobotBodyDefinition> Bodies { get; set; } = new();
    public List<RobotJointDefinition> Joints { get; set; } = new();
    public ImportedRobotDefinition Copy() => new() { Parts = Parts.Select(p => p.Copy()).ToList(), Units = Units, UpAxis = UpAxis, QuarterTurns = QuarterTurns,
        Bodies = Bodies.Select(b => b.Copy()).ToList(), Joints = Joints.Select(j => j.Copy()).ToList(), SourceFormat = SourceFormat, ImportNotes = new(ImportNotes) };
    public void Validate()
    {
        if (Parts == null || Parts.Count > 32 || !new[] { "mm", "cm", "m", "in" }.Contains(Units)
            || !new[] { "Z", "Y" }.Contains(UpAxis) || QuarterTurns < 0 || QuarterTurns > 3 || !new[] { "stl", "urdf" }.Contains(SourceFormat)
            || ImportNotes == null || ImportNotes.Count > 600 || ImportNotes.Any(n => n == null || n.Length > 1000))
            throw new ArgumentException("Invalid imported robot settings.");
        foreach (var part in Parts)
            if (part == null || string.IsNullOrWhiteSpace(part.Name) || part.Name.Length > 100 || part.Asset == null
                || part.Asset.Length != 68 || !part.Asset.EndsWith(".stl", StringComparison.Ordinal)
                || !part.Asset[..64].All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                throw new ArgumentException("Invalid imported STL reference.");
        RobotMechanismValidation.ValidateStructure(this);
    }
}

/// <summary>STL assets and common-frame geometry shared by previews and articulated runtime rigs.</summary>
public static class ImportedRobot
{
    private const string AssetFolder = "user://robot-assets";
    private static readonly Dictionary<string, Vector3[]> Cache = new();
    public static string AssetPath(string asset) => ProjectSettings.GlobalizePath(AssetFolder + "/" + asset);
    public static ImportedPart Import(string path)
    {
        if (!Path.GetExtension(path).Equals(".stl", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Choose an STL file.");
        if (new FileInfo(path).Length > StlReader.MaxBytes) throw new InvalidDataException("STL exceeds 32 MB.");
        byte[] bytes = System.IO.File.ReadAllBytes(path);
        return Store(path, bytes, StlReader.Parse(bytes));
    }
    public static ImportedPart Store(string path, byte[] bytes, Vector3[] vertices)
    {
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + ".stl";
        Directory.CreateDirectory(ProjectSettings.GlobalizePath(AssetFolder));
        string destination = AssetPath(hash);
        if (!System.IO.File.Exists(destination)) System.IO.File.WriteAllBytes(destination, bytes);
        Remember(hash, vertices);
        string name = Path.GetFileNameWithoutExtension(path);
        return new() { Name = name.Length > 100 ? name[..100] : name, Asset = hash };
    }
    private static void Remember(string key, Vector3[] points)
    {
        if (Cache.Count >= 32 && !Cache.ContainsKey(key)) Cache.Clear();
        Cache[key] = points;
    }
    private static Vector3[] Points(ImportedPart part)
    {
        if (!Cache.TryGetValue(part.Asset, out var points))
        { points = StlReader.Read(AssetPath(part.Asset)); Remember(part.Asset, points); }
        return points;
    }
    public static float UnitScale(ImportedRobotDefinition definition) => definition.Units switch { "mm" => .001f, "cm" => .01f, "in" => .0254f, _ => 1 };
    public static Vector3 ConvertPoint(Vector3 p, ImportedRobotDefinition definition)
    {
        p *= UnitScale(definition);
        if (definition.UpAxis == "Z") p = new(p.X, p.Z, -p.Y);
        return p.Rotated(Vector3.Up, definition.QuarterTurns * Mathf.Pi / 2);
    }
    public static Vector3 NormalizationOffset(ImportedRobotDefinition definition)
    {
        var bounds = Bounds(definition); return new(-bounds.GetCenter().X, -bounds.Position.Y, -bounds.GetCenter().Z);
    }
    public static Aabb GetBodyBounds(ImportedRobotDefinition definition, string id)
    {
        var offset = NormalizationOffset(definition);
        bool first = true; Aabb bounds = default;
        foreach (var part in definition.Parts.Where(p => p.BodyId == id)) foreach (var point in Points(part))
        {
            var p = ConvertPoint(point, definition) + offset;
            if (first) { bounds = new(p, Vector3.Zero); first = false; } else bounds = bounds.Expand(p);
        }
        if (first || bounds.Size.X < .001f || bounds.Size.Y < .001f || bounds.Size.Z < .001f)
            throw new InvalidDataException("Rigid group '" + id + "' needs a non-flat STL mesh (minimum 1 mm per axis).");
        return bounds;
    }
    public static int TriangleCount(ImportedRobotDefinition definition)
    {
        definition.Validate(); int count = 0;
        foreach (var part in definition.Parts) count += Points(part).Length / 3;
        if (count > StlReader.MaxTriangles) throw new InvalidDataException("Robot exceeds 200,000 triangles. Export coarser STL meshes.");
        return count;
    }
    public static Aabb Bounds(ImportedRobotDefinition definition)
    {
        definition.Validate();
        TriangleCount(definition);
        bool first = true; Aabb bounds = default;
        foreach (var part in definition.Parts)
        {
            var points = Points(part);
            foreach (var point in points)
            {
                var p = ConvertPoint(point, definition);
                if (first) { bounds = new(p, Vector3.Zero); first = false; } else bounds = bounds.Expand(p);
            }
        }
        if (first || !bounds.Size.IsFinite() || bounds.Size.X < .001f || bounds.Size.Y < .001f || bounds.Size.Z < .001f
            || bounds.Size.X > 3 || bounds.Size.Y > 3 || bounds.Size.Z > 3)
            throw new InvalidDataException("Robot dimensions must be 1 mm–3 m on each axis. Check STL units and up axis.");
        return bounds;
    }
    public static Node3D Build(ImportedRobotDefinition definition)
    {
        var bounds = Bounds(definition);
        var root = new Node3D { Name = "ImportedRobot" };
        // Shared normalization preserves the relative positions of all Fusion exports.
        Vector3 offset = new(-bounds.GetCenter().X, -bounds.Position.Y, -bounds.GetCenter().Z);
        int index = 0;
        foreach (var part in definition.Parts)
        {
            var points = Points(part); var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < points.Length; i += 3)
            {
                var a = ConvertPoint(points[i], definition) + offset;
                var b = ConvertPoint(points[i + 1], definition) + offset;
                var c = ConvertPoint(points[i + 2], definition) + offset;
                var normal = (b - a).Cross(c - a);
                if (normal.LengthSquared() == 0) continue;
                normal = normal.Normalized();
                // Godot uses clockwise front faces; normals are computed from the source STL winding.
                surface.SetNormal(normal); surface.AddVertex(a); surface.AddVertex(c); surface.AddVertex(b);
            }
            VisualFactory.Mesh(root, "RigidGroup" + index, surface.Commit(), Vector3.Zero,
                index++ % 2 == 0 ? VisualFactory.Steel : new Color("adb8c5"));
            surface.Dispose();
        }
        return root;
    }
}
