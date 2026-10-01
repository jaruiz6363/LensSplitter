# Coefficient reference fixtures

Twenty-nine small designs in three families, all purpose-built for testing and this
repository's own:

- **F1 to F10** - aspheric designs and two mirrors, object at infinity. F1 to F7 carry the
  third-, fifth- and seventh-order coefficients they should produce (`.buchdahl.json`: per
  surface, the isolated aspheric part of each surface, and the system totals); F8, F9 and F10
  have no reference file, for the reasons given with each.
- **G0 to G2 and H1 to H3** - the r-squared term at a finite conjugate.
- **The E family** - the two ENDS of the system: curved end surfaces and immersed end media.

Each is a single wavelength (0.5875618 um) and a non-zero field - the chief ray is traced at
full field, so a zero field would collapse the Lagrange invariant and there would be nothing to
check. The F family, which the first table lists, is at infinity; the G, H and half the E
designs are at a finite conjugate.

| fixture | what it exercises |
|---|---|
| F1_conic_singlet | one conic surface |
| F2_a4_equivalent | an r^4 term chosen to match that conic |
| F3_conic_a4_a6_a8 | a conic and three polynomial terms together |
| F4_parabolic_mirror | a mirror, where k = -1 and every (1-k) factor becomes 2 |
| F5_doublet_rear_asphere | figuring behind a powered surface |
| F6_triplet_two_aspheres | two figured surfaces, so one induces on the other |
| F7_conic_as_polynomial | the same surface written the other way round |
| F10_spherical_mirror | F4 as a SPHERE, at 5 degrees, behind a dummy plane - BUCH7 wants two surfaces and declines figuring, so this is the mirror it can run |

**F9_r2_as_shifted_sphere is F8 written the other way, and exists to be compared with it.** The
same physical surface: an r-squared coefficient is a curvature, so `cb = 0.02` with `A2 = 1E-04`
is the surface of curvature `0.0202` with `A2 = 0` once the higher terms absorb the difference
between the two spheres. Matched through `r^8`, so the pair is equivalent at the third, fifth and
seventh orders alike - they part company only at `r^10`.

The two files must give the SAME answer, and in this program they do: the same focal length to
the digit and the same five Seidel sums to twelve figures. **No external reference is needed to
use them.** A program that disagrees between F8 and F9 is contradicting itself, which is a
stronger statement than disagreeing with somebody else - and it is the check that found the
r-squared defect in this repository. See `docs/verification.md`.

**F8_r2_conic_a4_a6_a8 has no reference file, and that is the point of it.** It is
F3_conic_a4_a6_a8 with `PARM 1 = 1.0E-04` added and nothing else changed, so it carries the one
term F1 to F7 deliberately avoid. No FIFTHORD reference can exist for it - see the note
on `PARM 1` below - so `Fixtures()` never picks it up, since that enumerates the `.buchdahl.json`
files and this design has none. It is here to be compared the other way: against Forbes, against
real rays, and against the same file opened in OpticStudio.

The term is not a perturbation. It moves the effective focal length from 78.0375 to 77.4194, the
F/number from 3.9019 to 3.8710, `B` by three per cent and `B7` by 0.43 - so anything that drops
it is analysing a visibly different lens.


## The G family: the r-squared question at a FINITE conjugate

Three designs, added 21 September 2026. The same optics in all three - object 400 mm away, a
figured front surface, and the STOP behind the lens rather than on it, so the stop parameter is
not zero - and they differ only in how the r-squared term is expressed.

| fixture | surface 1 | what it asks |
|---|---|---|
| G0_finite_no_r2 | `c = 0.02`, no `A2` | the baseline |
| G1_finite_r2 | the same, `A2 = 1E-04` | is the term read at all? |
| G2_finite_r2_as_shifted_sphere | `c = 0.0202`, no `A2`, higher terms adjusted | is it read from the right sphere? |

**G1 against G0** moves the focal length from 78.0375 to 77.4194 and `S1` from -3.0516E-03 to
-3.1630E-03. A program that reports the same numbers for both is dropping the term.

**G1 against G2** must give the SAME answer, because they are the same surface: an r-squared
coefficient is a curvature, so `c = 0.02` with `A2 = 1E-04` is `c = 0.0202` with none, once the
higher terms absorb the difference between the two spheres. Matched through `r^8`, so the pair is
equivalent at third, fifth and seventh order and parts company only at `r^10`. In this program
they agree to the last stored digit, per surface and in total.

**Why a finite conjugate, when F8 and F9 already ask this at infinity.** Because that is where
this repository's own defects have hidden. `iota` is non-zero only at a finite conjugate, the stop
parameter stops being the scheme's derived value only there, and the figured-and-finite
combination is what carried the stop-parameter defect recorded in docs/verification.md, *The
stop parameter the aspheric increments were measured from*. Putting the stop behind the
lens rather than on it makes the stop parameter live as well.

These were built as the acceptance test for a fix to another program, and kept because the
combination they cover - figured, finite, r-squared, stop off the first surface - was in no
fixture here either.

## The H family: an r-squared term on MORE THAN ONE surface

Three more, on the same optics as the G family, so `G0_finite_no_r2` is their baseline too.

| fixture | surface 1 | surface 2 |
|---|---|---|
| G0_finite_no_r2 | no `A2` | no `A2` |
| H1_finite_r2_second_surface | no `A2` | `A2 = -5E-05` |
| H2_finite_r2_both_surfaces | `A2 = 1E-04` | `A2 = -5E-05` |
| H3_finite_r2_both_as_shifted_spheres | `c = 0.0202` | `c = -0.0051` |

**H1 exists to catch a fix that only reaches the first surface**, which is the natural shape of a
half-done one: the term is picked up where the ray starts and forgotten further in. Its focal
length is 77.7368 against the baseline's 78.0375, and its `S1` moves the other way from G1's -
-2.7754E-03 against -3.0516E-03 - because the term is on a surface of the opposite curvature.

**H2 carries one on each surface, of opposite sign**, so a program that folds the first correctly
and the second not at all lands between H1 and H2 rather than on either.

**H3 is H2 with both surfaces written as the spheres their r-squared terms make them**, each
matched through `r^8`. H2 and H3 must give the same answer; here they agree to the last stored
digit, per surface and in total.

    H2/H3      S1                  S2                  S3
    surf 1     -0.0145828581868    -0.00465922990209   -0.000342852591855
    surf 2      0.0116998892083     0.00358928715771    0.00109992636639
    TOTAL      -0.00288296897845   -0.00106994274438    0.00075707377453

    EFL        77.12359458

The image distance is the one that focuses G1 rather than each of these, so the image plane sits a
little off focus here. That is deliberate and harmless: the Seidel sums do not depend on where the
image surface is, and holding the distance fixed across the whole G and H family keeps every
comparison between two of them a comparison of one thing.
## Where the numbers came from

The reference values were produced by running the FIFTHORD macro (Rimmer 1962, via
M. MacFarlane 1998, with the mirror index-sign correction of T. A. Mitchell 2003 and the
Lagrange-invariant correction of J. Sasian 2019) on these seven designs, and recording what
it reported.

**Neither that macro nor anything derived from it is in this repository, and neither will
be.** It ships with a commercial program and is not ours to redistribute. What is here is the
numerical output for designs that are ours - the same status as any measurement.

`PARM 1`, the r^2 deformation term, is zero in every fixture on purpose: the macro ignores
that term while this program's sag includes it, so a non-zero value would put the two on
different surfaces and the comparison would mean nothing.

**That is now verified from the macro's source rather than inferred.** Its aspheric block reads
`par2`, `par3` and `par4` - r^4, r^6 and r^8 - into `aterm`, `bterm` and `cterm`, never touches
`par1`, and uses the base curvature throughout. Its author says the same in the header: "Zemax
uses a second-order aspheric deformation coefficient which is not used in this treatment."

`macros/BUCH7_ASPH.ZPL` does the opposite and says so: a non-zero `PARM 1` is folded into the
vertex curvature, the conic is zeroed, and A4/A6/A8 are re-measured as departure from that
sphere.

## What F8 measured, when both macros were run on it

**OpticStudio's own paraxial data accounts for `PARM 1`.** BUCH7_ASPH prints `efl` straight from
`GETSYSTEMDATA` and it reads 77.419426 on F8 - this program's number to every digit, against
78.037505 for the same lens with the term removed. So the folded vertex curvature is consistent
with the EFL and pupil the macro reads back, and the worry that prompted this fixture does not
arise.

**BUCH7_ASPH and this program agree to every printed digit on F8**, all eighteen totals and all
twenty tau, `B7 = 2.515701E-04` and `tau20 = -4.928042E-09`. That is the first time the two have
been held against each other on a surface carrying an r^2 term. Forbes and the ray inversion
agree with both.

**FIFTHORD does not merely ignore the term - it is internally inconsistent on such a surface.**
It was expected here to reproduce the A2-free answer. It does not: its `B` is 2.5314E-02, against
2.7696E-02 with the term and 2.6888E-02 without, matching neither. The cause is visible in two
places. On surface 2, which is unfigured, the two agree to all five printed digits on every third-
and fifth-order term. On surface 1, which carries the term, Petzval gives the mechanism exactly -
Petzval depends on the surface curvature alone, and

    FIFTHORD  -5.2159E-03  x  (c_vertex/c_base = 0.0202/0.0200)  =  -5.268059E-03
    BUCH7_ASPH                                                      -5.268041E-03

differ by 3.4E-06, the limit of FIFTHORD's five printed digits. So it takes paraxial data from
OpticStudio that INCLUDES the r^2 power and then computes the surface contributions with the BASE
curvature. It is not analysing the A2-free lens; it is analysing a lens that does not exist. The
totals differ by -8.6 per cent on `B`, -4.4 on `N1` and -1.65 on `B7`, while `E`, `E5`, `N2` and
`M2` are unmoved - the signature of a curvature error rather than a dropped term.

None of that is a fault in the macro's arithmetic and none of it touches the seven fixtures above,
where `PARM 1` is zero and the 586-value agreement stands. It is a statement about where that
agreement stops.

## What this reference is, and is not

It is a cross-check. Two implementations of the same published method agreeing across 586
values is worth having, and a reader with that program can reproduce it without trusting
anything here.

It is **not** the authority for these coefficients, and the test that reads it is not named
as though it were. What establishes them is Buchdahl's own printed table, closed-form conic
surfaces that need no other program at all, this repository's inverse ray tracing, an
independent implementation written from the book, and Forbes' series trace. See
`docs/references.md`.

## A third program: OSLO

**Provenance.** OSLO EDU, Revision 6.6.0, run by hand on the `.len` files in this folder,
written by LensHH-LT's OSLO exporter. Seven designs, F1 to F7. As with the FIFTHORD data
above, neither OSLO nor anything derived from it is in this repository - what is here is the
numerical output for designs that are ours, which is the same status as any measurement.

**The raw listings are in `oslo/`**, exactly as OSLO printed them, under the names they were
captured with:

| file | OSLO command | what it carries |
|---|---|---|
| `F*_SEIDEL.txt` | `sei` | `SA3 CMA3 AST3 PTZ3 DIS3` = `B F C Pi E` |
| `F*_FIFTH.txt` | `fif`, *intrinsic/transferred* | the twelve mu, split `INT` and `XFR` |
| `F*_FIFTH_IMAGING.txt` | `fif`, *imaging* | `SA5 CMA5 AST5 PTZ5 DIS5 SA7`, `INT`+`XFR` combined |
| `F6_FIFTH_BUCHDAHL.txt` | `fif`, *Buchdahl* | the twelve mu, `INT`+`XFR` combined |

The intrinsic/transferred listing is the one to keep for any design added later: the imaging
and Buchdahl forms carry nothing it does not, and combining the two parts is what stops a
per-surface comparison from working. `F4` has no imaging listing - a parabola's `B7` is 2.5E-20
and there is nothing there to compare. `F8_FIFTH.txt` is included as the evidence for the
dropped `PARM 1` described below, and is not a reference for F8.

The `.len` files carry a `// OSLO 5.10` banner. It is a hardcoded comment in the exporter and
designates nothing - a `.len` file is a script in OSLO's command language, not a versioned
format, and both OSLO and this repository's `OsloReader` discard `//` lines.

**Reading the listings.** OSLO reports Buchdahl's twelve fifth-order mu, which are the FIFTHORD
names regrouped. The mapping is exact, and four of the twelve are combinations:

    mu1 = B5      mu4 = M1+M2    mu7 = N1+N2/2    mu10 = 5*C5+Pi5
    mu2 = F1      mu5 = M2       mu8 = N2/2       mu11 = C5+Pi5
    mu3 = F2      mu6 = M3       mu9 = N3         mu12 = E5

Per-surface values need the reference's own scaling put back: the `.buchdahl.json` totals are
multiplied by the f-number while the per-surface entries are not, and a figured surface carries
its figuring in the separate `aspheric` block. So the comparison is

    OSLO per-surface  =  fnum * ( surfaces[i] + aspheric[i] )

and with that, OSLO's `INT` is the same quantity as the reference's per-surface entry. The
`fif` imaging listing has `INT` and `XFR` already combined, so it compares per surface only on
a first powered surface, where `XFR` is zero.

**What agrees: 425 values, third and fifth order, per surface and in total.** Worst 2.1E-05, on
`mu4 = M1+M2` at F6 surface 4 where the two terms cancel to a twentieth of their size; typical
1E-07. The floor is the exporter, which writes `WV 0.58756` against the fixtures' 0.5875618 -
differencing two OSLO runs cancels it and the agreement tightens to 7.9E-10, which is how the
floor was identified rather than assumed.

**F4 differs by one sign, and it is the f-number's.** Every non-zero coefficient at both orders
is the exact negative of the reference. Dividing OSLO's per-surface values by the reference's
unscaled ones gives +2.5000000000 on all of them, to ten digits, while this repository stores
`fnum = -2.5` for that design - the reflection carried into f/#. Nothing in the arithmetic
differs.

### Where OSLO is wrong: `SA7` on a figured surface that receives induced aberration

OSLO's `fif` imaging listing carries a seventh-order column, `SA7`, which is `B7`. It agrees on
F1, F2, F3 and F7 to between 1.9E-07 and 5.2E-07, and disagrees on F5 by +3.22 per cent and on
F6 by -3.74 per cent.

The split is not by size or surface count. **It is whether a figured surface has lower-order
aberration incident on it.** F1, F2, F3 and F7 carry their figuring on the first powered
surface, which receives nothing; F5's is last and F6's are first and last. Splitting `B7` by
surface shows it directly - F3's figured surface has a transferred part of 1.3E-11 and its
total is exact, while F5's figured surface carries 1.92E-04 and F6's 7.05E-04.

**The rays settle it against OSLO.** On both designs the scheme, Forbes' series trace and the
ray inversion agree:

| `B7` | scheme | Forbes | ray inversion | OSLO `SA7` |
|---|---|---|---|---|
| F5 | 6.5893354135E-04 | 6.5893354135E-04 | 6.5893356729E-04 | 6.801346633E-04 |
| F6 | -1.2433949700E-03 | -1.2433949700E-03 | -1.2433950863E-03 | -1.1969513021E-03 |

The inversion sits 3.9E-08 and 9.4E-08 from the scheme, with its least-squares residual at
6.2E-07 and 3.0E-07 over 75 ray shapes - five to six orders of magnitude inside OSLO's gap. All
twenty tau agree with Forbes on both designs to 1E-13 or better. So this is OSLO EDU 6.6.0's
seventh-order aspheric induced term, and `B7` here is not in question. Premium was not tested
and no claim is made about it; the failure's shape argues it is the `SA7` routine rather than
anything about the EDU build, since a cut-down numerical core would not match 425 values to
1E-07 and then miss one column in exactly two configurations.

**F8 has no OSLO data either, for the same reason it has no FIFTHORD reference.** OSLO's
asphere has no r^2 term, so the exporter drops `PARM 1` silently. **`F8_r2_conic_a4_a6_a8.len`
is therefore not F8**: it is F3's surface carrying F8's image-plane aperture, and
`oslo/F8_FIFTH.txt` is bit-identical to `oslo/F3_FIFTH.txt`. Both are kept only as the evidence
for that, and neither says anything about the design F8 actually is. A reader who wants F8 in
OSLO cannot have it; that is the point of the fixture.

**Two internal checks the OSLO data passes on its own**, needing no reference at all. F1 and F7
produce bit-identical output at both orders including `SA7` - the conic and its polynomial
expansion match through r^8, and nothing in either listing can tell them apart. And F1, F2 and
F7 are bit-identical at third order, where only the r^4 departure can be seen, while F3 differs
from them in `SA3` alone and only on the figured surface, the stop being on it.

## The E family: the two ENDS of the system

Thirteen more designs - twelve added 20 September 2026 and Ek on 21 September - for a different
question: what happens when the
OBJECT surface or the IMAGE surface is not a plane, and when the object or image MEDIUM is not
air. Every fixture above, and every lens this program had ever been checked on, is plane at both
ends and in air at both ends, which is the same shape of blind spot the r-squared term lived in.

They are all the same singlet - R = 60 / -60, 4 mm thick, model glass n = 1.6, EPD 20, one
wavelength, image at the paraxial focus - so that ANY difference between two of them is caused by
the one thing that differs. That also makes them the smallest designs here, which is deliberate:
a disagreement in one of these can be chased by hand.

| fixture | conjugate | what differs |
|---|---|---|
| E0_infinite_flat | infinite, 5 deg | baseline |
| Ea_image_curved | infinite | image surface R = -50 |
| Eb_image_curved_conic | infinite | image surface R = -50, k = -1 |
| Ec_image_curved_a4a6 | infinite | image surface R = -50, A4 = 1E-04, A6 = 1E-06 |
| Ed_image_flat_a2 | infinite | image surface FLAT with A2 = -0.01 |
| E0_finite_flat | finite, object 200 mm, height 10 | baseline |
| Ee_object_curved | finite | object surface R = 50 |
| Ef_object_curved_conic | finite | object surface R = 50, k = -1 |
| Eg_object_curved_a4a6 | finite | object surface R = 50, A4 = 1E-05, A6 = 1E-08 |
| Eh_object_flat_a2 | finite | object surface FLAT with A2 = 0.01 |
| Ei_image_space_n101 | infinite | last medium n = 1.01 |
| Ej_object_space_n101 | finite | object medium n = 1.01 |
| Ek_figured_finite_conjugate | finite, object 200 mm, height 10 | front surface figured (k = -0.6, A4 = 2E-07, A6 = 1E-11), stop 8 mm behind the lens, EPD 16 |

**`Ea` and `Ed` are the same surface written two ways**, and so are `Ee` and `Eh`. An r-squared
coefficient is a curvature: `A2 = -0.01` on a flat surface is vertex curvature `-0.02`, which is
`R = -50`. The two agree term for term through `r^2` and part company only at `r^6`. A program
that treats them differently is folding one and not the other, which is a fault worth finding and
is exactly the fault the r-squared bug was.

**What OpticStudio says about the first three.** `E0`, `Ea` and `Ed` give identical Seidel tables
and identical FIFTHORD output, to every printed digit, with an `IMA` row of zeros - so ignoring
the image surface in the coefficients is the convention rather than a gap, and this program does
the same. That run also matched this program's Seidel sums digit for digit and its Buchdahl
coefficients 18 of 18, per surface and in total. See `docs/verification.md`, *The two ends of the
system*.

**The model glass is not incidental.** These are the first .zmx fixtures here to use
`GLAS ___BLANK`, and adding them exposed a bug that deleted it on save-back. Keep at least one
model-glass fixture in this folder.
