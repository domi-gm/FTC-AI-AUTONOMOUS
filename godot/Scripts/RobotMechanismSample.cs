using Godot;

public static class RobotMechanismSample
{
    public static ImportedRobotDefinition Load()
    {
        var d = new ImportedRobotDefinition();
        foreach (string id in new[] { "chassis", "arm", "slider", "tool" })
        {
            var part = ImportedRobot.Import(ProjectSettings.GlobalizePath("res://Assets/RobotSamples/mechanism-" + id + ".stl"));
            part.BodyId = id; d.Parts.Add(part);
            d.Bodies.Add(new RobotBodyDefinition { Id = id, Name = id, MassKg = id == "chassis" ? 5 : .1f });
        }
        d.Joints.Add(new RobotJointDefinition { Id = "shoulder", Name = "Shoulder", ChildBody = "arm", PivotSource = new float[] { 200, 150, 165 }, AxisSource = new float[] { 0, 1, 0 },
            Lower = -Mathf.Pi / 4, Upper = Mathf.Pi / 4, MotorEnabled = true, MaxSpeed = 1, MaxEffort = 2 });
        d.Joints.Add(new RobotJointDefinition { Id = "lift", Name = "Lift", ChildBody = "slider", PivotSource = new float[] { 100, 150, 100 }, AxisSource = new float[] { 0, 0, 1 },
            Type = "prismatic", Lower = 0, Upper = .12f, MotorEnabled = true, MaxSpeed = .1f, MaxEffort = 20 });
        d.Joints.Add(new RobotJointDefinition { Id = "tool-mount", Name = "Tool mount", ParentBody = "slider", ChildBody = "tool", Type = "fixed",
            PivotSource = new float[] { 100, 150, 200 }, AxisSource = new float[] { 0, 0, 1 } });
        RobotMechanismValidation.ValidateReady(d); return d;
    }
}
