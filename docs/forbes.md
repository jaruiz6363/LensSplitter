# A second route for the aspheric case: Forbes' Lagrangian series

G. W. Forbes, *Order doubling in the computation of aberration coefficients*, JOSA **73** (6),
782 (1983).

## Why

Buchdahl's scheme is the only one in Focke's 1965 survey that reaches seventh order, and this
program's implementation of it agrees with real ray tracing to 0.003 per cent on spherical
systems. **Buchdahl also gives the aspheric scheme** — §65–66 for the D and L split and
(85.2)–(85.5) for the two passes — so nothing about the method was ever guessed. What he never
published is an *arranged table* for the aspheric case, the way Table I arranges the spherical
one, and that arrangement is what had to be re-derived: the one part of the work with no printed
answer to check against.

**It has been re-derived, and it is right.** `BuchdahlAsphericScheme` carries it, and it agrees
with the route described in this document on all twenty tertiary coefficients to between 2E-13
and 2E-10 on every figured refracting design tested, a figured flat facing collimated light
included. The real rays agree with both. (On a figured mirror this route declines - see *The
trace* - and the reflected rays are the check.) The same arrangement has since been transcribed independently into
`macros/BUCH7_ASPH.ZPL`, in a different language and inside OpticStudio, where it reproduces
`FORBES.ZPL` to every digit either macro prints on every refracting design they were both run on. See [docs/verification.md](verification.md) for what each of
those checks is worth. The aspheric case is not an open problem here, and neither this document
nor its title should be read as saying it is.

**What that leaves this route doing** is the more interesting question, and the answer is that it
is what settled the matter. A reconstruction cannot be checked against itself. Buchdahl's aspheric
arrangement needed a judge sharing none of its arithmetic, and the reason to build this one was to
have that judge before the reconstruction was trusted rather than after — which is what happened.
While the arrangement was still wrong, this route and the real rays said so together, and the
errors they found, 17 to 467 per cent across the ladder, were not ones that reading the algebra
had turned up. It stays in the program as the standing second opinion, and it is what the aspheric
numbers are checked against.

**That is its whole job at the seventh order, and none of its job one order up.** At the ninth
there is almost nothing left for it to be a second opinion about: Buchdahl published one
coefficient of thirty, paper IV's quaternary spherical, on spherical surfaces only, and no
aspheric arrangement exists at that order at any level of completeness. Forbes' route has no
arrangement to extend — the order is the truncation degree, figuring is ordinary coefficients in
`f_i`, and the code does not branch — so **the property that made it a fit judge is the same one
that makes it the only route that reaches the ninth order on a figured refracting design at
all** - on a figured mirror, which it declines, no route reaches it. What it
would take, what it could be checked against and what it would not give are
[docs/forbes-ninth-order.md](forbes-ninth-order.md). Nothing of it is implemented; the trace
already carries the order and only the reading stops at seven.

Forbes' formulation removes the difficulty rather than solving it. He writes the *i*th surface as

    x = f_i(y . y)

with `f_i` a power series. A sphere, a conic and an even asphere differ only in the coefficients
of `f_i`, and are traced by identical code. There is no D and L split, no hat and check pass, and
no carrying ratio — the entire class of fault that had to be chased through the arranged tables
cannot be expressed in his formulation at all. That is what makes it a fit judge, and it is the
reason it was built. Speed was never the reason.

## Why not order doubling

Forbes' headline result is that intercepts known to order *M* determine the characteristic
function to order *2M*. That economy is for orders far beyond ours. Everything is expanded in the
three rotational invariants

    p = y0 . y0 ,   k = y0 . b0 ,   u = b0 . b0

each of which is quadratic in the ray coordinates, so a term of degree *m* multiplying `y0` or
`b0` is of order `2m + 1`. Degree 1 is the primary aberrations, degree 2 the secondary, degree 3
the tertiary. Twenty coefficients per series. The direct Lagrangian trace is small enough that
the concatenation machinery order doubling needs would be the larger part of the work, and the
larger part of the risk.

## The trace (Forbes §3.A)

Per surface, with `y_i` the intercept and `b_i` the direction,

    y_i = S_i y0 + T_i b0 ,      b_i = V_i y0 + W_i b0                          (3.1)

where `S, T, V, W` are series in `p, k, u`. Then

    p_i = S^2 p + 2 S T k + T^2 u
    k_i = S V p + (S W + T V) k + T W u
    u_i = V^2 p + 2 V W k + W^2 u

Transfer is (3.2)–(3.3), and is solved *recursively by order*: coefficients of degree `m+1` follow
from those of degree `m` or less, because the argument of `f_{i+1}` is built from the series
already known. Refraction is Snell in the form (3.4), with the surface normal from
`df/dy = 2 y (df/dp)`. Forbes also gives closed forms for the spherical case, (3.9)–(3.14), which
are worth having as an independent check on the general path.

**Mirrors are NOT supported, in this trace or in `FORBES.ZPL`.** Forbes carries a reflection by
letting `n` change sign, but the root of (3.4) that picks the reflected cosine then has to be
chosen explicitly, and that is not written. Both implementations DECLINE a reflecting system:
`ForbesCoefficients.Invert` returns null and the macro stops with a message. Until September 2026
neither did - the trace was handed unsigned indices, its own guard against a sign change never
fired, and a mirror was traced as a refraction into the same medium, returning zeros without
complaint (see verification.md, *Mirrors*). On a mirror the seventh order is therefore checked
against real rays instead, which do reflect; Buchdahl's scheme is the only series route there.

## Staging, and how each stage is checked

1. **`InvariantSeries`** — the truncated series ring in `p, k, u`. *Done.* Checked on algebraic
   identities to machine precision, not to a tolerance chosen to pass: difference of squares,
   `s · 1/s = 1`, `sqrt(s)^2 = s`, the binomial coefficients of `(1-u)^(1/2)` term by term, and
   the spherical sag series against its closed form.

2. **The trace, spherical only.** Read the tertiary coefficients off `y_{N+1}` and require them to
   reproduce what `BuchdahlTableI` already gives. This is the stage that earns the rest: the
   spherical answer is validated to 0.003 per cent against the ray inversion, so a new
   implementation that reproduces it is proven before it is used anywhere it cannot be checked.
   Anything less and we would have two unvalidated routes rather than one.

3. **Aspheres.** Extra coefficients in `f_i` and nothing else. Check against
   `Ladder1_A4`, `Ladder1_Conic`, `Ladder1_FiguredSphere` and the figured-sphere fixtures, all of
   which the Buchdahl route already gets right, before going to the ones it does not —
   `Ladder2_A4_Second` at 6.85 per cent, `Ladder2_FiguredSphere_Then_A4` at 7.75.

   **Those two figures are the state this plan was written in, and they no longer hold.** The
   aspheric arrangement has since been reconstructed and agrees with this route to between
   2E-13 and 2E-10 on every figured design in the ladder and on the three aspheric triplets;
   the same arrangement is in `macros/BUCH7_ASPH.ZPL`, where the two agree on all twenty
   coefficients to every digit either macro prints. The staging above is left as written
   because it is what the work was planned against, and because step 4 is the reason the
   disagreement could be adjudicated at all rather than argued about.

4. **Adjudication.** Where the two routes disagree, the ray inversion decides. It recovers all
   twenty coefficients separately from real traced rays and is independent of both.

## What this does not replace

Buchdahl's route stays, and on a figured design it is still the one the coefficients come from.
It is validated at both conjugates, it is fast, and it reports **per-surface** contributions split
into intrinsic, figuring and induced - which is what makes the tool useful for designing rather
than only for checking, and which Forbes' route does not give at all: a series trace through the
whole system produces system totals, and the question "which surface, and is it that surface's
own fault" cannot be put to it.

Forbes is the second opinion, and that is a permanent job rather than a probationary one. It
adjudicated the aspheric arrangement once; it stands ready to adjudicate the next change to it,
and `ForbesCoefficientsTests` puts the question on every build.

**Except on a mirror**, which this route declines (see *The trace* above). There the seventh
order has no second series opinion at all - only real rays, which reflect and which it has been
checked against on a parabola and a spherical mirror.

**At the seventh order.** The division of labour above is a seventh-order one throughout, and it
is stated that way because one order up it reverses: there is no Buchdahl route to be second to,
and the roles it would have to take on — sole source, with only real rays to check it — are
harder ones than adjudication. [docs/forbes-ninth-order.md](forbes-ninth-order.md) is about that
and says so rather than promising it.

## The extraction, and why the inversion is gone

The coefficients are the homogeneous degree-seven part of the transverse aberration in the
normalised pupil and field - equivalently the `s^7` term of a ray shrunk by `s` in both. A traced
ray gives no way to isolate that except by watching how it shrinks, which is why the real-ray
route runs a ladder of twelve scales and fits eight odd powers through it. A series trace can
write it down.

It cannot, however, be read off the degree-three part of `S` and `T` directly. Under that scaling
the height at the input base plane IS exactly linear - the walk back from the entrance pupil
contributes `-ep tan(field)`, and the tangent is what the inversion makes linear in `s` - but the
direction is `sin = tan / sqrt(1 + tan^2)`, whose cubic and quintic terms carry lower-degree parts
of `S` and `T` up into the seventh. Dropping them would be a quiet error of order the field
squared. So the whole construction is carried as a series in the scale, `ScaleSeries`, and the
seventh coefficient read off. That takes all of it, exactly.

The mapping onto the twenty coefficients is then the same linear model the real-ray route uses,
lifted out of it as `CoefficientInversion.ModelRow`. Nothing here rests on a change of basis
derived by hand, which was the point.

**What it bought.** The residual - how well the twenty coefficients reproduce the data they were
solved from - falls from about 1E-6 to 1E-16. That is not a refinement: it says the degree-seven
aberration of the trace lies exactly in the span of the twenty rather than approximately in it,
and it is the guard on the whole path. It also removes the readout floor. The earlier
Forbes-against-rays figures were limited by what the ladder could separate, not by what the
method could reach; on every fixture where Table I is right, the two now agree identically.

## Cost

Release build, best of three runs of five hundred, one machine — read the ratios, not the
absolutes.

    design                          surfaces   Buchdahl   Forbes trace   Forbes + inversion
    Ladder2_Sphere                  3  sph       0.0329       0.1404            3.8785
    CookeTriplet                    6  sph       0.0656       0.3652            3.9940
    TertiaryTestbed_Triplet24       6  asph      0.5300       0.3664            3.9942
    SPOTM_..._A4_A8                 6  asph      0.5381       0.3590            3.9570

Three things this says.

**On spherical systems Buchdahl wins, four to six times over.** It is a closed-form scheme
evaluating a table; Forbes traces a ray through a truncated series ring, and twenty coefficients
per surface per quantity is simply more arithmetic than the table needs.

**On aspheric systems Forbes wins, by about half again.** Not because it got faster - its cost
barely moves, 0.3652 spherical against 0.3664 and 0.3590 aspheric, because an asphere is only
more coefficients in `f_i` and the code does not branch. Buchdahl's cost is what changes: 0.066
to 0.53, eight times, for the spherical twin the aspheric increments need and the two-pass the
tertiary runs on top of it. The route with no special case for figuring does not pay for one.

**The 3.99 ms column is scaffolding, not Forbes.** That is the trace plus the ray inversion used
to read the coefficients off it, and the inversion is the whole of it - the oracle costs 3.6 ms
on its own rays. It exists so that no change of basis had to be derived by hand, which was the
right trade while establishing whether the route works. Reading the twenty coefficients directly
off the degree-three part of `S` and `T` would cost nothing measurable and put the real figure at
the 0.36 ms of the trace.

Scaling with truncation is steep: degree four costs about 3.6 times degree three, close to the
ratio of the squares of the term counts, 35^2 over 20^2. Going beyond the tertiary this way would
start to want Forbes' order doubling after all.

## Per-surface intrinsic and induced

`ForbesPerSurface` splits each surface's share three ways. Write `A(i)` for the coefficients when
surfaces 1 to `i` act in full and every other step is linearised, and `B(i)` for when surface `i`
alone acts. Then

    surface i contributes  A(i) - A(i-1)
    intrinsic(i)           B(i) with the figuring taken off surface i,  less A(0)
    aspheric(i)            B(i) - that
    induced(i)             A(i) - A(i-1) - (B(i) - A(0))

All of it telescopes by construction, not by arrangement: the contributions and the reference sum
to the totals to 3E-17, the three parts sum to each contribution to 1E-17, and the first surface
induces exactly zero because nothing precedes it. The aspheric part is identically zero on an
unfigured surface and non-zero on a figured one.

**What makes this natural here.** "The aberration already present when light reaches this surface"
needs no derivation - it is the non-constant part of the trace state. Suppressing it is
truncation; suppressing a surface's own contribution is linearising its step. Neither needs a
scheme, and neither can be arranged wrongly.

### The reference term

`A(0)`, the all-linearised system, is not zero, and it is reported separately rather than charged
to a surface. It is one coefficient - `tau20`, pure field, seventh-order distortion - because a
paraxially perfect system launched with direction cosines sends a ray at angle `theta` to about
`efl sin(theta)` where the coefficients are referred to `efl tan(theta)`. On CookeTriplet it is
-1.32E-2 against a system total of 1.08E-3, so it is not small, and hiding it in the first surface
would misreport that surface badly. Both routes share the convention - it is why their totals
agree - so it belongs to the reference, not to the optics. A test holds it to `tau20` alone: if it
ever appeared elsewhere, a linearised step would not be linear and the decomposition would rest on
nothing.

### It does not agree with Table I's per-surface split, and is not expected to

Both sum to the same system total, and on spherical systems those totals agree to 0.003 per cent.
The attributions differ, and substantially - on CookeTriplet, surface 2 comes to 7.90E-5 here
against -2.32E-4 there, opposite in sign:

    surf   Forbes total   Table I x scale     Forbes intrinsic   Table I intrinsic
       1    -8.87906E-05     -7.13570E-05         -8.87906E-05        -7.13570E-05
       2     7.90439E-05     -2.31907E-04          2.85668E-05        -1.00270E-05
       3     1.08466E-03      1.40743E-03          4.15080E-04         4.02173E-04
       4     1.34804E-03      2.12528E-03          8.05585E-05         2.05915E-04
       5     5.78023E-05     -6.49783E-04          6.15486E-06        -4.50055E-07
       6    -7.99305E-04     -8.98211E-04         -1.09471E-03        -1.16213E-03

A total can be attributed across surfaces in many ways once the system is nonlinear, and the two
schemes choose differently; neither is wrong. But it does mean the two per-surface tables are not
interchangeable, and a reader given both without this warning would reasonably think one of them
was broken.

**How to settle which convention to publish.** At PRIMARY order the question does not arise:
third-order aberration is additive, nothing is induced, and every scheme must give the same
per-surface numbers. Running this decomposition at degree one and comparing with the Seidel
contributions would say whether the Forbes split reduces to the standard one where the standard
one is unambiguous. That check is not built, and it is the thing to build before either table is
put in front of anyone.
