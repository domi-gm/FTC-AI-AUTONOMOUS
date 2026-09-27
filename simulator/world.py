"""Scene geometry in cm/radians. Pymunk validates placement; no simulation loop."""
from dataclasses import dataclass
import json
import math
from pathlib import Path
import pymunk
from .field import FIELD_WIDTH, FIELD_HEIGHT, BALL_RADIUS, COLORS, OBSTACLES, zone_at, contains
from .season import PIECE_RADII, PIECE_MASSES, FLOWERS, CELLS, ROBOT_SLOTS, HIVE_INITIAL_UP, ground_setup, hive_setup


@dataclass
class Entity:
    id: int
    kind: str
    body: pymunk.Body
    shape: pymunk.Shape
    color: str = "yellow"
    width: float = 40.64
    length: float = 40.64
    radius: float = BALL_RADIUS
    state: str = "free"
    location: str = "ground"
    slot: int = 0
    robot_slot: str = "red1"

    @property
    def x(self):
        return self.body.position.x

    @property
    def y(self):
        return self.body.position.y

    @property
    def heading(self):
        return self.body.angle

    def data(self):
        return dict(id=self.id, kind=self.kind, x=self.x, y=self.y,
                    heading=self.heading, width=self.width, length=self.length,
                    radius=self.radius, color=self.color, state=self.state,
                    location=self.location, slot=self.slot, robot_slot=self.robot_slot)

    @property
    def on_floor(self):
        return self.location == "ground"

    @property
    def visible(self):
        return not self.location.startswith(("reserve:", "preload:"))


class World:
    def __init__(self):
        self.space = pymunk.Space()
        self.space.gravity = (0, 0)
        self.space.damping = 0.3
        self.space.iterations = 30
        self.space.collision_slop = 0.05
        self.entities = []
        self.obstacle_shapes = []
        self.next_id = 1
        self.preset = "custom"
        self.active_robot_id = None
        self.hives = dict(HIVE_INITIAL_UP)
        corners = ((0, 0), (FIELD_WIDTH, 0), (FIELD_WIDTH, FIELD_HEIGHT), (0, FIELD_HEIGHT))
        for a, b in zip(corners, corners[1:] + corners[:1]):
            wall = pymunk.Segment(self.space.static_body, a, b, 0.25)
            wall.friction, wall.elasticity = 0.7, 0.35
            self.space.add(wall)
            self.obstacle_shapes.append(wall)
        for region in OBSTACLES:
            poly = pymunk.Poly(self.space.static_body, region.vertices)
            poly.friction, poly.elasticity = 0.7, 0.25
            self.space.add(poly)
            self.obstacle_shapes.append(poly)

    @property
    def robot(self):
        return next((e for e in self.entities if e.kind == "robot" and e.id == self.active_robot_id),
                    next((e for e in self.entities if e.kind == "robot"), None))

    def by_id(self, entity_id):
        return next((e for e in self.entities if e.id == entity_id), None)

    def make_entity(self, kind, x, y, heading=0, width=40.64, length=40.64,
                    radius=None, color="yellow", entity_id=None, location="ground", slot=0, robot_slot="red1"):
        if kind not in ("robot", "ball") or color not in COLORS:
            raise ValueError("Tip sau culoare invalida.")
        radius = PIECE_RADII[color] if radius is None else radius
        if not isinstance(location, str) or robot_slot not in ROBOT_SLOTS or type(slot) is not int or not 0 <= slot < 500:
            raise ValueError("Container, slot sau identitate robot invalida.")
        allowed = {"ground"} | {"flower:"+name for name in FLOWERS} | {"hive:"+c.key for c in CELLS}
        allowed |= {"reserve:red", "reserve:blue"} | {"preload:"+name for name in ROBOT_SLOTS}
        if location not in allowed and not (location.startswith("robot:") and location[6:].isdigit()):
            raise ValueError("Container necunoscut.")
        if kind == "robot" and location != "ground":
            raise ValueError("Robotul trebuie sa fie pe teren.")
        numbers = (x, y, heading, width, length, radius)
        if not all(isinstance(n, (int, float)) and math.isfinite(n) for n in numbers):
            raise ValueError("Coordonatele trebuie sa fie numere finite.")
        if not (5 <= width <= 100 and 5 <= length <= 100 and 1 <= radius <= 20):
            raise ValueError("Robot: 5..100 cm; raza mingii: 1..20 cm.")
        if kind == "robot":
            body = pymunk.Body(12, pymunk.moment_for_box(12, (length, width)))
            shape = pymunk.Poly.create_box(body, (length, width))
        else:
            mass = PIECE_MASSES[color]
            body = pymunk.Body(mass, pymunk.moment_for_circle(mass, 0, radius))
            shape = pymunk.Circle(body, radius)
        body.position = (x, y)
        body.angle = heading % math.tau
        shape.friction, shape.elasticity = 0.6, 0.45
        shape.cache_bb()
        return Entity(entity_id if entity_id is not None else self.next_id,
                      kind, body, shape, color, width, length, radius,
                      location=location, slot=slot, robot_slot=robot_slot)

    def valid(self, entity, ignore=None):
        if not entity.on_floor:
            return True
        bb = entity.shape.cache_bb()
        if bb.left < 0.3-1e-7 or bb.bottom < 0.3-1e-7 or bb.right > FIELD_WIDTH-0.3+1e-7 or bb.top > FIELD_HEIGHT-0.3+1e-7:
            return False
        for hit in self.space.shape_query(entity.shape):
            if hit.shape is entity.shape or (ignore is not None and hit.shape is ignore.shape):
                continue
            # A circle fully contained by a polygon can report a zero-distance
            # contact in Chipmunk. The query itself is the overlap signal.
            return False
        return True

    def add(self, kind, x, y, **kwargs):
        if len(self.entities) >= 500:
            raise ValueError("Limita este de 500 de obiecte.")
        entity = self.make_entity(kind, x, y, **kwargs)
        if kind == "robot" and any(e.kind == "robot" and e.robot_slot == entity.robot_slot for e in self.entities):
            raise ValueError("Acest slot de robot este deja ocupat.")
        self.validate_storage(entity)
        if not self.valid(entity):
            raise ValueError("Pozitie ocupata sau in afara terenului.")
        if entity.on_floor:
            self.space.add(entity.body, entity.shape)
        self.entities.append(entity)
        self.next_id = max(self.next_id, entity.id + 1)
        self.update_states()
        return entity

    def change(self, entity, **changes):
        """Place/edit an entity; only the destination is checked. For dragging use move."""
        if not entity.on_floor and changes.get("location") != "ground":
            raise ValueError("Minge in container. Foloseste Pune pe teren pentru a o extrage in editor.")
        data = entity.data()
        data.update(changes)
        replacement = self.make_entity(data["kind"], data["x"], data["y"],
            heading=data["heading"], width=data["width"], length=data["length"],
            radius=data["radius"], color=data["color"], entity_id=entity.id,
            location=data["location"], slot=data["slot"], robot_slot=data["robot_slot"])
        if not self.valid(replacement, ignore=entity):
            raise ValueError("Pozitie ocupata sau in afara terenului.")
        index = self.entities.index(entity)
        if entity.on_floor:
            self.space.remove(entity.shape, entity.body)
        if replacement.on_floor:
            self.space.add(replacement.body, replacement.shape)
        self.entities[index] = replacement
        self.update_states()
        return replacement

    def move(self, entity, x, y):
        """Translate without tunnelling. Heading and dimensions stay constant.

        The convex hull of both box poses is its exact swept footprint.
        For a circle the footprint is a capsule (segment + circle radius).
        Query the whole footprint before committing, without stepping physics.
        """
        if not entity.on_floor:
            raise ValueError("Mutarea continua este disponibila doar pe podea.")
        if not all(isinstance(n, (int, float)) and math.isfinite(n) for n in (x, y)):
            raise ValueError("Coordonatele trebuie sa fie numere finite.")
        delta = pymunk.Vec2d(x - entity.x, y - entity.y)
        if delta.length < 1e-10:
            return entity
        body = pymunk.Body(body_type=pymunk.Body.STATIC)
        body.position = entity.body.position + delta / 2
        if entity.kind == "robot":
            vertices = [entity.body.local_to_world(v) - body.position
                        for v in entity.shape.get_vertices()]
            swept = pymunk.Poly(body, vertices + [v + delta for v in vertices])
        else:
            swept = pymunk.Segment(body, -delta / 2, delta / 2, entity.radius)
        swept.cache_bb()
        for hit in self.space.shape_query(swept):
            if hit.shape is entity.shape:
                continue
            name = next((region.name for region, shape in
                         zip(OBSTACLES, self.obstacle_shapes[4:]) if shape is hit.shape), None)
            other = next((e for e in self.entities if e.shape is hit.shape), None)
            name = name or (f"{other.kind} #{other.id}" if other else "marginea terenului")
            raise ValueError(f"Traseu blocat de {name}. Ocoleste obstacolul sau foloseste Plaseaza.")
        return self.change(entity, x=x, y=y)

    def remove(self, entity):
        if entity.kind == "robot":
            for ball in self.entities:
                if ball.location == f"robot:{entity.id}":
                    ball.location = "preload:"+entity.robot_slot
                    ball.body.position = (0, 0)
        if entity.on_floor:
            self.space.remove(entity.shape, entity.body)
        self.entities.remove(entity)
        if self.active_robot_id == entity.id:
            self.active_robot_id = None
        self.update_states()

    def pick(self, point):
        for entity in reversed(self.entities):
            if entity.visible and not entity.location.startswith("robot:") and entity.shape.point_query(point).distance <= 1.5:
                return entity
        return None

    def update_states(self):
        self.sync_contained()
        for entity in self.entities:
            entity.state = ("in_zone" if zone_at(entity.body.position) else "free") if entity.on_floor else {
                "flower": "in_flower", "hive": "in_cell", "robot": "preloaded",
                "reserve": "alliance_reserve", "preload": "virtual_preload"}[entity.location.split(":")[0]]

    def contents(self, location):
        return sorted((e for e in self.entities if e.location == location), key=lambda e: e.slot)

    def validate_storage(self, entity):
        location = entity.location
        if location == "ground":
            return
        if any(e.location == location and e.slot == entity.slot and e.id != entity.id for e in self.entities):
            raise ValueError("Slot de container duplicat.")
        if location.startswith("robot:"):
            robot = self.by_id(int(location[6:]))
            if robot is None or robot.kind != "robot" or entity.slot >= 4:
                raise ValueError("Robotul pentru preload lipseste sau capacitatea este depasita.")
        if location.startswith("preload:") and (entity.slot >= 4 or entity.color != "yellow"):
            raise ValueError("Preload virtual invalid.")
        if location.startswith("reserve:") and (entity.color != location[8:] or entity.slot >= 5):
            raise ValueError("Rezerva de alianta invalida.")
        if location.startswith("hive:"):
            cell = next(c for c in CELLS if c.key == location[5:])
            if not contains(cell.footprint, entity.body.position):
                raise ValueError("Mingea nu este in CELL-ul indicat.")

    def sync_contained(self):
        for entity in self.entities:
            if entity.location.startswith("robot:"):
                robot = self.by_id(int(entity.location[6:]))
                if robot:
                    dx = (-0.18 if entity.slot < 2 else 0.18) * robot.length
                    dy = (-0.18 if entity.slot % 2 == 0 else 0.18) * robot.width
                    entity.body.position = robot.body.local_to_world((dx, dy))
                    entity.body.angle = robot.heading
            elif entity.location.startswith("flower:"):
                entity.body.position = FLOWERS[entity.location[7:]]
            elif entity.location.startswith(("preload:", "reserve:")):
                entity.body.position = (0, 0)  # no field coordinate; hidden in renderer
            if not entity.on_floor:
                entity.shape.cache_bb()

    def snapshot(self):
        return {"version": 2, "field": "biobuzz", "units": "cm", "heading_units": "rad",
                "field_width": FIELD_WIDTH, "field_height": FIELD_HEIGHT,
                "preset": self.preset, "hives": dict(self.hives), "active_robot_id": self.active_robot_id,
                "entities": [e.data() for e in self.entities]}

    @classmethod
    def from_data(cls, data):
        if not isinstance(data, dict) or data.get("version") not in (1, 2) or data.get("units") != "cm" or data.get("field") != "biobuzz" or data.get("heading_units") != "rad":
            raise ValueError("Fisier incompatibil: este necesara o scena BIOBUZZ in cm/rad.")
        if data.get("field_width") != FIELD_WIDTH or data.get("field_height") != FIELD_HEIGHT:
            raise ValueError("Dimensiunile terenului nu corespund.")
        records = data.get("entities")
        if not isinstance(records, list) or len(records) > 500:
            raise ValueError("Lista de obiecte invalida.")
        world = cls()
        world.preset = data.get("preset", "custom")
        if world.preset not in ("custom", "pedro", "match"):
            raise ValueError("Preset necunoscut.")
        world.hives = data.get("hives", dict(HIVE_INITIAL_UP))
        if world.hives != HIVE_INITIAL_UP:
            raise ValueError("Bascularea HIVE nu este inca simulata; configuratie incompatibila.")
        for record in records:
            if not isinstance(record, dict):
                raise ValueError("Obiect invalid.")
            try:
                entity_id = record["id"]
                if type(entity_id) is not int or entity_id < 1 or world.by_id(entity_id):
                    raise ValueError("ID duplicat sau invalid.")
                entity = world.make_entity(record["kind"], record["x"], record["y"],
                    heading=record["heading"], width=record["width"], length=record["length"],
                    radius=record["radius"], color=record["color"], entity_id=entity_id,
                    location=record.get("location", "ground"), slot=record.get("slot", 0),
                    robot_slot=record.get("robot_slot", "red1"))
                if entity.kind == "robot" and any(e.kind == "robot" and e.robot_slot == entity.robot_slot for e in world.entities):
                    raise ValueError("Identitate robot duplicata.")
                # Solver contacts can have tiny penetration. Validate bounds on
                # load; preserve legitimate contact poses exactly, without edits.
                bb = entity.shape.cache_bb()
                if entity.on_floor and (bb.left < -0.5 or bb.bottom < -0.5 or bb.right > FIELD_WIDTH + 0.5 or bb.top > FIELD_HEIGHT + 0.5):
                    raise ValueError("Obiect in afara terenului.")
                for hit in world.space.shape_query(entity.shape) if entity.on_floor else []:
                    if (any(p.distance < -0.5 for p in hit.contact_point_set.points)
                        or hit.shape.point_query(entity.body.position).distance < -0.5
                        or (hit.shape.body.body_type == pymunk.Body.DYNAMIC
                            and entity.shape.point_query(hit.shape.body.position).distance < -0.5)):
                        raise ValueError("Obiecte suprapuse in fisier.")
                if entity.on_floor:
                    world.space.add(entity.body, entity.shape)
                world.entities.append(entity)
                world.next_id = max(world.next_id, entity_id + 1)
            except (KeyError, TypeError) as exc:
                raise ValueError("Proprietati lipsa sau invalide.") from exc
        for entity in world.entities:
            world.validate_storage(entity)
        world.active_robot_id = data.get("active_robot_id")
        if world.active_robot_id is not None:
            active = world.by_id(world.active_robot_id)
            if active is None or active.kind != "robot":
                raise ValueError("Robotul activ nu exista.")
        world.update_states()
        return world

    def save(self, path):
        path = Path(path)
        temporary = path.with_suffix(".tmp")
        temporary.write_text(json.dumps(self.snapshot(), indent=2), encoding="utf-8")
        temporary.replace(path)

    @classmethod
    def load(cls, path):
        return cls.from_data(json.loads(Path(path).read_text(encoding="utf-8")))


def initial_world(preset="pedro"):
    if preset not in ("pedro", "match"):
        raise ValueError("Preset necunoscut.")
    world = World()
    world.preset = preset
    # Pedro's robot screenshot: 56 inches along X, heading north, against wall.
    poses = ((142.24, 20.62), (80, FIELD_HEIGHT-20.62),
             (FIELD_WIDTH-80, 20.62), (FIELD_WIDTH-142.24, FIELD_HEIGHT-20.62))
    robots = {}
    for i, name in enumerate(ROBOT_SLOTS):
        if preset == "match" or i == 0:
            robots[name] = world.add("robot", *poses[i], heading=math.pi/2, robot_slot=name)
    world.active_robot_id = robots["red1"].id
    for x, y, color in ground_setup():
        world.add("ball", x, y, color=color)
    for name, center in FLOWERS.items():
        for slot in range(4):
            world.add("ball", *center, color="yellow", location="flower:"+name, slot=slot)
    for i, (x, y, color, location) in enumerate(hive_setup()):
        world.add("ball", x, y, color=color, location=location, slot=i%3)
    for name in ROBOT_SLOTS:
        for slot in range(4):
            location = f"robot:{robots[name].id}" if name in robots else "preload:"+name
            world.add("ball", 0, 0, color="yellow", location=location, slot=slot)
    for color in ("red", "blue"):
        for slot in range(5):
            world.add("ball", 0, 0, color=color, location="reserve:"+color, slot=slot)
    return world
