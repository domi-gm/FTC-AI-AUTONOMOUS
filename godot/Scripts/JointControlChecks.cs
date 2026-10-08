using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

public static class JointControlChecks
{
    public static async Task Run(Simulation game)
    {
        try
        {
            int count = 0;
            void Check(bool condition, string caption) { if (!condition) throw new Exception(caption); count++; GD.Print("PASS: " + caption); }
            async Task Seconds(float seconds) { for (int i=0;i<Mathf.CeilToInt(seconds*Engine.PhysicsTicksPerSecond);i++) await game.ToSignal(game.GetTree(), SceneTree.SignalName.PhysicsFrame); }
            void Reject(Action action, string caption) { bool failed = false; try { action(); } catch { failed = true; } Check(failed, caption); }
            var d = RobotMechanismSample.Load(); var shoulder = d.Joints[0]; var lift = d.Joints[1];
            shoulder.AxisSource = new float[] { 0,0,1 }; shoulder.Lower = -.2f; shoulder.Upper = .6f;
            shoulder.ToggleEnabled = true; shoulder.ToggleKey = "L"; shoulder.ClosedPosition = -.1f; shoulder.OpenPosition = .45f;
            lift.ToggleEnabled = true; lift.ToggleKey = "M"; lift.ClosedPosition = .01f; lift.OpenPosition = .09f;
            RobotMechanismValidation.ValidateReady(d);
            var copy = JsonSerializer.Deserialize<ImportedRobotDefinition>(JsonSerializer.Serialize(d));
            Check(copy.Joints[0].ToggleKey == "L" && copy.Joints[0].OpenPosition == .45f, "Joint button/key and target positions survive profile JSON round trip");
            var invalid = d.Copy(); invalid.Joints[1].ToggleKey = "L";
            Reject(() => invalid.Validate(), "Duplicate joint control keys rejected");
            invalid = d.Copy(); invalid.Joints[0].ToggleKey = "W";
            Reject(() => invalid.Validate(), "Drive/gameplay keys cannot be assigned to joint buttons");
            invalid = d.Copy(); invalid.Joints[0].OpenPosition = 1;
            Reject(() => invalid.Validate(), "Target outside joint motion limits rejected");
            invalid = d.Copy(); invalid.Joints[0].MotorEnabled = false;
            Reject(() => invalid.Validate(), "Toggle control requires a working motor");
            game.Testing = true; game.Practice = true; game.Profile = new RobotProfile { Imported = d }; game.Reset(); game.Hud.ShowPause(false);
            foreach (var r in game.Robots) r.Bot = false;
            var rig = game.Player.Rig; foreach (var body in rig.Links.Values) body.GravityScale = 0;
            game.Player.Position = new(.8f,0,-.9f); rig.Restore(null); await Seconds(.05f);
            game.Player.TurnCommand = 1; await Seconds(.2f);
            Check(game.Player.Rotation.Y > .4f, "Imported chassis can turn on open floor without treating floor contact as a wall");
            game.Player.TurnCommand = -1; await Seconds(.2f);
            Check(Mathf.Abs(game.Player.Rotation.Y) < .02f, "Imported chassis can turn back in the opposite direction");
            game.Player.TurnCommand = 0;
            var pushed=game.SpawnBall(PieceKind.Pollen,game.Player.Position+new Vector3(game.Player.CollisionSize.X/2+.10f,.03556f,0));
            float ballX=pushed.Position.X; game.Player.Intake=false; game.Player.Command=Vector3.Right; await Seconds(.5f);
            Check(pushed.Position.X>ballX+.1f,"Imported CAD chassis pushes a loose ball with intake off");
            game.Player.Command=Vector3.Zero; game.Player.Velocity=Vector3.Zero; game.RemoveBall(pushed);
            game.Player.Position=new(.8f,0,-.9f); rig.Restore(null);
            var origin = new Vector3(.8f,5,-.9f); var target = origin + Vector3.Right;
            var ownLink = new RigidBody3D { Freeze = true, CollisionLayer = 4, Position = origin };
            ownLink.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * .2f } }); game.AddChild(ownLink); rig.Links.Add("test-launch-link", ownLink); await Seconds(.01f);
            Check(ShotPlanner.TrySolve(game.Player,origin,target,.03556f,out _), "Own articulated link does not falsely block the gameplay launcher");
            var obstruction = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0, Position = origin };
            obstruction.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * .2f } }); game.AddChild(obstruction); await Seconds(.01f);
            Check(!ShotPlanner.TrySolve(game.Player,origin,target,.03556f,out _), "A real external obstacle still blocks the launch");
            obstruction.QueueFree(); rig.Links.Remove("test-launch-link"); ownLink.QueueFree(); rig.Restore(null); await Seconds(.05f);
            InputEventKey KeyEvent(Key key, bool echo = false) => new() { PhysicalKeycode = key, Pressed = true, Echo = echo };
            Check(rig.HandleToggleKey(KeyEvent(Key.L)), "A single L press commands the open position");
            Check(!rig.HandleToggleKey(KeyEvent(Key.L, true)), "Keyboard auto-repeat does not flip the target repeatedly");
            await Seconds(2);
            Check(Mathf.Abs(rig.JointPosition(shoulder.Id) - .45f) < .006f, "Hinge motor reaches configured open angle rather than running indefinitely");
            game._UnhandledInput(KeyEvent(Key.L)); await Seconds(2);
            Check(Mathf.Abs(rig.JointPosition(shoulder.Id) + .1f) < .006f, "Pressing L again closes the arm through the real input handler");
            rig.HandleToggleKey(KeyEvent(Key.L)); await Seconds(.12f); rig.HandleToggleKey(KeyEvent(Key.L)); await Seconds(1);
            Check(Mathf.Abs(rig.JointPosition(shoulder.Id) + .1f) < .006f, "Second press while moving reverses the arm back to its closed target");
            rig.HandleToggleKey(KeyEvent(Key.M)); await Seconds(2);
            Check(Mathf.Abs(rig.JointPosition(lift.Id) - .09f) < .001f, "A separately bound slider reaches its own open position");
            Check(Mathf.Abs(rig.JointPosition(shoulder.Id) + .1f) < .006f, "Independent joint commands do not disturb the other target");
            game.Paused = true; game.SetFrozen(true); var paused = rig.Capture();
            Check(!rig.HandleToggleKey(KeyEvent(Key.L)) && !rig.ToggleJoint(lift.Id), "Paused keyboard and button presses cannot change joint targets");
            await Seconds(.03f);
            Check(rig.Capture().First(s=>s.BodyId==shoulder.ChildBody).TargetPosition == paused.First(s=>s.BodyId==shoulder.ChildBody).TargetPosition,
                "Pause preserves the commanded target");
            var snapshot = JsonSerializer.Deserialize<Simulation.Snapshot>(JsonSerializer.Serialize(game.Capture()));
            game.Restore(snapshot); game.Hud.ShowPause(false); rig = game.Player.Rig;
            Check(rig.Capture().First(s=>s.BodyId==lift.ChildBody).TargetPosition == .09f, "Practice snapshot restores the desired joint target along with its body pose");
            foreach (var body in rig.Links.Values) body.GravityScale = 0;
            Check(rig.HandleToggleKey(KeyEvent(Key.M)), "Restored slider remembers that the next key press should close it"); await Seconds(2);
            Check(Mathf.Abs(rig.JointPosition(lift.Id) - .01f) < .001f, "Restored slider closes to its configured target");
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(SimulatorHud).GetMethod("UpdateJointControls", flags).Invoke(game.Hud, null);
            var buttons = game.Hud.FindChildren("*", nameof(Button), true, false).Cast<Button>().ToArray();
            var armButton = buttons.First(b=>b.Text.StartsWith("[L] Shoulder:"));
            Check(armButton.IsVisibleInTree() && armButton.Text.EndsWith("OPEN"), "Gameplay HUD displays an on-screen button with the bound key and next action");
            armButton.EmitSignal(BaseButton.SignalName.Pressed); await Seconds(2);
            Check(Mathf.Abs(rig.JointPosition(shoulder.Id) - .45f) < .006f, "Clicking the HUD button performs the same movement as the keyboard key");
            var malformed = JsonSerializer.Deserialize<Simulation.Snapshot>(JsonSerializer.Serialize(game.Capture()));
            malformed.Robots[0].Mechanisms.First(s=>s.BodyId==shoulder.ChildBody).TargetPosition = 99;
            var actor = game.Player; Reject(()=>game.Restore(malformed), "Invalid saved joint target rejected before scene replacement");
            Check(game.Player == actor, "Invalid target snapshot leaves the active scene intact");
            rig.SetMotorCommand(shoulder.Id, -1); await Seconds(.1f);
            Check(rig.Capture().First(s=>s.BodyId==shoulder.ChildBody).TargetPosition == null, "Manual velocity control cancels the position target");
            typeof(SimulatorHud).GetMethod("ImportSetup", flags).Invoke(game.Hud, null);
            typeof(SimulatorHud).GetMethod("MechanismSetup", flags).Invoke(game.Hud, null);
            Check(game.Hud.FindChildren("*", nameof(SpinBox), true, false).Count >= 12, "Mechanism editor exposes open/closed position and key configuration");
            var urdf = UrdfRobotReader.Read(ProjectSettings.GlobalizePath("res://Assets/RobotSamples/urdf-demo/urdf/robot.urdf")).Store();
            var joint = urdf.Joints.First(j=>j.Name=="shoulder"); joint.ToggleEnabled = true; joint.ToggleKey = "L"; joint.ClosedPosition = .25f; joint.OpenPosition = .55f;
            RobotMechanismValidation.ValidateReady(urdf); game.Profile = new RobotProfile { Imported = urdf }; game.Reset(); game.Hud.ShowPause(false); rig = game.Player.Rig;
            foreach(var robot in game.Robots) robot.Bot=false; foreach (var body in rig.Links.Values) body.GravityScale = 0;
            rig.HandleToggleKey(KeyEvent(Key.L)); await Seconds(2);
            Check(Mathf.Abs(rig.JointPosition(joint.Id) - .55f) < .006f, "Toggle target uses absolute URDF angles including its nonzero imported reference position");
            foreach (var body in rig.Links.Values) body.GravityScale = 1;
            await Seconds(.5f);
            Check(Mathf.Abs(rig.JointPosition(joint.Id) - .55f) < .01f, "Position motor holds the arm against gravity within its configured effort");
            GD.Print($"ALL {count} JOINT CONTROL CHECKS PASSED"); game.GetTree().Quit();
        }
        catch(Exception ex) { GD.PushError("JOINT CONTROL FAILURE: " + ex); game.GetTree().Quit(1); }
    }
}
