using Godot;
using static VisualFactory;

public partial class Arena : Node3D
{
    // Dimensiuni din clientul Turtle Sim; nu reprezintă CAD oficial validat.
    public const float Inches = 141.35f;
    public const float Size = Inches * .0254f;
    public const float Center = Size / 2;
    public static Vector3 World(float x, float y, float z = 0) => new(x, z, -y);
    public override void _Ready()
    {
        Box(this, "Base", World(Center, Center, -.06f), new(Size + .13f, .12f, Size + .13f), new("141820"), true, PieceContactModel.FloorMaterial());
        for (int x = 0; x < 6; x++) for (int y = 0; y < 6; y++)
        {
            float tile = Size / 6;
            Box(this, $"Tile_{x}_{y}", World((x + .5f) * tile, (y + .5f) * tile, .001f), new(tile - .003f, .002f, tile - .003f),
                (x + y) % 2 == 0 ? new("42454b") : new("404349"));
        }
        for (int side = 0; side < 4; side++)
        {
            bool horizontal = side < 2;
            float edge = side % 2 == 0 ? -.0127f : Size + .0127f;
            var center = horizontal ? World(Center, edge, .145f) : World(edge, Center, .145f);
            var dimensions = horizontal ? new Vector3(Size + .05f, .29f, .0254f) : new Vector3(.0254f, .29f, Size + .05f);
            Box(this, "Wall", center, dimensions, new Color(.5f, .68f, .8f, .13f), true);
            Box(this, "WallRail", center + new Vector3(0, .15f, 0), horizontal ? new(Size + .08f, .025f, .025f) : new(.025f, .025f, Size + .08f), Steel);
            for (int i = 0; i <= 6; i++) Box(this, "WallPost", horizontal ? World(i * Size / 6, edge, .15f) : World(edge, i * Size / 6, .15f), new(.024f, .30f, .024f), Steel);
        }
        Zone(0, Size * 4 / 6, .2794f, Size / 6, Red);
        Zone(Size - .2794f, Size / 6, .2794f, Size / 6, Blue);
        Box(this, "RedGarden", World(Size / 12, .0254f, .005f), new(Size / 6, .004f, .0508f), Red);
        Box(this, "BlueGarden", World(Size * 11 / 12, Size - .0254f, .005f), new(Size / 6, .004f, .0508f), Blue);
        float halfW = 24.73f * .0254f, halfD = 19.475f * .0254f;
        foreach (int sign in new[] { -1, 1 })
        {
            float x = Center + sign * halfW;
            Box(this, "FrameFoot", World(x, Center, .013f), new(.0254f, .026f, halfD * 2), Steel, true);
            foreach (int end in new[] { -1, 1 })
                Beam(this, "FrameBrace", World(x, Center + end * halfD, .025f), World(x, Center + end * .03f, 1.1f), .026f, Steel, true);
        }
        Beam(this, "Axle", World(Center - halfW, Center, 1.13f), World(Center + halfW, Center, 1.13f), .04f, Steel, true);
        foreach (int sign in new[] { -1, 1 })
            Box(this, "BIOBUZZ_Banner", World(Center, Center + sign * .061f, 1.0f), new(1.16f, .16f, .006f), Gold);
        var text = Label(this, "BIOBUZZ", "BIOBUZZ", World(Center, Center - .065f, 1f), new("17191e"), 56);
        text.PixelSize = .002f;
        BuildFlower(3.9f, Inches * 2 / 6, Red);
        BuildFlower(Inches * 2 / 6, Inches - 3.9f, Red);
        BuildFlower(Inches - 3.9f, Inches * 4 / 6, Blue);
        BuildFlower(Inches * 4 / 6, 3.9f, Blue);
        // Platformele exterioare sunt doar decor; robotul rămâne în teren.
        foreach (bool red in new[] { true, false })
        {
            float x = red ? -.75f : Size + .75f;
            var color = red ? Red : Blue;
            Box(this, "AlliancePlatform", World(x, Center, -.04f), new(1.4f, .04f, 2.5f), new Color(color, .22f));
            for (int i = 0; i < 2; i++)
            {
                float stationY = Center + (i == 0 ? -.7f : .7f);
                Box(this, "DriverDesk", World(x, stationY, .78f), new(.55f, .035f, .42f), new("202a38"));
                foreach (int leg in new[] {-1,1})
                    Box(this,"DeskLeg",World(x+leg*.21f,stationY,.38f),new(.025f,.76f,.025f),Steel);
                var screen = Box(this, "DriverScreen", World(x, Center + (i == 0 ? -.7f : .7f), .95f), new(.3f, .18f, .025f), color);
                screen.RotationDegrees = new(-15, 0, 0);
            }
        }
        foreach (bool red in new[] {true,false})
        {
            float tableX = red ? -.325f : Size+.325f;
            float tableY = red ? Size*.75f-.048f : Size*.25f-.048f;
            Box(this,"HumanPlayerTable",World(tableX,tableY,.69f),new(.4f,.035f,.34f),new("283343"));
            foreach (int sign in new[] {-1,1})
                Box(this,"TableLeg",World(tableX+sign*.15f,tableY,.34f),new(.025f,.68f,.025f),Steel);
        }
        var skyShader = new Shader { Code = @"
shader_type sky;
void sky() {
    vec2 uv = vec2(atan(EYEDIR.z, EYEDIR.x), asin(clamp(EYEDIR.y,-1.0,1.0))) * 110.0;
    vec2 cell = floor(uv);
    float seed = fract(sin(dot(cell,vec2(127.1,311.7)))*43758.5453);
    vec2 offset = vec2(seed, fract(seed*17.13))*.7+.15;
    float d = length(fract(uv)-offset);
    float star = (1.0-smoothstep(.015,.07,d))*step(.97,seed);
    COLOR = vec3(.028,.042,.068) + vec3(.55,.64,.8)*star;
}" };
        var environment = new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ShaderMaterial { Shader=skyShader } },
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new("b6c8df"), AmbientLightEnergy = .55f,
            TonemapMode = Godot.Environment.ToneMapper.Linear
        } };
        AddChild(environment);
        AddChild(new DirectionalLight3D { RotationDegrees = new(-65, -25, 0), LightEnergy = .8f,
            ShadowEnabled = true, DirectionalShadowMaxDistance = 12, ShadowBlur = 1.5f });
    }
    private void Zone(float x, float y, float width, float length, Color color)
    {
        var a = World(x, y, .006f); var b = World(x + width, y, .006f); var c = World(x + width, y + length, .006f); var d = World(x, y + length, .006f);
        Beam(this, "Zone", a, b, .012f, color); Beam(this, "Zone", b, c, .012f, color); Beam(this, "Zone", c, d, .012f, color); Beam(this, "Zone", d, a, .012f, color);
    }
    private void BuildFlower(float x, float y, Color color)
    {
        Vector3 p = Inch(x, y);
        Cylinder(this, "FlowerBase", p + Vector3.Up * .015f, .08f, .03f, Steel);
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.Tau / 4;
            Box(this, "FlowerStem", p + new Vector3(Mathf.Cos(angle) * .065f, .27f, Mathf.Sin(angle) * .065f), new(.012f, .54f, .012f), new("49ad58"));
        }
        foreach (float height in new[] { .10f, .53f })
            Mesh(this, "FlowerRing", new TorusMesh { InnerRadius = .051f, OuterRadius = .079f }, p + Vector3.Up * height, Gold);
        for (int i = 0; i < 24; i++)
        {
            float angle = i * Mathf.Tau / 24;
            var wall = Box(this, "FlowerTube", p + new Vector3(Mathf.Cos(angle) * .056f, .28f, Mathf.Sin(angle) * .056f),
                new(.006f, .53f, .015f), new Color(.78f,.87f,.9f,.12f), true);
            wall.Rotation = new(0,-angle,0);
        }
        Box(this,"FlowerSupport",p + Vector3.Up * .004f,new(.11f,.008f,.11f),Steel,true);
    }
}
