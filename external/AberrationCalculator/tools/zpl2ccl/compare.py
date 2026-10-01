"""Compare one run of buch7_asph.ccl with AberrationCalculator's surfaces.tsv, all orders.

    python tools/zpl2ccl/compare.py <report> <lens>.surfaces.tsv

<report> holds ONE run (buch7_asph appends each run to its report file; cut out the block that
starts "BUCH7_ASPH (OSLO)"). The .tsv is what the program writes with -o, or one of those in
ccl/reference. Compared: per surface, intrinsic / figuring / induced / TOTAL for the third and
fifth order and B7, and the twenty tau; for the system, the 18 totals and the twenty tau.
Values below 1e-15 on both sides are round-off and are not compared.
"""
import re, sys, csv

rep, tsv = sys.argv[1], sys.argv[2]
rows = [r for r in csv.reader((l for l in open(tsv) if not l.startswith("#")), delimiter="\t") if r]
head = rows[0]
ac = {}
fn = None
for line in open(tsv):
    m = re.match(r"# \((-?[\d.]+(?:[eE][-+]?\d+)?)\)", line)
    if m:
        fn = float(m.group(1))
for r in rows[1:]:
    ac[(r[0], r[1])] = dict(zip(head[2:], (float(x) for x in r[2:])))
txt = open(rep).read()

def worst_of(pairs, label):
    w, wk = -1.0, None
    big = max(abs(b) for _, _, b in pairs) or 1.0
    for k, a, b in pairs:
        if abs(a) < 1e-15 and abs(b) < 1e-15:
            continue
        r = abs(a - b) / max(abs(b), 1e-9 * big)
        if r > w:
            w, wk = r, (k, a, b)
    print(f"{label}: {len(pairs)} compared, worst relative difference {w:.2e} at {wk[0]} "
          f"(report {wk[1]:.7e}, AC {wk[2]:.7e})")

# ---- per surface, third / fifth / B7 split
pairs = []
surf, cols = None, None
part = {"intrinsic": "intrinsic", "figuring": "aspheric", "induced": "induced"}
sec = txt.split("SURFACE BY SURFACE - intrinsic, figuring, induced.", 1)
if len(sec) > 1:
    for line in sec[1].split("Induced share", 1)[0].split("\n"):
        m = re.match(r"Surface\s+(\d+)", line.strip())
        if m:
            surf = m.group(1); continue
        t = line.split()
        if not t:
            continue
        if t[0] in ("B", "B5", "M3", "Pi5"):
            cols = [c.lower() for c in t]; continue
        if t[0] in part or t[0] == "TOTAL":
            for c, v in zip(cols, t[1:]):
                v = float(v)
                if t[0] == "TOTAL":
                    if cols[0] == "b":   # third-order TOTAL = intrinsic + aspheric
                        ref = sum(ac.get((surf, k), {}).get(c, 0.0) for k in ("intrinsic", "aspheric"))
                    else:
                        ref = ac[(surf, "surface_total")][c]
                else:
                    ref = ac.get((surf, part[t[0]]), {}).get(c, 0.0)
                pairs.append(((surf, t[0], c), v, ref * fn))
    worst_of(pairs, "per-surface third/fifth/B7 entries")

# ---- per surface tau
sec = txt.split("SEVENTH ORDER, SURFACE BY SURFACE", 1)
if len(sec) > 1:
    pairs, cols = [], None
    for line in sec[1].split("\n"):
        t = line.split()
        if not t:
            continue
        if t[0] == "Surf":
            cols = t[1:]; continue
        if cols and re.fullmatch(r"\d+", t[0]):
            for c, v in zip(cols, t[1:]):
                pairs.append(((t[0], c), float(v), ac[(t[0], "surface_total")]["b7" if c == "tau1" else c] * fn))
    worst_of(pairs, "per-surface tau")

# ---- system totals
tot = ac[("TOTAL", "transverse")]
pairs = []
s18 = txt.split("System totals, transverse measure", 1)[1].split("=====", 1)[0].split("Seventh order", 1)[0]
for name in ["B", "F", "C", "Pi", "E", "B5", "F1", "F2", "M1", "M2", "M3", "N1", "N2", "N3", "C5", "Pi5", "E5", "B7"]:
    m = re.search(r"(?<![A-Za-z0-9])" + re.escape(name) + r"\s+(-?\d\.\d+e[+-]\d+)", s18)
    pairs.append((name, float(m.group(1)), tot[name.lower()]))
worst_of(pairs, "18 system totals")
m7 = txt.split("Seventh order, system totals", 1)
if len(m7) > 1:
    pairs = []
    for k in range(1, 21):
        m = re.search(r"tau%d\s+(-?\d\.\d+e[+-]\d+)" % k, m7[1])
        pairs.append(("tau%d" % k, float(m.group(1)), tot["b7" if k == 1 else "tau%d" % k]))
    worst_of(pairs, "20 system tau")
