# Aberration Calculator

Opens a lens design written by any of the common optical design programs, prints its
prescription, evaluates its aberration coefficients, and optimizes the design by minimizing a
spot size computed from those coefficients.

First it tells you what a lens is: the surfaces and materials as stated, the refractive index
of every material at every wavelength in the file, the first-order layout, and the aberration
coefficients that follow from them. Then, if you ask it to, it will change the lens: **you** say
what may move - curvatures, thicknesses, conic constants and the r⁴, r⁶ and r⁸ aspheric terms -
and between what limits, it moves them to minimize the merit function you wrote, and the result
goes back into the file it came from.

**To build it and use it, start with the [user guide](docs/user-guide.md)**: installing, a first
run, reading the report, optimising, the MCP server, the OpticStudio macros and the OSLO version.

**The merit function can be made of named aberrations rather than of rays.** Any of the
thirty-seven coefficients is an operand, written as the name the report prints it under:

    B,     1, TAR 0          # third-order spherical
    Pi5,   2, TAR 0          # fifth-order field curvature
    M2,    5, TAR 0          # sagittal oblique spherical
    M2,    5, TAR 0, 5       # ...and SURFACE 5's share of it alone
    M2.IND, 5, MAX 1e-3, 5   # ...or a bound on what is INDUCED in surface 5
    Tau15, 1, TAR 0          # the seventh order too

They cost almost nothing together, because all thirty-seven come out of one run of the scheme,
and they answer a question a predicted spot cannot: a spot mixes eighteen coefficients into one
number, and two designs whose `tau15` differs by a factor of five predict the same spot to one
part in ten thousand.

A surface number after the target takes **that surface's share** instead of the system's, and a
`.INT`, `.FIG` or `.IND` suffix takes one part of it - what the surface generates itself, what its
figuring adds, or what was induced in it by everything ahead. The parts add to the contribution
and the contributions add to the total, exactly. That is the question a table of totals cannot be
asked - not whether the design is wrong but *which surface, and is it that surface's own doing*.
The two readings call for opposite actions: an intrinsic aberration is corrected where it is
generated, an induced one is a reaction to something upstream.

**There are two optimizers**, and which you want depends on whether you are improving a design or
looking for a different one.

    abcalc lens.zmx --optimize                                  # the local optimizer
    abcalc lens.zmx --optimize_basin_hopping --save runs/       # the global search

The **local optimizer** finds the bottom of the valley the design starts in and stops. The step is
Dilworth's PSD III by default, with PSD II and Levenberg-Marquardt available through `--method`.
It is fast, it is deterministic, and it will never leave the form you gave it.

The **basin hopping** optimizer is the local one run many times over from kicked starting points,
with Metropolis acceptance deciding which valleys to keep walking from. Each hop is a Hooke-Jeeves
pattern search followed by the local optimizer, chains run in parallel - one per physical core by
default - and because chains land in different valleys it produces **one design per chain** rather
than a single answer. It is also the only one that can change the **glasses**: with
`--glass_substitution <catalogue>` a hop may swap a material for another from a named substitution
catalogue, which is a move no continuous optimizer can make.

**Every derivative the optimizer uses is analytic.** Not a difference quotient with a
well-chosen step - analytic, to machine precision, including through the predicted spot, which
is a quadratic form in thirty-seven Buchdahl coefficients reached through five thousand lines
of computing scheme. That is done by compiling the whole aberration chain **twice from one
source**: once in `double`, which is the analysis this program has always performed and which
its tests still guard bit-for-bit, and once with the arithmetic aliased to a forward-mode dual
number. Nothing is copied and nothing can drift, because they are the same files.

**The optimizer carries conics and even aspheres.** The coefficients come from Buchdahl's
closed-form scheme, which is the fastest route to a seventh-order coefficient there is; a figured
surface takes the arrangement of his Sec. 85, which he never published as a table and which is
reconstructed here. The optimizer refused every figured design until that reconstruction was
established - it agrees with Forbes' series trace on all twenty tertiary coefficients to between
2E-13 and 2E-10 on every figured refracting design, and the rays agree with both; on a figured
mirror, which Forbes declines, the reflected rays are the check ([docs/verification.md](docs/verification.md)) - and
now a conic and the r^4, r^6 and r^8 terms are variables like any other, `CC`, `A4`, `A6`, `A8`.
A spherical design takes Buchdahl's own published table exactly as before, bit for bit.

**The figured flat in collimated light is carried too**, which was the last design the optimizer
refused. There the incidence ratio is infinite and the finite coefficients arrive only after
terms in different powers of it cancel; the analysis side reaches them through a Laurent series
in that surface's curvature, and the optimizer now does the same in `DualSeries` - a dual number
whose value and derivative are each such a series - so a Schmidt corrector plate optimises like
anything else. What is still refused is not a class of design but a failure to converge: the
series route vouches for itself or it is not used. See
[docs/optimizer.md](docs/optimizer.md).

PSD recovers the curvature Gauss-Newton discards, from two successive **exact** Jacobians - a
secant that is only worth taking when both ends are real measurements, which is why nothing here
is ever Broyden-updated. Bounded variables are held by **reflection — not clamping, and not a
sigmoid**: a step past a limit is folded back inside, and the gradient keeps magnitude one. A
clamp would pin the variable and discard the rest of the step, leaving the search stalled against
a boundary with a gradient it cannot act on; a sigmoid's derivative vanishes *at* the bound, so a
variable pressed against a limit goes numb and nothing brings it back when the design later wants
it.

The report says what MOVED - surface by surface, in radii, thicknesses, glass names, conics and
aspheric terms rather than in the optimizer's own variables - together with the merit it started
at and the merit it reached, and which operand is holding whatever error is left.

### Reading and writing the settings

Settings are split by what they describe. The **merit function** says what the design should be
and can be carried from one design to another; the **variables** say what may change about it and
mean nothing away from the design they name.

| | `.lhlt` | every other format |
|---|---|---|
| variables, bounds, pickups | in the lens file | `<lens>.var` |
| merit function | `<lens>.mf` | `<lens>.mf` |

A `.lhlt` states which surfaces have variable curvatures, thicknesses, conics and aspheric
terms, the bounds on them and
its pickups; all of that is read, honoured, and written back when the design is saved. Its own
**merit function is not read** - this tool optimizes a different one - and it is left untouched in
the file.

```
PRMSA,   1, TAR 0                        # the predicted spot
Pi5,     2, TAR 0                        # ONE NAMED COEFFICIENT - see the table in docs/optimizer.md
M2,      5, TAR 0,           5           # ...and surface 5's share of one
Tau15,   1, MIN -1e-3, MAX 1e-3          # and a seventh-order one, held in a band
EFL,   100, TAR 50,          2           # focal length, in wavelength 2
EGT,    10, MIN 1,           2, 4        # glass edges over surfaces 2 to 4
DTRGT,  10, MIN 1.5, MAX 12, 2, 4        # diameter-to-thickness ratio
DISTF,  10, MIN -2, MAX 2,   0.7         # distortion at seven tenths of the field
RY,      1, TAR 0,           7, 1, 1, 0, 1
```

The second field is the weight. The numbers in that column here are only there to show that the
column exists - they are not a recommendation, for the reason at the end of this section.

```
VAR CV 1                                 # curvature of surface 1
VAR TH 2 MIN 1.0 MAX 25.0                # a thickness, bounded
VAR CC 3                                 # conic constant - this FIGURES the surface
VAR A4 3                                 # and the aspheric terms: A4, A6, A8
PICKUP TH 2 INDEX 1 SCALE 1 OFFSET -0.1
```

`CV`, `TH`, `CC`, `A4`, `A6` and `A8`. The last four figure a surface, and a surface that becomes
figured sends the seventh order down the aspheric arrangement of Buchdahl's Sec. 85 rather than
his published table - which is why they were not offered until that arrangement was established.
Limits are optional and **merge**, so
setting a maximum does not discard a minimum set a moment earlier; `FREE` takes them off again.
An operand with `TAR` is driven to it and weighed against everything else, while one with `MIN` or
`MAX` costs *exactly zero* - in the merit and in the Jacobian - until it is threatened, which is
what lets a lens carry a dozen manufacturability limits without any of them bending the answer.

### Three things the predicted spot cannot see

`PRMSA` is the obvious thing to ask for and it is not enough on its own. Robb's spot is the
variance of the ray intersection **at the Gaussian image plane**, **about its own centroid**, from
coefficients computed **per wavelength from that wavelength's own paraxial trace**. Each of those
costs the merit function something that might be assumed to be included:

| invisible to `PRMSA` | because | ask for it with |
|---|---|---|
| where the image surface is | the spot is referred to paraxial focus, wherever the file put the image | `PY` at the image surface |
| a focus shift between colours | each wavelength is measured at *its own* focus | `AXC` |
| an image-height shift between colours | a shift of the whole patch does not change its size | `LCF` |

```
PY,    1, TAR 0, 10, 2, 0, 0, 1          # marginal ray height at the image surface
AXC,   1, MIN -0.15, MAX 0.15            # axial colour
LCF,   1, MIN -0.01, MAX 0.01            # lateral colour, at the full field
```

**Focusing the image plane with `PY` is the effective way to do it.** Driving the paraxial
marginal ray to zero height at the image surface puts that surface at paraxial focus - which is
where the spot was being measured all along, so the operand does not pull the design anywhere. It
makes the last thickness *mean* something. Without it a variable back focus has exactly zero
gradient from `PRMSA`: on the double Gauss in `tests/fixtures/lenses`, moving the image surface
twenty millimetres leaves `PRMSA` at 0.042589, the same to every digit printed.

**Both colours, or neither.** Axial colour on that design starts at -0.0990 mm of focus shift -
at f/8 a blur radius near 0.0062 mm - while the on-axis predicted spot in blue reads 0.0011 mm.
Lateral colour is the one most often left out, and a run given `AXC` alone can come back
achromatic on axis and smeared in colour at the edge of the field with nothing in the merit
function having mentioned it.

**No advice is offered here on weights.** Where a requirement is a boundary, say it as `MIN`/`MAX`
and no weight has to be chosen at all. Where it must be a target, the right weight depends on the
design and the few examples in this repository are not enough to generalise from - and since
changing a weight changes the scale of the merit, two differently weighted runs cannot be compared
by their merit numbers. Judge them on the physical quantities the report prints.

Both files are also built **a command at a time**, and the command *is* the file line:

    abcalc lens.zmx VAR "TH 2 MIN 1.0 MAX 25.0"
    abcalc lens.zmx OP  "EFL, 100, TAR 50, 2"
    abcalc lens.zmx OPLIST            # numbered; OPREMOVE 3 takes number 3 out
    abcalc lens.zmx VARLIST           # likewise VARREMOVE, PICKUPLIST, PICKUPREMOVE
    abcalc HELP                       # every command and operand; HELP EFL for one

Each command reads the settings, changes them and writes them back, so a command line that exits
between every command still behaves like a program that remembers. A transcript of commands is a
valid settings file and a settings file is a script of commands - one grammar, not two dialects
for the same ideas. `VAR` lines merge, so setting a maximum does not discard a minimum set a
moment earlier; `VAR "TH 2 FREE"` is how the bounds come off deliberately.

Commands may be followed by a run in the same invocation:

    abcalc lens.zmx VAR "CV 1" VAR "CV 2" --optimize --save
    abcalc lens.zmx --optimize --saveas better.zmx
    abcalc lens.zmx --optimize --saveas better.len     # the same, written as OSLO
    abcalc lens.zmx --optimize_basin_hopping --glass_substitution CoreSet28 --save runs/

Nothing is overwritten unless overwriting is asked for by name: with neither `--save` nor
`--saveas` the result is written beside the original as `<name>.optimised.<ext>`. Saved in the
format it was read, the file is edited and everything else in it kept; `--saveas` with another
extension writes a whole lens in that format, carrying what this program models - see
[docs/optimizer.md](docs/optimizer.md#saving-back). Basin hopping
lands its chains in different valleys and so produces one design **per chain** - it is refused
without a folder rather than keeping the lowest merit and discarding the rest.

Glasses for substitution come from a **named** catalogue in `catalogs/Substitution`, kept apart
from the catalogues a design is read through: a search free to pick from every vendor at once
settles on glasses nobody stocks. `CoreSet28` ships with it.

One command is not about a lens at all, and is the only one that can be given on its own:

    abcalc BASE "C:\lenses\project7"     bare names now mean this folder
    abcalc BASELIST                      show it, and where it came from
    abcalc BASEREMOVE                    forget it

It is kept until it is changed, so it holds in the next shell too. In a terminal that is a
convenience - `cd` already does most of it - but over MCP it is the difference between working
and not: an MCP server's working directory is whatever the client started it in, not anything the
user chose, so without a base every path has to be absolute. Four things can set it, most
specific first: `--dir` on the command line, `ABCALC_DIR` in the environment, the folder set with
`BASE`, and failing those the working directory - and `BASELIST` says which of the four is
answering. An absolute path is never re-rooted.

### What goes back into the file

An optimized design goes back **in the format it came from**, by editing that file rather than
regenerating it: only curvatures, thicknesses, glass names, conics and aspheric terms change,
in the file's own units, and everything this program does not model - solves, coatings, tolerances, somebody else's merit
function - survives untouched. That matters more than it sounds. The .zmx reader here recognises
twenty-three directives and a real `.zmx` has many times that, so a writer that rebuilt the file
from what it understood would quietly delete the rest of a design.

| format | reads | writes back |
|---|---|---|
| LensHH-LT `.lhlt` | yes | **yes**, including figuring, variables, bounds and pickups |
| ZEMAX `.zmx` | yes | **yes**, including figuring |
| Optiland `.json` | yes | **yes**, including figuring |
| CODE V `.seq` | yes | **yes**, but not figuring: a design whose conic or aspheric terms moved is refused |
| OPTALIX `.otx` `.opt` | yes | **yes**, but not figuring, as CODE V |
| OSLO `.len` `.osl` | yes | **yes**, but not figuring, as CODE V |

Every format goes back the way it came, and each one asks for something different. A `.seq`
surface line is positional - `S radius thickness material` - so editing the radius means taking
the line apart and putting the other two back as the file spelled them. A `.len` writes nothing
for a property that has nothing to say, so bending a plane means *adding* the `RD` line it never
had, in the right block. An `.otx` gives the shape as a curvature rather than a radius, and marks
its image surface with a `-999` that is a flag and not a distance. A `.zmx` is UTF-16 where the
rest are plain text, and none of the plain-text three may come back with a byte order mark they
did not arrive with.

A format still outside the list says so plainly rather than producing a file that looks like the
design and is not; the optimized prescription is in the report, and the settings in the sidecar.

[docs/optimizer.md](docs/optimizer.md) has the merit-function format, every operand, and - at the end - what this
does not do.

## How far the coefficients get you

Predicting a spot from coefficients costs a small fraction of tracing rays for one, which is
what makes it attractive early in a design, before the shape is settled enough to be worth a
full evaluation. The question is where the prediction stops being worth quoting.
[docs/spot-prediction.md](docs/spot-prediction.md) measures that on five lenses, at both conjugates, against rays this
program traces itself.

Cooke triplet, f/5, 20° half-field, as error in the predicted RMS spot radius:

| field | 3rd | 3rd + 5th | + 7th-order spherical | full 7th |
|---|---|---|---|---|
| 0.7 | +79.1% | +6.6% | +5.2% | +1.6% |
| 0.8 | +97.7% | +9.2% | +8.2% | +1.4% |
| 0.9 | +127.0% | +14.5% | +13.9% | **+0.4%** |

The `+ 7th-order spherical` column adds that one coefficient on its own - the one tertiary
quantity already available elsewhere, and it barely moves the off-axis error. What closes the
gap is the other nineteen coefficients.

**The order a design needs is a property of that design, not a general rule.** Of the five
lenses measured, one is described by third order alone, two need the full seventh to reach a
per cent, one is not well described at seventh, and one - a hard-corrected asphere - is not
described at all. That last case is examined rather than glossed: the series is still
converging there, and what runs out is the extraction at seventh order rather than the method.

**Distortion asks the same question of the coefficients, and reaches something a spot cannot.**

    abcalc <lensfile> --distortion-coefficients

**This is not a way to obtain a distortion figure.** Tracing one chief ray gives that exactly,
at the same speed, and does not degrade at the corner where the seventh order is out by a
third. What the coefficients give that a trace cannot is WHICH ORDER the distortion is —
third order is stop position and symmetry, the higher orders are not, and they answer to
different changes — and which SURFACE it comes from.

It reports both mappings, F-tan(theta) and F-theta with the exact relation between them, and
reconciles the paraxial image plane the coefficients live at with the image surface the file
defines, where a design program quotes. On a figured design the seventh-order term is taken
from Forbes' series trace - except on a mirror, which Forbes declines, where the scheme's is used -
and the report says which route it used - a choice made while the
scheme's aspheric arrangement was still a reconstruction the rays rejected, and kept now that
it is not, because the two agree there and a route the report names costs nothing.

At zero pupil radius the polynomial keeps three terms — `E h^3 + E5 h^5 + tau20 h^7` — and
they are separated by their power of the field alone, so each is measured against traced rays
**on its own** rather than inside a sum where errors cancel. It needs no fit and no model of
the other seventeen coefficients, and it is a check B7 cannot pass, having no field in it.
[docs/distortion-prediction.md](docs/distortion-prediction.md) has the measurement. **It is what convicted the aspheric
arrangement while that arrangement was wrong** - `tau20` out by up to a factor of four on a
figured design, with the rays landing on Forbes every time the two disagreed - and it is worth
recording that this was found by measurement rather than by inspection of the algebra. The
arrangement has since been completed and now agrees with Forbes on all twenty tau to between
2E-13 and 2E-10 on every figured refracting design, a figured flat in collimated light included,
and the rays agree with both; on a figured mirror, which Forbes declines, the reflected rays are
the check ([docs/verification.md](docs/verification.md)). On spherical designs the two routes always
agreed, and the rays back both.

## When the surfaces are not on a common axis

Everything above assumes a lens with an axis. Tilt a surface or decentre it and the aberration
coefficients stop being the whole story - not because new aberrations appear, but because the
old ones move.

A perturbed surface contributes the same rotationally symmetric field it always did,
**displaced**: shifted to a different centre in the field of view. The system's aberration is the
sum of those displaced fields, and the sum's zeros - the **nodes** - leave the middle of the
field and separate from one another. Astigmatism acquires two nodes, elliptical coma three,
fifth-order astigmatism four. That is nodal aberration theory, and this program computes it to
**fifth order**.

    abcalc lens.zmx --nat

The perturbation goes in an `.align` sidecar beside the lens, and it is the same file whichever
of the six formats the lens is in - `.zmx` has coordinate breaks, `.lhlt` does not, and neither
matters here:

    TILT 2 Y 0.115        degrees
    DEC  3 X 0.05         lens units
    ZERN 1 Z10 0.0005     a Fringe Zernike overlay, as surface sag

**Why the nodes are worth having: they are a signature.** Where they sit says what moved. Binodal
astigmatism whose midpoint stays at the field centre is figure error at the stop; a displaced
midpoint is misalignment. A spot diagram shows the blur either way and cannot tell you which. The
same holds for a freeform surface - a Zernike trefoil plate at the stop produces trefoil, and the
*same plate moved off the stop* produces astigmatism growing linearly with field, which is the
aberration a three-point mount error makes.

The report gives both orders in the design's own units, each surface's displacement, the nodes of
every aberration type, and what the fifth order does to the third - on a triplet with one surface
tilted, third-order coma is 17 per cent larger than the third-order coefficients alone say, and
its node has moved. A field grid goes to `<name>.nat.tsv` beside the lens.

**It is checked against published tables rather than against another program.** Thompson's
Tables 3 to 5, Buchdahl's VI Table II and VII Table I; the internal identities of VI (4.17); and
physical invariants - a uniformly displaced system splits no node, an aligned one collapses to
ordinary Seidel. Where a result could not be had honestly it is not printed: fifth-order
distortion's field is exact and its nodes are refused, because the closed solution is in a 1980
dissertation this repository does not hold and a numerical search disagreed with a direct scan of
the field.

**And one piece of it reaches the optimizer.** The `ASBLT` merit operand is the wavefront error a
decentre and tilt tolerance would induce, from the same theory and from the two paraxial rays the
program already has - no rays traced, and analytic on the dual-number compile like every other
derivative here. It exists because a design can always be driven to a smaller predicted spot by
making it more sensitive to the tolerances it will be built to, and nothing else in the merit
function objects: `PRMSA` is measured on a perfectly centred lens and does not know the lens will
be assembled by somebody. [docs/optimizer.md](docs/optimizer.md) has it.

[docs/nodal-aberration-theory.md](docs/nodal-aberration-theory.md) is the documentation - how to read every line of the report and
every column of the TSV, what is verified and against what, and what it does not do.
[docs/nat-development.md](docs/nat-development.md) is the working log, kept because several of its conclusions are only
defensible with the reasoning attached.

## Two things for OpticStudio users

Beside the calculator itself, the repository holds two programs that report third-, fifth-
and seventh-order aberration coefficients **per surface**, split into what each surface
generates on its own and what it generates by acting on the aberration already present when
light reaches it. A table of totals cannot say which surface to change; that split can.

| | `macros/BUCH7.ZPL` | `macros/BUCH7_ASPH.ZPL` | `zosapi/` FORBES7 |
|---|---|---|---|
| method | Buchdahl's computing scheme | the same, with the aspheric arrangement of his Sec. 85 | Forbes' series trace, JOSA 73, 782 (1983) |
| form | one ZPL macro | one ZPL macro | C# driving OpticStudio through the ZOS-API |
| to install | nothing | nothing | build one solution, run FixBinaries once |
| spherical surfaces | yes | yes, reproducing BUCH7 entry for entry | yes |
| conics and even aspheres | declined by name | all three orders | all three orders, with the figuring separated |
| object at infinity or finite | either | either | either |
| mirrors | on spheres (needs two surfaces before the image; a dummy plane will do) | yes | declined by name |

They share no code and agree: seventh-order spherical aberration is reached through
Buchdahl's fifth-order working in one and through a power series in the other, and both
give 1.681450E-03 on a Cooke triplet at infinite conjugate, 2.305287E-03 with the object
at 250 mm. FORBES7 prints that comparison itself, every run.

**On a figured lens the agreement now runs to all twenty seventh-order coefficients.**
`BUCH7_ASPH.ZPL` and `FORBES.ZPL` reproduce each other to every digit either prints on a
conic singlet, on a conic carrying r⁴, r⁶ and r⁸ together, and on a triplet with two
figured surfaces where one induces on the other — Buchdahl's arranged tables against an
order-doubling series trace, sharing no arithmetic past the paraxial ray. That is worth
rather more than either agreeing with itself, and until the aspheric arrangement was
reconstructed there was nothing to check it against at all.

The seventh order is also reachable without OpticStudio at all. It needs no ray tracer and
no other program - only the lens file:

    abcalc <lensfile> --forbes

and the MCP server offers the same as `seventh_order`, so an assistant can ask for it
directly. All three print the identical report from one formatter, and all three decline a design
with a mirror: the series trace does not trace a reflection.

What either of them buys over the orders already available is the section above, and
[docs/spot-prediction.md](docs/spot-prediction.md) in full.

See `macros/README.md` and `zosapi/README.md`. Note the repository holds **two solution
files**: `AberrationCalculator.sln` is .NET 8, and `ForbesAberrationCalculatorZOSAPI.sln`
is .NET Framework, which is what the ZOS-API requires — so `dotnet build` and
`dotnet test` need the solution named.

## For OSLO users

`ccl/buch7_asph.ccl` is `BUCH7_ASPH.ZPL` for OSLO: the same third-, fifth- and seventh-order
coefficients, per surface, computed inside OSLO on OSLO's own lens data. Each surface's third
order, fifth order and B7 are split into intrinsic, figuring and induced, and the twenty
seventh-order tau are given surface by surface. It takes spheres, conics and OSLO's standard
asphere (AD, AE, AF), mirrors, either conjugate, and up to 16 surfaces; OSLO EDU is enough.

Copy it into OSLO's `private\ccl` folder, compile it (**Tools > Compile CCL**) and type
`buch7_asph` with a lens open. Everything past the lens data is translated from the ZPL macro
statement for statement by `tools/zpl2ccl`, so the arithmetic is the macro's own. On a figured
triplet, a parabolic mirror and a double Gauss of spheres it agrees with this program to the last
printed digit, per surface and in total.

For a lens of spheres, `ccl/buch7.ccl` is `BUCH7.ZPL` for OSLO. It gives the same coefficients
and adds the seventh order split into intrinsic and induced, the ninth-order spherical aberration
and Buchdahl's Table I entry by entry; `buch7 1` runs it with the ninth order. See
[ccl/README.md](ccl/README.md) and section 11 of the [user guide](docs/user-guide.md).

## Formats it reads and writes

| Program | Extension |
|---|---|
| ZEMAX / OpticStudio | `.zmx` |
| CODE V | `.seq` |
| OPTALIX | `.otx`, `.opt` |
| OSLO | `.len`, `.osl` |
| Optiland | `.json` |
| LensHH-LT | `.lhlt` |

Every one of them is also written back, by editing the file rather than regenerating it - see
**What goes back into the file** above for what that means and why it is done that way.

Glasses are resolved from AGF catalogs. A material that is not a catalog name is read as a
six-digit glass code (`517642`, or `564.610` in the decimal form some files use), which fixes nd and Vd and
gives a model dispersion; anything still unresolved is reported rather than quietly treated
as air.

## What it computes

- **Prescription** - radius, thickness, material, semi-diameter, conic and aspheric terms,
  as the file states them.
- **Refractive indices** - per material, per wavelength.
- **First order** - effective and back focal length, entrance and exit pupils, F/number,
  paraxial image height and magnification.
- **Aberration coefficients** - third order (Seidel), fifth and seventh order
  (Buchdahl/Rimmer).
- **Ninth-order spherical aberration** - Buchdahl's quaternary coefficient, per surface,
  `abcalc <lensfile> --quaternary-spherical`. SPHERICAL SURFACES ONLY, and a figured design is
  refused with its reason: Buchdahl published no aspheric arrangement at this order. Everything
  else here stops at the seventh, so an on-axis residual has had to be ATTRIBUTED to the ninth
  rather than measured; this makes it arithmetic.
- **PRMS** - the RMS spot size predicted from those coefficients, per field and per
  wavelength, and **PRMSA**, the weighted composite over all of them.
- **Best focus** - where that spot is smallest, per wavelength: the shift from each colour's own
  paraxial plane, its own back focal length beside it, and the one plane that serves the whole
  field. Robb's polynomial has no defocus term and says so; this is the plane it would choose,
  by the minimum-radius-of-gyration criterion of Sands (1973).
- **Nodal aberration theory** - third and fifth order, for a design whose surfaces are not on
  a common axis: where each surface's aberration field has been displaced to, where the nodes
  of the system's field are, and what the fifth order does to the third.

## Status

Working, and validated in [docs/verification.md](docs/verification.md):

- `abcalc`, the command-line tool, and the six file readers
- bundled glass catalogs and index resolution - nothing to point at, nothing to install
- the paraxial trace, which agrees with an independent implementation to every
  digit it prints
- Seidel third-order coefficients
- Buchdahl/Rimmer fifth- and seventh-order coefficients, split into intrinsic,
  aspheric and induced parts, per surface and totalled
- PRMS and PRMSA, the RMS spot radius predicted from those coefficients with no
  rays traced
- the best-focus plane per wavelength, derived from the same polynomial rather than
  transcribed from Sands, and checked three ways: against his one published number, against
  an exactly-integrated pupil, and against real traced rays
- the re-normalised per-aberration and per-surface contributions to that spot
- the optimizer: PSD, Hooke-Jeeves and basin hopping over analytic derivatives, with the
  Jacobian checked operand by operand and variable by variable against central differences.
  Conics and even aspheres carried, as values and as variables; any named aberration
  coefficient as an operand - for the system, for one surface, or for the intrinsic,
  figuring or induced part of a surface's share ([docs/optimizer.md](docs/optimizer.md))
- **nodal aberration theory**, third and fifth order: what the aberrations do when the
  surfaces are not on a common axis, and where the nodes go. Driven by an `.align` sidecar
  that works the same for all six formats, and checked against Thompson's and Buchdahl's
  own published tables rather than against another program ([docs/nodal-aberration-theory.md](docs/nodal-aberration-theory.md))
- **mirrors**, since 22 September 2026: the real ray trace reflects, the seventh order is
  finite and in the same frame as the third and fifth, and Forbes' series trace declines a
  mirror rather than trace it as a refraction, and NAT's wave front and real-ray sigma are right
  on a mirror. These were defects on the reflecting path, and they were missed because the data
  was not read critically enough: the parabola's ray fit failed and was dismissed as noise, and
  its seventh order was NaN and passed silently. OpticStudio was never wrong - FIFTHORD reaches
  only the third and fifth order, which were always right - and Optiland's rays merely
  reinforced that reflection was amiss. They are settled against the parabola's exact answer and
  Thompson's telescope, and fixed, the macros included; one reflection and two are measured in this program, the macros
  on one reflection only ([docs/verification.md](docs/verification.md), *Mirrors*)

There is no GUI. The tool writes plain text and TSV files that you can read, diff and
feed to something else.

## Layout

```
src/AberrationCalculator.Core   models, glass, ray trace, coefficients
src/AberrationCalculator.Core.Ad  the same source, compiled against a dual number
src/AberrationCalculator.Core.Series    the same source, in Laurent series arithmetic
src/AberrationCalculator.Core.Series.Ad the same source, in both at once
src/AberrationCalculator.Optimize variables, operands, PSD, Hooke-Jeeves, basin hopping
src/AberrationCalculator.IO     one reader per format, and the .lhlt writer
src/AberrationCalculator.Cli    abcalc - the command-line tool
src/AberrationCalculator.Optiland  the same lens inside Optiland, through embedded Python
catalogs/Glass                  bundled AGF glass catalogs
docs/                           what is established and how - see the table below
macros/                         ZPL macros that run inside OpticStudio, and their README
ccl/                            buch7_asph.ccl and buch7.ccl, the Buchdahl macros for OSLO, their README and reference output
tests/                          unit tests
tools/smoke                     command-line harness used during development
tools/setup-python.ps1          installs the embedded Python and optiland, for the cross-check
tools/zpl2ccl                   translates the Buchdahl macros into CCL, and checks a run of each
```

### The documents

Each answers one question, and they are meant to be read on their own rather than in order.

| | what it answers |
|---|---|
| [docs/user-guide.md](docs/user-guide.md) | **How to build it, install it and use it**: a first run, reading the report, the other reports, optimising and basin hopping, the MCP server, the OpticStudio macros, the OSLO version, and what to do when something goes wrong. |
| [docs/verification.md](docs/verification.md) | **What is actually established here, by what evidence, and what is not.** The order of evidence, the standing results, and the aspheric arrangement with everything it rests on. Read this one first if you are deciding whether to trust any number this program prints. |
| [docs/references.md](docs/references.md) | Every source the method comes from, which of them have been read, and where each piece of the implementation came from. |
| [docs/forbes.md](docs/forbes.md) | The second, independent route to the tertiary coefficients - a Lagrangian series trace - and why a program that already had one needed another. |
| [docs/forbes-ninth-order.md](docs/forbes-ninth-order.md) | A scope note: what the same series trace would take to reach the NINTH order on a figured design, where Buchdahl published one coefficient of thirty and no aspheric arrangement at all. |
| [docs/optimizer.md](docs/optimizer.md) | The optimiser: analytic derivatives throughout, the merit-function format, how figuring is carried, and which coefficient is which aberration. |
| [docs/spot-prediction.md](docs/spot-prediction.md) | How well a spot predicted from coefficients matches a traced one, measured rather than asserted, and where seventh order runs out. |
| [docs/distortion-prediction.md](docs/distortion-prediction.md) | Distortion from the coefficients against traced chief rays. The cleanest window onto a single coefficient there is, and what it found. |
| [docs/nodal-aberration-theory.md](docs/nodal-aberration-theory.md) | What the aberrations do when the surfaces are not on a common axis, and where the nodes go. |
| [docs/nat-development.md](docs/nat-development.md) | How that was built and what each stage was checked against. |
| [docs/optiland.md](docs/optiland.md) | The Optiland cross-check: Seidel sums surface by surface, the fifth order from Optiland's own rays, three defects and an unstated sign convention it found in Optiland 0.6.2, and the defects here that came to light alongside it, all fixed. |
| [docs/mcp.md](docs/mcp.md) | The MCP server: what each tool exposes and what it returns. |
| [macros/README.md](macros/README.md) | The ZPL macros - what each computes, what it refuses, and the ZPL traps they had to respect. |
| [ccl/README.md](ccl/README.md) | `buch7_asph.ccl` and `buch7.ccl` in OSLO: installing and running them, their limits, how they are built from the ZPL macros, and how they were checked. |

The method is not original work: it is Buchdahl's aberration coefficients in Rimmer's
notation, Robb's analytic integration of them into a spot size, and Rosete-Aguilar and
Rayces's re-normalisation of them into comparable quantities.
[docs/references.md](docs/references.md) gives the full chain, says which papers have
actually been read, and records where the implementation came from.

## Using it

```
abcalc mylens.zmx
```

Prints a report and writes it alongside the lens file, together with tab-separated data
files a script or spreadsheet can read: prescription, refractive indices, first-order
quantities, and the paraxial marginal and chief rays at every surface. `abcalc --help`
lists the options.

Glass catalogs ship in `catalogs/Glass` and are found automatically - there is nothing to
configure. `--glass <dir>` overrides them if you have your own.

## Building

```
dotnet build AberrationCalculator.sln
dotnet test  tests/AberrationCalculator.Tests/AberrationCalculator.Tests.csproj
```

.NET 8, so Windows, Linux and macOS.

### Against Optiland

This cross-check is optional; nothing else needs Python. Run the setup script once from the
repository folder in PowerShell. It needs an internet connection: it downloads Python's embeddable
package from python.org into `python-embed\` and installs Optiland into it with pip. Nothing is
installed system-wide, and a fresh clone does not include it, because `python-embed\` is not kept
in git.

    .\tools\setup-python.ps1        # once: Python + optiland, into python-embed\

puts [Optiland](https://github.com/HarrisonKramer/optiland) in reach of the test suite. Each design
is built inside Optiland from the prescription this program parsed, and two things are compared:
Optiland's Seidel sums, which agree **surface by surface to 1E-14** on every design they can be
asked about once Optiland's opposite sign is turned; and the **fifth order**, which Optiland does
not compute - its rays are inverted for all twelve coefficients instead, and reproduce Buchdahl's
to between 3E-9 and 1.3E-5 on 45 designs, aspheric ones and a mirror included. The comparison
found three defects in Optiland 0.6.2 (it ignores r² in its paraxial trace and r⁴ upward in its
Seidel sums, and its chief ray at a finite conjugate misses the object distance) and a sign
convention it does not state. Alongside it, defects in this program came to light - the Seidel
distortion of a flat face in collimated light lost, the real ray trace not reflecting, and with it
the rest of the reflecting path: the seventh order NaN on a mirror, Forbes' series trace tracing
one as a refraction, NAT's wave front zero - all fixed (docs/verification.md, *Mirrors*). They were
not Optiland's discovery: the symptoms were already in this repository and had been explained
away, and OpticStudio, compared on a mirror only at the third and fifth order, was never wrong. See [docs/optiland.md](docs/optiland.md). Without the embedded
Python those tests are reported as skipped.

## Licence

MIT - see `LICENSE`. Copyright Javier Ruiz; authors in `AUTHORS`.

The file readers began as the MIT-licensed readers from
[LensHH-LT](https://github.com/SynapseOptics/LensHH-LT); everything else here is written
for this program.
