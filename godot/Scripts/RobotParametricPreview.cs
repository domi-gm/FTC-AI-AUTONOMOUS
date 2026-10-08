using Godot;

public partial class RobotParametricPreview : SubViewportContainer
{
    public RobotProfile Profile;
    private Node3D _model;

    public override void _Ready()
    {
        Stretch = true;
        CustomMinimumSize = new(450, 240);

        var viewport = new SubViewport
        {
            Size = new(450, 240),
            OwnWorld3D = true,
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        };
        AddChild(viewport);

        viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("101824"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = 0.8f
            }
        });

        _model = BuildParametricMesh(Profile);
        viewport.AddChild(_model);

        var light = new DirectionalLight3D { RotationDegrees = new(-45, -30, 0), LightEnergy = 1.2f };
        viewport.AddChild(light);

        float w = (Profile?.WidthCm ?? 45.72f) / 100f;
        float l = (Profile?.LengthCm ?? 45.72f) / 100f;
        float extent = Mathf.Max(w, l);
        var center = new Vector3(0, 0.12f, 0);
        var camera = new Camera3D
        {
            Current = true,
            Fov = 35,
            Position = center + new Vector3(1.7f, 1.2f, 1.7f) * extent * 1.5f,
            Near = 0.001f,
            Far = 30
        };
        viewport.AddChild(camera);
        camera.LookAt(center);

        GuiInput += input =>
        {
            if (input is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Left) != 0)
            {
                _model?.RotateY(motion.Relative.X * 0.01f);
                AcceptEvent();
            }
        };
        TooltipText = "Drag left mouse to orbit the 3D robot chassis preview.";
    }

    private static Node3D BuildParametricMesh(RobotProfile profile)
    {
        var root = new Node3D();
        float w = (profile?.WidthCm ?? 45.72f) / 100f;
        float l = (profile?.LengthCm ?? 45.72f) / 100f;
        int turrets = profile?.Turrets ?? 1;
        int intakes = profile?.Intakes ?? 1;

        // Base Chassis
        VisualFactory.Box(root, "Chassis", new(0, 0.10f, 0), new(w, 0.12f, l), new("242b34"));
        // Top Deck
        VisualFactory.Box(root, "Deck", new(0, 0.165f, 0), new(w - 0.04f, 0.01f, l - 0.04f), VisualFactory.Steel);
        // Electronics
        VisualFactory.Box(root, "Electronics", new(0, 0.18f, 0.04f), new(0.14f, 0.03f, 0.10f), new("19222e"));

        // Bumpers (Gold outline)
        Color bumperCol = VisualFactory.Gold;
        foreach (int s in new[] { -1, 1 })
        {
            VisualFactory.Box(root, "BumperSide", new(s * w / 2, 0.15f, 0), new(0.026f, 0.09f, l), bumperCol);
            VisualFactory.Box(root, "BumperEnd", new(0, 0.15f, s * l / 2), new(w, 0.09f, 0.026f), bumperCol);
        }

        // Swerve Wheel Modules (4 corners)
        foreach (int x in new[] { -1, 1 })
        {
            foreach (int z in new[] { -1, 1 })
            {
                VisualFactory.Cylinder(root, "Wheel", new(x * (w / 2 - 0.045f), 0.045f, z * (l / 2 - 0.055f)), 0.045f, 0.025f, new("1a1a1a"));
            }
        }

        // Turrets
        if (turrets == 1)
        {
            VisualFactory.Cylinder(root, "Turret1", new(0, 0.22f, -0.05f), 0.045f, 0.10f, VisualFactory.Gold);
        }
        else
        {
            VisualFactory.Cylinder(root, "Turret1", new(-0.08f, 0.22f, -0.05f), 0.04f, 0.10f, VisualFactory.Gold);
            VisualFactory.Cylinder(root, "Turret2", new(0.08f, 0.22f, -0.05f), 0.04f, 0.10f, VisualFactory.Gold);
        }

        // Intakes
        VisualFactory.Box(root, "FrontIntake", new(0, 0.09f, -l / 2 - 0.025f), new(w * 0.7f, 0.045f, 0.045f), VisualFactory.Green);
        if (intakes == 2)
        {
            VisualFactory.Box(root, "RearIntake", new(0, 0.09f, l / 2 + 0.025f), new(w * 0.7f, 0.045f, 0.045f), VisualFactory.Green);
        }

        return root;
    }
}
