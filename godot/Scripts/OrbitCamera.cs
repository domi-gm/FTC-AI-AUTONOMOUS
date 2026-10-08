using Godot;

public partial class OrbitCamera : Camera3D
{
    public Simulation Game;
    public int Mode;
    public float Distance = 6.4f;
    private float _yaw = .25f, _pitch = .70f;
    private Vector3 _pan;
    public void FocusPlayer()
    {
        if (Game.Player == null) return;
        Mode = 0; Distance = 1.7f; _yaw = .6f; _pitch = .8f;
        _pan = Game.Player.Position + Vector3.Up * .2f - Arena.World(Arena.Center,Arena.Center,.30f);
    }
    public void ResetView() { Mode=0; Distance=6.4f; _yaw=.25f; _pitch=.70f; _pan=Vector3.Zero; }
    public void Drag(Vector2 relative, bool pan)
    {
        if (Mode!=0)
        {
            var center=Arena.World(Arena.Center,Arena.Center,.30f)+_pan;
            if (Mode==2 && Game.Player!=null)
                center=Game.Player.Position+Vector3.Up*.25f+Game.Player.Front;
            Vector3 offset=Position-center;
            Distance=Mathf.Clamp(offset.Length(),2.5f,12);
            _pan=center-Arena.World(Arena.Center,Arena.Center,.30f);
            _yaw=Mathf.Atan2(offset.X,offset.Z);
            _pitch=Mathf.Asin(Mathf.Clamp(offset.Y/Mathf.Max(offset.Length(),.001f),-1,1));
        }
        Mode=0;
        if (pan)
        {
            Vector3 right=GlobalBasis.X;
            Vector3 up=GlobalBasis.Y;
            _pan+=(-right*relative.X+up*relative.Y)*Distance*.0015f;
            _pan=_pan.LimitLength(Arena.Size*2);
        }
        else { _yaw-=relative.X*.006f; _pitch=Mathf.Clamp(_pitch+relative.Y*.004f,.15f,1.56f); }
    }

    /// <summary>Transformă direcția de pe ecran în deplasare pe podea (X/Z).</summary>
    public Vector3 ToGroundMovement(Vector2 screenInput)
    {
        // Folosim axa dreapta a camerei. Proiecția direcției de privire ar fi
        // zero în vederea de sus, când camera privește vertical în jos.
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
            if (b.ButtonIndex == MouseButton.WheelUp) Distance = Mathf.Max(2.5f, Distance - .3f);
            if (b.ButtonIndex == MouseButton.WheelDown) Distance = Mathf.Min(12, Distance + .3f);
        }
        if (e is InputEventMouseMotion m && (m.ButtonMask & (MouseButtonMask.Middle|MouseButtonMask.Right)) != 0)
            Drag(m.Relative, (m.ButtonMask & MouseButtonMask.Middle)!=0 && m.ShiftPressed);
    }
    public override void _Process(double delta)
    {
        var target = Arena.World(Arena.Center, Arena.Center, .30f) + _pan;
        Projection = Mode == 1 ? ProjectionType.Orthogonal : ProjectionType.Perspective;
        if (Mode == 1)
        {
            Size = Distance * .85f; Position = target + Vector3.Up * 8;
            LookAt(target, Vector3.Forward); return;
        }
        if (Mode == 2 && Game.Player != null)
        {
            target = Game.Player.Position + Vector3.Up * .25f;
            Position = target - Game.Player.Front * 1.4f + Vector3.Up * .9f;
            LookAt(target + Game.Player.Front); return;
        }
        Position = target + new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(_pitch), Mathf.Sin(_pitch), Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * Distance;
        LookAt(target);
    }
}
