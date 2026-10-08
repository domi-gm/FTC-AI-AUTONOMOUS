using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class RobotRoster
{
    private const string RosterPath = "user://robot_roster.json";
    private static List<RobotProfile> _cachedRoster;

    public static List<RobotProfile> GetRoster()
    {
        if (_cachedRoster != null) return _cachedRoster;
        LoadRoster();
        return _cachedRoster;
    }

    public static void LoadRoster()
    {
        try
        {
            if (FileAccess.FileExists(RosterPath))
            {
                string json = FileAccess.GetFileAsString(RosterPath);
                var list = JsonSerializer.Deserialize<List<RobotProfile>>(json);
                if (list != null && list.Count > 0)
                {
                    _cachedRoster = list;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning("Robot roster load error: " + ex.Message);
        }

        _cachedRoster = CreateDefaultPresets();
        SaveRoster();
    }

    public static void SaveRoster()
    {
        try
        {
            using var file = FileAccess.Open(RosterPath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(JsonSerializer.Serialize(_cachedRoster));
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning("Robot roster save error: " + ex.Message);
        }
    }

    public static void AddOrUpdate(RobotProfile profile)
    {
        GetRoster();
        var copy = profile.Copy();
        int existing = _cachedRoster.FindIndex(r => r.Name.Equals(copy.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            _cachedRoster[existing] = copy;
        else
            _cachedRoster.Add(copy);
        SaveRoster();
    }

    public static void Remove(int index)
    {
        GetRoster();
        if (index >= 0 && index < _cachedRoster.Count && _cachedRoster.Count > 1)
        {
            _cachedRoster.RemoveAt(index);
            SaveRoster();
        }
    }

    public static void ResetToDefaults()
    {
        _cachedRoster = CreateDefaultPresets();
        SaveRoster();
    }

    public static List<RobotProfile> CreateDefaultPresets()
    {
        var list = new List<RobotProfile>();

        // 1. SWYFT Standard
        list.Add(new RobotProfile
        {
            Name = "SWYFT Standard (45x45)",
            WidthCm = 45.72f,
            LengthCm = 45.72f,
            Speed = 1.5f,
            Acceleration = 3.0f,
            TurnSpeed = 2.8f,
            Turrets = 1,
            Intakes = 1
        });

        // 2. BioSprint 38 (Agile Parametric)
        list.Add(new RobotProfile
        {
            Name = "BioSprint 38 (Agile)",
            WidthCm = 38.0f,
            LengthCm = 38.0f,
            Speed = 2.2f,
            Acceleration = 5.0f,
            TurnSpeed = 3.8f,
            Turrets = 1,
            Intakes = 2
        });

        // 3. TwinStinger 44 (2 Turrets)
        list.Add(new RobotProfile
        {
            Name = "TwinStinger 44 (2 Turrets)",
            WidthCm = 44.0f,
            LengthCm = 44.0f,
            Speed = 1.3f,
            Acceleration = 2.5f,
            TurnSpeed = 2.4f,
            Turrets = 2,
            Intakes = 1
        });

        // 4. Articulated CAD Demo
        try
        {
            list.Add(new RobotProfile
            {
                Name = "Articulated CAD Demo",
                Imported = RobotMechanismSample.Load()
            });
        }
        catch { }

        return list;
    }
}
