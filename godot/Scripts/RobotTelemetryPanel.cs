using Godot;

/// <summary>Telemetrie din starea simulată; nu pretinde un model electric REV.</summary>
public partial class RobotTelemetryPanel : Control
{
    public Simulation Game;
    private readonly Vector2[] _history=new Vector2[100];
    private int _count, _next;
    private float _sample;
    private RobotAgent _robot;
    private Font _font;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        _font=GD.Load<FontFile>("res://Assets/Fonts/Poppins-Regular.ttf");
    }
    public override void _Process(double delta)
    {
        Visible=Game.Started;
        if (Game.Player!=_robot)
        { _robot=Game.Player; _count=_next=0; _sample=0; }
        if (Game.Running && (_sample-=(float)delta)<=0)
        {
            _sample=.05f;
            _history[_next]=new(Game.Aim.PollenSpeed,Game.Aim.NectarSpeed);
            _next=(_next+1)%_history.Length; _count=Mathf.Min(_count+1,_history.Length);
        }
        QueueRedraw();
    }
    private void Text(string text,float x,float y,Color color,int size=12)
        => DrawString(_font,new(x,y),text,HorizontalAlignment.Left,-1,size,color);
    private void Badge(string text,float y,Color background,Color foreground)
    {
        DrawRect(new(14,y,276,22),background);
        Text(text,22,y+16,foreground,11);
    }
    public override void _Draw()
    {
        var robot=Game.Player; if (robot==null || _font==null) return;
        Color muted=new("9097a7"), white=new("e2e5ed"), green=new("62e5a4"), red=new("ff667b");
        DrawRect(new(0,0,304,408),new Color(.065f,.075f,.095f,.95f));
        DrawRect(new(0,0,304,408),new("343a48"),false,1);
        string title=Game.Profile.Name ?? "ROBOT";
        if (title.Length>15) title=title[..15];
        Text(title.ToUpperInvariant(),14,27,VisualFactory.Gold,16);
        DrawRect(new(222,12,68,18),new("3479d5"));
        bool sizeOk=robot.Width<=.45721f && robot.Length<=.45721f;
        Text(sizeOk ? "SIZE OK" : "SIZE !",230,25,white,10);
        Text($"{(robot.Red ? "RED" : "BLUE")} {robot.Number}  •  LOCAL ROBOT",14,48,white,11);
        for (int i=0;i<robot.Capacity;i++)
        {
            Color color=i>=robot.Inventory.Count ? new("292e39") : robot.Inventory[i]==PieceKind.Pollen
                ? VisualFactory.Gold : robot.Inventory[i]==PieceKind.RedNectar ? VisualFactory.Red : VisualFactory.Blue;
            DrawCircle(new(25+i*28,69),10,color);
            DrawArc(new(25+i*28,69),10,0,Mathf.Tau,32,new("515866"),1,true);
        }
        bool inField=robot.Position.X>=0 && robot.Position.X<=Arena.Size && robot.Position.Z<=0 && robot.Position.Z>=-Arena.Size;
        Badge(Game.Paused ? "PAUSED" : inField ? "IN FIELD  •  TRACKING" : "OUT OF FIELD",91,new("173b2d"),green);
        Badge(Game.Aim.PollenClear ? "POLLEN  •  CLEAR PATH" : "POLLEN  •  BLOCKED",120,Game.Aim.PollenClear ? new("173b2d") : new("622936"),Game.Aim.PollenClear ? green : white);
        Badge(Game.Aim.NectarClear ? "NECTAR  •  CLEAR PATH" : "NECTAR  •  BLOCKED",149,Game.Aim.NectarClear ? new("173b2d") : new("622936"),Game.Aim.NectarClear ? green : white);
        Vector3 direction=Game.Target(robot)-robot.Turret.GlobalPosition; direction.Y=0;
        Vector3 front=-robot.Turret.GlobalBasis.Z; front.Y=0;
        float error=direction.LengthSquared()>.0001f ? front.SignedAngleTo(direction,Vector3.Up) : 0;
        Vector2 center=new(35,200);
        DrawArc(center,19,0,Mathf.Tau,48,new("444b59"),2,true);
        DrawLine(center,center+new Vector2(Mathf.Sin(error),-Mathf.Cos(error))*16,green,3,true);
        DrawCircle(center,3,white);
        Text($"{(Game.AimFlower ? "FLOWER" : "HIVE")}  /  TURRET TO TARGET",68,193,muted,10);
        Text(Mathf.Abs(error)<.05f ? "ON TARGET" : $"ERROR {Mathf.RadToDeg(error):0.0}°",68,213,Mathf.Abs(error)<.05f ? green : red,14);
        Text($"LAUNCH SPEED      {Game.Aim.Speed/.0254f:0} IN/S",14,242,white,11);
        Text($"FLIGHT TIME       {Game.Aim.FlightTime:0.00} S",14,258,muted,11);
        Rect2 graph=new(14,270,276,54); DrawRect(graph,new("1b2430"));
        DrawLine(new(14,297),new(290,297),new("303b49"),1);
        for (int i=1;i<_count;i++)
        {
            int start=(_next-_count+_history.Length)%_history.Length;
            var a=_history[(start+i-1)%_history.Length]; var b=_history[(start+i)%_history.Length];
            float x1=14+(i-1)*276f/99, x2=14+i*276f/99;
            DrawLine(new(x1,324-Mathf.Clamp(a.X/ShotPlanner.MaximumSpeed,0,1)*54),new(x2,324-Mathf.Clamp(b.X/ShotPlanner.MaximumSpeed,0,1)*54),VisualFactory.Gold,1,true);
            DrawLine(new(x1,324-Mathf.Clamp(a.Y/ShotPlanner.MaximumSpeed,0,1)*54),new(x2,324-Mathf.Clamp(b.Y/ShotPlanner.MaximumSpeed,0,1)*54),new("db8bea"),1,true);
        }
        Text("POLLEN",18,281,VisualFactory.Gold,8);
        Text("NECTAR",225,281,new("db8bea"),8);
        var hive=robot.Red ? Game.RedHive : Game.BlueHive;
        float load=hive.TipLoadFraction;
        Text($"{(robot.Red ? "RED" : "BLUE")} HIVE  /  UP CELL",14,346,muted,11);
        Text($"{load*100:0}% TO TIP",207,346,white,10);
        DrawRect(new(14,358,276,8),new("292f3a")); DrawRect(new(14,358,276*load,8),VisualFactory.Gold);
        Text($"SHOTS {robot.ShotsFired}  •  LANDED {robot.ShotsMade}",14,391,muted,10);
        Text($"{robot.Velocity.Length()/.0254f:0} IN/S",237,391,white,10);
    }
}
