using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>Follower geometric simplu; nu este implementarea Java Pedro Pathing.</summary>
public partial class PracticePath : Node3D
{
    public Simulation Game;
    public readonly List<Vector3> Points = new();
    public bool Editing, Following;
    public int Current;
    public override void _UnhandledInput(InputEvent e)
    {
        if (!Game.Practice || !Game.Running) return;
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.PhysicalKeycode == Key.F2) { Editing = !Editing; Following = false; Game.Status = "Path editor: left click adds, Backspace removes, P follows"; }
            if (k.PhysicalKeycode == Key.P && Points.Count > 0) { Following = !Following; Current = 0; }
            if (k.PhysicalKeycode == Key.Backspace && Editing && Points.Count > 0) { Points.RemoveAt(Points.Count - 1); Rebuild(); }
        }
        if (Editing && e is InputEventMouseButton b && b.Pressed && b.ButtonIndex == MouseButton.Left)
        {
            var camera = Game.Camera; var from = camera.ProjectRayOrigin(b.Position); var direction = camera.ProjectRayNormal(b.Position);
            if (Mathf.Abs(direction.Y) < .001f) return;
            float distance = -from.Y / direction.Y; if (distance < 0) return;
            var point = from + direction * distance;
            if (point.X < .23f || point.X > Arena.Size - .23f || point.Z > -.23f || point.Z < -Arena.Size + .23f) return;
            point.Y = 0; Points.Add(point); Rebuild();
        }
    }
    public Vector3 Command()
    {
        if (!Following || Current >= Points.Count) { Following = false; return Vector3.Zero; }
        var difference = Points[Current] - Game.Player.Position; difference.Y = 0;
        if (difference.Length() < .035f) { Current++; return Command(); }
        return difference.Normalized() * Mathf.Min(1, difference.Length() * 4);
    }
    private void Rebuild()
    {
        foreach (var n in GetChildren()) { RemoveChild(n); n.QueueFree(); }
        for (int i = 0; i < Points.Count; i++)
        {
            VisualFactory.Cylinder(this, "Waypoint", Points[i] + Vector3.Up * .01f, .035f, .01f, VisualFactory.Gold);
            if (i > 0) VisualFactory.Beam(this, "PathSegment", Points[i - 1] + Vector3.Up * .02f, Points[i] + Vector3.Up * .02f, .012f, VisualFactory.Gold);
        }
    }
    public void Save()
    {
        using var file = FileAccess.Open("user://path.json", FileAccess.ModeFlags.Write);
        file.StoreString(JsonSerializer.Serialize(Points.Select(p => new[] { p.X * 100, -p.Z * 100 }).ToArray()));
        Game.Status = "Path saved in centimetres";
    }
    public void Load()
    {
        try
        {
            if (!FileAccess.FileExists("user://path.json")) return;
            var data = JsonSerializer.Deserialize<float[][]>(FileAccess.GetFileAsString("user://path.json"));
            if (data == null || data.Length > 1000) return;
            var valid = data.All(p => p?.Length == 2 && float.IsFinite(p[0] + p[1]) && p[0] >= 0 && p[0] <= Arena.Size * 100 && p[1] >= 0 && p[1] <= Arena.Size * 100);
            if (!valid) { Game.Status = "Invalid path"; return; }
            Points.Clear(); foreach (var p in data) Points.Add(Field.ToGodot(p[0], p[1]));
            Following = false; Rebuild(); Game.Status = "Path loaded";
        }
        catch (System.Exception ex) { Game.Status = "Path: " + ex.Message; }
    }
}
