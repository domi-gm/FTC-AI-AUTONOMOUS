using Godot;

public partial class Field : Node3D
{
	public const float WidthCm = 359.41f;
	public const float HeightCm = 359.41f;

	public static Vector3 ToGodot(float xCm, float yCm, float heightCm = 0f)
	{
		return new Vector3(
			xCm / 100f,
			heightCm / 100f,
			-yCm / 100f
		);
	}

	public static Vector2 ToFieldCm(Vector3 position)
	{
		return new Vector2(
			position.X * 100f,
			-position.Z * 100f
		);
	}

public override void _Ready()
{
	MeshInstance3D floor = GetNode<MeshInstance3D>("Floor");

	PlaneMesh plane = new PlaneMesh
	{
		Size = new Vector2(WidthCm / 100f, HeightCm / 100f)
	};

	floor.Mesh = plane;
	floor.Scale = Vector3.One;
	floor.Position = ToGodot(WidthCm / 2f, HeightCm / 2f);

	// Desenul folosește aceleași dimensiuni și conversii ca terenul.
	AddChild(new FieldVisuals { Name = "Visuals" });

	GD.Print($"Teren: {WidthCm} × {HeightCm} cm");
	GD.Print($"Centrul podelei: {floor.Position}");
}
}
