using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Scenarii de regresie care nu scriu salvările personale ale utilizatorului.</summary>
public static class SimulatorRegressionChecks
{
    /// <summary>Măsoară corpuri reale în Jolt; formulele nu înlocuiesc simularea.</summary>
    public static async Task RunGravity(Simulation game)
    {
        try
        {
            void Check(bool valid,string text) { if (!valid) throw new Exception(text); GD.Print("PASS: "+text); }
            async Task Frames(int n) { for (int i=0;i<n;i++) await game.ToSignal(game.GetTree(),SceneTree.SignalName.PhysicsFrame); }
            game.Testing=true; game.Practice=true; game.Reset(); foreach (var r in game.Robots) r.Bot=false;
            float gravity=(float)ProjectSettings.GetSetting("physics/3d/default_gravity");
            Vector3 direction=(Vector3)ProjectSettings.GetSetting("physics/3d/default_gravity_vector");
            Check(Mathf.Abs(gravity-9.80665f)/9.80665f<.001f && direction.DistanceTo(Vector3.Down)<.00001f,
                "Project gravity is within 0.1% of standard Earth gravity and points down");
            Check(Mathf.Abs(gravity-ShotPlanner.Gravity)<.00001f,"Planner and physics use the same gravity");
            // Pornim sus, deasupra obstacolelor, păstrând proprietățile normale ale mingilor.
            var pollen=game.SpawnBall(PieceKind.Pollen,new(.6f,10,-.8f));
            var nectar=game.SpawnBall(PieceKind.RedNectar,new(.9f,10,-.8f));
            await Frames(3);
            Check(pollen.GravityScale==1 && nectar.GravityScale==1 && pollen.LinearDamp==0 && nectar.LinearDamp==0
                && pollen.LinearDampMode==RigidBody3D.DampMode.Replace && nectar.LinearDampMode==RigidBody3D.DampMode.Replace,
                "Both piece types have unit gravity scale and no linear air damping");
            var start=pollen.Position; var velocity=pollen.LinearVelocity;
            var nectarStart=nectar.Position; var nectarVelocity=nectar.LinearVelocity;
            ulong firstFrame=Engine.GetPhysicsFrames();
            int frames=Engine.PhysicsTicksPerSecond/2; await Frames(frames);
            float time=(Engine.GetPhysicsFrames()-firstFrame)/(float)Engine.PhysicsTicksPerSecond;
            float measuredPollen=(velocity.Y-pollen.LinearVelocity.Y)/time;
            float measuredNectar=(nectarVelocity.Y-nectar.LinearVelocity.Y)/time;
            float drop=start.Y-pollen.Position.Y;
            float analytic=-velocity.Y*time+gravity*time*time/2;
            float discrete=analytic+gravity*time/(2*Engine.PhysicsTicksPerSecond);
            GD.Print($"GRAVITY measured: pollen {measuredPollen:0.00000}, nectar {measuredNectar:0.00000} m/s^2; masses {pollen.Mass*1000:0.00}/{nectar.Mass*1000:0.00} g");
            GD.Print($"FALL {time:0.0000} s: measured {drop:0.00000} m; continuous {analytic:0.00000} m; discrete {discrete:0.00000} m");
            Check(Mathf.Abs(measuredPollen-gravity)<.005f && Mathf.Abs(measuredNectar-gravity)<.005f,
                "Measured free-fall acceleration matches configured gravity for both masses");
            Check(Mathf.Abs(drop-discrete)<.002f && Mathf.Abs(drop-analytic)<.025f,
                "Free-fall distance matches the numerical step and stays within 2.5 cm of continuous physics at 0.5 s");
            Check(Mathf.Abs(drop-(nectarStart.Y-nectar.Position.Y))<.0002f
                && Mathf.Abs(velocity.Y-nectarVelocity.Y)<.00001f,
                "Different ball masses fall equally in the current no-drag model");
            game.RemoveBall(pollen); game.RemoveBall(nectar);
            var dropped=game.SpawnBall(PieceKind.Pollen,new(.75f,1.03556f,-1.2f));
            ulong releaseFrame=Engine.GetPhysicsFrames(); bool bounced=false; float beforeImpact=0;
            for (int i=0;i<Engine.PhysicsTicksPerSecond;i++)
            {
                await Frames(1);
                if (dropped.LinearVelocity.Y>0) { bounced=true; break; }
                beforeImpact=-dropped.LinearVelocity.Y;
            }
            float fallTime=(Engine.GetPhysicsFrames()-releaseFrame)/(float)Engine.PhysicsTicksPerSecond;
            float expectedTime=Mathf.Sqrt(2/gravity);
            GD.Print($"DROP 1 m to floor: {fallTime:0.0000} s; continuous {expectedTime:0.0000} s; last falling speed {beforeImpact:0.000} m/s");
            Check(bounced && Mathf.Abs(fallTime-expectedTime)<2f/Engine.PhysicsTicksPerSecond,
                "A ball dropped one metre contacts the floor at the expected time within two physics steps");
            // Lansare fără obstacole: comparăm predicția cu o minge efectiv integrată.
            var launched=game.SpawnBall(PieceKind.Pollen,new(.7f,5,-.9f));
            launched.LinearVelocity=new(1,3,0); await Frames(3);
            start=launched.Position; velocity=launched.LinearVelocity; firstFrame=Engine.GetPhysicsFrames();
            await Frames(frames); time=(Engine.GetPhysicsFrames()-firstFrame)/(float)Engine.PhysicsTicksPerSecond;
            float error=launched.Position.DistanceTo(ShotPlanner.PositionAt(start,velocity,time));
            GD.Print($"BALLISTIC actual/planner error after {time:0.0000} s: {error*100:0.000} cm");
            Check(error<.002f,"Ballistic preview matches an actual flying body within 2 mm");
            GD.Print("ALL GRAVITY CHECKS PASSED"); game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("GRAVITY FAILURE: "+ex); game.GetTree().Quit(1); }
    }
    private static T Find<T>(Node root, Func<T,bool> predicate) where T:Node
    {
        if (root is T candidate && predicate(candidate)) return candidate;
        foreach (var child in root.GetChildren()) { var found=Find(child,predicate); if (found!=null) return found; }
        return null;
    }
    public static async Task Run(Simulation game)
    {
        try
        {
            void Check(bool valid,string text) { if (!valid) throw new Exception(text); GD.Print("PASS: "+text); }
            async Task Frames(int n) { for (int i=0;i<Mathf.CeilToInt(n*Engine.PhysicsTicksPerSecond/120f);i++) await game.ToSignal(game.GetTree(),SceneTree.SignalName.PhysicsFrame); }
            void Reset(bool practice=true) { game.Practice=practice; game.Reset(); foreach (var r in game.Robots) r.Bot=false; }
            game.Testing=true;
            game.Hud.ShowPause(true); Reset();
            Check(!game.Hud.MenuVisible && game.Running,"Reset closes the pause/menu overlay");
            await Frames(3);
            game.TogglePause();
            Find<Button>(game.Hud,b=>b.Text=="ROBOT SETUP").EmitSignal(Button.SignalName.Pressed);
            var slider=Find<HSlider>(game.Hud,s=>s.MaxValue==45.72);
            float oldWidth=game.Profile.WidthCm;
            slider.Value=30;
            Check(Mathf.Abs(game.Profile.WidthCm-oldWidth)<.0001f,"Creator draft does not mutate the active profile");
            Find<Button>(game.Hud,b=>b.Text=="BACK").EmitSignal(Button.SignalName.Pressed);
            Check(Mathf.Abs(game.Profile.WidthCm-oldWidth)<.0001f,"Back discards unapplied robot edits");
            var detached=game.Capture();
            float snapshotWidth=detached.Profile.WidthCm;
            game.Profile.WidthCm=31;
            Check(detached.Profile.WidthCm==snapshotWidth,"Captured profile is independent of later edits");
            game.Profile.WidthCm=oldWidth;
            game.Restore(detached); foreach (var r in game.Robots) r.Bot=false;
            Check(game.Running && !game.Hud.MenuVisible,"Restore leaves a running scene with no stale pause overlay");
            var invalid=game.Capture(); invalid.Tokens[0]=-1;
            var originalPlayer=game.Player; bool rejected=false;
            try { game.Restore(invalid); } catch { rejected=true; }
            Check(rejected && game.Player==originalPlayer,"Negative human-player tokens are rejected before reset");
            invalid=game.Capture(); invalid.RedSide=int.MinValue; rejected=false;
            try { game.Restore(invalid); } catch { rejected=true; }
            Check(rejected && game.Player==originalPlayer,"Invalid HIVE side cannot overflow validation or replace the scene");
            game.Camera.Mode=2; game.Camera._Process(0);
            var cameraBefore=game.Camera.Position; var front=-game.Camera.GlobalBasis.Z;
            game.Camera.Drag(Vector2.Zero,false); game.Camera._Process(0);
            Check(game.Camera.Position.DistanceTo(cameraBefore)<.03f && (-game.Camera.GlobalBasis.Z).Dot(front)>.999f,
                "Follow-to-orbit handoff preserves framing");
            game.Camera.ResetView();
            await Frames(3);
            float[][] good={new float[] {80,80},new float[] {90,80}};
            Check(game.PathEditor.ReplacePoints(good),"Path accepts open floor waypoints");
            Check(!game.PathEditor.ReplacePoints(new[] {new float[] {0,0}}) && game.PathEditor.Points.Count==2,
                "Wall waypoint is rejected without replacing a valid path");
            float frameX=(Arena.Center-24.73f*.0254f)*100;
            Check(!game.PathEditor.ReplacePoints(new[] {new float[] {frameX,Arena.Center*100}}),
                "Waypoint inside the central structure is rejected");
            game.Player.Position=new(.8f,0,-.8f);
            Check(game.PathEditor.ReplacePoints(Enumerable.Range(0,1000).Select(_=>new float[] {80,80}).ToArray()),
                "Duplicate waypoint file loads without degenerate beams");
            game.PathEditor.Following=true;
            Check(game.PathEditor.Command()==Vector3.Zero && !game.PathEditor.Following && game.PathEditor.Current==1000,
                "A thousand reached waypoints finish without recursive calls");
            game.Player.Position=new(.5f,0,-Arena.Center); game.Player.Velocity=Vector3.Zero;
            Check(game.PathEditor.ReplacePoints(new[] {new float[] {280,Arena.Center*100}}),"Destination beyond frame is valid");
            game.PathEditor.Following=true;
            for (int i=0;i<720 && game.PathEditor.Following;i++)
            { game.Player.Command=game.PathEditor.Command(); await Frames(1); }
            Check(!game.PathEditor.Following && game.Status.Contains("blocked") && game.Player.Position.X<1.1f,
                "Follower stops and explains a physical obstruction");
            game.PathEditor.ReplacePoints(Array.Empty<float[]>());
            Reset(false); await Frames(3);
            game.Elapsed=30-.5f/Engine.PhysicsTicksPerSecond; game.Player.Velocity=Vector3.Right; game.Player.Command=Vector3.Right;
            game.Player.FireCommand=true;
            int shots=game.Player.ShotsFired; await Frames(3);
            game.Player.Fire();
            Check(!game.DrivingAllowed && game.Player.Velocity==Vector3.Zero && game.Player.ShotsFired==shots,
                "Transition stops driving and rejects direct firing");
            game.Elapsed=37.995f; await Frames(3);
            Check(game.DrivingAllowed,"Teleop re-enables driving after transition");
            game.Elapsed=157.995f; await Frames(3);
            Check(game.Elapsed==158 && game.Finished && game.Balls.Where(b=>!b.Stored).All(b=>b.Freeze),
                "Match ends at 158 seconds with frozen physics");
            game.TogglePause(); await Frames(3);
            Check(!game.Running && !game.Paused && game.Balls.Where(b=>!b.Stored).All(b=>b.Freeze),
                "Escape cannot reactivate physics after the final result");
            var finished=game.Capture(); game.Restore(finished); await Frames(3);
            Check(game.Finished && !game.Running && game.Hud.MenuVisible && game.Balls.Where(b=>!b.Stored).All(b=>b.Freeze),
                "Loading a finished match preserves the final result and freeze");
            GD.Print("ALL AUDIT REGRESSIONS PASSED"); game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("AUDIT FAILURE: "+ex); game.GetTree().Quit(1); }
    }
}
