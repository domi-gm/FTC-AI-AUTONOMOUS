using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public partial class SimulatorHud
{
    private string _importMessage = "";
    private bool _importBusy;
    public void ShowMechanismDemo()
    {
        _draft = new RobotProfile { Name = "Articulated STL demo", Imported = RobotMechanismSample.Load() };
        _bodySelection = "arm"; _jointSelection = "shoulder"; MechanismSetup();
    }
    private void ImportSetup()
    {
        _draft ??= Game.Profile.Copy();
        _draft.Imported ??= new ImportedRobotDefinition();
        var definition = _draft.Imported;
        Clear("IMPORT ROBOT", "STL: geometry + manual joint setup.\nURDF: geometry + existing joints and limits imported automatically.");
        var name = new LineEdit { Text = _draft.Name, PlaceholderText = "Robot name", MaxLength = 100 };
        name.TextChanged += text => _draft.Name = text; _content.AddChild(name);
        if (definition.SourceFormat == "stl")
        {
            Choice("STL units", new[] { "mm", "cm", "m", "in" }, definition.Units, value => { definition.Units = value; ImportSetup(); });
            Choice("Vertical axis in Fusion", new[] { "Z", "Y" }, definition.UpAxis, value => { definition.UpAxis = value; ImportSetup(); });
        }
        else AddImportText("URDF imported automatically • metres / Z-up • review joints before applying");
        Button(_content, $"FRONT ORIENTATION: {definition.QuarterTurns * 90}° (Godot forward = -Z)", () => { definition.QuarterTurns = (definition.QuarterTurns + 1) % 4; ImportSetup(); });
        bool valid = false;
        if (definition.Parts.Count > 0)
        {
            try
            {
                var bounds = ImportedRobot.Bounds(definition);
                _content.AddChild(new RobotImportPreview { Definition = definition.Copy(), CustomMinimumSize = new(450, 240) });
                AddImportText($"Width {bounds.Size.X * 100:0.0} × length {bounds.Size.Z * 100:0.0} × height {bounds.Size.Y * 100:0.0} cm\n{definition.Parts.Count} STL meshes • {ImportedRobot.TriangleCount(definition):N0} triangles\nKinematic chassis + {Math.Max(0, definition.Bodies.Count - 1)} dynamic links • {definition.Joints.Count} joints");
                if (bounds.Size.X > .4572f || bounds.Size.Z > .4572f) AddImportText("Footprint exceeds 45.72 cm. Check scale / competition constraints.");
                RobotMechanismValidation.ValidateReady(definition); valid = true;
            }
            catch (Exception ex) { AddImportText(ex.Message); }
        }
        if (_importMessage != "") AddImportText(_importMessage);
        if (definition.ImportNotes.Count > 0) AddImportText(string.Join("\n", definition.ImportNotes));
        Button(_content, _importBusy ? "IMPORTING…" : "IMPORT URDF + EXISTING JOINTS…", SelectUrdf).Disabled = _importBusy;
        var import = Button(_content, _importBusy ? "IMPORTING…" : "ADD STL SUBASSEMBLIES…", SelectStl);
        import.Disabled = _importBusy || definition.Parts.Count >= 32 || definition.SourceFormat == "urdf";
        if (definition.SourceFormat == "urdf") Button(_content, "START NEW MANUAL STL ROBOT", () =>
        {
            _draft.Imported = new ImportedRobotDefinition(); _importMessage = "New STL draft. Configure joints manually after importing geometry."; ImportSetup();
        }).Disabled = _importBusy;
        if (definition.Parts.Count == 0)
        {
            Button(_content, "LOAD ARTICULATED DEMO (hinge + lift)", () =>
            {
                try { _draft.Imported = RobotMechanismSample.Load(); _draft.Name = "Articulated STL demo"; _importMessage = "Demo loaded. Open RIGID GROUPS / JOINTS / MOTORS."; }
                catch (Exception ex) { _importMessage = ex.Message; }
                ImportSetup();
            }).Disabled = _importBusy;
            var sample = Button(_content, "LOAD EXAMPLE STL (40 × 30 × 25 cm)", () =>
            {
                try
                {
                    var imported = ImportedRobot.Import(ProjectSettings.GlobalizePath("res://Assets/RobotSamples/fusion-box-mm.stl"));
                    _draft.Imported = new ImportedRobotDefinition(); _draft.Imported.Parts.Add(imported);
                    _draft.Name = "Fusion STL example"; _importMessage = "Example loaded. Drag the preview, then SAVE + TEST IN PRACTICE.";
                }
                catch (Exception ex) { _importMessage = ex.Message; }
                ImportSetup();
            });
            sample.Disabled = _importBusy;
        }
        foreach (var part in definition.Parts.ToArray())
        {
            var row = new HBoxContainer(); _content.AddChild(row);
            var groupName = new LineEdit { Text = part.Name, MaxLength = 100, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            groupName.TextChanged += text => part.Name = text; row.AddChild(groupName);
            Button(row, "REMOVE", () => { definition.Parts.Remove(part); _importMessage = ""; ImportSetup(); }).Disabled = _importBusy;
        }
        Button(_content, "RIGID GROUPS / JOINTS / MOTORS", MechanismSetup).Disabled = definition.Parts.Count == 0 || _importBusy;
        AddImportText("Drive uses the existing kinematic chassis. Imported links use box collisions; internal self-collisions are disabled. Intake/shooting remain gameplay anchors. Joint motors: I selects, U/O moves.");
        var apply = Button(_content, "SAVE + TEST IN PRACTICE", () =>
        {
            try { _draft.Save(); Game.Profile = _draft.Copy(); Game.Practice = true; Game.Reset(); _draft = null; }
            catch (Exception ex) { _importMessage = "Cannot apply: " + ex.Message; ImportSetup(); }
        });
        apply.Disabled = !valid || _importBusy;
        Button(_content, "USE PARAMETRIC ROBOT", () => { _draft.Imported = null; RobotSetup(); }).Disabled = _importBusy;
        Button(_content, "BACK / DISCARD", Back);
    }
    private void AddImportText(string text) => _content.AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart });
    private void Choice(string caption, string[] values, string selected, Action<string> changed)
    {
        var row = new HBoxContainer(); _content.AddChild(row);
        row.AddChild(new Label { Text = caption, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var choice = new OptionButton(); foreach (var value in values) choice.AddItem(value);
        choice.Selected = Array.IndexOf(values, selected);
        choice.ItemSelected += index => changed(values[(int)index]); row.AddChild(choice);
    }
    private void SelectStl()
    {
        var draft = _draft;
        var dialog = new FileDialog { FileMode = FileDialog.FileModeEnum.OpenFiles, Access = FileDialog.AccessEnum.Filesystem,
            Title = "Select Fusion STL subassemblies", Filters = new[] { "*.stl ; STL mesh" }, UseNativeDialog = false };
        _root.AddChild(dialog);
        dialog.Canceled += () => dialog.QueueFree();
        dialog.FilesSelected += async paths =>
        {
            dialog.QueueFree();
            if (_draft != draft) return;
            _importBusy = true; _importMessage = "Reading STL geometry…"; ImportSetup();
            try
            {
                if (paths.Length + draft.Imported.Parts.Count > 32) throw new InvalidDataException("Use at most 32 rigid subassembly files.");
                // Only file parsing runs off-thread. Godot nodes/resources and profile changes stay on the main thread.
                var parsed = await Task.Run(() =>
                {
                    var items = new List<(string Path, byte[] Bytes, Vector3[] Points)>(); int triangles = 0;
                    foreach (string path in paths)
                    {
                        if (!Path.GetExtension(path).Equals(".stl", StringComparison.OrdinalIgnoreCase) || new FileInfo(path).Length > StlReader.MaxBytes)
                            throw new InvalidDataException("Select STL files of at most 32 MB each.");
                        var bytes = System.IO.File.ReadAllBytes(path); var points = StlReader.Parse(bytes);
                        triangles += points.Length / 3;
                        if (triangles > StlReader.MaxTriangles) throw new InvalidDataException("Selection exceeds 200,000 triangles. Export coarser meshes from Fusion.");
                        items.Add((path, bytes, points));
                    }
                    return items;
                });
                if (_draft != draft || !IsInsideTree()) return;
                var candidate = draft.Imported.Copy();
                foreach (var item in parsed) candidate.Parts.Add(ImportedRobot.Store(item.Path, item.Bytes, item.Points));
                candidate.Validate();
                ImportedRobot.TriangleCount(candidate);
                // Allow correction of units/orientation in the editor; applying still requires valid bounds.
                draft.Imported = candidate;
                _importMessage = $"Imported {parsed.Count} STL files. Review units, orientation and dimensions before applying.";
            }
            catch (Exception ex) { _importMessage = "Import failed: " + ex.Message; }
            finally { _importBusy = false; if (_draft == draft && IsInsideTree()) ImportSetup(); }
        };
        dialog.PopupCentered(new Vector2I(820, 560));
    }
}

public partial class RobotImportPreview : SubViewportContainer
{
    public ImportedRobotDefinition Definition;
    private Node3D _model;
    public void SetJointPose(string id, float value)
    {
        if (_model == null) return;
        var transforms = RobotMechanismPose.Evaluate(Definition, id, value);
        for (int i = 0; i < Definition.Parts.Count; i++)
            ((Node3D)_model.GetChild(i)).Transform = transforms.GetValueOrDefault(Definition.Parts[i].BodyId, Transform3D.Identity);
    }
    public override void _Ready()
    {
        Stretch = true;
        var viewport = new SubViewport { Size = new(450, 240), OwnWorld3D = true, TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        viewport.AddChild(new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("101824"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .7f } });
        var size = ImportedRobot.Bounds(Definition).Size;
        _model = ImportedRobot.Build(Definition); viewport.AddChild(_model);
        var light = new DirectionalLight3D { RotationDegrees = new(-45, -30, 0), LightEnergy = 1.2f }; viewport.AddChild(light);
        float extent = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        var center = new Vector3(0, size.Y / 2, 0);
        var camera = new Camera3D { Current = true, Fov = 35, Position = center + new Vector3(1.7f, 1.1f, 1.7f) * extent, Near = .001f, Far = 30 };
        viewport.AddChild(camera); camera.LookAt(center);
        GuiInput += input =>
        {
            if (input is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Left) != 0)
            { _model.RotateY(motion.Relative.X * .01f); AcceptEvent(); }
        };
        TooltipText = "Drag left mouse to orbit the preview. Front orientation is set with the orientation button.";
    }
}
