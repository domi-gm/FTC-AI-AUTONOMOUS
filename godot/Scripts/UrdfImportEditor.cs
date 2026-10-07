using Godot;
using System;
using System.Threading.Tasks;

public partial class SimulatorHud
{
    private void SelectUrdf()
    {
        var dialog = new FileDialog { FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem,
            Title = "Select URDF inside its exported package (keep the meshes folder)", Filters = new[] { "*.urdf ; URDF robot description" }, UseNativeDialog = false };
        _root.AddChild(dialog); dialog.Canceled += () => dialog.QueueFree();
        dialog.FileSelected += async path => { dialog.QueueFree(); await ImportUrdf(path); };
        dialog.PopupCentered(new Vector2I(820, 560));
    }
    // A successful import replaces only the editable draft; failures leave both draft and applied profile intact.
    private async Task ImportUrdf(string path)
    {
        var draft = _draft; if (_importBusy || draft == null) return;
        _importBusy = true; _importMessage = "Reading URDF geometry and joints…"; ImportSetup();
        try
        {
            var parsed = await Task.Run(() => UrdfRobotReader.Read(path));
            if (_draft != draft || !IsInsideTree()) return;
            var definition = parsed.Store();
            draft.Imported = definition; draft.Name = parsed.Name;
            _bodySelection = "chassis"; _jointSelection = "";
            _importMessage = $"URDF imported: {definition.Bodies.Count} bodies, {definition.Joints.Count} existing joints. Review → SAVE + TEST IN PRACTICE.";
        }
        catch (Exception ex) { _importMessage = "URDF import failed: " + ex.Message; }
        finally { _importBusy = false; if (_draft == draft && IsInsideTree()) ImportSetup(); }
    }
}
