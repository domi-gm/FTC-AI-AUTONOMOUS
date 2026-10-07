using Godot;
using System;

/// <summary>Caută în ordinea vitezei; prima traiectorie liberă este cea cu cost minim.</summary>
public static class ShotPlanner
{
    public const float Gravity = 9.81f;
    public const float MaximumSpeed = 17.78f;
    public readonly record struct Plan(Vector3 Velocity, float FlightTime);
    private readonly record struct Candidate(Vector3 Velocity, float Time, float Cost);
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
    {
        plan = default;
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
        query.Transform = new Transform3D(Basis.Identity, origin);
        if (space.IntersectShape(query, 1).Count > 0) return false;
        var candidates = new Candidate[51];
        for (int i=0;i<candidates.Length;i++)
        {
            float time = .25f + i*.025f;
            Vector3 velocity = VelocityFor(origin,target,time);
            candidates[i] = new(velocity,time,velocity.LengthSquared());
        }
        Array.Sort(candidates, (a,b) => a.Cost.CompareTo(b.Cost));
        foreach (var candidate in candidates)
        {
            if (candidate.Cost > MaximumSpeed*MaximumSpeed) break;
            bool blocked = false;
            Vector3 previous = origin;
            int segments = Mathf.CeilToInt(candidate.Time/sampleStep);
            for (int i=1;i<=segments;i++)
            {
                Vector3 point = PositionAt(origin,candidate.Velocity,candidate.Time*i/segments);
                query.Transform = new Transform3D(Basis.Identity,previous); query.Motion = point-previous;
                var fractions = space.CastMotion(query);
                if (fractions.Length > 0 && fractions[0] < .999f) { blocked=true; break; }
                query.Transform = new Transform3D(Basis.Identity,point); query.Motion=Vector3.Zero;
                // CastMotion nu detectează o suprapunere existentă: păstrăm controlul capetelor.
                if (space.IntersectShape(query,1).Count > 0) { blocked=true; break; }
                previous=point;
            }
            if (!blocked) { plan = new(candidate.Velocity,candidate.Time); return true; }
        }
        return false;
    }
}
