using Godot;

public partial class AimPreview : Node3D
{
    public Simulation Game;
    public string Status = "NO PIECE";
    public float Speed;
    public bool PollenClear, NectarClear;
    public float PollenSpeed, NectarSpeed;
    public float PollenFlightTime, NectarFlightTime;
    public ShotPlanner.Diagnosis PollenDiagnosis, NectarDiagnosis;
    public float FlightTime { get; private set; }
    public Vector3 DisplayOrigin { get; private set; }
    public Vector3 DisplayEnd { get; private set; }
    public int PlanUpdates { get; private set; }
    private float _timer;
    private bool _clear;
    private RobotAgent _lastRobot;
    private PieceKind? _lastKind;
    private bool _lastTarget;
    private int _lastCount = -1;
    private MeshInstance3D _line;
    private readonly ImmediateMesh _mesh = new();
    private StandardMaterial3D _green, _red;
    public override void _Ready()
    {
        ProcessPhysicsPriority = 100;
        _green = VisualFactory.Material(new("63e6b0"), true); _green.NoDepthTest = true;
        _red = VisualFactory.Material(new("ff667b"), true); _red.NoDepthTest = true;
        _line = new MeshInstance3D { Mesh = _mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_line);
    }
    public override void _PhysicsProcess(double delta)
    {
        var robot = Game.Player;
        if (!Game.Running || robot == null) { _line.Visible = false; return; }
        _line.Visible = Game.ShowTrajectory;
        PieceKind? kind = robot.Inventory.Count > 0 ? robot.Inventory[0] : null;
        bool changed = robot != _lastRobot || kind != _lastKind || Game.AimFlower != _lastTarget || robot.Inventory.Count != _lastCount;
        _lastRobot = robot; _lastKind = kind; _lastTarget = Game.AimFlower;
        _lastCount = robot.Inventory.Count;
        _timer -= (float)delta;
        if (!changed && _timer > 0) return;
        _timer = .05f;
        var origin = robot.LaunchOrigin; var target = Game.Target(robot);
        PollenClear = ShotPlanner.TrySolve(robot, origin, target, .03556f, out var pollen, out PollenDiagnosis);
        NectarClear = ShotPlanner.TrySolve(robot, origin, target, .04572f, out var nectar, out NectarDiagnosis);
        PollenSpeed = pollen.Velocity.Length(); NectarSpeed = nectar.Velocity.Length();
        PollenFlightTime = pollen.FlightTime; NectarFlightTime = nectar.FlightTime;
        if (kind == null)
        {
            Status = "NO PIECE IN ROBOT"; Speed = 0; FlightTime = 0; _clear = false; robot.UpdateShotReadiness(false, default); PlanUpdates++; return;
        }
        var plan = kind == PieceKind.Pollen ? pollen : nectar;
        _clear = kind == PieceKind.Pollen ? PollenClear : NectarClear;
        FlightTime = plan.FlightTime; Speed = plan.Velocity.Length(); PlanUpdates++;
        var diagnosis = kind == PieceKind.Pollen ? PollenDiagnosis : NectarDiagnosis;
        Status = _clear ? "CLEAR PATH" : diagnosis.Message;
        robot.UpdateShotReadiness(_clear, diagnosis);
    }
    public override void _Process(double delta)
    {
        var robot = Game.Player;
        _line.Visible = Game.Running && robot != null && robot.Inventory.Count > 0;
        if (!_line.Visible) { Speed = 0; _mesh.ClearSurfaces(); return; }
        Vector3 origin = robot.LaunchOrigin, target = Game.Target(robot);
        DisplayOrigin = origin; DisplayEnd = target;
        _mesh.ClearSurfaces();
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _clear ? _green : _red);
        if (_clear && FlightTime > 0)
        {
            Vector3 velocity = ShotPlanner.VelocityFor(origin, target, FlightTime);
            Speed = velocity.Length();
            Vector3 previous = origin;
            for (int i = 1; i <= 48; i++)
            {
                Vector3 point = ShotPlanner.PositionAt(origin, velocity, FlightTime * i / 48);
                Segment(_mesh, previous, point); previous = point;
            }
        }
        else Segment(_mesh, origin, target);
        Segment(_mesh, target - Vector3.Right * .07f, target + Vector3.Right * .07f);
        Segment(_mesh, target - Vector3.Forward * .07f, target + Vector3.Forward * .07f);
        _mesh.SurfaceEnd();
    }
    private static void Segment(ImmediateMesh mesh, Vector3 a, Vector3 b)
    {
        if (a.DistanceSquaredTo(b) < .00000001f) return;
        var direction = (b - a).Normalized();
        Vector3 side = direction.Cross(Vector3.Up);
        side = side.LengthSquared() < .0001f ? Vector3.Right : side.Normalized();
        Quad(mesh, a, b, side * .009f);
        Quad(mesh, a, b, direction.Cross(side).Normalized() * .009f);
    }
    private static void Quad(ImmediateMesh mesh, Vector3 a, Vector3 b, Vector3 o)
    {
        mesh.SurfaceAddVertex(a - o); mesh.SurfaceAddVertex(b - o); mesh.SurfaceAddVertex(b + o);
        mesh.SurfaceAddVertex(a - o); mesh.SurfaceAddVertex(b + o); mesh.SurfaceAddVertex(a + o);
    }
}
