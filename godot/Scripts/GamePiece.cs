using Godot;

public enum PieceKind { Pollen, RedNectar, BlueNectar }
public partial class GamePiece : RigidBody3D
{
    public PieceKind Kind;
    public bool Stored;
    public int ShotRobotIndex=-1;
    public bool ShotConfirmed;
    public float Radius => Kind == PieceKind.Pollen ? .03556f : .04572f;
    private static Shader _shader;
    private readonly System.Collections.Generic.List<PhysicsBody3D> _launchExceptions = new();
    private float _launchGrace;
    public void IgnoreLauncherBriefly(RobotAgent robot)
    {
        _launchGrace=.2f;
        _launchExceptions.Add(robot); AddCollisionExceptionWith(robot);
        if (robot.Rig != null) foreach (var link in robot.Rig.Links.Values)
        { _launchExceptions.Add(link); AddCollisionExceptionWith(link); }
    }
    public override void _PhysicsProcess(double delta)
    {
        if (_launchExceptions.Count==0 || Freeze) return;
        _launchGrace-=(float)delta;
        if (_launchGrace>0) return;
        foreach (var body in _launchExceptions)
            if (GodotObject.IsInstanceValid(body)) RemoveCollisionExceptionWith(body);
        _launchExceptions.Clear();
    }
    public override void _Ready()
    {
        // La referință masele sunt în livre; Godot folosește kg.
        Mass = (Kind == PieceKind.Pollen ? .055f : .091f) * .45359237f;
        ContinuousCd = true;
        LinearDampMode=DampMode.Replace; LinearDamp=0;
        AngularDampMode=DampMode.Replace; AngularDamp=0;
        float inertia=PieceContactModel.InertiaFactor*Mass*Radius*Radius;
        Inertia=Vector3.One*inertia;
        MaxContactsReported=8;
        PhysicsMaterialOverride=PieceContactModel.BallMaterial();
        CollisionLayer = 2; CollisionMask = 1 | 2 | 4;
        AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = Radius } });
        _shader ??= new Shader { Code = @"
shader_type spatial;
uniform vec4 ball_color : source_color;
void fragment() {
    vec2 tile = vec2(UV.x * 10.0 + step(0.5, fract(UV.y * 6.0)) * 0.5, UV.y * 12.0);
    float hole = 1.0 - smoothstep(0.18, 0.26, length(fract(tile) - vec2(0.5)));
    ALBEDO = mix(ball_color.rgb, ball_color.rgb * 0.13, hole);
    ROUGHNESS = 0.8;
}" };
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("ball_color", Kind == PieceKind.Pollen ? VisualFactory.Gold : Kind == PieceKind.RedNectar ? VisualFactory.Red : VisualFactory.Blue);
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = Radius, Height = Radius * 2, RadialSegments = 24, Rings = 12 }, MaterialOverride = material });
    }
    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (Stored || Freeze) return;
        for (int i=0;i<state.GetContactCount();i++)
        {
            // Frânăm numai rostogolirea pe o suprafață de sprijin statică.
            // Contactele cu alte mingi sunt rezolvate exclusiv de motor.
            var collider=state.GetContactColliderObject(i);
            if (collider is not StaticBody3D || collider is AnimatableBody3D) continue;
            // Jolt raportează această normală în coordonatele lumii, chiar dacă
            // numele API conține Local; nu o rotim din nou odată cu mingea.
            Vector3 normal=state.GetContactLocalNormal(i).Normalized();
            if (normal.Dot(Vector3.Up)<.7f) continue;
            float inertia=PieceContactModel.InertiaFactor*Mass*Radius*Radius;
            float twist=state.AngularVelocity.Dot(normal);
            if (Mathf.Abs(twist)>.01f)
                state.ApplyTorque(-normal*Mathf.Sign(twist)*inertia*Mathf.Min(PieceContactModel.SpinDeceleration,Mathf.Abs(twist)/(float)state.Step));
            Vector3 tangent=state.LinearVelocity-normal*state.LinearVelocity.Dot(normal);
            Vector3 spin=state.AngularVelocity-normal*state.AngularVelocity.Dot(normal);
            if (tangent.Length()<.005f || spin.Length()<.01f) break;
            Vector3 slip=tangent+state.AngularVelocity.Cross(-normal*Radius);
            if (slip.Length()>Mathf.Max(.02f,tangent.Length()*.15f)) break;
            float torque=Mass*Radius*(1+PieceContactModel.InertiaFactor)*PieceContactModel.RollingDeceleration;
            // Limităm impulsul ca frâna să nu inverseze singură rotația.
            torque=Mathf.Min(torque,inertia*spin.Length()/(float)state.Step);
            state.ApplyTorque(-spin.Normalized()*torque);
            break;
        }
    }
}
