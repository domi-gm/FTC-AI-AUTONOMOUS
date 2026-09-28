"""BIOBUZZ AprilTag identities and a schematic metric layout.

IDs, ordering, family and sticker offsets: manual TU02, figures 9-15/9-17.
The XY anchors come from the existing Pedro-calibrated CELLS. They are NOT
calibrated 3D tag poses: the stickers are underneath moving cells. No camera
visibility or localization may be inferred from this unfolded editor overlay.
"""
from dataclasses import dataclass
import math

from .season import CELLS

TAG_FAMILY = "36h11"
TAG_SIZE_CM = 3.25 * 2.54  # Black square; manual rounds this to 8.25 cm.
# Native AprilRobotics images: 8 black-border cells + 1 white cell each side.
TAG_IMAGE_SIZE_CM = TAG_SIZE_CM * 10 / 8
LOCAL_OFFSETS_CM = tuple(inches * 2.54 for inches in (-6.5, -2.75, 2.75, 6.5))


@dataclass(frozen=True)
class TagCluster:
    cell_key: str
    label: str
    ids: tuple[int, ...]
    layout_heading: float  # Rotation of the unfolded glyphs, not a 3D normal.
    # Editor annotation offset leaves the preloaded balls visible. Not CAD.
    layout_offset: tuple[float, float] = (0.0, -10.0)

    @property
    def cell(self):
        return next(cell for cell in CELLS if cell.key == self.cell_key)


@dataclass(frozen=True)
class AprilTag:
    id: int
    cluster: TagCluster
    local_u_cm: float

    @property
    def layout_center(self):
        x, y = self.cluster.cell.center
        x += self.cluster.layout_offset[0]
        y += self.cluster.layout_offset[1]
        angle = self.cluster.layout_heading
        return x + self.local_u_cm * math.cos(angle), y + self.local_u_cm * math.sin(angle)

    @property
    def image_name(self):
        return f"tag36_11_{self.id:05}.png"

    def contains_layout_point(self, point):
        x, y = self.layout_center
        dx, dy = point[0] - x, point[1] - y
        c, s = math.cos(self.cluster.layout_heading), math.sin(self.cluster.layout_heading)
        half = TAG_IMAGE_SIZE_CM / 2
        return abs(c * dx + s * dy) <= half and abs(-s * dx + c * dy) <= half


# Audience = bottom of the Pedro image. Far/scoring side = top.
# Figure 9-17, left-to-right: 33 32 31 30 / 45 44 43 42 at the top;
# 34 35 36 37 / 38 39 40 41 at the bottom.
APRILTAG_CLUSTERS = (
    TagCluster("red:back", "RED SCORING", (30, 31, 32, 33), math.pi),
    TagCluster("red:front", "RED AUDIENCE", (34, 35, 36, 37), 0.0),
    TagCluster("blue:front", "BLUE AUDIENCE", (38, 39, 40, 41), 0.0),
    TagCluster("blue:back", "BLUE SCORING", (42, 43, 44, 45), math.pi),
)
APRILTAGS = tuple(AprilTag(tag_id, cluster, offset)
                 for cluster in APRILTAG_CLUSTERS
                 for tag_id, offset in zip(cluster.ids, LOCAL_OFFSETS_CM))


def tag_by_id(tag_id):
    return next((tag for tag in APRILTAGS if tag.id == tag_id), None)
