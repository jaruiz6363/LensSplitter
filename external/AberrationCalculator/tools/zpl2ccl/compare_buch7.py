"""Compare one run of buch7.ccl with AberrationCalculator's surfaces.tsv.

    python tools/zpl2ccl/compare_buch7.py <report> <lens>.surfaces.tsv

<report> holds ONE run (buch7 appends each run to its report file; cut out the block that starts
"BUCH7 (OSLO)"). Compared against the C#:

  - the third order per surface, and the fifth order and B7 per surface as intrinsic, induced
    and total - all unconverted, as buch7 prints them and as surfaces.tsv holds them;
  - the eighteen system totals and the twenty tau, in transverse measure;
  - the twenty tau per surface (the total; surfaces.tsv does not split them).

And within the report: every tau's intrinsic plus induced equals its total, per surface and for
the system. Values below 1e-15 on both sides are round-off and are not compared.
"""
import csv, re, sys

rep, tsv = sys.argv[1], sys.argv[2]
rows = [r for r in csv.reader((l for l in open(tsv) if not l.startswith("#")), delimiter="\t") if r]
head = rows[0]
ac = {(r[0], r[1]): dict(zip(head[2:], (float(x) for x in r[2:]))) for r in rows[1:]}
fn = None
for line in open(tsv):
    m = re.match(r"# \((-?[\d.]+(?:[eE][-+]?\d+)?)\)", line)
    if m:
        fn = float(m.group(1))
txt = open(rep).read()
num = r"-?\d\.\d+e[+-]\d+"


def worst_of(pairs, label):
    w, wk = -1.0, None
    big = max((abs(b) for _, _, b in pairs), default=1.0) or 1.0
    n = 0
    for k, a, b in pairs:
        if abs(a) < 1e-15 and abs(b) < 1e-15:
            continue
        n += 1
        r = abs(a - b) / max(abs(b), 1e-9 * big)
        if r > w:
            w, wk = r, (k, a, b)
    print(f"{label}: {n} compared, worst relative difference {w:.2e} at {wk[0]} "
          f"(report {wk[1]:.7e}, reference {wk[2]:.7e})")


def ref(surf, part, col):
    key = {"int": "intrinsic", "ind": "induced", "tot": "surface_total"}[part]
    return ac.get((surf, key), {}).get(col, 0.0)


# ---- third order, per surface (intrinsic = total at third order)
pairs = []
sec = txt.split("Third order, per surface", 1)[1].split("SUM", 1)[0]
for line in sec.split("\n"):
    t = line.split()
    if len(t) == 6 and t[0].isdigit():
        for c, v in zip(["b", "f", "c", "pi", "e"], t[1:]):
            pairs.append(((t[0], c), float(v), ref(t[0], "int", c)))
worst_of(pairs, "third order per surface")

# ---- fifth order, per surface: int / ind / tot
pairs, cols, surf = [], None, None
sec = txt.split("Fifth order, per surface", 1)[1].split("Surf                B7", 1)[0]
for line in sec.split("\n"):
    t = line.split()
    if not t:
        continue
    if t[0] == "Surf":
        cols = [c.lower() for c in t[1:]]; continue
    if t[0].isdigit():
        surf, t = t[0], t[1:]
    if t and t[0] in ("int", "ind", "tot"):
        for c, v in zip(cols, t[1:]):
            pairs.append(((surf, t[0], c), float(v), ref(surf, t[0], c)))
worst_of(pairs, "fifth order per surface, intrinsic / induced / total")

# ---- B7 per surface
pairs = []
sec = txt.split("(seventh-order spherical aberration)", 1)[1].split("System totals", 1)[0]
for line in sec.split("\n"):
    m = re.match(r"\s*(\d+)\s+int\s+(%s)\s+ind\s+(%s)\s+tot\s+(%s)" % (num, num, num), line)
    if m:
        for part, v in zip(("int", "ind", "tot"), m.groups()[1:]):
            pairs.append(((m.group(1), part, "b7"), float(v), ref(m.group(1), part, "b7")))
worst_of(pairs, "B7 per surface, intrinsic / induced / total")

# ---- the eighteen system totals
tot = ac[("TOTAL", "transverse")]
pairs = []
s18 = txt.split("System totals, transverse measure", 1)[1].split("Table I", 1)[0]
for name in ["B", "F", "C", "Pi", "E", "B5", "F1", "F2", "M1", "M2", "M3", "N1", "N2", "N3", "C5", "Pi5", "E5", "B7"]:
    m = re.search(r"(?<![A-Za-z0-9])" + re.escape(name) + r"\s+(" + num + ")", s18)
    pairs.append((name, float(m.group(1)), tot[name.lower()]))
worst_of(pairs, "18 system totals")

# ---- tau, per surface and for the system
def tau_blocks(text):
    """{(label, part, tauN): value} from blocks of 'tauA .. tauE' headers and tot/int/ind rows."""
    out, cols = {}, None
    for line in text.split("\n"):
        t = line.split()
        if t and all(re.fullmatch(r"tau\d+", x) for x in t):
            cols = t; continue
        if t and t[0] in ("tot", "int", "ind") and cols:
            for c, v in zip(cols, t[1:]):
                out[(t[0], c)] = float(v)
    return out

per_surface = txt.split("Seventh order, per surface", 1)[1].split("Seventh order, system totals", 1)[0]
system = txt.split("Seventh order, system totals", 1)[1].split("tau1 above", 1)[0]
pairs, adds = [], []
for block in re.split(r"\n\s*Surface\s+", per_surface)[1:]:
    s = block.split()[0]
    vals = tau_blocks(block)
    for k in range(1, 21):
        c = "tau%d" % k
        pairs.append(((s, c), vals[("tot", c)], ac[(s, "surface_total")]["b7" if k == 1 else c] * fn))
        adds.append(((s, c), vals[("int", c)] + vals[("ind", c)], vals[("tot", c)]))
worst_of(pairs, "tau per surface (total)")
sv = tau_blocks(system)
worst_of([(("system", "tau%d" % k), sv[("tot", "tau%d" % k)], tot["b7" if k == 1 else "tau%d" % k]) for k in range(1, 21)],
         "20 system tau")
adds += [(("system", "tau%d" % k), sv[("int", "tau%d" % k)] + sv[("ind", "tau%d" % k)], sv[("tot", "tau%d" % k)])
         for k in range(1, 21)]
worst_of(adds, "tau intrinsic + induced = total, within the report")
