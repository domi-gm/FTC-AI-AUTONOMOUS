using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class SimulatorHud
{
    private PanelContainer _jointControls;
    private VBoxContainer _jointControlsList;
    private ImportedRobotRig _buttonRig;
    private readonly Dictionary<string, Button> _jointButtons = new();
    private Button _intakeButton;
    private void CreateJointControls()
    {
        _jointControls = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = -345, OffsetRight = -24, OffsetTop = -255, OffsetBottom = -80, Visible = false };
        _jointControls.AddThemeStyleboxOverride("panel", CreateBox(new Color(.045f, .06f, .09f, .9f), Gold, 8, 1, 10, 10));
        _root.AddChild(_jointControls);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        scroll.GetVScrollBar().Modulate = Colors.Transparent;
        scroll.GetVScrollBar().CustomMinimumSize = Vector2.Zero;
        _jointControls.AddChild(scroll);
        _jointControlsList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(_jointControlsList);
    }
    private void UpdateJointControls()
    {
        var rig = Game.Player?.Rig;
        if (rig != _buttonRig)
        {
            _buttonRig = rig; _jointButtons.Clear(); _intakeButton = null;
            foreach (var child in _jointControlsList.GetChildren()) { _jointControlsList.RemoveChild(child); child.QueueFree(); }
            if (rig != null)
            {
                _jointControlsList.AddChild(new Label { Text = "JOINT CONTROLS" });
                _intakeButton = Button(_jointControlsList, "INTAKE", () => rig.Actor.ToggleIntake());
                _intakeButton.Name = "IntakeToggle";
                _intakeButton.FocusMode = Control.FocusModeEnum.None;
                foreach (var spec in rig.Definition.Joints.Where(j => j.ToggleEnabled))
                    _jointButtons[spec.Id] = Button(_jointControlsList, spec.Name, () => rig.ToggleJoint(spec.Id));
            }
        }
        _jointControls.Visible = rig != null && !MenuVisible && Game.Running;
        if (!_jointControls.Visible) return;
        bool full = rig.Actor.Inventory.Count >= rig.Actor.Capacity;
        _intakeButton.Text = $"[J / {Game.KeyIntake}] INTAKE: {(rig.Actor.Intake ? "ON" : "OFF")}\n" +
            (full ? $"FULL ({rig.Actor.Inventory.Count}/{rig.Actor.Capacity}) — shoot first" : $"Pieces: {rig.Actor.Inventory.Count}/{rig.Actor.Capacity}");
        _intakeButton.TooltipText = "Toggle intake. Approach floor pieces with the front of the robot. A full inventory requires shooting before collecting more.";
        _intakeButton.Disabled = !Game.DrivingAllowed;
        foreach (var spec in rig.Definition.Joints.Where(j => j.ToggleEnabled))
        {
            var button = _jointButtons[spec.Id];
            button.Text = (spec.ToggleKey == "" ? "" : "[" + spec.ToggleKey + "] ") + spec.Name + ": " + (rig.NextToggleOpens(spec.Id) ? "OPEN" : "CLOSE");
            button.Disabled = !Game.DrivingAllowed;
        }
    }
}
