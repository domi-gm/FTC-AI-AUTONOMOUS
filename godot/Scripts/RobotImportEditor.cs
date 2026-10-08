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
        Clear("IMPORT CAD ROBOT", "Step-by-step CAD loader: URDF automatic kinematics or STL subassemblies");

        // Robot Name Card
        var nameCard = CreateCard(_dialogBody, "ROBOT PROFILE NAME");
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 12);
        nameCard.AddChild(nameRow);

        var nameLbl = new Label { Text = "Robot Name:", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        nameLbl.AddThemeFontSizeOverride("font_size", 18);
        nameLbl.AddThemeColorOverride("font_color", TextWhite);
        nameRow.AddChild(nameLbl);

        var name = new LineEdit { Text = _draft.Name, PlaceholderText = "Enter robot name...", MaxLength = 100, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        name.AddThemeFontSizeOverride("font_size", 17);
        name.TextChanged += text => _draft.Name = text;
        nameRow.AddChild(name);

        if (definition.Parts.Count == 0)
        {
            // Two Big Cards Container: Option 1 (URDF) vs Option 2 (STL)
            var cadOptionsCard = CreateCard(_dialogBody, "CHOOSE CAD IMPORT METHOD & INSTRUCTIONS");
            var cadGrid = new GridContainer { Columns = 2 };
            cadGrid.AddThemeConstantOverride("h_separation", 18);
            cadGrid.AddThemeConstantOverride("v_separation", 16);
            cadOptionsCard.AddChild(cadGrid);

            // Card 1: URDF
            var urdfCard = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            urdfCard.AddThemeStyleboxOverride("panel", CreateBox(CardInner, Gold, 8, 1, 18, 16));
            cadGrid.AddChild(urdfCard);

            var urdfBox = new VBoxContainer();
            urdfBox.AddThemeConstantOverride("separation", 10);
            urdfCard.AddChild(urdfBox);

            var urdfTitle = new Label { Text = "OPTION 1: IMPORT URDF (RECOMMENDED)" };
            urdfTitle.AddThemeFontSizeOverride("font_size", 18);
            urdfTitle.AddThemeColorOverride("font_color", Gold);
            urdfBox.AddChild(urdfTitle);

            var urdfInst = new Label
            {
                Text = "• Automatic kinematics from Onshape, Fusion 360, or SolidWorks URDF exporters\n• Hinges, sliders, limits, and visual STL links are placed and connected automatically\n• No manual joint positioning or pivot calibration required",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            urdfInst.AddThemeFontSizeOverride("font_size", 14);
            urdfInst.AddThemeColorOverride("font_color", TextWhite);
            urdfBox.AddChild(urdfInst);

            var urdfBtn = Button(urdfBox, _importBusy ? "IMPORTING…" : "IMPORT URDF + EXISTING JOINTS…", SelectUrdf);
            urdfBtn.Disabled = _importBusy;
            urdfBtn.CustomMinimumSize = new(0, 48);

            // Card 2: STL
            var stlCard = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            stlCard.AddThemeStyleboxOverride("panel", CreateBox(CardInner, Green, 8, 1, 18, 16));
            cadGrid.AddChild(stlCard);

            var stlBox = new VBoxContainer();
            stlBox.AddThemeConstantOverride("separation", 10);
            stlCard.AddChild(stlBox);

            var stlTitle = new Label { Text = "OPTION 2: IMPORT STL SUBASSEMBLIES" };
            stlTitle.AddThemeFontSizeOverride("font_size", 18);
            stlTitle.AddThemeColorOverride("font_color", Green);
            stlBox.AddChild(stlTitle);

            var stlInst = new Label
            {
                Text = "• Import individual 3D .stl geometry files for chassis, arms, and sliders\n• Manually configure joint types, travel limits, and motors in Mechanism Editor\n• Full flexibility for custom FTC CAD parts and subassemblies",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            stlInst.AddThemeFontSizeOverride("font_size", 14);
            stlInst.AddThemeColorOverride("font_color", TextWhite);
            stlBox.AddChild(stlInst);

            var stlBtn = Button(stlBox, _importBusy ? "IMPORTING…" : "ADD STL SUBASSEMBLIES…", SelectStl);
            stlBtn.Disabled = _importBusy;
            stlBtn.CustomMinimumSize = new(0, 48);

            // Demos & Examples Row
            var demoCard = CreateCard(_dialogBody, "SAMPLE CAD PRESETS");
            var demoRow = new HBoxContainer();
            demoRow.AddThemeConstantOverride("separation", 12);
            demoCard.AddChild(demoRow);

            var demoBtn = Button(demoRow, "LOAD ARTICULATED DEMO (hinge + lift)", () =>
            {
                try
                {
                    _draft.Imported = RobotMechanismSample.Load();
                    _draft.Name = "Articulated STL demo";
                    _importMessage = "Demo loaded. Open RIGID GROUPS / JOINTS / MOTORS.";
                }
                catch (Exception ex) { _importMessage = ex.Message; }
                ImportSetup();
            });
            demoBtn.Disabled = _importBusy;
            demoBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            demoBtn.CustomMinimumSize = new(0, 44);

            var sampleBtn = Button(demoRow, "LOAD EXAMPLE STL (40 × 30 × 25 cm)", () =>
            {
                try
                {
                    var imported = ImportedRobot.Import(ProjectSettings.GlobalizePath("res://Assets/RobotSamples/fusion-box-mm.stl"));
                    _draft.Imported = new ImportedRobotDefinition();
                    _draft.Imported.Parts.Add(imported);
                    _draft.Name = "Fusion STL example";
                    _importMessage = "Example loaded. Drag the preview, then SAVE + TEST IN PRACTICE.";
                }
                catch (Exception ex) { _importMessage = ex.Message; }
                ImportSetup();
            });
            sampleBtn.Disabled = _importBusy;
            sampleBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            sampleBtn.CustomMinimumSize = new(0, 44);
        }
        else
        {
            var sourceCard = CreateCard(_dialogBody, definition.SourceFormat == "urdf" ? "CAD SOURCE: URDF KINEMATIC PACKAGE" : "CAD SOURCE: STL SUBASSEMBLIES");

            var importRow = new HBoxContainer();
            importRow.AddThemeConstantOverride("separation", 12);
            sourceCard.AddChild(importRow);

            var urdfBtn = Button(importRow, _importBusy ? "IMPORTING…" : "IMPORT URDF + EXISTING JOINTS…", SelectUrdf);
            urdfBtn.Disabled = _importBusy;
            urdfBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            urdfBtn.CustomMinimumSize = new(0, 48);

            var stlBtn = Button(importRow, _importBusy ? "IMPORTING…" : "ADD STL SUBASSEMBLIES…", SelectStl);
            stlBtn.Disabled = _importBusy || definition.Parts.Count >= 32 || definition.SourceFormat == "urdf";
            stlBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            stlBtn.CustomMinimumSize = new(0, 48);

            if (definition.SourceFormat == "urdf")
            {
                var newStlBtn = Button(sourceCard, "START NEW MANUAL STL ROBOT", () =>
                {
                    _draft.Imported = new ImportedRobotDefinition();
                    _importMessage = "New STL draft. Configure joints manually after importing geometry.";
                    ImportSetup();
                });
                newStlBtn.Disabled = _importBusy;
                newStlBtn.CustomMinimumSize = new(0, 44);
            }
        }

        // Step 2: Scale & Orientation Calibration Card
        var calibCard = CreateCard(_dialogBody, "STEP 2: UNITS & ORIENTATION");

        if (definition.SourceFormat == "stl")
        {
            Choice(calibCard, "STL export units:", new[] { "mm", "cm", "m", "in" }, definition.Units, value => { definition.Units = value; ImportSetup(); });
            Choice(calibCard, "Vertical up-axis in CAD:", new[] { "Z", "Y" }, definition.UpAxis, value => { definition.UpAxis = value; ImportSetup(); });
        }
        else
        {
            var urdfInfo = new Label { Text = "URDF imported automatically • metres / Z-up • review joints before applying", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            urdfInfo.AddThemeFontSizeOverride("font_size", 16);
            urdfInfo.AddThemeColorOverride("font_color", Gold);
            calibCard.AddChild(urdfInfo);
        }

        var orientRow = new HBoxContainer();
        orientRow.AddThemeConstantOverride("separation", 12);
        calibCard.AddChild(orientRow);

        var orientBtn = Button(orientRow, $"FRONT ORIENTATION: {definition.QuarterTurns * 90}° (Godot forward = -Z)", () =>
        {
            definition.QuarterTurns = (definition.QuarterTurns + 1) % 4;
            ImportSetup();
        });
        orientBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        orientBtn.CustomMinimumSize = new(0, 44);

        // Step 3: 3D Visual Validation & Sizing Card
        bool valid = false;
        if (definition.Parts.Count > 0)
        {
            var previewCard = CreateCard(_dialogBody, "STEP 3: 3D PREVIEW & FTC 18\" SIZING");

            try
            {
                var bounds = ImportedRobot.Bounds(definition);
                previewCard.AddChild(new RobotImportPreview { Definition = definition.Copy(), CustomMinimumSize = new(450, 240) });

                var statsLabel = new Label
                {
                    Text = $"Width {bounds.Size.X * 100:0.0} × length {bounds.Size.Z * 100:0.0} × height {bounds.Size.Y * 100:0.0} cm\n{definition.Parts.Count} STL meshes • {ImportedRobot.TriangleCount(definition):N0} triangles\nKinematic chassis + {Math.Max(0, definition.Bodies.Count - 1)} dynamic links • {definition.Joints.Count} joints",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                statsLabel.AddThemeFontSizeOverride("font_size", 16);
                statsLabel.AddThemeColorOverride("font_color", TextWhite);
                previewCard.AddChild(statsLabel);

                if (bounds.Size.X > .4572f || bounds.Size.Z > .4572f)
                {
                    var warnLabel = new Label
                    {
                        Text = "Footprint exceeds 45.72 cm. Check scale / competition constraints.",
                        AutowrapMode = TextServer.AutowrapMode.WordSmart
                    };
                    warnLabel.AddThemeFontSizeOverride("font_size", 16);
                    warnLabel.AddThemeColorOverride("font_color", Red);
                    previewCard.AddChild(warnLabel);
                }

                RobotMechanismValidation.ValidateReady(definition);
                valid = true;
            }
            catch (Exception ex)
            {
                var errLabel = new Label { Text = ex.Message, AutowrapMode = TextServer.AutowrapMode.WordSmart };
                errLabel.AddThemeFontSizeOverride("font_size", 16);
                errLabel.AddThemeColorOverride("font_color", Red);
                previewCard.AddChild(errLabel);
            }

            // Subassembly list
            if (definition.Parts.Count > 0)
            {
                var partsCard = CreateCard(_dialogBody, "SUBASSEMBLIES & PARTS LIST");
                foreach (var part in definition.Parts.ToArray())
                {
                    var row = new HBoxContainer();
                    partsCard.AddChild(row);
                    var groupName = new LineEdit { Text = part.Name, MaxLength = 100, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    groupName.AddThemeFontSizeOverride("font_size", 16);
                    groupName.TextChanged += text => part.Name = text;
                    row.AddChild(groupName);
                    var remBtn = Button(row, "REMOVE", () => { definition.Parts.Remove(part); _importMessage = ""; ImportSetup(); });
                    remBtn.Disabled = _importBusy;
                }
            }
        }

        // Step 4: Articulations, Joints & Motors
        var mechCard = CreateCard(_dialogBody, "STEP 4: MECHANISMS & ARTICULATIONS");
        var mechBtn = Button(mechCard, "RIGID GROUPS / JOINTS / MOTORS", MechanismSetup);
        mechBtn.Disabled = definition.Parts.Count == 0 || _importBusy;
        mechBtn.CustomMinimumSize = new(0, 48);

        var mechHelp = new Label
        {
            Text = "Drive uses the existing kinematic chassis. Imported links use box collisions; internal self-collisions are disabled. Intake/shooting remain gameplay anchors. Joint motors: I selects, U/O moves.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        mechHelp.AddThemeFontSizeOverride("font_size", 15);
        mechHelp.AddThemeColorOverride("font_color", TextMuted);
        mechCard.AddChild(mechHelp);

        if (!string.IsNullOrEmpty(_importMessage))
        {
            var msgLabel = new Label { Text = _importMessage, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            msgLabel.AddThemeFontSizeOverride("font_size", 16);
            msgLabel.AddThemeColorOverride("font_color", Gold);
            _content.AddChild(msgLabel);
        }

        if (definition.ImportNotes.Count > 0)
        {
            var notesLabel = new Label { Text = string.Join("\n", definition.ImportNotes), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            notesLabel.AddThemeFontSizeOverride("font_size", 15);
            notesLabel.AddThemeColorOverride("font_color", TextMuted);
            _content.AddChild(notesLabel);
        }

        // Bottom Actions
        var actionsRow = new HBoxContainer();
        actionsRow.AddThemeConstantOverride("separation", 14);
        _dialogBody.AddChild(actionsRow);

        var apply = Button(actionsRow, "SAVE + TEST IN PRACTICE", () =>
        {
            try
            {
                _draft.Save();
                Game.Profile = _draft.Copy();
                Game.Practice = true;
                Game.Reset();
                _draft = null;
            }
            catch (Exception ex)
            {
                _importMessage = "Cannot apply: " + ex.Message;
                ImportSetup();
            }
        });
        apply.Disabled = !valid || _importBusy;
        apply.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        apply.CustomMinimumSize = new(0, 52);

        var addToListBtn = Button(actionsRow, "SAVE TO ROBOT LIST", () =>
        {
            try
            {
                _draft.Validate();
                RobotRoster.AddOrUpdate(_draft);
                Game.Status = $"Saved '{_draft.Name}' to robot roster!";
                NavigateTo(ScreenType.RobotRoster);
            }
            catch (Exception ex)
            {
                _importMessage = "Cannot save: " + ex.Message;
                ImportSetup();
            }
        });
        addToListBtn.Disabled = !valid || _importBusy;
        addToListBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        addToListBtn.CustomMinimumSize = new(0, 52);

        var paramBtn = Button(actionsRow, "USE PARAMETRIC ROBOT", () =>
        {
            _draft.Imported = null;
            RobotSetup();
        });
        paramBtn.Disabled = _importBusy;
        paramBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        paramBtn.CustomMinimumSize = new(0, 52);

        var backBtn = Button(actionsRow, "BACK / DISCARD", Back);
        backBtn.CustomMinimumSize = new(180, 52);
    }
    private void AddImportText(string text) => _content.AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart });
    private void Choice(Node parent, string caption, string[] values, string selected, Action<string> changed)
    {
        var row = new HBoxContainer(); parent.AddChild(row);
        var label = new Label { Text = caption, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", 16);
        row.AddChild(label);
        var choice = new OptionButton(); foreach (var value in values) choice.AddItem(value);
        choice.Selected = Array.IndexOf(values, selected);
        choice.ItemSelected += index => changed(values[(int)index]); row.AddChild(choice);
    }
    private void Choice(string caption, string[] values, string selected, Action<string> changed) => Choice(_content, caption, values, selected, changed);
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
