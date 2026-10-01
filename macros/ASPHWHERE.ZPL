! ASPHWHERE - WHERE an asphere would do the most good, and what it would buy.
!
! Every other macro in this folder says what is wrong with the lens in hand, per
! surface. This one asks the question the other way round: an asphere costs money,
! so if you can afford one, where does it go? Third order only.
!
! G. Schulz, "Aspheric surfaces", in E. Wolf (ed.), PROGRESS IN OPTICS XXV
! (Elsevier, 1988), Sec. 3.3, reading the Delano (H, h) diagram:
!
!   "The normalized ratio H_i/h_i quantitatively determines the weight by which an
!    aspheric deformation of the surface i influences the individual Seidel
!    aberrations. If |H_i/h_i| is small, primarily the spherical aberration can be
!    controlled, and if |H_i/h_i| is large, primarily the distortion. If |H_i/h_i|
!    has a value in a middle range, the asphericity parameter influences all five
!    Seidel aberrations by nearly equal weights."
!
! ---------------------------------------------------------------------------------
! IT CHANGES NOTHING. THE LENS IS NOT TOUCHED.
!
!   No surface type is switched, no PARM is written, nothing is restored afterwards
!   because nothing is disturbed. The whole calculation is paraxial: two ray traces,
!   the index steps, and arithmetic. A figure is never applied - what is reported is
!   what one WOULD do.
!
!   That is not a convenience, it is the design. A macro that edits the lens must put
!   it back, and if it stops on an error part way through it leaves the design damaged
!   on your screen. This one cannot.
!
!   It follows that the macro cannot mark its own work. If you want to see a
!   prediction come true, type the coefficient into PARM 2 of the surface named and
!   watch Analyze > Aberrations > Seidel Coefficients move. That is more convincing
!   than a macro agreeing with itself.
! ---------------------------------------------------------------------------------
! WHAT IS APPLIED, AND WHAT IS NOT
!
!   r^4 AND ONLY r^4 - one coefficient, PARM 2 of an EVENASPH surface. That is forced
!   rather than chosen. A deformation r^n first reaches transverse order n-1, so r^6
!   reaches the fifth order and r^8 the seventh, and NEITHER CAN MOVE A SEIDEL SUM AT
!   ALL. r^4 is the only figuring the third order can see.
!
!   r^2, PARM 1, IS NOT TOUCHED. It is not figuring: it is a curvature change and it
!   moves the focal length. Where a design carries one, the paraxial rays traced below
!   already account for it, because OpticStudio's own first-order data does - but
!   nothing here proposes changing it.
!
!   A CONIC WOULD DO THE SAME JOB AT THIS ORDER and is deliberately not offered. A
!   conic K on a surface of vertex curvature c is an r^4 coefficient of K c^3 / 8, so
!   either states the same third-order correction - but they are NOT the same surface.
!   A conic drags r^6 and r^8 along with it, growing as K^2 and K^3, so the two agree
!   here and part company at the fifth and seventh orders, which this macro cannot
!   see. A conic also cannot figure a flat at all, since c^3 is then zero.
!
!   PETZVAL CANNOT BE REACHED BY ANY OF IT. S4 depends on the surface curvatures and
!   the index steps alone. Schulz says the same - the Petzval condition "cannot be
!   influenced by asphericities" - and it is why a flat field can need a glass change
!   or another element rather than a figure.
! ---------------------------------------------------------------------------------
! LIMITS, STATED PLAINLY
!
!   STANDARD and EVENASPH surfaces only, declined by name on anything else, because
!   PARM on a toroid or a grating is not an aspheric coefficient and reading it as one
!   would give a confident wrong answer rather than an error.
!
!   THIRD ORDER ONLY. The aspheric contribution is exactly LINEAR in the coefficient
!   at this order, so the sensitivities below are exact derivatives with no step size
!   to choose and the "what it buys" table is exact rather than linearised. That is a
!   property of the third order. A figure sized here WILL move the fifth and seventh
!   orders, and this macro does not look at them. Treat it as where to START.
!
!   Rotationally symmetric and sequential. Either conjugate.
! ---------------------------------------------------------------------------------
! CHECKING IT
!
!   Against abcalc --asphere-placement, which is this repository's C# and shares no
!   arithmetic with the block below. The predicted coefficients must agree to every
!   digit either prints.
!
!   That comparison is worth more than it looks. The C# works in Welford's Seidel
!   convention and this macro in Buchdahl's, and the two differ in SIGN and in SCALE -
!   on a Cooke triplet the same spherical aberration reads +6.9600E-03 there and
!   -3.4800E-02 here. The predicted coefficient is nevertheless identical in both,
!   because it is a RATIO, -total/derivative, and any common convention factor cancels
!   in it. So agreement is a real check on the arithmetic rather than on whether two
!   conventions were matched up correctly.
!
!   The third-order sums this macro prints can be checked in turn against
!   Analyze > Aberrations > Seidel Coefficients, converted to transverse measure, and
!   against BUCH7.ZPL in this same folder, which computes them by the same scheme.
! ---------------------------------------------------------------------------------

paraxmode = PMOD()
PARAXIAL ON
eps = 1E-10
wl = PWAV()

ns = NSUR()
nsm = ns - 1
IF (nsm < 2)
   PRINT "ASPHWHERE needs at least two optical surfaces."
   GOTO 999
ENDIF

! Surface types. Declined by name rather than by reading PARM off something that is
! not an aspheric coefficient.
badtyp = 0
FOR i = 1, nsm, 1
   gdum = SPRO(i, 0)
   gtyp$ = $BUFFER()
   gok = 0
   IF (SCOM(gtyp$, "STANDARD") == 0) THEN gok = 1
   IF (SCOM(gtyp$, "EVENASPH") == 0) THEN gok = 1
   IF (gok == 0)
      badtyp = 1
      FORMAT 5.0
      PRINT "Surface ", i, " is of type ", gtyp$
   ENDIF
NEXT
IF (badtyp == 1)
   PRINT " "
   PRINT "ASPHWHERE carries STANDARD and EVENASPH surfaces. On any other type the"
   PRINT "parameter columns mean something other than aspheric coefficients, and"
   PRINT "reading them as if they did would give a confident wrong answer rather"
   PRINT "than an error - so it declines instead."
   GOTO 999
ENDIF

GETSYSTEMDATA 1
efl = VEC1(7)
IF (ABSO(efl) < eps)
   PRINT "The system has no focal length; an afocal system is not handled."
   GOTO 999
ENDIF

SETVECSIZE 4000
DECLARE cand, DOUBLE, 2, nsm + 2, 8

! ---------------------------------------------------------------------------------
! The two paraxial rays. Both are traced once and stored, because RAYY and RAYM
! report only the ray traced last.
!
!   VEC2(i + 1)     marginal height at surface i,  Schulz's h
!   VEC2(501 + i)   marginal angle AFTER surface i, so 500 + i is the angle before
!   VEC3            the same for the chief ray,     Schulz's H
! ---------------------------------------------------------------------------------

RAYTRACE 0, 0, 0, 1, wl
FOR i = 0, nsm, 1
   VEC2(i + 1) = RAYY(i)
   VEC2(501 + i) = RAYM(i) / RAYN(i)
NEXT

RAYTRACE 0, 1, 0, 0, wl
FOR i = 0, nsm, 1
   VEC3(i + 1) = RAYY(i)
   VEC3(501 + i) = RAYM(i) / RAYN(i)
NEXT

n0 = INDX(0)
IF (ISMS(0)) THEN n0 = -n0
lag = n0 * (VEC3(501) * VEC2(2) - VEC2(501) * VEC3(2))
IF (ABSO(lag) < eps)
   PRINT "The Lagrange invariant is zero. Is the field zero, or the aperture?"
   GOTO 999
ENDIF

! ---------------------------------------------------------------------------------
! The third order per surface, and the sensitivity of each sum to r^4 figuring there.
!
! The third-order block is BUCH7's stage A, which is checked against OpticStudio's
! own Seidel analysis. The aspheric increment is BUCH7_ASPH's, Buchdahl Sec. 77:
! with deln = n - n' and c1 = 8 A4, the figuring adds
!
!     dB = deln c1 h^4,  dF = dB (H/h),  dC = dB (H/h)^2,  dE = dB (H/h)^3
!
! and nothing to Petzval. BOTH BLOCKS ARE TAKEN FROM THE SAME CONVENTION ON PURPOSE.
! The predicted coefficient is -total/derivative, so a convention factor cancels in
! it - but only if the total and the derivative carry the SAME one.
!
!   cand(i, 1) h       cand(i, 2) H        cand(i, 3) H/h
!   cand(i, 4) dB/dA4  cand(i, 5) dF/dA4   cand(i, 6) dC/dA4   cand(i, 7) dE/dA4
!   cand(i, 8) semi-aperture the sag is judged at
! ---------------------------------------------------------------------------------

sumb = 0
sumf = 0
sumc = 0
sump = 0
sume = 0

FOR i = 1, nsm, 1

   cv = CURV(i)
   nb = INDX(i - 1)
   na = INDX(i)
   IF (ISMS(i - 1)) THEN nb = -nb
   IF (ISMS(i)) THEN na = -na
   IF (ABSO(na) < eps) THEN na = 1
   kk = nb / na
   km1 = kk - 1
   deln = nb - na

   py = VEC2(i + 1)
   pu = VEC2(500 + i)
   pup = VEC2(501 + i)
   pyc = VEC3(i + 1)
   puc = VEC3(500 + i)
   pucp = VEC3(501 + i)

   pai = cv * py + pu
   paic = cv * pyc + puc

   Pz = cv * km1 * lag * lag / nb
   sfm = nb * km1 * py * (pai + pup)
   sfc = nb * km1 * pyc * (paic + pucp)

   Ba = sfm * pai * pai
   Fa = sfm * pai * paic
   Ca = sfm * paic * paic
   Fb = sfc * pai * paic
   Ea = Fb + lag * km1 * paic * (pucp + puc)

   ! The figuring already on the surface belongs in the sums, or the screen would be
   ! advising against a design that is not the one in the file.
   conic = CONI(i)
   aterm = PARM(2, i)
   cv3 = cv * cv * cv
   c1 = 8 * aterm + conic * cv3
   IF (ABSO(c1) > 0)
      c1b = deln * c1
      Ba = Ba + c1b * py * py * py * py
      Fa = Fa + c1b * py * py * py * pyc
      Ca = Ca + c1b * py * py * pyc * pyc
      Ea = Ea + c1b * py * pyc * pyc * pyc
   ENDIF

   sumb = sumb + Ba
   sumf = sumf + Fa
   sumc = sumc + Ca
   sump = sump + Pz
   sume = sume + Ea

   ! The sensitivity to one more unit of A4. Exact, because the contribution above is
   ! linear in it.
   d1 = 8 * deln * py * py * py * py
   rat = 0
   atim = 0
   IF (ABSO(py) < 1E-12)
      atim = 1
   ELSE
      rat = pyc / py
   ENDIF

   cand(i, 1) = py
   cand(i, 2) = pyc
   cand(i, 3) = rat
   cand(i, 4) = d1
   cand(i, 5) = d1 * rat
   cand(i, 6) = d1 * rat * rat
   cand(i, 7) = d1 * rat * rat * rat
   IF (atim == 1)
      cand(i, 5) = 0
      cand(i, 6) = 0
      cand(i, 7) = 0
   ENDIF

   ! The radius the sag is judged at. A stated semi-diameter is authoritative;
   ! otherwise the beam needs the marginal ray plus the chief ray at full field.
   sd = SDIA(i)
   IF (ABSO(sd) < eps) THEN sd = ABSO(py) + ABSO(pyc)
   cand(i, 8) = sd
NEXT

! ---------------------------------------------------------------------------------
! Output.
! ---------------------------------------------------------------------------------

PRINT "ASPHWHERE - where an asphere would act, and what it would buy"
PRINT " "
FORMAT 12.6
PRINT "Wavelength number      ", wl
PRINT "Effective focal length ", efl
PRINT " "
PRINT "Third order, system totals, unconverted - Buchdahl's convention, the same as"
PRINT "BUCH7 prints. Check them against Analyze > Aberrations > Seidel Coefficients."
FORMAT 15.6 EXP
PRINT "   B  ", sumb, "   F  ", sumf, "   C  ", sumc
PRINT "   Pi ", sump, "   E  ", sume
PRINT " "
PRINT "H/h decides the whole pattern: figuring contributes to the four sums as"
PRINT "1, H/h, (H/h)^2 and (H/h)^3 of one another. Small: spherical and little else."
PRINT "Large: distortion. In between: all four together."
PRINT " "
PRINT " Surf            h            H          H/h   reaches"

FOR i = 1, nsm, 1
   ! A named band rather than a normalised profile. A profile was printed here and was
   ! useless: with |H/h| < 1 the first entry is 1.000 on every surface of every design,
   ! because B is always the largest, and the other three are only the powers of the
   ! ratio in the column to its left.
   big = ABSO(cand(i, 4))
   IF (ABSO(cand(i, 5)) > big) THEN big = ABSO(cand(i, 5))
   IF (ABSO(cand(i, 6)) > big) THEN big = ABSO(cand(i, 6))
   IF (ABSO(cand(i, 7)) > big) THEN big = ABSO(cand(i, 7))

   FORMAT 5.0 INT
   PRINT "  ", i,
   FORMAT 13.4
   PRINT cand(i, 1), cand(i, 2), cand(i, 3),
   IF (big < 1E-30)
      PRINT "   nothing - no index step, or h = 0"
   ELSE
      ar = ABSO(cand(i, 3))
      IF (ar < 0.15) THEN PRINT "   spherical, essentially alone"
      IF (ar >= 0.15) & (ar < 0.5) THEN PRINT "   spherical mainly, some coma"
      IF (ar >= 0.5) & (ar < 2.0) THEN PRINT "   all four, comparably"
      IF (ar >= 2.0) THEN PRINT "   distortion mainly"
   ENDIF
NEXT

PRINT " "
PRINT "The four sensitivities stand as 1 : H/h : (H/h)^2 : (H/h)^3, so H/h alone fixes"
PRINT "what a surface can reach and the bands above are a reading of it rather than a"
PRINT "second measurement. They say how a surface DIVIDES its effect, never how much it"
PRINT "has, and they are NOT ranked against what this design suffers from."
PRINT " "
PRINT "The figuring that would null each sum on its own, AS SAG at the surface's"
PRINT "semi-aperture, in lens units. A row is FOUR ALTERNATIVES, not four things at"
PRINT "once: one surface, one figure, and you choose which sum to kill."
PRINT " "
PRINT "The semi-aperture is SDIA, OpticStudio's own, which knows the real bundle. The C#"
PRINT "has no ray tracer here and falls back to |h| + |H| where a file states no"
PRINT "semi-diameter, which is a little larger - 9.08 against 8.71 on a double Gauss. So"
PRINT "the SAG column can differ by a few per cent between the two. The A4 column cannot:"
PRINT "it has no aperture in it. Nor can the ranking below, which is by outcome."
PRINT " "
PRINT " Surf      semi-ap   SAG null B   SAG null F   SAG null C   SAG null E"

FOR i = 1, nsm, 1
   sd = cand(i, 8)
   r4 = sd * sd * sd * sd
   FORMAT 5.0 INT
   PRINT "  ", i,
   FORMAT 13.4
   PRINT sd,
   FOR j = 4, 7, 1
      tot = sumb
      IF (j == 5) THEN tot = sumf
      IF (j == 6) THEN tot = sumc
      IF (j == 7) THEN tot = sume
      d = cand(i, j)
      ok = 0
      sag = 0
      IF (ABSO(d) > 1E-30)
         sag = -tot * r4 / d
         IF (ABSO(sag) <= sd) THEN ok = 1
      ENDIF
      IF (ok == 1)
         FORMAT 13.4 EXP
         PRINT sag,
      ELSE
         PRINT " out of reach",
      ENDIF
   NEXT
   PRINT " "
NEXT

PRINT " "
PRINT "OUT OF REACH means the figure needed would be deeper than the surface is wide -"
PRINT "not a correction to that surface but a different one. It is what a surface AT"
PRINT "THE STOP returns for coma, astigmatism and distortion: its chief-ray height is"
PRINT "zero, so its leverage on those three is zero, and the coefficient that would"
PRINT "null them is unbounded. The test is on the lens, not on the arithmetic."
PRINT " "
PRINT "THE SAME FIGURES AS r^4 COEFFICIENTS - the number to type into PARM 2. The"
PRINT "table above is the same figuring expressed as depth of glass; this one is what"
PRINT "A4 must be set to. Nothing above is an A4 and nothing here is a sag."
PRINT " "
PRINT " Surf       A4 null B       A4 null F       A4 null C       A4 null E"

FOR i = 1, nsm, 1
   sd = cand(i, 8)
   r4 = sd * sd * sd * sd
   FORMAT 5.0 INT
   PRINT "  ", i,
   FOR j = 4, 7, 1
      tot = sumb
      IF (j == 5) THEN tot = sumf
      IF (j == 6) THEN tot = sumc
      IF (j == 7) THEN tot = sume
      d = cand(i, j)
      ok = 0
      a4 = 0
      IF (ABSO(d) > 1E-30)
         a4 = -tot / d
         IF (ABSO(a4 * r4) <= sd) THEN ok = 1
      ENDIF
      IF (ok == 1)
         FORMAT 16.9 EXP
         PRINT a4,
      ELSE
         PRINT "               -",
      ENDIF
   NEXT
   PRINT " "
NEXT

! ---------------------------------------------------------------------------------
! What each choice buys. Everything above is the COST of a figure; this is the
! benefit, and without it a reader cannot tell a large aberration knocked to zero
! from a small one, nor see the collateral.
! ---------------------------------------------------------------------------------

PRINT " "
PRINT "WHAT EACH CHOICE BUYS. Each row nulls its target from whichever surface leaves"
PRINT "the design in the best state overall - NOT the cheapest one - and shows that"
PRINT "state, so a large aberration knocked to zero is visible as such, and so is the"
PRINT "collateral: the other three move too and a figure aimed at one sum can cancel a"
PRINT "second for nothing or wreck it. Exact, not linearised."
PRINT " "
PRINT " choice   surf            B            F            C            E         size  worst"

sz = SQRT(sumb * sumb + sumf * sumf + sumc * sumc + sume * sume)
FORMAT 13.4 EXP
PRINT " as it is    -", sumb, sumf, sumc, sume, sz
before = sz
bestsz = sz
bestj = 0
bests = 0

FOR j = 4, 7, 1
   tot = sumb
   IF (j == 5) THEN tot = sumf
   IF (j == 6) THEN tot = sumc
   IF (j == 7) THEN tot = sume

   ! BY OUTCOME, NOT BY COST. The first version of this picked the surface needing the
   ! least figuring, which is the wrong question for a row whose subject is what the
   ! figure BUYS. On a double Gauss the coma sags are within three per cent across the
   ! whole lens while the outcomes differ by a factor of two, so the cheapest surface
   ! was chosen on a near-tie and could be the worst available. Ranking by outcome also
   ! frees the recommendation from how the semi-aperture is defined, which the cost
   ! ranking depended on.
   !
   ! chp is kept alongside so the cheaper surface can be named when it is a different
   ! one - a slightly deeper figure can be worth a much better lens, and sometimes not.
   pick = 0
   bof = 1E30
   chp = 0
   least = 1E30
   FOR i = 1, nsm, 1
      sd = cand(i, 8)
      r4 = sd * sd * sd * sd
      d = cand(i, j)
      IF (ABSO(d) > 1E-30)
         sag = -tot * r4 / d
         IF (ABSO(sag) <= sd)
            a4t = -tot / d
            t1 = sumb + a4t * cand(i, 4)
            t2 = sumf + a4t * cand(i, 5)
            t3 = sumc + a4t * cand(i, 6)
            t5 = sume + a4t * cand(i, 7)
            oz = SQRT(t1 * t1 + t2 * t2 + t3 * t3 + t5 * t5)
            IF (oz < bof)
               bof = oz
               pick = i
            ENDIF
            IF (ABSO(sag) < least)
               least = ABSO(sag)
               chp = i
            ENDIF
         ENDIF
      ENDIF
   NEXT

   IF (pick == 0)
      IF (j == 4) THEN PRINT " null B     -   out of reach on every surface"
      IF (j == 5) THEN PRINT " null F     -   out of reach on every surface"
      IF (j == 6) THEN PRINT " null C     -   out of reach on every surface"
      IF (j == 7) THEN PRINT " null E     -   out of reach on every surface"
   ELSE
      a4 = -tot / cand(pick, j)
      nb2 = sumb + a4 * cand(pick, 4)
      nf2 = sumf + a4 * cand(pick, 5)
      nc2 = sumc + a4 * cand(pick, 6)
      ne2 = sume + a4 * cand(pick, 7)
      sz = SQRT(nb2 * nb2 + nf2 * nf2 + nc2 * nc2 + ne2 * ne2)

      ! The worst of the three sums that were NOT targeted, as a multiple of what it
      ! was. Without it the collateral is in the columns and has to be worked out by
      ! dividing - exactly the arithmetic a reader should not have to do to see that a
      ! figure has wrecked something.
      fac = 1
      wrs$ = "none"
      IF (j != 4) & (ABSO(sumb) > 1E-15)
         t = ABSO(nb2) / ABSO(sumb)
         IF (t > fac) THEN fac = t
         IF (t >= fac) THEN wrs$ = "B"
      ENDIF
      IF (j != 5) & (ABSO(sumf) > 1E-15)
         t = ABSO(nf2) / ABSO(sumf)
         IF (t > fac) THEN fac = t
         IF (t >= fac) THEN wrs$ = "F"
      ENDIF
      IF (j != 6) & (ABSO(sumc) > 1E-15)
         t = ABSO(nc2) / ABSO(sumc)
         IF (t > fac) THEN fac = t
         IF (t >= fac) THEN wrs$ = "C"
      ENDIF
      IF (j != 7) & (ABSO(sume) > 1E-15)
         t = ABSO(ne2) / ABSO(sume)
         IF (t > fac) THEN fac = t
         IF (t >= fac) THEN wrs$ = "E"
      ENDIF

      FORMAT 5.0 INT
      IF (j == 4) THEN PRINT " null B ",
      IF (j == 5) THEN PRINT " null F ",
      IF (j == 6) THEN PRINT " null C ",
      IF (j == 7) THEN PRINT " null E ",
      PRINT pick,
      FORMAT 13.4 EXP
      PRINT nb2, nf2, nc2, ne2, sz,
      IF (fac <= 1.000001)
         PRINT "  none"
      ELSE
         FORMAT 6.1
         IF (fac >= 3) THEN PRINT "  ", wrs$, " x", fac, "  <- WRECKED"
         IF (fac < 3) THEN PRINT "  ", wrs$, " x", fac
      ENDIF

      ! Name the cheaper surface where it is a different one, with both outcomes, so
      ! the trade is visible rather than decided silently here.
      IF (chp != 0) & (chp != pick)
         sdc = cand(chp, 8)
         a4c = -tot / cand(chp, j)
         u1 = sumb + a4c * cand(chp, 4)
         u2 = sumf + a4c * cand(chp, 5)
         u3 = sumc + a4c * cand(chp, 6)
         u5 = sume + a4c * cand(chp, 7)
         uz = SQRT(u1 * u1 + u2 * u2 + u3 * u3 + u5 * u5)
         FORMAT 5.0 INT
         PRINT "          cheaper on surface ", chp,
         FORMAT 12.4 EXP
         PRINT " - less glass, but leaves ", uz, " against ", bof
      ENDIF

      IF (sz < bestsz)
         bestsz = sz
         bestj = j
         bests = pick
      ENDIF
   ENDIF
NEXT

PRINT " "
IF (bestj == 0)
   PRINT "NO SINGLE FIGURE IMPROVES THE THIRD ORDER OVERALL on this design. Every null"
   PRINT "costs more elsewhere than it gains, which is what a design already balanced"
   PRINT "at third order looks like from here."
ELSE
   FORMAT 5.0 INT
   IF (bestj == 4) THEN PRINT "BEST SINGLE FIGURE by that measure: null B on surface ", bests
   IF (bestj == 5) THEN PRINT "BEST SINGLE FIGURE by that measure: null F on surface ", bests
   IF (bestj == 6) THEN PRINT "BEST SINGLE FIGURE by that measure: null C on surface ", bests
   IF (bestj == 7) THEN PRINT "BEST SINGLE FIGURE by that measure: null E on surface ", bests
   FORMAT 13.4 EXP
   PRINT "taking the four from ", before, " to ", bestsz
ENDIF

PRINT " "
PRINT "'size' weights the four equally and is NOT a spot size - it is there to rank"
PRINT "rows that would otherwise be compared four numbers at a time. Petzval is left"
PRINT "out of it because no figure moves it, so it would add the same constant to"
PRINT "every row."
PRINT " "
PRINT "Cheapest means DEPTH OF GLASS TO REMOVE, a manufacturing cost and not an"
PRINT "optical one. A surface far from the stop has a wide footprint, so the whole of"
PRINT "it must be figured to the quoted depth even though only the marginal-ray zone"
PRINT "does the work on spherical aberration. That mildly favours surfaces near the"
PRINT "stop here for a geometric reason rather than an optical one."
PRINT " "
PRINT "AND THIS IS THE THIRD ORDER. A figure sized here will move the fifth and"
PRINT "seventh orders, which this macro does not look at. Treat it as where to START."

LABEL 999
IF (ABSO(paraxmode) < eps) THEN PARAXIAL OFF
END
