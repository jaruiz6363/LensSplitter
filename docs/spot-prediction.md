# What each order buys

Third-, fifth- and seventh-order coefficients predict an RMS spot radius. This measures how
close that prediction gets, against rays traced by this program, on five lenses that behave
quite differently, at both conjugates.

## What this is not

**It is not a check that the coefficients are right**, and there is now a measurement saying
so rather than a caution. On the aspheric design below, Buchdahl's coefficients and Forbes'
disagree on tau15 by a factor of nearly five including its sign, and on tau20 by half - while
the spot they predict differs by one part in ten thousand. The disagreement sits in the
smallest coefficients, which a spot barely weights. See [verification.md](verification.md).

A spot is one number standing in for twenty, and errors that cancel or that land in terms the
sum hardly reaches would pass unnoticed. The coefficients are
established elsewhere and by other means — Buchdahl's own printed table, closed-form conic
surfaces, inverse ray tracing, an independent implementation, and Forbes' series trace; see
[references.md](references.md).

This answers a different question, and the one a designer actually asks: **how far into the
field is a prediction from these coefficients still worth quoting?**

## Method

`Prms.Value` evaluates the spot from the coefficient set. Four truncations of the same set are
used, so the only thing that varies is how much of the series is kept:

| column | what is kept |
|---|---|
| **3rd** | the five primary coefficients |
| **3rd+5th** | and the twelve secondary |
| **+B7 only** | and seventh-order SPHERICAL aberration alone, tau1 |
| **full 7th** | and the other nineteen tau |

The third column is there because B7 is not new. FIFTHORD prints it, and so does this
repository's fifth-order working. Separating it says what the twenty tertiary coefficients buy
that was not already available.

The reference is a ray trace by `RealRayTrace`, this repository's own, at the paraxial focus,
centroid-referenced, at the primary wavelength. Nothing else computes it.

The pupil is sampled in equal-area annuli, the *k*th ring at `r = sqrt((k - 1/2)/n)`. That
half-ring offset matters more than it looks. Taking `r = sqrt(k/n)` puts every ring on the
outer edge of the area it represents, which weights the pupil outward and inflates the spot;
it converges, but slowly and from above:

| rings × spokes | outer edge | equal-area midpoint |
|---|---|---|
| 12 × 24 | 0.014651 | 0.013685 |
| 24 × 48 | 0.014176 | 0.013695 |
| 48 × 96 | 0.013938 | 0.013698 |
| 96 × 192 | 0.013818 | 0.013698 |

The left column is still moving in the fourth figure at 18,432 rays; the right is settled at
1,152. The tables below use 48 × 96.

## Five lenses

**Cooke triplet, f/5, 20° half-field, all spherical.** Errors against the traced spot:

```
     H       traced      3rd     3rd+5th   +B7 only   full 7th
  0.00     0.013698   +27.0%       +4.7%      +0.7%      +0.7%
  0.20     0.013873   +33.6%       +4.9%      +1.1%      +0.8%
  0.40     0.014927   +49.6%       +6.0%      +2.8%      +1.5%
  0.60     0.017625   +67.5%       +6.2%      +4.2%      +1.9%
  0.70     0.019264   +79.1%       +6.6%      +5.2%      +1.6%
  0.80     0.020426   +97.7%       +9.2%      +8.2%      +1.4%
  0.90     0.020799  +127.0%      +14.5%     +13.9%      +0.4%
  1.00     0.023604  +132.7%       +5.9%      +5.8%     -12.6%
```

**This is the answer to what the seventh order buys, and it is not what B7 buys.**

On axis, B7 is the whole of it: +4.7 per cent becomes +0.7, and the other nineteen coefficients
add nothing, because on axis there is no field and spherical aberration is the entire seventh
order.

Off axis it inverts. At H = 0.9, adding B7 to the fifth order moves the error from +14.5 per
cent to +13.9 — near enough to nothing. Adding the other nineteen takes it to **+0.4 per cent**,
a factor of thirty-five. The whole of the improvement off axis comes from the coefficients that
carry field, and B7 - the one seventh-order quantity that was already available, from FIFTHORD
and from this program's own fifth-order working - contributes almost none of it.

Third order, meanwhile, is not an estimate of this lens at any field: out by a quarter on axis
and by more than a factor of two at the edge.

**The same triplet at a finite conjugate**, object 250 mm away, f/6.15 working, 20° half-field.

```
     H       traced      3rd     3rd+5th   +B7 only   full 7th
  0.00     0.016646   +31.4%       +5.2%      +0.7%      +0.7%
  0.20     0.018151   +30.9%       +4.4%      +0.5%      +0.6%
  0.40     0.022078   +34.1%       +3.5%      +0.9%      +0.4%
  0.60     0.026906   +47.3%       +3.7%      +2.1%      +0.3%
  0.80     0.029749   +81.4%       +6.8%      +5.8%      +0.5%
  0.90     0.029791  +110.5%       +8.8%      +8.0%      -1.2%
  1.00     0.033128  +119.0%       -3.9%      -4.2%     -14.8%
```

The best of the three: the full seventh order holds inside half a per cent from the axis to
eight tenths of the field, and 1.2 per cent at nine tenths. The B7 column behaves as it does
at infinite conjugate — the whole of the seventh order on axis, and almost none of it off,
+8.8 to +8.0 at H = 0.9 against +8.8 to -1.2 for the full set.

**This is also the only end-to-end check the finite-conjugate work has.** The coefficients
themselves were established by agreement between two routes, Buchdahl's scheme and Forbes',
which agree to 2E-13 — but two routes can share a mistake, and until now nothing had compared
either of them with rays at a finite conjugate. These are rays, traced independently of both.

What that can and cannot establish is worth being exact about. It cannot certify an individual
small coefficient, for the reason set out above. It can certify the CONVERSION, and that is
what most of the finite-conjugate work changed: the length factor, the aperture variable and
the field variable were all wrong before today, and any of them wrong scales the whole
prediction. A prediction that tracks traced rays to half a per cent across four fifths of the
field is not one built on a wrong conversion.

**Ladder1_Conic, f/8, 4° half-field, one figured surface.**

```
     H       traced      3rd     3rd+5th   +B7 only   full 7th
  0.00     0.002755    -0.1%       +0.0%      +0.0%      +0.0%
  0.20     0.002400    -0.0%       -0.0%      -0.0%      -0.0%
  0.40     0.002219    +0.3%       +0.1%      +0.1%      +0.1%
  0.60     0.004363    +0.2%       +0.2%      +0.2%      +0.2%
  0.80     0.008453    +0.1%       +0.1%      +0.1%      +0.1%
  0.90     0.011043    -0.0%       +0.1%      +0.1%      +0.1%
  1.00     0.013968    -0.1%       -0.0%      -0.0%      -0.0%
```

Here the whole question is moot, and the four columns are worth printing precisely because
they are identical. Third order alone is within a few tenths of a per cent everywhere; the
fifth, B7 and the other nineteen each add nothing, because on a slow lens over four degrees
there is nothing for them to add. Any argument that seventh order is generally necessary has
to survive this design, and does not.

**A Cooke triplet optimised with an asphere, f/5, 20° half-field.**

```
     H       traced      3rd     3rd+5th   +B7 only   full 7th
  0.00     0.001504   -80.4%      +33.7%      +4.5%      +4.5%
  0.20     0.002548   -27.9%      +22.2%     +11.0%      +5.3%
  0.40     0.006455    +8.4%      +11.9%      +9.8%      +6.1%
  0.60     0.012051   +29.6%       +9.3%      +8.7%      +5.4%
  0.80     0.015347   +80.4%      +22.8%     +22.7%      +9.9%
  0.90     0.013474  +159.9%      +54.6%     +54.5%     +21.7%
  1.00     0.011837  +265.0%      +84.5%     +84.8%     +10.9%
```

The same pattern, on a design where nothing predicts well in absolute terms. B7 alone is worth
a great deal on axis (+33.7 to +4.5) and nothing at all off it (+54.6 to +54.5 at H = 0.9);
the other nineteen then halve what is left, to +21.7. Third order is out by 80 per cent on
axis in the *other* direction, because the design has been optimised until the third-order
terms nearly cancel and what remains is almost entirely higher order.

**The same triplet with more aspheric freedom**, r^4, r^6 and r^8 on two surfaces, spot
optimised. f/5, 20° half-field.

```
     H       traced      3rd     3rd+5th   +B7 only   full 7th
  0.00     0.005171  +196.1%      -29.6%      -3.7%      -3.7%
  0.20     0.005488  +192.2%      -28.9%     -13.5%      -2.2%
  0.40     0.008040  +143.9%       -9.1%     -13.9%      +4.0%
  0.60     0.012953  +117.5%       +9.1%      +1.4%     +10.3%
  0.80     0.015578  +170.8%      +48.3%     +39.9%     +34.2%
  0.90     0.012698  +302.7%     +124.6%    +113.4%     +90.1%
  1.00     0.006752  +807.8%     +418.8%    +396.8%    +318.5%
```

**More aspheric freedom makes the prediction worse, not better, and by a long way.** It is the
worst predicted of the five and the only one where the seventh order is out by a factor rather
than a percentage.

The reason is in the traced column, not the predicted ones. This design's spot peaks at
0.015578 around eight tenths of the field and then collapses to 0.006752 at the corner — under
half its own worst zone, and under half what the single-asphere version manages at the same
point. It is the best-corrected lens here and the least predictable, which is not a
coincidence but the same mechanism twice: the optimiser had two more aspheric terms to spend,
it spent them driving the low orders towards cancellation, and what is left is residual of an
order the series does not reach. On axis the third order is out by 196 per cent *upward* while
the fifth is out by 30 per cent *downward*, both of them large multiples of a spot that is
itself tiny.

It is not our aspheric coefficients. Taking tau2..tau20 from Forbes instead of from Buchdahl's
arrangement moves H = 1.0 from +318.5 per cent to +310.0. The two routes are equally lost, so
what is missing is the ninth order and beyond.

## What it comes to

**The nineteen field-bearing coefficients are what the seventh order is for.** On the plain
triplet at H = 0.9 they take the prediction from +13.9 per cent to +0.4. B7 on its own, which
was available before any of this work, moves it from +14.5 to +13.9. That gap is the answer to
"does the tertiary set earn its keep": on axis no, and off axis by a factor of thirty-five.
The finite conjugate says the same, +8.0 to -1.2 at the same field.

**The order you need is nonetheless a property of the lens, not a number.** Of the five here,
one is described by third order alone, two need the full seventh to reach a per cent or so,
one is not well described at seventh, and one is not described at all. Quoting a general rule
— "fifth order is good to about here" — would be wrong on four of the five.

**And the designs that most need the seventh order are the ones it serves worst.** That is the
uncomfortable finding of this document. A lens with nothing much wrong with it is predicted by
third order; a lens corrected hard enough that the seventh order would tell you something is
also a lens whose residual has moved past the seventh order. The two aspheric triplets sit on
either side of the line — one still readable at six per cent through the middle field, one out
by a factor at the corner — and they differ only in how much aspheric freedom the optimiser
was given.

**Buchdahl said this, and declined to blame the aperture.** From paper III, §4:

> "it might be thought that the first two or three orders are insufficient for systems
> operating at higher apertures. This is however certainly not the case. The meaningfulness
> or otherwise of the aberration coefficients is determined by the rapidity of convergence of
> the series of which they are the coefficients; and this is not simply governed by the
> aperture. For one system the convergence may become hopelessly slow at f/10, for another
> only at f/2. The rate of convergence in fact depends almost certainly mainly on the
> magnitude of the angles of incidence and refraction within the system of the ray being
> considered."

The three lenses above are that paragraph, measured. Note also that his "first two or three
orders" *includes* the tertiary — he is defending the scheme he wrote paper III to compute,
not the fifth order on its own.

**One of his claims does not survive the aspheric design.** He continues:

> "It is probably the case for any given system that as apertures and fields are reached for
> which predicted values become seriously at variance with actual values the performance of
> the system falls below tolerable limits in any case."

That is a comfortable rule: where prediction fails, the lens is bad anyway, so nothing is
lost. It holds for the plain triplet, whose spot grows steadily out to the edge. It fails for
the aspheric one, whose traced spot *shrinks* from 0.0135 at H = 0.9 to 0.0118 at the corner
— its best off-axis performance is exactly where the series is worst, at 54 per cent error
for fifth order and 22 for seventh. A design corrected hard enough to be interesting is a
design whose residual is higher-order, and that is the case his rule does not cover.

The aspheric case, and the reference-plane check that rules out defocus as its cause,
are summarised in [verification.md](verification.md).

**The turnover at the very edge is real and is not a fault.** On the plain triplet the
seventh-order prediction is the best of the three everywhere until H = 0.9 and then the worst
of the three at H = 1.0, where it under-predicts by 13 per cent while the traced spot climbs
away. That is the ninth order arriving: the truncated series turns over while the real lens
does not. It is smooth, it is not moved by ray aiming, and nothing is being clipped. It does
mean the last tenth of the field on that lens is outside what seventh order can describe.

## Is the seventh order the ceiling, or just where we stopped reading?

The two aspheric triplets say the seventh order runs out on a hard-corrected design at large
field. That could mean two quite different things: that the series itself has stopped
converging there, or that it converges perfectly well and we stop reading it too early.

It is the second. `ForbesTrace` carries every order its truncation keeps; only
`ForbesCoefficients`, which reads named coefficients off it, stops at the seventh. So the
trace can be asked directly for a real ray, at increasing truncation, on the worst design in
the set — `CookeTriplet_SPOTM_START_LO_ASPHERE_A4_A8` at its corner field:

| trace degree | worst \|predicted − traced\| over the pupil |
|---|---|
| 3 | 134.2 µm |
| 4 | 11.0 µm |
| 5 | 11.8 µm |
| 6 | 6.4 µm |
| 7 | 4.0 µm |

One step past the seventh-order truncation is a factor of twelve. The content that the twenty
tau cannot express is there, it is well behaved, and this repository can already compute it —
what does not exist is the extraction of it into named coefficients, and a spot formula that
would consume them.

**So "seventh order is not enough for hard-corrected aspherics" is a statement about the
reading, not about the method.** Buchdahl's scheme stops here: the tertiary is the last order
he arranged, and the quaternary aspheric arrangement does not exist at all — paper IV reaches
quaternary spherical aberration and no further. The tertiary aspheric arrangement he also left
unpublished, but that one has been reconstructed and checked ([verification.md](verification.md)), so it is the
NINTH order and not the seventh that is the wall. Forbes has no such wall — the order is a
parameter.

**What would be needed, concretely, and one corner of it is now done.** The ninth order has more
than twenty coefficients, so it is not a matter of extending an array: it needs the polynomial
written out, the extraction generalised past its hard-coded degree seven, and `Prms` extended to
form a spot from the larger set. None of that is research. It is the same work again at the next
order, on machinery that has already been shown to carry it.

The exception is **ninth-order spherical**, which Buchdahl published separately — paper IV, three
pages, fourteen entries per surface appended to the tertiary scheme — and which
`QuaternarySpherical.cs` now computes, checked against his own printed table and against his
closed-form plane-surface identity. `ROBB.ZPL` carries it into a spot as a fifth column.

That matters for exactly one of the cases on this page and not the others. **On axis, spherical IS
the whole ninth order** — every other term of that order carries a power of the field — so the
axial prediction can now be completed rather than truncated. Off axis it is one term of more than
twenty, and the wall described above still stands. It is also spherical surfaces only: no aspheric
arrangement exists at the ninth order, so the hard-corrected asphere that this page finds
undescribed stays undescribed.

**Unless the order is reached the other way.** Buchdahl's is not the only route to a ninth-order
coefficient, and Forbes' series trace has no aspheric arrangement to be missing — the trace
already carries the order and only the reading stops at seven.
[forbes-ninth-order.md](forbes-ninth-order.md) scopes what that would take: thirty coefficients,
of which paper IV gives one, and a checking problem harder than the tertiary's rather than easier,
because at that order there is no second route left to disagree with.

**And a caveat on where this bites.** Every design here that the seventh order fails on is
f/5 at 20 degrees, and the one it comfortably describes is f/8 at 4. Speed and field vary
together across these fixtures, so nothing here separates them, and "fast lenses need higher
order" is not something this evidence can support on its own. Buchdahl's own claim is that
the rate of convergence depends mainly on the angles of incidence and refraction inside the
system rather than on the aperture — which is testable, would be a far better diagnostic than
the f/number, and has not been tested here.

## Reproducing it

The traced values here are this program's own, and they agree with the figures recorded
earlier in `TertiaryCoefficients` — 0.013698 against 0.013699 on axis, 0.023604 against
0.023603 at the corner — which settles that those were never taken from anywhere else.

The generating program is kept out of the repository because it is a few dozen lines of
scaffolding around `Prms.Value` and `RealRayTrace.Trace`; both are public, and the recipe
above is the whole of it.
