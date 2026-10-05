using Godot;
using System.Collections.Generic;
using static VisualFactory;

public partial class Hive : Node3D
{
    public bool Red;
    public int Tips;
    public float Angle;
    public float AngularVelocity;
    public float LoadTorque { get; private set; }
    // Proprietățile din clientul de referință, convertite din lb / inch în SI.
    private const float RestAngle = Mathf.Pi / 6;
    private const float BodyMass = 5.247f * .45359237f;
    private const float CenterOfMassHeight = 1.5f * .0254f;
    private const float Inertia = 1219f * .45359237f * .0254f * .0254f;
    private const float FrictionTorque = 171f * .45359237f * .0254f * .0254f;
    private const float Damper = 6834f * .45359237f * .0254f * .0254f;
    private int _upSide;
    private double _loadTime;
    public readonly List<GamePiece>[] Contents = { new(), new() };
    public Vector3 Mouth(int side) => ToGlobal(new Vector3(0, .105f, side * .48f));
    public int UpSide => _upSide;
    public float TipLoadFraction
    {
        get
        {
            float hold = BodyMass*9.81f*CenterOfMassHeight*Mathf.Abs(Mathf.Sin(Angle))+FrictionTorque;
            float downhillTorque=LoadTorque*_upSide;
            return Mathf.Clamp(downhillTorque/Mathf.Max(hold,.001f),0,1);
        }
    }
    public override void _Ready()
    {
        Angle = Red ? -.523599f : .523599f;
        _upSide = Red ? 1 : -1;
        Rotation = new(Angle, 0, 0);
        Color color = Red ? VisualFactory.Red : Blue;
        Beam(this, "Rocker", new(0, -.063f, -.56f), new(0, -.063f, .56f), .025f, Steel);
        for (int side = -1; side <= 1; side += 2)
        {
            float inner = side * .23927f, outer = side * .54508f;
            float min = Mathf.Min(inner, outer), max = Mathf.Max(inner, outer);
            Box(this, "CellFloor", new(0, -.0345f, (inner + outer) / 2), new(.508f, .006f, max - min), new("8a9299"));
            var glass = new Color(.72f, .79f, .86f, .22f);
            foreach (int end in new[] { -1, 1 })
                Box(this, "CellSide", new(end * .254f, .060f, (inner + outer) / 2), new(.006f, .193f, max - min), glass);
            Box(this, "CellBack", new(0, .060f, inner), new(.508f, .193f, .006f), glass);
            Panel(this, "PhysicalFloor", new(-.254f, -.0345f, inner), new(.254f, -.0345f, inner), new(.254f, -.0345f, outer), new(-.254f, -.0345f, outer), new("888e95"), true);
            Panel(this, "PhysicalBack", new(-.254f, -.0345f, inner), new(.254f, -.0345f, inner), new(.254f, .159f, inner), new(-.254f, .159f, inner), glass, true);
            foreach (int edge in new[] { -1, 1 })
            {
                Panel(this, "PhysicalSide", new(edge * .254f, -.0345f, inner), new(edge * .254f, -.0345f, outer), new(edge * .254f, .159f, outer), new(edge * .254f, .159f, inner), glass, true);
                Panel(this, "Roof", new(edge * .254f, .159f, inner), new(edge * .254f, .159f, outer), new(0, .321f, outer), new(0, .321f, inner), glass, true);
            }
            Vector3[] cross = { new(-.254f, -.0345f, outer), new(.254f, -.0345f, outer), new(.254f, .159f, outer), new(0, .321f, outer), new(-.254f, .159f, outer) };
            for (int i = 0; i < cross.Length; i++)
            {
                Beam(this, "CellRim", cross[i], cross[(i + 1) % cross.Length], .014f, color);
                var back = cross[i]; back.Z = inner;
                var next = cross[(i + 1) % cross.Length]; next.Z = inner;
                Beam(this, "CellBackRim", back, next, .012f, color);
                Beam(this, "CellLongRim", cross[i], back, .01f, color);
            }
            var label = Label(this, "Alliance", Red ? "RED HIVE" : "BLUE HIVE", new(0, -.075f, outer + side * .01f), color, 24);
            if (side < 0) label.RotationDegrees = new(0, 180, 0);
            // Tagurile urmăresc bascularea mecanismului; montajul 3D este aproximativ.
            int firstId = Red ? (side > 0 ? 34 : 30) : (side > 0 ? 38 : 42);
            for (int tag = 0; tag < 4; tag++)
            {
                float x = (tag - 1.5f) * .105f;
                float z = outer + side * .018f;
                float half = .05159f;
                var tagMesh = Panel(this, "AprilTag" + (firstId + tag), new(x - half, -.15f, z), new(x + half, -.15f, z),
                    new(x + half, -.15f + half * 2, z), new(x - half, -.15f + half * 2, z), Colors.White);
                string file = $"res://Assets/AprilTags/tag36_11_{firstId + tag:00000}.png";
                if (ResourceLoader.Exists(file))
                {
                    var material = Material(Colors.White, true); material.AlbedoTexture = GD.Load<Texture2D>(file);
                    material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
                    tagMesh.MaterialOverride = material;
                }
            }
        }
    }
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _loadTime+=delta;
        if (_loadTime+1e-9>=Simulation.ControlStep)
        {
            _loadTime=System.Math.Max(0,_loadTime-Simulation.ControlStep);
            foreach (var bucket in Contents)
                bucket.RemoveAll(ball => !GodotObject.IsInstanceValid(ball) || !Inside(ball));
            LoadTorque = 0;
            float cosine=Mathf.Cos(Angle), sine=Mathf.Sin(Angle);
            foreach (var bucket in Contents) foreach (var ball in bucket)
            {
                var local = ToLocal(ball.GlobalPosition);
                // Brațul greutății față de ax: poziția Z după rotație.
                float lever = local.Z * cosine + local.Y * sine;
                LoadTorque += ball.Mass * 9.81f * lever;
            }
        }
        float previousAngle=Angle;
        float torque = BodyMass * 9.81f * CenterOfMassHeight * Mathf.Sin(Angle) + LoadTorque;
        float damperStart = 24 * Mathf.Pi / 180;
        if (Mathf.Abs(Angle) > damperStart && Mathf.Sign(AngularVelocity) == Mathf.Sign(Angle))
            torque -= Damper * Mathf.Clamp((Mathf.Abs(Angle) - damperStart) / (RestAngle - damperStart), 0, 1) * AngularVelocity;
        AngularVelocity += torque / Inertia * dt;
        float frictionStep = FrictionTorque / Inertia * dt;
        AngularVelocity = Mathf.MoveToward(AngularVelocity, 0, frictionStep);
        Angle += AngularVelocity * dt;
        if (Angle > RestAngle) { Angle = RestAngle; if (AngularVelocity > 0) AngularVelocity *= -.05f; }
        if (Angle < -RestAngle) { Angle = -RestAngle; if (AngularVelocity < 0) AngularVelocity *= -.05f; }
        // Nu invalidăm transformările tuturor panourilor când unghiul e identic.
        if (Angle!=previousAngle) Rotation = new(Angle, 0, 0);
        int nextSide = Angle >= RestAngle - .00872665f ? -1 : Angle <= -RestAngle + .00872665f ? 1 : 0;
        if (nextSide != 0 && nextSide != _upSide)
        {
            _upSide = nextSide; Tips++;

        }
    }
    private static Vector3 StoredPosition(int bucket, int index)
    {
        int side = bucket == 0 ? -1 : 1;
        // Poziții de pornire; ulterior corpurile se așază prin coliziuni.
        return new((index % 5 - 2) * .085f, .013f + index / 15 * .075f, side * (.2925f + index / 5 % 3 * .083f));
    }
    private bool Inside(GamePiece ball)
    {
        var p = ToLocal(ball.GlobalPosition);
        return Mathf.Abs(p.X) < .254f + ball.Radius && Mathf.Abs(p.Z) > .22f
            && Mathf.Abs(p.Z) < .57f && p.Y > -.06f && p.Y < .34f;
    }
    public bool TryCatch(GamePiece ball)
    {
        if (ball.Stored || !Inside(ball)) return false;
        foreach (var bucket in Contents) if (bucket.Contains(ball)) return true;
        Contents[ToLocal(ball.GlobalPosition).Z < 0 ? 0 : 1].Add(ball); return true;
    }
    public void Store(GamePiece ball, int side)
    {
        int bucket = side < 0 ? 0 : 1;
        ball.GlobalPosition = ToGlobal(StoredPosition(bucket, Contents[bucket].Count));
        ball.Stored = false; ball.Freeze = false; ball.CollisionLayer = 2; ball.CollisionMask = 7;
        Contents[bucket].Add(ball);
    }
    public void RestorePose(float angle, int upSide, int tips, float angularVelocity = 0)
    { Angle = angle; _upSide = upSide; Tips = tips; AngularVelocity = angularVelocity; Rotation = new(angle, 0, 0); }
}
