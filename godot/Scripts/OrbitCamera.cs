using Godot;

public partial class OrbitCamera : Camera3D
{
    public Simulation Game;
    public int Mode;
    public float Distance = 6.4f;
    private float _yaw = .25f, _pitch = .70f;

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
        if (e is InputEventMouseMotion m && Input.IsMouseButtonPressed(MouseButton.Right))
        { _yaw -= m.Relative.X * .006f; _pitch = Mathf.Clamp(_pitch + m.Relative.Y * .004f, .15f, 1.45f); }
    }
    public override void _Process(double delta)
    {
        var target = Arena.World(Arena.Center, Arena.Center, .30f);
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
