using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class AutonomousPathRoster
{
    private const string RosterPath = "user://autonomous_paths.json";
    private static List<AutonomousPathDefinition> _cachedPaths;

    public static List<AutonomousPathDefinition> GetPaths()
    {
        if (_cachedPaths != null) return _cachedPaths;
        LoadPaths();
        return _cachedPaths;
    }

    public static void LoadPaths()
    {
        try
        {
            if (FileAccess.FileExists(RosterPath))
            {
                string json = FileAccess.GetFileAsString(RosterPath);
                var list = JsonSerializer.Deserialize<List<AutonomousPathDefinition>>(json);
                if (list != null && list.Count > 0)
                {
                    _cachedPaths = list;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning("Autonomous path roster load error: " + ex.Message);
        }

        _cachedPaths = CreateDefaultPresets();
        SavePaths();
    }

    public static void SavePaths()
    {
        try
        {
            using var file = FileAccess.Open(RosterPath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(JsonSerializer.Serialize(_cachedPaths, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning("Autonomous path roster save error: " + ex.Message);
        }
    }

    public static void AddOrUpdate(AutonomousPathDefinition path)
    {
        GetPaths();
        var copy = path.Copy();
        int existing = _cachedPaths.FindIndex(p => p.Id == copy.Id || p.Name.Equals(copy.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            _cachedPaths[existing] = copy;
        else
            _cachedPaths.Add(copy);
        SavePaths();
    }

    public static void Remove(int index)
    {
        GetPaths();
        if (index >= 0 && index < _cachedPaths.Count)
        {
            _cachedPaths.RemoveAt(index);
            SavePaths();
        }
    }

    public static void ResetToDefaults()
    {
        _cachedPaths = CreateDefaultPresets();
        SavePaths();
    }

    public static AutonomousPathDefinition GetPathByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var paths = GetPaths();
        return paths.Find(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Copy();
    }

    public static AutonomousPathDefinition GetPathForPosition(string pos)
    {
        var paths = GetPaths();
        var found = paths.Find(p => p.StartingPosition.Equals(pos, StringComparison.OrdinalIgnoreCase));
        return (found ?? (paths.Count > 0 ? paths[0] : null))?.Copy();
    }

    public static List<AutonomousPathDefinition> CreateDefaultPresets()
    {
        float size = Arena.Size;
        var list = new List<AutonomousPathDefinition>();

        // 1. Red Near: 3-Piece Hive Cycle
        var redNear = new AutonomousPathDefinition
        {
            Name = "Red Near 3-Piece Hive Cycle",
            StartingPosition = "Red Near",
            Waypoints = new List<AutonomousWaypoint>
            {
                new() { X = 0.84f, Z = -0.75f, Action = WaypointAction.Shoot, ActionParam = 0.5f },
                new() { X = 1.15f, Z = -1.15f, Action = WaypointAction.IntakeOn, ActionParam = 0f },
                new() { X = 1.60f, Z = -0.85f, Action = WaypointAction.Wait, ActionParam = 0.4f },
                new() { X = 0.95f, Z = -0.75f, Action = WaypointAction.Shoot, ActionParam = 0.5f },
                new() { X = 0.84f, Z = -0.35f, Action = WaypointAction.IntakeOff, ActionParam = 0f }
            }
        };
        list.Add(redNear);

        // 2. Red Far: Wing Intake & Hive
        var redFar = new AutonomousPathDefinition
        {
            Name = "Red Far Wing Intake & Hive",
            StartingPosition = "Red Far",
            Waypoints = new List<AutonomousWaypoint>
            {
                new() { X = 0.55f, Z = -2.25f, Action = WaypointAction.Shoot, ActionParam = 0.5f },
                new() { X = 0.35f, Z = -1.50f, Action = WaypointAction.IntakeOn, ActionParam = 0f },
                new() { X = 0.75f, Z = -1.10f, Action = WaypointAction.Shoot, ActionParam = 0.5f },
                new() { X = 0.45f, Z = -0.65f, Action = WaypointAction.IntakeOff, ActionParam = 0f }
            }
        };
        list.Add(redFar);

        // 3. Blue Near: Audience Hive Cycle
        var blueNear = new AutonomousPathDefinition
        {
            Name = "Blue Near Audience Hive Cycle",
            StartingPosition = "Blue Near",
            Waypoints = new List<AutonomousWaypoint>
            {
                new() { X = size - 0.84f, Z = -(size - 0.75f), Action = WaypointAction.Shoot, ActionParam = 0.5f },
                new() { X = size - 1.15f, Z = -(size - 1.15f), Action = WaypointAction.IntakeOn, ActionParam = 0f },
                new() { X = size - 1.60f, Z = -(size - 0.85f), Action = WaypointAction.Wait, ActionParam = 0.4f },
                new() { X = size - 0.95f, Z = -(size - 0.75f), Action = WaypointAction.Shoot, ActionParam = 0.5f },
                new() { X = size - 0.84f, Z = -(size - 0.35f), Action = WaypointAction.IntakeOff, ActionParam = 0f }
            }
        };
        list.Add(blueNear);

        // 4. Blue Far: Backstage Cross & Hive
        var blueFar = new AutonomousPathDefinition
        {
            Name = "Blue Far Backstage Cross & Hive",
            StartingPosition = "Blue Far",
            Waypoints = new List<AutonomousWaypoint>
            {
                new() { X = size - 0.55f, Z = -(size - 2.25f), Action = WaypointAction.Shoot, ActionParam = 0.5f },
                new() { X = size - 0.35f, Z = -(size - 1.50f), Action = WaypointAction.IntakeOn, ActionParam = 0f },
                new() { X = size - 0.75f, Z = -(size - 1.10f), Action = WaypointAction.Shoot, ActionParam = 0.5f },
                new() { X = size - 0.45f, Z = -(size - 0.65f), Action = WaypointAction.IntakeOff, ActionParam = 0f }
            }
        };
        list.Add(blueFar);

        return list;
    }
}
