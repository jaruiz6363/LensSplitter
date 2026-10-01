! FORBES - aberration coefficients by G. W. Forbes' Lagrangian series trace.
!
! Written by Javier Ruiz with Claude Code, September 2026.
!
! G. W. Forbes, "Order doubling in the computation of aberration coefficients,"
! J. Opt. Soc. Am. 73(6), 782 (1983), Sec. 3(a).
!
! STATUS: ALL FOUR STAGES, both conjugates. The truncated series ring, the
! trace built on it, conics and even aspheres - which is what BUCH7 cannot do and
! the reason this exists - and the third-, fifth- and seventh-order coefficients.
! MIRRORS ARE DECLINED (September 2026): the trace does not carry a reflection; see
! the note at the surface-type check.
!
! The ring checks ITSELF on algebraic identities every run, before the trace, and
! declines to trace if any of them fails.
!
! WHY THIS METHOD, GIVEN BUCH7 ALREADY EXISTS
!
!   BUCH7 refuses a conic or an aspheric term, and the refusal is honest: Buchdahl
!   gives the aspheric scheme but never published an ARRANGED TABLE for it the way
!   Table I arranges the spherical case, so there is no printed answer to check a
!   transcription against.
!
!   Forbes removes the difficulty rather than solving it. He writes the ith surface as
!
!       x = f_i(y . y)
!
!   with f_i a power series. A sphere, a conic and an even asphere differ ONLY in the
!   coefficients of f_i and are traced by identical code. There is no D and L split,
!   no hat and check pass, no carrying ratio - the entire class of fault the aspheric
!   Buchdahl arrangement is prone to cannot be expressed in this formulation.
!
!   So this is not a faster BUCH7. It is the route that reaches the refracting figured
!   systems BUCH7 declines, and on the systems both reach they must agree, which is the
!   check.
!
! THE ONE IDEA THE WHOLE THING RESTS ON
!
!   For a rotationally symmetric system every quantity carried through a ray trace is
!   either a scalar function of the three rotational invariants
!
!       p = y0 . y0 ,    k = y0 . b0 ,    u = b0 . b0
!
!   or a vector S y0 + T b0 with S and T such scalars, where y0 is the ray's height at
!   the input plane and b0 its direction. So the entire trace reduces to ARITHMETIC ON
!   ONE DATA TYPE: a power series in p, k and u, truncated at some total degree.
!
!   Each invariant is quadratic in the ray coordinates, so a term of degree m here
!   multiplies y0 or b0 to give order 2m + 1. Degree 1 is the primary aberrations,
!   degree 2 the secondary, degree 3 the tertiary - the seventh order. Twenty
!   monomials at degree three, which is why a series is twenty numbers.
!
!   Forbes' headline result, order doubling, is not used. It gets the characteristic
!   function to order 2M from a trace carried to order M, which is an economy for
!   orders far beyond the seventh; here the concatenation machinery it needs would be
!   the larger part of the work and the larger part of the risk.
!
! HOW A SERIES IS HELD
!
!   Twenty consecutive slots of VEC1. A "series" in what follows is the INDEX OF ITS
!   FIRST SLOT, and the subroutines take those indices in the globals sa, sb, sc.
!   The monomial order is fixed and is the one the C# in this repository uses, so the
!   two can be compared coefficient by coefficient:
!
!       m :  0  1  2  3   4  5  6  7  8  9  10 11 12 13 14 15 16 17 18 19
!       p :  0  1  0  0   2  1  1  0  0  0   3  2  2  1  1  1  0  0  0  0
!       k :  0  0  1  0   0  1  0  2  1  0   0  1  0  2  1  0  3  2  1  0
!       u :  0  0  0  1   0  0  1  0  1  2   0  0  1  0  1  2  0  1  2  3
!
!   Built once into VEC4 by the subroutine mktab, with an index table for the reverse
!   lookup (a, b, c) -> m that the multiply needs.
!
! SUBROUTINES AND THE ONE TRAP IN THEM
!
!   ZPL has GOSUB, SUB and RETURN, and up to a hundred subroutines - which is what
!   makes this feasible at all, since a series multiply is called dozens of times per
!   surface and inlining it would be unreadable.
!
!   BUT ALL ZPL VARIABLES ARE GLOBAL. A subroutine has no locals, so its working
!   variables are visible to, and clobberable by, everything else. Two rules keep
!   that safe here and both are checked in the audit that accompanies this file:
!
!     1. Every subroutine's private variables carry a prefix of its own - q for smul,
!        n for sinv, r for ssqrt, and so on - and no other subroutine uses them.
!
!     2. A SUBROUTINE THAT CALLS ANOTHER SUBROUTINE COPIES ITS OWN PARAMETERS FIRST.
!        sa, sb and sc are the argument registers; a nested call overwrites them, so
!        sinv begins by taking nsrc = sa and ndst = sb and never reads sa again.
!
!   The second rule is the one that would produce a plausible wrong answer rather
!   than an error, which is why it is stated before the code rather than after it.
!
! STAGING - the four stages are from docs/forbes.md, and all four are now written
!
!   1. THE RING. Multiply, inverse, square root, and the monomial tables. Checked on
!      ALGEBRAIC identities that need no optics and no reference implementation:
!      difference of squares, s * (1/s) = 1, sqrt(s)^2 = s, the binomial coefficients
!      of (1 - u)^(1/2) term by term, and the truncation actually truncating.
!      *** DONE, and re-checked on every run before the trace starts. ***
!
!   2. THE TRACE, spherical surfaces only. Transfer and refraction, carrying S, T, V
!      and W from the input base plane to the paraxial focus.
!      *** DONE. Checked against ForbesTrace in src/AberrationCalculator.Core, all
!      four series and all twenty coefficients of each, to 7E-15 relative on the
!      Cooke triplet. Two of those numbers can also be checked without leaving the
!      printout: T(0,0,0) is the focal length and -1/V(0,0,0) is the same thing by a
!      different route. ***
!
!   3. ASPHERES. Extra coefficients in f_i and nothing else changes.
!      *** DONE. Checked against the C# on a Cooke triplet carrying a conic on one
!      surface and even-asphere figuring at r^2, r^4, r^6 and r^8 on two others: all
!      four series again to 8E-16. The macro now carries STANDARD and EVENASPH
!      surfaces and declines any other type, because PARM on a toroid or a grating is
!      not an aspheric coefficient and reading it as one would be a confident wrong
!      answer rather than an error.
!
!      Only PARM 1 to 4 take part - they are r^2 to r^8. An r^10 term or above enters
!      the figure past the truncation and CANNOT reach the seventh order; it is not
!      being ignored, it genuinely does not appear. The macro says so when it meets
!      one rather than silently dropping it.
!
!      PARM 1 IS CARRIED HERE AND IS NOT CARRIED BY FIFTHORD. This macro puts it
!      straight into the sag series as the coefficient of p, doing no folding at all,
!      while BUCH7_ASPH folds it into the vertex curvature and re-measures the rest
!      from that sphere. The two treatments are opposite and they agree: on a conic
!      with r^4, r^6, r^8 and PARM 1 = 1E-4 they match on all eighteen totals and all
!      twenty tau to every digit either prints, and OpticStudio's own focal length
!      agrees with both. FIFTHORD does not - it reads paraxial data that includes the
!      power the term adds and then uses the base curvature for the contributions, so
!      its answer is neither lens. The closing block below refuses that comparison
!      when it finds such a surface instead of inviting it. ***
!
!   4. THE NAMED COEFFICIENTS - the five of the third order, the twelve of the fifth
!      and the twenty of the seventh. These are NOT read off S, T, V and W. The
!      transverse aberration is built as a series in the ray SCALE, its degree-3,
!      degree-5 and degree-7 parts taken exactly, and the coefficients recovered from
!      seventy-five ray shapes by a linear inversion.
!      *** DONE, both conjugates (see below). Checked against the C# on a Cooke triplet:
!      third order to 4E-14, fifth to 6E-14, seventh to 2E-13. ***
!
!      The solve uses the NORMAL equations where the C# uses a Householder QR. That
!      squares the condition number, which is a real objection and was tested rather
!      than waved away: on this model - polynomial evaluations at well separated
!      shapes - the two agree to the figures above.
!
!      BOTH CONJUGATES. The trace is right at either; it is the RAY BASIS that
!      differs, because a ray from a finite object point is not collimated - its
!      direction depends on the pupil point as well as the field, and its sagittal
!      direction no longer vanishes. Checked on the Cooke triplet with the object
!      250 mm away against BUCH7 and the C#: all thirty-seven to the seven figures
!      those print.
!
! WHERE THIS AND BUCH7 SHOULD AGREE, AND WHERE THEY SHOULD NOT
!
!   On a system of SPHERES all three orders must match BUCH7, and that has been
!   checked on a Cooke triplet. Two wholly different schemes - Buchdahl's arranged
!   tables and Forbes' series trace - sharing no arithmetic.
!
!   On a FIGURED system the comparison splits, and the split is exact rather than a
!   matter of degree:
!
!     tau1, which is B7, IS RIGHT in the Buchdahl route even for an asphere. It is
!     not reached through the aspheric table at all - it falls out of the fifth-order
!     working, which is why BUCH7 arrives at it twice over and gets the same number
!     both ways. Compare it with confidence.
!
!     tau2 TO tau20 WERE NOT RIGHT there, and that is no longer so. Buchdahl gives
!     the aspheric scheme in the monograph but never published an ARRANGED TABLE for
!     it, and for a long time this project could not resolve his instructions into
!     one - which is the difficulty this route was written to get round, Forbes
!     having no aspheric table to arrange because a sphere, a conic and an asphere
!     are the same code with different coefficients in f.
!
!     BUCH7_ASPH.ZPL, in this same folder, is that arrangement: members one to five
!     of the barred q accumulation from the identities of M Sec. 22, the sixth from
!     the dual run of XII Sec. 6, and the figuring's D half carried in the hat pass.
!     Run on the same file it reproduces all twenty of the coefficients printed
!     below to every digit either macro prints, on a conic singlet, on a conic with
!     r^4, r^6 and r^8 together, and on a triplet with two figured surfaces where
!     one induces on the other.
!
!     So the two routes now agree on a figured lens as well as a spherical one, and
!     they still share no arithmetic - Buchdahl's arranged tables against an
!     order-doubling series trace. THAT is what makes the agreement worth having,
!     and it is the strongest check either macro has.
!
!     BUCH7.ZPL itself still declines a figured lens. It is the SPHERICAL routine,
!     checked against Buchdahl's own printed numbers, and it is left undisturbed.
!
!   The third and fifth orders are not affected either way.
!
!   BUCH7.ZPL itself declines a figured system outright, so the comparison there is
!   with the Buchdahl route in src/AberrationCalculator.Core, not with the macro.
!
! ---------------------------------------------------------------------------------

eps = 1E-10

! Read before anything changes it, and restored at label 900 - including on the
! paths that leave early, which is why it is set here and not beside the trace.
paraxmode = PMOD()

! Set to 1 to print the stage 4 intermediates for the first ray shape - ym, bm, the
! invariants, S and T on them, and where the ray lands. That block is what found the
! one transcription fault stage 4 shipped with, and it is left in because the next
! fault of that kind will be found the same way.
dbg4 = 0

! Degree three is the tertiary. Twenty monomials: (d+1)(d+2)(d+3)/6.
ndeg = 3
nterm = 20

SETVECSIZE 4000

! ---------------------------------------------------------------------------------
! The heap. Every base below is the FIRST slot of a twenty-slot block.
!
! Blocks 1 to 6 are the caller's to use. The blocks from 201 on are private to the
! subroutine named, and nothing outside it may touch them - that is what allows sinv
! and ssqrt to call smul without either losing its own working values.
! ---------------------------------------------------------------------------------

! --- the trace's own state and working blocks, VEC1 ---
bser = 1
bter = 21
bver = 41
bwer = 61

bp0 = 81
bk0 = 101
bu0 = 121

bph = 141
buh = 161
bal = 181
bia = 201
bfh = 221

bsn = 241
btn = 261
bpt = 281
bst = 301

bpa = 321
bfp = 341
bsl = 361
bgg = 381
bhh = 401
bih = 421
bci = 441
bla = 461
bfc = 481

bx1 = 501
bx2 = 521
bx3 = 541
bx4 = 561

! --- private to sdot ---
bd1 = 581
bd2 = 601
bd3 = 621
bd4 = 641

! --- private to scomp ---
bc1 = 661
bc2 = 681
bc3 = 701

! --- private to sinv and ssqrt; nothing else may touch these ---
bn1 = 1001
bn2 = 1021
bn3 = 1041
bn4 = 1061

br1 = 1101
br2 = 1121
br3 = 1141
br4 = 1161

! --- the stage 1 self-tests' operands, kept clear of everything above ---
ba = 1201
bb = 1221
bc = 1241
bd = 1261
be = 1281
bf = 1301

! --- univariate series in p alone, VEC3. Five coefficients each, degrees 0 to 4.
! The surface figure needs one degree beyond the trace, and it involves only p, so a
! full twenty-term invariant series would be the wrong shape as well as wasteful. ---
nfig = 5
fu1 = 1
fu2 = 11
fu3 = 21
fu4 = 31
fu5 = 41
fu6 = 51
fud = 61

ui1 = 101
ui2 = 111
ui3 = 121
ui4 = 131

uq1 = 201
uq2 = 211
uq3 = 221
uq4 = 231

! --- scale series, VEC3 from 1001. Eight coefficients each, orders 0 to 7. ---
nzc = 8
zsy = 1001
zsz = 1011
zby = 1021
zbz = 1031
zpp = 1041
zkk = 1051
zuu = 1061
ztn = 1071
zsn = 1081
zss = 1091
ztt = 1101
zom = 1111
zos = 1121
zt1 = 1131
zt2 = 1141
zt3 = 1151
zt4 = 1161
zp0 = 1201
zk0 = 1241
zu0 = 1281
zq1 = 1401
zq2 = 1411
zq3 = 1421
zq4 = 1431
zv1 = 1501
zv2 = 1511
zv3 = 1521
zv4 = 1531

GOSUB mktab

! ---------------------------------------------------------------------------------
! The self-tests. These need no optics and no reference numbers: they are identities
! that any correct implementation of a commutative ring with square roots satisfies,
! and they fail loudly if the monomial tables, the truncation or the recursions are
! wrong. Each prints the worst coefficient discrepancy it found.
! ---------------------------------------------------------------------------------

tol = 1E-12
nfail = 0

PRINT "==================================================================="
PRINT "FORBES - stage 1, the truncated series ring in p, k and u"
PRINT "Forbes, J. Opt. Soc. Am. 73, 782 (1983), Sec. 3(a)"
PRINT "==================================================================="
PRINT " "
FORMAT 5.0 INT
PRINT "Degree kept        ", ndeg
PRINT "Monomials          ", nterm
PRINT " "
PRINT "These are algebraic identities, not comparisons with stored numbers."
PRINT " "

! ---- table round trip -------------------------------------------------------------
! Every monomial must map to an index and back to itself. A wrong index table would
! otherwise show up only as a wrong answer much later.

worst = 0
FOR xm = 0, nterm - 1, 1
   xa = VEC4(101 + xm)
   xb = VEC4(121 + xm)
   xc = VEC4(141 + xm)
   xk = VEC4(1 + xa * 16 + xb * 4 + xc) - 1
   IF (xk != xm) THEN worst = 1
   IF (VEC4(161 + xm) != (xa + xb + xc)) THEN worst = 1
NEXT

! scopy is a ring primitive stage 2 leans on, so it is exercised here rather than
! left untested until something depends on it.
GOSUB mkopa
sa = ba
sb = bf
GOSUB scopy
sa = bf
sb = ba
GOSUB sdiff
IF (gmax > tol) THEN worst = 1

GOSUB report1

! ---- 1. difference of squares -----------------------------------------------------
! (A + B)(A - B) must equal A*A - B*B. This exercises the multiply and the truncation
! in a way that a single product cannot: the cross terms have to cancel exactly.

GOSUB mkopa
GOSUB mkopb

sa = ba
sb = bb
sc = bd
GOSUB sadd
sa = ba
sb = bb
sc = be
GOSUB ssub
sa = bd
sb = be
sc = bc
GOSUB smul

sa = ba
sb = ba
sc = bd
GOSUB smul
sa = bb
sb = bb
sc = be
GOSUB smul
sa = bd
sb = be
sc = bf
GOSUB ssub

sa = bc
sb = bf
GOSUB sdiff
worst = gmax
GOSUB report2

! ---- 2. the inverse ---------------------------------------------------------------
! A * (1/A) must be one. A's constant term is one, which is the only case the trace
! ever meets and the only case sinv accepts.

sa = ba
sb = bc
GOSUB sinv
sa = ba
sb = bc
sc = bd
GOSUB smul

sa = bf
GOSUB szero
VEC1(bf) = 1
sa = bd
sb = bf
GOSUB sdiff
worst = gmax
GOSUB report3

! ---- 3. the square root -----------------------------------------------------------
! sqrt(A) squared must be A again.

sa = ba
sb = bc
GOSUB ssqrt
sa = bc
sb = bc
sc = bd
GOSUB smul

sa = bd
sb = ba
GOSUB sdiff
worst = gmax
GOSUB report4

! ---- 4. the binomial series of (1 - u)^(1/2) --------------------------------------
! This is not an arbitrary series: it is the direction cosine a = (1 - u)^(1/2), which
! is exactly what the refraction takes the square root of. Its coefficients are known
! in closed form - 1, -1/2, -1/8, -1/16 - so this pins the recursion against
! arithmetic rather than against another implementation.

sa = bc
GOSUB szero
VEC1(bc) = 1
VEC1(bc + 3) = -1

sa = bc
sb = bd
GOSUB ssqrt

worst = ABSO(VEC1(bd) - 1)
xv = ABSO(VEC1(bd + 3) + 0.5)
IF (xv > worst) THEN worst = xv
xv = ABSO(VEC1(bd + 9) + 0.125)
IF (xv > worst) THEN worst = xv
xv = ABSO(VEC1(bd + 19) + 0.0625)
IF (xv > worst) THEN worst = xv
GOSUB report5

FORMAT 15.6 EXP
PRINT " "
PRINT "   (1-u)^(1/2) coefficients, which must be 1, -1/2, -1/8, -1/16:"
PRINT "   ", VEC1(bd), VEC1(bd + 3), VEC1(bd + 9), VEC1(bd + 19)
PRINT " "

! ---- 5. the truncation actually truncates -----------------------------------------
! p^2 times p^2 is p^4, which is past degree three and must vanish entirely. Without
! this a multiply that silently kept high terms would pass every identity above,
! because those identities hold at any truncation.

sa = bc
GOSUB szero
VEC1(bc + 4) = 1
sa = bc
sb = bc
sc = bd
GOSUB smul

worst = 0
FOR xm = 0, nterm - 1, 1
   xv = ABSO(VEC1(bd + xm))
   IF (xv > worst) THEN worst = xv
NEXT
GOSUB report6

! ---- verdict ----------------------------------------------------------------------

PRINT " "
FORMAT 5.0 INT
IF (nfail == 0)
   PRINT "All six checks pass. The ring is sound at degree ", ndeg
   PRINT " "
   PRINT "The ring is what everything below rests on, so it is re-checked on every run"
   PRINT "rather than taken on trust from the last one. The trace follows."
ELSE
   PRINT "FAILURES: ", nfail
   PRINT " "
   PRINT "The ring is wrong and nothing built on it would mean anything. The most"
   PRINT "likely cause is the monomial index table in mktab, which every other"
   PRINT "operation reads."
   PRINT " "
   PRINT "The trace below is not run, because it would only propagate the fault."
   GOTO 900
ENDIF

! =================================================================================
! STAGE 2 - THE TRACE.
!
! Forbes Sec. 3(a). A general ray leaves an input base plane at height y0 with
! direction b0, and at every surface the state is held as
!
!     y_i = S y0 + T b0 ,     b_i = V y0 + W b0                              (3.1)
!
! with S, T, V and W series in p, k and u. The whole trace is the four of them being
! carried forward, and nothing else is ever needed - which is the economy the
! invariants buy.
!
! The input base plane is the first surface's vertex and the output base plane is the
! paraxial focus, so that what comes out is directly comparable with the aberration
! coefficients computed elsewhere in this repository.
! =================================================================================

PARAXIAL ON
wl = PWAV()
ns = NSUR()
nsm = ns - 1

! Which conjugate. THIC(0) is enormous for an object at infinity, and the ray
! basis of stage 4 differs between the two even though the trace does not.
!
! THE BOUNDARY GOES ON THE INFINITE SIDE. OpticStudio shows INFINITY in the lens
! data editor but ZPL's THIC(0) hands back the sentinel EXACTLY 1E10, so a test
! written as "> 1E10" is false at infinity and takes the finite branch. Here that is
! not cosmetic: with objd = 1E10 the ray mkrhs builds keeps its full aperture and
! loses ALL of its field - 0.0 per cent of the intended direction, and 4.19 mm out
! in height at the base plane. Every coefficient carrying no field would come out
! right and every coefficient carrying field would be wrong, which is the same
! signature as the reversed zscal this file has already been bitten by once.
!
! This was found from RAYINV, which carried the same line but only ever used it to
! decide what to PRINT. BUCH7 has always had the polarity below.
infconj = 1
IF (ABSO(THIC(0)) < 1E10) THEN infconj = 0

IF (nsm < 1)
   PRINT "FORBES needs at least one optical surface."
   GOTO 900
ENDIF
IF (nsm > 60)
   PRINT "FORBES is arranged for up to 60 surfaces and this system has more."
   GOTO 900
ENDIF

! ---------------------------------------------------------------------------------
! Which surfaces this can carry.
!
! A conic and an even asphere are handled by exactly the same code as a sphere: they
! are only more coefficients in f. That is Forbes' whole point, and it is why this
! macro reaches systems BUCH7 declines.
!
! What is refused is a surface type whose PARAMETERS MEAN SOMETHING ELSE. PARM on a
! toroid or a grating is not an aspheric coefficient, and reading it as one would
! produce a confident wrong answer rather than an error - so the type is read and
! checked rather than assumed from the parameters being non-zero.
!
! Only PARM 1 to 4 are used. They multiply p, p^2, p^3 and p^4, which is r^2 to r^8.
! An r^10 term or above enters the figure at degree five or more, past the
! truncation, and so contributes NOTHING at seventh order - it is not being ignored,
! it genuinely does not appear.
! ---------------------------------------------------------------------------------

badtyp = 0
FOR gi = 1, nsm, 1
   gdum = SPRO(gi, 0)
   gtyp$ = $BUFFER()
   gok = 0
   IF (SCOM(gtyp$, "STANDARD") == 0) THEN gok = 1
   IF (SCOM(gtyp$, "EVENASPH") == 0) THEN gok = 1
   IF (gok == 0)
      badtyp = 1
      FORMAT 5.0 INT
      PRINT "Surface ", gi, " is of type ", gtyp$
   ENDIF
   ! An even asphere may carry figuring past r^8. It cannot reach the seventh order,
   ! but a reader is owed the fact that it was dropped rather than left out.
   FOR gj = 5, 8, 1
      IF (ABSO(PARM(gj, gi)) > eps)
         FORMAT 5.0 INT
         PRINT "NOTE: surface ", gi, " parameter ", gj, " is beyond r^8 and cannot"
         PRINT "reach the seventh order, so it takes no part in what follows."
      ENDIF
   NEXT
NEXT

! No mirrors. The refraction below takes INDX on each side of a surface, and INDX does
! not carry a reflection's sign, so a mirror would be traced as a refraction into the
! same medium: the ray would not bend and every coefficient would come out near zero,
! with nothing to say so. Forbes carries a reflection by letting the index change sign,
! but the root that picks the reflected cosine has to be chosen explicitly, and it is
! not written here - the C# series trace declines a mirror for the same reason.
mirr = 0
FOR gi = 1, nsm, 1
   IF (ISMS(gi)) THEN mirr = 1
NEXT
IF (mirr == 1)
   PRINT "This system reflects. FORBES does not trace a mirror: it would treat it as a"
   PRINT "refraction into the same medium and return near-zero coefficients without"
   PRINT "complaint, so it declines instead. BUCH7_ASPH and RAYINV handle mirrors, and"
   PRINT "BUCH7 does on a spherical one."
   GOTO 900
ENDIF

IF (badtyp == 1)
   PRINT " "
   PRINT "FORBES carries STANDARD and EVENASPH surfaces. On any other type the"
   PRINT "parameter columns mean something other than aspheric coefficients, and"
   PRINT "reading them as if they did would give a confident wrong answer rather"
   PRINT "than an error - so it declines instead."
   GOTO 900
ENDIF

! The paraxial focus, where the output base plane goes. Same construction as the one
! ROBB.ZPL uses: the marginal ray's -y/u at the last surface.
RAYTRACE 0, 0, 0, 1, wl
IF (RAYE() != 0)
   PRINT "The paraxial marginal ray will not trace. Is the aperture set?"
   GOTO 900
ENDIF
gy = RAYY(nsm)
gu = RAYM(nsm) / RAYN(nsm)
IF (ABSO(gu) < eps)
   PRINT "The system is afocal, so there is no paraxial focus to catch the ray on."
   GOTO 900
ENDIF
gpfd = -gy / gu

! ---------------------------------------------------------------------------------
! The figures. Figure 0 is the input base plane, 1 to nsm the optical surfaces, and
! nsm+1 the output base plane. Each is five numbers: the vertex position on the axis
! and the coefficients of p, p^2, p^3, p^4 in the sag.
!
! The figure is carried ONE DEGREE BEYOND the trace on purpose. The refraction needs
! df/dp itself accurate to the trace's degree, and the p^3 term of df/dp comes from
! the p^4 term of f. Everywhere else df/dp is multiplied by something that vanishes
! with the ray, so the shortfall would stay hidden and cost exactly the top order.
! ---------------------------------------------------------------------------------

FOR gi = 0, nsm + 1, 1
   FOR gj = 0, nfig - 1, 1
      VEC2(1 + gi * 8 + gj) = 0
   NEXT
NEXT

gz = 0
anyr2 = 0
FOR gi = 1, nsm, 1
   ccur = CURV(gi)
   ccon = CONI(gi)
   cas1 = PARM(1, gi)
   IF (ABSO(cas1) > 0) THEN anyr2 = 1
   cas2 = PARM(2, gi)
   cas3 = PARM(3, gi)
   cas4 = PARM(4, gi)
   cbase = 1 + gi * 8
   cvz = gz
   GOSUB mkfig
   gzl = gz
   gz = gz + THIC(gi)
NEXT
VEC2(1 + (nsm + 1) * 8) = gzl + gpfd

! ---------------------------------------------------------------------------------
! The invariants themselves, and the initial state: y = y0, b = b0, so S = 1, T = 0,
! V = 0, W = 1.
! ---------------------------------------------------------------------------------

sa = bp0
GOSUB szero
VEC1(bp0 + 1) = 1
sa = bk0
GOSUB szero
VEC1(bk0 + 2) = 1
sa = bu0
GOSUB szero
VEC1(bu0 + 3) = 1

sa = bser
GOSUB szero
VEC1(bser) = 1
sa = bter
GOSUB szero
sa = bver
GOSUB szero
sa = bwer
GOSUB szero
VEC1(bwer) = 1

! ---------------------------------------------------------------------------------
! The loop over steps. Step gi is the move from figure gi into figure gi+1.
! ---------------------------------------------------------------------------------

FOR gi = 0, nsm, 1

   ! pHere = y.y and uHere = b.b at the figure we are leaving.
   da1 = bser
   db1 = bter
   da2 = bser
   db2 = bter
   dres = bph
   GOSUB sdot
   da1 = bver
   db1 = bwer
   da2 = bver
   db2 = bwer
   dres = buh
   GOSUB sdot

   ! alpha, the axial direction cosine, is sqrt(1 - u). This is the square root the
   ! ring's (1-u)^(1/2) self-test exists for.
   sa = bx1
   GOSUB szero
   VEC1(bx1) = 1
   sa = bx1
   sb = buh
   sc = bal
   GOSUB ssub
   sa = bal
   sb = bx1
   GOSUB ssqrt
   sa = bx1
   sb = bal
   sfac = 1
   GOSUB sscal
   sa = bal
   sb = bia
   GOSUB sinv

   ! f at the figure we are leaving, as a series.
   cser = bph
   cfig = 1 + gi * 8
   cres = bfh
   GOSUB scomp

   ! ---- transfer, Forbes (3.2)-(3.3) ------------------------------------------
   ! It is IMPLICIT: how far the ray travels along the axis depends on where it
   ! lands, which is what it is being solved for. Forbes' resolution is to solve it
   ! BY ORDER - with S and T known to degree m the argument of f is known to degree
   ! m+1, and (3.3) then gives S and T to degree m+1 - so iterating past the
   ! truncation reaches it exactly. One extra pass is run so that the last one is
   ! provably a fixed point rather than merely the one we stopped at.

   sa = bser
   sb = bsn
   GOSUB scopy
   sa = bter
   sb = btn
   GOSUB scopy

   FOR gpass = 0, ndeg + 1, 1
      da1 = bsn
      db1 = btn
      da2 = bsn
      db2 = btn
      dres = bpt
      GOSUB sdot

      cser = bpt
      cfig = 1 + (gi + 1) * 8
      cres = bx2
      GOSUB scomp

      sa = bx2
      sb = bfh
      sc = bx3
      GOSUB ssub
      sa = bx3
      sb = bia
      sc = bst
      GOSUB smul

      sa = bver
      sb = bst
      sc = bx3
      GOSUB smul
      sa = bser
      sb = bx3
      sc = bsn
      GOSUB sadd

      sa = bwer
      sb = bst
      sc = bx3
      GOSUB smul
      sa = bter
      sb = bx3
      sc = btn
      GOSUB sadd
   NEXT

   sa = bsn
   sb = bser
   GOSUB scopy
   sa = btn
   sb = bter
   GOSUB scopy

   ! The last step lands on the output base plane, which catches the ray rather
   ! than refracting it.
   IF (gi < nsm)
      GOSUB refract
   ENDIF
NEXT

GOSUB shows


! =================================================================================
! STAGE 4 - the named coefficients.
!
! The ray basis comes from the paraxial rays rather than from GETSYSTEMDATA, which
! would fill a vector and overwrite the surface figures. Three numbers are needed and
! all three fall out of rays already being traced:
!
!   epr   the entrance pupil radius. At infinite conjugate the marginal ray runs
!         parallel to the axis before the lens, so its height at surface one IS the
!         pupil radius.
!   hmax  tan of the maximum field. The chief ray's slope in object space is exactly
!         that, whatever the field type is stated as - which is why it is taken from
!         the ray rather than from MAXF and a units assumption.
!   epp   the entrance pupil position, where the chief ray crosses the axis:
!         -ybar / ubar from surface one.
! =================================================================================

RAYTRACE 0, 0, 0, 1, wl
IF (RAYE() != 0)
   PRINT "The paraxial marginal ray will not trace. Is the aperture set?"
   GOTO 900
ENDIF
mgy = RAYY(1)
mgu = RAYM(0) / RAYN(0)

RAYTRACE 0, 1, 0, 0, wl
IF (RAYE() != 0)
   PRINT "The paraxial chief ray will not trace. Is a field defined?"
   GOTO 900
ENDIF
ubar0 = RAYM(0) / RAYN(0)
IF (ABSO(ubar0) < eps)
   PRINT " "
   PRINT "The chief ray carries no field, so there is nothing to separate the field"
   PRINT "dependence of the coefficients with. Is the field zero?"
   GOTO 900
ENDIF
epp = -RAYY(1) / ubar0

! The entrance pupil RADIUS is the marginal ray's height AT THE PUPIL, which is its
! height at surface one carried forward over the pupil distance. At infinite
! conjugate the marginal ray runs parallel to the axis and this reduces to its
! height at surface one; at a finite conjugate it does not, and taking the height at
! surface one there would shrink the aperture by a per cent or two in silence.
epr = mgy + epp * mgu
IF (ABSO(epr) < eps)
   PRINT "The entrance pupil has no radius; is the aperture set?"
   GOTO 900
ENDIF

objd = 0
objh = 0
hmax = 0
IF (infconj == 1)
   ! The chief ray's slope in object space IS the tangent of the field, whatever
   ! the field type is stated as - which is why it is taken from the ray rather
   ! than from MAXF and an assumption about units.
   hmax = ubar0
ELSE
   ! The object is at a finite distance, so the field is an object HEIGHT and the
   ! chief ray's height at surface zero is exactly that.
   objd = THIC(0)
   objh = RAYY(0)
   IF (ABSO(objh) < eps)
      PRINT "The object has no height, so the field cannot be separated."
      GOTO 900
   ENDIF
ENDIF

! ---------------------------------------------------------------------------------
! The seventy-five shapes. Three pupil radii by seven azimuths by three fields, then
! nine pure-aperture shapes with no field and three pure-field shapes with no
! aperture. The last two groups matter: without them the solve has nothing that
! isolates the coefficients which carry aperture alone or field alone.
! ---------------------------------------------------------------------------------

VEC2(3901) = 0.35
VEC2(3902) = 0.7
VEC2(3903) = 1.0

pival = 2 * ACOS(0)
VEC2(3911) = 0
VEC2(3912) = pival / 6
VEC2(3913) = pival / 4
VEC2(3914) = pival / 3
VEC2(3915) = pival / 2
VEC2(3916) = 2 * pival / 3
VEC2(3917) = 5 * pival / 6

nsh = 0
FOR jr = 1, 3, 1
   FOR jt = 1, 7, 1
      FOR jh = 1, 3, 1
         VEC2(3201 + nsh * 3) = VEC2(3900 + jr)
         VEC2(3202 + nsh * 3) = VEC2(3910 + jt)
         VEC2(3203 + nsh * 3) = VEC2(3900 + jh)
         nsh = nsh + 1
      NEXT
   NEXT
NEXT
FOR jr = 1, 3, 1
   FOR jt = 1, 3, 1
      VEC2(3201 + nsh * 3) = VEC2(3900 + jr)
      VEC2(3202 + nsh * 3) = VEC2(3911 + (jt - 1) * 2)
      VEC2(3203 + nsh * 3) = 0
      nsh = nsh + 1
   NEXT
NEXT
FOR jh = 1, 3, 1
   VEC2(3201 + nsh * 3) = 0
   VEC2(3202 + nsh * 3) = 0
   VEC2(3203 + nsh * 3) = VEC2(3900 + jh)
   nsh = nsh + 1
NEXT

! ---------------------------------------------------------------------------------
! Three normal-equation systems, accumulated in ONE pass over the shapes. The scale
! series for a shape costs far more than the three model rows do, so it is built once
! and all three orders read their own coefficient off it.
! ---------------------------------------------------------------------------------

mrowy = 3101
mrowz = 3121

FOR jr = 0, 24, 1
   VEC2(2001 + jr) = 0
NEXT
FOR jr = 0, 4, 1
   VEC2(2101 + jr) = 0
NEXT
FOR jr = 0, 143, 1
   VEC2(2201 + jr) = 0
NEXT
FOR jr = 0, 11, 1
   VEC2(2401 + jr) = 0
NEXT
FOR jr = 0, 399, 1
   VEC2(2501 + jr) = 0
NEXT
FOR jr = 0, 19, 1
   VEC2(3001 + jr) = 0
NEXT

PRINT " "
PRINT "Building the model over 75 ray shapes. This is the slow part - the scale"
PRINT "series for each shape costs a few hundred series multiplies."

FOR jsh = 0, nsh - 1, 1
   srho = VEC2(3201 + jsh * 3)
   sthe = VEC2(3202 + jsh * 3)
   shh = VEC2(3203 + jsh * 3)

   GOSUB mkrhs

   ! TEMPORARY DIAGNOSTIC. Stage 4 gives the right answer for every coefficient
   ! that carries no field and the wrong one for every coefficient that does, so
   ! the fault is somewhere between the shape and the right-hand side rather than
   ! in the model or the solve, both of which are checked separately. This prints
   ! the intermediates for the first shape so the divergence can be located
   ! against the same quantities computed outside OpticStudio.
   IF ((jsh == 0) & (dbg4 == 1))
      PRINT " "
      PRINT "DIAGNOSTIC - shape 0, rho 0.35, theta 0, h 0.35"
      FORMAT 12.6
      PRINT "   srho ", srho, "  sthe ", sthe, "  shh ", shh
      PRINT "   epr  ", epr, "  epp  ", epp, "  hmax ", hmax
      PRINT " "
      PRINT " coef              ym              bm               p               k"
      FOR jd = 0, nzc - 1, 1
         FORMAT 4.0
         PRINT jd,
         FORMAT 16.6 EXP
         PRINT VEC3(zsy + jd), VEC3(zby + jd), VEC3(zpp + jd), VEC3(zkk + jd)
      NEXT
      PRINT " "
      PRINT " coef               u         sSeries         tSeries            outM"
      FOR jd = 0, nzc - 1, 1
         FORMAT 4.0
         PRINT jd,
         FORMAT 16.6 EXP
         PRINT VEC3(zuu + jd), VEC3(zss + jd), VEC3(ztt + jd), VEC3(zom + jd)
      NEXT
      PRINT " "
   ENDIF

   mord = 3
   nunk = 5
   gata = 2001
   gatb = 2101
   GOSUB mkmod
   ry = VEC3(zom + 3)
   rz = VEC3(zos + 3)
   GOSUB accum

   mord = 5
   nunk = 12
   gata = 2201
   gatb = 2401
   GOSUB mkmod
   ry = VEC3(zom + 5)
   rz = VEC3(zos + 5)
   GOSUB accum

   mord = 7
   nunk = 20
   gata = 2501
   gatb = 3001
   GOSUB mkmod
   ry = VEC3(zom + 7)
   rz = VEC3(zos + 7)
   GOSUB accum
NEXT

nunk = 5
gata = 2001
gatb = 2101
GOSUB gsolve

nunk = 12
gata = 2201
gatb = 2401
GOSUB gsolve

nunk = 20
gata = 2501
gatb = 3001
GOSUB gsolve

GOSUB showco

LABEL 900
IF (ABSO(paraxmode) < eps) THEN PARAXIAL OFF

END

! =================================================================================
! SUBROUTINES
! =================================================================================

! ---------------------------------------------------------------------------------
! mktab - the monomial tables, built once.
!
!   VEC4(1 + a*16 + b*4 + c)  the monomial number of p^a k^b u^c, plus one, or zero
!                             if that monomial is past the truncation
!   VEC4(101 + m)             a of monomial m
!   VEC4(121 + m)             b
!   VEC4(141 + m)             c
!   VEC4(161 + m)             total degree a + b + c
!
! The order is Forbes-neutral - any consistent order works - but it is deliberately
! the order the C# in src/AberrationCalculator.Core/Forbes uses, so that the two can
! be compared coefficient by coefficient rather than as sets.
! ---------------------------------------------------------------------------------
SUB mktab
   FOR tt = 1, 64, 1
      VEC4(tt) = 0
   NEXT
   tm = 0
   FOR tdeg = 0, ndeg, 1
      FOR ta = tdeg, 0, -1
         FOR tb = tdeg - ta, 0, -1
            tc = tdeg - ta - tb
            VEC4(1 + ta * 16 + tb * 4 + tc) = tm + 1
            VEC4(101 + tm) = ta
            VEC4(121 + tm) = tb
            VEC4(141 + tm) = tc
            VEC4(161 + tm) = tdeg
            tm = tm + 1
         NEXT
      NEXT
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! szero - the series at sa becomes zero.
! ---------------------------------------------------------------------------------
SUB szero
   FOR zi = 0, nterm - 1, 1
      VEC1(sa + zi) = 0
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! scopy - sb := sa.
! ---------------------------------------------------------------------------------
SUB scopy
   FOR ci = 0, nterm - 1, 1
      VEC1(sb + ci) = VEC1(sa + ci)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! sadd - sc := sa + sb. Aliasing is harmless here.
! ---------------------------------------------------------------------------------
SUB sadd
   FOR di = 0, nterm - 1, 1
      VEC1(sc + di) = VEC1(sa + di) + VEC1(sb + di)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! ssub - sc := sa - sb. Aliasing is harmless here.
! ---------------------------------------------------------------------------------
SUB ssub
   FOR ei = 0, nterm - 1, 1
      VEC1(sc + ei) = VEC1(sa + ei) - VEC1(sb + ei)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! sscal - sb := sa * sfac, a plain number times a series.
! ---------------------------------------------------------------------------------
SUB sscal
   FOR fi = 0, nterm - 1, 1
      VEC1(sb + fi) = VEC1(sa + fi) * sfac
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! smul - sc := sa * sb, truncated at degree ndeg.
!
! sc MUST NOT be sa or sb: the result is accumulated into sc as the pass proceeds, so
! aliasing would read values already overwritten. Callers use a scratch block.
!
! The truncation is what makes the whole scheme finite. A term of degree i is skipped
! against every term of degree greater than ndeg - i, which is the "room" below.
! ---------------------------------------------------------------------------------
SUB smul
   FOR qi = 0, nterm - 1, 1
      VEC1(sc + qi) = 0
   NEXT
   FOR qi = 0, nterm - 1, 1
      qx = VEC1(sa + qi)
      IF (qx != 0)
         qroom = ndeg - VEC4(161 + qi)
         FOR qj = 0, nterm - 1, 1
            qy = VEC1(sb + qj)
            IF (qy != 0)
               IF (VEC4(161 + qj) <= qroom)
                  qa = VEC4(101 + qi) + VEC4(101 + qj)
                  qb = VEC4(121 + qi) + VEC4(121 + qj)
                  qc = VEC4(141 + qi) + VEC4(141 + qj)
                  qk = VEC4(1 + qa * 16 + qb * 4 + qc) - 1
                  VEC1(sc + qk) = VEC1(sc + qk) + qx * qy
               ENDIF
            ENDIF
         NEXT
      ENDIF
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! sinv - sb := 1 / sa, for a series whose constant term is not zero.
!
! Writing sa = c0 (1 + v) with v of zero constant term, the geometric series in v
! TERMINATES at the truncation, so the result is exact for the degree kept rather
! than approximate. Every quantity the trace inverts has a constant term of one, so
! a zero constant term here means a quantity has been built wrongly and not that a
! singularity has been met - hence the refusal rather than a guard value.
! ---------------------------------------------------------------------------------
SUB sinv
   nsrc = sa
   ndst = sb
   nc0 = VEC1(nsrc)
   IF (ABSO(nc0) < eps)
      PRINT "FORBES: sinv on a series with no constant term. In this scheme that"
      PRINT "means a quantity has been built wrongly, not that a singularity was met."
      nfail = nfail + 1
   ELSE
      ! v = sa/c0 - 1, in bn1
      sa = nsrc
      sb = bn1
      sfac = 1 / nc0
      GOSUB sscal
      VEC1(bn1) = VEC1(bn1) - 1

      ! term = 1, sum = 1
      sa = bn2
      GOSUB szero
      VEC1(bn2) = 1
      sa = bn3
      GOSUB szero
      VEC1(bn3) = 1

      FOR ni = 1, ndeg, 1
         ! term := term * (-v)
         sa = bn2
         sb = bn1
         sc = bn4
         GOSUB smul
         sa = bn4
         sb = bn2
         sfac = -1
         GOSUB sscal
         ! sum := sum + term
         sa = bn3
         sb = bn2
         sc = bn3
         GOSUB sadd
      NEXT

      sa = bn3
      sb = ndst
      sfac = 1 / nc0
      GOSUB sscal
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! ssqrt - sb := sqrt(sa), for a series with a positive constant term.
!
! The same reduction as sinv with the binomial series for (1 + v)^(1/2). The only
! square root a symmetric trace takes is of a quantity that is one on the axis - the
! direction cosine (1 - u)^(1/2) - so a non-positive constant term is again a fault
! rather than a singularity.
! ---------------------------------------------------------------------------------
SUB ssqrt
   rsrc = sa
   rdst = sb
   rc0 = VEC1(rsrc)
   IF (rc0 <= eps)
      PRINT "FORBES: ssqrt on a series whose constant term is not positive. The only"
      PRINT "square roots a symmetric trace takes are of quantities that are one on axis."
      nfail = nfail + 1
   ELSE
      ! v = sa/c0 - 1, in br1
      sa = rsrc
      sb = br1
      sfac = 1 / rc0
      GOSUB sscal
      VEC1(br1) = VEC1(br1) - 1

      sa = br2
      GOSUB szero
      VEC1(br2) = 1
      sa = br3
      GOSUB szero
      VEC1(br3) = 1

      rbin = 1
      FOR ri = 1, ndeg, 1
         rbin = rbin * (0.5 - (ri - 1)) / ri
         ! term := term * v
         sa = br2
         sb = br1
         sc = br4
         GOSUB smul
         sa = br4
         sb = br2
         sfac = 1
         GOSUB sscal
         ! sum := sum + binom * term
         sa = br2
         sb = br4
         sfac = rbin
         GOSUB sscal
         sa = br3
         sb = br4
         sc = br3
         GOSUB sadd
      NEXT

      sa = br3
      sb = rdst
      sfac = SQRT(rc0)
      GOSUB sscal
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! sdiff - gmax := the largest absolute difference between the series at sa and sb.
! ---------------------------------------------------------------------------------
SUB sdiff
   gmax = 0
   FOR wi = 0, nterm - 1, 1
      wv = ABSO(VEC1(sa + wi) - VEC1(sb + wi))
      IF (wv > gmax) THEN gmax = wv
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! mkopa, mkopb - two arbitrary test operands. A has constant term one so that it can
! be inverted and square-rooted; B is unconstrained. Both carry terms at every degree
! so that no part of the truncation goes unexercised.
! ---------------------------------------------------------------------------------
SUB mkopa
   sa = ba
   GOSUB szero
   VEC1(ba) = 1
   VEC1(ba + 1) = 0.3
   VEC1(ba + 2) = -0.7
   VEC1(ba + 3) = 0.11
   VEC1(ba + 5) = 0.23
   VEC1(ba + 9) = -0.41
   VEC1(ba + 14) = 0.17
   VEC1(ba + 19) = -0.29
RETURN

SUB mkopb
   sa = bb
   GOSUB szero
   VEC1(bb) = -0.4
   VEC1(bb + 2) = 0.9
   VEC1(bb + 4) = -0.13
   VEC1(bb + 6) = 0.37
   VEC1(bb + 11) = 0.53
   VEC1(bb + 16) = -0.19
RETURN

! ---------------------------------------------------------------------------------
! The six verdicts. Each prints the worst discrepancy it found so that a pass is a
! number rather than a word.
! ---------------------------------------------------------------------------------
SUB report1
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  monomial table round trip           ", worst
   ELSE
      PRINT "   FAIL  monomial table round trip           ", worst
      nfail = nfail + 1
   ENDIF
RETURN

SUB report2
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  (A+B)(A-B) = A*A - B*B              ", worst
   ELSE
      PRINT "   FAIL  (A+B)(A-B) = A*A - B*B              ", worst
      nfail = nfail + 1
   ENDIF
RETURN

SUB report3
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  A * (1/A) = 1                       ", worst
   ELSE
      PRINT "   FAIL  A * (1/A) = 1                       ", worst
      nfail = nfail + 1
   ENDIF
RETURN

SUB report4
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  sqrt(A)^2 = A                       ", worst
   ELSE
      PRINT "   FAIL  sqrt(A)^2 = A                       ", worst
      nfail = nfail + 1
   ENDIF
RETURN

SUB report5
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  (1-u)^(1/2) binomial coefficients   ", worst
   ELSE
      PRINT "   FAIL  (1-u)^(1/2) binomial coefficients   ", worst
      nfail = nfail + 1
   ENDIF
RETURN

SUB report6
   FORMAT 15.6 EXP
   IF (worst < tol)
      PRINT "   PASS  p^2 * p^2 truncates to nothing      ", worst
   ELSE
      PRINT "   FAIL  p^2 * p^2 truncates to nothing      ", worst
      nfail = nfail + 1
   ENDIF
RETURN

! =================================================================================
! STAGE 2 SUBROUTINES
! =================================================================================

! ---------------------------------------------------------------------------------
! The univariate series in p alone. Five coefficients, degrees 0 to 4. These exist
! only to build a surface figure: the sag is a function of p by itself, so carrying
! it in the full twenty-term invariant ring would be the wrong shape as well as four
! times the work - and the figure needs degree four where the ring keeps three.
! ---------------------------------------------------------------------------------

! uzero - the univariate series at ua becomes zero.
SUB uzero
   FOR ai = 0, nfig - 1, 1
      VEC3(ua + ai) = 0
   NEXT
RETURN

! umul - uc := ua * ub, truncated at degree nfig - 1. uc must not alias ua or ub.
SUB umul
   FOR bi = 0, nfig - 1, 1
      VEC3(uc + bi) = 0
   NEXT
   FOR bi = 0, nfig - 1, 1
      bx = VEC3(ua + bi)
      IF (bx != 0)
         FOR bj = 0, nfig - 1 - bi, 1
            by = VEC3(ub + bj)
            IF (by != 0) THEN VEC3(uc + bi + bj) = VEC3(uc + bi + bj) + bx * by
         NEXT
      ENDIF
   NEXT
RETURN

! uinv - ub := 1 / ua. Same terminating geometric series as sinv.
SUB uinv
   csrc = ua
   cdst = ub
   cc0 = VEC3(csrc)
   IF (ABSO(cc0) < eps)
      PRINT "FORBES: uinv on a figure series with no constant term."
      nfail = nfail + 1
   ELSE
      FOR ci2 = 0, nfig - 1, 1
         VEC3(ui1 + ci2) = VEC3(csrc + ci2) / cc0
      NEXT
      VEC3(ui1) = VEC3(ui1) - 1
      ua = ui2
      GOSUB uzero
      VEC3(ui2) = 1
      ua = ui3
      GOSUB uzero
      VEC3(ui3) = 1
      FOR ci2 = 1, nfig - 1, 1
         ua = ui2
         ub = ui1
         uc = ui4
         GOSUB umul
         FOR ci3 = 0, nfig - 1, 1
            VEC3(ui2 + ci3) = -VEC3(ui4 + ci3)
            VEC3(ui3 + ci3) = VEC3(ui3 + ci3) + VEC3(ui2 + ci3)
         NEXT
      NEXT
      FOR ci2 = 0, nfig - 1, 1
         VEC3(cdst + ci2) = VEC3(ui3 + ci2) / cc0
      NEXT
   ENDIF
RETURN

! usqrt - ub := sqrt(ua), constant term positive. Binomial series, as ssqrt.
SUB usqrt
   dsrc = ua
   ddst = ub
   dc0 = VEC3(dsrc)
   IF (dc0 <= eps)
      PRINT "FORBES: usqrt on a figure series whose constant term is not positive."
      nfail = nfail + 1
   ELSE
      FOR di2 = 0, nfig - 1, 1
         VEC3(uq1 + di2) = VEC3(dsrc + di2) / dc0
      NEXT
      VEC3(uq1) = VEC3(uq1) - 1
      ua = uq2
      GOSUB uzero
      VEC3(uq2) = 1
      ua = uq3
      GOSUB uzero
      VEC3(uq3) = 1
      dbin = 1
      FOR di2 = 1, nfig - 1, 1
         dbin = dbin * (0.5 - (di2 - 1)) / di2
         ua = uq2
         ub = uq1
         uc = uq4
         GOSUB umul
         FOR di3 = 0, nfig - 1, 1
            VEC3(uq2 + di3) = VEC3(uq4 + di3)
            VEC3(uq3 + di3) = VEC3(uq3 + di3) + dbin * VEC3(uq4 + di3)
         NEXT
      NEXT
      dr0 = SQRT(dc0)
      FOR di2 = 0, nfig - 1, 1
         VEC3(ddst + di2) = VEC3(uq3 + di2) * dr0
      NEXT
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! mkfig - the figure of one surface, from its vertex position cvz, its curvature
! ccur and its conic ccon, written into VEC2 at cbase.
!
! The sag of a conic is  c p / (1 + sqrt(1 - (1 + K) c^2 p)), built here through the
! univariate series rather than from hand-expanded coefficients, so that the
! expansion is the one the ring's own tests already check. A sphere is K = 0 and an
! even asphere would add A_j p^j - the same code, which is Forbes' whole point and
! what stage 3 turns on.
! ---------------------------------------------------------------------------------
SUB mkfig
   ua = fu6
   GOSUB uzero
   IF (ABSO(ccur) > eps)
      ua = fu1
      GOSUB uzero
      VEC3(fu1) = 1
      VEC3(fu1 + 1) = -(1 + ccon) * ccur * ccur
      ua = fu1
      ub = fu2
      GOSUB usqrt
      ua = fu3
      GOSUB uzero
      FOR ei2 = 0, nfig - 1, 1
         VEC3(fu3 + ei2) = VEC3(fu2 + ei2)
      NEXT
      VEC3(fu3) = VEC3(fu3) + 1
      ua = fu3
      ub = fu4
      GOSUB uinv
      ua = fu5
      GOSUB uzero
      VEC3(fu5 + 1) = ccur
      ua = fu5
      ub = fu4
      uc = fu6
      GOSUB umul
   ENDIF

   ! The even asphere. sag += alpha_j p^j, which is alpha_j r^(2j) - the same code
   ! path as the conic and the reason this route has no separate aspheric machinery.
   ! Added outside the block above so that a FLAT surface carrying figuring works.
   VEC3(fu6 + 1) = VEC3(fu6 + 1) + cas1
   VEC3(fu6 + 2) = VEC3(fu6 + 2) + cas2
   VEC3(fu6 + 3) = VEC3(fu6 + 3) + cas3
   VEC3(fu6 + 4) = VEC3(fu6 + 4) + cas4

   VEC2(cbase) = cvz
   FOR ei2 = 1, nfig - 1, 1
      VEC2(cbase + ei2) = VEC3(fu6 + ei2)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! sdot - the inner product of two vectors held in the (y0, b0) basis, reduced to the
! invariants. With v1 = a1 y0 + b1 b0 and v2 = a2 y0 + b2 b0,
!
!     v1 . v2 = a1 a2 p + (a1 b2 + b1 a2) k + b1 b2 u
!
! because y0.y0 = p, y0.b0 = k and b0.b0 = u. This is the step that makes the whole
! trace scalar: no vector is ever carried componentwise.
!
! Parameters da1, db1, da2, db2 and dres. dres must not be bd1 to bd4.
! ---------------------------------------------------------------------------------
SUB sdot
   ea1 = da1
   eb1 = db1
   ea2 = da2
   eb2 = db2
   eres = dres

   sa = ea1
   sb = ea2
   sc = bd1
   GOSUB smul
   sa = bd1
   sb = bp0
   sc = bd2
   GOSUB smul

   sa = ea1
   sb = eb2
   sc = bd1
   GOSUB smul
   sa = eb1
   sb = ea2
   sc = bd3
   GOSUB smul
   sa = bd1
   sb = bd3
   sc = bd4
   GOSUB sadd
   sa = bd4
   sb = bk0
   sc = bd1
   GOSUB smul
   sa = bd2
   sb = bd1
   sc = bd2
   GOSUB sadd

   sa = eb1
   sb = eb2
   sc = bd1
   GOSUB smul
   sa = bd1
   sb = bu0
   sc = bd3
   GOSUB smul
   sa = bd2
   sb = bd3
   sc = eres
   GOSUB sadd
RETURN

! ---------------------------------------------------------------------------------
! scomp - substitute the invariant series at cser, which must have no constant term,
! into the univariate figure at cfig, leaving the result at cres.
!
! This is how a surface enters the trace: Forbes writes it as x = f(y . y), and y . y
! is itself a series in the invariants. Because cser vanishes on the axis its jth
! power has degree at least j, so the sum terminates at the truncation.
! ---------------------------------------------------------------------------------
SUB scomp
   hser = cser
   hfig = cfig
   hres = cres

   sa = bc1
   GOSUB szero
   VEC1(bc1) = VEC2(hfig)

   sa = bc2
   GOSUB szero
   VEC1(bc2) = 1

   FOR hj = 1, ndeg, 1
      sa = bc2
      sb = hser
      sc = bc3
      GOSUB smul
      sa = bc3
      sb = bc2
      sfac = 1
      GOSUB sscal
      hcf = VEC2(hfig + hj)
      IF (hcf != 0)
         sa = bc2
         sb = bc3
         sfac = hcf
         GOSUB sscal
         sa = bc1
         sb = bc3
         sc = bc1
         GOSUB sadd
      ENDIF
   NEXT

   sa = bc1
   sb = hres
   sfac = 1
   GOSUB sscal
RETURN

! ---------------------------------------------------------------------------------
! refract - Snell at figure gi+1, Forbes M (3.4).
!
! The surface normal is (1, -2 f' y) over its length, both components taken with the
! axial one positive, so with
!
!     G = alpha - 2 f' (b . y)    and    H = sqrt(1 + 4 p f'^2)
!
! the cosine of incidence is G / H. Preserving the tangential component gives
!
!     n' cos I' = sqrt(n'^2 - n^2 + n^2 cos^2 I)
!
! whose constant term is n' and so is safely rooted, and the refracted direction
! follows from n' b' = n b + (n' cos I' - n cos I) times the normal.
! ---------------------------------------------------------------------------------
SUB refract
   krn = INDX(gi)
   krnp = INDX(gi + 1)
   kfig = 1 + (gi + 1) * 8

   ! df/dp of that figure, one degree shorter than f itself.
   FOR ki = 0, nfig - 1, 1
      VEC2(fud + ki) = 0
   NEXT
   FOR ki = 0, nfig - 2, 1
      VEC2(fud + ki) = (ki + 1) * VEC2(kfig + ki + 1)
   NEXT

   da1 = bser
   db1 = bter
   da2 = bser
   db2 = bter
   dres = bpa
   GOSUB sdot

   cser = bpa
   cfig = fud
   cres = bfp
   GOSUB scomp

   da1 = bver
   db1 = bwer
   da2 = bser
   db2 = bter
   dres = bsl
   GOSUB sdot

   ! G = alpha - 2 f' (b . y)
   sa = bfp
   sb = bsl
   sc = bx1
   GOSUB smul
   sa = bx1
   sb = bx2
   sfac = 2
   GOSUB sscal
   sa = bal
   sb = bx2
   sc = bgg
   GOSUB ssub

   ! H = sqrt(1 + 4 p f'^2), and its inverse
   sa = bfp
   sb = bfp
   sc = bx1
   GOSUB smul
   sa = bx1
   sb = bpa
   sc = bx2
   GOSUB smul
   sa = bx2
   sb = bx1
   sfac = 4
   GOSUB sscal
   VEC1(bx1) = VEC1(bx1) + 1
   sa = bx1
   sb = bhh
   GOSUB ssqrt
   sa = bhh
   sb = bih
   GOSUB sinv

   sa = bgg
   sb = bih
   sc = bci
   GOSUB smul

   ! lambda = n' cos I' - n cos I
   sa = bci
   sb = bci
   sc = bx1
   GOSUB smul
   sa = bx1
   sb = bx2
   sfac = krn * krn
   GOSUB sscal
   VEC1(bx2) = VEC1(bx2) + krnp * krnp - krn * krn
   sa = bx2
   sb = bx3
   GOSUB ssqrt
   sa = bci
   sb = bx1
   sfac = krn
   GOSUB sscal
   sa = bx3
   sb = bx1
   sc = bla
   GOSUB ssub

   ! factor = (2/n') lambda f' / H
   sa = bla
   sb = bfp
   sc = bx1
   GOSUB smul
   sa = bx1
   sb = bih
   sc = bx2
   GOSUB smul
   sa = bx2
   sb = bfc
   sfac = 2 / krnp
   GOSUB sscal

   ! V := (n/n') V - factor S ,  W := (n/n') W - factor T
   sa = bver
   sb = bx1
   sfac = krn / krnp
   GOSUB sscal
   sa = bfc
   sb = bser
   sc = bx2
   GOSUB smul
   sa = bx1
   sb = bx2
   sc = bx3
   GOSUB ssub

   sa = bwer
   sb = bx1
   sfac = krn / krnp
   GOSUB sscal
   sa = bfc
   sb = bter
   sc = bx2
   GOSUB smul
   sa = bx1
   sb = bx2
   sc = bx4
   GOSUB ssub

   sa = bx3
   sb = bver
   GOSUB scopy
   sa = bx4
   sb = bwer
   GOSUB scopy
RETURN

! ---------------------------------------------------------------------------------
! shows - print the four series, with the constant terms called out because those
! three can be checked against first-order optics at a glance and need no reference.
! ---------------------------------------------------------------------------------
SUB shows
   PRINT " "
   PRINT "==================================================================="
   PRINT "STAGE 2 - the trace"
   PRINT "==================================================================="
   PRINT " "
   FORMAT 5.0 INT
   PRINT "Optical surfaces           ", nsm
   FORMAT 12.6
   PRINT "Paraxial focus distance    ", gpfd
   PRINT " "
   ! ONLY -1/V IS A CHECK AT EITHER CONJUGATE. V is the optical power, so its
   ! constant term is minus the reciprocal of the focal length wherever the object
   ! sits. T(0,0,0) and S(0,0,0) are NOT universal: they describe the input base
   ! plane's relation to the output plane, which is a conjugate pair only when the
   ! object is at infinity. An earlier version labelled all three as though they
   ! held always, so on a finite-conjugate lens it printed 58.78 beside 50.00 and
   ! said nothing about the difference. That is the sort of quiet disagreement the
   ! rest of this file exists to refuse.
   PRINT "First-order checks on the constant terms, which need no reference:"
   PRINT "   -1 / V(0,0,0) is the focal length ", -1 / VEC1(bver)
   IF (infconj == 1)
      PRINT "   T(0,0,0) is the same thing        ", VEC1(bter)
      PRINT "   S(0,0,0) is zero at focus         ", VEC1(bser)
      wdif = ABSO(VEC1(bter) + 1 / VEC1(bver)) / (1 + ABSO(VEC1(bter)))
      IF (wdif > 1E-6)
         PRINT " "
         PRINT "   WARNING: those two reach the focal length by different paths - T"
         PRINT "   through the transfers and V through the refractions - and they"
         PRINT "   disagree, so the trace is wrong."
      ENDIF
   ELSE
      PRINT "   T(0,0,0)                          ", VEC1(bter)
      PRINT "   S(0,0,0)                          ", VEC1(bser)
      PRINT " "
      PRINT "   The object is at a finite distance, so T(0,0,0) is NOT the focal"
      PRINT "   length and S(0,0,0) is NOT zero. Both describe where the input base"
      PRINT "   plane - the first surface's vertex - sits relative to the output"
      PRINT "   plane, and those are a conjugate pair only at infinite conjugate."
      PRINT "   Only the focal length above is a check here."
   ENDIF
   PRINT " "
   PRINT " m   p  k  u              S              T              V              W"

   ! FORMAT n.0 INT PRINTS A ZERO AS BLANK. The first draft of this table used it and
   ! came out with the m = 0 row unlabelled and every zero power missing, so that
   ! p^0 k^0 u^3 read as though it were blank blank 3. Fixed format has no such
   ! trouble - the header above prints -0.000000 for a quantity of 1E-16 - so the
   ! integer columns are printed as fixed with no decimals instead.
   FOR mi = 0, nterm - 1, 1
      FORMAT 4.0
      PRINT mi, VEC4(101 + mi), VEC4(121 + mi), VEC4(141 + mi),
      FORMAT 15.6 EXP
      PRINT VEC1(bser + mi), VEC1(bter + mi), VEC1(bver + mi), VEC1(bwer + mi)
   NEXT
   PRINT " "
   PRINT "Stage 2 ends here. The twenty named coefficients are read off these four by"
   PRINT "a linear inversion over a set of ray shapes - that is stage 4, and it is a"
   PRINT "piece of work in its own right rather than a printing step."
RETURN

! =================================================================================
! STAGE 4 SUBROUTINES - the twenty named coefficients, and the twelve and five below
! them.
!
! S, T, V and W carry the whole aberration content, but not in a form anyone quotes.
! Getting the named coefficients out is not a printing step:
!
!   1. Shrink a ray by a scale s. The pupil goes to s rho and the field to s h.
!   2. Follow where it lands, AS A SERIES IN s. That needs a second series type -
!      univariate in s, to order seven - because the direction cosine is
!      sin = tan / sqrt(1 + tan^2), whose cubic and quintic parts carry lower-degree
!      parts of S and T up into the seventh. Reading the degree-three part of S and
!      T alone would drop that and be wrong by of order the field squared.
!   3. The coefficient of s^3, s^5 and s^7 is the third-, fifth- and seventh-order
!      transverse aberration of that ray, exactly - not fitted.
!   4. Do that for seventy-five ray shapes and solve the resulting overdetermined
!      linear system for the coefficients.
!
! The model in step 4 is Robb's Eq. (2) with one coefficient set to one at a time,
! which is the same polynomial ROBB.ZPL evaluates. So nothing new is derived here:
! the model rows are its degree-3, degree-5 and degree-7 terms written out.
!
! DISTORTION HAS TO BE PUT BACK. Robb's polynomial is a SPOT SIZE formula and says
! so: E, E5 and tau20 displace the whole patch without changing its size, so they
! are deliberately absent from it. A landing ray does contain them. Their form is
! forced rather than chosen - no aperture dependence, hence H^3, H^5 and H^7 with no
! azimuth - and they are the last column of each model below.
! =================================================================================

! ---------------------------------------------------------------------------------
! The scale series: univariate in s, coefficients 0 to 7. A third ring, and the last.
! ---------------------------------------------------------------------------------

! zzero - the scale series at za becomes zero.
SUB zzero
   FOR zi = 0, nzc - 1, 1
      VEC3(za + zi) = 0
   NEXT
RETURN

! zmul - zc := za * zb, truncated at order seven. zc must not alias za or zb.
SUB zmul
   FOR zj = 0, nzc - 1, 1
      VEC3(zc + zj) = 0
   NEXT
   FOR zj = 0, nzc - 1, 1
      zx = VEC3(za + zj)
      IF (zx != 0)
         FOR zk = 0, nzc - 1 - zj, 1
            zy = VEC3(zb + zk)
            IF (zy != 0) THEN VEC3(zc + zj + zk) = VEC3(zc + zj + zk) + zx * zy
         NEXT
      ENDIF
   NEXT
RETURN

! zadd - zc := za + zb.
SUB zadd
   FOR zi = 0, nzc - 1, 1
      VEC3(zc + zi) = VEC3(za + zi) + VEC3(zb + zi)
   NEXT
RETURN

! zscal - zb := za * zfac.
SUB zscal
   FOR zi = 0, nzc - 1, 1
      VEC3(zb + zi) = VEC3(za + zi) * zfac
   NEXT
RETURN

! zsqrt - zb := sqrt(za), constant term positive.
SUB zsqrt
   zqs = za
   zqd = zb
   zqc = VEC3(zqs)
   FOR zqi = 0, nzc - 1, 1
      VEC3(zq1 + zqi) = VEC3(zqs + zqi) / zqc
   NEXT
   VEC3(zq1) = VEC3(zq1) - 1
   za = zq2
   GOSUB zzero
   VEC3(zq2) = 1
   za = zq3
   GOSUB zzero
   VEC3(zq3) = 1
   zqb = 1
   FOR zqi = 1, nzc - 1, 1
      zqb = zqb * (0.5 - (zqi - 1)) / zqi
      za = zq2
      zb = zq1
      zc = zq4
      GOSUB zmul
      FOR zqj = 0, nzc - 1, 1
         VEC3(zq2 + zqj) = VEC3(zq4 + zqj)
         VEC3(zq3 + zqj) = VEC3(zq3 + zqj) + zqb * VEC3(zq4 + zqj)
      NEXT
   NEXT
   zqr = SQRT(zqc)
   FOR zqi = 0, nzc - 1, 1
      VEC3(zqd + zqi) = VEC3(zq3 + zqi) * zqr
   NEXT
RETURN

! zinv - zb := 1 / za, constant term not zero.
SUB zinv
   zvs = za
   zvd = zb
   zvc = VEC3(zvs)
   FOR zvi = 0, nzc - 1, 1
      VEC3(zv1 + zvi) = VEC3(zvs + zvi) / zvc
   NEXT
   VEC3(zv1) = VEC3(zv1) - 1
   za = zv2
   GOSUB zzero
   VEC3(zv2) = 1
   za = zv3
   GOSUB zzero
   VEC3(zv3) = 1
   FOR zvi = 1, nzc - 1, 1
      za = zv2
      zb = zv1
      zc = zv4
      GOSUB zmul
      FOR zvj = 0, nzc - 1, 1
         VEC3(zv2 + zvj) = -VEC3(zv4 + zvj)
         VEC3(zv3 + zvj) = VEC3(zv3 + zvj) + VEC3(zv2 + zvj)
      NEXT
   NEXT
   FOR zvi = 0, nzc - 1, 1
      VEC3(zvd + zvi) = VEC3(zv3 + zvi) / zvc
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! zeval - re-express an invariant series as a series in the scale.
!
! The series at yser is a polynomial in p, k and u; the three are themselves scale
! series here, so the result is one too. The powers are built once rather than formed
! afresh for each of the twenty monomials.
! ---------------------------------------------------------------------------------
SUB zeval
   ysrc = yser
   ydst = yres

   za = zp0
   GOSUB zzero
   VEC3(zp0) = 1
   za = zk0
   GOSUB zzero
   VEC3(zk0) = 1
   za = zu0
   GOSUB zzero
   VEC3(zu0) = 1

   FOR yi = 1, ndeg, 1
      za = zp0 + (yi - 1) * 10
      zb = zpp
      zc = zp0 + yi * 10
      GOSUB zmul
      za = zk0 + (yi - 1) * 10
      zb = zkk
      zc = zk0 + yi * 10
      GOSUB zmul
      za = zu0 + (yi - 1) * 10
      zb = zuu
      zc = zu0 + yi * 10
      GOSUB zmul
   NEXT

   za = ydst
   GOSUB zzero

   FOR yi = 0, nterm - 1, 1
      ycf = VEC1(ysrc + yi)
      IF (ycf != 0)
         za = zp0 + VEC4(101 + yi) * 10
         zb = zk0 + VEC4(121 + yi) * 10
         zc = zt1
         GOSUB zmul
         IF (VEC4(141 + yi) > 0)
            za = zt1
            zb = zu0 + VEC4(141 + yi) * 10
            zc = zt2
            GOSUB zmul
            za = zt2
            zb = zt1
            zfac = 1
            GOSUB zscal
         ENDIF
         za = zt1
         zb = zt2
         zfac = ycf
         GOSUB zscal
         za = ydst
         zb = zt2
         zc = ydst
         GOSUB zadd
      ENDIF
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! mkrhs - where a ray of shape (srho, sthe, shh) lands, as a series in the scale.
!
! At infinite conjugate the HEIGHT at the input base plane is exactly linear in the
! scale: the walk back from the entrance pupil contributes -ep tan(field), and the
! inversion's own parameterisation makes the tangent linear. The DIRECTION is not,
! and that is the whole reason this subroutine exists rather than a multiplication.
! ---------------------------------------------------------------------------------
SUB mkrhs
   IF (infconj == 1)

      ! INFINITE CONJUGATE. The beam is collimated, so the HEIGHT at the input base
      ! plane is exactly linear in the scale - the walk back from the entrance pupil
      ! contributes -ep tan(field) and the tangent is linear by the inversion's own
      ! parameterisation. The DIRECTION is not, and that is why this is a series.

      ! tan(field) = s * h * hmax
      za = ztn
      GOSUB zzero
      VEC3(ztn + 1) = shh * hmax

      ! sin = tan / sqrt(1 + tan^2)
      za = ztn
      zb = ztn
      zc = zt3
      GOSUB zmul
      VEC3(zt3) = VEC3(zt3) + 1
      za = zt3
      zb = zt4
      GOSUB zsqrt
      za = zt4
      zb = zt3
      GOSUB zinv
      za = ztn
      zb = zt3
      zc = zsn
      GOSUB zmul

      za = zsy
      GOSUB zzero
      VEC3(zsy + 1) = srho * COSI(sthe) * epr - epp * shh * hmax
      za = zsz
      GOSUB zzero
      VEC3(zsz + 1) = srho * SINE(sthe) * epr

      ! zscal is zb := za * zfac - SOURCE in za, DESTINATION in zb. These two were
      ! once the other way round, which copied the still-empty zby into zsn instead
      ! of the other way about, so the direction cosine bm was zero for every ray.
      ! The symptom was that every coefficient carrying no field came out exactly
      ! right and every coefficient carrying field came out wrong, because with bm
      ! zero the invariants k and u vanish and the ray has aperture but no field.
      za = zsn
      zb = zby
      zfac = 1
      GOSUB zscal
      za = zbz
      GOSUB zzero

   ELSE

      ! FINITE CONJUGATE. The ray is the straight line from the object point to the
      ! chosen point on the entrance pupil. Its POSITION where it crosses surface
      ! one's vertex plane is a weighted mean of the two, so the normalisation
      ! cancels and it stays exactly linear in the scale, as the collimated case is.
      !
      ! Its DIRECTION is not linear: the transverse numerators scale with the ray
      ! and the axial one does not, so the normalising root carries lower-degree
      ! parts of S and T up into the seventh. And the SAGITTAL direction no longer
      ! vanishes - a skew ray from a finite object point is tilted out of the
      ! meridian - which is why bs is built here and is zero only above.

      wdd = epp + objd
      wyp = srho * COSI(sthe) * epr
      wxp = srho * SINE(sthe) * epr
      whb = shh * objh

      za = zsy
      GOSUB zzero
      VEC3(zsy + 1) = (whb * epp + wyp * objd) / wdd
      za = zsz
      GOSUB zzero
      VEC3(zsz + 1) = wxp * objd / wdd

      ! the unnormalised direction, in zt3 and zt4
      za = zt3
      GOSUB zzero
      VEC3(zt3 + 1) = (wyp - whb) / wdd
      za = zt4
      GOSUB zzero
      VEC3(zt4 + 1) = wxp / wdd

      ! 1 / sqrt(1 + dy^2 + dx^2), into ztn
      za = zt3
      zb = zt3
      zc = ztn
      GOSUB zmul
      za = zt4
      zb = zt4
      zc = zsn
      GOSUB zmul
      za = ztn
      zb = zsn
      zc = ztn
      GOSUB zadd
      VEC3(ztn) = VEC3(ztn) + 1
      za = ztn
      zb = zsn
      GOSUB zsqrt
      za = zsn
      zb = ztn
      GOSUB zinv

      za = zt3
      zb = ztn
      zc = zby
      GOSUB zmul
      za = zt4
      zb = ztn
      zc = zbz
      GOSUB zmul

   ENDIF

   ! the invariants
   za = zsy
   zb = zsy
   zc = zt1
   GOSUB zmul
   za = zsz
   zb = zsz
   zc = zt2
   GOSUB zmul
   za = zt1
   zb = zt2
   zc = zpp
   GOSUB zadd

   za = zsy
   zb = zby
   zc = zt1
   GOSUB zmul
   za = zsz
   zb = zbz
   zc = zt2
   GOSUB zmul
   za = zt1
   zb = zt2
   zc = zkk
   GOSUB zadd

   za = zby
   zb = zby
   zc = zt1
   GOSUB zmul
   za = zbz
   zb = zbz
   zc = zt2
   GOSUB zmul
   za = zt1
   zb = zt2
   zc = zuu
   GOSUB zadd

   ! S and T on them
   yser = bser
   yres = zss
   GOSUB zeval
   yser = bter
   yres = ztt
   GOSUB zeval

   ! and where the ray lands
   za = zss
   zb = zsy
   zc = zt1
   GOSUB zmul
   za = ztt
   zb = zby
   zc = zt2
   GOSUB zmul
   za = zt1
   zb = zt2
   zc = zom
   GOSUB zadd

   za = zss
   zb = zsz
   zc = zt1
   GOSUB zmul
   za = ztt
   zb = zbz
   zc = zt2
   GOSUB zmul
   za = zt1
   zb = zt2
   zc = zos
   GOSUB zadd
RETURN

! ---------------------------------------------------------------------------------
! mkmod - the model row for shape (srho, sthe, shh) at order mord.
!
! This is Robb's Eq. (2) with one coefficient set to one at a time, keeping only the
! terms of total degree mord. It is the same polynomial ROBB.ZPL evaluates; nothing
! new is derived here. The last column of each is distortion, put back by hand for
! the reason given at the head of this section.
!
! Rows land in VEC2(mrowy + i) and VEC2(mrowz + i), i from 0.
! ---------------------------------------------------------------------------------
SUB mkmod
   mc = COSI(sthe)
   ms = SINE(sthe)
   mc2 = COSI(2 * sthe)
   ms2 = SINE(2 * sthe)
   mc4 = COSI(4 * sthe)
   ms4 = SINE(4 * sthe)
   mc3 = mc * mc * mc
   mcs = mc * mc * ms

   mr1 = srho
   mr2 = mr1 * srho
   mr3 = mr2 * srho
   mr4 = mr3 * srho
   mr5 = mr4 * srho
   mr6 = mr5 * srho
   mr7 = mr6 * srho
   mh1 = shh
   mh2 = mh1 * shh
   mh3 = mh2 * shh
   mh4 = mh3 * shh
   mh5 = mh4 * shh
   mh6 = mh5 * shh
   mh7 = mh6 * shh

   FOR mi = 0, 19, 1
      VEC2(mrowy + mi) = 0
      VEC2(mrowz + mi) = 0
   NEXT

   IF (mord == 3)
      VEC2(mrowy + 0) = mr3 * mc
      VEC2(mrowz + 0) = mr3 * ms
      VEC2(mrowy + 1) = 2 * mr2 * mh1 + mr2 * mh1 * mc2
      VEC2(mrowz + 1) = mr2 * mh1 * ms2
      VEC2(mrowy + 2) = 3 * mr1 * mh2 * mc
      VEC2(mrowz + 2) = mr1 * mh2 * ms
      VEC2(mrowy + 3) = mr1 * mh2 * mc
      VEC2(mrowz + 3) = mr1 * mh2 * ms
      VEC2(mrowy + 4) = mh3
   ENDIF

   IF (mord == 5)
      VEC2(mrowy + 0) = mr5 * mc
      VEC2(mrowz + 0) = mr5 * ms
      VEC2(mrowy + 1) = mr4 * mh1
      VEC2(mrowy + 2) = mr4 * mh1 * mc2
      VEC2(mrowz + 2) = mr4 * mh1 * ms2
      VEC2(mrowy + 3) = mr3 * mh2 * mc
      VEC2(mrowy + 4) = mr3 * mh2 * mc
      VEC2(mrowz + 4) = mr3 * mh2 * ms
      VEC2(mrowy + 5) = mr3 * mh2 * mc3
      VEC2(mrowz + 5) = mr3 * mh2 * mcs
      VEC2(mrowy + 6) = mr2 * mh3
      VEC2(mrowy + 7) = 0.5 * mr2 * mh3 + 0.5 * mr2 * mh3 * mc2
      VEC2(mrowz + 8) = mr2 * mh3 * ms2
      VEC2(mrowy + 9) = 5 * mr1 * mh4 * mc
      VEC2(mrowz + 9) = mr1 * mh4 * ms
      VEC2(mrowy + 10) = mr1 * mh4 * mc
      VEC2(mrowz + 10) = mr1 * mh4 * ms
      VEC2(mrowy + 11) = mh5
   ENDIF

   IF (mord == 7)
      VEC2(mrowy + 0) = mr7 * mc
      VEC2(mrowz + 0) = mr7 * ms
      VEC2(mrowy + 1) = mr6 * mh1
      VEC2(mrowy + 2) = mr6 * mh1 * mc2
      VEC2(mrowz + 2) = mr6 * mh1 * ms2
      VEC2(mrowy + 3) = mr5 * mh2 * mc
      VEC2(mrowz + 4) = mr5 * mh2 * ms
      VEC2(mrowy + 5) = mr5 * mh2 * mc3
      VEC2(mrowz + 5) = mr5 * mh2 * mcs
      VEC2(mrowy + 6) = mr4 * mh3
      VEC2(mrowy + 7) = mr4 * mh3 * mc2
      VEC2(mrowz + 8) = mr4 * mh3 * ms2
      VEC2(mrowy + 9) = mr4 * mh3 * mc4
      VEC2(mrowz + 9) = mr4 * mh3 * ms4
      VEC2(mrowy + 10) = mr3 * mh4 * mc
      VEC2(mrowy + 11) = mr3 * mh4 * mc3
      VEC2(mrowz + 12) = mr3 * mh4 * ms
      VEC2(mrowz + 13) = mr3 * mh4 * mcs
      VEC2(mrowy + 14) = mr2 * mh5
      VEC2(mrowy + 15) = mr2 * mh5 * mc2
      VEC2(mrowz + 16) = mr2 * mh5 * ms2
      VEC2(mrowy + 17) = mr1 * mh6 * mc
      VEC2(mrowz + 18) = mr1 * mh6 * ms
      VEC2(mrowy + 19) = mh7
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! gsolve - solve the nunk by nunk system at VEC2(gata) with right-hand side at
! VEC2(gatb), leaving the answer in VEC2(gatb). Gaussian elimination with partial
! pivoting.
!
! These are the NORMAL equations, formed as A'A and A'b rather than by factorising A.
! That squares the condition number, which is a real objection in general and was
! checked rather than waved away here: against the C# - which uses a Householder QR
! on the same system - the answers agree to 2E-13 at seventh order and 4E-14 at
! third. The model is polynomial evaluations at well separated shapes and is nowhere
! near ill-conditioned enough for the difference to show.
! ---------------------------------------------------------------------------------
SUB gsolve
   FOR gc = 0, nunk - 1, 1
      gp = gc
      gbest = ABSO(VEC2(gata + gc * nunk + gc))

      ! GUARDED because ZPL treats an EMPTY range as an infinite loop rather than
      ! executing it zero times. On the last column gc + 1 exceeds nunk - 1 and the
      ! macro stops with "Infinite FOR LOOP detected". Every descending-bound loop
      ! below carries the same guard for the same reason.
      IF (gc < nunk - 1)
         FOR gr = gc + 1, nunk - 1, 1
            gv2 = ABSO(VEC2(gata + gr * nunk + gc))
            IF (gv2 > gbest)
               gbest = gv2
               gp = gr
            ENDIF
         NEXT
      ENDIF
      IF (gp != gc)
         FOR gk2 = 0, nunk - 1, 1
            gtmp = VEC2(gata + gc * nunk + gk2)
            VEC2(gata + gc * nunk + gk2) = VEC2(gata + gp * nunk + gk2)
            VEC2(gata + gp * nunk + gk2) = gtmp
         NEXT
         gtmp = VEC2(gatb + gc)
         VEC2(gatb + gc) = VEC2(gatb + gp)
         VEC2(gatb + gp) = gtmp
      ENDIF
      gd = VEC2(gata + gc * nunk + gc)
      IF (ABSO(gd) < 1E-300)
         PRINT "FORBES: the normal equations are singular. The shape set cannot"
         PRINT "separate the coefficients, which should not happen with the standard"
         PRINT "seventy-five and means the model rows are wrong."
         nfail = nfail + 1
      ELSE
         IF (gc < nunk - 1)
            FOR gr = gc + 1, nunk - 1, 1
               gf = VEC2(gata + gr * nunk + gc) / gd
               IF (gf != 0)
                  FOR gk2 = gc, nunk - 1, 1
                     VEC2(gata + gr * nunk + gk2) = VEC2(gata + gr * nunk + gk2) - gf * VEC2(gata + gc * nunk + gk2)
                  NEXT
                  VEC2(gatb + gr) = VEC2(gatb + gr) - gf * VEC2(gatb + gc)
               ENDIF
            NEXT
         ENDIF
      ENDIF
   NEXT

   FOR gr = nunk - 1, 0, -1
      gs = VEC2(gatb + gr)
      IF (gr < nunk - 1)
         FOR gk2 = gr + 1, nunk - 1, 1
            gs = gs - VEC2(gata + gr * nunk + gk2) * VEC2(gatb + gk2)
         NEXT
      ENDIF
      VEC2(gatb + gr) = gs / VEC2(gata + gr * nunk + gr)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! accum - add one shape's contribution to the normal equations at gata and gatb.
!
! The system is A'A x = A'b, accumulated a row at a time so that the full model
! matrix - 150 rows by 20 columns - never has to be held.
! ---------------------------------------------------------------------------------
SUB accum
   FOR ai = 0, nunk - 1, 1
      VEC2(gatb + ai) = VEC2(gatb + ai) + VEC2(mrowy + ai) * ry + VEC2(mrowz + ai) * rz
      FOR aj = 0, nunk - 1, 1
         apr = VEC2(mrowy + ai) * VEC2(mrowy + aj) + VEC2(mrowz + ai) * VEC2(mrowz + aj)
         VEC2(gata + ai * nunk + aj) = VEC2(gata + ai * nunk + aj) + apr
      NEXT
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! showco - the coefficients.
!
! All three orders are in TRANSVERSE measure and lens units, the same measure BUCH7
! prints its system totals in, so the two can be compared directly on a system of
! spheres. That comparison is the point: the third order can also be checked against
! Analyze > Aberrations > Seidel Coefficients and the fifth against FIFTHORD, neither
! of which is in this repository, and OpticStudio's Seidel analysis carries conics
! and aspheres - so on a figured lens it is the only independent check there is.
! ---------------------------------------------------------------------------------
SUB showco
   PRINT " "
   PRINT "==================================================================="
   PRINT "STAGE 4 - the aberration coefficients, transverse measure"
   PRINT "==================================================================="
   PRINT " "
   FORMAT 12.6
   PRINT "Entrance pupil radius      ", epr
   PRINT "Entrance pupil position    ", epp

   ! How the field is measured depends on the conjugate, so print the quantity that
   ! is actually being used. Printing "tan of the maximum field 0.000000" at a
   ! finite conjugate is true - hmax is not used there - but it reads as a failure.
   IF (infconj == 1)
      PRINT "Object at infinity"
      PRINT "tan of the maximum field   ", hmax
   ELSE
      PRINT "Object distance            ", objd
      PRINT "Maximum object height      ", objh
      PRINT "   The field is an object height at this conjugate, not an angle, so"
      PRINT "   there is no tangent to report."
   ENDIF
   FORMAT 5.0 INT
   PRINT "Ray shapes used            ", nsh
   PRINT " "
   FORMAT 15.6 EXP
   PRINT "Third order"
   PRINT "   B  ", VEC2(2101), "   F  ", VEC2(2102), "   C  ", VEC2(2103)
   PRINT "   Pi ", VEC2(2104), "   E  ", VEC2(2105)
   PRINT " "
   PRINT "Fifth order"
   PRINT "   B5 ", VEC2(2401), "   F1 ", VEC2(2402), "   F2 ", VEC2(2403)
   PRINT "   M1 ", VEC2(2404), "   M2 ", VEC2(2405), "   M3 ", VEC2(2406)
   PRINT "   N1 ", VEC2(2407), "   N2 ", VEC2(2408), "   N3 ", VEC2(2409)
   PRINT "   C5 ", VEC2(2410), "   Pi5", VEC2(2411), "   E5 ", VEC2(2412)
   PRINT " "
   PRINT "Seventh order, Buchdahl's tau. tau1 is B7."
   PRINT "   tau1 ", VEC2(3001), "  tau2 ", VEC2(3002), "  tau3 ", VEC2(3003)
   PRINT "   tau4 ", VEC2(3004), "  tau5 ", VEC2(3005), "  tau6 ", VEC2(3006)
   PRINT "   tau7 ", VEC2(3007), "  tau8 ", VEC2(3008), "  tau9 ", VEC2(3009)
   PRINT "   tau10", VEC2(3010), "  tau11", VEC2(3011), "  tau12", VEC2(3012)
   PRINT "   tau13", VEC2(3013), "  tau14", VEC2(3014), "  tau15", VEC2(3015)
   PRINT "   tau16", VEC2(3016), "  tau17", VEC2(3017), "  tau18", VEC2(3018)
   PRINT "   tau19", VEC2(3019), "  tau20", VEC2(3020)
   PRINT " "
   PRINT "HOW TO CHECK THESE WITHOUT TRUSTING THIS MACRO"
   PRINT " "
   PRINT "ON A SYSTEM OF SPHERES all three orders must equal what BUCH7 prints, which"
   PRINT "is a wholly different scheme - Buchdahl's arranged tables against Forbes'"
   PRINT "series trace, sharing no arithmetic. tau1 must equal BUCH7's B7. That"
   PRINT "agreement has been demonstrated on a Cooke triplet, and on spheres only."
   PRINT " "
   PRINT "ON A FIGURED SYSTEM the comparison splits, and it splits exactly:"
   PRINT " "
   PRINT "   tau1, which is B7, IS RIGHT in the Buchdahl route even for an asphere."
   PRINT "   It is not reached through the aspheric table at all - it falls out of"
   PRINT "   the fifth-order working, which is why BUCH7 arrives at it twice over and"
   PRINT "   gets the same number both ways. Compare it with confidence."
   PRINT " "
   PRINT "   tau2 TO tau20 were NOT right there for a long time, and now they are."
   PRINT "   Buchdahl gives the aspheric scheme in the monograph but never published"
   PRINT "   an ARRANGED TABLE for it. BUCH7_ASPH.ZPL, in this same folder, is that"
   PRINT "   arrangement - the identities of M Sec. 22 for members one to five of the"
   PRINT "   barred q accumulation, the dual run of XII Sec. 6 for the sixth, and the"
   PRINT "   figuring's D half carried in the hat pass."
   PRINT " "
   PRINT "   Run BUCH7_ASPH on this same file: it reproduces all twenty printed above"
   PRINT "   to every digit either macro prints. That has been done on a conic"
   PRINT "   singlet, on a conic carrying r^4, r^6 and r^8 together, and on a triplet"
   PRINT "   with two figured surfaces where one induces on the other."
   PRINT " "
   PRINT "So the two routes agree on a figured lens as well as a spherical one, and"
   PRINT "they still share no arithmetic - Buchdahl's arranged tables against an"
   PRINT "order-doubling series trace. That is what makes the agreement worth having."
   PRINT " "
   PRINT "BUCH7.ZPL itself still declines a figured lens: it is the SPHERICAL"
   PRINT "routine, checked against Buchdahl's own printed numbers, left undisturbed."
   PRINT " "
   PRINT "THE CHECK THAT NEEDS NOTHING FROM THIS REPOSITORY, AND WORKS ON ASPHERES:"
   PRINT " "
   IF (anyr2 == 0)
      PRINT "FIFTHORD.ZPL ships with OpticStudio and CARRIES ASPHERES - it prints the"
      PRINT "aspheric contribution surface by surface. Its transverse totals are the"
      PRINT "same eighteen quantities printed above: B through E5, and B7 which is"
      PRINT "tau1. Run it on the same file and the two must agree."
      PRINT " "
      PRINT "That has been done. On CookeTriplet_SPOTM_START_LO_ASPHERE, which carries"
      PRINT "r^4 and r^6 figuring on two surfaces and which BUCH7 declines outright, all"
      PRINT "EIGHTEEN agree to every digit FIFTHORD prints - worst 3.7E-05, which is its"
      PRINT "own five-figure rounding. That includes B7 against tau1, and is the direct"
      PRINT "evidence for the claim above that B7 survives the aspheric case."
   ELSE
      PRINT "NOT ON THIS FILE. A surface here carries a non-zero PARM 1, the r^2"
      PRINT "deformation, and FIFTHORD does not handle that term. It is not that it"
      PRINT "ignores it and returns the answer for the surface without it: it takes"
      PRINT "paraxial data from OpticStudio, which INCLUDES the power the term adds,"
      PRINT "and then computes each surface's contribution from the BASE curvature."
      PRINT "The two halves describe different surfaces, so its answer is neither"
      PRINT "lens. Its author flags the term as 'not used in this treatment', which"
      PRINT "reads as an omission and is in fact an inconsistency."
      PRINT " "
      PRINT "The numbers above are not in doubt. Compare them with BUCH7_ASPH.ZPL,"
      PRINT "which folds the r^2 term into the vertex curvature and shares no"
      PRINT "arithmetic with the trace above. On a conic carrying r^4, r^6, r^8 and"
      PRINT "PARM 1 = 1E-4 the two agree on all eighteen totals and all twenty tau to"
      PRINT "every digit either prints."
   ENDIF
   PRINT " "
   PRINT "The third order can also be had from Analyze > Aberrations > Seidel"
   PRINT "Coefficients, which carries aspheres too. Converted to transverse measure"
   PRINT "it agrees with the five printed above to the figures it prints."
RETURN
