"""Regenerate the generated part of a CCL port from its ZPL macro.

    python tools/zpl2ccl/regen.py buch7_asph     ccl/buch7_asph.ccl from macros/BUCH7_ASPH.ZPL
    python tools/zpl2ccl/regen.py buch7          ccl/buch7.ccl      from macros/BUCH7.ZPL

Each file has two parts. The OSLO front end - the lens data and the two paraxial rays, from the
top of the file through the ray-trace helper, and the run function and the command at the
bottom - is written by hand. Everything between the marker "// ---- generated from macros/..."
and the run function is the macro, from its stage A on, translated statement for statement:

  zpl2ccl.py  the translation. Every ZPL variable becomes a global (ZPL is case-insensitive and
              has no locals), each SUB a function, PRINT/FORMAT a line built with
              sprintf/strcat and written by the front end's output function. Notes about
              OpticStudio (FIFTHORD, FORBES, the call buffer ROBB reads) are left out.
  split.py    CCL refuses a function past an internal size, so the macro's main body is cut
              into functions of at most 200 lines at statement boundaries; the macro's early
              exit becomes a stop flag.

Both files are compiled into one OSLO, so each has its own prefixes: z_ and b7_ for
buch7_asph, s7_ and bs_ for buch7. The arrays are sized for 16 surfaces between object and
image (CONFIGS in zpl2ccl.py), to fit the 1 MB of global storage CCL shares with OSLO's own CCL.
Re-run this after changing a macro, then compile in OSLO and check with compare.py.
"""
import os, re, subprocess, sys, tempfile

MACROS = {  # name: (ccl file, generated-name prefix, front-end prefix)
    "buch7_asph": ("buch7_asph.ccl", "z_", "b7_"),
    "buch7": ("buch7.ccl", "s7_", "bs_"),
}
name = sys.argv[1] if len(sys.argv) > 1 else "buch7_asph"
ccl_name, gp, fp = MACROS[name]

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.dirname(os.path.dirname(here))
ccl = os.path.join(root, "ccl", ccl_name)

gen = os.path.join(tempfile.mkdtemp(), "gen.ccl")
subprocess.run([sys.executable, os.path.join(here, "zpl2ccl.py"), name, gen], check=True)
t = open(gen, encoding="utf-8").read().replace(
    "System totals, transverse measure - these are what FIFTHORD prints", "System totals, transverse measure")
# BUCH7's closing note loses its pointer to OpticStudio's tools (line 1510 is dropped).
t = t.replace("the F/number, which is done for their totals. Check the third order against",
              "the F/number, which is done for their totals.")
open(gen, "w", encoding="utf-8").write(t)
subprocess.run([sys.executable, os.path.join(here, "split.py"), gen], check=True)
g = open(gen, encoding="utf-8").read()

# The translator and splitter name everything z_ and b7_; give this file its own prefixes.
if gp != "z_":
    g = re.sub(r"(?<![A-Za-z0-9_])z_", gp, g)
if fp != "b7_":
    g = re.sub(r"(?<![A-Za-z0-9_])b7_", fp, g)

src = open(ccl, encoding="utf-8").read()
a = src.index("// ---- generated from macros/")
b = src.index("static int\n%srun(" % fp)
out = src[:a] + g + "\n" + src[b:]
if out == src:
    print("unchanged")
else:
    open(ccl, "w", encoding="utf-8").write(out)
    print("regenerated", ccl)
