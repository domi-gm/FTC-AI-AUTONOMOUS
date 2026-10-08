using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

public static class UrdfImportChecks
{
    public static async Task Run(Simulation game)
    {
        try
        {
            int count = 0;
            void Check(bool condition, string text) { if (!condition) throw new Exception(text); GD.Print("PASS: " + text); count++; }
            string sample = ProjectSettings.GlobalizePath("res://Assets/RobotSamples/urdf-demo/urdf/robot.urdf");
            var parsed = UrdfRobotReader.Read(sample); var d = parsed.Store();
            Check(d.SourceFormat == "urdf" && d.Bodies.Count == 4 && d.Joints.Count == 3 && d.Parts.Count == 4,
                "URDF creates bodies and existing joints without manual setup; empty fixed root is folded");
            Check(d.Units == "m" && d.UpAxis == "Z" && Mathf.Abs(ImportedRobot.Bounds(d).Size.X - .4f) < .0001f,
                "URDF mesh scale converts millimetre STL geometry to metres");
            var shoulder = d.Joints.First(j => j.Name == "shoulder"); var lift = d.Joints.First(j => j.Name == "lift");
            var expectedAxis = new Vector3(-Mathf.Sin(.4f), Mathf.Cos(.4f), 0);
            Check(RobotMechanismValidation.Vec(shoulder.PivotSource).DistanceTo(new(.2f, .15f, .165f)) < .00001f
                && RobotMechanismValidation.Vec(shoulder.AxisSource).DistanceTo(expectedAxis) < .00001f,
                "Joint origins and axes include parent transforms and URDF rpy rotation");
            Check(Mathf.Abs(shoulder.ReferencePosition - .2f) < .00001f && shoulder.Lower == 0 && Mathf.Abs(shoulder.Upper - .5f) < .00001f
                && shoulder.MaxEffort == 2 && shoulder.MaxSpeed == 1 && shoulder.MotorEnabled,
                "Nonzero URDF limits preserve absolute coordinates and choose a valid initial pose");
            Check(lift.Type == "prismatic" && lift.Upper == .12f && d.Joints.First(j => j.Name == "tool_mount").ParentBody == lift.ChildBody,
                "Slider travel and nested fixed tool connection are imported automatically");
            var armMesh = parsed.Meshes.First(m => m.BodyId == shoulder.ChildBody);
            var raw = StlReader.Read(ProjectSettings.GlobalizePath("res://Assets/RobotSamples/urdf-demo/meshes/arm.stl"));
            var frame = new Transform3D(new Basis(Vector3.Back, .4f) * new Basis(Vector3.Up, .2f), new(.2f,.15f,.165f));
            var expectedPoint = frame * (raw[0] * .001f - new Vector3(.2f,.15f,.165f));
            Check(armMesh.Points[0].DistanceTo(expectedPoint) < .00001f, "Visual origins, mesh scales and initial joint angle compose in the correct order");
            var bounds = ImportedRobot.Bounds(d);
            typeof(ImportedRobot).GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null).GetType().GetMethod("Clear").Invoke(
                typeof(ImportedRobot).GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), null);
            var restored = JsonSerializer.Deserialize<ImportedRobotDefinition>(JsonSerializer.Serialize(d)); RobotMechanismValidation.ValidateReady(restored);
            Check(ImportedRobot.Bounds(restored).IsEqualApprox(bounds) && restored.Joints[0].ReferencePosition == shoulder.ReferencePosition,
                "Baked geometry and automatic joints survive JSON reload without an in-memory mesh cache");
            string package = ProjectSettings.GlobalizePath("user://urdf-checks/urdf-demo");
            Directory.CreateDirectory(Path.Combine(package, "urdf")); Directory.CreateDirectory(Path.Combine(package, "meshes"));
            foreach (string mesh in new[] { "chassis", "arm", "slider", "tool" }) System.IO.File.Copy(
                ProjectSettings.GlobalizePath("res://Assets/RobotSamples/urdf-demo/meshes/" + mesh + ".stl"), Path.Combine(package,"meshes",mesh + ".stl"), true);
            string text = System.IO.File.ReadAllText(sample); string invalid = Path.Combine(package,"urdf","invalid.urdf");
            void Reject(string xml, string caption)
            {
                System.IO.File.WriteAllText(invalid, xml); bool rejected = false;
                try { UrdfRobotReader.Read(invalid); } catch { rejected = true; } Check(rejected, caption);
            }
            Reject(text.Replace("../meshes/slider.stl", "../../../escape.stl"), "Mesh traversal outside the selected package rejected");
            Reject(text.Replace("../meshes/slider.stl", "https://example.com/slider.stl"), "Remote mesh URLs rejected without network access");
            Reject(text.Replace("../meshes/slider.stl", "../meshes/missing.stl"), "Missing mesh reported instead of importing a partial robot");
            Reject(text.Replace("../meshes/slider.stl", "../meshes/slider.dae"), "Unsupported mesh format reported explicitly");
            Reject(text.Replace("type=\"prismatic\"", "type=\"floating\""), "Unsupported floating joint rejected instead of silently changing its type");
            Reject(text.Replace("<axis xyz=\"0 0 1\" />", "<axis xyz=\"0 0 0\" />"), "Zero URDF axis rejected");
            Reject(text.Replace("<parent link=\"base_link\" /><child link=\"arm\" />", "<parent link=\"tool\" /><child link=\"arm\" />")
                .Replace("<parent link=\"base_link\" /><child link=\"slider\" />", "<parent link=\"arm\" /><child link=\"slider\" />"), "URDF cycles rejected");
            Reject(text.Replace("<limit lower=\"0.2\"", "<mimic joint=\"lift\"/><limit lower=\"0.2\""), "Mimic coupling rejected with an explicit unsupported-feature error");
            Reject("<!DOCTYPE robot [<!ENTITY injected SYSTEM 'file:///sensitive'>]>" + text[(text.IndexOf("<robot"))..].Replace("URDF automatic joints demo", "&injected;"),
                "XML DTD / external entities rejected");
            Reject(text.Replace("0.001 0.001 0.001", "${scale} ${scale} ${scale}"), "Unexpanded xacro rejected");
            var continuousText = text.Replace("type=\"revolute\"", "type=\"continuous\""); System.IO.File.WriteAllText(invalid, continuousText);
            var continuous = UrdfRobotReader.Read(invalid).Store().Joints.First(j => j.Name == "shoulder");
            Check(!continuous.LimitsEnabled && continuous.Type == "revolute", "Continuous URDF joints become unlimited hinges");
            var primitives = "<robot name='primitives'><link name='base'><visual><geometry><box size='0.3 0.2 0.1'/></geometry></visual></link>"
                + "<link name='cylinder'><visual><geometry><cylinder radius='0.03' length='0.1'/></geometry></visual></link><joint name='mount' type='fixed'><parent link='base'/><child link='cylinder'/><origin xyz='0 0 0.1'/></joint>"
                + "<link name='sphere'><visual><geometry><sphere radius='0.03'/></geometry></visual></link><joint name='mount2' type='fixed'><parent link='cylinder'/><child link='sphere'/><origin xyz='0 0 0.1'/></joint></robot>";
            System.IO.File.WriteAllText(invalid, primitives); var primitive = UrdfRobotReader.Read(invalid).Store();
            Check(primitive.Parts.Count == 3 && ImportedRobot.TriangleCount(primitive) > 100, "URDF box, cylinder and sphere primitives import without external mesh files");
            var reordered = System.Xml.Linq.XDocument.Parse(primitives);
            var rootLink = reordered.Root.Elements("link").First(); rootLink.Remove(); reordered.Root.Add(rootLink);
            System.IO.File.WriteAllText(invalid, reordered.ToString());
            Check(UrdfRobotReader.Read(invalid).Store().Bodies.First(b => b.Id == "chassis").Name == "base",
                "URDF chassis selection follows the graph rather than XML link order");
            game.Testing = true; game.Practice = true; game.Profile = new RobotProfile { Imported = d }; game.Profile.Validate(); game.Reset(); game.Hud.ShowPause(false);
            foreach (var robot in game.Robots) robot.Bot = false;
            var rig = game.Player.Rig; foreach (var body in rig.Links.Values) body.GravityScale = 0;
            Check(rig != null && rig.Links.Count == 3 && rig.Connections.Count == 3, "Automatically imported URDF mechanism spawns in the simulator");
            rig.SetMotorCommand(lift.Id, 1);
            for (int i = 0; i < Engine.PhysicsTicksPerSecond * 2; i++) await game.ToSignal(game.GetTree(), SceneTree.SignalName.PhysicsFrame);
            var start = ImportedRobot.GetBodyBounds(d, lift.ChildBody).GetCenter();
            Check(Mathf.Abs(game.Player.ToLocal(rig.Links[lift.ChildBody].GlobalPosition).Y - start.Y - .12f) < .005f,
                "URDF imported slider physically reaches its original travel limit");
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(SimulatorHud).GetMethod("ImportSetup", flags).Invoke(game.Hud, null);
            var draft = (RobotProfile)typeof(SimulatorHud).GetField("_draft", flags).GetValue(game.Hud); var previous = draft.Imported;
            System.IO.File.WriteAllText(invalid, text.Replace("../meshes/slider.stl", "../meshes/missing.stl"));
            await (Task)typeof(SimulatorHud).GetMethod("ImportUrdf", flags).Invoke(game.Hud, new object[] { invalid });
            Check(draft.Imported == previous && game.Profile.Imported == d, "Failed asynchronous URDF import leaves draft and applied robot unchanged");
            await (Task)typeof(SimulatorHud).GetMethod("ImportUrdf", flags).Invoke(game.Hud, new object[] { sample });
            Check(draft.Imported != previous && draft.Imported.SourceFormat == "urdf" && draft.Imported.Joints.Count == 3 && game.Profile.Imported == d,
                "Successful asynchronous URDF import replaces only the editable draft");
            var buttons = game.Hud.FindChildren("*", nameof(Button), true, false).Cast<Button>().ToArray();
            Check(buttons.Any(b => b.Text == "IMPORT URDF + EXISTING JOINTS…") && buttons.Any(b => b.Text == "ADD STL SUBASSEMBLIES…" && b.Disabled),
                "Import UI separates automatic URDF from manual STL geometry");
            GD.Print($"ALL {count} URDF IMPORT CHECKS PASSED"); game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("URDF IMPORT FAILURE: " + ex); game.GetTree().Quit(1); }
    }
}
