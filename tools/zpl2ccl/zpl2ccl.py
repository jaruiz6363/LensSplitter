"""Translate a ZPL macro (BUCH7_ASPH.ZPL or BUCH7.ZPL, from its stage A on) into CCL, mechanically.

    python zpl2ccl.py <buch7_asph | buch7> <output file>

Every ZPL variable becomes a static global z_<lowercase name> (ZPL is case-insensitive and
has no locals). Arrays become static 2-D arrays of fixed size. SUBs become static int
functions. PRINT/FORMAT become sprintf/strcat into b7_ln and b7_out().
"""
import os, re, sys

MACROS = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "macros")

# One configuration per macro: which source lines (1-based, inclusive) are the main body and the
# subroutines, which to drop, and the fixed array sizes - sized for at most 16 surfaces between
# object and image (nsm <= 16), with the macro's 1-based indices fitting.
CONFIGS = {
    "buch7_asph": dict(
        src="BUCH7_ASPH.ZPL", main=(406, 1893), subs=(1896, 3783),
        drop=set(range(908, 920)) | set(range(1781, 1796)),   # FIFTHORD / FORBES notes (OpticStudio)
        arrays={
            "vfm": (19, 9), "cub": (3, 13), "mon": (7, 71), "scr": (3, 69), "mq": (37, 19), "fo": (19, 61),
            "pw": (41, 67), "pc": (19, 113), "pcf": (19, 5), "tt": (79, 161), "yf": (79, 31), "rv": (79, 80),
            "ai": (79, 9), "bw": (5, 10), "sc": (13, 13), "dh": (9, 9), "tz": (5, 13), "po": (13, 13),
            "sv": (6, 33), "pt": (19, 23), "tw": (19, 23),
        },
        vecs={"vec2": 520, "vec3": 520, "vec4": 42},
        int_arrays={"mon"},
        extra_int="""ga gb gc pmsa pmsb pmsc pmx pma pmb pmc pmd pme pmf pqs pqq ecs eqs brs brp brg brm brw
brr tig tib tipr tipn tjg tjb tjp psr psb sqr sqb sqf fmr fmb fmd fmc tzb mb nmon km grun gdual gfig nruns
tdb tdd tdg tddg k1 scb scst flatbad anyfig anyr2 finite srf chk dbg nsm anymir kc"""),
    "buch7": dict(
        src="BUCH7.ZPL", main=(240, 1683), subs=None,
        # 1300-1312 compare OpticStudio's field (MAXF) with the chief ray; OSLO's chief ray is
        # built from ANG, so there is nothing to compare. 615 and 1510 send the reader to
        # FIFTHORD and OpticStudio's Seidel analysis. Stage E (the call buffer) is past 1683.
        drop=set(range(1300, 1313)) | {615, 1510},
        arrays={"tt": (19, 251)},
        vecs={"vec1": 64, "vec2": 520, "vec3": 520, "vec4": 2640},
        int_arrays=set(),
        extra_int="finite q9 nsm"),
}
CFG = CONFIGS[sys.argv[1]]
SRC = os.path.join(MACROS, CFG["src"])
lines = open(SRC, encoding="utf-8", errors="replace").read().split("\n")
MAIN, SUBS, DROP = CFG["main"], CFG["subs"], CFG["drop"]
ARRAYS, VECS, INT_ARRAYS = CFG["arrays"], CFG["vecs"], CFG["int_arrays"]
STRINGS = {"unot$": "z_unot"}
EXTRA_INT = set(CFG["extra_int"].split())
KEYWORDS = {"FOR", "NEXT", "IF", "THEN", "ELSE", "ENDIF", "GOSUB", "SUB", "RETURN", "GOTO", "PRINT",
            "FORMAT", "DECLARE", "EXP", "DOUBLE", "LABEL"}

def strip_comment(s):
    out, q = [], False
    for i, ch in enumerate(s):
        if ch == '"':
            q = not q
        if ch == "!" and not q and s[i + 1:i + 2] != "=":
            break
        out.append(ch)
    return "".join(out).rstrip()

def split_top(s, sep=","):
    parts, depth, cur, q = [], 0, "", False
    for ch in s:
        if ch == '"':
            q = not q
        if not q:
            if ch == "(":
                depth += 1
            elif ch == ")":
                depth -= 1
            elif ch == sep and depth == 0:
                parts.append(cur); cur = ""; continue
        cur += ch
    parts.append(cur)
    return [p.strip() for p in parts]

variables = set()
int_vars = set(EXTRA_INT)

def match_paren(s, i):
    """s[i] == '(' - return index of the matching ')'."""
    d = 0
    for j in range(i, len(s)):
        if s[j] == "(":
            d += 1
        elif s[j] == ")":
            d -= 1
            if d == 0:
                return j
    raise ValueError("unbalanced: " + s)

def expr(s, index=False):
    """Translate a ZPL expression."""
    out, i = "", 0
    while i < len(s):
        m = re.match(r"[A-Za-z_][A-Za-z0-9_]*\$?", s[i:])
        if m and not (i > 0 and (s[i - 1].isdigit() or s[i - 1] == ".") and s[i:i + 1] in "eE"):
            name = m.group(0)
            j = i + len(name)
            low = name.lower()
            if j < len(s) and s[j] == "(":
                k = match_paren(s, j)
                args = split_top(s[j + 1:k])
                if low == "abso":
                    out += "fabs(" + expr(args[0]) + ")"
                elif low == "indx":
                    out += "b7_ns[" + expr(args[0], True) + "]"
                elif low == "curv":
                    out += "cv[" + expr(args[0], True) + "]"
                elif low == "thic":
                    out += "th[" + expr(args[0], True) + "]"
                elif low == "isms":
                    out += "(gltyp[" + expr(args[0], True) + "] == 4)"
                elif low in VECS:
                    out += "z_" + low + "[" + expr(args[0], True) + "]"
                elif low in ARRAYS:
                    out += "z_" + low + "".join("[" + expr(a, True) + "]" for a in args)
                else:
                    raise ValueError("unknown function " + name + " in " + s)
                i = k + 1
                continue
            if low in STRINGS:
                out += STRINGS[low]
            else:
                variables.add(low)
                if index:
                    int_vars.add(low)
                out += "z_" + low
            i = j
            continue
        out += s[i]
        i += 1
    return out

fmt = "%12.6f"   # the macro's FORMAT 12.6 before line 406

def ccl_fmt(spec):
    spec = spec.strip().upper()
    e = spec.endswith("EXP")
    # INT prints a whole number; the value is passed as a double, so it is %w.0f.
    spec = spec.replace("EXP", "").replace("INT", "").strip()
    w, d = spec.split(".")
    return "%" + w + "." + d + ("e" if e else "f")

def tr_print(args_s):
    items = split_top(args_s) if args_s.strip() else []
    nl = True
    if items and items[-1] == "":
        items = items[:-1]; nl = False
    if args_s.rstrip().endswith(","):
        nl = False
    code = []
    for it in items:
        if it.startswith('"'):
            code.append('strcat(b7_ln, %s);' % it)
        elif it.lower() in STRINGS:
            code.append('strcat(b7_ln, %s);' % STRINGS[it.lower()])
        else:
            code.append('sprintf(b7_tmp, "%s", 1.0*(%s)); strcat(b7_ln, b7_tmp);' % (fmt, expr(it)))
    if nl:
        code.append("b7_out();")
    return " ".join(code) if code else "b7_out();"

def translate(rng, drop=set()):
    global fmt
    out, indent = [], 1
    for ln in range(rng[0], rng[1] + 1):
        if ln in drop:
            continue
        raw = lines[ln - 1]
        s = strip_comment(raw).strip()
        if not s:
            continue
        u = s.upper()
        pad = "\t" * indent
        if u.startswith("DECLARE") or u.startswith("LABEL"):
            continue
        if u.startswith("SUB "):
            name = s.split()[1].lower()
            out.append("static int\nz_s_%s()\n{" % name)
            indent = 1
            continue
        if u == "RETURN":
            out.append("\treturn (0);\n}\n")
            continue
        if u.startswith("GOTO"):
            out.append(pad + "return (0);")
            continue
        if u.startswith("GOSUB"):
            out.append(pad + "z_s_%s();" % s.split()[1].lower())
            continue
        if u.startswith("FORMAT"):
            fmt = ccl_fmt(s[6:])
            continue
        if re.match(r"PRINT\b", s, re.I):
            out.append(pad + tr_print(s[5:].strip()))
            continue
        if re.match(r"FOR\s+\w+\s*=", s, re.I):
            m = re.match(r"FOR\s+(\w+)\s*=\s*(.*)$", s, re.I)
            v = m.group(1).lower(); int_vars.add(v); variables.add(v)
            a, b, c = split_top(m.group(2))
            step = float(c)
            cmp = "<=" if step > 0 else ">="
            out.append(pad + "for (z_%s = %s; z_%s %s %s; z_%s = z_%s + (%s))" % (v, expr(a), v, cmp, expr(b), v, v, c))
            out.append(pad + "{")
            indent += 1
            continue
        if u == "NEXT":
            indent -= 1
            out.append("\t" * indent + "}")
            continue
        if u == "ELSE":
            out.append("\t" * (indent - 1) + "}\n" + "\t" * (indent - 1) + "else\n" + "\t" * (indent - 1) + "{")
            continue
        if u == "ENDIF":
            indent -= 1
            out.append("\t" * indent + "}")
            continue
        if re.match(r"IF\s*\(", s, re.I):
            k = s.index("(")
            e = match_paren(s, k)
            cond = s[k:e + 1]
            rest = s[e + 1:].strip()
            if rest.upper().startswith("THEN"):
                stmt = rest[4:].strip()
                # the ZPL mirror flips: the index here is already signed by the mirror count
                if "ISMS" in cond.upper() and re.fullmatch(r"(\w+)\s*=\s*-\s*\1", stmt):
                    continue
                out.append(pad + "if " + expr(cond) + " { " + tr_stmt(stmt) + " }")
            else:
                out.append(pad + "if " + expr(cond))
                out.append(pad + "{")
                indent += 1
            continue
        out.append(pad + tr_stmt(s))
    return out

def tr_stmt(s):
    k = split_assign(s)
    lhs, rhs = k
    if lhs.lower() in STRINGS:
        return "strcpy(%s, %s);" % (STRINGS[lhs.lower()], rhs.strip())
    if lhs.upper().startswith("PRINT"):
        return tr_print(lhs[5:])
    return "%s = %s;" % (expr(lhs), expr(rhs))

def split_assign(s):
    depth = 0
    for i, ch in enumerate(s):
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        elif ch == "=" and depth == 0 and s[i + 1:i + 2] != "=" and s[i - 1:i] not in "!<>=":
            return s[:i].strip(), s[i + 1:].strip()
    raise ValueError("not an assignment: " + s)

subs = translate(SUBS) if SUBS else []
main = translate(MAIN, DROP)

# The header supplies these; they must not be re-declared by accident as int.
decl = []
ints = sorted(v for v in variables if v in int_vars)
dbls = sorted(v for v in variables if v not in int_vars)
for group, typ in ((ints, "int"), (dbls, "double")):
    for i in range(0, len(group), 10):
        decl.append("static %s    %s;" % (typ, ", ".join("z_" + v for v in group[i:i + 10])))
for n, (r, c) in ARRAYS.items():
    decl.append("static %s z_%s[%d][%d];" % ("int   " if n in INT_ARRAYS else "double", n, r, c))
for n, sz in VECS.items():
    decl.append("static double z_%s[%d];" % (n, sz))
decl.append("static char   z_unot[100];")
decl.append("static char   b7_tmp[100];")

zero = ["static int\nz_zero()\n{", "\tint zi, zj;"]
for n, (r, c) in ARRAYS.items():
    zero.append("\tfor (zi = 0; zi < %d; zi++) for (zj = 0; zj < %d; zj++) z_%s[zi][zj] = 0;" % (r, c, n))
for n, sz in VECS.items():
    zero.append("\tfor (zi = 0; zi < %d; zi++) z_%s[zi] = 0.0;" % (sz, n))
zero.append("\treturn (0);\n}\n")

open(sys.argv[2], "w", encoding="utf-8").write(
    "// ---- generated from macros/%s by zpl2ccl.py: globals ----\n" % CFG["src"] + "\n".join(decl) + "\n\n"
    + "\n".join(zero) + "\n"
    + "// ---- generated: the macro's subroutines ----\n" + "\n".join(subs) + "\n"
    + "// ---- generated: the macro's body from stage A on ----\nstatic int\nz_main()\n{\n" + "\n".join(main)
    + "\n\treturn (0);\n}\n")

# Report ints assigned something that looks real, for review.
print("variables:", len(variables), "ints:", len(ints))
for ln in range(1, len(lines) + 1):
    s = strip_comment(lines[ln - 1]).strip()
    m = re.match(r"(\w+)\s*=\s*(.+)$", s)
    if m and m.group(1).lower() in int_vars and not s.upper().startswith("FOR"):
        rhs = m.group(2)
        toks = [t.lower() for t in re.findall(r"[A-Za-z_]\w*", rhs)]
        bad = [t for t in toks if t not in int_vars and t not in ("mon",) and not t.isupper()]
        if "/" in rhs or bad or re.search(r"\d\.\d|E-", rhs):
            print("REVIEW int assign line %d: %s" % (ln, s))
