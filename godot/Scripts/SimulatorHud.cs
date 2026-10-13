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
        RobotWorkshop,
        RobotImport,
        RobotRoster,
        PracticePositions,
        AutonomousPathing,
        AutonomousPathEditor,
        Settings,
        Controls,
        Help,
        Results
    }

    private readonly Stack<ScreenType> _navStack = new();
    private ScreenType _currentScreen = ScreenType.None;
    public ScreenType CurrentScreen => _currentScreen;

    // Root containers
    private Control _root;
    private RobotProfile _draft;
    private AutonomousPathDefinition _draftPath;
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
    private Control _countdownOverlay;
    private Label _countdownNumberLabel;
    private Label _countdownSubLabel;
    private float _startFlashTimer = 0f;
    private string _rebindingAction;
    private int _selectedPositionIndex = 0;
    private VBoxContainer _trainingCornerContainer;
    private HBoxContainer _trainingMultiplierRow;
    private HBoxContainer _trainingActionsRow;
    private Button _trainingStartBtn;
    private Button _trainingStopBtn;
    private readonly List<(float Multiplier, Button Btn)> _multiplierButtons = new();
    private float _lastKnownMultiplier = -1f;

    // Classic Theme Palette (Restored original aesthetic, refined sleek sizing)
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
        CreateJointControls();

        _modalScrim.Visible = false;
        _dialogPanel.Visible = false;
    }

    private void SetupTheme()
    {
        var theme = new Theme { DefaultFontSize = 16 };
        if (ResourceLoader.Exists("res://Assets/Fonts/Poppins-Regular.ttf"))
        {
            var font = GD.Load<FontFile>("res://Assets/Fonts/Poppins-Regular.ttf");
            theme.DefaultFont = font;
        }

        // Sleek refined button styling
        var btnNormal = CreateBox(BtnNormalBg, BtnNormalBorder, 8, 1, 18, 10);
        var btnHover = CreateBox(BtnHoverBg, Gold, 8, 1, 18, 10);
        var btnPressed = CreateBox(new("121824"), Gold, 8, 1, 18, 10);
        var btnFocus = CreateBox(BtnHoverBg, Gold, 8, 2, 18, 10);
        var btnDisabled = CreateBox(new(0.07f, 0.09f, 0.13f, 0.6f), new(0.13f, 0.17f, 0.23f, 0.5f), 8, 1, 18, 10);

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

        // Remove scrollbars from every part of the menu
        var emptyScroll = new StyleBoxEmpty();
        theme.SetStylebox("scroll", "VScrollBar", emptyScroll);
        theme.SetStylebox("scroll_focus", "VScrollBar", emptyScroll);
        theme.SetStylebox("grabber", "VScrollBar", emptyScroll);
        theme.SetStylebox("grabber_highlight", "VScrollBar", emptyScroll);
        theme.SetStylebox("grabber_pressed", "VScrollBar", emptyScroll);

        theme.SetStylebox("scroll", "HScrollBar", emptyScroll);
        theme.SetStylebox("scroll_focus", "HScrollBar", emptyScroll);
        theme.SetStylebox("grabber", "HScrollBar", emptyScroll);
        theme.SetStylebox("grabber_highlight", "HScrollBar", emptyScroll);
        theme.SetStylebox("grabber_pressed", "HScrollBar", emptyScroll);

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
        _scoreCard.AddThemeStyleboxOverride("panel", CreateBox(DialogBg, Gold, 10, 1, 24, 8));
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

        // Countdown Overlay (Flashing 1, 2, 3 before autonomous drill)
        _countdownOverlay = new Control
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -250,
            OffsetRight = 250,
            OffsetTop = -140,
            OffsetBottom = 140,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _hudLayer.AddChild(_countdownOverlay);

        var cdBox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        cdBox.AddThemeConstantOverride("separation", 6);
        _countdownOverlay.AddChild(cdBox);

        _countdownNumberLabel = new Label
        {
            Text = "1",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _countdownNumberLabel.AddThemeFontSizeOverride("font_size", 96);
        _countdownNumberLabel.AddThemeColorOverride("font_color", Gold);
        cdBox.AddChild(_countdownNumberLabel);

        _countdownSubLabel = new Label
        {
            Text = "AUTONOMOUS DRILL STARTING",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _countdownSubLabel.AddThemeFontSizeOverride("font_size", 16);
        _countdownSubLabel.AddThemeColorOverride("font_color", TextWhite);
        cdBox.AddChild(_countdownSubLabel);

        // Bottom-Right AI Training Ground Controls
        _trainingCornerContainer = new VBoxContainer
        {
            AnchorLeft = 1.0f,
            AnchorRight = 1.0f,
            AnchorTop = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = -380,
            OffsetRight = -25,
            OffsetTop = -118,
            OffsetBottom = -24,
            MouseFilter = Control.MouseFilterEnum.Pass,
            Visible = false
        };
        _trainingCornerContainer.AddThemeConstantOverride("separation", 8);
        _hudLayer.AddChild(_trainingCornerContainer);

        // Top row: Speed multiplier selector
        _trainingMultiplierRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        _trainingMultiplierRow.AddThemeConstantOverride("separation", 6);
        _trainingCornerContainer.AddChild(_trainingMultiplierRow);

        var speedLbl = new Label
        {
            Text = "SPEED:",
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new(54, 30)
        };
        speedLbl.AddThemeFontSizeOverride("font_size", 13);
        speedLbl.AddThemeColorOverride("font_color", Gold);
        _trainingMultiplierRow.AddChild(speedLbl);

        _multiplierButtons.Clear();
        float[] multipliers = { 1.0f, 2.0f, 3.0f, 5.0f };
        foreach (float m in multipliers)
        {
            float multVal = m;
            var mBtn = new Button
            {
                Text = $"{multVal:0}x",
                FocusMode = Control.FocusModeEnum.None,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new(0, 32)
            };
            mBtn.AddThemeFontSizeOverride("font_size", 13);
            mBtn.Pressed += () =>
            {
                if (Game != null)
                {
                    Game.SetTimeMultiplier(multVal);
                    Game.SaveSettings();
                    UpdateMultiplierButtonStyles();
                }
            };
            _trainingMultiplierRow.AddChild(mBtn);
            _multiplierButtons.Add((multVal, mBtn));
        }

        // Bottom row: Action buttons (START/RESTART & STOP)
        _trainingActionsRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        _trainingActionsRow.AddThemeConstantOverride("separation", 10);
        _trainingCornerContainer.AddChild(_trainingActionsRow);

        _trainingStartBtn = CreateButton(_trainingActionsRow, "START 30S DRILL", () =>
        {
            if (Game != null)
            {
                if (!Game.TrainingLoopActive) Game.StartTrainingDrill();
                else Game.ResetTrainingCycle();
            }
        }, true);
        _trainingStartBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _trainingStartBtn.CustomMinimumSize = new(0, 44);

        _trainingStopBtn = CreateButton(_trainingActionsRow, "STOP", () =>
        {
            if (Game != null) Game.StopTrainingDrill();
        }, true);
        _trainingStopBtn.CustomMinimumSize = new(100, 44);
        _trainingStopBtn.Visible = false;

        UpdateMultiplierButtonStyles();
    }

    public void TriggerStartFlash(string title = "START!", string sub = "30S LOOP RESTARTED")
    {
        _countdownNumberLabel.Text = title;
        _countdownNumberLabel.AddThemeColorOverride("font_color", Green);
        _countdownSubLabel.Text = sub;
        _startFlashTimer = 0.6f;
    }

    private void UpdateMultiplierButtonStyles()
    {
        if (Game == null) return;
        float current = Game.TrainingTimeMultiplier;
        foreach (var (m, btn) in _multiplierButtons)
        {
            if (btn == null || !IsInstanceValid(btn)) continue;
            bool active = Mathf.IsEqualApprox(m, current);
            if (active)
            {
                btn.AddThemeStyleboxOverride("normal", CreateBox(new("242214"), Gold, 6, 2, 8, 4));
                btn.AddThemeStyleboxOverride("hover", CreateBox(new("3a331c"), Gold, 6, 2, 8, 4));
                btn.AddThemeStyleboxOverride("pressed", CreateBox(new("3a331c"), Gold, 6, 2, 8, 4));
                btn.AddThemeColorOverride("font_color", Gold);
                btn.AddThemeColorOverride("font_hover_color", Colors.White);
            }
            else
            {
                btn.AddThemeStyleboxOverride("normal", CreateBox(new("16181f"), new("383e4a"), 6, 1, 8, 4));
                btn.AddThemeStyleboxOverride("hover", CreateBox(new("242833"), new("555e70"), 6, 1, 8, 4));
                btn.AddThemeStyleboxOverride("pressed", CreateBox(new("242833"), new("555e70"), 6, 1, 8, 4));
                btn.AddThemeColorOverride("font_color", TextMuted);
                btn.AddThemeColorOverride("font_hover_color", Colors.White);
            }
        }
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
    // MODAL DIALOG SETUP (Centered Floating Dialog: 940px width x 620px height)
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
            OffsetLeft = -470,
            OffsetRight = 470,
            OffsetTop = -310,
            OffsetBottom = 310,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        var dialogStyle = CreateBox(DialogBg, Gold, 10, 1, 20, 16);
        dialogStyle.BorderWidthTop = 3;
        dialogStyle.BorderColor = Gold;
        _dialogPanel.AddThemeStyleboxOverride("panel", dialogStyle);
        _root.AddChild(_dialogPanel);

        var dialogLayout = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        dialogLayout.AddThemeConstantOverride("separation", 12);
        _dialogPanel.AddChild(dialogLayout);

        // Header Section (Single prominent Gold Title, clean subtitle, no duplicate badge)
        _dialogHeader = new VBoxContainer();
        _dialogHeader.AddThemeConstantOverride("separation", 4);
        dialogLayout.AddChild(_dialogHeader);

        _dialogTitle = new Label
        {
            Text = "BIOBUZZ SIMULATOR",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _dialogTitle.AddThemeFontSizeOverride("font_size", 26);
        _dialogTitle.AddThemeColorOverride("font_color", Gold);
        _dialogHeader.AddChild(_dialogTitle);

        _dialogSubtitle = new Label
        {
            Text = "GODOT + C#  /  LOCAL DEVELOPMENT BUILD",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _dialogSubtitle.AddThemeFontSizeOverride("font_size", 14);
        _dialogSubtitle.AddThemeColorOverride("font_color", TextMuted);
        _dialogHeader.AddChild(_dialogSubtitle);

        var div = new HSeparator();
        div.AddThemeStyleboxOverride("separator", CreateBox(BtnNormalBorder, Colors.Transparent, 1, 0, 0, 1));
        _dialogHeader.AddChild(div);

        // Scrollable Body
        _dialogScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        dialogLayout.AddChild(_dialogScroll);
        _dialogScroll.GetVScrollBar().Modulate = Colors.Transparent;
        _dialogScroll.GetVScrollBar().CustomMinimumSize = Vector2.Zero;
        _dialogScroll.GetHScrollBar().Modulate = Colors.Transparent;
        _dialogScroll.GetHScrollBar().CustomMinimumSize = Vector2.Zero;

        _dialogBody = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _dialogBody.AddThemeConstantOverride("separation", 12);
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
        if (clearStack)
        {
            _navStack.Clear();
        }
        else if (_navStack.Contains(screen))
        {
            while (_navStack.Count > 0 && _navStack.Peek() != screen)
            {
                _navStack.Pop();
            }
        }

        if (_navStack.Count == 0 || _navStack.Peek() != screen)
        {
            _navStack.Push(screen);
        }

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
        if (_dialogBody != null)
        {
            foreach (var child in _dialogBody.GetChildren())
            {
                _dialogBody.RemoveChild(child);
                child.QueueFree();
            }
        }
    }

    private void ClearDialog(string title, string subtitle = "", bool resetScroll = true)
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
        _dialogScroll.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        _dialogScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        if (resetScroll) _dialogScroll.ScrollVertical = 0;
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
            case ScreenType.RobotWorkshop:
            case ScreenType.RobotRoster:
                BuildRobotRoster();
                break;
            case ScreenType.RobotCreator:
                BuildRobotCreator();
                break;
            case ScreenType.RobotImport:
                ImportSetup();
                break;
            case ScreenType.PracticePositions:
                BuildPracticePositions();
                break;
            case ScreenType.AutonomousPathing:
                BuildAutonomousPaths();
                break;
            case ScreenType.AutonomousPathEditor:
                BuildAutonomousPathEditor();
                break;
            case ScreenType.Settings:
                BuildSettings();
                break;
            case ScreenType.Controls:
                BuildControls();
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

    private Button CreateModeCard(GridContainer parent, Color accentColor, string title, string description, Action action, bool primary = false)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(380, 115),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All
        };

        var cardNormal = CreateBox(CardBg, Gold, 10, 1, 16, 12);
        var cardHover = CreateBox(new Color(0.12f, 0.16f, 0.24f, 0.98f), Gold, 10, 2, 16, 12);
        var cardPressed = CreateBox(new Color(0.06f, 0.08f, 0.12f, 0.98f), Gold, 10, 2, 16, 12);
        var cardFocus = CreateBox(CardBg, new Color(0.28f, 0.36f, 0.48f, 0.9f), 10, 1, 16, 12);

        button.AddThemeStyleboxOverride("normal", cardNormal);
        button.AddThemeStyleboxOverride("hover", cardHover);
        button.AddThemeStyleboxOverride("pressed", cardPressed);
        button.AddThemeStyleboxOverride("focus", cardFocus);

        var vbox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vbox.OffsetLeft = 16; vbox.OffsetTop = 12; vbox.OffsetRight = -16; vbox.OffsetBottom = -12;
        vbox.AddThemeConstantOverride("separation", 6);
        button.AddChild(vbox);

        var titleLabel = new Label { Text = title };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);
        titleLabel.AddThemeColorOverride("font_color", TextWhite);
        vbox.AddChild(titleLabel);

        var descLabel = new Label
        {
            Text = description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        descLabel.AddThemeFontSizeOverride("font_size", 13);
        descLabel.AddThemeColorOverride("font_color", TextMuted);
        vbox.AddChild(descLabel);

        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    // =========================================================================
    // SCREEN: MAIN MENU
    // =========================================================================
    private void BuildMainMenu()
    {
        ClearDialog("BIOBUZZ SIMULATOR", "GODOT + C#  /  2026 BIOBUZZ AUTONOMOUS PLATFORM");

        // Primary Game Modes Card (2x2 Matrix)
        var modesCard = CreateCard(_dialogBody, "MATCH & TRAINING MODES");
        var modesGrid = new GridContainer { Columns = 2 };
        modesGrid.AddThemeConstantOverride("h_separation", 16);
        modesGrid.AddThemeConstantOverride("v_separation", 14);
        modesCard.AddChild(modesGrid);

        CreateModeCard(modesGrid, Gold, "AI TRAINING GROUND",
            "Dedicated reinforcement learning & sandbox grounds for training autonomous navigation and scoring policies.",
            () =>
            {
                Game.TrainingGround = true;
                Game.AutonomousDrill = true;
                Game.Practice = false;
                Game.TrainingLoopActive = false;
                Engine.TimeScale = Game.TrainingTimeMultiplier;
                Game.Reset();
                CloseMenu();
            });

        CreateModeCard(modesGrid, new("3b9ee0"), "30S AUTONOMOUS DRILL",
            "Drill the official 30-second autonomous period with waypoint trajectory following and scoring.",
            () =>
            {
                Engine.TimeScale = 1.0f;
                Game.TrainingGround = false;
                Game.AutonomousDrill = true;
                StartMatch(false);
            });

        CreateModeCard(modesGrid, Green, "PLAY FULL MATCH (158S)",
            "Official FTC BioBuzz rules: 30s Auto, 8s Transition, and 120s TeleOp with live alliance scoring.",
            () =>
            {
                Engine.TimeScale = 1.0f;
                Game.TrainingGround = false;
                Game.AutonomousDrill = false;
                StartMatch(false);
            });

        CreateModeCard(modesGrid, Purple, "FREE PRACTICE ARENA",
            "Unlimited sandbox driving: test robot maneuvers, spawn game pieces, and test scoring mechanisms.",
            () =>
            {
                Engine.TimeScale = 1.0f;
                Game.TrainingGround = false;
                Game.AutonomousDrill = false;
                StartMatch(true);
            });

        // Navigation & Tools Card
        var toolsCard = CreateCard(_dialogBody, "CONFIGURATION & TOOLS");
        var toolsRow = new HBoxContainer();
        toolsRow.AddThemeConstantOverride("separation", 14);
        toolsCard.AddChild(toolsRow);

        var rBtn = CreateButton(toolsRow, "ROBOT WORKSHOP", () => NavigateTo(ScreenType.RobotWorkshop), true);
        rBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var aBtn = CreateButton(toolsRow, "AUTONOMOUS PATHS", () => NavigateTo(ScreenType.AutonomousPathing), true);
        aBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var sBtn = CreateButton(toolsRow, "SETTINGS", () => NavigateTo(ScreenType.Settings));
        sBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var cBtn = CreateButton(toolsRow, "CONTROLS", () => NavigateTo(ScreenType.Controls));
        cBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var hBtn = CreateButton(toolsRow, "HOW TO PLAY", () => NavigateTo(ScreenType.Help));
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
        ClearDialog(Game.TrainingGround ? "AI TRAINING GROUNDS PAUSED" : "PAUSED", "");

        // 1. Resume & Restart Row
        var quickRow = new HBoxContainer();
        quickRow.AddThemeConstantOverride("separation", 14);
        _dialogBody.AddChild(quickRow);

        var resumeBtn = CreateButton(quickRow, "RESUME [ESC]", () => Game.TogglePause(), false);
        resumeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        resumeBtn.CustomMinimumSize = new(0, 42);

        var restartBtn = CreateButton(quickRow, "RESTART [R]", () => Game.Reset());
        restartBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        restartBtn.CustomMinimumSize = new(0, 42);

        if (!Game.TrainingGround)
        {
            // Autonomous & Waypoints Card (only for standard matches, removed from AI Training Ground)
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
        }

        // 2. Starting Positions Section (Quick load row + open full 3D manager)
        var posCard = CreateCard(_dialogBody, "STARTING POSITION");

        var posIntro = new Label
        {
            Text = "Quick spawn your robot at an official starting position, or open the 3D position manager:",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        posIntro.AddThemeFontSizeOverride("font_size", 14);
        posIntro.AddThemeColorOverride("font_color", TextMuted);
        posCard.AddChild(posIntro);

        var posRow = new HBoxContainer();
        posRow.AddThemeConstantOverride("separation", 10);
        posCard.AddChild(posRow);

        string[] quickPosLabels = { "RED NEAR", "RED FAR", "BLUE NEAR", "BLUE FAR", "CUSTOM" };
        for (int i = 0; i < 5; i++)
        {
            int pIndex = i;
            bool isCurrent = Game.SelectedStartingPositionIndex == pIndex;
            string caption = isCurrent ? $"{quickPosLabels[pIndex]} (ACTIVE)" : quickPosLabels[pIndex];
            var qBtn = CreateButton(posRow, caption, () =>
            {
                Game.SetPlayerStartingPosition(pIndex);
                Game.TogglePause();
            }, isCurrent);
            if (isCurrent)
            {
                qBtn.Disabled = true;
                var curBox = CreateBox(new Color("242214"), Gold, 8, 2, 16, 8);
                qBtn.AddThemeStyleboxOverride("disabled", curBox);
                qBtn.AddThemeColorOverride("font_disabled_color", Gold);
            }
            qBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            qBtn.CustomMinimumSize = new(0, 40);
        }

        var managePosRow = new HBoxContainer();
        posCard.AddChild(managePosRow);
        var managePosBtn = CreateButton(managePosRow, "MANAGE POSITIONS, SNAPSHOTS & 3D PREVIEW ➔", () =>
        {
            NavigateTo(ScreenType.PracticePositions);
        }, false);
        managePosBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        managePosBtn.CustomMinimumSize = new(0, 42);

        // 3. Navigation Grid (2 Balanced Columns, No Horizontal Cutoff)
        var navCard = CreateCard(_dialogBody, "WORKSHOP & SETTINGS");
        var navGrid = new GridContainer { Columns = 2 };
        navGrid.AddThemeConstantOverride("h_separation", 16);
        navGrid.AddThemeConstantOverride("v_separation", 12);
        navCard.AddChild(navGrid);

        var setupBtn = CreateButton(navGrid, "ROBOT SETUP", () => NavigateTo(ScreenType.RobotRoster));
        setupBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        setupBtn.CustomMinimumSize = new(0, 46);

        var autoPathBtn = CreateButton(navGrid, "AUTONOMOUS PATHS", () => NavigateTo(ScreenType.AutonomousPathing));
        autoPathBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        autoPathBtn.CustomMinimumSize = new(0, 46);

        var ctrlBtn = CreateButton(navGrid, "CONTROLS", () => NavigateTo(ScreenType.Controls));
        ctrlBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ctrlBtn.CustomMinimumSize = new(0, 46);

        var settBtn = CreateButton(navGrid, "SETTINGS", () => NavigateTo(ScreenType.Settings));
        settBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        settBtn.CustomMinimumSize = new(0, 46);

        string helpTitle = Game.TrainingGround ? "HOW TO MANAGE AI" : "HOW TO PLAY";
        var helpBtn = CreateButton(navCard, helpTitle, () => NavigateTo(ScreenType.Help));
        helpBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        helpBtn.CustomMinimumSize = new(0, 42);

        // 4. Footer: Main Menu
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _dialogBody.AddChild(footer);

        var menuBtn = CreateButton(footer, "MAIN MENU", () =>
        {
            Game.Started = false;
            NavigateTo(ScreenType.Main, true);
        });
        menuBtn.CustomMinimumSize = new(200, 50);
    }

    // =========================================================================
    // =========================================================================
    // SCREEN: ROBOT WORKSHOP (Robot List & Management)
    // =========================================================================
    private void BuildRobotWorkshop()
    {
        BuildRobotRoster();
    }

    // =========================================================================
    // SCREEN: PARAMETRIC CHASSIS BUILDER (Tuning sliders + 3D Preview)
    // =========================================================================
    private void BuildRobotCreator()
    {
        ClearDialog("BASIC ROBOT CHASSIS BUILDER", "Tune robot footprint, speeds, and subsystems with live 3D preview");

        // Pin the dialog body height and disable outer scrolling so the preview stays pinned
        _dialogScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _dialogBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        _draft ??= Game.Profile.Copy();
        _draft.Imported = null;
        var profile = _draft;

        var mainSplit = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        mainSplit.AddThemeConstantOverride("separation", 16);
        _dialogBody.AddChild(mainSplit);

        // LEFT COLUMN: Scrollable container for settings & sliders
        var leftScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(480, 0)
        };
        leftScroll.GetVScrollBar().Modulate = Colors.Transparent;
        leftScroll.GetVScrollBar().CustomMinimumSize = Vector2.Zero;
        mainSplit.AddChild(leftScroll);

        var leftCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        leftCol.AddThemeConstantOverride("separation", 14);
        leftScroll.AddChild(leftCol);

        // RIGHT COLUMN: Pinned 3D Preview & Sizing Compliance (Stationary)
        var rightCol = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            CustomMinimumSize = new(400, 0)
        };
        rightCol.AddThemeConstantOverride("separation", 14);
        mainSplit.AddChild(rightCol);

        // Right Column: 3D Visual Preview Card
        var previewCard = CreateCard(rightCol, "3D ROBOT PREVIEW");
        var preview = new RobotParametricPreview { Profile = profile.Copy(), CustomMinimumSize = new(380, 250) };
        previewCard.AddChild(preview);

        var previewHint = new Label
        {
            Text = "Drag left mouse to rotate the 3D model",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        previewHint.AddThemeFontSizeOverride("font_size", 14);
        previewHint.AddThemeColorOverride("font_color", TextMuted);
        previewCard.AddChild(previewHint);

        // Right Column: FTC Sizing Compliance Card
        var sizingCard = CreateCard(rightCol, "FTC 18\" SIZING COMPLIANCE");
        var sizingRow = new HBoxContainer();
        sizingRow.AddThemeConstantOverride("separation", 14);
        sizingCard.AddChild(sizingRow);

        bool isLegal = profile.WidthCm <= 45.721f && profile.LengthCm <= 45.721f;
        var legalBadge = CreateBadge(isLegal ? "SIZE OK" : "OVERSIZED !", isLegal ? Green : Red, Colors.White, 15);
        legalBadge.CustomMinimumSize = new(120, 30);
        sizingRow.AddChild(legalBadge);

        var sizingLabel = new Label
        {
            Text = $"{profile.WidthCm:0.0} x {profile.LengthCm:0.0} cm  ({profile.WidthCm / 2.54f:0.0}\" x {profile.LengthCm / 2.54f:0.0}\")",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        sizingLabel.AddThemeFontSizeOverride("font_size", 16);
        sizingLabel.AddThemeColorOverride("font_color", TextWhite);
        sizingRow.AddChild(sizingLabel);

        void UpdateSizing()
        {
            bool ok = profile.WidthCm <= 45.721f && profile.LengthCm <= 45.721f;
            legalBadge.Text = ok ? "SIZE OK" : "OVERSIZED !";
            legalBadge.AddThemeStyleboxOverride("normal", CreateBox(ok ? Green : Red, Colors.Transparent, 6, 0, 10, 4));
            sizingLabel.Text = $"{profile.WidthCm:0.0} x {profile.LengthCm:0.0} cm  ({profile.WidthCm / 2.54f:0.0}\" x {profile.LengthCm / 2.54f:0.0}\")";
        }

        // Left Column: Name & Presets Card
        var nameCard = CreateCard(leftCol, "ROBOT NAME & PRESETS");
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 12);
        nameCard.AddChild(nameRow);

        var nameLbl = new Label { Text = "Robot Name:", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        nameLbl.AddThemeFontSizeOverride("font_size", 18);
        nameLbl.AddThemeColorOverride("font_color", TextWhite);
        nameRow.AddChild(nameLbl);

        var nameEdit = new LineEdit { Text = profile.Name, MaxLength = 100, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        nameEdit.AddThemeFontSizeOverride("font_size", 17);
        nameEdit.TextChanged += text => profile.Name = text;
        nameRow.AddChild(nameEdit);

        var presetsGrid = new GridContainer { Columns = 2 };
        presetsGrid.AddThemeConstantOverride("h_separation", 10);
        presetsGrid.AddThemeConstantOverride("v_separation", 8);
        nameCard.AddChild(presetsGrid);

        // Left Column: Sliders Card
        var slidersCard = CreateCard(leftCol, "DRIVETRAIN & DIMENSIONS");

        HSlider widthSlider = null, lengthSlider = null, speedSlider = null, accelSlider = null, turnSlider = null;

        widthSlider = CreateSlider(slidersCard, "Width (cm)", 25.0, 45.72, 0.25, profile.WidthCm, v =>
        {
            profile.WidthCm = (float)v;
            UpdateSizing();
            preview.UpdateProfile(profile);
        }, v => $"{v:0.00} cm ({v / 2.54:0.0}\")");

        lengthSlider = CreateSlider(slidersCard, "Length (cm)", 25.0, 45.72, 0.25, profile.LengthCm, v =>
        {
            profile.LengthCm = (float)v;
            UpdateSizing();
            preview.UpdateProfile(profile);
        }, v => $"{v:0.00} cm ({v / 2.54:0.0}\")");

        speedSlider = CreateSlider(slidersCard, "Speed (m/s)", 0.3, 3.0, 0.05, profile.Speed, v =>
        {
            profile.Speed = (float)v;
            preview.UpdateProfile(profile);
        }, v => $"{v:0.00} m/s ({v / 0.0254:0} in/s)");

        accelSlider = CreateSlider(slidersCard, "Acceleration (m/s²)", 0.5, 8.0, 0.1, profile.Acceleration, v =>
        {
            profile.Acceleration = (float)v;
            preview.UpdateProfile(profile);
        }, v => $"{v:0.00} m/s²");

        turnSlider = CreateSlider(slidersCard, "Turn speed (rad/s)", 0.5, 6.0, 0.1, profile.TurnSpeed, v =>
        {
            profile.TurnSpeed = (float)v;
            preview.UpdateProfile(profile);
        }, v => $"{v:0.00} rad/s ({Mathf.RadToDeg((float)v):0}°/s)");

        // Left Column: Subsystems
        var subCard = CreateCard(leftCol, "SUBSYSTEMS");

        var turretRow = new HBoxContainer();
        turretRow.AddThemeConstantOverride("separation", 10);
        var tLbl = new Label { Text = "Turrets:", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        tLbl.AddThemeFontSizeOverride("font_size", 16);
        tLbl.CustomMinimumSize = new(90, 0);
        turretRow.AddChild(tLbl);

        var turretButtons = new Button[3];
        for (int t = 1; t <= 3; t++)
        {
            int turrets = t;
            int idx = t - 1;
            turretButtons[idx] = CreateButton(turretRow, $"{turrets} Turret{(turrets > 1 ? "s" : "")}", () =>
            {
                profile.Turrets = turrets;
                for (int b = 0; b < 3; b++)
                {
                    bool active = b + 1 == profile.Turrets;
                    turretButtons[b].AddThemeStyleboxOverride("normal", CreateBox(active ? Gold : BtnNormalBg, active ? Gold : BtnNormalBorder, 8, active ? 2 : 1, 12, 8));
                    turretButtons[b].AddThemeColorOverride("font_color", active ? Colors.Black : TextWhite);
                }
                preview.UpdateProfile(profile);
            }, profile.Turrets == turrets);
            turretButtons[idx].CustomMinimumSize = new(100, 38);
            turretButtons[idx].AddThemeFontSizeOverride("font_size", 15);
            turretButtons[idx].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        subCard.AddChild(turretRow);

        var intakeRow = new HBoxContainer();
        intakeRow.AddThemeConstantOverride("separation", 10);
        var iLbl = new Label { Text = "Intakes:", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        iLbl.AddThemeFontSizeOverride("font_size", 16);
        iLbl.CustomMinimumSize = new(90, 0);
        intakeRow.AddChild(iLbl);

        var intakeButtons = new Button[2];
        for (int i = 1; i <= 2; i++)
        {
            int intakes = i;
            int idx = i - 1;
            intakeButtons[idx] = CreateButton(intakeRow, intakes == 1 ? "1 Front Intake" : "2 Front + Rear", () =>
            {
                profile.Intakes = intakes;
                for (int b = 0; b < 2; b++)
                {
                    bool active = b + 1 == profile.Intakes;
                    intakeButtons[b].AddThemeStyleboxOverride("normal", CreateBox(active ? Gold : BtnNormalBg, active ? Gold : BtnNormalBorder, 8, active ? 2 : 1, 12, 8));
                    intakeButtons[b].AddThemeColorOverride("font_color", active ? Colors.Black : TextWhite);
                }
                preview.UpdateProfile(profile);
            }, profile.Intakes == intakes);
            intakeButtons[idx].CustomMinimumSize = new(140, 38);
            intakeButtons[idx].AddThemeFontSizeOverride("font_size", 15);
            intakeButtons[idx].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        subCard.AddChild(intakeRow);

        void ApplyPreset(string name, float w, float l, float spd, float acc, float trn, int tur, int intk)
        {
            profile.Name = name;
            nameEdit.Text = name;
            profile.Turrets = tur;
            profile.Intakes = intk;
            for (int b = 0; b < 3; b++)
            {
                bool active = b + 1 == profile.Turrets;
                turretButtons[b].AddThemeStyleboxOverride("normal", CreateBox(active ? Gold : BtnNormalBg, active ? Gold : BtnNormalBorder, 8, active ? 2 : 1, 12, 8));
                turretButtons[b].AddThemeColorOverride("font_color", active ? Colors.Black : TextWhite);
            }
            for (int b = 0; b < 2; b++)
            {
                bool active = b + 1 == profile.Intakes;
                intakeButtons[b].AddThemeStyleboxOverride("normal", CreateBox(active ? Gold : BtnNormalBg, active ? Gold : BtnNormalBorder, 8, active ? 2 : 1, 12, 8));
                intakeButtons[b].AddThemeColorOverride("font_color", active ? Colors.Black : TextWhite);
            }
            widthSlider.Value = w;
            lengthSlider.Value = l;
            speedSlider.Value = spd;
            accelSlider.Value = acc;
            turnSlider.Value = trn;
            UpdateSizing();
            preview.UpdateProfile(profile);
        }

        void AddPresetButton(string label, Action action)
        {
            var btn = CreateButton(presetsGrid, label, action);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btn.CustomMinimumSize = new(0, 38);
            btn.AddThemeFontSizeOverride("font_size", 15);
        }

        AddPresetButton("FTC Max 18\"", () => ApplyPreset("Max Swerve", 45.72f, 45.72f, 1.8f, 3.5f, 3.2f, 1, 1));
        AddPresetButton("Agile Compact", () => ApplyPreset("Agile Runner", 35.0f, 35.0f, 2.5f, 5.0f, 4.5f, 1, 1));
        AddPresetButton("Sniper Turret", () => ApplyPreset("Sniper Turret", 45.72f, 45.72f, 1.4f, 2.8f, 2.5f, 2, 1));
        AddPresetButton("Dual Harvester", () => ApplyPreset("Dual Intake", 42.0f, 42.0f, 1.6f, 3.2f, 3.0f, 1, 2));

        // Bottom Actions (PINNED)
        var actionsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actionsRow.AddThemeConstantOverride("separation", 14);
        _dialogBody.AddChild(actionsRow);

        var saveBtn = CreateButton(actionsRow, "SAVE + APPLY ACTIVE", () =>
        {
            try
            {
                _draft.Validate();
                _draft.Save();
                Game.Profile = _draft.Copy();
                Game.Status = $"Applied '{_draft.Name}' as active robot profile!";
                _draft = null;
                NavigateTo(ScreenType.Main, true);
            }
            catch (Exception ex)
            {
                Game.Status = "Cannot apply robot: " + ex.Message;
            }
        }, true);
        saveBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var addToListBtn = CreateButton(actionsRow, "SAVE TO ROBOT LIST", () =>
        {
            try
            {
                _draft.Validate();
                RobotRoster.AddOrUpdate(_draft);
                Game.Status = $"Saved '{_draft.Name}' to robot roster!";
                NavigateTo(ScreenType.RobotRoster);
            }
            catch (Exception ex)
            {
                Game.Status = "Cannot save: " + ex.Message;
            }
        });
        addToListBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var backBtn = CreateButton(actionsRow, "BACK", () =>
        {
            _draft = null;
            GoBack();
        });
        backBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    // =========================================================================
    // SCREEN: ROBOT ROSTER (Robot Array Management)
    // =========================================================================
    private void BuildRobotRoster(int preservedScroll = -1)
    {
        bool isTraining = Game.TrainingGround && Game.Started;
        string title = isTraining ? "ROBOT SETUP" : "ROBOT WORKSHOP";
        string subtitle = isTraining
            ? "Configure active robots on field and view saved robot profiles"
            : "View saved robot library, edit chassis profiles, or build and import CAD robots";
        ClearDialog(title, subtitle, resetScroll: preservedScroll < 0);

        void RefreshRoster()
        {
            int scroll = _dialogScroll != null ? _dialogScroll.ScrollVertical : -1;
            BuildRobotRoster(scroll);
        }

        if (isTraining)
        {
            // 1. Test Participants Card (1, 2, 3, or 4 robots) - ONLY from AI Training Ground Robot Setup!
            int curCount = Game.TrainingSolo ? 1 : Mathf.Clamp(Game.TrainingRobotCount, 1, 4);
            var countCard = CreateCard(_dialogBody, "ACTIVE ROBOTS ON FIELD");

            var countRow = new HBoxContainer();
            countRow.AddThemeConstantOverride("separation", 10);
            countCard.AddChild(countRow);

            (string Label, int Count)[] countOptions =
            {
                ("1 ROBOT (SOLO)", 1),
                ("2 ROBOTS (1v1)", 2),
                ("3 ROBOTS", 3),
                ("4 ROBOTS (2v2)", 4)
            };
            foreach (var (cLabel, cNum) in countOptions)
            {
                int targetNum = cNum;
                bool isSelected = curCount == targetNum;
                var cBtn = CreateButton(countRow, cLabel, () =>
                {
                    Game.TrainingRobotCount = targetNum;
                    Game.TrainingSolo = (targetNum == 1);
                    Game.ApplyTrainingSettings();
                    Game.SaveSettings();
                    RefreshRoster();
                }, isSelected);
                cBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                cBtn.CustomMinimumSize = new(0, 42);
                cBtn.AddThemeFontSizeOverride("font_size", 14);
            }

            // When 3 robots is selected, allow choosing alliance configuration
            if (curCount == 3)
            {
                var splitRow = new HBoxContainer();
                splitRow.AddThemeConstantOverride("separation", 10);
                countCard.AddChild(splitRow);

                var allyBtn = CreateButton(splitRow, "2 ON YOUR ALLIANCE (WITH ALLY VS 1)", () =>
                {
                    Game.TrainingThreeWithAlly = true;
                    Game.ApplyTrainingSettings();
                    Game.SaveSettings();
                    RefreshRoster();
                }, Game.TrainingThreeWithAlly);
                allyBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                allyBtn.CustomMinimumSize = new(0, 38);
                allyBtn.AddThemeFontSizeOverride("font_size", 13);

                var oppBtn = CreateButton(splitRow, "1 SOLO (YOU VS 2 OPPONENTS)", () =>
                {
                    Game.TrainingThreeWithAlly = false;
                    Game.ApplyTrainingSettings();
                    Game.SaveSettings();
                    RefreshRoster();
                }, !Game.TrainingThreeWithAlly);
                oppBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                oppBtn.CustomMinimumSize = new(0, 38);
                oppBtn.AddThemeFontSizeOverride("font_size", 13);
            }
        }
        else
        {
            // Robot Workshop Actions from Main Menu
            var actionsCard = CreateCard(_dialogBody, "ROBOT WORKSHOP ACTIONS");
            var actionsRow = new HBoxContainer();
            actionsRow.AddThemeConstantOverride("separation", 12);
            actionsCard.AddChild(actionsRow);

            var newParamBtn = CreateButton(actionsRow, "+ NEW PARAMETRIC ROBOT", () =>
            {
                _draft = new RobotProfile { Name = $"Custom Robot {RobotRoster.GetRoster().Count + 1}" };
                NavigateTo(ScreenType.RobotCreator);
            }, true);
            newParamBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            newParamBtn.CustomMinimumSize = new(0, 40);
            newParamBtn.AddThemeFontSizeOverride("font_size", 14);

            var newCadBtn = CreateButton(actionsRow, "+ IMPORT CAD ROBOT", () =>
            {
                _draft = new RobotProfile { Name = $"CAD Robot {RobotRoster.GetRoster().Count + 1}", Imported = new ImportedRobotDefinition() };
                NavigateTo(ScreenType.RobotImport);
            });
            newCadBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            newCadBtn.CustomMinimumSize = new(0, 40);
            newCadBtn.AddThemeFontSizeOverride("font_size", 14);

            var resetRosterBtn = CreateButton(actionsRow, "RESTORE DEFAULTS", () =>
            {
                RobotRoster.ResetToDefaults();
                RefreshRoster();
            });
            resetRosterBtn.CustomMinimumSize = new(160, 40);
            resetRosterBtn.AddThemeFontSizeOverride("font_size", 13);
        }

        // Saved Robots in Roster Cards
        var roster = RobotRoster.GetRoster();
        var rosterCard = CreateCard(_dialogBody, $"ROBOT LIBRARY ({roster.Count})");

        if (roster.Count == 0)
        {
            var emptyPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            emptyPanel.AddThemeStyleboxOverride("panel", CreateBox(CardInner, BtnNormalBorder, 8, 1, 20, 24));
            rosterCard.AddChild(emptyPanel);

            var emptyLabel = new Label
            {
                Text = isTraining
                    ? "No robots in library. Return to Robot Workshop on the Main Menu to create or import robots."
                    : "No robots in library. Click '+ NEW PARAMETRIC ROBOT' or '+ IMPORT CAD ROBOT' above to create one.",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 15);
            emptyLabel.AddThemeColorOverride("font_color", TextMuted);
            emptyPanel.AddChild(emptyLabel);
        }

        for (int i = 0; i < roster.Count; i++)
        {
            int index = i;
            var robot = roster[i];
            bool isPlayer = robot.Name == Game.Profile.Name;
            bool isBot2 = Game.AiSlotProfiles[0]?.Name == robot.Name || (Game.AiSlotProfiles[0] == null && Game.AiProfile?.Name == robot.Name);
            bool isBot3 = Game.AiSlotProfiles[1]?.Name == robot.Name || (Game.AiSlotProfiles[1] == null && Game.AiProfile?.Name == robot.Name);
            bool isBot4 = Game.AiSlotProfiles[2]?.Name == robot.Name || (Game.AiSlotProfiles[2] == null && Game.AiProfile?.Name == robot.Name);
            bool isAssigned = isPlayer || (isTraining && (isBot2 || isBot3 || isBot4));

            var rPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            rPanel.AddThemeStyleboxOverride("panel", CreateBox(CardInner, (isTraining ? isAssigned : isPlayer) ? Gold : BtnNormalBorder, 8, (isTraining ? isAssigned : isPlayer) ? 2 : 1, 16, 10));
            rosterCard.AddChild(rPanel);

            if (!isTraining)
            {
                // Robot Workshop from Main Menu:
                // Click row to select as active robot
                rPanel.MouseFilter = Control.MouseFilterEnum.Stop;
                rPanel.GuiInput += (InputEvent e) =>
                {
                    if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    {
                        Game.Profile = robot.Copy();
                        Game.Profile.Save();
                        Game.SaveSettings();
                        if (Game.Started) Game.Reset();
                        RefreshRoster();
                    }
                };

                // Single clean row: Robot Name on left, EDIT and DELETE on right
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);
                row.Alignment = BoxContainer.AlignmentMode.Center;
                rPanel.AddChild(row);

                var nameLabel = new Label
                {
                    Text = robot.Name,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    VerticalAlignment = VerticalAlignment.Center
                };
                nameLabel.AddThemeFontSizeOverride("font_size", 17);
                nameLabel.AddThemeColorOverride("font_color", isPlayer ? Gold : TextWhite);
                row.AddChild(nameLabel);

                var editBtn = CreateButton(row, "EDIT", () =>
                {
                    _draft = robot.Copy();
                    if (_draft.Imported != null) NavigateTo(ScreenType.RobotImport);
                    else NavigateTo(ScreenType.RobotCreator);
                });
                editBtn.CustomMinimumSize = new(80, 36);
                editBtn.AddThemeFontSizeOverride("font_size", 13);

                var delBtn = CreateButton(row, "DELETE", () =>
                {
                    bool wasPlayer = isPlayer;
                    RobotRoster.Remove(index);
                    if (wasPlayer && RobotRoster.GetRoster().Count > 0)
                    {
                        Game.Profile = RobotRoster.GetRoster()[0].Copy();
                        Game.Profile.Save();
                        if (Game.Started) Game.Reset();
                    }
                    else if (wasPlayer)
                    {
                        Game.Profile = new RobotProfile { Name = "Default Robot" };
                        Game.Profile.Save();
                        if (Game.Started) Game.Reset();
                    }
                    RefreshRoster();
                });
                delBtn.CustomMinimumSize = new(80, 36);
                delBtn.AddThemeFontSizeOverride("font_size", 13);
            }
            else
            {
                // AI Training Ground Robot Setup:
                // Ability to say which robot is Player and which is AI is fully preserved, with no text top right
                var rBox = new VBoxContainer();
                rBox.AddThemeConstantOverride("separation", 8);
                rPanel.AddChild(rBox);

                // Row 1: Robot Name on left (NO text on top right)
                var hRow = new HBoxContainer();
                rBox.AddChild(hRow);

                var nameLabel = new Label
                {
                    Text = robot.Name,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    VerticalAlignment = VerticalAlignment.Center
                };
                nameLabel.AddThemeFontSizeOverride("font_size", 18);
                nameLabel.AddThemeColorOverride("font_color", isPlayer ? Gold : TextWhite);
                hRow.AddChild(nameLabel);

                // Row 2: Slot Assignment Buttons + Edit/Delete
                var bRow = new HBoxContainer();
                bRow.AddThemeConstantOverride("separation", 8);
                rBox.AddChild(bRow);

                var assignLbl = new Label { Text = "Assign to:", CustomMinimumSize = new(70, 0) };
                assignLbl.AddThemeFontSizeOverride("font_size", 13);
                assignLbl.AddThemeColorOverride("font_color", TextMuted);
                bRow.AddChild(assignLbl);

                var setPlayerBtn = CreateButton(bRow, "PLAYER", () =>
                {
                    Game.Profile = robot.Copy();
                    Game.Profile.Save();
                    Game.SaveSettings();
                    if (Game.Started) Game.Reset();
                    RefreshRoster();
                }, isPlayer);
                setPlayerBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                setPlayerBtn.CustomMinimumSize = new(0, 36);
                setPlayerBtn.AddThemeFontSizeOverride("font_size", 13);

                var setBot2Btn = CreateButton(bRow, "BOT 2", () =>
                {
                    Game.AiSlotProfiles[0] = robot.Copy();
                    Game.AiProfile = robot.Copy();
                    Game.SaveSettings();
                    if (Game.Started) Game.Reset();
                    RefreshRoster();
                }, isBot2);
                setBot2Btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                setBot2Btn.CustomMinimumSize = new(0, 36);
                setBot2Btn.AddThemeFontSizeOverride("font_size", 13);

                var setBot3Btn = CreateButton(bRow, "BOT 3", () =>
                {
                    Game.AiSlotProfiles[1] = robot.Copy();
                    Game.SaveSettings();
                    if (Game.Started) Game.Reset();
                    RefreshRoster();
                }, isBot3);
                setBot3Btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                setBot3Btn.CustomMinimumSize = new(0, 36);
                setBot3Btn.AddThemeFontSizeOverride("font_size", 13);

                var setBot4Btn = CreateButton(bRow, "BOT 4", () =>
                {
                    Game.AiSlotProfiles[2] = robot.Copy();
                    Game.SaveSettings();
                    if (Game.Started) Game.Reset();
                    RefreshRoster();
                }, isBot4);
                setBot4Btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                setBot4Btn.CustomMinimumSize = new(0, 36);
                setBot4Btn.AddThemeFontSizeOverride("font_size", 13);

                var editBtn = CreateButton(bRow, "EDIT", () =>
                {
                    _draft = robot.Copy();
                    if (_draft.Imported != null) NavigateTo(ScreenType.RobotImport);
                    else NavigateTo(ScreenType.RobotCreator);
                });
                editBtn.CustomMinimumSize = new(80, 36);
                editBtn.AddThemeFontSizeOverride("font_size", 13);

                var delBtn = CreateButton(bRow, "DELETE", () =>
                {
                    bool wasPlayer = isPlayer;
                    RobotRoster.Remove(index);
                    if (wasPlayer && RobotRoster.GetRoster().Count > 0)
                    {
                        Game.Profile = RobotRoster.GetRoster()[0].Copy();
                        Game.Profile.Save();
                        if (Game.Started) Game.Reset();
                    }
                    else if (wasPlayer)
                    {
                        Game.Profile = new RobotProfile { Name = "Default Robot" };
                        Game.Profile.Save();
                        if (Game.Started) Game.Reset();
                    }
                    RefreshRoster();
                });
                delBtn.CustomMinimumSize = new(80, 36);
                delBtn.AddThemeFontSizeOverride("font_size", 13);
            }
        }

        // Footer
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _dialogBody.AddChild(footer);
        var backBtn = CreateButton(footer, "BACK [ESC]", () =>
        {
            if (isTraining)
            {
                GoBack();
            }
            else
            {
                NavigateTo(ScreenType.Main, true);
            }
        });
        backBtn.CustomMinimumSize = new(160, 42);

        if (preservedScroll >= 0)
        {
            _dialogScroll.ScrollVertical = preservedScroll;
            Callable.From(() =>
            {
                if (IsInstanceValid(_dialogScroll))
                {
                    _dialogScroll.ScrollVertical = preservedScroll;
                }
            }).CallDeferred();
        }
    }

    // =========================================================================
    // SCREEN: FIELD POSITIONS & PRACTICE SNAPSHOTS
    // =========================================================================
    private void BuildPracticePositions(int preservedScroll = -1)
    {
        ClearDialog("FIELD POSITIONS & SNAPSHOTS", "Select FTC starting positions, drag or click to place robot & balls on 3D field, configure practice snapshots");

        // Pin the dialog body height and disable outer scrolling so preview remains locked on right
        _dialogScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _dialogBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        Vector3 curPos = Game.CustomStartingPosition ?? (Game.Player != null ? Game.Player.Position : new Vector3(0.84f, 0, -0.26f));
        float curHead = Game.CustomStartingPosition.HasValue ? Game.CustomStartingHeading : (Game.Player != null ? Game.Player.Rotation.Y : 0f);

        var mainSplit = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        mainSplit.AddThemeConstantOverride("separation", 20);
        _dialogBody.AddChild(mainSplit);

        // LEFT COLUMN: Scrollable container for positions, custom adjustments, snapshots & ball setups
        var leftScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(460, 0)
        };
        leftScroll.GetVScrollBar().Modulate = Colors.Transparent;
        leftScroll.GetVScrollBar().CustomMinimumSize = Vector2.Zero;
        mainSplit.AddChild(leftScroll);

        var leftCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        leftCol.AddThemeConstantOverride("separation", 14);
        leftScroll.AddChild(leftCol);

        // RIGHT COLUMN: Pinned / Locked 3D Field Preview
        var rightCol = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(400, 0)
        };
        rightCol.AddThemeConstantOverride("separation", 12);
        mainSplit.AddChild(rightCol);

        var previewCard = CreateCard(rightCol, "3D FIELD & SPAWN PREVIEW (LOCKED)");
        var preview = new FieldPositionPreview
        {
            Game = Game,
            Profile = Game.Profile.Copy(),
            CustomMinimumSize = new(340, 210),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        previewCard.AddChild(preview);

        var previewStatusLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        previewStatusLabel.AddThemeFontSizeOverride("font_size", 13);
        previewStatusLabel.AddThemeColorOverride("font_color", Gold);
        previewCard.AddChild(previewStatusLabel);

        var dragHint = new Label
        {
            Text = "Click once on robot or any ball to pick up & move freely • Click again to place\nHold & drag also supported • Right-click drag to orbit • Scroll wheel to zoom",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        dragHint.AddThemeFontSizeOverride("font_size", 12);
        dragHint.AddThemeColorOverride("font_color", TextMuted);
        previewCard.AddChild(dragHint);

        // Snapshot & Balls Setup Telemetry Card on Preview Screen
        var snapInfoCard = CreateCard(rightCol, "PRACTICE SNAPSHOTS & BALLS SETUP DISPLAY");
        var snapInfoBox = new VBoxContainer();
        snapInfoBox.AddThemeConstantOverride("separation", 6);
        snapInfoCard.AddChild(snapInfoBox);

        int looseBalls = 0;
        int matPollen = 0;
        foreach (var b in Game.Balls)
        {
            if (Game.IsFloorBall(b))
            {
                looseBalls++;
                if (b.Kind == PieceKind.Pollen) matPollen++;
            }
        }
        int matNectar = looseBalls - matPollen;
        int invPollen = Game.TrainingInventoryConfigured ? Game.TrainingInventoryPollen : 0;
        int invNectar = Game.TrainingInventoryConfigured ? Game.TrainingInventoryNectar : 0;
        if (!Game.TrainingInventoryConfigured && Game.Player != null)
        {
            foreach (var k in Game.Player.Inventory)
            {
                if (k == PieceKind.Pollen) invPollen++;
                else invNectar++;
            }
        }
        int invCount = invPollen + invNectar;

        var floorBallsLabel = new Label
        {
            Text = $"Floor Balls on Mat: {looseBalls} loose ({matPollen} Pollen, {matNectar} Nectar)",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        floorBallsLabel.AddThemeFontSizeOverride("font_size", 13);
        floorBallsLabel.AddThemeColorOverride("font_color", Gold);
        snapInfoBox.AddChild(floorBallsLabel);

        var invLoadLabel = new Label
        {
            Text = $"Robot Hopper Loadout: {invCount} / 4 pieces ({invPollen} Pollen, {invNectar} Nectar)",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        invLoadLabel.AddThemeFontSizeOverride("font_size", 13);
        invLoadLabel.AddThemeColorOverride("font_color", TextWhite);
        snapInfoBox.AddChild(invLoadLabel);

        void UpdateBallTelemetry()
        {
            int loose = 0;
            int pol = 0;
            foreach (var b in Game.Balls)
            {
                if (Game.IsFloorBall(b))
                {
                    loose++;
                    if (b.Kind == PieceKind.Pollen) pol++;
                }
            }
            int nec = loose - pol;
            floorBallsLabel.Text = $"Floor Balls on Mat: {loose} loose ({pol} Pollen, {nec} Nectar)";
        }

        void UpdateInventoryTelemetry()
        {
            int c = invPollen + invNectar;
            invLoadLabel.Text = $"Robot Hopper Loadout: {c} / 4 pieces ({invPollen} Pollen, {invNectar} Nectar)";
            Game.TrainingInventoryPollen = invPollen;
            Game.TrainingInventoryNectar = invNectar;
            Game.TrainingInventoryConfigured = true;
            Game.SetRobotInventoryPreset(invPollen, invNectar);
            preview.RefreshInventory();
        }

        var coordLabel = new Label
        {
            Text = $"Current Custom:  X: {curPos.X:0.00} m   •   Z: {curPos.Z:0.00} m   •   Heading: {Mathf.RadToDeg(curHead):0.0}°",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        coordLabel.AddThemeFontSizeOverride("font_size", 14);
        coordLabel.AddThemeColorOverride("font_color", Gold);

        var xEdit = new LineEdit
        {
            Text = curPos.X.ToString("0.00"),
            CustomMinimumSize = new(0, 36),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var zEdit = new LineEdit
        {
            Text = curPos.Z.ToString("0.00"),
            CustomMinimumSize = new(0, 36),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var headEdit = new LineEdit
        {
            Text = Mathf.RadToDeg(curHead).ToString("0.0"),
            CustomMinimumSize = new(0, 36),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        // 1. Starting Positions Selection Card
        var posCard = CreateCard(leftCol, "STARTING POSITIONS (FTC 2026 BIOBUZZ)");

        var posButtons = new List<Button>();
        Button customBtn = null;

        void HighlightPositionButton(int selectedIdx)
        {
            _selectedPositionIndex = selectedIdx;
            Game.SelectedStartingPositionIndex = selectedIdx;
            for (int i = 0; i < posButtons.Count; i++)
            {
                bool active = (i == selectedIdx);
                var btnStyle = CreateBox(active ? new Color("242214") : BtnNormalBg, active ? Gold : BtnNormalBorder, 6, active ? 2 : 1, 16, 8);
                posButtons[i].AddThemeStyleboxOverride("normal", btnStyle);
                posButtons[i].AddThemeStyleboxOverride("disabled", btnStyle);
                posButtons[i].AddThemeColorOverride("font_color", active ? Gold : TextWhite);
                posButtons[i].AddThemeColorOverride("font_disabled_color", Gold);
                posButtons[i].Disabled = active;
            }
        }

        for (int i = 0; i < Simulation.DefaultStartingPositions.Length; i++)
        {
            int pIndex = i;
            var def = Simulation.DefaultStartingPositions[pIndex];
            bool isSelected = _selectedPositionIndex == pIndex;
            var pBtn = CreateButton(posCard, $"{pIndex + 1}. {def.Name.ToUpperInvariant()}", () =>
            {
                SelectStartingPreset(pIndex);
            }, isSelected);
            pBtn.CustomMinimumSize = new(0, 38);
            pBtn.AddThemeFontSizeOverride("font_size", 14);
            pBtn.Disabled = isSelected;
            posButtons.Add(pBtn);
        }

        string customText = Game.CustomStartingPosition.HasValue
            ? $"5. CUSTOM POSITION ({curPos.X:0.00}, {curPos.Z:0.00})  [{Mathf.RadToDeg(curHead):0}°]"
            : "5. CUSTOM POSITION (CUSTOM)";
        customBtn = CreateButton(posCard, customText, () =>
        {
            SelectStartingPreset(4);
        }, _selectedPositionIndex == 4);
        customBtn.CustomMinimumSize = new(0, 38);
        customBtn.AddThemeFontSizeOverride("font_size", 14);
        customBtn.Disabled = (_selectedPositionIndex == 4);
        posButtons.Add(customBtn);

        // 2. Custom Position & Heading Setup Card
        var customCard = CreateCard(leftCol, "CUSTOM POSITION & HEADING SETUP");

        customCard.AddChild(coordLabel);

        // Numeric LineEdit inputs for direct typing of X, Z, and Heading
        var inputGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        inputGrid.AddThemeConstantOverride("h_separation", 12);
        inputGrid.AddThemeConstantOverride("v_separation", 8);
        customCard.AddChild(inputGrid);

        // X Input
        var xCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var xLbl = new Label { Text = "X Coord (m):" };
        xLbl.AddThemeFontSizeOverride("font_size", 14);
        xCol.AddChild(xLbl);
        xCol.AddChild(xEdit);
        inputGrid.AddChild(xCol);

        // Z Input
        var zCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var zLbl = new Label { Text = "Z Coord (m):" };
        zLbl.AddThemeFontSizeOverride("font_size", 14);
        zCol.AddChild(zLbl);
        zCol.AddChild(zEdit);
        inputGrid.AddChild(zCol);

        // Heading Input
        var hCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var hLbl = new Label { Text = "Heading (°):" };
        hLbl.AddThemeFontSizeOverride("font_size", 14);
        hCol.AddChild(hLbl);
        hCol.AddChild(headEdit);
        inputGrid.AddChild(hCol);

        bool updatingInputs = false;

        void ApplyTypedPosition()
        {
            if (updatingInputs) return;
            if (float.TryParse(xEdit.Text, out float xVal) &&
                float.TryParse(zEdit.Text, out float zVal) &&
                float.TryParse(headEdit.Text, out float hDeg))
            {
                xVal = Mathf.Clamp(xVal, 0.25f, Arena.Size - 0.25f);
                zVal = Mathf.Clamp(zVal, -Arena.Size + 0.25f, -0.25f);
                float hRad = Mathf.DegToRad(hDeg);
                while (hRad > Mathf.Pi) hRad -= Mathf.Tau;
                while (hRad < -Mathf.Pi) hRad += Mathf.Tau;

                Game.CustomStartingPosition = new Vector3(xVal, 0, zVal);
                Game.CustomStartingHeading = hRad;
                _selectedPositionIndex = 4;
                Game.SelectedStartingPositionIndex = 4;

                preview.SetPosition(new Vector3(xVal, 0, zVal), hRad);
                coordLabel.Text = $"Current Custom:  X: {xVal:0.00} m   •   Z: {zVal:0.00} m   •   Heading: {hDeg:0.0}°";
                previewStatusLabel.Text = $"Custom Position  •  X: {xVal:0.00}m, Z: {zVal:0.00}m  •  Heading: {hDeg:0.0}°";
                customBtn.Text = $"5. CUSTOM POSITION ({xVal:0.00}, {zVal:0.00})  [{hDeg:0}°]";
                HighlightPositionButton(4);
            }
        }

        xEdit.TextChanged += _ => ApplyTypedPosition();
        zEdit.TextChanged += _ => ApplyTypedPosition();
        headEdit.TextChanged += _ => ApplyTypedPosition();

        void SelectStartingPreset(int pIndex)
        {
            HighlightPositionButton(pIndex);
            Vector3 pos;
            float heading;
            string desc;
            if (pIndex >= 0 && pIndex < Simulation.DefaultStartingPositions.Length)
            {
                var p = Simulation.DefaultStartingPositions[pIndex];
                pos = p.Position;
                heading = p.Heading;
                desc = $"{p.Name}  •  X: {pos.X:0.00}m, Z: {pos.Z:0.00}m  •  Heading: {Mathf.RadToDeg(heading):0}°";
            }
            else
            {
                pos = Game.CustomStartingPosition ?? (Game.Player != null ? Game.Player.Position : new Vector3(0.84f, 0, -0.26f));
                heading = Game.CustomStartingPosition.HasValue ? Game.CustomStartingHeading : (Game.Player != null ? Game.Player.Rotation.Y : 0f);
                desc = $"Custom Position  •  X: {pos.X:0.00}m, Z: {pos.Z:0.00}m  •  Heading: {Mathf.RadToDeg(heading):0}°";
            }
            preview.SetPosition(pos, heading);
            previewStatusLabel.Text = desc;

            updatingInputs = true;
            xEdit.Text = pos.X.ToString("0.00");
            zEdit.Text = pos.Z.ToString("0.00");
            headEdit.Text = Mathf.RadToDeg(heading).ToString("0.0");
            updatingInputs = false;

            coordLabel.Text = $"Current Custom:  X: {pos.X:0.00} m   •   Z: {pos.Z:0.00} m   •   Heading: {Mathf.RadToDeg(heading):0.0}°";
        }

        void SetPose(Vector3 pos, float heading)
        {
            Game.CustomStartingPosition = pos;
            Game.CustomStartingHeading = heading;
            _selectedPositionIndex = 4;
            Game.SelectedStartingPositionIndex = 4;

            updatingInputs = true;
            xEdit.Text = pos.X.ToString("0.00");
            zEdit.Text = pos.Z.ToString("0.00");
            headEdit.Text = Mathf.RadToDeg(heading).ToString("0.0");
            updatingInputs = false;

            preview.SetPosition(pos, heading);
            float deg = Mathf.RadToDeg(heading);
            coordLabel.Text = $"Current Custom:  X: {pos.X:0.00} m   •   Z: {pos.Z:0.00} m   •   Heading: {deg:0.0}°";
            previewStatusLabel.Text = $"Custom Position  •  X: {pos.X:0.00}m, Z: {pos.Z:0.00}m  •  Heading: {deg:0.0}°";
            customBtn.Text = $"5. CUSTOM POSITION ({pos.X:0.00}, {pos.Z:0.00})  [{deg:0}°]";
            HighlightPositionButton(4);
        }

        preview.OnPositionDragged = hitPos =>
        {
            Game.CustomStartingPosition = hitPos;
            _selectedPositionIndex = 4;
            Game.SelectedStartingPositionIndex = 4;

            updatingInputs = true;
            xEdit.Text = hitPos.X.ToString("0.00");
            zEdit.Text = hitPos.Z.ToString("0.00");
            updatingInputs = false;

            float curH = Game.CustomStartingHeading;
            float degH = Mathf.RadToDeg(curH);
            coordLabel.Text = $"Current Custom:  X: {hitPos.X:0.00} m   •   Z: {hitPos.Z:0.00} m   •   Heading: {degH:0.0}°";
            previewStatusLabel.Text = $"Placed Robot at X: {hitPos.X:0.00}m, Z: {hitPos.Z:0.00}m  •  Heading: {degH:0.0}°";
            customBtn.Text = $"5. CUSTOM POSITION ({hitPos.X:0.00}, {hitPos.Z:0.00})  [{degH:0}°]";
            HighlightPositionButton(4);
        };

        preview.OnBallMoved = (ball, hitPos) =>
        {
            Game.TrainingBallsPreset = "custom";
            Game.TrainingFloorBallsConfigured = true;
            Game.CaptureTrainingFloorBalls();
            UpdateBallTelemetry();
        };

        preview.OnStatusChanged = status =>
        {
            previewStatusLabel.Text = status;
        };

        // Quick presets
        var presetRow = new HBoxContainer();
        presetRow.AddThemeConstantOverride("separation", 8);
        customCard.AddChild(presetRow);

        var capBtn = CreateButton(presetRow, "CAPTURE POSE", () =>
        {
            if (Game.Player != null)
            {
                SetPose(Game.Player.Position, Game.Player.Rotation.Y);
            }
        });
        capBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        capBtn.CustomMinimumSize = new(0, 36);
        capBtn.AddThemeFontSizeOverride("font_size", 13);

        var redBtn = CreateButton(presetRow, "RED START", () =>
        {
            SetPose(new Vector3(0.84f, 0, -0.26f), 0f);
        });
        redBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        redBtn.CustomMinimumSize = new(0, 36);
        redBtn.AddThemeFontSizeOverride("font_size", 13);

        var blueBtn = CreateButton(presetRow, "BLUE START", () =>
        {
            SetPose(new Vector3(Arena.Size - 0.84f, 0, -Arena.Size + 0.26f), Mathf.Pi);
        });
        blueBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        blueBtn.CustomMinimumSize = new(0, 36);
        blueBtn.AddThemeFontSizeOverride("font_size", 13);

        var centerBtn = CreateButton(presetRow, "CENTER FIELD", () =>
        {
            SetPose(new Vector3(Arena.Center, 0, -Arena.Center), 0f);
        });
        centerBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        centerBtn.CustomMinimumSize = new(0, 36);
        centerBtn.AddThemeFontSizeOverride("font_size", 13);

        // 3. Practice Snapshots & Ball Rearrangement Card
        var snapCard = CreateCard(leftCol, "PRACTICE SNAPSHOTS & BALLS SETUP");

        var ballLbl = new Label { Text = "Rearrange Arena Floor Balls:" };
        ballLbl.AddThemeFontSizeOverride("font_size", 15);
        snapCard.AddChild(ballLbl);

        var ballRow1 = new HBoxContainer();
        ballRow1.AddThemeConstantOverride("separation", 8);
        snapCard.AddChild(ballRow1);

        var ftcBtn = CreateButton(ballRow1, "OFFICIAL FTC (8 BALLS)", () =>
        {
            Game.SetFieldBallsPreset("ftc");
            preview.RefreshBalls();
            UpdateBallTelemetry();
        });
        ftcBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ftcBtn.CustomMinimumSize = new(0, 36);
        ftcBtn.AddThemeFontSizeOverride("font_size", 12);

        var spBtn = CreateButton(ballRow1, "SPARSE (12 BALLS)", () =>
        {
            Game.SetFieldBallsPreset("sparse");
            preview.RefreshBalls();
            UpdateBallTelemetry();
        });
        spBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        spBtn.CustomMinimumSize = new(0, 36);
        spBtn.AddThemeFontSizeOverride("font_size", 12);

        var ballRow2 = new HBoxContainer();
        ballRow2.AddThemeConstantOverride("separation", 8);
        snapCard.AddChild(ballRow2);

        var flBtn = CreateButton(ballRow2, "FULL (36 BALLS)", () =>
        {
            Game.SetFieldBallsPreset("full");
            preview.RefreshBalls();
            UpdateBallTelemetry();
        });
        flBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        flBtn.CustomMinimumSize = new(0, 36);
        flBtn.AddThemeFontSizeOverride("font_size", 12);

        var clrBtn = CreateButton(ballRow2, "CLEAR (0 BALLS)", () =>
        {
            Game.SetFieldBallsPreset("clear");
            preview.RefreshBalls();
            UpdateBallTelemetry();
        });
        clrBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        clrBtn.CustomMinimumSize = new(0, 36);
        clrBtn.AddThemeFontSizeOverride("font_size", 12);

        var invLbl = new Label { Text = "Robot Inventory Loadout (Capacity: Up to 4 Balls):" };
        invLbl.AddThemeFontSizeOverride("font_size", 15);
        snapCard.AddChild(invLbl);

        // Inventory +/- steppers for Pollen and Nectar up to capacity 4
        var stepperRow = new HBoxContainer();
        stepperRow.AddThemeConstantOverride("separation", 12);
        snapCard.AddChild(stepperRow);

        var polStepLbl = new Label { Text = $"Pollen: {invPollen}" };
        polStepLbl.AddThemeFontSizeOverride("font_size", 15);
        stepperRow.AddChild(polStepLbl);

        var polMinus = CreateButton(stepperRow, "-", () =>
        {
            if (invPollen > 0)
            {
                invPollen--;
                polStepLbl.Text = $"Pollen: {invPollen}";
                UpdateInventoryTelemetry();
            }
        });
        polMinus.CustomMinimumSize = new(40, 36);

        var polPlus = CreateButton(stepperRow, "+", () =>
        {
            if (invPollen + invNectar < 4)
            {
                invPollen++;
                polStepLbl.Text = $"Pollen: {invPollen}";
                UpdateInventoryTelemetry();
            }
        });
        polPlus.CustomMinimumSize = new(40, 36);

        var necStepLbl = new Label { Text = $"Nectar: {invNectar}" };
        necStepLbl.AddThemeFontSizeOverride("font_size", 15);
        stepperRow.AddChild(necStepLbl);

        var necMinus = CreateButton(stepperRow, "-", () =>
        {
            if (invNectar > 0)
            {
                invNectar--;
                necStepLbl.Text = $"Nectar: {invNectar}";
                UpdateInventoryTelemetry();
            }
        });
        necMinus.CustomMinimumSize = new(40, 36);

        var necPlus = CreateButton(stepperRow, "+", () =>
        {
            if (invPollen + invNectar < 4)
            {
                invNectar++;
                necStepLbl.Text = $"Nectar: {invNectar}";
                UpdateInventoryTelemetry();
            }
        });
        necPlus.CustomMinimumSize = new(40, 36);

        var emptyInvBtn = CreateButton(stepperRow, "CLEAR (0)", () =>
        {
            invPollen = 0;
            invNectar = 0;
            polStepLbl.Text = "Pollen: 0";
            necStepLbl.Text = "Nectar: 0";
            UpdateInventoryTelemetry();
        });
        emptyInvBtn.CustomMinimumSize = new(80, 36);
        emptyInvBtn.AddThemeFontSizeOverride("font_size", 13);

        if (!string.IsNullOrEmpty(Game.Status))
        {
            var statLbl = new Label
            {
                Text = Game.Status,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            statLbl.AddThemeFontSizeOverride("font_size", 14);
            statLbl.AddThemeColorOverride("font_color", Gold);
            snapCard.AddChild(statLbl);
        }

        // Footer pinned at bottom of dialog body (outside mainSplit)
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        footer.AddThemeConstantOverride("separation", 14);
        _dialogBody.AddChild(footer);

        var backBtn = CreateButton(footer, "BACK [ESC]", GoBack);
        backBtn.CustomMinimumSize = new(160, 42);

        var applySpawnBtn = CreateButton(footer, "APPLY & SPAWN TO FIELD", () =>
        {
            Game.SetPlayerStartingPosition(_selectedPositionIndex);
            Game.SelectedStartingPositionIndex = _selectedPositionIndex;
            Game.TrainingInventoryPollen = invPollen;
            Game.TrainingInventoryNectar = invNectar;
            Game.TrainingInventoryConfigured = true;
            if (Game.TrainingBallsPreset == "custom")
            {
                Game.CaptureTrainingFloorBalls();
            }
            Game.TrainingFloorBallsConfigured = true;
            Game.TrainingLoopActive = false;
            if (Game.TrainingGround || Game.AutonomousDrill)
            {
                Game.Reset();
            }
            Game.Save();
            Game.SaveSettings();
            CloseMenu();
        }, true);
        applySpawnBtn.CustomMinimumSize = new(220, 42);

        // Initialize preview display and inputs for current selection
        SelectStartingPreset(_selectedPositionIndex);

        if (preservedScroll >= 0 && leftScroll != null)
        {
            leftScroll.ScrollVertical = preservedScroll;
            Callable.From(() =>
            {
                if (IsInstanceValid(leftScroll))
                {
                    leftScroll.ScrollVertical = preservedScroll;
                }
            }).CallDeferred();
        }
    }

    // =========================================================================
    // SCREEN: AUTONOMOUS PATH ROSTER & MANAGEMENT
    // =========================================================================
    private Color GetPosColor(string pos)
    {
        if (string.IsNullOrEmpty(pos)) return TextMuted;
        if (pos.StartsWith("Red", StringComparison.OrdinalIgnoreCase)) return new Color("e04b4b");
        if (pos.StartsWith("Blue", StringComparison.OrdinalIgnoreCase)) return new Color("3b9ee0");
        return Gold;
    }

    private void BuildAutonomousPaths(int preservedScroll = -1)
    {
        bool isTraining = Game.TrainingGround && Game.Started;
        string title = isTraining ? "AUTONOMOUS PATH SETUP" : "AUTONOMOUS PATHS";
        string subtitle = isTraining
            ? "Configure autonomous routines for robots on field and manage path library"
            : "View saved autonomous path library, customize waypoints and actions, or create new routines";
        ClearDialog(title, subtitle, resetScroll: preservedScroll < 0);

        void RefreshPaths()
        {
            int scroll = _dialogScroll != null ? _dialogScroll.ScrollVertical : -1;
            BuildAutonomousPaths(scroll);
        }

        if (isTraining)
        {
            // 1. Active Autonomous Routines on Field Card
            var activeCard = CreateCard(_dialogBody, "ACTIVE AUTONOMOUS ROUTINES ON FIELD");
            var slotsGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            slotsGrid.AddThemeConstantOverride("h_separation", 14);
            slotsGrid.AddThemeConstantOverride("v_separation", 10);
            activeCard.AddChild(slotsGrid);

            string[] slotTitles = { "RED NEAR (SLOT 1 / PLAYER)", "RED FAR (SLOT 2)", "BLUE NEAR (SLOT 3)", "BLUE FAR (SLOT 4)" };
            Color[] slotColors = { new Color("e04b4b"), new Color("e04b4b"), new Color("3b9ee0"), new Color("3b9ee0") };

            for (int s = 0; s < 4; s++)
            {
                int sIdx = s;
                var sPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                sPanel.AddThemeStyleboxOverride("panel", CreateBox(CardInner, slotColors[s], 8, 1, 14, 10));
                slotsGrid.AddChild(sPanel);

                var sBox = new VBoxContainer();
                sBox.AddThemeConstantOverride("separation", 6);
                sPanel.AddChild(sBox);

                var sTitle = new Label { Text = slotTitles[s], SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                sTitle.AddThemeFontSizeOverride("font_size", 13);
                sTitle.AddThemeColorOverride("font_color", slotColors[s]);
                sBox.AddChild(sTitle);

                string assignedPath = sIdx < Game.AssignedAutonomousPaths.Length ? Game.AssignedAutonomousPaths[sIdx] : "";
                bool isManual = sIdx == 0 && (string.IsNullOrEmpty(assignedPath) || assignedPath.Equals("[MANUAL DRIVING]", StringComparison.OrdinalIgnoreCase));
                string displayPath = isManual ? "[MANUAL DRIVING (WASD)]" : (string.IsNullOrEmpty(assignedPath) ? "[NONE - IDLE]" : assignedPath);

                var sValue = new Label { Text = displayPath, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                sValue.AddThemeFontSizeOverride("font_size", 15);
                sValue.AddThemeColorOverride("font_color", isManual ? Gold : TextWhite);
                sBox.AddChild(sValue);

                if (sIdx == 0)
                {
                    var manBtn = CreateButton(sBox, isManual ? "MANUAL DRIVING (ACTIVE)" : "SWITCH TO MANUAL DRIVING", () =>
                    {
                        Game.AssignedAutonomousPaths[0] = isManual ? "Red Near 3-Piece Hive Cycle" : "[MANUAL DRIVING]";
                        Game.SaveSettings();
                        RefreshPaths();
                    }, isManual);
                    manBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    manBtn.CustomMinimumSize = new(0, 32);
                    manBtn.AddThemeFontSizeOverride("font_size", 12);
                }
            }
        }

        // Actions Card
        var actionsCard = CreateCard(_dialogBody, "AUTONOMOUS PATH ACTIONS");
        var actionsRow = new HBoxContainer();
        actionsRow.AddThemeConstantOverride("separation", 12);
        actionsCard.AddChild(actionsRow);

        var newPathBtn = CreateButton(actionsRow, "+ NEW AUTONOMOUS PATH", () =>
        {
            _draftPath = new AutonomousPathDefinition
            {
                Name = $"Custom Routine {AutonomousPathRoster.GetPaths().Count + 1}",
                StartingPosition = "Red Near",
                Waypoints = new List<AutonomousWaypoint>
                {
                    new() { X = 0.84f, Z = -0.75f, Action = WaypointAction.Shoot, ActionParam = 0.5f },
                    new() { X = 1.20f, Z = -1.20f, Action = WaypointAction.IntakeOn, ActionParam = 0f },
                    new() { X = 1.00f, Z = -0.75f, Action = WaypointAction.Shoot, ActionParam = 0.5f }
                }
            };
            NavigateTo(ScreenType.AutonomousPathEditor);
        }, true);
        newPathBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        newPathBtn.CustomMinimumSize = new(0, 40);
        newPathBtn.AddThemeFontSizeOverride("font_size", 14);

        var resetDefaultsBtn = CreateButton(actionsRow, "RESTORE DEFAULT PATHS", () =>
        {
            AutonomousPathRoster.ResetToDefaults();
            RefreshPaths();
        });
        resetDefaultsBtn.CustomMinimumSize = new(200, 40);
        resetDefaultsBtn.AddThemeFontSizeOverride("font_size", 13);

        // Saved Paths Library Card
        var paths = AutonomousPathRoster.GetPaths();
        var rosterCard = CreateCard(_dialogBody, $"AUTONOMOUS PATH LIBRARY ({paths.Count})");

        if (paths.Count == 0)
        {
            var emptyPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            emptyPanel.AddThemeStyleboxOverride("panel", CreateBox(CardInner, BtnNormalBorder, 8, 1, 20, 24));
            rosterCard.AddChild(emptyPanel);

            var emptyLabel = new Label
            {
                Text = "No autonomous paths in library. Click '+ NEW AUTONOMOUS PATH' or 'RESTORE DEFAULT PATHS' above.",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 15);
            emptyLabel.AddThemeColorOverride("font_color", TextMuted);
            emptyPanel.AddChild(emptyLabel);
        }

        for (int i = 0; i < paths.Count; i++)
        {
            int index = i;
            var path = paths[i];
            bool isRedNear = Game.AssignedAutonomousPaths[0] == path.Name;
            bool isRedFar = Game.AssignedAutonomousPaths[1] == path.Name;
            bool isBlueNear = Game.AssignedAutonomousPaths[2] == path.Name;
            bool isBlueFar = Game.AssignedAutonomousPaths[3] == path.Name;
            bool isAssigned = isRedNear || isRedFar || isBlueNear || isBlueFar;

            var rPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            rPanel.AddThemeStyleboxOverride("panel", CreateBox(CardInner, isAssigned ? Gold : BtnNormalBorder, 8, isAssigned ? 2 : 1, 16, 10));
            rosterCard.AddChild(rPanel);

            if (!isTraining)
            {
                // Robot Workshop style (from Main Menu):
                // Single clean row: Path Name on left, Start Pos badge, Waypoint badge, EDIT and DELETE on right (no text top right)
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);
                row.Alignment = BoxContainer.AlignmentMode.Center;
                rPanel.AddChild(row);

                var nameLabel = new Label
                {
                    Text = path.Name,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    VerticalAlignment = VerticalAlignment.Center
                };
                nameLabel.AddThemeFontSizeOverride("font_size", 17);
                nameLabel.AddThemeColorOverride("font_color", TextWhite);
                row.AddChild(nameLabel);

                var posBadge = CreateBadge(path.StartingPosition.ToUpper(), GetPosColor(path.StartingPosition), Colors.White, 12);
                posBadge.CustomMinimumSize = new(90, 28);
                row.AddChild(posBadge);

                var wpBadge = CreateBadge($"{path.Waypoints.Count} WP", CardInner, TextMuted, 12);
                wpBadge.CustomMinimumSize = new(60, 28);
                row.AddChild(wpBadge);

                var editBtn = CreateButton(row, "EDIT", () =>
                {
                    _draftPath = path.Copy();
                    NavigateTo(ScreenType.AutonomousPathEditor);
                });
                editBtn.CustomMinimumSize = new(80, 36);
                editBtn.AddThemeFontSizeOverride("font_size", 13);

                var delBtn = CreateButton(row, "DELETE", () =>
                {
                    AutonomousPathRoster.Remove(index);
                    RefreshPaths();
                });
                delBtn.CustomMinimumSize = new(80, 36);
                delBtn.AddThemeFontSizeOverride("font_size", 13);
            }
            else
            {
                // AI Training Ground Robot Setup style:
                // Row 1: Name on left, badges on right of name (no telemetry text top right)
                // Row 2: Slot assignment buttons (RED NEAR, RED FAR, BLUE NEAR, BLUE FAR) + EDIT + DELETE
                var rBox = new VBoxContainer();
                rBox.AddThemeConstantOverride("separation", 8);
                rPanel.AddChild(rBox);

                var hRow = new HBoxContainer();
                hRow.AddThemeConstantOverride("separation", 10);
                rBox.AddChild(hRow);

                var nameLabel = new Label
                {
                    Text = path.Name,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    VerticalAlignment = VerticalAlignment.Center
                };
                nameLabel.AddThemeFontSizeOverride("font_size", 18);
                nameLabel.AddThemeColorOverride("font_color", isAssigned ? Gold : TextWhite);
                hRow.AddChild(nameLabel);

                var posBadge = CreateBadge(path.StartingPosition.ToUpper(), GetPosColor(path.StartingPosition), Colors.White, 12);
                posBadge.CustomMinimumSize = new(90, 28);
                hRow.AddChild(posBadge);

                var wpBadge = CreateBadge($"{path.Waypoints.Count} WP", CardInner, TextMuted, 12);
                wpBadge.CustomMinimumSize = new(60, 28);
                hRow.AddChild(wpBadge);

                var bRow = new HBoxContainer();
                bRow.AddThemeConstantOverride("separation", 8);
                rBox.AddChild(bRow);

                var assignLbl = new Label { Text = "Assign to:", CustomMinimumSize = new(70, 0) };
                assignLbl.AddThemeFontSizeOverride("font_size", 13);
                assignLbl.AddThemeColorOverride("font_color", TextMuted);
                bRow.AddChild(assignLbl);

                var setRnBtn = CreateButton(bRow, "RED NEAR", () =>
                {
                    Game.AssignedAutonomousPaths[0] = isRedNear ? "[MANUAL DRIVING]" : path.Name;
                    Game.SaveSettings();
                    RefreshPaths();
                }, isRedNear);
                setRnBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                setRnBtn.CustomMinimumSize = new(0, 36);
                setRnBtn.AddThemeFontSizeOverride("font_size", 12);

                var setRfBtn = CreateButton(bRow, "RED FAR", () =>
                {
                    Game.AssignedAutonomousPaths[1] = isRedFar ? "" : path.Name;
                    Game.SaveSettings();
                    RefreshPaths();
                }, isRedFar);
                setRfBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                setRfBtn.CustomMinimumSize = new(0, 36);
                setRfBtn.AddThemeFontSizeOverride("font_size", 12);

                var setBnBtn = CreateButton(bRow, "BLUE NEAR", () =>
                {
                    Game.AssignedAutonomousPaths[2] = isBlueNear ? "" : path.Name;
                    Game.SaveSettings();
                    RefreshPaths();
                }, isBlueNear);
                setBnBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                setBnBtn.CustomMinimumSize = new(0, 36);
                setBnBtn.AddThemeFontSizeOverride("font_size", 12);

                var setBfBtn = CreateButton(bRow, "BLUE FAR", () =>
                {
                    Game.AssignedAutonomousPaths[3] = isBlueFar ? "" : path.Name;
                    Game.SaveSettings();
                    RefreshPaths();
                }, isBlueFar);
                setBfBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                setBfBtn.CustomMinimumSize = new(0, 36);
                setBfBtn.AddThemeFontSizeOverride("font_size", 12);

                var editBtn = CreateButton(bRow, "EDIT", () =>
                {
                    _draftPath = path.Copy();
                    NavigateTo(ScreenType.AutonomousPathEditor);
                });
                editBtn.CustomMinimumSize = new(70, 36);
                editBtn.AddThemeFontSizeOverride("font_size", 13);

                var delBtn = CreateButton(bRow, "DELETE", () =>
                {
                    AutonomousPathRoster.Remove(index);
                    RefreshPaths();
                });
                delBtn.CustomMinimumSize = new(70, 36);
                delBtn.AddThemeFontSizeOverride("font_size", 13);
            }
        }

        // Footer: Back button
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _dialogBody.AddChild(footer);

        var backBtn = CreateButton(footer, "BACK [ESC]", GoBack);
        backBtn.CustomMinimumSize = new(180, 50);
    }

    // =========================================================================
    // SCREEN: AUTONOMOUS PATH EDITOR / SIMULATOR
    // =========================================================================
    private void BuildAutonomousPathEditor(int preservedScroll = -1)
    {
        _draftPath ??= new AutonomousPathDefinition
        {
            Name = $"Custom Routine {AutonomousPathRoster.GetPaths().Count + 1}",
            StartingPosition = "Red Near",
            Waypoints = new List<AutonomousWaypoint>
            {
                new() { X = 0.84f, Z = -0.75f, Action = WaypointAction.Shoot, ActionParam = 0.5f },
                new() { X = 1.20f, Z = -1.20f, Action = WaypointAction.IntakeOn, ActionParam = 0f },
                new() { X = 1.00f, Z = -0.75f, Action = WaypointAction.Shoot, ActionParam = 0.5f }
            }
        };

        string title = string.IsNullOrEmpty(_draftPath.Name) ? "AUTONOMOUS PATH EDITOR" : $"PATH EDITOR: {_draftPath.Name.ToUpper()}";
        ClearDialog(title, "Interactive 2D waypoint trajectory & action simulator", resetScroll: preservedScroll < 0);

        // Pin the dialog body height and disable outer scrolling so the 2D visualizer stays pinned
        _dialogScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _dialogBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        ScrollContainer leftScroll = null;
        void RefreshEditor()
        {
            int scroll = leftScroll != null ? leftScroll.ScrollVertical : preservedScroll;
            BuildAutonomousPathEditor(scroll);
        }

        var mainSplit = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        mainSplit.AddThemeConstantOverride("separation", 16);
        _dialogBody.AddChild(mainSplit);

        leftScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(380, 0)
        };
        leftScroll.GetVScrollBar().Modulate = Colors.Transparent;
        leftScroll.GetVScrollBar().CustomMinimumSize = Vector2.Zero;
        mainSplit.AddChild(leftScroll);

        var leftCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        leftCol.AddThemeConstantOverride("separation", 10);
        leftScroll.AddChild(leftCol);

        // 1. Routine Settings Card
        var propsCard = CreateCard(leftCol, "ROUTINE SETTINGS");

        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 8);
        propsCard.AddChild(nameRow);

        var nameLbl = new Label { Text = "Name:", CustomMinimumSize = new(45, 0), VerticalAlignment = VerticalAlignment.Center };
        nameLbl.AddThemeFontSizeOverride("font_size", 13);
        nameLbl.AddThemeColorOverride("font_color", TextMuted);
        nameRow.AddChild(nameLbl);

        var nameInput = new LineEdit
        {
            Text = _draftPath.Name,
            PlaceholderText = "Routine Name...",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(0, 32)
        };
        nameInput.AddThemeFontSizeOverride("font_size", 13);
        nameInput.TextChanged += (txt) => _draftPath.Name = txt;
        nameRow.AddChild(nameInput);

        var posRow = new HBoxContainer();
        posRow.AddThemeConstantOverride("separation", 6);
        propsCard.AddChild(posRow);

        var posLbl = new Label { Text = "Start:", CustomMinimumSize = new(45, 0), VerticalAlignment = VerticalAlignment.Center };
        posLbl.AddThemeFontSizeOverride("font_size", 13);
        posLbl.AddThemeColorOverride("font_color", TextMuted);
        posRow.AddChild(posLbl);

        string[] positions = { "Red Near", "Red Far", "Blue Near", "Blue Far", "Any" };
        foreach (var pName in positions)
        {
            bool isSel = _draftPath.StartingPosition.Equals(pName, StringComparison.OrdinalIgnoreCase);
            var pBtn = CreateButton(posRow, pName.ToUpper(), () =>
            {
                string oldPos = _draftPath.StartingPosition;
                _draftPath.StartingPosition = pName;

                if (!oldPos.Equals(pName, StringComparison.OrdinalIgnoreCase))
                {
                    Vector2 oldStart = PedroPathVisualizer2D.GetStartPoseForPosition(oldPos);
                    Vector2 newStart = PedroPathVisualizer2D.GetStartPoseForPosition(pName);

                    bool oldIsBlue = oldPos.StartsWith("Blue", StringComparison.OrdinalIgnoreCase);
                    bool newIsBlue = pName.StartsWith("Blue", StringComparison.OrdinalIgnoreCase);

                    if (_draftPath.Waypoints.Count == 0)
                    {
                        float fwdZ = newIsBlue ? 0.5f : -0.5f;
                        _draftPath.Waypoints.Add(new AutonomousWaypoint
                        {
                            X = newStart.X,
                            Z = Mathf.Clamp(newStart.Y + fwdZ, -Arena.Size + 0.20f, -0.20f),
                            Action = WaypointAction.Shoot,
                            ActionParam = 0.5f
                        });
                    }
                    else if (oldIsBlue != newIsBlue)
                    {
                        // Mirror waypoints across field center
                        foreach (var wp in _draftPath.Waypoints)
                        {
                            wp.X = Mathf.Clamp(Arena.Size - wp.X, 0.20f, Arena.Size - 0.20f);
                            wp.Z = Mathf.Clamp(-Arena.Size - wp.Z, -Arena.Size + 0.20f, -0.20f);
                        }
                    }
                    else
                    {
                        // Near <-> Far shift on same alliance
                        Vector2 shift = newStart - oldStart;
                        foreach (var wp in _draftPath.Waypoints)
                        {
                            wp.X = Mathf.Clamp(wp.X + shift.X, 0.20f, Arena.Size - 0.20f);
                            wp.Z = Mathf.Clamp(wp.Z + shift.Y, -Arena.Size + 0.20f, -0.20f);
                        }
                    }
                }
                RefreshEditor();
            }, isSel);
            pBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            pBtn.CustomMinimumSize = new(0, 28);
            pBtn.AddThemeFontSizeOverride("font_size", 11);
        }

        // =========================================================================
        // RIGHT COLUMN: PINNED 2D PEDRO PATHING SIMULATOR
        // =========================================================================
        var rightCol = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            CustomMinimumSize = new(330, 0)
        };
        rightCol.AddThemeConstantOverride("separation", 10);

        var simCard = CreateCard(rightCol, "2D PATH SIMULATOR");

        var visualizer = new PedroPathVisualizer2D
        {
            Path = _draftPath,
            CustomMinimumSize = new Vector2(310, 310),
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.Fill
        };
        visualizer.OnWaypointDragFinished = RefreshEditor;
        simCard.AddChild(visualizer);

        // Pinned Simulation Controls Bar with fixed button widths so layout never jumps
        var simBar = new HBoxContainer();
        simBar.AddThemeConstantOverride("separation", 6);
        simCard.AddChild(simBar);

        Button simBtn = null;
        simBtn = CreateButton(simBar, visualizer.IsSimulating ? (visualizer.IsPaused ? "▶ RESUME" : "⏸ PAUSE") : "▶ SIMULATE PATH", () =>
        {
            visualizer.TogglePauseSimulation();
            if (visualizer.IsSimulating)
            {
                simBtn.Text = visualizer.IsPaused ? "▶ RESUME" : "⏸ PAUSE";
            }
            else
            {
                simBtn.Text = "▶ SIMULATE PATH";
            }
        }, true);
        simBtn.CustomMinimumSize = new(135, 36);
        simBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        simBtn.AddThemeFontSizeOverride("font_size", 12);

        visualizer.OnSimulationFinished = () =>
        {
            if (IsInstanceValid(simBtn)) simBtn.Text = "▶ SIMULATE PATH";
        };

        var restartSimBtn = CreateButton(simBar, "RESTART", () =>
        {
            visualizer.ResetSimulation();
            visualizer.StartSimulation();
            if (IsInstanceValid(simBtn)) simBtn.Text = "⏸ PAUSE";
        });
        restartSimBtn.CustomMinimumSize = new(75, 36);
        restartSimBtn.AddThemeFontSizeOverride("font_size", 12);

        var live3dBtn = CreateButton(simBar, "TEST ON FIELD", () =>
        {
            AutonomousPathRoster.AddOrUpdate(_draftPath);

            int posIdx = 0;
            if (_draftPath.StartingPosition.StartsWith("Red Far", StringComparison.OrdinalIgnoreCase)) posIdx = 1;
            else if (_draftPath.StartingPosition.StartsWith("Blue Near", StringComparison.OrdinalIgnoreCase)) posIdx = 2;
            else if (_draftPath.StartingPosition.StartsWith("Blue Far", StringComparison.OrdinalIgnoreCase)) posIdx = 3;

            Game.SelectedStartingPositionIndex = posIdx;
            Game.AssignedAutonomousPaths[0] = _draftPath.Name;
            Game.SaveSettings();

            Game.TrainingGround = true;
            Game.Reset(true);
            Game.TrainingLoopActive = true;
            Game.Countdown = 0f;
            Game.Elapsed = 0;
            Game.Paused = false;
            Game.Started = true;
            Engine.TimeScale = Game.TrainingTimeMultiplier;

            Game.StartAutonomousPathRunners();
            CloseMenu();
            TriggerStartFlash("START!", $"TESTING: {_draftPath.Name.ToUpper()}");
        });
        live3dBtn.CustomMinimumSize = new(100, 36);
        live3dBtn.AddThemeFontSizeOverride("font_size", 12);

        // 2. Waypoints List Card
        var wpsCard = CreateCard(leftCol, $"WAYPOINTS ({_draftPath.Waypoints.Count})");

        if (_draftPath.Waypoints.Count == 0)
        {
            var emptyPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            emptyPanel.AddThemeStyleboxOverride("panel", CreateBox(CardInner, BtnNormalBorder, 8, 1, 14, 16));
            wpsCard.AddChild(emptyPanel);

            var emptyLabel = new Label
            {
                Text = "No waypoints in routine yet. Use '+ ADD WAYPOINT' below to begin.",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 13);
            emptyLabel.AddThemeColorOverride("font_color", TextMuted);
            emptyPanel.AddChild(emptyLabel);
        }

        for (int k = 0; k < _draftPath.Waypoints.Count; k++)
        {
            int wpIndex = k;
            var wp = _draftPath.Waypoints[k];

            var wpPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            wpPanel.AddThemeStyleboxOverride("panel", CreateBox(CardInner, BtnNormalBorder, 6, 1, 8, 6));
            wpsCard.AddChild(wpPanel);

            var wpBox = new VBoxContainer();
            wpBox.AddThemeConstantOverride("separation", 6);
            wpPanel.AddChild(wpBox);

            // Row 1: Index badge, Action button, Action param (if Shoot/Wait), Spacer, Reorder ▲▼, Delete ✕
            var topRow = new HBoxContainer();
            topRow.AddThemeConstantOverride("separation", 6);
            topRow.Alignment = BoxContainer.AlignmentMode.Center;
            wpBox.AddChild(topRow);

            var numBadge = new Label
            {
                Text = $"#{wpIndex + 1}",
                CustomMinimumSize = new(24, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            numBadge.AddThemeFontSizeOverride("font_size", 13);
            numBadge.AddThemeColorOverride("font_color", Gold);
            topRow.AddChild(numBadge);

            // Action Toggle
            string actionText = wp.Action switch
            {
                WaypointAction.Shoot => "SHOOT",
                WaypointAction.IntakeOn => "INTAKE ON",
                WaypointAction.IntakeOff => "INTAKE OFF",
                WaypointAction.Wait => "WAIT",
                _ => "DRIVE"
            };

            var actionBtn = CreateButton(topRow, actionText, () =>
            {
                wp.Action = (WaypointAction)(((int)wp.Action + 1) % 5);
                RefreshEditor();
            }, wp.Action != WaypointAction.None);
            actionBtn.CustomMinimumSize = new(84, 26);
            actionBtn.AddThemeFontSizeOverride("font_size", 11);

            if (wp.Action == WaypointAction.Shoot || wp.Action == WaypointAction.Wait)
            {
                var timeLbl = new Label { Text = "TIME:", CustomMinimumSize = new(32, 0), VerticalAlignment = VerticalAlignment.Center };
                timeLbl.AddThemeFontSizeOverride("font_size", 11);
                timeLbl.AddThemeColorOverride("font_color", TextMuted);
                topRow.AddChild(timeLbl);

                var timeInput = new LineEdit
                {
                    Text = $"{wp.ActionParam:0.0}s",
                    CustomMinimumSize = new(48, 24),
                    Alignment = HorizontalAlignment.Center
                };
                timeInput.AddThemeFontSizeOverride("font_size", 11);

                void UpdateTime(float newTime)
                {
                    wp.ActionParam = Mathf.Clamp(Mathf.Round(newTime * 10f) / 10f, 0.1f, 10.0f);
                    timeInput.Text = $"{wp.ActionParam:0.0}s";
                    visualizer.QueueRedraw();
                }

                timeInput.FocusExited += () =>
                {
                    string clean = timeInput.Text.Trim().TrimEnd('s', 'S');
                    if (float.TryParse(clean, out float parsed)) UpdateTime(parsed);
                    else timeInput.Text = $"{wp.ActionParam:0.0}s";
                };
                timeInput.TextSubmitted += (txt) =>
                {
                    string clean = txt.Trim().TrimEnd('s', 'S');
                    if (float.TryParse(clean, out float parsed)) UpdateTime(parsed);
                    else timeInput.Text = $"{wp.ActionParam:0.0}s";
                    timeInput.ReleaseFocus();
                };

                var tMinus = CreateButton(topRow, "-", () => UpdateTime(wp.ActionParam - 0.1f));
                tMinus.CustomMinimumSize = new(22, 22);
                tMinus.AddThemeFontSizeOverride("font_size", 11);

                topRow.AddChild(timeInput);

                var tPlus = CreateButton(topRow, "+", () => UpdateTime(wp.ActionParam + 0.1f));
                tPlus.CustomMinimumSize = new(22, 22);
                tPlus.AddThemeFontSizeOverride("font_size", 11);
            }

            var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            topRow.AddChild(spacer);

            if (wpIndex > 0)
            {
                var upBtn = CreateButton(topRow, "▲", () =>
                {
                    (_draftPath.Waypoints[wpIndex], _draftPath.Waypoints[wpIndex - 1]) = (_draftPath.Waypoints[wpIndex - 1], _draftPath.Waypoints[wpIndex]);
                    RefreshEditor();
                });
                upBtn.CustomMinimumSize = new(22, 22);
                upBtn.AddThemeFontSizeOverride("font_size", 10);
            }

            if (wpIndex < _draftPath.Waypoints.Count - 1)
            {
                var downBtn = CreateButton(topRow, "▼", () =>
                {
                    (_draftPath.Waypoints[wpIndex], _draftPath.Waypoints[wpIndex + 1]) = (_draftPath.Waypoints[wpIndex + 1], _draftPath.Waypoints[wpIndex]);
                    RefreshEditor();
                });
                downBtn.CustomMinimumSize = new(22, 22);
                downBtn.AddThemeFontSizeOverride("font_size", 10);
            }

            var delWpBtn = CreateButton(topRow, "✕", () =>
            {
                _draftPath.Waypoints.RemoveAt(wpIndex);
                RefreshEditor();
            });
            delWpBtn.CustomMinimumSize = new(22, 22);
            delWpBtn.AddThemeFontSizeOverride("font_size", 10);

            // Row 2: Coordinates X and Y(Z) steppers with direct input
            var coordRow = new HBoxContainer();
            coordRow.AddThemeConstantOverride("separation", 6);
            wpBox.AddChild(coordRow);

            // X Stepper & Input
            var xTitle = new Label { Text = "X:", CustomMinimumSize = new(16, 0), VerticalAlignment = VerticalAlignment.Center };
            xTitle.AddThemeFontSizeOverride("font_size", 11);
            xTitle.AddThemeColorOverride("font_color", TextMuted);
            coordRow.AddChild(xTitle);

            var xInput = new LineEdit
            {
                Text = $"{wp.X:0.00}m",
                CustomMinimumSize = new(54, 24),
                Alignment = HorizontalAlignment.Center
            };
            xInput.AddThemeFontSizeOverride("font_size", 11);

            void UpdateX(float newX)
            {
                wp.X = Mathf.Clamp(Mathf.Round(newX * 100f) / 100f, 0.20f, Arena.Size - 0.20f);
                xInput.Text = $"{wp.X:0.00}m";
                visualizer.QueueRedraw();
            }

            xInput.FocusExited += () =>
            {
                string clean = xInput.Text.Trim().TrimEnd('m', 'M');
                if (float.TryParse(clean, out float parsed)) UpdateX(parsed);
                else xInput.Text = $"{wp.X:0.00}m";
            };
            xInput.TextSubmitted += (txt) =>
            {
                string clean = txt.Trim().TrimEnd('m', 'M');
                if (float.TryParse(clean, out float parsed)) UpdateX(parsed);
                else xInput.Text = $"{wp.X:0.00}m";
                xInput.ReleaseFocus();
            };

            var xMinus = CreateButton(coordRow, "-", () => UpdateX(wp.X - 0.05f));
            xMinus.CustomMinimumSize = new(22, 22);
            xMinus.AddThemeFontSizeOverride("font_size", 11);

            coordRow.AddChild(xInput);

            var xPlus = CreateButton(coordRow, "+", () => UpdateX(wp.X + 0.05f));
            xPlus.CustomMinimumSize = new(22, 22);
            xPlus.AddThemeFontSizeOverride("font_size", 11);

            var coordGap = new Control { CustomMinimumSize = new(12, 0) };
            coordRow.AddChild(coordGap);

            // Y (Field Z) Stepper & Input
            var yTitle = new Label { Text = "Y(Z):", CustomMinimumSize = new(30, 0), VerticalAlignment = VerticalAlignment.Center };
            yTitle.AddThemeFontSizeOverride("font_size", 11);
            yTitle.AddThemeColorOverride("font_color", TextMuted);
            coordRow.AddChild(yTitle);

            var zInput = new LineEdit
            {
                Text = $"{wp.Z:0.00}m",
                CustomMinimumSize = new(54, 24),
                Alignment = HorizontalAlignment.Center
            };
            zInput.AddThemeFontSizeOverride("font_size", 11);

            void UpdateZ(float newZ)
            {
                wp.Z = Mathf.Clamp(Mathf.Round(newZ * 100f) / 100f, -Arena.Size + 0.20f, -0.20f);
                zInput.Text = $"{wp.Z:0.00}m";
                visualizer.QueueRedraw();
            }

            zInput.FocusExited += () =>
            {
                string clean = zInput.Text.Trim().TrimEnd('m', 'M');
                if (float.TryParse(clean, out float parsed)) UpdateZ(parsed);
                else zInput.Text = $"{wp.Z:0.00}m";
            };
            zInput.TextSubmitted += (txt) =>
            {
                string clean = txt.Trim().TrimEnd('m', 'M');
                if (float.TryParse(clean, out float parsed)) UpdateZ(parsed);
                else zInput.Text = $"{wp.Z:0.00}m";
                zInput.ReleaseFocus();
            };

            var zMinus = CreateButton(coordRow, "-", () => UpdateZ(wp.Z - 0.05f));
            zMinus.CustomMinimumSize = new(22, 22);
            zMinus.AddThemeFontSizeOverride("font_size", 11);

            coordRow.AddChild(zInput);

            var zPlus = CreateButton(coordRow, "+", () => UpdateZ(wp.Z + 0.05f));
            zPlus.CustomMinimumSize = new(22, 22);
            zPlus.AddThemeFontSizeOverride("font_size", 11);
        }

        // Quick add waypoint row
        var addRow = new HBoxContainer();
        addRow.AddThemeConstantOverride("separation", 8);
        leftCol.AddChild(addRow);

        var addWpBtn = CreateButton(addRow, "+ ADD WAYPOINT", () =>
        {
            Vector2 startPose = PedroPathVisualizer2D.GetStartPoseForPosition(_draftPath.StartingPosition);
            float lastX = _draftPath.Waypoints.Count > 0 ? _draftPath.Waypoints[^1].X : startPose.X;
            float lastZ = _draftPath.Waypoints.Count > 0 ? _draftPath.Waypoints[^1].Z : startPose.Y;
            bool isBlue = _draftPath.StartingPosition.StartsWith("Blue", StringComparison.OrdinalIgnoreCase);
            float offsetZ = isBlue ? 0.35f : -0.35f;
            _draftPath.Waypoints.Add(new AutonomousWaypoint
            {
                X = Mathf.Clamp(lastX + (isBlue ? -0.20f : 0.20f), 0.20f, Arena.Size - 0.20f),
                Z = Mathf.Clamp(lastZ + offsetZ, -Arena.Size + 0.20f, -0.20f),
                Action = WaypointAction.None
            });
            RefreshEditor();
        }, true);
        addWpBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        addWpBtn.CustomMinimumSize = new(0, 34);
        addWpBtn.AddThemeFontSizeOverride("font_size", 12);

        var capBtn = CreateButton(addRow, "+ CAPTURE POSE", () =>
        {
            var p = Game.Player != null ? Game.Player.Position : new Vector3(0.84f, 0, -0.75f);
            _draftPath.Waypoints.Add(new AutonomousWaypoint
            {
                X = Mathf.Clamp(p.X, 0.20f, Arena.Size - 0.20f),
                Z = Mathf.Clamp(p.Z, -Arena.Size + 0.20f, -0.20f),
                Action = WaypointAction.None
            });
            RefreshEditor();
        });
        capBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        capBtn.CustomMinimumSize = new(0, 34);
        capBtn.AddThemeFontSizeOverride("font_size", 12);

        // Footer in left column
        var footRow = new HBoxContainer();
        footRow.AddThemeConstantOverride("separation", 8);
        leftCol.AddChild(footRow);

        var saveBtn = CreateButton(footRow, "SAVE ROUTINE", () =>
        {
            AutonomousPathRoster.AddOrUpdate(_draftPath);
            NavigateTo(ScreenType.AutonomousPathing);
        }, true);
        saveBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        saveBtn.CustomMinimumSize = new(0, 36);
        saveBtn.AddThemeFontSizeOverride("font_size", 13);

        var cancelBtn = CreateButton(footRow, "CANCEL [ESC]", () =>
        {
            NavigateTo(ScreenType.AutonomousPathing);
        });
        cancelBtn.CustomMinimumSize = new(90, 36);
        cancelBtn.AddThemeFontSizeOverride("font_size", 12);

        // Add right column to main split
        mainSplit.AddChild(rightCol);

        // Restore scroll position so editing never snaps to the top
        if (preservedScroll >= 0 && leftScroll != null)
        {
            leftScroll.ScrollVertical = preservedScroll;
            Callable.From(() =>
            {
                if (IsInstanceValid(leftScroll))
                {
                    leftScroll.ScrollVertical = preservedScroll;
                }
            }).CallDeferred();
        }
    }

    // =========================================================================
    // SCREEN: SETTINGS
    // =========================================================================
    private void BuildSettings(int preservedScroll = -1)
    {
        if (preservedScroll < 0 && _dialogScroll != null)
        {
            preservedScroll = _dialogScroll.ScrollVertical;
        }

        ClearDialog("SETTINGS", "Display, Camera, Gameplay, and Controls");

        // 1. Display & Graphics Settings
        var dispCard = CreateCard(_dialogBody, "DISPLAY & GRAPHICS");

        var dispGrid = new GridContainer { Columns = 2 };
        dispGrid.AddThemeConstantOverride("h_separation", 20);
        dispGrid.AddThemeConstantOverride("v_separation", 10);
        dispCard.AddChild(dispGrid);

        // Fullscreen Toggle
        bool isFullscreen = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        var fsLbl = new Label { Text = "Display Mode:", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        fsLbl.AddThemeFontSizeOverride("font_size", 18);
        dispGrid.AddChild(fsLbl);

        var fsBtn = CreateButton(dispGrid, isFullscreen ? "FULLSCREEN [ALT+ENTER]" : "WINDOWED", () =>
        {
            DisplayServer.WindowSetMode(isFullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
            Game.SaveSettings();
            BuildSettings(preservedScroll);
        }, isFullscreen);
        fsBtn.CustomMinimumSize = new(240, 44);

        // V-Sync Toggle
        bool isVsync = DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled;
        var vsyncLbl = new Label { Text = "Vertical Sync (V-Sync):", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        vsyncLbl.AddThemeFontSizeOverride("font_size", 18);
        dispGrid.AddChild(vsyncLbl);

        var vsyncBtn = CreateButton(dispGrid, isVsync ? "ENABLED (60 FPS CAP)" : "DISABLED (MAX FPS)", () =>
        {
            DisplayServer.WindowSetVsyncMode(isVsync ? DisplayServer.VSyncMode.Disabled : DisplayServer.VSyncMode.Enabled);
            Game.SaveSettings();
            BuildSettings(preservedScroll);
        }, isVsync);
        vsyncBtn.CustomMinimumSize = new(240, 44);

        // Trajectory Line Toggle
        var trajLbl = new Label { Text = "Shot Trajectory Prediction:", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        trajLbl.AddThemeFontSizeOverride("font_size", 18);
        dispGrid.AddChild(trajLbl);

        var trajBtn = CreateButton(dispGrid, Game.ShowTrajectory ? "VISIBLE (TRAJECTORY LINE ON)" : "HIDDEN (REALISTIC DRILL)", () =>
        {
            Game.ShowTrajectory = !Game.ShowTrajectory;
            Game.SaveSettings();
            BuildSettings(preservedScroll);
        }, Game.ShowTrajectory);
        trajBtn.CustomMinimumSize = new(240, 44);

        // 2. Camera View & Perspectives
        var camCard = CreateCard(_dialogBody, "CAMERA VIEW & PERSPECTIVES");

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
                Game.SaveSettings();
                BuildSettings(preservedScroll);
            }, Game.Camera.Mode == modeIndex);
            mBtn.CustomMinimumSize = new(140, 44);
        }
        camCard.AddChild(modeRow);

        CreateSlider(camCard, "Camera distance", 2.5, 12.0, 0.1, Game.Camera.Distance, v =>
        {
            Game.Camera.Distance = (float)v;
            Game.SaveSettings();
        }, v => $"{v:0.00} m");

        CreateSlider(camCard, "Field of view", 30.0, 75.0, 1.0, Game.Camera.Fov, v =>
        {
            Game.Camera.Fov = (float)v;
            Game.SaveSettings();
        }, v => $"{v:0.0}°");

        var resetCamBtn = CreateButton(camCard, "RESET CAMERA VIEW", () =>
        {
            Game.Camera.ResetView();
            Game.SaveSettings();
            BuildSettings(preservedScroll);
        });
        resetCamBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;

        // Back
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _dialogBody.AddChild(footer);
        var backBtn = CreateButton(footer, "BACK [ESC]", GoBack);
        backBtn.CustomMinimumSize = new(160, 42);

        if (preservedScroll >= 0 && _dialogScroll != null)
        {
            _dialogScroll.ScrollVertical = preservedScroll;
            Callable.From(() =>
            {
                if (IsInstanceValid(_dialogScroll))
                {
                    _dialogScroll.ScrollVertical = preservedScroll;
                }
            }).CallDeferred();
        }
    }

    // =========================================================================
    // SCREEN: CONTROLS & KEY REMAPPING
    // =========================================================================
    private void BuildControls()
    {
        ClearDialog("CONTROLS", "Configure custom keyboard controls, drive mode, and gamepad inputs");

        // Drive Mode Toggle Card
        var modeCard = CreateCard(_dialogBody, "DRIVING ORIENTATION MODE");
        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 14);
        modeCard.AddChild(modeRow);

        var modeBtn = CreateButton(modeRow, Game.RobotCentricDrive
            ? "ACTIVE: ROBOT-CENTRIC (DRIVES IN ROBOT HEADING DIRECTION) [N]"
            : "ACTIVE: FIELD-CENTRIC (FIXED W/A/S/D DIRECTIONS) [N]", () =>
        {
            Game.RobotCentricDrive = !Game.RobotCentricDrive;
            Game.SaveControls();
            BuildControls();
        }, Game.RobotCentricDrive);
        modeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        modeBtn.CustomMinimumSize = new(0, 46);

        var modeExpl = new Label
        {
            Text = "Default: field-centric. W/S and A/D keep the same directions on the field when the robot or camera turns. Press [N] to choose robot-centric driving relative to the robot heading.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        modeExpl.AddThemeFontSizeOverride("font_size", 14);
        modeExpl.AddThemeColorOverride("font_color", TextMuted);
        modeCard.AddChild(modeExpl);

        var controlsCard = CreateCard(_dialogBody, "INTERACTIVE KEY REMAPPING");

        if (_rebindingAction != null)
        {
            var alert = CreateBadge($"► PRESS ANY KEY TO REBIND [{_rebindingAction.ToUpperInvariant()}] (OR ESC TO CANCEL) ◄", Orange, Colors.White, 16);
            alert.CustomMinimumSize = new(0, 36);
            controlsCard.AddChild(alert);
        }

        var controlsHint = new Label
        {
            Text = "Click any action key below to remap it, then press the desired key on your keyboard. Press ESC to cancel.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        controlsHint.AddThemeFontSizeOverride("font_size", 15);
        controlsHint.AddThemeColorOverride("font_color", TextMuted);
        controlsCard.AddChild(controlsHint);

        // Key Remapping Container (2 Balanced Columns)
        var keysCols = new HBoxContainer();
        keysCols.AddThemeConstantOverride("separation", 16);
        controlsCard.AddChild(keysCols);

        var leftKeysCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        leftKeysCol.AddThemeConstantOverride("separation", 8);
        keysCols.AddChild(leftKeysCol);

        var rightKeysCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rightKeysCol.AddThemeConstantOverride("separation", 8);
        keysCols.AddChild(rightKeysCol);

        (string Id, string ActionName, Key CurrentKey)[] actionKeys =
        {
            ("Forward", "Drive Forward", Game.KeyForward),
            ("Backward", "Drive Backward", Game.KeyBackward),
            ("Left", "Strafe Left", Game.KeyLeft),
            ("Right", "Strafe Right", Game.KeyRight),
            ("TurnLeft", "Rotate Left", Game.KeyTurnLeft),
            ("TurnRight", "Rotate Right", Game.KeyTurnRight),
            ("DriveMode", "Toggle Drive Mode (N)", Game.KeyDriveMode),
            ("Intake", "Intake (Toggle)", Game.KeyIntake),
            ("Launch", "Launch Game Piece", Game.KeyLaunch),
            ("Target", "Toggle Target (Hive/Flower)", Game.KeyTarget),
            ("Camera", "Switch Camera View", Game.KeyCamera),
            ("Human", "Human Player Nectar", Game.KeyHuman),
            ("Extract", "Extract Flower Bottom", Game.KeyExtract),
            ("Reset", "Reset Match Simulation", Game.KeyReset)
        };

        for (int i = 0; i < actionKeys.Length; i++)
        {
            var (id, actionName, currentKey) = actionKeys[i];
            var col = i < (actionKeys.Length + 1) / 2 ? leftKeysCol : rightKeysCol;

            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 10);
            col.AddChild(row);

            var aLbl = new Label
            {
                Text = actionName,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            aLbl.AddThemeFontSizeOverride("font_size", 16);
            aLbl.AddThemeColorOverride("font_color", TextWhite);
            row.AddChild(aLbl);

            bool isRebindingThis = _rebindingAction == id;
            string keyText = isRebindingThis ? "► PRESS KEY ◄" : $"[ {GetKeyName(currentKey)} ]";
            var kBtn = CreateButton(row, keyText, () =>
            {
                _rebindingAction = isRebindingThis ? null : id;
                BuildControls();
            }, isRebindingThis);
            kBtn.CustomMinimumSize = new(130, 38);
            kBtn.AddThemeFontSizeOverride("font_size", 15);
        }

        var resetKeysBtn = CreateButton(controlsCard, "RESET ALL KEYS TO DEFAULTS (WASD)", () =>
        {
            Game.ResetKeyBindings();
            _rebindingAction = null;
            BuildControls();
        });
        resetKeysBtn.CustomMinimumSize = new(0, 44);

        // Gamepad and Mouse Quick Reference (2 Balanced Columns)
        var padCard = CreateCard(_dialogBody, "GAMEPAD & MOUSE CONTROLS");
        var padCols = new HBoxContainer();
        padCols.AddThemeConstantOverride("separation", 16);
        padCard.AddChild(padCols);

        var leftPadCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        leftPadCol.AddThemeConstantOverride("separation", 8);
        padCols.AddChild(leftPadCol);

        var rightPadCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rightPadCol.AddThemeConstantOverride("separation", 8);
        padCols.AddChild(rightPadCol);

        (string Input, string Func)[] padItems =
        {
            ("Left Stick", "Camera-relative omnidirectional ground movement"),
            ("Right Stick X", "Robot heading rotation"),
            ("Right Trigger / Bumper", "Launch active game piece"),
            ("Left Trigger / Bumper", "Intake game pieces from field (toggle)"),
            ("Right / Middle Mouse Drag", "Orbit 3D arena camera"),
            ("Shift + Mouse Drag", "Pan arena field camera"),
            ("Mouse Wheel", "Zoom camera in / out"),
            ("N Key", "Toggle fixed Field-Centric vs Robot-Centric drive"),
            ("F2 Key", "Toggle waypoint path editor"),
            ("P Key", "Follow waypoint path in practice"),
            ("F5 / F9 Keys", "Quick-save / quick-load practice arena state")
        };

        for (int i = 0; i < padItems.Length; i++)
        {
            var (input, func) = padItems[i];
            var col = i < (padItems.Length + 1) / 2 ? leftPadCol : rightPadCol;

            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 10);
            col.AddChild(row);

            var iBadge = CreateBadge(input, CardInner, Gold, 15);
            iBadge.CustomMinimumSize = new(150, 28);
            iBadge.HorizontalAlignment = HorizontalAlignment.Left;
            row.AddChild(iBadge);

            var fLbl = new Label
            {
                Text = func,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            fLbl.AddThemeFontSizeOverride("font_size", 14);
            fLbl.AddThemeColorOverride("font_color", TextWhite);
            row.AddChild(fLbl);
        }

        // Back
        var cFooter = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _dialogBody.AddChild(cFooter);
        var cBackBtn = CreateButton(cFooter, "BACK [ESC]", GoBack);
        cBackBtn.CustomMinimumSize = new(180, 52);
    }

    // =========================================================================
    // SCREEN: HOW TO PLAY (Match rules, timeline & scoring guide)
    // =========================================================================
    private void BuildHelp()
    {
        if (Game.TrainingGround)
        {
            ClearDialog("HOW TO MANAGE AI", "AI Training Grounds: Opponent Simulation, Autonomous Drill Practice & Field Setup");

            // 1. AI Opponents & Multi-Robot Simulation
            var aiCard = CreateCard(_dialogBody, "AI OPPONENTS & MULTI-ROBOT SIMULATION");
            var aiGrid = new GridContainer { Columns = 2 };
            aiGrid.AddThemeConstantOverride("h_separation", 20);
            aiGrid.AddThemeConstantOverride("v_separation", 10);
            aiCard.AddChild(aiGrid);

            (string Feature, string Description)[] aiFeatures =
            {
                ("Solo vs Multi-Robot Scenarios", "In Settings, toggle Solo Mode to practice alone, or select 2, 3, or 4 robots to test collision avoidance, traffic lanes, and defense."),
                ("Opponent AI Automation", "Opponent robots utilize real-time pathing and targeting heuristics to collect field pieces and score into the opposing alliance basket."),
                ("Field Formations & Placement", "Configure starting formations (FTC Starts, Hive Defense, Scattered Field, Wing/Corners) to test specific defense or clearance scenarios."),
                ("Autonomous Drill Testing", "Enable 30s Auto Drill in Training Settings to practice autonomous cycles and verify trajectory algorithms without match timeouts.")
            };

            foreach (var (feat, desc) in aiFeatures)
            {
                var fBadge = CreateBadge(feat, CardInner, Gold, 15);
                fBadge.CustomMinimumSize = new(220, 32);
                fBadge.HorizontalAlignment = HorizontalAlignment.Left;
                aiGrid.AddChild(fBadge);

                var dLbl = new Label { Text = desc, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
                dLbl.AddThemeFontSizeOverride("font_size", 15);
                dLbl.AddThemeColorOverride("font_color", TextWhite);
                aiGrid.AddChild(dLbl);
            }

            // 2. Spawn Positions & Scene Snapshots
            var snapCard = CreateCard(_dialogBody, "SPAWN POSITIONS & SCENE SNAPSHOTS");
            var snapLabel = new Label
            {
                Text = "• Quick Spawn: Use the quick position buttons in the Pause Menu (Red Near/Far, Blue Near/Far) to instantly test cycles from either alliance side.\n• Custom Setup & Edit: Press EDIT to select any robot from your roster, customize spawn coordinates, configure floor balls, and adjust robot inventory.\n• Scene Snapshots [F5 / F9]: Press F5 at any moment to save the complete arena state (including all game piece positions, inventories, Hive tilt angles, and timer), and press F9 to instantly reload.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            snapLabel.AddThemeFontSizeOverride("font_size", 15);
            snapLabel.AddThemeColorOverride("font_color", TextWhite);
            snapCard.AddChild(snapLabel);

            // 3. Driving Mode & Path Recording
            var driveCard = CreateCard(_dialogBody, "DRIVING ORIENTATION & PATHING TOOLS");
            var driveLabel = new Label
            {
                Text = "• Driving Orientation [N]: Field-Centric is the default: W/A/S/D directions stay fixed on the field even when the robot or camera turns. Press [N] to choose Robot-Centric driving relative to the robot heading.\n• Waypoint Path Editor [F2]: Press F2 to drop waypoints on the floor mat and P to trace paths for autonomous trajectory rehearsal.\n• Trajectory Line: Turn on the trajectory prediction arc in Settings to visualize firing arc and target intersection heights.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            driveLabel.AddThemeFontSizeOverride("font_size", 15);
            driveLabel.AddThemeColorOverride("font_color", TextMuted);
            driveCard.AddChild(driveLabel);

            // Back
            var aiFooter = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
            _dialogBody.AddChild(aiFooter);
            var aiBackBtn = CreateButton(aiFooter, "BACK [ESC]", GoBack);
            aiBackBtn.CustomMinimumSize = new(180, 52);
            return;
        }

        ClearDialog("HOW TO PLAY", "Official 2026 BioBuzz FTC Game Rules, Match Timing & Scoring Guide");

        // 1. Match Structure & Timeline
        var timeCard = CreateCard(_dialogBody, "MATCH STRUCTURE & TIMELINE (158 SECONDS)");
        var timeGrid = new GridContainer { Columns = 2 };
        timeGrid.AddThemeConstantOverride("h_separation", 20);
        timeGrid.AddThemeConstantOverride("v_separation", 10);
        timeCard.AddChild(timeGrid);

        (string Period, string Details)[] timeline =
        {
            ("30s Autonomous Period", "Pre-loaded game pieces are launched and scored following autonomous algorithms or recorded waypoint paths. No driver intervention permitted."),
            ("8s Driver Transition", "Robots halt autonomous movement; drivers take control of physical gamepads and prepare for TeleOp play."),
            ("120s TeleOp Period", "Full driver control. Navigate the arena, intake Pollen and Nectar game pieces from the field floor or Human Player substation, and score into the central Hive and Flower columns."),
            ("Final 30s End Game", "Special bonuses become active: alliance coordination for the 20-point Hive balance bonus and final alliance parking zones.")
        };

        foreach (var (period, details) in timeline)
        {
            var pBadge = CreateBadge(period, CardInner, Gold, 15);
            pBadge.CustomMinimumSize = new(200, 32);
            pBadge.HorizontalAlignment = HorizontalAlignment.Left;
            timeGrid.AddChild(pBadge);

            var dLbl = new Label { Text = details, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            dLbl.AddThemeFontSizeOverride("font_size", 15);
            dLbl.AddThemeColorOverride("font_color", TextWhite);
            timeGrid.AddChild(dLbl);
        }

        // 2. Field Scoring Breakdown
        var scoreCard = CreateCard(_dialogBody, "FIELD SCORING BREAKDOWN");
        var scoreGrid = new GridContainer { Columns = 2 };
        scoreGrid.AddThemeConstantOverride("h_separation", 20);
        scoreGrid.AddThemeConstantOverride("v_separation", 10);
        scoreCard.AddChild(scoreGrid);

        (string Action, string Points)[] scores =
        {
            ("Pollen Ball into Hive Basket", "+2 Points each"),
            ("Nectar Ball into Hive Basket", "+5 Points each"),
            ("Flower Column Extraction & Score", "+2 Points each"),
            ("Autonomous Movement & Parking", "+5 to +10 Points"),
            ("End Game Hive Balance / Tipping", "+20 Points")
        };

        foreach (var (action, points) in scores)
        {
            var aLbl = new Label { Text = action, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            aLbl.AddThemeFontSizeOverride("font_size", 16);
            aLbl.AddThemeColorOverride("font_color", TextWhite);
            scoreGrid.AddChild(aLbl);

            var pBadge = CreateBadge(points, CardInner, Green, 15);
            pBadge.CustomMinimumSize = new(170, 30);
            scoreGrid.AddChild(pBadge);
        }

        // 3. Pro Tips & Simulator Tools
        var tipsCard = CreateCard(_dialogBody, "TIPS & SIMULATOR FEATURES");
        var tipsLabel = new Label
        {
            Text = "• Trajectory Assist: Enable the launch trajectory arc in Settings to visualize firing angles and arc heights directly toward the basket.\n• Practice Grounds: Use the AI Training Ground to rehearse high-efficiency cycle routines or train autonomous neural network policies.\n• Waypoint Recording: Open the Path Editor (F2) in practice to place waypoints on the field, test pathing algorithms, and export autonomous routes.\n• Opponent Simulation: Toggle the Opponent AI in Settings to simulate realistic defensive pressure during practice matches.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        tipsLabel.AddThemeFontSizeOverride("font_size", 15);
        tipsLabel.AddThemeColorOverride("font_color", TextMuted);
        tipsCard.AddChild(tipsLabel);

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
        panel.AddThemeStyleboxOverride("panel", CreateBox(CardBg, Gold, 8, 1, 24, 18));
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
            CustomMinimumSize = new(140, 0)
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

        UpdateJointControls();

        _redScoreLabel.Text = Game.RedScore.ToString();
        _blueScoreLabel.Text = Game.BlueScore.ToString();

        string phaseName;
        float timeLeft;
        Color phaseColor;

        if (Game.TrainingGround)
        {
            phaseName = Game.TrainingLoopActive ? "AI TRAINING GROUND (30S LOOP)" : "AI TRAINING GROUND (STANDBY)";
            timeLeft = Game.TrainingLoopActive ? Mathf.Max(0, 30 - Game.Elapsed) : 30f;
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

        if (_trainingCornerContainer != null)
        {
            bool showTrainingControls = Game != null && Game.TrainingGround && !MenuVisible;
            _trainingCornerContainer.Visible = showTrainingControls;
            if (showTrainingControls)
            {
                if (Game.TrainingLoopActive)
                {
                    _trainingStartBtn.Text = "RESTART DRILL";
                    _trainingStopBtn.Visible = true;
                }
                else
                {
                    _trainingStartBtn.Text = "START 30S DRILL";
                    _trainingStopBtn.Visible = false;
                }

                if (!Mathf.IsEqualApprox(_lastKnownMultiplier, Game.TrainingTimeMultiplier))
                {
                    _lastKnownMultiplier = Game.TrainingTimeMultiplier;
                    UpdateMultiplierButtonStyles();
                }
            }
        }

        // Countdown Overlay Display
        if (Game.Countdown > 0 && !MenuVisible)
        {
            _countdownOverlay.Visible = true;
            float cd = Game.Countdown;
            // 3 seconds total: elapsed from 0 to 3s
            // Flashing: 1, 2, 3
            float elapsed = 3.0f - cd;
            string num;
            if (elapsed < 1.0f)
            {
                num = "1";
            }
            else if (elapsed < 2.0f)
            {
                num = "2";
            }
            else
            {
                num = "3";
            }

            // Flashing / pulsing effect for each second
            float secFrac = elapsed % 1.0f;
            float alpha = Mathf.Clamp(1.0f - secFrac * 0.45f, 0.55f, 1.0f);
            Color flashCol = new Color(Gold.R, Gold.G, Gold.B, alpha);

            _countdownNumberLabel.Text = num;
            _countdownNumberLabel.AddThemeColorOverride("font_color", flashCol);
            _countdownSubLabel.Text = "AUTONOMOUS DRILL IN 3s";
            _startFlashTimer = 0.5f;
        }
        else if (_startFlashTimer > 0 && !MenuVisible && (Game.AutonomousDrill || Game.TrainingGround))
        {
            _startFlashTimer -= (float)delta;
            _countdownOverlay.Visible = true;
            _countdownNumberLabel.Text = "START!";
            _countdownNumberLabel.AddThemeColorOverride("font_color", Green);
            _countdownSubLabel.Text = "AUTONOMOUS ACTIVE";
        }
        else
        {
            _countdownOverlay.Visible = false;
        }

    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (_rebindingAction != null)
            {
                if (k.PhysicalKeycode == Key.Escape)
                {
                    _rebindingAction = null;
                }
                else
                {
                    ApplyRebind(_rebindingAction, k.PhysicalKeycode);
                    _rebindingAction = null;
                }
                BuildControls();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (k.PhysicalKeycode == Key.Escape && _currentScreen != ScreenType.None)
            {
                GoBack();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void ApplyRebind(string action, Key key)
    {
        switch (action)
        {
            case "Forward": Game.KeyForward = key; break;
            case "Backward": Game.KeyBackward = key; break;
            case "Left": Game.KeyLeft = key; break;
            case "Right": Game.KeyRight = key; break;
            case "TurnLeft": Game.KeyTurnLeft = key; break;
            case "TurnRight": Game.KeyTurnRight = key; break;
            case "DriveMode": Game.KeyDriveMode = key; break;
            case "Intake": Game.KeyIntake = key; break;
            case "Launch": Game.KeyLaunch = key; break;
            case "Target": Game.KeyTarget = key; break;
            case "Camera": Game.KeyCamera = key; break;
            case "Reset": Game.KeyReset = key; break;
            case "Human": Game.KeyHuman = key; break;
            case "Extract": Game.KeyExtract = key; break;
        }
        Game.SaveControls();
    }

    private static string GetKeyName(Key key)
    {
        string name = OS.GetKeycodeString(key);
        if (string.IsNullOrEmpty(name)) name = key.ToString();
        return name;
    }

    // =========================================================================
    // CAD / URDF & PARTIAL CLASS COMPATIBILITY BRIDGES
    // =========================================================================
    private VBoxContainer _content => _dialogBody;
    private void Clear(string heading, string description = "") => ClearDialog(heading, description);
    private void Back() { _draft = null; if (Game.Started) ShowPause(true); else ShowMenu(); }
    private void RobotSetup() => NavigateTo(ScreenType.RobotCreator);
    private static Button Button(Node parent, string caption, Action action)
    {
        var button = new Button { Text = caption, FocusMode = Control.FocusModeEnum.All };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }
    private void Slider(string title, double min, double max, double value, Action<double> changed)
    {
        CreateSlider(_dialogBody, title, min, max, 0.05, value, changed);
    }
}
