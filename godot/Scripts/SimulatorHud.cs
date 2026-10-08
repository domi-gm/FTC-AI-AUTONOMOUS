using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public partial class SimulatorHud : CanvasLayer
{
    public Simulation Game;

    public enum ScreenType
    {
        None,
        Main,
        Pause,
        RobotCreator,
        AutonomousPathing,
        Settings,
        Help,
        Results
    }

    private readonly Stack<ScreenType> _navStack = new();
    private ScreenType _currentScreen = ScreenType.None;

    // Root containers
    private Control _root;
    private RobotProfile _draft;
    public bool MenuVisible => _currentScreen != ScreenType.None;

    private Control _hudLayer;
    private ColorRect _modalScrim;
    private PanelContainer _dialogPanel;
    private VBoxContainer _dialogHeader;
    private ScrollContainer _dialogScroll;
    private VBoxContainer _dialogBody;
    private Label _dialogTitle;
    private Label _dialogSubtitle;

    private PanelContainer _scoreCard;
    private Label _redScoreLabel, _blueScoreLabel;
    private Label _phaseBadge, _clockLabel;
    private PanelContainer _telemetryCard;
    private Label _telemetryHeader, _telemetrySubsystems, _telemetryHints;
    private HBoxContainer _inventorySlots;
    private PanelContainer _statusToast;
    private Label _statusToastLabel;
    private HBoxContainer _controlsBar;
    private Button _cameraQuickBtn, _targetQuickBtn;
    private RobotTelemetryPanel _telemetryPanel;

    // Classic Theme Palette (Restored original aesthetic, scaled 1.5x)
    private static readonly Color Gold = VisualFactory.Gold;           // #f2c230
    private static readonly Color GoldBorder = new("d4ac37");
    private static readonly Color Red = VisualFactory.Red;             // #e0453a
    private static readonly Color Blue = VisualFactory.Blue;           // #3b7de0
    private static readonly Color Green = new("49ad58");
    private static readonly Color Orange = new("e67e22");
    private static readonly Color Purple = new("9b59b6");
    private static readonly Color TextWhite = Colors.White;
    private static readonly Color TextMuted = new("a1afc6");
    private static readonly Color TextDark = new("68768e");
    private static readonly Color DialogBg = new(0.045f, 0.06f, 0.09f, 0.97f);
    private static readonly Color CardBg = new(0.075f, 0.10f, 0.15f, 0.92f);
    private static readonly Color CardInner = new(0.10f, 0.135f, 0.195f, 0.88f);
    private static readonly Color BtnNormalBg = new("1a2231");
    private static readonly Color BtnNormalBorder = new("3b485d");
    private static readonly Color BtnHoverBg = new("34445a");
    private static readonly Color ScrimColor = new(0.02f, 0.035f, 0.06f, 0.78f);

    public override void _Ready()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        SetupTheme();
        BuildHudLayer();
        BuildModalDialog();

        _modalScrim.Visible = false;
        _dialogPanel.Visible = false;
    }

    private void SetupTheme()
    {
        // 1.5x scaled base font size: 21px
        var theme = new Theme { DefaultFontSize = 21 };
        if (ResourceLoader.Exists("res://Assets/Fonts/Poppins-Regular.ttf"))
        {
            var font = GD.Load<FontFile>("res://Assets/Fonts/Poppins-Regular.ttf");
            theme.DefaultFont = font;
        }

        // 1.5x scaled button styling with original clean look
        var btnNormal = CreateBox(BtnNormalBg, BtnNormalBorder, 8, 1, 24, 14);
        var btnHover = CreateBox(BtnHoverBg, Gold, 8, 1, 24, 14);
        var btnPressed = CreateBox(new("121824"), Gold, 8, 1, 24, 14);
        var btnFocus = CreateBox(BtnHoverBg, Gold, 8, 2, 24, 14);
        var btnDisabled = CreateBox(new(0.07f, 0.09f, 0.13f, 0.6f), new(0.13f, 0.17f, 0.23f, 0.5f), 8, 1, 24, 14);

        theme.SetStylebox("normal", "Button", btnNormal);
        theme.SetStylebox("hover", "Button", btnHover);
        theme.SetStylebox("pressed", "Button", btnPressed);
        theme.SetStylebox("focus", "Button", btnFocus);
        theme.SetStylebox("disabled", "Button", btnDisabled);

        theme.SetColor("font_color", "Button", TextWhite);
        theme.SetColor("font_hover_color", "Button", TextWhite);
        theme.SetColor("font_pressed_color", "Button", Gold);
        theme.SetColor("font_focus_color", "Button", TextWhite);
        theme.SetColor("font_disabled_color", "Button", TextDark);

        // Slider Styling (1.5x scaled)
        var sliderTrack = CreateBox(new("121824"), BtnNormalBorder, 4, 1, 0, 6);
        var sliderFill = CreateBox(Gold, Colors.Transparent, 4, 0, 0, 6);
        theme.SetStylebox("slider", "HSlider", sliderTrack);
        theme.SetStylebox("grabber_area", "HSlider", sliderFill);
        theme.SetStylebox("grabber_area_highlight", "HSlider", sliderFill);

        _root.Theme = theme;
    }

    private static StyleBoxFlat CreateBox(Color bg, Color border, int radius = 8, int borderWidth = 1, int padX = 16, int padY = 12)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = padX,
            ContentMarginRight = padX,
            ContentMarginTop = padY,
            ContentMarginBottom = padY
        };
    }

    // =========================================================================
    // HUD LAYER SETUP (Scaled 1.5x, spacious, no overlap)
    // =========================================================================
    private void BuildHudLayer()
    {
        _hudLayer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _hudLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_hudLayer);

        // 1. Top Match Score Bar (Spacious 760px wide, 80px tall)
        _scoreCard = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -380,
            OffsetRight = 380,
            OffsetTop = 16,
            OffsetBottom = 92,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _scoreCard.AddThemeStyleboxOverride("panel", CreateBox(DialogBg, BtnNormalBorder, 10, 1, 24, 8));
        _hudLayer.AddChild(_scoreCard);

        var scoreLayout = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        scoreLayout.AddThemeConstantOverride("separation", 28);
        _scoreCard.AddChild(scoreLayout);

        // Red alliance score
        var redCol = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        redCol.AddThemeConstantOverride("separation", 10);
        var redBadge = CreateBadge("RED", Red, Colors.White, 14);
        redBadge.CustomMinimumSize = new(64, 30);
        _redScoreLabel = new Label { Text = "0", HorizontalAlignment = HorizontalAlignment.Left };
        _redScoreLabel.AddThemeFontSizeOverride("font_size", 34);
        _redScoreLabel.AddThemeColorOverride("font_color", Red);
        redCol.AddChild(redBadge);
        redCol.AddChild(_redScoreLabel);
        scoreLayout.AddChild(redCol);

        // Center timer & phase
        var centerCol = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        centerCol.CustomMinimumSize = new(240, 0);
        centerCol.AddThemeConstantOverride("separation", 2);
        _phaseBadge = new Label { Text = "AUTO", HorizontalAlignment = HorizontalAlignment.Center };
        _phaseBadge.AddThemeFontSizeOverride("font_size", 14);
        _phaseBadge.AddThemeColorOverride("font_color", Gold);
        _clockLabel = new Label { Text = "0:30", HorizontalAlignment = HorizontalAlignment.Center };
        _clockLabel.AddThemeFontSizeOverride("font_size", 28);
        _clockLabel.AddThemeColorOverride("font_color", TextWhite);
        centerCol.AddChild(_phaseBadge);
        centerCol.AddChild(_clockLabel);
        scoreLayout.AddChild(centerCol);

        // Blue alliance score
        var blueCol = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        blueCol.AddThemeConstantOverride("separation", 10);
        _blueScoreLabel = new Label { Text = "0", HorizontalAlignment = HorizontalAlignment.Right };
        _blueScoreLabel.AddThemeFontSizeOverride("font_size", 34);
        _blueScoreLabel.AddThemeColorOverride("font_color", Blue);
        var blueBadge = CreateBadge("BLUE", Blue, Colors.White, 14);
        blueBadge.CustomMinimumSize = new(64, 30);
        blueCol.AddChild(_blueScoreLabel);
        blueCol.AddChild(blueBadge);
        scoreLayout.AddChild(blueCol);

        // 2. Robot Telemetry Panel (Left graph canvas)
        _telemetryPanel = new RobotTelemetryPanel
        {
            Game = Game,
            OffsetLeft = 24,
            OffsetTop = 104,
            OffsetRight = 328,
            OffsetBottom = 512
        };
        _hudLayer.AddChild(_telemetryPanel);

        // 3. Compact Robot Status Card (Bottom Left, 1.5x enlarged, 490px wide)
        _telemetryCard = new PanelContainer
        {
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 24,
            OffsetRight = 490,
            OffsetTop = -170,
            OffsetBottom = -20,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _telemetryCard.AddThemeStyleboxOverride("panel", CreateBox(DialogBg, BtnNormalBorder, 10, 1, 18, 12));
        _hudLayer.AddChild(_telemetryCard);

        var telemBox = new VBoxContainer();
        telemBox.AddThemeConstantOverride("separation", 8);
        _telemetryCard.AddChild(telemBox);

        var topTelemRow = new HBoxContainer();
        _telemetryHeader = new Label { Text = "R1  •  0.0, 0.0 cm", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _telemetryHeader.AddThemeFontSizeOverride("font_size", 17);
        _telemetryHeader.AddThemeColorOverride("font_color", TextWhite);
        topTelemRow.AddChild(_telemetryHeader);

        _inventorySlots = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _inventorySlots.AddThemeConstantOverride("separation", 8);
        for (int i = 0; i < 4; i++)
        {
            var dot = new ColorRect { CustomMinimumSize = new(16, 16), Color = new("292e39") };
            _inventorySlots.AddChild(dot);
        }
        topTelemRow.AddChild(_inventorySlots);
        telemBox.AddChild(topTelemRow);

        _telemetrySubsystems = new Label { Text = "INTAKE: OFF   |   TARGET: HIVE   |   SHOT: READY" };
        _telemetrySubsystems.AddThemeFontSizeOverride("font_size", 15);
        _telemetrySubsystems.AddThemeColorOverride("font_color", TextMuted);
        telemBox.AddChild(_telemetrySubsystems);

        _telemetryHints = new Label { Text = "WASD Swerve  •  Q/E Turn  •  SHIFT Intake  •  SPACE Shoot" };
        _telemetryHints.AddThemeFontSizeOverride("font_size", 14);
        _telemetryHints.AddThemeColorOverride("font_color", TextDark);
        telemBox.AddChild(_telemetryHints);

        // 4. Quick Action Bar (Bottom Right, 1.5x enlarged)
        _controlsBar = new HBoxContainer
        {
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -560,
            OffsetRight = -24,
            OffsetTop = -68,
            OffsetBottom = -20
        };
        _controlsBar.AddThemeConstantOverride("separation", 12);
        _hudLayer.AddChild(_controlsBar);

        _cameraQuickBtn = CreateButton(_controlsBar, "CAM [C]", () => Game.Camera.Mode = (Game.Camera.Mode + 1) % 3);
        _targetQuickBtn = CreateButton(_controlsBar, "TARGET [T]", () => Game.AimFlower = !Game.AimFlower);
        CreateButton(_controlsBar, "RESET [R]", () => Game.Reset());
        CreateButton(_controlsBar, "MENU [ESC]", () => { if (Game.Started) Game.TogglePause(); else ShowMenu(); });

        // 5. Status / Toast Notification (Top Left)
        _statusToast = new PanelContainer
        {
            OffsetLeft = 24,
            OffsetTop = 16,
            OffsetRight = 400,
            OffsetBottom = 64,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _statusToast.AddThemeStyleboxOverride("panel", CreateBox(DialogBg, BtnNormalBorder, 8, 1, 16, 8));
        _statusToastLabel = new Label { Text = "Ready", HorizontalAlignment = HorizontalAlignment.Left };
        _statusToastLabel.AddThemeFontSizeOverride("font_size", 15);
        _statusToastLabel.AddThemeColorOverride("font_color", TextMuted);
        _statusToast.AddChild(_statusToastLabel);
        _hudLayer.AddChild(_statusToast);
    }

    private static Label CreateBadge(string text, Color bg, Color textCol, int fontSize = 14)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", textCol);
        label.AddThemeStyleboxOverride("normal", CreateBox(bg, Colors.Transparent, 6, 0, 10, 4));
        return label;
    }

    // =========================================================================
    // MODAL DIALOG SETUP (1.5x Enlarged: 920px width x 680px height)
    // =========================================================================
    private void BuildModalDialog()
    {
        _modalScrim = new ColorRect
        {
            Color = ScrimColor,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _modalScrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_modalScrim);

        _dialogPanel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -460,
            OffsetRight = 460,
            OffsetTop = -340,
            OffsetBottom = 340
        };
        var dialogStyle = CreateBox(DialogBg, BtnNormalBorder, 10, 1, 36, 26);
        dialogStyle.BorderWidthTop = 3;
        dialogStyle.BorderColor = GoldBorder;
        _dialogPanel.AddThemeStyleboxOverride("panel", dialogStyle);
        _root.AddChild(_dialogPanel);

        var dialogLayout = new VBoxContainer();
        dialogLayout.AddThemeConstantOverride("separation", 16);
        _dialogPanel.AddChild(dialogLayout);

        // Header Section (Single prominent Gold Title, clean subtitle, no duplicate badge)
        _dialogHeader = new VBoxContainer();
        _dialogHeader.AddThemeConstantOverride("separation", 6);
        dialogLayout.AddChild(_dialogHeader);

        _dialogTitle = new Label
        {
            Text = "BIOBUZZ SIMULATOR",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _dialogTitle.AddThemeFontSizeOverride("font_size", 34);
        _dialogTitle.AddThemeColorOverride("font_color", Gold);
        _dialogHeader.AddChild(_dialogTitle);

        _dialogSubtitle = new Label
        {
            Text = "GODOT + C#  /  LOCAL DEVELOPMENT BUILD",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _dialogSubtitle.AddThemeFontSizeOverride("font_size", 16);
        _dialogSubtitle.AddThemeColorOverride("font_color", TextMuted);
        _dialogHeader.AddChild(_dialogSubtitle);

        var div = new HSeparator();
        div.AddThemeStyleboxOverride("separator", CreateBox(BtnNormalBorder, Colors.Transparent, 1, 0, 0, 1));
        _dialogHeader.AddChild(div);

        // Scrollable Body
        _dialogScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        dialogLayout.AddChild(_dialogScroll);

        _dialogBody = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _dialogBody.AddThemeConstantOverride("separation", 14);
        _dialogScroll.AddChild(_dialogBody);
    }

    // =========================================================================
    // NAVIGATION SYSTEM
    // =========================================================================
    public void ShowMenu()
    {
        NavigateTo(ScreenType.Main, true);
    }

    public void ShowPause(bool paused)
    {
        if (paused)
        {
            NavigateTo(ScreenType.Pause, true);
        }
        else
        {
            CloseMenu();
        }
    }

    public void ShowResults()
    {
        NavigateTo(ScreenType.Results, true);
    }

    public void NavigateTo(ScreenType screen, bool clearStack = false)
    {
        if (clearStack) _navStack.Clear();
        _navStack.Push(screen);
        _currentScreen = screen;
        RenderCurrentScreen();
    }

    public void GoBack()
    {
        _draft = null;
        if (_navStack.Count > 1)
        {
            _navStack.Pop();
            _currentScreen = _navStack.Peek();
            RenderCurrentScreen();
        }
        else
        {
            if (Game.Started)
            {
                if (Game.Paused) Game.TogglePause();
                else CloseMenu();
            }
            else
            {
                NavigateTo(ScreenType.Main, true);
            }
        }
    }

    public void CloseMenu()
    {
        _draft = null;
        _currentScreen = ScreenType.None;
        _navStack.Clear();
        _modalScrim.Visible = false;
        _dialogPanel.Visible = false;
    }

    private void ClearDialog(string title, string subtitle = "")
    {
        foreach (var child in _dialogBody.GetChildren())
        {
            _dialogBody.RemoveChild(child);
            child.QueueFree();
        }
        _dialogTitle.Text = title.ToUpperInvariant();
        _dialogSubtitle.Text = subtitle;
        _dialogSubtitle.Visible = !string.IsNullOrEmpty(subtitle);
        _modalScrim.Visible = true;
        _dialogPanel.Visible = true;
        _dialogScroll.ScrollVertical = 0;
    }

    private void RenderCurrentScreen()
    {
        switch (_currentScreen)
        {
            case ScreenType.Main:
                BuildMainMenu();
                break;
            case ScreenType.Pause:
                BuildPauseMenu();
                break;
            case ScreenType.RobotCreator:
                BuildRobotCreator();
                break;
            case ScreenType.AutonomousPathing:
                BuildAutonomousPathing();
                break;
            case ScreenType.Settings:
                BuildSettings();
                break;
            case ScreenType.Help:
                BuildHelp();
                break;
            case ScreenType.Results:
                BuildResults();
                break;
            default:
                CloseMenu();
                break;
        }

        CallDeferred(MethodName.FocusFirstInteractiveControl);
    }

    private void FocusFirstInteractiveControl()
    {
        foreach (var node in _dialogBody.GetChildren())
        {
            if (node is Button btn && btn.IsVisibleInTree() && !btn.Disabled)
            {
                btn.GrabFocus();
                return;
            }
            if (node is Container container)
            {
                foreach (var sub in container.GetChildren())
                {
                    if (sub is Button subBtn && subBtn.IsVisibleInTree() && !subBtn.Disabled)
                    {
                        subBtn.GrabFocus();
                        return;
                    }
                }
            }
        }
    }

    // =========================================================================
    // SCREEN: MAIN MENU
    // =========================================================================
    private void BuildMainMenu()
    {
        ClearDialog("BIOBUZZ SIMULATOR", "GODOT + C#  /  LOCAL DEVELOPMENT BUILD");

        // Primary Game Modes Card
        var modesCard = CreateCard(_dialogBody, "MATCH & TRAINING MODES");

        CreateButton(modesCard, "AI TRAINING GROUND [TRAIN]", () =>
        {
            Game.TrainingGround = true;
            Game.AutonomousDrill = false;
            Game.Practice = true;
            Game.Reset();
            CloseMenu();
        }, true);

        CreateButton(modesCard, "30S AUTONOMOUS DRILL [AUTO]", () =>
        {
            Game.TrainingGround = false;
            Game.AutonomousDrill = true;
            StartMatch(false);
        });

        CreateButton(modesCard, "PLAY FULL MATCH (158S)", () =>
        {
            Game.TrainingGround = false;
            Game.AutonomousDrill = false;
            StartMatch(false);
        });

        CreateButton(modesCard, "FREE PRACTICE ARENA", () =>
        {
            Game.TrainingGround = false;
            Game.AutonomousDrill = false;
            StartMatch(true);
        });

        // Navigation & Tools Card (Clean 2-Column Grid)
        var toolsCard = CreateCard(_dialogBody, "CONFIGURATION & TOOLS");
        var toolsGrid = new GridContainer { Columns = 2 };
        toolsGrid.AddThemeConstantOverride("h_separation", 14);
        toolsGrid.AddThemeConstantOverride("v_separation", 12);
        toolsCard.AddChild(toolsGrid);

        var rBtn = CreateButton(toolsGrid, "ROBOT CREATOR", () => NavigateTo(ScreenType.RobotCreator));
        rBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var pBtn = CreateButton(toolsGrid, "AUTONOMOUS PATH EDITOR", () => NavigateTo(ScreenType.AutonomousPathing));
        pBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var sBtn = CreateButton(toolsGrid, "SETTINGS", () => NavigateTo(ScreenType.Settings));
        sBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var hBtn = CreateButton(toolsGrid, "HOW TO PLAY", () => NavigateTo(ScreenType.Help));
        hBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // Footer Row
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        footer.AddThemeConstantOverride("separation", 14);
        _dialogBody.AddChild(footer);

        var quitBtn = CreateButton(footer, "QUIT", () => GetTree().Quit());
        quitBtn.CustomMinimumSize = new(180, 52);
    }

    private void StartMatch(bool practice)
    {
        Game.Practice = practice;
        Game.Reset();
        CloseMenu();
    }

    // =========================================================================
    // SCREEN: PAUSE MENU
    // =========================================================================
    private void BuildPauseMenu()
    {
        ClearDialog("PAUSED", "");

        // Resume & Restart Row
        var quickRow = new HBoxContainer();
        quickRow.AddThemeConstantOverride("separation", 14);
        _dialogBody.AddChild(quickRow);

        var resumeBtn = CreateButton(quickRow, "RESUME [ESC]", () => Game.TogglePause(), true);
        resumeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var restartBtn = CreateButton(quickRow, "RESTART [R]", () => Game.Reset());
        restartBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // Autonomous & Waypoints Card
        var pathCard = CreateCard(_dialogBody, "AUTONOMOUS & WAYPOINT PATHING");

        int waypointsCount = Game.PathEditor != null ? Game.PathEditor.Points.Count : 0;
        bool isFollowing = Game.PathEditor != null && Game.PathEditor.Following;
        bool isEditing = Game.PathEditor != null && Game.PathEditor.Editing;

        var pathInfo = new Label
        {
            Text = $"Waypoints: {waypointsCount}   •   State: {(isFollowing ? "FOLLOWING TRAJECTORY" : isEditing ? "EDITING MODE (Click floor to place)" : "STANDBY")}"
        };
        pathInfo.AddThemeFontSizeOverride("font_size", 16);
        pathInfo.AddThemeColorOverride("font_color", isFollowing ? Green : isEditing ? Gold : TextMuted);
        pathCard.AddChild(pathInfo);

        var pathRow1 = new HBoxContainer();
        pathRow1.AddThemeConstantOverride("separation", 12);
        pathCard.AddChild(pathRow1);

        CreateButton(pathRow1, isEditing ? "DISABLE PATH EDITOR [F2]" : "ENABLE PATH EDITOR [F2]", () =>
        {
            if (Game.PathEditor != null)
            {
                Game.PathEditor.Editing = !Game.PathEditor.Editing;
                Game.PathEditor.Following = false;
                BuildPauseMenu();
            }
        }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        CreateButton(pathRow1, isFollowing ? "STOP FOLLOWING [P]" : "FOLLOW PATH [P]", () =>
        {
            if (Game.PathEditor != null && Game.PathEditor.Points.Count > 0)
            {
                Game.PathEditor.Following = !Game.PathEditor.Following;
                Game.PathEditor.Current = 0;
                BuildPauseMenu();
            }
        }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var pathRow2 = new HBoxContainer();
        pathRow2.AddThemeConstantOverride("separation", 12);
        pathCard.AddChild(pathRow2);

        CreateButton(pathRow2, "SAVE PATH", () => Game.PathEditor?.Save()).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(pathRow2, "LOAD PATH", () => { Game.PathEditor?.Load(); BuildPauseMenu(); }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(pathRow2, "CLEAR PATH", () => { Game.PathEditor?.ClearPath(); BuildPauseMenu(); }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // Practice State (Snapshots) Card
        var snapCard = CreateCard(_dialogBody, "PRACTICE SNAPSHOTS");
        var snapRow = new HBoxContainer();
        snapRow.AddThemeConstantOverride("separation", 12);
        snapCard.AddChild(snapRow);

        CreateButton(snapRow, "SAVE PRACTICE POSITIONS [F5]", () => Game.Save()).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(snapRow, "LOAD PRACTICE POSITIONS [F9]", () =>
        {
            if (Game.Load()) CloseMenu();
        }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // Submenus Row
        var subRow = new HBoxContainer();
        subRow.AddThemeConstantOverride("separation", 12);
        _dialogBody.AddChild(subRow);

        CreateButton(subRow, "ROBOT SETUP", () => NavigateTo(ScreenType.RobotCreator)).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(subRow, "AI TRAINING GROUND", () =>
        {
            Game.TrainingGround = true;
            Game.AutonomousDrill = false;
            Game.Practice = true;
            Game.Reset();
            CloseMenu();
        }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(subRow, "SETTINGS", () => NavigateTo(ScreenType.Settings)).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(subRow, "HOW TO PLAY", () => NavigateTo(ScreenType.Help)).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // Main Menu Button
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _dialogBody.AddChild(footer);

        var menuBtn = CreateButton(footer, "MAIN MENU", () =>
        {
            Game.Started = false;
            NavigateTo(ScreenType.Main, true);
        });
        menuBtn.CustomMinimumSize = new(200, 52);
    }

    // =========================================================================
    // SCREEN: ROBOT CONFIGURATOR
    // =========================================================================
    private void BuildRobotCreator()
    {
        ClearDialog("ROBOT CREATOR", "Local profile • Apply restarts the scene");

        _draft ??= Game.Profile.Copy();
        var profile = _draft;

        // FTC Sizing Compliance Card
        var sizingCard = CreateCard(_dialogBody, "FTC 18\" SIZING COMPLIANCE");
        var sizingRow = new HBoxContainer();
        sizingRow.AddThemeConstantOverride("separation", 16);
        sizingCard.AddChild(sizingRow);

        bool isLegal = profile.WidthCm <= 45.721f && profile.LengthCm <= 45.721f;
        var legalBadge = CreateBadge(isLegal ? "SIZE OK" : "OVERSIZED !", isLegal ? Green : Red, Colors.White, 15);
        legalBadge.CustomMinimumSize = new(130, 32);
        sizingRow.AddChild(legalBadge);

        var sizingLabel = new Label
        {
            Text = $"{profile.WidthCm:0.0} x {profile.LengthCm:0.0} cm  ({profile.WidthCm / 2.54f:0.0}\" x {profile.LengthCm / 2.54f:0.0}\")",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        sizingLabel.AddThemeFontSizeOverride("font_size", 18);
        sizingLabel.AddThemeColorOverride("font_color", TextWhite);
        sizingRow.AddChild(sizingLabel);

        // Architecture Presets Card
        var presetsCard = CreateCard(_dialogBody, "PRESETS");
        var presetsRow = new HBoxContainer();
        presetsRow.AddThemeConstantOverride("separation", 10);
        presetsCard.AddChild(presetsRow);

        void ApplyPreset(string name, float w, float l, float spd, float acc, float trn, int tur, int intk)
        {
            profile.Name = name;
            profile.WidthCm = w;
            profile.LengthCm = l;
            profile.Speed = spd;
            profile.Acceleration = acc;
            profile.TurnSpeed = trn;
            profile.Turrets = tur;
            profile.Intakes = intk;
            BuildRobotCreator();
        }

        CreateButton(presetsRow, "FTC Max 18\"", () => ApplyPreset("Max Swerve", 45.72f, 45.72f, 1.8f, 3.5f, 3.2f, 1, 1)).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(presetsRow, "Agile Compact", () => ApplyPreset("Agile Runner", 35.0f, 35.0f, 2.5f, 5.0f, 4.5f, 1, 1)).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(presetsRow, "Sniper Turret", () => ApplyPreset("Sniper Turret", 45.72f, 45.72f, 1.4f, 2.8f, 2.5f, 2, 1)).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(presetsRow, "Dual Harvester", () => ApplyPreset("Dual Intake", 42.0f, 42.0f, 1.6f, 3.2f, 3.0f, 1, 2)).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // Sliders Card
        var slidersCard = CreateCard(_dialogBody, "DRIVETRAIN & DIMENSIONS");

        void UpdateSizing()
        {
            bool ok = profile.WidthCm <= 45.721f && profile.LengthCm <= 45.721f;
            legalBadge.Text = ok ? "SIZE OK" : "OVERSIZED !";
            legalBadge.AddThemeStyleboxOverride("normal", CreateBox(ok ? Green : Red, Colors.Transparent, 6, 0, 10, 4));
            sizingLabel.Text = $"{profile.WidthCm:0.0} x {profile.LengthCm:0.0} cm  ({profile.WidthCm / 2.54f:0.0}\" x {profile.LengthCm / 2.54f:0.0}\")";
        }

        CreateSlider(slidersCard, "Width (cm)", 25.0, 45.72, 0.25, profile.WidthCm, v =>
        {
            profile.WidthCm = (float)v;
            UpdateSizing();
        }, v => $"{v:0.00} cm ({v / 2.54:0.0}\")");

        CreateSlider(slidersCard, "Length (cm)", 25.0, 45.72, 0.25, profile.LengthCm, v =>
        {
            profile.LengthCm = (float)v;
            UpdateSizing();
        }, v => $"{v:0.00} cm ({v / 2.54:0.0}\")");

        CreateSlider(slidersCard, "Speed (m/s)", 0.3, 3.0, 0.05, profile.Speed, v =>
        {
            profile.Speed = (float)v;
        }, v => $"{v:0.00} m/s ({v / 0.0254:0} in/s)");

        CreateSlider(slidersCard, "Acceleration (m/s²)", 0.5, 8.0, 0.1, profile.Acceleration, v =>
        {
            profile.Acceleration = (float)v;
        }, v => $"{v:0.00} m/s²");

        CreateSlider(slidersCard, "Turn speed (rad/s)", 0.5, 6.0, 0.1, profile.TurnSpeed, v =>
        {
            profile.TurnSpeed = (float)v;
        }, v => $"{v:0.00} rad/s ({Mathf.RadToDeg((float)v):0}°/s)");

        // Subsystems (Turrets & Intakes)
        var subCard = CreateCard(_dialogBody, "SUBSYSTEMS");

        // Turret selection
        var turretRow = new HBoxContainer();
        turretRow.AddThemeConstantOverride("separation", 12);
        var tLbl = new Label { Text = "Turrets:", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tLbl.AddThemeFontSizeOverride("font_size", 18);
        turretRow.AddChild(tLbl);
        for (int t = 1; t <= 3; t++)
        {
            int turrets = t;
            var tBtn = CreateButton(turretRow, $"{turrets} Turret{(turrets > 1 ? "s" : "")}", () =>
            {
                profile.Turrets = turrets;
                BuildRobotCreator();
            }, profile.Turrets == turrets);
            tBtn.CustomMinimumSize = new(130, 44);
        }
        subCard.AddChild(turretRow);

        // Intake selection
        var intakeRow = new HBoxContainer();
        intakeRow.AddThemeConstantOverride("separation", 12);
        var iLbl = new Label { Text = "Intakes:", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        iLbl.AddThemeFontSizeOverride("font_size", 18);
        intakeRow.AddChild(iLbl);
        for (int i = 1; i <= 2; i++)
        {
            int intakes = i;
            var iBtn = CreateButton(intakeRow, intakes == 1 ? "1 Front Intake" : "2 Front + Rear", () =>
            {
                profile.Intakes = intakes;
                BuildRobotCreator();
            }, profile.Intakes == intakes);
            iBtn.CustomMinimumSize = new(160, 44);
        }
        subCard.AddChild(intakeRow);

        // Actions
        var actionsRow = new HBoxContainer();
        actionsRow.AddThemeConstantOverride("separation", 14);
        _dialogBody.AddChild(actionsRow);

        var saveBtn = CreateButton(actionsRow, "SAVE + APPLY", () =>
        {
            try
            {
                _draft.Save();
                Game.Profile = _draft;
                Game.Reset(Game.Started);
                CloseMenu();
            }
            catch (Exception ex)
            {
                Game.Status = "Cannot apply robot: " + ex.Message;
            }
        }, true);
        saveBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var backBtn = CreateButton(actionsRow, "BACK", () =>
        {
            _draft = null;
            GoBack();
        });
        backBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    // =========================================================================
    // SCREEN: AUTONOMOUS PATHING
    // =========================================================================
    private void BuildAutonomousPathing()
    {
        ClearDialog("AUTONOMOUS & PATH EDITOR", "Record waypoints and test autonomous trajectory following");

        int waypointsCount = Game.PathEditor != null ? Game.PathEditor.Points.Count : 0;
        bool isFollowing = Game.PathEditor != null && Game.PathEditor.Following;
        bool isEditing = Game.PathEditor != null && Game.PathEditor.Editing;

        // Path Status Card
        var statusCard = CreateCard(_dialogBody, "PATH STATUS");
        var statsRow = new HBoxContainer();
        statsRow.AddThemeConstantOverride("separation", 16);
        statusCard.AddChild(statsRow);

        var badge = CreateBadge(isFollowing ? "FOLLOWING PATH" : isEditing ? "EDITOR ACTIVE" : "IDLE",
            isFollowing ? Green : isEditing ? Gold : TextDark, Colors.White, 15);
        badge.CustomMinimumSize = new(160, 32);
        statsRow.AddChild(badge);

        var countLabel = new Label
        {
            Text = $"Total Waypoints: {waypointsCount}   •   Active Target: {(isFollowing ? $"{Game.PathEditor.Current + 1} / {waypointsCount}" : "None")}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        countLabel.AddThemeFontSizeOverride("font_size", 18);
        countLabel.AddThemeColorOverride("font_color", TextWhite);
        statsRow.AddChild(countLabel);

        // Actions Grid (Spacious 2 columns)
        var actionsCard = CreateCard(_dialogBody, "WAYPOINT ACTIONS");
        var actionsGrid = new GridContainer { Columns = 2 };
        actionsGrid.AddThemeConstantOverride("h_separation", 14);
        actionsGrid.AddThemeConstantOverride("v_separation", 12);
        actionsCard.AddChild(actionsGrid);

        CreateButton(actionsGrid, "RUN 30S AUTO DRILL", () =>
        {
            Game.AutonomousDrill = true;
            StartMatch(false);
        }, true).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        CreateButton(actionsGrid, isFollowing ? "STOP FOLLOWING [P]" : "FOLLOW PATH [P]", () =>
        {
            if (Game.PathEditor != null && Game.PathEditor.Points.Count > 0)
            {
                Game.PathEditor.Following = !Game.PathEditor.Following;
                Game.PathEditor.Current = 0;
                BuildAutonomousPathing();
            }
        }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        CreateButton(actionsGrid, isEditing ? "DISABLE EDITOR [F2]" : "ENABLE EDITOR [F2]", () =>
        {
            if (Game.PathEditor != null)
            {
                Game.PathEditor.Editing = !Game.PathEditor.Editing;
                Game.PathEditor.Following = false;
                BuildAutonomousPathing();
            }
        }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        CreateButton(actionsGrid, "CLEAR ALL WAYPOINTS", () =>
        {
            Game.PathEditor?.ClearPath();
            BuildAutonomousPathing();
        }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        CreateButton(actionsGrid, "SAVE PATH [path.json]", () => Game.PathEditor?.Save()).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(actionsGrid, "LOAD PATH", () =>
        {
            Game.PathEditor?.Load();
            BuildAutonomousPathing();
        }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // Info Card
        var infoCard = CreateCard(_dialogBody, "HOW TO USE PATH EDITOR");
        var infoLabel = new Label
        {
            Text = "• Press F2 to activate Path Editor. Left-click anywhere on the floor to place waypoints.\n• Press Backspace to remove the last waypoint. Press P to start following the path.\n• During 30s Autonomous Drill, following a recorded path traverses your custom routine.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        infoLabel.AddThemeFontSizeOverride("font_size", 16);
        infoLabel.AddThemeColorOverride("font_color", TextMuted);
        infoCard.AddChild(infoLabel);

        // Back
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _dialogBody.AddChild(footer);
        var backBtn = CreateButton(footer, "BACK [ESC]", GoBack);
        backBtn.CustomMinimumSize = new(180, 52);
    }

    // =========================================================================
    // SCREEN: SETTINGS
    // =========================================================================
    private void BuildSettings()
    {
        ClearDialog("SETTINGS", "");

        // Camera Settings
        var camCard = CreateCard(_dialogBody, "CAMERA VIEW & PERSPECTIVES");

        // Mode row
        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 12);
        var modeLbl = new Label { Text = "Camera Mode:", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        modeLbl.AddThemeFontSizeOverride("font_size", 18);
        modeRow.AddChild(modeLbl);

        string[] modeNames = { "Orbit 3D", "Overhead 2D", "Follower" };
        for (int m = 0; m < 3; m++)
        {
            int modeIndex = m;
            var mBtn = CreateButton(modeRow, modeNames[modeIndex], () =>
            {
                Game.Camera.Mode = modeIndex;
                BuildSettings();
            }, Game.Camera.Mode == modeIndex);
            mBtn.CustomMinimumSize = new(140, 44);
        }
        camCard.AddChild(modeRow);

        CreateSlider(camCard, "Camera distance", 2.5, 12.0, 0.1, Game.Camera.Distance, v =>
        {
            Game.Camera.Distance = (float)v;
        }, v => $"{v:0.00} m");

        CreateSlider(camCard, "Field of view", 30.0, 75.0, 1.0, Game.Camera.Fov, v =>
        {
            Game.Camera.Fov = (float)v;
        }, v => $"{v:0.0}°");

        var resetCamBtn = CreateButton(camCard, "RESET CAMERA VIEW", () =>
        {
            Game.Camera.ResetView();
            BuildSettings();
        });
        resetCamBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;

        // Two Players Setting
        var playCard = CreateCard(_dialogBody, "MULTIPLAYER SETTINGS");
        var playRow = new HBoxContainer();
        playRow.AddThemeConstantOverride("separation", 14);
        playCard.AddChild(playRow);

        var pLbl = new Label { Text = "Local Two Players:", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        pLbl.AddThemeFontSizeOverride("font_size", 18);
        playRow.AddChild(pLbl);

        CreateButton(playRow, Game.TwoPlayers ? "ON" : "OFF", () =>
        {
            Game.TwoPlayers = !Game.TwoPlayers;
            BuildSettings();
        }, Game.TwoPlayers).CustomMinimumSize = new(140, 44);

        // Back
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _dialogBody.AddChild(footer);
        var backBtn = CreateButton(footer, "BACK [ESC]", GoBack);
        backBtn.CustomMinimumSize = new(180, 52);
    }

    // =========================================================================
    // SCREEN: HOW TO PLAY (Clean categorized table, zero text overlap)
    // =========================================================================
    private void BuildHelp()
    {
        ClearDialog("HOW TO PLAY", "");

        void AddCategory(string title, (string Key, string Desc)[] bindings)
        {
            var card = CreateCard(_dialogBody, title);
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 18);
            grid.AddThemeConstantOverride("v_separation", 8);
            card.AddChild(grid);

            foreach (var b in bindings)
            {
                var kBadge = CreateBadge(b.Key, CardInner, Gold, 15);
                kBadge.CustomMinimumSize = new(180, 28);
                kBadge.HorizontalAlignment = HorizontalAlignment.Left;
                grid.AddChild(kBadge);

                var dLbl = new Label { Text = b.Desc, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                dLbl.AddThemeFontSizeOverride("font_size", 16);
                dLbl.AddThemeColorOverride("font_color", TextWhite);
                grid.AddChild(dLbl);
            }
        }

        AddCategory("ROBOT DRIVING", new[]
        {
            ("WASD", "Camera-relative ground movement"),
            ("Q / E", "Rotate robot left / right"),
            ("Gamepad Left Stick", "Analog translation relative to camera"),
            ("Gamepad Right Stick X", "Analog rotation")
        });

        AddCategory("SUBSYSTEMS & GAME PIECES", new[]
        {
            ("SHIFT / J", "Hold to run intake (collects up to 4 pieces)"),
            ("SPACE", "Launch game piece toward target"),
            ("T", "Target HIVE / FLOWER toggle"),
            ("K", "Extract bottom ball near FLOWER column"),
            ("H", "Human player releases NECTAR piece"),
            ("B / N", "Quick-spawn POLLEN / NECTAR in practice")
        });

        AddCategory("PATHING & PRACTICE TOOLS", new[]
        {
            ("F2", "Toggle path waypoint editor"),
            ("Left Click", "Place waypoint on arena floor (when editing)"),
            ("Backspace", "Remove last waypoint"),
            ("P", "Start / pause following recorded path"),
            ("F5 / F9", "Save / load practice scene positions")
        });

        AddCategory("CAMERA & SYSTEM", new[]
        {
            ("C", "Orbit / top / robot follower camera"),
            ("Middle/Right Drag", "Orbit camera view"),
            ("Shift + Middle Drag", "Pan field view"),
            ("Mouse Wheel", "Zoom camera in / out"),
            ("R", "Reset match / simulation"),
            ("ESC", "Pause match / Open menu")
        });

        // Back
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _dialogBody.AddChild(footer);
        var backBtn = CreateButton(footer, "BACK [ESC]", GoBack);
        backBtn.CustomMinimumSize = new(180, 52);
    }

    // =========================================================================
    // SCREEN: MATCH RESULTS
    // =========================================================================
    private void BuildResults()
    {
        ClearDialog("MATCH COMPLETE", $"RED {Game.RedScore}   /   BLUE {Game.BlueScore}");

        // Scoreboard Card
        var scoreCard = CreateCard(_dialogBody, "MATCH SCORES");
        var scoreRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        scoreRow.AddThemeConstantOverride("separation", 48);
        scoreCard.AddChild(scoreRow);

        // Red Box
        var redBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var rBadge = CreateBadge("RED ALLIANCE", Red, Colors.White, 16);
        rBadge.CustomMinimumSize = new(140, 32);
        redBox.AddChild(rBadge);
        var rScore = new Label { Text = Game.RedScore.ToString(), HorizontalAlignment = HorizontalAlignment.Center };
        rScore.AddThemeFontSizeOverride("font_size", 44);
        rScore.AddThemeColorOverride("font_color", Red);
        redBox.AddChild(rScore);
        scoreRow.AddChild(redBox);

        // Blue Box
        var blueBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var bBadge = CreateBadge("BLUE ALLIANCE", Blue, Colors.White, 16);
        bBadge.CustomMinimumSize = new(140, 32);
        blueBox.AddChild(bBadge);
        var bScore = new Label { Text = Game.BlueScore.ToString(), HorizontalAlignment = HorizontalAlignment.Center };
        bScore.AddThemeFontSizeOverride("font_size", 44);
        bScore.AddThemeColorOverride("font_color", Blue);
        blueBox.AddChild(bScore);
        scoreRow.AddChild(blueBox);

        // Statistics Card
        int fired = Game.Player != null ? Game.Player.ShotsFired : 0;
        int made = Game.Player != null ? Game.Player.ShotsMade : 0;
        float acc = fired > 0 ? (float)made * 100f / fired : 0f;

        var statsCard = CreateCard(_dialogBody, "PERFORMANCE STATISTICS");
        var statsGrid = new GridContainer { Columns = 2 };
        statsGrid.AddThemeConstantOverride("h_separation", 24);
        statsGrid.AddThemeConstantOverride("v_separation", 8);
        statsCard.AddChild(statsGrid);

        void AddStat(string label, string val)
        {
            var l = new Label { Text = label };
            l.AddThemeFontSizeOverride("font_size", 17);
            l.AddThemeColorOverride("font_color", TextMuted);
            statsGrid.AddChild(l);

            var v = new Label { Text = val, HorizontalAlignment = HorizontalAlignment.Right };
            v.AddThemeFontSizeOverride("font_size", 17);
            v.AddThemeColorOverride("font_color", TextWhite);
            statsGrid.AddChild(v);
        }

        AddStat("Shots Fired / Landed:", $"{fired} / {made}");
        AddStat("Shot Accuracy:", $"{acc:0.0}%");
        AddStat("Red Hive Tips / Blue Hive Tips:", $"{Game.RedHive.Tips} / {Game.BlueHive.Tips}");

        // Actions Row
        var actionsRow = new HBoxContainer();
        actionsRow.AddThemeConstantOverride("separation", 14);
        _dialogBody.AddChild(actionsRow);

        CreateButton(actionsRow, Game.AutonomousDrill ? "RUN AUTO DRILL AGAIN" : "PLAY AGAIN", () =>
        {
            StartMatch(false);
        }, true).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        CreateButton(actionsRow, "PRACTICE", () => StartMatch(true)).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        CreateButton(actionsRow, "MAIN MENU", () =>
        {
            Game.Started = false;
            NavigateTo(ScreenType.Main, true);
        }).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    // =========================================================================
    // UI COMPONENT HELPERS (CreateCard returns inner VBoxContainer so children never overlap title)
    // =========================================================================
    private VBoxContainer CreateCard(Node parent, string title = "")
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", CreateBox(CardBg, BtnNormalBorder, 8, 1, 24, 18));
        parent.AddChild(panel);

        var vbox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        vbox.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vbox);

        if (!string.IsNullOrEmpty(title))
        {
            var titleLbl = new Label
            {
                Text = title.ToUpperInvariant(),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            titleLbl.AddThemeFontSizeOverride("font_size", 18);
            titleLbl.AddThemeColorOverride("font_color", Gold);
            vbox.AddChild(titleLbl);
        }

        return vbox;
    }

    private Button CreateButton(Node parent, string caption, Action action, bool primary = false)
    {
        var button = new Button
        {
            Text = caption,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new(0, 52)
        };
        button.AddThemeFontSizeOverride("font_size", 20);

        if (primary)
        {
            var pNormal = CreateBox(new("242214"), Gold, 8, 2, 24, 14);
            var pHover = CreateBox(new("3a331c"), Gold, 8, 2, 24, 14);
            button.AddThemeStyleboxOverride("normal", pNormal);
            button.AddThemeStyleboxOverride("hover", pHover);
            button.AddThemeStyleboxOverride("pressed", pHover);
            button.AddThemeColorOverride("font_color", Gold);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
        }

        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private HSlider CreateSlider(VBoxContainer parent, string title, double min, double max, double step, double value, Action<double> onChanged, Func<double, string> formatter = null)
    {
        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 6);
        parent.AddChild(container);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        container.AddChild(header);

        var label = new Label { Text = title, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", TextMuted);
        header.AddChild(label);

        string format(double v) => formatter != null ? formatter(v) : v.ToString("0.00", CultureInfo.InvariantCulture);

        var valLabel = new Label
        {
            Text = format(value),
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new(180, 0)
        };
        valLabel.AddThemeFontSizeOverride("font_size", 18);
        valLabel.AddThemeColorOverride("font_color", Gold);
        header.AddChild(valLabel);

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            CustomMinimumSize = new(0, 28),
            FocusMode = Control.FocusModeEnum.All
        };
        slider.ValueChanged += v =>
        {
            valLabel.Text = format(v);
            onChanged(v);
        };
        container.AddChild(slider);

        return slider;
    }

    // =========================================================================
    // FRAME PROCESS
    // =========================================================================
    public override void _Process(double delta)
    {
        if (Game == null || Game.Player == null) return;

        bool isMenuOpen = _currentScreen != ScreenType.None;
        _telemetryPanel.Visible = Game.Started && !isMenuOpen;
        _telemetryCard.Visible = Game.Started && !isMenuOpen;
        _controlsBar.Visible = !isMenuOpen;
        _statusToast.Visible = !isMenuOpen && !string.IsNullOrEmpty(Game.Status);

        _telemetryCard.TooltipText = $"Current readiness refreshes at 20 Hz. Last attempt: {Game.Player.LastShotAttempt}\n{Game.Player.LastShotDiagnosis.Reason}: {Game.Player.LastShotDiagnosis.Obstacle}\nContact estimate (Godot metres): {Game.Player.LastShotDiagnosis.Point}";
        _statusToast.TooltipText = Game.Status;

        _redScoreLabel.Text = Game.RedScore.ToString();
        _blueScoreLabel.Text = Game.BlueScore.ToString();

        string phaseName;
        float timeLeft;
        Color phaseColor;

        if (Game.TrainingGround)
        {
            phaseName = "AI TRAINING GROUND";
            timeLeft = Game.Elapsed;
            phaseColor = Gold;
        }
        else if (Game.Practice)
        {
            phaseName = "PRACTICE ARENA";
            timeLeft = Game.Elapsed;
            phaseColor = Purple;
        }
        else if (Game.AutonomousDrill)
        {
            phaseName = "30S AUTONOMOUS DRILL";
            timeLeft = Mathf.Max(0, 30 - Game.Elapsed);
            phaseColor = Gold;
        }
        else if (Game.Elapsed < 30)
        {
            phaseName = "AUTONOMOUS PERIOD";
            timeLeft = 30 - Game.Elapsed;
            phaseColor = Gold;
        }
        else if (Game.Elapsed < 38)
        {
            phaseName = "TRANSITION";
            timeLeft = 38 - Game.Elapsed;
            phaseColor = Orange;
        }
        else if (Game.Elapsed < 158)
        {
            phaseName = "TELEOP PERIOD";
            timeLeft = 158 - Game.Elapsed;
            phaseColor = Green;
        }
        else
        {
            phaseName = "MATCH COMPLETE";
            timeLeft = 0;
            phaseColor = Red;
        }

        _phaseBadge.Text = phaseName;
        _phaseBadge.AddThemeColorOverride("font_color", phaseColor);
        _clockLabel.Text = $"{(int)timeLeft / 60}:{(int)timeLeft % 60:00}";

        var pos = Game.Player.Position;
        _telemetryHeader.Text = $"ROBOT 1 ({(Game.Player.Red ? "RED" : "BLUE")})  •  X: {pos.X * 100:0.0} cm, Y: {-pos.Z * 100:0.0} cm";

        var inv = Game.Player.Inventory;
        for (int i = 0; i < 4; i++)
        {
            var dot = _inventorySlots.GetChild<ColorRect>(i);
            if (i < inv.Count)
            {
                dot.Color = inv[i] == PieceKind.Pollen ? Gold : inv[i] == PieceKind.RedNectar ? Red : Blue;
            }
            else
            {
                dot.Color = new("292e39");
            }
        }

        string intakeState = Game.Player.Intake ? "ACTIVE" : "OFF";
        string targetState = Game.AimFlower ? "FLOWER" : "HIVE";
        _telemetrySubsystems.Text = $"INTAKE: {intakeState}   |   TARGET: {targetState}   |   SHOT: {Game.Player.ShotStatus}";

        string[] camModes = { "ORBIT", "OVERHEAD", "CHASE" };
        _cameraQuickBtn.Text = $"CAM: {camModes[Game.Camera.Mode % 3]} [C]";
        _targetQuickBtn.Text = $"TARGET: {(Game.AimFlower ? "FLOWER" : "HIVE")} [T]";

        if (_statusToast.Visible)
        {
            _statusToastLabel.Text = Game.Status;
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.PhysicalKeycode == Key.Escape && _currentScreen != ScreenType.None)
            {
                GoBack();
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
