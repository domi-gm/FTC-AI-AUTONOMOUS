using Godot;

/// <summary>Caută o parabolă care ajunge la țintă fără să traverseze pereții.</summary>
public static class ShotPlanner
{
    public const float Gravity = 9.81f;
    public const float MaximumSpeed = 17.78f;
    public static bool TryPlan(RobotAgent robot, Vector3 origin, Vector3 target, float radius, out Vector3 velocity)
    {
        velocity = Vector3.Zero;
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new SphereShape3D { Radius = radius + .002f },
            CollisionMask = 1 | 4,
            Exclude = new Godot.Collections.Array<Rid> { robot.GetRid() },
            Margin = .001f
        };
        var space = robot.GetWorld3D().DirectSpaceState;
        query.Transform = new Transform3D(Basis.Identity, origin);
        if (space.IntersectShape(query, 1).Count > 0) return false;
        float step = 1f / Engine.PhysicsTicksPerSecond;
        float best = float.MaxValue;
        for (float time = .25f; time <= 1.5f; time += .025f)
        {
            // Jolt aplică gravitația la pași discreți; compensăm jumătate de pas.
            Vector3 candidate = (target - origin) / time + Vector3.Up * (Gravity * (time + step) / 2);
            if (candidate.Length() > MaximumSpeed) continue;
            bool blocked = false;
            Vector3 previous = origin;
            for (float t = step; t < time + step; t += step)
            {
                float sample = Mathf.Min(t, time);
                Vector3 point = origin + candidate * sample - Vector3.Up * (Gravity * sample * (sample + step) / 2);
                query.Transform = new Transform3D(Basis.Identity, previous);
                query.Motion = point - previous;
                var fractions = space.CastMotion(query);
                if (fractions.Length > 0 && fractions[0] < .999f) { blocked = true; break; }
                // CastMotion ignoră suprapunerile existente. Verificăm și capătul
                // fiecărui pas, altfel un pas scurt poate începe deja în obstacol.
                query.Transform = new Transform3D(Basis.Identity, point);
                query.Motion = Vector3.Zero;
                if (space.IntersectShape(query, 1).Count > 0) { blocked = true; break; }
                previous = point;
            }
            if (blocked) continue;
            float cost = candidate.LengthSquared();
            if (cost < best) { best = cost; velocity = candidate; }
        }
        return best < float.MaxValue;
    }
}
