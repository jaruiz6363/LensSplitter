# The ninth order, by the Forbes route

A scope note, not an implementation. Nothing in this repository reads a ninth-order coefficient
off a series trace yet, and the one ninth-order quantity that is computed — quaternary spherical
aberration, `QuaternarySpherical.cs` — comes from Buchdahl's paper IV and not from here.

## Why this route, and at this order why only this route

[forbes.md](forbes.md) describes Forbes' series trace as the standing second opinion on the
aspheric arrangement, which is the job it does at the seventh order and does permanently. That
description is complete for the seventh order and misleading one order up, because at the ninth
there is almost nothing left to be a second opinion *about*.

| | seventh order | ninth order |
|---|---|---|
| Buchdahl, spherical | Table I, arranged and published | **one coefficient of thirty** — paper IV, quaternary spherical |
| Buchdahl, figured | §85 method, arrangement re-derived here | **nothing** — no arrangement at any level of completeness |
| Forbes | the judge, on a refracting system (it declines a mirror) | the only route there is, on a refracting system |

The asymmetry is not an accident of what got published. Buchdahl's scheme arranges each order into
a table of `t`-numbered entries, and the arrangement is per-order work — the tertiary needed a
spherical twin and a two-pass split before a figured surface could be carried, and the quaternary
would need the same again over a larger set. Paper IV takes three pages to reach the single
coefficient in `rho^9`, the one term that survives on axis, and stops. Forbes' formulation has no
arrangement to extend: the order is the truncation degree of the invariant ring, `f_i` already
carries figuring as ordinary coefficients, and the code does not branch on whether a surface is
aspheric. **The property that made it a fit judge — no special case for figuring — is the same
property that makes it the only thing that can reach the ninth order on a figured refracting
design** (it does not trace a mirror).

So the answer to "is Forbes useful for anything beyond adjudication" is that one order up it stops
being the second opinion and becomes the first, and for twenty-nine coefficients of thirty it is
the only opinion available from series at all.

**One correction to the table's first row, from Sands (1973).** A ninth-order scheme for all
thirty did exist — Sands says his ninth-order columns come from a program he wrote "based on some
unpublished work of Buchdahl", running at 0.035 s per surface on a Univac 1108. So the gap at the
ninth order is a publishing gap and not a gap in what Buchdahl knew. Two things about it are
unchanged, though: it was **spherical surfaces only**, the same restriction paper IV carries, and
neither the scheme nor the program is available. What is available is its output, which is why
Sands's printed tables appear below as an oracle.

## Thirty coefficients, and where that number comes from

The transverse aberration of a rotationally symmetric system is a vector polynomial, and its
degree-`N` part is spanned by `(p^a k^b u^c)` times either `y0` or `b0`, with `a+b+c = (N-1)/2` and

    p = y0 . y0 ,   k = y0 . b0 ,   u = b0 . b0

Counting monomials gives `2 * C(m+2, 2)` for `m = (N-1)/2`, and grouping them by aperture and field
power gives a family breakdown that can be checked against tables this program already carries:

| order | coefficients per family, all-aperture first, all-field last | total |
|---|---|---|
| fifth | 1, 2, 3, 3, 2, 1 | 12 |
| seventh | 1, 2, 3, 4, 4, 3, 2, 1 | 20 |
| **ninth** | 1, 2, 3, 4, 5, 5, 4, 3, 2, 1 | **30** |

The seventh-order row is Robb's table exactly as
[optimizer.md](optimizer.md#which-coefficient-is-which-aberration) prints it — `B7`; `Tau2 Tau3`;
`Tau4 Tau5 Tau6`; `Tau7`–`Tau10`; `Tau11`–`Tau14`; `Tau15 Tau16 Tau17`; `Tau18 Tau19`; `Tau20` —
and the fifth-order row is `B5`; `F1 F2`; `M1 M2 M3`; `N1 N2 N3`; `C5 Pi5`; `E5`. Both come out
right.

**And thirty is published, not extrapolated.** Sands (1973), footnote 8, describing the program
behind his ninth-order columns: "the a and b components of six third-order, 12 fifth-order, 20
seventh-order, and 30 ninth-order coefficients." His Table I prints the ninth-order polynomials
themselves, and its groups count 1, 2, 3, 4, 5, 5, 4, 3, 2, 1 — the row above, from a source that
knew nothing of this repository. This paragraph replaces one that called the number an
extrapolation; it was, and it no longer is.

Note also his **six** at third order, where Seidel has five. That is the monomial count, and it is
the reason the third-order exception below is an exception in the classical naming rather than in
the algebra.

Written out, with the family names as Sands's Table I gives them. They are consistent with the rule
[optimizer.md](optimizer.md#which-coefficient-is-which-aberration) derives from the monomial — no
field is spherical, one power of field coma, two oblique spherical; no aperture is distortion, one
power of aperture astigmatism and field curvature, two elliptical coma — which is worth noting,
because the rule was applied at the seventh order here before anyone had seen a ninth-order table
to test it against:

| multiplies | how many | family |
|---|---|---|
| ρ⁹ | 1 | spherical — **the only one Buchdahl published**, paper IV |
| ρ⁸H | 2 | circular coma |
| ρ⁷H² | 3 | oblique spherical |
| ρ⁶H³ | 4 | cubic coma |
| ρ⁵H⁴ | 5 | quintic astigmatism |
| ρ⁴H⁵ | 5 | quintic coma |
| ρ³H⁶ | 4 | cubic astigmatism |
| ρ²H⁷ | 3 | elliptical coma |
| ρH⁸ | 2 | linear astigmatism and curvature of field |
| H⁹ | 1 | distortion |

**The names are Sands's, from his Table I, and the middle four rows are the ones to check against
a page image before they are used.** He names the families rather than leaving them blank, which
is better than the rule would have managed on its own — the seventh order has two families this
program labels *no classical counterpart*, and the ninth apparently does not. But the table is a
2005 scan whose formulae are OCR noise, the row-to-name alignment was read off that damaged layer,
and he footnotes that two of the types have no counterpart among the lower orders without the
footnote marker surviving legibly. Treat the counts as settled and the four middle names as
provisional.

**The third order looks one short of the rule and is not.** The rule counts 1, 2, 2, 1 = six, and
Seidel names five: the two `ρ²H` monomials are one classical aberration, coma, rather than two.
Sands counts **six** there, so the sixth is real and it is the naming that collapses it, not the
algebra. Nothing collapses from the fifth order on either — Robb carries `F1` and `F2` separately,
and `Tau2` and `Tau3` likewise. So the rule holds at every order and the third order is an
exception only in what the classical vocabulary chose to name.

Even so, the implementation should **generate** the basis and assert its size rather than take
thirty on trust. This table was written by hand, and the seventh-order table in
[optimizer.md](optimizer.md) was wrong when it was first written by hand.

## What already exists, and what it is worth

**The trace carries every order its truncation keeps.** `ForbesTrace.Run` takes `degree` as a
parameter and `ForbesTrace.Figure` expands `f_i` to match; nothing in either is written for the
tertiary. Only `ForbesCoefficients.DegreeSeven` stops at seven, and it stops at a `const int`.

**And the content past the seventh order has been measured, not assumed.** On the worst design in
the fixture set — `CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8` at its corner field — asking the
trace directly for a real ray at increasing truncation gives, as the worst error over the pupil:

| trace degree | worst \|predicted − traced\| |
|---|---|
| 3 (seventh) | 134.2 µm |
| 4 (ninth) | 11.0 µm |
| 5 (eleventh) | 11.8 µm |

A factor of twelve sits in one step, on the design that [spot-prediction.md](spot-prediction.md)
finds undescribed at the seventh order and where the aspheric coefficients are not at fault — the
Buchdahl and Forbes tertiary sets are equally lost there, +318.5 and +310.0 per cent. The same
thing shows in distortion, where the chief-ray displacement at the corner of `CookeTriplet` walks
from 1.219E-02 at degree 3 towards the traced 8.836E-03 as the degree rises
([distortion-prediction.md](distortion-prediction.md)).

**What does not exist is the reading.** The trace holds the degree-nine part; nothing turns it into
named coefficients, and nothing consumes them.

**One anchor exists.** `QuaternarySpherical.cs` is Buchdahl's paper IV, checked against his own
printed table and against his closed-form plane-surface identity. It gives one of the thirty, on
spherical surfaces only, per surface and split into intrinsic and induced. That is one column of a
thirty-column answer, and it is the only published number this whole order can be checked against.

## What has to be built

Four things, in the order they unblock each other, and a fifth that needs nothing.

**1. Unfreeze the extraction degree.** `ForbesCoefficients.DegreeSeven` opens with
`const int order = 7`, builds `ScaleSeries.S(order)`, and returns `outM[order]`. Everything in it —
the scale parameterisation, the `sin = tan/sqrt(1+tan^2)` expansion, the finite-conjugate ray
construction — is written in terms of `order` and not in terms of seven. This is the small part.

**2. The degree-nine model rows.** `CoefficientInversion.Build` forms each shape's twenty-column
row as `Prms.Transverse(unit, rho, theta, h, 7) - Prms.Transverse(unit, rho, theta, h, 5)`, plus
one hand-supplied column for `Tau20`, which Robb's spot polynomial omits because distortion does not
change a patch's size. There is no ninth-order `Prms`, so there is no ninth-order model row. This is
the substantial part, and §"The basis, which turns out to be printed after all" below is what it
has to be built against.

**3. Shapes that can separate thirty columns.** `DefaultShapes` gives seventy-five shapes — three
pupil radii by seven azimuths by three fields, plus nine pure-aperture and three pure-field cases —
and each contributes up to two rows, so there is no shortage of equations. The question is
conditioning, not count: the ninth order carries azimuthal harmonics up to `5θ` where the seventh
stops at `4θ`, and seven azimuths that resolve harmonics to four need not resolve five. A shape set
that cannot separate `cos 5θ` from `cos θ` fails **silently** — the solve returns numbers and the
self-residual can still look small. `FixedModel` already builds a least-squares operator once per
shape set, so the rank and condition of the thirty-column model can be examined before any
coefficient is believed, and should be.

**4. `Prms` extended, if a spot is wanted.** The term table is written as `(A, B, theta-function)`
triples and the azimuthal averages are computed numerically by `ThetaAverage`, so the machinery
generalises; what has to be supplied is the ninth-order rows and the `cos 5θ` family. Its trapezoid
comment — "a trigonometric polynomial of degree at most six" — becomes ten, still far below its
4096 samples.

**5. Nothing, for the aspheric term that becomes live — and that is worth checking rather than
assuming.** `r^10` cannot reach the seventh order and **does** reach the ninth, which is exactly
the order it first contributes at. So a ninth-order treatment has to carry it, and the trace
already does: `ForbesTrace.Figure` builds the sag to `degree + 1`, so degree 3 admits `p` to `p^4`,
which is `r^2` to `r^8`, and degree 4 admits `p^5`, which is `r^10`. That is not a happy accident
of the code — the highest deformation that can reach transverse order `2m+1` is `r^(2m+2)`, which
is precisely what `degree + 1` lets in, so the one line is right at every order.

`RTenthIsDeadAtTheSeventhOrderAndLiveAtTheNinth` pins both halves on one pair of traces: at
degree 3 an added `r^10` moves no monomial of `S` or `T` at all, and at degree 4 it moves the
degree-four part while still moving nothing below it.

**What would have to move is everything built around "r^8 is the ceiling."**
`AsphericDiagnostic.BeyondEighthOrder` measures unrepresentable figuring as the sag from `r^10`
upward against `r^4` to `r^8`; at the ninth order that boundary becomes `r^12` against `r^4` to
`r^10`. The optimiser refuses `r^10` and beyond as variables, and `Surface.AsphericVariable` says
why — at the ninth order `r^10` becomes a legitimate variable and the refusal would be wrong. The
report's new note on figuring beyond `r^8` would need the same shift. None of that is difficult;
all of it is easy to forget, which is why it is listed.

## The basis, which turns out to be printed after all

**This section said the opposite when it was written, and the correction is the most useful thing
on the page.** It said there was no printed ninth-order basis to land on and that defining one was
the first irreversible decision of the work. There is one: **Sands (1973), Table I, "The
ninth-order aberration polynomials"** — the generic form of both components, thirty coefficients
grouped by monomial, with the families named. It is in hand. The lower orders he gives by
reference, to Eqs. (2.6)-(2.11) of Cruickshank and Hills (1960), which is also in hand, so the
whole chain from the third order to the ninth is on the shelf.

That changes what the extraction step is. It is a **transcription against a printed table**, which
is the same kind of work as the seventh order and carries the same kind of check — and this
repository knows what that work costs, having done it once for Table I and once, wrongly at first,
for the aspheric arrangement. It is not a definition exercise, and no convention has to be
invented and then defended.

Two cautions before anyone starts. The scan's formulae are OCR noise, so **Table I has to be read
from a page image**, and the group counts are the only part of it relied on here. And Sands's
coefficients are in his own normalisation; the conversion to Rimmer's is exactly the class of step
this repository has twice lost days to, which is the argument for the generated basis below as a
cross-check rather than as a replacement.

Two forms are available for what the program stores.

**The monomial-and-azimuth form**, continuing Robb's pattern: each coefficient multiplies
`ρ^A H^B` times one azimuthal function, with the meridional and sagittal components carrying the
cosine and sine members of each harmonic. It is what every table in this program is already written
in, it makes the family names derivable by the rule `AberrationNames` already applies — no field is
spherical, one power of field coma, two oblique spherical, and from the other end distortion,
astigmatism and field curvature, elliptical coma — and a reader who knows the seventh-order table
can read the ninth-order one.

**The invariant form**, `p^a k^b u^c` times `y0` or `b0`, which is what the trace is natively in. It
needs no derivation at all: the coefficients fall out of `ForbesTrace.S` and `ForbesTrace.T` with no
model row, no shapes and no solve. It is also unreadable to anyone who has not read Forbes, and it
does not connect to `Prms`, to the merit function, or to any name a designer would use.

**The recommendation is the monomial form, generated rather than typed, and then checked against
Sands's printed table.** Generate the basis from the symmetry rule for degree `N`, run the
generator at `N = 7`, and require that it reproduce Robb's twenty columns — the same `ModelRow`
matrix, to roundoff, in the same order. A generator that regenerates a table which has itself been
validated against real rays to 0.003 per cent is a generator that can be trusted at the next
degree, and that check exists today, before any ninth-order work is done. It is the cheapest thing
on this page and the one that would settle the most.

Generating it and reading Table I are not alternatives, and the case for doing both is that they
fail differently: a generator can be systematically wrong in a way nothing catches, and a
transcription from a damaged scan can be wrong in one entry. Agreement between them is worth more
than either.

## How it would be checked, and the honest difficulty

The seventh-order aspheric arrangement was trusted because two routes sharing no arithmetic agreed,
and the rays agreed with both. **That structure is not available at the ninth order** — there is no
second implementation to run — and saying so plainly is the point of this section. What is
available is weaker and is worth having in this order:

**1. Quaternary spherical, against paper IV.** One coefficient of thirty, spherical surfaces only.
On axis it is the *whole* ninth order — every other term carries a power of the field — so the
comparison is clean and the oracle is Buchdahl's own printed table. It is the cleanest check that
will ever exist here, and it covers a thirtieth of the answer.

**2. Sands's own printed numbers, which are the nearest thing to a ninth-order oracle in
existence.** His Tables III and IV give two complete prescriptions — a triplet at f/2.9 and 24
degrees, and a Vega type at f/6.1 and 45 degrees, curvatures, separations, indices, stop position
and the image-space direction constants — and beside each of them the computed `x(n)a` and
`m(n)a`, in units of 1E-3, for `n = 1..4`. **The `n = 4` rows are the ninth order**, evaluated on a
named lens.

This is the standard this repository already trusts: numbers printed beside the prescription they
were computed on, the same footing as Buchdahl's Table I and Thompson's Tables 1 to 5. It is a
weaker oracle than a per-coefficient one, because each `x(n)a` is a combination of many
coefficients rather than one of them, so a compensating pair of errors would survive it. It is
also the only check available that exercises the ninth order off axis at all, and reaching it
needs Sands's Table II as well — which means both tables transcribed from page images, and the
normalisation conversion done and defended.

**3. The real-ray inversion, read one power higher.** `CoefficientInversion.Invert` traces twelve
scales and fits eight odd powers, `s^1` through `s^15`; the `s^9` coefficient is already in that fit
and is thrown away by `OddFit(ey, powers, 7)`. Asking for nine instead is a one-argument change, and
it is a genuinely independent oracle — real rays, no series anywhere.

**But the ladder was tuned for `s^7` and its accuracy at `s^9` is unmeasured.** The comment in that
file records what the tuning cost: a ladder out to `s = 1` with powers to `s^11` recovered `B7` to
only 1.5 per cent, and twelve points out to six tenths with powers to `s^15` recovered it to one
part in a million. Nothing there says what the same ladder does for the next power up, and the
answer is certainly worse. **Measuring that is a prerequisite, not a follow-up** — an oracle of
unknown accuracy cannot adjudicate anything, and at this order it is the only general one.

**4. The self-residual.** The degree-nine part of the trace should lie *exactly* in the span of the
thirty, giving a residual near 1E-16 as the seventh order does, rather than near 1E-6. This is a
strong structural check and a weak correctness check: it catches a basis that is incomplete or
degenerate, and it passes happily on a basis that is complete and wrongly scaled.

**5. Per-surface telescoping.** `ForbesPerSurface` splits by construction — truncation and
linearisation, not arrangement — so its identities hold at any degree: contributions plus reference
sum to the totals, the three parts sum to each contribution, the first surface induces exactly
zero, and the aspheric part is identically zero on an unfigured surface. These carry over to the
ninth order for nothing and are worth asserting there.

**6. The end-to-end statement, which is the reason to do any of it.** The hard-corrected aspheric
triplet is mispredicted by +318 per cent at its corner and the trace says the missing content is a
factor of twelve of it. A ninth-order spot that lands near the traced one on that design is the
result this work is for, and it is a measurement rather than an identity.

**And the reference term will move.** `A(0)`, the all-linearised system, is `tau20` alone at the
seventh order — one coefficient, pure field, because a paraxially perfect system launched with
direction cosines sends a ray to about `efl sin(theta)` where the coefficients are referred to
`efl tan(theta)`. The ninth-order expansion of that same sine puts a reference term in the `H^9`
coefficient too, and a test asserting the reference is `tau20` alone will have to be told about it.
It is not a fault and it should not be charged to a surface.

## Cost

Measured, from [forbes.md](forbes.md): degree four costs about 3.6 times degree three, close to the
ratio of the squares of the term counts, `35^2 / 20^2`. On the figures in that table the trace goes
from about 0.36 ms to about 1.3 ms per design — still under the 3.99 ms the tertiary inversion
costs today, and that scaffolding disappears if the coefficients are read directly off `S` and `T`.

The scaling is steep and keeps getting steeper, which is what Forbes' order doubling is actually
for. It is still not for us: degree four is one truncation step, the concatenation machinery order
doubling needs would be larger than the trace, and this page proposes reaching the ninth order by
turning a `const int` into a parameter rather than by adopting his headline result.

## What this would not give

**A Buchdahl-style per-surface breakdown in his own terms.** `ForbesPerSurface` splits a surface's
share three ways and that carries to any degree, but it is not `t`-numbered, it does not agree with
Table I's attribution at the seventh order and is not expected to
([forbes.md](forbes.md#it-does-not-agree-with-table-is-per-surface-split-and-is-not-expected-to)),
and nothing at the ninth order could settle which convention to publish, because there is no
competing convention to settle it against.

**An independent judge.** Twenty-nine of the thirty would rest on one series implementation, a ray
inversion of unmeasured accuracy at that power, and two printed tables from 1973 that constrain
combinations rather than coefficients. That is a weaker footing than anything else this program
reports, and it should be reported as such rather than presented beside the tertiary numbers as
though it carried the same weight.

## Suggested first step

**One number, two routes, no basis decision.** On a spherical design, take the degree-nine part of
the Forbes trace on an axial fan — zero field, where the ninth order is the `ρ^9` coefficient alone
and no model row, no shape set and no basis convention is needed — and compare it with
`QuaternarySpherical`, which is Buchdahl's paper IV and is already checked against his printed
table.

It is a few hours of work. If the two agree, the trace is validated at the ninth order against the
only published number in existence, and everything above becomes a question of reading rather than
of method. If they disagree, that is worth knowing before thirty columns of basis are written.

**Then the step that has no oracle**, and the one this is all for: the same axial quantity on a
*figured* design, where `QuaternarySpherical` refuses to answer and no arrangement exists. The
ladder of ray traces is the only check, and by then its accuracy at `s^9` will have been measured
on the spherical case where the truth is known.

## Source

G. W. Forbes, *Order doubling in the computation of aberration coefficients*, JOSA **73** (6), 782
(1983) — §3.A for the trace, as [forbes.md](forbes.md) describes it. No new source is needed for
this work, which is the whole argument for doing it this way.

H. A. Buchdahl, *J. Opt. Soc. Am.* **48**, 757 (1958) — paper IV, quaternary spherical aberration,
the one ninth-order coefficient Buchdahl published himself.

P. J. Sands, "Aberration coefficients and surfaces of best focus," *J. Opt. Soc. Am.* **63**(5),
582 (1973) — **the ninth order's printed basis and its only off-axis oracle**, in Table I and in
Tables II to IV respectively, together with the footnote that fixes the count at thirty. It was
read for an unrelated reason, the defocus-blindness of Robb's spot, and it carries this as well;
see [references.md](references.md). The lower-order polynomials it refers to are Eqs. (2.6)-(2.11)
of F. D. Cruickshank and G. A. Hills, *J. Opt. Soc. Am.* **50**, 379 (1960), also in hand.
