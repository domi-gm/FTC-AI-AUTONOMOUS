"""Pygame rendering. All field geometry enters this module in centimetres."""
import math
from pathlib import Path
import pygame
from .field import FIELD_WIDTH, FIELD_HEIGHT, TILE, CELL_OUTLINES, STRUCTURE, ZONES, STATIONS, OBSTACLES, COLORS, RED, BLUE, YELLOW
from .season import CELLS, FLOWERS
from .apriltags import APRILTAGS, TAG_IMAGE_SIZE_CM

ROOT = Path(__file__).resolve().parents[1]
BG = (18, 20, 24)
PANEL = (26, 29, 34)
BORDER = (49, 54, 62)
TEXT = (233, 236, 242)
MUTED = (139, 149, 165)
ACCENT = (241, 86, 175)
CYAN = (85, 212, 218)


class Painter:
    def __init__(self, screen):
        self.screen = screen
        self.fonts = {}
        self.reference = pygame.image.load(ROOT / "assets/biobuzz-reference.webp").convert()
        self.reference_scaled = None
        self.tag_images = {tag.id: pygame.image.load(ROOT / "assets/apriltags" / tag.image_name).convert()
                           for tag in APRILTAGS}
        self.tag_scaled = {}

    def tag_image(self, tag, size, rotate=True):
        """Nearest-neighbour scaling preserves the binary pattern and border."""
        size = max(10, round(size))
        angle = round(math.degrees(tag.cluster.layout_heading)) if rotate else 0
        key = (tag.id, size, angle)
        if key not in self.tag_scaled:
            # Bounded cache also supports repeated window resizing.
            if len(self.tag_scaled) >= 128:
                self.tag_scaled.clear()
            scaled = pygame.transform.scale(self.tag_images[tag.id], (size, size))
            self.tag_scaled[key] = pygame.transform.rotate(scaled, angle)
        return self.tag_scaled[key]

    def apriltags(self, tags, view, selected_id=None):
        """Unfolded annotation: underneath-cell tags are deliberately visible."""
        for tag in tags:
            center = view.to_screen(tag.layout_center)
            surface = self.tag_image(tag, TAG_IMAGE_SIZE_CM * view.scale)
            rect = surface.get_rect(center=center)
            self.screen.blit(surface, rect)
            if selected_id == tag.id:
                pygame.draw.rect(self.screen, CYAN, rect.inflate(6, 6), 2)
            font_size = 8 if view.scale < 1.8 else 9
            label = self.text(tag.id, (0, -100), font_size, TEXT, True)
            label_rect = pygame.Rect(round(center[0] - label.width / 2 - 1), rect.bottom + 2,
                                     label.width + 2, 15)
            self.box(label_rect, BG, radius=2)
            self.text(tag.id, (label_rect.x + 1, label_rect.y), font_size, TEXT, True)

    def text(self, value, pos, size=14, color=TEXT, bold=False):
        key = (size, bold)
        if key not in self.fonts:
            name = "Poppins-SemiBold.ttf" if bold else "Poppins-Regular.ttf"
            self.fonts[key] = pygame.font.Font(ROOT / "assets" / name, size)
        surface = self.fonts[key].render(str(value), True, color)
        self.screen.blit(surface, pos)
        return surface.get_rect(topleft=pos)

    def box(self, rect, color=PANEL, border=None, radius=8):
        pygame.draw.rect(self.screen, color, rect, border_radius=radius)
        if border:
            pygame.draw.rect(self.screen, border, rect, width=1, border_radius=radius)

    def field(self, view, grid=True, collisions=False, zones=False, reference=False):
        s = self.screen
        sc = view.to_screen
        px = lambda cm: max(1, round(cm * view.scale))
        points = lambda vertices: [sc(p) for p in vertices]

        def poly(vertices, fill, outline=None, thickness=1):
            pygame.draw.polygon(s, fill, points(vertices))
            if outline:
                pygame.draw.polygon(s, outline, points(vertices), px(thickness))

        def line(a, b, color, width=1):
            pygame.draw.line(s, color, sc(a), sc(b), px(width))

        field_rect = pygame.Rect(round(view.left), round(view.top), round(view.size), round(view.size))
        if reference:
            if self.reference_scaled is None or self.reference_scaled.get_width() != field_rect.width:
                self.reference_scaled = pygame.transform.smoothscale(self.reference, field_rect.size)
            s.blit(self.reference_scaled, field_rect)
        else:
            pygame.draw.rect(s, (43, 43, 43), field_rect)
            for i in range(1, 6):
                line((i * TILE, 0), (i * TILE, FIELD_HEIGHT), (17, 18, 19), 0.55)
                line((0, i * TILE), (FIELD_WIDTH, i * TILE), (17, 18, 19), 0.55)
            # Floor tape: traversable, independent of structural collision shapes.
            for z in ZONES:
                poly(z.vertices, (43, 43, 43), (14, 15, 18), 4.2)
                pygame.draw.lines(s, z.color, True, points(z.vertices), px(2.5))
            # Central structural diagonals and interior panels reproduce Pedro's drawing.
            for a, b in (((119, 228), (147, 181)), ((119, 132), (147, 177)),
                         ((241, 227), (212, 181)), ((241, 131), (212, 178))):
                line(a, b, (17, 18, 18), 3.8)
                line(a, b, (122, 122, 119), 2)
            for x in (124, 191):
                for y in (136, 203):
                    poly(((x, y), (x + 45, y), (x + 45, y + 17), (x, y + 17)),
                         (180, 181, 178), (83, 85, 85), 1.2)
            for x in (145, 212):
                line((x, 132), (x, 226), (89, 90, 89), 3.8)
                line((x, 132), (x, 226), (155, 156, 151), 1.8)
            for part in STRUCTURE:
                poly(part.vertices, part.color, (10, 11, 12), 1.3)
            line((145, 179), (215, 179), (16, 17, 17), 4)
            for tray in CELL_OUTLINES:
                poly(tray.vertices, (78, 80, 81), (18, 19, 20), 3)
                pygame.draw.lines(s, tray.color, True, points(tray.vertices), px(1.5))
            # Four perimeter stations; these are top-down visual markers.
            for x, y in STATIONS:
                hexagon = [(x + 6.8 * math.cos(a * math.pi / 3), y + 6.8 * math.sin(a * math.pi / 3)) for a in range(6)]
                poly(hexagon, YELLOW, (13, 14, 16), 0.9)
                pygame.draw.circle(s, (18, 19, 21), sc((x, y)), px(3.7))
                for i in range(6):
                    a = i * math.pi / 3
                    pygame.draw.circle(s, YELLOW, sc((x + 2 * math.cos(a), y + 2 * math.sin(a))), px(0.55))
            pygame.draw.rect(s, (9, 10, 12), field_rect, 2)
        if grid:
            overlay = pygame.Surface(s.get_size(), pygame.SRCALPHA)
            for n in range(0, 360, 30):
                pygame.draw.line(overlay, (170, 186, 212, 24), sc((n, 0)), sc((n, FIELD_HEIGHT)), 1)
                pygame.draw.line(overlay, (170, 186, 212, 24), sc((0, n)), sc((FIELD_WIDTH, n)), 1)
            s.blit(overlay, (0, 0))
        if zones:
            overlay = pygame.Surface(s.get_size(), pygame.SRCALPHA)
            for z in ZONES:
                pygame.draw.polygon(overlay, (*z.color, 65), points(z.vertices))
            for cell in CELLS:
                pygame.draw.polygon(overlay, (*COLORS[cell.alliance], 60), points(cell.footprint))
            s.blit(overlay, (0, 0))
        if collisions:
            for region in OBSTACLES:
                pygame.draw.polygon(s, CYAN, points(region.vertices), 2)
            pygame.draw.rect(s, CYAN, field_rect, 2)

    def entity(self, entity, view, selected=False, ghost=False, valid=True):
        if not entity.visible:
            return
        s = self.screen
        sc = view.to_screen
        center = sc(entity.body.position)
        if entity.kind == "ball":
            radius = max(3, round(entity.radius * view.scale))
            color = COLORS[entity.color]
            pygame.draw.circle(s, (11, 12, 15), (center[0] + 2, center[1] + 3), radius + 1)
            pygame.draw.circle(s, color, center, radius)
            pygame.draw.circle(s, (16, 17, 22), center, max(1, radius // 5))
            for i in range(5):
                a = i * math.tau / 5 + entity.heading
                pt = (center[0] + math.cos(a) * radius * 0.57, center[1] - math.sin(a) * radius * 0.57)
                pygame.draw.circle(s, (16, 17, 22), pt, max(1, radius // 6))
            if selected or ghost:
                pygame.draw.circle(s, CYAN if valid else RED, center, radius + 5, 2)
        else:
            def local(x, y):
                return sc(entity.body.local_to_world((x, y)))
            length, width = entity.length, entity.width
            corners = [local(x, y) for x, y in ((-length/2, -width/2), (length/2, -width/2), (length/2, width/2), (-length/2, width/2))]
            color = ACCENT if valid else RED
            pygame.draw.polygon(s, (61, 34, 53), corners)
            pygame.draw.polygon(s, color, corners, 2)
            for sign in (-1, 1):
                verts = [local(x, y) for x, y in ((-length/2, sign*width/2), (length/2, sign*width/2), (length/2, sign*(width/2-6)), (-length/2, sign*(width/2-6)))]
                pygame.draw.polygon(s, (122, 48, 91), verts)
                pygame.draw.polygon(s, color, verts, 2)
                for i in range(8):
                    x = -length/2 + i * length/8
                    pygame.draw.line(s, color, local(x, sign*width/2), local(x+length/10, sign*(width/2-6)), 1)
            pygame.draw.line(s, color, local(-length/2+6, -width/2), local(-length/2+6, width/2), 2)
            pygame.draw.line(s, TEXT, local(-length*0.15, 0), local(length*0.3, 0), 2)
            pygame.draw.lines(s, TEXT, False, [local(length*0.15, -3), local(length*0.3, 0), local(length*0.15, 3)], 2)
            if selected or ghost:
                pygame.draw.circle(s, CYAN if valid else RED, center, 4)
                verts = [local(x, y) for x, y in ((-length/2-2, -width/2-2), (length/2+2, -width/2-2), (length/2+2, width/2+2), (-length/2-2, width/2+2))]
                pygame.draw.polygon(s, CYAN if valid else RED, verts, 1)

    def storage_badges(self, world, view):
        """Top-down stacks show one silhouette plus their actual inventory count."""
        for name, point in FLOWERS.items():
            count = len(world.contents("flower:"+name))
            if not count:
                continue
            x, y = view.to_screen(point)
            dx = -35 if name == "east" else 10
            dy = 9 if name == "north" else -23
            self.box((x+dx, y+dy, 27, 19), (23, 28, 32), BORDER, 4)
            self.text(f"x{count}", (x+dx+4, y+dy), 11, YELLOW, True)
        for robot in (e for e in world.entities if e.kind == "robot"):
            count = len(world.contents(f"robot:{robot.id}"))
            x, y = view.to_screen((robot.x, robot.y))
            self.text(robot.robot_slot.upper(), (x-16, y-robot.length*view.scale/2-19), 10, ACCENT, True)
            if count:
                self.text(f"{count}P", (x+robot.width*view.scale/2+4, y-7), 10, YELLOW, True)

    def axes(self, view):
        for value in (0, 60, 120, 180, 240, 300, FIELD_WIDTH):
            label = f"{value:g}" if value < FIELD_WIDTH else "359.41"
            x, y = view.to_screen((value, 0))
            self.text(label, (x - 10, y + 9), 11, MUTED)
            x, y = view.to_screen((0, value))
            self.text(label, (x - 45, y - 7), 11, MUTED)
        origin = view.to_screen((0, 0))
        pygame.draw.circle(self.screen, CYAN, origin, 4)
        self.text("X / cm", (view.left + view.size - 38, view.top + view.size + 30), 11, CYAN)
        self.text("Y / cm", (view.left - 44, view.top - 24), 11, CYAN)
