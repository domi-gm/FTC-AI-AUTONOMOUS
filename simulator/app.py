"""Field and visuals editor. No robot controller or physics stepping."""
import argparse
import copy
import math
import os
from pathlib import Path
import pygame
from .field import FIELD_WIDTH, FIELD_HEIGHT, BALL_RADIUS, Viewport, COLORS, zone_at
from .world import World, initial_world
from .season import PIECE_RADII, FLOWERS, CELLS, ROBOT_SLOTS, storage_label
from .render import Painter, ROOT, BG, PANEL, BORDER, TEXT, MUTED, ACCENT, CYAN, RED, BLUE, YELLOW


class App:
    def __init__(self, size=None):
        pygame.init()
        pygame.display.set_caption("Patric | FTC Field Studio - BIOBUZZ")
        if size is None:
            desktop = pygame.display.get_desktop_sizes()[0]
            size = (min(1440, max(1100, desktop[0]-80)), min(960, max(760, desktop[1]-100)))
        self.screen = pygame.display.set_mode(size, pygame.RESIZABLE)
        self.painter = Painter(self.screen)
        self.world = initial_world()
        self.selected_id = self.world.robot.id
        self.mode = "select"
        self.alive = True
        self.grid = False
        self.collisions = False
        self.zones = False
        self.reference = False
        self.inventory_open = False
        self.buttons = []
        self.inputs = {}
        self.active_input = None
        self.input_text = ""
        self.dragging = False
        self.drag_offset = (0, 0)
        self.drag_before = None
        self.history = []
        self.future = []
        self.measure = []
        self.message = "Start Pedro: 56 mingi in model. I = inventar; mingile suprapuse sunt marcate x4."
        self.scene_path = ROOT / "scene.json"
        self.list_offset = 0
        self.layout()

    @property
    def selected(self):
        return self.world.by_id(self.selected_id)

    def layout(self):
        w, h = self.screen.get_size()
        self.left_width = 214
        self.right_width = 286
        self.right_x = w - self.right_width
        available_width = self.right_x - self.left_width - 83
        available_height = h - 218
        size = max(150, min(available_width, available_height))
        left = self.left_width + 56 + (available_width - size) / 2
        top = 133 + max(0, (available_height - size) / 2)
        self.view = Viewport(left, top, size)
        self.field_rect = pygame.Rect(round(left), round(top), round(size), round(size))
        self.visible_rows = max(2, min(5, (h - 598) // 37))

    def remember(self, data=None):
        self.history.append(copy.deepcopy(data if data is not None else self.world.snapshot()))
        self.history = self.history[-60:]
        self.future.clear()

    def undo(self, redo=False):
        source, destination = (self.future, self.history) if redo else (self.history, self.future)
        if not source:
            return
        destination.append(self.world.snapshot())
        self.world = World.from_data(source.pop())
        self.message = "Refacut." if redo else "Ultima actiune a fost anulata."

    def action(self, key):
        self.active_input = None
        try:
            if key in ("select", "robot", "yellow", "red", "blue", "measure"):
                self.mode = key
                self.reference = False
                self.measure.clear()
            elif key in ("grid", "collisions", "zones", "reference"):
                setattr(self, key, not getattr(self, key))
            elif key == "undo":
                self.undo()
            elif key == "redo":
                self.undo(True)
            elif key == "delete" and self.selected:
                self.remember()
                self.world.remove(self.selected)
                self.selected_id = None
            elif key == "clear":
                self.remember()
                for entity in list(self.world.entities):
                    if entity.kind == "ball":
                        self.world.remove(entity)
                self.message = "Mingile au fost eliminate. Ctrl+Z pentru anulare."
            elif key in ("reset", "preset:pedro", "preset:match"):
                self.remember()
                preset = key.split(":")[1] if key.startswith("preset:") else (self.world.preset if self.world.preset != "custom" else "pedro")
                self.world = initial_world(preset)
                self.selected_id = self.world.robot.id
                self.inventory_open = False
                self.reference = False
                self.mode = "select"
                self.measure.clear()
                self.message = "Scena initiala restaurata: 40 POLLEN + 8 NECTAR rosu + 8 albastru."
            elif key == "inventory":
                self.inventory_open = not self.inventory_open
            elif key == "release" and self.selected and not self.selected.on_floor:
                self.mode = "release"
                self.reference = False
                self.inventory_open = False
                self.message = "Editor: click pe podea pentru a extrage mingea selectata din container."
            elif key == "save":
                self.world.save(self.scene_path)
                self.message = "Salvat: " + str(self.scene_path.name)
            elif key == "load":
                loaded = World.load(self.scene_path)
                self.remember()
                self.world = loaded
                self.selected_id = self.world.robot.id if self.world.robot else None
                self.message = "Scena incarcata din " + str(self.scene_path.name)
            elif key.startswith("entity:"):
                self.selected_id = int(key.split(":")[1])
                self.mode = "select"
                self.inventory_open = False
                if self.selected and self.selected.kind == "robot":
                    self.world.active_robot_id = self.selected.id
            elif key.startswith("color:") and self.selected:
                before = self.world.snapshot()
                color = key.split(":")[1]
                self.world.change(self.selected, color=color, radius=PIECE_RADII[color])
                self.remember(before)
        except (ValueError, OSError) as exc:
            self.message = str(exc)

    def button(self, key, label, rect, active=False, color=None):
        rect = pygame.Rect(rect)
        hover = rect.collidepoint(pygame.mouse.get_pos())
        fill = (57, 40, 54) if active else ((43, 47, 55) if hover else (34, 38, 45))
        self.painter.box(rect, fill, ACCENT if active else BORDER, 6)
        self.painter.text(label, (rect.x + 11, rect.y + (rect.h - 21) / 2), 13, color or (TEXT if active else (194, 203, 217)))
        self.buttons.append((rect, key))

    def input(self, key, label, value, rect):
        rect = pygame.Rect(rect)
        p = self.painter
        p.text(label, (rect.x, rect.y - 21), 11, MUTED)
        p.box(rect, BG, CYAN if self.active_input == key else BORDER, 5)
        string = self.input_text if self.active_input == key else f"{value:.2f}"
        p.text(string + ("|" if self.active_input == key else ""), (rect.x + 9, rect.y + 7), 14)
        self.inputs[key] = (rect, value)

    def commit_input(self):
        if self.active_input is None or not self.selected:
            self.active_input = None
            return
        try:
            value = float(self.input_text.replace(",", "."))
            key = self.active_input
            if key == "heading":
                value = math.radians(value)
            before = self.world.snapshot()
            self.world.change(self.selected, **{key: value})
            self.remember(before)
            self.message = "Proprietate actualizata."
        except ValueError as exc:
            self.message = "Valoare invalida: " + str(exc)
        self.active_input = None

    def handle_event(self, event):
        if event.type == pygame.QUIT:
            self.alive = False
        elif event.type == pygame.WINDOWFOCUSLOST:
            self.finish_drag()
        elif event.type == pygame.VIDEORESIZE:
            self.screen = pygame.display.set_mode((max(1100, event.w), max(760, event.h)), pygame.RESIZABLE)
            self.painter.screen = self.screen
            self.layout()
        elif event.type == pygame.KEYDOWN:
            if self.inventory_open:
                if event.key in (pygame.K_ESCAPE, pygame.K_i):
                    self.inventory_open = False
                return
            if self.active_input:
                if event.key in (pygame.K_RETURN, pygame.K_KP_ENTER, pygame.K_TAB):
                    self.commit_input()
                elif event.key == pygame.K_ESCAPE:
                    self.active_input = None
                elif event.key == pygame.K_BACKSPACE:
                    self.input_text = self.input_text[:-1]
                elif event.unicode and event.unicode in "0123456789.,-+eE" and len(self.input_text) < 14:
                    self.input_text += event.unicode
                return
            ctrl = event.mod & pygame.KMOD_CTRL
            if ctrl:
                shortcut = {pygame.K_s: "save", pygame.K_o: "load", pygame.K_z: "undo", pygame.K_y: "redo"}.get(event.key)
            else:
                shortcut = {pygame.K_v: "select", pygame.K_1: "robot", pygame.K_2: "yellow",
                            pygame.K_3: "red", pygame.K_4: "blue", pygame.K_g: "grid", pygame.K_c: "collisions",
                            pygame.K_m: "measure", pygame.K_i: "inventory", pygame.K_DELETE: "delete", pygame.K_r: "reset", pygame.K_ESCAPE: "select"}.get(event.key)
            if shortcut:
                self.action(shortcut)
        elif event.type == pygame.MOUSEWHEEL:
            if self.inventory_open:
                return
            if pygame.mouse.get_pos()[0] < self.left_width:
                self.list_offset = max(0, min(max(0, len(self.world.entities) - self.visible_rows), self.list_offset - event.y))
            elif self.selected and not self.reference and self.active_input is None:
                try:
                    before = self.world.snapshot()
                    self.world.change(self.selected, heading=self.selected.heading + math.radians(5 * event.y))
                    self.remember(before)
                except ValueError as exc:
                    self.message = str(exc)
        elif event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
            if self.inventory_open:
                for rect, key in self.buttons:
                    if rect.collidepoint(event.pos):
                        self.action(key)
                        return
                return
            for key, (rect, value) in self.inputs.items():
                if rect.collidepoint(event.pos):
                    self.active_input, self.input_text = key, ""
                    return
            if self.active_input:
                self.commit_input()
            for rect, key in self.buttons:
                if rect.collidepoint(event.pos):
                    self.action(key)
                    return
            if self.field_rect.collidepoint(event.pos) and not self.reference:
                point = self.view.to_world(event.pos)
                if self.mode == "measure":
                    if len(self.measure) == 2:
                        self.measure.clear()
                    self.measure.append(point)
                elif self.mode == "select":
                    entity = self.world.pick(point)
                    self.selected_id = entity.id if entity else None
                    if entity:
                        if entity.kind == "robot":
                            self.world.active_robot_id = entity.id
                        if entity.on_floor:
                            self.dragging = True
                            self.drag_before = self.world.snapshot()
                            self.drag_offset = (entity.x - point[0], entity.y - point[1])
                        else:
                            self.message = "Minge in container: consulta inventarul sau foloseste Pune pe teren."
                else:
                    try:
                        before = self.world.snapshot()
                        if self.mode == "release":
                            if not self.selected:
                                raise ValueError("Selecteaza o minge din inventar.")
                            entity = self.world.change(self.selected, x=point[0], y=point[1], location="ground", slot=0)
                            self.mode = "select"
                        elif self.mode == "robot":
                            entity = self.world.change(self.world.robot, x=point[0], y=point[1]) if self.world.robot else self.world.add("robot", *point, heading=math.pi/2)
                        else:
                            entity = self.world.add("ball", *point, color=self.mode)
                        self.remember(before)
                        self.selected_id = entity.id
                        self.message = f"{entity.kind} plasat la ({entity.x:.1f}, {entity.y:.1f}) cm."
                    except ValueError as exc:
                        self.message = str(exc)
        elif event.type == pygame.MOUSEMOTION and self.dragging and self.selected:
            point = self.view.to_world(event.pos)
            try:
                self.world.move(self.selected, x=point[0] + self.drag_offset[0], y=point[1] + self.drag_offset[1])
                self.message = "Mutare in coordonate reale. Elibereaza pentru confirmare."
            except ValueError as exc:
                self.message = str(exc)
        elif event.type == pygame.MOUSEBUTTONUP and event.button == 1:
            self.finish_drag()

    def finish_drag(self):
        if self.dragging and self.drag_before != self.world.snapshot():
            self.remember(self.drag_before)
        self.dragging = False
        self.drag_before = None

    def draw(self):
        p, s = self.painter, self.screen
        w, h = s.get_size()
        s.fill(BG)
        self.buttons.clear()
        self.inputs.clear()
        pygame.draw.rect(s, PANEL, (0, 0, w, 66))
        pygame.draw.line(s, BORDER, (0, 66), (w, 66))
        p.box((20, 17, 32, 32), (60, 35, 53), radius=7)
        p.text("P", (29, 19), 20, ACCENT, True)
        p.text("FIELD STUDIO", (66, 16), 18, TEXT, True)
        p.text("PATRIC  /  FIELD + VISUALS", (67, 40), 10, MUTED)
        p.text("BIOBUZZ", (self.left_width + 62, 23), 14, TEXT, True)
        p.text("2026 - 2027", (self.left_width + 160, 26), 11, MUTED)
        self.button("undo", "Undo", (w - 510, 16, 73, 35))
        self.button("redo", "Redo", (w - 429, 16, 73, 35))
        self.button("save", "Salveaza", (w - 337, 16, 95, 35))
        self.button("load", "Incarca", (w - 234, 16, 91, 35))
        pygame.draw.rect(s, PANEL, (0, 67, self.left_width, h - 101))
        pygame.draw.line(s, BORDER, (self.left_width, 67), (self.left_width, h - 34))
        pygame.draw.rect(s, PANEL, (self.right_x, 67, self.right_width, h - 101))
        pygame.draw.line(s, BORDER, (self.right_x, 67), (self.right_x, h - 34))

        p.text("CONSTRUIESTE", (20, 89), 11, MUTED, True)
        tools = (("select", "V   Selecteaza / muta"), ("robot", "1   Plaseaza robot"),
                 ("yellow", "2   Minge galbena"), ("red", "3   Minge rosie"),
                 ("blue", "4   Minge albastra"), ("measure", "M  Masoara distanta"))
        for i, (key, label) in enumerate(tools):
            self.button(key, label, (16, 119 + i*44, 182, 36), self.mode == key, COLORS.get(key))
        self.button("inventory", "I  Inventar", (16, 391, 103, 29), self.inventory_open)
        count = len(self.world.entities)
        p.text(f"{count} obiecte", (113, 403), 11, MUTED)
        self.list_offset = min(self.list_offset, max(0, count-self.visible_rows))
        for i, entity in enumerate(self.world.entities[self.list_offset:self.list_offset+self.visible_rows]):
            label = entity.robot_slot.upper() if entity.kind == "robot" else {"yellow": "POLLEN", "red": "NECTAR R", "blue": "NECTAR B"}[entity.color]
            self.button(f"entity:{entity.id}", f"{label}   #{entity.id}", (16, 431+i*37, 182, 31), self.selected_id == entity.id)
        if count > self.visible_rows:
            p.text("Scroll pentru lista completa", (20, 437+self.visible_rows*37), 10, MUTED)
        self.button("clear", "Goleste mingile", (16, h-135, 182, 34))
        self.button("reset", "R   Reset start", (16, h-92, 182, 34))

        x = self.left_width + 26
        p.text("Teren", (x, 84), 18, TEXT, True)
        p.text("359.41 x 359.41 cm", (x + 80, 90), 11, MUTED)
        self.button("grid", "Grila", (self.right_x-240, 83, 64, 30), self.grid)
        self.button("collisions", "Coliziuni", (self.right_x-168, 83, 83, 30), self.collisions)
        self.button("zones", "Zone", (self.right_x-77, 83, 65, 30), self.zones)
        s.set_clip(self.field_rect)
        p.field(self.view, self.grid, self.collisions, self.zones, self.reference)
        if not self.reference:
            for entity in self.world.entities:
                if entity.location.startswith("flower:") and entity != self.world.contents(entity.location)[-1]:
                    continue
                p.entity(entity, self.view, entity.id == self.selected_id)
            p.storage_badges(self.world, self.view)
            if self.mode not in ("select", "measure") and not self.inventory_open and self.field_rect.collidepoint(pygame.mouse.get_pos()):
                point = self.view.to_world(pygame.mouse.get_pos())
                kind = "robot" if self.mode == "robot" else "ball"
                ghost_color = (self.selected.color if self.selected else "yellow") if self.mode == "release" else ("yellow" if kind == "robot" else self.mode)
                ghost = self.world.make_entity(kind, *point, heading=self.world.robot.heading if kind == "robot" and self.world.robot else math.pi/2,
                    width=self.world.robot.width if kind == "robot" and self.world.robot else 40.64,
                    length=self.world.robot.length if kind == "robot" and self.world.robot else 40.64,
                    color=ghost_color, radius=self.selected.radius if self.mode == "release" and self.selected else None)
                p.entity(ghost, self.view, ghost=True, valid=self.world.valid(ghost, self.world.robot if kind == "robot" else None))
        if self.measure:
            a = self.measure[0]
            b = self.measure[1] if len(self.measure) == 2 else self.view.to_world(pygame.mouse.get_pos())
            pygame.draw.line(s, YELLOW, self.view.to_screen(a), self.view.to_screen(b), 2)
            for pt in (a, b):
                pygame.draw.circle(s, YELLOW, self.view.to_screen(pt), 4)
            mid = self.view.to_screen(((a[0]+b[0])/2, (a[1]+b[1])/2))
            p.text(f"{math.dist(a, b):.2f} cm", (mid[0]+8, mid[1]-24), 13, YELLOW, True)
        s.set_clip(None)
        p.axes(self.view)
        status = "REFERINTA PEDRO / include marcaje desenate" if self.reference else "EDITOR  /  FIELD + VISUALS"
        p.text(status, (self.view.left, h - 53), 11, CYAN)

        rx = self.right_x + 19
        p.text("Inspector", (rx, 87), 18, TEXT, True)
        entity = self.selected
        if entity:
            p.text(f"{'ROBOT' if entity.kind == 'robot' else 'MINGE'}  /  #{entity.id}", (rx, 125), 12, ACCENT, True)
            if entity.on_floor:
                self.input("x", "X / cm", entity.x, (rx, 177, 116, 36))
                self.input("y", "Y / cm", entity.y, (rx+130, 177, 116, 36))
                self.input("heading", "Heading / grade", math.degrees(entity.heading) % 360, (rx, 247, 246, 36))
                p.text(f"{entity.heading % math.tau:.4f} rad   /   sens antiorar", (rx, 289), 10, MUTED)
            else:
                p.text(storage_label(entity.location), (rx, 171), 13, CYAN)
                p.text(f"Slot individual: {entity.slot+1}", (rx, 201), 12, MUTED)
                p.text(f"X {entity.x:.2f} / Y {entity.y:.2f} cm" if entity.visible else "In inventar, in afara terenului", (rx, 233), 11, MUTED)
                p.text("Nu este corp liber pe podea.", (rx, 266), 11, MUTED)
            if entity.kind == "robot":
                self.input("width", "Latime / cm", entity.width, (rx, 338, 116, 36))
                self.input("length", "Lungime / cm", entity.length, (rx+130, 338, 116, 36))
            elif entity.on_floor:
                self.input("radius", "Raza / cm", entity.radius, (rx, 338, 116, 36))
                for i, (color, label) in enumerate((("yellow", "Galben"), ("red", "Rosu"), ("blue", "Albastru"))):
                    self.button("color:"+color, label, (rx+i*83, 389, 77, 30), entity.color == color, COLORS[color])
            else:
                p.text(f"Raza: {entity.radius:.2f} cm", (rx, 326), 13, TEXT)
                p.text("POLLEN" if entity.color == "yellow" else "NECTAR", (rx, 355), 12, COLORS[entity.color])
                self.button("release", "Pune pe teren (editor)", (rx, 389, 246, 32))
            p.text("STATE", (rx, 438), 10, MUTED)
            p.text(entity.state, (rx, 457), 14, CYAN)
            region = zone_at(entity.body.position) if entity.on_floor else storage_label(entity.location)
            p.text(region or "Suprafata libera", (rx, 482), 11, MUTED)
            self.button("delete", "Sterge obiectul  /  Del", (rx, 515, 246, 35))
        else:
            p.text("Selecteaza un obiect pe teren", (rx, 143), 12, MUTED)
            p.text("sau adauga unul din stanga.", (rx, 166), 12, MUTED)
        info_y = max(575, h - 330)
        pygame.draw.line(s, BORDER, (rx, info_y-13), (w-20, info_y-13))
        p.text("CONTROL", (rx, info_y), 11, MUTED, True)
        help_lines = ("Click + drag   muta obiectul", "Scroll   roteste cu 5 grade", "M   masoara distanta", "I   inventar mingi", "Ctrl+Z / Ctrl+Y   undo / redo")
        if h < 900:
            help_lines = ("Click + drag: muta / Scroll: rotire", "M: masurare / Ctrl+Z: undo")
        for i, line in enumerate(help_lines):
            p.text(line, (rx, info_y + 27+i*23), 11, MUTED)
        self.button("reference", "Compara cu Pedro", (rx, h-122, 246, 34), self.reference)
        p.text("Geometrie calibrata din referinta.", (rx, h-78), 10, MUTED)
        p.text("Editor 2D; pozitionare in cm.", (rx, h-60), 10, MUTED)
        pygame.draw.rect(s, (22, 25, 30), (0, h-34, w, 34))
        pygame.draw.line(s, BORDER, (0, h-34), (w, h-34))
        p.text(self.message[:115], (18, h-26), 11, MUTED)
        if self.field_rect.collidepoint(pygame.mouse.get_pos()):
            mx, my = self.view.to_world(pygame.mouse.get_pos())
            p.text(f"X {mx:6.2f}   Y {my:6.2f} cm", (w-244, h-26), 11, CYAN)
        if self.inventory_open:
            self.draw_inventory()

    def draw_inventory(self):
        p, s = self.painter, self.screen
        w, h = s.get_size()
        shade = pygame.Surface((w, h), pygame.SRCALPHA)
        shade.fill((0, 0, 0, 175))
        s.blit(shade, (0, 0))
        p.box((28, 76, w-56, h-130), PANEL, BORDER, 12)
        self.buttons.clear()
        self.inputs.clear()
        balls = [e for e in self.world.entities if e.kind == "ball"]
        counts = {c: sum(e.color == c for e in balls) for c in COLORS}
        p.text(f"INVENTAR  /  {len(balls)} mingi", (48, 91), 19, TEXT, True)
        p.text(f"POLLEN {counts['yellow']}    NECTAR rosu {counts['red']}    NECTAR albastru {counts['blue']}", (48, 121), 12, MUTED)
        self.button("preset:pedro", "Start Pedro / 1 robot", (w-542, 94, 180, 34), self.world.preset == "pedro")
        self.button("preset:match", "Start / 4 roboti", (w-352, 94, 163, 34), self.world.preset == "match")
        self.button("inventory", "Inchide / I", (w-178, 94, 126, 34))
        groups = [("ground", "Podea / GARDEN")]
        groups += [("flower:"+n, "FLOWER "+n) for n in FLOWERS]
        groups += [("hive:"+c.key, "CELL "+c.key) for c in CELLS]
        robots = {e.robot_slot: e for e in self.world.entities if e.kind == "robot"}
        groups += [(f"robot:{robots[n].id}" if n in robots else "preload:"+n,
                    "Preload "+n+("" if n in robots else " / virtual")) for n in ROBOT_SLOTS]
        groups += [("reserve:"+c, "Rezerva alianta "+c) for c in ("red", "blue")]
        cw = (w-124)/3
        ch = (h-260)/5
        for i, (location, label) in enumerate(groups):
            x, y = 48+(i%3)*(cw+14), 160+(i//3)*ch
            items = [e for e in self.world.contents(location) if e.kind == "ball"]
            p.box((x, y, cw, ch-10), BG, BORDER, 7)
            p.text(f"{label}  ({len(items)})", (x+10, y+8), 12, TEXT, True)
            for j, entity in enumerate(items[:8]):
                rect = pygame.Rect(x+9+j*32, y+36, 28, 28)
                pygame.draw.circle(s, COLORS[entity.color], rect.center, 10)
                p.text(entity.id, (rect.x+3, rect.y+5), 9, BG, True)
                self.buttons.append((rect, f"entity:{entity.id}"))
        p.text("Click pe o minge pentru inspectie. Sloturile suprapuse sunt obiecte separate. Preseturile pot fi anulate cu Ctrl+Z.", (48, h-87), 11, MUTED)

    def run(self, frames=None, screenshot=None):
        clock = pygame.time.Clock()
        n = 0
        while self.alive:
            clock.tick(60)
            for event in pygame.event.get():
                self.handle_event(event)
            self.draw()
            pygame.display.flip()
            n += 1
            if frames and n >= frames:
                break
        if screenshot:
            Path(screenshot).parent.mkdir(parents=True, exist_ok=True)
            pygame.image.save(self.screen, screenshot)
        pygame.quit()


def main():
    parser = argparse.ArgumentParser(description="FTC BIOBUZZ field editor - world coordinates in cm")
    parser.add_argument("--headless", action="store_true")
    parser.add_argument("--frames", type=int)
    parser.add_argument("--screenshot")
    parser.add_argument("--scene", type=Path)
    args = parser.parse_args()
    if args.headless:
        os.environ["SDL_VIDEODRIVER"] = "dummy"
        os.environ["SDL_AUDIODRIVER"] = "dummy"
    app = App((1440, 960) if args.headless else None)
    if args.scene:
        app.world = World.load(args.scene)
        app.scene_path = args.scene
        app.selected_id = app.world.robot.id if app.world.robot else None
    app.run(args.frames or (1 if args.headless else None), args.screenshot)
