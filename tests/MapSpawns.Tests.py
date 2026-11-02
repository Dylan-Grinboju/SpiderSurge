import contextlib
import copy
import io
import json
from pathlib import Path
import sys
import unittest


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "surge/Maps/Tools"))
import layouts


class MapSpawnsTests(unittest.TestCase):
    def problems(self, arena):
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            count = layouts.check_spawn_distribution(arena)
        return count, output.getvalue()

    def trampoline(self):
        return copy.deepcopy(next(m for m in layouts.MAPS if m["id"] == "trampoline"))

    def test_every_shipped_map_meets_budget_and_spacing(self):
        for source in layouts.MAPS:
            with self.subTest(map=source["id"]):
                shipped = json.loads((ROOT / "surge/Maps/Layouts" / (source["id"] + ".json")).read_text())
                shipped.pop("camera")
                self.assertEqual(source, shipped, "Regenerate JSON from layouts.py")
                self.assertEqual((0, ""), self.problems(shipped))

    def test_rejects_original_two_regular_weapon_layout(self):
        arena = self.trampoline()
        weapons = [e for e in arena["elements"] if e["o"] == "Weapon Spawner A"]
        for e in weapons[2:]:
            arena["elements"].remove(e)
        count, output = self.problems(arena)
        self.assertGreater(count, 0)
        self.assertIn("needs 6 Weapon Spawner A points, has 2", output)

    def test_rejects_padding_budget_with_stacked_weapons(self):
        arena = self.trampoline()
        weapons = [e for e in arena["elements"] if e["o"] == "Weapon Spawner A"]
        weapons[-1].update(x=weapons[0]["x"], y=weapons[0]["y"])
        self.assertIn("weapon spawn points must be at least 60 apart", self.problems(arena)[1])

    def test_rejects_removing_low_route_supply(self):
        arena = self.trampoline()
        for e in arena["elements"]:
            if e["o"] in layouts.WEAPON_SPAWN_COUNTS:
                e["y"] = 200
        self.assertIn("no weapon supply near the lowest player route", self.problems(arena)[1])

    def test_rejects_sparse_or_crowded_enemy_spawns(self):
        arena = self.trampoline()
        arena["enemies"].pop()
        self.assertIn("needs 6 enemy spawn points, has 5", self.problems(arena)[1])
        arena = self.trampoline()
        arena["enemies"][1] = dict(arena["enemies"][0])
        self.assertIn("enemy spawn points must be at least 90 apart", self.problems(arena)[1])

    def test_rare_weapon_cannot_replace_low_route_regular_supply(self):
        arena = self.trampoline()
        for e in arena["elements"]:
            if e["o"] == "Weapon Spawner A":
                e["y"] = 200
            elif e["o"] == "Rare Weapon Spawner":
                e["y"] = -100
        self.assertIn("no weapon supply near the lowest player route", self.problems(arena)[1])

    def test_rejects_enemy_on_player_start(self):
        arena = self.trampoline()
        player = next(e for e in arena["elements"] if e["o"] == "Player Spawn Point")
        arena["enemies"][0] = {"x": player["x"], "y": player["y"]}
        self.assertIn("enemy spawn is within 60 of a player spawn", self.problems(arena)[1])


if __name__ == "__main__":
    unittest.main()
