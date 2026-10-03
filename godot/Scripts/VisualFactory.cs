using Godot;

public static class VisualFactory
{
    public static readonly Color Red = new("e0453a"), Blue = new("3b7de0"), Gold = new("f2c230"), Steel = new("71818c");
    public static StandardMaterial3D Material(Color color, bool unshaded = false) => new()
    {
        AlbedoColor = color, Roughness = 0.65f,
        ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
        Transparency = color.A < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };
    public static MeshInstance3D Mesh(Node3D parent, string name, Mesh mesh, Vector3 at, Color color)
    {
        var node = new MeshInstance3D { Name = name, Mesh = mesh, Position = at, MaterialOverride = Material(color) };
        parent.AddChild(node); return node;
    }
    public static MeshInstance3D Box(Node3D parent, string name, Vector3 at, Vector3 size, Color color, bool solid = false)
    {
        var node = Mesh(parent, name, new BoxMesh { Size = size }, at, color);
        if (solid) { var body = new StaticBody3D(); node.AddChild(body); body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } }); }
        return node;
    }
    public static MeshInstance3D Beam(Node3D parent, string name, Vector3 a, Vector3 b, float width, Color color, bool solid = false)
    {
        var node = Box(parent, name, (a + b) / 2, new(width, width, a.DistanceTo(b)), color, solid);
        var direction = (b - a).Normalized();
        node.LookAt(parent.ToGlobal(b), Mathf.Abs(direction.Dot(Vector3.Up)) > .99f ? Vector3.Right : Vector3.Up);
        return node;
    }
    public static MeshInstance3D Cylinder(Node3D parent, string name, Vector3 at, float radius, float height, Color color)
        => Mesh(parent, name, new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 24 }, at, color);
    public static MeshInstance3D Panel(Node3D parent, string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, bool collision = false)
    {
        var surface = new SurfaceTool(); surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
        Vector3[] points = { a, b, c, a, c, d };
        Vector2[] uv = { new(0, 1), new(1, 1), new(1, 0), new(0, 1), new(1, 0), new(0, 0) };
        for (int i = 0; i < 6; i++) { surface.SetUV(uv[i]); surface.AddVertex(points[i]); }
        surface.GenerateNormals();
        var node = Mesh(parent, name, surface.Commit(), Vector3.Zero, color);
        if (collision)
        {
            Vector3 normal = (b - a).Cross(c - a).Normalized() * .003f;
            var body = new AnimatableBody3D { SyncToPhysics = false };
            node.AddChild(body); body.AddChild(new CollisionShape3D { Shape = new ConvexPolygonShape3D
            { Points = new[] { a + normal, b + normal, c + normal, d + normal, a - normal, b - normal, c - normal, d - normal } } });
        }
        return node;
    }
    public static Label3D Label(Node3D parent, string name, string text, Vector3 at, Color color, int size = 32)
    {
        var label = new Label3D { Name = name, Text = text, Position = at, Modulate = color, FontSize = size, PixelSize = .0015f, OutlineSize = 3 };
        parent.AddChild(label); return label;
    }
    public static Vector3 Inch(float x, float y, float z = 0) => new(x * .0254f, z * .0254f, -y * .0254f);
}
