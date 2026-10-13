using Godot;
using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public static class PhysicalDriveChecks
{
    public static async Task Run(Simulation game)
    {
        try
        {
            int checks=0;
            void Check(bool value,string text){if(!value)throw new Exception(text);GD.Print("PASS: "+text);checks++;}
            async Task Frames(double seconds){for(int i=0;i<Engine.PhysicsTicksPerSecond*seconds;i++)await game.ToSignal(game.GetTree(),SceneTree.SignalName.PhysicsFrame);}
            var xml=new StringBuilder("<robot name='physical mecanum fixture'><link name='base'><inertial><origin xyz='0 0 .13'/><mass value='10'/><inertia ixx='.1' iyy='.1' izz='.1' ixy='0' ixz='0' iyz='0'/></inertial><visual><origin xyz='0 0 .13'/><geometry><box size='.32 .32 .05'/></geometry></visual></link>");
            string[] names={"front_left","front_right","rear_left","rear_right"};
            foreach(var name in names)
            {
                string x=name.StartsWith("front") ? ".14" : "-.14",y=name.EndsWith("left") ? ".17" : "-.17";
                xml.Append($"<link name='{name}'><inertial><mass value='.3'/><inertia ixx='.0003' iyy='.00045' izz='.0003' ixy='0' ixz='0' iyz='0'/></inertial><visual><origin rpy='1.57079632679 0 0'/><geometry><cylinder radius='.055' length='.03'/></geometry></visual></link><joint name='{name}' type='continuous'><parent link='base'/><child link='{name}'/><origin xyz='{x} {y} .055'/><axis xyz='0 1 0'/><limit effort='1.55' velocity='49.5'/><dynamics damping='.001' friction='.001'/></joint>");
            }
            xml.Append("<ftc_simulator drivetrain='mecanum' wheel_radius='.055' free_speed='49.5' stall_torque='1.55' front_quarter_turns='1'>");
            foreach(var name in names)xml.Append($"<wheel joint='{name}' roller_sign='{(name=="front_left" || name=="rear_right" ? 1 : -1)}' turn_sign='{(name.EndsWith("left") ? -1 : 1)}'/>");
            xml.Append("</ftc_simulator></robot>");
            string path=ProjectSettings.GlobalizePath("user://physical-drive-fixture.urdf");File.WriteAllText(path,xml.ToString());
            var d=UrdfRobotReader.Read(path).Store();
            Check(d.PhysicalDrive?.Wheels.Count==4,"Physical drivetrain extension survives URDF parsing");
            game.Testing=true;game.Practice=true;game.AiProfile=new RobotProfile();game.Profile=new RobotProfile{Imported=d};game.Reset();game.Hud.ShowPause(false);
            foreach(var r in game.Robots)r.Bot=false;
            foreach(var ball in game.Balls.ToArray())game.RemoveBall(ball);
            await Frames(.5);
            var actor=game.Player;var rig=actor.Rig;
            Check(rig.PhysicalChassis!=null && actor.CollisionLayer==0,"Imported chassis is a dynamic body with no kinematic collision duplicate");
            Check(actor.GlobalPosition.IsFinite() && Mathf.Abs(actor.GlobalPosition.Y)<.02f && actor.GlobalBasis.Y.Dot(Vector3.Up)>.99f,"Gravity and wheel contacts support the chassis without tipping or falling through floor");
            var start=actor.GlobalPosition;actor.Command=actor.Front*.2f;await Frames(.7);actor.Command=Vector3.Zero;
            var displacement=actor.GlobalPosition-start;
            GD.Print($"DRIVE forward displacement={displacement} speed={actor.Velocity}");
            Check(displacement.Dot(Vector3.Forward)>.08f && Mathf.Abs(displacement.X)<.03f,"Motor torque drives forward through mecanum contact forces");
            await Frames(.5);start=actor.GlobalPosition;actor.Command=Vector3.Right*.2f;await Frames(.7);actor.Command=Vector3.Zero;
            displacement=actor.GlobalPosition-start;GD.Print($"DRIVE strafe displacement={displacement}");
            Check(displacement.X>.06f && Mathf.Abs(displacement.Z)<.05f,"Roller directions produce physical lateral travel");
            await Frames(.5);float yaw=actor.GlobalBasis.GetEuler().Y;actor.TurnCommand=.2f;await Frames(.5);actor.TurnCommand=0;
            Check(Mathf.Wrap(actor.GlobalBasis.GetEuler().Y-yaw,-Mathf.Pi,Mathf.Pi)>.05f,"Opposite wheel commands produce physical positive yaw");
            var snapshot=JsonSerializer.Deserialize<Simulation.Snapshot>(JsonSerializer.Serialize(game.Capture()));
            Check(snapshot.Robots[0].Mechanisms.Any(b=>b.BodyId=="chassis"),"Snapshot includes dynamic chassis orientation and velocity");
            var rootPose=rig.PhysicalChassis.GlobalTransform;rig.SetFrozen(true);game.Paused=true;await Frames(.2);
            Check(rig.PhysicalChassis.GlobalTransform.IsEqualApprox(rootPose),"Pause freezes the physical chassis and all wheel links");
            game.Paused=false;rig.SetFrozen(false);game.Restore(snapshot);await Frames(.002);
            Check(game.Player.Rig.PhysicalChassis.GlobalPosition.DistanceTo(rootPose.Origin)<.03f,"Scene restore preserves dynamic chassis pose");
            GD.Print($"ALL {checks} PHYSICAL DRIVE CHECKS PASSED");game.GetTree().Quit();
        }
        catch(Exception ex){GD.PushError("PHYSICAL DRIVE FAILURE: "+ex);game.GetTree().Quit(1);}
    }
}
