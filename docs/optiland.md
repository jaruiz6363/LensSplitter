# Optiland as a cross-check

[Optiland](https://github.com/HarrisonKramer/optiland) is an open-source optical design package
in Python. It is embedded here, through Python.NET, as a second implementation to check this
program against: `src/AberrationCalculator.Optiland`, following the embedding in
[RelativeIlluminationCalculator](https://github.com/jaruiz6363/RelativeIlluminationCalculator).

Measured on 2026-09-22 with optiland 0.6.2, the current release (its `__version__` string still
says 0.6.1), numpy 2.5.3 and Python 3.12.8. Nothing in Optiland was modified.

## Setting it up

    .\tools\setup-python.ps1        # embeddable Python + optiland, into python-embed\ (gitignored)
    dotnet test --filter "FullyQualifiedName~Optiland"

`ABCALC_PYTHON_HOME` points it at another Python that has optiland installed. Without either,
the Optiland tests are reported as skipped, with the reason, so that a fresh clone's suite stays
green without counting them as checked.

## What is handed over

The lens is built inside Optiland from the prescription this program parsed, not read by
Optiland's own importers: radii, thicknesses, conics, the even-asphere coefficients from r² up,
the stop, and each medium's index at the primary wavelength as an `IdealMaterial`. Optiland
0.6.2's `.zmx` import resolves a glass name without the catalog the file names - Schott's F4
arrives as CDGM's F4, 0.35 % off in index - so an imported lens need not be the lens this program
analysed. The image surface is put flat at the paraxial focus, which is where the coefficients
are referred to.

## What Optiland has, and what it does not

| | Optiland 0.6.2 |
|---|---|
| Seidel sums S1..S5 | yes, `optic.aberrations.seidels()`; per surface through its `ThirdOrderAberrations` terms |
| fifth order | **no**. Its `aberrations` package stops at third order, and nothing else in it computes one |
| seventh order | no |
| real-ray trace | yes |

So the fifth order is checked the way this repository checks everything no other program
computes: **Optiland's rays are put through `CoefficientInversion.InvertThirdAndFifth`**, which
scales pupil and field together, fits the odd polynomial in the scale and reads the s³ and s⁵
coefficients off each ray shape, then solves for B..E and B5..E5 in Buchdahl's basis. The rays
are Optiland's; the reading of them is this program's, and is gated first on this program's own
rays (`LowerOrderInversionTests`), so that the only thing new when Optiland's rays go through it
is the rays.

Those rays are launched from THIS program's paraxial entrance pupil, not by Optiland's aiming.
That is deliberate, because of the second finding below.

## Results

### Fifth order: Optiland's rays reproduce Buchdahl's closed form

All five third-order and all twelve fifth-order coefficients, on 45 designs - every design on
disk that has an object at infinity and a field and that the bridge builds, including every
aspheric one between the end surfaces, the figured flat in collimated light, immersed image
space, the r² fixtures and the parabolic mirror
(`OptilandTests.ThirdAndFifthOrderFromOptilandsRaysMatchBuchdahl`). Worst over each order,
relative to that order's largest coefficient:

| | third order | fifth order |
|---|---|---|
| most designs | 1E-12 to 5E-11 | 3E-9 to 6E-7 |
| Cooke triplets with aspheres | 3E-9 to 9E-9 | 7E-7 to 5.4E-6 |
| `TertiaryTestbed_Triplet24`, the worst | 5.1E-8 | 1.3E-5 |

This program's own rays land on the same floor on the same designs, so the residual is the scale
ladder's separation of the fifth order from the seventh and ninth, not a disagreement. On the
Cooke triplet:

| | Buchdahl | from Optiland's rays |
|---|---|---|
| B5 | 7.69191153E-03 | 7.69190735E-03 |
| M3 | 3.18219890E-02 | 3.18220190E-02 |
| N2 | −3.25384941E-02 | −3.25384484E-02 |
| Pi5 | 5.73301167E-02 | 5.73301100E-02 |
| E5 | 1.48427717E-03 | 1.48427681E-03 |

and on `F3_conic_a4_a6_a8`, a conic carrying r⁴, r⁶ and r⁸, every one of the seventeen within
1E-10 absolute, 1E-8 of the largest.

The rays themselves: over a grid of three fields, three pupil radii and three azimuths,
Optiland's land within 1E-13 mm of this program's on spheres and conics, within 2E-9 mm on the
other aspheric designs, and within 2E-8 mm on the aspheric Cooke triplets - Optiland iterates to
an aspheric surface, and that is its convergence (`OptilandsRaysLandWhereOursDo`).

### Seidel: surface by surface, once the sign is turned

On the 16 designs Optiland's Seidel analysis can fairly be asked about - object at infinity and
no even-asphere terms - all five sums agree with this program's **for every surface**, to 1E-14
of the largest sum (`SeidelSumsAgreeSurfaceBySurfaceWithTheSignTurned`). Conics are included, and
so are the parabolic mirror and the flat face in collimated light - the last only since the
defect below was fixed.

## What the comparison found in Optiland

**1. Its Seidel sums have the opposite sign.** `seidels()` returns −S1..−S5 in Welford's
convention, which is the one this program and OpticStudio's Seidel analysis use. On a positive
singlet, whose spherical aberration is undercorrected, Welford's S1 and OpticStudio's SPHA are
positive and Optiland's is negative (`OptilandsSeidelSignIsTheOppositeOfWelfords`). A
convention, not an error, but it is not stated anywhere a user would see it.

**2. Its paraxial trace ignores an r² coefficient.** Optiland's paraxial refraction is
`power = (n2 - n1) / geometry.radius`: the surface's power from its radius alone. An r²
coefficient is a curvature change, c + 2A2, which its REAL-ray trace carries (the sag includes
it) and its paraxial trace does not. On `F8_r2_conic_a4_a6_a8` Optiland's focal length is
78.037505, the value for the lens without the term, where OpticStudio, this program and
`BUCH7_ASPH.ZPL` give 77.419426. `F9`, the same surface written as a shifted sphere, it gets
right (`OptilandsParaxialTraceLeavesOutTheR2Term`). This is the defect FIFTHORD has, and LensHH-LT
had until 1.0.156 (see verification.md, *The r-squared term*). Everything paraxial follows from
it - pupils, the aiming, and the Seidel sums - which is why rays are launched here from this
program's pupil rather than by Optiland's aiming.

**3. Its Seidel sums leave out the even-asphere terms.** The figuring term in its third-order
code is `(n' - n) k c³ yᵃ ȳᵇ`: the conic alone. An r⁴ coefficient contributes to third order
exactly as a conic does and never enters. `F3` - F1's conic singlet with r⁴, r⁶ and r⁸ added -
gets F1's sums from Optiland to the last digit, while this program - whose third order on F3
agrees with the recorded FIFTHORD reference - finds S1 moved from 1.79E-02 to −6.89E-03
(`OptilandsSeidelSumsLeaveOutTheEvenAsphereTerms`). On an aspheric design Optiland's Seidel
analysis should not be used; its rays are fine.

**4. At a finite conjugate its paraxial chief ray is wrong.** `Paraxial.chief_ray` traces a unit
ray backward from the stop and scales it to the object height. The backward trace stops at
surface 1 and never makes the last transfer to the object plane - on `G0_finite_no_r2` its
returned heights are 2.0, 2.375, 2.375, the last entry repeating surface 1 - so the ray is
scaled as though the object sat on the first surface. The object-space chief slope comes out as
height/EPL, 12/33.79, instead of height/(object distance + EPL), 12/433.79. Every Seidel sum that
uses the chief ray is wrong with it: on G0, Optiland's S5 is −42.3 against 3.1E-04 (G0 carries
r⁴ to r⁸ terms as well, but they cannot account for five orders of magnitude). With the stop
ON the first surface EPL is zero, the scale divides by zero, and every sum is NaN
(`E0_finite_flat`; `OptilandsChiefRayAtAFiniteConjugateMissesTheObjectDistance`).

Worth reporting upstream, all four - three defects and the undocumented sign convention - to
[HarrisonKramer/optiland](https://github.com/HarrisonKramer/optiland). The two import issues
found by RelativeIlluminationCalculator - the glass catalog, and dropped surface apertures - are
recorded in that repository's `docs/optiland-0.6.2.md`.

## Defects in THIS program that came to light during the comparison - all now fixed

To be plain about what these are and are not. None of them means OpticStudio was wrong: it never
disagreed with this program, and the only OpticStudio comparison on a mirror before this was
FIFTHORD's third and fifth order on the parabola, which this program matched then and matches now.
Nor was Optiland needed to find them. The mirror defects had symptoms in this repository already -
the parabola's ray fit failing with a residual of 0.75, dismissed as noise, and its seventh order
NaN, passed silently by two sweeps - and the flat-face one is exposed by an identity, S5 = 2 E n'u',
that this program had every means to check and did not. They were missed because that data was not looked at critically enough.
Optiland's rays landing 20 mm away on the parabola reinforced that something was amiss with
reflection; that is the extent of its part.

Both showed up as disagreements with Optiland, and a disagreement alone does not say which side
is wrong. Each was settled by evidence that does not involve Optiland at all, and those tests run
on every build, embedded Python or not.

**A flat face in collimated light lost its Seidel distortion.** The Seidel S5 and Buchdahl's E
are the same aberration in two normalisations, S5 = 2 E n'u', and the identity holds to six
figures on every ordinary design here. E is confirmed independently by real rays - this program's
own, put through the inversion. On `Ladder2_FlatPlain`, before the fix:

| | flat face | same face at R = 1E10 |
|---|---|---|
| E from real rays | −7.049679E-03 | −7.049679E-03 |
| distortion of the real chief ray at full field | −7.050071E-03 | −7.050071E-03 |
| S5, this program | **−5.335812E-04** | +5.267021E-04 |
| S5 / (E n'u') | **−2.03** | 2.000000 |
| S5, Optiland (sign turned) | +5.267021E-04 | +5.267021E-04 |

The rays see the same distortion on both designs, so S5 had to be +5.267E-04 on both. The cause
was in `SeidelCoefficients`: the marginal ray meets the flat face at normal incidence, so A = 0
and the distortion formula (Ā/A)(S3 + S4) is 0/0. It set that surface's S5 to zero, where the bent
face contributes +1.060E-03 - and because S4 is zero on a flat, it did not list the surface in
`DistortionSuppressedAt` either, so the omission was silent.

**The fix.** The 1/A is only apparent. With u = A/n − yc on both sides of the surface,
Δ(u/n) = AΔ(1/n²) − ycΔ(1/n), and with H = Āy − Aȳ, Ā²y² − H² = Aȳ(2Āy − Aȳ); so

    S5 = −Ā³ y Δ(1/n²) + Ā ȳ c (2Āy − Aȳ) Δ(1/n)

with the A divided out exactly, finite everywhere. It is used where A = 0; the quotient is kept
everywhere else, so no other design computes a different bit. `DistortionSuppressedAt` and the
report's note about it are gone, since nothing is suppressed any more. On the flat design S5 is
now +5.267021372E-04 against +5.267021409E-04 at R = 1E10, and the identity reads 2.000000000
(`LowerOrderInversionTests.SeidelDistortionOfAFlatFaceInCollimatedLight`). The closed form is
held against the quotient on the 147 surfaces of the fixtures where both are defined
(`TheDistortionFormWithoutOneOverAIsTheQuotient`), and the design has joined the Optiland sweep.

**`RealRayTrace` did not reflect.** A paraboloid images an axial point at infinity perfectly at
every aperture, so every axial ray must cross the focal plane at y = 0 - a closed-form answer, no
program needed. On `F4_parabolic_mirror`, `RealRayTrace` landed them at 10 mm (half pupil) and
20 mm (full pupil): exactly the heights they were launched at. It refracted at the mirror between
equal indices and the ray carried straight on.

**The fix.** A reflection branch in the trace's refraction step, d' = d − 2(d·n̂)n̂. Nothing
downstream needed changing: the intersection, the transfer by the file's negative thickness and
the image plane are all written along the ray. Every axial ray on the parabola now lands within
1E-12 of the axis (`AParaboloidFocusesEveryAxialRayOnTheAxis`), and over the full grid the rays
agree with Optiland's to 7E-15 mm. `RealRayTrace` is compiled into the differentiating build too,
so the optimiser's real-ray operands now reflect as well.

**What the rays then showed about the coefficients.** With the mirror traced correctly, all
seventeen third- and fifth-order coefficients recovered from the rays came out exactly the
negative of Buchdahl's, the zeros included. That is a change of frame, not an error - an error
would not flip all seventeen together: Buchdahl carries a reflection in the sign of the index,
and his image-space transverse aberration is measured along an axis the mirror has reversed,
while the ray trace, like OpticStudio and Optiland, keeps the global frame. The inversion now
multiplies the landings by the sign of the image-space index (−1 after an odd number of
reflections, +1 otherwise, so no refracting design changes by a bit), and the parabola agrees with
Buchdahl to 2E-10 in the third order and 1E-6 in the fifth, from this program's rays and from
Optiland's. That rule is established on one reflection by the parabola and on two by Thompson's
two-mirror telescope (`ThompsonTelescopeTests.TwoReflectionsRestoreTheFrameInEveryOrder`), where the
image-space index is positive again, nothing is turned, and all three orders agree with the rays.
Only the OpticStudio macros remain unmeasured on two reflections (see verification.md, *What is
still open*).

**The seventh order was NaN on the mirror - fixed too, and it hid two more.** Buchdahl's
τ2..τ20 came out NaN on the parabola, before any ray was involved. `TertiaryCoefficients.Attach`
handed the Table I scheme the plain indices, so the mirror was a curved surface with no index
step - no power at all - while the paraxial data it was scaled by said f = 100. The fifth-order
code never had the problem because it reads the signed indices from the paraxial trace. Given
signed indices (negated after an odd number of mirrors, returned untouched when nothing
reflects) the scheme is finite; and with the image index taken as |N'| in the length factor, as
the fifth-order code already takes it for the F/number, the seventh order lands in the same frame
as the third and fifth instead of negated - which matters, because `Prms` multiplies them.

The reference is the reflected real rays. At the design's own half degree the field-dependent τ
are too small to stand above the ray inversion's floor of about 1E-9; at ten degrees every one
does, and all nineteen agree with the rays to 6E-6 of the largest, τ13..τ20 being zero from the
scheme and the floor from the rays (`ParabolicMirrorTests.TheSeventhOrderOfAMirrorIsFiniteAndAgreesWithRealRays`).

Two sweeps had been passing the mirror hollowly, because every comparison with NaN is false:

- **Forbes' series trace ignored the mirror.** `ForbesTrace` refuses a reflection, recognising
  one by the index changing sign - and it was handed the plain indices, so it never saw one and
  traced the parabola as a refraction into the same index: every τ zero. It now gets signed
  indices, so its own guard fires, and `ForbesCoefficients.Invert` declines a mirror outright.
  The report used Forbes' τ20 on figured designs, so on a mirror it would have printed that zero;
  it now falls back to the scheme's. `CoefficientSweepTests` cross-checks the mirror against the
  real rays instead - which it used to skip, because the non-reflecting rays left a fit residual
  of 0.75.
- **PRMSA has a kink on a perfect mirror.** The parabola's axial spot is exactly zero, so an r⁴
  term makes it |c·A4|: a cone. At its tip the analytic derivative is one side's slope and a
  central difference averages to zero, and neither is wrong. `DerivativeSweepTests` now moves a
  figuring variable that sits at exactly zero off the kink (A4 = 1E-9) when a PRMSA case
  vanishes, and differentiates the mirror there - which it now does for the first time,
  real-ray operands included, since the reflection is in the differentiating build too.

## The macros, which had the same mirror defects

The OpticStudio macros were written by the same hands and had the same exposure, in macro form.
`BUCH7`, `BUCH7_ASPH`, `ASPHWHERE` and `STRESS` sign their indices through `ISMS` inside the
surface loops, so none had the NaN. But:

| macro | on a system with an odd number of mirrors | change |
|---|---|---|
| `BUCH7_ASPH` | third and fifth scaled by \|N'\|, seventh by signed N': the seventh in the opposite frame from the orders beside it | \|N'\| for both |
| `BUCH7` | signed N' for everything: consistent, but every total negated against FIFTHORD | \|N'\| for both |
| `FORBES` | no `ISMS` at all: the mirror traced as a refraction into the same medium | mirrors declined |
| `RAYINV` | OpticStudio's rays in one frame through the mirror: every coefficient negated | landings turned by the parity of `MIRROR` surfaces |

`ROBB` calls `BUCH7`, and a spot is quadratic in the coefficients, so a consistent negation
never reached it; `ASPHWHERE` and `STRESS` scale nothing into a frame.

**Run in OpticStudio on 22 September 2026, and every run passed:**

| fixture | macro | result |
|---|---|---|
| F4 | `BUCH7_ASPH` | all thirty-seven match the C# program to every printed digit; the third and fifth unchanged, the tau with the signs reflected rays give |
| F4 | `FORBES` | declines the mirror |
| F4 | `RAYINV` | third exact, fifth to 3E-5; the seventh at the fit's floor, as expected at half a degree |
| F4 | `BUCH7` | declines (F4 has one surface, and is figured) |
| F10 | `BUCH7` | all thirty-seven to every printed digit, F/# −2.5; the dummy plane contributes exactly zero |
| F10 | `BUCH7_ASPH` | identical to `BUCH7` on every printed digit |
| F10 | `RAYINV` | third exact, fifth to 1E-6, seventh to 1-4E-3 of the largest τ - the size of what it returns for the τ that are exactly zero |
| F10 | `FORBES` | declines the mirror |

`RAYINV` needed a second fix during these runs. Its first version read the reflection parity
from the marginal ray's direction cosine after the last surface; that ray is traced with
`PARAXIAL ON`, where `RAYN` is not the direction of travel, so the flip never fired and F4 came
back negated. It now counts the surfaces whose glass is `MIRROR`, as the C# program does.
OpticStudio's ray fit is also about a hundred times noisier than the C# one on F10 (a few 1E-3
against 1E-5 on the seventh order), which is resolution, not disagreement.

The values, from the C# program - Buchdahl's closed form for `BUCH7` and `BUCH7_ASPH`, the ray
inversion for `RAYINV`:

`F10_spherical_mirror.zmx` - a sphere, R = −200, stop at the mirror, EPD 40, field 5°, EFL 100,
F/# −2.5, with a dummy plane 50 mm in front: `BUCH7` needs two surfaces, and a plane in air
changes nothing. `BUCH7`, `BUCH7_ASPH` and `RAYINV` agree with this; `FORBES` declines.

| | Buchdahl | real rays | | Buchdahl | real rays |
|---|---|---|---|---|---|
| B | 1.000000E-01 | 1.000000E-01 | B7 | 4.625000E-05 | 4.625009E-05 |
| F | −8.748866E-02 | −8.748866E-02 | τ2 | −1.290458E-04 | −1.290453E-04 |
| C | 7.654266E-02 | 7.654266E-02 | τ3 | −8.858227E-05 | −8.858183E-05 |
| Pi | −7.654266E-02 | −7.654266E-02 | τ4 | 1.817888E-04 | 1.817907E-04 |
| E | 0 | 1E-14 | τ5 | 2.678993E-05 | 2.679213E-05 |
| B5 | 2.250000E-03 | 2.250000E-03 | τ6 | 1.913567E-04 | 1.913516E-04 |
| F1 | −5.030598E-03 | −5.030598E-03 | τ7 | −1.439772E-04 | −1.439780E-04 |
| F2 | −3.062103E-03 | −3.062103E-03 | τ8 | −1.272357E-04 | −1.272344E-04 |
| M1 | 5.357986E-03 | 5.357986E-03 | τ9 | −3.013477E-05 | −3.013630E-05 |
| M2 | 7.654266E-04 | 7.654266E-04 | τ10 | −6.696615E-06 | −6.696287E-06 |
| M3 | 3.061706E-03 | 3.061707E-03 | τ11 | 2.929390E-05 | 2.929219E-05 |
| N1 | −6.696615E-04 | −6.696615E-04 | τ12 | 4.687023E-05 | 4.687382E-05 |
| N2 | −2.678646E-03 | −2.678646E-03 | τ13..τ20 | 0 | ≤ 4E-09 |
| N3, C5, Pi5, E5 | 0 | ≤ 1.5E-11 | | | |

`F4_parabolic_mirror.zmx` - the same mirror with k = −1, field 0.5°. `BUCH7` declines (figured);
`FORBES` declines (mirror); `BUCH7_ASPH`'s third and fifth order are unchanged and still match
FIFTHORD, and its tau are the ones that flip sign:

| | Buchdahl | | Buchdahl |
|---|---|---|---|
| F | −8.726868E-03 | τ2, τ3 | −1.745374E-06 |
| C | 7.615822E-04 | τ4 | 6.092658E-07 |
| Pi | −7.615822E-04 | τ5 | 1.523164E-07 |
| F1, F2 | −1.745374E-04 | τ6 | 1.218532E-06 |
| M1 | 4.569493E-05 | τ7 | −1.212936E-07 |
| M2 | 7.615822E-06 | τ8 | −1.096628E-07 |
| M3 | 3.046329E-05 | τ9 | −2.990802E-08 |
| N1 | −6.646227E-07 | τ10 | −6.646227E-09 |
| N2 | −2.658491E-06 | τ11 | 2.900037E-09 |
| B, E, B5, N3, C5, Pi5, E5, B7 | 0 | τ12 | 4.640060E-09 |

On F4, `RAYINV` gave the third order exactly and the fifth to 3E-5, but not the seventh: at half
a degree the field-dependent tau sit at the ray fit's floor of a few 1E-08 in OpticStudio, and that
is the fit's resolution, not a disagreement. F10 is the file the seventh order is checked on.

## What is not compared

- **Finite conjugates**, for the fifth order: `CoefficientInversion` measures the field as
  tan θ and is written for an object at infinity. For the Seidel sums, finite conjugates are
  where Optiland's chief ray is wrong (finding 4).
- **Surfaces the bridge refuses** rather than builds as something else: figuring on the object
  or image surface, decentres and tilts, Zernike terms, and surface types other than standard
  and even asphere.
- **Chromatic terms.** Each medium is handed over as an index at one wavelength.
