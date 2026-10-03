using Godot;

public enum PieceKind { Pollen, RedNectar, BlueNectar }
public partial class GamePiece : RigidBody3D
{
    public PieceKind Kind;
    public bool Stored;
    public float Radius => Kind == PieceKind.Pollen ? .03556f : .04572f;
    private static Shader _shader;
    public override void _Ready()
    {
        // La referință masele sunt în livre; Godot folosește kg.
        Mass = (Kind == PieceKind.Pollen ? .055f : .091f) * .45359237f;
        ContinuousCd = true;
        LinearDampMode = DampMode.Replace; LinearDamp = 0; AngularDamp = .25f;
        PhysicsMaterialOverride = new PhysicsMaterial { Bounce = .28f, Friction = .35f };
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
}
