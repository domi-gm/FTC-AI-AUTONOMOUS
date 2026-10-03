using Godot;
using System;
using System.Text.Json;

public sealed class RobotProfile
{
    public string Name { get; set; } = "SWYFT prototype";
    public float WidthCm { get; set; } = 45.72f;
    public float LengthCm { get; set; } = 45.72f;
    public float Speed { get; set; } = 1.5f;
    public float Acceleration { get; set; } = 3;
    public float TurnSpeed { get; set; } = 2.8f;
    public int Turrets { get; set; } = 1;
    public int Intakes { get; set; } = 1;
    public void Validate()
    {
        if (!float.IsFinite(WidthCm + LengthCm + Speed + Acceleration + TurnSpeed)) throw new ArgumentException("Non-finite robot parameters");
        WidthCm = Mathf.Clamp(WidthCm, 25, 45.72f); LengthCm = Mathf.Clamp(LengthCm, 25, 45.72f);
        Speed = Mathf.Clamp(Speed, .3f, 3); Acceleration = Mathf.Clamp(Acceleration, .5f, 8); TurnSpeed = Mathf.Clamp(TurnSpeed, .5f, 6);
        Turrets = Math.Clamp(Turrets, 1, 3); Intakes = Math.Clamp(Intakes, 1, 2);
    }
    public static RobotProfile Load()
    {
        try { if (FileAccess.FileExists("user://robot.json")) { var p = JsonSerializer.Deserialize<RobotProfile>(FileAccess.GetFileAsString("user://robot.json")); p.Validate(); return p; } }
        catch (Exception e) { GD.PushWarning("Robot profile: " + e.Message); }
        return new();
    }
    public void Save()
    {
        Validate(); using var file = FileAccess.Open("user://robot.json", FileAccess.ModeFlags.Write); file.StoreString(JsonSerializer.Serialize(this));
    }
}
