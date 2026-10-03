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

## State and path validation

Robot Creator edits a temporary profile; BACK discards changes, SAVE + APPLY
saves and restarts. Reset/load closes stale pause menus. Finished matches stay
frozen at 158 seconds, including after loading or pressing Escape. The transition
phase stops robot commands while allowing already airborne pieces to fall.

Practice paths validate the robot footprint against static obstacles at every
waypoint and accept at most 1000 points. Invalid loads preserve the old path.
Duplicate reached points are skipped iteratively; zero-length segments are
not drawn. Following stops with a status message after two seconds of blocked
movement. Valid waypoints do not guarantee a clear segment between them; this
follower does not plan around obstacles. Camera drag from robot view initializes
orbit from its actual framing. Long status messages wrap; hover for full text.

Additional regressions: Godot --headless --path . -- --audit-test. Covers menu
state, draft edits, snapshot ownership/validation, camera handoff, paths, match
transition and finished matches without overwriting personal save files.

Gravity check: Godot --headless --path . -- --gravity-test. Measures both ball
types in Jolt, a one-metre drop onto the real floor, and ballistic prediction
against a flying body. Configured/measured gravity is 9.81 m/s^2; a one-metre
drop takes about 0.450 s versus 0.4515 s analytically. At 120 Hz, numerical
integration adds about 2.04 cm of fall after 0.5 s; the planner accounts for
this step. Air drag is absent and bounce/friction remain uncalibrated.
