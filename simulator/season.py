"""BIOBUZZ TU02 setup, checked 2026-09-27. See docs/BIOBUZZ_RESEARCH.md.

Counts and nominal diameters: manual 9.8 / 10.3.1. XY coordinates follow
Pedro's 141.5-inch reference, not a CAD extraction. Contained pieces have
individual IDs and slots instead of overlapping floor collision bodies.
"""
from dataclasses import dataclass
from .field import FIELD_WIDTH, FIELD_HEIGHT, STATIONS

POLLEN_RADIUS = 7.1 / 2
NECTAR_RADIUS = 9.1 / 2
PIECE_RADII = {"yellow": POLLEN_RADIUS, "red": NECTAR_RADIUS, "blue": NECTAR_RADIUS}
# AndyMark product specifications, converted from pounds to kilograms.
PIECE_MASSES = {"yellow": 0.055 * 0.45359237, "red": 0.091 * 0.45359237, "blue": 0.091 * 0.45359237}
FLOWERS = dict(zip(("north", "east", "west", "south"), STATIONS))
ROBOT_SLOTS = ("red1", "red2", "blue1", "blue2")
HIVE_INITIAL_UP = {"red": "front", "blue": "back"}
HIVE_CLEARANCE_CM = 77.7  # TU01 correction, not the opening height
HIVE_PIVOT_CM = 111.65
FLOWER_OPENING_HEIGHT_CM = 54.6
CELL_OPENING_CM = (50.8, 35.6)


@dataclass(frozen=True)
class Cell:
    key: str
    alliance: str
    end: str
    center: tuple[float, float]
    footprint: tuple[tuple[float, float], ...]


CELLS = (
    Cell("red:back", "red", "back", (147, 220), ((121, 199), (173, 199), (173, 235), (148, 243), (121, 235))),
    Cell("red:front", "red", "front", (147, 153), ((121, 131), (173, 131), (173, 166), (147, 175), (121, 166))),
    Cell("blue:back", "blue", "back", (213, 195), ((187, 217), (239, 217), (239, 181), (213, 173), (187, 181))),
    Cell("blue:front", "blue", "front", (213, 127), ((187, 149), (239, 149), (239, 113), (213, 105), (187, 113))),
)


def storage_label(location):
    if location == "ground":
        return "Podea"
    kind, name = location.split(":", 1)
    return {"flower": "FLOWER ", "hive": "CELL ", "robot": "Robot #",
            "reserve": "Rezerva NECTAR ", "preload": "Preload virtual "}[kind] + name


def ground_setup():
    r = POLLEN_RADIUS
    for i in range(4):
        x, y = r + 0.3 + i * (2*r + 0.05), r + 0.3
        yield x, y, "yellow"
        yield FIELD_WIDTH-x, FIELD_HEIGHT-y, "yellow"


def hive_setup():
    for x in (126.7, 135.9, 145.1):
        yield x, 155.2, "red", "hive:red:front"
    for x in (216.5, 225.7, 234.9):
        yield x, 204.3, "blue", "hive:blue:back"
