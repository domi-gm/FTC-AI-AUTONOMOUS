using Godot;
using System;

public partial class SimulatorHud : CanvasLayer
{
    public Simulation Game;
    private Control _root;
    private PanelContainer _menu;
    private VBoxContainer _content;
    private Label _red, _blue, _clock, _telemetry, _status;
    private HBoxContainer _score;
    private Label _aim;
    public override void _Ready()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(_root);
        var theme = new Theme { DefaultFontSize = 17 };
        if (ResourceLoader.Exists("res://Assets/Fonts/Poppins-Regular.ttf"))
            theme.DefaultFont=GD.Load<FontFile>("res://Assets/Fonts/Poppins-Regular.ttf");
        var buttonStyle = new StyleBoxFlat { BgColor = new("1a2231"), BorderColor = new("3b485d"), BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12 };
        theme.SetStylebox("normal", "Button", buttonStyle);
        buttonStyle.CornerRadiusTopLeft=6; buttonStyle.CornerRadiusTopRight=6;
        buttonStyle.CornerRadiusBottomLeft=6; buttonStyle.CornerRadiusBottomRight=6;
        var hover = (StyleBoxFlat)buttonStyle.Duplicate(); hover.BgColor = new("34445a"); hover.BorderColor = VisualFactory.Gold;
        theme.SetStylebox("hover", "Button", hover); theme.SetStylebox("pressed", "Button", hover);
        _root.Theme = theme;
        _score = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, OffsetTop = 16, OffsetLeft = -260, OffsetRight = 260, OffsetBottom = 80, AnchorLeft = .5f, AnchorRight = .5f };
        _root.AddChild(_score);
        _red = ScoreLabel(VisualFactory.Red); _clock = ScoreLabel(Colors.White); _blue = ScoreLabel(VisualFactory.Blue);
        _score.AddChild(_red); _score.AddChild(_clock); _score.AddChild(_blue);
        _telemetry = new Label { OffsetLeft = 24, OffsetTop = -128, OffsetRight = 560, OffsetBottom = -24, AnchorTop = 1, AnchorBottom = 1 };
        _root.AddChild(_telemetry);
        var aimPanel = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -260, OffsetRight = -24, OffsetTop = 100, OffsetBottom = 200 };
        aimPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.04f,.06f,.09f,.9f), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12 });
        _root.AddChild(aimPanel); _aim = new Label(); aimPanel.AddChild(_aim);
        _status = new Label { OffsetLeft = 24, OffsetTop = 16, OffsetRight = 380, OffsetBottom = 50 };
        _status.AddThemeColorOverride("font_color", new("a1afc6")); _root.AddChild(_status);
        var controls = new HBoxContainer { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = -455, OffsetRight = -24, OffsetTop = -62, OffsetBottom = -20 };
        _root.AddChild(controls);
        Button(controls, "CAMERA [C]", () => Game.Camera.Mode = (Game.Camera.Mode + 1) % 3);
        Button(controls, "RESET [R]", () => Game.Reset());
        Button(controls, "MENU [ESC]", () => { if (Game.Started) Game.TogglePause(); else ShowMenu(); });
        _menu = new PanelContainer { AnchorLeft = .5f, AnchorRight = .5f, AnchorTop = .5f, AnchorBottom = .5f, OffsetLeft = -295, OffsetRight = 295, OffsetTop = -280, OffsetBottom = 280 };
        _menu.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.045f, .06f, .09f, .96f), BorderColor = new("d4ac37"), BorderWidthTop = 2, ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 20, ContentMarginBottom = 20 });
        _root.AddChild(_menu);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _menu.AddChild(scroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 8); scroll.AddChild(_content);
    }
    private Label ScoreLabel(Color color)
    {
        var label = new Label { CustomMinimumSize = new(160, 50), HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeColorOverride("font_color", color); label.AddThemeFontSizeOverride("font_size", 28); return label;
    }
    private void Clear(string heading, string description = "")
    {
        foreach (var node in _content.GetChildren()) { _content.RemoveChild(node); node.QueueFree(); }
        _menu.Visible = true;
        var title = new Label { Text = heading, HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32); title.AddThemeColorOverride("font_color", VisualFactory.Gold); _content.AddChild(title);
        if (description != "") _content.AddChild(new Label { Text = description, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center });
    }
    private static Button Button(Node parent, string caption, Action action)
    {
        var button = new Button { Text = caption, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += action; parent.AddChild(button); return button;
    }
    public void ShowMenu()
    {
        Clear("BIOBUZZ SIMULATOR", "GODOT + C#  /  LOCAL DEVELOPMENT BUILD");
        Button(_content, "PLAY MATCH", () => Start(false));
        Button(_content, "PRACTICE", () => Start(true));
        Button(_content, "ROBOT CREATOR", RobotSetup);
        Button(_content, "TWO PLAYERS: " + (Game.TwoPlayers ? "ON" : "OFF"), () => { Game.TwoPlayers = !Game.TwoPlayers; ShowMenu(); });
        Button(_content, "SETTINGS", Settings);
        Button(_content, "HOW TO PLAY", Help);
        Button(_content, "ACCURACY / PORT STATUS", Accuracy);
        Button(_content, "QUIT", () => GetTree().Quit());
    }
    private void Start(bool practice) { Game.Practice = practice; Game.Reset(); _menu.Visible = false; }
    public void ShowResults()
    {
        Clear("MATCH COMPLETE", $"RED {Game.RedScore}   /   BLUE {Game.BlueScore}");
        Button(_content, "PLAY AGAIN", () => Start(false));
        Button(_content, "MAIN MENU", () => { Game.Started = false; ShowMenu(); });
    }
    public void ShowPause(bool paused)
    {
        if (!paused) { _menu.Visible = false; return; }
        Clear("PAUSED");
        Button(_content, "RESUME", Game.TogglePause);
        Button(_content, "ROBOT SETUP", RobotSetup);
        Button(_content, "SETTINGS", Settings);
        Button(_content, "SAVE PRACTICE POSITIONS [F5]", Game.Save);
        Button(_content, "LOAD PRACTICE POSITIONS [F9]", () => { Game.Load(); _menu.Visible = false; });
        Button(_content, "SAVE PATH", Game.PathEditor.Save);
        Button(_content, "LOAD PATH", Game.PathEditor.Load);
        Button(_content, "MAIN MENU", () => { Game.Started = false; ShowMenu(); });
    }
    private void Back() { if (Game.Started) ShowPause(true); else ShowMenu(); }
    private void Help()
    {
        Clear("HOW TO PLAY");
        _content.AddChild(new Label { Text = "WASD  Move relative to the camera\nW: screen forward / D: screen right\nQ / E  Rotate robot\nSHIFT / J  Hold to collect (max 4 pieces)\nSPACE  Launch toward selected target\nT  Target HIVE / FLOWER\nH  Human player releases NECTAR\nK  Extract bottom ball near FLOWER\nF2  Path editor / P follows / Backspace removes\nC  Orbit / top / robot camera\nRight mouse drag  Orbit • Wheel  Zoom\nB / N  Add POLLEN / NECTAR in practice\nR  Reset • ESC  Pause\nF5 / F9  Save / load practice positions\n\nPlayer 2: arrows, comma/period, Enter, slash\nGamepad: camera-relative left stick, right stick X, LB / RB", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        Button(_content, "BACK", Back);
    }
    private void Accuracy()
    {
        Clear("PORT STATUS");
        _content.AddChild(new Label { Text = "Reference: turtle-sim.com / downloaded client 2026-10-02.\n\nNative Godot implementation. Not yet an identical port.\n\nImplemented: arena, 4 robots, pieces, intake, ballistic launch, HIVE/flower capture, tipping, practice, match phases, camera, local 2-player input.\n\nPhysical HIVE and hollow flowers; live aim panel and trajectory.\n\nApproximate: HIVE impact coupling, extraction actuator, swerve physics, AI, scoring. Creator supports a subset of options. Geometric waypoint follower; Java Pedro is not integrated.\n\nLocal simulator; online services are outside the requested scope.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        Button(_content, "BACK", Back);
    }
    private void Settings()
    {
        Clear("SETTINGS");
        Slider("Camera distance", 3, 10, Game.Camera.Distance, value => Game.Camera.Distance = (float)value);
        Slider("Field of view", 30, 75, Game.Camera.Fov, value => Game.Camera.Fov = (float)value);
        Button(_content, "CAMERA: " + Game.Camera.Mode, () => { Game.Camera.Mode = (Game.Camera.Mode + 1) % 3; Settings(); });
        Button(_content, "BACK", Back);
    }
    private void Slider(string title, double min, double max, double value, Action<double> changed)
    {
        var label = new Label { Text = $"{title}: {value:0.00}" }; _content.AddChild(label);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = .05, Value = value, CustomMinimumSize = new(450, 24) };
        slider.ValueChanged += v => { label.Text = $"{title}: {v:0.00}"; changed(v); }; _content.AddChild(slider);
    }
    private void RobotSetup()
    {
        Clear("ROBOT CREATOR", "Local profile • Apply restarts the scene");
        Slider("Width (cm)", 25, 45.72, Game.Profile.WidthCm, v => Game.Profile.WidthCm = (float)v);
        Slider("Length (cm)", 25, 45.72, Game.Profile.LengthCm, v => Game.Profile.LengthCm = (float)v);
        Slider("Speed (m/s)", .3, 3, Game.Profile.Speed, v => Game.Profile.Speed = (float)v);
        Slider("Acceleration (m/s²)", .5, 8, Game.Profile.Acceleration, v => Game.Profile.Acceleration = (float)v);
        Slider("Turn speed (rad/s)", .5, 6, Game.Profile.TurnSpeed, v => Game.Profile.TurnSpeed = (float)v);
        Button(_content, $"TURRETS: {Game.Profile.Turrets}  /  INTAKES: {Game.Profile.Intakes}", () => { Game.Profile.Turrets = Game.Profile.Turrets % 3 + 1; RobotSetup(); });
        Button(_content, "TOGGLE SECOND INTAKE", () => { Game.Profile.Intakes = 3 - Game.Profile.Intakes; RobotSetup(); });
        Button(_content, "SAVE + APPLY", () => { Game.Profile.Save(); Game.Reset(); _menu.Visible = false; });
        Button(_content, "BACK", Back);
    }
    public override void _Process(double delta)
    {
        if (Game.Player == null) return;
        _red.Text = $"RED  {Game.RedScore}"; _blue.Text = $"{Game.BlueScore}  BLUE";
        string phase = Game.Practice ? "PRACTICE" : Game.Elapsed < 30 ? "AUTO" : Game.Elapsed < 38 ? "TRANSITION" : Game.Elapsed < 158 ? "TELEOP" : "FINAL";
        float time = Game.Practice ? Game.Elapsed : Game.Elapsed < 30 ? 30 - Game.Elapsed : Game.Elapsed < 38 ? 38 - Game.Elapsed : Mathf.Max(0, 158 - Game.Elapsed);
        _clock.Text = $"{(int)time / 60}:{(int)time % 60:00}  {phase}";
        _clock.AddThemeFontSizeOverride("font_size", 22);
        var p = Game.Player.Position;
        _aim.Text = $"AIM  /  {(Game.AimFlower ? "FLOWER" : "HIVE")}\n{Game.Aim.Status}\nLaunch speed: {Game.Aim.Speed:0.0} m/s";
        _aim.AddThemeColorOverride("font_color", Game.Aim.Status == "ON TARGET" ? new Color("63e6b0") : new Color("ff667b"));
        _telemetry.Text = $"R1   {p.X * 100:0.0}, {-p.Z * 100:0.0} cm\nINVENTORY {Game.Player.Inventory.Count}/4    INTAKE {(Game.Player.Intake ? "ON" : "OFF")}\nTARGET {(Game.AimFlower ? "FLOWER" : "HIVE")}    SHOT {Game.Player.ShotStatus}\nWASD Camera-relative   Q/E Turn   SHIFT Collect   SPACE Shoot";
        _status.Text = Game.Status;
    }
}
