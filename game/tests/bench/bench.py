import argparse, collections, json, os, pathlib, subprocess, sys
from concurrent.futures import ThreadPoolExecutor

ROOT = pathlib.Path(__file__).resolve().parents[3]
DLL = ROOT / "game" / "tests" / "SimCheck" / "bin" / "Release" / "net8.0" / "SimCheck.dll"
KIND = [("ЗАХВАТ БАШНЕЙ", "башня"), ("ПРИВОДНЕНИЕ", "море"), ("КАСАНИЕ", "вне"), ("РАЗРУШ", "разбит"), ("ПРОГАР", "разбит")]
BAD = {"разбит", "вне", "полёт"}


def decode(b):
    try:
        return b.decode("utf-8")
    except UnicodeDecodeError:
        return b.decode("cp866", errors="replace")


def one(mission, seed, extra):
    flags = subprocess.BELOW_NORMAL_PRIORITY_CLASS if os.name == "nt" else 0
    p = subprocess.run(["dotnet", str(DLL), "mission", mission, "--quiet", "--log", "--seed", str(seed)] + extra,
                       capture_output=True, cwd=ROOT, creationflags=flags)
    text = decode(p.stdout + p.stderr)
    res = {}
    for tag in ("Б", "К"):
        res[tag] = "полёт"
        for line in text.splitlines():
            if f"{tag}: " not in line:
                continue
            for key, name in KIND:
                if key in line:
                    res[tag] = name
    return mission, seed, res


def run(a):
    extra = [x for x in a.extra if x != "--"]
    jobs = [(m, s) for m in ("orbital", "high") for s in range(1, a.seeds + 1)]
    with ThreadPoolExecutor(a.workers) as ex:
        rs = list(ex.map(lambda j: one(j[0], j[1], extra), jobs))
    out = {"set": " ".join(extra),
           "booster": dict(collections.Counter(r[2]["Б"] for r in rs)),
           "ship": dict(collections.Counter(r[2]["К"] for r in rs)),
           "bad": [[m, s, r] for m, s, r in rs if BAD & set(r.values())]}
    print(out["set"], "ускоритель", out["booster"])
    print(out["set"], "корабль", out["ship"])
    print("  плохие:", out["bad"])
    if a.json:
        pathlib.Path(a.json).write_text(json.dumps(out, ensure_ascii=False), encoding="utf-8")


def fmt(c):
    return ", ".join(f"{k} {v}" for k, v in sorted(c.items()))


def report(a):
    sets = [json.loads(p.read_text(encoding="utf-8")) for p in sorted(pathlib.Path(a.dir).rglob("*.json"))]
    base = {}
    if a.baseline and pathlib.Path(a.baseline).exists():
        base = {s["set"]: s for s in json.loads(pathlib.Path(a.baseline).read_text(encoding="utf-8"))}
    print("| набор | ускоритель | корабль | плохие | против базы |")
    print("|---|---|---|---|---|")
    for s in sorted(sets, key=lambda s: s["set"]):
        b = base.get(s["set"])
        diff = "—" if b is None else ("как было" if (b["booster"], b["ship"]) == (s["booster"], s["ship"])
                                      else f"было: {fmt(b['booster'])} / {fmt(b['ship'])}")
        bad = "; ".join(f"{m} {sd} {r}" for m, sd, r in s["bad"]) or "—"
        print(f"| `{s['set'] or 'штатно'}` | {fmt(s['booster'])} | {fmt(s['ship'])} | {bad} | {diff} |")
    if a.write:
        pathlib.Path(a.write).write_text(json.dumps(sorted(sets, key=lambda s: s["set"]), ensure_ascii=False, indent=1),
                                         encoding="utf-8")


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("run")
    r.add_argument("--seeds", type=int, default=20)
    r.add_argument("--workers", type=int, default=max(1, (os.cpu_count() or 4) - 2))
    r.add_argument("--json")
    r.add_argument("extra", nargs=argparse.REMAINDER)
    p = sub.add_parser("report")
    p.add_argument("dir")
    p.add_argument("--baseline")
    p.add_argument("--write")
    a = ap.parse_args()
    if sys.stdout.encoding.lower() != "utf-8":
        sys.stdout.reconfigure(encoding="utf-8")
    run(a) if a.cmd == "run" else report(a)


main()
