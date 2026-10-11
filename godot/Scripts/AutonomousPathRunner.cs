using Godot;
using System;

public class AutonomousPathRunner
{
    public RobotAgent Robot;
    public AutonomousPathDefinition Path { get; private set; }
    public int CurrentWaypoint { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    public float ActionTimer { get; private set; }
    public bool ActionActive { get; private set; }
    public string Status { get; private set; } = "IDLE";

    public AutonomousPathRunner(RobotAgent robot = null)
    {
        Robot = robot;
    }

    public void Start(AutonomousPathDefinition path)
    {
        if (path == null || path.Waypoints.Count == 0)
        {
            Stop();
            return;
        }
        Path = path.Copy();
        CurrentWaypoint = 0;
        ActionTimer = 0f;
        ActionActive = false;
        IsFinished = false;
        IsRunning = true;
        Status = $"RUNNING: {Path.Name}";
    }

    public void Stop()
    {
        IsRunning = false;
        IsFinished = false;
        CurrentWaypoint = 0;
        ActionTimer = 0f;
        ActionActive = false;
        Status = "STOPPED";
        if (Robot != null)
        {
            Robot.Command = Vector3.Zero;
            Robot.TurnCommand = 0f;
        }
    }

    public void Reset()
    {
        if (Path != null)
        {
            CurrentWaypoint = 0;
            ActionTimer = 0f;
            ActionActive = false;
            IsFinished = false;
            IsRunning = true;
            Status = $"RUNNING: {Path.Name}";
        }
        else
        {
            Stop();
        }
    }

    public void Update(double delta)
    {
        if (!IsRunning || IsFinished || Robot == null || Path == null || Path.Waypoints.Count == 0)
            return;

        if (CurrentWaypoint >= Path.Waypoints.Count)
        {
            IsFinished = true;
            Robot.Command = Vector3.Zero;
            Robot.TurnCommand = 0f;
            Status = "FINISHED";
            return;
        }

        var wp = Path.Waypoints[CurrentWaypoint];
        Vector3 target = wp.Position;
        Vector3 diff = target - Robot.Position;
        diff.Y = 0;

        float dist = diff.Length();

        if (ActionActive)
        {
            // Execute the current waypoint action
            Robot.Command = Vector3.Zero;
            Robot.TurnCommand = 0f;
            ActionTimer += (float)delta;

            switch (wp.Action)
            {
                case WaypointAction.Shoot:
                    Robot.AimTurrets();
                    if (ActionTimer >= 0.12f && !Robot.FireCommand)
                    {
                        Robot.Fire();
                    }
                    if (ActionTimer >= Math.Max(0.40f, wp.ActionParam))
                    {
                        Robot.FireCommand = false;
                        ActionActive = false;
                        ActionTimer = 0f;
                        CurrentWaypoint++;
                    }
                    break;

                case WaypointAction.Wait:
                    if (ActionTimer >= Math.Max(0.1f, wp.ActionParam))
                    {
                        ActionActive = false;
                        ActionTimer = 0f;
                        CurrentWaypoint++;
                    }
                    break;

                default:
                    ActionActive = false;
                    ActionTimer = 0f;
                    CurrentWaypoint++;
                    break;
            }
            return;
        }

        if (dist > 0.08f)
        {
            // Steer towards target waypoint
            Vector3 dir = diff.Normalized();
            float spd = Mathf.Clamp(dist * 3.5f, 0.25f, 1.0f) * wp.SpeedScale;
            Robot.Command = dir * spd;

            // Compute turn heading towards target waypoint
            Vector3 forward = Robot.Front;
            forward.Y = 0;
            if (forward.LengthSquared() > 0.001f)
            {
                forward = forward.Normalized();
                float cross = forward.X * dir.Z - forward.Z * dir.X;
                float dot = Mathf.Clamp(forward.Dot(dir), -1f, 1f);
                float angle = Mathf.Atan2(cross, dot);
                Robot.TurnCommand = -Mathf.Clamp(angle * 2.5f, -1f, 1f);
            }
        }
        else
        {
            // Arrived at waypoint -> trigger action
            ActionActive = true;
            ActionTimer = 0f;

            switch (wp.Action)
            {
                case WaypointAction.IntakeOn:
                    Robot.Intake = true;
                    ActionActive = false;
                    CurrentWaypoint++;
                    break;

                case WaypointAction.IntakeOff:
                    Robot.Intake = false;
                    ActionActive = false;
                    CurrentWaypoint++;
                    break;

                case WaypointAction.None:
                    ActionActive = false;
                    CurrentWaypoint++;
                    break;

                case WaypointAction.Shoot:
                case WaypointAction.Wait:
                    // Handled above in ActionActive loop
                    break;
            }
        }
    }
}
