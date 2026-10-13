using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class Simulation : Node3D
{
    public const double ControlStep=1.0/120;
    public readonly List<GamePiece> Balls = new();
    public readonly List<RobotAgent> Robots = new();
    public Hive RedHive, BlueHive;
    public RobotAgent Player => Robots.Count > 0 ? Robots[0] : null;
    public OrbitCamera Camera;
    public SimulatorHud Hud;
    public PracticePath PathEditor;
    public AimPreview Aim;
    public bool Started, Paused, Practice = true, TwoPlayers, AutonomousDrill, TrainingGround;
    public float Countdown = 0f;
    public bool OpponentAi = true;
    public bool ShowTrajectory = true;
    public bool RobotCentricDrive = false;
    public Key KeyForward = Key.W;
    public Key KeyBackward = Key.S;
    public Key KeyLeft = Key.A;
    public Key KeyRight = Key.D;
    public Key KeyTurnLeft = Key.Q;
    public Key KeyTurnRight = Key.E;
    public Key KeyDriveMode = Key.N;
    public Key KeyIntake = Key.Shift;
    public Key KeyLaunch = Key.Space;
    public Key KeyTarget = Key.T;
    public Key KeyCamera = Key.C;
    public Key KeyReset = Key.R;
    public Key KeyHuman = Key.H;
    public Key KeyExtract = Key.K;
    private bool _lastIntakePressed;
    private bool _lastIntakePressedSecond;
    private bool _lastJoyIntakePressed;
    private bool _lastJoyIntakePressedSecond;

    public bool TrainingSolo = true;
    public bool TrainingAutonomousMode = false;
    public int TrainingRobotCount = 1;
    public bool TrainingThreeWithAlly = true; // When 3 bots: true = Ally on your team vs 1 opponent; false = 2 on opposing team
    public int TrainingPlacement = 0; // 0: FTC Starts, 1: Hive Defense, 2: Scattered, 3: Wing/Corners
    public int SelectedStartingPositionIndex = 0;

    public bool TrainingLoopActive;
    public int TrainingInventoryPollen;
    public int TrainingInventoryNectar;
    public bool TrainingInventoryConfigured;
    public string TrainingBallsPreset = "full";
    public bool TrainingFloorBallsConfigured;

    public struct TrainingBallData
    {
        public PieceKind Kind;
        public Vector3 Position;
    }
    public readonly List<TrainingBallData> CustomTrainingFloorBalls = new();
    public float TrainingTimeMultiplier = 1.0f;

    public readonly AutonomousPathRunner[] PathRunners = { new(), new(), new(), new() };
    public readonly string[] AssignedAutonomousPaths = {
        "Red Near 3-Piece Hive Cycle",
        "Red Far Wing Intake & Hive",
        "Blue Near Audience Hive Cycle",
        "Blue Far Backstage Cross & Hive"
    };

    public bool IsPathRunningForRobot(RobotAgent robot)
    {
        if (robot == null) return false;
        int idx = Robots.IndexOf(robot);
        return idx >= 0 && idx < PathRunners.Length && PathRunners[idx] != null && PathRunners[idx].IsRunning;
    }

    public void StartAutonomousPathRunners()
    {
        for (int i = 0; i < Robots.Count && i < PathRunners.Length; i++)
        {
            var robot = Robots[i];
            PathRunners[i].Robot = robot;
            string pathName = i < AssignedAutonomousPaths.Length ? AssignedAutonomousPaths[i] : null;
            if (!string.IsNullOrEmpty(pathName) && !pathName.Equals("[MANUAL DRIVING]", StringComparison.OrdinalIgnoreCase))
            {
                var pathDef = AutonomousPathRoster.GetPathByName(pathName);
                if (pathDef != null && pathDef.Waypoints.Count > 0)
                {
                    PathRunners[i].Start(pathDef);
                    continue;
                }
            }
            PathRunners[i].Stop();
        }
    }

    public void StopAutonomousPathRunners()
    {
        for (int i = 0; i < PathRunners.Length; i++)
        {
            PathRunners[i]?.Stop();
            if (PathRunners[i] != null) PathRunners[i].Robot = null;
        }
    }

    public void SetTimeMultiplier(float multiplier)
    {
        TrainingTimeMultiplier = Mathf.Clamp(multiplier, 0.5f, 10.0f);
        Engine.TimeScale = TrainingTimeMultiplier;
    }

    public void StartTrainingDrill()
    {
        TrainingLoopActive = true;
        Countdown = 3.0f;
        _elapsed = 0;
        Engine.TimeScale = TrainingTimeMultiplier;
        StartAutonomousPathRunners();
    }

    public void StopTrainingDrill()
    {
        TrainingLoopActive = false;
        Countdown = 0f;
        _elapsed = 0;
        StopAutonomousPathRunners();
        Reset(true);
        Engine.TimeScale = TrainingTimeMultiplier;
    }

    public void ResetTrainingCycle()
    {
        TrainingLoopActive = true;
        Reset(true);
        Countdown = 0f;
        _elapsed = 0;
        Engine.TimeScale = TrainingTimeMultiplier;
        StartAutonomousPathRunners();
        if (Hud != null)
        {
            Hud.TriggerStartFlash("START!", "30S LOOP RESTARTED");
        }
    }

    public bool IsFloorBall(GamePiece b)
    {
        if (b == null || !GodotObject.IsInstanceValid(b)) return false;
        if (b.Stored || b.ShotRobotIndex >= 0) return false;
        if (Flowers.Any(f => f.Balls.Contains(b))) return false;
        if (RedHive != null && (RedHive.Contents[0].Contains(b) || RedHive.Contents[1].Contains(b))) return false;
        if (BlueHive != null && (BlueHive.Contents[0].Contains(b) || BlueHive.Contents[1].Contains(b))) return false;
        return true;
    }

    public void CaptureTrainingFloorBalls()
    {
        CustomTrainingFloorBalls.Clear();
        foreach (var b in Balls)
        {
            if (IsFloorBall(b))
            {
                CustomTrainingFloorBalls.Add(new TrainingBallData { Kind = b.Kind, Position = b.Position });
            }
        }
    }

    public void SpawnTrainingFloorBalls()
    {
        if (CustomTrainingFloorBalls.Count > 0 && TrainingBallsPreset == "custom")
        {
            foreach (var b in CustomTrainingFloorBalls)
            {
                SpawnBall(b.Kind, b.Position);
            }
        }
        else if (TrainingBallsPreset == "clear")
        {
            // 0 loose balls
        }
        else if (TrainingBallsPreset == "ftc" || TrainingBallsPreset == "official")
        {
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
            }
        }
        else if (TrainingBallsPreset == "sparse")
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.Tau / 12;
                float dist = 0.8f + (i % 3) * 0.3f;
                SpawnBall(PieceKind.Pollen, Arena.World(Arena.Center + Mathf.Cos(angle) * dist, Arena.Center + Mathf.Sin(angle) * dist, .04f));
            }
        }
        else if (TrainingBallsPreset == "full")
        {
            for (int i = 0; i < 28; i++)
            {
                float angle = i * Mathf.Tau / 28;
                float dist = 0.5f + (i % 4) * 0.25f;
                SpawnBall(PieceKind.Pollen, Arena.World(Arena.Center + Mathf.Cos(angle) * dist, Arena.Center + Mathf.Sin(angle) * dist, .04f));
            }
            for (int i = 0; i < 4; i++)
            {
                SpawnBall(PieceKind.RedNectar, Arena.World(Arena.Center - 0.7f + i * 0.35f, Arena.Center - 0.6f, .04f));
                SpawnBall(PieceKind.BlueNectar, Arena.World(Arena.Center - 0.7f + i * 0.35f, Arena.Center + 0.6f, .04f));
            }
        }
        else
        {
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
            }
        }
    }

    public static readonly (string Name, Vector3 Position, float Heading)[] DefaultStartingPositions = new[]
    {
        ("Red Near (Audience)", new Vector3(.84f, 0, -.26f), 0f),
        ("Red Far (Backstage)", new Vector3(.28f, 0, -2.75f), 0f),
        ("Blue Near (Audience)", new Vector3(Arena.Size - .84f, 0, -Arena.Size + .26f), Mathf.Pi),
        ("Blue Far (Backstage)", new Vector3(Arena.Size - .28f, 0, -.84f), Mathf.Pi)
    };
    public Vector3? CustomStartingPosition;
    public float CustomStartingHeading;
    public RobotProfile CustomStartingProfile;

    public void ApplyTrainingSettings()
    {
        if (Robots.Count == 0) return;

        int activeCount = TrainingSolo ? 1 : Mathf.Clamp(TrainingRobotCount, 1, 4);

        Vector3[][] placementPositions = new[]
        {
            new[] {
                new Vector3(.84f, 0, -.26f),
                new Vector3(.28f, 0, -2.75f),
                new Vector3(Arena.Size - .84f, 0, -Arena.Size + .26f),
                new Vector3(Arena.Size - .28f, 0, -.84f)
            },
            new[] {
                new Vector3(.84f, 0, -.26f),
                new Vector3(Arena.Center - .4f, 0, -Arena.Center + .6f),
                new Vector3(Arena.Center + .4f, 0, -Arena.Center - .6f),
                new Vector3(Arena.Center, 0, -Arena.Center - .8f)
            },
            new[] {
                new Vector3(.84f, 0, -.26f),
                new Vector3(1.2f, 0, -2.2f),
                new Vector3(2.4f, 0, -1.2f),
                new Vector3(3.0f, 0, -2.8f)
            },
            new[] {
                new Vector3(.84f, 0, -.26f),
                new Vector3(.35f, 0, -.35f),
                new Vector3(Arena.Size - .35f, 0, -Arena.Size + .35f),
                new Vector3(Arena.Size - .35f, 0, -.35f)
            }
        };

        var positions = placementPositions[Mathf.Clamp(TrainingPlacement, 0, placementPositions.Length - 1)];

        for (int i = 0; i < Robots.Count; i++)
        {
            var r = Robots[i];
            bool active = false;
            if (activeCount == 1)
            {
                active = (i == 0);
            }
            else if (activeCount == 2)
            {
                active = (i == 0 || i == 2);
            }
            else if (activeCount == 3)
            {
                if (TrainingThreeWithAlly)
                    active = (i == 0 || i == 1 || i == 2);
                else
                    active = (i == 0 || i == 2 || i == 3);
            }
            else
            {
                active = true;
            }

            r.Visible = active;
            r.CollisionLayer = active ? 4u : 0u;
            r.CollisionMask = active ? (1u | 2u | 4u) : 0u;
            r.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;

            if (active && i > 0)
            {
                r.Position = positions[i];
                r.Rotation = new Vector3(0, i >= 2 ? Mathf.Pi : 0, 0);
                r.Velocity = Vector3.Zero;
                r.Bot = true;
            }
        }

        if (TrainingGround)
        {
            AutonomousDrill = true;
            Practice = false;
        }
        else if (TrainingAutonomousMode)
        {
            AutonomousDrill = true;
        }

        if (SelectedStartingPositionIndex >= 0 && SelectedStartingPositionIndex <= 4)
        {
            SetPlayerStartingPosition(SelectedStartingPositionIndex);
        }
    }

    public void SetFieldBallsPreset(string preset)
    {
        TrainingBallsPreset = preset;
        TrainingFloorBallsConfigured = true;
        CustomTrainingFloorBalls.Clear();
        var loose = Balls.Where(IsFloorBall).ToList();
        foreach (var b in loose) RemoveBall(b);
        if (preset == "clear")
        {
            Status = "Floor balls cleared (0 loose balls)";
        }
        else if (preset == "ftc" || preset == "official")
        {
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
            }
            Status = "Official FTC match ball distribution restored (8 perimeter balls)";
        }
        else if (preset == "sparse")
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.Tau / 12;
                float dist = 0.8f + (i % 3) * 0.3f;
                SpawnBall(PieceKind.Pollen, Arena.World(Arena.Center + Mathf.Cos(angle) * dist, Arena.Center + Mathf.Sin(angle) * dist, .04f));
            }
            Status = "Sparse floor balls set (12 balls)";
        }
        else if (preset == "full")
        {
            for (int i = 0; i < 28; i++)
            {
                float angle = i * Mathf.Tau / 28;
                float dist = 0.5f + (i % 4) * 0.25f;
                SpawnBall(PieceKind.Pollen, Arena.World(Arena.Center + Mathf.Cos(angle) * dist, Arena.Center + Mathf.Sin(angle) * dist, .04f));
            }
            for (int i = 0; i < 4; i++)
            {
                SpawnBall(PieceKind.RedNectar, Arena.World(Arena.Center - 0.7f + i * 0.35f, Arena.Center - 0.6f, .04f));
                SpawnBall(PieceKind.BlueNectar, Arena.World(Arena.Center - 0.7f + i * 0.35f, Arena.Center + 0.6f, .04f));
            }
            Status = "Full field ball distribution restored (36 balls)";
        }
    }

    public void SetRobotInventoryPreset(int pollenCount, int nectarCount)
    {
        TrainingInventoryPollen = pollenCount;
        TrainingInventoryNectar = nectarCount;
        TrainingInventoryConfigured = true;
        if (Player == null) return;
        Player.Inventory.Clear();
        for (int i = 0; i < pollenCount && Player.Inventory.Count < Player.Capacity; i++)
            Player.Inventory.Add(PieceKind.Pollen);
        for (int i = 0; i < nectarCount && Player.Inventory.Count < Player.Capacity; i++)
            Player.Inventory.Add(Player.Red ? PieceKind.RedNectar : PieceKind.BlueNectar);
        Status = $"Inventory set: {Player.Inventory.Count} pieces ({pollenCount} Pollen, {nectarCount} Nectar)";
    }

    public void SetPlayerStartingPosition(int presetIndex)
    {
        SelectedStartingPositionIndex = presetIndex;
        if (Player == null) return;
        if (presetIndex >= 0 && presetIndex < DefaultStartingPositions.Length)
        {
            var pos = DefaultStartingPositions[presetIndex];
            Player.Position = pos.Position;
            Player.Rotation = new Vector3(0, pos.Heading, 0);
            Player.Velocity = Vector3.Zero;
            Player.Command = Vector3.Zero;
            Player.TurnCommand = 0f;
            Status = $"Loaded position: {pos.Name}";
        }
        else if (presetIndex == 4)
        {
            Vector3 pos = CustomStartingPosition ?? new Vector3(1.8f, 0, -1.8f);
            float heading = CustomStartingHeading;
            if (CustomStartingProfile != null)
            {
                bool needReset = Profile.Name != CustomStartingProfile.Name;
                Profile = CustomStartingProfile.Copy();
                if (needReset && Started)
                {
                    Reset(true);
                }
            }
            if (Player != null)
            {
                Player.Width = Profile.WidthCm / 100;
                Player.Length = Profile.LengthCm / 100;
                Player.Speed = Profile.Speed;
                Player.Acceleration = Profile.Acceleration;
                Player.TurnSpeed = Profile.TurnSpeed;
                Player.TurretCount = Profile.Turrets;
                Player.IntakeCount = Profile.Intakes;
                Player.Imported = Profile.Imported;
                Player.Position = pos;
                Player.Rotation = new Vector3(0, heading, 0);
                Player.Velocity = Vector3.Zero;
                Player.Command = Vector3.Zero;
                Player.TurnCommand = 0f;
            }
            Status = "Loaded custom robot & position";
        }
    }

    public void SaveCurrentAsCustomPosition()
    {
        if (Player == null) return;
        CustomStartingPosition = Player.Position;
        CustomStartingHeading = Player.Rotation.Y;
        CustomStartingProfile = Profile.Copy();
        Status = $"Saved custom position ({Player.Position.X:0.00}, {Player.Position.Z:0.00}) & robot '{Profile.Name}'";
    }

    public sealed class ControlsConfig
    {
        public bool RobotCentric { get; set; } = false;
        public Key DriveMode { get; set; } = Key.N;
        public Key Forward { get; set; } = Key.W;
        public Key Backward { get; set; } = Key.S;
        public Key Left { get; set; } = Key.A;
        public Key Right { get; set; } = Key.D;
        public Key TurnLeft { get; set; } = Key.Q;
        public Key TurnRight { get; set; } = Key.E;
        public Key Intake { get; set; } = Key.Shift;
        public Key Launch { get; set; } = Key.Space;
        public Key Target { get; set; } = Key.T;
        public Key Camera { get; set; } = Key.C;
        public Key Reset { get; set; } = Key.R;
        public Key Human { get; set; } = Key.H;
        public Key Extract { get; set; } = Key.K;
    }

    public void SaveControls()
    {
        try
        {
            var config = new ControlsConfig
            {
                RobotCentric = RobotCentricDrive,
                DriveMode = KeyDriveMode,
                Forward = KeyForward,
                Backward = KeyBackward,
                Left = KeyLeft,
                Right = KeyRight,
                TurnLeft = KeyTurnLeft,
                TurnRight = KeyTurnRight,
                Intake = KeyIntake,
                Launch = KeyLaunch,
                Target = KeyTarget,
                Camera = KeyCamera,
                Reset = KeyReset,
                Human = KeyHuman,
                Extract = KeyExtract
            };
            using var file = Godot.FileAccess.Open("user://controls.json", Godot.FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(JsonSerializer.Serialize(config));
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning("Failed to save controls: " + ex.Message);
        }
    }

    public void LoadControls()
    {
        try
        {
            if (Godot.FileAccess.FileExists("user://controls.json"))
            {
                string json = Godot.FileAccess.GetFileAsString("user://controls.json");
                var config = JsonSerializer.Deserialize<ControlsConfig>(json);
                if (config != null)
                {
                    RobotCentricDrive = config.RobotCentric;
                    KeyDriveMode = config.DriveMode;
                    KeyForward = config.Forward;
                    KeyBackward = config.Backward;
                    KeyLeft = config.Left;
                    KeyRight = config.Right;
                    KeyTurnLeft = config.TurnLeft;
                    KeyTurnRight = config.TurnRight;
                    KeyIntake = config.Intake;
                    KeyLaunch = config.Launch;
                    KeyTarget = config.Target;
                    KeyCamera = config.Camera;
                    KeyReset = config.Reset;
                    KeyHuman = config.Human;
                    KeyExtract = config.Extract;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning("Failed to load controls: " + ex.Message);
        }
    }

    public void ResetKeyBindings()
    {
        RobotCentricDrive = false;
        KeyDriveMode = Key.N;
        KeyForward = Key.W;
        KeyBackward = Key.S;
        KeyLeft = Key.A;
        KeyRight = Key.D;
        KeyTurnLeft = Key.Q;
        KeyTurnRight = Key.E;
        KeyIntake = Key.Shift;
        KeyLaunch = Key.Space;
        KeyTarget = Key.T;
        KeyCamera = Key.C;
        KeyReset = Key.R;
        KeyHuman = Key.H;
        KeyExtract = Key.K;
        SaveControls();
    }

    public class TrainingBallDataDto
    {
        public int Kind { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class SettingsConfig
    {
        public bool Fullscreen { get; set; }
        public bool Vsync { get; set; } = true;
        public bool ShowTrajectory { get; set; } = true;
        public int CameraMode { get; set; } = 0;
        public float CameraDistance { get; set; } = 5.2f;
        public float CameraFov { get; set; } = 45f;
        public bool OpponentAi { get; set; } = true;
        public bool TwoPlayers { get; set; } = false;
        public bool RobotCentric { get; set; } = false;

        public int TrainingRobotCount { get; set; } = 1;
        public bool TrainingSolo { get; set; } = true;
        public bool TrainingThreeWithAlly { get; set; } = true;
        public int TrainingPlacement { get; set; } = 0;
        public bool TrainingAutonomousMode { get; set; } = false;

        public float? CustomStartX { get; set; }
        public float? CustomStartY { get; set; }
        public float? CustomStartZ { get; set; }
        public float CustomStartHeading { get; set; }
        public int SelectedStartingPositionIndex { get; set; } = 0;

        public string PlayerProfileName { get; set; }
        public string[] AiSlotProfileNames { get; set; }

        public int TrainingInventoryPollen { get; set; } = 0;
        public int TrainingInventoryNectar { get; set; } = 0;
        public bool TrainingInventoryConfigured { get; set; } = false;
        public string TrainingBallsPreset { get; set; } = "full";
        public bool TrainingFloorBallsConfigured { get; set; } = false;
        public float TrainingTimeMultiplier { get; set; } = 1.0f;
        public string[] AssignedAutonomousPaths { get; set; }
        public List<TrainingBallDataDto> CustomTrainingBalls { get; set; } = new();
    }

    public void SaveSettings()
    {
        try
        {
            var config = new SettingsConfig
            {
                Fullscreen = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen,
                Vsync = DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled,
                ShowTrajectory = ShowTrajectory,
                CameraMode = Camera != null ? Camera.Mode : 0,
                CameraDistance = Camera != null ? Camera.Distance : 5.2f,
                CameraFov = Camera != null ? Camera.Fov : 45f,
                OpponentAi = OpponentAi,
                TwoPlayers = TwoPlayers,
                RobotCentric = RobotCentricDrive,

                TrainingRobotCount = TrainingRobotCount,
                TrainingSolo = TrainingSolo,
                TrainingThreeWithAlly = TrainingThreeWithAlly,
                TrainingPlacement = TrainingPlacement,
                TrainingAutonomousMode = TrainingAutonomousMode,

                CustomStartX = CustomStartingPosition?.X,
                CustomStartY = CustomStartingPosition?.Y,
                CustomStartZ = CustomStartingPosition?.Z,
                CustomStartHeading = CustomStartingHeading,
                SelectedStartingPositionIndex = SelectedStartingPositionIndex,

                PlayerProfileName = Profile?.Name,
                AiSlotProfileNames = new string[]
                {
                    AiSlotProfiles[0]?.Name ?? "",
                    AiSlotProfiles[1]?.Name ?? "",
                    AiSlotProfiles[2]?.Name ?? ""
                },

                TrainingInventoryPollen = TrainingInventoryPollen,
                TrainingInventoryNectar = TrainingInventoryNectar,
                TrainingInventoryConfigured = TrainingInventoryConfigured,
                TrainingBallsPreset = TrainingBallsPreset,
                TrainingFloorBallsConfigured = TrainingFloorBallsConfigured,
                TrainingTimeMultiplier = TrainingTimeMultiplier,
                AssignedAutonomousPaths = (string[])AssignedAutonomousPaths.Clone(),
                CustomTrainingBalls = CustomTrainingFloorBalls.Select(b => new TrainingBallDataDto
                {
                    Kind = (int)b.Kind,
                    X = b.Position.X,
                    Y = b.Position.Y,
                    Z = b.Position.Z
                }).ToList()
            };
            using var file = Godot.FileAccess.Open("user://settings.json", Godot.FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(JsonSerializer.Serialize(config));
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning("Failed to save settings: " + ex.Message);
        }
    }

    public void LoadSettings()
    {
        try
        {
            if (Godot.FileAccess.FileExists("user://settings.json"))
            {
                string json = Godot.FileAccess.GetFileAsString("user://settings.json");
                var config = JsonSerializer.Deserialize<SettingsConfig>(json);
                if (config != null)
                {
                    if (config.Fullscreen)
                        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                    DisplayServer.WindowSetVsyncMode(config.Vsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
                    ShowTrajectory = config.ShowTrajectory;
                    if (Camera != null)
                    {
                        Camera.Mode = config.CameraMode;
                        if (config.CameraDistance > 0) Camera.Distance = config.CameraDistance;
                        if (config.CameraFov > 0) Camera.Fov = config.CameraFov;
                    }
                    OpponentAi = config.OpponentAi;
                    TwoPlayers = config.TwoPlayers;
                    RobotCentricDrive = config.RobotCentric;

                    TrainingRobotCount = Mathf.Clamp(config.TrainingRobotCount, 1, 4);
                    TrainingSolo = config.TrainingSolo;
                    TrainingThreeWithAlly = config.TrainingThreeWithAlly;
                    TrainingPlacement = Mathf.Clamp(config.TrainingPlacement, 0, 3);
                    TrainingAutonomousMode = config.TrainingAutonomousMode;

                    if (config.CustomStartX.HasValue && config.CustomStartZ.HasValue)
                    {
                        CustomStartingPosition = new Vector3(config.CustomStartX.Value, config.CustomStartY ?? 0f, config.CustomStartZ.Value);
                        CustomStartingHeading = config.CustomStartHeading;
                    }
                    SelectedStartingPositionIndex = config.SelectedStartingPositionIndex;

                    var roster = RobotRoster.GetRoster();
                    if (!string.IsNullOrEmpty(config.PlayerProfileName))
                    {
                        var playerMatch = roster.Find(r => r.Name == config.PlayerProfileName);
                        if (playerMatch != null) Profile = playerMatch.Copy();
                    }
                    if (config.AiSlotProfileNames != null)
                    {
                        for (int i = 0; i < 3 && i < config.AiSlotProfileNames.Length; i++)
                        {
                            string aName = config.AiSlotProfileNames[i];
                            if (!string.IsNullOrEmpty(aName))
                            {
                                var match = roster.Find(r => r.Name == aName);
                                if (match != null) AiSlotProfiles[i] = match.Copy();
                            }
                        }
                    }

                    TrainingInventoryPollen = config.TrainingInventoryPollen;
                    TrainingInventoryNectar = config.TrainingInventoryNectar;
                    TrainingInventoryConfigured = config.TrainingInventoryConfigured;
                    TrainingBallsPreset = string.IsNullOrEmpty(config.TrainingBallsPreset) ? "full" : config.TrainingBallsPreset;
                    TrainingFloorBallsConfigured = config.TrainingFloorBallsConfigured;
                    TrainingTimeMultiplier = config.TrainingTimeMultiplier > 0 ? config.TrainingTimeMultiplier : 1.0f;
                    if (config.AssignedAutonomousPaths != null)
                    {
                        for (int i = 0; i < 4 && i < config.AssignedAutonomousPaths.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(config.AssignedAutonomousPaths[i]))
                                AssignedAutonomousPaths[i] = config.AssignedAutonomousPaths[i];
                        }
                    }
                    CustomTrainingFloorBalls.Clear();
                    if (config.CustomTrainingBalls != null)
                    {
                        foreach (var b in config.CustomTrainingBalls)
                        {
                            CustomTrainingFloorBalls.Add(new TrainingBallData
                            {
                                Kind = (PieceKind)b.Kind,
                                Position = new Vector3(b.X, b.Y, b.Z)
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning("Failed to load settings: " + ex.Message);
        }
    }

    private readonly HashSet<GamePiece> _redScoredHive = new();
    private readonly HashSet<GamePiece> _blueScoredHive = new();
    public bool Finished => !Practice && !TrainingGround && _elapsed >= (AutonomousDrill ? 30 : 158);
    public bool Running => Started && !Paused && !Finished;
    public bool DrivingAllowed => Running && Countdown <= 0 && (Practice || (TrainingGround && TrainingLoopActive) || Elapsed < 30 || Elapsed >= 38);
    private double _elapsed;
    private double _observationTime;
    private bool _wasTransition;
    public float Elapsed { get => (float)_elapsed; set => _elapsed = value; }
    public int RedScore, BlueScore;
    public string Status = "Godot C# / local prototype";
    public bool AimFlower;
    public RobotProfile Profile = RobotProfile.Load();
    public RobotProfile AiProfile = new RobotProfile { Name = "AI Opponent Standard" };
    public readonly RobotProfile[] AiSlotProfiles = new RobotProfile[3];
    public bool Testing;
    private Node3D _session;
    private readonly Dictionary<GamePiece, (Vector3 Linear, Vector3 Angular)> _pausedVelocities = new();
    private readonly Vector3[] _flowers = { VisualFactory.Inch(3.9f, Arena.Inches * 2 / 6, 21.5f), VisualFactory.Inch(Arena.Inches * 2 / 6, Arena.Inches - 3.9f, 21.5f), VisualFactory.Inch(Arena.Inches - 3.9f, Arena.Inches * 4 / 6, 21.5f), VisualFactory.Inch(Arena.Inches * 4 / 6, 3.9f, 21.5f) };
    public readonly List<FlowerColumn> Flowers = new();
    public readonly List<GamePiece>[] HumanStock = { new(), new() };
    public readonly int[] HumanTokens = { 0, 0 };
    private int _redLastTips, _blueLastTips;
    public override void _Ready()
    {
        LoadControls();
        LoadSettings();
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--physics-test-") && int.TryParse(arg.Substring(15), out int ticks) && ticks >= 120 && ticks <= 1920)
                Engine.PhysicsTicksPerSecond = ticks;
        AddChild(new Arena { Name = "Arena" });
        Camera = new OrbitCamera { Game = this, Current = true, Fov = 46, Far = 50, Near = .02f, Name = "Camera" }; AddChild(Camera);
        Hud = new SimulatorHud { Game = this }; AddChild(Hud);
        PathEditor = new PracticePath { Game = this, Name = "PracticePath" }; AddChild(PathEditor);
        Aim = new AimPreview { Game = this, Name = "AimPreview" }; AddChild(Aim);
        Reset(false);
        Hud.ShowMenu();
        if (OS.GetCmdlineUserArgs().Contains("--smoke-test")) CallDeferred(MethodName.SmokeTest);
        if (OS.GetCmdlineUserArgs().Contains("--performance-test")) CallDeferred(MethodName.PerformanceTest);
        if (OS.GetCmdlineUserArgs().Contains("--rendered-preview-test")) CallDeferred(MethodName.RenderedPreviewTest);
        if (OS.GetCmdlineUserArgs().Contains("--audit-test")) CallDeferred(MethodName.AuditTest);
        if (OS.GetCmdlineUserArgs().Contains("--gravity-test")) CallDeferred(MethodName.GravityTest);
        if (OS.GetCmdlineUserArgs().Contains("--contact-test")) CallDeferred(MethodName.ContactTest);
        if (OS.GetCmdlineUserArgs().Contains("--fps-test")) CallDeferred(MethodName.FpsTest);
if (OS.GetCmdlineUserArgs().Contains("--stl-test")) CallDeferred(nameof(StlTest));
if (OS.GetCmdlineUserArgs().Contains("--mechanism-test")) CallDeferred(nameof(MechanismTest));
if (OS.GetCmdlineUserArgs().Contains("--urdf-test")) CallDeferred(nameof(UrdfTest));
if (OS.GetCmdlineUserArgs().Contains("--physical-drive-test")) CallDeferred(nameof(PhysicalDriveTest));
if (OS.GetCmdlineUserArgs().Contains("--robot-import-test")) CallDeferred(nameof(CadRobotTest));
if (OS.GetCmdlineUserArgs().Contains("--joint-controls-test")) CallDeferred(nameof(JointControlsTest));
if (OS.GetCmdlineUserArgs().Contains("--mechanism-demo")) Hud.CallDeferred(nameof(SimulatorHud.ShowMechanismDemo));
if (OS.GetCmdlineUserArgs().Contains("--joint-controls-demo")) CallDeferred(nameof(JointControlsDemo));
if (OS.GetCmdlineUserArgs().Contains("--shot-test")) CallDeferred(MethodName.ShotTest);

    }
    private void StlTest() => StlImportChecks.Run(this);
    private async void MechanismTest() => await RobotMechanismChecks.Run(this);
    private async void UrdfTest() => await UrdfImportChecks.Run(this);
    private async void PhysicalDriveTest() => await PhysicalDriveChecks.Run(this);
    private async void CadRobotTest() => await CadRobotChecks.Run(this);
    private async void JointControlsTest() => await JointControlChecks.Run(this);
    public void Reset(bool start = true)
    {
        StopAutonomousPathRunners();
        if (_session != null) { RemoveChild(_session); _session.QueueFree(); }
        Balls.Clear(); Robots.Clear(); _pausedVelocities.Clear(); Elapsed = 0; Paused = false; Started = start;
        _lastIntakePressed = _lastIntakePressedSecond = _lastJoyIntakePressed = _lastJoyIntakePressedSecond = false;
        _observationTime=0; _wasTransition=false;
        _redScoredHive.Clear(); _blueScoredHive.Clear();
        if (PathEditor != null) PathEditor.Following = false;
        Flowers.Clear(); HumanStock[0].Clear(); HumanStock[1].Clear();
        HumanTokens[0] = HumanTokens[1] = _redLastTips = _blueLastTips = 0;
        _session = new Node3D { Name = "Session" }; AddChild(_session);
        RedHive = new Hive { Red = true, Position = VisualFactory.Inch(Arena.Inches / 2 - 12.75f, Arena.Inches / 2, 43.95f), Name = "RedHive" };
        BlueHive = new Hive { Red = false, Position = VisualFactory.Inch(Arena.Inches / 2 + 12.75f, Arena.Inches / 2, 43.95f), Name = "BlueHive" };
        _session.AddChild(RedHive); _session.AddChild(BlueHive);
        var starts = new[] { new Vector3(.84f, 0, -.26f), new Vector3(.28f, 0, -2.75f), new Vector3(Arena.Size - .84f, 0, -Arena.Size + .26f), new Vector3(Arena.Size - .28f, 0, -.84f) };
        for (int i = 0; i < 4; i++)
        {
            var robot = new RobotAgent { Game = this, Red = i < 2, Number = i % 2 + 1, Bot = i > 0 && !(TwoPlayers && i == 2), Position = starts[i], Name = $"Robot{i + 1}" };
            var p = (i == 0) ? Profile : (AiSlotProfiles[i - 1] ?? AiProfile ?? Profile);
            robot.Width = p.WidthCm / 100; robot.Length = p.LengthCm / 100; robot.Speed = p.Speed; robot.Acceleration = p.Acceleration; robot.TurnSpeed = p.TurnSpeed; robot.TurretCount = p.Turrets; robot.IntakeCount = p.Intakes; robot.Imported = p.Imported;
            robot.Rotation = new(0, i >= 2 ? Mathf.Pi : 0, 0);
            _session.AddChild(robot); Robots.Add(robot);
            if (TrainingGround && TrainingInventoryConfigured)
            {
                if (i == 0)
                {
                    for (int j = 0; j < TrainingInventoryPollen && robot.Inventory.Count < robot.Capacity; j++)
                        robot.Inventory.Add(PieceKind.Pollen);
                    for (int j = 0; j < TrainingInventoryNectar && robot.Inventory.Count < robot.Capacity; j++)
                        robot.Inventory.Add(robot.Red ? PieceKind.RedNectar : PieceKind.BlueNectar);
                }
                else
                {
                    for (int j = 0; j < 4; j++) robot.Inventory.Add(PieceKind.Pollen);
                }
            }
            else
            {
                for (int j = 0; j < 4; j++) robot.Inventory.Add(PieceKind.Pollen);
            }
        }
        if (TrainingGround || (Practice && !Testing))
        {
            ApplyTrainingSettings();
        }
        if ((TrainingGround || AutonomousDrill) && !Testing)
        {
            Countdown = (TrainingGround && !TrainingLoopActive) ? 0f : 3.0f;
        }
        else
        {
            Countdown = 0f;
        }
        // 40 POLLEN + 8 NECTAR roșii + 8 NECTAR albastre, inclusiv inventarele.
        for (int alliance = 0; alliance < 2; alliance++)
        {
            bool red = alliance == 0;
            if (!TrainingGround || !TrainingFloorBallsConfigured)
            {
                for (int i = 0; i < 4; i++)
                {
                    float x = .03683f + i * .071628f;
                    float y = .03683f;
                    if (!red) { x = Arena.Size - x; y = Arena.Size - y; }
                    SpawnBall(PieceKind.Pollen, Arena.World(x, y, .04f));
                }
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
            for (int i = 0; i < 3; i++)
            {
                var ball = SpawnBall(red ? PieceKind.RedNectar : PieceKind.BlueNectar, hive.Position);
                ball.Preload = true;
                hive.Store(ball, hive.UpSide);
            }
        }
        if (TrainingGround && TrainingFloorBallsConfigured)
        {
            SpawnTrainingFloorBalls();
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
        else Hud.ShowPause(false);
    }
    public GamePiece SpawnBall(PieceKind kind, Vector3 position)
    {
        var ball = new GamePiece { Kind = kind, Position = position, Name = "Ball" };
        _session.AddChild(ball, true); Balls.Add(ball); return ball;
    }
    public void RemoveBall(GamePiece ball)
    {
        _redScoredHive.Remove(ball);
        _blueScoredHive.Remove(ball);
        foreach (var hive in new[] { RedHive, BlueHive })
            foreach (var bucket in hive.Contents) bucket.Remove(ball);
        foreach (var flower in Flowers) flower.Balls.Remove(ball);
        foreach (var stock in HumanStock) stock.Remove(ball);
        _pausedVelocities.Remove(ball); Balls.Remove(ball); ball.QueueFree();
    }
    public Vector3 Target(RobotAgent robot)
    {
        if (AimFlower && !robot.Bot)
        {
            int begin=robot.Red ? 0 : 2, nearest=begin;
            float best=float.MaxValue;
            for (int i=begin;i<begin+2;i++)
            {
                float d=_flowers[i].DistanceSquaredTo(robot.Position);
                if (d<best) { best=d; nearest=i; }
            }
            return _flowers[nearest]+Vector3.Up*.05f;
        }
        var hive = robot.Red ? RedHive : BlueHive;
        // 4.04 inch deasupra axei locale și 4.5 inch în interiorul gurii,
        // ca punctul de țintire din clientul de referință.
        return hive.ToGlobal(new Vector3(0, .102616f, hive.UpSide * .430784f));
    }
    public override void _PhysicsProcess(double delta)
    {
        if (!Running) return;
        if (TrainingGround && !TrainingLoopActive)
        {
            foreach (var robot in Robots)
            {
                robot.Velocity = Vector3.Zero;
                robot.Command = Vector3.Zero;
                robot.TurnCommand = 0f;
            }
            return;
        }
        if (Countdown > 0)
        {
            Countdown -= (float)delta;
            if (Countdown < 0) Countdown = 0;
            return;
        }
        _elapsed += delta;
        if (TrainingGround)
        {
            if (TrainingLoopActive && _elapsed >= 30.0f)
            {
                ResetTrainingCycle();
                return;
            }
        }
        else if (!Practice && _elapsed >= (AutonomousDrill ? 30 : 158))
        {
            _elapsed = AutonomousDrill ? 30 : 158;
            UpdateScore();
            SetFrozen(true);
            Hud.ShowResults();
            return;
        }
        bool transition = !Practice && !AutonomousDrill && Elapsed >= 30 && Elapsed < 38;
        if (transition != _wasTransition)
        {
            foreach (var robot in Robots)
            {
                robot.SetPhysicsProcess(!transition);
                if (transition)
                {
                    robot.Velocity = Vector3.Zero;
                    robot.Command = Vector3.Zero;
                    robot.TurnCommand = 0;
                    robot.Intake = robot.FireCommand = false;
                }
            }
            _wasTransition = transition;
        }
        _observationTime += delta;
        if (_observationTime + 1e-9 < ControlStep) return;
        _observationTime = Math.Max(0, _observationTime - ControlStep);
        if (!transition)
        {
            if (DrivingAllowed)
            {
                for (int i = 0; i < Robots.Count && i < PathRunners.Length; i++)
                {
                    if (PathRunners[i] != null && PathRunners[i].IsRunning)
                    {
                        PathRunners[i].Update(ControlStep);
                    }
                }
            }
            if (!Testing) { ReadPlayer(Player, false); if (TwoPlayers) ReadPlayer(Robots[2], true); }
        }
        foreach (var flower in Flowers) flower.Update();
        foreach (var ball in Balls)
        {
            if (ball.Stored) continue;
            if (ball.Position.Y < -1 || Mathf.Abs(ball.Position.X) > 20 || Mathf.Abs(ball.Position.Z) > 20)
            { ball.Position = Arena.World(.15f, .15f, ball.Radius + .01f); ball.LinearVelocity = Vector3.Zero; }
            if (RedHive.TryCatch(ball))
            {
                if (!ball.Preload) _redScoredHive.Add(ball);
                ConfirmShot(ball);
                continue;
            }
            if (BlueHive.TryCatch(ball))
            {
                if (!ball.Preload) _blueScoredHive.Add(ball);
                ConfirmShot(ball);
                continue;
            }
            foreach (var flower in Flowers) if (flower.TryCatch(ball)) { ConfirmShot(ball); break; }
        }
        HumanTokens[0] += RedHive.Tips - _redLastTips; HumanTokens[1] += BlueHive.Tips - _blueLastTips;
        _redLastTips = RedHive.Tips; _blueLastTips = BlueHive.Tips;
        UpdateScore();
    }
    private void ConfirmShot(GamePiece ball)
    {
        if (ball.ShotConfirmed || ball.ShotRobotIndex<0 || ball.LinearVelocity.Length()>.35f) return;
        ball.ShotConfirmed=true; Robots[ball.ShotRobotIndex].ShotsMade++;
    }
    public Vector3 ManualDriveDirection(Vector2 input, RobotAgent robot)
    {
        // Field axes stay fixed even when the robot or orbit camera turns.
        var direction = new Vector3(input.X, 0, input.Y);
        if (RobotCentricDrive) direction = robot.GlobalBasis * direction;
        direction.Y = 0;
        return direction.LimitLength();
    }
    private void ReadPlayer(RobotAgent robot, bool second)
    {
        if (TrainingGround)
        {
            robot.Bot = false;
            if (TrainingLoopActive)
            {
                if (!second && PathRunners.Length > 0 && PathRunners[0] != null && PathRunners[0].IsRunning)
                {
                    bool HeldKey(Key key) => Input.IsPhysicalKeyPressed(key);
                    var vCheck = new Vector3((HeldKey(KeyRight) ? 1 : 0) - (HeldKey(KeyLeft) ? 1 : 0), 0, (HeldKey(KeyBackward) ? 1 : 0) - (HeldKey(KeyForward) ? 1 : 0));
                    float turnCheck = (HeldKey(KeyTurnLeft) ? 1 : 0) - (HeldKey(KeyTurnRight) ? 1 : 0);
                    if (vCheck.LengthSquared() > 0 || Mathf.Abs(turnCheck) > 0.001f)
                    {
                        PathRunners[0].Stop();
                    }
                    else
                    {
                        return;
                    }
                }
                else if (!second && PathEditor != null && PathEditor.Following)
                {
                    robot.Command = PathEditor.Command();
                    robot.TurnCommand = PathEditor.TurnCommand();
                    return;
                }
            }
            else
            {
                robot.Command = Vector3.Zero;
                robot.TurnCommand = 0f;
                return;
            }
        }
        else if (!Practice && Elapsed < 30)
        {
            if (!second && PathEditor != null && PathEditor.Following)
            {
                robot.Bot = false;
                robot.Command = PathEditor.Command();
                robot.TurnCommand = PathEditor.TurnCommand();
                return;
            }
            robot.Bot = true;
            return;
        }
        robot.Bot = false;
        bool Held(Key key) => Input.IsPhysicalKeyPressed(key);
        var v = second ? new Vector3((Held(Key.Right) ? 1 : 0) - (Held(Key.Left) ? 1 : 0), 0, (Held(Key.Down) ? 1 : 0) - (Held(Key.Up) ? 1 : 0))
            : new Vector3((Held(KeyRight) ? 1 : 0) - (Held(KeyLeft) ? 1 : 0), 0, (Held(KeyBackward) ? 1 : 0) - (Held(KeyForward) ? 1 : 0));
        if (v.LengthSquared() > 0)
        {
            Vector3 worldDir = ManualDriveDirection(new Vector2(v.X, v.Z), robot);
            worldDir.Y = 0;
            robot.Command = worldDir.Normalized();
        }
        else
        {
            robot.Command = Vector3.Zero;
        }
        if (!second && Practice && PathEditor.Following)
        {
            if (v.LengthSquared() > 0) PathEditor.Following = false;
            else
            {
                robot.Command = PathEditor.Command();
                robot.TurnCommand = PathEditor.TurnCommand();
            }
        }
        robot.TurnCommand = second ? (Held(Key.Comma) ? 1 : 0) - (Held(Key.Period) ? 1 : 0) : (Held(KeyTurnLeft) ? 1 : 0) - (Held(KeyTurnRight) ? 1 : 0);
        bool intakePressed = (Held(second ? Key.Enter : KeyIntake) && (second || !Input.IsMouseButtonPressed(MouseButton.Middle)))
            || (!second && Held(Key.J));
        ref bool lastIntake = ref (second ? ref _lastIntakePressedSecond : ref _lastIntakePressed);
        if (intakePressed && !lastIntake)
        {
            robot.ToggleIntake();
        }
        lastIntake = intakePressed;
        robot.FireCommand = Held(second ? Key.Slash : KeyLaunch);
        int pad = second ? 1 : 0;
        if (Input.GetConnectedJoypads().Contains(pad))
        {
            Vector2 axes = new(Input.GetJoyAxis(pad, JoyAxis.LeftX), Input.GetJoyAxis(pad, JoyAxis.LeftY));
            if (axes.Length() > .15f)
            {
                if (!second) PathEditor.Following = false;
                Vector3 worldJoy = ManualDriveDirection(axes, robot);
                worldJoy.Y = 0;
                robot.Command = worldJoy.LimitLength(1f);
            }
            float turn = Input.GetJoyAxis(pad, JoyAxis.RightX); if (Mathf.Abs(turn) > .15f) robot.TurnCommand = -turn;
            bool joyIntake = Input.IsJoyButtonPressed(pad, JoyButton.LeftShoulder);
            ref bool lastJoy = ref (second ? ref _lastJoyIntakePressedSecond : ref _lastJoyIntakePressed);
            if (joyIntake && !lastJoy)
            {
                robot.ToggleIntake();
            }
            lastJoy = joyIntake;
            robot.FireCommand |= Input.IsJoyButtonPressed(pad, JoyButton.RightShoulder);
        }
    }
    private void UpdateScore()
    {
        for (int s = 0; s < 2; s++)
        {
            foreach (var b in RedHive.Contents[s]) if (!b.Preload) _redScoredHive.Add(b);
            foreach (var b in BlueHive.Contents[s]) if (!b.Preload) _blueScoredHive.Add(b);
        }
        int red = RedHive.Tips * 20;
        foreach (var b in _redScoredHive)
            red += b.Kind == PieceKind.Pollen ? 2 : b.Kind == PieceKind.RedNectar ? 5 : 0;
        foreach (var flower in Flowers)
            if (flower.Owner == PieceKind.RedNectar) red += 2 * flower.ScoringCount;

        int blue = BlueHive.Tips * 20;
        foreach (var b in _blueScoredHive)
            blue += b.Kind == PieceKind.Pollen ? 2 : b.Kind == PieceKind.BlueNectar ? 5 : 0;
        foreach (var flower in Flowers)
            if (flower.Owner == PieceKind.BlueNectar) blue += 2 * flower.ScoringCount;

        RedScore = red;
        BlueScore = blue;
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (Hud.MenuVisible && e is InputEventKey menuKey && menuKey.PhysicalKeycode != Key.Escape) return;
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (Player.Rig?.HandleToggleKey(k) == true) { GetViewport().SetInputAsHandled(); return; }
            if (k.PhysicalKeycode == Key.Escape) TogglePause();
            else if (k.PhysicalKeycode == KeyCamera) Camera.Mode = (Camera.Mode + 1) % 3;
            else if (k.PhysicalKeycode == KeyReset) Reset();
            else if (k.PhysicalKeycode == KeyTarget) AimFlower = !AimFlower;
            else if (k.PhysicalKeycode == KeyDriveMode)
            {
                RobotCentricDrive = !RobotCentricDrive;
                Status = RobotCentricDrive ? "Drive Mode: Robot-Centric (Heading)" : "Drive Mode: Field-Centric (Fixed W/A/S/D)";
                SaveControls();
            }
            else if (k.PhysicalKeycode == Key.B && Practice && Running) SpawnBall(PieceKind.Pollen, Player.Position + Player.Front * .5f + Vector3.Up * .12f);
            else if (k.PhysicalKeycode == Key.M && Practice && Running) SpawnBall(Player.Red ? PieceKind.RedNectar : PieceKind.BlueNectar, Player.Position + Player.Front * .5f + Vector3.Up * .12f);
            else if (k.PhysicalKeycode == KeyHuman) DropHuman(Player.Red);
            else if (k.PhysicalKeycode == KeyExtract) KnockFlower(Player);
            else if (k.PhysicalKeycode == Key.I) Player.Rig?.SelectNextMotor();
            else if (k.PhysicalKeycode == Key.F5) Save();
            else if (k.PhysicalKeycode == Key.F9) Load();
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
        if (!Running) return;
        var column = Flowers.OrderBy(f => f.Base.DistanceSquaredTo(robot.Position)).First();
        if (column.Base.DistanceTo(robot.Position) < .5f) column.ReleaseBottom();
    }
    public void TogglePause()
    {
        if (!Started) return;
        if (Finished) { Hud.ShowResults(); return; }
        Paused = !Paused;
        SetFrozen(Paused);
        Hud.ShowPause(Paused);
    }
    public void SetFrozen(bool frozen)
    {
        _session.ProcessMode = frozen ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        foreach (var robot in Robots) robot.Rig?.SetFrozen(frozen);
        foreach (var ball in Balls) if (!ball.Stored)
        {
            if (frozen && !_pausedVelocities.ContainsKey(ball)) _pausedVelocities[ball] = (ball.LinearVelocity, ball.AngularVelocity);
            ball.Freeze = frozen;
            if (!frozen && _pausedVelocities.Remove(ball, out var velocities)) { ball.LinearVelocity = velocities.Linear; ball.AngularVelocity = velocities.Angular; }
        }
    }
    public void Save()
    {
        try
        {
            var snapshot=Capture();
            using var file=Godot.FileAccess.Open("user://practice.json",Godot.FileAccess.ModeFlags.Write);
            if (file==null) throw new Exception("Cannot open save file: "+Godot.FileAccess.GetOpenError());
            file.StoreString(JsonSerializer.Serialize(snapshot));
            Status="Scene saved: pieces, inventories, HIVE, flowers, timer";
        }
        catch (Exception ex) { Status="Cannot save: "+ex.Message; }
    }
    public bool Load()
    {
        if (!Godot.FileAccess.FileExists("user://practice.json")) { Status = "No saved scene"; return false; }
        try
        {
            var data = JsonSerializer.Deserialize<Snapshot>(Godot.FileAccess.GetFileAsString("user://practice.json"));
            Restore(data);
            if (!Finished) Hud.ShowPause(false);
            Status = "Scene restored";
            return true;
        }
        catch (Exception ex) { Status = "Cannot load: " + ex.Message; return false; }
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
            Version = 2, Practice = Practice, Time = Elapsed, TwoPlayers = TwoPlayers, Profile = Profile.Copy(),
            AimFlower = AimFlower,
            RedAngle = RedHive.Angle, BlueAngle = BlueHive.Angle, RedSide = RedHive.UpSide, BlueSide = BlueHive.UpSide,
            RedAngularVelocity = RedHive.AngularVelocity, BlueAngularVelocity = BlueHive.AngularVelocity,
            RedTips = RedHive.Tips, BlueTips = BlueHive.Tips, Tokens = (int[])HumanTokens.Clone(),
            Robots = Robots.Select(r => new RobotData { X = r.Position.X, Z = r.Position.Z, Yaw = r.Rotation.Y, Inventory = r.Inventory.ToArray(),
                Mechanisms = r.Rig?.Capture(), ShotsFired=r.ShotsFired, ShotsMade=r.ShotsMade, Vx = r.Velocity.X, Vz = r.Velocity.Z, Cooldown = r.Cooldown, Speed = r.Speed, Acceleration = r.Acceleration, TurnSpeed = r.TurnSpeed }).ToArray(),
            Balls = Balls.Select(b => {
                string container = Container(b, out int slot);
                var linear = _pausedVelocities.TryGetValue(b, out var motion) ? motion.Linear : b.LinearVelocity;
                var angular = _pausedVelocities.TryGetValue(b, out motion) ? motion.Angular : b.AngularVelocity;
                return new BallData { Preload=b.Preload, ShotRobotIndex=b.ShotRobotIndex, ShotConfirmed=b.ShotConfirmed, Kind = b.Kind, X = b.Position.X, Y = b.Position.Y, Z = b.Position.Z,
                    Vx = linear.X, Vy = linear.Y, Vz = linear.Z, Wx = angular.X, Wy = angular.Y, Wz = angular.Z, Container = container, Slot = slot };
            }).ToArray()
        };
    }
    public void Restore(Snapshot data)
    {
        if (data == null || data.Version != 2 || data.Robots?.Length != 4 || data.Balls == null || data.Tokens?.Length != 2) throw new Exception("Unsupported snapshot");
        if (!float.IsFinite(data.Time+data.RedAngle+data.BlueAngle+data.RedAngularVelocity+data.BlueAngularVelocity)
            || data.Time<0 || Mathf.Abs(data.RedAngle)>Mathf.Pi/6+.001f || Mathf.Abs(data.BlueAngle)>Mathf.Pi/6+.001f
            || (data.RedSide!=1 && data.RedSide!=-1) || (data.BlueSide!=1 && data.BlueSide!=-1)
            || data.RedTips<0 || data.BlueTips<0 || data.Tokens.Any(n=>n<0)) throw new Exception("Invalid match or HIVE state");
        foreach (var r in data.Robots)
            if (r==null || !float.IsFinite(r.X+r.Z+r.Yaw+r.Vx+r.Vz+r.Cooldown+r.Speed+r.Acceleration+r.TurnSpeed)
                || r.X<0 || r.X>Arena.Size || r.Z>0 || r.Z < -Arena.Size || r.Inventory==null || r.Inventory.Length>4
                || r.Inventory.Any(k => !Enum.IsDefined(k)) || r.Speed<=0 || r.Acceleration<=0 || r.TurnSpeed<=0
                || r.ShotsFired<0 || r.ShotsMade<0 || r.ShotsMade>r.ShotsFired)
                throw new Exception("Invalid robot state");
        foreach (var b in data.Balls)
        {
            if (b==null || !float.IsFinite(b.X+b.Y+b.Z+b.Vx+b.Vy+b.Vz+b.Wx+b.Wy+b.Wz) || Math.Abs(b.X)>30
                || Math.Abs(b.Y)>30 || Math.Abs(b.Z)>30 || b.ShotRobotIndex < -1 || b.ShotRobotIndex>3 || !Enum.IsDefined(b.Kind)) throw new Exception("Invalid ball state");
            if (b.Container!="free")
            {
                string[] parts=b.Container?.Split(':');
                if (parts?.Length!=2 || !int.TryParse(parts[1],out int index) || index<0
                    || (parts[0]=="flower" ? index>=4 : index>=2)
                    || (parts[0]!="flower" && parts[0]!="red" && parts[0]!="blue" && parts[0]!="human"))
                    throw new Exception("Invalid piece container");
            }
        }
        var profile=data.Profile?.Copy() ?? new RobotProfile(); profile.Validate();
        for (int i = 0; i < data.Robots.Length; i++)
        {
            var rDef = (i == 0) ? profile.Imported : (AiSlotProfiles[i - 1] ?? AiProfile ?? profile)?.Imported;
            RobotLinkState.Validate(data.Robots[i].Mechanisms, rDef);
        }
        Practice=data.Practice; TwoPlayers=data.TwoPlayers; Profile=profile; AimFlower=data.AimFlower; Reset(); Elapsed=data.Time;
        for (int i = 0; i < 4; i++)
        {
            var d = data.Robots[i]; var r = Robots[i]; r.Position = new(d.X, 0, d.Z); r.Rotation = new(0, d.Yaw, 0);
            r.Rig?.Restore(d.Mechanisms);
            r.Inventory.Clear(); r.Inventory.AddRange(d.Inventory); r.Velocity = new(d.Vx, 0, d.Vz); r.Cooldown = d.Cooldown;
            r.ShotsFired=d.ShotsFired; r.ShotsMade=d.ShotsMade; r.Speed = d.Speed; r.Acceleration = d.Acceleration; r.TurnSpeed = d.TurnSpeed;
        }
        RedHive.RestorePose(data.RedAngle, data.RedSide, data.RedTips, data.RedAngularVelocity); BlueHive.RestorePose(data.BlueAngle, data.BlueSide, data.BlueTips, data.BlueAngularVelocity);
        _redLastTips = data.RedTips; _blueLastTips = data.BlueTips; Array.Copy(data.Tokens, HumanTokens, 2);
        foreach (var ball in Balls.ToArray()) RemoveBall(ball);
        foreach (var list in RedHive.Contents.Concat(BlueHive.Contents)) list.Clear();
        foreach (var f in Flowers) f.Balls.Clear(); foreach (var stock in HumanStock) stock.Clear();
        foreach (var b in data.Balls.OrderBy(b => b.Slot))
        {
            var ball = SpawnBall(b.Kind, new(b.X, b.Y, b.Z));
            ball.Preload = b.Preload;
            ball.ShotRobotIndex=b.ShotRobotIndex; ball.ShotConfirmed=b.ShotConfirmed;
            if (ball.ShotRobotIndex >= 0) Robots[ball.ShotRobotIndex].ExcludeLauncher(ball);
            if (b.Container != "free")
            {
                string[] parts = b.Container.Split(':'); int index = int.Parse(parts[1]);
                if (parts[0] == "red") { RedHive.Store(ball, index == 0 ? -1 : 1); if (!ball.Preload) _redScoredHive.Add(ball); }
                else if (parts[0] == "blue") { BlueHive.Store(ball, index == 0 ? -1 : 1); if (!ball.Preload) _blueScoredHive.Add(ball); }
                else if (parts[0] == "flower") Flowers[index].Store(ball);
                else if (parts[0] == "human") { HumanStock[index].Add(ball); ball.Stored = true; ball.Freeze = true; ball.CollisionLayer = 0; ball.CollisionMask = 0; }
            }
            if (!ball.Stored) { ball.Position = new(b.X,b.Y,b.Z); ball.LinearVelocity = new(b.Vx, b.Vy, b.Vz); ball.AngularVelocity = new(b.Wx, b.Wy, b.Wz); }
        }
        UpdateScore();
        if (Finished) { SetFrozen(true); Hud.ShowResults(); }
    }
    public sealed class Snapshot
    {
        public int Version { get; set; } public bool Practice { get; set; } public bool TwoPlayers { get; set; } public float Time { get; set; }
        public RobotProfile Profile { get; set; }
        public bool AimFlower { get; set; }
        public float RedAngle { get; set; } public float BlueAngle { get; set; } public int RedSide { get; set; } public int BlueSide { get; set; }
        public float RedAngularVelocity { get; set; } public float BlueAngularVelocity { get; set; }
        public int RedTips { get; set; } public int BlueTips { get; set; } public int[] Tokens { get; set; }
        public RobotData[] Robots { get; set; } public BallData[] Balls { get; set; }
    }
    public sealed class RobotData { public int ShotsFired {get;set;} public int ShotsMade {get;set;} public float X { get; set; } public float Z { get; set; } public float Yaw { get; set; } public PieceKind[] Inventory { get; set; }
        public RobotLinkState[] Mechanisms { get; set; }
        public float Vx { get; set; } public float Vz { get; set; } public float Cooldown { get; set; } public float Speed { get; set; } public float Acceleration { get; set; } public float TurnSpeed { get; set; } }
    public sealed class BallData { public int ShotRobotIndex {get;set;}=-1; public bool ShotConfirmed {get;set;} public bool Preload {get;set;} public PieceKind Kind { get; set; } public float X { get; set; } public float Y { get; set; } public float Z { get; set; }
        public float Vx { get; set; } public float Vy { get; set; } public float Vz { get; set; } public float Wx { get; set; } public float Wy { get; set; } public float Wz { get; set; }
        public string Container { get; set; } public int Slot { get; set; } }
    public async void AuditTest() => await SimulatorRegressionChecks.Run(this);
    public async void GravityTest() => await SimulatorRegressionChecks.RunGravity(this);
    public async void ContactTest() => await PieceContactChecks.Run(this);
    public async void FpsTest() => await SimulatorPerformanceChecks.Run(this);
    public async void ShotTest() => await ShotPlanningChecks.Run(this);
    public async void RenderedPreviewTest()
    {
        try
        {
            if (DisplayServer.GetName()=="headless") throw new Exception("This check requires a rendered window");
            Testing=true; Practice=true; Reset(); foreach (var r in Robots) r.Bot=false;
            Hud.ShowPause(false); Player.Command=Vector3.Right;
            for (int i=0;i<30;i++)
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            float originError=0, targetError=0;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            for (int i=0;i<120;i++)
            {
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                originError=Mathf.Max(originError,Aim.DisplayOrigin.DistanceTo(Player.LaunchOrigin));
                targetError=Mathf.Max(targetError,Aim.DisplayEnd.DistanceTo(Target(Player)));
            }
            watch.Stop();
            GD.Print($"PREVIEW rendered: {120/watch.Elapsed.TotalSeconds:0.0} FPS; origin lag {originError*100:0.000} cm; target lag {targetError*100:0.000} cm; {Aim.PlanUpdates} collision refreshes");
            if (originError>.002f || targetError>.002f) throw new Exception("Trajectory lags behind the moving robot");
            GD.Print("PASS: Rendered trajectory follows moving robot without the old 200 ms delay"); GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
    public async void PerformanceTest()
    {
        Testing = true; Practice = true; Reset(); foreach (var r in Robots) r.Bot = false;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i=0;i<30;i++) ShotPlanner.TryPlan(Player,Player.LaunchOrigin,Target(Player),.03556f,out _);
        watch.Stop(); GD.Print($"PERF planner mean: {watch.Elapsed.TotalMilliseconds/30:0.000} ms");
        GetTree().Quit();
    }
    public async void SmokeTest()
    {
        try
        {
            void Check(bool condition, string label) { if (!condition) throw new Exception(label); GD.Print("PASS: " + label); }
            // Duratele scenariilor originale erau exprimate în pași la 120 Hz.
            async System.Threading.Tasks.Task Frames(int n) { for (int i=0;i<Mathf.CeilToInt(n*Engine.PhysicsTicksPerSecond/120f);i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
            int Count() => Balls.Count + Robots.Sum(r => r.Inventory.Count);
            Testing = true; Practice = true; Reset(); foreach (var r in Robots) r.Bot = false;
            Hud.ShowMenu();
            Hud.NavigateTo(SimulatorHud.ScreenType.RobotCreator);
            Hud.NavigateTo(SimulatorHud.ScreenType.PracticePositions);
            Hud.NavigateTo(SimulatorHud.ScreenType.AutonomousPathing);
            Hud.NavigateTo(SimulatorHud.ScreenType.AutonomousPathEditor);
            Hud.NavigateTo(SimulatorHud.ScreenType.Settings);
            Hud.NavigateTo(SimulatorHud.ScreenType.Help);
            Hud.NavigateTo(SimulatorHud.ScreenType.Pause);
            Hud.NavigateTo(SimulatorHud.ScreenType.Results);
            Hud.GoBack();
            Hud.CloseMenu();
            Check(true, "All HUD menu screens navigate and render cleanly");
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
            Camera.ResetView(); Camera._Process(0);
            Vector3 cameraBefore=Camera.Position;
            Camera._UnhandledInput(new InputEventMouseMotion { ButtonMask=MouseButtonMask.Middle, Relative=new(70,20) });
            Camera._Process(0);
            Check(Camera.Position.DistanceTo(cameraBefore)>.1f,"Middle mouse drag changes camera angle");
            Vector3 viewBefore=-Camera.GlobalBasis.Z;
            cameraBefore=Camera.Position;
            Camera._UnhandledInput(new InputEventMouseMotion { ButtonMask=MouseButtonMask.Middle, ShiftPressed=true, Relative=new(40,-20) });
            Camera._Process(0);
            Check(Camera.Position.DistanceTo(cameraBefore)>.1f && (-Camera.GlobalBasis.Z).DistanceTo(viewBefore)<.001f,
                "Shift-middle drag pans without changing camera angle");
            Camera.Mode=1;
            Camera._UnhandledInput(new InputEventMouseMotion { ButtonMask=MouseButtonMask.Middle, Relative=new(10,0) });
            Camera._Process(0); Check(Camera.Mode==0,"Dragging exits the fixed top view");
            Camera.ResetView(); Camera._Process(0);
            Check(Count() == 56 && Flowers.Sum(f => f.Balls.Count) == 16 && HumanStock.Sum(h => h.Count) == 10, "Initial distribution: 56 pieces, 16 in flowers, 10 in human stock");
            int before = Balls.Count; Player.Fire();
            Check(Balls.Count == before + 1 && Player.Inventory.Count == 3 && Count() == 56, "Launch conserves inventory");
            await Frames(180);
            Check(RedHive.Contents.Sum(c => c.Count) == 4, "A real launch enters the red HIVE without teleportation");
            Check(Player.ShotsFired==1 && Player.ShotsMade==1,"Telemetry counts one launched and landed shot");
            Check(Aim.PollenClear && Aim.NectarClear && RedHive.TipLoadFraction>=0 && RedHive.TipLoadFraction<=1,
                "Telemetry predicts both piece sizes and bounds the HIVE load bar");
            var blocker = new StaticBody3D { Position = new(Arena.Center, 2.5f, -.85f) };
            AddChild(blocker); blocker.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(Arena.Size + 2, 6, .1f) } });
            await Frames(2); Player.Cooldown = 0;
            int beforeBlockedShot = Balls.Count, beforeBlockedInventory = Player.Inventory.Count;
            Player.Fire();
            Check(Balls.Count == beforeBlockedShot && Player.Inventory.Count == beforeBlockedInventory && Player.ShotStatus.Contains("BLOCKED"), "Blocked launch preserves inventory");
            Check(Player.ShotsFired==1,"Blocked attempts are not counted as launched shots");
            RemoveChild(blocker); blocker.QueueFree(); await Frames(2);
            var snapshot = JsonSerializer.Deserialize<Snapshot>(JsonSerializer.Serialize(Capture()));
            Restore(snapshot); foreach (var r in Robots) r.Bot = false;
            Check(Count() == 56 && Player.Inventory.Count == 3 && RedHive.Contents.Sum(c => c.Count) == 4, "Save/load preserves free, held and stored pieces");
            Check(Player.ShotsFired==1 && Player.ShotsMade==1 && Balls.Count(b=>b.ShotConfirmed)==1,
                "Save/load preserves shot counters without recounting a landed piece");
            var invalid = JsonSerializer.Deserialize<Snapshot>(JsonSerializer.Serialize(Capture()));
            invalid.Balls[0].Container="flower:9";
            var playerBeforeInvalid=Player; bool rejected=false;
            try { Restore(invalid); } catch { rejected=true; }
            Check(rejected && Player==playerBeforeInvalid && Count()==56,"Invalid snapshot is rejected before replacing the scene");
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
            Player.Inventory.Clear(); Player.IntakeCount=2;
            var rearColumn=Flowers[0];
            Player.Position=rearColumn.Base+rearColumn.Inward*.31f;
            Vector3 toward=rearColumn.Inward;
            Player.Rotation=new(0,Mathf.Atan2(-toward.X,-toward.Z),0);
            Player.Intake=true; await Frames(5);
            Check(Player.Inventory.Count>0 && rearColumn.Balls.Count<4,"Rear intake extracts from a flower");
            Player.Intake=false;
            var member=RedHive.Contents.SelectMany(b=>b).First(); RemoveBall(member);
            await Frames(2);
            Check(!RedHive.Contents.SelectMany(b=>b).Contains(member),"Removing a physical piece also removes container references");
            SetFrozen(true); Paused=true;
            int flowerBeforePause=rearColumn.Balls.Count; KnockFlower(Player);
            Check(rearColumn.Balls.Count==flowerBeforePause,"Paused practice cannot extract pieces");
            Paused=false; SetFrozen(false);
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
