using Godot;
using System.Globalization;

/// <summary>Doar desen: fără corpuri fizice sau modificări ale coordonatelor.</summary>
public partial class FieldVisuals : Node3D
{
    private const int Tiles = 6;
    private static readonly Color GridColor = new("101820");
    private static readonly Color TextColor = new("aab8c5");
    private static readonly Color XColor = new("ff6b75");
    private static readonly Color YColor = new("5ddcc1");

    public override void _Ready()
    {
        var floor = GetParent().GetNode<MeshInstance3D>("Floor");
        floor.MaterialOverride = Material(new Color("29333e"));

        // Rosturile sunt dreptunghiuri subțiri, la 2 mm deasupra podelei.
        // La podea rămâne un singur plan; liniile nu au coliziuni.
        for (int i = 0; i <= Tiles; i++)
        {
            float x = Field.WidthCm * i / Tiles;
            float y = Field.HeightCm * i / Tiles;
            // 0.45 cm devenea sub un pixel: liniile centrale dispăreau la rasterizare.
            // Aceeași grosime pentru contur și interior păstrează celulele uniforme.
            float thickness = 1.0f;
            Line($"Vertical_{i}", new(x, 0), new(x, Field.HeightCm), thickness, GridColor);
            Line($"Horizontal_{i}", new(0, y), new(Field.WidthCm, y), thickness, GridColor);
            if (i > 0)
            {
                Text($"X_{i}", Format(x), new(x, -9), TextColor, 20);
                Text($"Y_{i}", Format(y), new(-13, y), TextColor, 20);
            }
        }

        Arrow("AxisX", new(0, 0), new(42, 0), XColor);
        Arrow("AxisY", new(0, 0), new(0, 42), YColor);
        Text("OriginLabel", "(0, 0)", new(-2, -9), Colors.White, 24);
        Text("XLabel", "+X", new(44, -9), XColor, 24);
        Text("YLabel", "+Y", new(-10, 43), YColor, 24);
        Text("Title", "FTC  /  FIELD + VISUALS", new(Field.WidthCm / 2, Field.HeightCm + 20), Colors.White, 30);
        Text("Dimensions", $"{Format(Field.WidthCm)} x {Format(Field.HeightCm)} cm   |   6 x 6 dale",
            new(Field.WidthCm / 2, -24), TextColor, 22);

        var origin = new MeshInstance3D
        {
            Name = "Origin",
            Mesh = new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.018f, Height = 0.003f },
            Position = Field.ToGodot(0, 0, 0.7f),
            MaterialOverride = Material(Colors.White),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(origin);
    }

    private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static StandardMaterial3D Material(Color color) => new()
    {
        AlbedoColor = color,
        // Culorile reperelor rămân lizibile indiferent de lumina scenei.
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };

    private void Line(string name, Vector2 from, Vector2 to, float widthCm, Color color, float heightCm = 0.2f)
    {
        Vector3 start = Field.ToGodot(from.X, from.Y, heightCm);
        Vector3 end = Field.ToGodot(to.X, to.Y, heightCm);
        var line = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = new Vector3(widthCm / 100, 0.001f, start.DistanceTo(end)) },
            Position = (start + end) / 2,
            MaterialOverride = Material(color),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(line);
        // Orientăm axa lungă a dreptunghiului către capătul segmentului.
        line.LookAt(ToGlobal(end), Vector3.Up);
    }

    private void Arrow(string name, Vector2 from, Vector2 to, Color color)
    {
        Vector2 direction = (to - from).Normalized();
        Vector2 side = new(-direction.Y, direction.X);
        Line(name, from, to, 1.1f, color, 0.4f);
        Line(name + "Left", to, to - direction * 5 + side * 3, 1.1f, color, 0.4f);
        Line(name + "Right", to, to - direction * 5 - side * 3, 1.1f, color, 0.4f);
    }

    private void Text(string name, string text, Vector2 positionCm, Color color, int fontSize)
    {
        AddChild(new Label3D
        {
            Name = name,
            Text = text,
            Position = Field.ToGodot(positionCm.X, positionCm.Y, 0.8f),
            RotationDegrees = new Vector3(-90, 0, 0),
            FontSize = fontSize,
            PixelSize = 0.0025f,
            Modulate = color,
            OutlineSize = 0,
            NoDepthTest = false
        });
    }
}
