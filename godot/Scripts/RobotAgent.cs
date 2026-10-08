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
    public string LastShotAttempt = "NONE";
    public ShotPlanner.Diagnosis LastShotDiagnosis;
    public int ShotsFired, ShotsMade;
    public Node3D Turret;
    public Vector3 LaunchOrigin => _turrets[Inventory.Count % _turrets.Count].GlobalPosition + Vector3.Up * .06f;
    private Node3D[] _modules = new Node3D[4];
    private float _botStuck, _botAvoid;
    private Vector3 _previous;
    private Vector3 _lastCollisionPosition;
    private bool _needsRecovery=true;
    private double _decisionTime, _intakeTime;
    private PhysicsShapeQueryParameters3D _rotationQuery;
    private BoxShape3D _rotationShape;
    public ImportedRobotDefinition Imported;
    public ImportedRobotRig Rig { get; private set; }
    public Vector3 CollisionSize { get; private set; }
    public Vector3 CollisionCenter { get; private set; }
    public Vector3 Front => -GlobalBasis.Z;
    public override void _Ready()
    {
        CollisionLayer = 4; CollisionMask = 1 | 2 | 4;
        Node3D importedVisual = null;
        CollisionSize = new(Width, .3f, Length);
        if (Imported != null)
        {
            try
            {
                var bounds = ImportedRobot.Bounds(Imported);
                RobotMechanismValidation.ValidateReady(Imported);
                importedVisual = ImportedRobot.Build(Imported);
                CollisionSize = bounds.Size; Width = bounds.Size.X; Length = bounds.Size.Z;
            }
            catch (System.Exception ex) { Game.Status = "STL unavailable; using default robot: " + ex.Message; }
        }
        CollisionCenter = new(0, CollisionSize.Y / 2, 0);
        if (importedVisual != null && Imported.Bodies.Count > 0)
        {
            var chassis = ImportedRobot.GetBodyBounds(Imported, "chassis");
            CollisionSize = chassis.Size; CollisionCenter = chassis.GetCenter();
        }
        // Leave a small contact tolerance, including for CAD chassis resting on the floor.
        _rotationShape = new BoxShape3D { Size = new Vector3(Mathf.Max(.001f,CollisionSize.X-.006f),Mathf.Max(.001f,CollisionSize.Y-.006f),Mathf.Max(.001f,CollisionSize.Z-.006f)) };
        _rotationQuery = new PhysicsShapeQueryParameters3D { Shape=_rotationShape, CollisionMask=1|4,
            Exclude=new Godot.Collections.Array<Rid> {GetRid()} };
        AddChild(new CollisionShape3D { Position = CollisionCenter, Shape = new BoxShape3D { Size = CollisionSize } });
        var color = Red ? VisualFactory.Red : Blue;
        Box(this, "Chassis", new(0, .11f, 0), new(Width, .15f, Length), new("242b34"));
        Box(this, "Deck", new(0,.192f,0),new(Width-.035f,.012f,Length-.035f),Steel);
        Box(this, "Electronics", new(0,.215f,.07f),new(.15f,.035f,.10f),new("19222e"));
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
            Box(turret, "LauncherFloor", new(0,.015f,-.10f),new(.10f,.018f,.26f),Gold);
            foreach (int sign in new[] {-1,1})
            {
                Box(turret,"LauncherRail",new(sign*.057f,.06f,-.10f),new(.012f,.07f,.26f),Gold);
                var wheel=Cylinder(turret,"Flywheel",new(sign*.061f,.06f,-.06f),.047f,.023f,new("161c25"));
                wheel.RotationDegrees=new(0,0,90);
            }
            _turrets.Add(turret);
        }
        Turret = _turrets[0];
        var plate = Label(this, "Team", $"{(Red ? "R" : "B")}{Number}", new(0, .20f, Length / 2 + .018f), Colors.White, 50);
        if (importedVisual != null)
        {
            foreach (var child in GetChildren()) if (child is Node3D visual && child is not CollisionShape3D) visual.Visible = false;
            if (Imported.Bodies.Count > 0)
            {
                importedVisual.Free();
                Rig = new ImportedRobotRig { Name = "ImportedRobotRig", Actor = this, Definition = Imported.Copy() }; AddChild(Rig);
                var excluded = new Godot.Collections.Array<Rid> { GetRid() };
                foreach (var body in Rig.Links.Values) excluded.Add(body.GetRid());
                _rotationQuery.Exclude = excluded;
            }
            else AddChild(importedVisual);
            // Keep the existing gameplay launcher anchors until joints/mechanisms are configured.
            foreach (var turret in _turrets) turret.Position = new(turret.Position.X, CollisionSize.Y + .06f, 0);
        }
        _previous = Position;
    }
    public override void _PhysicsProcess(double delta)
    {
        float dt=(float)delta;
        if (!Game.DrivingAllowed) { Velocity = Vector3.Zero; return; }
        Cooldown = Mathf.Max(0, Cooldown - dt);
        _decisionTime+=delta;
        if (_decisionTime+1e-9>=Simulation.ControlStep)
        {
            if (Bot) Think((float)_decisionTime);
            _decisionTime=0;
        }
        Vector3 desired = Command.LimitLength() * Speed;
        Vector3 velocity = Velocity; velocity.Y = 0;
        Velocity = velocity.MoveToward(desired, Acceleration * dt);
        // MoveAndSlide verifică întregul traseu al translației, nu doar poziția finală.
        var position=Position;
        // Un robot oprit nu repetă sweep-ul; resetarea/teleportarea cere recuperare.
        if (Velocity.LengthSquared()>0 || _needsRecovery || position!=_lastCollisionPosition)
        {
            MoveAndSlide(); position=Position; _needsRecovery=false;
            for (int contact=0; contact<GetSlideCollisionCount(); contact++)
            {
                var hit=GetSlideCollision(contact);
                if (hit.GetCollider() is GamePiece ball && !ball.Stored && !ball.Freeze)
                {
                    var direction=-hit.GetNormal(); direction.Y=0; direction=direction.Normalized();
                    float closing=desired.Dot(direction)-ball.LinearVelocity.Dot(direction);
                    if (closing>0) ball.ApplyCentralImpulse(direction*closing*ball.Mass);
                }
            }
        }
        // Verificăm volumul orientat înainte de a accepta rotația.
        if (Mathf.Abs(TurnCommand) > .0001f)
        {
            float angle=TurnCommand*TurnSpeed*dt;
            var candidate=GlobalTransform;
            candidate.Basis=candidate.Basis.Rotated(Vector3.Up,angle);
            candidate.Origin+=candidate.Basis*CollisionCenter;
            _rotationQuery.Transform=candidate;
            // Verificăm candidatul înainte de a modifica corpul și toate mesh-urile.
            if (GetWorld3D().DirectSpaceState.IntersectShape(_rotationQuery,1).Count==0)
                RotateY(angle);
        }
        if (position.Y!=0) { position.Y=0; Position=position; }
        _lastCollisionPosition=position;
        _intakeTime+=delta;
        if (_intakeTime+1e-9>=Simulation.ControlStep)
        {
            _intakeTime=System.Math.Max(0,_intakeTime-Simulation.ControlStep);
            Collect();
        }
        if (FireCommand) Fire();
    }
    public override void _Process(double delta)
    {
        if (!Game.Running) return;
        var local = GlobalBasis.Inverse() * Velocity;
        foreach (var module in _modules)
        {
            var wheelVelocity=local+new Vector3(TurnCommand*module.Position.Z,0,-TurnCommand*module.Position.X);
            if (wheelVelocity.LengthSquared()>.01f) module.Rotation=new(0,Mathf.Atan2(-wheelVelocity.X,-wheelVelocity.Z),0);
        }
        AimTurrets();
    }
    private void AimTurrets()
    {
        var target=Game.Target(this);
        foreach (var turret in _turrets)
            if ((target-turret.GlobalPosition).LengthSquared()>.01f) turret.LookAt(new(target.X,turret.GlobalPosition.Y,target.Z));
    }
    private void Collect()
    {
        if (Intake && Inventory.Count < Capacity)
        {
            foreach (var flower in Game.Flowers)
            {
                var offset = ToLocal(flower.Base);
                bool inReach = (offset.Z < -.05f && offset.Z > -Length/2-.20f)
                    || (IntakeCount>1 && offset.Z>.05f && offset.Z<Length/2+.20f);
                if (flower.Balls.Count>0 && Mathf.Abs(offset.X)<Width*.6f && inReach)
                {
                    var ball=flower.ReleaseBottom();
                    if (ball!=null) { Inventory.Add(ball.Kind); Game.RemoveBall(ball); }
                    break;
                }
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
    }
    public void Fire()
    {
        if (Inventory.Count==0) { ShotStatus="NO PIECE IN ROBOT"; return; }
        if (!Game.DrivingAllowed || Cooldown > 0) return;
        AimTurrets();
        var origin = LaunchOrigin;
        var target = Game.Target(this);
        float radius = Inventory[0] == PieceKind.Pollen ? .03556f : .04572f;
        bool clear=ShotPlanner.TrySolve(this,origin,target,radius,out var plan,out var diagnosis);
        LastShotDiagnosis=diagnosis;
        if (!clear)
        {
            ShotStatus = LastShotAttempt=diagnosis.Message;
            // Nu consumăm inventarul; încercăm din nou după o scurtă pauză.
            Cooldown = .15f; return;
        }
        ShotStatus = LastShotAttempt="LAUNCHED";
        var ball = Game.SpawnBall(Inventory[0], origin); Inventory.RemoveAt(0);
        ExcludeLauncher(ball);
        ball.ShotRobotIndex=Game.Robots.IndexOf(this); ShotsFired++;
        ball.LinearVelocity = plan.Velocity;
        Cooldown = .45f / TurretCount;
    }
public void UpdateShotReadiness(bool clear, ShotPlanner.Diagnosis diagnosis)
{
    if (Inventory.Count == 0) { ShotStatus = "NO PIECE IN ROBOT"; return; }
    if (Cooldown > 0 && ShotStatus == "LAUNCHED") return;
    ShotStatus = clear ? "READY" : diagnosis.Message;
}

public void ExcludeLauncher(GamePiece ball)
{
    ball.IgnoreLauncherBriefly(this);
}

    private void Think(float dt)
    {
        Intake = true; FireCommand = false;
        Vector3 current=GlobalPosition, goal=current;
        Vector3? collectionTarget=null;
        if (Inventory.Count > 0)
        {
            goal = new(Red ? .8f : Arena.Size - .8f, 0, Number % 2 == 1 ? -.8f : -Arena.Size + .8f);
            FireCommand = current.DistanceTo(goal) < .18f;
        }
        else
        {
            float best = float.MaxValue;
            foreach (var ball in Game.Balls)
            {
                if (ball.Stored || Game.Flowers.Exists(f=>f.Balls.Contains(ball))) continue;
                var position=ball.Position;
                float distance=position.DistanceSquaredTo(current);
                if (position.Y<.15f && distance<best) { best=distance; goal=position; }
            }
            if (best==float.MaxValue)
                foreach (var flower in Game.Flowers)
                {
                    if (flower.Balls.Count==0) continue;
                    var approach=flower.Base+flower.Inward*(Length/2+.10f);
                    float distance=approach.DistanceSquaredTo(current);
                    if (distance<best) { best=distance; goal=approach; collectionTarget=flower.Base; }
                }
        }
        Vector3 move = goal - current; move.Y = 0;
        _botStuck = current.DistanceTo(_previous) < .12f*dt && move.Length() > .15f ? _botStuck + dt : 0;
        if (_botStuck > .5f) { _botAvoid = 1.2f; _botStuck = 0; }
        if (_botAvoid > 0) { _botAvoid -= dt; move = move.Rotated(Vector3.Up, Mathf.Pi / 2); }
        Command = move.Length() < .1f ? Vector3.Zero : move.Normalized();
        var facing=collectionTarget.HasValue && move.Length()<.15f ? collectionTarget.Value-current : move;
        float heading = Mathf.Atan2(-facing.X, -facing.Z);
        TurnCommand = Mathf.Clamp(Mathf.AngleDifference(Rotation.Y, heading) * 2, -1, 1);
        _previous = current;
    }
}
