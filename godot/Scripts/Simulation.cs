using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class Simulation : Node3D
{
    public readonly List<GamePiece> Balls = new();
    public readonly List<RobotAgent> Robots = new();
    public Hive RedHive, BlueHive;
    public RobotAgent Player => Robots.Count > 0 ? Robots[0] : null;
    public OrbitCamera Camera;
    public SimulatorHud Hud;
    public PracticePath PathEditor;
    public AimPreview Aim;
    public bool Started, Paused, Practice = true, TwoPlayers;
    public bool Running => Started && !Paused && (Practice || Elapsed < 158);
    public float Elapsed;
    public int RedScore, BlueScore;
    public string Status = "Godot C# / local prototype";
    public bool AimFlower;
    public RobotProfile Profile = RobotProfile.Load();
    public bool Testing;
    public int FlowerRed, FlowerBlue;
    private Node3D _session;
    private readonly Dictionary<GamePiece, (Vector3 Linear, Vector3 Angular)> _pausedVelocities = new();
    private readonly Random _random = new(14712);
    private readonly Vector3[] _flowers = { VisualFactory.Inch(3.9f, Arena.Inches * 2 / 6, 21.5f), VisualFactory.Inch(Arena.Inches * 2 / 6, Arena.Inches - 3.9f, 21.5f), VisualFactory.Inch(Arena.Inches - 3.9f, Arena.Inches * 4 / 6, 21.5f), VisualFactory.Inch(Arena.Inches * 4 / 6, 3.9f, 21.5f) };
    public readonly List<FlowerColumn> Flowers = new();
    public readonly List<GamePiece>[] HumanStock = { new(), new() };
    public readonly int[] HumanTokens = { 0, 0 };
    private int _redLastTips, _blueLastTips;
    public override void _Ready()
    {
        AddChild(new Arena { Name = "Arena" });
        Camera = new OrbitCamera { Game = this, Current = true, Fov = 46, Far = 50, Near = .02f, Name = "Camera" }; AddChild(Camera);
        Hud = new SimulatorHud { Game = this }; AddChild(Hud);
        PathEditor = new PracticePath { Game = this, Name = "PracticePath" }; AddChild(PathEditor);
        Aim = new AimPreview { Game = this, Name = "AimPreview" }; AddChild(Aim);
        Reset(false);
        Hud.ShowMenu();
        if (OS.GetCmdlineUserArgs().Contains("--smoke-test")) CallDeferred(MethodName.SmokeTest);
    }
    public void Reset(bool start = true)
    {
        if (_session != null) { RemoveChild(_session); _session.QueueFree(); }
        Balls.Clear(); Robots.Clear(); _pausedVelocities.Clear(); Elapsed = 0; Paused = false; Started = start;
        if (PathEditor != null) PathEditor.Following = false;
        FlowerRed = FlowerBlue = 0; Flowers.Clear(); HumanStock[0].Clear(); HumanStock[1].Clear();
        HumanTokens[0] = HumanTokens[1] = _redLastTips = _blueLastTips = 0;
        _session = new Node3D { Name = "Session" }; AddChild(_session);
        RedHive = new Hive { Red = true, Position = VisualFactory.Inch(Arena.Inches / 2 - 12.75f, Arena.Inches / 2, 43.95f), Name = "RedHive" };
        BlueHive = new Hive { Red = false, Position = VisualFactory.Inch(Arena.Inches / 2 + 12.75f, Arena.Inches / 2, 43.95f), Name = "BlueHive" };
        _session.AddChild(RedHive); _session.AddChild(BlueHive);
        var starts = new[] { new Vector3(.84f, 0, -.26f), new Vector3(.28f, 0, -2.75f), new Vector3(Arena.Size - .84f, 0, -Arena.Size + .26f), new Vector3(Arena.Size - .28f, 0, -.84f) };
        for (int i = 0; i < 4; i++)
        {
            var robot = new RobotAgent { Game = this, Red = i < 2, Number = i % 2 + 1, Bot = i > 0 && !(TwoPlayers && i == 2), Position = starts[i], Name = $"Robot{i + 1}" };
            if (i == 0) { robot.Width = Profile.WidthCm / 100; robot.Length = Profile.LengthCm / 100; robot.Speed = Profile.Speed; robot.Acceleration = Profile.Acceleration; robot.TurnSpeed = Profile.TurnSpeed; robot.TurretCount = Profile.Turrets; robot.IntakeCount = Profile.Intakes; }
            robot.Rotation = new(0, i >= 2 ? Mathf.Pi : 0, 0);
            _session.AddChild(robot); Robots.Add(robot);
            for (int j = 0; j < 4; j++) robot.Inventory.Add(PieceKind.Pollen);
        }
        // 40 POLLEN + 8 NECTAR roșii + 8 NECTAR albastre, inclusiv inventarele.
        for (int alliance = 0; alliance < 2; alliance++)
        {
            bool red = alliance == 0;
            for (int i = 0; i < 4; i++)
            {
                float x = .03683f + i * .071628f;
                float y = .03683f;
                if (!red) { x = Arena.Size - x; y = Arena.Size - y; }
                SpawnBall(PieceKind.Pollen, Arena.World(x, y, .04f));
            }
            for (int i = 0; i < 5; i++)
            {
                var ball = SpawnBall(red ? PieceKind.RedNectar : PieceKind.BlueNectar,
                    Arena.World(red ? -.2286f - i % 3 * .0965f : Arena.Size + .2286f + i % 3 * .0965f,
                        (red ? Arena.Size * 4.5f / 6 : Arena.Size * 1.5f / 6) + (i / 3 - 1) * .0965f, .7419f));
                ball.Stored = true; ball.Freeze = true; ball.CollisionLayer = 0; ball.CollisionMask = 0;
                HumanStock[alliance].Add(ball);
            }
            var hive = red ? RedHive : BlueHive;
            for (int i = 0; i < 3; i++) hive.Store(SpawnBall(red ? PieceKind.RedNectar : PieceKind.BlueNectar, hive.Position), hive.UpSide);
        }
        Vector3[] inward = { Vector3.Right, Vector3.Back, Vector3.Left, Vector3.Forward };
        for (int i = 0; i < 4; i++)
        {
            var column = new FlowerColumn { Base = new(_flowers[i].X, 0, _flowers[i].Z), Inward = inward[i] };
            Flowers.Add(column);
            for (int j = 0; j < 4; j++) column.Store(SpawnBall(PieceKind.Pollen, column.Base));
        }
        UpdateScore();
        if (!start) SetFrozen(true);
    }
    public GamePiece SpawnBall(PieceKind kind, Vector3 position)
    {
        var ball = new GamePiece { Kind = kind, Position = position, Name = "Ball" };
        _session.AddChild(ball, true); Balls.Add(ball); return ball;
    }
    public void RemoveBall(GamePiece ball) { Balls.Remove(ball); ball.QueueFree(); }
    public Vector3 Target(RobotAgent robot)
    {
        if (AimFlower && !robot.Bot) return _flowers.Where((p, i) => robot.Red ? i < 2 : i >= 2).OrderBy(p => p.DistanceSquaredTo(robot.Position)).First() + Vector3.Up * .05f;
        var hive = robot.Red ? RedHive : BlueHive;
        // 4.04 inch deasupra axei locale și 4.5 inch în interiorul gurii,
        // ca punctul de țintire din clientul de referință.
        return hive.ToGlobal(new Vector3(0, .102616f, hive.UpSide * .430784f));
    }
    public override void _PhysicsProcess(double delta)
    {
        if (!Running) return;
        Elapsed += (float)delta;
        if (!Practice && Elapsed >= 158) { SetFrozen(true); Hud.ShowResults(); return; }
        bool transition = !Practice && Elapsed >= 30 && Elapsed < 38;
        foreach (var robot in Robots) robot.SetPhysicsProcess(!transition);
        if (!transition)
        {
            if (!Testing) { ReadPlayer(Player, false); if (TwoPlayers) ReadPlayer(Robots[2], true); }
        }
        foreach (var flower in Flowers) flower.Update();
        foreach (var ball in Balls.ToArray())
        {
            if (ball.Stored) continue;
            if (ball.Position.Y < -1 || Mathf.Abs(ball.Position.X) > 20 || Mathf.Abs(ball.Position.Z) > 20)
            { ball.Position = Arena.World(.15f, .15f, ball.Radius + .01f); ball.LinearVelocity = Vector3.Zero; }
            if (RedHive.TryCatch(ball) || BlueHive.TryCatch(ball)) continue;
            foreach (var flower in Flowers) if (flower.TryCatch(ball)) break;
        }
        HumanTokens[0] += RedHive.Tips - _redLastTips; HumanTokens[1] += BlueHive.Tips - _blueLastTips;
        _redLastTips = RedHive.Tips; _blueLastTips = BlueHive.Tips;
        UpdateScore();
    }
    private void ReadPlayer(RobotAgent robot, bool second)
    {
        if (!Practice && Elapsed < 30) { robot.Bot = true; return; }
        robot.Bot = false;
        bool Held(Key key) => Input.IsPhysicalKeyPressed(key);
        var v = second ? new Vector3((Held(Key.Right) ? 1 : 0) - (Held(Key.Left) ? 1 : 0), 0, (Held(Key.Down) ? 1 : 0) - (Held(Key.Up) ? 1 : 0))
            : new Vector3((Held(Key.D) ? 1 : 0) - (Held(Key.A) ? 1 : 0), 0, (Held(Key.S) ? 1 : 0) - (Held(Key.W) ? 1 : 0));
        robot.Command = Camera.ToGroundMovement(new Vector2(v.X, v.Z));
        if (!second && Practice && PathEditor.Following)
        {
            if (v.LengthSquared() > 0) PathEditor.Following = false;
            else robot.Command = PathEditor.Command();
        }
        robot.TurnCommand = second ? (Held(Key.Comma) ? 1 : 0) - (Held(Key.Period) ? 1 : 0) : (Held(Key.Q) ? 1 : 0) - (Held(Key.E) ? 1 : 0);
        robot.Intake = Held(second ? Key.Enter : Key.Shift) || (!second && Held(Key.J));
        robot.FireCommand = Held(second ? Key.Slash : Key.Space);
        int pad = second ? 1 : 0;
        if (Input.GetConnectedJoypads().Contains(pad))
        {
            Vector2 axes = new(Input.GetJoyAxis(pad, JoyAxis.LeftX), Input.GetJoyAxis(pad, JoyAxis.LeftY));
            if (axes.Length() > .15f)
            {
                if (!second) PathEditor.Following = false;
                robot.Command = Camera.ToGroundMovement(axes);
            }
            float turn = Input.GetJoyAxis(pad, JoyAxis.RightX); if (Mathf.Abs(turn) > .15f) robot.TurnCommand = -turn;
            robot.Intake |= Input.IsJoyButtonPressed(pad, JoyButton.LeftShoulder);
            robot.FireCommand |= Input.IsJoyButtonPressed(pad, JoyButton.RightShoulder);
        }
    }
    private void UpdateScore()
    {
        int Score(Hive hive, bool red)
        {
            int score = hive.Tips * 20;
            for (int side = 0; side < 2; side++) foreach (var ball in hive.Contents[side])
                score += ball.Kind == PieceKind.Pollen ? 2 : ball.Kind == (red ? PieceKind.RedNectar : PieceKind.BlueNectar) ? 5 : 0;
            foreach (var flower in Flowers) if (flower.Owner == (red ? PieceKind.RedNectar : PieceKind.BlueNectar)) score += 2 * flower.ScoringCount;
            return score;
        }
        RedScore = Score(RedHive, true); BlueScore = Score(BlueHive, false);
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            switch (k.PhysicalKeycode)
            {
                case Key.Escape: TogglePause(); break;
                case Key.C: Camera.Mode = (Camera.Mode + 1) % 3; break;
                case Key.R: Reset(); break;
                case Key.T: AimFlower = !AimFlower; break;
                case Key.B: if (Practice) SpawnBall(PieceKind.Pollen, Player.Position + Player.Front * .5f + Vector3.Up * .12f); break;
                case Key.N: if (Practice) SpawnBall(Player.Red ? PieceKind.RedNectar : PieceKind.BlueNectar, Player.Position + Player.Front * .5f + Vector3.Up * .12f); break;
                case Key.H: DropHuman(Player.Red); break;
                case Key.K: KnockFlower(Player); break;
                case Key.F5: Save(); break;
                case Key.F9: Load(); break;
            }
        }
    }
    public void DropHuman(bool red)
    {
        int i = red ? 0 : 1;
        if (!Running || HumanStock[i].Count == 0) return;
        if (!Practice && Elapsed < 98 && HumanTokens[i] <= 0) { Status = "NECTAR locked: tip HIVE or wait for last 60 seconds"; return; }
        if (!Practice && Elapsed < 98) HumanTokens[i]--;
        var ball = HumanStock[i][0]; HumanStock[i].RemoveAt(0);
        ball.Position = Arena.World(red ? .127f : Arena.Size - .127f, red ? Arena.Size * .75f : Arena.Size * .25f, .38f);
        ball.Stored = false; ball.Freeze = false; ball.CollisionLayer = 2; ball.CollisionMask = 7;
        ball.LinearVelocity = new(red ? .45f : -.45f, 0, 0);
    }
    public void KnockFlower(RobotAgent robot)
    {
        var column = Flowers.OrderBy(f => f.Base.DistanceSquaredTo(robot.Position)).First();
        if (column.Base.DistanceTo(robot.Position) < .5f) column.ReleaseBottom();
    }
    public void TogglePause()
    {
        if (!Started) return;
        Paused = !Paused;
        SetFrozen(Paused);
        Hud.ShowPause(Paused);
    }
    public void SetFrozen(bool frozen)
    {
        _session.ProcessMode = frozen ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        foreach (var ball in Balls) if (!ball.Stored)
        {
            if (frozen && !_pausedVelocities.ContainsKey(ball)) _pausedVelocities[ball] = (ball.LinearVelocity, ball.AngularVelocity);
            ball.Freeze = frozen;
            if (!frozen && _pausedVelocities.Remove(ball, out var velocities)) { ball.LinearVelocity = velocities.Linear; ball.AngularVelocity = velocities.Angular; }
        }
    }
    public void Save()
    {
        var snapshot = Capture();
        using var file = Godot.FileAccess.Open("user://practice.json", Godot.FileAccess.ModeFlags.Write); file.StoreString(JsonSerializer.Serialize(snapshot));
        Status = "Scene saved: pieces, inventories, HIVE, flowers, timer";
    }
    public void Load()
    {
        if (!Godot.FileAccess.FileExists("user://practice.json")) { Status = "No saved scene"; return; }
        try
        {
            var data = JsonSerializer.Deserialize<Snapshot>(Godot.FileAccess.GetFileAsString("user://practice.json"));
            Restore(data);
            Status = "Scene restored";
        }
        catch (Exception ex) { Status = "Cannot load: " + ex.Message; }
    }
    public Snapshot Capture()
    {
        string Container(GamePiece ball, out int slot)
        {
            for (int i = 0; i < 2; i++)
            {
                if ((slot = RedHive.Contents[i].IndexOf(ball)) >= 0) return "red:" + i;
                if ((slot = BlueHive.Contents[i].IndexOf(ball)) >= 0) return "blue:" + i;
                if ((slot = HumanStock[i].IndexOf(ball)) >= 0) return "human:" + i;
            }
            for (int i = 0; i < Flowers.Count; i++) if ((slot = Flowers[i].Balls.IndexOf(ball)) >= 0) return "flower:" + i;
            slot = 0; return "free";
        }
        return new Snapshot
        {
            Version = 2, Practice = Practice, Time = Elapsed, TwoPlayers = TwoPlayers, Profile = Profile,
            RedAngle = RedHive.Angle, BlueAngle = BlueHive.Angle, RedSide = RedHive.UpSide, BlueSide = BlueHive.UpSide,
            RedAngularVelocity = RedHive.AngularVelocity, BlueAngularVelocity = BlueHive.AngularVelocity,
            RedTips = RedHive.Tips, BlueTips = BlueHive.Tips, Tokens = (int[])HumanTokens.Clone(),
            Robots = Robots.Select(r => new RobotData { X = r.Position.X, Z = r.Position.Z, Yaw = r.Rotation.Y, Inventory = r.Inventory.ToArray(),
                Vx = r.Velocity.X, Vz = r.Velocity.Z, Cooldown = r.Cooldown, Speed = r.Speed, Acceleration = r.Acceleration, TurnSpeed = r.TurnSpeed }).ToArray(),
            Balls = Balls.Select(b => {
                string container = Container(b, out int slot);
                var linear = _pausedVelocities.TryGetValue(b, out var motion) ? motion.Linear : b.LinearVelocity;
                var angular = _pausedVelocities.TryGetValue(b, out motion) ? motion.Angular : b.AngularVelocity;
                return new BallData { Kind = b.Kind, X = b.Position.X, Y = b.Position.Y, Z = b.Position.Z,
                    Vx = linear.X, Vy = linear.Y, Vz = linear.Z, Wx = angular.X, Wy = angular.Y, Wz = angular.Z, Container = container, Slot = slot };
            }).ToArray()
        };
    }
    public void Restore(Snapshot data)
    {
        if (data == null || data.Version != 2 || data.Robots?.Length != 4 || data.Balls == null || data.Tokens?.Length != 2) throw new Exception("Unsupported snapshot");
        foreach (var b in data.Balls)
            if (!float.IsFinite(b.X + b.Y + b.Z + b.Vx + b.Vy + b.Vz) || Math.Abs(b.X) > 30 || Math.Abs(b.Y) > 30 || Math.Abs(b.Z) > 30) throw new Exception("Invalid ball position");
        Practice = data.Practice; TwoPlayers = data.TwoPlayers; Profile = data.Profile ?? new RobotProfile(); Profile.Validate(); Reset(); Elapsed = data.Time;
        for (int i = 0; i < 4; i++)
        {
            var d = data.Robots[i]; var r = Robots[i]; r.Position = new(d.X, 0, d.Z); r.Rotation = new(0, d.Yaw, 0);
            r.Inventory.Clear(); r.Inventory.AddRange(d.Inventory); r.Velocity = new(d.Vx, 0, d.Vz); r.Cooldown = d.Cooldown;
            r.Speed = d.Speed; r.Acceleration = d.Acceleration; r.TurnSpeed = d.TurnSpeed;
        }
        RedHive.RestorePose(data.RedAngle, data.RedSide, data.RedTips, data.RedAngularVelocity); BlueHive.RestorePose(data.BlueAngle, data.BlueSide, data.BlueTips, data.BlueAngularVelocity);
        _redLastTips = data.RedTips; _blueLastTips = data.BlueTips; Array.Copy(data.Tokens, HumanTokens, 2);
        foreach (var ball in Balls.ToArray()) RemoveBall(ball);
        foreach (var list in RedHive.Contents.Concat(BlueHive.Contents)) list.Clear();
        foreach (var f in Flowers) f.Balls.Clear(); foreach (var stock in HumanStock) stock.Clear();
        foreach (var b in data.Balls.OrderBy(b => b.Slot))
        {
            var ball = SpawnBall(b.Kind, new(b.X, b.Y, b.Z));
            if (b.Container != "free")
            {
                string[] parts = b.Container.Split(':'); int index = int.Parse(parts[1]);
                if (parts[0] == "red") RedHive.Store(ball, index == 0 ? -1 : 1);
                else if (parts[0] == "blue") BlueHive.Store(ball, index == 0 ? -1 : 1);
                else if (parts[0] == "flower") Flowers[index].Store(ball);
                else if (parts[0] == "human") { HumanStock[index].Add(ball); ball.Stored = true; ball.Freeze = true; ball.CollisionLayer = 0; ball.CollisionMask = 0; }
            }
            if (!ball.Stored) { ball.Position = new(b.X,b.Y,b.Z); ball.LinearVelocity = new(b.Vx, b.Vy, b.Vz); ball.AngularVelocity = new(b.Wx, b.Wy, b.Wz); }
        }
        UpdateScore();
    }
    public sealed class Snapshot
    {
        public int Version { get; set; } public bool Practice { get; set; } public bool TwoPlayers { get; set; } public float Time { get; set; }
        public RobotProfile Profile { get; set; }
        public float RedAngle { get; set; } public float BlueAngle { get; set; } public int RedSide { get; set; } public int BlueSide { get; set; }
        public float RedAngularVelocity { get; set; } public float BlueAngularVelocity { get; set; }
        public int RedTips { get; set; } public int BlueTips { get; set; } public int[] Tokens { get; set; }
        public RobotData[] Robots { get; set; } public BallData[] Balls { get; set; }
    }
    public sealed class RobotData { public float X { get; set; } public float Z { get; set; } public float Yaw { get; set; } public PieceKind[] Inventory { get; set; }
        public float Vx { get; set; } public float Vz { get; set; } public float Cooldown { get; set; } public float Speed { get; set; } public float Acceleration { get; set; } public float TurnSpeed { get; set; } }
    public sealed class BallData { public PieceKind Kind { get; set; } public float X { get; set; } public float Y { get; set; } public float Z { get; set; }
        public float Vx { get; set; } public float Vy { get; set; } public float Vz { get; set; } public float Wx { get; set; } public float Wy { get; set; } public float Wz { get; set; }
        public string Container { get; set; } public int Slot { get; set; } }
    public async void SmokeTest()
    {
        try
        {
            void Check(bool condition, string label) { if (!condition) throw new Exception(label); GD.Print("PASS: " + label); }
            async System.Threading.Tasks.Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
            int Count() => Balls.Count + Robots.Sum(r => r.Inventory.Count);
            Testing = true; Practice = true; Reset(); foreach (var r in Robots) r.Bot = false;
            var oldCameraTransform = Camera.GlobalTransform;
            var oldProjection = Camera.Projection;
            Vector3 center = Arena.World(Arena.Center, Arena.Center);
            for (int angle = 0; angle < 4; angle++)
            {
                float yaw = angle * Mathf.Pi / 2;
                Camera.Projection = Camera3D.ProjectionType.Perspective;
                Camera.Position = center + new Vector3(Mathf.Sin(yaw) * 4, 3, Mathf.Cos(yaw) * 4);
                Camera.LookAt(center);
                Vector3 up = Camera.ToGroundMovement(new(0, -1));
                Vector3 right = Camera.ToGroundMovement(new(1, 0));
                var screen = Camera.UnprojectPosition(center);
                Check(Camera.UnprojectPosition(center + up * .1f).Y < screen.Y && Camera.UnprojectPosition(center + right * .1f).X > screen.X,
                    $"Camera-relative W/D at {angle * 90} degrees");
                Check(Mathf.Abs(up.Y) < .00001f && Mathf.Abs(right.Y) < .00001f && Mathf.Abs(Camera.ToGroundMovement(new(1, -1)).Length() - 1) < .00001f,
                    $"Ground-only movement and diagonal speed at {angle * 90} degrees");
            }
            Camera.Projection = Camera3D.ProjectionType.Orthogonal;
            Camera.Position = center + Vector3.Up * 8;
            Camera.LookAt(center, Vector3.Forward);
            Check(Camera.ToGroundMovement(new(0, -1)).DistanceTo(Vector3.Forward) < .00001f, "Top camera W has a stable direction");
            Camera.GlobalTransform = oldCameraTransform; Camera.Projection = oldProjection;
            Check(Count() == 56 && Flowers.Sum(f => f.Balls.Count) == 16 && HumanStock.Sum(h => h.Count) == 10, "Initial distribution: 56 pieces, 16 in flowers, 10 in human stock");
            int before = Balls.Count; Player.Fire();
            Check(Balls.Count == before + 1 && Player.Inventory.Count == 3 && Count() == 56, "Launch conserves inventory");
            await Frames(180);
            Check(RedHive.Contents.Sum(c => c.Count) == 4, "A real launch enters the red HIVE without teleportation");
            var blocker = new StaticBody3D { Position = new(Arena.Center, 2.5f, -.85f) };
            AddChild(blocker); blocker.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(Arena.Size + 2, 6, .1f) } });
            await Frames(2); Player.Cooldown = 0;
            int beforeBlockedShot = Balls.Count, beforeBlockedInventory = Player.Inventory.Count;
            Player.Fire();
            Check(Balls.Count == beforeBlockedShot && Player.Inventory.Count == beforeBlockedInventory && Player.ShotStatus.Contains("BLOCKED"), "Blocked launch preserves inventory");
            RemoveChild(blocker); blocker.QueueFree(); await Frames(2);
            var snapshot = JsonSerializer.Deserialize<Snapshot>(JsonSerializer.Serialize(Capture()));
            Restore(snapshot); foreach (var r in Robots) r.Bot = false;
            Check(Count() == 56 && Player.Inventory.Count == 3 && RedHive.Contents.Sum(c => c.Count) == 4, "Save/load preserves free, held and stored pieces");
            var flying = Balls.First(b => !b.Stored); flying.LinearVelocity = new(1, 2, 3);
            SetFrozen(true); await Frames(5); SetFrozen(false);
            Check(flying.LinearVelocity.DistanceTo(new(1, 2, 3)) < .001f, "Pause restores velocities");
            Player.Position = new(.5f, 0, -Arena.Center); Player.Speed = 10; Player.Acceleration = 100;
            Player.Command = Vector3.Right;
            await Frames(120);
            Check(Player.Position.X < 1.0f, "Robot cannot cross the central frame at 10 m/s");
            Player.Command = Vector3.Left; await Frames(120);
            Check(Player.Position.X >= Player.Width / 2 - .005f, "Robot cannot cross perimeter wall");
            Player.Command = Vector3.Zero; Player.Speed = 1.5f; Player.Acceleration = 3;
            Player.Position = new(.8f, 0, -.8f); Player.Rotation = Vector3.Zero; Player.Inventory.Clear(); Player.Intake = true;
            int countBeforeIntake = Balls.Count;
            SpawnBall(PieceKind.Pollen, Player.Position + Vector3.Forward * .3f + Vector3.Up * .04f);
            await Frames(5);
            Check(Player.Inventory.Count == 1 && Balls.Count == countBeforeIntake, "Front intake transfers a floor piece exactly once");
            Player.Intake = false;
            Reset(); foreach (var r in Robots) r.Bot = false;
            int side = RedHive.UpSide;
            var physical = RedHive.Contents[side < 0 ? 0 : 1][0];
            Check(!physical.Freeze && physical.CollisionLayer == 2, "HIVE contents remain physical");
            await Frames(30);
            Vector3 earlier = physical.GlobalPosition;
            physical.Sleeping = false; physical.ApplyCentralImpulse(Vector3.Up * .05f);
            await Frames(6);
            Check(physical.GlobalPosition.DistanceTo(earlier) > .005f, "HIVE ball responds to impulse instead of locking to a slot");
            Reset(); foreach (var r in Robots) r.Bot = false;
            var column = Flowers[0];
            var falling = SpawnBall(PieceKind.RedNectar, column.Base + Vector3.Up * .75f);
            await Frames(240);
            Check(column.Balls.Contains(falling) && !falling.Freeze && falling.Position.Y < FlowerColumn.Top,
                "Flower accepts a physically falling nectar through its hollow opening");
            float upperBefore = column.Balls[1].Position.Y;
            column.ReleaseBottom(); await Frames(120);
            Check(column.Balls.Count == 4 && column.Balls[0].Position.Y < upperBefore - .025f,
                "Flower stack falls after bottom extraction");
            Reset(); foreach (var r in Robots) r.Bot = false;
            AimFlower = true; Player.Inventory.Clear(); Player.Inventory.Add(PieceKind.RedNectar);
            Player.Position = new(.45f,0,-.8f); Player.Cooldown = 0;
            Player.Fire(); await Frames(240);
            Check(Player.Inventory.Count == 0 && Flowers.Sum(f => f.Balls.Count) == 17,
                "Planned physical launch scores nectar in a flower");
            AimFlower = false; Reset(); foreach (var r in Robots) r.Bot = false;
            for (int i=0;i<18;i++) RedHive.Store(SpawnBall(PieceKind.Pollen,RedHive.Position),RedHive.UpSide);
            await Frames(720);
            Check(RedHive.Tips > 0, "Physical HIVE load causes tipping");
            DropHuman(true); Check(HumanStock[0].Count == 4, "Human player releases one nectar");
            var first = Flowers[0].ReleaseBottom(); Check(first != null && !first.Stored && Flowers[0].Balls.Count == 3, "Flower bottom extraction");
            Reset(); Check(Count() == 56 && Player.Inventory.Count == 4 && Flowers[0].Balls.Count == 4, "Reset reconstructs original state");
            GD.Print("ALL INTEGRATION CHECKS PASSED"); GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError("INTEGRATION FAILURE: " + e); GetTree().Quit(1); }
    }
}
