using Godot;

public partial class OrbitCamera : Camera3D
{
    public Simulation Game;
    public int Mode;
    public float Distance = 6.4f;
    private float _targetDistance = 6.4f;
    private float _yaw = .25f, _pitch = .70f;
    private float _targetYaw = .25f, _targetPitch = .70f;
    private Vector3 _pan = Vector3.Zero;
    private Vector3 _targetPan = Vector3.Zero;

    // Smoothed Mode 2 (Behind-the-robot) follow state
    private Vector3 _smoothedCamPos = Vector3.Zero;
    private Vector3 _smoothedCamTarget = Vector3.Zero;

    public void FocusPlayer()
    {
        if (Game.Player == null) return;
        Mode = 0;
        Distance = 1.7f; _targetDistance = 1.7f;
        _yaw = .6f; _targetYaw = .6f;
        _pitch = .8f; _targetPitch = .8f;
        _pan = Game.Player.Position + Vector3.Up * .2f - Arena.World(Arena.Center, Arena.Center, .30f);
        _targetPan = _pan;
    }

    public void ResetView()
    {
        Mode = 0;
        Distance = 6.4f; _targetDistance = 6.4f;
        _yaw = .25f; _targetYaw = .25f;
        _pitch = .70f; _targetPitch = .70f;
        _pan = Vector3.Zero; _targetPan = Vector3.Zero;
    }

    public void Drag(Vector2 relative, bool pan)
    {
        if (Mode != 0)
        {
            var center = Arena.World(Arena.Center, Arena.Center, .30f) + _pan;
            if (Mode == 2 && Game.Player != null)
                center = Game.Player.Position + Vector3.Up * .25f + Game.Player.Front;
            Vector3 offset = Position - center;
            Distance = Mathf.Clamp(offset.Length(), 2.0f, 13.0f);
            _targetDistance = Distance;
            _pan = center - Arena.World(Arena.Center, Arena.Center, .30f);
            _targetPan = _pan;
            _yaw = Mathf.Atan2(offset.X, offset.Z);
            _targetYaw = _yaw;
            _pitch = Mathf.Asin(Mathf.Clamp(offset.Y / Mathf.Max(offset.Length(), .001f), -1, 1));
            _targetPitch = _pitch;
        }
        Mode = 0;
        if (pan)
        {
            Vector3 right = GlobalBasis.X;
            Vector3 up = GlobalBasis.Y;
            Vector3 deltaPan = (-right * relative.X + up * relative.Y) * Distance * .0015f;
            _targetPan += deltaPan;
            _targetPan = _targetPan.LimitLength(Arena.Size * 2);
            _pan = _targetPan;
        }
        else
        {
            _targetYaw -= relative.X * .006f;
            _targetPitch = Mathf.Clamp(_targetPitch + relative.Y * .004f, .15f, 1.56f);
            _yaw = _targetYaw;
            _pitch = _targetPitch;
        }
    }

    /// <summary>Transformă direcția de pe ecran în deplasare pe podea (X/Z).</summary>
    public Vector3 ToGroundMovement(Vector2 screenInput)
    {
        Vector3 right = GlobalBasis.X;
        right.Y = 0;
        right = right.LengthSquared() > .000001f ? right.Normalized() : Vector3.Right;
        Vector3 forward = Vector3.Up.Cross(right);
        return (right * screenInput.X - forward * screenInput.Y).LimitLength();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton b && b.Pressed)
        {
            if (b.ButtonIndex == MouseButton.WheelUp)
            {
                _targetDistance = Mathf.Max(1.6f, _targetDistance * 0.88f);
            }
            if (b.ButtonIndex == MouseButton.WheelDown)
            {
                _targetDistance = Mathf.Min(13.5f, _targetDistance * 1.14f);
            }
        }
        if (e is InputEventMouseMotion m && (m.ButtonMask & (MouseButtonMask.Middle | MouseButtonMask.Right)) != 0)
        {
            Drag(m.Relative, (m.ButtonMask & MouseButtonMask.Middle) != 0 && m.ShiftPressed);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float smooth = 1.0f - Mathf.Exp(-20.0f * dt);
        Distance = Mathf.Lerp(Distance, _targetDistance, smooth);
        _yaw = Mathf.Lerp(_yaw, _targetYaw, smooth);
        _pitch = Mathf.Lerp(_pitch, _targetPitch, smooth);
        _pan = _pan.Lerp(_targetPan, smooth);

        var target = Arena.World(Arena.Center, Arena.Center, .30f) + _pan;
        Projection = Mode == 1 ? ProjectionType.Orthogonal : ProjectionType.Perspective;
        if (Mode == 1)
        {
            Size = Distance * .85f;
            Position = target + Vector3.Up * 8;
            LookAt(target, Vector3.Forward);
            _smoothedCamPos = Vector3.Zero;
            return;
        }
        if (Mode == 2 && Game.Player != null)
        {
            var pTarget = Game.Player.Position + Vector3.Up * .25f;
            var desiredPos = pTarget - Game.Player.Front * 1.4f + Vector3.Up * .9f;
            var desiredLook = pTarget + Game.Player.Front;

            if (_smoothedCamPos == Vector3.Zero || _smoothedCamPos.DistanceSquaredTo(desiredPos) > 16f)
            {
                _smoothedCamPos = desiredPos;
                _smoothedCamTarget = desiredLook;
            }
            else
            {
                float followSmooth = 1.0f - Mathf.Exp(-14.0f * dt);
                _smoothedCamPos = _smoothedCamPos.Lerp(desiredPos, followSmooth);
                _smoothedCamTarget = _smoothedCamTarget.Lerp(desiredLook, followSmooth);
            }

            Position = _smoothedCamPos;
            LookAt(_smoothedCamTarget);
            return;
        }

        _smoothedCamPos = Vector3.Zero;
        Position = target + new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(_pitch), Mathf.Sin(_pitch), Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * Distance;
        LookAt(target);
    }
}
