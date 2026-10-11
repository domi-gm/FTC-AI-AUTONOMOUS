using Godot;
using System;
using System.Collections.Generic;

public partial class FieldPositionPreview : SubViewportContainer
{
    public Simulation Game;
    public RobotProfile Profile;
    public Action<Vector3> OnPositionDragged;
    public Action<GamePiece, Vector3> OnBallMoved;
    public Action<string> OnStatusChanged;

    private SubViewport _viewport;
    private Camera3D _camera;
    private Node3D _robotMarker;
    private Node3D _robotMesh;
    private Node3D _inventoryMarker;
    private Node3D _ballsContainer;
    private Node3D _hoverRing;

    private float _orbitYaw = 0.45f;
    private float _orbitPitch = 0.62f;
    private float _orbitDist = 4.8f;
    private Vector3 _center;

    public class BallPreviewItem
    {
        public GamePiece Source;
        public Node3D Mesh;
        public PieceKind Kind;
        public float Radius;
    }
    private readonly List<BallPreviewItem> _ballItems = new();

    private BallPreviewItem _heldBall;
    private bool _heldRobot;
    private Vector2 _pressScreenPos;
    private bool _isPressed;
    private bool _justPickedUp;

    private bool RaycastFloor(Vector2 screenPos, out Vector3 hitPoint)
    {
        hitPoint = Vector3.Zero;
        if (_camera == null) return false;
        var origin = _camera.ProjectRayOrigin(screenPos);
        var dir = _camera.ProjectRayNormal(screenPos);
        if (Mathf.Abs(dir.Y) < 1e-5f) return false;
        float t = -origin.Y / dir.Y;
        if (t < 0) return false;
        hitPoint = origin + dir * t;
        // Clamp to FTC playing floor boundaries (with 25cm wall clearance):
        hitPoint.X = Mathf.Clamp(hitPoint.X, 0.25f, Arena.Size - 0.25f);
        hitPoint.Y = 0f;
        hitPoint.Z = Mathf.Clamp(hitPoint.Z, -Arena.Size + 0.25f, -0.25f);
        return true;
    }

    public override void _Ready()
    {
        Stretch = true;
        if (CustomMinimumSize == Vector2.Zero)
            CustomMinimumSize = new Vector2(360, 320);

        _viewport = new SubViewport
        {
            Size = new Vector2I((int)CustomMinimumSize.X, (int)CustomMinimumSize.Y),
            OwnWorld3D = true,
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible
        };
        AddChild(_viewport);

        _center = Arena.World(Arena.Center, Arena.Center);

        // Add complete FTC Arena (Floor mats, walls, flowers, lighting, sky)
        var arena = new Arena { Name = "PreviewArena" };
        _viewport.AddChild(arena);

        // Add Center Hives
        var redHive = new Hive
        {
            Red = true,
            Position = VisualFactory.Inch(Arena.Inches / 2 - 12.75f, Arena.Inches / 2, 43.95f),
            Name = "PreviewRedHive"
        };
        var blueHive = new Hive
        {
            Red = false,
            Position = VisualFactory.Inch(Arena.Inches / 2 + 12.75f, Arena.Inches / 2, 43.95f),
            Name = "PreviewBlueHive"
        };
        _viewport.AddChild(redHive);
        _viewport.AddChild(blueHive);

        // Container for loose floor balls
        _ballsContainer = new Node3D { Name = "PreviewBallsContainer" };
        _viewport.AddChild(_ballsContainer);

        // Floor hover target ring
        _hoverRing = VisualFactory.Mesh(_ballsContainer, "HoverTargetRing", new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.12f }, new Vector3(0, 0.005f, 0), VisualFactory.Gold);
        _hoverRing.Visible = false;

        // Robot marker node placed on field
        _robotMarker = new Node3D { Name = "PreviewRobotMarker" };
        _viewport.AddChild(_robotMarker);

        // High-contrast starting position floor highlight pad
        VisualFactory.Cylinder(_robotMarker, "SpawnPad", new Vector3(0, 0.003f, 0), 0.34f, 0.003f, VisualFactory.Gold);
        VisualFactory.Mesh(_robotMarker, "SpawnRing", new TorusMesh { InnerRadius = 0.32f, OuterRadius = 0.38f }, new Vector3(0, 0.005f, 0), VisualFactory.Gold);

        // Heading direction arrow (Front is -Z)
        VisualFactory.Beam(_robotMarker, "HeadingBeam", new Vector3(0, 0.01f, 0), new Vector3(0, 0.01f, -0.48f), 0.03f, VisualFactory.Gold);

        // Robot 3D model
        _robotMesh = RobotParametricPreview.BuildParametricMesh(Profile);
        _robotMarker.AddChild(_robotMesh);

        // Container for preloaded inventory balls inside the robot
        _inventoryMarker = new Node3D { Name = "PreviewInventoryMarker" };
        _robotMarker.AddChild(_inventoryMarker);

        // Camera
        _camera = new Camera3D
        {
            Current = true,
            Fov = 38,
            Near = 0.01f,
            Far = 50
        };
        _viewport.AddChild(_camera);
        UpdateCamera();

        // Mouse controls: Click once to pick up & move freely, click again to place; or click & drag
        GuiInput += HandleInput;
        TooltipText = "Click once on robot or any ball to pick up & move freely • Click again to place • Right-click drag to orbit camera • Scroll wheel to zoom";

        // Initialize balls and inventory
        RefreshBalls();
        RefreshInventory();
    }

    private Node3D CreateBallMeshNode(PieceKind kind, float radius)
    {
        var node = new Node3D { Name = "BallPreview_" + kind };
        var meshInst = new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = radius,
                Height = radius * 2,
                RadialSegments = 16,
                Rings = 8
            }
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = kind == PieceKind.Pollen ? VisualFactory.Gold : (kind == PieceKind.RedNectar ? VisualFactory.Red : VisualFactory.Blue),
            Roughness = 0.4f,
            Metallic = 0.1f
        };
        meshInst.MaterialOverride = mat;
        node.AddChild(meshInst);
        return node;
    }

    public void RefreshBalls()
    {
        if (_ballsContainer == null) return;
        _heldBall = null;
        if (_hoverRing != null) _hoverRing.Visible = false;

        foreach (var item in _ballItems)
        {
            if (IsInstanceValid(item.Mesh))
                item.Mesh.QueueFree();
        }
        _ballItems.Clear();

        if (Game == null) return;

        foreach (var b in Game.Balls)
        {
            if (b == null || !IsInstanceValid(b)) continue;
            if (!Game.IsFloorBall(b)) continue;

            var node = CreateBallMeshNode(b.Kind, b.Radius);
            node.Position = b.Position;
            _ballsContainer.AddChild(node);
            _ballItems.Add(new BallPreviewItem
            {
                Source = b,
                Mesh = node,
                Kind = b.Kind,
                Radius = b.Radius
            });
        }
    }

    public void RefreshInventory()
    {
        if (_inventoryMarker == null) return;
        foreach (var child in _inventoryMarker.GetChildren())
        {
            child.QueueFree();
        }

        if (Game?.Player == null) return;

        Vector3[] offsets = {
            new Vector3(0, 0.08f, 0),
            new Vector3(-0.06f, 0.08f, -0.04f),
            new Vector3(0.06f, 0.08f, -0.04f),
            new Vector3(0, 0.15f, -0.02f)
        };

        for (int i = 0; i < Game.Player.Inventory.Count && i < 4; i++)
        {
            var kind = Game.Player.Inventory[i];
            float r = (kind == PieceKind.Pollen ? 0.03556f : 0.04572f) * 0.85f;
            var ballMesh = CreateBallMeshNode(kind, r);
            ballMesh.Position = offsets[i];
            _inventoryMarker.AddChild(ballMesh);
        }
    }

    private void HandleInput(InputEvent input)
    {
        if (input is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _isPressed = true;
                    _pressScreenPos = mb.Position;

                    if (!RaycastFloor(mb.Position, out var hit)) return;

                    // If we ALREADY hold an object, this click drops / places it!
                    if (_heldBall != null)
                    {
                        DropHeldBall(hit);
                        AcceptEvent();
                        return;
                    }
                    if (_heldRobot)
                    {
                        DropHeldRobot(hit);
                        AcceptEvent();
                        return;
                    }

                    // Nothing held: Click to PICK UP
                    // 1. Check if a floor ball is clicked
                    BallPreviewItem closestBall = null;
                    float minBallDist = 0.24f;
                    foreach (var b in _ballItems)
                    {
                        float d = hit.DistanceTo(b.Mesh.Position);
                        if (d < minBallDist)
                        {
                            minBallDist = d;
                            closestBall = b;
                        }
                    }

                    if (closestBall != null)
                    {
                        _heldBall = closestBall;
                        _heldRobot = false;
                        _justPickedUp = true;
                        _hoverRing.Visible = true;
                        _hoverRing.Scale = Vector3.One;
                        _hoverRing.Position = new Vector3(hit.X, 0.005f, hit.Z);
                        _heldBall.Mesh.Position = new Vector3(hit.X, _heldBall.Radius + 0.05f, hit.Z);
                        OnStatusChanged?.Invoke($"Holding {_heldBall.Kind} — Move freely, click to place");
                        AcceptEvent();
                        return;
                    }

                    // 2. Check if robot is clicked
                    float robotDist = hit.DistanceTo(new Vector3(_robotMarker.Position.X, 0, _robotMarker.Position.Z));
                    if (robotDist < 0.45f)
                    {
                        _heldRobot = true;
                        _heldBall = null;
                        _justPickedUp = true;
                        _hoverRing.Visible = true;
                        _hoverRing.Scale = new Vector3(3.5f, 1f, 3.5f);
                        _hoverRing.Position = new Vector3(_robotMarker.Position.X, 0.005f, _robotMarker.Position.Z);
                        _robotMarker.Position = new Vector3(_robotMarker.Position.X, 0.08f, _robotMarker.Position.Z); // Pop up in air!
                        OnStatusChanged?.Invoke("Holding Robot — Move freely, click to place");
                        AcceptEvent();
                        return;
                    }
                }
                else // Left button released
                {
                    _isPressed = false;
                    if (_justPickedUp)
                    {
                        _justPickedUp = false;
                        return;
                    }

                    // If dragged significantly (> 35 pixels) while holding, drop on release!
                    if (_pressScreenPos.DistanceTo(mb.Position) > 35f)
                    {
                        if (RaycastFloor(mb.Position, out var hit))
                        {
                            if (_heldBall != null)
                            {
                                DropHeldBall(hit);
                                AcceptEvent();
                                return;
                            }
                            if (_heldRobot)
                            {
                                DropHeldRobot(hit);
                                AcceptEvent();
                                return;
                            }
                        }
                    }
                    // Otherwise, user just clicked once: keep it held so they can move freely!
                }
            }
            else if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp)
            {
                _orbitDist = Mathf.Clamp(_orbitDist - 0.3f, 2.2f, 7.5f);
                UpdateCamera();
                AcceptEvent();
            }
            else if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown)
            {
                _orbitDist = Mathf.Clamp(_orbitDist + 0.3f, 2.2f, 7.5f);
                UpdateCamera();
                AcceptEvent();
            }
        }
        else if (input is InputEventMouseMotion motion)
        {
            if (_heldBall != null)
            {
                if (RaycastFloor(motion.Position, out var hit))
                {
                    _heldBall.Mesh.Position = new Vector3(hit.X, _heldBall.Radius + 0.05f, hit.Z);
                    _hoverRing.Position = new Vector3(hit.X, 0.005f, hit.Z);
                    AcceptEvent();
                }
            }
            else if (_heldRobot)
            {
                if (RaycastFloor(motion.Position, out var hit))
                {
                    _robotMarker.Position = new Vector3(hit.X, 0.08f, hit.Z); // Hover smoothly in air without invoking rebuild!
                    _hoverRing.Position = new Vector3(hit.X, 0.005f, hit.Z);
                    AcceptEvent();
                }
            }
            else if ((motion.ButtonMask & (MouseButtonMask.Right | MouseButtonMask.Middle)) != 0)
            {
                _orbitYaw += motion.Relative.X * 0.01f;
                _orbitPitch = Mathf.Clamp(_orbitPitch - motion.Relative.Y * 0.01f, 0.15f, 1.4f);
                UpdateCamera();
                AcceptEvent();
            }
        }
    }

    private void DropHeldBall(Vector3 hit)
    {
        if (_heldBall == null) return;
        var b = _heldBall.Source;
        if (b != null && IsInstanceValid(b))
        {
            b.Position = new Vector3(hit.X, _heldBall.Radius, hit.Z);
            b.LinearVelocity = Vector3.Zero;
            b.AngularVelocity = Vector3.Zero;
        }
        _heldBall.Mesh.Position = new Vector3(hit.X, _heldBall.Radius, hit.Z);
        OnBallMoved?.Invoke(_heldBall.Source, _heldBall.Mesh.Position);
        OnStatusChanged?.Invoke($"Placed {_heldBall.Kind} at X: {hit.X:0.00}m, Z: {hit.Z:0.00}m");
        _heldBall = null;
        if (_hoverRing != null) _hoverRing.Visible = false;
    }

    private void DropHeldRobot(Vector3 hit)
    {
        SetPosition(new Vector3(hit.X, 0f, hit.Z), _robotMarker != null ? _robotMarker.Rotation.Y : 0f);
        _heldRobot = false;
        if (_hoverRing != null) _hoverRing.Visible = false;
        OnPositionDragged?.Invoke(hit);
        OnStatusChanged?.Invoke($"Placed Robot at X: {hit.X:0.00}m, Z: {hit.Z:0.00}m");
    }

    private void UpdateCamera()
    {
        if (_camera == null) return;
        float hDist = _orbitDist * Mathf.Cos(_orbitPitch);
        float y = _orbitDist * Mathf.Sin(_orbitPitch);
        float x = hDist * Mathf.Sin(_orbitYaw);
        float z = hDist * Mathf.Cos(_orbitYaw);

        _camera.Position = _center + new Vector3(x, y, z);
        _camera.LookAt(_center + new Vector3(0, 0.1f, 0));
    }

    public void SetPosition(Vector3 worldPos, float heading)
    {
        if (_robotMarker != null)
        {
            _robotMarker.Position = worldPos;
            _robotMarker.Rotation = new Vector3(0, heading, 0);
        }
    }

    public void UpdateProfile(RobotProfile newProfile)
    {
        Profile = newProfile;
        if (_robotMarker != null)
        {
            if (_robotMesh != null && IsInstanceValid(_robotMesh))
            {
                _robotMesh.QueueFree();
            }
            _robotMesh = RobotParametricPreview.BuildParametricMesh(Profile);
            _robotMarker.AddChild(_robotMesh);
        }
    }

    public override void _Process(double delta)
    {
        if (_viewport != null && IsInstanceValid(_viewport))
        {
            _viewport.RenderTargetUpdateMode = IsVisibleInTree()
                ? SubViewport.UpdateMode.WhenVisible
                : SubViewport.UpdateMode.Disabled;
        }
    }
}
