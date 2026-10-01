# User Guide

`abcalc` reads a lens prescription and reports its aberrations: the Seidel sums, the
Buchdahl/Rimmer coefficients through the **seventh order**, per surface and in total, and the
RMS spot those coefficients predict with no rays traced. It also optimises a design against a
merit function, and it can be driven from Claude through an MCP server. This guide covers
building it, running it, reading its output and optimising with it. How the numbers are
computed, and how they were checked, is in the [README](../README.md) and the documents it
lists.

## 1. What you need

- **Windows.** `abcalc` is a .NET program and may well build elsewhere, but it has been used and
  tested on Windows. The MCP setup window (section 9) and the OpticStudio tools (section 10)
  are Windows-only.
- **The .NET 8 SDK**, from [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).
  It includes the .NET 8 runtime that `abcalc` runs on. A later SDK also builds it, but the
  .NET 8 runtime must still be installed to run it.
- *Optional:* an internet connection once, to set up Python and Optiland for the Optiland
  cross-check in the test suite (section 12).
- *Optional:* Zemax OpticStudio, for the ZPL macros and the `Forbes7` ZOS-API program, which
  compute the same coefficients inside OpticStudio (section 10).
- *Optional:* OSLO, for `buch7_asph.ccl`, which computes them inside OSLO (section 11). OSLO EDU
  is enough.
- *Optional:* Claude Code or Claude Desktop, to use the MCP server (section 9).

To check what .NET is installed - look for an SDK, and for `Microsoft.NETCore.App 8.x` among the
runtimes:

    dotnet --list-sdks
    dotnet --list-runtimes

## 2. Getting and building it

    git clone https://github.com/jaruiz6363/AberrationCalculator.git
    cd AberrationCalculator
    dotnet build AberrationCalculator.sln -c Release

**Name the solution.** The repository root holds two - `AberrationCalculator.sln` (.NET 8: the
program, the MCP server, the tests) and `ForbesAberrationCalculatorZOSAPI.sln` (.NET Framework
4.8, for OpticStudio; section 10) - and a bare `dotnet build` stops and asks which one.

The program is then

    src\AberrationCalculator.Cli\bin\Release\net8.0\abcalc.exe

Run it by that path, or from the repository folder with
`dotnet run --project src\AberrationCalculator.Cli -c Release -- <lens file> [options]`. The
examples below write `abcalc` for short. Inside the repository the program finds the glass
catalogs in `catalogs\` by itself.

## 3. Installing it (optional)

To have `abcalc` in its own folder, independent of the source:

    dotnet publish src\AberrationCalculator.Cli -c Release -o C:\Tools\abcalc

and add `C:\Tools\abcalc` to your PATH. The folder holds everything the program needs: its
libraries, the glass catalogs (`catalogs\Glass`) and the glass-substitution catalog
(`catalogs\Substitution`). The MCP server publishes the same way:

    dotnet publish src\AberrationCalculator.Mcp -c Release -o C:\Tools\abcalc-mcp

To use catalogs of your own instead, pass `--glass <folder>` or set `ABCALC_GLASS_DIR` to a
folder of `.agf` files.

## 4. A first run

    abcalc tests\fixtures\lenses\KingslakeDG.zmx

This analyses Kingslake's double Gauss (EFL 100, F/8, 14° half-field). It prints the report and
writes it, and eleven tab-separated data files, **beside the lens file**. It takes about a
second. The report opens with how the file was understood:

```
File      : ...\tests\fixtures\lenses\KingslakeDG.zmx
Format    : ZMX
Surfaces  : 11   Wavelengths: 3   Fields: 3
Aperture  : EPD = 12.5
Field type: ObjectAngle

PRESCRIPTION
Surf  Type                 Radius    Thickness Material          Index     SemiDiam      Conic  Note
OBJ   Plane              infinity     infinity
1     Sphere               25.907        5.083 SK4            1.612720
2     Sphere              147.341        2.355
...
FIRST ORDER
Effective focal length                 100.0039
Back focal length                      89.8344
F/number                               8.0003
```

**Check this part first**: the surfaces, glasses and indices, the focal length and the F/number
should be what your design program says. If they are not, nothing after them is about your lens.

Where the output goes:

| Option | Effect |
|---|---|
| (none) | report printed, and it and the data files written beside the lens |
| `-o <dir>`, `--out <dir>` | the files go into `<dir>` instead |
| `--no-files` | print only; write nothing |
| `-q`, `--quiet` | write the files; print nothing but errors |

## 5. Reading the report

The report runs in this order. Every section says in its own words what it contains and what it
cannot see; this table is the map.

| Section | What it is |
|---|---|
| PRESCRIPTION | the lens as read: type, radius, thickness, glass, index at the primary wavelength, conic |
| REFRACTIVE INDICES | every glass at every wavelength, and which catalog it came from |
| FIRST ORDER | focal lengths, F/number, pupils, image height, the Lagrange invariant |
| SEIDEL | the third-order sums S1-S5 and the two chromatic ones, per surface and total (Welford's convention) |
| FIELD SURFACES | Petzval, sagittal, medial and tangential field curvature at the full field, and the detector radius that matches the medial surface |
| BUCHDAHL / RIMMER | third order (B F C Π E), fifth order (B5 ... E5) and seventh order (B7 and τ2-τ20). Per surface these are the **intrinsic** parts; the totals include the aspheric and induced parts and carry the F/number, so they are *not* the column sums |
| PREDICTED RMS SPOT (PRMS) | the RMS spot radius those coefficients imply, per field and wavelength, and the weighted average **PRMSA** - integrated analytically over the pupil (Robb 1976), no rays traced |
| BEST FOCUS | where that spot is smallest, per field and wavelength and for the whole field (Sands 1973) |
| WHICH ABERRATION | each coefficient's isolated spot, and its share of the spot the design actually has. A **negative share** means that aberration is cancelling others |
| WHICH SURFACE | each surface's share of the spot, and how much of it is **induced** - generated by acting on aberration handed to it by the surfaces ahead |
| WARNINGS | anything unusual about how the file was read - see section 14 |

Two things worth knowing before relying on the numbers:

- **PRMS is referred to the paraxial image plane** and in lens units. It cannot see defocus or
  apertures - a clipped beam reads the same as an unclipped one - and it is a geometric spot,
  meaningless below the Airy radius. It is most accurate at small field; at the edge of the
  field the ninth order arrives and the seventh-order series under-predicts. The report says
  this where it prints the numbers, and [spot-prediction.md](spot-prediction.md) measures it
  against traced rays.
- **In the BUCHDAHL / RIMMER table, τ2-τ20 read zero per surface.** They are not split into
  intrinsic and induced parts. Each surface's *share* of them is in `<name>.surfaces.tsv` (the
  `surface_total` rows), and the WHICH SURFACE table includes them, so its column adds to 100.

The data files, all tab-separated with full precision, for a script or a spreadsheet:

| File | Contents |
|---|---|
| `<name>.report.txt` | the report above |
| `<name>.prescription.tsv` | one row per surface |
| `<name>.indices.tsv` | one row per glass, one column per wavelength |
| `<name>.firstorder.tsv` | name/value pairs |
| `<name>.paraxial.tsv` | marginal and chief ray at every surface |
| `<name>.seidel.tsv` | Seidel sums per surface, and totals |
| `<name>.buchdahl.tsv` | third, fifth and seventh order, per surface and total |
| `<name>.prms.tsv` | predicted RMS spot per field and wavelength, and PRMSA |
| `<name>.contributions.tsv` | each aberration's isolated spot and share |
| `<name>.surfaces.tsv` | per surface: intrinsic, aspheric, induced, and their total |
| `<name>.surface-share.tsv` | per surface: share of the spot, and induced fraction |

**Lens files:** OpticStudio `.zmx`, CODE V `.seq`, OPTALIX `.otx`/`.opt`, OSLO `.len`/`.osl`,
Optiland `.json` and LensHH-LT `.lhlt`. Centred systems of spheres, conics and even aspheres
(r² to r¹⁶), with the object at infinity or at a finite distance, and mirrors.

## 6. The other reports

Each of these switches produces one specialised report *instead of* the main one, and writes
nothing else (`--nat` also writes a `.nat.tsv`).

| Option | What it answers |
|---|---|
| `--nat` (or `--nodal`) | Nodal aberration theory, third and fifth order: what a tilted, decentred or Zernike-deformed surface does to the field - where each surface's aberration field has moved to and where the nodes are. The perturbation is read from a sidecar, `<lens>.align` (below). [nodal-aberration-theory.md](nodal-aberration-theory.md) |
| `--forbes [d]` | Third, fifth and seventh order by Forbes' series trace, per surface, split into what each surface generates itself and what it induces. Optional truncation degree 3-8, default 3; raising it must not change the answer, which is a check that the series has converged. Not for mirrors. [forbes.md](forbes.md) |
| `--distortion-coefficients` (or `--distortion`) | How far the third, fifth and seventh-order coefficients can be trusted for distortion, against real chief rays, over a ladder of field fractions. [distortion-prediction.md](distortion-prediction.md) |
| `--quaternary-spherical` (or `--quaternary`) | The ninth-order spherical coefficient per surface (Buchdahl paper IV). **Spherical surfaces only**: a figured design is refused, because no aspheric arrangement exists at this order |
| `--asphere-placement` (or `--asphere-where`) | Where an asphere would do the most good: per surface, the ray heights, their ratio H/h, and the r⁴ coefficient that would zero each Seidel sum on its own. Third order only |
| `--screen [h]` | Whether this design would exercise the aspheric seventh-order path; optional field fraction, default 1 |

A perturbation for `--nat` is kept beside the lens, never in it, and is built a line at a time:

    abcalc lens.zmx TILT "2 X 0.115"     surface 2 tilted 0.115 degrees about X
    abcalc lens.zmx DEC "3 Y 0.05"       surface 3 decentred 0.05 lens units in Y
    abcalc lens.zmx ZERN "1 Z5 0.0001"   a Zernike figure error on surface 1, as sag
    abcalc lens.zmx ALIGNLIST            what is out of place
    abcalc lens.zmx --nat

Delete `lens.zmx.align` to restore the nominal design.

## 7. Optimising

`--optimize` changes the design to reduce a **merit function**, and writes the result back into
the same format it came from. Two things have to be said first: what may change (the
**variables**) and what "better" means (the **operands**). Both live in small text files beside
the lens, built by commands:

    abcalc KingslakeDG.zmx VAR "CV 1" VAR "CV 2" VAR "CV 3" VAR "CV 4"
    abcalc KingslakeDG.zmx OP "PRMSA, 1, TAR 0" OP "EFL, 1, TAR 100"
    abcalc KingslakeDG.zmx --optimize

The first line makes the curvatures of surfaces 1-4 variable. The second asks for the smallest
predicted spot while holding the focal length at 100. The third runs it:

```
WHAT CHANGED
  surface  quantity           before             after           change
  1        radius               25.907        28.0230999          +8.17%
  2        radius              147.341       339.1195915        +130.16%
  3        radius               34.804        39.2290974         +12.71%
  4        radius                17.34        18.2267807          +5.11%

OPERANDS
  operand          asked            start              end     share
  PRMSA                 -> 0        0.0010884        0.0024351      0.2%
  ...
  EFL                 -> 100      100.0039392      100.0068238      0.0%

MERIT   0.0301149  ->  0.0117975   (-60.8%)
```

**What changed** is in prescription terms - radii, thicknesses, glasses, conics, aspheric terms.
**Operands** lists each operand at the start and at the end, with its share of the merit that is
left; the largest share is the operand holding the design back.

### The settings files

| | `.lhlt` | every other format |
|---|---|---|
| variables, bounds, pickups | in the lens file itself | `<lens>.var` |
| merit function | `<lens>.mf` | `<lens>.mf` |

Sidecars are named for the lens *including* its extension (`KingslakeDG.zmx.mf`). They are plain
text, and **the command is the file line**: `VAR "TH 2 MIN 1 MAX 25"` writes the line
`VAR TH 2 MIN 1 MAX 25`, so the files can be edited directly, and a file of commands is a valid
settings file.

| Command | Effect |
|---|---|
| `VAR "CV 1"` | surface 1's curvature may move. Kinds: `CV` curvature, `TH` thickness, `CC` conic, `A4` `A6` `A8` aspheric terms |
| `VAR "TH 2 MIN 1.0 MAX 25.0"` | a bounded variable; a later `VAR` line **merges**, so setting a maximum keeps a minimum |
| `VAR "TH 2 FREE"` | drop the bounds |
| `PICKUP "TH 2 INDEX 1 SCALE 1 OFFSET -0.1"` | tie one surface's value to another's |
| `OP "EFL, 100, TAR 50, 2"` | add an operand: type, weight, target (`TAR`, or `MIN`/`MAX`), then its inputs |
| `VARLIST`, `PICKUPLIST`, `OPLIST` | list them, numbered |
| `VARREMOVE 2`, `PICKUPREMOVE 1`, `OPREMOVE 3` | remove by number; the rest renumber |
| `HELP`, `HELP VAR`, `HELP EFL` | every command and operand, or one of them |

The operands include `PRMSA` (the predicted spot), `EFL`, `TTL`, axial and lateral colour,
distortion, edge thicknesses, paraxial and real ray coordinates, and the wavefront error a build
tolerance would cause (`ASBLT`). **Any of the thirty-seven aberration coefficients is an operand
too**, by the name the report prints: `B, 1, TAR 0` for third-order spherical, `Tau15, 1, TAR 0`
for a seventh-order term, and `M2, 5, TAR 0, 5` for surface 5's share alone. `abcalc HELP` lists
the operands with their inputs, and [optimizer.md](optimizer.md) explains each.

A `MIN` or `MAX` operand costs nothing while it is satisfied, so manufacturing limits can be
added freely without pulling the design about. A `.lhlt` states its own variables, which are
read and honoured; its own merit function is not read, and is left alone in the file.

### Methods and options

| Option | Effect |
|---|---|
| `--optimize [mf]` | optimise; the merit function from `<lens>.mf`, or from the file named |
| `--method m` | `psd3` (default: Dilworth's pseudo-second-derivative), `psd2`, `lm` (Levenberg-Marquardt), or `hj` (Hooke-Jeeves, no derivatives) |
| `--iterations n` | a cap on the iterations; default 200 |

The derivatives are exact - computed analytically, not by finite differences - so there is no
step size to tune.

### Where the result goes

| Option | Result |
|---|---|
| (none) | written beside the lens as `<name>.optimised.<ext>`; the original is untouched |
| `--saveas <path>` | written there - in another format if the extension names one (`.zmx .seq .len .otx .json .lhlt`) |
| `--save` | **overwrites** the lens that was read |

The optimisation report is also written, as `<lens>.optimisation.txt`. The design is saved by
**editing** the original file, not regenerating it, so everything this program does not model -
solves, coatings, tolerances, another program's merit function - survives. Only what moved
changes, in the file's own units.

**Conics and aspheric terms are saved into `.lhlt`, `.zmx` and Optiland `.json` only.** For a
CODE V, OSLO or OPTALIX design whose conic or aspheric terms moved, the save is **refused**:
nothing is written, the command exits with an error naming what moved, and the report - which
holds every new value - is written anyway. To keep them in a lens file, work from a `.zmx` or
`.lhlt` copy of the design.

### Basin hopping

A local optimiser finds the bottom of the valley the design starts in. Basin hopping searches
across valleys: it kicks the design, re-optimises, keeps or rejects the result, on several
independent chains at once, and produces **one design per chain**. It therefore needs a folder:

    abcalc lens.zmx --optimize_basin_hopping --save results

| Option | Effect |
|---|---|
| `--hops n` | hops per chain; default 3000 |
| `--chains n` | independent chains; default one per physical core |
| `--seed n` | random seed; default 1234 |
| `--hop-sigma s`, `--initial-sigma s` | size of a hop, and of the first one only |
| `--hop-figuring` | kick conics and aspheric terms too (off by default; they are still optimised) |
| `--glass_substitution CoreSet28` | let the hopping try glasses from that catalog in `catalogs\Substitution` |
| `--iterations n` | cap on the local iterations per hop; default 6000 |

A long run shows its progress. **Press Ctrl+C when the best stops improving**: every chain has
already written its best design as it went (`<name>.chainNN.<ext>`, each with its own `.mf`
and `.var`), so a stopped run loses nothing. The report ranks the chains.

## 8. The base folder

Bare lens names can be taken to mean a folder of your choosing, which saves typing paths:

    abcalc BASE "C:\lenses\project7"     bare names now mean this folder
    abcalc BASELIST                      show it, and where it came from
    abcalc BASEREMOVE                    forget it

It is remembered until changed, and the MCP server reads the same setting. Most specific first,
the folder comes from `--dir <dir>` on the command line, `ABCALC_DIR` in the environment, the
`BASE` setting, and failing those the current directory. An absolute path always means what it
says.

## 9. Using it from Claude (MCP)

`abcalc-mcp` exposes the same calculations to Claude over the Model Context Protocol, so you can
ask about a lens in plain language - "what limits the spot of KingslakeDG.zmx at full field?" -
and Claude reads the file and answers from the coefficients. It is a thin layer over the same
code as the command line, so the two cannot disagree.

The easy way to register it:

    dotnet build tools\mcpsetup -c Release
    tools\mcpsetup\bin\Release\net8.0-windows\mcp-setup.exe

A small window finds the built server (or builds it), lists the Claude clients it found, and
registers it. It backs up your Claude configuration before every write and leaves other servers
alone; there is a **Remove** button. Choose the `user` scope to have it everywhere. **Restart
Claude afterwards** - a client reads its MCP servers only when it starts.

By hand, for Claude Code:

    claude mcp add abcalc -- <repo>\src\AberrationCalculator.Mcp\bin\Release\net8.0\abcalc-mcp.exe

The server's tools cover the report and each of its parts, most of the other reports (the
seventh order by Forbes' trace, nodal aberrations, the ninth order, distortion), optimisation
with the merit function passed as text, and the base folder. Nothing is written
unless a tool is given somewhere to save. [mcp.md](mcp.md) lists the tools and their arguments.

## 10. In OpticStudio

The `macros\` folder holds ZPL macros that compute the same coefficients inside OpticStudio, on
OpticStudio's own lens data. Copy them to your OpticStudio `Macros` folder
(`Documents\Zemax\Macros`) and run them from **Programming > Macro List**.

| Macro | What it does |
|---|---|
| `BUCH7.ZPL` | third, fifth and seventh order (and ninth-order spherical), per surface, intrinsic and induced. Spherical surfaces only |
| `BUCH7_ASPH.ZPL` | the same for conics and even aspheres |
| `FORBES.ZPL` | the same three orders by Forbes' series trace. Not for mirrors |
| `RAYINV.ZPL` | the coefficients recovered from real traced rays - an independent check |
| `ROBB.ZPL` | the predicted RMS spot (PRMS), from BUCH7's coefficients |
| `STRESS.ZPL` | Sasian's lens stress and relaxation parameters |
| `ASPHWHERE.ZPL` | where an asphere would do the most good |

Each is documented, with expected output, in the [macro guide](../macros/README.md).

`zosapi\` holds `Forbes7`, a .NET Framework program that drives OpticStudio through the ZOS-API
and computes the seventh order by Forbes' series trace, separating what each surface's figuring
contributes. It needs OpticStudio installed and its own solution
(`ForbesAberrationCalculatorZOSAPI.sln`); run its `FixBinaries` project first, which finds
OpticStudio. See [zosapi/README.md](../zosapi/README.md).

## 11. In OSLO

The `ccl\` folder holds `buch7_asph.ccl`, the OSLO version of `BUCH7_ASPH.ZPL`. It computes the
Buchdahl coefficients through the seventh order inside OSLO, on OSLO's own lens data, for spherical,
conic and aspheric surfaces (AD, AE, AF).

To install it:

1. Copy `ccl\buch7_asph.ccl` into OSLO's private CCL folder. For OSLO EDU that is
   `C:\Users\Public\Documents\OSLO66 EDU\private\ccl`.
2. Choose **Tools > Compile CCL** and check that the window shows `No errors detected`. OSLO runs
   the version it last compiled, so compile again whenever the file is replaced.
3. Open a lens and type `buch7_asph`.

```
buch7_asph               per surface, current wavelength
buch7_asph 0             system totals only
buch7_asph 1 2           at wavelength 2
buch7_asph 1 0 0         without the report file
```

The text window shows:

- the system totals of the third order, the fifth order, B7 and the twenty seventh-order tau;
- every surface's third order, fifth order and B7, split into **intrinsic** (what the surface
  generates itself), **figuring** (what its conic and aspheric terms add) and **induced** (what the
  aberration from the surfaces ahead produces in it);
- each surface's induced share of its fifth order, which says whether a surface's aberration is its
  own doing or was handed to it;
- the twenty tau surface by surface.

Each run is also added to the end of a report file, `ccl\buch7_asph.txt` in this repository. Change
`B7_REPORT` at the top of `buch7_asph.ccl` to a folder on your machine.

It takes at most 16 surfaces between object and image. On the test lenses it agrees with this
program to the last printed digit, so a lens opened in both gives the same coefficients.

For a lens of spheres the folder also holds `buch7.ccl`, the OSLO version of `BUCH7.ZPL`. Install
it the same way and type `buch7`, or `buch7 1` to add the ninth-order spherical aberration. On
spheres it gives the same coefficients as `buch7_asph`, and adds:

- the twenty seventh-order tau split into intrinsic and induced, per surface and for the system;
- the ninth-order spherical aberration per surface, intrinsic and total;
- Buchdahl's Table I entry by entry.

It declines a lens with a conic or an aspheric term; use `buch7_asph` for those. Its report goes to
`ccl\buch7.txt`.

[ccl/README.md](../ccl/README.md) has the details for both files: the limits, how they are built
from the ZPL macros, and how they were checked.

## 12. Checking against Optiland (optional)

The test suite can cross-check this program against Optiland, an independent open-source ray
tracer: Seidel sums surface by surface, and the fifth order recovered from Optiland's own rays.
Set it up once, from the repository folder in PowerShell:

    .\tools\setup-python.ps1

This downloads an embeddable Python into `python-embed\` and installs Optiland in it. Then:

    dotnet test AberrationCalculator.sln --filter "FullyQualifiedName~Optiland"

`ABCALC_PYTHON_HOME` points it at another Python that has Optiland installed. Without either,
the Optiland tests are reported as skipped. What was compared, and what it found - in both
programs - is in [optiland.md](optiland.md). The command-line program itself does not use
Optiland.

## 13. All the options

`abcalc --help` prints the whole list; `abcalc HELP` the commands and operands.

| Option | Section |
|---|---|
| `-o`/`--out`, `--no-files`, `-q`/`--quiet` | where the output goes (4) |
| `--glass <dir>` | a folder of `.agf` catalogs instead of the bundled ones (3, 13) |
| `--dir <dir>` | the folder bare names mean, for this run (8) |
| `--nat`, `--forbes`, `--distortion-coefficients`, `--quaternary-spherical`, `--asphere-placement`, `--screen` | the other reports (6) |
| `--optimize`, `--method`, `--iterations`, `--save`, `--saveas` | optimising (7) |
| `--optimize_basin_hopping`, `--hops`, `--chains`, `--seed`, `--hop-sigma`, `--initial-sigma`, `--hop-figuring`, `--glass_substitution` | basin hopping (7) |
| `-h`, `--help` | the list of options |

Exit codes: `0` read and analysed; `1` the file could not be read, no glass catalogs were found,
or an optimised design could not be saved; `2` read, but one or more glasses could not be
resolved.

## 14. When something goes wrong

| Message or symptom | What it means |
|---|---|
| `No glass catalogs found` | a published copy without its `catalogs` folder, or a program moved away from it. Republish (section 3), or set `ABCALC_GLASS_DIR` / pass `--glass` |
| `warning: unresolved materials: ...` (exit code 2) | the glass is in none of the loaded catalogs and is treated as air. Add its catalog with `--glass`, or change the glass. The results are wrong until it is fixed |
| `! This file names no glass catalog, and SK4 ... exists in more than one` | the name is ambiguous across vendors. Schott is used when it has it; point `--glass` at a folder holding only the catalog you mean to settle it |
| `Specify which project or solution file to use` | `dotnet build` or `dotnet test` without the solution named: add `AberrationCalculator.sln` |
| `no operands are declared, so there is nothing to optimise towards` | there is no `<lens>.mf`: add operands with `OP` (section 7) |
| `--optimize_basin_hopping needs a folder to save into` | add `--save <folder>` |
| `the design was NOT saved ... writing conics and aspheric terms back into a CODE V file is not implemented` | the optimised figuring cannot go into that format; the values are in the `.optimisation.txt` report (section 7) |
| a report or tool refuses a design, naming why | e.g. the ninth order on a figured design, or Forbes' trace on a mirror. The program refuses rather than return a number it cannot vouch for |
| the MCP server does not appear in Claude | restart Claude after registering; check the scope (section 9) |
| Optiland tests are skipped | run `tools\setup-python.ps1`, or set `ABCALC_PYTHON_HOME` (section 12) |

## 15. Running the tests

    dotnet test AberrationCalculator.sln -c Release

runs about 1,460 tests in three to four minutes. They check the coefficients against published
tables (Buchdahl's, Rimmer's, Sands's), against exact answers (the parabolic mirror, closed-form conics on axis),
against real ray traces, and against OpticStudio output recorded in the fixtures; they check that
every format reads and saves back correctly, and that the optimiser's analytic derivatives match
the values they differentiate. What is established, and how, is in
[verification.md](verification.md).
