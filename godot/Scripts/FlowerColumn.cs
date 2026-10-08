using Godot;
using System.Collections.Generic;
using System.Linq;
public sealed class FlowerColumn
{
    public Vector3 Base, Inward;
    public readonly List<GamePiece> Balls = new();
    public const float Top = .5461f;
    public void Store(GamePiece ball)
    {
        float total = 0;
        for (int i = 0; i < Balls.Count; i++) total += Balls[i].Radius * 2;
        float height = .008f + total + ball.Radius;
        ball.GlobalPosition = Base + Vector3.Up * height;
        ball.Stored = false; ball.Freeze = false; ball.CollisionLayer = 2; ball.CollisionMask = 7;
        Balls.Add(ball);
    }
    public void Arrange() { }
    public void Update()
    {
        for (int i = Balls.Count - 1; i >= 0; i--)
        {
            var b = Balls[i];
            if (!GodotObject.IsInstanceValid(b) || !Inside(b)) Balls.RemoveAt(i);
        }
        for (int i = 1; i < Balls.Count; i++)
        {
            var key = Balls[i];
            int j = i - 1;
            while (j >= 0 && Balls[j].GlobalPosition.Y > key.GlobalPosition.Y)
            {
                Balls[j + 1] = Balls[j];
                j--;
            }
            Balls[j + 1] = key;
        }
    }
    private bool Inside(GamePiece ball)
    {
        var p = ball.GlobalPosition - Base;
        return new Vector2(p.X,p.Z).Length() < .059f && p.Y > 0 && p.Y < Top + ball.Radius;
    }
    public bool TryCatch(GamePiece ball)
    {
        if (ball.Stored || !Inside(ball)) return false;
        if (!Balls.Contains(ball)) Balls.Add(ball); return true;
    }
    public GamePiece ReleaseBottom(bool bottom = true)
    {
        Update(); if (Balls.Count == 0) return null;
        var ball = Balls[bottom ? 0 : Balls.Count - 1]; Balls.Remove(ball);
        foreach (var remaining in Balls) remaining.Sleeping = false;
        ball.GlobalPosition = Base + Inward * .16f + Vector3.Up * (bottom ? ball.Radius + .015f : Top + .1f);
        ball.LinearVelocity = Inward * .45f; ball.Sleeping = false; return ball;
    }
    public PieceKind? Owner
    {
        get
        {
            PieceKind? owner = null;
            for (int i = 0; i < Balls.Count; i++)
            {
                var b = Balls[i];
                if (b.Position.Y + b.Radius > .1024f && b.Position.Y - b.Radius < Top && b.Kind != PieceKind.Pollen)
                    owner = b.Kind;
            }
            return owner;
        }
    }
    public int ScoringCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Balls.Count; i++)
            {
                var b = Balls[i];
                if (b.Position.Y + b.Radius > .1024f && b.Position.Y - b.Radius < Top) count++;
            }
            return count;
        }
    }
}
