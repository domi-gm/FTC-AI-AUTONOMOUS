"""World geometry in centimetres. No renderer or screen coordinates here.

Field extent matches Pedro's 141.5 inch coordinate system. Structure footprints
are calibrated against biobuzz.webp, not certified CAD dimensions. Vertical
clearance is approximated by ground feet; elevated pieces live in containers.
"""
from dataclasses import dataclass

FIELD_WIDTH = 359.41
FIELD_HEIGHT = 359.41
TILE = FIELD_WIDTH / 6
BALL_RADIUS = 3.55  # POLLEN nominal; NECTAR uses 4.55 cm (season.py)
RED = (240, 49, 68)
BLUE = (83, 90, 222)
YELLOW = (255, 200, 75)
COLORS = {"yellow": YELLOW, "red": RED, "blue": BLUE}


@dataclass(frozen=True)
class Region:
    name: str
    vertices: tuple[tuple[float, float], ...]
    color: tuple[int, int, int]
    kind: str


def rectangle(x, y, width, height):
    return ((x, y), (x + width, y), (x + width, y + height), (x, y + height))


def beam(a, b, width):
    import math
    dx, dy = b[0] - a[0], b[1] - a[1]
    norm = math.hypot(dx, dy)
    ox, oy = -dy / norm * width / 2, dx / norm * width / 2
    return ((a[0]+ox, a[1]+oy), (b[0]+ox, b[1]+oy),
            (b[0]-ox, b[1]-oy), (a[0]-ox, a[1]-oy))


def contains(vertices, point):
    """Point-in-polygon for arbitrary simple polygons (world coordinates)."""
    x, y = point
    inside = False
    j = len(vertices) - 1
    for i, (xi, yi) in enumerate(vertices):
        xj, yj = vertices[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return inside


# These coordinates are explicit metric geometry, editable independently of pixels.
# Eight drawing outlines describe FOUR cells on TWO hives, not eight trays.
CELL_OUTLINES = (
    Region("Red upper outer", ((121, 224), (173, 224), (173, 235), (148, 243), (121, 235)), RED, "scoring"),
    Region("Red upper inner", ((121, 199), (173, 199), (173, 210), (147, 218), (121, 210)), RED, "scoring"),
    Region("Red lower inner", ((121, 155), (173, 155), (173, 166), (147, 175), (121, 166)), RED, "scoring"),
    Region("Red lower outer", ((121, 131), (173, 131), (173, 142), (147, 151), (121, 142)), RED, "scoring"),
    Region("Blue upper outer", ((187, 217), (239, 217), (239, 206), (213, 198), (187, 206)), BLUE, "scoring"),
    Region("Blue upper inner", ((187, 192), (239, 192), (239, 181), (213, 173), (187, 181)), BLUE, "scoring"),
    Region("Blue lower inner", ((187, 149), (239, 149), (239, 138), (213, 130), (187, 138)), BLUE, "scoring"),
    Region("Blue lower outer", ((187, 124), (239, 124), (239, 113), (213, 105), (187, 113)), BLUE, "scoring"),
)
STRUCTURE = (
    Region("West frame", rectangle(116.5, 130, 4, 99), (115, 118, 119), "solid"),
    Region("East frame", rectangle(239.5, 130, 4, 99), (115, 118, 119), "solid"),
    Region("Upper bridge", rectangle(149, 183, 61, 7), YELLOW, "solid"),
    Region("Lower bridge", rectangle(149, 169, 61, 7), YELLOW, "solid"),
    Region("Upper west brace", beam((119, 228), (147, 181), 2), (122, 122, 119), "solid"),
    Region("Lower west brace", beam((119, 132), (147, 177), 2), (122, 122, 119), "solid"),
    Region("Upper east brace", beam((241, 227), (212, 181), 2), (122, 122, 119), "solid"),
    Region("Lower east brace", beam((241, 131), (212, 178), 2), (122, 122, 119), "solid"),
    Region("West tray support", rectangle(143.1, 132, 3.8, 94), (122, 122, 119), "solid"),
    Region("East tray support", rectangle(210.1, 132, 3.8, 94), (122, 122, 119), "solid"),
    Region("Center crossbar", rectangle(145, 177, 70, 4), (65, 67, 67), "solid"),
)
ZONES = (
    Region("Red loading", rectangle(0, TILE * 4, 27.95, TILE), RED, "loading"),
    Region("Blue loading", rectangle(FIELD_WIDTH - 27.95, TILE, 27.95, TILE), BLUE, "loading"),
    Region("Red garden", rectangle(0, 0, TILE, 5.1), RED, "garden"),
    Region("Blue garden", rectangle(TILE * 5, FIELD_HEIGHT - 5.1, TILE, 5.1), BLUE, "garden"),
)
STATIONS = ((TILE * 2, FIELD_HEIGHT - 5), (FIELD_WIDTH - 5, TILE * 4),
            (5, TILE * 2), (TILE * 4, 5))
# Conservative frame feet. Elevated cells/crossbar do not close the floor.
# This is a low-profile envelope; exact slanted-brace clearance needs 3D CAD.
GROUND_FRAME = STRUCTURE[:2]
FLOWER_BASES = tuple(Region(f"Flower base {i}", rectangle(x-4.6, y-4.6, 9.2, 9.2),
                           YELLOW, "solid") for i, (x, y) in enumerate(STATIONS))
OBSTACLES = GROUND_FRAME + FLOWER_BASES


def zone_at(point):
    return next((z.name for z in ZONES if contains(z.vertices, point)), None)


@dataclass
class Viewport:
    """Only world/screen conversion boundary. Y grows upward in the world."""
    left: float
    top: float
    size: float

    @property
    def scale(self):
        return self.size / FIELD_WIDTH

    def to_screen(self, point):
        x, y = point
        return self.left + x * self.scale, self.top + (FIELD_HEIGHT - y) * self.scale

    def to_world(self, point):
        x, y = point
        return (x - self.left) / self.scale, FIELD_HEIGHT - (y - self.top) / self.scale
