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
