"""Tests for the balancing model of tools/dev/genera_finanza.py (stdlib unittest only).

Run from the repo root:  python -m unittest tools/dev/test_genera_finanza.py
"""
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest

DEV_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(DEV_DIR, "..", ".."))
CONTENT = os.path.join(REPO, "data", "content.json")
SCRIPT = os.path.join(DEV_DIR, "genera_finanza.py")
if DEV_DIR not in sys.path:
    sys.path.insert(0, DEV_DIR)

import genera_finanza  # noqa: E402


def load_content():
    with open(CONTENT, encoding="utf-8") as fh:
        return json.load(fh)


def run(*args):
    return subprocess.run([sys.executable, SCRIPT, *args], capture_output=True, text=True, encoding="utf-8")


class BalanceModelTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.content = load_content()
        cls.model = genera_finanza.balance(cls.content)

    def test_everything_unlocks_in_about_six_hours_without_jackpot(self):
        hours = self.model["hours"][0]
        self.assertGreaterEqual(hours, 5.5)
        self.assertLessEqual(hours, 6.5)

    def test_jackpots_shorten_the_game(self):
        hours = self.model["hours"]
        self.assertLess(hours[1], hours[0])
        self.assertLessEqual(hours[2], hours[1])

    def test_research_money_totals_22_to_27_point_63_million(self):
        # 25.5 M since the impact drill (Ricerca allargata, plan 1), 26.7 M since the stack conveyor, 26.72 M since
        # the long bridge and 26.74 M since the solar panels (plan 2), 27.57 M since the upgrades (plan 3, task 1,
        # provisional minutes), 27.63 M since the campaign rewards at ×3 (task 2); plan 3 rebalances the whole tree.
        total = sum(node["money"] for node in self.model["nodes"])
        self.assertGreaterEqual(total, 22_000_000)
        self.assertLessEqual(total, 27_630_000)

    def test_a_ten_thousand_titanium_jackpot_is_about_five_and_a_half_million(self):
        self.assertGreater(self.model["jackpot"], 5_000_000)
        self.assertLess(self.model["jackpot"], 6_000_000)

    def test_defense_row_is_zero_for_now(self):
        self.assertEqual([0, 0, 0, 0], [phase["defense"] for phase in genera_finanza.PHASES])

    def test_every_research_node_has_a_cost_and_no_other(self):
        priced = {s["id"] for s in self.content["structures"] if s.get("unlock") is not None}
        self.assertEqual(priced, {node["id"] for node in self.model["nodes"]})

    # The inverted sorter costs the same as the sorter (logistics spec, like Mindustry).
    SAME_COST = {("sorter", "sorter-inverted")}

    def test_costs_increase_along_every_prerequisite_chain(self):
        money = {node["id"]: node["money"] for node in self.model["nodes"]}
        for s in self.content["structures"]:
            for pre in s.get("prerequisites", []):
                if s["id"] in money and pre in money:
                    if (pre, s["id"]) in self.SAME_COST:
                        self.assertEqual(money[s["id"]], money[pre], f"{pre} → {s['id']}")
                    else:
                        self.assertGreater(money[s["id"]], money[pre], f"{pre} → {s['id']}")

    def test_the_inverted_sorter_costs_the_same_as_the_sorter(self):
        nodes = {node["id"]: node for node in self.model["nodes"]}
        self.assertEqual(nodes["sorter"]["money"], nodes["sorter-inverted"]["money"])
        self.assertEqual(nodes["sorter"]["materials"], nodes["sorter-inverted"]["materials"])

    def test_examples_of_the_spec_are_roughly_respected(self):
        money = {node["id"]: node["money"] for node in self.model["nodes"]}
        for node_id, expected in [("smelter", 1_500), ("market-screen", 150_000), ("core-foundation", 1_500_000),
                                  ("core-nucleus", 7_000_000), ("finance-odds", 4_000_000)]:
            self.assertGreaterEqual(money[node_id], expected * 0.8, node_id)
            self.assertLessEqual(money[node_id], expected * 1.2, node_id)

    def test_materials_keep_their_items_and_grow(self):
        current = {s["id"]: s["unlock"] for s in self.content["structures"] if s.get("unlock") is not None}
        for node in self.model["nodes"]:
            items = [m["itemId"] for m in node["materials"]]
            self.assertEqual(items, [m["itemId"] for m in current[node["id"]]["materials"]], node["id"])
            self.assertEqual(items, [m["itemId"] for m in node["materials_base"]], node["id"])
            for m, base in zip(node["materials"], node["materials_base"]):
                self.assertIsInstance(m["amount"], int)
                self.assertGreater(m["amount"], base["amount"], f"{node['id']} {m['itemId']}")

    def test_the_four_campaign_rewards_are_chapters_one_to_four(self):
        self.assertEqual({"combustion-generator": 1, "conveyor-stack": 2, "circuit": 3, "drone-boost": 4},
                         genera_finanza.REWARDS)
        in_content = {s["id"]: s["campaignReward"] for s in self.content["structures"] if "campaignReward" in s}
        self.assertEqual(genera_finanza.REWARDS, in_content)

    def test_a_reward_costs_three_times_the_sheet_in_free_play(self):
        self.assertEqual(3, genera_finanza.REWARD_FACTOR)
        nodes = {node["id"]: node for node in self.model["nodes"]}
        for node_id, minutes in ((n[0], n[2]) for n in genera_finanza.NODES):
            node = nodes[node_id]
            sheet = genera_finanza.nice(self.model["incomes"][node["phase"]] * minutes * 60)
            factor = 3 if node_id in genera_finanza.REWARDS else 1
            self.assertEqual(sheet, node["sheet_money"], node_id)
            self.assertEqual(sheet * factor, node["money"], node_id)
            phase_factor = genera_finanza.PHASES[node["phase"]]["materials_factor"]
            for m, base in zip(node["materials"], node["materials_base"]):
                self.assertEqual(base["amount"] * phase_factor * factor, m["amount"], f"{node_id} {m['itemId']}")

    def test_the_hours_pay_the_rewards_at_three_times(self):
        # The model buys every node at the written price: without the ×3 the tree would unlock sooner.
        cheap = [dict(n, money=n["sheet_money"]) for n in self.model["nodes"]]
        seconds, _ = genera_finanza.unlock_time(cheap, self.model["incomes"], 0, self.model["jackpot_extra"])
        self.assertLess(seconds / 3600, self.model["hours"][0])

    def test_purchase_order_respects_prerequisites(self):
        bought = set(s["id"] for s in self.content["structures"] if s.get("unlock") is None)
        for node in self.model["nodes"]:
            structure = next(s for s in self.content["structures"] if s["id"] == node["id"])
            for pre in structure.get("prerequisites", []):
                self.assertIn(pre, bought, f"{node['id']} before {pre}")
            bought.add(node["id"])


class WriteAndCheckTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp()
        self.copy = os.path.join(self.tmp, "content.json")
        shutil.copyfile(CONTENT, self.copy)

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def test_repo_content_matches_the_sheet(self):
        result = run("--controlla", CONTENT)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_check_fails_on_a_changed_cost_and_passes_after_writing(self):
        with open(self.copy, encoding="utf-8") as fh:
            text = fh.read()
        text = re.sub(r'("id": "smelter",.*?"money": )(\d+)', lambda m: m.group(1) + str(int(m.group(2)) + 1), text,
                      count=1, flags=re.S)
        with open(self.copy, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)

        failed = run("--controlla", self.copy)
        self.assertNotEqual(0, failed.returncode)
        self.assertIn("smelter", failed.stdout + failed.stderr)

        written = run("--scrivi-costi", self.copy)
        self.assertEqual(0, written.returncode, written.stdout + written.stderr)
        passed = run("--controlla", self.copy)
        self.assertEqual(0, passed.returncode, passed.stdout + passed.stderr)

    def test_writing_changes_only_the_unlock_objects(self):
        with open(self.copy, encoding="utf-8") as fh:
            before = json.load(fh)
        for s in before["structures"]:
            if s.get("unlock") is not None:
                s["unlock"]["money"] = 1
        for c in before["conveyors"]:
            if c.get("unlock") is not None:
                c["unlock"]["money"] = 1
        with open(self.copy, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(json.dumps(before, indent=2, ensure_ascii=False) + "\n")
        with open(self.copy, encoding="utf-8") as fh:
            original_text = fh.read()

        self.assertEqual(0, run("--scrivi-costi", self.copy).returncode)
        with open(self.copy, encoding="utf-8") as fh:
            text = fh.read()
        after = json.loads(text)
        self.assertTrue(text.endswith("}\n"))
        # Same lines outside the unlock objects; same keys and order everywhere.
        strip = re.compile(r'"unlock": \{.*?\n      \}', re.S)
        self.assertEqual(strip.sub("U", original_text), strip.sub("U", text))
        for key in before:
            if key not in ("structures", "conveyors"):
                self.assertEqual(before[key], after[key])
        model = {n["id"]: n for n in genera_finanza.balance(after)["nodes"]}
        for c in after["conveyors"]:
            if c.get("unlock") is not None:
                self.assertEqual(model[c["id"]]["money"], c["unlock"]["money"])

    def test_one_line_unlocks_stay_on_one_line(self):
        self.assertEqual(0, run("--scrivi-costi", self.copy).returncode)
        with open(CONTENT, encoding="utf-8") as fh:
            original = fh.read()
        with open(self.copy, encoding="utf-8") as fh:
            text = fh.read()
        self.assertEqual(original.count("\n"), text.count("\n"))


if __name__ == "__main__":
    unittest.main()
