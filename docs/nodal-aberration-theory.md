# Nodal aberration theory

What happens to a lens's aberrations when its surfaces are not on a common axis.

A tilted or decentred surface does not acquire a new kind of aberration. It contributes the same
rotationally symmetric field it always did, **displaced** — moved to a different centre in the
field of view. The system's aberration is the sum of those displaced fields, and the sum's zeros,
the **nodes**, leave the centre of the field and generally separate from one another. Astigmatism
acquires two nodes, elliptical coma three, fifth-order astigmatism four.

That is the whole content of the theory, and it is why it is useful: the nodes are a *signature*.
Where they sit says what moved. A binodal astigmatism whose midpoint stays at the field centre is
figure error at the stop; a displaced midpoint is misalignment. Nothing in a spot diagram
distinguishes those.

[docs/nat-development.md](nat-development.md) is the working log — what was read, what went wrong, how it was caught.
This file is what the program does and how to drive it.

## What this program computes

| | |
|---|---|
| **third order** | `W040 W131 W222 W220P W220S W220M W220T W311`, each surface's sigma, the coma node, the astigmatic node pair, the medial vertex |
| **fifth order** | `W060 W151 W240 W242 W331 W333 W420 W422 W511`, and nodes for all but `W511` |
| **freeform** | Zernike astigmatism, coma, trefoil, oblique spherical and fifth-order coma overlays on any surface |
| **coupling** | what the fifth order does to the third — it changes both the magnitude and the node |

Both orders are reported in the design's own aperture and field, so they may be compared directly.

The third order comes from the Seidel sums. The fifth comes by a longer road, because Thompson's
theory is written in wave coefficients and this program's fifth order is transverse: it runs
Buchdahl's computing scheme in his **W coordinates** (paper VI Table I), converts the aberration
coefficients to deformation coefficients (VII Eqs. 6.5-6), and converts those to the
**retardation** of the wave front (VII Eq. 3.4), which is what a wave aberration is. Deformation
and retardation differ by up to 21 per cent at fifth order; using the wrong one is a plausible
wrong number rather than an obvious one.

## Driving it

A perturbation goes in an `.align` sidecar beside the lens file, and it is the same file for all
six formats this program reads — `.zmx` has coordinate breaks, `.lhlt` does not, and neither
matters here.

```
    TILT 2 Y 0.115        degrees, about the surface vertex
    DEC  3 X 0.05         lens units
    ZERN 1 Z10 0.0005     Fringe Zernike, as a surface SAG
    TILT 2 FREE           removes only the tilt, leaving any decentre
```

Lines merge: `TILT 2 X 0.1` followed by `TILT 2 Y 0.05` leaves surface 2 tilted about both axes.
Angles are degrees throughout the program, with no exceptions. The next section is the full
grammar.

Then:

```
    abcalc lens.zmx --nat
```

which writes the report to the console and `lens.nat.tsv` beside the lens. `--nodal` is the same
switch.

**On an aligned design it still runs, and says so.** Every sigma is zero, the sums collapse to
the ordinary Seidel ones, and every node sits at the field centre. That is the theory reducing
correctly rather than declining the case, and it is worth seeing once.

**Over MCP** it is the `nodal_aberrations` tool, which takes the perturbation either from the
sidecar or as `alignment` text in the grammar below — through the same parser, so the two cannot
disagree — and returns the field grid instead of the report when asked for `full_field`. Every
other tool on that server reads `<lens>.align` too, so a perturbed lens reads the same way there
as it does here. [docs/mcp.md](mcp.md) has the arguments.

## The `.align` file

### Why it is a file of its own

A perturbation is a statement about **one built instance** — "this surface ended up fifty microns
off" — not about the lens. Writing it into the prescription would corrupt the design record with a
build error, and for a `.lhlt`, which keeps its variables inside the lens file, that is exactly
where the variables rule would have put it. So alignment gets its own sidecar, uniformly for all
six formats, and **deleting that file restores the nominal design exactly**.

It is not a `.var` either. Variables say what the OPTIMISER may move; these say what the WORKSHOP
got wrong. Neither is a merit function, which is the only one of the three that can be carried
from one design to another at all.

### The grammar

    TILT <surface> [X <deg>] [Y <deg>] [FREE]
    DEC  <surface> [X <len>] [Y <len>] [FREE]         DECENTER and DECENTRE also accepted
    ZERN <surface> [Z<n> <sag>]... [FREE]             ZERNIKE also accepted

Blank lines are ignored and `#` starts a comment. Surfaces are numbered from 1. A keyword with nothing after
the surface number is a no-op rather than an error, so `TILT 2` does not wipe surface 2.

**Tilts are in DEGREES**, which is how every lens format states one, and degrees are the only
angular unit this program takes from a user anywhere — the `ASBLT` merit operand states its tilt
tolerance in them too, so a number copied from one to the other means what it said. Radians appear
once, in the conversion on read.

**Decentres are in the design's length units.** Zernike coefficients are a surface **sag**, in the
same units, and are **raw Fringe** terms.

The file accepts `Z1` to `Z18` and refuses anything outside that by name rather than ignoring it.
The theory consumes **five pairs**:

    Z5/6    astigmatism          Z7/8    coma           Z10/11  trefoil
    Z12/13  oblique spherical    Z14/15  fifth-order aperture coma

**Everything else parses and does nothing** - `Z9` spherical aberration among them, since a
rotationally symmetric departure displaces no field centre. A term stated outside those five
pairs is accepted in silence and has no effect, which is worth knowing before wondering why the
nodes did not move.

### Lines merge, and `FREE` is the way back out

Exactly as `VAR` does:

    TILT 2 X 0.1
    TILT 2 Y 0.05          # surface 2 is now tilted about BOTH axes

Because a value cannot be removed by leaving it off, there has to be a word that says to drop it.
`FREE` drops **only its own kind**: `TILT 2 FREE` clears the tilt and leaves any decentre on
surface 2 alone, and `ZERN 2 FREE` clears every Zernike term without touching either.

### Where the file lives

Beside the lens, named for it **including** the extension: `triplet.zmx` reads
`triplet.zmx.align`. That is the same rule `.mf` and `.var` follow, and for the same reason — a
folder holding `triplet.zmx` and `triplet.seq` keeps their records apart rather than sharing one
account of how the workshop built two different designs.

### What it is for, and what it is not

The paraxial trace is a rotationally symmetric construction and does not read these at all. Nodal
aberration theory treats a perturbation as a **small departure** from the symmetric system, which
is right for alignment errors and wrong for a design with large deliberate tilts. Such a design
needs a real-ray treatment on the optical axis ray, which this program does not have.

So: use it for build errors and figure error. Do not use it to model a scanner.

## Reading the report

### The sigma table

```
    surf      dec x      dec y     tilt x     tilt y      sigma x      sigma y
       2          0          0          0     0.0026   9.5477E-03   0.0000E+00
```

`sigma` points to where that surface's own aberration field has been displaced to, in the units
of the design's field measured from its centre. It is a geometric quantity — the field point
whose chief ray strikes that surface the way the axial ray would if the surface were centred.

It **diverges** where the chief ray meets a surface at normal incidence. That is not a failure of
the theory: the same surface then contributes no coma and no astigmatism either, and the products
the theory actually uses stay finite. The report prints `diverges` rather than a number, and the
field is computed from the finite form.

**Field curvature is the exception.** The Petzval part carries no factor of the chief-ray
incidence, so nothing cancels the division, and the medial vertex genuinely cannot be formed at
such a surface. The report says so rather than printing a number it does not have.

A *figured* surface has a second field centre — the aspheric cap is a zero-power plate, centred by
where the optical axis ray crosses it rather than by an angle of incidence — and that one divides
by the chief-ray HEIGHT, so it fails **at a pupil** instead. Two conditions, two surfaces, two
reasons. An ordinary surface at the stop is not affected by either.

### The nodes

Third order gives coma one node, astigmatism two, and field curvature a vertex. Fifth order:

| | |
|---|---|
| `W151` | one node, behaving exactly like third-order coma |
| `W240M` | a vertex and a scalar, like `W220M` |
| `W242` | two nodes, like `W222` |
| `W331M` | **three collinear** nodes — the outer two symmetric about the middle |
| `W333` | **three** nodes, from a cubic in Thompson's vector algebra |
| `W420M` | a vertex and a scalar |
| `W422` | **four** nodes — quadranodal |
| `W511` | not solved; see the limits below |

`W331M`'s collinearity is what distinguishes it from `W333`'s trefoil at a glance.

### What the fifth order does to the third

```
    W131    -6.1744E-04  ->  W131E   -7.4922E-04
          node (3.9517E-01, 0)  ->  (3.2670E-01, 0)
```

Expanding a fifth-order term about its displaced field centre throws off terms of third-order
**form**, and they change both the magnitude and the node of the third-order aberration they
belong with. So the third-order block is not the last word on a perturbed system — on a triplet
with one surface tilted, third-order coma is 17 per cent larger than the third-order block alone
reports, and its node has moved.

This is what Thompson means by an *induced* term. It is a consequence of the nodal algebra, not
an interaction between surfaces, and it is unrelated to Buchdahl's induced aberrations.

**The three left-hand numbers are the round trip.** They are the third-order block's own
coefficients, reached by the entire fifth-order route and converted back. They agree to every
digit, which is the whole chain closing at once.

### The unit note

The fifth order is computed in Buchdahl's normalised aperture and field, which differ from the
design's by a factor `A^l F^k` — one power of an aperture scale per power of `rho`, one of a field
scale per power of `H`. The report fits that scale, converts, and prints `A`, `F` and the fit
residual so the conversion can be audited.

The fit **checks itself**: four third-order coefficients give four ratios against two unknowns, so
`W040` fixes `A`, `W131` fixes `F`, and `W222` and `W311` are free checks. They agree to machine
precision. Where the fit is ill-conditioned the report says so and falls back to Buchdahl's units
rather than scaling by a number it cannot justify.

Node positions never needed any of this. Each is a ratio of quantities carrying the same powers,
so the scales cancel.

## Reading `nat.tsv`

A field grid, one row per point, with the aberration's magnitude and orientation at each.

```
hx  hy
coma         coma_orientation_deg         astigmatism    line_image_azimuth_deg
coma_E       coma_E_orientation_deg       astigmatism_E  astigmatism_E_azimuth_deg
coma5        coma5_orientation_deg        coma331        coma331_orientation_deg
trefoil      trefoil_azimuth_deg          astig5         astig5_azimuth_deg
distortion5  distortion5_orientation_deg
```

The first six columns keep the names and meanings they have always had.

**`_E` are the corrected third order** — coma and astigmatism *with* the terms the fifth order
generates, which is what a perturbed system actually has. They are in the same units as the
columns beside them, exactly: the ratio `W131E/W131` is free of the normalisation, so multiplying
by the Seidel `W131` lands in Seidel units with no scale factor to derive. On an aligned design
they equal the uncorrected columns to the last digit.

**Each azimuth is the orientation divided by the power of theta the aberration carries** — one for
coma and distortion, two for a line image, three for trefoil.

## Freeform overlays

A Fringe Zernike departure polished onto a surface generates **no new aberration types**. Every
term it contributes lands on one that already exists, and where it lands depends on whether the
surface is at the stop.

| overlay | at the stop | away from the stop |
|---|---|---|
| astigmatism `Z5/6` | field-constant astigmatism | nothing more - the rest is tilt and piston |
| coma `Z7/8` | field-constant coma | + field-linear astigmatism and field curvature |
| trefoil `Z10/11` | field-constant elliptical coma | + **field-conjugate field-linear astigmatism** |
| oblique spherical `Z12/13` | field-constant oblique spherical | + four more, on the vector squares |
| fifth-order coma `Z14/15` | field-constant fifth-order coma | + six more, on the first moments |

**A raw Fringe term carries more than its name.** `Z12` is `4 rho^4 cos2phi - 3 rho^2 cos2phi`,
and the quadratic half is astigmatism; `Z14` carries coma the same way. The sidecar states raw
sag, so those halves are routed to the third-order overlays rather than dropped - stating `Z12`
alone really does move the third-order astigmatism, and stating `Z12 + 3 Z5` cancels it back out,
which is the "adjusted Zernike" the paper works with. `Z10`'s leftover is a pupil tilt, which
moves the image instead of blurring it, so trefoil needs no such handling.

The trefoil row is the one worth knowing. A trefoil plate at the stop gives pure trefoil; moved
off the stop it generates astigmatism that grows linearly with field. That is the aberration a
**three-point mount** produces, and it is what Fuerschbach's Schmidt telescope was built to
demonstrate. The mechanism is the beam displacement `ybar/y`, which is zero at a pupil — there
the beam footprint is the same for every field point, so a contribution cannot acquire a field
dependence.

An overlay contributes a physical wave amplitude and so needs the unit bridge above; where the
bridge cannot be fitted, the overlay is declined and the report says why rather than applying a
factor it cannot justify.

## What is checked, and against what

Nothing here is validated against another optical design program. Everything is either a
published number, an internal identity, or two independent routes made to agree.

**Against published numbers**

- Thompson (2009) Table 3, the paraxial trace, every digit; Table 4, the optical axis ray
  crossing; **Table 5, sigma and sigma_aspheric to seven figures** — and matching his real-ray
  column more closely than his own paraxial route.
- Buchdahl VI **Table II**, the triplet Sigma1's seventeen aberration coefficients, in **both**
  coordinate systems. The unpatched scheme reproduces the paracanonical column and the patched one
  the W column, so the baseline is checked before the patch is trusted.
- Buchdahl VII **Table I**, the deformation *and* retardation coefficients of the same triplet.
  All fourteen, with **nothing fitted** — `e` is computed from where the two paraxial rays reach
  the axis, and comes out 0.968799 against the 0.968801 his table implies.

**Internal identities**

- Buchdahl VI (4.17), the secondary identities in W coordinates, once the powers of `e` are
  applied. These localised the one row that was wrong during development.
- The per-surface wave front split sums to the system to machine precision — which it must, every
  step being linear, and which is asserted because that additivity is why NAT can decompose at
  all.
- The analytic node positions are zeros of the unnormalised expansions. Thompson gives both forms
  and they are a page of vector algebra apart; both are implemented independently, so agreement
  checks the cubic, the branch choice and the vector products at once.
- The two astigmatic forms agree at every field and pupil point, which is what verifies
  Eqs. (C19-24) — nothing else does.
- Fifth-order distortion's closed form against the defining sum, surface by surface, to 1e-11.

**On mirrors**

- Thompson's two-mirror telescope, in the published-numbers list above, is the only reflecting
  design NAT is held to, and until September 2026 only its paraxial quantities were - the trace,
  the optical axis ray, and sigma by magnitude.
  Two paths had never met a mirror and both were wrong there: the wave front coefficients came
  out exactly zero (unsigned indices), and real-ray sigma lost the secondary (an intersection that
  gave up after a long gap). Both are fixed. The wave front now closes against the Seidel sums on
  the telescope to 4E-12, and real-ray sigma agrees with the paraxial one on both mirrors to 2E-5,
  signs included. See verification.md, *Nodal aberration theory on mirrors*.

**Physical invariants**

- A **uniformly displaced** system is the same system about a shifted axis, so every node moves
  with it and none splits. Carried all the way through the cubic, where a wrong branch would show
  as a splitting rather than a shift.
- An **aligned** system puts every node on the axis and collapses to ordinary Seidel.
- A **trefoil plate at the stop** moves the trefoil nodes and leaves the astigmatic ones alone; off
  the stop it moves both.
- The **medial vertex** comes out in the same place by both routes - the third order from the
  Seidel sums, the fifth-order machinery through Buchdahl's W coordinates - sharing nothing but
  the sigmas.

## What it does not do

- **`W511`'s nodes are not solved.** Its field is exact and checked, but Thompson's closed nodal
  form is in his 1980 dissertation, which is not to hand. A numerical search was written and
  removed: a direct scan of the field disagreed with it on real lenses, and the answer moved every
  time the tolerance was retuned. Five plausible coordinates that a scan contradicts are worse
  than none, so it reports none.
- **Seventh order** is not implemented. The tertiary rows of VI Table I are transcribed but unused.
- **NAT here is analysis, and feeds the optimiser one thing.** It is the
  `ASBLT` operand, Gu's as-built tolerance sensitivity.

## References

[references.md](references.md) has the full list under **Nodal aberration theory**. The load-bearing ones:

- **Thompson, K. P.**, "Description of the third-order optical aberrations of near-circular pupil
  optical systems without symmetry," *J. Opt. Soc. Am. A* **22**, 1389 (2005) — the vector algebra
  every other paper cites.
- **Thompson, K. P.**, *J. Opt. Soc. Am. A* **26**, 1090 (2009); **27**, 1490 (2010); **28**, 821
  (2011) — the multinodal fifth-order trilogy, and the source of every nodal solution here.
- **Buchdahl, H. A.**, "Optical Aberration Coefficients. VI and VII," *J. Opt. Soc. Am.* **50**,
  534 and 540 (1960) — the W coordinates, and the wave front coefficients they produce.
- **Fuerschbach, Rolland and Thompson**, "Theory of aberration fields for general optical systems
  with freeform surfaces," *Opt. Express* **22**, 26585 (2014) — the overlays.
- **Gu, Z. et al.** (2020) — the as-built sensitivity behind `ASBLT`.
