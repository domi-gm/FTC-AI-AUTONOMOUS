import copy
import math
import os
import unittest
from collections import Counter
os.environ["SDL_VIDEODRIVER"] = "dummy"
os.environ["SDL_AUDIODRIVER"] = "dummy"

import pygame
from simulator.world import World, initial_world
from simulator.season import FLOWERS, CELLS, PIECE_RADII
from simulator.app import App


class SeasonSetupTest(unittest.TestCase):
    def test_official_inventory_conserved_in_both_presets(self):
        for preset, robot_count in (("pedro", 1), ("match", 4)):
            world = initial_world(preset)
            balls = [e for e in world.entities if e.kind == "ball"]
            self.assertEqual(len(balls), 56)
            self.assertEqual(Counter(e.color for e in balls), {"yellow": 40, "red": 8, "blue": 8})
            self.assertEqual(sum(e.kind == "robot" for e in world.entities), robot_count)
            self.assertEqual(sum(e.on_floor for e in balls), 8)
            self.assertEqual(sum(e.location.startswith("preload:") for e in balls), 16-robot_count*4)
            self.assertEqual(len({e.id for e in world.entities}), len(world.entities))
            for e in balls:
                self.assertEqual(e.radius, PIECE_RADII[e.color])

    def test_gardens_and_cells_match_orientation_of_reference(self):
        world = initial_world()
        garden = [e for e in world.entities if e.kind == "ball" and e.on_floor]
        self.assertEqual(sum(e.x < 30 and e.y < 5 for e in garden), 4)
        self.assertEqual(sum(e.x > 329 and e.y > 354 for e in garden), 4)
        self.assertEqual(len(CELLS), 4)
        self.assertEqual(len(world.contents("hive:red:front")), 3)
        self.assertEqual(len(world.contents("hive:blue:back")), 3)
        self.assertFalse(world.contents("hive:red:back"))
        self.assertFalse(world.contents("hive:blue:front"))
        for name in FLOWERS:
            self.assertEqual(len(world.contents("flower:"+name)), 4)

    def test_contained_balls_not_floor_collision_bodies(self):
        world = initial_world()
        shapes = set(world.space.shapes)
        for e in world.entities:
            self.assertEqual(e.shape in shapes, e.on_floor)
        before = [(e.id, tuple(e.body.position)) for e in world.entities if e.location.startswith(("hive:", "flower:"))]
        world.change(world.robot, x=280, y=280)
        after = [(e.id, tuple(e.body.position)) for e in world.entities if e.location.startswith(("hive:", "flower:"))]
        self.assertEqual(before, after)

    def test_preloads_follow_robot_translation_and_rotation(self):
        world = initial_world()
        robot = world.robot
        before = [tuple(e.body.position) for e in world.contents(f"robot:{robot.id}")]
        world.change(robot, x=280, y=280, heading=math.pi)
        robot = world.robot
        cargo = world.contents(f"robot:{robot.id}")
        self.assertEqual(len(cargo), 4)
        self.assertNotEqual(before, [tuple(e.body.position) for e in cargo])
        for ball in cargo:
            self.assertLess(robot.shape.point_query(ball.body.position).distance, 0)

    def test_robot_can_be_placed_under_elevated_hive(self):
        world = World()
        robot = world.add("robot", 180, 180)
        self.assertAlmostEqual(robot.y, 180)
        self.assertAlmostEqual(robot.x, 180)

    def test_ground_ball_under_cell_not_contained_ball(self):
        world = initial_world()
        ball = world.add("ball", 135.9, 155.2)
        self.assertEqual(ball.state, "free")
        self.assertTrue(ball.on_floor)
        self.assertEqual(len(world.contents("hive:red:front")), 3)

    def test_scene_roundtrip_preserves_all_storage_and_active_robot(self):
        for preset in ("pedro", "match"):
            world = initial_world(preset)
            if preset == "match":
                world.active_robot_id = 3
            scene = world.snapshot()
            restored = World.from_data(scene)
            self.assertEqual(restored.snapshot(), scene)

    def test_releasing_piece_is_atomic_and_conserves_count(self):
        world = initial_world()
        piece = world.contents("hive:red:front")[0]
        before = world.snapshot()
        with self.assertRaises(ValueError):
            world.change(piece, x=118, y=200, location="ground")
        self.assertEqual(world.snapshot(), before)
        moved = world.change(piece, x=60, y=70, location="ground")
        self.assertTrue(moved.on_floor)
        self.assertIn(moved.shape, world.space.shapes)
        self.assertEqual(len(world.contents("hive:red:front")), 2)
        self.assertEqual(sum(e.kind == "ball" for e in world.entities), 56)

    def test_deleting_robot_returns_preloads_to_virtual_inventory(self):
        world = initial_world()
        world.remove(world.robot)
        self.assertEqual(len(world.contents("preload:red1")), 4)
        self.assertEqual(sum(e.kind == "ball" for e in world.entities), 56)
        self.assertEqual(World.from_data(world.snapshot()).snapshot(), world.snapshot())

    def test_corrupt_container_data_rejected(self):
        scene = initial_world().snapshot()
        piece = next(i for i, e in enumerate(scene["entities"]) if e["location"].startswith("flower:"))
        for changes in ({"location": "flower:bogus"}, {"location": "robot:9999"},
                        {"slot": -1}, {"location": "reserve:red"},
                        {"location": "hive:red:front", "x": -90}):
            bad = copy.deepcopy(scene)
            bad["entities"][piece].update(changes)
            with self.assertRaises(ValueError):
                World.from_data(bad)
        bad = copy.deepcopy(scene)
        bad["entities"][piece+1]["slot"] = bad["entities"][piece]["slot"]
        with self.assertRaises(ValueError):
            World.from_data(bad)


class InventoryEditorTest(unittest.TestCase):
    def setUp(self):
        self.app = App((1100, 760))

    def tearDown(self):
        pygame.quit()

    def test_inventory_and_preset_buttons_fit_small_window(self):
        app = self.app
        app.action("inventory")
        app.draw()
        for i, (a, _) in enumerate(app.buttons):
            for b, _ in app.buttons[i+1:]:
                self.assertFalse(a.colliderect(b))
        app.action("preset:match")
        self.assertEqual(sum(e.kind == "robot" for e in app.world.entities), 4)
        app.action("undo")
        self.assertEqual(sum(e.kind == "robot" for e in app.world.entities), 1)

    def test_release_via_editor_and_undo(self):
        app = self.app
        app.selected_id = app.world.contents("reserve:red")[0].id
        app.action("release")
        pos = app.view.to_screen((70, 90))
        app.handle_event(pygame.event.Event(pygame.MOUSEBUTTONDOWN, button=1, pos=pos))
        self.assertTrue(app.selected.on_floor)
        self.assertEqual(len(app.world.contents("reserve:red")), 4)
        app.undo()
        self.assertEqual(len(app.world.contents("reserve:red")), 5)

    def test_select_robot_changes_editor_target(self):
        app = self.app
        app.action("preset:match")
        robots = [e for e in app.world.entities if e.kind == "robot"]
        app.action(f"entity:{robots[2].id}")
        self.assertEqual(app.world.robot.id, robots[2].id)


if __name__ == "__main__":
    unittest.main()
