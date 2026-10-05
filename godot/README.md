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
Local measurements: planner ~0.06 ms versus ~11 ms before. The rendered preview
check measured ~100 FPS with no origin/target lag after removing an extra frame
wait in its timing loop. Results depend on scene and hardware.
Disable anti_aliasing/quality/msaa_3d in project.godot on
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
drop takes about 0.4516 s versus 0.4515 s analytically. At 1920 Hz, numerical
integration adds about 1.28 mm of fall after 0.5 s; the planner accounts for
this step. Air drag is absent and contact coefficients remain uncalibrated.

## Ball contacts

PieceContactModel centralizes provisional contact parameters. Each ball contributes
0.14 bounce, giving 0.28 for ball-ball and ball-floor contacts. Other surfaces
retain their default zero contribution. Sliding uses Jolt friction; rolling and
vertical spin receive resistance only on static supporting surfaces. Airborne
rotation has no hidden damping. Inertia approximates a thin spherical shell.
These parameters need real drop, roll and impact measurements before claiming
physical calibration; hole geometry and plastic deformation are absent.

Physics runs at 1920 Hz with 16 velocity and 4 position iterations, a 1 mm
penetration tolerance, and tighter CCD settings. This costs more CPU and was
selected after tests of opposite maximum-speed balls and a near miss with 5 mm
clearance. Match time uses double precision; blocked-motion thresholds use speed
so they remain independent of physics frequency.

Contact regressions: Godot --headless --path . -- --contact-test. Its 21 checks
cover different masses, restitution, momentum/energy, 17 fast-impact phases,
near misses, glancing spin, rolling/spin settling, flower stacks and floor bounce.

## Gameplay performance

Contacts and moving-body sweeps retain 1920 Hz. Input, bot decisions, intake,
container observations, score and HIVE load sampling run at 120 Hz; HIVE angle
integration retains the physics rate. Wheel/turret visuals update once per
rendered frame, with aim and clearance checked again immediately before firing.
Stationary robots skip redundant sweeps but still recover after teleporting.
Rotation candidates are checked before changing the body; unchanged HIVE angles
do not invalidate child transforms. Gameplay observations can lag up to 8.33 ms.

Run Godot --path . -- --fps-test for a rendered benchmark: one-second warmup and
four-second samples for rest, driving and four bots. It disables VSync and the
FPS cap in that test process only and reports FPS, frame p95/p99, the sampled
physics monitor, simulated/wall time and managed allocations. It writes no saves.
At 1280x800 on the tested RTX 3060, baseline/final were ~113/329 FPS at rest,
~97/189 while driving and ~39/86 with four bots. These are short-scenario results,
not guaranteed frame rates; bots still had p99 frames around 40 ms. Audit coverage
includes stationary recovery and blocking rotation at the perimeter wall.

## Shot readiness and narrow approaches

SHOT now reflects current 20 Hz aim validation rather than retaining a previous
blocked attempt. LastShotAttempt and LastShotDiagnosis preserve attempt history
for the bottom telemetry tooltip. TURRET ALIGNED means orientation only; use
the separate POLLEN/NECTAR CLEAR PATH badges for ballistic clearance. Empty
inventory, launch-point overlap, target overlap, speed limits and path obstacles
have distinct diagnostics. Inventory-count changes refresh the selected launcher
even when the next ball has the same type.

After the original 51 flight-time samples at 25 ms spacing fail, the planner
refines up to 16 promising intervals at 5 ms spacing (64 extra candidates).
Sphere sweeps, endpoint overlaps and safety margins remain active. A standard
robot at field (160,250) cm can now launch pollen on the previously missed
0.530 s approach into the red HIVE; this is checked with a physical landing.
The finite search can still miss narrower windows and does not predict moving
obstacles. Live aim may lag movement by about 50 ms; firing always revalidates.

Godot --headless --path . -- --shot-test runs 11 checks for readiness, inventory,
diagnostics, refined physical scoring and multi-launcher changes. The tested
uncapped benchmark measured ~214 FPS driving and ~95 with four bots, with p99
bot frames around 59 ms; refinement adds cost to blocked attempts.
