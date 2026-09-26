import pathlib, sys, unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import bench

LOG = """LOG T+6402 К: ЗАХВАТ БАШНЕЙ — руки сомкнулись (2.4 м/с, по центру)
LOG T+ 520 Б: ЗАХВАТ БАШНЕЙ — руки сомкнулись (3.7 м/с, по центру)
SUM pay=67.0t bProp=43.2t bMaxQ=161kPa sProp=32.3t sMaxQ=5kPa sMaxTile=1277K sMode=caught
BURN bBurn=22.2s sBurn=21.3s bTouch=3.74m/s sTouch=2.37m/s
FLIP sVhMax=29.8m/s sHover=9.7s sFlipMiss=-4m sFlipVh=0.0m/s
MET sGas=2894kg bGas=1029kg sPf=302.5kPa sPo=NaNkPa cpu=2.77s
"""


def flight(seed, ship="башня", miss=4.0, gas=2900.0):
    return {"mission": "orbital", "seed": seed, "res": {"Б": "башня", "К": ship}, "m": {"sFlipMiss": miss, "sGas": gas}}


def one_set(flights):
    return {"set": "--disp", "missions": "orbital", "flights": flights}


class Bench(unittest.TestCase):
    def test_parse_reads_outcomes_and_numbers(self):
        res, m = bench.parse(LOG)
        self.assertEqual(res, {"Б": "башня", "К": "башня"})
        self.assertEqual((m["sFlipMiss"], m["sTouch"], m["sProp"], m["sGas"], m["cpu"]), (4.0, 2.37, 32.3, 2894.0, 2.77))
        self.assertNotIn("sPo", m)

    def test_compare_flags_only_real_regressions(self):
        base = {"--disp": one_set([flight(s) for s in range(1, 11)])}
        same = [one_set([flight(s, miss=4 + s % 3, gas=2900 + s) for s in range(1, 11)])]
        crash = [one_set([flight(s, ship="разбит" if s == 7 else "башня") for s in range(1, 11)])]
        sea3 = [one_set([flight(s, ship="море" if s <= 3 else "башня") for s in range(1, 11)])]
        sea4 = [one_set([flight(s, ship="море" if s <= 4 else "башня") for s in range(1, 11)])]
        miss = [one_set([flight(s, miss=30) for s in range(1, 11)])]
        gas = [one_set([flight(s, gas=3600) for s in range(1, 11)])]
        self.assertEqual(bench.compare(same, base), [])
        self.assertEqual(len(bench.compare(crash, base)), 1)
        self.assertEqual(bench.compare(sea3, base), [])
        self.assertEqual(len(bench.compare(sea4, base)), 1)
        self.assertEqual(len(bench.compare(miss, base)), 1)
        self.assertEqual(len(bench.compare(gas, base)), 1)


if __name__ == "__main__":
    unittest.main()
