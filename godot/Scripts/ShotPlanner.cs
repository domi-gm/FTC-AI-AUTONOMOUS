using Godot;
using System;

/// <summary>Caută întâi vitezele mici pe grila principală; rafinează după eșec.</summary>
public static class ShotPlanner
{
    public const float Gravity = 9.81f;
    public const float MaximumSpeed = 17.78f;
    public readonly record struct Plan(Vector3 Velocity, float FlightTime);
    public enum Failure { None, InvalidInput, OriginBlocked, TargetBlocked, SpeedLimit, PathBlocked }
    public readonly record struct Diagnosis(Failure Reason, string Obstacle, Vector3 Point)
    {
        public string Message => Reason switch {
            Failure.None => "READY",
            Failure.InvalidInput => "INVALID SHOT PARAMETERS",
            Failure.OriginBlocked => "BLOCKED: LAUNCH POINT / "+Obstacle,
            Failure.TargetBlocked => "BLOCKED: TARGET / "+Obstacle,
            Failure.SpeedLimit => "NO PATH WITHIN LAUNCH SPEED",
            _ => "BLOCKED: "+Obstacle
        };
    }
    private readonly record struct Candidate(Vector3 Velocity, float Time, float Cost);
    private readonly record struct Interval(int Index, float Progress, float Cost);
    public const int RefinementIntervals=16;
    public static Vector3 VelocityFor(Vector3 origin, Vector3 target, float time)
        => (target-origin)/time + Vector3.Up*(Gravity*(time+1f/Engine.PhysicsTicksPerSecond)/2);
    public static Vector3 PositionAt(Vector3 origin, Vector3 velocity, float time)
        => origin + velocity*time - Vector3.Up*(Gravity*time*(time+1f/Engine.PhysicsTicksPerSecond)/2);
    public static bool TryPlan(RobotAgent robot, Vector3 origin, Vector3 target, float radius, out Vector3 velocity)
    {
        bool clear = TrySolve(robot, origin, target, radius, out var plan);
        velocity = plan.Velocity; return clear;
    }
    public static bool TrySolve(RobotAgent robot, Vector3 origin, Vector3 target, float radius, out Plan plan)
        => TrySolve(robot,origin,target,radius,out plan,out _);
    public static bool TrySolve(RobotAgent robot, Vector3 origin, Vector3 target, float radius, out Plan plan, out Diagnosis diagnosis)
    {
        plan = default; diagnosis=default;
        if (!float.IsFinite(origin.X+origin.Y+origin.Z+target.X+target.Y+target.Z+radius) || radius<=0)
        { diagnosis=new(Failure.InvalidInput,"",origin); return false; }
        const float sampleStep = 1f/30;
        // Săgeata parabolei față de coarda unui segment este g*dt²/8.
        // Extinderea sferei acoperă această eroare la verificarea continuă.
        using var sphere = new SphereShape3D { Radius = radius + .002f + Gravity*sampleStep*sampleStep/8 };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = sphere, CollisionMask = 1 | 4,
            Exclude = new Godot.Collections.Array<Rid> { robot.GetRid() }, Margin = .001f
        };
        // The existing launcher ignores its own chassis; include its articulated links too.
        var excluded = new Godot.Collections.Array<Rid> { robot.GetRid() };
        if (robot.Rig != null)
            foreach (var body in robot.Rig.Links.Values) excluded.Add(body.GetRid());
        query.Exclude = excluded;
        var space = robot.GetWorld3D().DirectSpaceState;
        string Obstacle()
        {
            var hits=space.IntersectShape(query,1);
            if(hits.Count==0) return "OBSTACLE";
            var node=hits[0]["collider"].AsGodotObject() as Node;
            for(var owner=node;owner!=null;owner=owner.GetParent())
                if(owner is RobotAgent other) return $"ROBOT {(other.Red ? "R" : "B")}{other.Number}";
            string name=(node?.GetParent() is MeshInstance3D mesh ? mesh.Name.ToString() : node?.Name.ToString()) ?? "OBSTACLE";
            if(name.Contains("StaticBody") || name.Contains("AnimatableBody")) return "OBSTACLE";
            if(name.StartsWith("Roof")) return "HIVE ROOF";
            if(name.StartsWith("Physical")) return "HIVE "+name[8..].ToUpperInvariant();
            if(name.StartsWith("Frame") || name.StartsWith("Axle")) return "FIELD FRAME";
            if(name.StartsWith("Flower")) return "FLOWER WALL";
            if(name.StartsWith("Wall")) return "FIELD WALL";
            return name.ToUpperInvariant();
        }
        query.Transform = new Transform3D(Basis.Identity, origin);
        if (space.IntersectShape(query, 1).Count > 0)
        { diagnosis=new(Failure.OriginBlocked,Obstacle(),origin); return false; }
        query.Transform=new(Basis.Identity,target);
        if(space.IntersectShape(query,1).Count>0)
        { diagnosis=new(Failure.TargetBlocked,Obstacle(),target); return false; }
        float bestProgress=-1, traceProgress=-1; Vector3 bestBlock=origin; bool speedAllowed=false;
        bool Trace(Candidate candidate)
        {
            traceProgress=-1;
            if(candidate.Cost>MaximumSpeed*MaximumSpeed) return false;
            speedAllowed=true;
            Vector3 previous=origin;
            int segments=Mathf.CeilToInt(candidate.Time/sampleStep);
            for(int i=1;i<=segments;i++)
            {
                Vector3 point=PositionAt(origin,candidate.Velocity,candidate.Time*i/segments);
                query.Transform=new(Basis.Identity,previous); query.Motion=point-previous;
                var fractions=space.CastMotion(query);
                if(fractions.Length>0 && fractions[0]<.999f)
                {
                    float progress=(i-1+fractions[0])/segments;
                    traceProgress=progress;
                    if(progress>bestProgress) { bestProgress=progress; bestBlock=previous.Lerp(point,Mathf.Min(1,fractions[1]+.001f)); }
                    return false;
                }
                query.Transform=new(Basis.Identity,point); query.Motion=Vector3.Zero;
                if(space.IntersectShape(query,1).Count>0)
                {
                    float progress=i/(float)segments;
                    traceProgress=progress;
                    if(progress>bestProgress) { bestProgress=progress; bestBlock=point; }
                    return false;
                }
                previous=point;
            }
            return true;
        }
        var candidates = new Candidate[51];
        for (int i=0;i<candidates.Length;i++)
        {
            float time = .25f + i*.025f;
            Vector3 velocity = VelocityFor(origin,target,time);
            candidates[i] = new(velocity,time,velocity.LengthSquared());
        }
        Array.Sort(candidates, (a,b) => a.Cost.CompareTo(b.Cost));
        var progressScores=new float[51]; Array.Fill(progressScores,-1);
        foreach (var candidate in candidates)
        {
            if (candidate.Cost > MaximumSpeed*MaximumSpeed) break;
            if (Trace(candidate)) { plan = new(candidate.Velocity,candidate.Time); return true; }
            progressScores[Math.Clamp(Mathf.RoundToInt((candidate.Time-.25f)/.025f),0,50)]=traceProgress;
        }
        // Ferestrele înguste dintre panouri pot încăpea între probele de 25 ms.
        // Rafinăm numai după eșec, păstrând verificarea volumului întregii mingi.
        // Buget limitat: întâi intervalele ale căror arce ajung cel mai aproape
        // de țintă înaintea coliziunii, apoi cele cu viteză mică la egalitate.
        var intervals=new Interval[50];
        for(int i=0;i<intervals.Length;i++)
        {
            float time=.25f+i*.025f+.0125f;
            intervals[i]=new(i,Mathf.Max(progressScores[i],progressScores[i+1]),VelocityFor(origin,target,time).LengthSquared());
        }
        Array.Sort(intervals,(a,b)=> { int score=b.Progress.CompareTo(a.Progress); return score!=0 ? score : a.Cost.CompareTo(b.Cost); });
        var refined=new Candidate[RefinementIntervals*4]; int next=0;
        for(int chosen=0;chosen<RefinementIntervals;chosen++) for(int offset=1;offset<=4;offset++)
        {
            float time=.25f+intervals[chosen].Index*.025f+offset*.005f;
            var velocity=VelocityFor(origin,target,time);
            refined[next++]=new(velocity,time,velocity.LengthSquared());
        }
        Array.Sort(refined,(a,b)=>a.Cost.CompareTo(b.Cost));
        foreach(var candidate in refined)
        {
            if(candidate.Cost>MaximumSpeed*MaximumSpeed) break;
            if(Trace(candidate)) { plan=new(candidate.Velocity,candidate.Time); return true; }
        }
        query.Transform=new(Basis.Identity,bestBlock); query.Motion=Vector3.Zero;
        diagnosis=speedAllowed ? new(Failure.PathBlocked,Obstacle(),bestBlock) : new(Failure.SpeedLimit,"",target);
        return false;
    }
}
