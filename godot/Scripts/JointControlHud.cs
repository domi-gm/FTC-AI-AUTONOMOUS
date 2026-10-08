using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class SimulatorHud
{
    private PanelContainer _jointControls;
    private VBoxContainer _jointControlsList;
    private ImportedRobotRig _buttonRig;
    private readonly Dictionary<string, Button> _jointButtons = new();
    private void CreateJointControls()
    {
        _jointControls = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = -345, OffsetRight = -24, OffsetTop = -255, OffsetBottom = -80, Visible = false };
        _jointControls.AddThemeStyleboxOverride("panel", CreateBox(new Color(.045f, .06f, .09f, .9f), Gold, 8, 1, 10, 10));
        _root.AddChild(_jointControls);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; _jointControls.AddChild(scroll);
        _jointControlsList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(_jointControlsList);
    }
    private void UpdateJointControls()
    {
        var rig = Game.Player?.Rig;
        if (rig != _buttonRig)
        {
            _buttonRig = rig; _jointButtons.Clear();
            foreach (var child in _jointControlsList.GetChildren()) { _jointControlsList.RemoveChild(child); child.QueueFree(); }
            if (rig != null)
            {
                _jointControlsList.AddChild(new Label { Text = "JOINT CONTROLS" });
                foreach (var spec in rig.Definition.Joints.Where(j => j.ToggleEnabled))
                    _jointButtons[spec.Id] = Button(_jointControlsList, spec.Name, () => rig.ToggleJoint(spec.Id));
            }
        }
        _jointControls.Visible = rig != null && _jointButtons.Count > 0 && !MenuVisible && Game.Running;
        if (!_jointControls.Visible) return;
        foreach (var spec in rig.Definition.Joints.Where(j => j.ToggleEnabled))
        {
            var button = _jointButtons[spec.Id];
            button.Text = (spec.ToggleKey == "" ? "" : "[" + spec.ToggleKey + "] ") + spec.Name + ": " + (rig.NextToggleOpens(spec.Id) ? "OPEN" : "CLOSE");
            button.Disabled = !Game.DrivingAllowed;
        }
    }
}
