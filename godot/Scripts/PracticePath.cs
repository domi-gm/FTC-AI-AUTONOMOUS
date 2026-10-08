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
    private Vector3 _previousPosition;
    private float _blockedTime;
    public override void _PhysicsProcess(double delta)
    {
        if (!Following || !Game.Running) { _blockedTime=0; _previousPosition=Game.Player.Position; return; }
        if (Current<Points.Count && Points[Current].DistanceTo(Game.Player.Position)>.05f
            && _previousPosition.DistanceTo(Game.Player.Position)<.06f*(float)delta) _blockedTime+=(float)delta;
        else _blockedTime=0;
        _previousPosition=Game.Player.Position;
        if (_blockedTime>2) { Following=false; Game.Player.Command=Vector3.Zero; Game.Status="Path stopped: blocked for 2 seconds; move the waypoint"; }
    }
    public bool IsValidPoint(Vector3 point)
    {
        if (!float.IsFinite(point.X+point.Y+point.Z) || point.X<0 || point.X>Arena.Size || point.Z>0 || point.Z < -Arena.Size) return false;
        var robot=Game.Player;
        using var shape=new BoxShape3D { Size=robot.Imported == null ? new Vector3(robot.Width+.006f,.28f,robot.Length+.006f) : robot.CollisionSize + new Vector3(.006f,0,.006f) };
        using var query=new PhysicsShapeQueryParameters3D { Shape=shape, CollisionMask=1,
            Transform=new Transform3D(robot.GlobalBasis,new Vector3(point.X,0,point.Z) + robot.GlobalBasis * robot.CollisionCenter) };
        return GetWorld3D().DirectSpaceState.IntersectShape(query,1).Count==0;
    }
    public bool ReplacePoints(float[][] data)
    {
        if (data==null || data.Length>1000 || !data.All(p=>p?.Length==2 && IsValidPoint(Field.ToGodot(p[0],p[1]))))
        { Game.Status="Invalid path: a waypoint overlaps an obstacle or field wall"; return false; }
        Points.Clear(); foreach (var point in data) Points.Add(Field.ToGodot(point[0],point[1]));
        Following=false; Current=0; _blockedTime=0; Rebuild(); return true;
    }
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
            if (Points.Count>=1000 || !IsValidPoint(point)) { Game.Status="Waypoint rejected: robot overlaps an obstacle or wall"; return; }
            point.Y = 0; Points.Add(point); Rebuild();
        }
    }
    public Vector3 Command()
    {
        if (!Following || Current >= Points.Count) { Following = false; return Vector3.Zero; }
        while (Current<Points.Count)
        {
            var difference=Points[Current]-Game.Player.Position; difference.Y=0;
            if (difference.Length()>=.035f) return difference.Normalized()*Mathf.Min(1,difference.Length()*4);
            Current++;
        }
        Following=false; return Vector3.Zero;
    }
    public void ClearPath()
    {
        Points.Clear();
        Following = false;
        Current = 0;
        Rebuild();
        if (Game != null) Game.Status = "Path cleared";
    }
    public void Rebuild()
    {
        foreach (var n in GetChildren()) { RemoveChild(n); n.QueueFree(); }
        for (int i = 0; i < Points.Count; i++)
        {
            VisualFactory.Cylinder(this, "Waypoint", Points[i] + Vector3.Up * .01f, .035f, .01f, VisualFactory.Gold);
            if (i>0 && Points[i-1].DistanceSquaredTo(Points[i])>.0000001f)
                VisualFactory.Beam(this,"PathSegment",Points[i-1]+Vector3.Up*.02f,Points[i]+Vector3.Up*.02f,.012f,VisualFactory.Gold);
        }
    }
    public void Save()
    {
        try
        {
            using var file=FileAccess.Open("user://path.json",FileAccess.ModeFlags.Write);
            if (file==null) throw new System.IO.IOException("Cannot open path file: "+FileAccess.GetOpenError());
            file.StoreString(JsonSerializer.Serialize(Points.Select(p=>new[] {p.X*100,-p.Z*100}).ToArray()));
            Game.Status="Path saved in centimetres";
        }
        catch (System.Exception ex) { Game.Status="Cannot save path: "+ex.Message; }
    }
    public void Load()
    {
        try
        {
            if (!FileAccess.FileExists("user://path.json")) return;
            var data = JsonSerializer.Deserialize<float[][]>(FileAccess.GetFileAsString("user://path.json"));
            if (ReplacePoints(data)) Game.Status="Path loaded";
        }
        catch (System.Exception ex) { Game.Status = "Path: " + ex.Message; }
    }
}
