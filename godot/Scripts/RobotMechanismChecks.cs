using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public static class RobotMechanismChecks
{
    public static async Task Run(Simulation game)
    {
        try
        {
            int count = 0;
            void Check(bool condition, string text) { if (!condition) throw new Exception(text); count++; GD.Print("PASS: " + text); }
            void Reject(Action action, string text) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, text); }
            async Task Frames(int frames) { for (int i = 0; i < frames; i++) await game.ToSignal(game.GetTree(), SceneTree.SignalName.PhysicsFrame); }
            var definition = RobotMechanismSample.Load();
            definition.Joints[0].AxisSource = new float[] { 0, 0, 1 }; definition.Joints[0].Lower = -.2f; definition.Joints[0].Upper = .6f;
            var profile = new RobotProfile { Imported = definition }; profile.Validate();
            var copy = profile.Copy(); copy.Imported.Joints[0].PivotSource[0] = 999; copy.Imported.Bodies[1].MassKg = 9;
            Check(definition.Joints[0].PivotSource[0] == 200 && definition.Bodies[1].MassKg == .1f, "Joint arrays and body settings are copied independently");
            var invalid = definition.Copy(); invalid.Joints[0].ParentBody = "tool"; invalid.Joints[1].ParentBody = "arm";
            Reject(() => invalid.Validate(), "Cyclic articulation graph rejected");
            invalid = definition.Copy(); invalid.Joints.Add(invalid.Joints[0].Copy());
            Reject(() => invalid.Validate(), "Duplicate joint / incoming child connection rejected");
            invalid = definition.Copy(); invalid.Joints[0].AxisSource = new float[3];
            Reject(() => invalid.Validate(), "Zero joint axis rejected");
            invalid = definition.Copy(); invalid.Joints.RemoveAt(0);
            Reject(() => RobotMechanismValidation.ValidateReady(invalid), "Unconnected moving group cannot be applied");
            invalid = definition.Copy(); invalid.Parts[1].BodyId = "chassis";
            Reject(() => RobotMechanismValidation.ValidateReady(invalid), "Empty rigid group cannot be applied");
            var poses = RobotMechanismPose.Evaluate(definition, "lift", .08f);
            Check(poses["slider"].Origin.DistanceTo(Vector3.Up * .08f) < .00001f && poses["tool"].Origin.DistanceTo(poses["slider"].Origin) < .00001f,
                "Slider preview converts the Fusion axis and carries fixed descendants");
            var pivot = ImportedRobot.ConvertPoint(new Vector3(200, 150, 165), definition) + ImportedRobot.NormalizationOffset(definition);
            poses = RobotMechanismPose.Evaluate(definition, "shoulder", .3f);
            Check((poses["arm"] * pivot).DistanceTo(pivot) < .00001f && poses["arm"].Basis.GetRotationQuaternion().GetAngle() > .29f,
                "Hinge preview rotates around its shared CAD pivot");
            game.Profile = JsonSerializer.Deserialize<RobotProfile>(JsonSerializer.Serialize(profile));
            game.Testing = true; game.Practice = true; game.Reset(); game.Hud.ShowPause(false);
            foreach (var robot in game.Robots) robot.Bot = false;
            var rig = game.Player.Rig;
            Check(rig != null && rig.Links.Count == 3 && rig.Connections.Count == 3, "Dynamic hinge, slider and fixed tool spawn as real Godot physics bodies");
            Check(game.Player.CollisionSize.DistanceTo(new(.4f, .1f, .3f)) < .00001f && Math.Abs(rig.Links["arm"].Mass - .1f) < .00001f,
                "Chassis collider excludes moving links; link mass is applied");
            foreach (var body in rig.Links.Values) body.GravityScale = 0; // Isolate signed motor / limit behavior from gravity.
            await Frames(5);
            float Angle()
            {
                var x = (game.Player.GlobalBasis.Inverse() * rig.Links["arm"].GlobalBasis).X;
                return Mathf.Atan2(Vector3.Up.Dot(Vector3.Right.Cross(x)), Vector3.Right.Dot(x));
            }
            float Lift() => game.Player.ToLocal(rig.Links["slider"].GlobalPosition).Y - ImportedRobot.GetBodyBounds(definition, "slider").GetCenter().Y;
            rig.SetMotorCommand("shoulder", 1); rig.SetMotorCommand("lift", 1);
            await Frames(Engine.PhysicsTicksPerSecond * 2);
            GD.Print($"MEASURE: shoulder {Angle():F4} rad, slider {Lift():F4} m");
            Check(Angle() > .5f && Angle() < .63f, "Positive hinge motor reaches upper motion limit");
            Check(Lift() > .11f && Lift() < .13f, "Positive slider motor reaches upper travel limit");
            var relative = rig.Links["slider"].GlobalTransform.AffineInverse() * rig.Links["tool"].GlobalTransform;
            Check(relative.Origin.DistanceTo(Vector3.Up * .06f) < .005f && relative.Basis.GetRotationQuaternion().GetAngle() < .01f,
                "Fixed joint preserves the tool attachment during lift motion");
            game.Paused = true; game.SetFrozen(true); var frozen = rig.Links["arm"].GlobalTransform;
            await Frames(30);
            Check(rig.Links.Values.All(b => b.Freeze) && rig.Links["arm"].GlobalTransform.IsEqualApprox(frozen), "Pause freezes articulated physics bodies");
            var snapshot = JsonSerializer.Deserialize<Simulation.Snapshot>(JsonSerializer.Serialize(game.Capture()));
            var saved = snapshot.Robots[0].Mechanisms.First(s => s.BodyId == "slider");
            game.Restore(snapshot); game.Hud.ShowPause(false); rig = game.Player.Rig;
            Check(game.Player.ToLocal(rig.Links["slider"].GlobalPosition).DistanceTo(RobotMechanismValidation.Vec(saved.Position)) < .00001f,
                "Snapshot restores mechanism body poses after rebuilding the scene");
            var corrupted = JsonSerializer.Deserialize<Simulation.Snapshot>(JsonSerializer.Serialize(snapshot)); corrupted.Robots[0].Mechanisms[0].Rotation[0] = float.NaN;
            var original = game.Player;
            Reject(() => game.Restore(corrupted), "Invalid mechanism snapshot rejected before resetting the scene");
            Check(game.Player == original, "Rejected snapshot leaves the running scene intact");
            foreach (var body in rig.Links.Values) body.GravityScale = 0;
            rig.SetMotorCommand("shoulder", -1); rig.SetMotorCommand("lift", -1);
            await Frames(Engine.PhysicsTicksPerSecond * 2);
            Check(Angle() < -.15f && Angle() > -.23f && Lift() < .01f && Lift() > -.01f, "Reversed motors reach both lower motion limits");
            rig.SetMotorCommand("shoulder", 0); rig.SetMotorCommand("lift", 0);
            var beforeDrive = game.Player.Position; game.Player.Command = Vector3.Right; game.Player.TurnCommand = .2f;
            await Frames(Engine.PhysicsTicksPerSecond / 2);
            game.Player.Command = Vector3.Zero; game.Player.TurnCommand = 0;
            Check(game.Player.Position.X > beforeDrive.X + .05f && rig.Links.Values.All(b => b.GlobalPosition.IsFinite() && game.Player.ToLocal(b.GlobalPosition).Length() < 1),
                "Articulated links remain attached while the chassis drives and turns");
            var shoulder = rig.Connections["shoulder"].GlobalPosition;
            var armPivot = rig.Links["arm"].GlobalTransform * (pivot - ImportedRobot.GetBodyBounds(definition, "arm").GetCenter());
            Check(shoulder.DistanceTo(armPivot) < .01f, "Hinge anchor follows the moving kinematic chassis");
            game.Player.Velocity = Vector3.Zero;
            foreach (var body in rig.Links.Values) body.GravityScale = 1;
            await Frames(Engine.PhysicsTicksPerSecond / 2);
            Check(Lift() > -.01f && rig.Links.Values.All(b => b.LinearVelocity.IsFinite() && b.AngularVelocity.IsFinite()),
                "Configured motors support dynamic links against gravity without invalid velocities");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(SimulatorHud).GetMethod("ImportSetup", flags).Invoke(game.Hud, null);
            typeof(SimulatorHud).GetMethod("MechanismSetup", flags).Invoke(game.Hud, null);
            Check(game.Hud.FindChildren("*", nameof(SpinBox), true, false).Count >= 6 && game.Hud.FindChildren("*", nameof(HSlider), true, false).Count > 0,
                "Mechanism editor exposes joint fields and a live motion preview");
            GD.Print($"ALL {count} ROBOT MECHANISM CHECKS PASSED"); game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("ROBOT MECHANISM FAILURE: " + ex); game.GetTree().Quit(1); }
    }
}
