using Godot;
using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public static class CadRobotChecks
{
    public static async Task Run(Simulation game)
    {
        try
        {
            int checks=0;
            void Check(bool condition,string text){if(!condition)throw new Exception(text);checks++;GD.Print("PASS: "+text);}
            async Task Frames(double seconds){for(int i=0;i<Engine.PhysicsTicksPerSecond*seconds;i++)await game.ToSignal(game.GetTree(),SceneTree.SignalName.PhysicsFrame);}
            var args=OS.GetCmdlineUserArgs();int index=Array.IndexOf(args,"--robot-import-test");
            if(index<0 || index+1>=args.Length)throw new ArgumentException("--robot-import-test requires an expanded URDF path.");
            var parsed=UrdfRobotReader.Read(args[index+1]);var d=parsed.Store();
            Check(d.Bodies.Count==27 && d.Joints.Count==26 && d.Parts.Count==27,"Actual Beebot package imports all 27 robot links and 26 joints");
            Check(d.Bodies.All(b=>b.InertiaSource!=null) && d.PhysicalDrive?.Wheels.Count==4,"All actual CAD mass properties and four drivetrain motors are present");
            var copy=JsonSerializer.Deserialize<ImportedRobotDefinition>(JsonSerializer.Serialize(d));RobotMechanismValidation.ValidateReady(copy);
            Check(copy.PhysicalDrive.Wheels.Count==4 && copy.Bodies[1].CenterOfMassSource.SequenceEqual(d.Bodies[1].CenterOfMassSource),"Actual robot geometry and dynamics survive saved profile serialization");
            game.Testing=true;game.Practice=true;game.AiProfile=new RobotProfile();Array.Fill(game.AiSlotProfiles,null);game.Profile=new RobotProfile{Name=parsed.Name,Imported=copy};game.Reset();game.Hud.ShowPause(false);
            foreach(var r in game.Robots)r.Bot=false;
            foreach(var ball in game.Balls.ToArray())game.RemoveBall(ball);
            await Frames(.5);
            var actor=game.Player;var rig=actor.Rig;
            game.RobotCentricDrive=false;
            var pose=actor.GlobalTransform;var cameraPose=game.Camera.GlobalTransform;int cameraMode=game.Camera.Mode;
            var inputs=new[]{new Vector2(0,-1),new Vector2(-1,0),new Vector2(0,1),new Vector2(1,0)};
            var expected=new[]{Vector3.Forward,Vector3.Left,Vector3.Back,Vector3.Right};
            bool fixedDirections=true;
            for(int headingCase=0;headingCase<4;headingCase++)for(int mode=0;mode<3;mode++)
            {
                actor.GlobalTransform=new Transform3D(new Basis(Vector3.Up,headingCase*Mathf.Pi/2),pose.Origin);
                game.Camera.Mode=mode;game.Camera.Rotation=new(.4f,headingCase*Mathf.Pi/2+.3f,0);
                for(int i=0;i<inputs.Length;i++) fixedDirections &= game.ManualDriveDirection(inputs[i],actor).IsEqualApprox(expected[i]);
            }
            Check(fixedDirections,"W/A/S/D keep fixed field directions at four robot headings and all three camera modes");
            Check(Mathf.IsEqualApprox(game.ManualDriveDirection(new(1,-1),actor).Length(),1)
                && Mathf.IsEqualApprox(game.ManualDriveDirection(new(.2f,-.3f),actor).Length(),new Vector2(.2f,-.3f).Length()),"Fixed controls cap diagonal speed and preserve analogue stick magnitude");
            game.RobotCentricDrive=true;actor.GlobalTransform=new Transform3D(new Basis(Vector3.Up,Mathf.Pi/2),pose.Origin);
            Check(game.ManualDriveDirection(new(0,-1),actor).IsEqualApprox(Vector3.Left),"Explicit robot-centric option still follows the robot heading");
            game.RobotCentricDrive=false;actor.GlobalTransform=pose;game.Camera.GlobalTransform=cameraPose;game.Camera.Mode=cameraMode;
            GD.Print($"BEEBOT rest position={actor.Position} up={actor.GlobalBasis.Y} root={rig.PhysicalChassis.GlobalPosition}");
            Check(rig.PhysicalChassis!=null && rig.Links.Count==26,"Actual imported Beebot instantiates a physical chassis and articulated links");
            var physicalBodies=rig.Links.Values.Append(rig.PhysicalChassis).Distinct().ToArray();
            Check(physicalBodies.Length==9 && Mathf.Abs(physicalBodies.Sum(b=>b.Mass)-d.Bodies.Sum(b=>b.MassKg))<.0001f,"Fixed links merge into nine physical bodies without losing robot mass");
            Check(rig.Links.Values.All(b=>b.GlobalPosition.IsFinite() && b.LinearVelocity.Length()<10) && actor.GlobalBasis.Y.Dot(Vector3.Up)>.9f && Mathf.Abs(actor.GlobalPosition.Y)<.08f,"Actual robot remains finite and supported under gravity");
            var stationary=actor.GlobalPosition;await Frames(5);
            GD.Print($"BEEBOT idle drift={actor.GlobalPosition-stationary}");
            GD.Print($"BEEBOT idle command={actor.Command} turn={actor.TurnCommand} velocity={actor.Velocity}; "+string.Join("; ",rig.DriveDiagnostics.Select(p=>p.Key+"="+p.Value)));
            Check(actor.GlobalPosition.DistanceTo(stationary)<.01f,"Actual robot remains stationary with zero motor command");
            var intakeJoints=d.Joints.Where(j=>j.IntakePower!=0).ToArray();
            Check(intakeJoints.Length==2 && copy.Joints.Count(j=>j.IntakePower!=0)==2,"Both intake motors import and persist their signed gameplay binding");
            float IntakeOmega(RobotJointDefinition j)
            {
                var axis=(actor.GlobalBasis*ImportedRobot.ConvertPoint(RobotMechanismValidation.Vec(j.AxisSource),d)).Normalized();
                return (rig.Links[j.ChildBody].AngularVelocity-rig.PhysicalChassis.AngularVelocity).Dot(axis);
            }
            var intakeButton=game.Hud.FindChild("IntakeToggle",true,false) as Button;
            Check(intakeButton!=null && !intakeButton.Disabled,"Actual Beebot exposes an enabled intake button in the driving HUD");
            intakeButton.EmitSignal(BaseButton.SignalName.Pressed);await Frames(.4);
            Check(actor.Intake && intakeButton.Text.Contains("ON"),"On-screen intake button activates the motor state and shows ON");
            GD.Print("BEEBOT intake speeds="+string.Join("; ",intakeJoints.Select(j=>$"{j.Name}: {IntakeOmega(j)}")));
            Check(intakeJoints.All(j=>IntakeOmega(j)*j.IntakePower>.5f),"Intake ON physically spins both CAD blades inward");
            intakeButton.EmitSignal(BaseButton.SignalName.Pressed);await Frames(.4);
            Check(intakeJoints.All(j=>Mathf.Abs(IntakeOmega(j))<.15f),"Intake OFF brakes both blade motors");
            actor.Inventory.Clear();for(int i=0;i<actor.Capacity;i++)actor.Inventory.Add(PieceKind.Pollen);
            var fullPiece=game.SpawnBall(PieceKind.Pollen,actor.GlobalPosition+actor.Front*(actor.Length/2+.10f)+Vector3.Up*.04f);
            actor.Intake=true;await Frames(.2);
            Check(actor.Inventory.Count==actor.Capacity && game.Balls.Contains(fullPiece) && intakeButton.Text.Contains("FULL"),"Full inventory visibly reports FULL and does not consume another field piece");
            actor.Intake=false;game.RemoveBall(fullPiece);
            actor.Inventory.Clear();
            var piece=game.SpawnBall(PieceKind.Pollen,actor.GlobalPosition+actor.Front*(actor.Length/2+.10f)+Vector3.Up*.04f);
            await Frames(.15);
            Check(actor.Inventory.Count==0 && game.Balls.Contains(piece),"Intake OFF leaves a reachable floor piece on the field");
            actor.Intake=true;await Frames(.2);
            Check(actor.Inventory.Count==1 && !game.Balls.Contains(piece),"Beebot intake transfers a real floor piece into inventory");
            await Frames(.2);
            Check(actor.Inventory.Count==1,"Collected Beebot piece is counted exactly once");
            actor.Intake=false;actor.Inventory.Clear();await Frames(.4);
            var start=actor.GlobalPosition;actor.Command=actor.Front*.4f;await Frames(.7);actor.Command=Vector3.Zero;
            var displacement=actor.GlobalPosition-start;GD.Print($"BEEBOT forward displacement={displacement} velocity={actor.Velocity}");
            GD.Print("BEEBOT drive: "+string.Join("; ",rig.DriveDiagnostics.Select(p=>p.Key+"="+p.Value)));
            Check(displacement.Dot(Vector3.Forward)>.02f,"Actual Beebot moves under its identified motor torque and estimated mass");
            await Frames(.4);start=actor.GlobalPosition;actor.Command=Vector3.Right*.3f;await Frames(.7);actor.Command=Vector3.Zero;
            displacement=actor.GlobalPosition-start;GD.Print($"BEEBOT strafe displacement={displacement}");
            Check(displacement.X>.02f,"Actual Beebot mecanum drivetrain strafes physically");
            await Frames(.3);float yaw=actor.GlobalBasis.GetEuler().Y;actor.TurnCommand=.25f;await Frames(.5);actor.TurnCommand=0;
            Check(Mathf.Wrap(actor.GlobalBasis.GetEuler().Y-yaw,-Mathf.Pi,Mathf.Pi)>.03f,"Actual Beebot turns through differential motor torque");
            var barrier=d.Joints.First(j=>j.Name=="dome_barrier_joint");float before=rig.JointPosition(barrier.Id);rig.SetMotorCommand(barrier.Id,1);await Frames(.4);rig.SetMotorCommand(barrier.Id,0);
            Check(rig.JointPosition(barrier.Id)>before+.05f && rig.JointPosition(barrier.Id)<barrier.Upper+.02f,"Actual Beebot barrier motor moves within its CAD limit");
            bool opens=rig.NextToggleOpens(barrier.Id);
            Check(barrier.ToggleEnabled && barrier.ToggleKey=="L" && rig.ToggleJoint(barrier.Id),"Actual imported barrier exposes its L open/close control");
            await Frames(2);
            GD.Print($"BEEBOT barrier position={rig.JointPosition(barrier.Id)} requested={(opens ? barrier.OpenPosition : barrier.ClosedPosition)}");
            Check(Mathf.Abs(rig.JointPosition(barrier.Id)-(opens ? barrier.OpenPosition : barrier.ClosedPosition))<.03f,"Actual barrier position control reaches the indicated target");
            rig.ToggleJoint(barrier.Id);await Frames(2);
            Check(Mathf.Abs(rig.JointPosition(barrier.Id)-(opens ? barrier.ClosedPosition : barrier.OpenPosition))<.03f,"Second barrier button press reaches the other open/closed target");
            var snapshot=JsonSerializer.Deserialize<Simulation.Snapshot>(JsonSerializer.Serialize(game.Capture()));
            var savedRoot=rig.PhysicalChassis.GlobalTransform;game.Restore(snapshot);
            Check(game.Player.Rig.PhysicalChassis.GlobalTransform.IsEqualApprox(savedRoot) && game.Player.Rig.Capture().Length==9,"Actual Beebot snapshot restores merged rigid groups and dynamic chassis pose");
            var result=new{passed=true,checks,links=d.Bodies.Count,joints=d.Joints.Count,physical_bodies=physicalBodies.Length,triangles=ImportedRobot.TriangleCount(d),mass_kg=d.Bodies.Sum(b=>b.MassKg),utc_time=DateTime.UtcNow.ToString("O"),notes=parsed.Notes};
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[index+1]),"runtime_validation.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"ALL {checks} ACTUAL ROBOT CHECKS PASSED");game.GetTree().Quit();
        }
        catch(Exception ex){GD.PushError("ACTUAL ROBOT FAILURE: "+ex);game.GetTree().Quit(1);}
    }
}
