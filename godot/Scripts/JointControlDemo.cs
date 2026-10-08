using Godot;

public partial class Simulation
{
    private void JointControlsDemo()
    {
        var d = RobotMechanismSample.Load();
        d.Joints[0].ToggleEnabled = true; d.Joints[0].ToggleKey = "L"; d.Joints[0].ClosedPosition = 0; d.Joints[0].OpenPosition = -.6f;
        d.Joints[1].ToggleEnabled = true; d.Joints[1].ToggleKey = "M"; d.Joints[1].ClosedPosition = 0; d.Joints[1].OpenPosition = .1f;
        Profile = new RobotProfile { Name = "Joint controls demo (unsaved)", Imported = d }; Profile.Validate(); Practice = true; Reset();
        Player.Bot = false;
        Player.Position = new(.8f,0,-.9f); Player.Rig.Restore(null);
        Hud.ShowPause(false); Camera.FocusPlayer();
        Status = "Unsaved demo: L opens/closes arm • M raises/lowers lift";
        DisplayServer.WindowSetTitle("FTC SIMULATOR — JOINT CONTROLS DEMO");
    }
}
