# Godot local FTC simulator

## Robot import: STL (manual joints) / URDF (automatic joints)

Main menu > IMPORT ROBOT (STL / URDF). For STL, select units (normally mm for Fusion)
and the source up axis (normally Z). Add one STL for the whole robot, or up
to 32 STL files exported at a common assembly origin. Keep screws and other
stationary hardware in their rigid subassembly rather than exporting each bolt.
ASCII and binary STL are supported; at most 200,000 triangles total and 32 MB
per file. Use a coarser Fusion export if the limit is exceeded.

Review the 3D preview, dimensions and front orientation; drag the preview to
orbit. Rename/remove visual groups, then SAVE + TEST IN PRACTICE. BACK discards
the draft. LOAD EXAMPLE STL provides a 40 x 30 x 25 cm box fixture. Imported
assets are copied into user://robot-assets with content hashes, so moving the
original STL does not break the saved profile. They also need to accompany a
profile when transferring it to a different computer.

RIGID GROUPS / JOINTS / MOTORS assigns meshes to bodies that move together.
Create one group per moving link, assign its STL meshes, then connect each group
to a parent with a fixed, revolute or prismatic joint. Enter the pivot in the
original common CAD frame, with the chosen STL units, and a nonzero axis vector.
Limits are relative to the imported pose (zero); UI angles are degrees and slider
travel uses the chosen STL length units. Disable limits for continuous wheels.
Configure link mass/friction, motor speed and torque/force. VALIDATE / REFRESH
MOTION PREVIEW exposes a pose slider; the preview has no gravity. Back to import,
then SAVE + TEST IN PRACTICE. I selects a motor, U/O command negative/positive
velocity; releasing the keys commands zero velocity, subject to motor effort.

For a motorized joint with motion limits, enable OPEN / CLOSE BUTTON in the
mechanism editor. Select an unused keyboard key (L is offered first), then set
closed/open positions inside the joint limits. URDF positions use the original
absolute joint coordinate; UI angles are degrees and distances use the selected
length unit. Save + apply the robot. During play, pressing the key once moves to
the open position; pressing again moves to closed, including while still moving.
The matching on-screen button performs the same action. Multiple joints can have
independent keys; BUTTON ONLY supports a clickable control without a key.

Targets use a proportional velocity controller on the existing physics motor,
respecting configured speed and torque/force limits. The motor holds the target
subject to its available effort; an obstructed or underpowered mechanism may
not reach the requested position. U/O manual movement takes over from the target.
Pause/menus/transition disable new button commands; pause and practice snapshots
preserve targets. Keyboard repeat/modifiers do not toggle repeatedly. Gameplay
keys and duplicate joint bindings are rejected. Continuous unlimited wheels use
manual velocity commands; fixed joints do not have movement controls.

Joint control checks: Godot --headless --path . -- --joint-controls-test. Covers
key/button parity, actual hinge/slider targets, reversal, repeat, pause, snapshots,
validation, manual override and imported URDF reference coordinates.
--joint-controls-demo starts an unsaved practice demo with L for the arm and M
for the lift, visible HUD buttons and a close camera. It does not save a profile.

The chassis retains the existing kinematic CharacterBody3D drive. Other groups
use actual RigidBody3D links, HingeJoint3D or Generic6DofJoint3D constraints, box
colliders and automatically approximated inertia/centre of mass. Chassis mass
is metadata; it does not affect the existing kinematic drive. Internal robot
self-collisions are disabled; links collide with the arena, pieces and other
robots. Motors control joint velocity, not physical chassis wheel propulsion.
Intake/shooting retain virtual gameplay anchors. This is a prototype, with no
CAD-calibrated inertia, transmissions, closed-loop mechanisms or actuator tuning.
Maximum 32 bodies / 31 joints, arranged as a tree rooted at chassis. Moving
groups need geometry and one incoming joint; invalid graphs cannot be applied.

LOAD ARTICULATED DEMO supplies chassis, arm, lift and fixed tool fixtures. Fusion
Joint / As-Built Joint and Motion Limits can describe the original mechanism,
but STL exports discard those settings. Keep the native Fusion design as well
as separate common-origin STL exports. Joint/body definitions are saved in the
robot profile JSON (radians/metres internally); automatic extraction from Fusion
itself is not implemented. A combined STL cannot recover separate moving parts.

IMPORT URDF + EXISTING JOINTS selects an expanded .urdf inside its original
exported package. Keep the accompanying meshes folder. Fusion has no standard
URDF option in File > Export; a separate exporter such as fusion2urdf generates
this package through Scripts and Add-Ins. Expand any xacro output to .urdf first.
No ROS installation is needed to read an already expanded URDF in this app.

The importer automatically reads links, fixed/revolute/prismatic/continuous
joints, parent/child relationships, joint origins and axes, motion limits,
velocity/effort limits, link mass, visual origins and mesh scales. STL meshes
and box/cylinder/sphere primitives are supported. Local relative mesh paths and
package:// references to the surrounding named package are supported; the package
folder name must match the URI. Imports replace only the editable draft, with
SAVE + TEST applying it. Failed imports leave the draft and active robot intact.
URDF geometry and joints are converted together from Z-up metres; unit/up-axis
fields are locked while front orientation remains editable. Manual STL import
and its joint editor continue to work independently. Use START NEW MANUAL STL
ROBOT to replace a URDF draft with a fresh manual draft.

If zero lies outside a joint's URDF limits, the initial assembly uses the closest
valid position. The editor displays absolute URDF coordinates; internal motion
is relative to that baked pose. Geometry-free fixed coordinate links are folded
into parent bodies. Geometry-free moving links, unsupported joint/mesh types,
mimic couplings and malformed/disconnected graphs are rejected explicitly.
COM/inertia tensors, material appearance, separate collision geometry when visuals
exist, dynamics/calibration/safety extensions and ROS control plugins are not
applied; import notes explain these approximations. Joint limits define motor
caps, not a complete motor model. Motors without positive speed/effort remain
disabled. Existing simulator size/body/mesh budgets and hinge range limits apply.

Example package: Assets/RobotSamples/urdf-demo/urdf/robot.urdf, with all meshes.
URDF checks: Godot --headless --path . -- --urdf-test. Covers transforms, nonzero
limits, persistent baked assets, invalid inputs, primitives, physics and async
draft ownership, without overwriting a personal robot profile.

Mechanism checks: Godot --headless --path . -- --mechanism-test. They cover graph
validation, preview pivots, real motors and both limits, pause and snapshots,
movement with the kinematic chassis and editor construction. --mechanism-demo
opens the unsaved demo configuration for visual inspection. Neither test nor
demo overwrites the personal profile; SAVE + TEST intentionally does.

Import checks: Godot --headless --path . -- --stl-test. The tests use fixture
assets and do not overwrite the personal robot profile.

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
