import math
import unittest

from simulator.world import World


class SweptMovementTest(unittest.TestCase):
    def test_clear_destination_across_frame_is_rejected_atomically(self):
        for kind, heading in (("robot", 0), ("robot", math.pi / 4), ("ball", 0)):
            with self.subTest(kind=kind, heading=heading):
                world = World()
                entity = world.add(kind, 80, 180, heading=heading)
                before = world.snapshot()
                for _ in range(20):
                    with self.assertRaisesRegex(ValueError, "West frame"):
                        world.move(entity, 180, 180)
                self.assertEqual(world.snapshot(), before)

    def test_route_around_frame_is_allowed(self):
        world = World()
        robot = world.add("robot", 80, 180)
        for point in ((80, 90), (180, 90), (180, 180)):
            robot = world.move(robot, *point)
        self.assertEqual((robot.x, robot.y), (180, 180))

    def test_robot_sweep_detects_ball_even_with_clear_endpoints(self):
        world = World()
        robot = world.add("robot", 50, 70)
        ball = world.add("ball", 100, 70)
        before = world.snapshot()
        with self.assertRaisesRegex(ValueError, f"ball #{ball.id}"):
            world.move(robot, 150, 70)
        self.assertEqual(world.snapshot(), before)

    def test_diagonal_sweep_does_not_use_oversized_bounding_box(self):
        world = World()
        ball = world.add("ball", 40, 40)
        world.add("ball", 40, 90)
        ball = world.move(ball, 100, 100)
        self.assertEqual((ball.x, ball.y), (100, 100))

    def test_zero_motion_and_invalid_destination_preserve_scene(self):
        world = World()
        robot = world.add("robot", 60, 60)
        before = world.snapshot()
        self.assertIs(world.move(robot, 60, 60), robot)
        for point in ((-40, 60), (float("nan"), 60)):
            with self.assertRaises(ValueError):
                world.move(robot, *point)
            self.assertEqual(world.snapshot(), before)

    def test_explicit_placement_can_relocate_across_frame(self):
        world = World()
        robot = world.add("robot", 80, 180)
        robot = world.change(robot, x=180, y=180)
        self.assertEqual((robot.x, robot.y), (180, 180))


if __name__ == "__main__":
    unittest.main()
