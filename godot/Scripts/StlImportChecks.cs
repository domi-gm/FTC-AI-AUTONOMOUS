using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>Import regressions use content-addressed fixture assets; no personal profile is overwritten.</summary>
public static class StlImportChecks
{
    public static void Run(Simulation game)
    {
        try
        {
            void Check(bool condition, string text) { if (!condition) throw new Exception(text); GD.Print("PASS: " + text); }
            void Reject(byte[] bytes, string text)
            {
                bool failed = false; try { StlReader.Parse(bytes); } catch { failed = true; }
                Check(failed, text);
            }
            byte[] Binary(Vector3[] points)
            {
                using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                var header = new byte[80]; Encoding.ASCII.GetBytes("solid binary fixture").CopyTo(header, 0); writer.Write(header);
                writer.Write((uint)(points.Length / 3));
                for (int i = 0; i < points.Length; i += 3)
                {
                    writer.Write(0f); writer.Write(0f); writer.Write(0f);
                    for (int j = 0; j < 3; j++) { writer.Write(points[i+j].X); writer.Write(points[i+j].Y); writer.Write(points[i+j].Z); }
                    writer.Write((ushort)0);
                }
                return stream.ToArray();
            }
            var fixture = StlReader.Read(ProjectSettings.GlobalizePath("res://Assets/RobotSamples/fusion-box-mm.stl"));
            Check(fixture.Length == 36, "ASCII fixture has twelve triangles");
            var bytes = Binary(fixture);
            Check(StlReader.Parse(bytes).SequenceEqual(fixture), "Binary STL with solid header matches ASCII geometry");
            Reject(bytes[..^1], "Truncated binary STL rejected");
            Reject(Binary(new[] { Vector3.Zero, Vector3.Zero, Vector3.Zero }), "Fully degenerate STL rejected");
            Reject(Binary(new[] { new Vector3(float.NaN, 0, 0), Vector3.Right, Vector3.Up }), "Non-finite STL coordinates rejected");
            Reject(Encoding.ASCII.GetBytes("solid test\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nendsolid test"), "Incomplete ASCII facet rejected");
            var part = ImportedRobot.Store("fixture.stl", bytes, fixture);
            var definition = new ImportedRobotDefinition(); definition.Parts.Add(part);
            var bounds = ImportedRobot.Bounds(definition);
            Check(bounds.Size.DistanceTo(new(.4f, .25f, .3f)) < .00001f, "Millimetres and Fusion Z-up convert to metres / Godot Y-up");
            var shifted = fixture.Select(p => p + new Vector3(200, 0, 0)).ToArray();
            definition.Parts.Add(ImportedRobot.Store("offset-group.stl", Binary(shifted), shifted));
            Check(Mathf.Abs(ImportedRobot.Bounds(definition).Size.X - .6f) < .00001f, "Multiple STL groups retain a common Fusion origin");
            definition.QuarterTurns = 1; bounds = ImportedRobot.Bounds(definition);
            Check(Mathf.Abs(bounds.Size.X - .3f) < .00001f && Mathf.Abs(bounds.Size.Z - .6f) < .00001f, "Front rotation swaps footprint axes");
            var profile = new RobotProfile { Imported = definition }; profile.Validate();
            var copy = profile.Copy(); copy.Imported.Parts[0].Name = "changed";
            Check(profile.Imported.Parts[0].Name != "changed", "Editing an imported draft does not mutate applied profile");
            var restored = JsonSerializer.Deserialize<RobotProfile>(JsonSerializer.Serialize(profile)); restored.Validate();
            Check(restored.Imported.Parts.Count == 2 && Math.Abs(restored.LengthCm - 60) < .001, "JSON profile round trip retains groups, scale and dimensions");
            var invalid = definition.Copy(); invalid.Parts[0].Asset = "../escape.stl";
            bool blocked = false; try { invalid.Validate(); } catch { blocked = true; }
            Check(blocked, "Asset references cannot escape robot-assets");
            game.Profile = restored; game.Testing = true; game.Practice = true; game.Reset();
            Check(game.Player.GetNodeOrNull<Node3D>("ImportedRobot") != null && game.Player.CollisionSize.DistanceTo(bounds.Size) < .00001f,
                "Imported visual and dimensioned collision body spawn in the arena");
            var visual = game.Player.GetNode<Node3D>("ImportedRobot");
            Check(visual.GetChildCount() == 2 && ((MeshInstance3D)visual.GetChild(0)).Mesh.GetAabb().Position.Y >= -.00001f,
                "Separate visual groups are grounded with one shared normalization");
            typeof(SimulatorHud).GetMethod("ImportSetup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(game.Hud, null);
            Check(game.Hud.FindChildren("*", nameof(SubViewportContainer), true, false).Count == 1,
                "Import editor creates its own live 3D preview without replacing the arena");
            GD.Print("ALL STL IMPORT CHECKS PASSED"); game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("STL IMPORT FAILURE: " + ex); game.GetTree().Quit(1); }
    }
}
