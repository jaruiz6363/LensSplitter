# The optimizer

    abcalc <lensfile> --optimize

This repository could always say what a lens *is*. This says what it could be instead.

What makes it worth having rather than being one more least-squares loop is that the derivatives
are **analytic everywhere**, including through the predicted spot — which is a quadratic form in
thirty-seven aberration coefficients reached through some five thousand lines of Buchdahl's
computing scheme.

**It carries conics and even aspheres**, and the figured flat in collimated light too. The
reasoning is below.

## The derivatives are exact

There is no finite difference anywhere in this optimizer. No step size is chosen, and no
quantity is ever formed as the difference of two nearly equal numbers.

The mechanism is one source compiled twice. Every file that computes a coefficient — the
paraxial recurrence, the skew ray trace, Buchdahl's scheme and its 155-entry table, the tertiary
cubics — is written in a type called `Scalar` rather than in `double`. In
`AberrationCalculator.Core` that alias **is** `double`, so the analysis computes exactly what it
always computed, to the bit; the 597 tests that guarded it before this work still guard it and
still pass. In `AberrationCalculator.Core.Ad` the *same files* are compiled with `Scalar` aliased
to a forward-mode dual number, and every quantity comes out carrying its own exact derivative.

    src/AberrationCalculator.Core/Numerics/ScalarAlias.cs     Scalar = double
    src/AberrationCalculator.Core.Ad/AdAlias.cs               Scalar = Dual

Nothing is copied. `AberrationCalculator.Core.Ad.csproj` *links* the same `.cs` files, so a
correction to Buchdahl's scheme lands in the derivative the moment it lands in the value, and the
two cannot drift apart.

**Why not .NET generic math.** `INumber<T>` is the modern answer and it is the wrong one here:
there are about 5,450 numeric literals in that chain, and every one would have to be written
`T.CreateChecked(...)`. A struct with an implicit conversion from `double` leaves all 5,450
exactly as Buchdahl and Rimmer wrote them.

**One derivative at a time, not a gradient vector.** A `Dual` is sixteen bytes — a value and one
derivative — so it lives in registers and allocates nothing. The *n* variables are *n*
independent passes, which run in parallel on as many cores as there are. Carrying a gradient
array instead would allocate on every one of the tens of thousands of arithmetic operations per
evaluation.

### How it is checked

`tests/AberrationCalculator.Tests/AnalyticDerivativeTests.cs` compares the analytic Jacobian
against a central difference of the same residuals, for **every operand** against **every
variable**. A central difference is accurate to about the two-thirds power of machine epsilon, so
agreement to five or six digits says the analytic derivative is right and the difference quotient
is the one carrying the error.

The step is sized so that the residuals move by about a part in a million, which matters more
than it sounds: a step that looks small in absolute terms can be enormous relative to a
variable's natural scale, and what comes back is then a secant rather than a derivative.

`PrmsaAgreesWithTheReport` closes the loop the other way: the PRMSA the optimizer descends is
equal to twelve decimals to the PRMSA `abcalc` prints.

### One place the derivative has to be taken deliberately

The ray-surface intersection is a Newton iteration, and an iteration converges on its *value*
long before a naive reading would give the right derivative. `RealRayTrace.Intersect` therefore
applies its final correction **after** deciding it has converged, not before. At a point where
the residual is zero that correction moves the value by nothing and evaluates to
`-(df/dp)/(df/dt)` — the implicit function theorem applied to `f(t, p) = 0`, exact whatever the
derivative was beforehand.

Returning one step early is the obvious optimisation and it is wrong. On a curved surface it
leaves every real-ray derivative one Newton step stale, which is small enough to look like
rounding. On a plane it is total: the flat-surface starting guess is already exact, the loop
returns on its first pass, and a plano surface being bent reports that bending it does not move
the ray at all. The finite-difference check caught it; nothing else would have.

## Figuring, and what is carried

The coefficients come from **Buchdahl's computing scheme** — closed-form sums over the paraxial
ray data, with no trace, no fit and no linear solve. It is the fastest route to a seventh-order
coefficient there is, and it is why the predicted spot can be evaluated tens of thousands of
times in a search.

It handles a conic or an even asphere correctly at third and fifth order. At **seventh** order a
figured system needs an aspheric arrangement Buchdahl never published as a table, and this
repository's reconstruction of it is a separate routine, `BuchdahlAsphericScheme`. Until
September 2026 that reconstruction was one the rays rejected, by up to a factor of four on
`tau20`, and **this optimizer refused every figured design** on that ground. It was the right
refusal: descending a quantity wrong by a factor of four is not slow, it is aimed wrongly.

**That arrangement is now established** — all twenty tau against Forbes' series trace to between
2E-13 and 2E-10 on every figured refracting design, the rays agreeing with both, and against
reflected real rays on a figured mirror, which Forbes declines, and an independent
transcription in `macros/BUCH7_ASPH.ZPL` reproducing `FORBES.ZPL` inside OpticStudio. See
[docs/verification.md](verification.md). So the optimizer carries figuring: a conic and the r^4,
r^6 and r^8 terms are variables like any other, spelled `CC`, `A4`, `A6` and `A8`.

**The route is not chosen in the inner loop**, which was the second objection and did not survive
contact with the code. `TertiaryCoefficients.Attach` makes the choice once per evaluation, out of
data it has already computed — not per surface, and not inside the arithmetic. A spherical design
travels the path it always did, bit for bit.

**What is still refused is not a class of design.** The figured flat facing collimated light was
the last one, and it is carried now — see below. What remains is a failure to CONVERGE: the
series route vouches for itself or it is not used, and on a design where it cannot, the run is
refused rather than allowed to keep values that are not finite in any useful sense. That is a
measurement on the design in front of it rather than a rule about a shape.

### The figured flat in collimated light, differentiated

A Schmidt corrector plate — a flat with r⁴ figuring in a collimated beam — has an identically
zero marginal incidence. The incidence ratio is infinite, and the finite coefficients arrive
only after terms carrying different powers of it cancel. The analysis side has reached them for
a long time by running the whole chain in Laurent series arithmetic with that surface's curvature
as the series variable and reading the answer at e⁰.

**The optimizer could not, and refused the design.** That route was compiled into Core alone; in
the differentiating build the call had no body and compiled away, so the optimizer would have had
a right value beside a silently wrong derivative. Refusing was the only honest option, because
nothing downstream could tell the difference.

**It is carried now, by a fifth arithmetic.** `DualSeries` is a dual number whose value and
derivative are each a `LaurentSeries`, and `AberrationCalculator.Core.Series.Ad` compiles the same
Core sources against it:

    Core             Scalar = double          the analysis
    Core.Ad          Scalar = Dual            the optimizer's derivatives
    Core.Series      Scalar = LaurentSeries   the flat in collimated light
    Core.Series.Ad   Scalar = DualSeries      both at once

**Why a dual OF series and not a series OF duals.** Both compute the same thing. The other way
round means making `LaurentSeries` generic over its coefficient type — some three hundred lines
of delicate, already-verified arithmetic edited for a case it was not written for, with the
sparse-skip hazard waiting at every `== 0.0` inside it. This way `LaurentSeries` is not touched at
all: every operation is the ordinary dual rule with the existing series arithmetic underneath, so
the half that was validated against Forbes stays exactly the code that was validated, and what is
new is one small struct whose rules are in every textbook.

**Why reading the two off independently is legitimate.** The answer wanted is the e⁰ coefficient,
and extracting a coefficient is linear — so the e⁰ term of the derivative series IS the derivative
of the e⁰ term. Nothing has to be re-derived to justify it.

**The one duplication, and the test that guards it.** The formulas are not duplicated; both builds
compile the same Core sources. What is written twice is about thirty lines of orchestration —
trace, coefficients, scheme, Table I, increments, tau, convert — because the double file carries a
diagnostic apparatus this build has no use for. If the two drifted, the derivative would belong to
a different calculation from the value, and the Jacobian check could not see it: that compares the
derivative against differences of the SAME route, so it would pass while the value came from
somewhere else. `TheDifferentiatedSeriesRouteAgreesWithTheDoubleOneOnValue` is the guard, and it
requires the two routes to agree on all thirty-seven coefficients.

**Measured**, on `Ladder2_FlatFigured`: the value agrees with the double route on all
thirty-seven, and the derivatives of `tau2` through `tau20` match central differences with respect
to a curvature, a thickness and the corrector's own r⁴ term. The tolerance there is looser than
the ordinary Jacobian check and deliberately so — the quantity being differenced is itself the
e⁰ term of a truncated series, so the central difference carries the series' truncation error on
top of its own. It is the less accurate of the two instruments, not the more.

One further case is worth recording because it looks like the bug and is not. Figuring a DUMMY
surface — air on both sides — changes nothing, because every figuring term in the scheme carries
`n' - n`. The derivative is zero and so is the central difference, and
`FiguringASurfaceThatCannotRefractHasNoEffectEitherWay` pins both halves, so a legitimate zero
column can be told apart from the one the broken route used to produce.

### The sparse-skip hazard, which figuring made real

Buchdahl's chain is full of skips of the shape `if (coefficient == 0.0) continue;`. They are
sound in ordinary arithmetic and a trap under differentiation: a quantity that is zero *because
nobody has moved it yet* has value nothing and a derivative of something, and dropping its term
leaves the value perfectly right while the gradient goes silently short.

This was known and handled for one case — a **plane** surface whose curvature is being bent, in
`Surface.Sag` and `RealRayTrace.SagSlope`, which ask `SMath.Vanishes` instead of `== 0.0`.
`Vanishes` is the same comparison in `double` and additionally asks about the derivative in the
dual build, so a structural zero is still skipped and the fast path stays fast.

**Carrying figuring made the same hazard bite much harder, and it took a test to find it.** The
case is the one a designer actually meets: a spherical surface, a conic declared variable, and
the optimizer asked whether figuring it would help. The conic is 0 on the first evaluation. Every
gate deciding whether the aspheric arrangement runs at all was a magnitude test on the VALUE —
`SMath.Abs(conic) > Eps`, `Abs(a) > 1e-30`, `a4 == 0.0` — so the surface read as a sphere, the
whole aspheric block was skipped, and every coefficient came back with a correct value and a zero
derivative. **The merit function would have been right and the gradient identically zero**, and
the optimizer would have reported that figuring the surface does not help, because it could not
see that it would. Seven gates in four files now ask `Vanishes`:
`BuchdahlCoefficients` (the conic and the three polynomial terms), `Surface.IsFigured`,
`TertiaryCubics.Figuring.From` and the polynomial multiply, `TertiaryScriptT`'s two expansions,
and one height-ratio shift in `BuchdahlAsphericScheme`.

`AFiguringVariableStartingAtExactlyZeroKeepsItsGradient` and
`HigherAsphericVariablesStartingAtZeroKeepTheirGradients` are the tests, and they failed before
the fix in the way that matters — the gradient was not wrong by a little, it was absent.

### What it costs

A figured evaluation is more work than a spherical one and always will be; the aspheric scheme is
a different and larger computation than Buchdahl's table. It is now considerably cheaper than it
was, because removing it uncovered a whole wasted pass.

`BuchdahlTableI.Compute` opened with a full recursive call to itself, assigned to a local that
nothing ever read — left behind by an approach that differenced against the unfigured system and
was abandoned before it was finished. It cost an entire extra pass of the scheme on every
evaluation of every figured design. Invisible while figured designs were only analysed one at a
time; not invisible at tens of thousands of evaluations a run. Measured, best of three runs of
three hundred, one machine:

| design | before | after |
|---|---|---|
| `Ladder2_A4_Both` | 0.385 ms | 0.258 ms |
| `CookeTriplet_SPOTM_..._A4_A8` | 0.557 ms | 0.326 ms |
| `CookeTriplet`, spherical | 0.063 ms | 0.066 ms |
| `KingslakeDG`, spherical | 0.087 ms | 0.092 ms |

A figured evaluation is 1.5 to 1.7 times faster. The spherical rows are the control: the dead
pass never ran there, so nothing should have moved, and what small movement there is is the
measurement's own spread.

**The analysis side is unaffected**, as it always was. `abcalc <lens>`, `--forbes`, `--screen`
and `--distortion-coefficients` handle conics and even aspheres at every order they report.

## Three files

The settings are split by what they describe, not by convenience. The **merit function** says
what the design should be, and can be carried from one design to another. The **variables** say
what may change about it, and mean nothing away from the design they name.

| | `.lhlt` | every other format |
|---|---|---|
| variables, bounds, pickups | in the lens file | `<lens>.var` |
| merit function | `<lens>.mf` | `<lens>.mf` |

A `.lhlt` states its own variables and pickups, so they are read from it and written back to it.
Its own **merit function is deliberately not read** — this tool optimizes a different one — and it
is left untouched in the file. Sidecars are named for the lens *including* its extension
(`triplet.zmx.mf`), so a folder holding `triplet.zmx` and `triplet.seq` keeps their settings
apart.

    abcalc lens.lhlt --optimize              # from the lens and its .mf
    abcalc lens.zmx  --optimize other.mf     # or a merit function you name

## The merit function

Plain text, one operand per line, because a merit function is something a designer argues with.
It wants to be diffed against last week's, commented, and read out loud in a design review.

```
TYPE, WEIGHT, TAR x, INPUTS
TYPE, WEIGHT, MIN x, INPUTS
TYPE, WEIGHT, MAX x, INPUTS
TYPE, WEIGHT, MIN x, MAX x, INPUTS
```

```
PRMSA,   1, TAR 0                        # the predicted spot: no inputs
EFL,   100, TAR 50,          2           # focal length, in wavelength 2
TTL,     5, MAX 60                       # total track
EGT,    10, MIN 1,           2, 4        # glass edges over surfaces 2 to 4
EAT,    10, MIN 0.1,         2, 4        # and the air gaps
DTRGT,  10, MIN 1.5, MAX 12, 2, 4        # diameter-to-thickness ratio
AXC,     2, TAR 0                        # axial colour
LCF,     5, TAR 0,           1.0         # lateral colour at the full field
DISTF,  10, MIN -2, MAX 2,   0.7         # distortion at seven tenths of the field
RY,      1, TAR 0,           7, 1, 1, 0, 1
```

The second field is the weight, and the numbers in that column above are there to show that the
column exists rather than to recommend anything — see **What the predicted spot cannot see** below.

**Targets and boundaries are different things.** An operand with `TAR` is driven to it and weighed
against everything else. One with `MIN` or `MAX` costs *exactly zero* — in the merit and in the
Jacobian — while it is satisfied. It does not pull the design gently toward the middle of its
range; it is simply not there until it is threatened. That is what lets a lens carry a dozen
manufacturability limits without any of them bending the answer.

**Residuals are relative where they can be.** A target operand contributes
`sqrt(weight) x (value - target) / |target|`, so a weight means the same thing whether the operand
is a 50 mm focal length or a 20 micron spot. Where the target is zero — which is what asking for
no spot and no distortion looks like — there is nothing to be relative to and the residual is
absolute.

### Operands, and what each takes

The inputs are positional. Which ones an operand takes is stated in exactly one place —
`OperandInputs` — so the parser, the writer and the error messages cannot disagree about what
`RY, 1, TAR 0, 7, 1, 1, 0, 1` means. **Trailing inputs may be left off** and take their defaults,
so `RY, 1, TAR 0, 7` is surface seven at the reference colour, the full field and the chief ray.

| operand | | inputs |
|---|---|---|
| `PRMSA` | predicted RMS spot over every field and wavelength | none |
| `TTL` | total track, first surface to image | none |
| `AXC` | real axial colour | none |
| `EFL` | effective focal length | `wave` |
| `LCF` | real lateral colour | `hy` |
| `DISTF` | real distortion, per cent | `hy` |
| `EGT` | edge thickness of a glass element | `surface, surface2` |
| `EAT` | edge thickness of an air space | `surface, surface2` |
| `DTRGT` | diameter-to-thickness ratio | `surface, surface2` |
| `PX PY PZ PL PM PN` | paraxial ray position and direction cosines | `surface, wave, hy, px, py` |
| `RX RY RZ RL RM RN` | the same for a real ray | `surface, wave, hy, px, py` |
| `ASBLT` | wavefront error a build tolerance would induce | `decentre, tilt-deg, wave` |
| a coefficient name | one named aberration coefficient, optionally one surface and one part (`ABER` internally) | `surface, wave` |

`hy` is a **fraction of the maximum field**, 0 on axis and 1 at the corner — not an index into the
field list, so a merit function can ask for seven tenths of the field whether or not the design
defines a point there. `px` and `py` are fractions of the pupil radius. `wave` is 1-based in the
order the lens file lists them. There is no zero: to use the design's reference colour, leave
the wavelength off.

`EGT`, `EAT` and `DTRGT` scan the span and produce **one residual per surface** they apply to,
rather than reducing it to the worst one. A minimum over surfaces has a gradient only at whichever
surface happens to be worst, so the optimizer fixes that one, the next becomes worst, and the
search chatters between them. They are measured at the **paraxial beam radius**, `|y| + |ybar|`,
not at the semi-diameter the file declared: a declared semi-diameter is a constant, and would tell
the optimizer that thinning a lens costs nothing at its edge when the beam is still the size it
was.


### Targeting a named aberration coefficient

The thirty-seven coefficients this program computes can be targeted individually, and **an
operand is written as the coefficient's own name** — the same name the report prints, so there is
no table between what a designer reads and what they type:

```
B,     1, TAR 0                 # third-order spherical to zero
Pi5,   2, TAR 0                 # fifth-order field curvature
M2,    5, TAR 0                 # sagittal oblique spherical - Shafer's limiting aberration
Tau15, 1, MIN -1e-3, MAX 1e-3   # and a seventh-order term held inside a band
```

Internally that is the `ABER` operand with the coefficient carried beside it; `ABER` on its own is
refused, because it does not say which. The names are `B F C Pi E` at third order,
`B5 F1 F2 M1 M2 M3 N1 N2 N3 C5 Pi5 E5` at fifth, and `B7` with `Tau2` to `Tau20` at seventh.
Case does not matter. `B7` and `Tau1` are the same quantity by two routes and only `B7` is
spelled.

**Why this is worth having beside `PRMSA`.** A predicted spot mixes eighteen coefficients into one
number, and it is a poor instrument for asking about any single one: two designs whose `tau15`
differs by a factor of five predict the same spot to one part in ten thousand, which is measured
in [verification.md](verification.md) and not assumed. A designer flattening a field or balancing
oblique spherical against fifth-order astigmatism is asking about the coefficient, and `PRMSA`
cannot hear that question.

It is also what makes a merit function with no rays in it practical. Shafer's case for that —
*"it is much quicker to try out many different configurations and ideas if there are no rays in
the merit function and you are only correcting the 3rd and 5th-order aberrations"* — needs the
coefficients targetable individually, not only their weighted sum. See
[references.md](references.md).

**System totals, or ONE SURFACE'S SHARE.** A surface number after the target picks out that
surface's contribution:

```
B,   1, TAR 0            # the system's third-order spherical
B,   1, TAR 0, 5         # surface 5's share of it
M2,  2, TAR 0, 5         # and its sagittal oblique spherical
Pi5, 3, TAR 0, 5, 2      # surface 5, in wavelength 2
```

Surface 0 is the object surface and contributes to nothing, so it means the whole system - which
is also what leaving the input off gives.

**The shares add to the total, exactly**, and that is the property worth having rather than an
incidental one: a designer comparing surface 5 with surface 3, or with the system, needs the
three to be the same kind of number. The chain keeps per-surface contributions unscaled and
multiplies only the totals by the F/number, so the scaling is applied on the way out;
`ThePerSurfaceContributionsSumToTheSystemTotal` holds all eighteen to it on a spherical design
and a figured one.

**`Tau2` to `Tau20` are system operands, and a surface or a part is refused.** The report does
split them by surface, as each surface's share of the tertiary scheme's totals, but there is no
intrinsic, figuring or induced split of them, and on a figured flat facing collimated light a
surface's own share has no finite value - only the sum does. Either question would then be
answered with a silent zero, which reads exactly like a surface that contributes nothing, so the
per-surface operands run `B` to `B7`.


**And the three PARTS of a contribution, which answer to different actions.** A suffix on the
name takes one of them:

```
M2,     2, TAR 0,     5      # what surface 5 carries, all told
M2.INT, 2, TAR 0,     5      # what it generates on its own - bend THIS surface
M2.FIG, 2, TAR 0,     5      # what its figuring adds - change the figuring
M2.IND, 5, MAX 1e-3,  5      # what was induced in it - the fix is UPSTREAM
```

`INT`, `FIG` and `IND`, spelled out as `INTRINSIC`, `FIGURING` and `INDUCED` if preferred, and
case does not matter. Leaving the suffix off gives all three together. **They add to the
contribution exactly**, so a designer who drives the intrinsic part and bounds the induced one has
accounted for everything the surface carries with nothing left unallocated;
`TheThreePartsSumToTheWholeContribution` holds thirteen coefficients on every surface of a
spherical design and a figured one.

Asked of the system - no surface, or surface 0 - a part is that part summed over the surfaces,
which is how to ask *how much of this design's oblique spherical is induced rather than made*.

**This is the operand Shafer's paper argues for**, and the reason the split is in this program at
all: *"This can only be done effectively, however, if the 5th-order aberration surface
contributions are broken into two components: the intrinsic component and the induced
component."* A surface can be blameless in isolation and still be the largest contributor, and
the two readings call for opposite actions - so an operand that cannot tell them apart is asking
the design to fix the wrong thing.

Two refusals, both because the honest answer would otherwise be a silent zero:

- **`B.IND` and the rest of the third order.** There is no induced third order - a third-order
  contribution is built from that surface's own quantities alone, so nothing earlier can act on
  it. The value would be zero on every design, which is indistinguishable from an aberration
  that has been corrected.
- **`Tau2.IND` and the rest of the tertiary.** They are not split into parts.
**What this makes possible** is the question Shafer says a design is decided by and a total cannot
be asked: not *is this design wrong* but *which surface, and is it that surface's own fault*.
"Surface 5 should contribute no coma" is an operand, and so is "surface 5 should not be having
coma induced in it".

**They are free in bulk.** Every coefficient comes out of one run of Buchdahl's scheme, which the
probe computes once per wavelength and caches, so a merit function of twenty coefficient operands
costs what one costs. That is what makes a coefficient-only merit function a practical way to
work rather than merely a possible one.

**In transverse measure throughout** — the numbers the report prints, whether the system's or a
surface's, so a target and a reading cannot disagree about either.

#### Which coefficient is which aberration

A designer reading Shafer, or Kidger, or a specification, meets aberrations by their names -
*fifth-order field curvature*, *sagittal oblique spherical* - and has to type a symbol. This is
that lookup, and it is the same mapping `AberrationNames` uses to annotate the report, so the
name beside a number in a report is the name in this table.

The classical names are R. B. Johnson's, "Polynomial Ray Aberrations Computed in Various Lens
Design Programs," *Appl. Opt.* **12**, 2079 (1973), Table I — the standard nomenclature Robb
cites. **Johnson's own finding is the reason to read the monomial column too:** he compared six
programs and found "significant variances in term definitions", so a named aberration means
little without the term it names. The monomial is what the coefficient multiplies in the
transverse polynomial, ρ the pupil radius and H the field.

| | coefficient | aberration | multiplies |
|---|---|---|---|
| **3rd** | `B` | spherical | ρ³ |
| | `F` | linear coma | ρ²H |
| | `C` | astigmatism | ρH² |
| | `Pi` | Petzval field curvature | ρH² |
| | `E` | distortion | H³ |
| **5th** | `B5` | spherical | ρ⁵ |
| | `F1` `F2` | linear coma — the pair together | ρ⁴H |
| | `M1` `M3` | oblique spherical, **tangential** — with `M2` | ρ³H² |
| | `M2` | oblique spherical, **sagittal** | ρ³H² |
| | `N1` `N2` | elliptical coma, tangential | ρ²H³ |
| | `N3` | elliptical coma, oblique | ρ²H³ |
| | `C5` | astigmatism — with `Pi5` | ρH⁴ |
| | `Pi5` | Petzval / field curvature — with `C5` | ρH⁴ |
| | `E5` | distortion | H⁵ |
| **7th** | `B7` | spherical. Robb's `tau1`, and the only tertiary term FIFTHORD and this program's own fifth-order working both reach | ρ⁷ |
| | `Tau2` `Tau3` | coma | ρ⁶H |
| | `Tau4` `Tau5` `Tau6` | oblique spherical | ρ⁵H² |
| | `Tau7` `Tau8` `Tau9` `Tau10` | *no classical counterpart* | ρ⁴H³ |
| | `Tau11` `Tau12` `Tau13` `Tau14` | *no classical counterpart* | ρ³H⁴ |
| | `Tau15` `Tau16` `Tau17` | elliptical coma | ρ²H⁵ |
| | `Tau18` `Tau19` | astigmatism / field curvature | ρH⁶ |
| | `Tau20` | distortion | H⁷ |

**The seventh-order rows name a family rather than an aberration, and two name nothing.**
`AberrationNames` gives the third and fifth orders the names Johnson tabulates. For the tertiary
there is nothing to look up — the classical vocabulary was built for a set that stops at the
fifth, where there are six monomials against the seventh's eight — so the family is *derived*
from the monomial by the rule the named orders already follow: no field is spherical, one power
of field coma, two oblique spherical; no aperture is distortion, one power of aperture
astigmatism and field curvature, two elliptical coma. All eleven named coefficients come out of
that rule, which is what makes applying it at seventh order reading the pattern rather than
inventing one. `TheFamilyRuleReproducesTheNamedOrders` is the anchor.

ρ⁴H³ and ρ³H⁴ get nothing, and that is the honest answer rather than a gap: they have no third-
or fifth-order counterpart to be named after. They are described by what they multiply, which is
exact.

**This table was wrong when first written**, and the rule above is what fixed it. Elliptical coma
and astigmatism were put two rows too high — elliptical coma is two powers of APERTURE, not two
of field — because the rows were typed out by hand against no rule at all. That is the argument
for deriving them: a hand-written table has no way to be checked against the pattern it is
supposed to follow.
**Shafer's two limiting aberrations, in this notation**, since they are the ones he argues a
design is decided by: *fifth-order field curvature* is `Pi5` (with `C5`), and *sagittal oblique
spherical* is `M2`. See [references.md](references.md).
### What the predicted spot cannot see

`PRMSA` is the obvious thing to ask for and it is not sufficient on its own. Robb's spot is the
variance of the ray intersection **at the Gaussian image plane**, taken **about its own centroid**,
and one coefficient set is computed **per wavelength from that wavelength's own paraxial trace**.
Each of those three words costs the merit function something it might be assumed to have:

| invisible to `PRMSA` | because | ask for it with |
|---|---|---|
| where the image surface is | the spot is referred to the paraxial focus, wherever the file put the image | `PY` at the image surface |
| a focus shift between colours | each wavelength is measured at *its own* focus | `AXC` |
| an image-height shift between colours | a shift of the whole patch does not change its size | `LCF` |

The third row is the same reason distortion is absent from a spot radius, which Robb's own terms
show by vanishing identically. The second is not an approximation either — it is what computing
the coefficients per wavelength means.

The scale of it, on the Kingslake–Kidger double Gauss shipped in `tests/fixtures/lenses`: axial
colour at the start is −0.0990 mm of focus shift, which at f/8 is a blur radius near 0.0062 mm,
while the on-axis predicted spot in blue reads 0.0011 mm. The spot is not wrong; it is answering a
different question. And moving the image surface twenty millimetres leaves `PRMSA` at 0.042589 —
the same to every digit printed.

**So focus the image plane with `PY`, and ask for both colours.**

```
PY,    1, TAR 0, 10, 2, 0, 0, 1          # marginal ray height at the image surface
AXC,   1, MIN -0.15, MAX 0.15            # axial colour
LCF,   1, MIN -0.01, MAX 0.01            # lateral colour, at the full field
```

`PY` here is the paraxial marginal ray (`px` 0, `py` 1) on axis (`hy` 0) at surface 10, the image.
Driving its height to zero puts the image surface at paraxial focus, which is where the spot was
being measured all along — so the operand does not pull the design anywhere, it makes the last
thickness *mean* something. Without it, a variable back focus has exactly zero gradient from
`PRMSA` and will sit wherever it started or wander on whatever else touches it.

Lateral colour is worth stating separately because it is the one most often left out. A run given
`AXC` alone comes back achromatic on axis and can be smeared in colour at the edge of the field,
and nothing in the merit function will have mentioned it.

**This document does not advise on weights.** Where a requirement is a boundary — an edge
thickness, a colour tolerance, a distortion limit — say it as `MIN`/`MAX` and it costs exactly
zero until it is threatened, which needs no weight chosen for it. Where it must be a target, the
right weight depends on the design, and the handful of examples in this repository are not enough
to generalise from. Changing a weight changes the scale of the merit, so two runs weighted
differently are not comparable by their merit numbers at all — judge them on the physical
quantities the report prints.

### The one operand about what gets BUILT

Every operand above measures the design on the page. `ASBLT` measures what will survive being
made.

```
ASBLT, 10, TAR 0, 0.04, 0.15             # 0.04 lens units of decentre, 0.15 DEGREES of tilt
```

It is the RMS wavefront error a decentre and tilt tolerance of the stated size would induce. The
theory is nodal: a perturbed surface contributes the same rotationally symmetric aberration field
it always did, displaced by a vector linear in the perturbation, so the induced coma and
astigmatism can be written down in closed form. Gu, Wang and Yan, *Opt. Express* **28**(6), 7928
(2020).

The two tolerances are **RSS'd, not added** - over the surfaces, and over the two error types at
each surface - because they are independent errors of manufacture rather than a single known
displacement. A decentre enters as its equivalent tilt, `c D`, which is how a shift of a surface
with curvature acts. The field dependence is carried by the mean-square field height over the
design's own field points and their weights.

**Why it is worth having.** A design can always be driven to a smaller predicted spot by making
it more sensitive to the tolerances it will actually be built to, and **nothing else in the merit
function objects** — `PRMSA` is measured on a perfectly centred lens and does not know the lens
will be assembled by somebody. This is the term that objects.

**It traces no rays.** The result is algebraic in the two paraxial rays the program already has —
`y, ybar, u, ubar, n, c` — so it costs the evaluation loop almost nothing, and it differentiates
exactly on the dual-number compile like everything else here rather than being a difference
quotient bolted on beside the analytic ones.

**Tilt is in DEGREES.** It is the one angular quantity this program takes from a user anywhere,
and it matches the `.align` sidecar that drives `--nat`. The conversion to radians happens once,
inside the evaluator.

A sensible use is as a bound rather than a target — `ASBLT, 1, MAX 0.05, 0.04, 0.15` says the
design must stay buildable to that tolerance without paying for buildability it does not need.
Stated as `TAR 0` with a weight it competes with the spot, which is the trade it exists to make
visible, and how hard to push is a decision this document will not make for you.

## Variables and pickups

```
VAR CV 1                                 # curvature of surface 1, unbounded
VAR TH 2 MIN 1.0 MAX 25.0                # a thickness, bounded
VAR CV 4 MIN -0.05 MAX 0.05
PICKUP TH 2 INDEX 1 SCALE 1 OFFSET -0.1  # surface 2's thickness follows surface 1's
```

`CV`, `TH`, `CC`, `A4`, `A6` and `A8` — see **Figuring, and what is carried** above.

**A `VAR` line merges into what is already known.** `VAR TH 2 MIN 1.0` followed by
`VAR TH 2 MAX 25.0` leaves both limits, not the second alone. For a file this program writes the
rule never comes up, since it emits one canonical line per variable; it matters when a *command*
sets one limit, because a command naming only the maximum would otherwise silently discard a
minimum set a moment earlier — the surprise `chmod u+x` exists to avoid. `FREE` drops the bounds
again, since merging means they can no longer be removed by omission.

Bounds are enforced by **reflection — not by clamping, and not by a sigmoid**. A step that would
carry a variable past a limit is folded back inside, as light off a mirror, and the variable keeps
its physical units throughout. Reflection has derivative of magnitude one everywhere: the sign
flips at each fold and nothing else changes.

Both of the usual alternatives lose that, in different ways. A **clamp** pins the variable at the
limit and throws the rest of the step away, so the Jacobian goes on describing a variable that is
not moving; the step is recomputed, clamped again, and the search stalls against the boundary with
a gradient it cannot act on. A **sigmoid** maps the interval onto an unbounded coordinate whose
derivative goes to zero *at* the bound — so a variable driven against a limit stops responding,
and no amount of gradient brings it back when the design later wants it. Either way a bounded
variable becomes numb exactly where the design is most likely to need it, which matters most in
the two places this optimizer lives: constrained descent, and a stochastic search that throws
large steps on purpose.

**That argument is from first principles and there is a literature it never consulted**, now in
[references.md](references.md#optimisation). Its finding, across twenty-eight algorithm variants
and thirteen boundary methods, is that the choice "substantially affects the empirical
performance"; that far more candidates go out of bounds than practitioners assume; and — the claim
that bears on this page whichever method is used — that results "cannot be considered reproducible
unless the algorithm fully specifies what should be done with solutions generated outside the
domain". By that standard this section is in order, since it names the rule and both rejected
alternatives and gives the reasoning. What it does not do is MEASURE any of it, and the
literature's importance-grows-with-dimensionality finding is a caution for basin hopping
specifically, where twenty variables is ordinary.

A variable that touches either end of a **pickup** is refused. Pickups are resolved when a file is
read and are not maintained afterwards, so optimizing one end of a cemented pair would part the
cement.

## The optimizers

### PSD — Dilworth's pseudo-second derivative

The merit function is a sum of squares whose exact second derivative is

    d2phi/dxj dxk = 2 [ (J'J)_jk + sum_i r_i d2r_i/dxj dxk ]

Gauss-Newton, and Levenberg-Marquardt after it, drop the second term entirely and damp with an
arbitrary multiple of the first. That is safe, and it is why LM is hard to break. But the
discarded term is not small on a lens — it is exactly the part that knows an aberration
coefficient is a strongly curved function of a curvature — and throwing it away is what makes a
least-squares run crawl once the residuals stop being small.

Dilworth's insight is that the term cannot be computed but its **diagonal can be estimated for
nothing**, from two gradients the optimizer already has:

    d2phi/dxj^2  ~  ( g_j(k) - g_j(k-1) ) / ( x_j(k) - x_j(k-1) )

Subtracting the Gauss-Newton diagonal from that leaves an estimate of precisely the part that was
dropped, and *that* — rather than a blind multiple of the identity — is what is added to the
normal equations. D. C. Dilworth, "Pseudo-second-derivative matrix and its application to
automatic lens design," *Applied Optics* **17**(21), 3372 (1978) - and his 1983 follow-up on the
stabilising factor, which is the one that bears on `psd2` against `psd3`. Both are in hand, and
neither has yet been read against the code; they, and the rest of what the optimiser is built
from, are in
[references.md](references.md#optimisation).

The secant is a difference of two gradients, so both ends have to be real measurements — which is
why this optimizer holds an **exact analytic Jacobian at every iteration and never Broyden-updates
one**. Differencing two approximated gradients measures the update rule rather than the design.
Here both ends are better than real, being analytic rather than differenced, so the estimate
carries no truncation error of its own.

`psd2` clips a negative estimate to zero and can therefore only ever be as bold as Marquardt.
`psd3` keeps the estimate **with its sign**: a negative estimate says the merit falls away along
that variable faster than Gauss-Newton believes, so the right step there is *longer*, and it is
floored at a fraction of the Gauss-Newton diagonal so the matrix cannot go indefinite. That
distinction is the whole of the difference between them.

On the Cooke triplet with eight variables and twelve operands, four hundred iterations:

| method | merit |
|---|---|
| `lm` | −23.4% |
| `psd2` | −31.7% |
| `psd3` | −30.8% |

**What is Dilworth's and what is not.** The per-variable secant curvature, and its use as the
damping diagonal, are his. The safeguards around it — clipping, smoothing between iterations,
answering a rejected step by increasing the damping — are ordinary practice, and the exact
division of labour between `psd2` and `psd3` here is this implementation's reading of the idea
rather than a transcription of his PSD-III. It has not been checked against the original paper.

### Hooke-Jeeves

    --method hj

A direct search that uses no derivatives at all, which sits inside a derivative-exact optimizer
because the two are good at different things. The least-squares step is built on a quadratic
model and is unbeatable while that model holds; it is helpless where it does not — at a boundary
operand's kink, where a residual switches on and its derivative jumps, or in the flat-bottomed
valleys a corrected lens sits in. Pattern search asks a cruder question — *is the merit lower over
there?* — and that question still has an answer in both places.

### Basin hopping

    --save results --hops 3000 --chains 8

Local minimisation, a kick, local minimisation again, and a rule for whether to stay. Every local
method finds the bottom of the valley it starts in and stops; a lens problem has a great many
valleys, most of them poor, and which one a design falls into is decided by the starting
prescription rather than by anything about the optics.

Accepting only improvements makes the walk a hill climb over basins and it sticks in the first
decent one, so a worse design is accepted with probability `exp(-dMerit/T)`. The temperature is
**autotuned from the merit the chain is working at**, because a fixed one set for the beginning
accepts everything by the end, and set for the end accepts nothing at the beginning.

Two rescues, and the difference between them matters. A chain that has gone a while without a new
best gets a **long jump** from its own best. Separately, a chain that is *both* stalled *and* far
worse than the global best may be handed that design — and both conditions are required, because
a Metropolis walk goes tens of hops between records while working perfectly well. Reseeding on a
stall alone collapses every chain onto the leader and throws away the independence that made
running several worth it.


**The kick has to be in each variable's own units, and for the figuring kinds it was not.** An
unbounded variable is kicked against its own size floored at one — the floor being what lets a
variable sitting at exactly zero move at all. For a curvature near 0.01 that is a tenth of the
curvature: meaningful and survivable. For an r⁴ coefficient near 1E-6 it was a kick of 1E-3,
three thousand times the value, putting several lens units of sag on the surface; for r⁸ it was
worse by a further six orders.

**It broke nothing, which is why it lasted.** The local minimisation after each hop hauled the
design back and the Metropolis test rejected it, so a run still converged — it simply spent every
figuring hop climbing out of somewhere absurd instead of exploring, which is the precise failure
this section already warns about for kicks that are too large. Figuring is now kicked against the
scale `Scaling.PhysicalCeilings` gives it, the same one the local optimiser steps by, which puts
every kind on one footing: how far a step moves the glass at the edge of the aperture. Curvature
and thickness keep the rule they had, because the default kick size was measured against it and
the figuring fix is not a reason to disturb it.

**An honest note on what that bought.** On the design it was found with, the fix does not change
the answer: same merit to every printed digit and the same r⁴ value to five, across several seeds
and up to thirty hops, because the local optimisation recovers either way. What it removes is
wasted work, and no design has yet been found where it removes more than that. It is made because
a kick three thousand times a quantity's own size is indefensible on its face, not because a
measurement demanded it — and saying so is better than implying a benefit that was not observed.

**And then the better question: should the kick reach the figuring at all?** LensHH-LT does not
randomize aspherics, and on reflection that is the right line. A conic or an r⁴ term is a
nearly-linear correction that the local stage refits reliably from wherever it starts, so throwing
it does not choose a different basin — it discards a figure that is about to be fitted again,
while the shape variables, which do choose the basin, get no more of the kick for it. So the hop
now passes the figuring over by default. `--hop-figuring` (`hop_figuring` over MCP) asks for the
old behaviour, which is defensible for a conic: at −1 and at 0 that is a genuinely different
surface rather than a small correction.

**The figuring is still optimised at every hop.** The exclusion is from the random kick and
nothing else — Hooke-Jeeves and the least-squares stage step the conic and the aspheric terms
exactly as before, off the same `PhysicalCeilings` scale. This is worth stating because an
implementation that skipped figuring in both places would still improve the merit on every design
and would pass any test that only watches the merit, while quietly freezing the figure; the two
are asserted apart in `HopFiguringTests`. On the design this was measured on, an r⁴ term seeded
at 3.0E−07 still lands at −4.90E−06 with the kick switched off: a change of sign and a factor of
sixteen, done entirely by the local stage.

**And it changed no answer either**, which by now is the expected result rather than a
disappointment. Seeds 7 and 23 over thirty hops give the same merit to every printed digit and
the same r⁴ to five, kicked or not. What the default buys is that the kick is spent where it can
choose a basin instead of where it cannot.

**The first hop takes its own sigma, which until now it did not.** `--initial-sigma`
(`initial_perturb_sigma` over MCP) sizes the kick applied before the design has ever been
minimised. That kick has a different job from the ones after it: it breaks exact symmetry, since
a design sitting on a stationary point has nowhere to go, while every later kick is asking to be
moved somewhere new. Raise it to start from a deliberately disturbed design — useful off a
skeleton, where the starting point is a guess rather than a design — without making every
subsequent hop that violent.

The option existed from the first optimizer commit, `01fa6a4`, with exactly that reasoning
written on it, and was **never connected to anything**. The first hop was kicked at `HopSigma`
like all the others. The two default to the same 0.001, so no run and no test could tell the
difference, and nothing said so: a knob that is documented, exported and inert is the quietest
kind of defect, because reading the code confirms the feature and only grepping for the reference
refutes it. Connecting it changes no default behaviour, which is asserted rather than assumed.

Steps are in units of each variable's **natural scale**, computed from the Jacobian as the step
that moves the merit by a set amount, capped by what the parameter can plausibly do on this design
(curvatures against the focal length, thicknesses against the total track). A search that stepped
every variable by the same fraction would be meaningless: a curvature lives near 0.02 per
millimetre and a thickness near ten. This is a use exact derivatives can be put to that a
finite-difference optimizer cannot easily match, since it would have to spend a whole extra
Jacobian to find out how big a step to take.

**Glass is the discrete variable.** There is no derivative from N-BK7 to SF11, only a list, so a
glass change can only be proposed and judged. The continuous variables are re-minimised around the
new glass before the acceptance rule sees it, because a glass that is better in the right shape is
generally worse in the shape that suited the old one.

The list is a **substitution catalogue, named on the command line** — `CoreSet28`, say — and not
the catalogues a design is read through. The two are kept apart on purpose: a search free to pick
from every vendor catalogue at once settles on glasses nobody stocks, because the space is dense
enough that there is always something a shade better a few weeks' lead time away. `CoreSet28` is
twenty-eight glasses that are common, available and spread across the diagram, and it lives in
`catalogs/Substitution`. Nothing in the analysis side reads that folder, and nothing there is
loaded when a lens is opened.

## Commands

Setting up a run takes more than fits on one command line — a merit function is a dozen operands
and a variable list is a dozen more — so the settings are built **a line at a time**, and each
command reads the settings file, changes it and writes it back. A one-shot command line therefore
behaves like a program that remembers, and what it remembers survives a restart, can be opened in
an editor, and can be committed alongside the lens.

**The command IS the file line.** `VAR "TH 2 MIN 1.0 MAX 25.0"` writes `VAR TH 2 MIN 1.0 MAX 25.0`
and that is the whole of the translation. A transcript of commands is a valid settings file and a
settings file is a script of commands — one grammar to learn, one to document, one to test. The
alternative, inventing `--min` to stand for `MIN`, is a second dialect for the same ideas that has
to be kept in step with the first forever.

    abcalc lens.zmx VAR "TH 2 MIN 1.0 MAX 25.0"     declare a variable, or bound one
    abcalc lens.zmx VAR "TH 2 FREE"                 drop its bounds
    abcalc lens.zmx VARLIST                         list them, numbered
    abcalc lens.zmx VARREMOVE 2                     remove number 2

    abcalc lens.zmx PICKUP "TH 2 INDEX 1 SCALE 1 OFFSET -0.1"
    abcalc lens.zmx PICKUPLIST
    abcalc lens.zmx PICKUPREMOVE 1

    abcalc lens.zmx OP "EFL, 100, TAR 50, 2"        add an operand
    abcalc lens.zmx OPLIST
    abcalc lens.zmx OPREMOVE 3                      remove number 3

Several commands may be given in one invocation, and a run may follow them:

    abcalc lens.zmx VAR "CV 1" VAR "CV 2" OP "PRMSA, 1, TAR 0" --optimize --save

A `VAR` line **merges** with what is already there rather than replacing it, so
`VAR "TH 2 MAX 25"` after `VAR "TH 2 MIN 1"` keeps the minimum. Last-wins would be simpler to
implement and worse to use: naming a maximum is not a statement about the minimum, and silently
dropping it is the kind of surprise that costs an afternoon. `FREE` is how you say the opposite
deliberately.

Removing **renumbers**, so a `REMOVE` prints the new listing. A user removing two things in a row
would otherwise be working from numbers that no longer mean what they meant. An out-of-range
number is refused by number — `there is no 9: the numbers run from 1 to 3` — rather than clamped
to the last one, because deleting the wrong thing helpfully is still deleting the wrong thing.


### Asking what a command or an operand does

    abcalc HELP           every command and every operand, briefly
    abcalc HELP VAR       one command: what it does, and an example
    abcalc HELP EFL       one operand: what it measures, what it takes, a line to copy

`abcalc --help` is still the whole command line. `HELP` is the part you need while writing a
merit function, which is where the questions actually are: the inputs on an operand line are
**positional**, so `RY, 1, TAR 0, 7, 1, 1, 0, 1` is unreadable unless something tells you that
the five trailing numbers are surface, wave, hy, px and py — and `HELP RY` does, with an example
in the same shape.

All of it is **generated**, from the same tables the parser reads: the commands from the one
list that also drives the command line and the MCP, the operand signatures from the table the
parser, the writer and the error messages already share. So an operand cannot be added without
showing up here, and a signature cannot change without the help changing with it. A help page
kept by hand beside the thing it describes is a page that is wrong within a release or two.

An operand's example is checked in the test suite by **parsing it** — an example that does not
parse is worse than none, because it will be copied, and then it will fail.

### The base folder

One command is not about any particular lens, and is the only one that can be given on its own —
naming a folder is what stops you having to type the path to a lens in the first place.

    abcalc BASE "C:\lenses\project7"     bare names now mean this folder
    abcalc BASELIST                      show it, and where it came from
    abcalc BASEREMOVE                    forget it

It is **kept until it is changed**, so it holds in the next shell too. In a terminal that is a
convenience — `cd` already does most of it — but over MCP it is the difference between working
and not: an MCP server's working directory is whatever the client started it in, not anything the
user chose and not anything they can change, so without a base every path an assistant passes has
to be absolute. The `base_path` tool sets the same setting.

**Four things can set it, most specific first:** `--dir` on the command line, `ABCALC_DIR` in the
environment, the folder set with `BASE`, and failing all of those the working directory. Each is
easier to change than the one below it, which is the order that lets a stored base be overridden
for one run without being un-set — a quick look at another folder should not cost you your
setting. `BASELIST` says which of the four is in force, because *why is it looking there?* is the
only hard question this raises.

**An absolute path is never re-rooted.** Whatever the base is, `C:\elsewhere\L.zmx` means what it
says; a base that quietly redirected absolute paths would be a trap rather than a convenience.
The base applies to what a run **writes** as well as what it reads — `--saveas better.zmx` lands
in the base folder, not beside the shell.


## Command line

| | |
|---|---|
| `--optimize [mf]` | optimise once; optional merit-function file |
| `--optimize_basin_hopping` | search over basins instead, one design per chain |
| `--method lm\|psd2\|psd3\|hj` | default `psd3` |
| `--iterations <n>` | local iterations, or iterations per hop - a cap, not a count; default 200 for a local run, 6000 per hop when hopping |
| `--hops <n>` | hops per chain; default 3000 under hopping. Ctrl+C stops the run and keeps what every chain found |
| `--chains <n>` | default 0 = one per PHYSICAL core, not per logical processor: two SMT threads share one core's execution units, and on a hybrid part the logical count includes efficiency cores, which hold up every hop they are given |
| `--seed <n>` | default 1234 |
| `--hop-sigma <s>` | size of a hop, in natural steps; default 0.001 |
| `--initial-sigma <s>` | size of the first hop only; default 0.001, the same as `--hop-sigma` |
| `--hop-figuring` | kick the conic and aspheric terms too; off by default, and they are optimised either way |
| `--glass_substitution <catalogue>` | let the hopping try glasses from that catalogue |
| `--dir <path>` | take bare names against this folder, for this run only |
| `--save` | overwrite the lens that was read |
| `--save <folder>` | under hopping, where the per-chain designs go |
| `--saveas <path>` | write the result somewhere new |

**Nothing is overwritten unless overwriting is asked for by name.** With none of those, the
original is left alone and the result is written beside it as `<name>.optimised.<ext>`, so a run
that made things worse costs nothing but the time — and the report says plainly when that is what
happened. `--save` with no path is the destructive form, and it is destructive only because it
was typed.

Basin hopping is **refused without `--save <folder>`**. It produces one design per chain, and
which of them is interesting is a judgement only a designer can make; keeping the single lowest
merit and discarding the rest throws away most of what the run paid for. Each chain's design is
written as `<name>.chainNN.<ext>` with its own `.mf` and `.var` beside it, so any of them can be
picked up and worked on further, and one report covers the run.

## Saving back

**Into another format.** `--saveas better.len` on a design read from `better.zmx` writes a whole
new OSLO lens - and likewise for any of `.zmx`, `.seq`, `.len`, `.otx`, Optiland `.json` and
`.lhlt`. That cannot be an edit, since the target has never seen the rest of the original, so it
carries what this program models: surfaces, glasses, conics and aspheric terms, aperture, fields
and wavelengths, each in the target's own terms (an OSLO lens gets its primary wavelength first
and, at a finite object, NAO and OBH; a glass Code V does not ship becomes a private glass). What
only the original format holds - solves, coatings, tolerances, configurations - does not go across,
and the save says so. A design the target cannot carry is refused, not written as another lens:
CODE V, OSLO and OPTALIX have no r² aspheric term. (This went through the editor whatever the
output was called, so a ZEMAX design saved "as" `out.len` became ZEMAX text in a file named for
OSLO.)

**Into the same format**, the optimised design goes back by editing the file rather than
regenerating it. Only what the optimiser can move ever changes — curvatures, thicknesses, glass
names, and conics and aspheric terms — and it goes back in the file's own units: a design opened
from a file written in inches returns in inches, aspheric coefficients included (a coefficient of
r^(2k+2) scales as length^-(2k+1)).

**Conics and aspheric terms go back into `.lhlt`, `.zmx` and Optiland `.json` only.** A `.zmx`
sphere that the optimiser figured becomes an `EVENASPH` with `PARM` lines; an Optiland
`StandardGeometry` becomes an `EvenAsphere`, whose `coefficients` start at r² (measured in
Optiland 0.6.2, and checked by having Optiland load the saved file —
`OptilandLoadsTheFiguringThisProgramSaved`). CODE V, OSLO and OPTALIX files have no writer for
figuring, because there is no real aspheric example of any of them here to check one against. A
design whose figuring moved is therefore **refused** in those formats: nothing is written, the
command exits with an error that names what moved, and the optimisation report — which lists every
conic and aspheric change — is written anyway, so the values are not lost. To keep them in a lens
file, start from a `.zmx` or `.lhlt` export of the design.

Until September 2026 no format but `.lhlt` took figuring back, and nothing said so: an optimised
conic singlet (F1, CC −0.6 → −1.46, predicted spot halved) saved a file that still said
`CONI -0.6` and read back as the design that went in. See docs/verification.md, *The figuring the
save dropped*.

Editing rather than regenerating is the whole point. This program recognises twenty-three .zmx
directives and a real `.zmx` has many times that in solves, coatings, apertures, tolerances and
multi-configuration data; a writer that rebuilt the file from what it understood would quietly
delete the rest of somebody's design. So the original is read, what the optimiser moved is
changed, and every other byte is left alone — including the encoding and the line endings,
which for a `.zmx` are UTF-16 and CRLF. `PatchingAZmxKeepsItsEncodingAndChangesOnlyWhatMoved`
holds it to exactly one changed line.

The same reasoning governs the `.lhlt`: a merit function, vignetting settings and whatever the
next version of the program that wrote it adds all survive being written through this one.

**Every format this reads, it writes back**: `.lhlt`, `.zmx`, Optiland `.json`, CODE V `.seq`,
OPTALIX `.otx`/`.opt` and OSLO `.len`/`.osl`. Each needed working out separately, because each
says the same things differently:

| format | shape | a plane | a property with nothing to say |
|---|---|---|---|
| `.zmx` | `CURV` curvature | curvature 0 | written anyway |
| `.json` | `radius`, plus an **absolute z** per surface | a very large radius | written anyway |
| `.seq` | radius, **positional** in the surface line | radius `0` | `AIR` in the third field |
| `.len` | `RD` radius | no `RD` line at all | **omitted** |
| `.otx` | `CUY` curvature | curvature 0 | **omitted** |

The two that omit are the awkward ones: bending a plane or substituting glass into an air space
means *adding* a line that was never there, in the block it belongs to, and the insertions are
applied from the bottom of the file upwards so no earlier position shifts under a later one.
Optiland is awkward differently — it records where each surface *is* rather than how far it is
from the one before, so moving one thickness moves everything after it, and it writes `Infinity`
as a bare word, which is not valid JSON and has to be carried through a parse without being
turned into a number.

`EveryFormatCarriesTheSameMoveBackUnchanged` runs the same three edits — a curvature, a
thickness, a glass — through all six and reads each back, which is the check that they agree;
`FiguringGoesBackIntoEveryFormatThatCanCarryIt` does the same for a conic and aspheric terms put
on spheres, and `AFormatThatCannotCarryFiguringRefusesToDropIt` holds the other three to refusing.

The MCP server offers the same as `optimize`, taking the merit function inline as text, so an
assistant can compose one without writing a file. It writes nothing unless given `save_to`.

## What this does not do

- **A design is refused only when the series route cannot vouch for its answer** - a failure to
  converge measured on that design, not a class of shape. Conics, even aspheres, the figured flat
  in collimated light and mirrors are carried; see *Figuring, and what is carried*.
- **A variable that touches a pickup is refused.** Pickups are resolved when a file is read and
  are not maintained afterwards, so optimising one end of a cemented pair would part the cement.
  Constrain the pair with operands instead.
- **Glass moves only in the hopping.** Model glass `nd`/`Vd` are not continuous variables.
- **PRMSA is the *predicted* spot**, and inherits the accuracy of the prediction —
  [docs/spot-prediction.md](spot-prediction.md) measures that at a few per cent on a plain triplet, and much worse
  on hard-corrected aspheric designs. On a design the series does not describe well, the optimizer will faithfully
  minimise a quantity that is not quite the spot.
- **Semi-diameters are not re-solved.** The clear aperture used by the edge and ratio operands is
  the paraxial beam, which follows the design, but vignetting is not modelled.

## Either conjugate

Every operand works with the object at infinity or at a finite distance. `PRMSA` and the paraxial
operands always did; the real-ray ones — `RX`–`RN`, `LCF`, `AXC`, `DISTF` — used to refuse a
finite conjugate, and that refusal was in the wrong place.

`RealRayTrace` propagates a ray given at surface one's vertex plane and **never asks where the
object is**. Only the *aiming* did: it built the ray direction from the field angle alone, which
is true of a collimated beam and of nothing else, because light from a finite object leaves at a
direction that depends on which pupil point it is heading for. Given the object distance both are
the same construction — the line through two known points — so the launch now takes the object
point, the pupil point, and draws the line between them.

The test that this is the *right* line, rather than merely a line, is that a real ray must
converge on the paraxial one as it shrinks toward the axis: scale field and pupil together and
the aberrations fall as the cube while the paraxial part falls linearly. A launch wrong at first
order — object on the wrong side, height read in the wrong convention, pupil at the wrong
distance — could not converge at all. `FiniteConjugateRayTests` measures that for both field
conventions, object height and object angle.

One caller still assumes infinity and now says so itself. `CoefficientInversion` measures the
field as `tan(theta)` and subtracts a paraxial height of `efl*tan(theta)`, neither of which means
anything at a finite conjugate; it used to be protected by the trace refusing, and it now carries
its own guard. That is the honest place for it — the assumption was always that method's, not the
tracer's.
