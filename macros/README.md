# ZPL macros

## BUCH7.ZPL

Third-, fifth- and seventh-order Buchdahl coefficients for a centred system of
**spherical** surfaces, per surface, with the intrinsic and induced parts separated.

### Why

`FIFTHORD.ZPL`, which ships with OpticStudio, gives the third and fifth orders plus
seventh-order spherical aberration, as system totals. What it does not give is the
seventh order entire, or any of it per surface, or the split between what a surface
generates on its own and what it generates by acting on the aberration handed to it.

That split is the useful part. On a Cooke triplet the induced share is the larger one on
four surfaces of six, and on one of them it is more than a thousand times the intrinsic -
a surface that is blameless in isolation and is nonetheless a large contributor. The cure
for that is upstream of the surface the total blames, and a table of totals cannot say so.

### Status

All six stages are written and all six pass, at either conjugate.

| stage | contents | agrees with | result |
|---|---|---|---|
| A | paraxial basis, per-surface primary, running sums | the Seidel analysis in OpticStudio | 30 of 30, both conjugates |
| B | fifth order per surface, intrinsic and induced, plus B7 | FIFTHORD, where `PARM 1` is zero; the C# otherwise | 18 of 18 totals, both conjugates; 18 of 18 and all 20 tau on a design carrying `PARM 1` |
| C | Buchdahl's Table I, t1 to t155, per surface | `reference/CookeTriplet_TableI.txt` | 155 of 155 to 7e-16 |
| D | the twenty tau, intrinsic and induced per surface | that file's tau block, and a Forbes series trace at 250 mm | 420 of 420; 20 of 20 to every printed digit |
| E | publishes the coefficients into the call buffer for `ROBB.ZPL` to read and turn into an RMS spot radius | `Prms.cs`, and the `rms_spot` tool, on the same lens | 33 of 33 to every printed digit |
| F | the NINTH order - quaternary spherical aberration, per surface, `q9 = 1` | `QuaternarySpherical.cs`, which reproduces Buchdahl's own paper IV Table I | run on KingslakeDG: every surface, all three columns and the total agree to 5 figures |


### Two kinds of spherical-only, and only one has a way out

This matters more since stage F arrived, because the file now contains both.

**Orders three, five and seven are spherical only *in this macro*.** A conic or an even asphere
stops it. That is a limit of `BUCH7.ZPL`, not of the subject: `BUCH7_ASPH.ZPL` is Buchdahl's
Sec. 85 arrangement reconstructed, it carries conics and even aspheres, and it agrees with
`FORBES.ZPL` on all twenty seventh-order coefficients on every figured refracting design tried
(FORBES declines a mirror). Want the seventh order on a figured design and you run the macro next
door.

**Stage F, the ninth order, is spherical only *in the subject*.** There is no aspheric
arrangement at this order anywhere. Buchdahl published none — paper IV reaches quaternary
spherical and stops — and unlike the tertiary one nobody has reconstructed it. `BUCH7_ASPH` has
no stage F and will not grow one by transcription; somebody would have to derive the arrangement
first, and the only exact check available for it would be a closed-form conic on axis.

So for a reader with a figured design: there **is** a seventh order to be had, and there is **no
ninth order to be had at all**. That is where the published subject ends, not where these macros
stop trying.

### Stage F, the ninth order

The coefficient is ALWAYS computed and always published to the call buffer; `q9 = 0`, near
the top of the file, gates only whether BUCH7 PRINTS it. That is because `ROBB.ZPL` reads it
out of the buffer and cannot set a variable in BUCH7 to ask for it - ZPL does not document
whether a child macro shares the parent's variables, which is why the buffer exists. Set
`q9 = 1` and the per-surface table appears, with the six intermediate `r` rows Buchdahl
prints beside the answer.

Source: Buchdahl, *Optical Aberration Coefficients IV: The Coefficient of Quaternary
Spherical Aberration*, **J. Opt. Soc. Am. 48**, 757 (1958).

**Why it is only fourteen lines of arithmetic.** Paper IV builds the quaternary as "an
appendix to that for the set of tertiary coefficients", so every quantity it needs is
already an entry of Table I that stages A to D computed. Buchdahl: "the computing scheme
is quite brief, viz. only 14 entries of the usual kind per surface."

**It needs no dual run**, though Sec. 3 sounds as if it does — it says obtaining
`T1-dagger` "requires `T1q`" via identity M (21.7), and then performs that reduction
himself: the three rows feeding `r3` *are* M (21.7) written out in p-side quantities.

**Two different sums, and Buchdahl flags it himself** because confusing them gives a
plausible wrong number: the sum in the `r3` row "exceptionally indicates the sum of the
entries in the three preceding rows", while the sum in the last row takes the **four**
above it, the intrinsic row included.

**Spherical surfaces only**, which is this macro's limit anyway — a conic or an even
asphere stops BUCH7 long before stage F. Buchdahl published no aspheric arrangement at the
ninth order, as he published none at the seventh.

Results land in `tt` columns 241–248, which were free: stage D ends at 240 and the array
is declared with 250.

**The conversion to transverse measure had no second route, and now it has one.** Stage D puts
each tau into transverse measure by `efac * um^a * hmax^b`; that rule is confirmed at twenty
points, and at `a = 7, b = 0` it agrees with `sB7 * fnum`, which arrives by a different path.
Ninth-order spherical is `a = 9, b = 0`, so it extends to `efac * um^9` — the natural
continuation, but every point confirming the rule has total degree seven and this has degree nine.

Measured on KingslakeDG, it holds. The printed ratio of the unconverted and transverse lines is
−1.454733E−09, and that factors into quantities taken from the prescription alone:
`um = −(EPD/2)/EFL = −0.06249754` gives `um^9 = −1.454676E−11`, leaving `efac = 100.00395`
against an EFL of 100.00394 — so `vpk = 1.000000`, which is exactly what it must be, the p ray
starting at unit height and leaving at unit angle in units of the focal length. Six significant
figures, from numbers that know nothing about this stage. BUCH7 still prints both lines, because
that is what made the check free.

**What the OpticStudio run actually settled.** Every surface of stage F agrees with
`QuaternarySpherical.cs` in all three columns to the five figures the C# prints, and so does the
system figure, 4.196252E+04 — cross-implementation confirmation of the fourteen rows on a design
that is not Buchdahl's own. It also confirmed `r1` and `r2` from inside the macro: they are running
sums of `t132` and `t133`, which the stage C dump prints, and they match at every surface to every
digit.

It took a bug fix to get there. The design reads **F4**, and the bare name was resolving to CDGM's
F4 (1.620047) rather than Schott's (1.616592), which put the two implementations eleven per cent
apart in the ninth order. See `GlassCatalog.FallbackPreference`. The macro was the one that agreed
with OpticStudio.

**How it was checked, in the order the checks were made.** First the twelve formulas were diffed
mechanically against `QuaternarySpherical.cs` — extracted from both files, normalised so that
`tt(i, 83)` and `t[83]` become the same token, and compared. They are identical, which made the
*transcription* checked but said nothing about the macro running. Then it was run, and the paraxial
inputs, the `tt` indices under a live `nsm` and the printing were checked all at once by the
agreement above.

### Where the numbers come from, and what merely agrees with them

The method is Buchdahl's and the authority for it is his published work. **The check that
settles the scheme is against his own printed numbers**: paper III prints Table I worked
through for a specific triplet, and `BuchdahlPublishedTableTests` reproduces it — entry by
entry for the scheme, and against his published totals for the tertiary. That is the oracle.
It is what found the errors in t100–t108. The reference file behind stages C and D is an
implementation held to it.

The second independent route is Forbes' series trace, *J. Opt. Soc. Am.* **73**, 782 (1983)
— a different paper, different mathematics, no shared code — which stage D is checked
against at both conjugates.

**OpticStudio's Seidel analysis and FIFTHORD are neither of those.** They are used here as
cross-checks *a reader can repeat without trusting anything in this repository*, which is
their whole value: two of the six stages, A and B, can be confirmed without taking this macro's word
for anything. They are not the source of the method and they are not the authority for the
numbers. Neither computes the seventh-order set this macro exists for, so neither could be.

Both conjugates. The object position enters the scheme in exactly one place - the four
starting values of the p and q rays, M (13.4) - and paper III says so in a parenthesis:
"the choice of different coordinate systems reflects itself only in the starting values
of y_p, v_p, y_q, v_q (cf. M Secs. 12-13)". Those four were hard-coded to the
infinite-conjugate case, so every finite-conjugate system was quietly given the answer
for an object at infinity, identical to seven figures and with no other symptom.

On a 250 mm Cooke triplet the corrected macro agrees with three things that share no code
with each other: the Seidel analysis in OpticStudio at third order (30 of 30), FIFTHORD at
fifth (18 of 18), and a Forbes series trace on all twenty seventh-order coefficients, to
every printed digit. Seventh-order spherical aberration is reached twice over within the
macro itself, through the fifth-order working and through Table I and Table II, and both
give 2.305287E-03 — a check that needs nothing outside it.

Both checkers were confirmed by perturbing values and watching them report exactly the
perturbed ones. An earlier version of this work spent an afternoon chasing thirty-three
reported failures in the macro that were a bug in the script comparing it, so a checker
that says nothing is wrong is not believed until it has been shown able to say otherwise.

The two orders overlap in one number and disagree in none: seventh-order spherical
aberration is reached in stage B through the fifth-order working and in stage D through
Table I and Table II, and on the Cooke triplet both give 1.681450E-03.

Stage D prints in TRANSVERSE measure where A, B and C print unconverted per surface.
That is not an inconsistency to be tidied away: every third- and fifth-order coefficient
takes the same conversion factor, so one factor at the foot of the table serves them all,
but each tau multiplies a different power of aperture and field and so takes its own.
Printed unconverted, tau1 and tau20 could not be compared with each other. The one
overlap between the conventions, tau1 against B7, is computed by two different routes
and agrees, which is the check that ties the two halves of the listing together.

### The companions

`FORBES.ZPL` in this folder computes the same three orders by Forbes' series trace; see below. On a
system of spheres the two agree on all thirty-seven coefficients to every printed digit,
which is the strongest check either has: Buchdahl arranged tables against a series trace,
sharing no arithmetic and no code. On a FIGURED system BUCH7 declines outright and FORBES
does not, which is the whole point of having it.

`zosapi/` holds FORBES7, which computes the same three orders by G. W. Forbes' series
trace, *J. Opt. Soc. Am.* **73**, 782 (1983), as a C# program driving OpticStudio through
the ZOS-API. It is the better tool on a **figured** system, where this macro stops: it
carries conics and even aspheres at all three orders and separates what the figuring itself
contributes. This macro is the better tool everywhere else — it is one text file, it needs
nothing installed, and two of its six stages check against OpticStudio's own analyses.

The two agree, which is the point of having both. Seventh-order spherical aberration comes
out of this macro through Buchdahl's fifth-order working and out of FORBES7 through a power
series, and both give 1.681450E-03 on the Cooke triplet at infinite conjugate and
2.305287E-03 with the object at 250 mm.

### Limits, which the macro enforces rather than documents

Spherical surfaces only. A conic or an even-asphere term makes it stop with an
explanation instead of returning a plausible number, and names its companion. Buchdahl
gives the aspheric scheme - §65-66 and (85.2)-(85.5) - but never published the arranged
table for it. That arrangement has since been reconstructed: it is `BUCH7_ASPH.ZPL`,
which carries conics and even aspheres and agrees with `FORBES.ZPL` on all twenty
seventh-order coefficients wherever FORBES applies - every refracting design; it declines a
mirror. This macro is left as the spherical routine, the one checked
against Buchdahl's own printed numbers.

Either conjugate, rotationally symmetric, sequential.

**Mirrors are carried, after one change in September 2026, run in OpticStudio.** The surface loops always signed the indices through `ISMS`, so the scheme
sees a reflection as a refraction into -n. But the F/number and the seventh order's
conversion 1/(N'v'_pk) both took the SIGNED image-space index, so after an odd number of
mirrors every total came out negated - consistently, but in the opposite frame from
FIFTHORD, `BUCH7_ASPH` and the C# program. Both now take its magnitude. The fixture for it
is `F10_spherical_mirror.zmx` (the parabola is declined here, being figured, and so would a
lone mirror be: this macro wants two surfaces, so F10 carries a dummy plane). Run on it, it
reproduces the C# program on all thirty-seven coefficients to every printed digit, F/number
-2.5, and the dummy contributes exactly zero. Established on ONE reflection: whether OpticStudio's `ISMS()` flags the mirror surface or every
space after an odd number of reflections has not been determined, and on a single mirror the two
readings agree. A two-mirror system has not been run through the macros; the C# program has been
measured on one, Thompson's telescope.

### An immersed object space, and the reduction it needs

**Both macros had the defect that was found in the C# on 20 September 2026 and both now carry
the fix.** Buchdahl's p and q rays have to be a pair whose Lagrange invariant is ONE, and the
four starting values of M (13.4) give a pair whose invariant is exactly `n0` - one when object
space is air, which is every published example and every design either macro had ever been run
on, and not one when the object sits in a medium.

The scheme is not homogeneous in the q ray's scale, so a pair with the wrong invariant cannot be
absorbed by any later normalisation: it comes out as a different error in every tau. Measured
against Forbes' series trace and against real traced rays, both of which are free of the
convention, the seventh order was 0.24 per cent out at `n0 = 1.01` and 23 per cent out at
`n0 = 1.30`, with two coefficients changing sign at the larger index.

The fix is the reduction the comment at that ray start always claimed: in reduced coordinates the
angle is `N u`, so `v_q = 1` means a plain angle of `1/n0`, and the transfer carries plain angles.
Two lines at the ray start, divided by `n0abs`, and the field variable `hmax` multiplied by the
same number, since the chief ray's plain tangent is now `n0` times the q ray it is expressed in.

**In air it is a division by exactly 1.0**, so every number either macro has ever printed is
unchanged, including the reference listings in this file.

### On the two ways it finds Buchdahl's p

`p` is the entrance pupil position in focal lengths, and everything else is built on it.
The macro takes it from the system data and, where there is a field to trace a chief ray
at, derives it a second way from that ray - because the two rest on different sign
conventions, and a silent disagreement would poison every number below it. If they
disagree it says so rather than picking one.

### Expected output, CookeTriplet - stage B

From the C# on the same lens. The eighteen system totals are in transverse measure and
must match FIFTHORD run on the same file.

```
Fifth order per surface, unconverted   (int / ind / tot)
 Surf                B5             F1             F2             M1             M2             M3
    1  int -4.092955E-004 -7.189070E-004 -4.060244E-004 -6.207642E-004 -1.279330E-003  1.550576E-003
      ind  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000
      tot -4.092955E-004 -7.189070E-004 -4.060244E-004 -6.207642E-004 -1.279330E-003  1.550576E-003
    2  int -1.436617E-004  1.323774E-003  8.769318E-004 -5.455176E-003 -2.730198E-003 -5.343197E-003
      ind -6.263158E-004  2.346151E-003  1.207026E-003 -2.329584E-003 -2.686517E-003 -3.792866E-004
      tot -7.699775E-004  3.669924E-003  2.083958E-003 -7.784760E-003 -5.416715E-003 -5.722484E-003
    3  int  1.492490E-003 -4.198192E-003 -1.867355E-003  5.832534E-003  3.806345E-004 -6.302961E-003
      ind  2.351401E-003 -7.311034E-003 -4.979961E-003  1.119313E-002  1.050506E-002  1.527106E-002
      tot  3.843891E-003 -1.150923E-002 -6.847316E-003  1.702566E-002  1.088569E-002  8.968097E-003
    4  int  9.013352E-004  5.537364E-003  3.898870E-003  1.417514E-002  9.738758E-003  1.363791E-002
      ind  2.132639E-003  1.463683E-003 -3.001885E-004 -6.689307E-003 -2.344094E-003 -1.814739E-002
      tot  3.033974E-003  7.001047E-003  3.598681E-003  7.485828E-003  7.394664E-003 -4.509476E-003
    5  int -9.293814E-006 -3.711552E-005 -3.685577E-007 -2.914493E-006  2.010163E-004  7.046019E-004
      ind -7.594452E-004 -3.672453E-003 -1.821558E-003 -6.553210E-003 -5.311631E-003  1.722267E-003
      tot -7.687390E-004 -3.709569E-003 -1.821926E-003 -6.556124E-003 -5.110615E-003  2.426868E-003
    6  int -3.213308E-003  6.895329E-003  5.083310E-003 -5.733068E-003 -6.361125E-003 -2.540855E-003
      ind -1.781624E-004  2.106077E-004 -4.365193E-004  1.647346E-003  4.213939E-003  6.191672E-003
      tot -3.391470E-003  7.105937E-003  4.646790E-003 -4.085721E-003 -2.147186E-003  3.650818E-003

 Surf                N1             N2             N3             C5            Pi5             E5
    1  int -9.779737E-004  4.123452E-003  1.469063E-003  5.615067E-004  3.214874E-003  2.886824E-003
      ind  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000  0.000000E+000
      tot -9.779737E-004  4.123452E-003  1.469063E-003  5.615067E-004  3.214874E-003  2.886824E-003
    2  int  8.491944E-003  3.323546E-002  8.308053E-003 -1.292058E-002  1.454949E-005  4.014262E-002
      ind -1.775234E-003  6.496174E-003  3.114606E-003 -1.220429E-003 -1.499338E-003 -1.235677E-002
      tot  6.716711E-003  3.973163E-002  1.142266E-002 -1.414101E-002 -1.484788E-003  2.778586E-002
    3  int -5.944409E-004  3.473054E-002  1.244357E-002 -9.716628E-003 -1.029460E-002  3.125174E-002
      ind -1.139817E-002 -7.073607E-002 -2.371288E-002  1.815842E-002  1.094115E-002 -4.451463E-002
      tot -1.199261E-002 -3.600552E-002 -1.126931E-002  8.441793E-003  6.465552E-004 -1.326289E-002
    4  int  1.770362E-002  5.629517E-002  1.575174E-002  1.431716E-002  8.843223E-004  2.763404E-002
      ind -1.436581E-002 -3.906255E-002 -6.825315E-003 -9.778304E-004  3.262900E-003  1.811926E-002
      tot  3.337814E-003  1.723263E-002  8.926420E-003  1.333933E-002  4.147222E-003  4.575330E-002
    5  int  7.948019E-004  7.823814E-003  2.518938E-003  4.979836E-003  3.133763E-003  3.208049E-002
      ind  2.306536E-003 -1.901303E-002 -7.939031E-003 -2.026191E-002 -4.117754E-003 -8.080477E-002
      tot  3.101338E-003 -1.118922E-002 -5.420093E-003 -1.528207E-002 -9.839916E-004 -4.872428E-002
    6  int  3.587108E-003  6.701073E-003  2.634129E-003 -7.427069E-004  6.512652E-004  5.156497E-005
      ind -6.687247E-003 -2.710175E-002 -1.080609E-002  5.215682E-003  5.274891E-003 -1.419352E-002
      tot -3.100139E-003 -2.040067E-002 -8.171964E-003  4.472975E-003  5.926156E-003 -1.414196E-002

 Surf                B7   (int / ind / tot)
    1 -1.427141E-005  0.000000E+000 -1.427141E-005
    2 -2.005394E-006 -4.437606E-005 -4.638145E-005
    3  8.043467E-005  2.010518E-004  2.814864E-004
    4  4.118292E-005  3.838727E-004  4.250556E-004
    5 -9.001096E-008 -1.298666E-004 -1.299567E-004
    6 -2.324267E-004  5.278437E-005 -1.796424E-004

System totals, transverse measure (what FIFTHORD prints)
   B   -3.480019E-002   F    6.174430E-003   C    4.453368E-002
   Pi  -1.285785E-001   E    9.292942E-003
   B5   7.691912E-003   F1   9.196030E-003   F2   6.270814E-003
   M1   2.732059E-002   M2   2.163254E-002   M3   3.182199E-002
   N1  -1.457431E-002   N2  -3.253849E-002   N3  -1.521610E-002
   C5  -1.303741E-002   Pi5  5.733012E-002   E5   1.484277E-003
   B7   1.681450E-003
```

### Stage B validated against FIFTHORD

Run on `CookeTriplet.zmx` at wavelength 2, all eighteen system totals match FIFTHORD to
every digit FIFTHORD prints, and every per-surface INTRINSIC row matches its per-surface
rows exactly - six surfaces, three groups, plus B7.

**That comparison holds only while `PARM 1` is zero, and the macro now refuses it when it
is not.** The r^2 deformation term is a radius in disguise - a surface of curvature `c`
carrying `A2` is the sphere of curvature `c + 2*A2` carrying whatever is left over - and
FIFTHORD does not handle it: it takes paraxial data from OpticStudio, which includes the
power that term adds, and then computes each surface's contribution from the base
curvature, so its answer is neither the lens with the term nor the lens without it.
`BUCH7_ASPH` folds the term into the vertex curvature and is accurate there, which is
measured rather than claimed - see the header block **THE r^2 TERM, PARM 1** and
[docs/verification.md](../docs/verification.md). On a file carrying one, the macro prints a
refusal in place of its usual invitation to compare, and points at `FORBES.ZPL` instead.

That comparison also settles what FIFTHORD's per-surface table is. It is the INTRINSIC
part alone. Its rows do not sum to its own totals, and cannot, because the induced part
is not in them:

    fifth-order spherical, B5, transverse measure
      sum of FIFTHORD per-surface rows   -6.908569E-03
      FIFTHORD reported system total      7.691900E-03
      sum of BUCH7 per-surface totals     7.691915E-03

The induced share is 1.460048E-02 - nearly twice the total, and opposite in sign to the
sum of the intrinsic parts. A designer reading the per-surface table alone would conclude
this triplet has negative fifth-order spherical aberration distributed over surfaces 1, 2
and 6, when the system has positive B5 and most of it is generated by surfaces acting on
aberration handed to them. That is not a criticism of FIFTHORD, which claims nothing
else; it is the gap this macro exists to close, now demonstrated with numbers rather than
asserted.

BUCH7's per-surface totals do sum to the system totals, as the same table shows.

### Stage C, and why it has a reference file

Stage C is not a continuation of stage B but a second arrangement. The fifth order is
built on the marginal and chief rays as traced; the seventh is built on Table I, which
uses its own basis - two rays p and q in units of the focal length, the p ray entering at
unit height parallel to the axis and the q ray at height p0 with unit angle. So the
paraxial basis is constructed again, and the entry numbers in the macro are Table I own.

It is also about a hundred and fifty entries, against the twenty or so of stage B. A
transcription that long, written where it cannot be executed, will contain mistakes; the
question is whether they can be found. So `reference/CookeTriplet_TableI.txt` carries
every non-zero entry, per surface, from the C# in this repository, and the macro will
print the same on request - set `dbg = 1` at the head of stage C. A wrong entry then
shows up as one wrong line rather than as a wrong answer at the bottom.

Built and CHECKED: all of Table I, t1 to t155 - the paraxial basis, the primary, the
secondary with its barred and mid forms, the accumulated secondaries, the q-side and the
tertiary chain. Every entry agrees with the reference on all six surfaces of the Cooke
triplet to machine precision (t1 to t33 to 8.33E-17, t34 to t98 to 1.39E-17), which is what a
faithful transcription of the same arithmetic gives. The per-surface intrinsic and induced
split is stage D. (This section once carried the build's progress notes; the build is done.)

One limit taken deliberately. The barred secondary needs the lift q s, formed here as a
product. Buchdahl Sec. 84(e) carries the powers of q on the incidences instead, which
stays finite where q does not - a flat surface facing collimated space. That
regularisation is not transcribed, so such a surface is refused at the top rather than
got wrong quietly.

### Four ZPL traps this macro has already hit

Recorded because each produced something that looked like working code.

1. **THEN belongs only to the single-statement form.** `IF (cond) THEN statement` is
   one line; a block is `IF (cond)` ... `ENDIF` with no THEN. A trailing bare THEN is
   a syntax error. Not one line in the macros shipped with OpticStudio ends in one.

2. **Variable names are case-insensitive.** `sB` and `Sb` are the same variable. This
   one did not error - it produced a correct-looking table with one wrong total, which
   is far worse. There is an audit for it in the repository history: extract every
   assigned name, lowercase them, look for duplicates.

3. **FOR does not skip an empty range.** `FOR m = 1, 0, 1` is reported as an infinite
   loop rather than executing zero times. Any loop whose bound can fall below its start
   - a sum over preceding surfaces at the first surface, say - needs guarding.

4. **A variable may not be named after a function.** `nobj = ABSO(n0)` is refused with
   "Variable name may not be the same as a function name: NOBJ", NOBJ being ZPL's
   non-sequential object count. The refusal is at least loud and immediate; the trap is
   that the reserved list is the whole function list, which is long, and an obvious name
   for a quantity is often an obvious name for a function. The object index is `n0abs`
   here for exactly that reason.

### ZPL variable names are case-insensitive

Worth stating on its own, because it cost a wrong answer that looked right. The running
total `sB` and the chief-ray surface factor `Sb` are the SAME variable in ZPL, so the
accumulator was overwritten on every pass and the printed total for B came out as the
last surface's `Sb + Ba` - confirmed exactly, 1.425923E+00 against the C#. Every
per-surface number in the column above it was correct, which is precisely what made it
easy to miss: the table looked right and only the total was wrong.

Stages B and C carry many more quantities. Keep every name distinct without relying on
case, and check each printed total against the column above it.

### Provenance

Written from the implementation in `src/AberrationCalculator.Core`, which is this
project's own transcription of Buchdahl's scheme. `FIFTHORD.ZPL` was read as a reference
for the ZPL interface only - the keyword names and the paraxial conventions - and no code
from it is reproduced here. Every ZPL keyword used has been checked against the macros
shipped with OpticStudio rather than written from memory.

### Expected output, CookeTriplet

Run against `CookeTriplet.zmx` at wavelength 2 (0.55 um), these are the numbers the C#
in this repository gives for the same lens. They are what stage A must reproduce.

    EFL                49.999982
    Buchdahl p          0.230243     (= entrance pupil position / EFL)
    stop surface        4

     Surf              B              F              C             Pi              E
        1  -1.385463E-02  -1.059106E-02  -8.096248E-03  -5.772738E-02  -5.031834E-02
        2  -1.125614E-02   3.501083E-02  -1.088968E-01  -2.916251E-03   3.477808E-01
        3   5.211528E-02  -8.138899E-02   1.271060E-01   5.726770E-02  -2.879384E-01
        4   2.413498E-02   4.387382E-02   7.975611E-02   6.269015E-02   2.589462E-01
        5  -4.079989E-03  -1.613194E-02  -6.378433E-02  -1.594791E-02  -3.152546E-01
        6  -5.401955E-02   3.046222E-02  -1.717798E-02  -6.908203E-02   4.864296E-02
      SUM  -6.960041E-03   1.234886E-03   8.906738E-03  -2.571572E-02   1.858589E-03

The first draft of stage A reproduced only `B`, and only up to a constant factor. That
was diagnostic rather than mysterious: `B` carries no chief-ray incidence, `F` carries
one and `C` two, so a wrong normalisation of the chief ray shows up as a different
factor on each. The macro had been built on Table I's normalised basis, where the q ray
is scaled to unit angle at the first surface, while the coefficients are defined on the
physically scaled chief ray. Stage A now works from the marginal and chief rays as
traced, with the surface factors written as the C# writes them.

---

## BUCH7_ASPH.ZPL

The third, fifth and seventh orders - not the ninth - for a system that **may carry conics
and even aspheres**. Buchdahl's computing scheme throughout, with the aspheric arrangement of
his Sec. 85. There is no ninth order here; see below for why that is the subject's limit
rather than this file's.

### It has no ninth order, and that is not an oversight

`BUCH7.ZPL` grew a stage F — the coefficient of quaternary (ninth-order) spherical aberration,
from Buchdahl paper IV. This macro has no counterpart and cannot gain one by transcription.

Paper IV reaches quaternary **spherical** and stops, and there is no aspheric arrangement at that
order in the literature at all. The tertiary one existed to be reconstructed because Buchdahl
wrote Secs. 84 and 85 for it; nothing corresponds at the ninth. Deriving one would be new work,
and the only exact check available for it would be a closed-form conic on axis — which is a
narrow oracle, though a real one.

So the division between the two macros is not symmetric. At the seventh order this file is the
capable one and `BUCH7.ZPL` is the restricted one. At the ninth, `BUCH7.ZPL` is the only one that
computes anything and it computes it only for spheres.

### Why, given BUCH7 already exists

BUCH7 declines a figured surface, and says why: Buchdahl gives the aspheric scheme in
the monograph but never published the arranged table for it. That is the one part of
this subject with no printed answer to check against, and guessing at it would have
been worse than declining.

The arrangement has since been reconstructed. This macro is it, and BUCH7 is left
exactly as it was — the spherical routine is the one checked against Buchdahl's own
printed numbers, and it is not disturbed by any of this. On a lens of spheres the two
macros agree entry for entry, which is the first thing to check and costs nothing.

### The arrangement, in one paragraph

Sec. 85 splits every tertiary total into a hat pass carried on the incidence ratio `q`
and a check pass carried on the height ratio `rho`. Three things that are not in the
printed scheme are needed to make it come out:

1. Members one to five of the barred q-side secondary accumulation come from the
   identities of M Sec. 22, not from the dagger recursion.
2. The sixth member, which the identities cannot supply (M Sec. 19 says so outright),
   comes from a **dual run** of the whole scheme — the two rays interchanged and the
   refractive indices negated, paper XII Sec. 6 — in which the accumulated `s_1p` is
   `-S-bar_6q`. The negation is not optional: Table I is built on `g/N1 = 1`, and
   `1/g = y_p v_q - y_q v_p` changes sign under the swap. Without it the dual agrees
   at ratio -1.00 on one surface and -0.30 on the next.
3. The figuring's D half of (60.3) rides the incidence ratio, so it belongs in the hat
   pass — in the intrinsic secondaries **and** in the five M entries, each of which
   carries the intrinsic secondary it follows from with coefficient one.

### Status

**Done, and checked against two independent routes.** The macro runs four passes of the
scheme — direct and dual, each unfigured then figured — and a trivariate polynomial
algebra for the figuring cubics of Secs. 77-79. A lens of spheres takes the first pass
alone, which is the routing that keeps the spherical result bit-for-bit what BUCH7
gives.

| checked on | against | result |
|---|---|---|
| Kingslake double Gauss, 9 spheres | BUCH7, all four stages | identical at every printed digit |
| the same, Table I entry by entry | BUCH7's table | 155 entries x 9 surfaces, 1170 numbers identical |
| F4, a parabolic mirror | FIFTHORD | eighteen identical, B, B5 and B7 all zero |
| F3, a conic with r^4, r^6 and r^8 | FIFTHORD | eighteen identical |
| F3 | **FORBES.ZPL, seventh order** | **all twenty tau identical** |
| F6, a triplet with two figured surfaces | FIFTHORD | eighteen identical |
| F6 | **FORBES.ZPL, seventh order** | **all twenty tau identical** |
| F4, a parabolic mirror | the C# program, all three orders | all thirty-seven identical (22 September 2026) |
| F10, a spherical mirror | the C# program, and BUCH7 | all thirty-seven identical to both |

The FORBES comparison is the one this macro exists to pass. The two share no arithmetic
past the paraxial trace — Buchdahl's arranged tables against an order-doubling series
trace — so agreement to the printed digits on a figured lens is worth a great deal more
than either macro agreeing with itself.

`tau1` is printed twice, once as `B7` out of the fifth-order working and once as what
the scheme reaches through Table I. On a lens of spheres those agree exactly; on the
figured designs above they also agree exactly, which is a check needing no second macro
at all.

### Four faults found on the way, and what each one teaches

**A stale workspace slot.** `X3 = beta1 S2 + beta2 S1 + beta3`, and beta3 sets only four
of its twenty coefficients. It was being built in the workspace slot that had just held
S3's second product, so those four overwrote the stale product and the other sixteen
kept it — visible only in X3's fifth and sixth cubic coefficients, `xi eta zeta` and
`xi zeta^2`, the two of the first six beta3 does not write. Because the stale product
carried `tau2` and `sigma2`, which carry `c1`, it made nu-prime depend on the figuring
where it must not. Eighteen of the twenty totals were already exact; only `T5` and its
barred partner moved. **Any polynomial built by naming a few coefficients must be zeroed
first unless the whole workspace is known clean.**

**A case collision, the trap this folder already documents.** `aCb`, the aspheric
contribution to C-bar, and `acb`, the accumulated third order B, are the same variable:
ZPL names are case-insensitive. At a figured surface the first destroyed the second, so
every induced term containing it — `B5`, `F1`, `F2`, `M1`, `M2` and `B7` — came out
wrong while the seven that do not contain it stayed exact. There is now a scan over the
whole file for identifiers differing only by case.

**Why the one-surface fixtures could not find it.** On F3 and F4 the stop sits *on* the
figured surface, so the chief-ray height there is zero, the corrupting term is
identically zero, and the collision wrote a harmless zero. It took a six-surface lens
with the stop elsewhere to expose it. A ladder of fixtures is not padding.

**Two more ZPL traps**, which join those already recorded here. The bound of a `FOR`
may not contain a bracketed index — ZPL splits the arguments on commas before it looks
at them, so `mon(6, m)` is torn in half and reported as a missing index, an improper
format, and then an infinite loop. And `VEC1` to `VEC4` are all there are: writing
`VEC6(i) = x` does not fail as an out-of-range vector but as *"Variable must be followed
by = sign"* on a line that plainly has one, because `VEC6` is read as an ordinary
variable name and the bracket after it is unexpected. Everything else here is a
`DECLARE`d array, and a declaration that comes after its first use produces that same
misleading message.

### Limits, which the macro enforces rather than documents

`STANDARD` and `EVENASPH` surfaces only, declined by name, and only `PARM` 1 to 4 —
r^2 to r^8. An r^10 term or above cannot reach the seventh order; it is not being
ignored, it genuinely does not appear, and the macro says so when it meets one.

**A figured flat facing collimated light is declined.** There the marginal incidence is
zero, `q` is infinite, and the finite coefficients arrive only after terms carrying
different powers of `q` cancel. Bending the surface makes it exact again. The macro says
so rather than returning a number that looks like an answer; the third and fifth orders
are unaffected and still stand.

One optical surface is enough — and that is not a relaxation for its own sake. A single
figured surface is the sharpest test the aspheric block has, because nothing is
accumulated ahead of it and every induced term is identically zero, so a fault cannot
hide in the induced part.

It is slow: four passes of the scheme, and a polynomial algebra under them.

**Mirrors, changed in September 2026 and run in OpticStudio.** The F/number took the
magnitude of the image-space index, as FIFTHORD does, but the seventh order's conversion
1/(N'v'_pk) took it signed - so after an odd number of mirrors tau2..tau20 came out in the
opposite frame from the third and fifth printed beside them. The C# program had the same
split, and on the parabola every tau was then minus what reflected real rays give while the
third and fifth agreed with them. Both now take the magnitude. The fixtures are
`F4_parabolic_mirror.zmx` and `F10_spherical_mirror.zmx`. Run on both, it reproduces the C#
program on all thirty-seven to every printed digit: on F4 the third and fifth are unchanged
and the tau carry the signs reflected rays give, and on F10 every number equals BUCH7's. On a
reflecting system its closing note now points at RAYINV rather than FORBES, which declines.
Established on ONE reflection: whether OpticStudio's `ISMS()` flags the mirror surface or every
space after an odd number of reflections has not been determined, and on a single mirror the two
readings agree. A two-mirror system has not been run through the macros; the C# program has been
measured on one, Thompson's telescope.

### Three switches, all off by default

`srf = 1` **breaks every coefficient down surface by surface**, and splits the third and
fifth order three ways: what the surface generates on its own, what its figuring adds,
and what was **induced** in it by the surfaces ahead of it. It is the printout a
designer works from rather than a diagnostic, and it is the answer to a question a
total cannot be asked - not "is this design wrong" but "which surface, and is it that
surface's own fault". The two readings call for opposite actions: an intrinsic
aberration is corrected where it is generated, an induced one is a reaction to
something upstream and correcting it *here* is a second wrong balancing a first.

Shafer put the case for it in 1989 and this is what he asked for: "This can only be
done effectively, however, if the 5th-order aberration surface contributions are broken
into two components: the intrinsic component and the induced component" - his example
being a Bouwers whose mirror shows induced spherochromatism because the front lens's
axial colour changes the beam diameter reaching it. See [docs/references.md](../docs/references.md).

Three things to know about the printout:

- Every number is in transverse measure, the same as the system totals, and **each
  column adds down to the total printed above it**. That is a check on the macro you
  can make by eye, and the seventh-order tables print their sum row so you can.
- There is **no induced term at third order**. A third-order contribution is built from
  that surface's own quantities alone, so that block has three rows where the
  fifth-order block has four. Nor is there a figuring term in `Pi`: the Petzval sum
  depends on the vertex curvature and the indices, and a figured surface has the same
  vertex sphere as the sphere it was figured from.
- The seventh order is printed per surface but is **not split**. At seventh order the
  surface's own quantities and the accumulated ones enter through the same Table I
  entries, and separating them would be a reconstruction of Buchdahl rather than a
  reading of him. Where the seventh order does have an unambiguous answer - `B7`, which
  is `tau1` - it is in the fifth-order block and split like the rest.

Under the tables is one number per surface: the induced share of the fifth-order
magnitude it carries, summed over the thirteen coefficients so that no single one of
them decides it. Near zero the surface is on its own; near one, almost everything it
carries was handed to it and the fix is upstream.

**It is not bounded by one, and a value above one is the reading worth having.** It says
the induced part is larger than the total, so the intrinsic and the induced are opposing
each other and what the surface reports is the residue left after they cancel. On the
F6 triplet two surfaces come out at 1.45 and 2.41 - surface 3's `N2` is intrinsic
`3.64E-02` against induced `-9.17E-02`, and the `-5.53E-02` it reports is what survives.
Such a surface looks quiet in any per-surface total and is not: two large terms are
standing against each other there, and anything that disturbs either - a bend, a
thickness, a melt - moves the residue by far more than its own size suggests.

`chk = 1` prints the bridge constants that carry the figuring into the scheme, and the
ten tertiary totals with their barred partners before Table II mixes them. The bridge
constants are measured per quantity and **must come out the same on every surface**; if
they do not, the coefficients for that lens are not to be trusted.

`dbg = 1` prints Table I entry by entry, the two passes surface by surface, and the
figuring cubics. It is verbose — 155 rows per surface — and it is what to turn on when a
coefficient looks wrong and you want to see which entry it came from.

## STRESS.ZPL

Sasian's lens stress and relaxation parameters - the power distribution `W`, the symmetry
about the stop `S`, and the real-ray metric `R` - as system totals and, which he does not
tabulate, per surface.

J. Sasian, *Introduction to Lens Design*, Cambridge University Press, section 12.2. The
quantities are his (12.5) to (12.8); the reference values are his Table 12.1 and the
n sin(I) plot is his Figure 12.3. He says in that section that the W and S values and
those plots were produced by writing a macro inside a lens design program. This is one.

### Why

A lens acquires its power either gradually, spread over several weak surfaces, or
violently, by pitting strong positive against strong negative. The second buys degrees of
freedom - a flat field, mainly - and pays for them in higher-order aberration and in
tolerances. Nothing in a spot diagram distinguishes the two, and nothing in a Seidel table
does either: both report the aberration that survived, not the effort spent producing it.

`W` measures how hard the surfaces are working, `S` how far the lens departs from symmetry
about the stop, and `R` does what `W` does but on real rays, so it knows about the aperture
and field the lens is actually used over. Smaller is more relaxed in all three.

The normalisations are the interesting part and they are Sasian's. `W` carries the factors
`n_k' u_k'` and `(1 - m)` that make it independent of scale and of the conjugate. `S`
carries the stop factors `Abar_stop` and `y_stop` as well, which additionally make it
independent of field and of F/#. So these are numbers you can compare between two lenses
that share nothing.

### What this adds to the parameters as published

`W` and `S` are each one number for a whole lens. That tells you a lens is stressed
without telling you where. Both are RMS over surfaces, so the macro prints the per-surface
term going into each sum; the surface with the largest term is the one carrying the
stress. On the Cooke sample below, surface 4 carries the largest weighted power and
surface 3 the largest asymmetry, and they are not the same surface.

### Status

| what | checked against | result |
|---|---|---|
| W and S, and both per-surface tables | an independent implementation outside OpticStudio, from the same prescription | agrees; W = 1.055890, S = 0.917432 |
| the conventions behind them | Sasian's Table 12.1 | 1.06 and 0.92 here against his 1.12 and 0.89 - a different triplet |
| n sin(I), all four rays | an independent real ray trace, itself checked against OpticStudio's own ray intercepts to 2.3E-14 lens units | agrees to every digit printed |
| every ZPL function and keyword used | the ZPL reference in the OpticStudio help | 26 functions and 13 keywords, all present |

### What is actually proven, and what is not

**The arithmetic is checked; the absolute values are not, and cannot be from published
numbers.** Sasian's Table 12.1 gives W = 1.12 and S = 0.89 for *his* Cooke triplet. The
OpticStudio sample used here is a different lens, so the 1.06 and 0.92 obtained cannot be
compared with his entry by entry. What that near-agreement does settle is the
**conventions** - the sign of the chief-ray refraction invariant, whether the stop factors
sit in the numerator or the denominator, what `k` counts - because getting any of those
wrong moves the answer by a factor, not by six per cent. Two different triplets landing
within six per cent of each other is what a correct arrangement looks like; it is not what
a wrong one looks like.

The arithmetic itself is checked the only way it can be, against a second implementation
of the same equations written outside OpticStudio and driven from the same prescription.

One thing that check needed first: the independent computation used catalogue glasses, and
a glass name alone does not say whose glass it is. The indices OpticStudio actually used
were recovered from its own optical path lengths - optical path over geometric path,
segment by segment - and match the catalogue values to ten decimal places for both SK16
and F2. Without that, an agreement to four figures would have proved nothing about the
fourth figure.

### Two checks the macro makes on itself, every run

Both are printed, and both are the sort that can fail.

`w = phi*y` must equal `n'u' - nu`, which is Sasian's (12.5) and an identity of the
paraxial refraction equation. It holding says the index handling, the mirror sign flips
and the ray basis are mutually consistent. OpticStudio's sign convention makes it
`n'u' - nu = -phi*y`; Sasian writes them equal, which is the same statement in his.

`n sin(I)` from the `RAID` operand must equal `n sin(I)` worked out from the ray direction
and the surface normal, which come from a different part of the program. These part company
on a system with coordinate breaks or tilts, which is one of the things that makes such a
system not the sort this macro is for - so the check earns its place rather than merely
passing.

### The one arbitrary choice, made visible rather than buried

Sasian writes "a system of k surfaces" and does not say what to do about surfaces that are
not optical surfaces at all - a dummy plane, a stop floating in air. They contribute
nothing to either sum, since `phi` is zero and so is `delta(u/n)`, but counting them still
shrinks W and S by `sqrt(kept/counted)`.

This macro counts a surface when the index changes across it, so refraction and reflection
count and dummies do not, which makes W and S properties of the lens rather than of how the
file was typed. **The value on the other convention is printed underneath**, because the
choice is arbitrary enough to be worth seeing both ways. On a lens with no dummy surfaces
the two lines are equal; where they differ the reader decides. The Cooke sample has no
dummy surfaces, so it does not discriminate - a Petzval objective with a separate stop
surface would move by about five per cent.

### Limits

Rotationally symmetric and sequential; a non-axial system is refused. Not afocal, since
both W and S divide by `n_k' u_k'`. Unit magnification makes Sasian's `(1 - m)` factor
vanish and W and S with it - a property of the definition, not of the lens - so the macro
says so and prints the unnormalised RMS rather than dividing by zero.

**Aspheres are fine here**, which is worth stating because `BUCH7.ZPL` in this folder
refuses them. W and S are built on the paraxial trace and a conic or an even-asphere term
does not touch it: such a surface carries no vertex power, which is the same reason Sasian
gives for aspheres being absent from the Petzval sum. R is a real-ray quantity and
OpticStudio's own angle of incidence carries the figuring correctly.

Vignetting factors move the real rays, so they move R and the n sin(I) table but not W and
S. If factors are set, the `Py = +/-1` rays sit at the vignetted edge of the pupil rather
than the full one - usually what you want, never what you expect. The macro reports it.

### Another ZPL trap, which this macro found

Those in the BUCH7 section above all still apply. This one is new:

4. **`OPEV` traces rays of its own.** It evaluates an optimization operand, and the ray
   operands trace to do it, so `RAYL`, `RAYM`, `RANX` and the rest - which report the ray
   traced *last* - come back describing the operand's ray rather than yours. Reading them
   after an `OPEV` call returns plausible numbers for the wrong ray. Everything wanted from
   a `RAYTRACE` is therefore read out before the first `OPEV` of that pass, not interleaved
   with it.

The case-insensitivity trap deserves its restatement. Writing this macro it bit three times
in one sitting, twice in the PowerShell used to check the macro rather than in the macro
itself: `$I` silently clobbered a loop counter `$i` and produced an infinite loop, and `$t`
silently clobbered a thickness array `$T` and produced a ray trace that agreed with
OpticStudio on surface 1 and disagreed on every surface after it. Both looked like physics
bugs. Neither was.

### Expected output, Cooke 40 degree field

Run against `C:\ProgramData\Zemax\Samples\Sequential\Objectives\Cooke 40 degree field.zmx`
at the primary wavelength, 0.55 um. Stop at surface 4, object at infinity, so `m = 0` and
Sasian's `(1 - m)` factor is 1.

    Lagrange invariant         1.819851
    n_k' u_k'                 -0.100000
    Stop surface                      4
    Abar at the stop           0.478801
    Marginal y at the stop     3.800852

     Surf            phi              y        w = phi y         W term
        1  2.828288E-002  5.000000E+000  1.414144E-001 -1.414144E+000
        2  1.428784E-003  4.715974E+000  6.738109E-003 -6.738109E-002
        3 -2.807578E-002  3.825940E+000 -1.074163E-001  1.074163E+000
        4 -3.073415E-002  3.800852E+000 -1.168160E-001  1.168160E+000
        5  7.813498E-003  4.162261E+000  3.252182E-002 -3.252182E-001
        6  3.384596E-002  4.241508E+000  1.435579E-001 -1.435579E+000

     Surf           Abar     delta(u/n)              y         S term
        1  1.736295E-001 -5.371140E-002  5.000000E+000  2.562266E-001
        2  4.944718E-001 -9.444108E-002  4.715974E+000  1.210146E+000
        3  5.003551E-001  1.327002E-001  3.825940E+000 -1.395893E+000
        4  4.788009E-001  9.153203E-002  3.800852E+000 -9.153203E-001
        5  5.073451E-001 -5.953575E-002  4.162261E+000  6.908357E-001
        6  1.864152E-001 -1.165440E-001  4.241508E+000  5.063556E-001

    W  (index-changing surfaces)    1.055890
    S  (index-changing surfaces)    0.917432
    W  (all surfaces counted)       1.055890
    S  (all surfaces counted)       0.917432

     Surf   marginal(0,1)     chief(1,0)         (1,+1)         (1,-1)
        1   2.271324E-001  1.631584E-001  3.765930E-001  5.027624E-002
        2   1.613820E-001  4.690591E-001  3.125282E-001  6.119860E-001
        3   3.216885E-001  4.652921E-001  1.678046E-001  7.226106E-001
        4   2.717694E-001  4.778641E-001  6.982383E-001  2.172980E-001
        5   1.367177E-001  4.984117E-001  6.219992E-001  3.529278E-001
        6   3.306640E-001  1.395184E-001  1.614162E-001  4.432840E-001
        R   2.526545E-001  3.998682E-001  4.412349E-001  4.596190E-001

The two W and S pairs are equal because this file carries no dummy surfaces; on a file that
does, they will differ.

Sasian remarks of his own triplet that surfaces 3 and 4 carry the largest n sin(I). They do
here too - 0.72 on surface 3 for the `(1,-1)` ray and 0.70 on surface 4 for `(1,+1)` - on a
different triplet, which is the sort of agreement a structural claim about a lens form
should produce.

### Provenance

Written from Sasian's published equations. `FIFTHORD.ZPL` and the `Wavefront Aberrations
from Sasian.zpl` macro shipped with OpticStudio were read as references for the ZPL
interface only - keyword names, the paraxial conventions, the handling of mirrors through
`ISMS` - and no code from either is reproduced here. Every ZPL function and keyword used
was checked against the ZPL reference in the OpticStudio help rather than written from
memory.

---

## ROBB.ZPL

The RMS spot radius predicted from the coefficients BUCH7 computes, with no rays traced.
Robb's analytic merit function.

P. N. Robb, "Analytic merit function based on Buchdahl's aberration coefficients,"
*J. Opt. Soc. Am.* **66**(10), 1037-1041 (1976). His Eq. (2) writes where a ray meets the
Gaussian image plane; his Eq. (4) is the variance of that over the pupil.

**Run this one.** It calls `BUCH7.ZPL` itself to get the coefficients it needs, so BUCH7's
whole output appears first and the spot table follows it. Both files are runnable on their
own: BUCH7 alone prints the coefficients as it always has, and ROBB prints those and then
what they imply about the spot.

The call goes in one direction only. BUCH7 publishes its totals into the call buffer and
calls nothing; ROBB calls BUCH7 and reads them back. Were both to call each other the pair
would recurse until OpticStudio gave up, so the call lives in exactly one of them.


### The `+B9` column, and why it stands apart

ROBB prints five truncations, not four. The first four are nested cuts of **one** series —
third, third plus fifth, plus B7, full seventh. The fifth adds **ninth-order spherical
alone**, read from call-buffer slot 47.

Adding it cost almost nothing, which is the point worth recording: the macro has no table of
constants. Every term is stored as four numbers — coefficient, ρ power `a`, field power `b`,
θ-function id — and the pupil average of any product is closed form. B9 is `a = 9, b = 0` with
**the same θ functions B and B7 already use** (2 in `eps_y`, 5 in `eps_z`), so none of the
eleven surviving θ pairings changed and no integral was re-derived. It is four lines in each
component.

**It needs a spherical design**, because ROBB calls `BUCH7.ZPL`, which declines a figured
surface - and at this order there is no `BUCH7_ASPH` to fall back on, since no aspheric
arrangement exists at the ninth. On a figured design ROBB reports no coefficients at all, from
BUCH7's own refusal, and the question of a ninth-order column does not arise.

**On axis the column is exact and completes the order.** Every other ninth-order term carries
a power of the field, so at `H = 0` spherical *is* the whole ninth order, and the gap between
the last two columns there is a real measurement.

**Off axis it is a fragment of that order** — some twenty terms absent — and a partial order
carries no guarantee of improvement, because terms of one order routinely cancel against each
other. This repository has measured that even *complete* orders are not monotone: on the worst
design in [../docs/spot-prediction.md](../docs/spot-prediction.md) the seventh-order truncation
misses by 134 µm, the ninth by 11, and the eleventh by 11.8 — worse than the ninth. The macro
prints that caveat above the table rather than leaving it here.

It is kept as a separate column, not folded into `full 7th`, for exactly that reason. Column
three is already `+B7 only` — the previous order plus spherical alone — so the shape is one the
file established before this.

**Run, and the axial columns reproduce by hand.** On KingslakeDG the four original columns and the
new one come out exactly as an independent evaluation of `RMS^2 = sum_ij c_i c_j 2/(a_i + a_j + 2)` from
the five published coefficients predicts, to all seven printed digits:

| | 3rd | 3rd+5th | +B7 | +B9 |
|---|---|---|---|---|
| computed by hand | 4.682353e-03 | 3.362864e-03 | 3.157879e-03 | 3.141444e-03 |
| ROBB printed | 4.682353E-03 | 3.362864E-03 | 3.157879E-03 | 3.141444E-03 |

`B9 = −6.104427E−05` moved the axial spot by −0.52 per cent. At `H = 0` the `+B7 only` and
`full 7th` columns are identical, as they must be: on axis B7 is the whole of the seventh order.

It was written the other way round first - BUCH7 as the parent calling ROBB - which works
equally well, because the call buffer carries data in both directions: the shipped
`PARENT.ZPL`/`CHILD.ZPL` example ends with the child writing a value the parent then reads.
It was inverted because the natural thing to run is the macro whose answer you want.

### Why

Thirty-four coefficients are hard to read as a statement about image quality. One RMS
radius is easy to read and throws away most of what they say. Printing both, truncated
four ways - with ninth-order spherical as a fifth column, below - says something neither gives
alone: **how much of the spot each order accounts
for**, and so whether a design is limited by aberration the third order already describes
or by something only the seventh reaches.

On the Cooke triplet below that difference is the whole story. On axis the third order
over-predicts the spot by a quarter; at the edge of the field by more than a factor of two.
A designer reading only the third order would be looking at the wrong lens.

### How it works, and why there is no table of magic constants in it

Every term of `eps_y` and `eps_z` has the form `coefficient * rho^a * H^b * f(theta)`, so
the pupil average of a product of two terms separates completely:

    <eps_i eps_j> = c_i c_j * <f_i f_j>_theta * H^(b_i + b_j) * 2/(a_i + a_j + 2)

The radial factor is the average of `rho^(a_i+a_j)` over the unit disc, in closed form. The
theta factor takes one of **eleven** values, because only eleven of the eighty-one pairings
of the nine theta functions survive a full turn - everything odd in theta vanishes, the
cosine family never pairs with the sine family, and the constant pairs only with itself.

So the macro carries 25 `eps_y` rows, 19 `eps_z` rows and a 13-entry theta table, and the
whole double sum is elementary. **The pupil is not sampled at all**: there are no rings, no
spokes and no convergence to worry about. The only approximation is the truncation of the
series itself.

Assembling the same thing as an explicit quadratic form would have meant transcribing 232
constants. This way the transcription is 44 rows whose structure mirrors `Prms.cs` line for
line, which is also what makes it checkable against it.

### Status

| what | checked against | result |
|---|---|---|
| the term tables and theta table, **parsed out of the ZPL file itself** | `Prms.cs` in this repository, all 34 coefficients distinct and non-zero | agrees to 5.6E-16 relative at H = 0, 0.3, 0.7 and 1.0 |
| the same, on a real lens | `Prms.cs` on the Cooke triplet, three orders x eleven fields | 33 of 33 to every digit printed |
| the whole chain end to end | the `rms_spot` MCP tool on the same lens | 2.061997E-02 against 0.020619968 at full field |
| single aberrations acting alone | the closed forms in `PrmsTests.cs` - `B/2`, `B5^2/6`, `B7^2/8` | exact |
| BUCH7's half of the interface | the transverse totals this README already records for BUCH7 | all eighteen identical |
| every ZPL function and keyword used | the ZPL reference in the OpticStudio help | 11 functions, all present |

### The checker was made to fail before it was believed

A checker that reports nothing wrong proves nothing until it has been shown able to report
something. Two deliberate faults were injected into `ROBB.ZPL` and the checker re-run:

| fault injected | worst relative error reported |
|---|---|
| none - the file as shipped | 5.6E-16 |
| one theta index wrong, `VEC3(59)` written as `VEC3(57)` | 1.2E-2 |
| one rho power wrong, the M3 term's `a` from 3 to 2 | 7.7E-3 |

The first of those is not hypothetical. It is the error that was actually made writing the
table, and it is what the check caught. Note that it left `H = 0` exactly right and only
showed up off axis, which is precisely the shape of mistake that a single on-axis spot
check would have passed.

The checker reads the term tables **out of the macro file** rather than from a copy of
them, so what it validates is the file as written, not what it was meant to say.

### The interface to BUCH7

BUCH7 gained a stage E which publishes its totals into the call buffer. It does not call
anything; ROBB calls BUCH7 and reads the buffer back after it returns. The buffer holds 51
numeric slots and 34 are needed, so the whole set fits:

| slot | contents |
|---|---|
| 1-4 | `B F C Pi` - third order |
| 5-15 | `B5 F1 F2 M1 M2 M3 N1 N2 N3 C5 Pi5` - fifth order |
| 16 | `B7`, which is Robb's `tau1` |
| 17-34 | `tau2` to `tau19` - seventh order |
| 45 | `761976`, a handshake, written last of all |
| 46 | 1 if the seventh order is present |

Slot 45 is written **last**, so it is set only if BUCH7 got all the way to the end. If BUCH7
declined - an aspheric surface, an afocal system, a chief ray carrying no field - the slot
is still zero and ROBB says there is nothing to report rather than printing a table. That
distinguishes a failed run from a lens whose coefficients are genuinely zero, which is what
a well-corrected system would legitimately produce.

Two smaller things the inversion forced, both to avoid resting on undocumented behaviour.
`CALLMACRO` is the **first statement** in ROBB, before it touches any vector: BUCH7 uses
`VEC1` to `VEC4` heavily and sizes them to its own needs, so ROBB builds its tables after
BUCH7 has finished with them rather than around it. And ROBB's settings - `eps`, `dbg`,
`nsteps` - are assigned **below** the call, because BUCH7 happens to use the names `eps`,
`dbg` and `kk` itself and ZPL nowhere documents whether a child shares the parent's ordinary
variables. The call buffer exists precisely so that one need not rely on it.

Everything is in **transverse measure** and in lens units. Unconverted coefficients would
give a number in no units at all.

**Distortion is deliberately absent.** `E`, `E5` and `tau20` displace the whole patch
without changing its size, so they cannot enter a spot radius, and Robb's corresponding
terms vanish identically. Thirty-four coefficients cross, not thirty-seven.

### What it is blind to, which its author said first

The reference is the **Gaussian image plane**, so this is not the spot at best focus and
moving the image plane does not change it. Robb says so in his own Conclusions: the image
plane "ceases to become a design variable", optimising the last thickness "will not have
the slightest effect on the solution and will only consume computing time or cause the
optimization algorithm to become unstable", and focus must be adjusted afterwards by the
method of Sands (1973). A design whose spot is dominated by defocus will read better here
than it deserves.

It is also referenced to the **centroid**, as Robb specifies - the variance about the mean
intersection, not about the chief ray.

### The defocus warning, and the run that earned it

This is the one misreading the output invites, and prose was not enough to prevent it.
**Most lens files are not at paraxial focus**, because that is not where an optimiser puts
the image plane, and a spot diagram taken at the drawn plane is then not comparable with
this table at all.

The first real run of the macro was on the OpticStudio `Double Gauss 28 degree field`
sample. ROBB reported 27.36 um on axis; OpticStudio's spot diagram said about 8 to 10. That
looks like a broken macro and is not one:

| field | traced at the file's own plane | traced at **paraxial focus** | ROBB predicts | error |
|---|---|---|---|---|
| H = 0 | 8.32 um | 27.251 um | 27.357 um | +0.4 % |
| H = 0.71 | 6.10 um | 24.483 um | 24.739 um | +1.0 % |
| H = 1 | 9.95 um | 29.880 um | 29.039 um | -2.8 % |

The sample's image plane sits **0.1834 mm inside paraxial focus**. At f/2.99 that defocus
alone is about 21.7 um RMS. Move OpticStudio's image plane to the paraxial distance and its
own traced rays agree with ROBB to well under a per cent on axis. Nothing was wrong.

So the macro now **measures** the defocus rather than cautioning about it. It traces a
paraxial marginal ray, takes the paraxial focus as `-y/u` from the last surface, compares
that with `THIC`, and prints the comparison **before** the table, because it decides what
the table can honestly be held against:

    Image plane:
       paraxial focus, from the last surface     57.497969
       where this lens file puts it              57.314538
       defocus                                    0.183431
       RMS radius a PERFECT lens would show at that plane   2.172386E-02

    *** WARNING - THIS LENS IS NOT AT PARAXIAL FOCUS. ***

A lens that *is* at paraxial focus gets a single line saying the table is directly
comparable with a spot diagram.

The blur figure is exact for defocus acting alone: a ray at normalised pupil height `rho`
misses the plane by `defocus * u * rho`, and the average of `rho^2` over the pupil is `1/2`,
so the RMS radius is `|defocus * u| / sqrt(2)`. On the Double Gauss the paraxial trace's own
marginal ray height at the image surface is 0.030722, which is `0.183431 * 0.167486` - the
same quantity before the `sqrt(2)`.

It is reported as the **scale** of the discrepancy and not as a correction to apply. Defocus
and aberration vary together across the pupil, so they do not simply add in quadrature: 8.32
and 21.72 do not make 27.25. The only honest comparison is to move the plane.

### How good the prediction is, measured rather than asserted

[docs/spot-prediction.md](../docs/spot-prediction.md) compares it against traced rays on five lenses at both
conjugates. The table below is consistent with it to the digit: at `H = 0` the full seventh
order gives 1.378753E-02 against a traced 0.013698, which is the +0.7 per cent that
document records, and at `H = 1` it gives 2.061997E-02 against a traced 0.023604, the -12.6
per cent it records there.

So the honest summary is that the full seventh order is worth about one to two per cent
across four fifths of the field and **falls apart at the very edge**. The third order alone
is not an estimate of this lens at any field.

### Limits

`tau2` to `tau19` come from BUCH7's Table I arrangement, which is for **spherical surfaces
only**. On a figured system BUCH7 refuses before reaching stage E, so this macro is not
reached either. Slot 46 exists so that a different parent can say it has only third and
fifth order, in which case `full 7th` comes out **identical to `+B7 only`**. Two columns
reading the same is the symptom, and it means the last column is not the full seventh order
and must not be read as one.

Stage E is reached only if stage D completed. BUCH7's earlier refusals - an afocal system,
a chief ray carrying no field - skip it, which is right: there would be no seventh order to
send.

### The optional second route

Setting `dbg = 1` at the head of ROBB also integrates Eq. (2) numerically over an
equal-area pupil grid, 24 rings by 48 spokes, and prints the two side by side. It is off by
default because it is slow and because its sampling error - around 1E-5 relative - is
larger than anything the analytic route can get wrong subtly. It is there because a table
of powers and indices transcribed by hand is exactly the sort of thing that can be wrong in
a way that still looks plausible.

The kth ring sits at `rho = sqrt((k - 1/2)/n)`, the equal-area midpoint rather than the
outer edge. Sampling at the edge weights the pupil outward and converges from above;
[docs/spot-prediction.md](../docs/spot-prediction.md) has the numbers.

### Expected output, CookeTriplet

Run **ROBB** against `CookeTriplet.zmx` at wavelength 2 (0.55 um). BUCH7's own output comes
first, through stage D, and this follows it. The coefficients are the ones this README
already records for BUCH7.

That fixture is at paraxial focus, so it draws the "directly comparable" line rather than
the defocus warning - unlike the Double Gauss sample above.

    RMS spot RADIUS in lens units, referenced to the CENTROID, at the GAUSSIAN image plane
    (recorded before the +B9 column was added; the four columns shown are unchanged by it)

         H            3rd        3rd+5th       +B7 only       full 7th
      0.00  1.740010E-002  1.433710E-002  1.378753E-002  1.378753E-002
      0.10  1.767375E-002  1.437992E-002  1.383288E-002  1.382400E-002
      0.20  1.852787E-002  1.455733E-002  1.402013E-002  1.397830E-002
      0.30  2.004758E-002  1.499266E-002  1.447805E-002  1.437061E-002
      0.40  2.233527E-002  1.581682E-002  1.534174E-002  1.514452E-002
      0.50  2.547637E-002  1.708349E-002  1.666392E-002  1.637350E-002
      0.60  2.952267E-002  1.871917E-002  1.836584E-002  1.795926E-002
      0.70  3.449472E-002  2.053853E-002  2.025730E-002  1.957802E-002
      0.80  4.039287E-002  2.230508E-002  2.210065E-002  2.071257E-002
      0.90  4.720787E-002  2.381192E-002  2.369201E-002  2.087998E-002
      1.00  5.492754E-002  2.499401E-002  2.497242E-002  2.061997E-002

**Read the last two columns against each other.** `B7` was available long before any of the
Table I work - FIFTHORD prints it, and so does BUCH7's own fifth-order working - so the gap
between those two is what the eighteen tau add that nothing else could give you. On axis
they are identical to every digit, because on axis there is no field and spherical
aberration is the whole of the seventh order. At `H = 0.9` they are 2.369E-02 against
2.088E-02, which against a traced 0.020799 is +13.9 per cent against +0.4 - a factor of
thirty-five in the error, and all of it from coefficients that were not previously
available.

Three of those can be checked without this repository at all: the `full 7th` column at
`H = 0`, `0.7` and `1.0` must equal the `prms` values the `rms_spot` tool reports for
wavelength 0.55 at fields 0, 14 and 20 degrees, which are 0.013787534, 0.019578024 and
0.020619968.

### Provenance

Written from Robb's published equations and from `src/AberrationCalculator.Core/Aberrations/Prms.cs`,
which is this project's own implementation of them. The `Wavefront Aberrations from
Sasian.zpl` macro shipped with OpticStudio was read as a reference for the ZPL interface
only, and no code from it is reproduced here. Every ZPL function and keyword used was
checked against the ZPL reference in the OpticStudio help rather than written from memory.

---

## FORBES.ZPL

Third-, fifth- and seventh-order aberration coefficients by G. W. Forbes' Lagrangian series
trace — **including on conics and even aspheres**, which is what `BUCH7.ZPL` cannot do and
the reason this exists.

G. W. Forbes, "Order doubling in the computation of aberration coefficients," *J. Opt. Soc.
Am.* **73**(6), 782 (1983), Sec. 3(a).

### Why, given BUCH7 already exists

BUCH7 refuses a conic or an aspheric term, and the refusal was honest: Buchdahl gives the
aspheric scheme in the monograph but never published an **arranged table** for it the way
Table I arranges the spherical case, so a transcription would have had no printed answer
to check against.

That arrangement has since been reconstructed, as `BUCH7_ASPH.ZPL`, and the two macros now
agree on all twenty seventh-order coefficients on every figured refracting design tried. On a
mirror this macro declines, and RAYINV is the check. Which makes
this route MORE valuable rather than less: the agreement is between Buchdahl's arranged
tables and an order-doubling series trace sharing no arithmetic past the paraxial ray, so
each is now the other's strongest check. Before, there was nothing to check the aspheric
arrangement against at all.

Forbes removes the difficulty rather than solving it. He writes the ith surface as

    x = f_i(y . y)

with `f_i` a power series. A sphere, a conic and an even asphere differ **only** in the
coefficients of `f_i` and are traced by identical code. There is no D and L split, no hat
and check pass, no carrying ratio — the entire class of fault the aspheric Buchdahl
arrangement is prone to cannot be expressed in this formulation.

### The one idea it rests on

For a rotationally symmetric system every quantity carried through a ray trace is either a
scalar function of the three rotational invariants

    p = y0 . y0 ,    k = y0 . b0 ,    u = b0 . b0

or a vector `S y0 + T b0` with `S` and `T` such scalars. So the whole trace reduces to
arithmetic on **one data type**: a power series in `p`, `k`, `u` truncated at total degree
three — twenty coefficients.

Each invariant is quadratic in the ray coordinates, so a term of degree `m` multiplies `y0`
or `b0` to give order `2m + 1`. Degree 1 is the primary aberrations, degree 2 the secondary,
degree 3 the tertiary.

Forbes' headline result, order doubling, is **not** used. It reaches order `2M` from a trace
carried to order `M`, an economy for orders far beyond the seventh; here the concatenation
machinery it needs would be the larger part of the work and the larger part of the risk.

### Status

| stage | contents | checked against | result |
|---|---|---|---|
| 1 | the truncated series ring in `p, k, u` | `InvariantSeries` in the C# | multiply, inverse and square root **bit-identical**, 0.0 |
| 2 | the trace — transfer and refraction, carrying S, T, V, W | `ForbesTrace` in the C# | 4 series x 20 coefficients, 7E-15 on the Cooke |
| 3 | conics and even aspheres | the same, with a conic and r²–r⁸ figuring | 8E-16; and confirmed in OpticStudio on `CookeTriplet_SPOTM_START_LO_ASPHERE` |
| 4 | the 5 third-, 12 fifth- and 20 seventh-order coefficients | BUCH7 and the C# | **all 37 to every printed digit** |

The macro re-checks the ring on **every run** before it traces, and declines to trace if any
identity fails.

### What makes it possible in ZPL at all

`GOSUB`, `SUB` and `RETURN` — up to a hundred subroutines. A series multiply is called dozens
of times per surface and inlining it would be unreadable.

But **all ZPL variables are global**, so a subroutine has no locals. Two rules keep that
safe, and an awk audit enforces both:

1. Every subroutine's working variables carry a prefix of its own — `q` for `smul`, `n` for
   `sinv`, `z` for the scale ring, and so on.
2. **A subroutine that calls another copies its parameters first.** `sa`/`sb`/`sc` are
   argument registers a nested call overwrites, so `sinv` begins `nsrc = sa`, `ndst = sb`.

The audit found one real fragility before it could bite — `sdiff` sharing the loop counter
`gi` with the trace.

### Three faults it shipped with, and how each was found

Worth recording because the three needed quite different tools.

**A wrong θ index in `ROBB`'s sibling table** — caught by the model checker, which reads the
tables out of the file and was itself made to fail first.

**An empty `FOR` range.** Trap #3 in the BUCH7 section above: ZPL reports an empty range as
an infinite loop rather than executing it zero times. `gsolve`'s elimination runs
`FOR gr = gc + 1, nunk - 1, 1`, which is empty on the last column. Knowing the trap and
writing it into this file's own header was **not enough** — so there is now an audit for it,
and it reports that this was the only place in `macros/` it could have bitten.

**A reversed copy, which is the instructive one.** `zscal` is `zb := za * zfac`, source in
`za`. In `mkrhs` the two were reversed, so instead of copying the computed direction cosine
into `bm` it copied the still-empty `bm` into `sin`. Every ray then had aperture and **no
direction**, so the invariants `k` and `u` vanished.

The symptom was sharp: every coefficient carrying no field came out *exactly* right — `B`,
`B5`, `tau1` — and every coefficient carrying field came out wrong, with `Pi` and `Pi5`
collapsing to machine zero, because with only the meridional rows surviving their columns
become proportional to `C` and `C5`.

**Three hypotheses read off that signature were all wrong.** So the macro was made to print
its own intermediates for one ray shape instead, and `bm` came back identically zero while
`ym` and `p` were exact — which named the line. Forcing `bm` to zero in the working outside
OpticStudio then reproduced the wrong output *digit for digit*, which is how it is known to
have been the only fault rather than one of several. All 28 copy and scale call sites were
then listed and checked for direction; `mkrhs` was the only reversed one.

That diagnostic is kept behind `dbg4 = 0` rather than deleted. It cost one run and settled a
question three rounds of code-reading had not.

### Where this and BUCH7 should agree, and where they should not

**On a system of spheres** all three orders must match BUCH7, and that is checked: all 37
coefficients agree on the Cooke triplet to every printed digit. Two wholly different schemes
— Buchdahl's arranged tables and Forbes' series trace — sharing no arithmetic and no code.

**On a figured system the comparison splits, and it splits exactly:**

- **`tau1`, which is `B7`, IS RIGHT in the Buchdahl route even for an asphere.** It is not
  reached through the aspheric table at all — it falls out of the fifth-order working, which
  is why BUCH7 arrives at it twice over and gets the same number both ways.
- **`tau2` to `tau20` are not.** Those nineteen rest on an arrangement re-derived with no
  printed answer to check against.

*How wrong, and against what:* the judge is neither scheme but a **ray inversion**, which
recovers the coefficients from real traced rays. By that measure the Buchdahl route gets
`Ladder1_A4`, `Ladder1_Conic` and `Ladder1_FiguredSphere` right, and two others wrong —
`Ladder2_A4_Second` by 6.85 per cent and `Ladder2_FiguredSphere_Then_A4` by 7.75. So it is
not wrong on every asphere; it is wrong on some, **with no way to tell in advance which**,
which is what makes it unusable rather than merely imprecise.

The third and fifth orders are not affected either way.



### The finite conjugate

The trace is right at either conjugate — the object position enters it only through the ray
basis. Stage 4 is where the two differ, and it now carries both.

**Why they differ.** At infinite conjugate the beam is collimated, so a ray's height at the
input base plane is `rho·epr − ep·tan(field)` and its sagittal direction is zero: a
collimated beam tilts in the meridian only. At a finite conjugate the ray is the straight
line from the object point to the chosen point on the entrance pupil. Its position where it
crosses surface one's vertex plane is a weighted mean of the two, so it stays exactly linear
in the scale — but its **direction** depends on the pupil point as well as the field, and its
**sagittal direction no longer vanishes**, because a skew ray from a finite object point is
tilted out of the meridian.

**A correctness fix that came with it.** `epr` was the marginal ray's height at surface one.
That is the entrance pupil radius only when the beam is collimated. On the Cooke at 250 mm
the marginal ray is at 4.7799 at surface one where the pupil radius is 5.0000 — a 4.4 per
cent aperture error that would have gone unnoticed, because nothing else in the output would
have looked wrong. It is now carried forward to the pupil, `epr = y1 + ep·u0`, which reduces
to the old form at infinite conjugate.

**Checked** on the Cooke triplet with the object 250 mm away, against BUCH7 and the C#: all
thirty-seven coefficients to the seven figures those print.

    Third order
       B    -4.373036E-02   F     2.897349E-02   C     5.350709E-02
       Pi   -1.581199E-01   E    -1.023213E-01

    Fifth order
       B5    1.093968E-02   F1    1.901174E-02   F2    1.297716E-02
       M1    2.808447E-02   M2    2.275551E-02   M3    3.281702E-02
       N1   -1.647215E-02   N2   -5.407001E-02   N3   -1.943421E-02
       C5   -1.531533E-02   Pi5   7.294664E-02   E5   -1.838898E-02

    tau1    2.305287E-03  ... tau20  -2.181701E-03

`tau1` equals BUCH7's `B7` here as it does at infinite conjugate, and **FIFTHORD agrees with
BUCH7 on all eighteen at this conjugate too** — so the target FORBES has to hit is
corroborated by something outside this repository before FORBES is compared with it.

### A first-order check that was wrong, and how it showed

The trace prints three constant terms as "first-order checks needing no reference". Two of
them were mislabelled.

Only `-1/V(0,0,0)` is the focal length at either conjugate, because `V` is the optical power.
`T(0,0,0)` and `S(0,0,0)` describe where the input base plane sits relative to the output
plane, and those are a conjugate pair **only** when the object is at infinity. Labelled as
universal truths, they printed this on a finite-conjugate lens:

    T(0,0,0) is the focal length         58.780874
    -1 / V(0,0,0) is the same thing      49.999982
    S(0,0,0) is zero at focus            -0.235123

Two contradictory numbers under labels asserting they are the same thing, and no comparison
between them — a check that should have fired and stayed silent instead. It now labels them
per conjugate, explains what they are when the object is finite, and **actually compares**
`T` against `-1/V` at infinite conjugate rather than printing both and hoping the reader
notices. `S(0,0,0)` at a finite conjugate is the magnification, which on this lens reads
-0.235123 and is right.

### The aspheric case, checked against something that ships with OpticStudio

This is the check that matters most, because it needs **nothing from this repository** and it
works on exactly the systems BUCH7 declines.

`FIFTHORD.ZPL` ships with OpticStudio and **carries aspheres** — it prints the aspheric
contribution surface by surface. Its transverse totals are the same eighteen quantities
`FORBES.ZPL` prints: `B` through `E5`, and `B7`, which is `tau1`.

Run on `CookeTriplet_SPOTM_START_LO_ASPHERE.zmx` — r⁴ and r⁶ figuring on two surfaces, a
lens BUCH7 refuses outright:

| | FORBES.ZPL | FIFTHORD | rel diff |
|---|---|---|---|
| B | −5.882911E-04 | −5.8829E-04 | 1.9E-06 |
| F | −2.649959E-03 | −2.6500E-03 | −1.5E-05 |
| C | 6.039270E-02 | 6.0393E-02 | −5.0E-06 |
| Pi | −1.291448E-01 | −1.2914E-01 | 3.7E-05 |
| E | −2.084687E-02 | −2.0847E-02 | −6.2E-06 |
| B5 | −4.218846E-03 | −4.2188E-03 | 1.1E-05 |
| F1 | 2.350097E-02 | 2.3501E-02 | −1.3E-06 |
| F2 | 1.567027E-02 | 1.5670E-02 | 1.7E-05 |
| M1 | −5.119278E-03 | −5.1193E-03 | −4.3E-06 |
| M2 | 7.573690E-03 | 7.5737E-03 | −1.3E-06 |
| M3 | −4.985060E-03 | −4.9851E-03 | −8.0E-06 |
| N1 | −4.463661E-03 | −4.4637E-03 | −8.7E-06 |
| N2 | 9.183179E-03 | 9.1832E-03 | −2.3E-06 |
| N3 | −4.087099E-03 | −4.0871E-03 | −2.4E-07 |
| C5 | −1.474444E-02 | −1.4744E-02 | 3.0E-05 |
| Pi5 | 5.624082E-02 | 5.6241E-02 | −3.2E-06 |
| E5 | −8.256648E-04 | −8.2566E-04 | 5.8E-06 |
| **B7 = tau1** | **1.268066E-03** | **1.2681E-03** | −2.7E-05 |

**All eighteen agree to every digit FIFTHORD prints.** The worst difference, 3.7E-05, is its
own five-figure rounding.

Two things follow that were previously asserted rather than shown.

**`B7` survives the aspheric case.** FIFTHORD's `B7` and Forbes' `tau1` agree on a figured
lens. That is the direct evidence for the split stated above — `tau1` comes out of the
fifth-order working, not the aspheric table, so it is right where `tau2` to `tau20` are not.

**The third and fifth orders are unaffected by the aspheric difficulty**, which the split
claimed but nothing here had demonstrated until now: seventeen of the eighteen are third or
fifth order, and all seventeen agree.

The third order can also be had from **Analyze > Aberrations > Seidel Coefficients**, which
carries aspheres as well. Converting its sums to transverse measure — `W040 = SI/8`,
`W131 = SII/2`, `W222 = SIII/2`, `W220 = SIV/4`, `W311 = SV/2`, then differentiating the
wavefront and dividing by `n'u'` — gives `B −5.900E-04`, `F −2.650E-03`, `C 6.0395E-02`,
`Pi −1.29145E-01`, `E −2.0845E-02`, against the macro's five to within the figures
OpticStudio prints. A third route, agreeing.

**What this does not check, and what now does.** FIFTHORD stops at `B7` and the Seidel
analysis at third order, so neither reaches `tau2` to `tau20` on a figured system. That gap
is now closed from the other side by `RAYINV.ZPL`, which recovers the coefficients from real
traced rays and uses no series at all: on this same lens all nineteen agree with FORBES to
better than 7E-04. See its section below.

### Limits

**Both conjugates.** Checked at 250 mm on the Cooke against BUCH7 and the C#: all
thirty-seven coefficients to the seven figures those print.

`STANDARD` and `EVENASPH` surfaces only. Any other type is declined **by name**, because
`PARM` on a toroid or a grating is not an aspheric coefficient and reading it as one would
be a confident wrong answer rather than an error.

Only `PARM` 1 to 4 take part — r² to r⁸. An r¹⁰ term or above enters the figure past the
truncation and **cannot reach the seventh order**; it is not being ignored, it genuinely
does not appear, and the macro says so when it meets one.

It is slow. Seventy-five ray shapes, each costing a few hundred series multiplies.

**Mirrors are declined** (September 2026). The refraction takes `INDX` on each side, which
carries no reflection sign, so a mirror used to be traced as a refraction into the same
medium - no bending, near-zero coefficients, no complaint. Forbes carries a reflection by
letting the index change sign, but the root that picks the reflected cosine has to be chosen
explicitly and is not written; the C# series trace declines a mirror for the same reason.

### Another ZPL trap

`FORMAT n.0 INT` **prints a zero as blank.** The first draft's monomial table came out with
the `m = 0` row unlabelled and every zero power missing, so `p^0 k^0 u^3` read as
`blank blank 3`. Fixed format has no such trouble. Nothing else in `macros/` prints an
integer that can be zero, so the other macros are unaffected.

### Expected output, CookeTriplet

Run against `CookeTriplet.zmx` at the primary wavelength. The trace prints S, T, V, W first;
these are the coefficients that follow.

    Third order
       B    -3.480019E-02   F     6.174430E-03   C     4.453368E-02
       Pi   -1.285785E-01   E     9.292942E-03

    Fifth order
       B5    7.691912E-03   F1    9.196030E-03   F2    6.270814E-03
       M1    2.732059E-02   M2    2.163254E-02   M3    3.182199E-02
       N1   -1.457431E-02   N2   -3.253849E-02   N3   -1.521610E-02
       C5   -1.303741E-02   Pi5   5.733012E-02   E5    1.484277E-03

    Seventh order, Buchdahl's tau. tau1 is B7.
       tau1    1.681450E-03  tau2    1.556696E-03  tau3    1.146383E-03
       tau4    3.820781E-03  tau5    3.086343E-03  tau6   -7.418192E-04
       tau7   -7.553284E-03  tau8   -7.157220E-03  tau9   -4.282416E-03
       tau10  -5.072596E-04  tau11   1.341386E-02  tau12   1.461144E-03
       tau13   3.345284E-03  tau14   3.453259E-03  tau15   1.049465E-02
       tau16   9.644176E-03  tau17   2.707664E-03  tau18  -4.593489E-02
       tau19  -1.394715E-03  tau20   1.083496E-03

Every one of those matches BUCH7 or the C# on the same lens. And eighteen of them can be
checked without this repository at all, on a FIGURED lens as well as a spherical one, by
running FIFTHORD - see "The aspheric case" above.

The trace also prints two first-order checks needing no reference: `T(0,0,0)` is the focal
length and `-1/V(0,0,0)` is the same thing by a different route. On the Cooke both read
49.999982.

### Provenance

Written from Forbes' published equations and from `src/AberrationCalculator.Core/Forbes`,
which is this project's own implementation of them. Every ZPL function and keyword used was
checked against the ZPL reference in the OpticStudio help rather than written from memory.

---

## RAYINV.ZPL

Third-, fifth- and seventh-order coefficients recovered from **real traced rays**, by
inverting the transverse aberration rather than by any series.

### Why, and it is a narrow reason

BUCH7 computes the coefficients from Buchdahl's arranged tables; FORBES from Forbes' series
trace. On a system of spheres those two agree on all thirty-seven to every printed digit,
and FIFTHORD — which ships with OpticStudio and needs nothing from this repository — agrees
with both on eighteen of them.

On an **aspheric** system that structure falls apart:

| | reaches |
|---|---|
| BUCH7 | nothing — it declines outright |
| FIFTHORD | third order, fifth order, and `B7`. Eighteen quantities, then it stops |
| Seidel analysis | third order, then it stops |

So `tau2` to `tau20` on a figured lens — **nineteen coefficients, and precisely the ones
Buchdahl's aspheric arrangement gets wrong** — had nothing to be checked against except a
second implementation of the same series method. That is not a check; it is the same
argument twice.

This macro is the third opinion. It uses no series at all.

### How

Each of the 75 shapes is traced at a **ladder of twelve scales** — shrink the ray by `s` and
the pupil goes to `s·rho`, the field to `s·h`. The paraxial image height is subtracted, an
odd polynomial in `s` is fitted through the twelve landings, and the coefficients of `s³`,
`s⁵` and `s⁷` are the third-, fifth- and seventh-order transverse aberration of that shape,
*measured*. Those feed the same 20-unknown model FORBES uses.

**The ladder is the whole game and is not arbitrary.** Measured against `B7` on an axial fan
— where the degree-seven part is that coefficient alone — a ladder to `s = 1` with powers to
`s¹¹` recovers it only to about 1.5 per cent, because the ninth order and above are still
large at full aperture and the fit cannot separate them. Twelve points to six tenths with
powers to `s¹⁵` recovers it to better than one part in a million.

**The paraxial image height is subtracted before fitting**, and that is fatal rather than
untidy if skipped: at seven tenths of the field it is some twelve millimetres sitting on a
third-order term of two hundredths, so the fit would have to cancel three orders of magnitude
to reach the aberration at all.

**The fit is done in `t = s/s_max`, not in `s`.** The powers run to fifteen, so in `s` the
normal-equations matrix spans thirty orders of magnitude and its diagonal ratio is 6.8E+06;
rescaled it is 4.2. That was tested *before* the macro was written, because it decides
whether an ordinary Gaussian elimination suffices or a Householder QR is needed — which is
the difference between something ZPL can carry and something it cannot. It suffices.

### The model is shared with FORBES on purpose

Both routes fit the same twenty coefficients through the same linear model — Robb's Eq. (2)
with one coefficient set to one at a time. **The independence lives entirely in the
right-hand side**: series arithmetic there, OpticStudio's own ray trace here. Were the two to
use different bases they would not be computing the same quantity and the comparison would
mean nothing, so the shared model is what makes the check a check rather than a coincidence.

### What it establishes

On `CookeTriplet_SPOTM_START_LO_ASPHERE` — r⁴ and r⁶ figuring on two surfaces, a lens BUCH7
refuses:

| | RAYINV, real rays | FORBES, series | FIFTHORD | rays vs series |
|---|---|---|---|---|
| B | −5.882910E-04 | −5.882911E-04 | −5.8829E-04 | −1.7E-07 |
| C | 6.039270E-02 | 6.039270E-02 | 6.0393E-02 | 0 |
| Pi | −1.291448E-01 | −1.291448E-01 | −1.2914E-01 | 0 |
| B5 | −4.218855E-03 | −4.218846E-03 | −4.2188E-03 | 2.1E-06 |
| Pi5 | 5.624080E-02 | 5.624082E-02 | 5.6241E-02 | −3.6E-07 |
| **B7 = tau1** | 1.268010E-03 | 1.268066E-03 | 1.2681E-03 | −4.4E-05 |

The third order agrees to seven digits, the fifth to about 1E-05, and — the point of the
exercise — **`tau2` to `tau20` agree to better than 7E-04**, worst at `tau15`.

Three routes, on an asphere: Buchdahl's tables (via FIFTHORD, for the eighteen it reaches),
Forbes' series, and real rays. Nothing is left resting on a single method.


### Why the aspheric result can be believed

RAYINV's whole value is on figured systems, where nothing else reaches `tau2` to `tau20`.
But that is exactly where it cannot itself be checked — so its credibility has to be
established somewhere else and carried over.

That somewhere else is a **sphere**, where the answer is independently known three times:
BUCH7 from Buchdahl's arranged tables, FORBES from Forbes' series trace, and FIFTHORD from
OpticStudio's own installation, all agreeing to every printed digit. On the Cooke triplet
RAYINV reproduces it:

| | worst disagreement with the series routes |
|---|---|
| third order | **identical to all seven printed digits**, all five |
| fifth order | 1.7E-06, at `F1` |
| the twenty tau | 1.0E-03, at `tau12` |
| `tau1` against `B7` | 1.3E-05 |

Those are from a run made *after* the conjugate-boundary fix, against a FORBES which was
itself re-checked against the C# on the same lens and matched 37 of 37 to every printed digit
— so the "series routes" column is not one implementation but two agreeing. The third order
and `tau1` reproduce the earlier record exactly; `tau12` was recorded as 9.1E-04 before and
measures 1.0E-03 now, which is the same coefficient at the same size and is quoted here as
measured rather than as remembered.

**That is the argument.** The ladder, the odd fit, the rescaling, the model and the solve are
the same code on a sphere and on an asphere — only the surface figures differ. Shown correct
where the answer is known, it can be used where it is not.

Without this run the aspheric agreement between RAYINV and FORBES would prove much less, because
two routes agreeing can be two errors meeting. Agreeing *and* both reproducing a triply-known
answer on a sphere is a different claim, and it is the one that carries the aspheric case.

### Read the tolerance correctly

**Expect a few parts in ten thousand, not machine precision**, and the macro says so in its
own output. A traced ray carries every order at once and the fit has to separate them, so
this arrives with a fitting residual where a series arrives exactly.

A disagreement at that level is the fit. A disagreement at **per cent** level is not — and on
an aspheric system it would be worth taking seriously, because that is the size of the error
the aspheric Buchdahl arrangement carries (6.85 and 7.75 per cent on two of the ladder
fixtures).

### The finite conjugate

RAYINV now carries both conjugates, and the interesting part is how little the trace itself
had to change: nothing. The ray basis is OpticStudio's own normalised pupil and field
coordinates, and OpticStudio already aims those correctly whichever side the object is on.
What changed is the two quantities the macro subtracts and divides by.

**What the conjugate does not decide.** The scale ladder has to move the field variable in
exact proportion to `s`, and whether the normalised field coordinate already does that turns
on the **field type**, not on the conjugate:

| field type | mapping | why |
|---|---|---|
| angle | `atan(s·h·tan a_max) / a_max` | the coordinate is a fraction of the *angle*, the field variable goes as its *tangent* |
| object height | `s·h`, unchanged | already linear |
| paraxial image height | `s·h`, unchanged | already linear |
| real image height | **refused** | see below |

The angle mapping is identical at both conjugates, and the reason is worth stating because it
is what makes the finite conjugate nearly free: OpticStudio gives the **object-space chief
ray** the field angle itself at either conjugate, so a finite object sits at height
`-(t + ep)·tan(field)` — still exactly linear in the tangent, only with a different constant
in front, and the constant divides out of a ratio. That was measured against OpticStudio,
not assumed. On the Cooke at 250 mm the chief ray's object-space slope at full field is
`0.363970234` against `tan 20° = 0.363970234`.

The earlier version applied the angle mapping whatever the field type was. That was right for
the one conjugate it accepted and wrong for paraxial image height, which it would have taken
without comment.

**Real image height is refused.** OpticStudio iterates the chief ray until it lands where it
was asked to, which makes the image height linear in the coordinate *by construction* and
folds the distortion into the field variable itself. The ladder would then be shrinking a
field of `s + O(s³)` rather than `s`, and every order above the third would take a share of
the order below it — third order right, the rest quietly wrong. That is the worse of the two
ways to fail, so it is refused rather than noted.

**Two corrections came with it.** `efl = -y₁/u_k` is the focal length only when the marginal
ray arrives collimated; at a finite conjugate it is a working distance and means nothing, so
the focal length is taken from the system data there and is printed for orientation only.
And the paraxial image height is now **measured** — the paraxial chief ray projected to the
same plane the real rays are caught on — rather than computed as `efl·tan(field)`, which is
that quantity divided by the image space index and so parts company with it when the image
sits in glass.

### The field mapping is proved on each run, not assumed

All of the above rests on one claim: that the normalised coordinate the macro builds puts the
paraxial image height at exactly its intended fraction of full field. That claim is about
OpticStudio's field conventions, not about optics, and a mapping off by half a per cent would
move every field-dependent coefficient and read as a fault in FORBES.

So the macro proves it, on the actual system, every run. The chief ray is sent at the
coordinate the ladder would use for seven tenths of full field, and the paraxial image height
has to come back as seven tenths of `hpmax`. One paraxial ray. The residual is printed.

The check is worth having because it fires. Against the shipped mapping on the finite Cooke
it reads `7.9E-17`; with the mapping deliberately broken:

| injected fault | residual | verdict |
|---|---|---|
| angle fields treated as linear | 1.5E-02 | refused |
| maximum field angle wrong by 2 per cent | 1.5E-02 | refused |
| `hmax` from the *refracted* slope at surface 1 | 5.0E-03 | refused |
| the mapping as shipped | 7.9E-17 | passes |

The third of those is not hypothetical. It is the mistake this work nearly shipped: the
direction cosines OpticStudio reports at a surface are those *after* refraction, and reading
them as the incoming slope makes the max field angle come out 16.4° instead of 20°.

### Checked at the finite conjugate

On `CookeTripletFinite` — the Cooke triplet with the object 250 mm away, angle fields, 20°
— against the answer this repository already agrees on by two other routes (FORBES' series
trace and Buchdahl, corroborated by FIFTHORD on the eighteen it reaches):

| | worst disagreement, real rays against the series |
|---|---|
| third order | 6.4E-08, at `E` |
| fifth order | 2.9E-06, at `N2` |
| the twenty tau | 6.9E-04, at `tau12` |

That is the same band as the infinite-conjugate spherical run, and worst at the same
coefficient. The replica used for this check traces its own skew rays and shares no code with
the ZPL or with the C#; it was itself validated against OpticStudio's own intercepts and
direction cosines, meridional and skew, to 1E-10 before being trusted.

### Limits

Field type angle, object height or paraxial image height. Real image height and theodolite
angles are refused, for the reason above.

**Real ray aiming is refused.** With it on the normalised pupil coordinate aims at the real
stop rather than at the paraxial entrance pupil, so `rho` stops being the variable these
coefficients are defined in — it differs from it by the pupil aberration. The third order
would be untouched, since that enters at degree three and shifts the third order only at
degree five, and the fifth and seventh would disagree with FORBES by a few per cent. That
reads exactly like a fault in one of the two routes and is not one. Ray aiming is far
likelier to be on at a finite conjugate than at infinity, so this guard earns its keep mostly
on the path just added. Paraxial ray aiming is noted rather than refused.

Rotationally symmetric — now checked, not assumed — and sequential. Rays that will not trace
are dropped rather than fudged, and the count is printed; if many are lost the remaining
shapes may not span the coefficients, and it says so.

**Mirrors, changed in September 2026 and run in OpticStudio.** OpticStudio keeps one
frame through a mirror, while Buchdahl measures the image-space transverse aberration along
an axis each reflection reverses. So after an odd number of mirrors every landing is the
negative of what the coefficients describe, and every recovered coefficient came out negated.
The landings are now turned by the parity of the surfaces whose glass is `MIRROR`. A first
version read it from the marginal ray's direction cosine after the last surface, and on F4
that did not fire - the ray is traced with `PARAXIAL ON`, where `RAYN` is not the direction
of travel - so the coefficients still came out negated. That is how it was found. With the
parity counted, it agrees in sign on every coefficient with BUCH7_ASPH on both mirror fixtures,
and with BUCH7 on F10 (BUCH7 declines the figured F4). It counts glasses rather than calling
`ISMS`, so the question below does not arise for it - but it too has been run on one reflection
only. On F10, at five degrees, the third order is exact, the fifth to 1E-6 and the seventh
to 1-4E-3 of the largest tau - the same size as what it returns for tau13..tau20, which are
exactly zero, so that is the fit's resolution here. On F4, at half a degree, the field-dependent
tau sit at that floor and only tau2 and tau3 stand above it.

It is **not** slow, despite the 900 real rays — those turn out to be cheap next to FORBES'
series arithmetic, which was the opposite of what was expected.

### Another ZPL trap: comments are whole-line only

**Comments are whole-line only.** There is no trailing comment: a `!` after a statement does
not start one, so the parser reads on and reports the next word as an unknown symbol. Eight
declaration lines here carried their descriptions on the right and the macro would not run at
all — `Syntax error: Unknown symbol SCALES`.

It only surfaced now because the other macros contained **zero** trailing comments between
them; whole-line commenting had been a habit rather than a known requirement. There is an
audit for it now, which ignores `!=` and any `!` inside a string literal.

Worth recording honestly: every audit in this folder is retrospective. Each was written after
a trap fired — case-insensitive names, empty `FOR` ranges, `INT` printing zero as blank, now
trailing comments. They catch recurrences, not first occurrences. What has actually caught
faults *before* they shipped is the numerical checking against an independent implementation,
not the syntax auditing.

### A further ZPL trap, and the one that has cost the most

**`INFINITY` in the lens data editor is exactly `1E10` to `THIC()`.** OpticStudio displays the
word; ZPL hands back the sentinel. So a conjugate test written as

```
IF (ABSO(THIC(0)) > 1E10) THEN infconj = 1
```

is **false for a lens at infinity**, because `1E10 > 1E10` is not true. The boundary has to go
on the infinite side, which is the polarity BUCH7 has always had and the reason it never
showed there:

```
infconj = 1
IF (ABSO(THIC(0)) < 1E10) THEN infconj = 0
```

**How it surfaced.** RAYINV was run on an infinite-conjugate asphere and printed `Conjugate
finite`, an object distance of ten thousand million, an object height of `-4.09` that means
nothing, and a magnification of zero — while the thirty-seven coefficients came out **right**.
They came out right because RAYINV keeps the conjugate and the field mapping deliberately
apart: the conjugate decides only what is printed, the field type decides the ladder, and the
paraxial image height is measured rather than derived from either. The wrong label was the
only symptom it could produce.

**FORBES carried the identical line, and there it was not cosmetic.** `infconj` selects the
ray basis in `mkrhs`, so on any `DISZ INFINITY` lens FORBES built rays from `objd = 1E10`:

| | height at base plane | direction (sin) |
|---|---|---|
| what it should build | 0.8099181901 | 0.3420201433 |
| what it did build | 4.9999999895 | 0.0000000009 |

Full aperture, and **0.0 per cent of the intended field** — the same signature as the reversed
`zscal` this file was bitten by once before: every coefficient carrying no field exactly
right, every coefficient carrying field wrong.

**How long FORBES was wrong, and how that was closed.** `infconj` arrived with the finite
conjugate; after that change FORBES was verified at the *finite* conjugate only. So from that
commit until this fix its infinite-conjugate path was untested and, by the analysis above,
wrong — while the infinite-conjugate results recorded in this file were measured *before*
`infconj` existed and so describe a version that predates the defect.

That gap is now closed by measurement rather than by argument. FORBES was re-run on
`CookeTriplet` against the C# reference, and **all thirty-seven coefficients are identical to
every printed digit** — including `tau1` against `B7` by Buchdahl's own working. The third and
fifth order are worth separating out: the C# reaches them through Buchdahl's scheme and the
ZPL through Forbes' series trace, so that agreement is two methods rather than two runs.

The lesson is the ordering, not the arithmetic. The finite conjugate was added, verified at
the finite conjugate, and pushed — and the conjugate that already worked was never re-run.
A change that introduces a branch invalidates the branch it did not take.

**What this says about the checking.** The macro that found it is the one whose only symptom
was a misprinted label, and it found it because a human read the header rather than the
coefficients. Nothing numerical would have caught it: RAYINV's own answers were right, and
FORBES' were wrong in a way that still agrees with everything on an axial fan. There is no
audit for this one and it is not clear what an audit would look like — the fix is that the
conjugate is now decided in one place per macro, with the boundary written down beside it.

---

## ASPHWHERE.ZPL

**WHERE** an asphere would do the most good, and what it would buy. Third order only.

### Why, and it is the opposite question

Every other macro in this folder says what is wrong with the lens in hand, per surface.
This one asks it the other way round: an asphere costs money, so if you can afford one,
where does it go?

The source is G. Schulz, "Aspheric surfaces", in E. Wolf (ed.), *Progress in Optics XXV*
(Elsevier, 1988), Sec. 3.3, reading the Delano `(H, h)` diagram:

> The normalized ratio `H_i/h_i` quantitatively determines the weight by which an aspheric
> deformation of the surface `i` influences the individual Seidel aberrations. If `|H_i/h_i|`
> is small, primarily the spherical aberration can be controlled, and if `|H_i/h_i|` is
> large, primarily the distortion. If `|H_i/h_i|` has a value in a middle range, the
> asphericity parameter influences all five Seidel aberrations by nearly equal weights.

### It changes nothing. The lens is not touched.

No surface type is switched, no `PARM` is written, and nothing is restored afterwards because
nothing is disturbed. The whole calculation is paraxial — two ray traces, the index steps, and
arithmetic. A figure is never applied; what is reported is what one *would* do.

That is the design rather than a convenience. **A macro that edits the lens must put it back,
and if it stops on an error part way through it leaves the design damaged on your screen.**
This one cannot.

It follows that the macro cannot mark its own work. To see a prediction come true, type the
coefficient into `PARM 2` of the surface named and watch **Analyze > Aberrations > Seidel
Coefficients** move — which is more convincing than a macro agreeing with itself.

### r⁴ and only r⁴, and that is forced rather than chosen

A deformation `r^n` first reaches transverse order `n-1`, so `r^6` reaches the fifth order and
`r^8` the seventh, and **neither can move a Seidel sum at all**. `r^4` is the only figuring the
third order can see.

| | why it is not here |
|---|---|
| `r^2`, `PARM 1` | not figuring — a curvature change, and it moves the focal length. Where a design carries one the paraxial rays already account for it; nothing here proposes changing it |
| a conic | would do the same job at this order and is deliberately not offered. A conic `K` on vertex curvature `c` is an `r^4` coefficient of `Kc^3/8`, so either states the same third-order correction — but a conic **drags `r^6` and `r^8` along with it**, growing as `K^2` and `K^3`. They agree here and part company at the fifth and seventh orders, which this macro cannot see. A conic also cannot figure a flat at all, since `c^3` is then zero |
| Petzval | **cannot be reached by any of it.** `S4` depends on the surface curvatures and the index steps alone. Schulz says the same — the Petzval condition "cannot be influenced by asphericities" — and it is why a flat field can need a glass change or another element rather than a figure |

### What it prints

Four tables, in Buchdahl's convention throughout: `B` spherical, `F` coma, `C` astigmatism,
`E` distortion.

| table | what it is |
|---|---|
| `h`, `H`, `H/h`, *reaches* | the ratio, and a reading of it. The four sensitivities stand as `1 : H/h : (H/h)^2 : (H/h)^3`, so `H/h` alone fixes what a surface can reach. The bands are a reading of that ratio, not a second measurement |
| SAG to null `B`, `F`, `C`, `E` | the figuring that would null each sum on its own, **as sag at the semi-aperture, in lens units**. A row is **four alternatives, not four things at once**: one surface, one figure, and you choose which sum to kill |
| `A4` to null each | the same figures as `r^4` coefficients — the number to type into `PARM 2`. Nothing in the sag table is an `A4` and nothing here is a sag |
| what each choice buys | each row nulls its target from whichever surface leaves the design **in the best state overall — not the cheapest one** — and shows that state, so the collateral is visible: the other three move too, and a figure aimed at one sum can cancel a second for nothing or wreck it. Exact, not linearised |

The *reaches* bands are `|H/h| < 0.15` spherical essentially alone, `0.15` to `0.5` spherical
mainly with some coma, `0.5` to `2` all four comparably, and `≥ 2` distortion mainly. They say
how a surface **divides** its effect, never how much it has, and they are **not** ranked against
what this design actually suffers from.

**OUT OF REACH** means the figure needed would be deeper than the surface is wide — not a
correction to that surface but a different one. It is what a surface **at the stop** returns for
coma, astigmatism and distortion: its chief-ray height is zero, so its leverage on those three is
zero and the coefficient that would null them is unbounded. The test is on the lens, not on the
arithmetic.

`size` weights the four equally and is **not a spot size** — it exists to rank rows that would
otherwise be compared four numbers at a time. Petzval is left out of it because no figure moves
it, so it would add the same constant to every row. *Cheapest* means **depth of glass to remove**,
a manufacturing cost and not an optical one; a surface far from the stop has a wide footprint, so
the whole of it must be figured to the quoted depth even though only the marginal-ray zone does
the work on spherical aberration. That mildly favours surfaces near the stop for a geometric
reason rather than an optical one.

### Checking it, and why the check is worth more than it looks

Against `abcalc --asphere-placement`, which is this repository's C# and shares no arithmetic with
the macro. The predicted coefficients must agree to every digit either prints.

The two work in **different conventions**: the C# in Welford's Seidel, the macro in Buchdahl's,
differing in sign and in scale. On a Cooke triplet the same spherical aberration reads
`+6.9600E-03` there and `-3.4800E-02` here. **The predicted coefficient is nevertheless identical
in both**, because it is a ratio — `-total/derivative` — and any common convention factor cancels
in it. So agreement is a real check on the arithmetic rather than on whether two conventions were
lined up correctly.

The `SAG` column is the one place the two can legitimately differ. The macro uses `SDIA`,
OpticStudio's own semi-aperture, which knows the real bundle; the C# has no ray tracer here and
falls back to `|h| + |H|` where a file states no semi-diameter, which is a little larger — `9.08`
against `8.71` on a double Gauss. **The `A4` column cannot differ**: it has no aperture in it.
Nor can the ranking, which is by outcome.

The third-order sums it prints can be checked in turn against **Analyze > Aberrations > Seidel
Coefficients**, converted to transverse measure, and against `BUCH7.ZPL` in this folder, which
computes them by the same scheme.

### Limits, stated plainly

**STANDARD and EVENASPH surfaces only**, declined by name on anything else, because `PARM` on a
toroid or a grating is not an aspheric coefficient and reading it as one would give a confident
wrong answer rather than an error. Rotationally symmetric and sequential, either conjugate; an
afocal system is refused.

**Third order only — and at that order it is exact.** The aspheric contribution is exactly
*linear* in the coefficient, so the sensitivities are exact derivatives with no step size to
choose and the "what it buys" table is exact rather than linearised. That is a property of the
third order and not a claim about the macro. **A figure sized here will move the fifth and seventh
orders, and this macro does not look at them. Treat it as where to start.**
