# Godot local FTC simulator

Open project.godot in Godot 4.7.2 .NET, build C#, and press F5.
WASD moves relative to the camera; Q/E turns; Shift/J collects; Space launches;
T changes HIVE/FLOWER target; H drops human-player nectar; K extracts from a
nearby flower. Right-drag orbits, wheel zooms, C changes view, R resets, Escape
pauses. In Practice, B/N adds pieces; F2 edits paths, click adds a point,
Backspace removes it, P follows. F5/F9 saves/loads the scene under user://.

Includes physical balls and hollow flowers, a tipping HIVE, configurable robots,
AprilTag visuals, match phases, paths and save/load. Rules, swerve traction and
HIVE impact coupling are approximate. Online multiplayer is excluded.

Trajectory geometry updates every rendered frame; collision prediction updates
at 20 Hz and is rechecked before firing. A green path is a prediction, not a
scoring guarantee. Graphics include MSAA 4x, metal materials, improved shadows,
procedural stars, Poppins type, and launcher detail.

Checks: dotnet build; Godot --headless --path . -- --smoke-test.
Benchmarks: --performance-test (headless), --rendered-preview-test (rendered).
Local measurements: planner ~0.06 ms versus ~11 ms before, rendered preview
~94 FPS with no origin/target lag in the moving-robot check. Results depend on
scene and hardware. Disable anti_aliasing/quality/msaa_3d in project.godot on
slower GPUs if necessary.

## Robot panel and camera

The robot panel shows four inventory slots, separate pollen/nectar clearance,
turret alignment, ballistic launch speed/time, a five-second velocity history,
HIVE load estimate, launched shots and landed balls. Yellow is pollen; purple
is nectar. This is simulation telemetry, not REV voltage or a PID measurement.
SIZE OK checks the configured footprint only, not full FTC legal compliance.
LANDED counts a launched piece once when it slows below 0.35 m/s inside a HIVE
or flower; it is not an official scoring decision.

Hold middle mouse and drag to orbit. Shift + middle-drag pans the view;
right-drag still orbits, wheel still zooms. Settings > RESET CAMERA VIEW resets
framing. Camera drag exits fixed top/robot view. Shift during pan does not
trigger intake. Shot counters survive save/load; failed loads keep pause visible.
