# FTC simulator — Godot + C#

Open `godot/project.godot` in Godot 4.7.2 .NET, build the C# project, and press F5.
This branch contains the native local 3D simulator. Pygame has been removed from
its current contents; older commits remain available in Git history.

The simulator includes a measured field, four configurable swerve-style robots,
camera-relative WASD/gamepad input, collisions, physical balls, intake, ballistic
shooting, tipping HIVE containers, hollow flowers, AprilTag visuals, aim and
trajectory feedback, practice paths, match phases, and state persistence.
Online multiplayer is outside scope. Mechanisms and scoring remain approximate;
this is not an identical port of the Turtle Sim reference.

WASD moves, Q/E turns, Shift collects, Space shoots, T switches HIVE/FLOWER,
right-drag orbits, C changes the camera, Escape pauses, and R resets.

Validation: `dotnet build` in godot/; run Godot with
`--headless --path godot -- --smoke-test` for integration checks.
Use `--audit-test` for menu, creator, snapshot, path and match-state regressions.
Use `--gravity-test` for measured gravity/ballistics and `--contact-test` for
ball impacts, near misses, rolling, spin and flower-stack settling. Contact
parameters remain estimates requiring calibration against physical balls.
Backups, personal explanations, research downloads and build caches are excluded.
