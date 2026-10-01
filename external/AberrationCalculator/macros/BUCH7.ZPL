! BUCH7 - third-, fifth- and seventh-order aberration coefficients of a centred
! system of SPHERICAL surfaces, reported per surface and split into the part each
! surface generates on its own and the part it generates by acting on the aberration
! already present when light reaches it.
!
! Written by Javier Ruiz with Claude Code, September 2026.
!
! Buchdahl, "Optical Aberration Coefficients", Dover 1968. Sections 31, 203 and 218
! carry the third- and fifth-order material; the seventh order is the tertiary of
! Chapter VI.
!
! WHAT THIS ADDS TO FIFTHORD, WHICH IS ALREADY IN THIS FOLDER
!
!   FIFTHORD gives the third and fifth orders plus seventh-order spherical aberration,
!   as system totals. This gives the whole seventh order, and gives every order PER
!   SURFACE with the intrinsic and induced parts separated.
!
!   The separation is the point. A surface can be blameless on its own and still be a
!   large contributor because of what it does to the aberration handed to it, and the
!   cure for that is upstream, not at the surface the total blames. On a Cooke triplet
!   the induced part is the larger share on four surfaces of six, and on one of them it
!   is more than a thousand times the intrinsic.
!
! LIMITS, STATED PLAINLY
!
!   TWO DIFFERENT KINDS OF SPHERICAL-ONLY LIVE IN THIS FILE NOW, and they are worth
!   keeping apart, because one of them has a way out and the other does not.
!
!   Orders three, five and seven are spherical only IN THIS MACRO. A conic or an
!   even-asphere term makes it stop with an explanation rather than return a number
!   that looks like an answer. Buchdahl gives the aspheric scheme but never published
!   the arranged table for it, so this file does not guess at one. BUCH7_ASPH.ZPL,
!   beside this one, IS that arrangement reconstructed, and it carries conics and even
!   aspheres; it agrees with FORBES.ZPL on all twenty seventh-order coefficients on
!   every figured refracting design tried. This macro is left as the spherical routine, checked
!   against Buchdahl's own printed numbers. So: want the seventh order on a figured
!   design, run BUCH7_ASPH.
!
!   STAGE F, THE NINTH ORDER, IS SPHERICAL ONLY IN THE SUBJECT. There is no aspheric
!   arrangement at this order anywhere - Buchdahl published none, paper IV reaches
!   quaternary spherical and stops, and unlike the tertiary one nobody has reconstructed
!   it. BUCH7_ASPH therefore has no stage F and will not grow one by transcription;
!   somebody would have to derive the arrangement first. That is not a gap in these
!   macros, it is where the published subject ends.
!
!   The consequence for a reader: on a figured design there IS a seventh order to be
!   had, from the macro next door, and there is NO ninth order to be had at all.
!
!   Rotationally symmetric and sequential. Either conjugate: the object position
!   enters only through M (13.4)'s four starting values, and is carried.
!
! HOW TO CHECK IT, AND PLEASE DO
!
!   The third order must agree with Analyze > Aberrations > Seidel Coefficients, and
!   the fifth with FIFTHORD on the same file. Both checks are available without leaving
!   the program and neither requires trusting this macro.
!
! TWO TRAPS IN ZPL, BOTH OF WHICH BIT THIS FILE
!
!   VARIABLE NAMES ARE CASE-INSENSITIVE. In the first working draft the running total
!   sB and the chief-ray surface factor Sb were the same variable, so the accumulator
!   was wiped on every pass and the printed total for B was the last surface Sb plus
!   its Ba. Every per-surface number was right, which is what made it easy to miss.
!   Keep every name distinct without relying on case, and check the totals against
!   the sum of the column printed above them.
!
!   THERE IS NO LINE CONTINUATION. A line beginning with + is a new statement, not a
!   continuation of the one above, and the parser says so with a message that names
!   the wrong thing. The long expressions below are either on one line or accumulate
!   into their own target.
!
! ---------------------------------------------------------------------------------
! SIX STAGES: A the paraxial basis and the third order, B the fifth order with its
! intrinsic and induced parts separated, C Buchdahl's Table I entry by entry, D the
! twenty tau of the seventh order with the same split, E the totals published to the
! call buffer for ROBB.ZPL, F the ninth-order spherical (q9 = 1). Seventh-order spherical
! aberration appears in stage B as well as stage D, because it falls out of the block
! the fifth order does; the two routes agree, which is a check the reader can make
! without leaving the listing. Each stage was checkable against something that
! already existed before the next was written.
! ---------------------------------------------------------------------------------

paraxmode = PMOD()
PARAXIAL ON
eps = 1E-10

! Set to 1 to add stage F, the coefficient of QUATERNARY - ninth-order - spherical
! aberration, by Buchdahl paper IV (J. Opt. Soc. Am. 48, 757, 1958). Off by default
! because it is a fifteenth column on a report that is already wide, and because
! almost nobody needs it: everything else here stops at the seventh order and that
! is enough for most designs. It is worth switching on when a predicted spot and a
! traced one part company ON AXIS, because the residual there has otherwise to be
! ATTRIBUTED to the ninth order rather than measured.
!
! It costs nothing to leave on. Paper IV builds the quaternary as an appendix to the
! tertiary scheme, so every quantity the fourteen rows need is an entry of Table I
! that stages A to D have already computed - fourteen more entries per surface on a
! table of two hundred and forty.
!
! SPHERICAL SURFACES ONLY, which is this macro's own limit anyway: a conic or an
! even-asphere term stops BUCH7 before it reaches here. Buchdahl published no
! aspheric arrangement at the ninth order, as he published none at the seventh.
q9 = 0
wl = PWAV()

ns = NSUR()
nsm = ns - 1
IF (nsm < 2)
   PRINT "BUCH7 needs at least two surfaces before the image (a dummy plane will do)."
   GOTO 999
ENDIF

! Refuse rather than mislead: this arrangement is the spherical one.
asph = 0
FOR i = 1, nsm, 1
   IF (ABSO(CONI(i)) > eps) THEN asph = 1
   FOR j = 1, 8, 1
      IF (ABSO(PARM(j, i)) > eps) THEN asph = 1
   NEXT
NEXT
IF (asph == 1)
   PRINT "BUCH7 handles spherical surfaces only, and this system carries a conic or"
   PRINT "an aspheric term. Buchdahl gives the aspheric scheme but never published the"
   PRINT "arranged table for it, so this macro declines rather than guess."
   PRINT "BUCH7_ASPH.ZPL, in this same folder, is that arrangement reconstructed and"
   PRINT "carries conics and even aspheres. Run it on this file instead."
   GOTO 999
ENDIF

! System data. This fills VEC1, so it is read out before VEC1 is wanted for anything.
GETSYSTEMDATA 1
efl = VEC1(7)
epp = VEC1(12)
IF (ABSO(efl) < eps)
   PRINT "The system has no focal length; an afocal system is not handled."
   GOTO 999
ENDIF

SETVECSIZE 4000

! ---------------------------------------------------------------------------------
! The paraxial rays. Both are traced once and stored, because RAYY and RAYM report
! only the ray traced last.
!
!   VEC2(i + 1)     marginal height at surface i
!   VEC2(501 + i)   marginal angle in the space AFTER surface i, so 500 + i is the
!                   angle in the space BEFORE surface i
!   VEC3            the same for the chief ray
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

! The Lagrange invariant, formed in the space before the first surface.
n0 = INDX(0)
IF (ISMS(0)) THEN n0 = -n0
lag = n0 * (VEC3(501) * VEC2(2) - VEC2(501) * VEC3(2))

! The object medium's index, magnitude only. The tertiary scheme needs it twice, in
! stage C: Buchdahl's p and q rays must carry a Lagrange invariant of ONE, and theirs
! comes out as exactly this number. See the note at the ray start below. The sign is
! dropped because it belongs to the mirror count, not to the medium.
n0abs = ABSO(n0)
IF (n0abs < eps) THEN n0abs = 1
IF (ABSO(lag) < eps)
   PRINT "The Lagrange invariant is zero. Is the field zero, or the aperture?"
   GOTO 999
ENDIF

! Buchdahl's p, the entrance pupil in focal lengths. The third order does not use it,
! but the higher orders are built on it, so it is reported and cross-checked here
! where that is cheap: once from the system data and once from the chief ray, which
! rest on different sign conventions.
p0 = epp / efl
uc0 = VEC3(501)
IF (ABSO(uc0) > eps)
   p0chief = (VEC3(2) * (-1 / uc0)) / efl
   IF (ABSO(p0chief - p0) > 1E-6 * (1 + ABSO(p0)))
      PRINT "WARNING: the entrance pupil position and the chief ray disagree about"
      PRINT "Buchdahl's p - system data ", p0
      PRINT "                chief ray ", p0chief
      PRINT "The higher orders are built on it, so treat them as suspect."
   ENDIF
ENDIF

! The F/number, from the marginal ray in image space. The third and fifth orders are
! reported per surface UNCONVERTED - as the recursion produces them - and this is the
! one factor that turns any of them into a transverse aberration in lens units. It is
! applied to the totals, so the per-surface columns times this number sum to the
! totals row printed under them.
pupf = VEC2(501 + nsm)
! The image-space index's MAGNITUDE, here and in the seventh order's conversion below.
! After an odd number of mirrors the signed index is negative, and dividing by it would
! negate every total this macro prints - self-consistently, but in the opposite frame
! from FIFTHORD, BUCH7_ASPH and the C# program, which all take the magnitude. Measured on
! the parabola in C#: with the magnitude, the third, fifth and seventh orders all agree
! with reflected real rays in one frame. (September 2026; this used the signed index.)
nlast = ABSO(INDX(nsm))
fnum = -0.5 / nlast / pupf

! M (13.4)'s iota: 1/l_01 in focal lengths, and the ONLY quantity in the scheme that
! knows where the object is. Paper III says exactly that - "the choice of different
! coordinate systems reflects itself only in the starting values of y_p, v_p, y_q, v_q
! (cf. M Secs. 12-13)" - and then sends the reader to the monograph for what they are.
! ZPL reports an infinite object thickness as a very large number, so it is tested by
! size rather than compared with an infinity.
objd = THIC(0)
finite = 0
iota = 0
IF (ABSO(objd) < 1E10)
   finite = 1
   iota = -efl / objd
ENDIF
goe = 1 - p0 * iota

FORMAT 12.6
PRINT "BUCH7 - Buchdahl coefficients, spherical surfaces"
PRINT "Wavelength number      ", wl
PRINT "Effective focal length ", efl
PRINT "F/number               ", fnum
PRINT "Lagrange invariant     ", lag
PRINT "Buchdahl p             ", p0
IF (finite == 1)
   PRINT "Object distance        ", objd
   PRINT "Buchdahl iota          ", iota
ELSE
   PRINT "Object                  at infinity"
ENDIF
PRINT " "

! ---------------------------------------------------------------------------------
! The third order, per surface.
!
! Nothing is induced at this order - the aberration is additive over surfaces - so
! intrinsic and total are the same thing here, and this is the stage that checks
! against the Seidel analysis.
!
! With i the paraxial incidence c y + u on each ray, and sfm, sfc the surface factor
! n (k - 1) y (i + u'), the three aperture-bearing coefficients are S carried through
! the incidences: B takes the marginal incidence twice, F one of each, C the chief
! twice. Petzval carries neither and stands outside that chain. Distortion needs the
! chief ray's own surface factor as well.
! ---------------------------------------------------------------------------------

PRINT "Third order, per surface, unconverted - multiply by the F/number above to"
PRINT "get a transverse aberration in lens units, which is done for the totals."
PRINT " "
PRINT " Surf              B              F              C             Pi              E"

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

   ! The b-side third order. These carry the chief ray where the a-side carries the
   ! marginal, and the fifth-order induced terms are built on both.
   Bb = sfc * paic * paic
   Cb = sfc * pai * pai
   Eb = Fa - lag * km1 * pai * (pup + pu)

   b = (i - 1) * 160
   VEC4(b + 1) = Ba
   VEC4(b + 2) = Fa
   VEC4(b + 3) = Ca
   VEC4(b + 4) = Pz
   VEC4(b + 5) = Ea
   VEC4(b + 6) = Bb
   VEC4(b + 7) = Fb
   VEC4(b + 8) = Cb
   VEC4(b + 9) = Eb

   ! -----------------------------------------------------------------------------
   ! Fifth order, intrinsic: what this surface would generate on a perfect beam.
   !
   ! The six S quantities are Buchdahl's, and each fifth-order coefficient is one of
   ! them carried through an incidence, exactly as the third order was carried through
   ! its own. t1p is the seventh-order spherical aberration of the surface, which comes
   ! out of the same working and is kept here rather than deferred to stage C.
   ! -----------------------------------------------------------------------------
   paip = pai * kk
   paicp = paic * kk

   w = (pai * pai + paip * paip + pup * pup - 3 * pu * pu) / 8
   x73 = 3 * pai * paip + 2 * pup * pup - 3 * pu * pu
   x74 = 3 * pai * paicp + 2 * pup * pucp - 3 * pu * puc
   x75 = 3 * paic * paicp + 2 * pucp * pucp - 3 * puc * puc
   x76 = pai * (3 * pu - pup)
   x77 = paic * (2 * pu - pup) + pai * puc
   x78 = paic * (3 * puc - pucp)
   x42 = pyc * pai * (paic - puc) + py * paic * (pucp + puc)
   x82 = pyc * pu * (paic - puc) - py * paicp * (pucp + puc)
   x42b = py * paic * (pai - pu) + pyc * pai * (pup + pu)
   x82b = py * puc * (pai - pu) - pyc * paip * (pup + pu)

   q1p = 3 * w * sfm * pai
   q2p = sfm * (paic * x73 + pai * x74 - pucp * x76 - pup * x77) / 4
   q3p = nb * km1 * (x42 * x73 + x76 * x82 + py * (pai + pup) * (pai * x75 - pup * x78)) / 4
   q4p = sfm * (paic * x74 - pucp * x77)
   q5p = nb * km1 * (x42 * x74 + x77 * x82 + py * (pai + pup) * (paic * x75 - pucp * x78)) / 4
   q6p = nb * km1 * (x42 * x75 + x78 * x82) / 4
   q1q = nb * km1 * (x42b * x73 + x76 * x82b) / 4
   t1p = 10 * w * w + sfm * pai * cv * (2 * pup - 5 * pu) / nb / 8

   iB5 = pai * q1p
   iF1 = paic * q1p + pai * q2p
   iF2 = pai * q2p
   iM1 = 2 * paic * q2p
   iM2 = pai * q3p
   iM3 = pai * q4p
   iN1 = paic * q3p
   iN2 = paic * q4p + 2 * pai * q5p
   iN3 = pai * q5p
   iC5 = 0.5 * paic * q5p
   iP5 = pai * q6p - 0.5 * paic * q5p
   iE5 = paic * q6p
   iE5b = pai * q1q
   iB7 = Ba * t1p

   VEC4(b + 10) = iB5
   VEC4(b + 11) = iF1
   VEC4(b + 12) = iF2
   VEC4(b + 13) = iM1
   VEC4(b + 14) = iM2
   VEC4(b + 15) = iM3
   VEC4(b + 16) = iN1
   VEC4(b + 17) = iN2
   VEC4(b + 18) = iN3
   VEC4(b + 19) = iC5
   VEC4(b + 20) = iP5
   VEC4(b + 21) = iE5
   VEC4(b + 22) = iB7
   VEC4(b + 23) = iE5b

   sumb = sumb + Ba
   sumf = sumf + Fa
   sumc = sumc + Ca
   sump = sump + Pz
   sume = sume + Ea

   FORMAT 5.0 INT
   PRINT i,
   FORMAT 15.6 EXP
   PRINT Ba, Fa, Ca, Pz, Ea
NEXT

PRINT " "
FORMAT 5.0 INT
PRINT "  SUM",
FORMAT 15.6 EXP
PRINT sumb, sumf, sumc, sump, sume

! ---------------------------------------------------------------------------------
! Fifth order: the induced part, and the totals.
!
! A surface induces aberration by acting on what is already there, so each correction
! is a product of this surface's third order with the RUNNING SUMS over the surfaces
! before it. The sums are advanced at the bottom of the loop, after the corrections
! that use them, which is what keeps "before it" honest.
!
! Seventh-order spherical aberration is included: it comes out of the same working,
! and it is the one seventh-order coefficient FIFTHORD also reports, so it is the one
! that can be checked before stage C exists.
! ---------------------------------------------------------------------------------

accb = 0
accf = 0
accc = 0
acce = 0
accpi = 0
accbb = 0
accfb = 0
acccb = 0
acceb = 0
accb5 = 0
acce5b = 0

FOR i = 1, nsm, 1
   b = (i - 1) * 160

   Ba = VEC4(b + 1)
   Fa = VEC4(b + 2)
   Ca = VEC4(b + 3)
   Pz = VEC4(b + 4)
   Ea = VEC4(b + 5)
   Bb = VEC4(b + 6)
   Fb = VEC4(b + 7)
   Cb = VEC4(b + 8)
   Eb = VEC4(b + 9)

   iB5 = VEC4(b + 10)
   iF1 = VEC4(b + 11)
   iF2 = VEC4(b + 12)
   iM1 = VEC4(b + 13)
   iM2 = VEC4(b + 14)
   iM3 = VEC4(b + 15)
   iN1 = VEC4(b + 16)
   iN2 = VEC4(b + 17)
   iN3 = VEC4(b + 18)
   iC5 = VEC4(b + 19)
   iP5 = VEC4(b + 20)
   iE5 = VEC4(b + 21)
   iB7 = VEC4(b + 22)
   iE5b = VEC4(b + 23)

   ! Seventh-order spherical first, because it is built on the fifth order BEFORE the
   ! fifth order takes its own induced part.
   z = 0.5 * accb * accb * (Pz + 3 * Ca) / lag
   z = z + 3 * (accb5 - accb * acceb / lag) * Fa
   z = z + 3 * (0.5 * acceb * acceb / lag - acce5b) * Ba
   z = (z + accb * (iF1 + iF2) - 5 * acceb * iB5) / (2 * lag)
   tB7 = iB7 + z

   tB5 = iB5 + 1.5 * (accb * Fa - acceb * Ba) / lag

   z = accb * (Pz + 4 * Ca) + (5 * accf - 4 * acceb) * Fa
   z = z - (2 * accpi + 5 * acccb) * Ba
   tF1 = iF1 + z / (2 * lag)

   z = accb * (Pz + 2 * Ca) + 2 * (2 * accf - acceb) * Fa
   z = z - (accpi + 4 * acccb) * Ba
   tF2 = iF2 + z / (2 * lag)

   z = accb * Ea + (4 * accf - acceb) * Ca - accfb * Ba
   z = z + (accc - 4 * acccb - 2 * accpi) * Fa
   tM1 = iM1 + z / lag

   z = accb * Ea + (2 * accf - acceb) * (Pz + Ca)
   z = z + (3 * accc - 2 * acccb + accpi) * Fa - 3 * accfb * Ba
   tM2 = iM2 + z / (2 * lag)

   z = accf * (Pz + 2 * Ca) - accfb * Ba + (accc - 2 * acccb) * Fa
   tM3 = iM3 + 2 * z / lag

   z = 3 * accf * Ea - (accpi + acccb) * (Pz + Ca) + 2 * (accc - acccb) * Ca
   z = z + (acce - 2 * accfb) * Fa - accbb * Ba
   tN1 = iN1 + z / (2 * lag)

   z = 3 * accf * Ea + (accpi - acccb + 3 * accc) * (Pz + 3 * Ca)
   z = z - accbb * Ba
   z = z - (accpi + accc) * Ca
   z = z + (acce - 8 * accfb) * Fa
   tN2 = iN2 + z / lag

   z = accf * Ea + (3 * accc + accpi - acccb) * (Pz + Ca) - accbb * Ba
   z = z + (accpi + accc) * Ca + (acce - 4 * accfb) * Fa
   tN3 = iN3 + z / (2 * lag)

   z = (4 * accc + accpi) * Ea - accfb * Pz + 2 * (acce - 2 * accfb) * Ca
   z = z - 2 * accbb * Fa
   tC5 = iC5 + z / (4 * lag)

   z = (accpi - 2 * accc) * Ea + (4 * acce - accfb) * Pz
   z = z - 2 * accbb * Fa
   z = z + 2 * (acce + accfb) * Ca
   tP5 = iP5 + z / (4 * lag)

   tE5 = iE5 + (3 * acce * Ea - accbb * (Pz + 3 * Ca)) / (2 * lag)
   tE5b = iE5b + (accb * (Pz + 3 * Cb) - 3 * acceb * Eb) / (2 * lag)

   VEC4(b + 50) = tB5
   VEC4(b + 51) = tF1
   VEC4(b + 52) = tF2
   VEC4(b + 53) = tM1
   VEC4(b + 54) = tM2
   VEC4(b + 55) = tM3
   VEC4(b + 56) = tN1
   VEC4(b + 57) = tN2
   VEC4(b + 58) = tN3
   VEC4(b + 59) = tC5
   VEC4(b + 60) = tP5
   VEC4(b + 61) = tE5
   VEC4(b + 62) = tB7

   ! Advance the running sums, now that this surface's induced part is done.
   accb = accb + Ba
   accf = accf + Fa
   accc = accc + Ca
   acce = acce + Ea
   accpi = accpi + Pz
   accbb = accbb + Bb
   accfb = accfb + Fb
   acccb = acccb + Cb
   acceb = acceb + Eb
   accb5 = accb5 + tB5
   acce5b = acce5b + tE5b
NEXT

! ---------------------------------------------------------------------------------
! Output. Three rows per surface: what the surface generates on its own, what it
! generates by acting on what reached it, and the sum of the two.
! ---------------------------------------------------------------------------------

PRINT " "
PRINT "Fifth order, per surface, unconverted - same factor as the third order."
PRINT " "
PRINT " Surf                B5             F1             F2             M1             M2             M3"

FOR i = 1, nsm, 1
   b = (i - 1) * 160
   FORMAT 5.0 INT
   PRINT i,
   FORMAT 15.6 EXP
   PRINT "  int", VEC4(b + 10), VEC4(b + 11), VEC4(b + 12), VEC4(b + 13), VEC4(b + 14), VEC4(b + 15)
   PRINT "      ind", VEC4(b + 50) - VEC4(b + 10), VEC4(b + 51) - VEC4(b + 11), VEC4(b + 52) - VEC4(b + 12), VEC4(b + 53) - VEC4(b + 13), VEC4(b + 54) - VEC4(b + 14), VEC4(b + 55) - VEC4(b + 15)
   PRINT "      tot", VEC4(b + 50), VEC4(b + 51), VEC4(b + 52), VEC4(b + 53), VEC4(b + 54), VEC4(b + 55)
NEXT

PRINT " "
PRINT " Surf                N1             N2             N3             C5            Pi5             E5"

FOR i = 1, nsm, 1
   b = (i - 1) * 160
   FORMAT 5.0 INT
   PRINT i,
   FORMAT 15.6 EXP
   PRINT "  int", VEC4(b + 16), VEC4(b + 17), VEC4(b + 18), VEC4(b + 19), VEC4(b + 20), VEC4(b + 21)
   PRINT "      ind", VEC4(b + 56) - VEC4(b + 16), VEC4(b + 57) - VEC4(b + 17), VEC4(b + 58) - VEC4(b + 18), VEC4(b + 59) - VEC4(b + 19), VEC4(b + 60) - VEC4(b + 20), VEC4(b + 61) - VEC4(b + 21)
   PRINT "      tot", VEC4(b + 56), VEC4(b + 57), VEC4(b + 58), VEC4(b + 59), VEC4(b + 60), VEC4(b + 61)
NEXT

PRINT " "
PRINT " Surf                B7      (seventh-order spherical aberration)"
FOR i = 1, nsm, 1
   b = (i - 1) * 160
   FORMAT 5.0 INT
   PRINT i,
   FORMAT 15.6 EXP
   PRINT "  int", VEC4(b + 22), "  ind", VEC4(b + 62) - VEC4(b + 22), "  tot", VEC4(b + 62)
NEXT

! System totals, in the transverse measure FIFTHORD prints, so the two can be compared
! directly rather than by eye.
sB5 = 0
sF1 = 0
sF2 = 0
sM1 = 0
sM2 = 0
sM3 = 0
sN1 = 0
sN2 = 0
sN3 = 0
sC5 = 0
sP5 = 0
sE5 = 0
sB7 = 0
FOR i = 1, nsm, 1
   b = (i - 1) * 160
   sB5 = sB5 + VEC4(b + 50)
   sF1 = sF1 + VEC4(b + 51)
   sF2 = sF2 + VEC4(b + 52)
   sM1 = sM1 + VEC4(b + 53)
   sM2 = sM2 + VEC4(b + 54)
   sM3 = sM3 + VEC4(b + 55)
   sN1 = sN1 + VEC4(b + 56)
   sN2 = sN2 + VEC4(b + 57)
   sN3 = sN3 + VEC4(b + 58)
   sC5 = sC5 + VEC4(b + 59)
   sP5 = sP5 + VEC4(b + 60)
   sE5 = sE5 + VEC4(b + 61)
   sB7 = sB7 + VEC4(b + 62)
NEXT

PRINT " "
PRINT "System totals, transverse measure - these are what FIFTHORD prints"
FORMAT 15.6 EXP
PRINT "   B  ", sumb * fnum, "   F  ", sumf * fnum, "   C  ", sumc * fnum
PRINT "   Pi ", sump * fnum, "   E  ", sume * fnum
PRINT "   B5 ", sB5 * fnum, "   F1 ", sF1 * fnum, "   F2 ", sF2 * fnum
PRINT "   M1 ", sM1 * fnum, "   M2 ", sM2 * fnum, "   M3 ", sM3 * fnum
PRINT "   N1 ", sN1 * fnum, "   N2 ", sN2 * fnum, "   N3 ", sN3 * fnum
PRINT "   C5 ", sC5 * fnum, "   Pi5", sP5 * fnum, "   E5 ", sE5 * fnum
PRINT "   B7 ", sB7 * fnum
PRINT " "
PRINT "Run FIFTHORD on this same file and these eighteen must match."


! =================================================================================
! STAGE C - the seventh order.
!
! The fifth order above is built on the marginal and chief rays as they are traced.
! The seventh order is not: Buchdahl arranges it as a numbered table, Table I, on a
! DIFFERENT basis - two rays p and q, in units of the focal length, with the p ray
! entering at unit height parallel to the axis and the q ray at height p0 with unit
! angle. So the basis is built again here rather than reused, and the entry numbers
! below are Table I's own, which is what makes the arrangement checkable against the
! printed table and against macros/reference/CookeTriplet_TableI.txt.
!
! Set dbg = 1 to print the entries and compare them with that file, one at a time.
! A single wrong entry is then a single wrong line rather than a wrong answer.
! =================================================================================

! ON while stage C is being built, because the entry dump IS the check. Set it to 0
! once stage C is finished and the coefficients themselves are what is wanted.
dbg = 1

DECLARE tt, DOUBLE, 2, nsm + 2, 250

FOR i = 1, nsm, 1
   FOR j = 1, 249, 1
      tt(i, j) = 0
   NEXT
NEXT

! ---------------------------------------------------------------------------------
! t1 to t9 - the p and q rays, and the quantities each surface carries.
!
! Buchdahl's angle is v = -u and his transfer is y(i) = y(i-1) - d v(i), the d being
! the gap BEFORE the surface. The primed angle v' = (1-k) c y + k v is what the
! transfer carries forward, so it is held from one pass to the next.
! ---------------------------------------------------------------------------------

! M (13.4), reduced OT-coordinates. At iota = 0 these are the four values this macro
! used to hard-code, and the p ray does then enter parallel to the axis; at any other
! conjugate it does not, and nothing else in the whole of stage C would notice.
!
! THE PAIR MUST CARRY A LAGRANGE INVARIANT OF ONE, and in an immersed object space it
! does not unless the q ray is divided by the object index. The invariant of these
! four values is n0 (v_q y_p - v_p y_q) = n0 (1 - p iota)/g = n0 exactly: one when
! object space is air and n0 when it is not. The scheme is not homogeneous in the q
! ray's scale - a_p divides by that ray combination while the field terms multiply by
! powers of it - so a pair whose invariant is not one cannot be absorbed by any later
! normalisation, and comes out instead as a different error in every tau: some out by
! exactly 1/n0, some by more, two of them changing SIGN at n0 = 1.3.
!
! "Reduced" is the word above and it is what was missing: in reduced coordinates the
! angle is N u, so v_q = 1 means a PLAIN angle of 1/n0. The transfer below carries
! plain angles, so the reduction has to be done here, in the starting values. At
! n0 = 1 it is a division by exactly 1.0 and nothing moves, which is why Buchdahl's
! own printed triplet - in air, like every published example - validated it for as
! long as it stood. The field variable carries the other half; see hmax in stage D.
typ = 1
tvp = iota
tyq = p0 / goe / n0abs
tvq = 1 / goe / n0abs

FOR i = 1, nsm, 1

   cvn = CURV(i) * efl
   nb = INDX(i - 1)
   na = INDX(i)
   IF (ISMS(i - 1)) THEN nb = -nb
   IF (ISMS(i)) THEN na = -na
   IF (ABSO(na) < eps) THEN na = 1
   kk = nb / na

   IF (i > 1)
      dgap = THIC(i - 1) / efl
      typ = typ - dgap * tvp
      tyq = tyq - dgap * tvq
   ENDIF

   tt(i, 1) = typ
   tt(i, 2) = tvp
   tt(i, 3) = cvn * typ - tvp
   tt(i, 4) = tyq
   tt(i, 5) = tvq

   tiq = cvn * tyq - tvq
   IF (ABSO(tt(i, 3)) > 1E-30)
      tt(i, 6) = tiq / tt(i, 3)
   ELSE
      tt(i, 6) = 0
   ENDIF
   tt(i, 7) = -tvp * tt(i, 6) + tvq
   tt(i, 8) = (kk - 1) * cvn / nb
   tt(i, 9) = tvp * tvp

   ! The primed angles. t156 and t157 are not Buchdahl's; they are scratch, kept in
   ! the same array so that the next surface can read them.
   tt(i, 156) = (1 - kk) * cvn * typ + kk * tvp
   tt(i, 157) = (1 - kk) * cvn * tyq + kk * tvq

   ! ------------------------------------------------------------------------------
   ! t10 to t14 - the intrinsic primary, in Table I's own normalisation.
   !
   ! The Lagrange invariant of this basis sits in the denominator. It is formed from
   ! the two rays at this surface and is the same at every surface; forming it here
   ! rather than once keeps the macro honest if a surface ever makes it degenerate.
   ! ------------------------------------------------------------------------------
   tlag = tt(i, 5) * tt(i, 1) - tt(i, 2) * tt(i, 4)
   IF (ABSO(tlag) > 1E-30)
      gg = 0.5 * (tt(i, 156) - tt(i, 3)) * (kk - 1) * tt(i, 1) / tlag
   ELSE
      gg = 0
   ENDIF

   tt(i, 10) = gg * tt(i, 3) * tt(i, 3)
   tt(i, 11) = gg * tt(i, 3) * tiq
   tt(i, 12) = 2 * gg * tiq * tiq
   tt(i, 13) = gg * tiq * tiq - 0.5 * tt(i, 8)

   ! t14 is the one place a division by the incidence survives, and it survives only
   ! through c/i, which is finite wherever the angle is. On a flat surface facing
   ! collimated space it is exactly 1/y, which is the limit taken here.
   IF (ABSO(tt(i, 3)) > 1E-30)
      coi = cvn / tt(i, 3)
   ELSE
      IF (ABSO(tt(i, 2)) < 1E-30)
         coi = 1 / tt(i, 1)
      ELSE
         coi = 0
      ENDIF
   ENDIF
   tiq3 = tiq * tiq * tiq
   IF (ABSO(tlag) > 1E-30)
      z = coi * (tt(i, 1) * tt(i, 1) * tiq3 / tlag - tiq / nb)
      z = z - (1 + kk) * tt(i, 1) * tiq3 / tlag
      tt(i, 14) = 0.5 * (kk - 1) * z
   ELSE
      tt(i, 14) = 0
   ENDIF

   tt(i, 158) = coi
   tt(i, 159) = kk
   tt(i, 160) = nb

   tvp = tt(i, 156)
   tvq = tt(i, 157)
NEXT

! ---------------------------------------------------------------------------------
! t15 to t24 - what each surface inherits. t15 to t19 are running sums of t10 to t14
! over the surfaces BEFORE this one, which is what makes the terms built on them
! induced rather than intrinsic. t20 to t23 are closed forms rather than sums, and
! t24 is a recursion carrying the previous surface's ratio.
! ---------------------------------------------------------------------------------

FOR i = 1, nsm, 1
   FOR j = 10, 14, 1
      z = 0
      ! ZPL does not skip a FOR whose end is below its start - it reports an infinite
      ! loop - so the empty sum at the first surface is guarded rather than left to the
      ! loop bounds. The first surface inherits nothing, which is the whole point of it.
      IF (i > 1)
         FOR m = 1, i - 1, 1
            z = z + tt(m, j)
         NEXT
      ENDIF
      tt(i, j + 5) = z
   NEXT
NEXT

f9 = tt(1, 9)
f81 = tt(1, 2) * tt(1, 5)
f82 = tt(1, 5) * tt(1, 5)

FOR i = 1, nsm, 1
   tt(i, 20) = 0.5 * (f9 - tt(i, 9)) + tt(i, 16)
   tt(i, 21) = 0.5 * (f81 - tt(i, 2) * tt(i, 5)) + tt(i, 18)
   tt(i, 22) = 2 * (tt(i, 21) - tt(i, 18)) + tt(i, 17)
   tt(i, 23) = 0.5 * (f82 - tt(i, 5) * tt(i, 5)) + tt(i, 19)
   IF (i == 1)
      tt(i, 24) = 0
   ELSE
      tt(i, 24) = (tt(i, 23) - tt(i - 1, 23)) * tt(i - 1, 6) + tt(i - 1, 24)
   ENDIF
NEXT

! ---------------------------------------------------------------------------------
! t25 to t33 - the dagger family, this surface's ratio applied to the sums above.
! ---------------------------------------------------------------------------------

FOR i = 1, nsm, 1
   qq = tt(i, 6)
   tt(i, 25) = qq * tt(i, 15) - tt(i, 20)
   tt(i, 26) = qq * tt(i, 16) - tt(i, 21)
   tt(i, 27) = 2 * qq * tt(i, 16) - tt(i, 22)
   tt(i, 28) = qq * tt(i, 17) - 2 * tt(i, 23)
   tt(i, 29) = qq * tt(i, 18) - tt(i, 23)
   tt(i, 30) = qq * tt(i, 19) - tt(i, 24)
   tt(i, 31) = -qq * tt(i, 25) + tt(i, 26)
   tt(i, 32) = -qq * tt(i, 27) + tt(i, 28)
   tt(i, 33) = -qq * tt(i, 29) + tt(i, 30)
NEXT


! ---------------------------------------------------------------------------------
! t34 to t37, and the w family - w1, w2, w3, w4, w5 at t43, t48, t49, t58, t64.
!
! t34 is Buchdahl's w, the quantity the intrinsic secondary is built on; t35 to t37
! are the three phi. The w family feeds the chain below. t81 and t82 are paraxial
! products of this surface alone, wanted by the q-side block further down.
! ---------------------------------------------------------------------------------

FOR i = 1, nsm, 1
   kk = tt(i, 159)
   t2s = tt(i, 156)
   t9s = t2s * t2s

   tt(i, 34) = ((kk * kk + 1) * tt(i, 3) * tt(i, 3) + t9s - 3 * tt(i, 9)) / 8
   tt(i, 35) = (kk - 1) * tt(i, 2) * tt(i, 3)
   tt(i, 36) = 0.5 * (-kk * tt(i, 3) * tt(i, 3) - tt(i, 9) + 8 * tt(i, 34) + 3 * tt(i, 35))
   tt(i, 37) = 2 * tt(i, 9) + 8 * tt(i, 34) + 5 * tt(i, 35) - tt(i, 36)

   tt(i, 43) = (t2s - 3 * tt(i, 2)) * tt(i, 7) * tt(i, 10)
   tt(i, 48) = 0.5 * tt(i, 7) * tt(i, 7) * tt(i, 10)
   tt(i, 49) = 0.25 * (t9s - tt(i, 9)) * t2s * tt(i, 7)
   tt(i, 58) = 0.5 * tt(i, 8) * tt(i, 8)
   tt(i, 64) = tt(i, 7) * tt(i, 7) * tt(i, 8) / 8

   tt(i, 81) = tt(i, 2) * tt(i, 5)
   tt(i, 82) = tt(i, 5) * tt(i, 5)
NEXT

! ---------------------------------------------------------------------------------
! t38 to t68 - the six intrinsic secondary coefficients, their barred partners and
! the middle quantities.
!
! The six are CHAINED: t44 is built from t38, t50 from t44 and so on, which is why
! they are formed in one pass rather than assembled coefficient by coefficient.
!
! The barred entry is the unbarred one lifted by the incidence ratio plus a bracket
! in the dagger family. The lift q s is formed as a product here, which is right for
! every surface except a flat one facing collimated space, where q is infinite and
! the product is 0 times infinity. Buchdahl's Sec. 84(e) carries the powers of q on
! the incidences instead and stays finite there; that is not transcribed, so such a
! surface is refused at the top of the macro instead of being got wrong quietly.
! ---------------------------------------------------------------------------------

FOR i = 1, nsm, 1
   qr = tt(i, 6)

   ss1 = 3 * tt(i, 10) * tt(i, 34)
   ss2 = 4 * qr * ss1 + tt(i, 43)
   ss3 = 0.5 * qr * (tt(i, 43) + ss2) + tt(i, 48) + tt(i, 49)
   ss4 = 2 * (ss3 - 2 * tt(i, 48) - tt(i, 49))
   ss5 = (-qr * ss2 + 2 * ss3 + ss4 - 2 * tt(i, 48)) * qr + tt(i, 58)
   ss6 = 0.5 * (0.25 * (2 * tt(i, 49) - 2 * ss3 - ss4) * qr + tt(i, 58) + ss5) * qr + tt(i, 64)

   tt(i, 38) = ss1
   tt(i, 44) = ss2
   tt(i, 50) = ss3
   tt(i, 54) = ss4
   tt(i, 59) = ss5
   tt(i, 65) = ss6

   ! The arguments Buchdahl's secondary construction takes: the primary and its lifts,
   ! all carried on the incidences rather than formed with q where that can be helped.
   aa = tt(i, 10)
   abq = tt(i, 11)
   q2a = tt(i, 12) / 2
   cc13 = tt(i, 13)
   qcc = tt(i, 14)
   qom = (tt(i, 159) - 1) * tt(i, 158) * (tt(i, 6) * tt(i, 3)) / tt(i, 160)

   d1 = tt(i, 25)
   d2 = tt(i, 26)
   d3 = tt(i, 27)
   d4 = tt(i, 28)
   d5 = tt(i, 29)
   d6 = tt(i, 30)
   d7 = tt(i, 31)
   d8 = tt(i, 32)
   d9 = tt(i, 33)

   ! Each q line below is the s line above it with every factor advanced one power of
   ! the incidence ratio, which is why they pair off.
   u1 = ss1 + 3 * aa * d1
   v1 = qr * ss1 + 3 * abq * d1
   tt(i, 41) = u1
   tt(i, 42) = aa * d7 + v1

   u2m = 2 * (abq * d1 + (d2 + d3) * aa) - tt(i, 8) * tt(i, 15) + ss2
   v2m = 2 * (q2a * d1 + (d2 + d3) * abq) - qom * tt(i, 15) + qr * ss2
   tt(i, 45) = u2m
   u2 = 2 * abq * d1 + aa * d3 + u2m
   v2 = 2 * q2a * d1 + abq * d3 + v2m
   tt(i, 46) = u2
   tt(i, 47) = 2 * abq * d7 + aa * d8 + v2

   u3m = 2 * (abq * d2 + d5 * aa) - tt(i, 8) * tt(i, 16) + ss3
   v3m = 2 * (q2a * d2 + d5 * abq) - qom * tt(i, 16) + qr * ss3
   tt(i, 51) = u3m
   u3 = cc13 * d1 + aa * d5 + u3m
   v3 = qcc * d1 + abq * d5 + v3m
   tt(i, 52) = u3
   tt(i, 53) = aa * d9 + cc13 * d7 + v3

   u4m = 2 * (u3m - ss3) + ss4
   v4m = 2 * (v3m - qr * ss3) + qr * ss4
   tt(i, 55) = u4m
   u4 = 2 * abq * d3 + u4m
   v4 = 2 * q2a * d3 + v4m
   tt(i, 56) = u4
   tt(i, 57) = 2 * abq * d8 + v4

   u5m = -(tt(i, 17) + tt(i, 18)) * tt(i, 8) + ss5 + 2 * (abq * (d4 + d5) + d6 * aa)
   v5m = -(tt(i, 17) + tt(i, 18)) * qom + qr * ss5 + 2 * (q2a * (d4 + d5) + d6 * abq)
   tt(i, 61) = u5m
   u5 = 2 * abq * d5 + cc13 * d3 + u5m
   v5 = 2 * q2a * d5 + qcc * d3 + v5m
   tt(i, 62) = u5
   tt(i, 63) = 2 * abq * d9 + cc13 * d8 + v5

   u6m = 2 * (cc13 * tt(i, 19) - abq * tt(i, 24)) + ss6
   v6m = 2 * (qcc * tt(i, 19) - q2a * tt(i, 24)) + qr * ss6
   tt(i, 66) = u6m
   u6 = cc13 * d5 + u6m
   v6 = qcc * d5 + v6m
   tt(i, 67) = u6
   tt(i, 68) = cc13 * d9 + v6

   tt(i, 39) = 2 * tt(i, 10) * tt(i, 25)
   tt(i, 40) = tt(i, 38) + tt(i, 39)
NEXT

! ---------------------------------------------------------------------------------
! t69 to t80 - running sums of the secondary over the surfaces before this one, the
! unbarred and barred alternating, as t41 and t42 do.
! ---------------------------------------------------------------------------------

! The sources are t41, t42, t46, t47, t52, t53, t56, t57, t62, t63, t67 and t68 -
! spaced 5, 6, 4, 6, 5 apart rather than evenly, so they are written out rather than
! indexed by a formula that would be wrong in the middle.
FOR i = 1, nsm, 1
   FOR j = 69, 80, 1
      tt(i, j) = 0
   NEXT
   IF (i > 1)
      FOR m = 1, i - 1, 1
         tt(i, 69) = tt(i, 69) + tt(m, 41)
         tt(i, 70) = tt(i, 70) + tt(m, 42)
         tt(i, 71) = tt(i, 71) + tt(m, 46)
         tt(i, 72) = tt(i, 72) + tt(m, 47)
         tt(i, 73) = tt(i, 73) + tt(m, 52)
         tt(i, 74) = tt(i, 74) + tt(m, 53)
         tt(i, 75) = tt(i, 75) + tt(m, 56)
         tt(i, 76) = tt(i, 76) + tt(m, 57)
         tt(i, 77) = tt(i, 77) + tt(m, 62)
         tt(i, 78) = tt(i, 78) + tt(m, 63)
         tt(i, 79) = tt(i, 79) + tt(m, 67)
         tt(i, 80) = tt(i, 80) + tt(m, 68)
      NEXT
   ENDIF
NEXT

! ---------------------------------------------------------------------------------
! t83 to t98 - the q-side accumulated secondaries, closed forms in the sums above.
! ---------------------------------------------------------------------------------

FOR i = 1, nsm, 1
   tt(i, 83) = tt(i, 16) - tt(i, 20)
   tt(i, 84) = tt(i, 17) - tt(i, 22)
   tt(i, 85) = tt(i, 19) - tt(i, 23)

   tt(i, 86) = -1.5 * tt(i, 83) * tt(i, 83) + tt(i, 9) * tt(i, 16) - tt(i, 16) * tt(i, 20)
   tt(i, 86) = tt(i, 86) + tt(i, 15) * tt(i, 21) - tt(i, 15) * tt(i, 81) + tt(i, 70)

   tt(i, 87) = (2 * tt(i, 21) - tt(i, 22) - tt(i, 81)) * tt(i, 16)
   tt(i, 87) = tt(i, 87) + tt(i, 9) * tt(i, 21) - tt(i, 20) * tt(i, 81)
   tt(i, 88) = (2 * tt(i, 23) - tt(i, 82)) * tt(i, 15) - tt(i, 17) * tt(i, 20) + tt(i, 9) * tt(i, 17)
   tt(i, 89) = -3 * tt(i, 83) * tt(i, 84) + tt(i, 87) + tt(i, 88) + tt(i, 72)

   tt(i, 90) = -0.5 * tt(i, 81) * tt(i, 84) + tt(i, 9) * tt(i, 19) - tt(i, 20) * tt(i, 82)
   tt(i, 91) = tt(i, 18) * tt(i, 21) + tt(i, 15) * tt(i, 24) - tt(i, 16) * tt(i, 23) - tt(i, 19) * tt(i, 20)
   tt(i, 92) = -3 * tt(i, 83) * tt(i, 85) + tt(i, 74) + tt(i, 90) + tt(i, 91)

   tt(i, 93) = 2 * (2 * tt(i, 16) * tt(i, 23) - tt(i, 16) * tt(i, 82) + tt(i, 9) * tt(i, 23))
   tt(i, 93) = tt(i, 93) - tt(i, 17) * tt(i, 22)
   tt(i, 94) = -1.5 * tt(i, 84) * tt(i, 84) + tt(i, 81) * tt(i, 84) + tt(i, 76) + tt(i, 93)

   tt(i, 95) = (2 * tt(i, 18) - tt(i, 17) + tt(i, 81)) * tt(i, 23)
   tt(i, 95) = tt(i, 95) + tt(i, 19) * tt(i, 81) - tt(i, 22) * tt(i, 82)
   tt(i, 96) = (2 * tt(i, 16) + tt(i, 9)) * tt(i, 24) - tt(i, 19) * tt(i, 22) - tt(i, 18) * tt(i, 82)
   tt(i, 97) = -3 * tt(i, 84) * tt(i, 85) + tt(i, 95) + tt(i, 96) + tt(i, 78)

   tt(i, 98) = -1.5 * tt(i, 85) * tt(i, 85) + tt(i, 24) * tt(i, 81) + tt(i, 18) * tt(i, 24)
   tt(i, 98) = tt(i, 98) - tt(i, 23) * tt(i, 82) - tt(i, 19) * tt(i, 23) + tt(i, 80)
NEXT


! ---------------------------------------------------------------------------------
! t99 to t155 - the tertiary.
!
! This block is TRANSCRIBED FROM THE C# rather than typed out, by a small generator
! that reads the assignments and rewrites t[k] as tt(i, k). Two dozen expressions of
! this density, retyped by hand where they cannot be executed, would contain mistakes;
! the generator cannot mistype and its output was diffed against the source.
!
! Two things it could not carry across, and which are written here instead. The
! aspheric correction that appears in the dagger recursions is identically zero on a
! spherical surface and is dropped. And the entries of the previous surface are held
! in locals rather than read as tt(i - 1, k), because nothing precedes the first
! surface and tt(0, k) is not an index this array has.
! ---------------------------------------------------------------------------------

FOR i = 1, nsm, 1

   cvn = CURV(i) * efl
   t2s = tt(i, 156)
   t9s = t2s * t2s
   t81s = t2s * tt(i, 157)
   qr = tt(i, 6)

   ! The previous surface's entries. Nothing precedes the first, so they are zero
   ! there; they are held in locals because tt(0, k) is not an index this array has.
   IF (i > 1)
      p6 = tt(i - 1, 6)
      p31 = tt(i - 1, 31)
      p32 = tt(i - 1, 32)
      p33 = tt(i - 1, 33)
      p70 = tt(i - 1, 70)
      p72 = tt(i - 1, 72)
      p74 = tt(i - 1, 74)
      p76 = tt(i - 1, 76)
      p78 = tt(i - 1, 78)
      p80 = tt(i - 1, 80)
      p86 = tt(i - 1, 86)
      p89 = tt(i - 1, 89)
      p92 = tt(i - 1, 92)
      p94 = tt(i - 1, 94)
      p97 = tt(i - 1, 97)
      p98 = tt(i - 1, 98)
      p80 = tt(i - 1, 80)
      p98 = tt(i - 1, 98)
      p99 = tt(i - 1, 99)
      p100 = tt(i - 1, 100)
      p102 = tt(i - 1, 102)
      p104 = tt(i - 1, 104)
      p107 = tt(i - 1, 107)
      p109 = tt(i - 1, 109)
      p112 = tt(i - 1, 112)
      p114 = tt(i - 1, 114)
   ELSE
      p6 = 0
      p31 = 0
      p32 = 0
      p33 = 0
      p70 = 0
      p72 = 0
      p74 = 0
      p76 = 0
      p78 = 0
      p80 = 0
      p86 = 0
      p89 = 0
      p92 = 0
      p94 = 0
      p97 = 0
      p98 = 0
      p80 = 0
      p98 = 0
      p99 = 0
      p100 = 0
      p102 = 0
      p104 = 0
      p107 = 0
      p109 = 0
      p112 = 0
      p114 = 0
   ENDIF

   ! --- t99, t100 and the dagger tertiary t101 to t120 ---
   tcar = p6 * (-0.5 * p33 * p100 - p80 + p98 - tt(i, 98))

   tt(i, 99) = 0.5 * (tt(i, 9) - t9s) + tt(i, 11)
   tt(i, 100) = tt(i, 81) - t81s + tt(i, 12)
   tt(i, 101) = tt(i, 6) * tt(i, 69) - tt(i, 86)
   tt(i, 102) = -p6 * (p70 - p86 + tt(i, 86)) + tt(i, 6) * tt(i, 70) - p31 * p99 + p102
   tt(i, 103) = tt(i, 6) * tt(i, 71) - tt(i, 89)
   tt(i, 104) = -p6 * (p72 - p89 + tt(i, 89)) + tt(i, 6) * tt(i, 72) - p31 * p100 - p32 * p99 + p104
   tt(i, 105) = tt(i, 6) * tt(i, 73) - tt(i, 92)
   tt(i, 106) = -0.5 * p100 * p31 * p6 + tt(i, 74) * tt(i, 6) - p33 * p99
   tt(i, 107) = -p6 * (p74 - p92 + tt(i, 92)) + tt(i, 106) + p107
   tt(i, 108) = tt(i, 6) * tt(i, 75) - tt(i, 94)
   tt(i, 109) = -p6 * (p76 - p94 + tt(i, 94)) - p32 * p100 + tt(i, 6) * tt(i, 76) + p109
   tt(i, 110) = tt(i, 6) * tt(i, 77) - tt(i, 97)
   tt(i, 111) = -((0.5 * p6 * p32 + p33) * p100) + tt(i, 6) * tt(i, 78)
   tt(i, 112) = -p6 * (p78 - p97 + tt(i, 97)) + tt(i, 111) + p112
   tt(i, 113) = tt(i, 6) * tt(i, 79) - tt(i, 98)
   tt(i, 114) = tcar + tt(i, 6) * tt(i, 80) + p114
   tt(i, 115) = -tt(i, 6) * tt(i, 101) + tt(i, 102)
   tt(i, 116) = -tt(i, 6) * tt(i, 103) + tt(i, 104)
   tt(i, 117) = -tt(i, 6) * tt(i, 105) + tt(i, 107)
   tt(i, 118) = -tt(i, 6) * tt(i, 108) + tt(i, 109)
   tt(i, 119) = -tt(i, 6) * tt(i, 110) + tt(i, 112)
   tt(i, 120) = -tt(i, 6) * tt(i, 113) + tt(i, 114)

   ! --- t121 to t130, the ten intrinsic tertiary coefficients ---
   tt(i, 121) = (0.25 * (2.0 * t2s - 5.0 * tt(i, 2)) * tt(i, 7) * tt(i, 10) + 10.0 * tt(i, 34) * tt(i, 34)) * tt(i, 10)
   tt(i, 122) = 0.5 * ((t2s - tt(i, 3)) * cvn * tt(i, 1) * tt(i, 2) * tt(i, 7) * tt(i, 10) + 10.0 * tt(i, 34) * tt(i, 43))
   tt(i, 123) = ((2.0 * tt(i, 34) + 0.5 * tt(i, 36) - 0.75 * tt(i, 35)) * tt(i, 35) - tt(i, 34) * tt(i, 37)) * 0.75 * tt(i, 8)
   tt(i, 124) = (5.0 * tt(i, 9) - 10.0 * tt(i, 34) + 2.0 * tt(i, 36)) * 0.25 * tt(i, 48)
   tt(i, 125) = -0.5 * (tt(i, 2) + t2s) * tt(i, 7) * tt(i, 8) * tt(i, 36)
   tt(i, 126) = 2.0 * (tt(i, 9) + tt(i, 34) - 5.0 * tt(i, 35) / 4.0) * tt(i, 64)
   tt(i, 127) = -tt(i, 7) * tt(i, 7) * tt(i, 43)
   tt(i, 128) = -2.0 * (4.0 * tt(i, 34) + 5.0 * tt(i, 35) - 2.0 * tt(i, 9)) * tt(i, 64)
   tt(i, 129) = -0.75 * tt(i, 7) * tt(i, 7) * tt(i, 58)
   tt(i, 130) = -0.5 * tt(i, 7) * tt(i, 7) * tt(i, 64)

   ! --- t131 to t155, the totals. The chain t134 to t152 is the intrinsic part; each
   !     total below it is that plus what the surface inherits. ---
   tt(i, 134) = 6.0 * tt(i, 6) * tt(i, 121) + tt(i, 122)
   tt(i, 136) = 0.5 * (tt(i, 134) + tt(i, 122)) * tt(i, 6) + tt(i, 123) + tt(i, 124)
   tt(i, 138) = 4.0 * (tt(i, 136) - tt(i, 123) + tt(i, 124))
   tt(i, 140) = (-2.0 * tt(i, 6) * tt(i, 134) + 4.0 * tt(i, 136) + tt(i, 138) + 8.0 * tt(i, 124)) * tt(i, 6) + tt(i, 125)
   tt(i, 143) = ((tt(i, 123) + tt(i, 124) - tt(i, 136)) * tt(i, 6) + 0.5 * tt(i, 140) + 0.5 * tt(i, 125)) * tt(i, 6) + tt(i, 126)
   tt(i, 145) = (2.0 / 3.0) * (4.0 * (tt(i, 124) - tt(i, 123)) * tt(i, 6) + tt(i, 140) - tt(i, 125)) + tt(i, 127)
   tt(i, 147) = (4.0 * (tt(i, 124) - tt(i, 123)) * tt(i, 6) + 3.0 * tt(i, 127) - 2.0 * tt(i, 125)) * tt(i, 6) + tt(i, 128) - 4.0 * tt(i, 126) + 4.0 * tt(i, 143)
   tt(i, 149) = ((4.0 * tt(i, 6) * tt(i, 124) + tt(i, 125) + 0.75 * tt(i, 127) - 0.75 * tt(i, 145)) * tt(i, 6) + tt(i, 147) + 2.0 * tt(i, 126) + tt(i, 128)) * tt(i, 6) + tt(i, 129)
   tt(i, 151) = ((tt(i, 6) * tt(i, 121) + tt(i, 122)) * tt(i, 6) + tt(i, 123) + 9.0 * tt(i, 124)) * tt(i, 6) + tt(i, 125) + tt(i, 127)
   tt(i, 152) = ((tt(i, 6) * tt(i, 151) + tt(i, 126) + tt(i, 128)) * tt(i, 6) + tt(i, 129)) * tt(i, 6) + tt(i, 130)
   tt(i, 131) = 4.0 * ((-tt(i, 8) * tt(i, 15) + 2.0 * tt(i, 44)) * 0.125 * tt(i, 15) - tt(i, 20) * tt(i, 38))
   tt(i, 132) = (tt(i, 25) * tt(i, 25) + 3.0 * tt(i, 101)) * tt(i, 10) + tt(i, 25) * tt(i, 40) + tt(i, 121) + tt(i, 131)
   tt(i, 133) = tt(i, 6) * tt(i, 132) + tt(i, 10) * tt(i, 115) + tt(i, 31) * tt(i, 40)
   tt(i, 135) = tt(i, 134)
   tt(i, 135) = tt(i, 135) + 4.0 * (0.5 * (tt(i, 26) + tt(i, 27)) * tt(i, 25) + qr * tt(i, 101) + 0.5 * tt(i, 102) + 0.75 * tt(i, 103)) * tt(i, 10)
   tt(i, 135) = tt(i, 135) + 3.0 * (-(tt(i, 15) * tt(i, 16) + tt(i, 69) / 3.0) * tt(i, 8) + tt(i, 44) * tt(i, 83)) + 2.0 * (tt(i, 50) + tt(i, 54)) * tt(i, 15)
   tt(i, 135) = tt(i, 135) + tt(i, 25) * tt(i, 45) + tt(i, 27) * tt(i, 40) - 4.0 * (tt(i, 21) + tt(i, 22)) * tt(i, 38)
   tt(i, 137) = tt(i, 136)
   tt(i, 137) = tt(i, 137) + (2.0 * (qr * tt(i, 102) + tt(i, 25) * tt(i, 29)) + tt(i, 26) * tt(i, 26) + 3.0 * tt(i, 105)) * tt(i, 10)
   tt(i, 137) = tt(i, 137) - (0.5 * tt(i, 16) * tt(i, 16) + tt(i, 15) * tt(i, 18) + tt(i, 70)) * tt(i, 8) + tt(i, 25) * tt(i, 51) + tt(i, 29) * tt(i, 40)
   tt(i, 137) = tt(i, 137) + 2.0 * (-2.0 * tt(i, 23) * tt(i, 38) + tt(i, 50) * tt(i, 83)) + tt(i, 15) * tt(i, 59) + 0.5 * tt(i, 44) * tt(i, 84) + tt(i, 13) * tt(i, 101)
   tt(i, 139) = tt(i, 138)
   tt(i, 139) = tt(i, 139) + (2.0 * (2.0 * qr * tt(i, 103) + tt(i, 25) * tt(i, 28) + tt(i, 26) * tt(i, 27)) + tt(i, 27) * tt(i, 27) + 2.0 * tt(i, 104) + 3.0 * tt(i, 108)) * tt(i, 10)
   tt(i, 139) = tt(i, 139) - (4.0 * tt(i, 16) * tt(i, 16) + tt(i, 15) * tt(i, 17) + tt(i, 71)) * tt(i, 8) + 4.0 * (-2.0 * tt(i, 23) * tt(i, 38) + tt(i, 16) * tt(i, 50))
   tt(i, 139) = tt(i, 139) + tt(i, 27) * tt(i, 45) + tt(i, 25) * tt(i, 55) + 2.0 * ((3.0 * tt(i, 16) - tt(i, 20)) * tt(i, 54) + tt(i, 15) * tt(i, 59))
   tt(i, 139) = tt(i, 139) + (tt(i, 17) - 2.0 * tt(i, 21) - 3.0 * tt(i, 22)) * tt(i, 44)
   tt(i, 141) = tt(i, 140)
   tt(i, 141) = tt(i, 141) + 2.0 * ((2.0 * tt(i, 105) + tt(i, 104)) * qr + tt(i, 107) + 1.5 * tt(i, 110)) * tt(i, 10) + tt(i, 13) * tt(i, 103)
   tt(i, 141) = tt(i, 141) + 2.0 * ((tt(i, 28) + tt(i, 29)) * tt(i, 26) + tt(i, 25) * tt(i, 30) + tt(i, 27) * tt(i, 29)) * tt(i, 10)
   tt(i, 141) = tt(i, 141) - ((3.0 * tt(i, 18) + tt(i, 17)) * tt(i, 16) + tt(i, 15) * tt(i, 19) + tt(i, 72) + tt(i, 73)) * tt(i, 8) + (tt(i, 19) - 5.0 * tt(i, 23)) * tt(i, 44)
   tt(i, 141) = tt(i, 141) + tt(i, 29) * tt(i, 45) + (3.0 * tt(i, 50) + tt(i, 54)) * tt(i, 84) + tt(i, 27) * tt(i, 51) + (5.0 * tt(i, 16) - tt(i, 20)) * tt(i, 59)
   tt(i, 141) = tt(i, 141) + tt(i, 25) * tt(i, 61) + 4.0 * (tt(i, 15) * tt(i, 65) - tt(i, 24) * tt(i, 38))
   tt(i, 144) = tt(i, 143)
   tt(i, 144) = tt(i, 144) + (2.0 * (qr * tt(i, 107) + tt(i, 26) * tt(i, 30)) + tt(i, 29) * tt(i, 29) + 3.0 * tt(i, 113)) * tt(i, 10)
   tt(i, 144) = tt(i, 144) - (0.5 * tt(i, 18) * tt(i, 18) + tt(i, 16) * tt(i, 19) + tt(i, 74)) * tt(i, 8) + tt(i, 29) * tt(i, 51) + tt(i, 25) * tt(i, 66)
   tt(i, 144) = tt(i, 144) + 2.0 * (2.0 * tt(i, 16) * tt(i, 65) + tt(i, 50) * tt(i, 85)) - tt(i, 24) * tt(i, 44) + tt(i, 13) * tt(i, 105) + 0.5 * tt(i, 59) * tt(i, 84)
   tt(i, 146) = tt(i, 145)
   tt(i, 146) = tt(i, 146) + 2.0 * ((2.0 * qr * tt(i, 108) + tt(i, 27) * tt(i, 28) + tt(i, 109)) * tt(i, 10) + tt(i, 54) * tt(i, 84))
   tt(i, 146) = tt(i, 146) - (2.0 * tt(i, 16) * tt(i, 17) + tt(i, 75)) * tt(i, 8) + tt(i, 27) * tt(i, 55) + 4.0 * (tt(i, 16) * tt(i, 59) - tt(i, 23) * tt(i, 44))
   tt(i, 148) = tt(i, 147)
   tt(i, 148) = tt(i, 148) - ((0.5 * tt(i, 17) + tt(i, 18)) * tt(i, 17) + tt(i, 76) + tt(i, 77)) * tt(i, 8) + tt(i, 13) * tt(i, 108)
   tt(i, 148) = tt(i, 148) + 2.0 * (-tt(i, 8) * tt(i, 16) + tt(i, 54)) * tt(i, 19) + tt(i, 29) * tt(i, 55) + tt(i, 27) * tt(i, 61)
   tt(i, 148) = tt(i, 148) + (2.0 * ((2.0 * tt(i, 110) + tt(i, 109)) * qr + tt(i, 28) * tt(i, 29) + tt(i, 27) * tt(i, 30) + tt(i, 112)) + tt(i, 28) * tt(i, 28)) * tt(i, 10)
   tt(i, 148) = tt(i, 148) + 8.0 * (-0.25 * ((2.0 * tt(i, 50) + 3.0 * tt(i, 54)) * tt(i, 23) + tt(i, 24) * tt(i, 44)) + tt(i, 16) * tt(i, 65))
   tt(i, 148) = tt(i, 148) + (3.0 * tt(i, 17) + 2.0 * tt(i, 18) - tt(i, 22)) * tt(i, 59)
   tt(i, 150) = tt(i, 149)
   tt(i, 150) = tt(i, 150) + 2.0 * ((2.0 * tt(i, 113) + tt(i, 112)) * qr + tt(i, 28) * tt(i, 30) + tt(i, 29) * tt(i, 30) + tt(i, 114)) * tt(i, 10)
   tt(i, 150) = tt(i, 150) - ((tt(i, 17) + tt(i, 18)) * tt(i, 19) + tt(i, 78) + tt(i, 79)) * tt(i, 8) + tt(i, 13) * tt(i, 110)
   tt(i, 150) = tt(i, 150) + 4.0 * ((tt(i, 17) + tt(i, 18)) * tt(i, 65) + 0.75 * tt(i, 59) * tt(i, 85)) + tt(i, 29) * tt(i, 61) + tt(i, 27) * tt(i, 66)
   tt(i, 150) = tt(i, 150) - 2.0 * (tt(i, 50) + tt(i, 54)) * tt(i, 24)
   tt(i, 153) = (2.0 * qr * tt(i, 114) + tt(i, 30) * tt(i, 30)) * tt(i, 10) + tt(i, 13) * tt(i, 113)
   tt(i, 154) = -(0.5 * tt(i, 19) * tt(i, 19) + tt(i, 80)) * tt(i, 8) + tt(i, 29) * tt(i, 66) - tt(i, 24) * tt(i, 59)
   tt(i, 155) = 4.0 * tt(i, 19) * tt(i, 65) + tt(i, 152) + tt(i, 153) + tt(i, 154)

   ! --- The ten tertiary coefficients, their barred partners, and the intrinsic half.
   !     Stored at 171, 181 and 191 so they survive the loop. ---
   tt(i, 171) = tt(i, 132)
   tt(i, 172) = tt(i, 135)
   tt(i, 173) = tt(i, 137)
   tt(i, 174) = tt(i, 139)
   tt(i, 175) = tt(i, 141)
   tt(i, 176) = tt(i, 144)
   tt(i, 177) = tt(i, 146)
   tt(i, 178) = tt(i, 148)
   tt(i, 179) = tt(i, 150)
   tt(i, 180) = tt(i, 155)

   tt(i, 181) = qr * tt(i, 132) + tt(i, 10) * tt(i, 115) + tt(i, 31) * tt(i, 40)
   tt(i, 182) = qr * (2 * tt(i, 10) * tt(i, 115) + tt(i, 135)) + tt(i, 10) * tt(i, 116) + tt(i, 31) * tt(i, 45) + tt(i, 32) * tt(i, 40)
   tt(i, 183) = qr * tt(i, 137) + tt(i, 10) * tt(i, 117) + tt(i, 13) * tt(i, 115) + tt(i, 31) * tt(i, 51) + tt(i, 33) * tt(i, 40)
   tt(i, 184) = qr * (2 * tt(i, 116) * tt(i, 10) + tt(i, 139)) + tt(i, 118) * tt(i, 10) + tt(i, 31) * tt(i, 55) + tt(i, 32) * tt(i, 45)
   tt(i, 185) = qr * (2 * tt(i, 117) * tt(i, 10) + tt(i, 141)) + tt(i, 119) * tt(i, 10) + tt(i, 13) * tt(i, 116)
   tt(i, 185) = tt(i, 185) + tt(i, 31) * tt(i, 61) + tt(i, 32) * tt(i, 51) + tt(i, 33) * tt(i, 45)
   tt(i, 186) = qr * tt(i, 144) + tt(i, 10) * tt(i, 120) + tt(i, 13) * tt(i, 117) + tt(i, 31) * tt(i, 66) + tt(i, 33) * tt(i, 51)
   tt(i, 187) = qr * (2 * tt(i, 10) * tt(i, 118) + tt(i, 146)) + tt(i, 32) * tt(i, 55)
   tt(i, 188) = qr * (2 * tt(i, 10) * tt(i, 119) + tt(i, 148)) + tt(i, 13) * tt(i, 118) + tt(i, 32) * tt(i, 61) + tt(i, 33) * tt(i, 55)
   tt(i, 189) = qr * (2 * tt(i, 10) * tt(i, 120) + tt(i, 150)) + tt(i, 13) * tt(i, 119) + tt(i, 32) * tt(i, 66) + tt(i, 33) * tt(i, 61)
   tt(i, 190) = qr * tt(i, 155) + tt(i, 13) * tt(i, 120) + tt(i, 33) * tt(i, 66)

   tt(i, 191) = tt(i, 121)
   tt(i, 192) = tt(i, 134)
   tt(i, 193) = tt(i, 136)
   tt(i, 194) = tt(i, 138)
   tt(i, 195) = tt(i, 140)
   tt(i, 196) = tt(i, 143)
   tt(i, 197) = tt(i, 145)
   tt(i, 198) = tt(i, 147)
   tt(i, 199) = tt(i, 149)
   tt(i, 200) = tt(i, 152)
NEXT

IF (dbg == 1)
   PRINT " "
   PRINT "Table I entries. The reference file macros/reference/CookeTriplet_TableI.txt"
   PRINT "is the CookeTriplet at INFINITE conjugate; move the object and these change,"
   PRINT "which is the point - t2 on surface one is iota."
   FOR j = 1, 155, 1
      FORMAT 4.0 INT
      PRINT "t", j,
      FORMAT 20.9 EXP
      FOR i = 1, nsm, 1
         PRINT tt(i, j),
      NEXT
      PRINT " "
   NEXT
ENDIF

! ---------------------------------------------------------------------------------
! Stage D - the twenty tau, and the split this macro exists to print.
!
! Buchdahl's scheme produces ten tertiary coefficients per surface and their ten
! barred partners. Table II of the same paper combines those twenty into the twenty
! tau of the seventh-order polynomial, and the combination is not one to one: tau2
! mixes a barred coefficient with half of an unbarred one, tau7 draws on four at
! once. That is why the coefficients are computed first and converted afterwards.
!
! Table II is LINEAR, so applying it to one surface's ten and ten gives that
! surface's share of each tau, and the shares sum to the system total. The same map
! applied to the intrinsic ten gives the intrinsic share, and the difference is what
! the surface induced.
!
! The barred partner of an intrinsic coefficient is the pass ratio times itself -
! M (84.23) own rule for reading a barred entry off an unbarred one, whose second
! half is induced by construction and so drops out here.
!
! ON THE UNITS, WHICH DIFFER FROM THE BLOCK ABOVE
!
!   The third and fifth orders are printed per surface unconverted, because every
!   coefficient there takes the same factor and the reader can apply it once. The
!   seventh order cannot be printed that way: Buchdahl's aperture variable is the
!   ray SLOPE and his field variable is the TANGENT of the field angle, so a tau
!   multiplying rho^a H^b converts by efl u^a H^b and every tau takes a DIFFERENT
!   factor. Unconverted, tau1 and tau20 could not be compared with each other or
!   with anything else. So the tables below are in transverse measure throughout.
!
!   tau1 is seventh-order spherical aberration. It is printed twice - once as B7
!   above, reached through the fifth-order working, and once here through Table I.
!   On a system of spheres the two routes agree exactly, and the totals printed
!   below can be checked against the B7 printed above without leaving this listing.
! ---------------------------------------------------------------------------------

! The aperture variable of M (13.5)/(31.13): the ray height at the entrance pupil, in
! focal lengths, over g = 1 - p iota. The marginal ray is at the pupil edge by
! definition, so its height there is that radius. At infinite conjugate this is
! -1/(2 F), the quantity the third and fifth orders are converted by.
epr = VEC2(2) + epp * VEC2(501)
um = -(epr / efl) / goe

! The field tangent, taken from the chief ray rather than from the field data,
! because the chief ray gives it whatever the field type is - angle, object height
! or image height - and the ray has already been traced. MAXF is read only to say
! so when the two disagree.
! The field variable, M (13.4)'s T = iota H_1: the object height over the object
! distance. It tends to the tangent of the field angle as the object recedes, which is
! why the two are one quantity and not two.
IF (finite == 1)
   hobj = VEC3(2) - objd * VEC3(501)
   hmax = -hobj / objd
ELSE
   hmax = ABSO(VEC3(501))
ENDIF
IF (ABSO(hmax) < eps)
   PRINT "The chief ray carries no field, so the field variable cannot be recovered."
   PRINT "Is the field zero?"
   GOTO 999
ENDIF
mf = MAXF()
IF ((ABSO(mf) > eps) & (finite == 0))
   ang = ABSO(mf) * 3.14159265358979 / 180
   tf = SINE(ang) / COSI(ang)
   IF (ABSO(tf - hmax) > 1E-6 * (1 + hmax))
      FORMAT 12.6
      PRINT " "
      PRINT "NOTE: the maximum field ", mf, " read as an angle has tangent ", tf
      PRINT "      but the chief ray object-space tangent is ", hmax
      PRINT "      The field is presumably a height, not an angle. The chief ray"
      PRINT "      is used, which is right either way."
   ENDIF
ENDIF

! THE FIELD VARIABLE CARRIES THE OBJECT INDEX, the other half of the reduction made
! at the ray start above. The chief ray's tangent recovered here is a PLAIN angle, and
! the q ray it is expressed in was divided by n0, so the number that multiplies that
! ray is n0 times the tangent. Applied after the MAXF comparison above so that the
! diagnostic goes on comparing two plain tangents, which is what it is about. One in
! air, so nothing this macro has ever reported moves.
hmax = hmax * n0abs

! The per-tau conversion factor, efl u^a H^(7-a). The aperture powers run
! 7 6 6 5 5 5 4 4 4 4 3 3 3 3 2 2 2 1 1 0 and the field power is seven less each.
! VEC1 held the system data, which was read out at the top, so it is free.
up2 = um * um
up3 = up2 * um
up4 = up3 * um
up5 = up4 * um
up6 = up5 * um
up7 = up6 * um
h2 = hmax * hmax
h3 = h2 * hmax
h4 = h3 * hmax
h5 = h4 * hmax
h6 = h5 * hmax
h7 = h6 * hmax

! M (31.11)'s conversion factor, 1/(N'_k v'_pk), where v'_pk is the p ray's OWN angle
! after the last surface - t156 there. Paper III states the same rule in words: the
! entries are the augmented coefficients and "must be divided by N_k'v_pk'" to give the
! actual ones. The scheme's v is the ordinary angle rather than the reduced one, so the
! image-space index stands beside it. It is exactly efl only for a collimated beam.
vpk = tt(nsm, 156)
IF (ABSO(vpk) < eps)
   PRINT "The p ray leaves the last surface parallel to the axis - the system is"
   PRINT "afocal, and the seventh order cannot be put into transverse measure."
   GOTO 999
ENDIF
efac = efl / (nlast * vpk)

VEC1(1) = efac * up7
VEC1(2) = efac * up6 * hmax
VEC1(3) = VEC1(2)
VEC1(4) = efac * up5 * h2
VEC1(5) = VEC1(4)
VEC1(6) = VEC1(4)
VEC1(7) = efac * up4 * h3
VEC1(8) = VEC1(7)
VEC1(9) = VEC1(7)
VEC1(10) = VEC1(7)
VEC1(11) = efac * up3 * h4
VEC1(12) = VEC1(11)
VEC1(13) = VEC1(11)
VEC1(14) = VEC1(11)
VEC1(15) = efac * up2 * h5
VEC1(16) = VEC1(15)
VEC1(17) = VEC1(15)
VEC1(18) = efac * um * h6
VEC1(19) = VEC1(18)
VEC1(20) = efac * h7

! System totals accumulate in VEC1(21..40) total and VEC1(41..60) intrinsic.
FOR k = 21, 60, 1
   VEC1(k) = 0
NEXT

FOR i = 1, nsm, 1

   qr = tt(i, 6)

   ! Twice through Table II: once on the totals, whose barred partners were built
   ! entry by entry, and once on the intrinsic, whose barred partner is the ratio
   ! times itself. Same map, different inputs, so it is written once and run twice.
   FOR half = 1, 2, 1

      IF (half == 1)
         gbase = 170
         bbase = 180
         bscl = 1
         obase = 200
      ELSE
         gbase = 190
         bbase = 190
         bscl = qr
         obase = 220
      ENDIF

      g1 = tt(i, gbase + 1)
      g2 = tt(i, gbase + 2)
      g3 = tt(i, gbase + 3)
      g4 = tt(i, gbase + 4)
      g5 = tt(i, gbase + 5)
      g6 = tt(i, gbase + 6)
      g7 = tt(i, gbase + 7)
      g8 = tt(i, gbase + 8)
      g9 = tt(i, gbase + 9)
      g10 = tt(i, gbase + 10)

      c1 = bscl * tt(i, bbase + 1)
      c2 = bscl * tt(i, bbase + 2)
      c3 = bscl * tt(i, bbase + 3)
      c4 = bscl * tt(i, bbase + 4)
      c5 = bscl * tt(i, bbase + 5)
      c6 = bscl * tt(i, bbase + 6)
      c7 = bscl * tt(i, bbase + 7)
      c8 = bscl * tt(i, bbase + 8)
      c9 = bscl * tt(i, bbase + 9)
      c10 = bscl * tt(i, bbase + 10)

      tt(i, obase + 1) = g1
      tt(i, obase + 2) = c1 + 0.5 * g2
      tt(i, obase + 3) = 0.5 * g2
      tt(i, obase + 4) = c2 + g3
      tt(i, obase + 5) = g3
      tt(i, obase + 6) = g4
      tt(i, obase + 7) = c3 + 0.5 * c4 + 0.5 * g5 + 0.375 * g7
      tt(i, obase + 8) = 0.5 * (c4 + g5 + g7)
      tt(i, obase + 9) = 0.5 * g5 + 0.25 * g7
      tt(i, obase + 10) = 0.125 * g7
      tt(i, obase + 11) = c5 + g6
      tt(i, obase + 12) = c7 + g8
      tt(i, obase + 13) = g6
      tt(i, obase + 14) = g8
      tt(i, obase + 15) = c6 + 0.5 * c8 + 0.5 * g9
      tt(i, obase + 16) = 0.5 * (c8 + g9)
      tt(i, obase + 17) = 0.5 * g9
      tt(i, obase + 18) = c9 + g10
      tt(i, obase + 19) = g10
      tt(i, obase + 20) = c10

      FOR k = 1, 20, 1
         tt(i, obase + k) = tt(i, obase + k) * VEC1(k)
      NEXT

   NEXT

   FOR k = 1, 20, 1
      VEC1(20 + k) = VEC1(20 + k) + tt(i, 200 + k)
      VEC1(40 + k) = VEC1(40 + k) + tt(i, 220 + k)
   NEXT

NEXT

PRINT " "
PRINT "Seventh order, per surface, transverse measure"
PRINT " "
PRINT "  tau1 is seventh-order spherical aberration; the aperture power falls from"
PRINT "  seven to zero across the twenty and the field power rises to meet it."

FOR i = 1, nsm, 1
   FORMAT 4.0 INT
   PRINT " "
   PRINT " Surface", i
   FORMAT 15.6 EXP
   PRINT "            tau1           tau2           tau3           tau4           tau5"
   PRINT "  tot", tt(i, 201), tt(i, 202), tt(i, 203), tt(i, 204), tt(i, 205)
   PRINT "  int", tt(i, 221), tt(i, 222), tt(i, 223), tt(i, 224), tt(i, 225)
   PRINT "  ind", tt(i, 201) - tt(i, 221), tt(i, 202) - tt(i, 222), tt(i, 203) - tt(i, 223), tt(i, 204) - tt(i, 224), tt(i, 205) - tt(i, 225)
   PRINT "            tau6           tau7           tau8           tau9          tau10"
   PRINT "  tot", tt(i, 206), tt(i, 207), tt(i, 208), tt(i, 209), tt(i, 210)
   PRINT "  int", tt(i, 226), tt(i, 227), tt(i, 228), tt(i, 229), tt(i, 230)
   PRINT "  ind", tt(i, 206) - tt(i, 226), tt(i, 207) - tt(i, 227), tt(i, 208) - tt(i, 228), tt(i, 209) - tt(i, 229), tt(i, 210) - tt(i, 230)
   PRINT "           tau11          tau12          tau13          tau14          tau15"
   PRINT "  tot", tt(i, 211), tt(i, 212), tt(i, 213), tt(i, 214), tt(i, 215)
   PRINT "  int", tt(i, 231), tt(i, 232), tt(i, 233), tt(i, 234), tt(i, 235)
   PRINT "  ind", tt(i, 211) - tt(i, 231), tt(i, 212) - tt(i, 232), tt(i, 213) - tt(i, 233), tt(i, 214) - tt(i, 234), tt(i, 215) - tt(i, 235)
   PRINT "           tau16          tau17          tau18          tau19          tau20"
   PRINT "  tot", tt(i, 216), tt(i, 217), tt(i, 218), tt(i, 219), tt(i, 220)
   PRINT "  int", tt(i, 236), tt(i, 237), tt(i, 238), tt(i, 239), tt(i, 240)
   PRINT "  ind", tt(i, 216) - tt(i, 236), tt(i, 217) - tt(i, 237), tt(i, 218) - tt(i, 238), tt(i, 219) - tt(i, 239), tt(i, 220) - tt(i, 240)
NEXT

PRINT " "
PRINT "Seventh order, system totals, transverse measure"
FORMAT 15.6 EXP
PRINT "            tau1           tau2           tau3           tau4           tau5"
PRINT "  tot", VEC1(21), VEC1(22), VEC1(23), VEC1(24), VEC1(25)
PRINT "  int", VEC1(41), VEC1(42), VEC1(43), VEC1(44), VEC1(45)
PRINT "  ind", VEC1(21) - VEC1(41), VEC1(22) - VEC1(42), VEC1(23) - VEC1(43), VEC1(24) - VEC1(44), VEC1(25) - VEC1(45)
PRINT "            tau6           tau7           tau8           tau9          tau10"
PRINT "  tot", VEC1(26), VEC1(27), VEC1(28), VEC1(29), VEC1(30)
PRINT "  int", VEC1(46), VEC1(47), VEC1(48), VEC1(49), VEC1(50)
PRINT "  ind", VEC1(26) - VEC1(46), VEC1(27) - VEC1(47), VEC1(28) - VEC1(48), VEC1(29) - VEC1(49), VEC1(30) - VEC1(50)
PRINT "           tau11          tau12          tau13          tau14          tau15"
PRINT "  tot", VEC1(31), VEC1(32), VEC1(33), VEC1(34), VEC1(35)
PRINT "  int", VEC1(51), VEC1(52), VEC1(53), VEC1(54), VEC1(55)
PRINT "  ind", VEC1(31) - VEC1(51), VEC1(32) - VEC1(52), VEC1(33) - VEC1(53), VEC1(34) - VEC1(54), VEC1(35) - VEC1(55)
PRINT "           tau16          tau17          tau18          tau19          tau20"
PRINT "  tot", VEC1(36), VEC1(37), VEC1(38), VEC1(39), VEC1(40)
PRINT "  int", VEC1(56), VEC1(57), VEC1(58), VEC1(59), VEC1(60)
PRINT "  ind", VEC1(36) - VEC1(56), VEC1(37) - VEC1(57), VEC1(38) - VEC1(58), VEC1(39) - VEC1(59), VEC1(40) - VEC1(60)

PRINT " "
PRINT "tau1 above must equal B7 in the fifth-order totals; the two reach it by"
PRINT "different routes and agree exactly on a system of spheres."

PRINT " "
PRINT "The third and fifth orders above are unconverted per surface - multiply by"
PRINT "the F/number, which is done for their totals. Check the third order against"
PRINT "Analyze > Aberrations > Seidel Coefficients and the fifth against FIFTHORD."



! =================================================================================
! STAGE F - the coefficient of QUATERNARY (ninth-order) spherical aberration.
!
! Buchdahl, "Optical Aberration Coefficients. IV. The Coefficient of Quaternary
! Spherical Aberration", J. Opt. Soc. Am. 48, 757 (1958). Its Table I is the
! fourteen rows below, and he says of them that "the computing scheme is quite
! brief, viz. only 14 entries of the usual kind per surface. It is constructed
! along the usual lines, and no further comment is required."
!
! WHY IT IS SO SHORT. Paper IV builds the quaternary as "an appendix to that for
! the set of tertiary coefficients", so every quantity it needs is already an entry
! of Table I. Stages A to D computed all of them; this stage only reads tt.
!
! IT NEEDS NO DUAL RUN, though Sec. 3 sounds as if it does. Sec. 3 says the one
! quantity in its Eq. (3.1) not explicit in the tertiary scheme is T1-dagger, and
! that obtaining it "requires T1q", through the identity M (21.7). Buchdahl then
! performs that reduction himself - the three rows feeding r3 below ARE M (21.7)
! written out, in p-side quantities alone. Reading Sec. 3 without the table sends
! an implementer looking for a second pass that is not wanted.
!
! THE TWO SIGMA CONVENTIONS DIFFER, and Buchdahl flags it because confusing them
! gives a plausible wrong number rather than an obvious one: the sum in the r3 row
! "exceptionally indicates the sum of the entries in the three preceding rows",
! while the sum in the last row follows the usual convention and takes the FOUR
! above it, the intrinsic row included. Both are written out here as explicit
! additions rather than as a running total, so neither can quietly gain a row.
!
! WHERE THE RESULTS GO. tt columns 241 to 248, which are free - stage D ends at
! 240 and the array is declared with 250.
!
!    241 r1, the accumulated tertiary spherical    244 r4
!    242 r2, its barred partner                    245 r5
!    243 r3, T1-dagger                             246 r6
!                                                  247 intrinsic q1p
!                                                  248 total q1p
!
! CHECKED AGAINST HIS OWN PRINTED TABLE. Paper IV computes all of this for the
! triplet Sigma1 and prints the six r rows beside the answer, so a wrong
! transcription shows up at a named row rather than at a total. The C# in this
! repository reproduces that table to a few parts in a million, against a system
! figure of -172968; this macro is the same fourteen rows reached through
! OpticStudio's paraxial data instead, so agreement between the two is a check
! across implementations rather than a repeat of one.
!
! UNITS. Buchdahl's, unconverted, as the per-surface third and fifth orders above
! are. His own rule of thumb from the closing paragraph: a system meant to work at
! f/2 should aim at individual contributions of at most order 1000 at unit focal
! length, and his triplet runs to six figures, which is why he chose it.
! =================================================================================

! THE COMPUTATION BELOW IS UNCONDITIONAL; q9 gates only the PRINTING further down.
! ROBB.ZPL reads the transverse total out of the call buffer, and it cannot set a
! variable in this macro to ask for it - ZPL does not document whether a child macro
! shares the parent's variables, which is exactly why the call buffer exists. So the
! coefficient is always formed and always published, and q9 decides only whether this
! macro prints it. That costs fourteen entries per surface on a table of two hundred
! and forty, which is nothing.

FOR i = 1, nsm, 1

   q9v2 = tt(i, 156)
   q9v9 = q9v2 * q9v2

   ! r1 and r2 - sums over the surfaces BEFORE this one, the convention t69 to
   ! t80 follow for the secondary and t15 to t19 for the primary. Guarded the way
   ! the t69 block above is, rather than trusting FOR m = 1, 0, 1 to do nothing.
   tt(i, 241) = 0
   tt(i, 242) = 0
   IF (i > 1)
      FOR m = 1, i - 1, 1
         tt(i, 241) = tt(i, 241) + tt(m, 132)
         tt(i, 242) = tt(i, 242) + tt(m, 133)
      NEXT
   ENDIF

   ! The three rows feeding r3, which are M (21.7) written out.
   q9a = ((tt(i, 83) - tt(i, 9) - tt(i, 20)) * tt(i, 15) + tt(i, 69)) * tt(i, 81) + tt(i, 6) * tt(i, 241) - tt(i, 242)
   q9b = ((0.5 * tt(i, 20) + tt(i, 9) - tt(i, 83)) * tt(i, 20) + tt(i, 9) * tt(i, 83) - tt(i, 86)) * tt(i, 9)
   q9c = (0.5 * tt(i, 15) * tt(i, 82) - tt(i, 6) * tt(i, 70) + tt(i, 102)) * tt(i, 15) + tt(i, 16) * tt(i, 86) - tt(i, 21) * tt(i, 69)

   ! r3 = T1-dagger. The sum here is the THREE rows above and nothing else.
   tt(i, 243) = (2.5 * tt(i, 83) - 3.0 * tt(i, 9)) * tt(i, 83) * tt(i, 83) + tt(i, 20) * tt(i, 70) + q9a + q9b + q9c

   ! The intrinsic coefficient, Eq. (2.15). t34 is Buchdahl's w and t10 his a_p,
   ! so the 280/8 below is the 35 that equation carries.
   tt(i, 244) = 0.25 * (q9v9 - tt(i, 9)) * tt(i, 10)
   tt(i, 245) = (17.0 * tt(i, 2) - 9.0 * q9v2) * tt(i, 7) * tt(i, 244)
   tt(i, 246) = (41.0 * q9v2 - 87.0 * tt(i, 2)) * tt(i, 7) * tt(i, 38) / 3.0

   tt(i, 247) = (280.0 * tt(i, 34) * tt(i, 34) * tt(i, 34) + tt(i, 245) + tt(i, 246)) * tt(i, 10) / 8.0

   ! The induced rows of Eq. (3.1). t8 is omega-tilde, t25 is A-dagger, t101 is
   ! S1-dagger, t121 is the intrinsic tertiary spherical t1p, and t122 is z2 -
   ! the one mu = 2 entry the paper keeps, every other one being irrelevant here.
   q9d = ((-0.5 * tt(i, 8) * tt(i, 25) - tt(i, 48) + tt(i, 49)) * tt(i, 15) - tt(i, 8) * tt(i, 69) + tt(i, 122)) * tt(i, 15)
   q9e = ((tt(i, 25) * tt(i, 25) + 6.0 * tt(i, 101)) * tt(i, 25) + 3.0 * tt(i, 243)) * tt(i, 10)
   q9f = (4.0 * tt(i, 15) * tt(i, 43) + 7.0 * tt(i, 121)) * tt(i, 25) + tt(i, 43) * tt(i, 69)

   ! And the last row, whose sum is the FOUR above it - the intrinsic included.
   tt(i, 248) = (2.0 * tt(i, 25) * tt(i, 25) + tt(i, 101)) * 5.0 * tt(i, 38) + tt(i, 247) + q9d + q9e + q9f

NEXT

q9sum = 0
FOR i = 1, nsm, 1
   q9sum = q9sum + tt(i, 248)
NEXT

! ---------------------------------------------------------------------------------
! INTO TRANSVERSE MEASURE. THIS WAS THE ONE UNCHECKED STEP AND IT IS NOW CHECKED.
!
! Stage D puts each tau into transverse measure by efac * um^a * hmax^b, where a is
! its aperture degree and b its field degree - VEC1(1) to VEC1(20) above. That rule is
! confirmed at twenty points, a running from seven down to zero, and at a = 7, b = 0 it
! agrees with sB7 * fnum, which reaches the same number by a different route.
!
! Ninth-order spherical is a = 9, b = 0, so the rule extends to efac * um^9. It is the
! natural continuation and nothing here contradicts it - but every point that confirms
! the rule has total degree SEVEN, and this has degree nine. Whether Buchdahl's
! quaternary coefficient is augmented in the same sense that M (31.11)'s single factor
! 1/(N_k' v_pk') assumes is not something this file can settle.
!
! MEASURED ON KINGSLAKE'S DOUBLE GAUSS, and it holds. The printed ratio of the two lines
! below came to -1.454733E-09, and that factors exactly into quantities taken from the
! prescription alone: um = -(EPD/2)/EFL = -0.06249754 gives um^9 = -1.454676E-11, which
! leaves efac = 100.00395 against an EFL of 100.00394 - so vpk = 1.000000, which is what
! it must be, the p ray starting at unit height and leaving at unit angle in units of the
! focal length. Six significant figures, from numbers that know nothing about this stage.
!
! The unconverted value is still printed beside the converted one. The check above cost
! nothing to make because both were there to divide.
! ---------------------------------------------------------------------------------

up9 = up7 * um * um
q9t = q9sum * efac * up9

IF (q9 == 1)

   PRINT " "
   PRINT "Ninth order - quaternary spherical aberration, per surface"
   PRINT "Buchdahl IV, J. Opt. Soc. Am. 48, 757 (1958), Table I. Unconverted, as the"
   PRINT "third and fifth orders above are. INTRINSIC is what the surface makes on its"
   PRINT "own; TOTAL adds what it makes by acting on the aberration already reaching it."
   FORMAT 15.6 EXP
   PRINT "  surf      intrinsic          total      T1-dagger             r1             r2"
   FOR i = 1, nsm, 1
      FORMAT 5.0 INT
      PRINT i,
      FORMAT 15.6 EXP
      PRINT tt(i, 247), tt(i, 248), tt(i, 243), tt(i, 241), tt(i, 242)
   NEXT

   PRINT " "
   FORMAT 15.6 EXP
   PRINT "Ninth-order spherical, unconverted     ", q9sum
   PRINT "Ninth-order spherical, transverse (B9) ", q9t
   PRINT " "
   PRINT "The transverse value carries efac * um^9. That extends a rule confirmed at total"
   PRINT "degree seven, and the extension was checked: the ratio of the two lines above"
   PRINT "factors into -(EPD/2)/EFL raised to the ninth and efac = EFL, to six figures."

   PRINT " "
   PRINT "Buchdahl's rule of thumb, from the closing paragraph of paper IV: a system"
   PRINT "intended to work at f/2 should aim at individual contributions of at most"
   PRINT "order 1000 at unit focal length. His own triplet runs to six figures, which"
   PRINT "is why he chose it - it is a poorly corrected system and the table shows it."

ENDIF

! =================================================================================
! STAGE E - publish the coefficients into the call buffer, so that a parent macro
! can pick them up. ROBB.ZPL is the one that does: it calls this macro and turns
! what comes back into an RMS spot radius by Robb's analytic merit function,
! J. Opt. Soc. Am. 66, 1037 (1976).
!
! This macro does NOT call ROBB. The direction is the other way round, which is what
! makes both files runnable on their own: BUCH7 by itself prints the coefficients as
! it always has, and ROBB by itself prints a spot, calling this macro to get what it
! needs. Were both to call each other the pair would recurse until OpticStudio gave
! up, so the call lives in exactly one of them.
!
! Run directly, the writes below go into this macro's own buffer and nobody reads
! them, which costs nothing and changes none of the output above.
!
! Thirty-four of the thirty-seven coefficients go across. E, E5 and tau20 are left
! behind on purpose: distortion displaces the image patch without resizing it, so it
! cannot enter a spot radius, and Robb's corresponding terms vanish identically.
!
! Everything sent is in TRANSVERSE measure, which for the third and fifth orders
! means the running sums multiplied by the F/number - the same conversion applied to
! the totals printed above - and for the seventh order means VEC1(21) to VEC1(39),
! which stage D already converted per tau. Sending the unconverted per-surface
! quantities instead would give a spot in no units at all.
!
! The call buffer holds fifty-one numeric slots. Thirty-four carry the third, fifth
! and seventh orders, slot 47 the ninth, and slots 45, 46 and 48 are control values -
! so the whole set fits with room to spare. Slot 45 carries a handshake, so that a
! caller can tell the difference between coefficients that are genuinely zero and a run
! of this macro that never got here; 46 and 48 say whether the seventh and the ninth
! order are present at all.
!
! This stage is reached only if stage D completed. The earlier GOTO 999 exits -
! an afocal system, a chief ray with no field - skip it, and slot 45 is then never
! written, which is exactly how the caller learns that there is nothing to use.
! =================================================================================

CALLSETDBL 1, sumb * fnum
CALLSETDBL 2, sumf * fnum
CALLSETDBL 3, sumc * fnum
CALLSETDBL 4, sump * fnum

CALLSETDBL 5, sB5 * fnum
CALLSETDBL 6, sF1 * fnum
CALLSETDBL 7, sF2 * fnum
CALLSETDBL 8, sM1 * fnum
CALLSETDBL 9, sM2 * fnum
CALLSETDBL 10, sM3 * fnum
CALLSETDBL 11, sN1 * fnum
CALLSETDBL 12, sN2 * fnum
CALLSETDBL 13, sN3 * fnum
CALLSETDBL 14, sC5 * fnum
CALLSETDBL 15, sP5 * fnum

! B7 is Robb's tau1, and stage D holds it already converted. It is reached twice
! over inside this macro - through the fifth-order working and through Table I - so
! VEC1(21) and sB7 * fnum are the same number by two routes. Sending VEC1(21) keeps
! it consistent with tau2 onward, which have no second route.
CALLSETDBL 16, VEC1(21)

FOR rbk = 2, 19, 1
   CALLSETDBL rbk + 15, VEC1(20 + rbk)
NEXT

! The ninth order, which stage F formed unconditionally. Slot 48 mirrors slot 46:
! it lets a caller tell a genuine zero from a run that never reached stage F.
!
! SLOT 47 CARRIES THE ONE CONVERSION IN THIS FILE WITHOUT A SECOND ROUTE - see stage F
! for what efac * um^9 rests on. Everything else published here is confirmed twice.
CALLSETDBL 47, q9t
CALLSETDBL 48, 1

! Written last, so that it is set only if everything above it was.
CALLSETDBL 45, 761976
CALLSETDBL 46, 1

LABEL 999
IF (ABSO(paraxmode) < eps) THEN PARAXIAL OFF
END
