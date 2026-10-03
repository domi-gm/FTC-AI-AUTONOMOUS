# FTC-AI-AUTONOMOUS

FTC Field + Visuals editor built with Pygame and Pymunk. Includes the BIOBUZZ field, robot and game-piece placement, inventory, scene saving, and collision-checked dragging. World coordinates are in centimetres with the origin at the bottom-left.

The 16 BIOBUZZ AprilTags are shown by default. Press `T` to toggle them and click a tag in selection mode (`V`) to inspect its ID, pattern and cell. Tag positions are a schematic 2D overlay, not calibrated 3D camera poses or simulated detections.

## Run

Python 3.12+:

```sh
python -m pip install -r requirements.txt
python main.py
```

On Windows, you can also run `Porneste Simulator.bat` after installing dependencies.

## Tests

```sh
python -m unittest discover -s tests -v
```

This version is a field editor; it does not include a Pedro Pathing follower or live robot driving. Field geometry is calibrated from the Pedro visual reference, not certified CAD dimensions.

Third-party asset licenses and attribution are included in `assets/`.

## Godot + C# simulator

Open `godot/project.godot` in Godot 4.7.2 .NET, build, and press F5.
The native local 3D simulator includes the field, four swerve-style robots,
camera-relative WASD/gamepad input, physical balls, intake and ballistic shooting,
tipping HIVE containers, hollow flowers, AprilTag visuals, a live aiming panel,
practice paths, robot setup, match phases, and scene save/load. Online multiplayer
is outside the project scope. Physics, scoring, and robot mechanisms remain
approximations of the Turtle Sim reference; this is not an identical port.

WASD moves, Q/E turns, Shift collects, Space shoots, T switches HIVE/FLOWER,
right-drag orbits, C changes the camera, Escape pauses, and R resets.
Integration checks: run Godot with `--headless --path godot -- --smoke-test`.
