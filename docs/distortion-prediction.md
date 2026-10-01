# Distortion from the aberration coefficients

Third-, fifth- and seventh-order coefficients predict a distortion figure. This measures how
close that prediction gets, against the chief ray this program traces, on six designs.

**The subject here is the coefficients, not distortion.** Nobody should use this to find out
what a lens's distortion is: one traced chief ray gives that exactly, in 2.4 microseconds
against the 46 the coefficients cost to build, and it does not go 34 per cent wrong at the
corner. Distortion is used here because it is the cleanest window onto the coefficients there
is — at zero pupil radius only three of them survive, and they separate by their power of the
field, so each can be measured on its own.

What the coefficients give a designer that a trace cannot is which ORDER the distortion is,
and which SURFACE it comes from. This document is about whether they can be trusted to say so.

It is the counterpart of [spot-prediction.md](spot-prediction.md), and it answers a question that document
cannot. A predicted RMS spot mixes eighteen coefficients into one number, so errors that
cancel pass unnoticed — that is measured there, not assumed. Distortion is the opposite case.

## Where a coefficient can be checked on its own

Set the pupil radius to zero in Robb's polynomial. Every term carrying rho vanishes and three
survive:

```
  eps_y(0, theta, h) = E h^3 + E5 h^5 + tau20 h^7
```

Three terms, separated by their power of the field alone. Subtract the lower orders from a
traced displacement, divide by `h^k`, and what is left approaches the *k*th coefficient as
the field shrinks. Each of the three is measured **individually**, against exact rays.

**This is not the first ray-traced check of a tertiary coefficient here, and it is worth being
exact about what it adds.** `CoefficientInversion` already recovers all twenty from traced rays
— by scaling ray shapes and fitting an odd polynomial in the scale — and its default shapes
include the zero-pupil case for precisely this reason. `ForbesCoefficientsTests` uses it on
nine fixtures, and it was that test which established, while the aspheric arrangement was still
wrong, that Forbes agreed with the rays where the arrangement did not. The arrangement has
since been completed and the same test now finds both agreeing with the rays. That is the
general instrument, and it came first.

What this one adds is narrowness. Three coefficients rather than twenty; no basis, no
least-squares solve, no model of the other seventeen, and an error bar of its own from the
spread between the two field fractions it extrapolates. The two ray routes agree on `tau20` to
between 0.03 and 1.2 per cent across the figured fixtures, and they share nothing but the
tracer — so what follows is a corroboration by a second, much simpler instrument, not a
discovery.

**And it cannot be passed by B7.** Seventh-order spherical aberration carries the seventh
power of the aperture and the zeroth of the field, so it contributes nothing to distortion.
The one seventh-order quantity that was available before the tertiary work — from FIFTHORD,
and from this program's own fifth-order working — says nothing here at all. Whatever the
seventh order buys, it buys through `tau20`.

## Method

Predicted is the expression above, at three truncations: `E` alone, `E + E5`, and all three.

Traced is the ray with rho = 0 — the one that leaves the field point and crosses the centre
of the **paraxial** entrance pupil — traced by `RealRayTrace`, caught at paraxial focus, and
measured from the paraxial chief-ray height there. That is the ray the polynomial is a series
for, so it is the only fair reference; aiming it iteratively at the centre of the real stop
would fold in pupil aberration, which is real but which the rho = 0 term makes no claim about.
It is also what a design program reports as distortion with ray aiming off.

**Two ideal heights, and they are alternatives — not two views of one number.** Distortion is a
departure from an ideal mapping and there are two mappings in use:

| | ideal height |
|---|---|
| **F-tan(theta)** *(printed first)* | *f* tan(theta) |
| **F-theta** | *f* theta |

The rays are the same in both; only the ideal moves. Both tables are printed and neither is
ranked — which mapping a design is specified against is the designer's, as it is in every
program that offers the choice. The two look nothing alike, and that is arithmetic rather than
disagreement: they are related exactly by

```
  (1 + F-tan(theta)/100) * tan(theta)/theta - 1 = F-theta/100
```

and tan(theta)/theta is 4.27 per cent at 20°. So a lens corrected for one mapping reads as
several per cent distorted in the other by construction. On the aspheric triplet below,
−0.0909 per cent F-tan(theta) is +4.1750 per cent F-theta, and the whole of the difference is
that factor.

**The reference plane is paraxial focus, and a design program's is not.** The coefficients are
referred to the paraxial image plane and Robb's polynomial has no defocus term, so the
comparison has to be made there. A design saved at best focus puts its image surface somewhere
else, and distortion read at that surface is a different number — on the Cooke triplet fixture,
0.0620 per cent at the corner against 0.0486, a difference of a quarter from the plane alone.
The report gives both and says which is which; the second is the one that matches what other
implementations report for that lens, 0.062021 per cent.

**The field variable is a tangent, not an angle.** The coefficients are converted with
H = tan(theta_max), so fractional field *h* means tan(theta) = *h* tan(theta_max), and the ray
to trace is at atan(*h* tan(theta_max)) — not at *h* theta_max. At nine tenths of a 20° field
the two readings differ by 0.8 per cent, which is 2.4 per cent in a term of degree three and
5.7 per cent in one of degree seven; worse, the Gaussian image heights they imply differ by
fifteen times the whole distortion being measured. Getting this wrong does not blur the
answer, it replaces it. `ReadingTheFieldAsAnAngleFractionRuinsIt` pins it.

## Six designs

Errors are per cent of the traced displacement. The distortion figure itself — per cent of
image height — is given so that a large relative error on a lens with no distortion can be
recognised as costing nothing.

**Cooke triplet, f/5, 20° half-field, all spherical.** Traced distortion at the corner:
0.0486 %.

```
     H     traced%      3rd    3rd+5th   full 7th
  0.20      0.0021     -0.7%      0.0%       0.0%
  0.40      0.0084     -2.7%     -0.2%      +0.1%
  0.60      0.0195     -5.8%     -0.4%      +1.1%
  0.80      0.0352     -7.3%     +2.2%      +6.7%
  0.90      0.0434     -4.6%     +7.7%     +15.0%
  1.00      0.0486     +5.2%    +22.0%     +34.2%
```

Through the inner half of the field the seventh order is essentially exact — a tenth of a per
cent where third order is already out by three. Past H = 0.7 it goes the other way and
**overshoots**, by a third at the corner, while third order alone lands within five per cent
by cancellation rather than by being right (it is 7 per cent low at H = 0.8 and 5 per cent
high at H = 1.0, having crossed zero in between).

The overshoot is not this program's. Forbes' series trace, run at the same truncation and
sharing no code with the Buchdahl route, overshoots the same way, and taking it further walks
the prediction back towards the ray:

| the chief ray's displacement at H = 1.0 | |
|---|---|
| traced exactly | 8.836E-03 |
| Buchdahl, seventh order | 1.186E-02 |
| Forbes, degree 3 (seventh order) | 1.219E-02 |
| Forbes, degree 4 (ninth) | 1.153E-02 |
| Forbes, degree 5 (eleventh) | 1.011E-02 |

That is the ninth order arriving, in a quantity small enough that its arrival is the whole
answer. The same mechanism as the spot's turnover at the very edge, and it bites earlier here:
distortion on this lens is five hundredths of a per cent of image height, a residual of
cancelling terms, so the next order is not a correction to it but a comparable fraction of it.

**The same triplet with the object at 250 mm.** Distortion is twelve times larger, and the
series behaves as a series should.

```
     H     traced%      3rd    3rd+5th   full 7th
  0.20     -0.0184     -0.7%      0.0%       0.0%
  0.40     -0.0753     -2.9%     -0.1%       0.0%
  0.60     -0.1760     -6.5%     -0.4%      -0.2%
  0.80     -0.3319    -11.8%     -1.7%      -0.9%
  0.90     -0.4379    -15.4%     -3.1%      -1.9%
  1.00     -0.5706    -19.9%     -5.5%      -3.8%
```

Each order improves on the one before it at **every** field, which is what convergence looks
like when the quantity being predicted is not itself a near-cancellation. The full seventh
order holds inside two per cent to nine tenths of the field and 3.8 per cent at the corner,
where third order alone is out by twenty.

This also checks the finite-conjugate conversion per coefficient. All three come back from the
rays to 0.02 per cent — the length factor, the aperture variable and the field variable at a
finite conjugate, each confirmed separately rather than through an aggregate.

**Ladder1_Conic, f/8, 4° half-field, one figured surface.**

```
     H     traced%      3rd    3rd+5th   full 7th
  0.20     -0.0090      0.0%      0.0%       0.0%
  0.60     -0.0811     +0.1%      0.0%       0.0%
  0.90     -0.1822     +0.1%      0.0%       0.0%
  1.00     -0.2249     +0.2%      0.0%       0.0%
```

Third order describes this lens by itself, to two tenths of a per cent at the corner, and the
higher orders neither help nor hurt. The same finding the spot gives on the same design: the
order a lens needs is a property of the lens.

The three figured designs below are predicted from **Forbes' `tau20`**, which is what the tool
now uses whenever any surface is figured and Forbes applies - it declines a mirror - see the next
section for why.

**Cooke triplet optimised with an asphere, f/5, 20°.**

```
     H     traced%      3rd    3rd+5th   full 7th
  0.40     -0.0184     -0.6%      0.0%       0.0%
  0.60     -0.0419     -1.5%     -0.1%      -0.4%
  0.80     -0.0765     -4.2%     -1.8%      -2.7%
  0.90     -0.1001     -7.3%     -4.3%      -5.8%
  1.00     -0.1311    -12.6%     -9.2%     -11.3%
```

**The same lens at 24°, the aspheric testbed.**

```
     H     traced%      3rd    3rd+5th   full 7th
  0.40     -0.0277     -0.9%      0.0%      -0.1%
  0.60     -0.0636     -3.0%     -0.9%      -1.6%
  0.80     -0.1236    -11.2%     -7.9%      -9.9%
  0.90     -0.1762    -21.2%    -17.4%     -20.3%
  1.00     -0.2693    -36.4%    -32.6%     -36.1%
```

On both of these the seventh order is *worse* than the fifth in the outer field, **and it is
worse with the right `tau20`**. That is worth being clear about, because it would be easy to
read the aspheric-arrangement defect as the explanation and it is not: correcting `tau20`
moves the 24° corner from −34.4 per cent to −36.1, in the wrong direction. What runs out here
is the truncation, not the coefficient — these two designs are the ones whose distortion is a
small residual of cancelling terms, and the ninth order arrives into it.

**The two-surface asphere, r^4 to r^8, spot optimised.** The one figured design where the
seventh order helps, taking the RMS error across the ladder from 5.9 per cent to 5.3.

```
     H     traced%      3rd    3rd+5th   full 7th
  0.40     -0.0186     +3.8%      0.0%       0.0%
  0.60     -0.0400     +8.5%     -0.7%      -0.5%
  0.80     -0.0683    +13.0%     -3.9%      -3.3%
  0.90     -0.0863    +13.2%     -8.2%      -7.3%
  1.00     -0.1101     +9.6%    -16.0%     -14.8%
```

## Which route the seventh order comes from, and why

**Figured design: Forbes, unless it declines - it does not trace a mirror - in which case the
scheme's own value is used. Unfigured: either, and the scheme's own value is kept.** The tool
decides this itself and says which it used; there is no option and nothing to choose. The
reason is the table below.

The conclusion is the one `ForbesCoefficientsTests` already reaches by the full inversion. What
the table below adds is that a second and far simpler instrument reaches it too, on `tau20`,
with the ray-derived and the Forbes value quoted side by side.

**`E` and `E5` always come from Buchdahl's scheme**, figured or not, at either conjugate. The
Forbes inversion produces the tertiary only, and nothing is lost: the aspheric third and fifth
orders rest on closed-form conic surfaces — a printed answer, not a reconstruction — and the
rays return both to a part in ten thousand on every design in the table.

"This program's tau20" below means the Buchdahl route as **this repository implemented it
when the table was measured** — for a figured design that included an aspheric tertiary
arrangement Buchdahl never published and which had to be reconstructed here. Nothing in this
document bears on his theory; the question it settled was whose arithmetic the rays back, and the
two candidates were this repository's reconstruction and this repository's Forbes trace.

> **Since September 2026 the "this program" column is out of date for the figured rows.** The
> reconstruction was completed (see [verification.md](verification.md), "The aspheric tertiary arrangement") and
> the aspheric routine now gives Forbes' value on every figured row below, to 2E-10 or better;
> the rays confirm it (`TheAsphericSeventhOrderDistortionAgreesWithTheRays`). The table is kept
> as the evidence that located the defect.

| design | figured | this program | Forbes | from the rays |
|---|---|---|---|---|
| Cooke triplet, spherical | | 1.0835E-03 | 1.0835E-03 | 1.0833E-03 |
| the same at 250 mm | | -2.1817E-03 | -2.1817E-03 | -2.1821E-03 |
| Ladder1_Conic | conic | -5.1720E-08 | -5.1720E-08 | -5.1747E-08 |
| Ladder1_A4 | r^4 | -5.2964E-08 | -5.2964E-08 | -5.2983E-08 |
| Ladder2_FiguredSphere_Both | figured | 2.6903E-08 | 2.6903E-08 | 2.6889E-08 |
| **Ladder2_A4_Both** | r^4, two | **2.7276E-08** | **2.4213E-08** | **2.4209E-08** |
| Cooke + one asphere | r^4 | 2.6392E-04 | 5.1300E-04 | 5.1304E-04 |
| the same at 24° | r^4 | 1.0817E-03 | 2.1025E-03 | 2.0754E-03 |
| Cooke + PRMSA asphere *(inconclusive)* | r^4 | 3.0012E-04 | 3.0986E-04 | 3.1063E-04 |
| Cooke + r^4..r^8, two surfaces | r^4..r^8 | -9.8912E-04 | -2.5775E-04 | -2.5694E-04 |

**Wherever the two routes agree, the rays agree with both.** That includes four figured
designs, so figuring by itself is not what breaks it, and the recovery is not systematically
partial to Forbes.

**Wherever they disagree by more than the measurement's own error bar, the rays land on
Forbes.** Six designs, gaps between the routes from 11 per cent to a factor of 3.8, no
exceptions.

**One design settles nothing, and is listed as such.** On `CookeTriplet_PRMSA_START_LO_ASPHERE`
the two routes differ by 3.2 per cent while the recovery's own error bar is 5.1 per cent. The
rays sit nearer Forbes — 0.25 per cent against 3.4 — but a gap inside the error bar is not a
verdict, and the report says so on that design rather than counting it as a seventh case. It
is listed in the table below for completeness, not as evidence.

**And on a purely spherical design they never disagree.** Over the seven all-spherical
fixtures and the finite-conjugate triplet, the worst departure between the two routes across
all twenty tau is 3E-15 of the largest of them — roundoff — on seven of the eight, and 2E-08
on `Ladder2_FlatPlain_NearLimit`, which is a deliberately near-degenerate flat. The rays
return `tau20` on every one of the eight. There is no spherical case in which the two part
company, which is what confines all of this to the part of the scheme that had to be
reconstructed. `OnSphericalSystemsTheTwoSchemesAgreeOnAllTwentyAndTheRaysBackBoth` holds it.

`Ladder2_A4_Both` is the sharp case rather than the loud one. The two routes differ there by
12.65 per cent, not by a factor, and the rays give 2.4209E-08 against Forbes' 2.4213E-08 —
two parts in ten thousand. A factor of two could be almost any mistake; a twelve per cent gap
hit to that precision could not be a coincidence.

`E` and `E5` come back exactly on every design in the table, which is what makes the reading
specific: the trace, the conjugate, the field variable, the length conversion and the Gaussian
reference are shared by all three coefficients and are exercised identically by the two that
agree. What is left is the seventh-order **aspheric** arrangement, and nothing else.

That this program's aspheric tertiary arrangement WAS wrong was recorded in
[verification.md](verification.md), where it was established by disagreement with Forbes; the arrangement has
since been completed (see below). This table reached the same conclusion from rays, per coefficient, with
no series on the other side of the comparison. It also settles which of the two routes is the
wrong one, which a disagreement between them could not.

**What it does not say.**

*Nothing about Buchdahl's theory.* He never published the tertiary aspheric arrangement — the
scheme is in Sec. 85 of the monograph, the arranged table for it is not anywhere — so the
figured coefficients here come from a reconstruction made in this repository. That
reconstruction is what the rays disagreed with, and it has since been completed; the
disagreement is gone and the arrangement now agrees with Forbes and with the rays
([verification.md](verification.md)). Where he DID publish, this program reproduces
him entry by entry, `BuchdahlPublishedTableTests` does it every run, and on spherical systems
his scheme and Forbes' trace agree to 2E-13 on all twenty at both conjugates with the rays
confirming both. A defect in a reconstruction of an unpublished arrangement is a defect in the
reconstruction.

*Nothing about the other nineteen.* `tau20` is the only coefficient the pupil-centre ray
reaches, because it is the only one with no aperture in it. For `tau2` to `tau19` the evidence
remains the disagreement between the two routes, which does not say which is wrong.

*Nothing about where.* The comparison is of reported totals, so it says the aspheric increment
to `tau20` is wrong without saying which step of the arrangement produces it.

**How the recovery avoids fooling itself.** The estimate at *h* carries the next coefficient
times *h*², so it improves as the field shrinks — until the term being measured falls below
the tracer's own precision, after which it is noise. Both ends are wrong and neither
announces itself. So the ladder is walked in halves, adjacent pairs are extrapolated in *h*²,
and the pair whose two estimates agree best is the one reported, with that disagreement
carried out as the estimate's error bar. A recovery whose own two estimates disagree says
nothing about the coefficient and is printed without a ratio. On `Ladder1_Conic`, where
`tau20` is 5E-08 and would be lost in the tracer's noise at a small field, the criterion
selects H = 0.2/0.1; on the triplets, where the ninth order is large, it selects H = 0.1/0.05.

## What it comes to

**Distortion is predicted well by third order and hardly improved by the seventh, on a lens
whose distortion is small.** On the spherical triplet the seventh order is exact to a tenth of
a per cent through the inner field, where third order is already inside three — and both are
quoting a distortion of two hundredths of a per cent, which no one specifies to that
precision. Where the seventh order would tell a designer something, at the corner, it
overshoots.

**Where distortion is large enough to matter, the seventh order earns its keep.** The finite
conjugate is the case: -0.57 per cent at the corner, third order out by a fifth of that,
the full seventh inside four per cent, and every order improving on the last at every field.

**On a figured design the tool uses Forbes' `tau20`, and says so.** That choice was made while
this program's reconstruction of the aspheric tertiary gave a `tau20` the rays rejected. The
reconstruction has since been completed and now agrees with Forbes on every figured refracting
test design, a figured flat facing collimated light included, so the choice changes nothing there
(Forbes declines the mirrors, where only the rays can be set against it) — but the
report still names the route it used. On an unfigured design the two agree to roundoff on all
twenty and the choice is empty.

**Getting `tau20` right does not rescue the outer field, and that is the more useful finding.**
On the two aspheric triplets the corrected coefficient makes the corner slightly worse, not
better — −34.4 per cent to −36.1 at 24°. Their distortion is a small residual of cancelling
terms, and what has arrived there is the ninth order, which no amount of care with the seventh
will reach. The place to be careful about `tau20` is a design where distortion is large enough
to be worth predicting, and there the seventh order is worth having: the finite conjugate holds
inside four per cent at the corner where third order is out by twenty.

## Reproducing it

    abcalc <lensfile> --distortion-coefficients

and the MCP server offers the same as `distortion_from_coefficients`. Both print the identical report from one
formatter, including the coefficient recovery. The finite-conjugate rows above are the
`CookeTriplet` fixture with its object thickness changed from infinity to 250; everything else
in this document is a fixture in `tests/fixtures/lenses` as it stands.

`DistortionPredictionTests` holds the measurements as tests, including the convergence rates —
halving the field must divide the third-order error by four, the fifth-order by sixteen and
the seventh-order by sixty-four — which is what a coefficient being right means, and what no
tolerance at a single field can establish.
