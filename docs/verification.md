# What is verified, and by what

This program computes quantities that are easy to get wrong and hard to check by eye. This
records what has actually been established, by what means, and what has not.

The working log those checks were made in is kept outside this repository, with the author's
notes: it transcribes source material verbatim to reason against, which belongs where the
sources are and not in an MIT-licensed repository. What follows is the standing position
rather than the history of arriving at it.

## The order of evidence

Not every check is worth the same, and they are listed in the order they carry weight.

**1. Buchdahl's own printed numbers.** Paper III works Table I through for a specific
triplet. `BuchdahlPublishedTableTests` reproduces it entry by entry, and his published
totals for the tertiary. This is the oracle, and it is what found the errors in t100–t108.

**2. Closed-form analytic surfaces, which need no other program at all.** A parabolic mirror
images infinity onto its focus with no spherical aberration at any order, so every order must
cancel term for term. A single conic surface can be traced analytically and expanded as
`eps = a3 y^3 + a5 y^5 + a7 y^7 + ...`, giving the third, fifth and seventh orders as numbers.
`ParabolicMirrorTests` and `ExactConicSurfaceTests`. **This is what establishes the aspheric
third and fifth order**, and it had to: nothing else available computes them.

**3. Inverse real ray tracing.** `CoefficientInversion` recovers coefficients from the
landings of real traced rays, by scaling and an odd-polynomial fit. It is this repository's
own code. Over a dozen test files use it as their reference, including every aspheric one, and
since September 2026 it recovers the third and fifth order the same way (`InvertThirdAndFifth`).

**4. An independent implementation** of the third and fifth order, written in C++ by this
repository's author directly from the monograph — a different lineage from the same source.
It does not implement aspherics.

**5. Forbes' series trace**, from a separate published paper with no shared code. It agrees
with Buchdahl's scheme on all twenty tertiary coefficients to 2E-13 at both conjugates. It does not
trace a mirror, and declines one; there the rays are the reference.

**And a cross-check anyone can repeat.** An independent commercial implementation's Seidel
analysis and the FIFTHORD macro agree with this program — 586 coefficient values across seven designs, worst residual 1.1E-12,
and 30 of 30 third-order values at a finite conjugate. That is recorded because a reader with
such a program can confirm it without trusting anything here. It is not the authority: neither
computes the seventh-order set this program exists for, so neither could be.

**That cross-check reaches the per-surface SPLIT, not only the totals.** The recorded reference is
what FIFTHORD reported for these designs, and it carries three things per fixture: what each
surface generates ON ITS OWN, the isolated ASPHERIC contribution of each figured surface, and the
system totals. `BuchdahlCoefficientsTests.MatchesTheRecordedReference` holds all three to 1E-9
relative on every build, over seven designs. So the intrinsic and figuring parts a merit function
can now target are checked against a second implementation of the same published method, not only
against their own sum.

**That cross-check has a measured boundary**, and it is the r-squared deformation term: every one
of the seven fixtures has `PARM 1` zero, and the agreement above holds only there. See
*The r-squared term* below.

The induced part is not in the reference, because FIFTHORD does not print it. It is reached the
other way about: the macro's totals are NOT the sum of the surface rows it prints, and
`TotalsAreNotTheSumOfTheIntrinsicParts` requires that gap to exist. Differencing a FIFTHORD run by
hand recovers the induced values this program reports to about 1.5E-4, which is what six roundings
of five printed digits costs - worth knowing, and weaker than everything above it.

**One statement in that reference is about a term being ABSENT.** FIFTHORD leaves the Petzval
column blank in every aspheric block, which says the figuring contributes nothing to the Petzval
sum - the sum depends on the vertex curvature and the indices, and a figured surface has the same
vertex sphere as the sphere it was figured from. Two implementations agreeing that a term is
absent is worth more than two agreeing about a value, and for a long time this one was not checked
at all: the fixtures simply omit `Pi` there, and a comparison written to skip what a fixture does
not state read the blank as no data rather than as zero. `TheFiguringContributesNothingToPetzval`
now asserts it.

## Standing results

| what | result |
|---|---|
| Table I, t1–t155, per surface | matches the reference implementation to 7E-16 |
| the twenty tau, Buchdahl against Forbes | 20 of 20 to 2E-13, both conjugates; Forbes declines a mirror |
| seventh-order spherical aberration, two routes | identical to every printed digit |
| third order against an independent Seidel analysis, finite conjugate | 30 of 30 |
| fifth order against FIFTHORD, finite conjugate | 18 of 18 totals |
| the per-surface split | intrinsic and figuring against the FIFTHORD reference, 1E-9 over seven designs, every build; the figuring's Petzval asserted absent; and internally, intrinsic + figuring + induced = total to 1.8E-14 |
| E, E5 and tau20 against traced chief rays | each to under one per cent wherever the two routes agree, at both conjugates |
| the field surfaces against OpticStudio | Petzval radius and the sagittal, medial and tangential sags, to five decimals on `E0_infinite_flat` |
| the E-family fixtures against OpticStudio | Seidel 5 of 5 and FIFTHORD 18 of 18, per surface and total, on a design never compared before |
| immersed IMAGE space, n = 1.01 and 1.30 | third order predicts traced rays, Seidel and Buchdahl agree through the immersion to 1E-10, and the tertiary set agrees with Forbes to 2E-12 |
| immersed OBJECT space | third order right by the same checks; the seventh order agrees with Forbes and with real rays to 2.2E-15, after the defect recorded below was fixed |
| LensHH-LT against this program, r-squared | nine fixtures, every per-surface value to every digit it prints, after the defect below was fixed in 1.0.156 |
| immersed OBJECT space against FIFTHORD | 17 of 18 to every digit the macro prints, on `Ej_object_space_n101`; `E5` differs by 8E-13, which is 1.6E-12 of the largest coefficient and at the agreement floor recorded above |
| figured at a FINITE conjugate against Forbes | 1E-13 at two stop positions, three object distances and an immersed object medium, after the defect recorded below was fixed |
| the analytic derivative, every design on disk | all 61 designs, the two mirrors included, against a central difference of their own residuals, every build; and Thompson's two-mirror telescope, built in code |
| the tertiary set against a different lineage | all 61 designs: against Forbes to better than 1E-9 wherever Forbes applies - every design but the two mirrors - and against real rays wherever the conjugate allows, every build |
| Seidel against Optiland 0.6.2 | 16 designs, every surface, all five sums, to 1E-14 once its opposite sign is turned; see [optiland.md](optiland.md) |
| fifth order from Optiland's rays | all twelve on 45 designs at infinity, aspheric and a mirror included, to between 3E-9 and 1.3E-5 of the largest - the inversion's own floor on this program's rays |
| the seventh order on a mirror | finite since the scheme was given signed indices; all nineteen tau against reflected real rays to 6E-6 of the largest on the parabola at ten degrees; Forbes declines a mirror |
| the macros on a mirror | BUCH7_ASPH reproduces this program on all thirty-seven, to every printed digit, on the parabola (F4) and the spherical mirror (F10) in OpticStudio; BUCH7 does the same on F10 and declines the figured F4; RAYINV agrees in sign everywhere and to its fit's floor; FORBES declines. One reflection only - see [optiland.md](optiland.md) |
| the suite | over a thousand tests, and everything they read is in this repository |

## The aspheric arrangement, and how it was established

This section used to live under **What is not established** below, and it is worth saying why it
no longer does. Buchdahl gives the aspheric **scheme** — Secs. 65-66 for the D and L split,
(85.2)-(85.5) for the two passes — so the method was never in question and nothing about it was
guessed. What he never published is the arranged **table** for it, the way Table I arranges the
spherical case, and that arrangement had to be re-derived. It has been, and what follows is what
that rests on.

**The arrangement is established, and the exception that used to qualify this is closed too.** The
reconstruction is a separate routine, `BuchdahlAsphericScheme`, and
`TertiaryCoefficients.Attach` sends every figured system there; spheres keep Buchdahl's own
arrangement in `BuchdahlTableI`, bit for bit. Its default arrangement is four things, each
derived and each gated before it was adopted:

| piece | source | gate |
|---|---|---|
| barred q accumulations, members 1-5 | identities, M Sec. 22 | (22.42) against (22.53), 1E-13 on figured systems |
| barred q accumulation, member 6 | the dual run, paper XII Sec. 6 (swap the rays, negate the indices) | reproduces members 1-5 from the identities to 2E-13, and all six on spheres to 6E-13 |
| the figuring's D half in the hat pass | (60.3), (85.3) | in the secondaries AND in the M entries built from them |
| the D half on an exactly flat surface | its curvature limit | continuous with the R = 1e10 twin |
| a figured flat facing collimated light | the same formulas in Laurent series arithmetic, curvature as the variable | reproduces the double route on regular surfaces to 1E-10; Forbes on the flat to 1.2E-9 |

**Corroborated a second time, outside this program.** The same arrangement is transcribed into
`macros/BUCH7_ASPH.ZPL`, which runs inside OpticStudio and shares nothing with this code but the
published equations. On a lens of spheres it reproduces `BUCH7.ZPL` entry for entry — 155 Table I
entries over 9 surfaces, 1170 numbers, and all twenty tau. On figured designs it reproduces
`FORBES.ZPL` on all twenty tau, and the recorded FIFTHORD reference on all eighteen third- and
fifth-order totals, on a conic singlet, a conic carrying r⁴, r⁶ and r⁸ together, and a triplet
with two figured surfaces where one induces on the other. A transcription agreeing to the
printed digits is not proof of the arrangement, but it does exclude a whole class of
implementation error in this code, since a shared bug would have to have been made twice in two
languages.

Against Forbes, all twenty tau: between 2E-13 and 2E-10 relative on every figured design in the
ladder and on the three aspheric triplets (`BuchdahlAsphericSchemeTests.TheAsphericRoutineAgreesWithForbes`),
where the arrangement as it stood before was out by 17 to 467 per cent, with 6 to 19 of the twenty
beyond one per cent. Against real rays both routes now sit at the rays' own floor
(`ForbesCoefficientsTests.BothRoutesAgreeWithRealRaysOnFiguredDesigns`), and `tau20` comes back
from the traced chief rays to within the recovery's scatter.

**A figured flat facing collimated light** (`Ladder2_FlatFigured`, a corrector plate in a parallel
beam) needed one more step. There the marginal incidence is identically zero, q is infinite, and
the finite coefficients arrive only after terms carrying different powers of q cancel - the
arrangement was right (bend the surface to R = 100 and it agrees with Forbes to 1.6E-12) but
the flat branch dropped the figured tertiary and a numerical limit reached only 4.5E-4. So
`TertiaryCoefficients.Attach` runs such a system through `AberrationCalculator.Core.Series`, the
same source files compiled in Laurent series arithmetic with that surface's curvature as the
variable, and reads the answer at e^0. It is used only when it vouches for itself - two
truncations agreeing, nothing below the lowest carried order, negative orders cancelled - and
the design now agrees with Forbes to 1.2E-9, where it was 710 per cent out
(`FlatCollimatedSeriesTests`). That was the one exception, and it is closed: there is no figured
REFRACTING case left that this program declines to compute or computes differently from Forbes. On
a figured mirror Forbes declines, and the reflected rays are the reference (see *Mirrors*).

**And it is differentiated too, which it was not at first.** The series route lived in Core alone,
so the OPTIMISER refused such a design: in the differentiating build the call had no body and
compiled away, leaving a right value beside a silently wrong derivative. It now runs in
`AberrationCalculator.Core.Series.Ad`, the same sources again with `Scalar` a dual number whose
value and derivative are each a Laurent series - so value and derivative come out of one run, and
reading them off independently is legitimate because extracting the e^0 coefficient is linear.
Checked two ways on `Ladder2_FlatFigured`: the differentiated route reproduces the double route on
all thirty-seven coefficients, and the derivatives of tau2 to tau20 match central differences with
respect to a curvature, a thickness and the corrector's own r^4 term
(`FlatCollimatedDerivativeTests`). The first of those is the one that matters, because the second
compares a derivative against differences of its OWN route and would pass even if that route had
drifted away from the value the analysis reports.
**The rest of this section records how the defect looked while it was open**, and is kept rather
than deleted because how an error was found is worth more than the fact that it was. Everything
in it is in the past tense as a matter of fact, whatever tense it is written in.

Two cautions for anyone measuring this, both of which cost time here. Normalising the error by
the largest coefficient in the set hides it almost entirely — a small coefficient wrong by five
times is nothing beside the largest, and the design that does exactly that reports as 1.7 per
cent. And a predicted spot cannot see it at all; see below.

**A second ray route says the same thing about `tau20`.** The inversion above (evidence 3) is
the general one. This is a narrower instrument over the same rays: at zero pupil radius the
transverse polynomial has three terms separated by their power of the field alone, so `E`,
`E5` and `tau20` fall out by differencing, with no basis, no least-squares solve and no model
of the other seventeen coefficients. The two agree on `tau20` to between 0.03 and 1.2 per cent
across the figured fixtures. Wherever the two routes agree the
rays agree with both, four figured designs included; wherever they disagree by more than the
recovery's own error bar the rays land on Forbes, six designs, no exceptions, over gaps from
11 per cent to a factor of 3.8. One further design has a 3.2 per cent gap against a 5.1 per
cent error bar and settles nothing; it is reported as no verdict. `E` and `E5`
come back exactly throughout, which is what confines the reading to the seventh-order aspheric
arrangement. Which route is the wrong one is something a disagreement between the two could
not establish.

**On a purely spherical system the two never disagree, and this was checked rather than
assumed.** Across the seven all-spherical fixtures and the finite-conjugate triplet, the worst
departure over all twenty tau is 3E-15 of the largest of them — roundoff — on seven of the
eight, and 2E-08 on the near-degenerate flat fixture. The rays return `tau20` on all eight. So nothing above touches the part of the scheme Buchdahl actually published: what
it convicts is this repository's reconstruction of the aspheric arrangement he did not, and it
convicts it in one coefficient of the twenty. See `distortion-prediction.md`.

**A predicted spot cannot settle it, and this is measured rather than assumed.** On that same
design the two routes agree on the spot to one part in ten thousand while disagreeing on the
coefficients as above, because the disagreement sits in the smallest terms and a spot barely
weights them. Predicted-versus-traced spot agreement would have certified a tau15 that is
wrong by five times and points the wrong way. It is not used as a correctness metric here,
and `spot-prediction.md` says what it is used for instead.

## The r-squared term, and where the FIFTHORD cross-check stops

An even asphere's first coefficient, `PARM 1` in OpticStudio, multiplies r-squared. **It is not
figuring.** A surface of curvature `c` carrying `A2` is exactly the sphere of curvature `c + 2 A2`
carrying whatever is left over, so the term changes the surface's POWER and with it the focal
length of the system. Everything from r^4 upward is genuine departure from a sphere; r^2 is a
radius in disguise.

That distinction is the whole of this section, because programs differ on whether they notice it.

### What was measured

`tests/fixtures/coefficient-reference/F8_r2_conic_a4_a6_a8` exists for this. It is
`F3_conic_a4_a6_a8` with `PARM 1 = 1.0E-04` added and nothing else changed. The term is not a
perturbation: it moves the effective focal length from 78.037505 to 77.419426, the F/number from
3.9019 to 3.8710, `B` by three per cent and `B7` by 0.43.

| | notices `PARM 1` | EFL it works at |
|---|---|---|
| OpticStudio's own first-order data | yes | 77.419426 |
| this program | yes | 77.419426 |
| `macros/BUCH7_ASPH.ZPL` | yes | 77.419426 |
| FIFTHORD | **partly** | mixes both |

**This program is correct here, and it is not this program's own opinion of itself.** Four things
that share no arithmetic agree on that design: the Buchdahl aspheric scheme, which folds the term
into the vertex curvature and re-measures the figuring from that sphere; the Forbes series trace,
which does no folding at all and simply carries `A2` as the first coefficient of the sag series;
the ray inversion, which recovers the coefficients from real traced rays and knows nothing of
either; and OpticStudio's own focal length. The first three agree on the twenty tau to nine
significant figures, with the rays at their own ladder floor of 3E-06.

**BUCH7_ASPH agrees with this program to every printed digit on that file**, all eighteen totals
and all twenty tau. Its EFL comes from `GETSYSTEMDATA`, so that agreement also establishes the
thing the fixture was built to test: OpticStudio's paraxial data accounts for `PARM 1`, and the
macro's folded vertex curvature is therefore consistent with the pupil and focal length it reads
back.

### Why FIFTHORD is not accurate when A2 is non-zero

It was expected to return the answer for the lens with the term removed. **It does not.** Its `B`
comes to 2.5314E-02, against 2.7696E-02 with the term and 2.6888E-02 without it - neither. The
reason is that it takes its paraxial ray data from OpticStudio, which INCLUDES the r-squared
power, and then computes each surface's contribution from the BASE curvature. The two halves
describe different surfaces.

Two measurements locate that rather than infer it. On surface 2 of the fixture, which carries no
figuring, FIFTHORD and this program agree on every third- and fifth-order term to all five printed
digits - so nothing general is wrong with either. On surface 1, which carries the term, the
Petzval contribution differs by exactly the ratio of the two curvatures:

    FIFTHORD    -5.2159E-03  x  (0.0202 / 0.0200)  =  -5.268059E-03
    this program                                     -5.268041E-03

to 3.4E-06, which is the limit of FIFTHORD's five printed digits. Petzval depends on the surface
curvature and the indices alone, so it isolates which curvature each program used and nothing
else. The totals then differ by -8.6 per cent on `B`, -4.4 on `N1` and -1.65 on `B7`, while `E`,
`E5`, `N2` and `M2` are unmoved - the signature of a curvature error rather than of a dropped
term.

### The author flags it, and the flag understates it

The macro's header says:

> "Zemax uses a second-order aspheric deformation coefficient which is not used in this treatment.
> It may appear in a future version."

That is candid and it was written in 1998, and none of this is a criticism of a macro given away
freely. But it describes an omission, and a reader would reasonably take it to mean the result is
the one for the surface without that term - an incomplete answer, and a defensible thing to hand
back. What the macro actually returns is an inconsistent one: a lens whose rays come from one
surface and whose contributions come from another. **A note strong enough for what happens would
have to say that the coefficients are not to be used at all when `PARM 1` is non-zero**, rather
than that the term is not used.

### What this does and does not disturb

**It does not touch the 586-value agreement.** Those seven fixtures have `PARM 1` zero, which the
README beside them records as deliberate, and the agreement there stands at a worst residual of
1.1E-12. This section says where that agreement stops applying, which is a boundary on a
cross-check and not a defect in either program.

**This section said "nothing in this repository needs fixing", and that was wrong.** It was
written on the strength of the Buchdahl route and the two macros, which do carry the term
correctly and which `AsphericR2TermTests` pins. The Seidel route had not been checked, because
nothing checked it, and it had been wrong since the file was written. See *The r-squared bug in
the Seidel route* below. The claim should have been that three things were checked and a fourth
had not been looked at.

**And the macro pair has now been run against each other on `F8`.** `FORBES.ZPL` reproduces
`BUCH7_ASPH.ZPL` on that file to every printed digit - all eighteen totals and all twenty tau,
`tau1 = 2.515701E-04` through `tau20 = -4.928042E-09` - and both agree with this program. The two
macros share no arithmetic and, on this term specifically, take opposite approaches: FORBES puts
`A2` straight into the sag series as the coefficient of `p` and folds nothing, while BUCH7_ASPH
folds it into the vertex curvature and re-measures the figuring from that sphere. Agreement
between those two is worth more than agreement between two implementations of the same treatment.

So on the r-squared term, four things carried it correctly - two macros, the C# Buchdahl route and
real rays - and the C# Seidel route did not, which is the subject of the next section.

## The r-squared bug in the Seidel route

**`SeidelCoefficients` computed the aspheric third order wrongly whenever a surface carried an
r-squared term, from the day the file was written until 19 September 2026.**

The aspheric Seidel term is the surface's departure from its VERTEX SPHERE at `r^4`, because the
vertex sphere is what the paraxial trace has already accounted for. Writing `cb` for the base
curvature and `c` for the vertex curvature `cb + 2 A2`:

    wanted:     (1+k) cb^3 / 8  +  A4  -  c^3 / 8
    computed:        k  c^3 / 8  +  A4

The `(cb^3 - c^3)/8` piece was absent. **When `A2 = 0` the two expressions are equal**, which is
why the error was invisible: the paraxial data was right, Petzval was right, the focal length was
right, and only the aspheric contribution to `S1`, `S2`, `S3` and `S5` was wrong.

### Why nothing caught it

Every fixture in this repository has `A2 = 0`, and the coefficient-reference README records that
as deliberate. `AsphericR2TermTests` makes exactly the right check and makes it on the BUCHDAHL
route; no equivalent existed for the Seidel route. There is even a shared helper that does the
conversion correctly - `Surface.VertexForm()`, used by `BuchdahlCoefficients` and twice by
`BuchdahlTableI` - and `SeidelCoefficients` did not call it. The defect was not a misunderstanding
of the optics; it was one file keeping its own copy of an expression the others got from a helper.

### What caught it, which is the part worth reusing

**Writing one surface two ways and requiring one answer.** A surface of base curvature `cb`
carrying `A2 = d` is exactly the surface of curvature `cb + 2d` carrying
`A4 += (cb^3 - (cb+2d)^3)/8`; the two sag series agree term for term and part company only at
`r^6`, which cannot reach a Seidel sum. On `KingslakeDG` with `d = 1e-4` on surface 1 the two
descriptions gave `S1 = -0.000572` and `S1 = -0.001412`.

That is a self-contradiction inside one program, established without reference to anything
external. What outside agreement settled was only WHICH of the two answers was right, and two
independent things said the same: this program's own Buchdahl route, which gives one answer for
both descriptions, and an independent commercial implementation's Seidel analysis, run on both
descriptions and giving `-0.001412` for each.

### The same term was wrong in a third program, in a different way - and is now fixed

**FIXED IN LensHH-LT 1.0.156, and verified here.** The defect above is 1.0.155 and earlier. The
candidate was checked on 21 September 2026 against nine fixtures in
`tests/fixtures/coefficient-reference`, and passes every one:

| the pair | what it asks | result |
|---|---|---|
| `F3` against `F8` | is the term read at all, at an infinite conjugate? | the focal length moves, 78.0375 to 77.4194 |
| `F8` against `F9` | is the `r^4` departure measured from the right sphere? | identical, per surface and in total |
| `G0` against `G1` | is it read at a FINITE conjugate, where `iota` is live? | the focal length moves, 78.0375 to 77.4194 |
| `G1` against `G2` | the right sphere there? | identical |
| `G0` against `H1` | does it reach a surface OTHER than the first? | yes, and surface 1's own contribution moves with it |
| `G0` against `H2` | one on each surface, of opposite sign? | yes |
| `H2` against `H3` | the right spheres, with two conversions at once? | identical |

The `F9`, `G` and `H` fixtures were built for this and kept, because the combinations they cover -
r-squared at a finite conjugate, on a surface that is not the first, on two surfaces at once - were
in no fixture here either.

**And it is not merely self-consistent.** Every per-surface Seidel value matches this program to
every digit LensHH-LT prints, across all nine designs. Per-surface is the stronger statement:
totals can agree through cancellation and individual surfaces cannot. `H1` is the one worth
singling out - putting the term on the SECOND surface moved the FIRST surface's contribution too,
which is what a curvature change downstream must do to the rays arriving there, and is a sign the
term reaches the paraxial data rather than being patched into the sums.

**What this does not cover**, and the author knows it: the real ray trace and the optimiser were
not exercised. If the term was being dropped at import those came along with the fix; if it was
repaired in the Seidel path alone they are untested.

**Running the same one-surface-two-ways check outward found a bug in LensHH-LT's Seidel
analysis.** That program did account for aspheric figuring in the third order - putting `A4` on a
surface moves that surface's `S1`, `S2`, `S3` and `S5`, which is more than several programs do -
but an r-squared coefficient reaches nothing at all. With `A2 = 1E-04` on surface 1 of the same
lens the reported radius is unchanged, the focal length is still 100, and the Seidel sums do not
budge.

That is a **different** fault from the one this repository had, and the difference is the
diagnosis. Here the paraxial data was correct - the r-squared term was folded into the vertex
curvature, so the focal length and Petzval both moved - and only the `r^4` departure measured from
that sphere was wrong. There the term is dropped before the paraxial data is formed, so nothing
downstream of it can be right either.

The fault belonged to LensHH-LT and was fixed by that program's author. It is recorded here for
three reasons: this repository's cross-check is what found it, it is why LensHH-LT could not be
the second opinion on the question above, and the fixtures built to accept the fix are now part
of this repository and cover combinations nothing here covered before.

### The first fix was also wrong

Grouping the correction as `(1+k) cb^3/8 - c^3/8` is the same algebra and NOT the same arithmetic:
`1 + k` rounds, so a conic of -0.6 moved in its last bits, and several tests in this suite demand
bit-identity of exactly those numbers. Regrouped as `k cb^3/8 + A4 + (cb^3 - c^3)/8`, the conic
keeps its untouched term and the correction is a difference of two cubes of the same double -
identically zero when `A2` is zero, so nothing previously reported moved. `SeidelR2TermTests`
holds both halves: five surface descriptions checked both ways, a guard that the term is not
silently dropped, and the bit-exactness of the correction.

### What it reached, and what it did not

**Reached:** the aspheric contribution to `S1`, `S2`, `S3`, `S5` on a surface with `A2 != 0`, and
through them `AspherePlacement`, which consumes `SeidelResult` and so gave wrong sensitivities and
wrong placement advice on such a design.

**Did not reach:** the Buchdahl route at any order, `BUCH7.ZPL` - which declines any lens carrying
an r-squared term outright - `BUCH7_ASPH.ZPL`, which does the full vertex conversion at `r^4`,
`r^6` and `r^8`, the Forbes trace, which carries `A2` in the sag series where no conversion is
needed, and real ray tracing, which uses the true sag. Petzval was never affected, since it takes
the vertex curvature directly.

**And the refutation is now direct rather than inferred.** Both macros were also run on
`F3_conic_a4_a6_a8`, the same lens without the term, and they reproduce this program's numbers for
it exactly - `B = 2.688792E-02`, `tau1 = 2.526552E-04`, `tau20 = -4.894869E-09`. So the A2-free
answer is in hand and measured, and FIFTHORD's 2.5314E-02 on `F8` is not it. It was expected to
return that answer; it returns a third thing.

**Both macros now refuse the FIFTHORD comparison when they meet a non-zero `PARM 1`**, in place of
the invitation they used to print unconditionally - an invitation that would have sent a reader
hunting a fault in the wrong program. The guard was exercised both ways on the pair above: the
refusal on `F8`, the original text on `F3`.


## The two ends of the system: the end surfaces and the end media

**Everything in this repository has been checked on lenses whose object and image surfaces are
plane and whose object and image spaces are air.** Every fixture was like that until the E family
below was built for this question. That is the same
shape of blind spot the r-squared bug lived in - a case no test happened to contain - so ten of
them were asked about deliberately: the image surface curved, curved with a conic, curved with
`A4` and `A6`, curved with `A2`; the same four on the object surface at finite conjugate; and
each end medium at `n = 1.01` instead of 1. `CurvedObjectAndImageSurfaceTests` and
`ImmersedSpaceTests` hold the answers.

Two different answers came back.

### The end SURFACES: their shape reaches nothing, and that is not always harmless

**Curvature, conic, `A2`, `A4` and `A6` on the object surface or the image surface change no
number this program produces.** Not the paraxial trace, not a Seidel sum, not a Buchdahl total at
any order, not the Forbes series, not a traced ray. Bit for bit, across all four shapes on both
end surfaces, against a fingerprint of the entire result - both rays at every surface, every
per-surface array, every coefficient found by reflection so that one added later is covered
without editing the test.

The reason is structural rather than an oversight in any one file: every route loops from surface
1 to `LastOpticalSurface()`, which is `Count - 2`, and the real trace finishes with a flat
transfer to a z target rather than an intersection with the image surface. The end surfaces are
outside the loop by construction. A file can carry a curved detector, load without complaint, and
be analysed as though the detector were flat.

**Whether that is wrong depends on which end, and the image end was settled in OpticStudio.**

For the IMAGE surface, **ignoring the shape in the coefficients is the convention, and that was
measured rather than argued.** Three fixtures were run in OpticStudio on 20 September 2026 -
`E0_infinite_flat`, `Ea_image_curved` (R = -50) and `Ed_image_flat_a2` (flat, `A2 = -0.01`, which
is the same surface written the other way). **Its Seidel table and its FIFTHORD output are
identical across all three, to every printed digit**, and its Seidel listing carries an `IMA` row
of zeros. The third-order sums are a property of the LENS, referred to the paraxial image point.
The detector is what they are telling you to choose, not an input to them.

That run also cross-checked this program twice over. OpticStudio's Seidel sums match ours to every
printed digit, and FIFTHORD matches our Buchdahl coefficients per surface and in total - 18 of 18
on a design that had never been compared before.

**What the coefficients say about a curved detector, they say the other way round.** Dividing `S3`
and `S4` by `2 n' u'^2` turns them into the longitudinal distances from the paraxial plane to the
sagittal and tangential foci, which is the form that answers a detector question. On `E0` those
are 0.2930 and 0.6337 mm at full field with a Petzval radius of -80, and OpticStudio prints
0.293001, 0.633715 and -79.9998 for the same lens. This program now prints them too - see *Field
surfaces* in the report - along with the radius a detector would need in order to sit on the
medial surface, and, when the file's image surface is curved, the residual between the two. That
residual is the only place in this program that reads the image surface's shape at all.

**What IS wrong is the ray trace.** A spot, an RMS radius or a fan on a curved detector has to be
measured on that detector, and both OpticStudio and LensHH-LT intersect it; this program transfers
to a plane. Measured on an f/12 singlet at 5 degrees with a detector curved to `R = -35`:
**17.24 um RMS reported, 9.68 um on the detector the file describes.** Same rays, same trace; the
only difference is the surface they are caught on. The fix is confined to the last step of
`RealRayTrace`, which ends with a flat transfer to a z target rather than an intersection.

For the OBJECT surface there is no such defence, and it is not yet settled. A curved object puts
each field point at its own conjugate distance, which changes the aberrations rather than where
they are measured. An object surface of `R = 25` at 10 mm off axis stands 2 mm out of its own
vertex plane - 1% of a 200 mm conjugate - and the worst ray lands **47 um** from where this
program puts it. Fixtures `Ee` through `Eh` exist to put that question to OpticStudio.

Both numbers are measured in the tests, from the trace's own direction cosines, so the size of
what is missing is recorded rather than described. Both tests are expected to FAIL the day end
surfaces are implemented; each says so, and says to replace it with one requiring agreement
rather than to retune it.

**What to do about it is now two different things, not one.** The image-surface INTERSECTION in
the real trace is a plain defect with a contained fix - the last step of `RealRayTrace` should
meet the image surface the way every other surface is met, through the same `Intersect`, when the
caller asks for the file's image plane rather than for the paraxial focus, which is a plane by
definition. The object surface is the larger question, because a per-field conjugate reaches the
coefficients as well as the rays; until it is settled, a design carrying one should be refused on
sight rather than answered about as though the object were flat.

### The end MEDIA: immersion is carried, after a defect in the seventh order was found and fixed

`n = 1.01` at either end was asked for. `n = 1.30` was tested alongside it, because a formula
missing a factor of `n'` is out by 1% at 1.01 - which could be a tolerance - and by 30% at 1.30,
which cannot be anything else.


**The seventh order did not survive an immersed OBJECT space, and now does.** Buchdahl's
tertiary table parted company with Forbes' series trace by 0.24% at `n = 1.01` and by **23% at
`n = 1.30`**, worst at `tau2`, at both conjugates, while agreeing to 2E-15 at `n = 1`. The cause
and the fix are below; the table now agrees at every index to 2.2E-15.

**Which of them was wrong was settled, not assumed.** On an infinite-conjugate system - contrived so
that the ray inversion, which refuses finite conjugates, can reach the failing case - Forbes and
real traced rays agree with each other to better than 1E-5 and both disagree with the table by the
same 23%. Two independent witnesses against one implementation, which is what made it an
accusation rather than a difference. All three now agree.

**It is not the focal length convention**, which was the first suspect: `Efl` is the length scale
the whole Buchdahl chain is normalised to and it carries the OBJECT index, `n_object/phi`.
Substituting `1/phi` or `n'/phi` for it changes the tertiary coefficients by not one bit at either
index - the normalisation cancels. The cause is below.

**What was never affected**, and each of these was measured rather than reasoned:

- **The third order, at either end.** `S1 rho^3 / (2 n' u')` predicts a traced ray as the pupil
  shrinks at 1.00, 1.01 and 1.30 on both sides, with the residual falling by four per halving,
  which identifies it as truncation rather than a wrong coefficient.
- **Everything with IMAGE space immersed.** The tertiary set agrees with Forbes to 2E-12 at
  `n' = 1.30`, and best focus predicted from the coefficients lands where the rays do.
- **Object-space immersion at `n = 1`**, which is every design in this repository except the
  immersed E fixtures built to test it. Nothing that has ever been reported here is affected.

**The fifth order is now checked under immersion on axis, and not off it.** Fitting the traced
axial aberration as `B rho^3 + B5 rho^5 + B7 rho^7` with the ninth order carried and discarded
puts `B` within 1E-6 of the rays, `B5` within 1.3E-4 and `B7` within 0.7 per cent - and, the
sharper statement, puts them there by the SAME margin in air as at `n = 1.30`, which a wrong
power of `n` could not do. That reaches the SPHERICAL part of the fifth and seventh orders only;
the field-dependent fifth-order coefficients still have no independent reference that works at a
finite conjugate.

### The cause, found

**Buchdahl's computing scheme requires its p and q rays to carry a Lagrange invariant of ONE, and
that holds only when the object medium is air.**

The pair starts, at surface 1, as

    y_p = 1,  v_p = iota        y_q = P/g,  v_q = 1/g,      g = 1 - P iota

and its invariant is `N_0 (v_q y_p - v_p y_q)`, which works out to **exactly `N_0`** - one when
object space is air and not one otherwise. The scheme's coefficient formulas are not homogeneous
in the q ray's scale: `a_p` divides by that ray combination while the field terms multiply by
powers of it, so a pair whose invariant is not one cannot be absorbed anywhere and comes out as a
different error in every coefficient. That is why the damage looked structureless - some tau out
by exactly `1/N_0`, some by more, two of them changing SIGN at `N_0 = 1.30`.

The code's own comment names the convention it then does not use: *"M (13.4), reduced
OT-coordinates"*. In reduced coordinates the angle is `N u`, so `v_q = 1` means a PLAIN angle of
`1/N_0`; the code sets the plain angle to 1. At `N_0 = 1` the two are the same line of arithmetic,
which is why Buchdahl's own printed triplet - in air, like every other published example and every
fixture here - validates it perfectly.

**The fix is two scalings, each a no-op when `N_0 = 1`:**

- the q ray's starting height and angle, divided by `N_0`, which makes the invariant one;
- the field normalisation `hmax`, multiplied by `N_0`, because the physical chief ray's plain
  angle is now `N_0` times the rescaled q ray's.

**Measured, with both applied by hand:** the tertiary set matches Forbes' series trace to
**1E-14** at `N_0 = 1.01` and `N_0 = 1.30`, against 1.2% and 37% before - on a single refracting
surface, on a singlet at infinite conjugate, and on a finite-conjugate system with the stop away
from the first surface so that `P` and `g` are both non-trivial. At `N_0 = 1` the change is
division by exactly 1.0 and nothing moves.

**It was NOT the focal length convention**, which was the first suspect and was eliminated first:
substituting `1/phi` or `n'/phi` for `Efl` changes the tertiary coefficients by not one bit.

**Applied.** Every route - the aspheric arrangement, the dual run, the increments, the Laurent
route for a figured flat - starts its pair through `BuchdahlTableI`, so the reduction goes in at
ONE ray start rather than four, and the dual-number arithmetic gets it by linking the same file.
The field factor goes in at the three places `hmax` is formed. The q ray is scaled by the
ABSOLUTE object index, because the dual run negates every index and what is wanted is the
medium, not that run's sign.

**The two macros carry the same fix, and it is measured.** `BUCH7.ZPL` and `BUCH7_ASPH.ZPL`
transcribe this scheme and had the same ray start, the same `hmax` and therefore the same defect;
both now divide the q ray by `n0abs` and multiply the field variable by it.

Run on `Ej_object_space_n101.zmx`, whose object medium is 1.01, `BUCH7` agrees with this program
on **all thirty-eight** coefficients - the five third-order, the twelve fifth-order, `B7` and the
twenty tau - to every digit this program prints. Its `t5` on surface one prints 9.900989774E-01,
which is 1/1.01, so the reduction is visibly there in the listing. And the regression is measured
too: on `KingslakeDG`, in air, the whole of Table I is IDENTICAL to
`macros/reference/KingslakeDG_TableI.txt`, which was recorded before any of this, and the
eighteen totals still match this program.

**Nothing in air moved.** The scaling is a division by exactly 1.0 there, and the whole suite -
including Buchdahl's own printed table, the 586-value FIFTHORD reference and every bit-identity
test in it - passes unchanged. On the immersed cases the tertiary set now agrees with Forbes to
2.2E-15 at both indices and with real traced rays to 2.8E-15, where it had been out by 0.24% and
23%.

Four statements that do hold, each one that must hold of any correct implementation rather than a
comparison against another program:

- **A coefficient predicts a traced ray.** On axis the transverse aberration is
  `S1 rho^3 / (2 n' u')`. Traced against predicted, the ratio goes to one as the pupil shrinks -
  at 1.00, at 1.01 and at 1.30, on both the object side and the image side - and the residual
  falls by four when the pupil is halved, which identifies it as the fifth order rather than a
  wrong coefficient. A dropped `n'` would settle the ratio on `n'` or `1/n'` instead.
- **The two coefficient routes carry the immersion identically.** `B = S1 / (2 n' u')` holds to
  1E-10 relative at every index, on both conjugates. Seidel is a wave sum and Buchdahl is already
  transverse; that conversion is exactly the factor immersion changes.
- **The Lagrange invariant scales with the object index exactly** - the object-space rays are
  fixed by the pupil and the field, not by the medium - and is still conserved to 1E-13 through
  the system.
- **The F/number is the one the numerical aperture implies**, `1/(2 n' u')`, not the geometric
  cone alone. Every Buchdahl total is normalised to it, so a missing `n'` there would rescale all
  of them silently.
- **Best focus predicted from the coefficients is where the traced rays actually focus**, within
  10%, at all three indices. That is the whole chain at once - trace, totals, defocus coupling,
  rays - and at 1.30 a missing index anywhere in it would be a 30% error against a 10% tolerance.

**One convention is now measured, and it is a genuine disagreement.** The focal length reported
here is `n_object/phi`, which is the FRONT focal length. OpticStudio prints `1/phi`, the
air-equivalent: on `Ej_object_space_n101.zmx` it prints 51.052799 where this program prints
51.5635, and the ratio is exactly the object index. The two agree on every other design in this
repository, and on every design anywhere whose object space is air, which is why this went
unnoticed.

Neither number is wrong, but only one of them is what a reader will expect. Changing ours is not
a one-line edit: `CoefficientInversion` takes the paraxial image height as `Efl tan(theta)`, which
is correct only for `n_object/phi`, and `ParaxialResult.FNumber` is `Efl/Epd` and would move with
it. The COEFFICIENTS are indifferent - substituting `1/phi` changes them by not one bit - so this
is a reporting decision and not a correctness one.


## The stop parameter the aspheric increments were measured from

**A figured system at a finite conjugate disagreed with Forbes' series trace by about 5E-5, where
a system of spheres agrees to 5E-15.** Found 20 September 2026, immediately after the immersion
defect and by the same check, and at first mistaken for part of it - it is not, the residual being
the same size in air as at `n = 1.30`.

`TertiaryCoefficients.Attach` builds an all-spherical run of Table I for the aspheric increments
to be differenced against, and built it at `scheme.P` - the stop parameter the scheme DERIVES
from the q/p ray-height ratio at the stop. The run those increments are then fed back into uses
the paraxial entrance pupil position instead. The file's own comment says why, and says it a dozen
lines further down:

> The scheme derives p instead as the q/p ray-height ratio at the stop, which holds only while the
> two conventions for the q ray differ by p times the p ray - an identity that fails once iota is
> non-zero.

So the reference table and the run measured against it used different pupils whenever `iota` was
not zero. The increments are a difference of two tables, and differencing two tables built for
different stops leaves the difference between the stops in the answer.

### Why nothing saw it

Three ways, and all three had to hold at once:

- **On a system of spheres** `AsphericSchemeIncrements.Build` returns null and that table is never
  used at all. Every finite-conjugate check here is on spheres.
- **At an infinite conjugate** the two stop parameters are not merely equal but the same
  expression - `infinite ? scheme.P : ...` - so the bug is unreachable. Every figured check here
  has the object at infinity.
- **With the stop on the first surface** the stop parameter is zero and the two candidates
  coincide. That is the commonest arrangement in a small test design.

`FiguredFiniteConjugateTests` now covers the combination at two stop positions, three object
distances and one immersed object medium, against Forbes, at 1E-13 - and keeps the three
neighbouring cases that were always right, because a fix that quietly moved one of those would be
a worse bargain than the defect.

**Nothing else moved.** At an infinite conjugate the change substitutes one expression for an
identical one, and the whole suite passes unchanged.


### Where else the same mismatch was, and where it was not

The first fix reached `Attach` only. There are two more copies of that orchestration - the Laurent
route for a figured flat facing collimated light, and its dual-number twin, which the optimiser
differentiates through - and both had the same two lines in the same wrong order. Both are fixed.
Their own header says why they are not shared with `Attach`: about thirty lines of orchestration
are repeated on purpose, and the guard against them drifting is a test that requires the same
VALUE from both. That guard does not catch a defect the two copies SHARE, which is what this was.

Two uses of the derived `scheme.P` remain, and both were measured rather than argued:

- **The ninth order.** `QuaternarySpherical` builds its own Table I at `scheme.P`. The stop
  parameter enters the scheme only through `t4` and `t5`, the q ray, and spherical aberration is
  a p-ray quantity, so it should not matter. It does not: on a finite-conjugate design where the
  two stop parameters genuinely differ, 0.2342 against 0.2490, the ninth-order total moves by
  3.6E-15 on a value of 11.47 - three parts in 1E16, which is rounding.
- **The flat-in-collimated-space detector.** It tests `t1`, `t2` and `t3`, all p-ray, with the
  invariant used only as a non-degeneracy guard. The q ray's scale cannot reach it.

`Nat/WaveFront.cs` never had the defect. It computes the stop parameter first and passes it to the
spherical table, with a comment saying it does this "exactly as the tertiary route does it" - which
the tertiary route had stopped doing.

### The NAT route, checked for both defects and clean on both

`Nat/WaveFront.cs` consumes the same Table I, so it was asked the same two questions.

**The stop parameter: it never had the defect.** It forms the stop parameter first and passes it
to the all-spherical table AND to the figured run, with a comment saying it does this "exactly as
the tertiary route does it" - which the tertiary route had stopped doing. A second part of the
codebase already doing it the right way is independent evidence that the fix is right and not
merely self-consistent.

**The immersion: it is immune by construction, and that was measured.** The fix rescales the
scheme's q ray by `1/N_0`, and the tertiary route compensates in its field normalisation.
`WaveFront` converts nothing - it reads the rows straight - so the rescaling reaches its W
coefficients uncompensated, and at `N_0 = 1.30` they genuinely are different numbers from before.

That is harmless because `NormalisationBridge` FITS the scale between the W route and the Seidel
route from the third order rather than assuming it, so an overall rescaling of the field variable
is absorbed into the fitted `F`. And the fit checks itself: four coefficients, two unknowns, two
free. Across twelve combinations - infinite and finite conjugate, spherical and figured, `N_0` at
1.00, 1.01 and 1.30 - the bridge residual is at machine precision and the two free coefficients
read back to 1E-16. A route that ASSUMED the normalisation would have failed the immersed cases
the day the q ray was rescaled.

Two other consumers of Table I were checked and need nothing: `QuaternarySpherical`, whose
ninth-order spherical is stop-parameter invariant to 3E-16, and the flat-in-collimated-space
detector, which reads p-ray quantities only.
### The macros did not have this one, and the reason is worth keeping

`BUCH7_ASPH.ZPL` needs no change. It has exactly ONE stop parameter - `stopp = p0 = epp/efl`, the
paraxial entrance pupil position in focal lengths - and uses it for all four of stage C's passes.
The derived q/p-height-ratio value that the C# picked up at the wrong call site does not exist
there at all, because ZPL reads the entrance pupil position directly from the program. `BUCH7` is
spherical-only, so it has no increments and could not have the defect either.

**So the macro was right and this program was wrong**, on figured designs at a finite conjugate,
for as long as both have existed. The cross-check that would have caught it is the one this
repository already documents and relies on - the two implementations against each other - and it
had only ever been run on the combinations where they agree: figured designs at infinite
conjugate, spherical designs at finite ones.

That is a second lesson beside the usual one. It is not only that a case was missing from the
fixtures; it is that two independent implementations were being compared only where they were
already known to agree, which is the comparison that cannot fail and therefore cannot inform.


## Every design on disk is differentiated

**`DerivativeSweepTests` takes the analytic Jacobian against a central difference of the same
residuals, on all 61 designs in the two fixture folders**, the parabolic and spherical mirrors
included - and `TheTwoMirrorTelescopeIsDifferentiatedThroughBothMirrors` adds Thompson's
telescope, which is built in code rather than read from a file. It replaces nothing: the four
hand-written cases in `AnalyticDerivativeTests` keep their much wider operand set, which names
surfaces only their own design has. What the sweep adds is breadth - and the designs it reaches
are the ones no one chose, including the whole E family, whose purpose is to sit in the awkward
cases.

**The reference is the value code itself**, evaluated on the spot. Change an aberration formula
and the expectation moves with it; there is nothing to update and nothing that can go stale. It
is also a DIFFERENT LINEAGE from the analytic derivative, which is the property that matters:
every comparison between two copies of the same arithmetic is blind to a defect they share, and
this repository watched that happen twice in one day - the two macros against the C#, and the two
copies of the flat-collimated orchestration against each other.

**A design the optimiser declines is reported, not skipped.** `EveryDesignOnDiskWasActuallyDifferentiated`
requires ALL of them to be checked and prints the reasons for any that were not. A theory that
skips looks exactly like a theory that passes, and this file has been caught by that before.

### Two things the sweep found immediately

**The step size was the fragile part, not the derivative.** One step is chosen per COLUMN, from
the largest entry in it, which can leave it far too small for the other operands. On a figured
FLAT the value code switches routes within 1E-13 of zero curvature, where the ordinary chain
divides by a vanishing incidence, and a difference quotient taken across that neighbourhood
measures the switch: a correct derivative of -0.9695 read as 2.3E+14. A failing entry is now
re-measured over a LADDER of steps spanning six decades and accepted if any rung agrees; the
failure prints the whole ladder, so a reader can see whether the quotient was converging on
something else or simply thrashing.

**A derivative cannot be more accurate than the value it differentiates.** On `Ladder2_FlatPlain_NearLimit`,
a face of radius 1E10, the analytic derivative sits 0.12 per cent from the slope of the computed
value, steady across four decades of step - so it is not a step artefact, and the value curve
through that region is smooth, so it is not a seam between routes. It is the conditioning: this
repository already measures that design's coefficients at 0.067 per cent and allows half a per
cent. Those designs are named individually in the sweep and held to the standard their values
meet; every other design on disk is held to 0.02 per cent.

## Every design is cross-checked against a different lineage

**`CoefficientSweepTests` compares the twenty tertiary coefficients against Forbes' series trace
on every one of the 61 designs where it applies, and against real traced rays wherever the
conjugate allows it.** Forbes declines the two mirrors on disk, the parabola and the spherical
mirror, which are held to the reflected real rays alone; everywhere else the scheme and the series
trace agree to better than 1E-9 relative, and to 2E-13 on most. A design must be held to at least
one of the two, and the sweep fails if any is held to neither.

**Lineage is the word that matters.** Buchdahl's computing scheme accumulates a table of
per-surface quantities through an arranged recursion; Forbes' trace propagates a ray through sag
polynomials and reads coefficients off a power series in the invariants. They share no arithmetic,
so agreement between them is evidence about the OPTICS. Agreement between two implementations of
the same method is evidence about transcription - and this repository has twice watched that kind
of agreement hold while both sides were wrong.

`ForbesCoefficientsTests` already made this comparison, on seven named designs, all figured at an
infinite conjugate. That is precisely how a figured design at a FINITE conjugate went 5E-5 wrong
without anything noticing: the pairing that could have failed was never formed. The sweep forms
every pairing the disk affords.

### The ray fit is held to what it says about itself

Real rays are a third lineage and the only one that knows nothing about either series, but the
inversion is a least squares over traced landings and carries its own noise. Measured across these
fixtures, the disagreement runs between two and forty times the fit's OWN reported residual, so
the bar is a hundred times it - a level every sound design clears while a coefficient that is
actually wrong, which would be out by per cent rather than by parts in ten thousand, still fails.

A fit that did not close is not a reference at all, and such a comparison is declined rather than
made against noise. The parabolic mirror once left a residual of 0.75 here - but that was the ray
trace failing to reflect (see *Mirrors*); with reflection fixed its fit closes, and the rays are its
only second lineage.
## The model glass the patcher deleted

**Saving an optimised .zmx back to disk removed a MODEL GLASS, turning the element into air.**
Found 20 September 2026, the same day and by the same means as the section above: a new fixture
was added that sat in a place nothing had occupied before.

A .zmx can name its glass out of a catalogue - `GLAS N-BK7 ...` - or state it outright as a model,
`GLAS ___BLANK 1 0 1.6 60 ...`, whose index comes from (Nd, Vd, dPgF) rather than from a lookup.
The reader handles both, and for a model glass it deliberately leaves `Material` BLANK, because
`GlassCatalog` gives the model precedence and a name there would be ambiguous.

`LensPatcher` then read that blank as the absence of a glass:

    string material = s.Material ?? string.Empty;
    if (string.IsNullOrWhiteSpace(material)) file.Lines[i] = null;   // "the glass is gone"

and deleted the `GLAS` line. **Nothing complained.** The patched file still parsed, still had the
right number of surfaces, still carried the curvature the optimiser had just found, and described
a lens with air where the glass had been.

### Why nothing caught it

Every .zmx fixture in this repository named a catalogue glass, so there was no model glass to
lose. `PatchingAZmxKeepsItsEncodingAndChangesOnlyWhatMoved` took "the first .zmx in the fixture
folder" and would have caught it the moment such a file sorted first - which is exactly how it
surfaced, when `E0_finite_flat.zmx` was added ahead of `F1`. It failed on a line count, which is a
poor way to learn that a lens has lost an element.

### What replaced it

`PatchingAZmxLeavesEveryOtherSurfaceDescribingTheSameLens` runs over EVERY .zmx fixture rather
than the first, patches one curvature, and requires the file that comes back to describe the same
lens: the same surface count, the same curvatures, conics and thicknesses everywhere else, and -
the check that matters - the same RESOLVED REFRACTIVE INDEX after every surface. An index is what
a lost glass actually costs, and no amount of line counting says it as plainly.

The fix keeps the `___BLANK` name token exactly as the file wrote it and updates only the two
model numbers, in the fields the reader takes them from.

## The surface table that did not add up

**The report's WHICH SURFACE table said "Share sums to 100%" and summed to 91 on the double
Gauss.** Found 23 September 2026 while writing the user guide. The coefficients, PRMS and PRMSA
were never affected; the table measured less than it said it did.

The table shares out the predicted spot, which is built from all thirty-seven coefficients, using
each surface's contribution to every one of them. The per-surface contributions stopped at B7:
the tertiary scheme was only ever summed, so tau2 to tau20 existed as system totals and as zeros
on every surface. Whatever they put into the spot was given to no surface - 9 per cent of it on
the double Gauss - and the data file beside it claimed the per-surface totals "reproduce the
transverse totals exactly", which held for everything but those nineteen.

### What was done

Both tertiary routes already form the totals by adding one row per surface - Table I's
`TertiaryTotal` for spheres, the two passes of Sec. 85 for a figured design - and Table II and
the transverse conversion that turn totals into tau are linear. So each surface's row, put through
the same two steps, is that surface's share, and the shares add to the total by construction.
`TertiaryCoefficients.Attach` now stores them. The figured flat in collimated light is the one
place that is not automatic: there the answer is read off a Laurent series, and a surface's own
row may carry negative powers that cancel only against another's, in which case it has no finite
value of its own. Each share is held to the same test as the total - no negative orders, the two
truncations agreeing - and if any fails nothing is split and the report shows tau2 to tau20 as a
line of their own. No design here fails it: the split is taken on all 61 designs with a tertiary
set, `Ladder2_FlatFigured` included.

What is still not split is intrinsic against induced for tau2 to tau20, so the per-surface tau are
surface totals only, and the tertiary coefficients remain system-only as operands.

`TertiarySplitTests` holds the per-surface tau to sum to the system tau, and the shares to 100%,
on every design on disk. The double Gauss now reads 100.0.

## The figuring the save dropped

**An optimised conic or aspheric term was lost when the design was saved, in every format but
`.lhlt`.** Found 23 September 2026 while writing the user guide: optimising
`F1_conic_singlet.zmx` with the conic as the variable took CC from -0.6 to -1.4599 and PRMSA from
0.0812 to 0.0421, and the saved `.optimised.zmx` still said `CONI -0.6`. Read back, it was the
design that went in. The report said "Nothing. The design that came out is the one that went in",
because it listed only radii, thicknesses and glasses, and the command exited 0.

The patchers were written when curvatures, thicknesses and glasses were all the optimiser could
move. Conics and aspheric terms became variables later and the `.lhlt` patcher learned to write
them; the other five were never revisited, and every save-back test moved only the original three.

### A second defect behind the first

The Optiland reader put an even asphere's `coefficients` one power too high, starting them at r^4.
Optiland starts them at r^2 - an even asphere built in Optiland 0.6.2 with [1e-7, 2e-11, 3e-15]
has the sag of 1e-7 r^2 + 2e-11 r^4 + 3e-15 r^6 to twelve figures. The reader had been written to
a hand-made fixture that Optiland itself cannot load ("Missing 'type' field"), and its test checked
the reader against that fixture, so the two agreed with each other and with nothing else. The
fixture is now a file Optiland wrote.

### What was done

- `.zmx` and Optiland `.json` now carry figuring: a conic is edited or inserted, and a sphere that
  was figured becomes `EVENASPH` with `PARM` lines, or an Optiland `EvenAsphere`. Coefficients go
  back in the file's own units.
- CODE V, OSLO and OPTALIX have no figuring writer - there is no real aspheric file in any of them
  here to check one against - so a save whose figuring moved is **refused**: nothing is written,
  and the reason names what moved. The command line writes the optimisation report regardless
  and exits 1; basin hopping deletes a chain checkpoint that its figuring has since outgrown; the
  MCP server says `NOT SAVED` and why.
- The report lists conic and `A2`..`A16` changes.
- Checked: re-running F1 now saves `CONI -1.4598647827590203`, which reads back with PRMSA
  0.042067. `FiguringGoesBackIntoEveryFormatThatCanCarryIt`,
  `AZmxEvenAsphereHasItsTermsEditedInPlace`, `FiguringGoesBackInTheFilesOwnUnits`,
  `AFormatThatCannotCarryFiguringRefusesToDropIt` and `TheReportNamesTheFiguringThatMoved` hold
  it; `OptilandLoadsTheFiguringThisProgramSaved` has Optiland itself load a saved file and agree
  on the sag of the figured surface to 1e-12.

### Why nothing caught it

Every save-back test moved a curvature, a thickness or a glass - the things the patchers wrote -
so the tests checked the patchers against their own scope rather than against the optimiser's.
And the one aspheric Optiland fixture had never been shown to Optiland.

## Which order each deformation term reaches

A deformation `A_n r^n` first contributes at wave order `n`, which is transverse order `n-1`.
Measured term by term rather than asserted, by `AsphericOnsetTests`: each term is nudged on a
surface that is figured already, and every coefficient below its onset must be **bit-identical**
while something at the onset must move.

| term | slot | first reaches | third order | fifth | seventh | ninth |
|---|---|---|---|---|---|---|
| r^2 | `[0]` | **the focal length** - it is a curvature change, not figuring | — | — | — | — |
| r^4 | `[1]` | **3rd** | yes | yes | yes | yes |
| r^6 | `[2]` | **5th** | **no** | yes | yes | yes |
| r^8 | `[3]` | **7th** | **no** | **no** | yes | yes |
| r^10 | `[4]` | **9th** | **no** | **no** | **no** | yes |
| r^12 | `[5]` | 11th | **no** | **no** | **no** | **no** |

**The "no" column is the useful one.** An r^6 term cannot touch the third order however large it
is, so it corrects the fifth without disturbing a third-order solution; r^8 leaves both the third
and the fifth alone. That is a property of the optics, not a tolerance, which is why the test
demands bit-identity rather than agreement.

**But a term is not finished at its onset.** It goes on affecting every order above it, both in
its own right - Buchdahl's aspheric increments cascade, the fifth-order term carrying the
fourth-order one inside it and the seventh carrying both - and by induction, since changing a
surface changes the rays every later surface sees. `ATermKeepsAffectingEveryOrderAboveItsOnset`
pins that for r^4 at all three orders. Reading the table as "r^4 is the third-order term" is the
natural mistake and it is wrong: the table says where a term STARTS.

The rest of this section is about the top of that ladder, where the terms fall off the end of what
this program computes at all. The claim made about r^10 and above is stronger than "it is
ignored": those terms **cannot appear** in anything below the ninth order.

**Both halves of that are tested, and either alone would be worthless.** That the coefficients do
not move is half; on its own it would pass equally against code that threw the term away before it
reached the sag - which is a real bug, and the one the r^2 term actually had once. So
`AsphericBeyondR8Tests` also requires that the surface genuinely is different:

- **Nothing moves.** Adding r^10, r^12, r^14 and r^16 to a surface that is figured already leaves
  every coefficient and the focal length **bit-identical**, on four designs. In the Forbes route
  bit-identity holds even on a surface that was spherical, because its figure is a truncated power
  series in `p = r^2` and r^10 falls outside the truncation rather than being filtered out of a
  list - there is no special case to leave.
- **But the lens really has changed.** A real marginal ray lands elsewhere.
- **And the displacement is ninth order.** Halving the pupil divides it by 512, measured, not by
  the 128 an r^8 term would give or the 2048 of an r^12 one. That is what "it cannot reach the
  seventh order" means when it is put as a measurement rather than an assertion.

**And the rule is a rule, not a convenient exclusion.** The same pair of traces shows `r^10` dead
at the seventh order and live at the ninth: `ForbesTrace.Figure` builds the sag to `degree + 1`, so
degree 3 carries `r^2` to `r^8` and degree 4 carries `r^10`. The highest deformation that can reach
transverse order `2m+1` is `r^(2m+2)`, which is exactly what that line admits, so it is right at
every order rather than tuned for this one. At degree 3 an added `r^10` moves no monomial of `S` or
`T`; at degree 4 it moves the degree-four part and still nothing below it. So the term is excluded
where it cannot contribute and included where it can, by the truncation itself.

**One thing does change, and it is the route rather than the answer.** `Surface.IsFigured` is true
for any aspheric term, r^10 included - correctly, since the surface really is aspheric - and that
flag decides whether the tertiary coefficients come from the spherical scheme or the aspheric one.
So adding r^10 to a sphere moves the design onto the other route, and the two agree to one or two
parts in 1E16 rather than to the bit. The test says so in that case instead of demanding a
bit-identity that would be asserting something untrue about the implementation.

**The report now says it too.** An r^10 row in the aspheric table is marked `takes no part`, with a
note below giving the reason and the consequence: the surface is still that shape and real rays
still see it, so a design whose figuring lives mostly in those terms is not described by the
coefficients, however strong its aspherics look. Both ZPL macros already warned; the C# report
printed the row and said nothing, which invited the reader to assume it had gone in.

## Seidel distortion on a flat face in collimated light

**`SeidelCoefficients` set the distortion of a flat refracting face in collimated light to zero,
silently, from the day the file was written until 22 September 2026.**

Distortion was computed as `S5 = (Abar/A)(S3 + S4)`. Where the marginal ray meets a surface at
normal incidence `A = 0`, and a flat face in a parallel beam is exactly that. The code set the
surface's S5 to zero there and was meant to list it in `DistortionSuppressedAt` - but only when
`S4` was non-zero, and `S4` is zero on a flat. So nothing was listed and nothing was said. On
`Ladder2_FlatPlain` the face's true contribution is +1.060E-03 and the total came out
-5.336E-04 where it should be +5.267E-04: wrong in sign as well as size.

### Why nothing caught it

The comment beside it said the quantity was "genuinely singular" there, so a zero read as a
deliberate refusal rather than a wrong value, and nobody questioned the comment. The means to do so
was already here: `S5 = 2 E n'u'` ties the Seidel distortion to Buchdahl's E, which this program's
own rays confirm, and on the flat design it came out -2.03 where every other design gives 2. It is
not singular. With `u = A/n - yc` either side
of the surface and `H = Abar y - A ybar`, the A divides out exactly:

    S5 = -Abar^3 y d(1/n^2) + Abar ybar c (2 Abar y - A ybar) d(1/n)

### How it came to light, and what settled it

It came to light when the per-surface Seidel sums were compared with Optiland's, which write
distortion without the 1/A and disagreed on that one design (see [optiland.md](optiland.md)). A
disagreement does not say which side is wrong, and it was settled - as it could have been found -
without Optiland: `S5 = 2 E n'u'` holds to six figures on every ordinary design, E on the
flat design is confirmed by this program's own real rays, and it is identical to its value with
the face bent to R = 1E10 - as is the distortion of the real chief ray. So S5 had to be the bent
design's. The form above is now used where `A = 0`, the quotient everywhere else so no other
design moves a bit, and `DistortionSuppressedAt` is gone. It agrees with the quotient on the 147
fixture surfaces where both are defined.

## Mirrors: the defects on the reflecting path, all fixed

**Beyond the third and fifth order, nothing in this program that handles a reflection had been
compared with another program until 22 September 2026, and five defects were found on the
reflecting path - four below, the fifth in NAT (see *Nodal aberration theory on mirrors* further
down).** A reflection is carried as a refraction into
`-n`: the paraxial trace and the fifth-order code always did that, and nothing else did. Every
one of the four is a place that was handed the plain indices, or divided by the signed one.

| defect | what it did on a mirror |
|---|---|
| `RealRayTrace` had no reflection | refracted between equal indices, so the ray went straight through: on the parabola every axial ray landed 10-20 mm off the axis, where the exact answer is zero |
| the seventh order (`TertiaryCoefficients.Attach`) was handed unsigned indices | the mirror had no power in the scheme while the paraxial data said f = 100: tau2..tau20 were NaN |
| the same routine divided by the SIGNED image index | once finite, every tau was negated against the third and fifth order - which `Prms` multiplies together |
| Forbes' series trace was handed unsigned indices | its own guard against reflection never fired; it traced the mirror as a refraction and returned zeros |

### Why nothing caught it: the data was not read critically enough

**The evidence was already in this repository, and it was explained away rather than chased.**
That is the real reason, and it is not a flattering one. The parabola is the one design whose
answer is known exactly, and on it:

- the real-ray fit left a residual of 0.75 - and the cross-lineage sweep's own comment called
  that "comparing against noise" and skipped it. It was the non-reflecting trace;
- the seventh order was NaN - and two sweeps passed it every build, because a comparison with NaN
  is false and no assertion could fire.

Either one, looked at critically, leads straight to the defects. No other program was needed to
see that a fit failing on an exact design is a finding, not noise, or that a sweep reporting a
pass on NaN has checked nothing. That is the lesson worth keeping.

**OpticStudio was never wrong here, and never disagreed with this program on a mirror.** The only
comparison with it on a reflecting design was FIFTHORD on the parabola: eighteen values, the
third order, the fifth and B7. This program matched them then and matches them now; the paraxial
trace and the fifth-order code always signed the index correctly. Every one of the defects lies
outside what FIFTHORD computes - the real ray trace, tau2 to tau20, the series trace, the NAT wave
front - and none of those had been set against OpticStudio, or anything else, on a mirror.
OpticStudio would have caught all of them: its ray trace reflects, and `RAYINV.ZPL` or a plain
ray-for-ray comparison on the parabola would have shown the trace and the seventh order wrong. It
was simply never asked.

### What prompted the second look, and what settled it

Embedding Optiland meant comparing ray landings on every design on disk, the parabola included,
and its rays landed 20 mm from this program's. That reinforced that something was amiss with
reflection - it did not find anything the symptoms above were not already showing, and Optiland is
no more right than OpticStudio, whose rays reflect too. It was settled without Optiland, by the
parabola itself: it images an axial point perfectly, so every axial ray
must reach the axis, and it now does to 1E-12. With the trace right, the seventh order is held
against reflected real rays - all nineteen tau to 6E-6 of the largest at ten degrees
(`ParabolicMirrorTests.TheSeventhOrderOfAMirrorIsFiniteAndAgreesWithRealRays`) - and a
spherical mirror, `F10_spherical_mirror`, was added: its thirty-seven coefficients agree with
reflected rays too.

**The frame is one frame now.** With correctly reflected rays, all seventeen third- and
fifth-order coefficients came out as exactly minus Buchdahl's, zeros included - a change of frame,
not an error: Buchdahl measures the image-space transverse aberration along an axis a reflection
reverses, and a ray trace keeps one frame. The ray inversion turns the landings after an odd
number of reflections, and the seventh order now takes |N'| as the fifth-order code always has.
All three orders are in FIFTHORD's frame: its recorded reference for the parabola pins the third
and fifth, and the seventh is tied to them by |N'| and by the reflected rays.

### The macros had the same four, and are fixed and run

`BUCH7_ASPH` took |N'| for the third and fifth and the signed index for the seventh; `BUCH7` took
the signed index for all of them, negating every total against FIFTHORD; `FORBES` traced a mirror
as a refraction; `RAYINV` returned every coefficient negated. All four are fixed, and were run in
OpticStudio on the parabola (F4) and the spherical mirror (F10): `BUCH7_ASPH` reproduces this
program on all thirty-seven to every printed digit on both, `BUCH7` does the same on F10 and
declines the figured F4, `RAYINV` agrees in sign on every coefficient and to its fit's floor, and
`FORBES` declines. The runs are tabulated in
[optiland.md](optiland.md).

### Nodal aberration theory on mirrors

NAT's third order was always tested on a mirror system, and against published numbers:
Thompson's two-mirror telescope reproduces his Tables 3 and 5. But that test compares the
magnitudes of sigma, and two other NAT paths had never been run on a mirror at all. Asked
directly, both were wrong:

| defect | what it did on a mirror |
|---|---|
| `WaveFront.FromSystem` - the wavefront coefficients the report's NAT sections print - was handed unsigned indices | every coefficient of the telescope, and of the parabola, came out EXACTLY ZERO, and was printed |
| `RealRayTrace`'s intersection stopped on an ABSOLUTE residual of 1E-13 | after the telescope's 7490 mm gap the roundoff in z + t dz alone exceeds that, so every off-axis ray was reported lost at the secondary, and `RealSigma` gave the secondary sigma = (0, 0) |

The second is not a reflection defect: it is a long gap, and the telescope is the only design
with one. Neither was visible from the axis, which is the only ray that reaches every vertex
exactly. Now `WaveFront` signs its indices as `Attach` does, and closes against the Seidel sums on
the telescope to 4E-12 as it does on refracting lenses; and the intersection also stops when the
Newton correction no longer moves t - a ray that stalls stalls forever, so no ray that ever
converged moves a bit, and the whole suite holds bit for bit. Real-ray sigma now agrees with the
paraxial sigma on both mirrors to 2E-5, SIGNS INCLUDED, which the Table 5 test could not check
(`ThompsonTelescopeTests`).

### Two reflections, measured

The same telescope settles whether the frame rule holds after two reflections. After two the image-space
index is positive again, so the rays are not turned - and inverted unturned they agree with
Buchdahl on every order: third to 7.5E-12, fifth to 8E-9, seventh to 1.7E-5 of the largest tau
at four degrees (`TwoReflectionsRestoreTheFrameInEveryOrder`). At Thompson's third of a degree
the seventh order's field terms sit at the fit's floor; the gap falls from 6E-2 there to 1.7E-5
at four degrees, which is a floor being outgrown.

### What is still open

The MACROS are established on one reflection only. They sign their indices through OpticStudio's
`ISMS`, and whether that flags the mirror surface or every space after an odd number of
reflections has not been determined: on a single mirror the two readings agree, which is why
both mirror fixtures passed either way. The telescope, run through `BUCH7`, `BUCH7_ASPH` and
`RAYINV`, would settle it. And the series trace, here and in `FORBES.ZPL`, still declines a mirror
rather than trace one.

## What is not established


**The FIELD-dependent fifth order under immersion, INDEPENDENTLY.** Its spherical part is
settled: `B5` fitted off traced axial rays sits 1.3E-4 from the coefficient at n = 1 and at
n = 1.30 alike, and the third and seventh orders are checked at either end. FIFTHORD agrees with
this program on the nine field-dependent fifth-order coefficients at an immersed object space
too - but that is CONSISTENCY, not proof: the macro and this program are two implementations of
the same published method and could carry the same inherited assumption, which is exactly how the
seventh-order defect survived in both. What is missing is a reference of different lineage - a
ray fit or a series trace - that reaches the field-dependent fifth order at a finite conjugate.
See *The two ends of the system* above.

**A curved image surface is not read by the RAY TRACE, and a curved object surface is not read
by anything.** The coefficients' indifference to the image surface is the convention and is
confirmed against OpticStudio; what is not established is the rest. A spot or fan on a curved
detector is measured on a plane here - 17.24 um reported against 9.68 um on a detector of
R = -35 - and a curved object, whose per-field conjugate genuinely does reach the aberrations,
is ignored outright, worth 47 um on an object of R = 25. Both sizes are measured in
`CurvedObjectAndImageSurfaceTests`. The end MEDIA, by contrast, are carried correctly, and that
is now tested at n = 1.01 and n = 1.30. See *The two ends of the system* above.

**Forbes' series trace on a mirror: not supported.** It declines a reflecting system, here and in
`FORBES.ZPL`, so on a mirror the seventh order has one series route, Buchdahl's, checked against
real rays alone - on one reflection, the parabola and a spherical mirror, and on two, Thompson's
telescope. See *Mirrors* above.

**How far the seventh order reaches.** It is a property of the lens and not a number. Of five
designs measured, one is described by third order alone, two need the full seventh to reach a
per cent, one is not well described at seventh, and one is not described at all. See
`spot-prediction.md`.
