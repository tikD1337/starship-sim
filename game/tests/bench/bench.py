import argparse, collections, json, math, os, pathlib, re, statistics, subprocess, sys
from concurrent.futures import ThreadPoolExecutor

ROOT = pathlib.Path(__file__).resolve().parents[3]
DLL = ROOT / "game" / "tests" / "SimCheck" / "bin" / "Release" / "net8.0" / "SimCheck.dll"
KIND = [("ЗАХВАТ БАШНЕЙ", "башня"), ("ПРИВОДНЕНИЕ", "море"), ("КАСАНИЕ", "вне"), ("РАЗРУШ", "разбит"), ("ПРОГАР", "разбит")]
BAD = {"разбит", "вне", "полёт"}
METRICS = [
    ("sFlipMiss", "промах переворота, м", -1, ("abs", 20)),
    ("sTouch", "касание корабля, м/с", -1, ("abs", 0.5)),
    ("bTouch", "касание ускорителя, м/с", -1, ("abs", 0.5)),
    ("sProp", "остаток корабля, т", 1, ("abs", 2)),
    ("bProp", "остаток ускорителя, т", 1, ("abs", 2)),
    ("sGas", "газ ДМТ корабля, кг", -1, ("pct", 20)),
    ("bGas", "газ ДМТ ускорителя, кг", -1, ("pct", 20)),
    ("cpu", "процессор на полёт, с", -1, ("pct", 30)),
    ("sBurn", "жига корабля, с", -1, None),
    ("sHover", "висение корабля, с", -1, None),
    ("sPf", "давление горючего, кПа", 1, None),
    ("sPo", "давление окислителя, кПа", 1, None),
    ("sMaxTile", "нагрев плиток, К", -1, None),
    ("sMaxQ", "напор корабля, кПа", -1, None),
    ("bMaxQ", "напор ускорителя, кПа", -1, None),
]
MAIN = ["sFlipMiss", "sTouch", "sProp", "sGas", "cpu"]
TOWER_DROP = 4


def decode(b):
    try:
        return b.decode("utf-8")
    except UnicodeDecodeError:
        return b.decode("cp866", errors="replace")


def parse(text):
    res = {}
    for tag in ("Б", "К"):
        res[tag] = "полёт"
        for line in text.splitlines():
            if f"{tag}: " not in line:
                continue
            for key, name in KIND:
                if key in line:
                    res[tag] = name
    met = {}
    names = {m[0] for m in METRICS}
    for line in text.splitlines():
        if line.split(" ", 1)[0] not in ("SUM", "BURN", "FLIP", "MET"):
            continue
        for k, v in re.findall(r"(\w+)=(-?\d+(?:\.\d+)?|NaN)", line):
            if k in names and v != "NaN":
                met[k] = abs(float(v)) if k == "sFlipMiss" else float(v)
    return res, met


def one(mission, seed, extra):
    flags = subprocess.BELOW_NORMAL_PRIORITY_CLASS if os.name == "nt" else 0
    p = subprocess.run(["dotnet", str(DLL), "mission", mission, "--quiet", "--log", "--seed", str(seed)] + extra,
                       capture_output=True, cwd=ROOT, creationflags=flags)
    res, met = parse(decode(p.stdout + p.stderr))
    return {"mission": mission, "seed": seed, "res": res, "m": met}


def counts(flights, tag):
    return dict(collections.Counter(f["res"][tag] for f in flights))


def bad(flights):
    return [f for f in flights if BAD & set(f["res"].values())]


def run(a):
    extra = [x for x in a.extra if x != "--"]
    jobs = [(m, s) for m in a.missions.split(",") for s in range(1, a.seeds + 1)]
    with ThreadPoolExecutor(a.workers) as ex:
        flights = list(ex.map(lambda j: one(j[0], j[1], extra), jobs))
    out = {"set": a.name or " ".join(extra) or "штатно", "args": extra, "missions": a.missions, "flights": flights}
    print(out["set"], "ускоритель", counts(flights, "Б"))
    print(out["set"], "корабль", counts(flights, "К"))
    print("  плохие:", [(f["mission"], f["seed"], f["res"]) for f in bad(flights)])
    if a.json:
        pathlib.Path(a.json).write_text(json.dumps(out, ensure_ascii=False), encoding="utf-8")


def load(path):
    sets = []
    for p in sorted(pathlib.Path(path).rglob("*.json")) if pathlib.Path(path).is_dir() else [pathlib.Path(path)]:
        d = json.loads(p.read_text(encoding="utf-8"))
        sets.extend(d if isinstance(d, list) else [d])
    return [s for s in sets if "flights" in s]


def num(x):
    return f"{x:.0f}" if abs(x) >= 100 else (f"{x:.1f}" if abs(x) >= 10 else f"{x:.2f}")


def stat(flights, key, sign):
    vals = [(f["m"][key], f) for f in flights if key in f["m"] and math.isfinite(f["m"][key])
            and (key != "sFlipMiss" or f["res"]["К"] == "башня")]
    if not vals:
        return None
    med = statistics.median(v for v, _ in vals)
    worst = (max if sign < 0 else min)(vals, key=lambda x: x[0])
    return med, worst[0], f"{worst[1]['mission']} {worst[1]['seed']}"


def compare(cur, base):
    fails = []
    for s in cur:
        b = base.get(s["set"])
        if b is None:
            continue
        was = {(f["mission"], f["seed"]) for f in bad(b["flights"])}
        for f in bad(s["flights"]):
            if (f["mission"], f["seed"]) not in was:
                fails.append(f"{s['set']}: новый провал — {f['mission']} {f['seed']} {f['res']}")
        for tag, who in (("Б", "ускоритель"), ("К", "корабль")):
            d = counts(b["flights"], tag).get("башня", 0) - counts(s["flights"], tag).get("башня", 0)
            if d >= TOWER_DROP:
                fails.append(f"{s['set']}: {who} у башни на {d} реже")
        for key, name, sign, gate in METRICS:
            if gate is None:
                continue
            c, o = stat(s["flights"], key, sign), stat(b["flights"], key, sign)
            if c is None or o is None:
                continue
            worse = (o[0] - c[0]) if sign > 0 else (c[0] - o[0])
            lim = gate[1] if gate[0] == "abs" else abs(o[0]) * gate[1] / 100
            if worse > lim:
                fails.append(f"{s['set']}: {name} — медиана {num(c[0])} против {num(o[0])}")
    return fails


def fmt(c):
    return ", ".join(f"{k} {v}" for k, v in sorted(c.items()))


def cell(s, b, key, sign):
    c = stat(s["flights"], key, sign)
    if c is None:
        return "—"
    o = stat(b["flights"], key, sign) if b else None
    if o is None:
        return num(c[0])
    d = c[0] - o[0]
    return f"{num(c[0])} ({'+' if d >= 0 else '−'}{num(abs(d))})"


def report(a):
    cur = load(a.dir)
    base = {s["set"]: s for s in load(a.baseline)} if a.baseline and pathlib.Path(a.baseline).exists() else {}
    fails = compare(cur, base) if base else []
    out = []
    head = "Стенд: регрессий нет" if not fails else f"Стенд: регрессии — {len(fails)}"
    out.append(f"### {'✅' if not fails else '❌'} {head}")
    out.append(a.note or ("база — прогон main" if base else "базы нет: сравнение появится после первого прогона в main"))
    out.extend(f"- {x}" for x in fails)
    out.append("")
    names = {m[0]: (m[1], m[2]) for m in METRICS}
    out.append("| набор | ускоритель | корабль | " + " | ".join(names[k][0] for k in MAIN) + " | плохие |")
    out.append("|---|---|---|" + "---|" * len(MAIN) + "---|")
    for s in sorted(cur, key=lambda s: s["set"]):
        b = base.get(s["set"])
        bd = "; ".join(f"{f['mission']} {f['seed']} {f['res']}" for f in bad(s["flights"])) or "—"
        row = [f"`{s['set']}`", fmt(counts(s["flights"], "Б")), fmt(counts(s["flights"], "К"))]
        row += [cell(s, b, k, names[k][1]) for k in MAIN] + [bd]
        out.append("| " + " | ".join(row) + " |")
    out.append("")
    out.append("<details><summary>Все цифры: медиана, худшее зерно, база</summary>")
    out.append("")
    for s in sorted(cur, key=lambda s: s["set"]):
        b = base.get(s["set"])
        out.append(f"**`{s['set']}`** ({s['missions']})")
        out.append("")
        out.append("| величина | медиана | худшее | база |")
        out.append("|---|---|---|---|")
        for key, name, sign, _ in METRICS:
            c = stat(s["flights"], key, sign)
            if c is None:
                continue
            o = stat(b["flights"], key, sign) if b else None
            out.append(f"| {name} | {num(c[0])} | {num(c[1])} ({c[2]}) | {'—' if o is None else num(o[0])} |")
        out.append("")
    out.append("</details>")
    text = "\n".join(out) + "\n"
    if a.out:
        pathlib.Path(a.out).write_text(text, encoding="utf-8")
    print(text)
    if a.write:
        pathlib.Path(a.write).write_text(json.dumps(sorted(cur, key=lambda s: s["set"]), ensure_ascii=False),
                                         encoding="utf-8")
    return 1 if fails and a.gate else 0


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("run")
    r.add_argument("--seeds", type=int, default=20)
    r.add_argument("--missions", default="orbital,high")
    r.add_argument("--name")
    r.add_argument("--workers", type=int, default=max(1, (os.cpu_count() or 4) - 2))
    r.add_argument("--json")
    r.add_argument("extra", nargs=argparse.REMAINDER)
    p = sub.add_parser("report")
    p.add_argument("dir")
    p.add_argument("--baseline")
    p.add_argument("--write")
    p.add_argument("--out")
    p.add_argument("--note")
    p.add_argument("--gate", action="store_true")
    a = ap.parse_args()
    if sys.stdout.encoding.lower() != "utf-8":
        sys.stdout.reconfigure(encoding="utf-8")
    if a.cmd == "run":
        run(a)
        return 0
    return report(a)


if __name__ == "__main__":
    sys.exit(main())
