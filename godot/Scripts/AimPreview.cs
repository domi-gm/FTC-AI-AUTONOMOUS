using Godot;

public partial class AimPreview : Node3D
{
    public Simulation Game;
    public string Status = "NO PIECE";
    public float Speed;
    private float _timer;
    private MeshInstance3D _line;
    public override void _Ready()
    {
        _line = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_line);
    }
    public override void _PhysicsProcess(double delta)
    {
        _line.Visible = Game.Started && !Game.Paused;
        if (!_line.Visible || (_timer -= (float)delta) > 0) return;
        _timer = .2f;
        var robot = Game.Player;
        if (robot == null || robot.Inventory.Count == 0)
        { Status = "NO PIECE"; _line.Mesh = null; return; }
        Vector3 origin = robot.LaunchOrigin, target = Game.Target(robot);
        float radius = robot.Inventory[0] == PieceKind.Pollen ? .03556f : .04572f;
        bool clear = ShotPlanner.TryPlan(robot, origin, target, radius, out Vector3 velocity);
        Status = clear ? "ON TARGET" : "BLOCKED"; Speed = velocity.Length();
        var mesh = new ImmediateMesh();
        Color color = clear ? new("63e6b0") : new("ff667b");
        var material = VisualFactory.Material(color, true);
        material.NoDepthTest = true;
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
        if (clear)
        {
            float horizontal = new Vector2(velocity.X, velocity.Z).Length();
            float time = new Vector2(target.X-origin.X,target.Z-origin.Z).Length() / Mathf.Max(horizontal,.001f);
            Vector3 previous = origin;
            for (int i=1; i<=48; i++)
            {
                float t = time*i/48;
                Vector3 point = origin + velocity*t - Vector3.Up*(ShotPlanner.Gravity*t*(t+1f/Engine.PhysicsTicksPerSecond)/2);
                Segment(mesh, previous, point); previous=point;
            }
        }
        else { Segment(mesh, origin, target); }
        Segment(mesh,target-Vector3.Right*.07f,target+Vector3.Right*.07f);
        Segment(mesh,target-Vector3.Forward*.07f,target+Vector3.Forward*.07f);
        mesh.SurfaceEnd(); _line.Mesh = mesh;
    }
    private static void Segment(ImmediateMesh mesh, Vector3 a, Vector3 b)
    {
        var direction = (b-a).Normalized();
        Vector3 side = direction.Cross(Vector3.Up);
        side = side.LengthSquared() < .0001f ? Vector3.Right : side.Normalized();
        foreach (Vector3 offset in new[] {side*.009f, direction.Cross(side).Normalized()*.009f})
            foreach (Vector3 vertex in new[] {a-offset,b-offset,b+offset,a-offset,b+offset,a+offset})
                mesh.SurfaceAddVertex(vertex);
    }
}
