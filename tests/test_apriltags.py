import hashlib
import json
import os
from pathlib import Path
import unittest

os.environ["SDL_VIDEODRIVER"] = "dummy"
os.environ["SDL_AUDIODRIVER"] = "dummy"

import pygame
from simulator.apriltags import APRILTAGS, TAG_SIZE_CM, tag_by_id
from simulator.app import App
from simulator.world import World, initial_world


class AprilTagLayoutTest(unittest.TestCase):
    def test_official_ids_and_left_to_right_order(self):
        # Independent expectations from manual TU02, figure 9-17.
        self.assertEqual({tag.id for tag in APRILTAGS}, set(range(30, 46)))
        self.assertEqual(len(APRILTAGS), 16)
        for cell, ids in (("red:back", [33, 32, 31, 30]),
                          ("red:front", [34, 35, 36, 37]),
                          ("blue:back", [45, 44, 43, 42]),
                          ("blue:front", [38, 39, 40, 41])):
            tags = sorted((t for t in APRILTAGS if t.cluster.cell_key == cell),
                          key=lambda t: t.layout_center[0])
            self.assertEqual([tag.id for tag in tags], ids)
            self.assertAlmostEqual(tags[-1].layout_center[0] - tags[0].layout_center[0], 33.02)
        self.assertAlmostEqual(TAG_SIZE_CM, 8.255)

    def test_canonical_asset_integrity(self):
        assets = Path(__file__).resolve().parents[1] / "assets/apriltags"
        manifest = json.loads((assets / "SOURCE.json").read_text())
        self.assertEqual(set(manifest["sha256"]), {tag.image_name for tag in APRILTAGS})
        for tag in APRILTAGS:
            data = (assets / tag.image_name).read_bytes()
            self.assertEqual(hashlib.sha256(data).hexdigest(), manifest["sha256"][tag.image_name])

    def test_tags_survive_scene_roundtrip_without_blocking_floor(self):
        world = World()
        # A ball beneath a tag must not collide with the annotation.
        world.add("ball", *tag_by_id(35).layout_center)
        self.assertEqual(len(world.space.shapes), len(world.obstacle_shapes) + 1)
        restored = World.from_data(world.snapshot())
        self.assertEqual(restored.apriltags, world.apriltags)
        self.assertEqual(restored.snapshot(), world.snapshot())
        self.assertEqual(sum(e.kind == "ball" for e in initial_world().entities), 56)


class AprilTagEditorTest(unittest.TestCase):
    def tearDown(self):
        pygame.quit()

    def test_inspection_is_read_only_and_toggle_preserves_scene(self):
        app = App((1100, 760))
        before = app.world.snapshot()
        app.draw()
        position = app.view.to_screen(tag_by_id(35).layout_center)
        app.handle_event(pygame.event.Event(pygame.MOUSEBUTTONDOWN, button=1, pos=position))
        app.draw()
        self.assertEqual(app.selected_tag_id, 35)
        self.assertIsNone(app.selected)
        self.assertFalse(app.dragging)
        self.assertEqual(app.inputs, {})
        app.action("delete")
        app.handle_event(pygame.event.Event(pygame.KEYDOWN, key=pygame.K_t, mod=0, unicode="t"))
        self.assertFalse(app.tags_visible)
        self.assertIsNone(app.selected_tag_id)
        self.assertEqual(app.world.snapshot(), before)
        self.assertEqual(app.history, [])

    def test_each_tag_can_be_inspected_after_resize(self):
        app = App((1440, 960))
        for size in ((1440, 960), (1100, 760)):
            app.handle_event(pygame.event.Event(pygame.VIDEORESIZE, w=size[0], h=size[1]))
            app.draw()
            for tag in APRILTAGS:
                point = app.view.to_screen(tag.layout_center)
                app.handle_event(pygame.event.Event(pygame.MOUSEBUTTONDOWN, button=1, pos=point))
                app.draw()
                self.assertEqual(app.selected_tag_id, tag.id)
            for i, (a, _) in enumerate(app.buttons):
                for b, _ in app.buttons[i + 1:]:
                    self.assertFalse(a.colliderect(b))


if __name__ == "__main__":
    unittest.main()
