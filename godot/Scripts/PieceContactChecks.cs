using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Impacturi izolate și contacte reale în arena simulatorului.</summary>
public static class PieceContactChecks
{
    private static float Energy(GamePiece b) => .5f*b.Mass*b.LinearVelocity.LengthSquared()
        +.5f*PieceContactModel.InertiaFactor*b.Mass*b.Radius*b.Radius*b.AngularVelocity.LengthSquared();
    public static async Task Run(Simulation game)
    {
        try
        {
            void Check(bool valid,string text) { if (!valid) throw new Exception(text); GD.Print("PASS: "+text); }
            async Task Frames(int n) { for (int i=0;i<n;i++) await game.ToSignal(game.GetTree(),SceneTree.SignalName.PhysicsFrame); }
            Task Seconds(float seconds) => Frames(Mathf.CeilToInt(seconds*Engine.PhysicsTicksPerSecond));
            game.Testing=true; game.Practice=true; game.Reset(); foreach (var r in game.Robots) r.Bot=false;
            foreach (var test in new[] {
                (Name:"slow equal",Kind:PieceKind.Pollen,Left:.3f,Right:-.3f,Restitution:0f),
                (Name:"equal",Kind:PieceKind.Pollen,Left:3f,Right:0f,Restitution:PieceContactModel.BallPairRestitution),
                (Name:"different masses",Kind:PieceKind.RedNectar,Left:3f,Right:0f,Restitution:PieceContactModel.BallPairRestitution),
                (Name:"fast opposing",Kind:PieceKind.Pollen,Left:ShotPlanner.MaximumSpeed,Right:-ShotPlanner.MaximumSpeed,Restitution:PieceContactModel.BallPairRestitution) })
            {
                var a=game.SpawnBall(PieceKind.Pollen,new(.5f,5,-.8f));
                var b=game.SpawnBall(test.Kind,new(1.1f,5,-.8f));
                a.GravityScale=b.GravityScale=0; await Frames(3);
                a.LinearVelocity=Vector3.Right*test.Left; b.LinearVelocity=Vector3.Right*test.Right;
                float momentum=a.Mass*test.Left+b.Mass*test.Right;
                float energy=Energy(a)+Energy(b), penetration=0; bool crossed=false;
                for (int i=0;i<Mathf.CeilToInt(1.25f*Engine.PhysicsTicksPerSecond);i++)
                {
                    await Frames(1); crossed|=a.Position.X>b.Position.X;
                    penetration=Mathf.Max(penetration,a.Radius+b.Radius-a.Position.DistanceTo(b.Position));
                }
                float e=(b.LinearVelocity.X-a.LinearVelocity.X)/(test.Left-test.Right);
                GD.Print($"CONTACT {test.Name}: e={e:0.000}; overlap max {penetration*1000:0.000} mm; velocities {a.LinearVelocity.X:0.000}/{b.LinearVelocity.X:0.000} m/s");
                Check(!crossed && penetration<.004f,$"{test.Name}: balls do not tunnel and overlap stays below 4 mm during impact");
                Check(Mathf.Abs(e-test.Restitution)<.035f,$"{test.Name}: measured restitution matches the contact model");
                Check(Mathf.Abs(a.Mass*a.LinearVelocity.X+b.Mass*b.LinearVelocity.X-momentum)<.00002f
                    && Energy(a)+Energy(b)<=energy*1.005f,$"{test.Name}: momentum is conserved without creating kinetic energy");
                game.RemoveBall(a); game.RemoveBall(b); await Frames(2);
            }
            float phaseOverlap=0; bool phaseCrossing=false;
            foreach (float gap in new[] {.15f,.18f,.21f,.25f,.28f,.32f,.35f,.4f,.45f,.5f,.55f,.6f,.65f,.7f,.8f,1f,1.1f})
            {
                var a=game.SpawnBall(PieceKind.Pollen,new(.5f,5,-.8f));
                var b=game.SpawnBall(PieceKind.Pollen,new(.5f+gap,5,-.8f));
                a.GravityScale=b.GravityScale=0; await Frames(3);
                a.LinearVelocity=Vector3.Right*ShotPlanner.MaximumSpeed;
                b.LinearVelocity=-a.LinearVelocity;
                for (int i=0;i<Mathf.CeilToInt(.12f*Engine.PhysicsTicksPerSecond);i++)
                {
                    await Frames(1); phaseCrossing|=a.Position.X>b.Position.X;
                    phaseOverlap=Mathf.Max(phaseOverlap,a.Radius+b.Radius-a.Position.DistanceTo(b.Position));
                }
                game.RemoveBall(a); game.RemoveBall(b); await Frames(2);
            }
            GD.Print($"FAST 17 launch phases: worst overlap {phaseOverlap*1000:0.000} mm");
            Check(!phaseCrossing && phaseOverlap<.004f,"Maximum-speed opposing collisions remain stable across 17 initial separations");
            var missA=game.SpawnBall(PieceKind.Pollen,new(.5f,5,-.8f));
            var missB=game.SpawnBall(PieceKind.Pollen,new(1.1f,5,-.8f+missA.Radius*2+.005f));
            missA.GravityScale=missB.GravityScale=0; await Frames(3);
            missA.LinearVelocity=Vector3.Right*ShotPlanner.MaximumSpeed; missB.LinearVelocity=-missA.LinearVelocity;
            await Seconds(.12f);
            Check(missA.LinearVelocity.DistanceTo(Vector3.Right*ShotPlanner.MaximumSpeed)<.02f
                && missB.LinearVelocity.DistanceTo(Vector3.Left*ShotPlanner.MaximumSpeed)<.02f,
                "A maximum-speed near miss with 5 mm clearance does not cause a ghost bounce");
            game.RemoveBall(missA); game.RemoveBall(missB);
            var angled=game.SpawnBall(PieceKind.Pollen,new(.5f,5,-.8f));
            var hit=game.SpawnBall(PieceKind.Pollen,new(.85f,5,-.755f));
            angled.GravityScale=hit.GravityScale=0; await Frames(3);
            angled.LinearVelocity=Vector3.Right*2; float initialEnergy=Energy(angled);
            await Seconds(.6f);
            Check(angled.AngularVelocity.Length()+hit.AngularVelocity.Length()>.1f && Energy(angled)+Energy(hit)<initialEnergy*1.005f,
                "Glancing collision creates spin without creating energy");
            game.RemoveBall(angled); game.RemoveBall(hit);
            var airborne=game.SpawnBall(PieceKind.Pollen,new(.7f,10,-.9f));
            airborne.AngularVelocity=new(1,2,3); await Frames(3);
            var initialSpin=airborne.AngularVelocity; await Seconds(.5f);
            Check(airborne.AngularVelocity.DistanceTo(initialSpin)<.005f,"Airborne spin has no hidden angular damping");
            game.RemoveBall(airborne);
            var rolling=game.SpawnBall(PieceKind.Pollen,new(.7f,.04f,-.8f));
            await Frames(45); rolling.Sleeping=false;
            rolling.LinearVelocity=Vector3.Right; rolling.AngularVelocity=Vector3.Forward/rolling.Radius;
            var release=rolling.Position;
            await Seconds(1);
            GD.Print($"ROLL 1 s: speed {rolling.LinearVelocity.X:0.000} m/s; spin {rolling.AngularVelocity.Length():0.000} rad/s; distance {rolling.Position.X-release.X:0.000} m");
            Check(rolling.LinearVelocity.X>.45f && rolling.LinearVelocity.X<.8f,"Floor contact slows rolling without an instant velocity lock");
            await Seconds(4);
            GD.Print($"ROLL settled: speed {rolling.LinearVelocity.Length():0.000} m/s; distance {rolling.Position.X-release.X:0.000} m");
            Check(rolling.LinearVelocity.Length()<.035f && rolling.Position.X>release.X && rolling.Position.X-release.X<2,
                "A rolling ball settles without reversing or crossing the field");
            game.RemoveBall(rolling);
            var spinning=game.SpawnBall(PieceKind.Pollen,new(.75f,.04f,-.8f));
            await Seconds(.1f); spinning.Sleeping=false; spinning.AngularVelocity=Vector3.Up*8;
            await Seconds(3);
            Check(spinning.AngularVelocity.Length()<.3f && spinning.LinearVelocity.Length()<.035f,
                "A ball spinning on the floor settles through contact torque without drifting");
            game.RemoveBall(spinning);
            foreach (var flower in game.Flowers) flower.Update();
            float maximumOverlap=0; float maximumSpeed=0;
            foreach (var flower in game.Flowers)
                for (int i=0;i<flower.Balls.Count;i++)
                {
                    maximumSpeed=Mathf.Max(maximumSpeed,flower.Balls[i].LinearVelocity.Length());
                    for (int j=0;j<i;j++) maximumOverlap=Mathf.Max(maximumOverlap,
                        flower.Balls[i].Radius+flower.Balls[j].Radius-flower.Balls[i].Position.DistanceTo(flower.Balls[j].Position));
                }
            GD.Print($"FLOWER settled: overlap {maximumOverlap*1000:0.000} mm, speed {maximumSpeed:0.0000} m/s");
            Check(game.Flowers.Sum(f=>f.Balls.Count)==16 && maximumOverlap<.002f && maximumSpeed<.035f,
                "Initial flower stacks retain all 16 balls and settle without deep penetration");
            var floor=game.SpawnBall(PieceKind.Pollen,new(.75f,1.03556f,-1.2f));
            float maximumBounce=0; bool bounced=false;
            for (int i=0;i<Engine.PhysicsTicksPerSecond*2;i++)
            {
                await Frames(1);
                if (floor.LinearVelocity.Y>0) bounced=true;
                if (bounced) maximumBounce=Mathf.Max(maximumBounce,floor.Position.Y-floor.Radius);
            }
            GD.Print($"FLOOR bounce after 1 m drop: {maximumBounce*100:0.00} cm");
            Check(maximumBounce>.04f && maximumBounce<.11f,"Floor bounce remains bounded after separating ball and floor materials");
            GD.Print("ALL CONTACT CHECKS PASSED"); game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("CONTACT FAILURE: "+ex); game.GetTree().Quit(1); }
    }
}
