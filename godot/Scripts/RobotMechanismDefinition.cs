using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RobotBodyDefinition
{
    public string Id { get; set; } = "chassis";
    public string Name { get; set; } = "Chassis";
    public float MassKg { get; set; } = 1;
    public float Friction { get; set; } = .6f;
    public RobotBodyDefinition Copy() => (RobotBodyDefinition)MemberwiseClone();
}

public sealed class RobotJointDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Joint";
    public string ParentBody { get; set; } = "chassis";
    public string ChildBody { get; set; } = "";
    public string Type { get; set; } = "revolute";
    // In the original shared CAD frame; converted alongside geometry at build time.
    public float[] PivotSource { get; set; } = new float[3];
    public float[] AxisSource { get; set; } = new float[] { 0, 0, 1 };
    public bool LimitsEnabled { get; set; } = true;
    // SI units, relative to the imported assembly pose: radians or metres.
    public float Lower { get; set; } = -Mathf.Pi / 2;
    public float Upper { get; set; } = Mathf.Pi / 2;
    public float ReferencePosition { get; set; } // URDF coordinate at the baked import pose; zero for manual STL.
    public bool MotorEnabled { get; set; }
    public float MaxSpeed { get; set; } = 1;
    public float MaxEffort { get; set; } = 2; // Nm for revolute; N for prismatic.
    public bool ToggleEnabled { get; set; }
    public string ToggleKey { get; set; } = ""; // Empty means on-screen button only.
    public float ClosedPosition { get; set; } // Absolute coordinate: rad / m, including URDF reference.
    public float OpenPosition { get; set; }
    public RobotJointDefinition Copy()
    {
        var copy = (RobotJointDefinition)MemberwiseClone(); copy.PivotSource = (float[])PivotSource.Clone(); copy.AxisSource = (float[])AxisSource.Clone(); return copy;
    }
}

public static class RobotMechanismValidation
{
    public static readonly string[] ToggleKeys = { "L", "G", "M", "V", "X", "Y", "Z", "F6", "F7", "F8" };
    public static bool Id(string value) => value != null && value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-');
    private static bool Name(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100;
    public static bool Vector(float[] v) => v?.Length == 3 && v.All(float.IsFinite);
    public static Vector3 Vec(float[] v) => new(v[0], v[1], v[2]);
    public static void ValidateStructure(ImportedRobotDefinition definition)
    {
        if (definition.Bodies == null || definition.Joints == null || definition.Bodies.Count > 32 || definition.Joints.Count > 31)
            throw new ArgumentException("At most 32 rigid bodies / 31 joints are supported.");
        if (definition.Bodies.Count == 0)
        {
            if (definition.Joints.Count != 0 || definition.Parts.Any(p => p.BodyId != "chassis")) throw new ArgumentException("Create rigid groups before adding joints.");
            return; // Legacy single-body profiles.
        }
        var ids = new HashSet<string>();
        foreach (var body in definition.Bodies)
            if (body == null || !Id(body.Id) || !ids.Add(body.Id) || !Name(body.Name) || !float.IsFinite(body.MassKg + body.Friction)
                || body.MassKg < .001f || body.MassKg > 500 || body.Friction < 0 || body.Friction > 2)
                throw new ArgumentException("Invalid or duplicate rigid group; mass 0.001–500 kg, friction 0–2.");
        if (!ids.Contains("chassis") || definition.Parts.Any(p => !ids.Contains(p.BodyId))) throw new ArgumentException("Every STL must belong to a known group, including chassis.");
        var jointIds = new HashSet<string>(); var parents = new Dictionary<string, string>(); var keys = new HashSet<string>();
        foreach (var joint in definition.Joints)
        {
            if (joint == null || !Id(joint.Id) || !jointIds.Add(joint.Id) || !Name(joint.Name) || !ids.Contains(joint.ParentBody)
                || !ids.Contains(joint.ChildBody) || joint.ChildBody == "chassis" || joint.ParentBody == joint.ChildBody
                || !parents.TryAdd(joint.ChildBody, joint.ParentBody) || !new[] { "fixed", "revolute", "prismatic" }.Contains(joint.Type)
                || !Vector(joint.PivotSource) || !Vector(joint.AxisSource) || Vec(joint.AxisSource).LengthSquared() < .000001f
                || Vec(joint.AxisSource).LengthSquared() > 1000000 || Vec(joint.PivotSource).Abs().Length() > 10000000
                || !float.IsFinite(joint.Lower + joint.Upper + joint.MaxSpeed + joint.MaxEffort + joint.ReferencePosition)
                || joint.MaxSpeed < 0 || joint.MaxSpeed > 1000 || joint.MaxEffort < 0 || joint.MaxEffort > 10000
                || (joint.MotorEnabled && (joint.MaxSpeed == 0 || joint.MaxEffort == 0))
                || (joint.Type == "fixed" && joint.MotorEnabled)) throw new ArgumentException("Invalid joint, axis, motor or duplicate child connection.");
            if (joint.LimitsEnabled && joint.Type != "fixed"
                && (joint.Lower > 0 || joint.Upper < 0 || joint.Lower >= joint.Upper
                    || (joint.Type == "revolute" ? joint.Lower < -Mathf.Pi || joint.Upper > Mathf.Pi : joint.Lower < -3 || joint.Upper > 3)))
                throw new ArgumentException("Limits must contain the imported pose (zero). Revolute range ±180°; sliders ±3 m. Disable limits for continuous wheels.");
            if (joint.ToggleKey == null || (joint.ToggleKey != "" && !ToggleKeys.Contains(joint.ToggleKey))
                || !float.IsFinite(joint.ClosedPosition + joint.OpenPosition)) throw new ArgumentException("Invalid joint button configuration.");
            if (joint.ToggleEnabled && (!joint.MotorEnabled || joint.Type == "fixed" || !joint.LimitsEnabled
                || (joint.ToggleKey != "" && !keys.Add(joint.ToggleKey)) || Mathf.Abs(joint.OpenPosition - joint.ClosedPosition) < .00001f
                || !TargetInRange(joint, joint.OpenPosition) || !TargetInRange(joint, joint.ClosedPosition)))
                throw new ArgumentException("Joint buttons need a motor, motion limits, two distinct positions inside those limits, and a unique available key.");
        }
        foreach (var body in ids)
        {
            var visited = new HashSet<string>(); string current = body;
            while (parents.TryGetValue(current, out string parent))
            { if (!visited.Add(current)) throw new ArgumentException("Joint graph contains a cycle."); current = parent; }
        }
    }
    public static bool TargetInRange(RobotJointDefinition joint, float position) => float.IsFinite(position)
        && position >= joint.Lower + joint.ReferencePosition - .000001f && position <= joint.Upper + joint.ReferencePosition + .000001f;
    public static void ValidateReady(ImportedRobotDefinition definition)
    {
        definition.Validate();
        if (definition.Bodies.Count == 0) return;
        foreach (var body in definition.Bodies)
        {
            ImportedRobot.GetBodyBounds(definition, body.Id);
            if (body.Id != "chassis" && !definition.Joints.Any(j => j.ChildBody == body.Id))
                throw new ArgumentException("Connect '" + body.Name + "' to a parent with a joint before applying.");
        }
    }
}
