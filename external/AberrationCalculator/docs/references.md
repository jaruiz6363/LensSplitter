# References

The method this program implements is not original work. It is Buchdahl's aberration
coefficients in Rimmer's notation, Robb's analytic spot-size integration over them, and
Rosete-Aguilar and Rayces's re-normalisation of them into comparable quantities. Nor is the
optimiser built on top of them: PSD is Dilworth's, the pattern search Hooke and Jeeves's, the
basin hopping Wales and Doye's - see [Optimisation](#optimisation). This file records what each
source contributes and whether it has been read.

Status: **[have]** the paper is in hand. **[wanted]** it is cited downstream but has not
been obtained here.

**The nine items still marked `[wanted]` have been looked for and are not expected to arrive**
(as of 18 September 2026), and the list should be read as a record rather than as a queue. Three
are theses - Rimmer's, Sands's and the two the nodal work rests on - which is why they are hard
to get. Two, Rayces (1964) and Nijboer (1943), are needed only if the wave-aberration
re-normalisation is ever implemented, which it is not. Two more, Shack and Thompson (1980) and
the Buchroeder and Thompson dissertations, are historical: Thompson (2005) is in hand and
supersedes them as a source of equations. **None of the nine is load-bearing for anything this
program computes**, and where one of them would have been - Rimmer's notation - the notation is
fixed instead by Robb's Eq. (2) and Johnson's Table I, both held. If that ceases to be true, this
paragraph is the thing that has to change first.

Note on the scanned papers: the text layer on the 1958, 1970, 1973 and 1976 scans is 1950s-70s OCR
and is unusable for anything mathematical - the tables and equations come out as noise. Their
prose is readable; their formulae are not. Working from them requires page images. Everything
quoted from Sands (1970) below is prose for that reason; his Eqs. (33)-(34) and (45) and his
Sec. VIII identities have been located but not transcribed. The same holds for Sands (1973):
his Tables I and II are the two things in this file most worth transcribing and neither can be
transcribed from the text layer.

## Primary sources

**[have] Buchdahl, H. A.**, *Optical Aberration Coefficients* (Oxford University Press,
London, 1954). Dover reprint, New York, 1968.
The origin of the coefficients this whole program computes.

**The Dover reprint carries the whole journal series as an appendix.** Sands (1970), who
cites the Dover edition as his reference 1, describes "the thirteen papers under the same
general title and reprinted at the end of OAC" and cites them by number throughout -
OACIII, OACVI, OACVII, OACXII. So holding the book holds papers I to XIII, and
`### The rest of Buchdahl's series` below is a numbering table rather than a shopping list.
XIV is 1969, after the reprint, and is held separately.

**This entry read `[wanted]` for far longer than it was true**, and it said the reprint was
"the highest-value single acquisition on this page". By then the monograph had been read
section by section and the whole aspheric tertiary result had come out of it: Secs. 84 and
85 off the page, Sec. 22's identities, Sec. 29 proving the (68.8) misprint rather than
inferring it, and XII Sec. 6's duality. A bibliography that says its principal source has
not been obtained, while the work visibly rests on it, misleads a reader about what the
results stand on - which is the one thing this file exists to prevent.

The sections consulted are Secs. 12-13, 19-20, 22, 29, 59-60, 62, 63-66, 67-68, 73, 74-76,
77-79, 80, 81, 83-85 and 218, together with Chapter V, Chapter VI's Table II, Appendix J
and the tables at pp. 143-146. They are held outside this repository, as page images: the
scans are not ours to redistribute and no path to them belongs in version control, for the
same reason `ZemaxPaths.props` is not committed.

**[wanted] Rimmer, M.**, M.S. Thesis, Institute of Optics, University of Rochester,
Rochester, New York, 1963.
Recast Buchdahl's coefficients into the notation used at the Institute of Optics, which
is the notation this program reports (B, F, C, Pi, E, B5, F1, F2, M1-M3, N1-N3, C5, Pi5,
E5, B7). Cited as reference [5] of Rosete-Aguilar and Rayces (1995).

Note: earlier versions of this repo cited Rimmer as "1962, University of Rochester Summer
School in Optics". That attribution came in with the implementation and could not be
verified. The primary literature gives the 1963 M.S. thesis, and that is what is cited
here now.

**[have] Robb, P. N.**, "Analytic merit function based on Buchdahl's aberration
coefficients," *J. Opt. Soc. Am.* **66**(10), 1037-1041 (1976).
DOI 10.1364/JOSA.66.001037 (OSA pattern - verify on retrieval).
`Prms.cs` implements this paper. Eq. (2) is the ray intersection polynomial; Eq. (4) is
its variance over the pupil. Two statements in it are load-bearing here:

- Distortion terms are identically zero in the spot size, because distortion moves the
  image without resizing it. This is why `E` and `E5` are absent from `Prms.Terms`.
- The Conclusions state that the image plane "ceases to become a design variable", that
  optimising the last thickness "will not have the slightest effect on the solution and
  will only consume computing time or cause the optimization algorithm to become
  unstable", and that focus must be adjusted afterwards by the method of Sands (1973).
  This is the defocus-blindness of PRMSA, documented by its author in 1976.

**[have] Rosete-Aguilar, M. and Rayces, J.**, "Re-normalization of Buchdahl-Rimmer
aberration coefficients to RMS expressions," *Proc. SPIE* **2730**, 499-502 (1996).
ISBN 0-8194-2111-1.
`ContributionAnalysis.cs` implements the geometric half of this paper - coefficients
re-normalised to RMS spot size, so that two aberrations with equal re-normalised values
do equal damage. The paper also covers the wave-aberration half in terms of the Strehl
ratio, which this program does not implement.

**[have] Rosete-Aguilar, M. and Rayces, J.**, "Renormalization of the Buchdahl-Rimmer
third- and fifth-order geometric aberration coefficients to rms wave aberration function
expressions," *J. Mod. Opt.* **42**(12), 2435-2445 (1995). DOI 10.1080/713824341.
The wave-aberration treatment, published a year earlier. Not implemented here; cited
because it is the fuller derivation of the re-normalisation idea.

**Welford, W. T.**, *Aberrations of Optical Systems* (Adam Hilger, Bristol), ch. 8.
The Seidel sign and normalisation convention `SeidelCoefficients.cs` follows. Edition and
year still to be pinned down for a proper citation.

## Cited by the sources above, not yet read

These are the references Robb and Rosete-Aguilar rely on. Two of them bear directly on
open questions in this program.

**[have] Buchdahl, H. A.**, "Optical Aberration Coefficients. III. The Computation of
the Tertiary Coefficients," *J. Opt. Soc. Am.* **48**, 747-756 (1958).
The route to seventh order. "Tertiary" is Buchdahl's word for it.

Robb assumed a COMPLETE seventh-order polynomial. He used "the first three polynomials
(of orders 3, 5, and 7)", and his Table I gives "Equations for the 37 S(J) coefficients",
of which S(5), S(17) and S(37) are carried but identically zero because distortion moves
the image without resizing it. Those three indices are the last of three blocks - 5
primary, 12 secondary, 20 tertiary - which is how his 37 partitions. His equations are
written in primary terms, secondary terms and tertiary terms, matching that split.

CONFIRMED. Robb Eq. (2), p.1038, names them: sigma_1..sigma_5, mu_1..mu_12, tau_1..tau_20,
with the distortion terms sigma_5, mu_12 and tau_20 falling at positions 5, 17 and 37.
No longer an inference.

This program supplies all 37: five primary, twelve secondary and the twenty tertiary, B7
among them - from Buchdahl's Table I on spherical designs and the reconstructed Sec. 85
arrangement on figured ones. (When this note was first written it supplied 18, the other
nineteen tertiary coefficients being absent, which was why full-field PRMS was then the weakest
number the report printed.)

What this paper provides:

- The full computing scheme for primary, secondary AND tertiary coefficients, as Table I.
  192 entries per surface. Buchdahl notes his assistants took about three hours per
  surface on desk calculators, which is a fair proxy for how much arithmetic it is.
- **Spherical surfaces only.** Aspheric figuring would still need separate treatment, as
  this program already does at third and fifth order.
- **Six identities between the tertiary coefficients** (Sec. 6), which Buchdahl uses as
  his own final check. That is a self-test independent of any oracle - worth having when
  transcribing a 192-entry table.
- Seventh order has eight aberration types: the six traditionally named ones plus, in
  Buchdahl's words, "two unnamed types". This program carries all eight, in the twenty tau
  (it carried only one, B7, when this was first written).
- A correction to the monograph: "Eqs. M (81.3) contain one (and only one) misprint. In
  fact, the product ApBF in the factor multiplying b in the equation for t5 should have a
  minus sign instead of a plus sign before it." Anyone implementing from the 1954 book
  alone inherits that error.

The paper is NOT self-contained. It defers to the monograph for the equations behind the
scheme - M Sec. 81 (Eqs. 81.3), Sec. 84, Sec. 22(b) and Eq. (22.27), and Sec. 218(a).
Those sections are needed before the scheme can be implemented.

Note that the 1954 book predates this paper by four years, and the Dover 1968 reprint is
normally unchanged, so the book alone may not carry the tertiary computation.

### The rest of Buchdahl's series

The series runs to at least fourteen papers, and I to XIII are reprinted at the end of the Dover
monograph - see the note under it above, so all fourteen are in hand: I to XIII through the
reprint, and XIV, which postdates it, separately. The numbering is exact, from
footnote 2 of XII and footnote 1 of XIV:

| | citation | subject | |
|---|---|---|---|
| I | *J. Opt. Soc. Am.* **46**, 941 (1956) | tertiary spherical aberration - B7's origin | have |
| II | **48**, 563 (1958) | the tertiary intrinsic coefficients | have |
| III | **48**, 747 (1958) | computing the tertiary - Table I, Table II | have |
| IV | **48**, 757 (1958) | quaternary (ninth-order) spherical | have |
| V | **49**, 1113 (1959) | on the quality of predicted displacements | have |
| VI | **50**, 534 (1960) | coordinates lying partly in the image space - the W coordinates | have |
| VII | **50**, 540 (1960) | deformation and retardation of the wave front | have |
| VIII | **50**, 678 (1960) | spherical aberration of order eleven | have |
| IX | **51**, 608 (1961) | theory of reversible optical systems | have |
| X, XI, XIII | ? | ? | have, via the reprint; citations unpinned |
| XII | **55**, 641 (1965) | remarks relating to aberrations of any order | have |
| XIV | **59**, 1422 (1969) | simplified computational form of the iteration equations | have |

Neither XII nor XIV cites X, XI or XIII, so their citations need a JOSA index; nothing yet
suggests they bear on this work.

**[have] Buchdahl, H. A.**, "Optical Aberration Coefficients. IV. The Coefficient of Quaternary
Spherical Aberration," *J. Opt. Soc. Am.* **48**, 757-759 (1958).
Ninth order, and only its spherical term. Three pages. `QuaternarySpherical.cs` is its Table I.

**It is an appendix to III, not a new scheme.** Buchdahl says so — "the computing scheme in this
instance being treated as an appendix to that for the set of tertiary coefficients" — and the
consequence is that every one of the twenty-eight quantities the fourteen rows need is already an
entry of the tertiary Table I. He adds: "the computing scheme is quite brief, viz. only 14 entries
of the usual kind per surface."

**Sec. 3 reads as though a dual run is needed, and it is not.** It says the one quantity in
Eq. (3.1) not explicit in the tertiary scheme is `T1-dagger`, and that obtaining it "requires
T1q", through the identity M (21.7). He then performs that reduction himself: the three rows
feeding `r3` in his Table I *are* M (21.7) written out in p-side quantities.

**Two checks come with it, and both were used.** Eq. (2.12): at a plane refracting surface, where
`i = -v`, the intrinsic coefficient must reduce to `(35/128) N (1 - k^2)^4 y v^9` — a closed form
needing no lens, which he calls "a fairly reliable check" and uses himself. And his Table I
computes the whole thing for the triplet `Sigma1`, the same system paper III is checked against
here, printing the six intermediate `r` rows beside the answer and a system figure of −172968.

**Spherical surfaces, and that is the subject's limit rather than a choice.** The paper narrows
itself explicitly — "all entries relating to t_μpj, ī_μpj (μ = 2,…,10) except z2 are of course
irrelevant" — and no aspheric arrangement exists at this order anywhere. Buchdahl wrote Secs. 84
and 85 for the tertiary; nothing corresponds at the quaternary.

His closing remark is worth keeping with the numbers: a system meant to work at f/2 should aim at
individual surface contributions of at most order 1000 at unit focal length. `Sigma1`'s run to six
figures, "as is of course to be expected of so poorly corrected a system."

**[have] Buchdahl, H. A.**, "Optical Aberration Coefficients. VII. The Primary, Secondary, and
Tertiary Deformation and Retardation of the Wave Front," *J. Opt. Soc. Am.* **50**, 540 (1960).
**The bridge between this program's transverse coefficients and the WAVE-FRONT coefficients
nodal aberration theory is written in.** Its Sec. 6 is titled "The Relations Between
W-Coefficients and Deformation Coefficients" and gives them explicitly at all three orders:
Eq. (6.5) the five primary, Eq. (6.6) the nine secondary, Eq. (6.7) fourteen tertiary.

This page is why the note at the top of this file matters. Its OCR is noise, and the paper sat
here looking like a curiosity until it was rendered as a page image - see
`reading-scanned-pdfs.md` in the working notes. [nat-development.md](nat-development.md) records what it
established and what it did not.

Three things it supplies beyond the relations themselves:

- **Eq. (4.6), `eps' = dD/dy`, which he calls exact**: the transverse ray displacement is the
  gradient of the wave-front deformation. That is the mechanism, and it is what makes the
  correspondence checkable against coefficients this program already computes.
- **Redundancy.** The derivation yields two expressions for several coefficients, and he notes
  that "the 10 identities between the W coefficients of the first three orders so implied are
  exactly those given by the equations of VI (4.16-18)". Self-checking, as his tertiary
  identities are.
- **A published numerical answer.** Sec. 7(b): Table I gives the deformation and retardation
  coefficients of the first three orders for the triplet of III Sec. 3 - the same lens whose
  Table I this repository already reproduces - with the W coefficients themselves in VI Table II.

And one trap, Sec. 7(a): his `e` is not unity, so the coefficients carry powers of it -
`A: -1, B: 0, C: 1, S1: -1, S3,S4: 1, S5: 2, S6: 3`, barred coefficients taking an extra factor.
A coefficient right and its power of `e` wrong reads as a plausible number rather than an error.

**[have] Buchdahl, H. A.**, *J. Opt. Soc. Am.* **46**, 941 (1956). Paper I of the series.
Held through the Dover reprint's appendix, which carries I to XIII.

**[have] Buchdahl, H. A.**, "Optical Aberration Coefficients. II. The Tertiary Intrinsic
Coefficients," *J. Opt. Soc. Am.* **48**, 563-568 (1958).
Gives the tertiary intrinsic coefficients of spherical surfaces in closed form: ten
quantities z1..z10, then t_1p..t_10p as polynomials in q and the z, with the barred ten
following as tbar = q*t. "The ten unbarred intrinsic coefficients ... require a total of
only thirteen entries per surface."

Its introduction also states the field-versus-aperture limitation this program measured,
sixty-eight years earlier: the limitation of stopping at fifth order "appears to be the
relatively poor agreement between predicted and actual aberrations in the outer parts of
the field, rather than the inaccuracies of the predictions for large apertures", caused by
"the intractable behavior of the coefficients of oblique spherical aberration".

It also explains why B7 is the one seventh-order coefficient in common tooling: "the only
tertiary coefficient considered in detail so far has been that of spherical aberration",
which was the subject of paper I.

**[wanted] Cruickshank, F. D.**, *Australian J. Phys.* **11**, 41 (1958).
The analytical initial-design method Buchdahl pairs his coefficients with. Not needed for
the coefficients themselves.

**[have] Sands, P. J.**, "Aberration Coefficients and Surfaces of Best Focus," *J. Opt.
Soc. Am.* **63**(5), 582-588 (1973).
The focus-adjustment method Robb points to for exactly the defocus-blindness problem. **Read,
and the criterion is implemented** - `BestFocus.cs`, reported per wavelength. It closes that
hole, and it also answers a question this repository had been treating as open.

*What was taken from it and what was not.* The criterion, and the two facts that make it
checkable: the third-order identity below, and that best focus is not the disk of least
confusion. **Not his Table II.** The focal shift is derived here from Robb's own polynomial in
this program's convention - the mean square radius is a quadratic in the plane shift whose two
non-constant terms are pupil averages of that polynomial, so the minimum is written down rather
than searched for and no conversion between his normalisation and Rimmer's is needed. His
published third-order plane is then an external check on that derivation rather than its source,
and `BestFocusTests` holds it, along with an exactly-integrated pupil and real traced rays.

*On the defocus problem, which is what it was wanted for.* The image is focused by minimising
the radius of gyration of the spot diagram, which Sands notes is equivalent to maximising the
low-frequency response. His Eq. (8) gives the focal shift as `x* = -b/c` for two pupil integrals,
and Sec. II carries those integrals out **once and for all in terms of the aberration
coefficients**, so that his Eqs. (12) and (13) read

    x* = (-2/va') SUM_n SUM_a  x(n)a  p0^(2(n-a)) H^(2a)
    m* =    m   + SUM_n SUM_a  m(n)a  p0^(2(n-a)) H^(2a)

with `n = 1..4` running over orders three, five, seven and nine, and **Table II giving every
`x(n)a` and `m(n)a` as a combination of the sigma, mu, tau and eta coefficients** - which are the
five, twelve, twenty and thirty this program's own sets correspond to. Nothing is traced. Two
structural facts fall out of the same section: `x*` depends only on the **symmetric** aberrations
and the centroid shift only on the **comatic** ones, and the best-focus plane is not the disk of
least confusion - for third-order spherical it is at `-(2 sigma1/3 va')p0^2` against the disk's
`-(3 sigma1/4 va')p0^2`. Sec. IV then averages over the field, with a weight `w(H) = w0 + w1 H^2`,
to give the single plane of best focus over the whole field of view.

That is implementable here from what the program already computes, third through seventh, and it
is the missing half of PRMSA rather than an improvement to it. The conversion between Sands's
normalisation and Rimmer's is the work, not the formulae.

*What he validates it against.* Five systems, predicted against ray-traced, with the orders added
one at a time. On an f/2.9 triplet at 24 degrees the third-through-ninth prediction is excellent
and third-through-seventh visibly short of it; on a 45-degree Vega-type it is poor beyond about
40 degrees, where "the series for distortion converges very slowly, if at all". He also tests the
paraxial approximation for the image-space direction and finds it justified except near
45-degree ray angles or very large distortion, and finds vignetting does not invalidate the
method except at the extreme edge of the field at full aperture.

**And then footnote 8, which is the find.** Describing the program behind his ninth-order columns:

> "The program computes the values of the a and b components of six third-order, 12 fifth-order,
> 20 seventh-order, and 30 ninth-order coefficients."

**Thirty at the ninth order, published.** Twelve and twenty are this program's own secondary and
tertiary counts exactly, and six is the third-order count in the transverse monomial basis rather
than Seidel's five. That is the count [forbes-ninth-order.md](forbes-ninth-order.md) derived from
the invariant monomials and could only call an extrapolation; it is now a published number, and
the derivation is confirmed rather than merely consistent.

Two further things in the same place. His Sec. II says the program was written **"based on some
unpublished work of Buchdahl"** and computes the ninth-order coefficients of a symmetric system
**of spherical surfaces** - so a ninth-order scheme existed in 1973, unpublished, and it had the
same restriction paper IV has. And his **Table I prints the ninth-order aberration polynomials**:
the generic form of both components, the thirty coefficients grouped by monomial, with classical
names - spherical aberration, circular coma, oblique spherical aberration, cubic coma, then
quintic astigmatism, quintic coma, cubic astigmatism and elliptical coma, then linear astigmatism
and curvature of field, then distortion - and a footnote that two of the types have no
counterpart among the lower orders. The group counts read 1, 2, 3, 4, 5, 5, 4, 3, 2, 1.

**The table is in a 2005 scan and its formulae are OCR noise; the row-to-name alignment has been
read off a damaged text layer and has not been checked against a page image.** The counts and the
footnote are legible and are what is relied on above. He gives the third-, fifth- and
seventh-order polynomials by reference rather than reprinting them - Eqs. (2.6)-(2.11) of
Cruickshank and Hills (1960), which is held here.

**[have] Cruickshank, F. D. and Hills, G. A.**, "Use of Optical Aberration Coefficients
in Optical Design," *J. Opt. Soc. Am.* **50**, 379-387 (1960).
Robb states his derivation is based on this paper.

**[have] Buchdahl, H. A.**, "Optical Aberration Coefficients. V. On the Quality of
Predicted Displacements," *J. Opt. Soc. Am.* **49**, 1113-1121 (1959).
On how well a truncated coefficient series predicts real ray displacements - the
question this program's field-accuracy caveat is about.

**[have] Hopkins, G. W.**, "Proximate Ray Tracing and Optical Aberration Coefficients,"
*J. Opt. Soc. Am.* **66**, 405-410 (1976).
A different algorithm for the same coefficients. Useful as an independent check.

**[have] Sands, P. J.**, "Aberration Coefficients and Unusual Coordinates for Specifying
Rays," *Appl. Opt.* **9**(4), 828-836 (1970).
Robb points at its section VI for designs with large pupil aberrations. It turns out to
carry rather more than that, and three of the four things it settles were open questions
in this file.

**1. It names the cause of the accuracy limit this program measured.** Sands found a system
whose seventh-order series failed at a half-field of only 35 degrees while others held to
almost 45, and the difference was not field, aperture or figuring:

> "Close inspection revealed that for the system in question the pupil aberrations were
> large, whereas for systems in which the predictions were good over an extended range of
> field angles, the pupil aberrations were quite small."

[spot-prediction.md](spot-prediction.md) reports the same behaviour from the other end - "the order a design
needs is a property of that design, not a general rule", with one of five lenses not
described at seventh order at all. Sands says which property. That makes it a testable
prediction rather than an observation: compute the pupil aberration of those five lenses
and see whether it sorts them in the order their series accuracy does. **Nothing in this
repository has tried that**, and it is the cheapest open question here.

**2. It is a caveat on the distortion report, and on per-aberration attribution generally.**
When rays are specified by the paraxial entrance pupil (Buchdahl's SPC, which is what this
program uses) and the pupil aberrations are large, the ray `S = 0` does not pass through
the centre of the stop. Sands' conclusion is blunt: the coefficients "are not the
coefficients of distortion", and for coma "the image patch obtained under the same
circumstances need bear no resemblance to the familiar comatic image. Strictly speaking in
this case, the coefficients in question do not govern linear coma and their usual
interpretation is invalid."

This bears directly on `--distortion-coefficients`, whose entire claim is that it says
WHICH ORDER the distortion is, and on `ContributionAnalysis`, which attributes spot size
per named aberration. Neither is wrong, and on the designs measured here neither is in
danger. But the condition under which the naming stops meaning anything is now known, is
checkable, and is not currently checked or stated.

**3. The fix, and what it would cost.** Section VI introduces *aperture coordinates* -
specify a ray by where it crosses the physical stop rather than the paraxial entrance
pupil. Vignetting becomes exactly `|S| <= 1`, the usual interpretation of the coefficients
is restored, and the new quantities are nearly free: `A*a = 0` and `A*b = -Gb` at the stop
surface, Eqs. (33)-(34), "their computation is somewhat trivial".

The cost is architectural and it is not small. With SPC the coefficients can be computed
**surface by surface**, which is exactly how `BuchdahlTableI` is written. With aperture
coordinates they cannot:

> "before the exact nth-order surface contributions can be computed at any surface, the
> intermediate coefficients of all lower orders must be known at least up to the aperture
> stop ... the computation of the aberration coefficients must proceed order by order, in
> contrast to the case of SPC."

Sands adds that aperture coordinates are a special case of Buchdahl's GPC and are identical
with the W coordinates of paper VI.

**4. A lead on Table I.** Sands states that "explicit use of the identities OAC Eq. (21.6)
was made when constructing OACIII, Table 1", and that the second set of identities - those
from the invariance of `E*` - were used there "in particular in lines 20-24 and 81-100 in
Table 1". Both sets change form under non-linear coordinates, and Sec. VIII gives the
modified versions.

Worth noting beside the record above that Buchdahl's own printed numbers found errors in
this program's `t100..t108`. Sands independently identifies lines 81-100 as the block built
on those identities. That is a coincidence of ranges and nothing more until someone looks,
but it is the kind of lead that is cheap to follow and expensive to have missed.

**[wanted] Sands, P. J.**, Thesis, Australian National University (1967).
Reference 2 of the above. Two things in it that nothing else here covers: an expansion of
the aberration function about the **principal ray** rather than the axis - which is a
generalisation of Buchdahl's theory, and is conceptually the same move nodal aberration
theory makes with its optical axis ray - and, in Chapter 10, a method for determining the
shape of the actual entrance pupil as a function of field angle "correct to any order",
which Sands says is widely believed to require extensive ray tracing.

**[wanted] Buchdahl, H. A.**, *An Introduction to Hamiltonian Optics* (Cambridge University
Press, New York, 1970), Sec. 37.
Reference 3 of the above. Cited by Sands for the ideal-wave-surface coordinates of his
Sec. VII.

**[wanted] Woodruff, C. J.**, "A Comparison, Using Orthogonal Coefficients, of Two Forms
of Aberration Balancing," *Opt. Acta* **22**, 933-941 (1975).

**[have] Johnson, R. B.**, "Polynomial Ray Aberrations Computed in Various Lens Design
Programs," *Appl. Opt.* **12**, 2079-2082 (1973).
The cross-program notation map Robb points at. It confirms that sigma/mu/tau IS the
standard nomenclature, and its Table I gives the published name for each coefficient
combination:

| aberration | base term | in this program's names |
|---|---|---|
| 3rd spherical | sigma1 | B |
| 5th spherical | mu1 | B5 |
| 7th spherical | tau1 | B7 |
| 3rd linear coma | 3 sigma2 | 3F |
| 5th linear coma | mu2 + mu3 | F1 + F2 |
| 3rd linear astigmatism, tangential | 3 sigma3 + sigma4 | 3C + Pi |
| 3rd linear astigmatism, sagittal | sigma3 + sigma4 | C + Pi |
| 5th linear astigmatism, tangential | mu10 | 5 C5 + Pi5 |
| 5th linear astigmatism, sagittal | mu11 | C5 + Pi5 |
| 3rd distortion | sigma5 | E |
| 5th distortion | mu12 | E5 |
| 5th oblique spherical, tangential | mu4 + mu6 | M1 + M2 + M3 |
| 5th oblique spherical, sagittal | mu5 | M2 |
| 5th elliptical coma, tangential | mu7 + mu8 | N1 + N2 |
| 5th elliptical coma, oblique | mu9 | N3 |
| 3rd Petzval | sigma4 | Pi |

Two historical points it settles:

- Across all six programs surveyed in 1972, **tau1 is the only seventh-order term exposed**.
  That is the same situation this program inherited, and it is why B7 stands alone.
- Yet FLAIR 43, from the Institute of Optics at Rochester - Rimmer's own institution - had
  a subroutine SWORD which "computes all 37 coefficients comprising the third, fifth and
  seventh orders". So a program computing the full set existed in 1972. The capability was
  not exposed, not absent.

Johnson's purpose is also a caution: he found "significant variances in term definitions"
between programs and reports "several anomalous term computations". Comparing a named
aberration across tools without checking its definition is unsafe.

**[wanted] Rayces, J.**, *Optica Acta* **11**, 85 (1964).
Cited by Rosete-Aguilar and Rayces for the conversion between the reduced geometric
components and the wave aberration function.

**[wanted] Nijboer, B. R. A.**, *Physica* **10**, 679 (1943).
The wave aberration function used in the 1995 paper. Only needed if the wave-aberration
re-normalisation is ever implemented here.

## Nodal aberration theory

NAT is a different axis of generalisation from the rest of this file. Everything above extends
the aberration expansion in **order**, with rotational symmetry assumed. NAT extends it in
**symmetry**. The two compose, and NAT wants as input exactly the per-surface coefficients this
program already computes.

**This section read "None of this is implemented" until 19 September 2026, and it was untrue the
day it was written.** The sentence went in with commit `7a00d7f`, which is the commit that added
nodal aberration theory stages 1 to 4a. The preamble was drafted while NAT was still the proposal
in [nat-development.md](nat-development.md) and was not revised when the proposal became code, in
the same commit. It is the second time this file has described work as not done while the work sat
beside it - the Buchdahl monograph entry above records the first - and the failure mode is the
same both times: an entry written at the start of a piece of work and never re-read at the end of
it.

**What is implemented**, and [nodal-aberration-theory.md](nodal-aberration-theory.md) is the page
for it: the third order from the Seidel sums, the fifth by way of Buchdahl's W coordinates and the
deformation-to-retardation conversion, each surface's sigma vector, the coma node, the astigmatic
node pair and the medial vertex, nodes for every fifth-order term but `W511`, freeform Zernike
overlays through trefoil and above, and what the fifth order does to the third. It is driven by an
`.align` sidecar, exposed as `--nat` and over MCP, and checked against Thompson's published
telescope tables.

**What is not** is the seventh order: the tertiary rows of VI Table I are transcribed but unused.
That, and the acquisitions below, are the real gap.

### The foundation

**[have] Thompson, K. P.**, "Description of the third-order optical aberrations of
near-circular pupil optical systems without symmetry," *J. Opt. Soc. Am. A* **22**(7),
1389-1401 (2005). DOI 10.1364/JOSAA.22.001389 (OSA pattern - verify on retrieval).
The paper every one of the six below cites, as [2], [4] or [13], and the foundation of the
whole subject. `Core/Nat/Vec2.cs` implements its Appendix A.

What it settled here:

- **The vector algebra, term for term.** Appendix A prints the component forms - (A1) and (A9)
  for the product, (A6) for the conjugate, (A7) for the product with a conjugate, (A10) for the
  squared vector, and identities (A11) to (A13). `NatTests` checks all of them against the
  implementation rather than against a restatement of it. An earlier draft of
  [nat-development.md](nat-development.md) had the product written with `x` as the real axis, which
  contradicted the conjugate rule three lines above it; the appendix settles it.
- **The third-order node structure**, which had been assembled here from Schmid and reasoning:
  coma (4.7)-(4.9), astigmatism (4.15)-(4.22), medial field curvature (4.27)-(4.31). The last
  of those corrected a guess - the medial focal surface is displaced, not tilted, and its
  `B220M` is a DOT product where astigmatism's `B222^2` is a vector square.
- **`W220M = W220 + W222/2`**, his Eq. (4.11), which `Nat/WaveCoefficients.cs` had already
  implemented from the same reasoning and is now sourced.
- **The sigma convention**, though not the paraxial formula for it. Sec. 3 defines sigma
  geometrically, as the projection of the line joining the pupil centre to the surface's centre
  of curvature, with `H_Aj = H - sigma_j` as Eq. (3.1). That resolves the direction Gu's
  Eqs. (21) and (27) disagree about into something testable.
- One implementation instruction, from the remark under Eq. (4.31): the displacement vectors
  "for each aberration are identical", so sigma is computed once per surface and only the
  weights differ.

**[have] Thompson, K. P., Schmid, T., Cakmakci, O. and Rolland, J. P.**, "Real-ray-based
method for locating individual surface aberration field centers in imaging optical systems
without rotational symmetry," *J. Opt. Soc. Am. A* **26**(6), 1503-1517 (2009).
DOI 10.1364/JOSAA.26.001503 (OSA pattern - verify on retrieval).
The authority on the sigma vector, and it settled an open question here rather than merely
informing one.

- **Eq. (10)** gives the paraxial sigma as `-ibar* / ibar`: the angle of incidence of the
  OPTICAL AXIS RAY on the local surface, over the nominal chief-ray incidence. `RealSigma.cs`
  measures exactly that quantity from traced rays.
- **Table 5 shows the two routes agreeing** to four or five figures, so they are not different
  quantities - which established that this repository's disagreement between them was a fault
  and not a convention. The fault is diagnosed in [nat-development.md](nat-development.md): Gu's expression
  is derived for ONE perturbed surface and superposing it over several drops the term that
  makes a rigid translation come out zero.
- **Tables 1 to 5 are a published oracle** - a Ritchey-Chretien prescription, the perturbations
  applied to it, both ray traces, and the resulting sigma vectors. The same standard as
  Buchdahl's Table I: numbers printed beside the lens they were computed on.
- **Eq. (11) gives a SECOND sigma vector for an aspheric surface**, from the aspheric departure
  treated as a zero-power plate after Burch, distinct from the one for the spherical base. At
  the secondary of his telescope the two differ by more than a factor of two. This is Schmid
  2010's `sigma_SPH` and `sigma_ASPH`. **This entry also said it was not implemented, and that
  `--nat` was right only for spherical surfaces. Both were out of date.**
  `Nat/SigmaVector.cs` carries `SigmaAspheric`, and
  `ThompsonTelescopeTests.TheAsphericSigmaVectorsReproduceTable5` holds it against the aspheric
  column of his Table 5 - 0.0540234 at the secondary, to six figures - with the vector required
  to vanish on the unfigured surface. The two conditions under which each sigma fails to form
  are different, and conflating them was a real bug; `nodal-aberration-theory.md` says which is
  which.

**[have] Thompson, K. P.**, the multinodal fifth-order trilogy, *J. Opt. Soc. Am. A*:

- I, "spherical aberration", **26**(5), 1090 (2009)
- II, "the comatic aberrations", **27**(6), 1490 (2010)
- III, "the astigmatic aberrations", **28**(5), 821 (2011)

Fifth-order nodal aberration theory. Three things they settle:

- **Appendix B of paper I is the GENERAL pattern**, not a list of special cases:

      Wklm  = sum_j Wklm,j
      Aklm  = sum_j Wklm,j sigma_j        ->  aklm  = Aklm/Wklm,  Hklm = H - aklm
      Bklm  = sum_j Wklm,j (sigma_j . sigma_j)  ->  bklm  = Bklm/Wklm - aklm . aklm
      B2klm = sum_j Wklm,j sigma_j^2      ->  b2klm = B2klm/Wklm - a2klm

  which is exactly what `NatField` already does at third order, scalar-versus-vector
  distinction included. **The fifth order is the same machinery with more coefficients**, so
  the structural work is done and what remains is supplying them.
- **Appendix A of paper I names the fifth-order set**: `W060, W151, W240, W242, W331, W333,
  W420, W422, W511`. Nine wavefront coefficients against Rimmer's twelve transverse ones,
  because a transverse coefficient is a derivative of a wavefront one and the two do not
  correspond term for term. Johnson (1973) Table I, already recorded above, bridges them
  through the NAMED aberrations - "5th oblique spherical, tangential = M1+M2+M3" and the rest -
  which is a second route to the map alongside Buchdahl paper VII Sec. 6.
- **An erratum for Thompson (2005)**: its Appendix A Eq. (A5) is misprinted and should read
  `A B = |A||B| exp(i(alpha + beta))`. `Vec2` implements the corrected form - orientations ADD -
  and `NatTests.MultiplicationAddsOrientations` pins it. The printed error was noticed here
  when the OCR of the 2005 appendix disagreed with its own Eq. (A1); it is now confirmed as a
  misprint by the author rather than a reading difficulty. Thompson also corrects Fig. 10 of
  that paper, where the lower arrow should be labelled `+ib311`.

**[wanted] Shack, R. V. and Thompson, K. P.**, "Influence of alignment errors of a telescope
system on its aberration field," *Proc. SPIE* **251**, 146-153 (1980).
Where the idea starts. Cited by all six papers below; of historical rather than implementation
value, since the 2005 paper supersedes it as a source of equations.

**[wanted] Buchroeder, R. A.**, "Tilted component optical systems," Ph.D. dissertation
(University of Arizona, 1976), and **Thompson, K. P.**, "Aberration fields in tilted and
decentered optical systems," Ph.D. dissertation (University of Arizona, 1980).
The two theses the whole field rests on. Gu's appendix credits Buchroeder with the optical axis
ray tracing method it uses.

### The application layer - all in hand

These six are the PDFs read for the proposal. Between them they cover figure error, mount error,
freeform surfaces and tolerancing, and one of them happens to carry the derivation that makes
the first three stages implementable without the 2005 paper.

**[have] Gu, Z., Wang, Y. and Yan, C.**, "Optical system optimization method for as-built
performance based on nodal aberration theory," *Opt. Express* **28**(6), 7928-7942 (2020).
DOI 10.1364/OE.385089 (verify on retrieval - OSA moved to manuscript-number DOIs around
this date, so the volume.page pattern used above does not apply). Open access.
**The most directly usable paper in the set, and the one Stage 2 implements.** Its appendix
reproduces the paraxial sigma-vector derivation in full - Eqs. (17) to (34) - which is why
third-order NAT can be built here before the 2005 paper arrives.

Two things make it a good fit for this repository specifically. It needs no ray tracing beyond
the paraxial marginal and chief rays, which are already computed; and it is algebraic in those
rays, so it lands on the dual-number path and the optimiser gets analytic derivatives of
tolerance sensitivity for free.

It also supplies a complete acceptance test, which almost nothing else in this file does:
Table 1 is the starting Cooke triplet, Table 3 is the optimised one, and the text gives every
setting needed to reproduce the run. An implementation that can walk from one to the other has
tested the model, the derivative and the optimiser at once.

Reported outcome: about a tenth of a wave of nominal performance given up, nearly two tenths
recovered at the eightieth percentile of a 2000-sample Monte Carlo, standard deviation at
64 per cent of the traditionally optimised design's. Against Zemax's TOLR, comparable quality in
one to two minutes rather than eight hours.

**[have] Schmid, T., Rolland, J. P., Rakich, A. and Thompson, K. P.**, "Separation of the
effects of astigmatic figure error from misalignments using Nodal Aberration Theory (NAT),"
*Opt. Express* **18**(16), 17433-17447 (2010). DOI 10.1364/OE.18.017433.
The access point by which a non-symmetric surface enters NAT at all: a Zernike Z5/6 error at the
stop is added as a field-constant `B222^2`, and everything follows. Gives the binodal solution
`H = +/- i sqrt(B222^2 / W222)` and the diagnostic that matters - figure error keeps the node
midpoint at the field centre, secondary-mirror misalignment does not. Its Eqs. (12)-(16) are the
node algebra transcribed into [nat-development.md](nat-development.md).

**[have] Fuerschbach, K., Rolland, J. P. and Thompson, K. P.**, "Extending Nodal Aberration
Theory to include mount-induced aberrations with application to freeform surfaces,"
*Opt. Express* **20**(18), 20139-20155 (2012). DOI 10.1364/OE.20.020139.
Takes the non-symmetric surface off the stop, where the beam displacement `dh = (ybar/y) H`
makes the contribution field dependent. The result worth having: three-point mount trefoil
produces **field linear, field conjugate astigmatism** as well as the expected trefoil - the
first time a conjugate field dependence was tied to an observable. Also introduces the freeform
sigma vector for an overlay decentred from the optical axis ray.

Caution for anyone implementing from the PDF in hand: the text layer of its Eq. (10), which is
the constant relating `C333^3` to the measured `z10/11`, is damaged. Read it off the page image.

**[have] Fuerschbach, K., Rolland, J. P. and Thompson, K. P.**, "Theory of aberration fields for
general optical systems with freeform surfaces," *Opt. Express* **22**(22), 26585-26606 (2014).
DOI 10.1364/OE.22.026585.
The complete table, and the reference for Stage 4. Every Fringe Zernike pair through Z17/18
(tetrafoil), what field-constant vector it contributes at the stop, and which existing NAT term
it extends when the surface is away from the stop. Its conclusion is the useful one: **there are
no new aberration types**, only field dependences NAT already described but which were too small
to notice in tilted-and-decentred systems and dominate in freeform ones.

**[have] Fuerschbach, K., Rolland, J. P. and Thompson, K. P.**, "Nodal Aberration Theory Applied
to Freeform Surfaces," *Proc. SPIE* (2014).
The conference version of the above, with the aberration-generating Schmidt telescope built and
measured. Shorter and more readable; the 2014 Optics Express paper is the one to implement from.

**[have] Jiang, Y., Wang, L., Zeng, X., Liu, Y., Hu, J. and Li, W.**, "Aberration field
distribution characterization and tolerance analysis based on nodal aberration theory,"
*Opt. Express* **33**(23), 49313-49330 (2025). DOI 10.1364/OE.582222 (verify on retrieval).
Open access.
The current state of the art, and the source of the validation threshold used in the proposal:
full-field displays from the analytic model compared against real-ray tracing by a similarity
measure, with agreement above 0.8 taken as validation.

Extends to fifth order and to off-axis pupils, and is a caution as much as a source: it takes
its fifth-order coefficients from **Sasian's** set rather than Thompson's or Rimmer's, so the
notation problem noted above has three sides to it and not two. Cites Zhang for the induced
fifth-order components and for the exact freeform decentre-and-tilt formulas; neither has been
obtained here.

## Aspheric surfaces

**[have, read] Schulz, G.**, "Aspheric surfaces," in E. Wolf (ed.), *Progress in Optics* **XXV**
(Elsevier, 1988), pp. 349-415.
The review of the subject: representations, design methods, fabrication and testing,
applications, and the theoretical limits. Three things in it bear directly on this program.

**Sec. 3.3 is now implemented as `AspherePlacement`.** Reading the Delano `(H, h)` diagram, he
states that the ratio of chief-ray to marginal-ray height at a surface "quantitatively determines
the weight by which an aspheric deformation of the surface i influences the individual Seidel
aberrations" - small `|H/h|` reaches spherical aberration, large reaches distortion, the middle
range moves all of them by comparable amounts. This repository already formed that ratio, as a
local variable named `ratio` inside `SeidelCoefficients`, and used exactly the power law he
describes; what it had never done was ask the question forwards. `--asphere-placement` does, and
because the third-order aspheric contribution is LINEAR in the coefficient, the sensitivities are
exact derivatives rather than differences.

**Sec. 2.1 is the source for an identity this program relies on and did not cite.** His Eq. (2.5),
`a2 = 1/2R` and `a4 = (1+b)/8R^3`, crediting Hopkins (1950, p. 151) and Born and Wolf (1964,
p. 138), is the conic-to-polynomial equivalence that `BUCH7_ASPH.ZPL` computes in its vertex-form
block as `k1*c3/8`, that `AsphericR2TermTests` rests on, and that the fixtures
`F2_a4_equivalent` and `F7_conic_as_polynomial` exist to test. It was derived here rather than
read, and it is correct; it now has a source.

**Sec. 3.3 also confirms, independently, something this repository had established against
FIFTHORD.** The Petzval condition "cannot be influenced by asphericities". This program leaves
the Petzval column blank in its aspheric block for that reason, `verification.md` records the
agreement with FIFTHORD that it is absent, and `AspherePlacement` has no `S4` column and says
why. Three routes to the same statement.

*Two counting results from Sec. 6.2, which bound what figuring can do and are worth knowing
rather than computing.* The number of aspherics needed for `m`th-order aplanatism is `(1+m/2)^2`
for even `m` and `(1+m)(3+m)/4` for odd - so one surface for axial stigmatism, **two for ordinary
aplanatism**, four for the second order, six for the third. And from Schulz (1980): three
refracting surfaces, two of them aspheric, suffice to zero all five Seidel sums.

**[have, read] Wassermann, G. D. and Wolf, E.**, "On the theory of aplanatic aspheric systems,"
*Proc. Phys. Soc. B* **62**, 2-8 (1949).
The classical construction of two aspheric profiles giving EXACT aplanatism - axial stigmatism
together with exact satisfaction of the sine condition, at all orders rather than to third or
fifth. Two coupled first-order differential equations for the profiles, whose coefficients come
from two ray-traced congruences: forward from the axial object point to the first corrector, and
backward from the axial image point to the second. Solved numerically to any accuracy wanted.
The two aspherics must be optical neighbours; any number of surfaces may precede or follow them.
That it takes exactly two is Schulz's `m = 1` count above.

**It is not implemented and is not proposed, and the reason is a mismatch rather than a doubt.**
It is design synthesis where this program analyses and optimises; and its output is a TABULATED
PROFILE, where every surface here is a conic plus an even polynomial to `r^16`. Fitting the one
to the other would reintroduce precisely the error the method exists to eliminate, which is not a
detail but the point of it. Recorded because it is the standard reference for what two aspherics
can be made to do exactly, and because it is the source of the two-surface aplanat that the
counting result above explains.

## Diffraction, which this program does not compute

**[have] Lewkowicz, M., Nowak, J. and Zając, M.**, "Calculation of the aberration spot - improved
numerical algorithm," Institute of Physics, Technical University of Wrocław. Undated; it cites a
1996 paper as in print and benchmarks on a 100 MHz Pentium.
**A draft rather than a final paper** - its acknowledgments section is empty, its figures are cited
without numbers, and an untranslated Polish editorial note is left in the body. Cite it with that
in mind.

It is here for one paragraph, which names a limit of this program that nothing else on this page
does. Their justification for abandoning geometric methods:

> "the geometrical methods have only limited value, especially when the investigated optical
> element has small aberrations... does not include the influence of diffraction, which becomes to
> prevail in well-corrected optical systems."

**PRMS is a geometric spot and said nothing about that.** The word "diffraction" appeared in no
document in this repository before this paper was read. `spot-prediction.md` already records that
predictions fail on hard-corrected designs and attributes it entirely to truncation; this is a
second and independent reason, running the opposite way. Truncation bites where the aberrations
are large. Diffraction bites where they are small, and no number of orders helps: the image cannot
be smaller than the Airy disc. The PRMS section of the report now says so.

*What the paper itself does*, for the record, is the step after the coefficients: given the complex
amplitude on the exit pupil, evaluate the diffraction integral. Two ideas - a global least-squares
polynomial fit to phase data known only at scattered points, then a local split of the Taylor
expansion into a linear part and the rest, with cosine and sine of the rest expanded so that
everything reduces to integrals of `x^n cos(bx)` and `x^n sin(bx)` done analytically. They report
eight or nine exact digits where linear-exponent methods reach five.

**It is not a source to implement from and it is not proposed as one.** The algorithm is of its
time; FFT of the pupil function and the extended Nijboer-Zernike theory are the modern defaults.
Two things in it would nonetheless be worth having if a diffraction spot were ever built here.
Their **error measure** is better than the usual one: rather than trusting the decimal digits that
stop changing, they compare the integrand against its local approximation at random points in each
subdomain and sum the local bounds, which gives an error bound instead of a stability heuristic -
the same preference for a measured bound that the rest of this repository runs on. And their
**step one would not be needed here**: they fit a polynomial to scattered ray-traced wavefront
data because that is all they have, where a coefficient program knows the wavefront analytically.
That is only an advantage if the WAVE coefficients exist. Here they do to fifth order, as NAT's
deformation and retardation coefficients (`Nat/WaveFront.cs`), though not in the Rayces/Nijboer
form the re-normalisation would need - Rayces (1964) and Nijboer (1943) are both still `[wanted]`.

## Books that shaped the approach

These did not supply the equations - Buchdahl, Rimmer, Robb and Forbes above did that - but
they shaped what this program tries to be. They are listed because a reference list of
papers alone would misrepresent where the thinking came from.

**[have] Kidger, M. J.**, *Intermediate Optical Design* (SPIE Press, Bellingham, 2004).
SPIE Press Monograph PM134, ISBN 978-0-8194-5217-7. Published posthumously; his
*Fundamental Optical Design* (SPIE Press, 2001, PM92) precedes it.
Aberration theory as a designer actually uses it rather than as a subject to be surveyed.
The stance this program takes from it is that per-surface contributions are the useful
form: a total says a design is wrong, and a breakdown says where to go and look.

**[have] Dilworth, D. C.**, *Lens Design: Automatic and quasi-autonomous computational
methods and techniques* (IOP Publishing, Bristol, 2018; second edition 2020).
That a program should be an active participant in the design rather than a calculator the
designer drives - that it should be capable of saying what to change and not only what is
wrong. Both halves of that are here now: the optimiser is his PSD, and the
intrinsic-and-induced split exists because "which surface, and is it that surface's own
fault" is the question a designer needs answered before deciding what to do.

The book is the argument; the algorithm has its own papers, and they are under
[Optimisation](#optimisation) below.

**[have] Dilworth, D. C.**, "The Ascendency of Numerical Methods in Lens Design",
*J. Imaging* **4**(12), 137 (2018). DOI 10.3390/jimaging4120137. Open access.

**The strongest published argument against the premise of this program, and it is here
because of that.** Dilworth holds that aberration theory has been overtaken in design work:

> "The authors of recent textbooks on lens design invariably instruct the reader to first
> work up a third-order solution by hand before submitting it to computer optimization.
> Even that idea is also now obsolete, in my opinion."

> "I argue that the theoretical approach has collapsed under its own weight. One simply
> cannot, in spite of generations of mathematical genius, design lenses according to a set of
> algebraic statements."

He credits the success of SYNOPSYS to two things: the PSD III optimiser, and a binary search
over the SIGNS of element powers - a five-element lens is 32 cases rather than a mesh of
200,000 nodes, and each is optimised numerically.

**This entry used to add "and neither is aberration theory", and that was wrong.** His own
2012 paper, which is the paper describing that binary search, puts the third and fifth orders
inside it - see the entry under [Optimisation](#optimisation) and the passage quoted there.
The screening stage of DSEARCH is an optimisation run against all the third- and fifth-order
aberrations plus three real rays, used to triage the 2^N candidates before the expensive
stages see any of them. So aberration theory is in his pipeline, in the fast-objective role,
and the sentence removed here was reading his conclusions without checking his methods.

**The correction cuts the other way too, and the concession below was too generous.** This
entry said the coefficients are "not offered as a way to arrive at a starting point, which is
the ground he is arguing on and where he is right". He uses coefficients to arrive at starting
points - that is what the screen is for.

**None of which makes his position inconsistent, and the distinction is the whole of it.**
What he argues against is the DESIGNER working up a third-order solution BY HAND before
submitting it to the computer: "one cannot design lenses according to a set of algebraic
statements". That is compatible with a program using a cheap analytic objective inside an
automated search, which is what his screen is and what this one would be. The disagreement was
never about whether coefficients are useful to a machine. It is about whether they are useful
to a person, which is the claim the per-surface split below rests on.

So what remains of the disagreement is smaller than this entry used to claim. He and this
program agree that coefficients are worth optimising against when rays are expensive; they
differ on whether the resulting numbers are worth READING.

The other half stands unchanged. "Surface 5 contributes almost nothing of its own and nearly
all of what it carries was induced upstream" is not a statement a merit function makes, at
any speed, and it is what tells a designer where to go and look. Dilworth's own case is that
the computer should say what to change; the remaining disagreement is over whether a number
the designer can reason about is worth having on the way there.

*A note on how this entry came to be written, and how it came out.* It was first recorded
here, on a recollection, as saying the opposite - that SYNOPSYS uses aberration theory early
because it is fast against real ray tracing. Reading the 2018 paper showed that it argues
close to the reverse, so the entry was rewritten and the recollection was set aside with this
note: "The recollection may still hold for the BOOK, or for how DSEARCH forms its first merit
function; neither has been checked, and neither should be cited until it is."

**It has now been checked, and the recollection was right, on the second of the two
possibilities it named.** DSEARCH forms its first merit function from the third- and
fifth-order aberrations with three real rays, in Dilworth's own words in the 2012 paper. The
recollection was correct about the program and wrong only about which of his writings would
show it - his conclusions argue one way and his methods section does the other, and reading
only the first is what produced the error here twice, in opposite directions.

Worth keeping as a record of the method rather than of the fact. An unverified recollection
was written down as unverified, the two places it could be true were named, it was kept out of
the argument until one of them was read, and then it was confirmed rather than quietly
adopted. That is the whole of what this file is for.

**[have] Shafer, D.**, "I Plead the 5th", *Recent Trends in Optical Systems Design II*,
SPIE Vol. 1049 (1989), pp. 11-16.

Six pages by a working designer arguing for exactly the two things this program was built to
provide, written thirty-seven years before it. It is here because it is the only source found
so far that states the design case for the per-surface intrinsic-and-induced split as a
requirement rather than as a nicety.

The premise is that in a design already corrected to third order the limiting monochromatic
aberrations are fifth-order field curvature and sagittal oblique spherical aberration, and
that these cannot be controlled from totals:

> "This can only be done effectively, however, if the 5th-order aberration surface
> contributions are broken into two components: the intrinsic component and the induced
> component."

His example is a Bouwers, where the mirror shows induced spherochromatism for a reason that
is not the mirror's: the front lens's axial colour changes the beam diameter arriving at it.
A surface contribution alone cannot distinguish that from a fault of the mirror, and the two
call for opposite actions. The closing sentence asks for the printout this program produces:
"surface by surface 5th-order aberration contributions printout, ideally with separate
intrinsic and induced components."

Two further claims bear directly on what is being built here. On merit functions: "it is much
quicker to try out many different configurations and ideas if there are no rays in the merit
function and you are only correcting the 3rd and 5th-order aberrations" - the performance
limits of a design are "built in at a very early level", and "unless you can control all the
5th-order ... you can't make the optimum higher-order balance required". On the seventh order,
which is this program's reason for existing: "It is the 7th-order which then determines if a
particular design is on the right track."

And on aspherics he takes a position worth recording because it is not the usual one: they
are a temporary device for design rather than a feature of the product. Separated aspheric
singlets carrying only fourth-order deformation, corrected for the third order together with
oblique spherical and Petzval, can afterwards be "replaced with equivalent non-aspheric
doublets or triplets without losing the higher-order correction" - which makes an aspheric
variable useful even to a designer who has no intention of ordering an asphere.

Set against Dilworth above, the two disagree about the merit function and agree about the
diagnosis: both want the program to say what to change, and Shafer is explicit that the
coefficient breakdown is how it says it.

*The two aberrations he argues a design is decided by, in this program's notation*, since he
names them the way a designer does and the program labels them the way Rimmer did: fifth-order
field curvature is `Pi5`, taken with `C5`; sagittal oblique spherical is `M2`. Both can be
targeted directly in a merit function, written as those names. The full lookup from an
aberration's name to its coefficient is in [optimizer.md](optimizer.md) - it was added because
of this paper, which is what made it obvious that a reader arriving with a name in mind had
nowhere to turn it into a symbol.

*And the split he asks for is an operand, not only a printout.* The sentence quoted above - that
the fifth order can be controlled effectively only if the surface contributions are broken into
intrinsic and induced components - now has a form the optimiser understands: `M2.INT` drives what
a surface generates on its own and `M2.IND` bounds what is induced in it, the second answered by
moving the surfaces ahead of it rather than that one. Shafer asks for the split so that a designer
knows where to act; this makes it the thing that acts. The closing sentence of his paper asks for
the printout, and that is `srf = 1` in `macros/BUCH7_ASPH.ZPL` and the per-surface breakdown in
the report.

## Optimisation

**This section was missing, and its absence said something wrong about the program.** Every source
above is a source for the *coefficients* - what they are, how they are computed, how they turn
into a spot. But `abcalc` optimises, and the optimiser is not this project's invention either: PSD
is Dilworth's, the pattern search is Hooke and Jeeves's, the basin hopping is Wales and Doye's.
[optimizer.md](optimizer.md) describes what is implemented and why; this is where it came from.
Nothing here had a citation anywhere in the repository except the one line of Applied Optics in
[optimizer.md](optimizer.md) itself.

All nine are in hand. **One has been read** - Dilworth (2012), marked `[have, read]` below - and
reading it corrected a claim this file made about its author elsewhere, which is the argument for
reading the other eight. The rest are `[have]` and unread, and that is a real distinction this
section keeps: what the optimiser does is described in [optimizer.md](optimizer.md) from the code,
not from these papers, and every claim about whose idea it is stands where it stood before the
PDFs arrived.

Dilworth's book and his 2018 *J. Imaging* paper are above under *Books that shaped the approach*,
and they stay there: they are entered for what they argue about how a design program should work,
which is a different debt from the algorithm. The algorithm is here.

**[have] Dilworth, D. C.**, "Pseudo-second-derivative matrix and its application to automatic
lens design," *Appl. Opt.* **17**(21), 3372-3375 (1978). DOI 10.1364/AO.17.003372 (OSA pattern -
verify on retrieval).
**The source of the default optimiser.** The diagonal of the dropped second-derivative term,
estimated from two successive gradients and added to the normal equations in place of Marquardt's
blind multiple of the identity. Four pages.

**In hand and not yet read, and the distinction matters here more than anywhere else on this
page.** [optimizer.md](optimizer.md) says where the code is what this entry says here: the
per-variable secant curvature and its use as the damping diagonal are his, but the clipping, the
smoothing between iterations and the division of labour between `psd2` and `psd3` are **this
implementation's reading of the idea and have not been checked against the paper**. Acquiring it
does not change that by itself. Until it is read against the code, the program should go on saying
it implements PSD rather than that it implements Dilworth's PSD.

**[have] Dilworth, D. C.**, "Improved convergence with the pseudo-second-derivative (PSD)
optimization method," *Proc. SPIE* **399**, *Optical System Design, Analysis, and Production*
(1983). DOI 10.1117/12.935427.
The follow-up, on altering the stabilising factor, with the improvement demonstrated in SYNOPSYS.
**This is the one that would settle `psd2` against `psd3`**, which is exactly a question about
what the stabilising factor is allowed to do - whether a negative curvature estimate is clipped to
zero or kept with its sign. That distinction is the whole difference between the two methods here
and it was arrived at by reasoning rather than read.

**In hand and not yet read.** Until it is, nothing in [optimizer.md](optimizer.md) changes: the
`psd2`/`psd3` division still stands on reasoning, and the entry above still says so. Reading this
one is the cheapest correction available to the optimiser's provenance, and it may make the 1978
paper unnecessary or may not - that is one of the things reading it would settle.

**[have] Faggiano, A.**, "Automatic lens design with pseudo-second-derivative matrix: a
contribution," *Appl. Opt.* **19**(24), 4226 (1980). DOI 10.1364/AO.19.004226 (verify on
retrieval).
An independent account of the method two years after it was published, including the selection of
the damping factor. Worth having for the same reason a second textbook is: where the original is
terse, a second account is what tells you whether your reading of it is the usual one. This
repository has twice lost days to a reading that was merely plausible.

**[have] Robb, P. N.**, "Accelerating convergence in automatic lens design," *Appl. Opt.*
**18**(24), 4191-4194 (1979). DOI 10.1364/AO.18.004191.
**The same Robb whose spot integration this program already implements**, on the same problem
Dilworth's paper addresses and one year later: an acceleration applied to damped least squares
that took a typical case from 175 iterations to 16 on the same stationary point. Of direct
interest because the fault it attacks - a least-squares run crawling once the residuals stop being
small - is the one PSD attacks, by a different device.

**[have, read] Dilworth, D. C.**, "Novel global optimization algorithms: binary construction and
the saddle-point method," *Proc. SPIE* **8486**, *Current Developments in Lens Design and Optical
Engineering XIII* (2012). DOI 10.1117/12.929156.
The other half of the SYNOPSYS case, and the one his 2018 paper credits alongside PSD: a binary
search over the SIGNS of the element powers, so that a five-element lens is 32 cases rather than a
mesh of 200,000 nodes. This program answers the same question with basin hopping, which is a
stochastic walk over the same space and makes no use of the structure his construction exploits.

**It also contains the passage that corrects this file's account of him**, and it is worth
quoting because it is a description of his own pipeline rather than an argument:

> "An obvious enhancement is first to screen all of the candidates in a very simple evaluation
> step, in our case consisting of an optimization run that minimizes all of the 3rd and 5th-order
> aberrations along with just three real rays. This step executes very quickly, and a specified
> number of candidates are saved for the next phase, which involves a more demanding optimization
> with a larger set of real rays, with another optional simulated annealing stage afterwards."

So DSEARCH runs in four stages - enumerate the 2^N sign combinations from a stack of near-flat
plates, screen them on third- and fifth-order aberrations with three real rays, optimise the
survivors against many rays, then optionally anneal - and the cheap stage is made of exactly the
quantities this program computes. See the note in *Books that shaped the approach* above for what
that does to the argument recorded there.

**The staged pattern is the interesting thing here, more than the binary search.** A screen has to
be cheap and needs only to rank, not to be right; a coefficient set is well suited to that and a
ray trace is not. This program's screen could be richer than his - the seventh order rather than
the fifth, and the per-surface intrinsic-and-induced split, which could reject a candidate for the
REASON its aberration is large rather than only for the size of it. Whether to adopt the pattern
is a design question and not a documentation one; nothing here implements it, and `--hops` remains
a stochastic walk with a full merit evaluation at every step.

*The rest of the paper* is the Saddle-Point Build, after Bociort: a zero-power shell added beside
an existing element opens new dimensions at a local minimum, and the merit function is unchanged
at the moment of insertion if it is made only of ray intercepts. Its inverse is automatic element
deletion. Neither is implemented here and neither is proposed.

**[have] Hooke, R. and Jeeves, T. A.**, "'Direct search' solution of numerical and statistical
problems," *J. ACM* **8**(2), 212-229 (1961). DOI 10.1145/321062.321069.
The pattern search `--method hj` implements: an exploratory move over the variables one at a time,
followed by a pattern move along whatever direction that found. Cited here because the method is
named in the program's own output and a reader should be able to find out what it is.

**[have] Wales, D. J. and Doye, J. P. K.**, "Global optimization by basin-hopping and the lowest
energy structures of Lennard-Jones clusters containing up to 110 atoms," *J. Phys. Chem. A*
**101**(28), 5111-5116 (1997). DOI 10.1021/jp970984n.
Basin hopping, from the chemistry it was invented in. The transformation it describes - local
minimisation flattens the landscape into interpenetrating staircases, and the walk is taken over
those rather than over the raw surface - is what `--hops` does, Metropolis acceptance included.
The lens-design literature generally arrives at the same device by other routes and under other
names; this is where the one implemented here comes from.

### Boundary constraint handling, which this program decides and never cited

[optimizer.md](optimizer.md) states that bounds are enforced by **reflection - not by clamping,
and not by a sigmoid** - and argues it from first principles: reflection has derivative of
magnitude one everywhere, a clamp leaves the Jacobian describing a variable that is not moving,
and a sigmoid's derivative goes to zero exactly at the bound, so a variable driven against a limit
stops responding. That argument was made here without reference to any literature. There is one,
and it says the question is larger than this program assumed.

**[wanted] Boks, R., Kononova, A. V. and Wang, H.**, "Quantifying the impact of boundary
constraint handling methods on differential evolution," *GECCO '21 Companion*, 1199-1207 (2021).
[arXiv:2105.06757](https://arxiv.org/abs/2105.06757).
Twenty-eight differential-evolution variants against thirteen boundary handling methods on
standard benchmarks. The choice "substantially affects the empirical performance as well as the
number of generated infeasible solutions", and the paper ends with recommendations for which to
use when - the closest thing to a direct answer to the question this repository settled by
argument.

**[wanted] Kononova, A. V., Caraffini, F. and Bäck, T.**, "Differential evolution outside the
box," *Information Sciences* **581**, 587-604 (2021).
[arXiv:2004.10489](https://arxiv.org/abs/2004.10489).
How often the question even arises, which is the surprise: "significantly more solutions than what
is usually assumed by practitioners need to undergo some sort of 'correction' to conform with the
definition of the problem's search domain", with strong dependence on the algorithm's components
and parameter settings. Relevant here because the basin hopping stage throws large steps ON
PURPOSE, so it generates out-of-domain candidates by design rather than by accident.

**[wanted] Kononova, A. V., Vermetten, D., Caraffini, F., Mitran, M.-A. and Zaharie, D.**, "The
importance of being constrained: dealing with infeasible solutions in differential evolution and
beyond," *Evolutionary Computation* **32**(1), 3-48 (2024).
[arXiv:2203.03512](https://arxiv.org/abs/2203.03512).
The strongest claim of the three, and the one that bears on this repository whichever method it
uses: results "cannot be considered reproducible unless the algorithm fully specifies what should
be done with solutions generated outside the domain, even in the case of simple box constraints".
Different choices induce "notably different behaviours - in terms of performance, disruptiveness
and population diversity", and the importance of the choice "quickly grows with problem's
dimensionality". It deliberately does not rank the methods; it argues the choice must be stated.

**By that standard this program is already in order, and for the right reason** - the rule is
named, both alternatives are named, and the reasoning is given, which is what the 2024 paper asks
for and says is rarely done. **What is NOT established here is whether reflection is a good choice
as well as a stated one.** The argument in `optimizer.md` is about derivatives and is sound as far
as it goes; none of it is a measurement, and a lens with bounded curvatures and thicknesses is not
a BBOB benchmark. These three would say what the alternatives actually cost. The dimensionality
finding is a specific caution for the basin hopping stage, where twenty variables is ordinary.

*None of the three is about lens design, and none is about a derivative-exact local method.* They
are about differential evolution, which this program does not use. They are here because the
boundary question is the same question whatever generates the step, and because this repository
had made a decision on it with no idea that a literature existed.

**[have] Levenberg, K.**, "A method for the solution of certain non-linear problems in least
squares," *Quart. Appl. Math.* **2**, 164-168 (1944).
**[have] Marquardt, D. W.**, "An algorithm for least-squares estimation of nonlinear
parameters," *J. Soc. Indust. Appl. Math.* **11**(2), 431-441 (1963). DOI 10.1137/0111030
(verify on retrieval).
`--method lm`, the baseline the other two are measured against in
[optimizer.md](optimizer.md#the-optimizers). Listed for completeness rather than because anything
about it is in doubt.

## Implementation provenance

The published sources above are the authority for the method. What establishes that this
program realises it correctly is, in the order the checks are worth anything:

**1. Buchdahl's own printed numbers.** Paper III works Table I through for a specific
triplet. `BuchdahlPublishedTableTests` reproduces it entry by entry, and his published
totals for the tertiary. That is the oracle, and it is what found the errors in t100-t108.

**2. Closed-form analytic surfaces, which need no other program at all.** A parabolic mirror
images infinity onto its focus with no spherical aberration at any order, so every order
must cancel term for term. A single conic surface can be traced analytically and expanded as
`eps = a3 y^3 + a5 y^5 + a7 y^7 + ...`, which gives the third, fifth and seventh order
coefficients as numbers - no series, no table, no macro. See `ParabolicMirrorTests` and
`ExactConicSurfaceTests`. **This is what establishes the ASPHERIC third and fifth order** - they were available first and
they need no other program at all. (An earlier version of this line added "and it had to,
because no other implementation available here computes them", which is wrong: FIFTHORD handles
aspheres. It was not the ESTABLISHING check, but it was never unable to be a corroborating one.
See **On FIFTHORD** below.)

**3. Inverse real ray tracing.** `CoefficientInversion` recovers coefficients from the
landings of real traced rays by scaling and an odd-polynomial fit. It is this repository's
own code and owes nothing to any other program. Eight test files use it as their reference,
including every aspheric one.

**4. An independent implementation of the third and fifth order**, written in C++ by this
repository's author directly from Buchdahl's book - a different lineage from the same
source, described below. It does not implement aspherics.

**4a. The same C++ codebase supplies the tilted-surface transform.** `RayTrace/LocalFrame.cs`
is a port of its `coordinate_break_C`, and the conventions it fixes - degrees, the minus sign
on the x tilt where the y tilt has none, the composition order, decentre-before-rotate - are
copied rather than re-derived, because they are the error-prone part and a tested version
existed. The INVERSE is derived here; `LocalFrameTests` guards the derivation by requiring the
round trip to be the identity, and the whole transform by requiring a uniformly decentred lens
to image exactly like the nominal one moved over. No third-party program is involved at any
point.

**5. Forbes' series trace** for the seventh order, from a separate published paper with no
shared code. See [forbes.md](forbes.md).

### On FIFTHORD

The FIFTHORD macro by M. MacFarlane (1998) - with the mirror index-sign correction of
T. A. Mitchell (2003) and the Lagrange-invariant correction of J. Sasian (2019) - realises
the same published method, and its results agree with this program's: 586 coefficient values
across seven designs, to a worst-case residual of 1.1e-12.

That agreement is recorded because it is worth recording, and because a reader with that
macro can repeat it without trusting anything here. **It is not what determined that
these results are correct.** The checks above did, and they were available first; the
agreement was an outcome, not a guide. FIFTHORD does not compute the twenty tertiary
coefficients, so it could not have settled those.

**Correction, 2026-09-12. This section previously said FIFTHORD computes neither the aspheric
coefficients nor the tertiary ones. The aspheric half of that is wrong: FIFTHORD does handle
aspheres.** The claim was never checked, and the page contradicted itself two paragraphs later
by listing "the aspheric r^8 handling" among the things this program departs from it on - a
departure that presupposes something to depart from. What the sentence was reaching for is
narrower and still true: the macro was not what established the aspheric third and fifth order
here, because the closed-form analytic surfaces above were available first and are stronger.
But it could have corroborated them, and saying it could not was a misstatement.

The macro is not redistributed here and no part of it is included in this repository. Where
this program departs from it - the aspheric r^8 handling, the F/number sign convention, the
analytic pupil integration in place of a table of constants - [verification.md](verification.md) says so.

### Independent cross-check available

A second implementation of the third- and fifth-order coefficients exists, written in C++
by this repository's author directly from Buchdahl's book. It is his own work and is
available to check against.

It is worth using because it is an independent derivation. It takes Buchdahl's own route
rather than Rimmer's: a p/q paraxial ray pair as the basis instead of marginal and chief,
reduced angles, k = n/n' and k1 = 1 - k, everything scaled to unit focal length, and a
numbered auxiliary table transcribed from the text. Agreement between it and the macro
would corroborate 17 of the 18 coefficients from two lineages rather than one, which the
present 586-value validation cannot do - it is a single lineage, so a shared misconception
would not surface.

It does not compute seventh order. The function carries a `compute_tertiary` flag that
sets a status field and gates nothing, so `B7` would remain corroborated by the macro
alone.

**It reproduces the values of the triplet example Buchdahl published.** Confirmed
independently here: extracted standalone and run on `TRIPLET_BUCHALD_EFL50.lhlt`, it
agrees with paper III Table I to 5.3e-6 on the stop parameter, 4.3e-5 on the primary
coefficient ap, and 2.6e-5 on the secondary s1p - residuals at the level seven-digit
inputs support. The prescription identifies itself: the first curvature scaled by the
focal length, 0.09648784 x 50 = 4.824392, is exactly Buchdahl's tabulated t3 = 4.82439.
The extraction and the run are kept with the author's working notes, outside this
repository.

An earlier note here suggested it might carry the misprint that paper III corrects. That
was wrong: the misprint is in the equation for t5 of (81.3), which is a TERTIARY formula.
This code computes primary and secondary only, so the misprint cannot reach it. The
correction matters when (81.3) is implemented, not before.

Three points in that code to settle against the book if the cross-check is done:

- It computes `N * (1/c) * i1_p`, guarded to zero when the curvature is near zero. As
  c -> 0 that quantity diverges rather than vanishing, and a plano surface does contribute
  aberration. This program never divides by curvature at all, so the two differ here.
- `k / k1^2` is guarded to zero at a same-index interface, and the product it feeds also
  diverges rather than vanishing.
- In the block computing its terms 103 to 108, one column of the recurrence runs
  61, 59, 60, 62, 63, 64 where the neighbouring columns march monotonically. That may be
  a genuine irregularity in the book's formulae or a transcription slip; it is cheap to
  check and expensive to miss.

### Status of the tertiary work

Working notes are kept outside this repository,
because they transcribe material from copyrighted papers. Summary of where it stands:

The chain from paraxial data to the twenty tertiary coefficients is now sourced end to end
except for one link. Paper II gives the intrinsic coefficients in closed form; the
monograph's Eqs. (81.3) give the totals; Sec. 80 gives the aspheric correction to the
intrinsic quantities.

That missing link has since been found. Robb's Eq. (2), p.1038, writes the ray
displacement explicitly in Buchdahl's coefficients - sigma_1..sigma_5 primary,
mu_1..mu_12 secondary, tau_1..tau_20 tertiary. Five plus twelve plus twenty is thirty-seven,
his stated total, and the three distortion terms sigma_5, mu_12, tau_20 fall at positions
5, 17 and 37 - exactly the S(J) he reports as identically zero. The earlier partition was
inferred; it is now confirmed.

Two things follow. First, the notational bridge asserted in `Prms.cs` is correct: every
radial power, field power and theta function in its Ey/Ez lists matches Eq. (2) term for
term, so the implementation is faithful and is simply fed tau_1 alone. Second, the PRMS
side is now fully specified - the eighteen additional seventh-order terms and their theta
functions are written down in the working notes, and need only two new theta functions,
cos4 and sin4.

What remains is the VALUES of tau_2..tau_20, which is the Buchdahl (81.3) work. The two
halves of the problem are now cleanly separated. *(Historical: this status section records the
work as it stood. All twenty tau have since been computed, spherical and figured.)*

One finding changes the scope. Eqs. (81.3) are written in Buchdahl's own quantities -
A, B, C and S1..S6 with their barred and p/q variants - whereas this program computes
fifth order in Rimmer's notation. Adding seventh order is therefore not "nineteen more
coefficients on top of what we have"; it needs a Buchdahl-notation pipeline for primary
and secondary as well, or a notation map that has not yet been located.

The three numeric tables of paper II have been transcribed and verified against Buchdahl's
own column identity - every column sums to zero except the last, which sums to unity. That
is 160 integers checked by an independent test rather than by re-reading. Paper III Sec. 6
provides six further identities between the tertiary coefficients themselves, so an
implementation would be self-testing throughout.

### Phase 0 resolved: the tau mapping is published

Paper III p.753 closes the last open question. Its Table II gives all twenty of Robb's
tertiary coefficients in terms of Buchdahl's, with numerical values for his own triplet -
`tau1 = T1`, `tau2 = Tbar1 + T2/2`, up to `tau20 = Tbar10`, where `T1 = T_1pk'/(N_k' v_pk')`
and analogously for the rest, that division converting augmented coefficients to actual
ones. Twenty published expected values on a lens whose prescription is also published, so
any implementation has a complete acceptance test.

The same page gives the tertiary displacement equations, and they are Robb's Eq. (2)
seventh-order lines verbatim - Robb took his seventh order straight from Buchdahl.

It also publishes the triplet's constitution. A reconstruction of it
reproduces every curvature, separation and index ratio to the printed digits, and the
oracle's stop parameter 0.113227601 matches the published p = 0.113227 directly rather than
by inference.

The largest consequence is that the 692-term Eqs. (81.3) are not needed. Table I's later
entries produce the tertiary coefficients directly - t121..t130 are exactly paper II's
z1..z10, and t131 onward are t1p, tbar1p, t2p and the rest. Table I is the condensed scheme
Sec. 84 describes, 192 entries per surface as a linear list of formulae, and it was the
point of the paper. That replaces the largest and riskiest phase of the work with a
transcription about a quarter the size. The trade is that Table I is spherical surfaces
only, so seventh order would initially be valid for all-spherical designs and the report
would have to say so. *(Since superseded: the aspheric arrangement of Sec. 85 has been
reconstructed, and figured designs are carried too.)*

Worth keeping in view, from p.753: in this triplet the large tertiary elliptical coma "is
due not so much to that of the seventh order, but due rather to the ninth and higher
orders". Seventh order will improve the full-field prediction. It will not make it exact.
