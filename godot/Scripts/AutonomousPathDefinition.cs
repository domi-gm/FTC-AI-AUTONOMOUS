using Godot;
using System;
using System.Collections.Generic;

public enum WaypointAction
{
    None,       // Drive through waypoint
    Shoot,      // Aim turret and launch ball into Hive/target
    IntakeOn,   // Turn on front/rear intake to collect floor pieces
    IntakeOff,  // Turn off intake
    Wait        // Pause for specified duration (ActionParam in seconds)
}

public class AutonomousWaypoint
{
    public float X { get; set; }
    public float Z { get; set; }
    public float HeadingDegrees { get; set; } = 0f;
    public WaypointAction Action { get; set; } = WaypointAction.None;
    public float ActionParam { get; set; } = 0.5f;
    public float SpeedScale { get; set; } = 1.0f;

    public Vector3 Position => new(X, 0, Z);

    public AutonomousWaypoint Copy()
    {
        return new AutonomousWaypoint
        {
            X = X,
            Z = Z,
            HeadingDegrees = HeadingDegrees,
            Action = Action,
            ActionParam = ActionParam,
            SpeedScale = SpeedScale
        };
    }
}

public class AutonomousPathDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Autonomous Path";
    public string StartingPosition { get; set; } = "Red Near"; // "Red Near", "Red Far", "Blue Near", "Blue Far", or "Any"
    public List<AutonomousWaypoint> Waypoints { get; set; } = new();

    public AutonomousPathDefinition Copy()
    {
        var copy = new AutonomousPathDefinition
        {
            Id = Id,
            Name = Name,
            StartingPosition = StartingPosition,
            Waypoints = new List<AutonomousWaypoint>()
        };
        foreach (var wp in Waypoints)
        {
            copy.Waypoints.Add(wp.Copy());
        }
        return copy;
    }
}
