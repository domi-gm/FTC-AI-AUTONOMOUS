using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public static class ShotPlanningChecks
{
    public static async Task Run(Simulation game)
    {
        try
        {
            void Check(bool valid,string text) { if(!valid) throw new Exception(text); GD.Print("PASS: "+text); }
            async Task Seconds(double time)
            { for(int i=0;i<Math.Ceiling(time*Engine.PhysicsTicksPerSecond);i++) await game.ToSignal(game.GetTree(),SceneTree.SignalName.PhysicsFrame); }
            void Reset(int turrets=1)
            {
                game.Profile=new RobotProfile {Turrets=turrets}; game.Practice=true; game.Reset();
                foreach(var r in game.Robots) r.Bot=false;
            }
            game.Testing=true; Reset();
            await Seconds(.1);
            var wall=new StaticBody3D { Position=new(Arena.Center,2.5f,-.85f) };
            game.AddChild(wall); wall.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=new(Arena.Size+2,6,.1f) } });
            await Seconds(.06);
            int count=game.Player.Inventory.Count;
            game.Player.Fire();
            Check(game.Player.ShotStatus.Contains("BLOCKED") && game.Player.Inventory.Count==count,
                "A real wall blocks the shot without consuming inventory");
            Check(game.Player.LastShotDiagnosis.Reason==ShotPlanner.Failure.PathBlocked,
                "Blocked launch distinguishes a path obstacle from empty inventory");
            wall.QueueFree(); await Seconds(.2);
            Check(game.Aim.PollenClear && game.Player.ShotStatus=="READY" && game.Player.LastShotAttempt.Contains("BLOCKED"),
                "Live readiness clears a previous failure while retaining last-attempt history");
            game.Player.Inventory.Clear(); await Seconds(.01);
            Check(game.Player.ShotStatus=="NO PIECE IN ROBOT" && game.Aim.Status=="NO PIECE IN ROBOT",
                "Empty robot inventory is distinct from balls elsewhere on the field");
            Reset(); game.Player.Position=new(1.6f,0,-2.5f); await Seconds(.06);
            bool clear=ShotPlanner.TrySolve(game.Player,game.Player.LaunchOrigin,game.Target(game.Player),.03556f,out var plan,out var issue);
            GD.Print($"REFINED real HIVE: {plan.FlightTime:0.000} s, {plan.Velocity.Length():0.000} m/s");
            Check(clear && issue.Reason==ShotPlanner.Failure.None && Math.Abs(plan.FlightTime/.025f-Mathf.Round(plan.FlightTime/.025f))>.01f,
                "A real HIVE approach finds a clear flight time missed by the old 25 ms grid");
            count=game.Player.Inventory.Count; game.Player.Fire();
            Check(game.Player.Inventory.Count==count-1 && game.Player.LastShotAttempt=="LAUNCHED",
                "The formerly blocked position launches a physical ball");
            await Seconds(2);
            Check(game.Player.ShotsMade==1 && game.RedHive.Contents.Sum(b=>b.Count)==4,
                "The refined physical trajectory lands in the intended HIVE");
            Reset(); await Seconds(.06);
            var obstruction=new StaticBody3D { Name="MuzzleBlock" }; game.AddChild(obstruction);
            obstruction.Position=game.Player.LaunchOrigin;
            obstruction.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=Vector3.One*.08f}});
            await Seconds(.01);
            Check(!ShotPlanner.TrySolve(game.Player,game.Player.LaunchOrigin,game.Target(game.Player),.03556f,out _,out issue)
                && issue.Reason==ShotPlanner.Failure.OriginBlocked,
                "An obstructed launch point is diagnosed separately");
            obstruction.Position=game.Target(game.Player); await Seconds(.01);
            Check(!ShotPlanner.TrySolve(game.Player,game.Player.LaunchOrigin,game.Target(game.Player),.03556f,out _,out issue)
                && issue.Reason==ShotPlanner.Failure.TargetBlocked,
                "An obstructed target is diagnosed separately");
            obstruction.QueueFree(); await Seconds(.01);
            Check(!ShotPlanner.TrySolve(game.Player,game.Player.LaunchOrigin,new(100,100,100),.03556f,out _,out issue)
                && issue.Reason==ShotPlanner.Failure.SpeedLimit,
                "An unreachable target reports the launch-speed limit");
            Reset(3); await Seconds(.06);
            var muzzle=new StaticBody3D { Position=game.Player.GetNode<Node3D>("Turret0").GlobalPosition+Vector3.Up*.06f };
            game.AddChild(muzzle); muzzle.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=Vector3.One*.05f}});
            await Seconds(.002);
            int updates=game.Aim.PlanUpdates;
            game.Player.Inventory.RemoveAt(0); await Seconds(.002);
            Check(game.Aim.PlanUpdates>updates && !game.Aim.PollenClear && game.Aim.PollenDiagnosis.Reason==ShotPlanner.Failure.OriginBlocked,
                "Same-kind inventory changes refresh the selected launcher immediately");
            GD.Print("ALL SHOT CHECKS PASSED"); game.GetTree().Quit();
        }
        catch(Exception ex) { GD.PushError("SHOT FAILURE: "+ex); game.GetTree().Quit(1); }
    }
}
