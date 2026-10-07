using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

/// <summary>Reads a local, expanded URDF package without ROS or a Fusion installation.</summary>
public static class UrdfRobotReader
{
    public sealed class Geometry
    {
        public string Name, BodyId;
        public byte[] Bytes;
        public Vector3[] Points;
    }
    public sealed class Result
    {
        public string Name;
        public ImportedRobotDefinition Definition;
        public readonly List<Geometry> Meshes = new();
        public readonly List<string> Notes = new();
        // Called on the Godot thread only after the complete input package was validated.
        public ImportedRobotDefinition Store()
        {
            var d = Definition.Copy();
            foreach (var mesh in Meshes)
            {
                var part = ImportedRobot.Store(mesh.Name + ".stl", mesh.Bytes, mesh.Points);
                part.Name = mesh.Name.Length > 100 ? mesh.Name[..100] : mesh.Name;
                part.BodyId = mesh.BodyId; d.Parts.Add(part);
            }
            RobotMechanismValidation.ValidateReady(d); return d;
        }
    }
    private sealed class Connection
    {
        public string Name, Parent, Child, Type;
        public Transform3D Origin;
        public Vector3 Axis;
        public float Lower, Upper, Reference, Speed, Effort;
    }
    public static Result Read(string path)
    {
        if (!Path.GetExtension(path).Equals(".urdf", StringComparison.OrdinalIgnoreCase)) throw Bad("Choose an expanded .urdf file, not .xacro.");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > 1024 * 1024) throw Bad("URDF missing or larger than 1 MB.");
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 };
        using var reader = XmlReader.Create(path, settings);
        var document = XDocument.Load(reader);
        var robot = document.Root;
        if (robot?.Name != "robot" || document.Descendants().Any(e => e.Name.NamespaceName != "" || e.Attributes().Any(a => a.Value.Contains("${") || a.Value.Contains("$("))))
            throw Bad("Use an expanded URDF: xacro macros and namespaced elements are not supported.");
        var result = new Result { Name = Required(robot, "name"), Definition = new ImportedRobotDefinition { Units = "m", UpAxis = "Z", SourceFormat = "urdf" } };
        var links = new Dictionary<string, XElement>();
        foreach (var link in robot.Elements("link")) if (!links.TryAdd(Required(link, "name"), link)) throw Bad("Duplicate URDF link name.");
        if (links.Count == 0 || links.Count > 256) throw Bad("URDF must contain 1–256 links, with at most 32 physical groups.");
        var incoming = new Dictionary<string, Connection>(); var names = new HashSet<string>();
        foreach (var joint in robot.Elements("joint"))
        {
            var j = new Connection { Name = Required(joint, "name"), Parent = Required(Single(joint, "parent", true), "link"),
                Child = Required(Single(joint, "child", true), "link"), Type = Required(joint, "type"), Origin = Origin(Single(joint, "origin")) };
            if (!new[] { "fixed", "revolute", "continuous", "prismatic" }.Contains(j.Type)) throw Bad("Joint '" + j.Name + "': unsupported type '" + j.Type + "'.");
            if (Single(joint, "mimic") != null) throw Bad("Joint '" + j.Name + "' uses mimic coupling, which is not supported yet.");
            if (!names.Add(j.Name) || !links.ContainsKey(j.Parent) || !links.ContainsKey(j.Child) || j.Parent == j.Child || !incoming.TryAdd(j.Child, j))
                throw Bad("Invalid parent/child graph or duplicate joint: " + j.Name);
            j.Axis = j.Type == "fixed" ? Vector3.Right : Vector(Single(joint, "axis")?.Attribute("xyz")?.Value, Vector3.Right);
            if (j.Axis.LengthSquared() < .000001f || j.Axis.LengthSquared() > 1000000) throw Bad("Invalid axis on joint '" + j.Name + "'.");
            j.Axis = j.Axis.Normalized();
            var limit = Single(joint, "limit");
            if (j.Type is "revolute" or "prismatic")
            {
                if (limit == null) throw Bad("Joint '" + j.Name + "' needs a limit element.");
                j.Lower = Number(limit.Attribute("lower")?.Value ?? "0"); j.Upper = Number(limit.Attribute("upper")?.Value ?? "0");
                if (j.Lower >= j.Upper) throw Bad("Joint '" + j.Name + "' must have lower < upper.");
                j.Reference = Mathf.Clamp(0, j.Lower, j.Upper);
                if (j.Reference != 0) result.Notes.Add(j.Name + ": initial pose shifted to " + j.Reference.ToString("G6", CultureInfo.InvariantCulture) + (j.Type == "prismatic" ? " m" : " rad") + " to respect its limits.");
            }
            if (j.Type != "fixed" && limit != null)
            {
                j.Speed = Number(Required(limit, "velocity")); j.Effort = Number(Required(limit, "effort"));
                if (j.Speed < 0 || j.Effort < 0) throw Bad("Negative motor limits on '" + j.Name + "'.");
            }
            if (Single(joint, "dynamics") != null || Single(joint, "safety_controller") != null || Single(joint, "calibration") != null)
                result.Notes.Add(j.Name + ": dynamics, safety and calibration extensions are not applied.");
        }
        var roots = links.Keys.Where(n => !incoming.ContainsKey(n)).ToArray();
        if (roots.Length != 1) throw Bad("URDF must be one connected tree with a single root link.");
        var poses = new Dictionary<string, Transform3D> { [roots[0]] = Transform3D.Identity };
        var visiting = new HashSet<string>();
        foreach (var link in links.Keys) Resolve(link);
        Transform3D Resolve(string name)
        {
            if (poses.TryGetValue(name, out var pose)) return pose;
            if (!visiting.Add(name) || !incoming.TryGetValue(name, out var joint)) throw Bad("Disconnected or cyclic URDF joint graph.");
            var motion = Transform3D.Identity;
            if (joint.Type == "revolute") motion.Basis = new Basis(joint.Axis, joint.Reference);
            if (joint.Type == "prismatic") motion.Origin = joint.Axis * joint.Reference;
            pose = Resolve(joint.Parent) * joint.Origin * motion; visiting.Remove(name); poses.Add(name, pose); return pose;
        }
        var geometries = new Dictionary<string, XElement[]>();
        foreach (var link in links)
        {
            var visuals = link.Value.Elements("visual").ToArray();
            geometries[link.Key] = visuals.Length == 0 ? link.Value.Elements("collision").ToArray() : visuals;
            if (visuals.Length == 0 && geometries[link.Key].Length > 0) result.Notes.Add(link.Key + ": collision geometry used because no visual was supplied.");
            if (geometries[link.Key].Length == 0 && incoming.TryGetValue(link.Key, out var connection) && connection.Type != "fixed")
                throw Bad("Moving link '" + link.Key + "' needs visual or collision geometry.");
        }
        bool FixedToRoot(string name)
        {
            while (incoming.TryGetValue(name, out var j)) { if (j.Type != "fixed") return false; name = j.Parent; } return true;
        }
        var physical = links.Keys.Where(n => geometries[n].Length > 0).ToArray();
        int Depth(string link) { int depth = 0; while (incoming.TryGetValue(link, out var j)) { depth++; link = j.Parent; } return depth; }
        string chassis = physical.Where(FixedToRoot).OrderBy(Depth).FirstOrDefault();
        if (chassis == null || physical.Length > 32 || geometries.Values.Sum(g => g.Length) > 32) throw Bad("URDF needs chassis geometry fixed to the root, and at most 32 physical groups / meshes.");
        var ids = physical.Select((name, i) => (name, id: name == chassis ? "chassis" : "urdf_body_" + i)).ToDictionary(p => p.name, p => p.id);
        string Body(string link) => ids.TryGetValue(link, out var id) ? id : incoming.TryGetValue(link, out var j) ? Body(j.Parent) : "chassis";
        if (links.Count != physical.Length) result.Notes.Add("Geometry-free fixed coordinate links were folded into their parent bodies.");
        int triangles = 0;
        foreach (string name in physical)
        {
            var inertia = Single(links[name], "inertial");
            float mass = inertia == null ? 1 : Number(Required(Single(inertia, "mass", true), "value"));
            if (inertia == null) result.Notes.Add(name + ": no mass supplied; using 1 kg.");
            result.Definition.Bodies.Add(new RobotBodyDefinition { Id = ids[name], Name = name, MassKg = mass });
            int index = 0;
            foreach (var visual in geometries[name])
            {
                var geometry = Single(visual, "geometry", true);
                if (geometry.Elements().Count() != 1) throw Bad("Each URDF geometry needs exactly one shape.");
                var shape = geometry.Elements().Single(); Vector3[] points;
                if (shape.Name == "mesh")
                {
                    var meshPath = ResolveMesh(path, Required(shape, "filename", 1024));
                    points = StlReader.Read(meshPath);
                    var scale = Vector(shape.Attribute("scale")?.Value, Vector3.One);
                    if (scale.X <= 0 || scale.Y <= 0 || scale.Z <= 0) throw Bad("Mesh scale must be positive.");
                    for (int i = 0; i < points.Length; i++) points[i] *= scale;
                }
                else points = Primitive(shape);
                triangles += points.Length / 3;
                if (triangles > StlReader.MaxTriangles) throw Bad("URDF geometry exceeds 200,000 triangles.");
                var transform = poses[name] * Origin(Single(visual, "origin"));
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = transform * points[i];
                    if (!points[i].IsFinite() || points[i].Length() > 1000) throw Bad("Invalid or excessive transformed URDF geometry.");
                }
                var bytes = Binary(points); StlReader.Parse(bytes);
                result.Meshes.Add(new Geometry { Name = name + "_" + index++, BodyId = ids[name], Points = points, Bytes = bytes });
            }
        }
        foreach (var j in incoming.Values)
        {
            if (!ids.ContainsKey(j.Child) || j.Child == chassis) continue;
            var frame = poses[j.Parent] * j.Origin;
            result.Definition.Joints.Add(new RobotJointDefinition { Id = "urdf_joint_" + result.Definition.Joints.Count, Name = j.Name,
                ParentBody = Body(j.Parent), ChildBody = ids[j.Child], Type = j.Type == "continuous" ? "revolute" : j.Type,
                PivotSource = Array(frame.Origin), AxisSource = Array(frame.Basis * j.Axis), LimitsEnabled = j.Type != "continuous",
                Lower = j.Lower - j.Reference, Upper = j.Upper - j.Reference, ReferencePosition = j.Reference,
                MaxSpeed = j.Speed, MaxEffort = j.Effort, MotorEnabled = j.Type != "fixed" && j.Speed > 0 && j.Effort > 0 });
        }
        RobotMechanismValidation.ValidateStructure(result.Definition);
        // Validate bounds before any draft/profile mutation or asset writes.
        foreach (var id in ids.Values)
        {
            var points = result.Meshes.Where(m => m.BodyId == id).SelectMany(m => m.Points).ToArray();
            var bounds = new Aabb(points[0], Vector3.Zero); foreach (var p in points) bounds = bounds.Expand(p);
            if (bounds.Size.X < .001f || bounds.Size.Y < .001f || bounds.Size.Z < .001f) throw Bad("URDF bodies need at least 1 mm thickness on every axis.");
        }
        var all = result.Meshes.SelectMany(m => m.Points).ToArray(); var overall = new Aabb(all[0], Vector3.Zero);
        foreach (var point in all) overall = overall.Expand(point);
        if (overall.Size.X > 3 || overall.Size.Y > 3 || overall.Size.Z > 3) throw Bad("URDF robot exceeds 3 m on an axis. Check mesh scale.");
        result.Notes.Add("Joints and motion limits imported automatically. Link inertia/COM and collisions remain box approximations; materials and ROS control plugins are not applied.");
        result.Definition.ImportNotes = result.Notes.Distinct().ToList(); return result;
    }
    private static XElement Single(XElement parent, string name, bool required = false)
    {
        var elements = parent?.Elements(name).ToArray() ?? System.Array.Empty<XElement>();
        if (elements.Length > 1 || (required && elements.Length == 0)) throw Bad("Missing or duplicate URDF element: " + name);
        return elements.FirstOrDefault();
    }
    private static string Required(XElement element, string attribute, int maxLength = 100)
    {
        string value = element?.Attribute(attribute)?.Value;
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength) throw Bad("Missing or invalid URDF attribute: " + attribute); return value;
    }
    private static float Number(string value)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !float.IsFinite(number)) throw Bad("Invalid URDF number: " + value); return number;
    }
    private static Vector3 Vector(string value, Vector3 fallback)
    {
        if (value == null) return fallback;
        var parts = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) throw Bad("URDF vectors require three values."); return new(Number(parts[0]), Number(parts[1]), Number(parts[2]));
    }
    private static Transform3D Origin(XElement origin)
    {
        var xyz = Vector(origin?.Attribute("xyz")?.Value, Vector3.Zero); var rpy = Vector(origin?.Attribute("rpy")?.Value, Vector3.Zero);
        // URDF fixed-axis roll/pitch/yaw: Rz(yaw) * Ry(pitch) * Rx(roll).
        return new(new Basis(Vector3.Back, rpy.Z) * new Basis(Vector3.Up, rpy.Y) * new Basis(Vector3.Right, rpy.X), xyz);
    }
    private static float[] Array(Vector3 vector) => new[] { vector.X, vector.Y, vector.Z };
    private static InvalidDataException Bad(string message) => new(message);
    public static string ResolveMesh(string urdf, string filename)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(urdf)), root = folder, relative = filename;
        if (filename.Length > 1024 || filename.Contains('%') || filename.Contains('\\') || filename.Contains('#') || filename.Contains('?')) throw Bad("Invalid mesh path.");
        if (filename.StartsWith("package://", StringComparison.Ordinal))
        {
            var tail = filename[10..]; int slash = tail.IndexOf('/');
            if (slash <= 0) throw Bad("Invalid package mesh URI.");
            string package = tail[..slash]; relative = tail[(slash + 1)..];
            if (!RobotMechanismValidation.Id(package)) throw Bad("Invalid package name.");
            root = null;
            for (var directory = new DirectoryInfo(folder); directory != null && directory.Parent != null; directory = directory.Parent)
            {
                if (directory.Name == package) { root = directory.FullName; break; }
                // Do not search beyond a local export package into the rest of the computer.
                if (directory.FullName == Path.GetPathRoot(folder) || directory.FullName.Split(Path.DirectorySeparatorChar).Length < folder.Split(Path.DirectorySeparatorChar).Length - 2) break;
            }
            if (root == null && Directory.Exists(Path.Combine(folder, package))) root = Path.Combine(folder, package);
            if (root == null) throw Bad("Cannot locate package '" + package + "'. Keep the URDF inside its original exported package folder.");
            folder = root;
        }
        else
        {
            if (filename.Contains(':') || Path.IsPathRooted(filename)) throw Bad("Use local relative or package:// mesh paths; absolute/remote paths are not supported.");
            // Exporters commonly place URDF in a urdf/ subfolder and meshes beside it.
            if (new DirectoryInfo(folder).Name.Equals("urdf", StringComparison.OrdinalIgnoreCase)) root = Directory.GetParent(folder).FullName;
        }
        string resolved = Path.GetFullPath(Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar)));
        string boundary = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw Bad("Mesh path escapes the selected export package.");
        // Reject symlinks/junctions so a package path cannot redirect into unrelated files.
        for (var current = new FileInfo(resolved) as FileSystemInfo; current != null && current.FullName.StartsWith(boundary, StringComparison.OrdinalIgnoreCase);
            current = current is FileInfo f ? f.Directory : ((DirectoryInfo)current).Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw Bad("Mesh package must not contain symlinks or junctions.");
        if (!Path.GetExtension(resolved).Equals(".stl", StringComparison.OrdinalIgnoreCase)) throw Bad("URDF mesh format is not supported: " + Path.GetExtension(resolved) + ". Export STL meshes.");
        if (!System.IO.File.Exists(resolved)) throw Bad("Missing mesh: " + relative + ". Select the URDF with its accompanying meshes folder.");
        return resolved;
    }
    public static byte[] Binary(Vector3[] points)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(new byte[80]); writer.Write((uint)(points.Length / 3));
        for (int i = 0; i < points.Length; i += 3)
        {
            writer.Write(0f); writer.Write(0f); writer.Write(0f);
            for (int j = 0; j < 3; j++) { writer.Write(points[i+j].X); writer.Write(points[i+j].Y); writer.Write(points[i+j].Z); } writer.Write((ushort)0);
        }
        return stream.ToArray();
    }
    private static Vector3[] Primitive(XElement shape)
    {
        var points = new List<Vector3>();
        void Triangle(Vector3 a, Vector3 b, Vector3 c) { points.Add(a); points.Add(b); points.Add(c); }
        if (shape.Name == "box")
        {
            var half = Vector(Required(shape, "size"), Vector3.Zero) / 2;
            if (half.X <= 0 || half.Y <= 0 || half.Z <= 0) throw Bad("Invalid URDF box size.");
            var p = new[] { new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(1,1,-1), new Vector3(-1,1,-1),
                new Vector3(-1,-1,1), new Vector3(1,-1,1), new Vector3(1,1,1), new Vector3(-1,1,1) }.Select(v => v * half).ToArray();
            int[] faces = { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 1,2,6, 1,6,5, 2,3,7, 2,7,6, 3,0,4, 3,4,7 };
            for (int i = 0; i < faces.Length; i += 3) Triangle(p[faces[i]], p[faces[i+1]], p[faces[i+2]]);
        }
        else if (shape.Name == "cylinder")
        {
            float radius = Number(Required(shape, "radius")), half = Number(Required(shape, "length")) / 2;
            if (radius <= 0 || half <= 0) throw Bad("Invalid URDF cylinder dimensions.");
            for (int i = 0; i < 24; i++)
            {
                float a = Mathf.Tau * i / 24, b = Mathf.Tau * (i+1) / 24;
                var p = new Vector3(Mathf.Cos(a)*radius, Mathf.Sin(a)*radius, -half); var q = new Vector3(Mathf.Cos(b)*radius, Mathf.Sin(b)*radius, -half);
                var P = p + Vector3.Back * half*2; var Q = q + Vector3.Back * half*2;
                Triangle(p,q,Q); Triangle(p,Q,P); Triangle(new(0,0,-half),q,p); Triangle(new(0,0,half),P,Q);
            }
        }
        else if (shape.Name == "sphere")
        {
            float radius = Number(Required(shape, "radius")); if (radius <= 0) throw Bad("Invalid URDF sphere radius.");
            Vector3 Point(int row, int column)
            {
                float latitude = -Mathf.Pi/2 + Mathf.Pi*row/12, longitude = Mathf.Tau*column/24;
                return new(Mathf.Cos(latitude)*Mathf.Cos(longitude)*radius, Mathf.Cos(latitude)*Mathf.Sin(longitude)*radius, Mathf.Sin(latitude)*radius);
            }
            for (int row=0; row<12; row++) for (int column=0; column<24; column++)
            { if(row>0) Triangle(Point(row,column),Point(row,column+1),Point(row+1,column)); if(row<11) Triangle(Point(row,column+1),Point(row+1,column+1),Point(row+1,column)); }
        }
        else throw Bad("Unsupported URDF geometry: " + shape.Name);
        return points.ToArray();
    }
}
