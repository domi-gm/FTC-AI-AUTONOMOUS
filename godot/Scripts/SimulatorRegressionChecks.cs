using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Scenarii de regresie care nu scriu salvările personale ale utilizatorului.</summary>
public static class SimulatorRegressionChecks
{
    /// <summary>Măsoară corpuri reale în Jolt; formulele nu înlocuiesc simularea.</summary>
    public static async Task RunGravity(Simulation game)
    {
        try
        {
            void Check(bool valid,string text) { if (!valid) throw new Exception(text); GD.Print("PASS: "+text); }
            async Task Frames(int n) { for (int i=0;i<n;i++) await game.ToSignal(game.GetTree(),SceneTree.SignalName.PhysicsFrame); }
            game.Testing=true; game.Practice=true; game.Reset(); foreach (var r in game.Robots) r.Bot=false;
            float gravity=(float)ProjectSettings.GetSetting("physics/3d/default_gravity");
            Vector3 direction=(Vector3)ProjectSettings.GetSetting("physics/3d/default_gravity_vector");
            Check(Mathf.Abs(gravity-9.80665f)/9.80665f<.001f && direction.DistanceTo(Vector3.Down)<.00001f,
                "Project gravity is within 0.1% of standard Earth gravity and points down");
            Check(Mathf.Abs(gravity-ShotPlanner.Gravity)<.00001f,"Planner and physics use the same gravity");
            // Pornim sus, deasupra obstacolelor, păstrând proprietățile normale ale mingilor.
            var pollen=game.SpawnBall(PieceKind.Pollen,new(.6f,10,-.8f));
            var nectar=game.SpawnBall(PieceKind.RedNectar,new(.9f,10,-.8f));
            await Frames(3);
            Check(pollen.GravityScale==1 && nectar.GravityScale==1 && pollen.LinearDamp==0 && nectar.LinearDamp==0
                && pollen.LinearDampMode==RigidBody3D.DampMode.Replace && nectar.LinearDampMode==RigidBody3D.DampMode.Replace,
                "Both piece types have unit gravity scale and no linear air damping");
            var start=pollen.Position; var velocity=pollen.LinearVelocity;
            var nectarStart=nectar.Position; var nectarVelocity=nectar.LinearVelocity;
            ulong firstFrame=Engine.GetPhysicsFrames();
            int frames=Engine.PhysicsTicksPerSecond/2; await Frames(frames);
            float time=(Engine.GetPhysicsFrames()-firstFrame)/(float)Engine.PhysicsTicksPerSecond;
            float measuredPollen=(velocity.Y-pollen.LinearVelocity.Y)/time;
            float measuredNectar=(nectarVelocity.Y-nectar.LinearVelocity.Y)/time;
            float drop=start.Y-pollen.Position.Y;
            float analytic=-velocity.Y*time+gravity*time*time/2;
            float discrete=analytic+gravity*time/(2*Engine.PhysicsTicksPerSecond);
            GD.Print($"GRAVITY measured: pollen {measuredPollen:0.00000}, nectar {measuredNectar:0.00000} m/s^2; masses {pollen.Mass*1000:0.00}/{nectar.Mass*1000:0.00} g");
            GD.Print($"FALL {time:0.0000} s: measured {drop:0.00000} m; continuous {analytic:0.00000} m; discrete {discrete:0.00000} m");
            Check(Mathf.Abs(measuredPollen-gravity)<.005f && Mathf.Abs(measuredNectar-gravity)<.005f,
                "Measured free-fall acceleration matches configured gravity for both masses");
            Check(Mathf.Abs(drop-discrete)<.002f && Mathf.Abs(drop-analytic)<.025f,
                "Free-fall distance matches the numerical step and stays within 2.5 cm of continuous physics at 0.5 s");
            Check(Mathf.Abs(drop-(nectarStart.Y-nectar.Position.Y))<.0002f
                && Mathf.Abs(velocity.Y-nectarVelocity.Y)<.00001f,
                "Different ball masses fall equally in the current no-drag model");
            game.RemoveBall(pollen); game.RemoveBall(nectar);
            var dropped=game.SpawnBall(PieceKind.Pollen,new(.75f,1.03556f,-1.2f));
            ulong releaseFrame=Engine.GetPhysicsFrames(); bool bounced=false; float beforeImpact=0;
            for (int i=0;i<Engine.PhysicsTicksPerSecond;i++)
            {
                await Frames(1);
                if (dropped.LinearVelocity.Y>0) { bounced=true; break; }
                beforeImpact=-dropped.LinearVelocity.Y;
            }
            float fallTime=(Engine.GetPhysicsFrames()-releaseFrame)/(float)Engine.PhysicsTicksPerSecond;
            float expectedTime=Mathf.Sqrt(2/gravity);
            GD.Print($"DROP 1 m to floor: {fallTime:0.0000} s; continuous {expectedTime:0.0000} s; last falling speed {beforeImpact:0.000} m/s");
            Check(bounced && Mathf.Abs(fallTime-expectedTime)<2f/Engine.PhysicsTicksPerSecond,
                "A ball dropped one metre contacts the floor at the expected time within two physics steps");
            // Lansare fără obstacole: comparăm predicția cu o minge efectiv integrată.
            var launched=game.SpawnBall(PieceKind.Pollen,new(.7f,5,-.9f));
            launched.LinearVelocity=new(1,3,0); await Frames(3);
            start=launched.Position; velocity=launched.LinearVelocity; firstFrame=Engine.GetPhysicsFrames();
            await Frames(frames); time=(Engine.GetPhysicsFrames()-firstFrame)/(float)Engine.PhysicsTicksPerSecond;
            float error=launched.Position.DistanceTo(ShotPlanner.PositionAt(start,velocity,time));
            GD.Print($"BALLISTIC actual/planner error after {time:0.0000} s: {error*100:0.000} cm");
            Check(error<.002f,"Ballistic preview matches an actual flying body within 2 mm");
            GD.Print("ALL GRAVITY CHECKS PASSED"); game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("GRAVITY FAILURE: "+ex); game.GetTree().Quit(1); }
    }
    private static T Find<T>(Node root, Func<T,bool> predicate) where T:Node
    {
        if (root is T candidate && predicate(candidate)) return candidate;
        foreach (var child in root.GetChildren()) { var found=Find(child,predicate); if (found!=null) return found; }
        return null;
    }
    public static async Task Run(Simulation game)
    {
        try
        {
            void Check(bool valid,string text) { if (!valid) throw new Exception(text); GD.Print("PASS: "+text); }
            async Task Frames(int n) { for (int i=0;i<Mathf.CeilToInt(n*Engine.PhysicsTicksPerSecond/120f);i++) await game.ToSignal(game.GetTree(),SceneTree.SignalName.PhysicsFrame); }
            void Reset(bool practice=true) { game.Practice=practice; game.Reset(); foreach (var r in game.Robots) r.Bot=false; }
            game.Testing=true;
            game.Hud.ShowPause(true); Reset();
            Check(!game.Hud.MenuVisible && game.Running,"Reset closes the pause/menu overlay");
            await Frames(3);
            game.TogglePause();
            Find<Button>(game.Hud,b=>b.Text=="ROBOT SETUP").EmitSignal(Button.SignalName.Pressed);
            Check(game.Hud.CurrentScreen == SimulatorHud.ScreenType.RobotRoster, "ROBOT SETUP navigates to Robot Roster");
            game.Hud.NavigateTo(SimulatorHud.ScreenType.RobotCreator);
            var slider=Find<HSlider>(game.Hud,s=>s.MaxValue==45.72);
            float oldWidth=game.Profile.WidthCm;
            slider.Value=30;
            Check(Mathf.Abs(game.Profile.WidthCm-oldWidth)<.0001f,"Creator draft does not mutate the active profile");
            Find<Button>(game.Hud,b=>b.Text=="BACK").EmitSignal(Button.SignalName.Pressed);
            Check(Mathf.Abs(game.Profile.WidthCm-oldWidth)<.0001f,"Back discards unapplied robot edits");
            game.Hud.NavigateTo(SimulatorHud.ScreenType.RobotWorkshop);
            Check(game.Hud.CurrentScreen == SimulatorHud.ScreenType.RobotWorkshop, "Navigate to Robot Workshop");
            game.Hud.NavigateTo(SimulatorHud.ScreenType.RobotImport);
            Check(game.Hud.CurrentScreen == SimulatorHud.ScreenType.RobotImport, "Navigate to Robot Import");
            var backDiscard = Find<Button>(game.Hud, b => b.Text == "BACK / DISCARD");
            Check(backDiscard != null, "CAD importer contains BACK / DISCARD button");
            backDiscard.EmitSignal(Button.SignalName.Pressed);
            Check(game.Hud.CurrentScreen == SimulatorHud.ScreenType.RobotWorkshop, "CAD importer back button returns to Robot Workshop");
            var rwBack = Find<Button>(game.Hud, b => b.Text == "BACK [ESC]");
            Check(rwBack != null, "Robot Workshop contains BACK [ESC] button");
            rwBack.EmitSignal(Button.SignalName.Pressed);
            Check(game.Hud.CurrentScreen == SimulatorHud.ScreenType.Main, "Robot Workshop back button returns to Main menu");
            game.Player.Position = Vector3.Zero;
            game.Player.Rotation = new Vector3(0, Mathf.Pi / 2, 0);
            Vector3 orientedForward = (game.Player.GlobalBasis * new Vector3(0, 0, -1)).Normalized();
            Check(orientedForward.DistanceTo(Vector3.Left) < .001f, "Robot drives forward in its current turned orientation");
            Key oldForward = game.KeyForward;
            game.KeyForward = Key.Up;
            game.SaveControls();
            game.KeyForward = Key.W;
            game.LoadControls();
            Check(game.KeyForward == Key.Up, "Saved control bindings persist across reload");
            game.KeyForward = oldForward;
            game.SaveControls();

            game.Hud.CloseMenu();
            bool oldDriveMode = game.RobotCentricDrive;
            var driveKeyEvent = new InputEventKey { Pressed = true, Echo = false, PhysicalKeycode = game.KeyDriveMode };
            game._UnhandledInput(driveKeyEvent);
            Check(game.RobotCentricDrive != oldDriveMode, "KeyDriveMode (N) toggles RobotCentricDrive");
            game._UnhandledInput(driveKeyEvent);
            Check(game.RobotCentricDrive == oldDriveMode, "Second KeyDriveMode press toggles back");

            game.SetPlayerStartingPosition(0);
            Check(game.Player.Position.DistanceTo(Simulation.DefaultStartingPositions[0].Position) < .001f, "SetPlayerStartingPosition(0) sets Red Near position");
            game.SetPlayerStartingPosition(2);
            Check(game.Player.Position.DistanceTo(Simulation.DefaultStartingPositions[2].Position) < .001f, "SetPlayerStartingPosition(2) sets Blue Near position");

            game.Hud.NavigateTo(SimulatorHud.ScreenType.PracticePositions);
            Check(game.Hud.CurrentScreen == SimulatorHud.ScreenType.PracticePositions, "Navigate to PracticePositions screen with 3D preview");
            game.Hud.CloseMenu();

            var detached=game.Capture();
            float snapshotWidth=detached.Profile.WidthCm;
            game.Profile.WidthCm=31;
            Check(detached.Profile.WidthCm==snapshotWidth,"Captured profile is independent of later edits");
            game.Profile.WidthCm=oldWidth;
            game.Restore(detached); foreach (var r in game.Robots) r.Bot=false;
            Check(game.Running && !game.Hud.MenuVisible,"Restore leaves a running scene with no stale pause overlay");
            var invalid=game.Capture(); invalid.Tokens[0]=-1;
            var originalPlayer=game.Player; bool rejected=false;
            try { game.Restore(invalid); } catch { rejected=true; }
            Check(rejected && game.Player==originalPlayer,"Negative human-player tokens are rejected before reset");
            invalid=game.Capture(); invalid.RedSide=int.MinValue; rejected=false;
            try { game.Restore(invalid); } catch { rejected=true; }
            Check(rejected && game.Player==originalPlayer,"Invalid HIVE side cannot overflow validation or replace the scene");
            game.Camera.Mode=2; game.Camera._Process(0);
            var cameraBefore=game.Camera.Position; var front=-game.Camera.GlobalBasis.Z;
            game.Camera.Drag(Vector2.Zero,false); game.Camera._Process(0);
            Check(game.Camera.Position.DistanceTo(cameraBefore)<.03f && (-game.Camera.GlobalBasis.Z).Dot(front)>.999f,
                "Follow-to-orbit handoff preserves framing");
            game.Camera.ResetView();
            await Frames(3);
            float[][] good={new float[] {80,80},new float[] {90,80}};
            Check(game.PathEditor.ReplacePoints(good),"Path accepts open floor waypoints");
            Check(!game.PathEditor.ReplacePoints(new[] {new float[] {0,0}}) && game.PathEditor.Points.Count==2,
                "Wall waypoint is rejected without replacing a valid path");
            float frameX=(Arena.Center-24.73f*.0254f)*100;
            Check(!game.PathEditor.ReplacePoints(new[] {new float[] {frameX,Arena.Center*100}}),
                "Waypoint inside the central structure is rejected");
            game.Player.Position=new(.8f,0,-.8f);
            Check(game.PathEditor.ReplacePoints(Enumerable.Range(0,1000).Select(_=>new float[] {80,80}).ToArray()),
                "Duplicate waypoint file loads without degenerate beams");
            game.PathEditor.Following=true;
            Check(game.PathEditor.Command()==Vector3.Zero && !game.PathEditor.Following && game.PathEditor.Current==1000,
                "A thousand reached waypoints finish without recursive calls");
            game.Player.Position=new(.5f,0,-Arena.Center); game.Player.Velocity=Vector3.Zero;
            Check(game.PathEditor.ReplacePoints(new[] {new float[] {280,Arena.Center*100}}),"Destination beyond frame is valid");
            game.PathEditor.Following=true;
            for (int i=0;i<720 && game.PathEditor.Following;i++)
            { game.Player.Command=game.PathEditor.Command(); await Frames(1); }
            Check(!game.PathEditor.Following && game.Status.Contains("blocked") && game.Player.Position.X<1.1f,
                "Follower stops and explains a physical obstruction");
            game.PathEditor.ReplacePoints(Array.Empty<float[]>());
            Reset(); await Frames(3);
            game.Player.Position=new(game.Player.Width/2-.01f,0,-.8f);
            game.Player.Velocity=Vector3.Zero; game.Player.Command=Vector3.Zero;
            await Frames(3);
            Check(game.Player.Position.X>=game.Player.Width/2-.001f,
                "Stationary robot still recovers wall penetration after teleporting");
            game.Player.Position=new(game.Player.Width/2+.008f,0,-.8f);
            game.Player.Rotation=Vector3.Zero; game.Player.TurnCommand=1;
            await Frames(60);
            float support=game.Player.Width/2*Mathf.Abs(Mathf.Cos(game.Player.Rotation.Y))
                +game.Player.Length/2*Mathf.Abs(Mathf.Sin(game.Player.Rotation.Y));
            Check(game.Player.Position.X-support>-.004f && Mathf.Abs(game.Player.Rotation.Y)<.2f,
                "Rotation candidates are blocked before the chassis turns through a wall");
            Reset(); await Frames(3);
            game.Player.Position=new(.8f,0,-.9f); game.Player.Intake=false;
            var pushed=game.SpawnBall(PieceKind.Pollen,game.Player.Position+new Vector3(game.Player.Width/2+.10f,.03556f,0));
            float beforePush=pushed.Position.X; game.Player.Command=Vector3.Right; await Frames(60);
            Check(pushed.Position.X>beforePush+.10f,"Driving chassis pushes a loose floor ball with intake off");
            game.Player.Command=Vector3.Zero; game.Player.ExcludeLauncher(pushed);
            Check(pushed.GetCollisionExceptions().Contains(game.Player),"Launch temporarily excludes its own chassis");
            await Frames(30);
            Check(!pushed.GetCollisionExceptions().Contains(game.Player),"Launch exclusion expires so the same robot can push that ball again");
            Reset(false); await Frames(3);
            game.Elapsed=30-.5f/Engine.PhysicsTicksPerSecond; game.Player.Velocity=Vector3.Right; game.Player.Command=Vector3.Right;
            game.Player.FireCommand=true;
            int shots=game.Player.ShotsFired; await Frames(3);
            game.Player.Fire();
            Check(!game.DrivingAllowed && game.Player.Velocity==Vector3.Zero && game.Player.ShotsFired==shots,
                "Transition stops driving and rejects direct firing");
            game.Elapsed=37.995f; await Frames(3);
            Check(game.DrivingAllowed,"Teleop re-enables driving after transition");
            var ai=game.Robots[1]; ai.Bot=true; ai.Position=new(.8f,0,-.9f); var aiStart=ai.Position;
            await Frames(60);
            Check(ai.IsPhysicsProcessing() && ai.Position.DistanceTo(aiStart)>.10f,"Opponent AI resumes driving during teleop after the transition");
            game.Elapsed=157.995f; await Frames(3);
            Check(game.Elapsed==158 && game.Finished && game.Balls.Where(b=>!b.Stored).All(b=>b.Freeze),
                "Match ends at 158 seconds with frozen physics");
            game.TogglePause(); await Frames(3);
            Check(!game.Running && !game.Paused && game.Balls.Where(b=>!b.Stored).All(b=>b.Freeze),
                "Escape cannot reactivate physics after the final result");
            var finished=game.Capture(); game.Restore(finished); await Frames(3);
            Check(game.Finished && !game.Running && game.Hud.MenuVisible && game.Balls.Where(b=>!b.Stored).All(b=>b.Freeze),
                "Loading a finished match preserves the final result and freeze");

            // AI Training Ground: floor balls, inventory loadout, standby start, and 30s auto-looping
            game.TrainingGround = true;
            game.TrainingLoopActive = false;
            game.TrainingInventoryConfigured = true;
            game.TrainingInventoryPollen = 2;
            game.TrainingInventoryNectar = 1;
            game.TrainingFloorBallsConfigured = true;
            game.TrainingBallsPreset = "sparse";
            game.Reset(); await Frames(3);
            Check(game.Player.Inventory.Count == 3 && game.Player.Inventory.Count(k => k == PieceKind.Pollen) == 2,
                "AI Training Ground applies configured robot inventory (2 pollen, 1 nectar)");
            Check(game.Balls.Count(game.IsFloorBall) == 12,
                "AI Training Ground persists sparse floor balls preset (12 balls)");
            Check(!game.TrainingLoopActive && game.Countdown == 0,
                "AI Training Ground does not auto-start autonomous sequence on entry (standby)");

            game.TrainingBallsPreset = "clear";
            game.Reset(); await Frames(3);
            Check(game.Balls.Count(game.IsFloorBall) == 0,
                "AI Training Ground persists clear floor balls preset (0 balls)");

            game.TrainingBallsPreset = "full";
            game.Reset(); await Frames(3);
            Check(game.Balls.Count(game.IsFloorBall) == 36,
                "AI Training Ground persists full floor balls preset (36 balls)");

            game.TrainingBallsPreset = "ftc";
            game.Reset(); await Frames(3);
            Check(game.Balls.Count(game.IsFloorBall) == 8,
                "AI Training Ground persists official FTC floor balls preset (8 balls)");

            game.SetFieldBallsPreset("official"); await Frames(3);
            Check(game.Balls.Count(game.IsFloorBall) == 8,
                "SetFieldBallsPreset('official') configures 8 perimeter balls");

            game.StartTrainingDrill();
            Check(game.TrainingLoopActive, "Start button activates 30s training drill");
            Check(!game.Player.Bot, "Player robot does not run bot autopilot in training ground");

            game.SetTimeMultiplier(3.0f);
            Check(Mathf.IsEqualApprox(game.TrainingTimeMultiplier, 3.0f) && Mathf.IsEqualApprox((float)Engine.TimeScale, 3.0f),
                "Time multiplier applies to TrainingTimeMultiplier and Engine.TimeScale");
            game.SetTimeMultiplier(1.0f);
            Engine.TimeScale = 1.0f;

            game.Countdown = 0;
            game.Elapsed = 30.01f;
            await Frames(3);
            Check(game.TrainingLoopActive && game.Elapsed < 1.0f && !game.Finished,
                "AI Training Ground loops back around at 30 seconds without showing results screen");

            game.StopTrainingDrill();
            Check(!game.TrainingLoopActive, "Stop button returns training ground to standby");

            // Autonomous Paths Roster, Presets, and Waypoint Actions
            var defaultPaths = AutonomousPathRoster.GetPaths();
            Check(defaultPaths.Count >= 4, "Autonomous path roster initializes with at least 4 default FTC routines");
            var rnPath = AutonomousPathRoster.GetPathByName("Red Near 3-Piece Hive Cycle");
            Check(rnPath != null && rnPath.StartingPosition == "Red Near", "Red Near preset routine exists with correct start position");
            Check(rnPath.Waypoints.Any(w => w.Action == WaypointAction.Shoot), "Autonomous path includes Shoot waypoint action");
            Check(rnPath.Waypoints.Any(w => w.Action == WaypointAction.IntakeOn), "Autonomous path includes IntakeOn waypoint action");

            // Autonomous Path Definition Copy & Deep Clone
            var clonedPath = rnPath.Copy();
            Check(clonedPath.Waypoints.Count == rnPath.Waypoints.Count && clonedPath.Id == rnPath.Id, "Autonomous path Copy creates valid clone");
            clonedPath.Waypoints[0].X = 2.5f;
            Check(rnPath.Waypoints[0].X != 2.5f, "Autonomous path Copy is a deep clone of waypoints");

            // Autonomous Path Slot Assignment & Persistence
            game.AssignedAutonomousPaths[0] = "Red Near 3-Piece Hive Cycle";
            game.AssignedAutonomousPaths[1] = "Red Far Wing Intake & Hive";
            game.AssignedAutonomousPaths[2] = "Blue Near Audience Hive Cycle";
            game.AssignedAutonomousPaths[3] = "Blue Far Backstage Cross & Hive";
            game.SaveSettings();
            game.AssignedAutonomousPaths[0] = "";
            game.LoadSettings();
            Check(game.AssignedAutonomousPaths[0] == "Red Near 3-Piece Hive Cycle",
                "Assigned autonomous paths are persisted to settings.json across sessions");

            // Autonomous Path Runner Execution & Multi-Robot Coordination
            game.TrainingRobotCount = 4;
            game.TrainingSolo = false;
            game.Reset(); await Frames(3);
            Check(game.Robots.Count == 4, "AI Training Ground spawns 4 robots for multi-robot drills");

            game.StartTrainingDrill();
            Check(game.PathRunners[0].IsRunning && game.PathRunners[1].IsRunning,
                "StartTrainingDrill activates AutonomousPathRunners for configured robots");
            Check(game.IsPathRunningForRobot(game.Robots[1]),
                "IsPathRunningForRobot correctly identifies autonomous bots running paths");

            // Runner Waypoint Action Handling (Intake & Driving)
            var testPath = new AutonomousPathDefinition
            {
                Name = "Test Routine",
                StartingPosition = "Red Near",
                Waypoints = new System.Collections.Generic.List<AutonomousWaypoint>
                {
                    new() { X = game.Robots[1].Position.X, Z = game.Robots[1].Position.Z, Action = WaypointAction.IntakeOn },
                    new() { X = game.Robots[1].Position.X + 0.5f, Z = game.Robots[1].Position.Z, Action = WaypointAction.Wait, ActionParam = 0.2f }
                }
            };
            var testRunner = new AutonomousPathRunner(game.Robots[1]);
            testRunner.Start(testPath);
            testRunner.Update(0.02);
            Check(game.Robots[1].Intake, "AutonomousPathRunner executes IntakeOn waypoint action on target robot");

            game.StopTrainingDrill();
            Check(!game.PathRunners[0].IsRunning && !game.PathRunners[1].IsRunning,
                "StopTrainingDrill terminates all active autonomous path runners");

            // PedroPathVisualizer2D Simulation & Coordinate Mapping Checks
            var visualizer = new PedroPathVisualizer2D
            {
                Path = testPath,
                Size = new Vector2(400, 400)
            };
            game.AddChild(visualizer);
            await Frames(2);

            Vector2 canvasP = visualizer.WorldToCanvas(1.5f, -1.5f);
            Vector2 roundTrip = visualizer.CanvasToWorld(canvasP);
            Check(Mathf.Abs(roundTrip.X - 1.5f) < 0.05f && Mathf.Abs(roundTrip.Y - (-1.5f)) < 0.05f,
                "PedroPathVisualizer2D converts between world and 2D canvas coordinates accurately");

            visualizer.StartSimulation();
            Check(visualizer.IsSimulating && !visualizer.IsPaused,
                "PedroPathVisualizer2D starts 2D simulation along waypoints");

            visualizer._Process(0.1);
            Check(visualizer.SimElapsed > 0.05f,
                "PedroPathVisualizer2D updates 2D robot position and timers during simulation");

            visualizer.TogglePauseSimulation();
            Check(visualizer.IsPaused, "PedroPathVisualizer2D toggles pause during simulation");

            visualizer.ResetSimulation();
            Check(!visualizer.IsSimulating && visualizer.SimElapsed == 0f,
                "PedroPathVisualizer2D resets simulation back to initial waypoint");

            game.RemoveChild(visualizer);
            visualizer.QueueFree();

            // Autonomous Path Editor UI dialog sizing checks
            game.Hud.NavigateTo(SimulatorHud.ScreenType.AutonomousPathEditor);
            await Frames(2);
            var editorVisualizer = Find<PedroPathVisualizer2D>(game.Hud, v => v != null);
            Check(editorVisualizer != null, "Autonomous Path Editor instantiates PedroPathVisualizer2D");
            Check(editorVisualizer.CustomMinimumSize.X <= 320 && editorVisualizer.CustomMinimumSize.Y <= 320,
                "PedroPathVisualizer2D custom minimum size is compact and fits within dialog bounds");
            var simRunBtn = Find<Button>(game.Hud, b => b.Text.Contains("SIMULATE PATH"));
            Check(simRunBtn != null, "Autonomous Path Editor contains TEST RUN SIMULATE PATH button");

            var testOnFieldBtn = Find<Button>(game.Hud, b => b.Text == "TEST ON FIELD");
            Check(testOnFieldBtn != null, "Autonomous Path Editor contains TEST ON FIELD button");
            testOnFieldBtn.EmitSignal(Button.SignalName.Pressed);
            await Frames(2);

            Check(game.Running && !game.Paused, "TEST ON FIELD starts simulation without pause");
            Check(game.Countdown == 0f, "TEST ON FIELD starts immediately with 0s countdown");
            Check(game.TrainingLoopActive, "TEST ON FIELD activates 30s training loop drill");
            Check(game.PathRunners[0].IsRunning, "TEST ON FIELD starts autonomous path runner for robot");
            game.StopTrainingDrill();

            GD.Print("ALL AUDIT REGRESSIONS PASSED"); game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("AUDIT FAILURE: "+ex); game.GetTree().Quit(1); }
    }
}
