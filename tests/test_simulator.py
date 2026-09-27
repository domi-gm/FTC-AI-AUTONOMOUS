import copy
import math
import os
from pathlib import Path
import tempfile
import unittest

os.environ["SDL_VIDEODRIVER"] = "dummy"
os.environ["SDL_AUDIODRIVER"] = "dummy"

import pygame
from simulator.field import FIELD_WIDTH, FIELD_HEIGHT, Viewport, zone_at
from simulator.world import World, initial_world
from simulator.app import App


class CoordinatesTest(unittest.TestCase):
    def test_world_pixel_round_trip_at_multiple_sizes(self):
        for size in (300, 702, 1100):
            view = Viewport(245, 121, size)
            self.assertEqual(view.to_screen((0, 0)), (245, 121 + size))
            self.assertEqual(view.to_screen((FIELD_WIDTH, FIELD_HEIGHT)), (245+size, 121))
            for point in ((72, 40), (0, 0), (FIELD_WIDTH, FIELD_HEIGHT), (113.78, 225.13)):
                result = view.to_world(view.to_screen(point))
                self.assertAlmostEqual(result[0], point[0])
                self.assertAlmostEqual(result[1], point[1])

    def test_zones_are_world_geometry(self):
        self.assertEqual(zone_at((15, 275)), "Red loading")
        self.assertIsNone(zone_at((75, 75)))


class SceneGeometryTest(unittest.TestCase):
    def test_invalid_placement_is_atomic(self):
        world = initial_world()
        before = world.snapshot()
        for x, y in ((0, 0), (118, 200), (world.robot.x, world.robot.y)):
            with self.assertRaises(ValueError):
                world.add("ball", x, y)
        with self.assertRaises(ValueError):
            world.change(world.robot, x=118, y=200)
        self.assertEqual(world.snapshot(), before)

    def test_save_load_dimensions_color_pose_and_state(self):
        world = World()
        world.add("robot", 80, 80, width=35, length=44, heading=1.57)
        world.add("ball", 15, 275, radius=5, color="red")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "test.json"
            world.save(path)
            restored = World.load(path)
            self.assertEqual(restored.snapshot(), world.snapshot())
        self.assertEqual(world.entities[1].state, "in_zone")

    def test_bad_files_rejected(self):
        scene = initial_world().snapshot()
        invalid = []
        for key, value in (("units", "px"), ("field_width", 100), ("version", 17)):
            other = copy.deepcopy(scene)
            other[key] = value
            invalid.append(other)
        other = copy.deepcopy(scene)
        other["entities"][0]["x"] = float("nan")
        invalid.append(other)
        other = copy.deepcopy(scene)
        other["entities"][1]["id"] = 1
        invalid.append(other)
        other = copy.deepcopy(scene)
        other["entities"][1]["x"] = 118
        other["entities"][1]["y"] = 200
        invalid.append(other)
        for data in invalid:
            with self.assertRaises(ValueError):
                World.from_data(data)


class EditorTest(unittest.TestCase):
    def setUp(self):
        self.app = App()
        self.app.draw()

    def tearDown(self):
        pygame.quit()

    def click_world(self, point):
        pos = self.app.view.to_screen(point)
        self.app.handle_event(pygame.event.Event(pygame.MOUSEBUTTONDOWN, button=1, pos=pos))
        self.app.handle_event(pygame.event.Event(pygame.MOUSEBUTTONUP, button=1, pos=pos))

    def test_placement_undo_redo_and_delete(self):
        app = self.app
        n = len(app.world.entities)
        app.action("blue")
        self.click_world((80, 110))
        self.assertEqual(len(app.world.entities), n+1)
        self.assertAlmostEqual(app.selected.x, 80)
        app.action("undo")
        self.assertEqual(len(app.world.entities), n)
        app.action("redo")
        self.assertEqual(len(app.world.entities), n+1)
        app.action("delete")
        self.assertEqual(len(app.world.entities), n)

    def test_driving_keys_do_not_change_scene(self):
        app = self.app
        before = app.world.snapshot()
        for key in (pygame.K_SPACE, pygame.K_w, pygame.K_a, pygame.K_s,
                    pygame.K_d, pygame.K_q, pygame.K_e, pygame.K_UP,
                    pygame.K_DOWN, pygame.K_LEFT, pygame.K_RIGHT):
            app.handle_event(pygame.event.Event(pygame.KEYDOWN, key=key, mod=0, unicode=""))
        app.draw()
        self.assertEqual(app.world.snapshot(), before)
        self.assertNotIn("play", [key for _, key in app.buttons])
        self.assertFalse(hasattr(app.world, "advance"))

    def test_numeric_heading_is_converted_to_radians(self):
        app = self.app
        app.active_input, app.input_text = "heading", "180"
        app.commit_input()
        self.assertAlmostEqual(app.world.robot.heading, math.pi)
        app.active_input, app.input_text = "x", "300"
        app.commit_input()
        self.assertAlmostEqual(app.world.robot.x, 300)

    def test_drag_changes_world_pose_and_is_one_undo(self):
        app = self.app
        before = app.world.snapshot()
        start = app.view.to_screen((app.world.robot.x, app.world.robot.y))
        end = app.view.to_screen((180, 80))
        app.handle_event(pygame.event.Event(pygame.MOUSEBUTTONDOWN, button=1, pos=start))
        app.handle_event(pygame.event.Event(pygame.MOUSEMOTION, pos=end))
        app.handle_event(pygame.event.Event(pygame.MOUSEBUTTONUP, button=1, pos=end))
        self.assertAlmostEqual(app.world.robot.x, 180)
        app.undo()
        self.assertEqual(app.world.snapshot(), before)

    def test_fast_repeated_drag_cannot_cross_frame(self):
        app = self.app
        app.world = World()
        robot = app.world.add("robot", 80, 180)
        app.selected_id = robot.id
        app.world.active_robot_id = robot.id
        before = app.world.snapshot()
        app.handle_event(pygame.event.Event(pygame.MOUSEBUTTONDOWN, button=1,
                                          pos=app.view.to_screen((80, 180))))
        for x in (110, 130, 170, 200, 280) * 10:
            app.handle_event(pygame.event.Event(pygame.MOUSEMOTION,
                                              pos=app.view.to_screen((x, 180))))
        app.handle_event(pygame.event.Event(pygame.MOUSEBUTTONUP, button=1,
                                          pos=app.view.to_screen((280, 180))))
        self.assertEqual(app.world.snapshot(), before)
        # The sweep can intersect both frames; query order is not travel order.
        self.assertRegex(app.message, "Traseu blocat de (West|East) frame")
        self.assertEqual(len(app.history), 0)

    def test_reference_does_not_mutate_scene(self):
        before = self.app.world.snapshot()
        self.app.action("reference")
        self.app.draw()
        self.click_world((70, 70))
        self.app.action("reference")
        self.assertEqual(self.app.world.snapshot(), before)

    def test_render_at_minimum_window(self):
        app = self.app
        app.handle_event(pygame.event.Event(pygame.VIDEORESIZE, w=1100, h=760))
        app.draw()
        self.assertGreater(app.view.size, 450)
        # Button rectangles must not overlap each other.
        for i, (first, _) in enumerate(app.buttons):
            for second, _ in app.buttons[i+1:]:
                self.assertFalse(first.colliderect(second))


if __name__ == "__main__":
    unittest.main()
