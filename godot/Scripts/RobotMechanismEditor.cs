using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class SimulatorHud
{
    private string _bodySelection = "chassis", _jointSelection = "";
    private void MechanismSetup()
    {
        var d = _draft.Imported;
        if (d.Bodies.Count == 0) d.Bodies.Add(new RobotBodyDefinition());
        Clear("ROBOT MECHANISMS", "Group parts that move together. Connect each moving group to its parent.\nPivot coordinates use your STL units and the original common Fusion origin.");
        AddImportText("Chassis remains kinematic. Link mass/friction affect dynamic links; inertia and collision are approximated from a bounding box.");
        SelectNamed("Rigid group", d.Bodies.Select(b => (b.Id, b.Name)).ToArray(), _bodySelection, value => { _bodySelection = value; MechanismSetup(); });
        var body = d.Bodies.FirstOrDefault(b => b.Id == _bodySelection) ?? d.Bodies[0]; _bodySelection = body.Id;
        EditName("Group name", body.Name, value => body.Name = value);
        Numeric("Mass (kg)", body.MassKg, .001, 500, .001, value => body.MassKg = value);
        Numeric("Friction", body.Friction, 0, 2, .001, value => body.Friction = value);
        Button(_content, "ADD RIGID GROUP", () =>
        {
            var created = new RobotBodyDefinition { Id = "body_" + Guid.NewGuid().ToString("N"), Name = "Link " + d.Bodies.Count };
            d.Bodies.Add(created); _bodySelection = created.Id; MechanismSetup();
        }).Disabled = d.Bodies.Count >= 32;
        if (body.Id != "chassis") Button(_content, "REMOVE SELECTED GROUP", () =>
        {
            foreach (var p in d.Parts.Where(p => p.BodyId == body.Id)) p.BodyId = "chassis";
            d.Joints.RemoveAll(j => j.ParentBody == body.Id || j.ChildBody == body.Id);
            d.Bodies.Remove(body); _bodySelection = "chassis"; MechanismSetup();
        });
        AddImportText("Geometry → rigid group assignment");
        foreach (var part in d.Parts)
            SelectNamed(part.Name, d.Bodies.Select(b => (b.Id, b.Name)).ToArray(), part.BodyId, value => part.BodyId = value);
        AddImportText("JOINTS");
        if (d.Joints.Count > 0)
        {
            var joint = d.Joints.FirstOrDefault(j => j.Id == _jointSelection) ?? d.Joints[0]; _jointSelection = joint.Id;
            SelectNamed("Joint", d.Joints.Select(j => (j.Id, j.Name)).ToArray(), joint.Id, value => { _jointSelection = value; MechanismSetup(); });
            EditName("Joint name", joint.Name, value => joint.Name = value);
            SelectNamed("Parent", d.Bodies.Where(b => b.Id != joint.ChildBody).Select(b => (b.Id, b.Name)).ToArray(), joint.ParentBody, value => { joint.ParentBody = value; MechanismSetup(); });
            SelectNamed("Child", d.Bodies.Where(b => b.Id != "chassis" && b.Id != joint.ParentBody).Select(b => (b.Id, b.Name)).ToArray(), joint.ChildBody, value => { joint.ChildBody = value; MechanismSetup(); });
            Choice("Type", new[] { "fixed", "revolute", "prismatic" }, joint.Type, value =>
            {
                joint.Type = value; joint.MotorEnabled = value != "fixed" && joint.MotorEnabled;
                joint.ReferencePosition = 0;
                joint.ToggleEnabled = false;
                joint.Lower = value == "prismatic" ? -.1f : -Mathf.Pi / 2; joint.Upper = -joint.Lower; MechanismSetup();
            });
            for (int i = 0; i < 3; i++)
            {
                int axis = i;
                Numeric("Pivot " + "XYZ"[i] + " (" + d.Units + ")", joint.PivotSource[i], -100000, 100000, d.Units == "m" ? .000001 : .001, value => joint.PivotSource[axis] = value);
                Numeric("Axis " + "XYZ"[i], joint.AxisSource[i], -1, 1, .000001, value => joint.AxisSource[axis] = value);
            }
            if (joint.Type != "fixed")
            {
                Toggle("Enable motion limits", joint.LimitsEnabled, value => { joint.LimitsEnabled = value; if (!value) joint.ToggleEnabled = false; MechanismSetup(); });
                float display = joint.Type == "revolute" ? 180 / Mathf.Pi : 1 / ImportedRobot.UnitScale(d);
                string unit = joint.Type == "revolute" ? "deg" : d.Units;
                if (joint.LimitsEnabled)
                {
                    Numeric("Lower (" + unit + ")", (joint.Lower + joint.ReferencePosition) * display, -100000, joint.ReferencePosition * display, .001, value => joint.Lower = value / display - joint.ReferencePosition);
                    Numeric("Upper (" + unit + ")", (joint.Upper + joint.ReferencePosition) * display, joint.ReferencePosition * display, 100000, .001, value => joint.Upper = value / display - joint.ReferencePosition);
                }
                Toggle("Enable motor", joint.MotorEnabled, value => { joint.MotorEnabled = value; if (!value) joint.ToggleEnabled = false; MechanismSetup(); });
                Numeric(joint.Type == "revolute" ? "Max speed (deg/s)" : "Max speed (" + d.Units + "/s)", joint.MaxSpeed * display, 0, 1000000, .001, value => joint.MaxSpeed = value / display);
                Numeric(joint.Type == "revolute" ? "Max torque (Nm)" : "Max force (N)", joint.MaxEffort, 0, 10000, .001, value => joint.MaxEffort = value);
                if (joint.MotorEnabled && joint.LimitsEnabled)
                {
                    Toggle("OPEN / CLOSE BUTTON", joint.ToggleEnabled, value =>
                    {
                        joint.ToggleEnabled = value;
                        if (value)
                        {
                            joint.ClosedPosition = joint.Lower + joint.ReferencePosition; joint.OpenPosition = joint.Upper + joint.ReferencePosition;
                            joint.ToggleKey = RobotMechanismValidation.ToggleKeys.FirstOrDefault(key => !d.Joints.Any(j => j != joint && j.ToggleEnabled && j.ToggleKey == key)) ?? "";
                        }
                        MechanismSetup();
                    });
                    if (joint.ToggleEnabled)
                    {
                        Choice("Keyboard key", new[] { "BUTTON ONLY" }.Concat(RobotMechanismValidation.ToggleKeys).ToArray(), joint.ToggleKey == "" ? "BUTTON ONLY" : joint.ToggleKey,
                            value => { joint.ToggleKey = value == "BUTTON ONLY" ? "" : value; MechanismSetup(); });
                        Numeric("Closed position (" + unit + ")", joint.ClosedPosition * display, (joint.Lower + joint.ReferencePosition) * display, (joint.Upper + joint.ReferencePosition) * display, .001,
                            value => joint.ClosedPosition = value / display);
                        Numeric("Open position (" + unit + ")", joint.OpenPosition * display, (joint.Lower + joint.ReferencePosition) * display, (joint.Upper + joint.ReferencePosition) * display, .001,
                            value => joint.OpenPosition = value / display);
                        AddImportText("Press the key once to open, again to close. A matching button appears during play. Pressing again while moving reverses the target.");
                    }
                }
                else AddImportText("Enable a motor and motion limits to configure an open/close button.");
            }
            Button(_content, "REMOVE SELECTED JOINT", () => { d.Joints.Remove(joint); _jointSelection = ""; MechanismSetup(); });
        }
        Button(_content, "ADD JOINT TO UNCONNECTED GROUP", () =>
        {
            var child = d.Bodies.First(b => b.Id != "chassis" && !d.Joints.Any(j => j.ChildBody == b.Id));
            var joint = new RobotJointDefinition { ChildBody = child.Id, Name = child.Name + " joint" };
            d.Joints.Add(joint); _jointSelection = joint.Id; MechanismSetup();
        }).Disabled = !d.Bodies.Any(b => b.Id != "chassis" && !d.Joints.Any(j => j.ChildBody == b.Id));
        Button(_content, "VALIDATE / REFRESH MOTION PREVIEW", MechanismSetup);
        try
        {
            RobotMechanismValidation.ValidateReady(d);
            var preview = new RobotImportPreview { Definition = d.Copy(), CustomMinimumSize = new(450, 240) }; _content.AddChild(preview);
            var selected = d.Joints.FirstOrDefault(j => j.Id == _jointSelection);
            if (selected != null && selected.Type != "fixed")
            {
                bool revolute = selected.Type == "revolute"; float display = revolute ? 180 / Mathf.Pi : 1 / ImportedRobot.UnitScale(d);
                AddImportText("Preview " + selected.Name + " (" + (revolute ? "deg" : d.Units) + ") — pose only, without gravity");
                var position = new Label { Text = $"Position: {selected.ReferencePosition * display:0.000} " + (revolute ? "deg" : d.Units) };
                _content.AddChild(position);
                var slider = new HSlider { MinValue = ((selected.LimitsEnabled ? selected.Lower : revolute ? -Mathf.Pi : -.5f) + selected.ReferencePosition) * display,
                    MaxValue = ((selected.LimitsEnabled ? selected.Upper : revolute ? Mathf.Pi : .5f) + selected.ReferencePosition) * display, Step = .001, Value = selected.ReferencePosition * display };
                slider.ValueChanged += value => { preview.SetJointPose(selected.Id, (float)value / display - selected.ReferencePosition); position.Text = $"Position: {value:0.000} " + (revolute ? "deg" : d.Units); }; _content.AddChild(slider);
            }
            AddImportText("Configuration valid. Back → SAVE + APPLY ACTIVE to apply.");
        }
        catch (Exception ex) { AddImportText("Needs attention: " + ex.Message); }
        Button(_content, "BACK TO ROBOT IMPORT", ImportSetup);
    }
    private void SelectNamed(string caption, (string Id, string Name)[] items, string selected, Action<string> changed)
    {
        var row = new HBoxContainer(); _content.AddChild(row);
        row.AddChild(new Label { Text = caption, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new(200, 0) });
        var choice = new OptionButton(); foreach (var item in items) choice.AddItem(item.Name);
        choice.Selected = Array.FindIndex(items, item => item.Id == selected);
        choice.ItemSelected += index => changed(items[(int)index].Id); row.AddChild(choice);
    }
    private void EditName(string caption, string value, Action<string> changed)
    {
        AddImportText(caption); var field = new LineEdit { Text = value, MaxLength = 100 }; field.TextChanged += text => changed(text); _content.AddChild(field);
    }
    private void Numeric(string caption, double value, double min, double max, double step, Action<float> changed)
    {
        var row = new HBoxContainer(); _content.AddChild(row); row.AddChild(new Label { Text = caption, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var input = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new(155, 0) };
        input.ValueChanged += number => changed((float)number); row.AddChild(input);
    }
    private void Toggle(string caption, bool value, Action<bool> changed)
    {
        var input = new CheckButton { Text = caption, ButtonPressed = value }; input.Toggled += on => changed(on); _content.AddChild(input);
    }
}

public static class RobotMechanismPose
{
    // Transforms the shared, normalized rest-frame geometry, including all descendants.
    public static Dictionary<string, Transform3D> Evaluate(ImportedRobotDefinition d, string jointId, float value)
    {
        RobotMechanismValidation.ValidateStructure(d);
        var transforms = new Dictionary<string, Transform3D> { ["chassis"] = Transform3D.Identity };
        var offset = ImportedRobot.NormalizationOffset(d);
        foreach (var body in d.Bodies) Resolve(body.Id);
        return transforms;
        Transform3D Resolve(string id)
        {
            if (transforms.TryGetValue(id, out var result)) return result;
            var joint = d.Joints.FirstOrDefault(j => j.ChildBody == id);
            if (joint == null) return transforms[id] = Transform3D.Identity;
            float q = joint.Id == jointId ? value : 0;
            if (joint.LimitsEnabled) q = Mathf.Clamp(q, joint.Lower, joint.Upper);
            var axis = ImportedRobot.ConvertPoint(RobotMechanismValidation.Vec(joint.AxisSource), d).Normalized();
            var pivot = ImportedRobot.ConvertPoint(RobotMechanismValidation.Vec(joint.PivotSource), d) + offset;
            var motion = Transform3D.Identity;
            if (joint.Type == "revolute") { var rotation = new Basis(axis, q); motion = new(rotation, pivot - rotation * pivot); }
            if (joint.Type == "prismatic") motion.Origin = axis * q;
            return transforms[id] = Resolve(joint.ParentBody) * motion;
        }
    }
}
