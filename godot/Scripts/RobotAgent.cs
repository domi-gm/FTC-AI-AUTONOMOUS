using Godot;
using System.Collections.Generic;
using static VisualFactory;

public partial class RobotAgent : CharacterBody3D
{
    public Simulation Game;
    public bool Red = true;
    public int Number = 1;
    public bool Bot;
    public float Speed = 1.5f, Acceleration = 3f, TurnSpeed = 2.8f;
    public float Width = .4572f, Length = .4572f;
    public int TurretCount = 1, IntakeCount = 1;
    private readonly List<Node3D> _turrets = new();
    public readonly List<PieceKind> Inventory = new();
    public int Capacity = 4;
    public bool Intake;
    public Vector3 Command;
    public float TurnCommand;
    public bool FireCommand;
    public float Cooldown;
    public string ShotStatus = "READY";
    public Node3D Turret;
    public Vector3 LaunchOrigin => _turrets[Inventory.Count % _turrets.Count].GlobalPosition + Vector3.Up * .06f;
    private Node3D[] _modules = new Node3D[4];
    private float _botStuck, _botAvoid;
    private Vector3 _previous;
    public Vector3 Front => -GlobalBasis.Z;
    public override void _Ready()
    {
        CollisionLayer = 4; CollisionMask = 1 | 4;
        AddChild(new CollisionShape3D { Position = new(0, .15f, 0), Shape = new BoxShape3D { Size = new(Width, .3f, Length) } });
        var color = Red ? VisualFactory.Red : Blue;
        Box(this, "Chassis", new(0, .11f, 0), new(Width, .15f, Length), new("242b34"));
        foreach (int s in new[] { -1, 1 })
        {
            Box(this, "BumperSide", new(s * Width / 2, .19f, 0), new(.028f, .12f, Length), color);
            Box(this, "BumperEnd", new(0, .19f, s * Length / 2), new(Width, .12f, .028f), color);
        }
        int i = 0;
        foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
        {
            var module = new Node3D { Position = new(x * (Width / 2 - .05f), .055f, z * (Length / 2 - .065f)) };
            AddChild(module); _modules[i++] = module;
            var wheel = Cylinder(module, "SwerveWheel", Vector3.Zero, .054f, .035f, new("11141a"));
            wheel.RotationDegrees = new(0, 0, 90);
            Box(module, "WheelStripe", new(0, .054f, 0), new(.035f, .003f, .022f), Gold);
        }
        Box(this, "Intake", new(0, .055f, -Length / 2 - .035f), new(Width * .8f, .08f, .09f), Gold);
        if (IntakeCount > 1) Box(this, "RearIntake", new(0, .055f, Length / 2 + .035f), new(Width * .8f, .08f, .09f), Gold);
        foreach (int s in new[] { -1, 1 }) Box(this, "Tower", new(s * .12f, .29f, .06f), new(.024f, .30f, .024f), Steel);
        for (int j = 0; j < TurretCount; j++)
        {
            var turret = new Node3D { Name = "Turret" + j, Position = new((j - (TurretCount - 1) / 2f) * Width / TurretCount, .43f, 0) }; AddChild(turret);
            Cylinder(turret, "Turntable", Vector3.Zero, .12f / Mathf.Sqrt(TurretCount), .045f, Steel);
            var barrel = Box(turret, "Launcher", new(0, .06f, -.10f), new(.10f, .12f, .26f), Gold);
            barrel.RotationDegrees = new(-25, 0, 0); _turrets.Add(turret);
        }
        Turret = _turrets[0];
        var plate = Label(this, "Team", $"{(Red ? "R" : "B")}{Number}", new(0, .20f, Length / 2 + .018f), Colors.White, 50);
        _previous = Position;
    }
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (!Game.Running) { Velocity = Vector3.Zero; return; }
        Cooldown = Mathf.Max(0, Cooldown - dt);
        if (Bot) Think(dt);
        Vector3 desired = Command.LimitLength() * Speed;
        Vector3 velocity = Velocity; velocity.Y = 0;
        Velocity = velocity.MoveToward(desired, Acceleration * dt);
        // MoveAndSlide verifică întregul traseu al translației, nu doar poziția finală.
        MoveAndSlide();
        // Verificăm volumul orientat înainte de a accepta rotația.
        float previousYaw = Rotation.Y;
        Rotation = new(0, previousYaw + TurnCommand * TurnSpeed * dt, 0);
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new BoxShape3D { Size = new(Width - .006f, .294f, Length - .006f) },
            Transform = GlobalTransform * new Transform3D(Basis.Identity, new Vector3(0, .15f, 0)),
            CollisionMask = 1 | 4, Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };
        if (GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count > 0) Rotation = new(0, previousYaw, 0);
        Position = new(Position.X, 0, Position.Z);
        foreach (var module in _modules)
        {
            var local = GlobalBasis.Inverse() * Velocity;
            local += new Vector3(TurnCommand * module.Position.Z, 0, -TurnCommand * module.Position.X);
            if (local.LengthSquared() > .01f) module.Rotation = new(0, Mathf.Atan2(-local.X, -local.Z), 0);
        }
        var target = Game.Target(this);
        foreach (var turret in _turrets)
            if ((target - turret.GlobalPosition).LengthSquared() > .01f) turret.LookAt(new(target.X, turret.GlobalPosition.Y, target.Z));
        if (Intake && Inventory.Count < Capacity)
        {
            foreach (var flower in Game.Flowers)
            {
                var offset = ToLocal(flower.Base);
                if (flower.Balls.Count > 0 && Mathf.Abs(offset.X) < Width * .6f && offset.Z < -.05f && offset.Z > -Length / 2 - .20f)
                { var ball = flower.ReleaseBottom(); Inventory.Add(ball.Kind); Game.RemoveBall(ball); break; }
            }
            foreach (var ball in Game.Balls)
            {
                if (Inventory.Count >= Capacity) break;
                if (ball.Stored || ball.Position.Y > .18f) continue;
                var p = ToLocal(ball.GlobalPosition);
                if (Mathf.Abs(p.X) < Width * .6f && ((p.Z < -.05f && p.Z > -Length / 2 - .2f) || (IntakeCount > 1 && p.Z > .05f && p.Z < Length / 2 + .2f)))
                { Inventory.Add(ball.Kind); Game.RemoveBall(ball); break; }
            }
        }
        if (FireCommand) Fire();
    }
    public void Fire()
    {
        if (Cooldown > 0 || Inventory.Count == 0) return;
        var origin = LaunchOrigin;
        var target = Game.Target(this);
        float radius = Inventory[0] == PieceKind.Pollen ? .03556f : .04572f;
        if (!ShotPlanner.TryPlan(this, origin, target, radius, out Vector3 launch))
        {
            ShotStatus = "BLOCKED / NO TRAJECTORY";
            // Nu consumăm inventarul; încercăm din nou după o scurtă pauză.
            Cooldown = .15f; return;
        }
        ShotStatus = "LAUNCHED";
        var ball = Game.SpawnBall(Inventory[0], origin); Inventory.RemoveAt(0);
        ball.LinearVelocity = launch;
        Cooldown = .45f / TurretCount;
    }
    private void Think(float dt)
    {
        var target = Game.Target(this);
        Intake = true; FireCommand = false;
        Vector3 goal = GlobalPosition;
        if (Inventory.Count > 0)
        {
            goal = new(Red ? .8f : Arena.Size - .8f, 0, Number % 2 == 1 ? -.8f : -Arena.Size + .8f);
            FireCommand = GlobalPosition.DistanceTo(goal) < .18f;
        }
        else
        {
            float best = float.MaxValue;
            foreach (var ball in Game.Balls)
                if (!ball.Stored && ball.Position.Y < .15f && ball.Position.DistanceSquaredTo(GlobalPosition) < best)
                { best = ball.Position.DistanceSquaredTo(GlobalPosition); goal = ball.Position; }
        }
        Vector3 move = goal - GlobalPosition; move.Y = 0;
        _botStuck = GlobalPosition.DistanceTo(_previous) < .001f && move.Length() > .15f ? _botStuck + dt : 0;
        if (_botStuck > .5f) { _botAvoid = 1.2f; _botStuck = 0; }
        if (_botAvoid > 0) { _botAvoid -= dt; move = move.Rotated(Vector3.Up, Mathf.Pi / 2); }
        Command = move.Length() < .1f ? Vector3.Zero : move.Normalized();
        float heading = Mathf.Atan2(-move.X, -move.Z);
        TurnCommand = Mathf.Clamp(Mathf.AngleDifference(Rotation.Y, heading) * 2, -1, 1);
        _previous = GlobalPosition;
    }
}
