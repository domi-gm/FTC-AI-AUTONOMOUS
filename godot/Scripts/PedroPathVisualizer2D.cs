using Godot;
using System;
using System.Collections.Generic;

public partial class PedroPathVisualizer2D : Control
{
    public AutonomousPathDefinition Path;
    public Action<int, Vector2> OnWaypointMoved;
    public Action<int> OnWaypointSelected;
    public Action OnWaypointDragFinished;
    public Action OnSimulationFinished;

    public int SelectedWaypointIndex = -1;
    public int HoveredWaypointIndex = -1;

    // Simulation state
    public bool IsSimulating { get; private set; }
    public bool IsPaused { get; private set; }
    public float SimElapsed { get; private set; }
    public int SimCurrentWp { get; private set; }
    public Vector2 SimRobotPos { get; private set; }
    public float SimRobotHeading { get; private set; }
    public bool SimActionActive { get; private set; }
    public float SimActionTimer { get; private set; }
    public float SimSpeedMultiplier { get; set; } = 1.0f;
    public int ShotsFired { get; private set; }

    private int _draggedWpIndex = -1;
    private float _pulseTimer = 0f;

    // Field sizing and transformation
    private float _fieldPx = 380f;
    private Vector2 _fieldOrigin = Vector2.Zero;

    // Colors
    private static readonly Color ColorBg = new("101318");
    private static readonly Color ColorTileEven = new("262930");
    private static readonly Color ColorTileOdd = new("22252c");
    private static readonly Color ColorGridLine = new(0.18f, 0.20f, 0.25f, 0.70f);
    private static readonly Color ColorWall = new("485263");
    private static readonly Color ColorRedAlliance = new("e04b4b");
    private static readonly Color ColorBlueAlliance = new("3b9ee0");
    private static readonly Color ColorGold = new("f5a623");
    private static readonly Color ColorCyan = new("00d2ff");
    private static readonly Color ColorGreen = new("00e676");
    private static readonly Color ColorPurple = new("b388ff");
    private static readonly Color ColorWhite = Colors.White;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(300, 300);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Process(double delta)
    {
        _pulseTimer += (float)delta * 4f;

        if (IsSimulating && !IsPaused && Path != null && Path.Waypoints.Count > 0)
        {
            float dt = (float)delta * SimSpeedMultiplier;
            SimElapsed += dt;

            if (SimCurrentWp >= Path.Waypoints.Count)
            {
                // Completed all waypoints
                IsSimulating = false;
                OnSimulationFinished?.Invoke();
                QueueRedraw();
                return;
            }

            var targetWp = Path.Waypoints[SimCurrentWp];
            Vector2 targetPos = new(targetWp.X, targetWp.Z);
            Vector2 diff = targetPos - SimRobotPos;
            float dist = diff.Length();

            if (SimActionActive)
            {
                SimActionTimer += dt;
                switch (targetWp.Action)
                {
                    case WaypointAction.Shoot:
                        // Aim turret at alliance hive
                        Vector2 hivePos = GetAllianceHivePos();
                        Vector2 toHive = (hivePos - SimRobotPos).Normalized();
                        SimRobotHeading = Mathf.LerpAngle(SimRobotHeading, Mathf.Atan2(toHive.X, -toHive.Y), 7f * dt);

                        if (SimActionTimer >= 0.20f && SimActionTimer - dt < 0.20f)
                        {
                            ShotsFired++;
                        }
                        if (SimActionTimer >= Math.Max(0.40f, targetWp.ActionParam))
                        {
                            SimActionActive = false;
                            SimActionTimer = 0f;
                            SimCurrentWp++;
                        }
                        break;

                    case WaypointAction.Wait:
                        if (SimActionTimer >= Math.Max(0.20f, targetWp.ActionParam))
                        {
                            SimActionActive = false;
                            SimActionTimer = 0f;
                            SimCurrentWp++;
                        }
                        break;

                    default:
                        SimActionActive = false;
                        SimActionTimer = 0f;
                        SimCurrentWp++;
                        break;
                }
            }
            else
            {
                if (dist > 0.05f)
                {
                    Vector2 dir = diff.Normalized();
                    float driveSpeed = 1.1f * Mathf.Clamp(targetWp.SpeedScale, 0.3f, 1.2f);
                    float step = Mathf.Min(dist, driveSpeed * dt);
                    SimRobotPos += dir * step;

                    float targetHeading = Mathf.Atan2(dir.X, -dir.Y);
                    SimRobotHeading = Mathf.LerpAngle(SimRobotHeading, targetHeading, 6f * dt);
                }
                else
                {
                    // Arrived at waypoint
                    SimActionActive = true;
                    SimActionTimer = 0f;

                    if (targetWp.Action == WaypointAction.IntakeOn || targetWp.Action == WaypointAction.IntakeOff || targetWp.Action == WaypointAction.None)
                    {
                        SimActionActive = false;
                        SimCurrentWp++;
                    }
                }
            }

            QueueRedraw();
        }
    }

    public Vector2 GetStartPosePosition()
    {
        return GetStartPoseForPosition(Path?.StartingPosition ?? "Red Near");
    }

    public static Vector2 GetStartPoseForPosition(string posName)
    {
        float size = Arena.Size;
        if (string.IsNullOrEmpty(posName)) return new Vector2(0.84f, -0.26f);
        if (posName.StartsWith("Red Far", StringComparison.OrdinalIgnoreCase))
            return new Vector2(0.28f, -2.75f);
        if (posName.StartsWith("Blue Near", StringComparison.OrdinalIgnoreCase))
            return new Vector2(size - 0.84f, -(size - 0.26f));
        if (posName.StartsWith("Blue Far", StringComparison.OrdinalIgnoreCase))
            return new Vector2(size - 0.28f, -0.84f);
        // Default: Red Near
        return new Vector2(0.84f, -0.26f);
    }

    public static float GetStartHeadingForPosition(string posName)
    {
        if (string.IsNullOrEmpty(posName)) return 0f;
        return posName.StartsWith("Blue", StringComparison.OrdinalIgnoreCase) ? Mathf.Pi : 0f;
    }

    public void StartSimulation()
    {
        if (Path == null || Path.Waypoints.Count == 0) return;

        IsSimulating = true;
        IsPaused = false;
        SimElapsed = 0f;
        SimCurrentWp = 0;
        SimActionTimer = 0f;
        SimActionActive = false;
        ShotsFired = 0;

        SimRobotPos = GetStartPosePosition();
        SimRobotHeading = GetStartHeadingForPosition(Path.StartingPosition);

        QueueRedraw();
    }

    public void TogglePauseSimulation()
    {
        if (!IsSimulating)
        {
            StartSimulation();
        }
        else
        {
            IsPaused = !IsPaused;
            QueueRedraw();
        }
    }

    public void ResetSimulation()
    {
        IsSimulating = false;
        IsPaused = false;
        SimElapsed = 0f;
        SimCurrentWp = 0;
        SimActionTimer = 0f;
        SimActionActive = false;
        ShotsFired = 0;

        SimRobotPos = GetStartPosePosition();
        SimRobotHeading = GetStartHeadingForPosition(Path?.StartingPosition);

        QueueRedraw();
    }

    private Vector2 GetAllianceHivePos()
    {
        bool isRed = Path == null || Path.StartingPosition.StartsWith("Red", StringComparison.OrdinalIgnoreCase);
        float hx = isRed ? (Arena.Size / 2 - 12.75f * 0.0254f) : (Arena.Size / 2 + 12.75f * 0.0254f);
        float hz = -(Arena.Size / 2);
        return new Vector2(hx, hz);
    }

    public Vector2 WorldToCanvas(float x, float z)
    {
        float ratioX = Mathf.Clamp(x / Arena.Size, 0f, 1f);
        float ratioY = Mathf.Clamp(-z / Arena.Size, 0f, 1f);
        return _fieldOrigin + new Vector2(ratioX * _fieldPx, ratioY * _fieldPx);
    }

    public Vector2 CanvasToWorld(Vector2 canvasPos)
    {
        float ratioX = Mathf.Clamp((canvasPos.X - _fieldOrigin.X) / _fieldPx, 0f, 1f);
        float ratioY = Mathf.Clamp((canvasPos.Y - _fieldOrigin.Y) / _fieldPx, 0f, 1f);
        float x = Mathf.Clamp(ratioX * Arena.Size, 0.20f, Arena.Size - 0.20f);
        float z = -Mathf.Clamp(ratioY * Arena.Size, 0.20f, Arena.Size - 0.20f);
        return new Vector2(x, z);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (Path == null) return;

        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    int clickedIdx = FindWaypointAtPos(mb.Position);
                    if (clickedIdx >= 0)
                    {
                        _draggedWpIndex = clickedIdx;
                        SelectedWaypointIndex = clickedIdx;
                        OnWaypointSelected?.Invoke(clickedIdx);
                        QueueRedraw();
                    }
                }
                else
                {
                    if (_draggedWpIndex >= 0)
                    {
                        _draggedWpIndex = -1;
                        OnWaypointDragFinished?.Invoke();
                    }
                    _draggedWpIndex = -1;
                }
            }
        }
        else if (e is InputEventMouseMotion mm)
        {
            if (_draggedWpIndex >= 0 && _draggedWpIndex < Path.Waypoints.Count)
            {
                Vector2 world = CanvasToWorld(mm.Position);
                Path.Waypoints[_draggedWpIndex].X = world.X;
                Path.Waypoints[_draggedWpIndex].Z = world.Y;
                OnWaypointMoved?.Invoke(_draggedWpIndex, world);
                QueueRedraw();
            }
            else
            {
                int hovered = FindWaypointAtPos(mm.Position);
                if (hovered != HoveredWaypointIndex)
                {
                    HoveredWaypointIndex = hovered;
                    MouseDefaultCursorShape = (hovered >= 0) ? CursorShape.PointingHand : CursorShape.Arrow;
                    QueueRedraw();
                }
            }
        }
    }

    private int FindWaypointAtPos(Vector2 canvasPos)
    {
        if (Path == null || Path.Waypoints == null) return -1;
        float hitDist = 20f;
        for (int i = 0; i < Path.Waypoints.Count; i++)
        {
            var wp = Path.Waypoints[i];
            Vector2 p = WorldToCanvas(wp.X, wp.Z);
            if (p.DistanceTo(canvasPos) <= hitDist)
            {
                return i;
            }
        }
        return -1;
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        float margin = 16f;
        _fieldPx = Mathf.Min(size.X - margin * 2, size.Y - margin * 2);
        _fieldOrigin = new Vector2((size.X - _fieldPx) / 2, (size.Y - _fieldPx) / 2);

        // 1. Draw Field Background & Border
        DrawRect(new Rect2(Vector2.Zero, size), ColorBg);

        // Field outer perimeter
        Rect2 fieldRect = new(_fieldOrigin, new Vector2(_fieldPx, _fieldPx));
        DrawRect(fieldRect, new Color("1c1e24"));

        // 2. 6x6 FTC Soft Foam Tiles
        float tileSize = _fieldPx / 6f;
        for (int x = 0; x < 6; x++)
        {
            for (int y = 0; y < 6; y++)
            {
                Rect2 tRect = new(_fieldOrigin.X + x * tileSize, _fieldOrigin.Y + y * tileSize, tileSize, tileSize);
                Color tColor = (x + y) % 2 == 0 ? ColorTileEven : ColorTileOdd;
                DrawRect(tRect, tColor);
                DrawRect(tRect, ColorGridLine, false, 1f);
            }
        }

        // 3. FTC Alliance Structures & Hives
        DrawFieldMarkings(tileSize);

        // 4. Waypoint Paths & Trajectory
        if (Path != null && Path.Waypoints != null && Path.Waypoints.Count > 0)
        {
            DrawTrajectory();
            DrawWaypoints();
        }

        // 5. Robot Simulation Mesh / Marker
        DrawRobot();

        // 6. Perimeter Walls
        DrawRect(fieldRect, ColorWall, false, 3.5f);

        // Subtle Title / Status Pill
        DrawHeaderPill();
    }

    private void DrawFieldMarkings(float tileSize)
    {
        // Red starting zone (Top-Left and Bottom-Left perimeters)
        Rect2 redZone1 = new(_fieldOrigin.X, _fieldOrigin.Y + tileSize * 4, tileSize * 0.45f, tileSize);
        DrawRect(redZone1, new Color(ColorRedAlliance, 0.25f));
        DrawRect(redZone1, ColorRedAlliance, false, 1.5f);

        // Blue starting zone (Top-Right and Bottom-Right perimeters)
        Rect2 blueZone1 = new(_fieldOrigin.X + _fieldPx - tileSize * 0.45f, _fieldOrigin.Y + tileSize, tileSize * 0.45f, tileSize);
        DrawRect(blueZone1, new Color(ColorBlueAlliance, 0.25f));
        DrawRect(blueZone1, ColorBlueAlliance, false, 1.5f);

        // Center Truss / Submersible Axle
        Vector2 center = _fieldOrigin + new Vector2(_fieldPx / 2, _fieldPx / 2);
        DrawLine(center - new Vector2(_fieldPx * 0.35f, 0), center + new Vector2(_fieldPx * 0.35f, 0), new Color(0.6f, 0.65f, 0.72f, 0.4f), 3.0f);

        // Red Hive (Left of Center)
        Vector2 redHive = WorldToCanvas(Arena.Size / 2 - 12.75f * 0.0254f, -(Arena.Size / 2));
        Rect2 redHiveRect = new(redHive - new Vector2(18, 22), new Vector2(36, 44));
        DrawRect(redHiveRect, new Color(ColorRedAlliance, 0.35f));
        DrawRect(redHiveRect, ColorRedAlliance, false, 2f);
        DrawCircle(redHive, 4f, ColorRedAlliance);

        // Blue Hive (Right of Center)
        Vector2 blueHive = WorldToCanvas(Arena.Size / 2 + 12.75f * 0.0254f, -(Arena.Size / 2));
        Rect2 blueHiveRect = new(blueHive - new Vector2(18, 22), new Vector2(36, 44));
        DrawRect(blueHiveRect, new Color(ColorBlueAlliance, 0.35f));
        DrawRect(blueHiveRect, ColorBlueAlliance, false, 2f);
        DrawCircle(blueHive, 4f, ColorBlueAlliance);

        // Flower Column Spots
        Color flowerColor = new(1f, 0.85f, 0.2f, 0.35f);
        DrawCircle(WorldToCanvas(0.10f, -Arena.Size * 2 / 6), 7f, flowerColor);
        DrawCircle(WorldToCanvas(Arena.Size * 2 / 6, -Arena.Size + 0.10f), 7f, flowerColor);
        DrawCircle(WorldToCanvas(Arena.Size - 0.10f, -Arena.Size * 4 / 6), 7f, flowerColor);
        DrawCircle(WorldToCanvas(Arena.Size * 4 / 6, -0.10f), 7f, flowerColor);
    }

    private void DrawTrajectory()
    {
        if (Path == null || Path.Waypoints == null || Path.Waypoints.Count == 0) return;

        var wps = Path.Waypoints;

        // Draw line from Robot Start Pose to Waypoint 1 (wps[0])
        Vector2 startP = WorldToCanvas(GetStartPosePosition().X, GetStartPosePosition().Y);
        Vector2 firstP = WorldToCanvas(wps[0].X, wps[0].Z);
        DrawTrajectorySegment(startP, firstP, new Color(ColorCyan, 0.75f));

        // Connect waypoints: wps[0] -> wps[1] -> wps[2] ...
        for (int i = 0; i < wps.Count - 1; i++)
        {
            Vector2 p1 = WorldToCanvas(wps[i].X, wps[i].Z);
            Vector2 p2 = WorldToCanvas(wps[i + 1].X, wps[i + 1].Z);
            DrawTrajectorySegment(p1, p2, ColorCyan);
        }
    }

    private void DrawTrajectorySegment(Vector2 p1, Vector2 p2, Color col)
    {
        // Glow line underneath
        DrawLine(p1, p2, new Color(col, 0.25f), 5.0f);
        // Crisp core line
        DrawLine(p1, p2, col, 2.2f);

        // Directional arrow at midpoint
        Vector2 mid = (p1 + p2) / 2f;
        Vector2 dir = (p2 - p1).Normalized();
        Vector2 normal = new(-dir.Y, dir.X);
        Vector2 arrowTip = mid + dir * 6f;
        Vector2 arrowLeft = mid - dir * 4f + normal * 4f;
        Vector2 arrowRight = mid - dir * 4f - normal * 4f;
        DrawLine(arrowTip, arrowLeft, col, 2.0f);
        DrawLine(arrowTip, arrowRight, col, 2.0f);
    }

    private void DrawWaypoints()
    {
        var font = ThemeDB.FallbackFont;

        // 1. Draw START POSE Node
        Vector2 startPosCanvas = WorldToCanvas(GetStartPosePosition().X, GetStartPosePosition().Y);
        DrawCircle(startPosCanvas, 14f, new Color("14161d"));
        DrawCircle(startPosCanvas, 12f, ColorGreen);
        DrawCircle(startPosCanvas, 9f, new Color("1e222b"));
        DrawString(font, startPosCanvas - new Vector2(3.5f, -4f), "S", HorizontalAlignment.Center, -1, 11, ColorWhite);
        DrawString(font, startPosCanvas + new Vector2(-16f, -15f), "START", HorizontalAlignment.Center, 32, 10, ColorGreen);

        // 2. Draw Waypoints #1, #2, ...
        for (int i = 0; i < Path.Waypoints.Count; i++)
        {
            var wp = Path.Waypoints[i];
            Vector2 pos = WorldToCanvas(wp.X, wp.Z);
            bool isSelected = (i == SelectedWaypointIndex);
            bool isHovered = (i == HoveredWaypointIndex);
            bool isSimTarget = IsSimulating && (i == SimCurrentWp);

            Color actColor = wp.Action switch
            {
                WaypointAction.Shoot => ColorGold,
                WaypointAction.IntakeOn => ColorGreen,
                WaypointAction.IntakeOff => new Color("ef5350"),
                WaypointAction.Wait => ColorPurple,
                _ => ColorCyan
            };

            float radius = isSelected ? 16f : (isHovered ? 15f : 13f);

            // Pulse ring on simulated or selected waypoint
            if (isSimTarget || isSelected)
            {
                float pulse = 16f + Mathf.Sin(_pulseTimer) * 4f;
                DrawArc(pos, pulse, 0, Mathf.Tau, 32, new Color(actColor, 0.45f), 2.5f);
            }

            // Outer node circle
            DrawCircle(pos, radius, new Color("14161d"));
            DrawCircle(pos, radius - 2f, actColor);

            // Core center
            DrawCircle(pos, radius - 4f, new Color("1e222b"));

            // Waypoint Number text
            string numStr = (i + 1).ToString();
            DrawString(font, pos - new Vector2(4.5f, -5f), numStr, HorizontalAlignment.Center, -1, 13, ColorWhite);

            // Mini Action Badge Tag above waypoint
            if (wp.Action != WaypointAction.None)
            {
                string tag = wp.Action switch
                {
                    WaypointAction.Shoot => "SHOOT",
                    WaypointAction.IntakeOn => "IN-ON",
                    WaypointAction.IntakeOff => "IN-OFF",
                    WaypointAction.Wait => $"{wp.ActionParam:0.0}s",
                    _ => ""
                };
                DrawString(font, pos + new Vector2(-16f, -16f), tag, HorizontalAlignment.Center, 32, 10, actColor);
            }
        }
    }

    private void DrawRobot()
    {
        Vector2 robotPos;
        float heading;

        if (IsSimulating)
        {
            robotPos = WorldToCanvas(SimRobotPos.X, SimRobotPos.Y);
            heading = SimRobotHeading;
        }
        else
        {
            Vector2 startP = GetStartPosePosition();
            robotPos = WorldToCanvas(startP.X, startP.Y);
            heading = GetStartHeadingForPosition(Path?.StartingPosition);
        }

        float robW = (_fieldPx / Arena.Size) * 0.45f; // 45cm chassis
        float robH = (_fieldPx / Arena.Size) * 0.45f;

        // Transform points with heading rotation
        Vector2 fwd = new Vector2(Mathf.Sin(heading), -Mathf.Cos(heading));
        Vector2 right = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));

        Vector2 pFrontLeft = robotPos + fwd * (robH / 2) - right * (robW / 2);
        Vector2 pFrontRight = robotPos + fwd * (robH / 2) + right * (robW / 2);
        Vector2 pBackRight = robotPos - fwd * (robH / 2) + right * (robW / 2);
        Vector2 pBackLeft = robotPos - fwd * (robH / 2) - right * (robW / 2);

        Vector2[] chassisPoly = { pFrontLeft, pFrontRight, pBackRight, pBackLeft };
        DrawColoredPolygon(chassisPoly, new Color("1e222d", 0.90f));
        DrawPolyline(new Vector2[] { pFrontLeft, pFrontRight, pBackRight, pBackLeft, pFrontLeft }, ColorWhite, 2.0f);

        // Front Intake Bar (Green if simulating intake)
        Color intakeColor = (IsSimulating && SimCurrentWp > 0 && Path.Waypoints[SimCurrentWp - 1].Action == WaypointAction.IntakeOn)
            ? ColorGreen : new Color(0.5f, 0.5f, 0.5f, 0.8f);
        DrawLine(pFrontLeft, pFrontRight, intakeColor, 4.0f);

        // Direction arrow inside chassis
        Vector2 tip = robotPos + fwd * (robH * 0.35f);
        Vector2 leftWing = robotPos - fwd * (robH * 0.15f) - right * (robW * 0.25f);
        Vector2 rightWing = robotPos - fwd * (robH * 0.15f) + right * (robW * 0.25f);
        DrawColoredPolygon(new Vector2[] { tip, rightWing, leftWing }, ColorCyan);

        // Shooting beam animation if currently shooting
        if (IsSimulating && SimActionActive && SimCurrentWp < Path.Waypoints.Count && Path.Waypoints[SimCurrentWp].Action == WaypointAction.Shoot)
        {
            Vector2 hive = GetAllianceHivePos();
            Vector2 hiveCanvas = WorldToCanvas(hive.X, hive.Y);
            DrawLine(robotPos, hiveCanvas, new Color(ColorGold, 0.8f), 3.0f);
            DrawCircle(hiveCanvas, 8f + Mathf.Sin(_pulseTimer * 2f) * 3f, ColorGold);
        }
    }

    private void DrawHeaderPill()
    {
        if (!IsSimulating) return;
        var font = ThemeDB.FallbackFont;
        string statusText = IsPaused ? "SIMULATOR: PAUSED" : $"SIMULATING: WP #{SimCurrentWp + 1} ({SimElapsed:0.0}s)";
        Color tagColor = IsPaused ? ColorGold : ColorGreen;
        Rect2 pillRect = new(_fieldOrigin.X + 8, _fieldOrigin.Y + 8, 190, 22);
        DrawRect(pillRect, new Color("0e1116", 0.85f));
        DrawRect(pillRect, tagColor, false, 1.2f);
        DrawString(font, pillRect.Position + new Vector2(8, 15), statusText, HorizontalAlignment.Left, -1, 11, ColorWhite);
    }
}
