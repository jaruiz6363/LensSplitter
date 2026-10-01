! BUCH7_ASPH - third-, fifth- and seventh-order aberration coefficients of a centred
! system that MAY CARRY CONICS AND EVEN ASPHERES, by Buchdahl's computing scheme.
!
! Written by Javier Ruiz with Claude Code, September 2026.
!
! Buchdahl, "Optical Aberration Coefficients", Dover 1968, Secs. 60, 65, 77-79 and
! 84-85; papers II (JOSA 48, 563) and III (JOSA 48, 747); paper XII (JOSA 73, 782 is
! Forbes - XII is JOSA 65) for the duality of Sec. 6.
!
! WHAT THIS IS, AND WHY IT IS A SECOND FILE
!
!   BUCH7.ZPL does this job for SPHERICAL surfaces and refuses a conic or an aspheric
!   term outright, because Buchdahl gives the aspheric scheme in Sec. 85 but never
!   published the arranged table for it. That arrangement has since been reconstructed,
!   and agrees with Forbes' order-doubling series trace to between 2E-13 and 2E-10 on
!   every figured refracting design it has been put to (Forbes declines a mirror); this
!   macro is that arrangement. BUCH7.ZPL
!   is left exactly as it was - the spherical routine is the one checked against
!   Buchdahl's own printed numbers, and it is not disturbed.
!
!   On a lens of spheres this macro reproduces BUCH7 entry for entry. That is the
!   first check to make and it costs nothing: run both on a spherical file.
!
! THE ARRANGEMENT, IN ONE PARAGRAPH
!
!   Sec. 85 splits every tertiary total into a hat pass, carried on the incidence
!   ratio q, and a check pass, carried on the height ratio rho. Three things that are
!   not in the printed scheme are needed to make it come out:
!
!     1. Members one to five of the barred q-side secondary accumulation come from the
!        identities of M Sec. 22, not from the dagger recursion.
!     2. The sixth member, which the identities cannot supply (M Sec. 19), comes from
!        a DUAL run of the whole scheme - the two rays interchanged and the refractive
!        indices negated, XII Sec. 6 - in which the accumulated s_1p is -S-bar_6q.
!     3. The figuring's D half of (60.3) rides the incidence ratio, so it belongs in
!        the hat pass, in the intrinsic secondaries AND in the five M entries that
!        carry them with coefficient one.
!
!   Against Forbes' order-doubling series trace, which shares no arithmetic with it,
!   this arrangement agrees to between 2E-13 and 2E-10 on every figured refracting
!   design it has been put to. On a mirror, which Forbes declines, it has been checked
!   against the C# program and reflected real rays instead (F4, F10).
!
! HOW TO CHECK IT ON YOUR OWN LENS, AND PLEASE DO
!
!   Everything below is available inside OpticStudio for whatever file you are
!   running. None of it is particular to any lens.
!
!   THE THIRD ORDER against Analyze > Aberrations > Seidel Coefficients, which
!   carries aspheres. Convert its sums to transverse measure - W040 = SI/8,
!   W131 = SII/2, W222 = SIII/2, W220 = SIV/4, W311 = SV/2 - then differentiate the
!   wavefront and divide by n'u'.
!
!   THE THIRD AND FIFTH ORDERS AND B7 against FIFTHORD.ZPL, which ships with
!   OpticStudio and carries aspheres: it prints the same eighteen transverse totals
!   stage B prints, and on a figured lens they must agree - UNLESS A SURFACE CARRIES
!   A NON-ZERO PARM 1. See the next block. The macro refuses the comparison in its
!   own output when it finds one, rather than leaving you to discover it.
!
!   THE SEVENTH ORDER against FORBES.ZPL in this same folder, which reaches tau1 to
!   tau20 by order doubling and shares no arithmetic with the scheme below - on a
!   refracting system. FORBES declines a mirror; there RAYINV.ZPL, from real rays, is
!   the check, and the closing note says so. Of the three references above, FORBES is
!   the only one that reaches tau2 to tau20 on a figured lens; RAYINV reaches them too.
!
!   ON A LENS OF SPHERES this macro and BUCH7.ZPL must agree entry for entry: none of
!   the aspheric machinery is reached there, so a disagreement is a fault in this file
!   rather than anything to do with the aspheric arrangement.
!
! THE r^2 TERM, PARM 1, IS HANDLED HERE AND IS NOT HANDLED BY FIFTHORD
!
!   PARM 1 is not figuring. A surface of curvature c carrying A2 is exactly the sphere
!   of curvature c + 2*A2 carrying whatever departure is left over, so the term changes
!   the surface's POWER - it is a radius in disguise, where r^4 and above are genuine
!   departures from a sphere. This macro folds it into the vertex curvature, zeroes the
!   conic, and re-measures A4, A6 and A8 from THAT sphere, which is the block headed
!   "Vertex form, per surface" below.
!
!   THIS MACRO IS ACCURATE WHEN A2 IS NON-ZERO, and that is measured rather than
!   claimed. On a test design built for it - a conic with A4, A6, A8 and PARM 1 = 1E-4,
!   where the term moves the focal length from 78.0375 to 77.4194 - this macro agrees
!   to every printed digit, all eighteen totals and all twenty tau, with an independent
!   C# implementation that is itself corroborated three ways: by Buchdahl's scheme
!   folding the term as above, by Forbes' series trace which does no folding at all and
!   carries A2 as the first coefficient of the sag series, and by coefficients recovered
!   from real traced rays. OpticStudio's own focal length agrees with all of them, which
!   is also what establishes that the paraxial data this macro reads accounts for PARM 1.
!
!   FIFTHORD IS NOT ACCURATE THERE. It is not that it ignores the term and returns the
!   answer for the surface without it, which would be incomplete but defensible. It
!   takes its paraxial ray data from OpticStudio, which includes the power the term
!   adds, and then computes each surface's contribution from the base curvature - so
!   the two halves describe different surfaces and the result is neither lens. On the
!   test design its B is 2.5314E-02, against 2.7696E-02 with the term and 2.6888E-02
!   without. On an unfigured surface of the same file it still agrees with this macro
!   to all five printed digits, and on the figured one its Petzval differs by exactly
!   the ratio of the two curvatures, which is what locates the cause.
!
!   Its author does flag the term - "Zemax uses a second-order aspheric deformation
!   coefficient which is not used in this treatment" - and that is candid, and the
!   macro was written in 1998 and given away freely. But the note describes an
!   omission, and what happens is an inconsistency, so it reads as weaker than the
!   situation warrants. Hence the refusal this macro prints instead of the usual
!   invitation to compare.
!
! THREE SWITCHES, all off by default and all about the lens in front of you
!
!   srf = 1 breaks every coefficient down SURFACE BY SURFACE, and splits the third
!   and fifth order into what the surface generates on its own, what its figuring
!   adds, and what was INDUCED in it by the surfaces ahead. A total says a design is
!   wrong; this says where to go and look, and whether the fault is that surface's
!   own. Each column sums down to the system total printed above it, which is a check
!   you can make by eye. See the note above the printout for what it does not do.
!
!   chk = 1 prints the bridge constants that carry the figuring into the scheme.
!   They are measured per quantity and must come out the SAME ON EVERY SURFACE of
!   your lens; if they do not, the coefficients for that lens are not to be trusted.
!
!   dbg = 1 prints Table I entry by entry for your lens. It is verbose - 155 rows per
!   surface - and it is what to turn on when a coefficient looks wrong and you want to
!   see which entry it came from.
!
! LIMITS, STATED PLAINLY
!
!   STANDARD and EVENASPH surfaces only, and only PARM 1 to 4 - r^2 to r^8. An r^10
!   term or above cannot reach the seventh order; it is not ignored, it genuinely does
!   not appear. Any other surface type is declined BY NAME, because PARM on a toroid
!   is not an aspheric coefficient.
!
!   A FIGURED FLAT FACING COLLIMATED LIGHT IS DECLINED. There the marginal incidence
!   is zero, q is infinite, and the finite coefficients arrive only after terms
!   carrying different powers of q cancel. The C# gets it by running the whole chain
!   in Laurent series arithmetic with that surface's curvature as the variable, which
!   is not a thing ZPL can do. Bend the surface and it is exact again: at R = 100 the
!   C# agrees with Forbes to 1.6E-12. The macro says so rather than returning a number.
!
!   Rotationally symmetric and sequential. Either conjugate.
!
!   IT IS SLOW. Four full passes of the scheme - direct and dual, each unfigured then
!   figured - and a trivariate polynomial algebra for the figuring cubics.
!
! THE ZPL TRAPS THIS FILE HAD TO RESPECT
!
!   VARIABLE NAMES ARE CASE-INSENSITIVE and there are no locals. Every subroutine's
!   working variables carry a prefix of its own and no other subroutine uses them.
!   A subroutine that calls another copies its arguments first.
!
!   THERE IS NO LINE CONTINUATION. A line beginning with + is a new statement. Long
!   expressions accumulate into their own target.
!
!   FOR does not skip when the end is below the start, so every empty sum is guarded.
!
! ---------------------------------------------------------------------------------
! The copy in the OpticStudio macro folder is kept in step with this one by a
! PostToolUse hook in .claude/settings.local.json; the repository is the original.
! ---------------------------------------------------------------------------------

paraxmode = PMOD()
PARAXIAL ON
eps = 1E-10
wl = PWAV()

! Print the self-checks. Leave it on: they are cheap and they are the only evidence
! inside the macro that the arrangement is being fed what it thinks it is.
chk = 0

! Table I entry by entry, for the lens being run, in the same format BUCH7 uses so
! the two can be set side by side. Verbose - 155 rows per surface - and off unless a
! coefficient looks wrong and you want to see which entry it came from, where a
! single wrong entry shows as a single wrong line rather than as a wrong coefficient
! twenty steps downstream.
dbg = 0

! Surface by surface, with the third and fifth order split into intrinsic, figuring
! and induced. This is the printout a designer works from rather than a diagnostic:
! the totals say the design is wrong and this says which surface, and whether that
! surface is generating the aberration or only reacting to what reached it. The two
! call for opposite actions, which is the whole reason for separating them.
srf = 0

ns = NSUR()
nsm = ns - 1
! ONE optical surface is enough, and that is not a relaxation for its own sake: a
! single figured surface is the sharpest test the aspheric block has, because nothing
! is accumulated ahead of it and every induced term is identically zero, so a fault
! cannot hide in the induced part. BUCH7 asks for two; all this one needs is that the
! file hold an object, a surface and an image. The loops below all run from 1 to nsm
! and are empty-safe at nsm = 1.
IF (nsm < 1)
   PRINT "BUCH7_ASPH needs at least one optical surface between object and image."
   GOTO 900
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
   ! An even asphere may carry figuring past r^8. It cannot reach the seventh order,
   ! but a reader is owed the fact that it was dropped rather than left out.
   FOR j = 5, 8, 1
      IF (ABSO(PARM(j, i)) > eps)
         FORMAT 5.0
         PRINT "NOTE: surface ", i, " parameter ", j, " is beyond r^8 and cannot"
         PRINT "reach the seventh order, so it takes no part in what follows."
      ENDIF
   NEXT
NEXT

IF (badtyp == 1)
   PRINT " "
   PRINT "BUCH7_ASPH carries STANDARD and EVENASPH surfaces. On any other type the"
   PRINT "parameter columns mean something other than aspheric coefficients, and"
   PRINT "reading them as if they did would give a confident wrong answer rather"
   PRINT "than an error - so it declines instead."
   GOTO 900
ENDIF

GETSYSTEMDATA 1
efl = VEC1(7)
epp = VEC1(12)
IF (ABSO(efl) < eps)
   PRINT "The system has no focal length; an afocal system is not handled."
   GOTO 900
ENDIF

SETVECSIZE 4000

! ---------------------------------------------------------------------------------
! ZPL PROVIDES VEC1 TO VEC4 AND NO MORE. Everything else this macro needs is a
! DECLAREd array. Writing VEC6(i) = x does not fail as an out-of-range vector - the
! parser stops at "Variable must be followed by = sign", because VEC6 is read as an
! ordinary variable name and the bracket after it is then unexpected. That is a
! sixth ZPL trap and it is worth stating plainly, because the message names neither
! the vector nor the real fault.
!
!   vfm   per surface, the VERTEX FORM: curvature, conic, A4, A6, A8, figured flag
!   cub   the ten cubic or six quadratic coefficients a poly extraction returns
!   mon   the monomial tables of the trivariate algebra
!   scr   the multiply's accumulator, which cannot be one of the operands
! ---------------------------------------------------------------------------------

DECLARE vfm, DOUBLE, 2, nsm + 2, 8
DECLARE cub, DOUBLE, 2, 2, 12
DECLARE mon, DOUBLE, 2, 6, 70
DECLARE scr, DOUBLE, 2, 2, 68

! ---------------------------------------------------------------------------------
! The paraxial rays, traced once and stored. RAYY and RAYM report only the ray traced
! last, so both are kept.
!
!   VEC2(i + 1)     marginal height at surface i
!   VEC2(501 + i)   marginal angle in the space AFTER surface i
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

n0 = INDX(0)
IF (ISMS(0)) THEN n0 = -n0
lag = n0 * (VEC3(501) * VEC2(2) - VEC2(501) * VEC3(2))

! The object medium's index, magnitude only. The tertiary pass needs it twice:
! Buchdahl's p and q rays must carry a Lagrange invariant of ONE, and theirs comes out
! as exactly this number. See the note at the ray start. The sign is dropped because
! it belongs to the mirror count, not to the medium.
n0abs = ABSO(n0)
IF (n0abs < eps) THEN n0abs = 1
IF (ABSO(lag) < eps)
   PRINT "The Lagrange invariant is zero. Is the field zero, or the aperture?"
   GOTO 900
ENDIF

p0 = epp / efl
pupf = VEC2(501 + nsm)
! The F/number takes the RAW index, WITHOUT the mirror sign flip applied inside the
! surface loop. On a system with an odd number of mirrors the signed index would flip
! the F/number, and with it every printed total - which is exactly the case of a single
! parabolic mirror, where the reference F/number is -2.5 and the flip would give +2.5.
nlast = ABSO(INDX(nsm))
fnum = -0.5 / nlast / pupf

! The seventh order's conversion factor 1/(N'_k v'_pk) takes the MAGNITUDE too. This
! used to take the signed index, "as the scheme sees it, mirror sign and all" - and on a
! system with an odd number of mirrors that put the seventh order in the opposite frame
! from the third and fifth printed beside it, which the F/number above scales by the
! magnitude. Measured on the parabola in the C# program (September 2026), which had the
! same split: with the signed index every tau came out as minus what reflected real rays
! give, while the third and fifth agreed with them; with the magnitude all three orders
! agree in one frame. ROBB-style spot sums multiply the orders together, so the frame has
! to be one frame.
nlsig = nlast

objd = THIC(0)
finite = 0
iota = 0
IF (ABSO(objd) < 1E10)
   finite = 1
   iota = -efl / objd
ENDIF
goe = 1 - p0 * iota
IF (ABSO(goe) < 1E-12) THEN goe = 1

! At a finite conjugate the entrance pupil in focal lengths is the stop parameter;
! at infinity the scheme's own q/p height ratio at the stop is kept, so that results
! validated there stay bit-identical. Both are p0 here because ZPL reads the entrance
! pupil position directly.
stopp = p0

! ---------------------------------------------------------------------------------
! Vertex form, per surface. Any r-squared coefficient is folded into the curvature and
! the figuring measured from THAT sphere: reading a raw conic against a shifted vertex
! measures it from a sphere the surface does not have, and it shows in B and nowhere
! else.
!
!   vfm(i, 1)        vertex curvature
!   vfm(i, 2)  conic in vertex form
!   vfm(i, 3)  A4      vfm(i, 4) A6      vfm(i, 5) A8
!   vfm(i, 6)  1 when the surface is figured at all
! ---------------------------------------------------------------------------------

anyfig = 0
anyr2 = 0
FOR i = 1, nsm, 1
   vcv = CURV(i)
   vkk = CONI(i)
   va2 = PARM(1, i)
   va4 = PARM(2, i)
   va6 = PARM(3, i)
   va8 = PARM(4, i)

   IF (ABSO(va2) > 0)
      anyr2 = 1
      vc2 = vcv * vcv
      vc3 = vc2 * vcv
      vc5 = vc3 * vc2
      vc7 = vc5 * vc2
      vk1 = 1 + vkk
      vt2 = vk1 * vc3 / 8 + va4
      vt3 = vk1 * vk1 * vc5 / 16 + va6
      vt4 = 5 * vk1 * vk1 * vk1 * vc7 / 128 + va8
      vv = vcv + 2 * va2
      vv2 = vv * vv
      vv3 = vv2 * vv
      vv5 = vv3 * vv2
      vv7 = vv5 * vv2
      vfm(i, 1) = vv
      vfm(i, 2) = 0
      vfm(i, 3) = vt2 - vv3 / 8
      vfm(i, 4) = vt3 - vv5 / 16
      vfm(i, 5) = vt4 - 5 * vv7 / 128
   ELSE
      vfm(i, 1) = vcv
      vfm(i, 2) = vkk
      vfm(i, 3) = va4
      vfm(i, 4) = va6
      vfm(i, 5) = va8
   ENDIF

   vfm(i, 6) = 0
   IF (ABSO(vfm(i, 2)) > 1E-14) THEN vfm(i, 6) = 1
   IF (vfm(i, 3) != 0) THEN vfm(i, 6) = 1
   IF (vfm(i, 4) != 0) THEN vfm(i, 6) = 1
   IF (vfm(i, 5) != 0) THEN vfm(i, 6) = 1
   IF (vfm(i, 6) == 1) THEN anyfig = 1
NEXT

FORMAT 12.6
PRINT "BUCH7_ASPH - Buchdahl coefficients, conics and even aspheres carried"
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
IF (anyfig == 0)
   PRINT "Nothing is figured: this run is the spherical routine, and every number"
   PRINT "below must equal BUCH7's on the same file."
ENDIF
PRINT " "

! =================================================================================
! STAGE A and B - the third and fifth orders, with the aspheric contributions.
!
! This is BUCH7's stages A and B with Buchdahl's aspheric block of Sec. 77 added, and
! it is run TWICE: once on the rays as traced, and once on the rays interchanged with
! the indices negated, which is what the dual run of stage C needs its figuring in.
!
! Per surface and per ray-set, what is carried forward is the SEVEN quantities the
! scheme's bridge consumes - B, B5, F2, M2, M3, N3, Pi5 + C5 - intrinsic and aspheric
! kept apart, because the bridge is measured on the first and applied to the second.
!
!   mq(rs, 1..7)   the seven intrinsic, mq(rs, 8..14) the seven aspheric
!   and so on, seven apart; see the table below.
! =================================================================================

DECLARE mq, DOUBLE, 2, 2 * nsm + 4, 18

! The induced part and the printed totals are wanted for the DIRECT ray-set only.
DECLARE fo, DOUBLE, 2, nsm + 2, 60

FOR rs = 1, 2, 1

   ! Running sums over the surfaces already passed.
   acb = 0
   acf = 0
   acc = 0
   ace = 0
   acpi = 0
   acbb = 0
   acfb = 0
   accb = 0
   aceb = 0
   acb5 = 0
   ace5b = 0

   FOR i = 1, nsm, 1

      cv = vfm(i, 1)
      nb = INDX(i - 1)
      na = INDX(i)
      IF (ISMS(i - 1)) THEN nb = -nb
      IF (ISMS(i)) THEN na = -na
      IF (rs == 2)
         nb = -nb
         na = -na
      ENDIF
      IF (ABSO(na) < eps) THEN na = 1
      kk = nb / na
      km1 = kk - 1
      deln = nb - na

      ! The interchange of XII Sec. 6(iii): the p ray takes the q ray's data and back.
      IF (rs == 1)
         py = VEC2(i + 1)
         pu = VEC2(500 + i)
         pup = VEC2(501 + i)
         pyc = VEC3(i + 1)
         puc = VEC3(500 + i)
         pucp = VEC3(501 + i)
      ELSE
         py = VEC3(i + 1)
         pu = VEC3(500 + i)
         pup = VEC3(501 + i)
         pyc = VEC2(i + 1)
         puc = VEC2(500 + i)
         pucp = VEC2(501 + i)
      ENDIF

      pai = cv * py + pu
      paip = pai * kk
      paic = cv * pyc + puc
      paicp = paic * kk

      Pz = cv * km1 * lag * lag / nb
      sfm = nb * km1 * py * (pai + pup)
      sfc = nb * km1 * pyc * (paic + pucp)

      Ba = sfm * pai * pai
      Fa = sfm * pai * paic
      Ca = sfm * paic * paic
      Bb = sfc * paic * paic
      Fb = sfc * pai * paic
      Cb = sfc * pai * pai
      Ea = Fb + lag * km1 * paic * (pucp + puc)
      Eb = Fa - lag * km1 * pai * (pup + pu)

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

      ! The seven the bridge is measured on: intrinsic, before anything is added.
      mb = (rs - 1) * (nsm + 2) + i
      mq(mb, 1) = Ba
      mq(mb, 2) = iB5
      mq(mb, 3) = iF2
      mq(mb, 4) = iM2
      mq(mb, 5) = iM3
      mq(mb, 6) = iN3
      mq(mb, 7) = iP5 + iC5
      FOR j = 8, 14, 1
         mq(mb, j) = 0
      NEXT

      ! The figuring's own share of each printed coefficient, zeroed here so that a
      ! spherical surface reports zero rather than whatever the last figured surface
      ! left behind. srf = 1 prints these; nothing else reads them.
      aBa = 0
      aFa = 0
      aCa = 0
      aEa = 0
      aB5 = 0
      aF1 = 0
      aF2 = 0
      aM1 = 0
      aM2 = 0
      aM3 = 0
      aN1 = 0
      aN2 = 0
      aN3 = 0
      aC5 = 0
      aP5 = 0
      aE5 = 0
      aB7 = 0

      ! ---------------------------------------------------------------------------
      ! The aspheric figuring, M Sec. 77. Every term carries c1b, c2b or c3b and so
      ! vanishes on a sphere; a figured sphere has c1 = 0 and leaves the primary
      ! untouched, which is the class the ladder fixtures separate.
      ! ---------------------------------------------------------------------------
      IF (vfm(i, 6) == 1)
         conic = vfm(i, 2)
         aterm = vfm(i, 3)
         bterm = vfm(i, 4)
         cterm = vfm(i, 5)

         cv2 = cv * cv
         cv3 = cv2 * cv
         c1 = 8 * aterm + conic * cv3
         c2 = 12 * bterm + 0.75 * cv2 * (cv3 * conic * (conic + 2) - 2 * c1)
         tmp = cv3 * conic * (conic * conic + 3 * conic + 3) - 3 * c1
         tmp = cv2 * (5 * cv2 * tmp - 12 * c2)
         tmp = (-6 * cv * c1 * c1 + tmp) / 8
         c3 = 16 * cterm + tmp

         c1b = deln * c1
         c2b = deln * c2
         c3b = deln * c3
         pysq = py * py
         pycsq = pyc * pyc

         aBa = c1b * pysq * pysq
         aFa = c1b * pysq * py * pyc
         aEb = aFa
         aCa = c1b * pysq * pycsq
         afgCb = aCa
         aEa = c1b * py * pycsq * pyc
         aFb = aEa
         aBb = c1b * pycsq * pycsq

         Ba = Ba + aBa
         Fa = Fa + aFa
         Ca = Ca + aCa
         Ea = Ea + aEa
         Bb = Bb + aBb
         Fb = Fb + aFb
         Cb = Cb + afgCb
         Eb = Eb + aEb

         mm = kk * lag / na
         la = (3 * paip - 2 * (1 - 2 * kk) * pup) / 4
         lb = (3 * paicp - 2 * (1 - 2 * kk) * pucp) / 4

         aS1p1 = aBa * la
         aS2p1 = 2 * aFa * la + 0.5 * c1b * pysq * py * mm
         aS3p1 = 2 * aCa * la + c1b * pysq * pyc * mm
         aS4p1 = 2 * aS3p1
         aS5p1 = 2 * aEa * la + 1.5 * c1b * py * pycsq * mm
         aS6p1 = aBb * la + c1b * pycsq * pyc * mm
         aS1q1 = aBa * lb - c1b * pysq * py * mm

         j0a = c2b * py - 0.25 * cv * c1b * (3 * paip - 5 * pup)
         j0b = c2b * pyc - 0.25 * cv * c1b * (3 * paicp - 5 * pucp)

         alph = 0.5 * (pup * (pup - pai) + pai * (3 * paip - pup))
         beta = pup * (pucp - paic) + pai * (3 * paicp - pucp)
         gamm = 0.5 * (pucp * (pucp - paic) + paic * (3 * paicp - pucp))

         lamb = alph * c1b + j0a * py
         muu = beta * c1b + 2 * j0a * pyc
         nuu = py * gamm * c1b + j0a * pycsq

         aS1p2 = pysq * py * lamb
         aS2p2 = pysq * pyc * lamb + 0.5 * pysq * py * muu
         aS3p2 = py * pycsq * lamb + pysq * nuu
         aS4p2 = 2 * pysq * pyc * muu
         aS5p2 = 0.5 * py * pycsq * muu + py * pyc * nuu
         aS6p2 = pycsq * nuu
         aS1q2 = pysq * (alph * c1b * pyc + j0b * pysq)

         aS1pa = pai * aS1p1 + py * aS1p2
         aS2pa = pai * aS2p1 + py * aS2p2
         aS3pa = pai * aS3p1 + py * aS3p2
         aS4pa = pai * aS4p1 + py * aS4p2
         aS5pa = pai * aS5p1 + py * aS5p2
         aS6pa = pai * aS6p1 + py * aS6p2

         aS1pb = paic * aS1p1 + pyc * aS1p2
         aS2pb = paic * aS2p1 + pyc * aS2p2
         aS3pb = paic * aS3p1 + pyc * aS3p2
         aS4pb = paic * aS4p1 + pyc * aS4p2
         aS5pb = paic * aS5p1 + pyc * aS5p2
         aS6pb = paic * aS6p1 + pyc * aS6p2

         aS1qa = pai * aS1q1 + py * aS1q2

         aB5 = aS1pa
         aF1 = aS1pb + aS2pa
         aF2 = aS2pa
         aM1 = 2 * aS2pb
         aM2 = aS3pa
         aM3 = aS4pa
         aN1 = aS3pb
         aN2 = aS4pb + 2 * aS5pa
         aN3 = aS5pa
         aC5 = 0.5 * aS5pb
         aP5 = aS6pa - 0.5 * aS5pb
         aE5 = aS6pb
         aE5b = aS1qa

         iB5 = iB5 + aB5
         iF1 = iF1 + aF1
         iF2 = iF2 + aF2
         iM1 = iM1 + aM1
         iM2 = iM2 + aM2
         iM3 = iM3 + aM3
         iN1 = iN1 + aN1
         iN2 = iN2 + aN2
         iN3 = iN3 + aN3
         iC5 = iC5 + aC5
         iP5 = iP5 + aP5
         iE5 = iE5 + aE5
         iE5b = iE5b + aE5b

         gam1 = c1 * pysq
         gam2 = py * pysq * (c2 * py + 0.25 * cv * c1 * (pai + 3 * pu))
         tq = cv2 * c1 * (pai * (pai + 5 * pu) - pu * (pai - 5 * pu)) / 8
         ! HALF what (77.2) prints for both c1-squared terms, and deliberately so: a
         ! parabola images infinity perfectly, so its spherical aberration must vanish
         ! at every order. It does with these values and does not with the printed ones.
         tq = tq + c1 * c1 * py * pai / 4
         gam3 = pysq * pysq * (tq + c3 * pysq + cv * c2 * py * (pai + 5 * pu) / 3)

         d3 = cv * py * (4 * cv * py * (pai + pu) + paip * (5 * (2 * pup + pai) + paip))
         t2q = kk * pup * (3 * pai * pai - 10 * pu * pu + paip * (4 * (2 * pup + paip + pai) + pai))
         d3 = c1 * (gam1 * py * (1 + 2 * kk * km1) + d3 + t2q) / 8
         d3 = deln * pysq * pysq * (c2 * pysq * (4 * paip + 3 * pup * (2 * kk - 1)) / 6 + d3)

         lq3 = py * gam3 * deln + 0.5 * (gam1 * (q1p + aS1p1) + gam2 * sfm * pai)
         aB7 = pai * d3 + py * lq3
         iB7 = iB7 + aB7

         mq(mb, 8) = aBa
         mq(mb, 9) = aB5
         mq(mb, 10) = aF2
         mq(mb, 11) = aM2
         mq(mb, 12) = aM3
         mq(mb, 13) = aN3
         mq(mb, 14) = aP5 + aC5
      ENDIF

      ! ---------------------------------------------------------------------------
      ! The induced corrections, which are needed for the printed totals only. The
      ! dual ray-set does not report, so this is skipped there.
      ! ---------------------------------------------------------------------------
      IF (rs == 1)
         fo(i, 1) = Ba
         fo(i, 2) = Fa
         fo(i, 3) = Ca
         fo(i, 4) = Pz
         fo(i, 5) = Ea
         fo(i, 10) = iB5
         fo(i, 11) = iF1
         fo(i, 12) = iF2
         fo(i, 13) = iM1
         fo(i, 14) = iM2
         fo(i, 15) = iM3
         fo(i, 16) = iN1
         fo(i, 17) = iN2
         fo(i, 18) = iN3
         fo(i, 19) = iC5
         fo(i, 20) = iP5
         fo(i, 21) = iE5
         fo(i, 22) = iB7

         ! The figuring's share, for srf = 1. Slots 1 to 5 and 10 to 22 above are
         ! the surface's own sphere PLUS its figuring, which is the form the induced
         ! terms below consume; these separate the second part out again. There is no
         ! figuring term in Pi: the Petzval sum depends on the vertex curvature and
         ! the indices alone, and a figured surface has the same vertex sphere.
         fo(i, 41) = aBa
         fo(i, 42) = aFa
         fo(i, 43) = aCa
         fo(i, 44) = aEa
         fo(i, 45) = aB5
         fo(i, 46) = aF1
         fo(i, 47) = aF2
         fo(i, 48) = aM1
         fo(i, 49) = aM2
         fo(i, 50) = aM3
         fo(i, 51) = aN1
         fo(i, 52) = aN2
         fo(i, 53) = aN3
         fo(i, 54) = aC5
         fo(i, 55) = aP5
         fo(i, 56) = aE5
         fo(i, 57) = aB7

         z = 0.5 * acb * acb * (Pz + 3 * Ca) / lag
         z = z + 3 * (acb5 - acb * aceb / lag) * Fa
         z = z + 3 * (0.5 * aceb * aceb / lag - ace5b) * Ba
         z = (z + acb * (iF1 + iF2) - 5 * aceb * iB5) / (2 * lag)
         tB7 = iB7 + z

         tB5 = iB5 + 1.5 * (acb * Fa - aceb * Ba) / lag

         z = acb * (Pz + 4 * Ca) + (5 * acf - 4 * aceb) * Fa
         z = z - (2 * acpi + 5 * accb) * Ba
         tF1 = iF1 + z / (2 * lag)

         z = acb * (Pz + 2 * Ca) + 2 * (2 * acf - aceb) * Fa
         z = z - (acpi + 4 * accb) * Ba
         tF2 = iF2 + z / (2 * lag)

         z = acb * Ea + (4 * acf - aceb) * Ca - acfb * Ba
         z = z + (acc - 4 * accb - 2 * acpi) * Fa
         tM1 = iM1 + z / lag

         z = acb * Ea + (2 * acf - aceb) * (Pz + Ca)
         z = z + (3 * acc - 2 * accb + acpi) * Fa - 3 * acfb * Ba
         tM2 = iM2 + z / (2 * lag)

         z = acf * (Pz + 2 * Ca) - acfb * Ba + (acc - 2 * accb) * Fa
         tM3 = iM3 + 2 * z / lag

         z = 3 * acf * Ea - (acpi + accb) * (Pz + Ca) + 2 * (acc - accb) * Ca
         z = z + (ace - 2 * acfb) * Fa - acbb * Ba
         tN1 = iN1 + z / (2 * lag)

         z = 3 * acf * Ea + (acpi - accb + 3 * acc) * (Pz + 3 * Ca)
         z = z - acbb * Ba
         z = z - (acpi + acc) * Ca
         z = z + (ace - 8 * acfb) * Fa
         tN2 = iN2 + z / lag

         z = acf * Ea + (3 * acc + acpi - accb) * (Pz + Ca) - acbb * Ba
         z = z + (acpi + acc) * Ca + (ace - 4 * acfb) * Fa
         tN3 = iN3 + z / (2 * lag)

         z = (4 * acc + acpi) * Ea - acfb * Pz + 2 * (ace - 2 * acfb) * Ca
         z = z - 2 * acbb * Fa
         tC5 = iC5 + z / (4 * lag)

         z = (acpi - 2 * acc) * Ea + (4 * ace - acfb) * Pz
         z = z - 2 * acbb * Fa
         z = z + 2 * (ace + acfb) * Ca
         tP5 = iP5 + z / (4 * lag)

         tE5 = iE5 + (3 * ace * Ea - acbb * (Pz + 3 * Ca)) / (2 * lag)
         tE5b = iE5b + (acb * (Pz + 3 * Cb) - 3 * aceb * Eb) / (2 * lag)

         fo(i, 30) = tB5
         fo(i, 31) = tF1
         fo(i, 32) = tF2
         fo(i, 33) = tM1
         fo(i, 34) = tM2
         fo(i, 35) = tM3
         fo(i, 36) = tN1
         fo(i, 37) = tN2
         fo(i, 38) = tN3
         fo(i, 39) = tC5
         fo(i, 40) = tP5
         fo(i, 23) = tE5
         fo(i, 24) = tB7

         acb = acb + Ba
         acf = acf + Fa
         acc = acc + Ca
         ace = ace + Ea
         acpi = acpi + Pz
         acbb = acbb + Bb
         acfb = acfb + Fb
         accb = accb + Cb
         aceb = aceb + Eb
         acb5 = acb5 + tB5
         ace5b = ace5b + tE5b
      ENDIF
   NEXT
NEXT

! ---------------------------------------------------------------------------------
! Stage A and B output. These eighteen are what FIFTHORD prints, and FIFTHORD carries
! aspheres - so this is a check you can make on any lens, needing nothing but what
! already ships with OpticStudio.
! ---------------------------------------------------------------------------------

sumb = 0
sumf = 0
sumc = 0
sump = 0
sume = 0
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
   sumb = sumb + fo(i, 1)
   sumf = sumf + fo(i, 2)
   sumc = sumc + fo(i, 3)
   sump = sump + fo(i, 4)
   sume = sume + fo(i, 5)
   sB5 = sB5 + fo(i, 30)
   sF1 = sF1 + fo(i, 31)
   sF2 = sF2 + fo(i, 32)
   sM1 = sM1 + fo(i, 33)
   sM2 = sM2 + fo(i, 34)
   sM3 = sM3 + fo(i, 35)
   sN1 = sN1 + fo(i, 36)
   sN2 = sN2 + fo(i, 37)
   sN3 = sN3 + fo(i, 38)
   sC5 = sC5 + fo(i, 39)
   sP5 = sP5 + fo(i, 40)
   sE5 = sE5 + fo(i, 23)
   sB7 = sB7 + fo(i, 24)
NEXT

PRINT "Third and fifth order, per surface, unconverted - multiply by the F/number"
PRINT "above for a transverse aberration in lens units, which is done for the totals."
PRINT " "
PRINT " Surf              B              F              C             Pi              E"
FOR i = 1, nsm, 1
   FORMAT 5.0
   PRINT i,
   FORMAT 15.6 EXP
   PRINT fo(i, 1), fo(i, 2), fo(i, 3), fo(i, 4), fo(i, 5)
NEXT
PRINT " "
FORMAT 5.0
PRINT "  SUM",
FORMAT 15.6 EXP
PRINT sumb, sumf, sumc, sump, sume

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
IF (anyr2 == 0)
   PRINT "Run FIFTHORD on this same file and these eighteen must match, asphere or not."
ELSE
   PRINT "DO NOT COMPARE THESE WITH FIFTHORD ON THIS FILE. A surface here carries a"
   PRINT "non-zero PARM 1, the r^2 deformation, and FIFTHORD does not handle it: it"
   PRINT "takes paraxial data from OpticStudio, which INCLUDES the power that term"
   PRINT "adds, and then computes each surface's contribution from the BASE curvature."
   PRINT "The two halves describe different surfaces, so its answer is neither this"
   PRINT "lens nor the lens without the term. The numbers above are the correct ones."
   PRINT "Compare against FORBES.ZPL instead, which shares no arithmetic with this."
ENDIF
PRINT " "

! ---------------------------------------------------------------------------------
! srf = 1. The third and fifth order surface by surface, each split three ways.
!
!   INTRINSIC  what the surface generates out of its own curvature, indices and the
!              two paraxial rays arriving at it. Correcting this means bending THIS
!              surface, or moving the glass either side of it.
!   FIGURING   what its conic and its r^4, r^6, r^8 terms add. Zero on a sphere.
!   INDUCED    what the aberration ALREADY ACCUMULATED ahead of the surface produces
!              in it. The surface is reacting rather than offending, and a change
!              made here to cancel it is a second wrong balancing a first. This is
!              the part that is not visible in any per-surface total, and it is the
!              reason the split exists.
!
! There is no induced term at THIRD order: a third-order contribution is built from
! that surface's own quantities alone, which is why the third-order block has three
! rows and the fifth-order block four.
!
! Every number is in transverse measure, the same as the system totals above, so the
! columns add downward to them - a check on this printout that costs nothing to make.
!
! WHAT THIS DOES NOT DO. The seventh order below is printed per surface but is NOT
! split: at seventh order the surface's own quantities and the accumulated ones enter
! through the same Table I entries, and separating them would be a reconstruction of
! Buchdahl rather than a reading of him. Where the seventh order has an unambiguous
! answer - B7, tau1 - it is in the fifth-order block above, split like the rest.
! ---------------------------------------------------------------------------------

IF (srf == 1)
   uz = 0
   PRINT " "
   PRINT "==============================================================================="
   PRINT "THIRD AND FIFTH ORDER, SURFACE BY SURFACE - intrinsic, figuring, induced."
   PRINT "Transverse measure. Each column adds down to the system total printed above."
   PRINT "==============================================================================="

   FOR i = 1, nsm, 1
      PRINT " "
      FORMAT 3.0
      IF (vfm(i, 6) == 1)
         PRINT "Surface ", i, "   (figured)"
      ELSE
         PRINT "Surface ", i, "   (spherical)"
      ENDIF

      PRINT "                          B              F              C             Pi              E"
      FORMAT 15.6 EXP
      PRINT "   intrinsic  ", (fo(i, 1) - fo(i, 41)) * fnum, (fo(i, 2) - fo(i, 42)) * fnum, (fo(i, 3) - fo(i, 43)) * fnum, fo(i, 4) * fnum, (fo(i, 5) - fo(i, 44)) * fnum
      PRINT "   figuring   ", fo(i, 41) * fnum, fo(i, 42) * fnum, fo(i, 43) * fnum, uz, fo(i, 44) * fnum
      PRINT "   TOTAL      ", fo(i, 1) * fnum, fo(i, 2) * fnum, fo(i, 3) * fnum, fo(i, 4) * fnum, fo(i, 5) * fnum

      PRINT "                         B5             F1             F2             M1             M2"
      FORMAT 15.6 EXP
      PRINT "   intrinsic  ", (fo(i, 10) - fo(i, 45)) * fnum, (fo(i, 11) - fo(i, 46)) * fnum, (fo(i, 12) - fo(i, 47)) * fnum, (fo(i, 13) - fo(i, 48)) * fnum, (fo(i, 14) - fo(i, 49)) * fnum
      PRINT "   figuring   ", fo(i, 45) * fnum, fo(i, 46) * fnum, fo(i, 47) * fnum, fo(i, 48) * fnum, fo(i, 49) * fnum
      PRINT "   induced    ", (fo(i, 30) - fo(i, 10)) * fnum, (fo(i, 31) - fo(i, 11)) * fnum, (fo(i, 32) - fo(i, 12)) * fnum, (fo(i, 33) - fo(i, 13)) * fnum, (fo(i, 34) - fo(i, 14)) * fnum
      PRINT "   TOTAL      ", fo(i, 30) * fnum, fo(i, 31) * fnum, fo(i, 32) * fnum, fo(i, 33) * fnum, fo(i, 34) * fnum

      PRINT "                         M3             N1             N2             N3             C5"
      FORMAT 15.6 EXP
      PRINT "   intrinsic  ", (fo(i, 15) - fo(i, 50)) * fnum, (fo(i, 16) - fo(i, 51)) * fnum, (fo(i, 17) - fo(i, 52)) * fnum, (fo(i, 18) - fo(i, 53)) * fnum, (fo(i, 19) - fo(i, 54)) * fnum
      PRINT "   figuring   ", fo(i, 50) * fnum, fo(i, 51) * fnum, fo(i, 52) * fnum, fo(i, 53) * fnum, fo(i, 54) * fnum
      PRINT "   induced    ", (fo(i, 35) - fo(i, 15)) * fnum, (fo(i, 36) - fo(i, 16)) * fnum, (fo(i, 37) - fo(i, 17)) * fnum, (fo(i, 38) - fo(i, 18)) * fnum, (fo(i, 39) - fo(i, 19)) * fnum
      PRINT "   TOTAL      ", fo(i, 35) * fnum, fo(i, 36) * fnum, fo(i, 37) * fnum, fo(i, 38) * fnum, fo(i, 39) * fnum

      PRINT "                        Pi5             E5             B7"
      FORMAT 15.6 EXP
      PRINT "   intrinsic  ", (fo(i, 20) - fo(i, 55)) * fnum, (fo(i, 21) - fo(i, 56)) * fnum, (fo(i, 22) - fo(i, 57)) * fnum
      PRINT "   figuring   ", fo(i, 55) * fnum, fo(i, 56) * fnum, fo(i, 57) * fnum
      PRINT "   induced    ", (fo(i, 40) - fo(i, 20)) * fnum, (fo(i, 23) - fo(i, 21)) * fnum, (fo(i, 24) - fo(i, 22)) * fnum
      PRINT "   TOTAL      ", fo(i, 40) * fnum, fo(i, 23) * fnum, fo(i, 24) * fnum
   NEXT

   ! ------------------------------------------------------------------------------
   ! One number per surface, to be read before the tables above rather than after
   ! them. It is the induced share of the fifth-order magnitude the surface carries,
   ! summed over the thirteen coefficients so that no one of them decides it.
   !
   ! Near zero, the surface is on its own and what it contributes is its own doing.
   ! Near one, almost everything it carries was handed to it, and a change made here
   ! is a correction applied at the wrong place - the fix is upstream.
   !
   ! IT IS NOT BOUNDED BY ONE, and a value above one is the reading worth having. It
   ! says the induced part is LARGER than the total, so the intrinsic and the induced
   ! are opposing each other and what the surface reports is the residue left after
   ! they cancel. Such a surface looks quiet in any per-surface total and is not: two
   ! large terms are standing against each other there, and anything that disturbs
   ! either - a bend, a thickness, a melt - moves the residue by far more than its own
   ! size suggests. Reported separately rather than folded in with "mostly induced",
   ! which is a different situation calling for a different response.
   !
   ! It is a ratio of sums of magnitudes and not of signed quantities, deliberately:
   ! two induced terms of opposite sign are still two induced terms.
   ! ------------------------------------------------------------------------------
   PRINT " "
   PRINT "Induced share of the fifth order carried by each surface."
   PRINT " "
   PRINT " Surf    induced/total   what it means"
   FOR i = 1, nsm, 1
      uin = 0
      utt = 0
      FOR uj = 10, 22, 1
         umt = fo(i, uj + 20)
         IF (uj == 21) THEN umt = fo(i, 23)
         IF (uj == 22) THEN umt = fo(i, 24)
         uin = uin + ABSO(umt - fo(i, uj))
         utt = utt + ABSO(umt)
      NEXT
      ufr = 0
      IF (utt > 0) THEN ufr = uin / utt
      unot$ = "   mixed"
      IF (ufr < 0.1)
         unot$ = "   its own doing - the fault, if there is one, is here"
      ENDIF
      IF (ufr > 0.9)
         unot$ = "   mostly induced - look upstream"
      ENDIF
      IF (ufr > 1.1)
         unot$ = "   induced exceeds the total - the two are cancelling here"
      ENDIF
      FORMAT 5.0
      PRINT i,
      FORMAT 12.4
      PRINT ufr, unot$
   NEXT
   PRINT " "
ENDIF


! =================================================================================
! THE POLYNOMIAL ALGEBRA, and the per-surface figuring polynomials.
!
! Buchdahl's (77.4-5) and (78.1) are polynomials in three variables xi, eta, zeta, of
! total degree at most three. The tertiary increment is built by multiplying them
! together, so the algebra has to exist before the scheme can be run.
!
! A polynomial lives in one slot of pw, indexed 1 + a*16 + b*4 + c for xi^a eta^b
! zeta^c. Only the twenty monomials of degree three or less are ever touched, and the
! multiply walks a table of them rather than all sixty-four.
!
! The polynomials depend on k, the curvature and the figuring - NOT on the ray data -
! so they are built ONCE per surface and used by all four runs of the scheme. That is
! what keeps this affordable: the ray-dependent work is the theta expansion alone.
! =================================================================================

DECLARE pw, DOUBLE, 2, 40, 66
DECLARE pc, DOUBLE, 2, nsm + 2, 112

! Buchdahl's c1, c2, c3 per surface, HALVED and scaled - the form Secs. 77-79 want.
! Declared HERE, beside the polynomials, because stage P fills it and stage P runs
! before anything below: a DECLARE that comes after its first use is not an
! out-of-range array, it is an unknown name, and ZPL reports it as a variable not
! followed by an = sign on a line that plainly has one.
DECLARE pcf, DOUBLE, 2, nsm + 2, 4

GOSUB pmk

! pc layout, per surface:
!    1..10   lambda figured        = 16 * cubic part of S3(c1, c2)
!   11..20   lambda spherical      = 16 * cubic part of S3(0, 0)
!   21..26   nu-prime figured      = 16 * cubic part of X3(c1, c2), first six
!   27..32   nu-prime spherical
!   33..42   cubic part of G3
!   43..52   cubic part of G2 * S1
!   53..62   cubic part of G2 * B1
!   63..72   cubic part of G1 * S2
!   73..82   cubic part of G1 * X2fig
!   83..88   quadratic part of S2 figured
!   89..94   quadratic part of X2 figured
!   95..100  quadratic part of S2 spherical
!  101..106  quadratic part of X2 spherical
!
! The spherical set is the SAME surface with the figuring set to zero. It is what the
! D-half increment is differenced against, and what the scheme's own unfigured run
! needs, so it is built here alongside rather than a second time later.

FOR i = 1, nsm, 1
   FOR j = 1, 112, 1
      pc(i, j) = 0
   NEXT
NEXT

FOR i = 1, nsm, 1
   IF (vfm(i, 6) == 1)
      sfk = 0
      nb = INDX(i - 1)
      na = INDX(i)
      IF (ISMS(i - 1)) THEN nb = -nb
      IF (ISMS(i)) THEN na = -na
      IF (ABSO(na) < eps) THEN na = 1
      sfk = nb / na

      ! Buchdahl's c1, c2, c3, HALVED: the fifth-order code's convention is twice his
      ! by (56.5), and (77.2), (77.4) and (77.5) are written in his. Mixing the two
      ! scales terms linear in c by two and c1-squared terms by four, which no single
      ! factor can undo. Scaled to the unit-focal-length system the scheme works in.
      conic = vfm(i, 2)
      aterm = vfm(i, 3)
      bterm = vfm(i, 4)
      cterm = vfm(i, 5)
      cv = vfm(i, 1)
      cv2 = cv * cv
      cv3 = cv2 * cv
      c1 = 8 * aterm + conic * cv3
      c2 = 12 * bterm + 0.75 * cv2 * (cv3 * conic * (conic + 2) - 2 * c1)
      tmp = cv3 * conic * (conic * conic + 3 * conic + 3) - 3 * c1
      tmp = cv2 * (5 * cv2 * tmp - 12 * c2)
      tmp = (-6 * cv * c1 * c1 + tmp) / 8
      c3 = 16 * cterm + tmp

      f2s = efl * efl
      f3s = f2s * efl
      sfc1 = 0.5 * c1 * f3s
      sfc2 = 0.5 * c2 * f3s * f2s
      sfc3 = 0.5 * c3 * f3s * f2s * f2s
      sfc0 = cv * efl

      ! Kept, because the D half of the secondary rebuilds this surface's
      ! polynomials at its own curvature and needs them again there.
      pcf(i, 1) = sfc1
      pcf(i, 2) = sfc2
      pcf(i, 3) = sfc3

      ! The figured surface: slots 1 S1, 2 S2, 3 S3, 4 X3, 5 G1, 6 G2, 7 G3, 8 B1, 9 B2
      sa1 = sfc1
      sa2 = sfc2
      sa3 = sfc3
      GOSUB psurf

      GOSUB pcub3
      FOR j = 1, 10, 1
         pc(i, j) = 16 * cub(1, j)
      NEXT
      GOSUB pcub4
      FOR j = 1, 6, 1
         pc(i, 20 + j) = 16 * cub(1, j)
      NEXT

      ! G3, and the four products the L cubic needs.
      ga = 7
      GOSUB pcubs
      FOR j = 1, 10, 1
         pc(i, 32 + j) = cub(1, j)
      NEXT

      ga = 6
      gb = 1
      gc = 20
      GOSUB pmul
      ga = 20
      GOSUB pcubs
      FOR j = 1, 10, 1
         pc(i, 42 + j) = cub(1, j)
      NEXT

      ga = 6
      gb = 8
      gc = 20
      GOSUB pmul
      ga = 20
      GOSUB pcubs
      FOR j = 1, 10, 1
         pc(i, 52 + j) = cub(1, j)
      NEXT

      ga = 5
      gb = 2
      gc = 20
      GOSUB pmul
      ga = 20
      GOSUB pcubs
      FOR j = 1, 10, 1
         pc(i, 62 + j) = cub(1, j)
      NEXT

      ! X2 = B2 + B1 * S1, figured.
      ga = 8
      gb = 1
      gc = 21
      GOSUB pmul
      FOR j = 1, 64, 1
         pw(22, j) = pw(21, j) + pw(9, j)
      NEXT
      ga = 5
      gb = 22
      gc = 20
      GOSUB pmul
      ga = 20
      GOSUB pcubs
      FOR j = 1, 10, 1
         pc(i, 72 + j) = cub(1, j)
      NEXT

      ! The figured quadratics: S2 and X2 = B2 + B1 S1, kept for the secondary D half.
      ga = 2
      GOSUB pquad
      FOR j = 1, 6, 1
         pc(i, 82 + j) = cub(1, j)
      NEXT
      ga = 22
      GOSUB pquad
      FOR j = 1, 6, 1
         pc(i, 88 + j) = cub(1, j)
      NEXT

      ! The same surface unfigured, which every increment is differenced against.
      sa1 = 0
      sa2 = 0
      sa3 = 0
      GOSUB psurf

      GOSUB pcub3
      FOR j = 1, 10, 1
         pc(i, 10 + j) = 16 * cub(1, j)
      NEXT
      GOSUB pcub4
      FOR j = 1, 6, 1
         pc(i, 26 + j) = 16 * cub(1, j)
      NEXT

      ga = 2
      GOSUB pquad
      FOR j = 1, 6, 1
         pc(i, 94 + j) = cub(1, j)
      NEXT

      ga = 8
      gb = 1
      gc = 21
      GOSUB pmul
      FOR j = 1, 64, 1
         pw(22, j) = pw(21, j) + pw(9, j)
      NEXT
      ga = 22
      GOSUB pquad
      FOR j = 1, 6, 1
         pc(i, 100 + j) = cub(1, j)
      NEXT
   ENDIF
NEXT

! =================================================================================
! STAGE C - Buchdahl's Table I, up to four passes.
!
!   run 1   direct rays, UNFIGURED    the spherical scheme, and what the bridge is
!                                     measured on
!   run 2   direct rays, figured      the working scheme
!   run 3   dual rays, UNFIGURED      the same for the interchanged ray data
!   run 4   dual rays, figured        its accumulated s_1p IS -S-bar_6q, XII (6.2)
!
! A SYSTEM OF SPHERES TAKES RUN 1 ALONE. That is not an optimisation for its own
! sake - it is the routing TertiaryCoefficients.Attach uses, spheres through Table I
! bit for bit and figured systems through the Sec. 85 arrangement - and it means the
! spherical result cannot be disturbed by anything the aspheric passes do.
!
! THE DUAL PASS is the same code with the two rays interchanged at surface one and
! the refractive indices negated. XII Sec. 6 gives every coefficient's dual
! "whether the surfaces of the optical system are spherical or not" - PROVIDED the
! indices are negated too, since Table I is built on g/N1 = 1 and
! 1/g = y_p v_q - y_q v_p changes sign under the swap. Without that caveat the dual
! agrees at ratio -1.00 on one surface and -0.30 on the next, which is exactly the
! shape of the earlier failed attempt at it.
!
! WHAT IS RUN-INDEPENDENT. The figuring polynomials of Sec. 77 depend on k, the
! curvature and the figuring - not on the ray data - and k = n/n' is unchanged by
! negating both indices. Stage P built them once and all four passes read them.
!
! The C# runs a fifth pass and discards it: BuchdahlTableI.cs assigns a `bare`
! recursion whose result is never read. It is not reproduced here.
!
! STORAGE. The row index folds the pass into the surface subscript, so two 2-D
! arrays hold all four passes:  rb = (run - 1) * (nsm + 2) + i + 1.
!
!   tt(rb, 1..160)   Table I's entries, numbered as Buchdahl numbers them
!   yf(rb, 1..29)    the (Y) family - 1..9 are Y[25..33], 10..29 are Y[101..120]
!   rv(rb, 1..77)    the row quantities Table I gives no number to
!   ai(rb, 1..7)     the aspheric increments in the scheme's units
! =================================================================================

DECLARE tt, DOUBLE, 2, 4 * nsm + 14, 162
DECLARE yf, DOUBLE, 2, 4 * nsm + 14, 32
DECLARE rv, DOUBLE, 2, 4 * nsm + 14, 80
DECLARE ai, DOUBLE, 2, 4 * nsm + 14, 8

! The bridge's own working, which has no business in the row arrays.
!   bw(1, q)  the conversion constant for quantity q
!   bw(2, q)  the largest fifth-order quantity seen so far, which chose it
!   bw(3, q)  the scheme's seven at the surface being read
DECLARE bw, DOUBLE, 2, 4, 9

! The secondary construction's registers. ZPL has no locals, so the two halves are
! run through the same rows and the spherical results are copied aside (8, 9, 10)
! before the figured call overwrites 4, 5 and 6.
!   1 the six lifts    2 the nine dagger entries   3 the six intrinsic s
!   4 total  5 barred  6 mid        7 the D half   8,9,10 the spherical 4,5,6
DECLARE sc, DOUBLE, 2, 12, 12

! dhalf's working: 1 S2 figured, 2 X2 figured, 3 S2 spherical, 4 X2 spherical,
! 5 the theta expansion, 6 the six g, 7 the six w.
DECLARE dh, DOUBLE, 2, 8, 8

! The figuring's tertiary increment: 1 dD/c0 expanded, 2 the hat half, 3 the check.
DECLARE tz, DOUBLE, 2, 4, 12

! The figuring's tertiary increment: 1 dD/c0 expanded, 2 the hat half, 3 the check.

! rv column map. Every one of these exists because a division by the incidence had
! to be avoided somewhere.
!
!    1 Rho       the HEIGHT ratio y_q/y_p, on which the figuring travels
!    2 ApSph     3 ApFig     4 C13Sph    5 C13Fig    6 COverI    7 Iq
!    8 Lag       9 KRatio   10 NBefore  11 QApSph   12 Q2ApSph  13 QC13Sph
!   14 QOmega   15 C14Fig   16 T19Fig   17 T24Fig   18 Flat     19 Cscaled
!   20 VpPrime  21 VqPrime
!   22..27 SecSph 1..6     28..33 SecFig 1..6     34..39 SecDFig 1..6
!   40..45 SecBarFigLift   46..50 MSph 1..5       51..55 MFig 1..5
!   56..65 ZHat 1..10      66..75 ZCheck 1..10

FOR i = 1, 4 * nsm + 14, 1
   FOR j = 1, 8, 1
      ai(i, j) = 0
   NEXT
NEXT

! A system of spheres needs the first pass only.
nruns = 1
IF (anyfig == 1) THEN nruns = 4

flatbad = 0

FOR grun = 1, nruns, 1
   gdual = 0
   IF (grun > 2) THEN gdual = 1
   gfig = 0
   IF (grun == 2) THEN gfig = 1
   IF (grun == 4) THEN gfig = 1

   IF (gfig == 1)
      GOSUB bridge
   ENDIF

   GOSUB tablei
   GOSUB tablei2
NEXT

! A figured flat facing collimated light makes q infinite and every injection below
! is skipped. The C# reaches it by running the whole chain in Laurent series
! arithmetic with that surface's curvature as the variable; ZPL cannot, so the macro
! says so rather than returning a number that looks like an answer.
IF (flatbad == 1)
   PRINT " "
   PRINT "DECLINED. A figured surface is flat and faces collimated light, so the"
   PRINT "marginal incidence is zero and the incidence ratio q is infinite. The"
   PRINT "finite coefficients arrive there only after terms carrying different powers"
   PRINT "of q cancel, which needs series arithmetic this macro does not have. Bend"
   PRINT "the surface and it is exact again. The third and fifth orders above are"
   PRINT "unaffected by this and stand."
   GOTO 900
ENDIF

! ---------------------------------------------------------------------------------
! Table I entry by entry for this lens, in the format BUCH7 prints so the two can be
! set side by side. This is the first of the passes - the one that carries no
! figuring - so on a lens of spheres it is the whole of the scheme.
! ---------------------------------------------------------------------------------
IF (dbg == 1)
   PRINT " "
   PRINT "Table I entries for this lens, the unfigured pass. The entry numbers are"
   PRINT "Buchdahl's own, and BUCH7 prints the same table in the same order."
   FOR j = 1, 155, 1
      FORMAT 4.0
      PRINT "t ", j,
      FORMAT 20.9 EXP
      FOR i = 1, nsm, 1
         PRINT tt(i + 1, j),
      NEXT
      PRINT " "
   NEXT
ENDIF


! =================================================================================
! STAGE D - the ten tertiary totals, and the twenty tau.
!
! A LENS OF SPHERES takes the plain route: one pass per surface on the whole row,
! which is Buchdahl's own arrangement and is left untouched. A FIGURED lens takes
! Sec. 85: each total is i_p(hat bracket) + y_p(check bracket), the two passes given
! their own halves of the surface's own quantities and their own family.
!
! THE RESIDUE. Some induced terms involve no quantity of this surface at all - they
! are built from the accumulated coefficients alone - and belong to ONE pass rather
! than to both. The all-zero residue measures them, with the same intermediates as
! the pass it is taken off, so that they are removed once and not twice. On a sphere
! every check input is zero and this makes the whole check half vanish identically,
! which it must.
! =================================================================================

DECLARE po, DOUBLE, 2, 12, 12
DECLARE sv, DOUBLE, 2, 5, 32

! The ten totals and their barred partners for ONE surface, kept so that the twenty
! tau can be formed surface by surface as well as for the system. The conversion of
! Table II is linear, so a per-surface tau is the conversion applied to that
! surface's own ten - which is why these columns add down to the totals exactly.
DECLARE pt, DOUBLE, 2, nsm + 2, 22

FOR k = 1, 10, 1
   po(9, k) = 0
   po(10, k) = 0
NEXT

IF (anyfig == 0)
   sqf = 2
   FOR i = 1, nsm, 1
      psb = 1 + i
      pscar = tt(psb, 6)
      GOSUB pass
      FOR k = 1, 10, 1
         po(9, k) = po(9, k) + po(1, k)
         po(10, k) = po(10, k) + po(2, k)
         pt(i, k) = po(1, k)
         pt(i, 10 + k) = po(2, k)
      NEXT
   NEXT
ELSE
   tdg = nsm + 3
   tddg = 3 * (nsm + 2) + 1
   sqf = tdg + 1

   FOR i = 1, nsm, 1
      tdb = tdg + i
      tdd = tddg + i

      FOR k = 1, 9, 1
         sv(1, k) = tt(tdb, 24 + k)
      NEXT
      FOR k = 1, 20, 1
         sv(1, 9 + k) = tt(tdb, 100 + k)
      NEXT
      sv(2, 1) = tt(tdb, 10)
      sv(2, 2) = tt(tdb, 13)
      sv(2, 3) = tt(tdb, 40)
      sv(2, 4) = tt(tdb, 38)
      sv(2, 5) = tt(tdb, 44)
      sv(2, 6) = tt(tdb, 50)
      sv(2, 7) = tt(tdb, 54)
      sv(2, 8) = tt(tdb, 59)
      sv(2, 9) = tt(tdb, 65)
      sv(2, 10) = tt(tdb, 45)
      sv(2, 11) = tt(tdb, 51)
      sv(2, 12) = tt(tdb, 55)
      sv(2, 13) = tt(tdb, 61)
      sv(2, 14) = tt(tdb, 66)
      FOR k = 1, 10, 1
         sv(3, k) = tt(tdb, 120 + k)
      NEXT

      ! The residue, on the check family and with every input of this surface zero.
      fmb = tdb
      fmd = tdd
      fmc = 1
      GOSUB fam
      tzb = tdb
      GOSUB tdzero
      psb = tdb
      pscar = rv(tdb, 1)
      GOSUB pass
      FOR k = 1, 10, 1
         po(3, k) = po(1, k)
         po(4, k) = po(2, k)
      NEXT

      ! The hat half. (60.3) puts the figuring's D half on the incidence and its L
      ! half on the height, so the D half belongs with the hat quantities - in the
      ! intrinsic secondaries AND in the five M entries, each of which carries the
      ! intrinsic secondary it follows from with coefficient one. The total is
      ! unchanged either way; what moves is which pass carries it.
      fmc = 0
      GOSUB fam
      tt(tdb, 10) = rv(tdb, 2)
      tt(tdb, 13) = rv(tdb, 4)
      tt(tdb, 38) = rv(tdb, 22) + rv(tdb, 34)
      tt(tdb, 44) = rv(tdb, 23) + rv(tdb, 35)
      tt(tdb, 50) = rv(tdb, 24) + rv(tdb, 36)
      tt(tdb, 54) = rv(tdb, 25) + rv(tdb, 37)
      tt(tdb, 59) = rv(tdb, 26) + rv(tdb, 38)
      tt(tdb, 65) = rv(tdb, 27) + rv(tdb, 39)
      tt(tdb, 45) = rv(tdb, 46) + rv(tdb, 35)
      tt(tdb, 51) = rv(tdb, 47) + rv(tdb, 36)
      tt(tdb, 55) = rv(tdb, 48) + rv(tdb, 37)
      tt(tdb, 61) = rv(tdb, 49) + rv(tdb, 38)
      tt(tdb, 66) = rv(tdb, 50) + rv(tdb, 39)
      FOR k = 1, 10, 1
         tt(tdb, 120 + k) = rv(tdb, 55 + k)
      NEXT
      tt(tdb, 40) = tt(tdb, 38) + 2 * tt(tdb, 10) * tt(tdb, 25)
      psb = tdb
      pscar = tt(tdb, 6)
      GOSUB pass
      FOR k = 1, 10, 1
         po(5, k) = po(1, k)
         po(6, k) = po(2, k)
      NEXT

      ! The hat pass's own z and intrinsic chain, kept before the check pass
      ! overwrites them - otherwise the diagnostic below would print the wrong half.
      FOR k = 1, 10, 1
         sv(4, k) = tt(tdb, 120 + k)
      NEXT
      sv(5, 1) = tt(tdb, 134)
      sv(5, 2) = tt(tdb, 136)
      sv(5, 3) = tt(tdb, 138)
      sv(5, 4) = tt(tdb, 140)
      sv(5, 5) = tt(tdb, 141)

      ! The check half, with the D half taken back off.
      fmc = 1
      GOSUB fam
      tt(tdb, 10) = rv(tdb, 3)
      tt(tdb, 13) = rv(tdb, 5)
      tt(tdb, 38) = rv(tdb, 28) - rv(tdb, 34)
      tt(tdb, 44) = rv(tdb, 29) - rv(tdb, 35)
      tt(tdb, 50) = rv(tdb, 30) - rv(tdb, 36)
      tt(tdb, 54) = rv(tdb, 31) - rv(tdb, 37)
      tt(tdb, 59) = rv(tdb, 32) - rv(tdb, 38)
      tt(tdb, 65) = rv(tdb, 33) - rv(tdb, 39)
      tt(tdb, 45) = rv(tdb, 51) - rv(tdb, 35)
      tt(tdb, 51) = rv(tdb, 52) - rv(tdb, 36)
      tt(tdb, 55) = rv(tdb, 53) - rv(tdb, 37)
      tt(tdb, 61) = rv(tdb, 54) - rv(tdb, 38)
      tt(tdb, 66) = rv(tdb, 55) - rv(tdb, 39)
      FOR k = 1, 10, 1
         tt(tdb, 120 + k) = rv(tdb, 65 + k)
      NEXT
      tt(tdb, 40) = tt(tdb, 38) + 2 * tt(tdb, 10) * tt(tdb, 25)
      psb = tdb
      pscar = rv(tdb, 1)
      GOSUB pass

      ! (85.3): the total is the sum of the two halves, and the barred entry the sum
      ! of the two BARRED halves - each formed by (84.23) within its own pass, not
      ! the total times a ratio, which would drop the barred intermediates entirely.
      FOR k = 1, 10, 1
         po(9, k) = po(9, k) + po(5, k) + po(1, k) - po(3, k)
         po(10, k) = po(10, k) + po(6, k) + po(2, k) - po(4, k)
         pt(i, k) = po(5, k) + po(1, k) - po(3, k)
         pt(i, 10 + k) = po(6, k) + po(2, k) - po(4, k)
      NEXT

      ! The two halves surface by surface. A total is a sum over surfaces of four
      ! numbers, and knowing WHICH of them disagrees places a fault on a surface and
      ! a pass rather than leaving it to be inferred from the total.
      IF (dbg == 1)
         FORMAT 3.0
         PRINT " "
         PRINT "Surface ", i, " - hat, then check less its residue:"
         FORMAT 15.6 EXP
         PRINT "   hat  ", po(5, 1), po(5, 2), po(5, 3), po(5, 4), po(5, 5)
         PRINT "        ", po(5, 6), po(5, 7), po(5, 8), po(5, 9), po(5, 10)
         PRINT "   hatb ", po(6, 1), po(6, 2), po(6, 3), po(6, 4), po(6, 5)
         PRINT "        ", po(6, 6), po(6, 7), po(6, 8), po(6, 9), po(6, 10)
         PRINT "   chk  ", po(1, 1) - po(3, 1), po(1, 2) - po(3, 2), po(1, 3) - po(3, 3), po(1, 4) - po(3, 4), po(1, 5) - po(3, 5)
         PRINT "        ", po(1, 6) - po(3, 6), po(1, 7) - po(3, 7), po(1, 8) - po(3, 8), po(1, 9) - po(3, 9), po(1, 10) - po(3, 10)
         PRINT "   chkb ", po(2, 1) - po(4, 1), po(2, 2) - po(4, 2), po(2, 3) - po(4, 3), po(2, 4) - po(4, 4), po(2, 5) - po(4, 5)
         PRINT "        ", po(2, 6) - po(4, 6), po(2, 7) - po(4, 7), po(2, 8) - po(4, 8), po(2, 9) - po(4, 9), po(2, 10) - po(4, 10)
         PRINT "   z hat", sv(4, 1), sv(4, 2), sv(4, 3), sv(4, 4), sv(4, 5)
         PRINT "        ", sv(4, 6), sv(4, 7), sv(4, 8), sv(4, 9), sv(4, 10)
         PRINT "   chain", sv(5, 1), sv(5, 2), sv(5, 3), sv(5, 4), sv(5, 5)
      ENDIF

      ! The row is put back exactly as it was found.
      FOR k = 1, 9, 1
         tt(tdb, 24 + k) = sv(1, k)
      NEXT
      FOR k = 1, 20, 1
         tt(tdb, 100 + k) = sv(1, 9 + k)
      NEXT
      tt(tdb, 10) = sv(2, 1)
      tt(tdb, 13) = sv(2, 2)
      tt(tdb, 40) = sv(2, 3)
      tt(tdb, 38) = sv(2, 4)
      tt(tdb, 44) = sv(2, 5)
      tt(tdb, 50) = sv(2, 6)
      tt(tdb, 54) = sv(2, 7)
      tt(tdb, 59) = sv(2, 8)
      tt(tdb, 65) = sv(2, 9)
      tt(tdb, 45) = sv(2, 10)
      tt(tdb, 51) = sv(2, 11)
      tt(tdb, 55) = sv(2, 12)
      tt(tdb, 61) = sv(2, 13)
      tt(tdb, 66) = sv(2, 14)
      FOR k = 1, 10, 1
         tt(tdb, 120 + k) = sv(3, k)
      NEXT
   NEXT
ENDIF

! The ten tertiary totals and their barred partners, before Table II mixes them.
! Each tau draws on several of these, so a fault is far easier to read here: the
! twenty below are what Sec. 85 computes, and the twenty tau are only their
! re-expression in Robb's variables.
IF (chk == 1)
   PRINT " "
   PRINT "The ten tertiary totals and their barred partners, unconverted."
   FORMAT 15.6 EXP
   PRINT "   T1  ", po(9, 1), "  T2  ", po(9, 2), "  T3  ", po(9, 3)
   PRINT "   T4  ", po(9, 4), "  T5  ", po(9, 5), "  T6  ", po(9, 6)
   PRINT "   T7  ", po(9, 7), "  T8  ", po(9, 8), "  T9  ", po(9, 9)
   PRINT "   T10 ", po(9, 10)
   PRINT "   Tb1 ", po(10, 1), "  Tb2 ", po(10, 2), "  Tb3 ", po(10, 3)
   PRINT "   Tb4 ", po(10, 4), "  Tb5 ", po(10, 5), "  Tb6 ", po(10, 6)
   PRINT "   Tb7 ", po(10, 7), "  Tb8 ", po(10, 8), "  Tb9 ", po(10, 9)
   PRINT "   Tb10", po(10, 10)
ENDIF

! ---------------------------------------------------------------------------------
! Paper III Table II: Buchdahl's ten tertiary totals and their barred partners read
! as Robb's twenty tau. The conversion is not one-to-one - tau2 mixes T-bar1 with
! half of T2, tau7 draws on four different T at once - which is why the T are formed
! first and converted afterwards rather than being produced in Robb's form directly.
! ---------------------------------------------------------------------------------
VEC4(1) = po(9, 1)
VEC4(2) = po(10, 1) + po(9, 2) / 2
VEC4(3) = po(9, 2) / 2
VEC4(4) = po(10, 2) + po(9, 3)
VEC4(5) = po(9, 3)
VEC4(6) = po(9, 4)
VEC4(7) = po(10, 3) + po(10, 4) / 2 + po(9, 5) / 2 + 3 * po(9, 7) / 8
VEC4(8) = (po(10, 4) + po(9, 5) + po(9, 7)) / 2
VEC4(9) = po(9, 5) / 2 + po(9, 7) / 4
VEC4(10) = po(9, 7) / 8
VEC4(11) = po(10, 5) + po(9, 6)
VEC4(12) = po(10, 7) + po(9, 8)
VEC4(13) = po(9, 6)
VEC4(14) = po(9, 8)
VEC4(15) = po(10, 6) + po(10, 8) / 2 + po(9, 9) / 2
VEC4(16) = (po(10, 8) + po(9, 9)) / 2
VEC4(17) = po(9, 9) / 2
VEC4(18) = po(10, 9) + po(9, 10)
VEC4(19) = po(9, 10)
VEC4(20) = po(10, 10)

! ---------------------------------------------------------------------------------
! Into transverse measure. Buchdahl works in units of the focal length and his pupil
! and field variables are not fractions: his rho is the ray SLOPE, running from zero
! to the marginal angle, and his H is the TANGENT of the field angle. So a
! coefficient multiplying rho^a H^b converts by u^a H^b, and the whole displacement
! then scales by M (31.11)'s 1/(N'_k v'_pk).
!
! The aperture powers run 7 6 6 5 5 5 4 4 4 4 3 3 3 3 2 2 2 1 1 0 and the field power
! is seven less each.
! ---------------------------------------------------------------------------------

! The aperture variable of M (13.5)/(31.13): the ray height at the entrance pupil, in
! focal lengths, over g. The marginal ray is at the pupil edge by definition, so its
! height there is that radius.
epr = VEC2(2) + epp * VEC2(501)
um = -(epr / efl) / goe

! The field variable, M (13.4)'s T = iota H_1: the object height over the object
! distance. It tends to the tangent of the field angle as the object recedes, which is
! why the two are one quantity and not two. Taken from the chief ray, which gives it
! whatever the field type is - angle, object height or image height.
IF (finite == 1)
   hobj = VEC3(2) - objd * VEC3(501)
   hmax = -hobj / objd
ELSE
   hmax = ABSO(VEC3(501))
ENDIF
IF (ABSO(hmax) < eps)
   PRINT "The chief ray carries no field, so the field variable cannot be recovered."
   PRINT "Is the field zero?"
   GOTO 900
ENDIF

! THE FIELD VARIABLE CARRIES THE OBJECT INDEX, the other half of the reduction made at
! the ray start. The chief ray's tangent recovered above is a PLAIN angle, and the q
! ray it is expressed in was divided by n0, so the number that multiplies that ray is
! n0 times the tangent. One in air, so nothing this macro has ever reported moves.
hmax = hmax * n0abs

! M (31.11)'s factor, where v'_pk is the p ray's OWN angle after the last surface.
! Paper III states the same rule in words: the entries are the augmented coefficients
! and "must be divided by N_k' v_pk'" to give the actual ones. The scheme's v is the
! ordinary angle rather than the reduced one, so the image-space index stands beside
! it. It is exactly the focal length only for a collimated beam.
vpk = rv(1 + nsm, 20)
IF (anyfig == 1) THEN vpk = rv(tdg + nsm, 20)
IF (ABSO(vpk) < eps)
   PRINT "The p ray leaves the last surface parallel to the axis - the system is"
   PRINT "afocal, and the seventh order cannot be put into transverse measure."
   GOTO 900
ENDIF
efac = efl / (nlsig * vpk)

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

VEC4(21) = efac * up7
VEC4(22) = efac * up6 * hmax
VEC4(23) = VEC4(22)
VEC4(24) = efac * up5 * h2
VEC4(25) = VEC4(24)
VEC4(26) = VEC4(24)
VEC4(27) = efac * up4 * h3
VEC4(28) = VEC4(27)
VEC4(29) = VEC4(27)
VEC4(30) = VEC4(27)
VEC4(31) = efac * up3 * h4
VEC4(32) = VEC4(31)
VEC4(33) = VEC4(31)
VEC4(34) = VEC4(31)
VEC4(35) = efac * up2 * h5
VEC4(36) = VEC4(35)
VEC4(37) = VEC4(35)
VEC4(38) = efac * um * h6
VEC4(39) = VEC4(38)
VEC4(40) = efac * h7

FOR k = 1, 20, 1
   VEC4(k) = VEC4(k) * VEC4(20 + k)
NEXT

! tau1 is the seventh-order spherical aberration, and the fifth-order working reaches
! it too - as B7 above. The two are already in the same measure, so the substitution
! needs no conversion and cannot introduce one. B7 is the better tested of the two,
! being checked against a closed-form mirror as well as a refractor; on a lens of
! spheres they agree exactly, which is what makes this a substitution rather than a
! fudge, and the scheme's own value is printed beside it so the two can be compared.
tau1s = VEC4(1)
VEC4(1) = sB7 * fnum

PRINT " "
PRINT "Seventh order, system totals, transverse measure - Buchdahl's tau."
PRINT " "
FORMAT 15.6 EXP
PRINT "   tau1 ", VEC4(1), "  tau2 ", VEC4(2), "  tau3 ", VEC4(3)
PRINT "   tau4 ", VEC4(4), "  tau5 ", VEC4(5), "  tau6 ", VEC4(6)
PRINT "   tau7 ", VEC4(7), "  tau8 ", VEC4(8), "  tau9 ", VEC4(9)
PRINT "   tau10", VEC4(10), "  tau11", VEC4(11), "  tau12", VEC4(12)
PRINT "   tau13", VEC4(13), "  tau14", VEC4(14), "  tau15", VEC4(15)
PRINT "   tau16", VEC4(16), "  tau17", VEC4(17), "  tau18", VEC4(18)
PRINT "   tau19", VEC4(19), "  tau20", VEC4(20)
PRINT " "
PRINT "tau1 above is B7, reached through the fifth-order working. The scheme reaches"
PRINT "it a second time through Table I, and gets"
PRINT "   tau1 ", tau1s
PRINT "On a lens of spheres the two agree exactly; on a figured lens they agree to"
PRINT "the accuracy of the arrangement, and a large disagreement is a warning."
PRINT " "
! FORBES declines a mirror, so on a reflecting system the check to point at is RAYINV.
anymir = 0
FOR i = 1, nsm, 1
   IF (ISMS(i)) THEN anymir = 1
NEXT
IF (anymir == 1)
   PRINT "This system reflects, and FORBES.ZPL declines a mirror. RAYINV.ZPL recovers"
   PRINT "the coefficients from OpticStudio's own real rays instead, sharing nothing with"
   PRINT "the scheme above; run it on this file and the two must agree to the resolution"
   PRINT "of its fit."
ELSE
   PRINT "FORBES.ZPL in this same folder reaches all twenty by order doubling and shares"
   PRINT "no arithmetic with the scheme above. Run it on this file and the two must"
   PRINT "agree - that is the check this macro exists to pass."
ENDIF

! ---------------------------------------------------------------------------------
! srf = 1. The twenty tau surface by surface.
!
! Table II is a linear re-expression of the ten totals and their barred partners, so
! applying it to ONE surface's ten gives that surface's share of each tau, and the
! shares add to the system value. The sum row under each table is that check, made
! where a reader can see it rather than asserted in a comment.
!
! tau1 is the exception and is taken from B7, the fifth-order working's route to the
! same quantity, because that is what the system total above is: the two agree exactly
! on a lens of spheres and to the accuracy of the arrangement on a figured one, and
! mixing the two routes between the total and its parts would make the column fail to
! add for a reason that has nothing to do with the lens.
!
! There is no intrinsic and induced split here; the note above the fifth-order tables
! says why.
! ---------------------------------------------------------------------------------

DECLARE tw, DOUBLE, 2, nsm + 2, 22

IF (srf == 1)
   FOR i = 1, nsm, 1
      tw(i, 1) = pt(i, 1)
      tw(i, 2) = pt(i, 11) + pt(i, 2) / 2
      tw(i, 3) = pt(i, 2) / 2
      tw(i, 4) = pt(i, 12) + pt(i, 3)
      tw(i, 5) = pt(i, 3)
      tw(i, 6) = pt(i, 4)
      tw(i, 7) = pt(i, 13) + pt(i, 14) / 2 + pt(i, 5) / 2 + 3 * pt(i, 7) / 8
      tw(i, 8) = (pt(i, 14) + pt(i, 5) + pt(i, 7)) / 2
      tw(i, 9) = pt(i, 5) / 2 + pt(i, 7) / 4
      tw(i, 10) = pt(i, 7) / 8
      tw(i, 11) = pt(i, 15) + pt(i, 6)
      tw(i, 12) = pt(i, 17) + pt(i, 8)
      tw(i, 13) = pt(i, 6)
      tw(i, 14) = pt(i, 8)
      tw(i, 15) = pt(i, 16) + pt(i, 18) / 2 + pt(i, 9) / 2
      tw(i, 16) = (pt(i, 18) + pt(i, 9)) / 2
      tw(i, 17) = pt(i, 9) / 2
      tw(i, 18) = pt(i, 19) + pt(i, 10)
      tw(i, 19) = pt(i, 10)
      tw(i, 20) = pt(i, 20)
      FOR k = 1, 20, 1
         tw(i, k) = tw(i, k) * VEC4(20 + k)
      NEXT
      ! The B7 route, to match the total printed above.
      tw(i, 1) = fo(i, 24) * fnum
   NEXT

   PRINT " "
   PRINT "==============================================================================="
   PRINT "SEVENTH ORDER, SURFACE BY SURFACE - the twenty tau, transverse measure."
   PRINT "==============================================================================="

   FOR kg = 0, 3, 1
      k1 = 5 * kg + 1
      PRINT " "
      IF (kg == 0)
         PRINT " Surf           tau1           tau2           tau3           tau4           tau5"
      ENDIF
      IF (kg == 1)
         PRINT " Surf           tau6           tau7           tau8           tau9          tau10"
      ENDIF
      IF (kg == 2)
         PRINT " Surf          tau11          tau12          tau13          tau14          tau15"
      ENDIF
      IF (kg == 3)
         PRINT " Surf          tau16          tau17          tau18          tau19          tau20"
      ENDIF
      ws1 = 0
      ws2 = 0
      ws3 = 0
      ws4 = 0
      ws5 = 0
      FOR i = 1, nsm, 1
         ws1 = ws1 + tw(i, k1)
         ws2 = ws2 + tw(i, k1 + 1)
         ws3 = ws3 + tw(i, k1 + 2)
         ws4 = ws4 + tw(i, k1 + 3)
         ws5 = ws5 + tw(i, k1 + 4)
         FORMAT 5.0
         PRINT i,
         FORMAT 15.6 EXP
         PRINT tw(i, k1), tw(i, k1 + 1), tw(i, k1 + 2), tw(i, k1 + 3), tw(i, k1 + 4)
      NEXT
      FORMAT 5.0
      PRINT "  SUM",
      FORMAT 15.6 EXP
      PRINT ws1, ws2, ws3, ws4, ws5
   NEXT
   PRINT " "
   PRINT "The SUM rows are the twenty totals printed above. They are computed here by a"
   PRINT "different route - summed after conversion rather than before it - so a"
   PRINT "disagreement between them is a fault in this macro and not in your lens."
   PRINT " "
ENDIF

GOTO 900

! =================================================================================
! SUBROUTINES
! =================================================================================

! ---------------------------------------------------------------------------------
! pmk - the monomial table for the trivariate algebra, built once.
!
!   mon(1, 1 + a*16 + b*4 + c)  the monomial number, 1..20, or zero past degree three
!   mon(2, m)             a of monomial m
!   mon(3, m)             b
!   mon(4, m)             c
!   mon(5, m)             the packed index 1 + a*16 + b*4 + c
! ---------------------------------------------------------------------------------
SUB pmk
   FOR ka = 1, 64, 1
      mon(1, ka) = 0
   NEXT
   km = 0
   FOR kd = 0, 3, 1
      FOR ka = kd, 0, -1
         FOR kb = kd - ka, 0, -1
            kc = kd - ka - kb
            km = km + 1
            mon(1, 1 + ka * 16 + kb * 4 + kc) = km
            mon(2, km) = ka
            mon(3, km) = kb
            mon(4, km) = kc
            mon(5, km) = 1 + ka * 16 + kb * 4 + kc
         NEXT
      NEXT
   NEXT
   nmon = km

   ! Table IV's weights: the power of Buchdahl's j each scaled theta monomial
   ! carries. They are what makes the physical form of the substitution work - j
   ! carries a factor of c0, and these are that factor's exponent.
   mon(6, 1) = 0
   mon(6, 2) = 1
   mon(6, 3) = 2
   mon(6, 4) = 2
   mon(6, 5) = 3
   mon(6, 6) = 4
   mon(6, 7) = 3
   mon(6, 8) = 4
   mon(6, 9) = 5
   mon(6, 10) = 6
RETURN

! ---------------------------------------------------------------------------------
! pmul - pw(gc, *) = pw(ga, *) * pw(gb, *), truncated at total degree three.
! Its own arguments are copied first, because nothing else may be trusted to survive.
! ---------------------------------------------------------------------------------
SUB pmul
   pmsa = ga
   pmsb = gb
   pmsc = gc
   FOR pmj = 1, 64, 1
      scr(1, pmj) = 0
   NEXT
   FOR pmi = 1, nmon, 1
      pmu = pw(pmsa, mon(5, pmi))
      IF (pmu != 0)
         pma = mon(2, pmi)
         pmb = mon(3, pmi)
         pmc = mon(4, pmi)
         FOR pmk2 = 1, nmon, 1
            pmv = pw(pmsb, mon(5, pmk2))
            IF (pmv != 0)
               pmd = pma + mon(2, pmk2)
               pme = pmb + mon(3, pmk2)
               pmf = pmc + mon(4, pmk2)
               IF (pmd + pme + pmf < 4)
                  pmx = 1 + pmd * 16 + pme * 4 + pmf
                  scr(1, pmx) = scr(1, pmx) + pmu * pmv
               ENDIF
            ENDIF
         NEXT
      ENDIF
   NEXT
   FOR pmj = 1, 64, 1
      pw(pmsc, pmj) = scr(1, pmj)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! pcubs - the ten cubic coefficients of slot ga, in Buchdahl's table order, into
! cub(1, 1..10): xi^3, xi^2 eta, xi^2 zeta, xi eta^2, xi eta zeta, xi zeta^2, eta^3,
! eta^2 zeta, eta zeta^2, zeta^3.
! ---------------------------------------------------------------------------------
SUB pcubs
   pqs = ga
   cub(1, 1) = pw(pqs, 1 + 3 * 16)
   cub(1, 2) = pw(pqs, 1 + 2 * 16 + 4)
   cub(1, 3) = pw(pqs, 1 + 2 * 16 + 1)
   cub(1, 4) = pw(pqs, 1 + 16 + 2 * 4)
   cub(1, 5) = pw(pqs, 1 + 16 + 4 + 1)
   cub(1, 6) = pw(pqs, 1 + 16 + 2)
   cub(1, 7) = pw(pqs, 1 + 3 * 4)
   cub(1, 8) = pw(pqs, 1 + 2 * 4 + 1)
   cub(1, 9) = pw(pqs, 1 + 4 + 2)
   cub(1, 10) = pw(pqs, 1 + 3)
RETURN

! ---------------------------------------------------------------------------------
! pquad - the six quadratic coefficients of slot ga, same sequence: xi^2, xi eta,
! xi zeta, eta^2, eta zeta, zeta^2.
! ---------------------------------------------------------------------------------
SUB pquad
   pqq = ga
   cub(1, 1) = pw(pqq, 1 + 2 * 16)
   cub(1, 2) = pw(pqq, 1 + 16 + 4)
   cub(1, 3) = pw(pqq, 1 + 16 + 1)
   cub(1, 4) = pw(pqq, 1 + 2 * 4)
   cub(1, 5) = pw(pqq, 1 + 4 + 1)
   cub(1, 6) = pw(pqq, 1 + 2)
RETURN

! ---------------------------------------------------------------------------------
! pcub3, pcub4 - the cubic parts of S3 and X3, which are slots 3 and 4.
! ---------------------------------------------------------------------------------
SUB pcub3
   ga = 3
   GOSUB pcubs
RETURN

SUB pcub4
   ga = 4
   GOSUB pcubs
RETURN

! ---------------------------------------------------------------------------------
! psurf - everything one surface contributes, M (77.4-5), (78.1), (77.2), (77.1) and
! paper II (4.1), built at the physical curvature sfc0 from sa1, sa2, sa3 and sfk.
!
! Slots written: 1 S1, 2 S2, 3 S3, 4 X3, 5 G1, 6 G2, 7 G3, 8 B1, 9 B2.
! Slots 10..19 are its own scratch and nothing outside may rely on them.
! ---------------------------------------------------------------------------------
SUB psurf
   pu1 = sa1
   pu2 = sa2
   pu3 = sa3
   puk = sfk
   pu0 = sfc0

   FOR pj = 1, 40, 1
      FOR pi2 = 1, 64, 1
         pw(pj, pi2) = 0
      NEXT
   NEXT

   ph1 = pu0 / 2
   ph2 = ph1 * ph1
   ph3 = ph2 * ph1
   ph4 = ph3 * ph1
   ph5 = ph4 * ph1
   ph6 = ph5 * ph1
   pk2 = puk * puk
   pms = 1 / (2 * puk)
   pmt = (puk - 1) / (2 * puk)
   pt0 = (1 - puk) / (2 * puk)

   ! sigma1 in slot 10
   pw(10, 1 + 16) = pms * 0.5 * pu0 * pu0
   pw(10, 1 + 4) = pms * (-pk2 * pu0)
   pw(10, 1 + 1) = pms * (pk2 - 1)

   ! sigma2 in slot 11
   pw(11, 1 + 2 * 16) = pms * (1.5 * ph1 * pu1 + 2 * ph4)
   pw(11, 1 + 16 + 4) = pms * (-(pk2 * pu1 + 4 * ph3))
   pw(11, 1 + 16 + 1) = pms * (2 * ph2)

   ! sigma3 in slot 12
   pw(12, 1 + 3 * 16) = pms * (4 * ph1 * pu2 / 3 + 4 * ph3 * pu1 + 0.25 * pu1 * pu1 + 4 * ph6)
   pw(12, 1 + 2 * 16 + 4) = pms * (-(pk2 * pu2 + (pk2 + 7) * ph2 * pu1 + 12 * ph5))
   pw(12, 1 + 2 * 16 + 1) = pms * (1.5 * ph1 * pu1 + 4 * ph4)
   pw(12, 1 + 16 + 2 * 4) = pms * (pk2 * pu0 * pu1 + 8 * ph4)
   pw(12, 1 + 16 + 4 + 1) = pms * (-4 * ph3)

   ! tau1 in slot 13
   pw(13, 1 + 16) = pmt * ((pk2 + 1) * pu0 * pu0)
   pw(13, 1 + 4) = pmt * (-2 * pk2 * pu0)
   pw(13, 1 + 1) = pmt * (pk2 - 1)

   ! tau2 in slot 14
   pw(14, 1 + 2 * 16) = pmt * ((4 * pk2 + 3) * ph1 * pu1)
   pw(14, 1 + 16 + 4) = pmt * (-(2 * pk2 * pu1 + pu0 * pu0 * pu0))
   pw(14, 1 + 16 + 1) = pmt * (pu0 * pu0)

   ! tau3 in slot 15
   pw(15, 1 + 3 * 16) = pmt * ((2 * pk2 + 4 / 3) * pu0 * pu2 + (pk2 + 0.5) * pu1 * pu1 + (4 * pk2 + 2) * ph3 * pu1)
   pw(15, 1 + 2 * 16 + 4) = pmt * (-(2 * pk2 * pu2 + (10 * pk2 + 14) * ph2 * pu1 + 8 * ph5))
   pw(15, 1 + 2 * 16 + 1) = pmt * (3 * ph1 * pu1 + 4 * ph4)
   pw(15, 1 + 16 + 2 * 4) = pmt * (2 * pk2 * pu0 * pu1 + pu0 * pu0 * pu0 * pu0)
   pw(15, 1 + 16 + 4 + 1) = pmt * (-pu0 * pu0 * pu0)

   ! S1 = tau1 + 2 sigma1 + (1+k)/(2k) zeta      (78.1)
   FOR pi2 = 1, 64, 1
      pw(1, pi2) = pw(13, pi2) + 2 * pw(10, pi2)
   NEXT
   pw(1, 1 + 1) = pw(1, 1 + 1) + (1 + puk) / (2 * puk)

   ! S2 = tau0 S1^2 + 2 (tau1 + sigma1) S1 + (tau2 + 2 sigma2)
   ga = 1
   gb = 1
   gc = 16
   GOSUB pmul
   FOR pi2 = 1, 64, 1
      pw(17, pi2) = pw(13, pi2) + pw(10, pi2)
   NEXT
   ga = 17
   gb = 1
   gc = 18
   GOSUB pmul
   FOR pi2 = 1, 64, 1
      pw(2, pi2) = pt0 * pw(16, pi2) + 2 * pw(18, pi2) + pw(14, pi2) + 2 * pw(11, pi2)
   NEXT

   ! S3 = 2 (tau0 S1 + tau1 + sigma1) S2 + (tau1 S1 + 2 tau2 + 2 sigma2) S1
   !      + (tau3 + 2 sigma3)
   FOR pi2 = 1, 64, 1
      pw(17, pi2) = pt0 * pw(1, pi2) + pw(13, pi2) + pw(10, pi2)
   NEXT
   ga = 17
   gb = 2
   gc = 18
   GOSUB pmul
   ga = 13
   gb = 1
   gc = 19
   GOSUB pmul
   FOR pi2 = 1, 64, 1
      pw(17, pi2) = pw(19, pi2) + 2 * pw(14, pi2) + 2 * pw(11, pi2)
   NEXT
   ga = 17
   gb = 1
   gc = 19
   GOSUB pmul
   FOR pi2 = 1, 64, 1
      pw(3, pi2) = 2 * pw(18, pi2) + pw(19, pi2) + pw(15, pi2) + 2 * pw(12, pi2)
   NEXT

   ! (77.2): the gamma. All zero for a sphere.
   pw(5, 1 + 16) = pu1

   pw(6, 1 + 2 * 16) = pu2 + ph2 * pu1
   pw(6, 1 + 16 + 4) = -pu0 * pu1

   pw(7, 1 + 3 * 16) = pu3 + pu0 * pu0 * pu2 / 3 + ph1 * pu1 * pu1 + 2 * ph4 * pu1
   pw(7, 1 + 2 * 16 + 4) = -(2 * pu0 * pu2 + 6 * ph3 * pu1 + 0.5 * pu1 * pu1)
   pw(7, 1 + 2 * 16 + 1) = ph2 * pu1
   pw(7, 1 + 16 + 2 * 4) = pu0 * pu0 * pu1

   ! (77.1) with theta2 and theta3 in closed form: solving (78.7) for beta gives
   ! -(p + ...)/c0 and the c0 cancels analytically but not numerically, which would
   ! put a division by zero on every plano surface. The cancellation is carried out
   ! here by hand instead.
   pe2 = pu0 * pu0 * pu0 / 8 + pu1 / 4
   pe3 = pu0 * pu0 * pu0 * pu0 * pu0 / 16 + pu0 * pu0 * pu1 / 4 + pu2 / 6

   pw(8, 1 + 16) = ph1

   pw(9, 1 + 2 * 16) = pe2
   pw(9, 1 + 16 + 4) = -2 * ph2

   ! ZEROED FIRST, AND THAT IS NOT HOUSEKEEPING. beta3 sets four coefficients and
   ! leaves the other sixteen at zero, but slot 19 has just been used as scratch for
   ! S3's second product - so without this the four are overwritten and the rest of
   ! beta3 is that stale product. It shows in X3's fifth and sixth cubic coefficients
   ! alone, xi eta zeta and xi zeta^2, because those are the only two of the first six
   ! beta3 does not write; and because the stale product carries tau2 and sigma2, which
   ! carry c1, it makes nu-prime depend on the figuring where it must not.
   !
   ! Any polynomial built by naming a few coefficients needs the same care. sigma1..3,
   ! tau1..3, beta1 and beta2 are safe only because they are written before the first
   ! multiply, while the whole workspace is still zero from the top of this routine.
   FOR pi2 = 1, 64, 1
      pw(19, pi2) = 0
   NEXT
   pw(19, 1 + 3 * 16) = pe3
   pw(19, 1 + 2 * 16 + 4) = -6 * ph1 * pe2
   pw(19, 1 + 2 * 16 + 1) = ph3
   pw(19, 1 + 16 + 2 * 4) = 4 * ph3

   ! X3 = beta1 S2 + beta2 S1 + beta3                             paper II (4.1)
   ga = 8
   gb = 2
   gc = 16
   GOSUB pmul
   ga = 9
   gb = 1
   gc = 17
   GOSUB pmul
   FOR pi2 = 1, 64, 1
      pw(4, pi2) = pw(16, pi2) + pw(17, pi2) + pw(19, pi2)
   NEXT
RETURN


! ---------------------------------------------------------------------------------
! bridge - the seven conversion constants, and the increments they produce.
!
! The scheme computes its primary and secondary from spherical formulae; the
! fifth-order code computes the figuring's own contribution in ITS variables. The two
! differ by one constant per coefficient, and those constants are measured rather
! than derived: on a spherical system both routes are right, so the ratio of the
! scheme's entry to the corresponding fifth-order quantity IS the conversion.
!
! The pairing is the one the fifth-order code's own mapping implies - its mu are its
! s multiplied by a ray angle, so B5, F2, M2, M3 and N3 each carry one factor of the
! marginal incidence, and the sixth needs Pi5 + C5, that same product for S6p. Taking
! E5 instead pairs S6p through the CHIEF incidence and the ratio then varies from
! surface to surface, which is how the right pairing was found.
!
! The constant is taken from whichever surface states each quantity most strongly.
! That is only a guard against dividing by a coefficient that happens to vanish: the
! ratio is the same on every surface, and chk = 1 prints it so that can be seen.
! ---------------------------------------------------------------------------------
SUB bridge
   brs = 1
   IF (grun == 4) THEN brs = 2
   brp = grun - 1
   brg = (brp - 1) * (nsm + 2) + 1

   FOR brq = 1, 7, 1
      bw(1, brq) = 0
      bw(2, brq) = 0
   NEXT

   FOR bri = 1, nsm, 1
      brm = (brs - 1) * (nsm + 2) + bri
      brw = brg + bri

      bw(3, 1) = tt(brw, 10)
      bw(3, 2) = tt(brw, 38)
      bw(3, 3) = tt(brw, 44)
      bw(3, 4) = tt(brw, 50)
      bw(3, 5) = tt(brw, 54)
      bw(3, 6) = tt(brw, 59)
      bw(3, 7) = tt(brw, 65)

      FOR brq = 1, 7, 1
         brv = mq(brm, brq)
         IF (ABSO(brv) > bw(2, brq))
            bw(2, brq) = ABSO(brv)
            bw(1, brq) = bw(3, brq) / brv
         ENDIF
      NEXT
   NEXT

   FOR bri = 1, nsm, 1
      brm = (brs - 1) * (nsm + 2) + bri
      brr = (grun - 1) * (nsm + 2) + 1 + bri
      FOR brq = 1, 7, 1
         ai(brr, brq) = bw(1, brq) * mq(brm, 7 + brq)
      NEXT
   NEXT

   IF (chk == 1)
      PRINT " "
      FORMAT 3.0
      PRINT "Bridge constants, pass ", grun, " - the same on every surface or the"
      PRINT "pairing is wrong and everything built on the increments is meaningless."
      FORMAT 15.6 EXP
      PRINT "   a_p ", bw(1, 1), "  S1p ", bw(1, 2), "  S2p ", bw(1, 3)
      PRINT "   S3p ", bw(1, 4), "  S4p ", bw(1, 5), "  S5p ", bw(1, 6)
      PRINT "   S6p ", bw(1, 7)
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! exbas - the theta substitution's basis, for one surface and one pass.
!
! Paper II sets c0 = 1 and restores it afterwards, which makes xi carry c0^2 and eta
! carry c0, so the figuring would have to be divided by powers of the curvature and a
! plano surface could not be carried through at all. Rewritten in physical variables
! the substitution has no division in it:
!
!     xi   = y^2 T1 + 2y T2 + T3
!     eta  = y v T1 + (c0 y + v) T2 + c0 T3
!     zeta = v^2 T1 + 2 c0 v T2 + c0^2 T3
!
! The ten cubic monomials in xi, eta, zeta are built here once, into slots 25..34,
! and the six quadratic ones into 35..40. Both expansions below then cost no
! multiplies at all - they are weighted sums of these.
! ---------------------------------------------------------------------------------
SUB exbas
   exy = exyy
   exv = exvv
   exc = excc

   FOR exj = 25, 40, 1
      FOR exi = 1, 64, 1
         pw(exj, exi) = 0
      NEXT
   NEXT

   ! xi in 23, eta in 24, zeta in 20 - scratch slots psurf is finished with.
   FOR exj = 1, 64, 1
      pw(23, exj) = 0
      pw(24, exj) = 0
      pw(20, exj) = 0
   NEXT
   pw(23, 1 + 16) = exy * exy
   pw(23, 1 + 4) = 2 * exy
   pw(23, 1 + 1) = 1
   pw(24, 1 + 16) = exy * exv
   pw(24, 1 + 4) = exc * exy + exv
   pw(24, 1 + 1) = exc
   pw(20, 1 + 16) = exv * exv
   pw(20, 1 + 4) = 2 * exc * exv
   pw(20, 1 + 1) = exc * exc

   ! The quadratics first: xi^2, xi eta, xi zeta, eta^2, eta zeta, zeta^2.
   ga = 23
   gb = 23
   gc = 35
   GOSUB pmul
   ga = 23
   gb = 24
   gc = 36
   GOSUB pmul
   ga = 23
   gb = 20
   gc = 37
   GOSUB pmul
   ga = 24
   gb = 24
   gc = 38
   GOSUB pmul
   ga = 24
   gb = 20
   gc = 39
   GOSUB pmul
   ga = 20
   gb = 20
   gc = 40
   GOSUB pmul

   ! The cubics, each one multiply from a quadratic already built:
   ! xi^3, xi^2 eta, xi^2 zeta, xi eta^2, xi eta zeta, xi zeta^2,
   ! eta^3, eta^2 zeta, eta zeta^2, zeta^3.
   ga = 35
   gb = 23
   gc = 25
   GOSUB pmul
   ga = 35
   gb = 24
   gc = 26
   GOSUB pmul
   ga = 35
   gb = 20
   gc = 27
   GOSUB pmul
   ga = 38
   gb = 23
   gc = 28
   GOSUB pmul
   ga = 36
   gb = 20
   gc = 29
   GOSUB pmul
   ga = 40
   gb = 23
   gc = 30
   GOSUB pmul
   ga = 38
   gb = 24
   gc = 31
   GOSUB pmul
   ga = 38
   gb = 20
   gc = 32
   GOSUB pmul
   ga = 40
   gb = 24
   gc = 33
   GOSUB pmul
   ga = 40
   gb = 20
   gc = 34
   GOSUB pmul
RETURN

! ---------------------------------------------------------------------------------
! excub - expand the cubic whose ten coefficients are in cub(2, 1..10) over the basis
! exbas built, leaving the ten theta coefficients in cub(1, 1..10).
! ---------------------------------------------------------------------------------
SUB excub
   FOR ecj = 1, 10, 1
      cub(1, ecj) = 0
   NEXT
   FOR ecm = 1, 10, 1
      ecw = cub(2, ecm)
      IF (ecw != 0)
         ecs = 24 + ecm
         cub(1, 1) = cub(1, 1) + ecw * pw(ecs, 1 + 3 * 16)
         cub(1, 2) = cub(1, 2) + ecw * pw(ecs, 1 + 2 * 16 + 4)
         cub(1, 3) = cub(1, 3) + ecw * pw(ecs, 1 + 2 * 16 + 1)
         cub(1, 4) = cub(1, 4) + ecw * pw(ecs, 1 + 16 + 2 * 4)
         cub(1, 5) = cub(1, 5) + ecw * pw(ecs, 1 + 16 + 4 + 1)
         cub(1, 6) = cub(1, 6) + ecw * pw(ecs, 1 + 16 + 2)
         cub(1, 7) = cub(1, 7) + ecw * pw(ecs, 1 + 3 * 4)
         cub(1, 8) = cub(1, 8) + ecw * pw(ecs, 1 + 2 * 4 + 1)
         cub(1, 9) = cub(1, 9) + ecw * pw(ecs, 1 + 4 + 2)
         cub(1, 10) = cub(1, 10) + ecw * pw(ecs, 1 + 3)
      ENDIF
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! exqua - the same at one degree lower: the six quadratic coefficients in
! cub(2, 1..6) out as six theta coefficients in cub(1, 1..6).
!
! Paper II Sec. 2 states the substitution and what it produces - "the coefficients of
! the resulting polynomial in theta1, theta2, theta3 correspond exactly to the
! secondary quantities". The BASIS matters: the secondary coefficients are not a
! reweighting of the xi-eta-zeta monomials, which is why comparing them in that basis
! gives ratios that are neither constant nor any power law.
! ---------------------------------------------------------------------------------
SUB exqua
   FOR eqj = 1, 6, 1
      cub(1, eqj) = 0
   NEXT
   FOR eqm = 1, 6, 1
      eqw = cub(2, eqm)
      IF (eqw != 0)
         eqs = 34 + eqm
         cub(1, 1) = cub(1, 1) + eqw * pw(eqs, 1 + 2 * 16)
         cub(1, 2) = cub(1, 2) + eqw * pw(eqs, 1 + 16 + 4)
         cub(1, 3) = cub(1, 3) + eqw * pw(eqs, 1 + 16 + 1)
         cub(1, 4) = cub(1, 4) + eqw * pw(eqs, 1 + 2 * 4)
         cub(1, 5) = cub(1, 5) + eqw * pw(eqs, 1 + 4 + 1)
         cub(1, 6) = cub(1, 6) + eqw * pw(eqs, 1 + 2)
      ENDIF
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! dhalf - the figuring's D half of the intrinsic secondary, M (60.3).
!
! (65.5) puts no figuring in D at first order, which is why the primary is exact with
! the whole increment on the height ratio. (65.6) puts it there at second order, so
! from the secondary on the increment is q D + rho L where the scheme forms
! rho (D + L). The difference is (q - rho) D and it lands on the BARRED entry alone -
! the unbarred total is D + L either way and is already right.
!
! The polynomials are rebuilt here at the curvature dhc rather than read from pc.
! That costs a psurf and an exbas per figured surface per pass, and it buys the flat
! case: SecondaryDHalf forms s1 (j/c)^p dTheta / sTheta0, in which every power of c
! cancels - but only after it has been formed, so a flat surface gives 0/0. A flat
! figured surface therefore takes the limit symmetrically from c = +/-1e-6, with the
! incidence, q and j re-formed at each, because j = c L / i_p carries the curvature
! as a factor: on the flat row j is exactly zero where its limit L/i_p is not.
!
! In: dhc curvature, dhy y_p, dhv v_p, dhyq y_q, dhvq v_q, dhk k, dhs1 the spherical
!     s1, dhc1/dhc2/dhc3 the figuring.  Out: sc(7, 1..6).
! ---------------------------------------------------------------------------------
SUB dhalf
   dhi = dhc * dhy - dhv
   dhq = 0
   IF (ABSO(dhi) > 1E-30) THEN dhq = (dhc * dhyq - dhvq) / dhi
   dhj = -dhv * dhq + dhvq

   ! The surface's polynomials at this curvature, figured and unfigured.
   sfk = dhk
   sfc0 = dhc
   sa1 = dhc1
   sa2 = dhc2
   sa3 = dhc3
   GOSUB psurf
   ga = 2
   GOSUB pquad
   FOR dhm = 1, 6, 1
      dh(1, dhm) = cub(1, dhm)
   NEXT
   ga = 8
   gb = 1
   gc = 21
   GOSUB pmul
   FOR dhm = 1, 64, 1
      pw(22, dhm) = pw(21, dhm) + pw(9, dhm)
   NEXT
   ga = 22
   GOSUB pquad
   FOR dhm = 1, 6, 1
      dh(2, dhm) = cub(1, dhm)
   NEXT

   sa1 = 0
   sa2 = 0
   sa3 = 0
   GOSUB psurf
   ga = 2
   GOSUB pquad
   FOR dhm = 1, 6, 1
      dh(3, dhm) = cub(1, dhm)
   NEXT
   ga = 8
   gb = 1
   gc = 21
   GOSUB pmul
   FOR dhm = 1, 64, 1
      pw(22, dhm) = pw(21, dhm) + pw(9, dhm)
   NEXT
   ga = 22
   GOSUB pquad
   FOR dhm = 1, 6, 1
      dh(4, dhm) = cub(1, dhm)
   NEXT

   ! The theta basis at this curvature.
   exyy = dhy
   exvv = dhv
   excc = dhc
   GOSUB exbas

   dhvp = (1 - dhk) * dhc * dhy + dhk * dhv

   ! D(2) = y S(2) - v" X(2), figured less unfigured.
   FOR dhm = 1, 6, 1
      cub(2, dhm) = dhy * (dh(1, dhm) - dh(3, dhm)) - dhvp * (dh(2, dhm) - dh(4, dhm))
   NEXT
   GOSUB exqua
   FOR dhm = 1, 6, 1
      dh(5, dhm) = cub(1, dhm)
   NEXT

   ! The bare spherical D(2), whose first theta coefficient sets the scale.
   FOR dhm = 1, 6, 1
      cub(2, dhm) = dhy * dh(3, dhm) - dhvp * dh(4, dhm)
   NEXT
   GOSUB exqua

   dhbase = cub(1, 1) * dhi / dhc
   FOR dhm = 1, 6, 1
      sc(7, dhm) = 0
   NEXT
   ! A surface whose spherical D(2) has no scale to set gives no D half. Written as
   ! a block rather than an early RETURN: no macro in this folder uses THEN RETURN,
   ! and an untested construct is what the VEC6 fault was.
   IF (ABSO(dhbase) > 1E-25)

   dhn = dhs1 / dhbase
   dhjc = dhj / dhc
   dhic = dhi / dhc

   ! The j powers of the six, M (60.3) read at second order: 0, 1, 2, 2, 3, 4.
   dh(6, 1) = dhn * dhic * dh(5, 1)
   dh(6, 2) = dhn * dhjc * dhic * dh(5, 2)
   dh(6, 3) = dhn * dhjc * dhjc * dhic * dh(5, 3)
   dh(6, 4) = dhn * dhjc * dhjc * dhic * dh(5, 4)
   dh(6, 5) = dhn * dhjc * dhjc * dhjc * dhic * dh(5, 5)
   dh(6, 6) = dhn * dhjc * dhjc * dhjc * dhjc * dhic * dh(5, 6)

   dhw2 = -0.5 * dh(6, 4)
   dh(7, 1) = dh(6, 1)
   dh(7, 2) = dh(6, 2)
   dh(7, 3) = dhw2
   dh(7, 4) = dh(6, 3) - dhw2
   dh(7, 5) = dh(6, 5)
   dh(7, 6) = dh(6, 6)

   dhq1 = dhq
   dhq2 = dhq1 * dhq1
   dhq3 = dhq2 * dhq1
   dhq4 = dhq3 * dhq1
   dha = dh(7, 1)

   sc(7, 1) = dha
   sc(7, 2) = 4 * dhq1 * dha + dh(7, 2)
   sc(7, 3) = 2 * dhq2 * dha + dhq1 * dh(7, 2) + dh(7, 3) + dh(7, 4)
   sc(7, 4) = 4 * dhq2 * dha + 2 * dhq1 * dh(7, 2) - 2 * dh(7, 3)
   sc(7, 5) = 4 * dhq3 * dha + 3 * dhq2 * dh(7, 2) - 2 * dhq1 * dh(7, 3)
   sc(7, 5) = sc(7, 5) + 2 * dhq1 * dh(7, 4) + dh(7, 5)
   sc(7, 6) = dhq4 * dha + dhq3 * dh(7, 2) - dhq2 * dh(7, 3) + dhq2 * dh(7, 4)
   sc(7, 6) = sc(7, 6) + dhq1 * dh(7, 5) + dh(7, 6)
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! secnd - Buchdahl's secondary construction, M (84.41-42), run once per half.
!
! Every barred entry is s-bar_mu = q s_mu + a bracket in the dagger family, so the
! only place q appears is the LIFT q s_mu. It is never formed as a product with q:
! the caller supplies q a, q^2 a, q c13 and q omega already carried on the incidences,
! and the lifts of the intrinsic six in sc(1, *). Each q line below is the s line
! above it with every factor advanced one power of the incidence ratio, which is why
! they pair off.
!
! In:  scb row base, sca a, scab q a, sccc c13, scq2 q^2 a, scqc q c13, scqo q omega,
!      sc(1,*) the six lifts, sc(2,*) the nine dagger entries, sc(3,*) the six s,
!      scst 1 when this is the standalone (spherical) half.
! Out: sc(4,*) total, sc(5,*) barred, sc(6,*) the five mid entries.
! ---------------------------------------------------------------------------------
SUB secnd
   scw1 = 0
   scw2 = 0
   scw3 = 0
   IF (scst == 1)
      scw1 = -tt(scb, 8) * tt(scb, 15)
      scw2 = -tt(scb, 8) * tt(scb, 16)
      scw3 = -(tt(scb, 17) + tt(scb, 18)) * tt(scb, 8)
   ENDIF
   scv1 = 0
   scv2 = 0
   scv3 = 0
   IF (scst == 1)
      scv1 = -scqo * tt(scb, 15)
      scv2 = -scqo * tt(scb, 16)
      scv3 = -(tt(scb, 17) + tt(scb, 18)) * scqo
   ENDIF

   scs1 = sc(3, 1) + 3 * sca * sc(2, 1)
   scq1 = sc(1, 1) + 3 * scab * sc(2, 1)
   sc(4, 1) = scs1
   sc(5, 1) = sca * sc(2, 7) + scq1

   scm2 = 2 * (scab * sc(2, 1) + (sc(2, 2) + sc(2, 3)) * sca) + scw1 + sc(3, 2)
   scn2 = 2 * (scq2 * sc(2, 1) + (sc(2, 2) + sc(2, 3)) * scab) + scv1 + sc(1, 2)
   sc(6, 1) = scm2
   scs2 = 2 * scab * sc(2, 1) + sca * sc(2, 3) + scm2
   scq2b = 2 * scq2 * sc(2, 1) + scab * sc(2, 3) + scn2
   sc(4, 2) = scs2
   sc(5, 2) = 2 * scab * sc(2, 7) + sca * sc(2, 8) + scq2b

   scm3 = 2 * (scab * sc(2, 2) + sc(2, 5) * sca) + scw2 + sc(3, 3)
   scn3 = 2 * (scq2 * sc(2, 2) + sc(2, 5) * scab) + scv2 + sc(1, 3)
   sc(6, 2) = scm3
   scs3 = sccc * sc(2, 1) + sca * sc(2, 5) + scm3
   scq3 = scqc * sc(2, 1) + scab * sc(2, 5) + scn3
   sc(4, 3) = scs3
   sc(5, 3) = sca * sc(2, 9) + sccc * sc(2, 7) + scq3

   scm4 = 2 * (scm3 - sc(3, 3)) + sc(3, 4)
   scn4 = 2 * (scn3 - sc(1, 3)) + sc(1, 4)
   sc(6, 3) = scm4
   scs4 = 2 * scab * sc(2, 3) + scm4
   scq4 = 2 * scq2 * sc(2, 3) + scn4
   sc(4, 4) = scs4
   sc(5, 4) = 2 * scab * sc(2, 8) + scq4

   scm5 = scw3 + sc(3, 5) + 2 * (scab * (sc(2, 4) + sc(2, 5)) + sc(2, 6) * sca)
   scn5 = scv3 + sc(1, 5) + 2 * (scq2 * (sc(2, 4) + sc(2, 5)) + sc(2, 6) * scab)
   sc(6, 4) = scm5
   scs5 = 2 * scab * sc(2, 5) + sccc * sc(2, 3) + scm5
   scq5 = 2 * scq2 * sc(2, 5) + scqc * sc(2, 3) + scn5
   sc(4, 5) = scs5
   sc(5, 5) = 2 * scab * sc(2, 9) + sccc * sc(2, 8) + scq5

   scm6 = 2 * (sccc * tt(scb, 19) - scab * tt(scb, 24)) + sc(3, 6)
   scn6 = 2 * (scqc * tt(scb, 19) - scq2 * tt(scb, 24)) + sc(1, 6)
   sc(6, 5) = scm6
   scs6 = sccc * sc(2, 5) + scm6
   scq6 = scqc * sc(2, 5) + scn6
   sc(4, 6) = scs6
   sc(5, 6) = sccc * sc(2, 9) + scq6
RETURN

! ---------------------------------------------------------------------------------
! tablei - one pass of Buchdahl's Table I, into rows tig + 1 .. tig + nsm.
!
! Six loops, and they cannot be merged: each needs totals over the surfaces before it
! that are not known while the first walk forward is still going on.
!
!   A  the two rays, the intrinsic primaries, w and the three phi, the intrinsic z,
!      and the figuring's own tertiary increment
!   B  the accumulations t15..t20 - what each surface inherits
!   C  t21..t24, the dagger family t25..t33, and the same six on the height ratio
!   D  the six intrinsic secondaries, their lifts, their barred partners, the D half
!   E  running sums of the secondary, t69..t80
!   F  the q-side closed forms t83..t98 and the tertiary dagger t99..t120
! ---------------------------------------------------------------------------------
SUB tablei
   tig = (grun - 1) * (nsm + 2) + 1

   FOR tii = 0, nsm + 1, 1
      tib = tig + tii
      FOR tij = 1, 160, 1
         tt(tib, tij) = 0
      NEXT
      FOR tij = 1, 30, 1
         yf(tib, tij) = 0
      NEXT
      FOR tij = 1, 79, 1
         rv(tib, tij) = 0
      NEXT
   NEXT

   ! ------------------------------------------------------------------------------
   ! LOOP A. Buchdahl's angle is v = -u and his transfer is y(i) = y(i-1) - d v(i),
   ! the d being the gap BEFORE the surface. The primed angle v" = (1-k) c y + k v is
   ! what the transfer carries forward, so it is held from one pass to the next.
   ! ------------------------------------------------------------------------------
   tivp = 0
   tivq = 0

   FOR tii = 1, nsm, 1
      tib = tig + tii
      tipr = tib - 1

      tic = vfm(tii, 1) * efl
      tinb = INDX(tii - 1)
      tina = INDX(tii)
      IF (ISMS(tii - 1)) THEN tinb = -tinb
      IF (ISMS(tii)) THEN tina = -tina
      IF (gdual == 1)
         tinb = -tinb
         tina = -tina
      ENDIF
      IF (ABSO(tina) < 1E-12) THEN tina = 1
      tik = tinb / tina
      tin = tinb
      tid = 0
      IF (tii > 1) THEN tid = THIC(tii - 1) / efl

      IF (tii == 1)
         ! M (13.4), reduced OT-coordinates. iota = 1/l_01 in focal lengths is the
         ! only quantity in the whole scheme that knows where the object is.
         !
         ! THE PAIR MUST CARRY A LAGRANGE INVARIANT OF ONE, and in an immersed object
         ! space it does not unless the q ray is divided by the object index: the
         ! invariant of these four values is n0 (1 - p iota)/g = n0 exactly. The
         ! scheme is not homogeneous in the q ray's scale - a_p divides by that ray
         ! combination while the field terms multiply by powers of it - so the error
         ! cannot be absorbed downstream and comes out differently in every tau.
         ! "Reduced" is the word above: the angle is N u, so v_q = 1 means a PLAIN
         ! angle of 1/n0, and the transfer below carries plain angles. At n0 = 1 this
         ! is a division by exactly 1.0. The field variable carries the other half;
         ! see hmax.
         tigoe = 1 - stopp * iota
         IF (ABSO(tigoe) < 1E-12) THEN tigoe = 1
         tt(tib, 1) = 1
         tt(tib, 2) = iota
         tt(tib, 4) = stopp / tigoe / n0abs
         tt(tib, 5) = 1 / tigoe / n0abs
         IF (gdual == 1)
            tisw = tt(tib, 1)
            tt(tib, 1) = tt(tib, 4)
            tt(tib, 4) = tisw
            tisw = tt(tib, 2)
            tt(tib, 2) = tt(tib, 5)
            tt(tib, 5) = tisw
         ENDIF
      ELSE
         tt(tib, 2) = tivp
         tt(tib, 1) = tt(tipr, 1) - tid * tt(tib, 2)
         tt(tib, 5) = tivq
         tt(tib, 4) = tt(tipr, 4) - tid * tt(tib, 5)
      ENDIF

      tt(tib, 3) = tic * tt(tib, 1) - tt(tib, 2)
      tt(tib, 6) = 0
      IF (ABSO(tt(tib, 3)) > 1E-30)
         tt(tib, 6) = (tic * tt(tib, 4) - tt(tib, 5)) / tt(tib, 3)
      ENDIF
      tt(tib, 7) = -tt(tib, 2) * tt(tib, 6) + tt(tib, 5)
      tt(tib, 8) = (tik - 1) * tic / tin
      tt(tib, 9) = tt(tib, 2) * tt(tib, 2)

      tivp = (1 - tik) * tic * tt(tib, 1) + tik * tt(tib, 2)
      tivq = (1 - tik) * tic * tt(tib, 4) + tik * tt(tib, 5)
      rv(tib, 20) = tivp
      rv(tib, 21) = tivq
      ti2s = tivp
      ti9s = tivp * tivp

      ! ---------------------------------------------------------------------------
      ! t10..t14. a_p carries Omega/j and BOTH vanish on a plane, so the naive form
      ! is 0/0 there. Substituting q = (c y_q - v_q)/(c y_p - v_p) into
      ! j = -v_p q + v_q gives j = c (v_q y_p - v_p y_q) / i_p, exactly proportional
      ! to c, the bracket being the Lagrange invariant - so c/j has the finite
      ! non-zero limit i_p/(v_q y_p - v_p y_q) and a_p is perfectly well defined on a
      ! plane, which it has to be: a plane surface in a converging beam contributes
      ! spherical aberration like any other.
      !
      ! M Sec. 84(e) is what makes the rest possible. The hatted quantities "appear
      ! only in the combinations i_p t_mu-p, i_q t_mu-p", so the powers of q are
      ! carried on the incidences and never formed alone: with a_p = g i^2,
      ! q a_p = g i i_q and q^2 a_p = g i_q^2, no division anywhere.
      ! ---------------------------------------------------------------------------
      tilag = tt(tib, 5) * tt(tib, 1) - tt(tib, 2) * tt(tib, 4)
      tiap = 0
      tig2 = 0
      IF (ABSO(tilag) > 1E-30)
         tig2 = 0.5 * (ti2s - tt(tib, 3)) * (tik - 1) * tt(tib, 1) / tilag
         tiap = tig2 * tt(tib, 3) * tt(tib, 3)
      ENDIF

      tiaf = ai(tib, 1)
      tirho = 0
      IF (ABSO(tt(tib, 1)) > 1E-30) THEN tirho = tt(tib, 4) / tt(tib, 1)
      tiiq = tic * tt(tib, 4) - tt(tib, 5)

      ! The figuring's primary comes entirely from the L half of (60.3) - the
      ! fifth-order code forms it as c1b times pure ray heights with no incidence
      ! anywhere - so its chain ratio is the HEIGHT ratio, not q. Running it down the
      ! q chain gets even the sign wrong.
      tt(tib, 10) = tiap + tiaf
      tt(tib, 11) = tig2 * tt(tib, 3) * tiiq + tirho * tiaf
      tt(tib, 12) = 2 * (tig2 * tiiq * tiiq + tirho * tirho * tiaf)
      tic13 = tig2 * tiiq * tiiq - 0.5 * tt(tib, 8)
      ticf = tirho * tirho * tiaf
      tt(tib, 13) = tic13 + ticf

      ! c/i is finite wherever v is: on a flat surface facing collimated space v = 0
      ! and c/i is exactly 1/y. It diverges only where i vanishes with v NOT zero - a
      ! surface concentric about the marginal focus - and there q is unbounded and
      ! the condensed scheme has no value to offer, so the guard is kept for that
      ! case alone.
      ticoi = 0
      IF (ABSO(tt(tib, 3)) > 1E-30)
         ticoi = tic / tt(tib, 3)
      ELSE
         IF (ABSO(tt(tib, 2)) < 1E-30)
            IF (ABSO(tt(tib, 1)) > 1E-30) THEN ticoi = 1 / tt(tib, 1)
         ENDIF
      ENDIF

      tiq3 = tiiq * tiiq * tiiq
      tiqc = 0
      IF (ABSO(tilag) > 1E-30)
         tiz = ticoi * (tt(tib, 1) * tt(tib, 1) * tiq3 / tilag - tiiq / tin)
         tiz = tiz - (1 + tik) * tt(tib, 1) * tiq3 / tilag
         tiqc = 0.5 * (tik - 1) * tiz
      ENDIF
      tt(tib, 14) = tiqc + tirho * ticf

      rv(tib, 1) = tirho
      rv(tib, 2) = tiap
      rv(tib, 3) = tiaf
      rv(tib, 4) = tic13
      rv(tib, 5) = ticf
      rv(tib, 6) = ticoi
      rv(tib, 7) = tiiq
      rv(tib, 8) = tilag
      rv(tib, 9) = tik
      rv(tib, 10) = tin
      rv(tib, 11) = tig2 * tt(tib, 3) * tiiq
      rv(tib, 12) = tig2 * tiiq * tiiq
      rv(tib, 13) = tiqc
      rv(tib, 14) = (tik - 1) * ticoi * tiiq / tin
      rv(tib, 15) = tirho * ticf
      rv(tib, 19) = tic

      ! A figured flat facing collimated light: q is infinite and the scheme has no
      ! finite form for it here.
      rv(tib, 18) = 0
      IF (ABSO(tt(tib, 3)) < 1E-30)
         IF (ABSO(tt(tib, 2)) < 1E-30)
            IF (ABSO(tilag) > 1E-30)
               IF (ABSO(tt(tib, 1)) > 1E-30)
                  rv(tib, 18) = 1
                  IF (vfm(tii, 6) == 1) THEN flatbad = 1
               ENDIF
            ENDIF
         ENDIF
      ENDIF

      ! ---------------------------------------------------------------------------
      ! t34..t37 and the w family. t35 is phi1 and carries NO factor of one half -
      ! paper II (8.1) gives (k-1) i v, and the half Table I appears to show is
      ! spurious. It cannot be settled on surface 1, where v is zero and phi1
      ! vanishes either way; on surface 2 the half puts z3 + z4 at 16.162 where
      ! Buchdahl's printed columns require 15.249, and without it it is 15.2499.
      !
      ! w1 and w2 are built from the SPHERICAL a_p. The figuring's contribution to
      ! the secondary comes in as its own increment below; letting it through here as
      ! well would count it twice.
      ! ---------------------------------------------------------------------------
      tt(tib, 34) = ((tik * tik + 1) * tt(tib, 3) * tt(tib, 3) + ti9s - 3 * tt(tib, 9)) / 8
      tt(tib, 35) = (tik - 1) * tt(tib, 2) * tt(tib, 3)
      tt(tib, 36) = 0.5 * (-tik * tt(tib, 3) * tt(tib, 3) - tt(tib, 9) + 8 * tt(tib, 34) + 3 * tt(tib, 35))
      tt(tib, 37) = 2 * tt(tib, 9) + 8 * tt(tib, 34) + 5 * tt(tib, 35) - tt(tib, 36)

      tt(tib, 43) = (ti2s - 3 * tt(tib, 2)) * tt(tib, 7) * tiap
      tt(tib, 48) = 0.5 * tt(tib, 7) * tt(tib, 7) * tiap
      tt(tib, 49) = 0.25 * (ti9s - tt(tib, 9)) * ti2s * tt(tib, 7)
      tt(tib, 58) = 0.5 * tt(tib, 8) * tt(tib, 8)
      tt(tib, 64) = tt(tib, 7) * tt(tib, 7) * tt(tib, 8) / 8

      tt(tib, 81) = tt(tib, 2) * tt(tib, 5)
      tt(tib, 82) = tt(tib, 5) * tt(tib, 5)

      ! ---------------------------------------------------------------------------
      ! z1..z10, paper II (8.3) as Table I writes them, from the spherical a_p for
      ! the same reason w1 and w2 are. The induced terms further down take the full
      ! t10.
      ! ---------------------------------------------------------------------------
      tt(tib, 121) = (0.25 * (2 * ti2s - 5 * tt(tib, 2)) * tt(tib, 7) * tiap + 10 * tt(tib, 34) * tt(tib, 34)) * tiap
      tt(tib, 122) = 0.5 * ((ti2s - tt(tib, 3)) * tic * tt(tib, 1) * tt(tib, 2) * tt(tib, 7) * tiap + 10 * tt(tib, 34) * tt(tib, 43))
      tt(tib, 123) = ((2 * tt(tib, 34) + 0.5 * tt(tib, 36) - 0.75 * tt(tib, 35)) * tt(tib, 35) - tt(tib, 34) * tt(tib, 37)) * 0.75 * tt(tib, 8)
      tt(tib, 124) = (5 * tt(tib, 9) - 10 * tt(tib, 34) + 2 * tt(tib, 36)) * 0.25 * tt(tib, 48)
      tt(tib, 125) = -0.5 * (tt(tib, 2) + ti2s) * tt(tib, 7) * tt(tib, 8) * tt(tib, 36)
      tt(tib, 126) = 2 * (tt(tib, 9) + tt(tib, 34) - 5 * tt(tib, 35) / 4) * tt(tib, 64)
      tt(tib, 127) = -tt(tib, 7) * tt(tib, 7) * tt(tib, 43)
      tt(tib, 128) = -2 * (4 * tt(tib, 34) + 5 * tt(tib, 35) - 2 * tt(tib, 9)) * tt(tib, 64)
      tt(tib, 129) = -0.75 * tt(tib, 7) * tt(tib, 7) * tt(tib, 58)
      tt(tib, 130) = -0.5 * tt(tib, 7) * tt(tib, 7) * tt(tib, 64)

      ! A sphere has no check half, so all of this belongs to the hat pass.
      FOR tim = 1, 10, 1
         rv(tib, 55 + tim) = tt(tib, 120 + tim)
         rv(tib, 65 + tim) = 0
      NEXT

      ! ---------------------------------------------------------------------------
      ! The figuring's own tertiary. From (60.3) a figured surface adds D(3) I +
      ! L(3) Y, and every L is proportional to a figuring coefficient, so a sphere
      ! has none - which is why paper II, dealing only with spherical surfaces,
      ! writes its (2.1) with no L term at all.
      !
      ! dz is i_p (dD/c) + y_p dL, so dD/c is the HAT half and dL the CHECK half, and
      ! M (85.3) puts them in different passes. The normalisation is paper II's own
      ! (6.1), with the ray height beside L where the incidence sits beside D: it is
      ! not fitted, and with i = 0 and c0 = 0 it reproduces Buchdahl's published
      ! (73.7) for a figured plate exactly.
      ! ---------------------------------------------------------------------------
      IF (vfm(tii, 6) == 1)
         IF (ABSO(tt(tib, 3)) > 1E-30)
            exyy = tt(tib, 1)
            exvv = tt(tib, 2)
            excc = tic
            GOSUB exbas

            tivpr = (1 - tik) * tic * tt(tib, 1) + tik * tt(tib, 2)

            ! dD/c0, figured less unfigured. Both of its terms carry c0 explicitly -
            ! Y = c0 y in the first and c0 v" in the second - so removing one power
            ! by hand rather than dividing afterwards is what lets a PLANO figured
            ! surface through: the scheme wants D/c0, and forming D first and
            ! dividing gives 0/0 on a flat surface though the ratio is finite.
            FOR tim = 1, 10, 1
               tidl = tt(tib, 1) * (pc(tii, tim) - pc(tii, 10 + tim))
               IF (tim < 7)
                  tidl = tidl - tivpr * (pc(tii, 20 + tim) - pc(tii, 26 + tim))
               ENDIF
               cub(2, tim) = tidl / tik
            NEXT
            GOSUB excub
            FOR tim = 1, 10, 1
               tz(1, tim) = cub(1, tim)
            NEXT

            ! The figuring cubics, for the direct figured pass only. These are what the
            ! hat half of the tertiary increment is built from, and a fault in the
            ! polynomial algebra shows here as a wrong coefficient rather than as a
            ! wrong aberration six steps downstream.
            IF (dbg == 1)
               IF (grun == 2)
                  FORMAT 3.0
                  PRINT " "
                  PRINT "Figuring cubics, surface ", tii
                  FORMAT 15.6 EXP
                  PRINT "  lamFig", pc(tii, 1), pc(tii, 2), pc(tii, 3), pc(tii, 4), pc(tii, 5)
                  PRINT "        ", pc(tii, 6), pc(tii, 7), pc(tii, 8), pc(tii, 9), pc(tii, 10)
                  PRINT "  lamSph", pc(tii, 11), pc(tii, 12), pc(tii, 13), pc(tii, 14), pc(tii, 15)
                  PRINT "        ", pc(tii, 16), pc(tii, 17), pc(tii, 18), pc(tii, 19), pc(tii, 20)
                  PRINT "  nupFig", pc(tii, 21), pc(tii, 22), pc(tii, 23), pc(tii, 24), pc(tii, 25), pc(tii, 26)
                  PRINT "  nupSph", pc(tii, 27), pc(tii, 28), pc(tii, 29), pc(tii, 30), pc(tii, 31), pc(tii, 32)
                  PRINT "  dDexp ", tz(1, 1), tz(1, 2), tz(1, 3), tz(1, 4), tz(1, 5)
                  PRINT "        ", tz(1, 6), tz(1, 7), tz(1, 8), tz(1, 9), tz(1, 10)
                  PRINT "  k     ", tik, "  c0 ", tic, "  y ", tt(tib, 1), "  v ", tt(tib, 2)
                  PRINT "  c1    ", pcf(tii, 1), "  c2 ", pcf(tii, 2), "  c3 ", pcf(tii, 3)
               ENDIF
            ENDIF

            ! The L cubic, 16 (1/k)[ y G3 + G2(y S1 - v" B1) + G1(y S2 - v" X2) ].
            FOR tim = 1, 10, 1
               tidl = tt(tib, 1) * pc(tii, 32 + tim)
               tidl = tidl + tt(tib, 1) * pc(tii, 42 + tim) - tivpr * pc(tii, 52 + tim)
               tidl = tidl + tt(tib, 1) * pc(tii, 62 + tim) - tivpr * pc(tii, 72 + tim)
               cub(2, tim) = 16 * tidl / tik
            NEXT
            GOSUB excub

            tinrm = tin * (1 - tik) / 16
            tijc = tilag / tt(tib, 3)

            FOR tim = 1, 10, 1
               tiw = tinrm
               ! A SEVENTH ZPL TRAP. The bound of a FOR may not contain a bracketed
               ! index: ZPL splits the FOR's arguments on commas before it looks at
               ! them, so mon(6, tim) is torn in half and reported as a missing index
               ! followed by an improper format, and then as an infinite loop. The
               ! power is taken into a scalar first. No macro in this folder puts a
               ! bracket in a FOR bound, which is the evidence that it cannot be done.
               ! The first of the ten carries j to the power ZERO, and ZPL does not
               ! skip a FOR whose end is below its start - it reports an infinite
               ! loop. The zero power is guarded rather than left to the bounds.
               tipn = mon(6, tim)
               IF (tipn > 0)
                  FOR tip = 1, tipn, 1
                     tiw = tiw * tijc
                  NEXT
               ENDIF
               tz(2, tim) = tiw * tt(tib, 3) * tz(1, tim)
               tz(3, tim) = tiw * tt(tib, 1) * cub(1, tim)
            NEXT

            ! z3 and z4 are packed: script-T_3 is z3 + z4 and script-T_4 is 8 z4.
            ! The packing is linear, so each half may be packed on its own.
            tiq4 = tz(2, 4) / 8
            tz(2, 3) = tz(2, 3) - tiq4
            tz(2, 4) = tiq4
            tiq4 = tz(3, 4) / 8
            tz(3, 3) = tz(3, 3) - tiq4
            tz(3, 4) = tiq4

            FOR tim = 1, 10, 1
               rv(tib, 55 + tim) = rv(tib, 55 + tim) + tz(2, tim)
               rv(tib, 65 + tim) = rv(tib, 65 + tim) + tz(3, tim)
               tt(tib, 120 + tim) = tt(tib, 120 + tim) + tz(2, tim) + tz(3, tim)
            NEXT
         ENDIF
      ENDIF
   NEXT

   ! ------------------------------------------------------------------------------
   ! LOOP B. Buchdahl's Sum_1^{j-1}: what a surface inherits from all those before
   ! it. A second walk, because each surface needs totals over its predecessors and
   ! those are not known while the first is still going forward.
   ! ------------------------------------------------------------------------------
   FOR tii = 1, nsm, 1
      tib = tig + tii
      tia = 0
      tiab = 0
      tibb = 0
      ticc = 0
      ticb = 0
      ticg = 0
      IF (tii > 1)
         FOR tim = 1, tii - 1, 1
            tia = tia + tt(tig + tim, 10)
            tiab = tiab + tt(tig + tim, 11)
            tibb = tibb + tt(tig + tim, 12)
            ticc = ticc + tt(tig + tim, 13)
            ticb = ticb + tt(tig + tim, 14)
            ticg = ticg + rv(tig + tim, 15)
         NEXT
      ENDIF
      rv(tib, 16) = ticg
      tt(tib, 15) = tia
      tt(tib, 16) = tiab
      tt(tib, 17) = tibb
      tt(tib, 18) = ticc
      tt(tib, 19) = ticb

      ! t20 is NOT a running sum. A subscript 1 means the value at the FIRST
      ! surface: Buchdahl now has three decorations that look alike - leading
      ! asterisk, trailing asterisk, subscript one - and each means something else.
      tt(tib, 20) = 0.5 * (tt(tig + 1, 9) - tt(tib, 9)) + tt(tib, 16)
   NEXT

   ! ------------------------------------------------------------------------------
   ! LOOP C. t24 carries its own previous-surface value, so this must run in surface
   ! order. Its figured half must accumulate on the PREVIOUS surface's height ratio,
   ! not on its q - which is what s5p and s6p were feeling, they being the only two
   ! whose formulae reach t24 rather than the dagger family.
   ! ------------------------------------------------------------------------------
   ti81f = tt(tig + 1, 2) * tt(tig + 1, 5)
   ti82f = tt(tig + 1, 5) * tt(tig + 1, 5)

   FOR tii = 1, nsm, 1
      tib = tig + tii
      tipr = tib - 1

      tt(tib, 21) = 0.5 * (ti81f - tt(tib, 2) * tt(tib, 5)) + tt(tib, 18)
      tt(tib, 22) = 2 * (tt(tib, 21) - tt(tib, 18)) + tt(tib, 17)
      tt(tib, 23) = 0.5 * (ti82f - tt(tib, 5) * tt(tib, 5)) + tt(tib, 19)

      ti23f = rv(tib, 16)
      ti23p = 0
      ti24p = 0
      tirhp = 0
      IF (tii > 1)
         ti23p = rv(tipr, 16)
         ti24p = rv(tipr, 17)
         tirhp = rv(tipr, 1)
      ENDIF
      rv(tib, 17) = (ti23f - ti23p) * tirhp + ti24p

      tiinc = (tt(tib, 23) - ti23f) - (tt(tipr, 23) - ti23p)
      ticar = tiinc * tt(tipr, 6)
      tt(tib, 24) = ticar + (tt(tipr, 24) - ti24p) + rv(tib, 17)

      tt(tib, 25) = tt(tib, 6) * tt(tib, 15) - tt(tib, 20)
      tt(tib, 26) = tt(tib, 6) * tt(tib, 16) - tt(tib, 21)
      tt(tib, 27) = 2 * tt(tib, 6) * tt(tib, 16) - tt(tib, 22)
      tt(tib, 28) = tt(tib, 6) * tt(tib, 17) - 2 * tt(tib, 23)
      tt(tib, 29) = tt(tib, 6) * tt(tib, 18) - tt(tib, 23)
      tt(tib, 30) = tt(tib, 6) * tt(tib, 19) - tt(tib, 24)
      tt(tib, 31) = -tt(tib, 6) * tt(tib, 25) + tt(tib, 26)
      tt(tib, 32) = -tt(tib, 6) * tt(tib, 27) + tt(tib, 28)
      tt(tib, 33) = -tt(tib, 6) * tt(tib, 29) + tt(tib, 30)

      ! (85.1): the same six, on the height ratio. The two families are formed from
      ! the SAME p and q quantities and differ only in which ratio combines them.
      tiy = rv(tib, 1)
      yf(tib, 1) = tiy * tt(tib, 15) - tt(tib, 20)
      yf(tib, 2) = tiy * tt(tib, 16) - tt(tib, 21)
      yf(tib, 3) = 2 * tiy * tt(tib, 16) - tt(tib, 22)
      yf(tib, 4) = tiy * tt(tib, 17) - 2 * tt(tib, 23)
      yf(tib, 5) = tiy * tt(tib, 18) - tt(tib, 23)
      yf(tib, 6) = tiy * tt(tib, 19) - tt(tib, 24)
      yf(tib, 7) = -tiy * yf(tib, 1) + yf(tib, 2)
      yf(tib, 8) = -tiy * yf(tib, 3) + yf(tib, 4)
      yf(tib, 9) = -tiy * yf(tib, 5) + yf(tib, 6)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! tablei2 - loops D, E and F. Split from tablei only because ZPL has no locals and
! one subroutine of this length is harder to keep prefixes straight in, not because
! anything separates them: this runs immediately after tablei on the same rows.
! ---------------------------------------------------------------------------------
SUB tablei2
   tjg = (grun - 1) * (nsm + 2) + 1

   ! ------------------------------------------------------------------------------
   ! LOOP D. The six intrinsic secondary coefficients are CHAINED - t44 is built
   ! from t38, t50 from t44 and so on - so an aspheric increment cannot be added as
   ! each is formed, or it would propagate down the chain as though it were part of
   ! the spherical surface. The chain runs first, then the increments are applied.
   ! ------------------------------------------------------------------------------
   FOR tji = 1, nsm, 1
      tjb = tjg + tji
      scb = tjb

      tjk = rv(tjb, 9)
      tjn = rv(tjb, 10)
      tjc = rv(tjb, 19)
      tjap = rv(tjb, 2)
      tjaf = rv(tjb, 3)
      tjrr = rv(tjb, 1)

      tjs1 = 3 * tjap * tt(tjb, 34)
      tjs2 = 4 * tt(tjb, 6) * tjs1 + tt(tjb, 43)
      tjs3 = 0.5 * tt(tjb, 6) * (tt(tjb, 43) + tjs2) + tt(tjb, 48) + tt(tjb, 49)
      tjs4 = 2 * (tjs3 - 2 * tt(tjb, 48) - tt(tjb, 49))
      tjs5 = (-tt(tjb, 6) * tjs2 + 2 * tjs3 + tjs4 - 2 * tt(tjb, 48)) * tt(tjb, 6) + tt(tjb, 58)
      tjs6 = 0.25 * (2 * tt(tjb, 49) - 2 * tjs3 - tjs4) * tt(tjb, 6) + tt(tjb, 58) + tjs5
      tjs6 = 0.5 * tjs6 * tt(tjb, 6) + tt(tjb, 64)

      rv(tjb, 22) = tjs1
      rv(tjb, 23) = tjs2
      rv(tjb, 24) = tjs3
      rv(tjb, 25) = tjs4
      rv(tjb, 26) = tjs5
      rv(tjb, 27) = tjs6
      FOR tjm = 1, 6, 1
         rv(tjb, 27 + tjm) = ai(tjb, 1 + tjm)
      NEXT

      tt(tjb, 38) = tjs1 + ai(tjb, 2)
      tt(tjb, 44) = tjs2 + ai(tjb, 3)
      tt(tjb, 50) = tjs3 + ai(tjb, 4)
      tt(tjb, 54) = tjs4 + ai(tjb, 5)
      tt(tjb, 59) = tjs5 + ai(tjb, 6)
      tt(tjb, 65) = tjs6 + ai(tjb, 7)

      ! ---------------------------------------------------------------------------
      ! The six q-lifts, by Sec. 84(e). The barred secondary is s-bar = q s + (84.42)
      ! so the only singular piece is q s, and q is infinite wherever the marginal
      ! incidence vanishes. Every quantity in the chain is a fixed power of i times
      ! something regular - a_p ~ i^3, t34 ~ i^2, t49 ~ i^3, t58 ~ i^2, t64 ~ i,
      ! P ~ i - and carrying each as X/i^p and reading q^m X = i_q^m (X/i^p) i^(p-m)
      ! brings the six out with p = 5, 4, 3, 3, 2, 1, every one regular. The apparent
      ! divergences, q^4 a_p among them, all cancel against one of those factors of
      ! i, which is why a term-by-term lift of the chain fails where this does not.
      ! ---------------------------------------------------------------------------
      tjci = rv(tjb, 6)
      tjiq = rv(tjb, 7)
      tjii = tt(tjb, 3)
      tjcy = tjci * tt(tjb, 1)

      tja3 = 0
      IF (ABSO(rv(tjb, 8)) > 1E-30)
         tja3 = 0.5 * (tjcy - (1 + tjk)) * (tjk - 1) * tt(tjb, 1) / rv(tjb, 8)
      ENDIF
      tjom = 0
      IF (ABSO(tt(tjb, 1)) > 1E-30)
         tjom = (tjk - 1) * tjcy / (tt(tjb, 1) * tjn)
      ENDIF
      tjpo = (3 - tjk - 2 * tjcy) * tt(tjb, 7)
      tj34 = ((tjk * tjk - 1) - tjcy * tjcy + tjcy * (3 - tjk)) / 4
      tj48 = 0.5 * tt(tjb, 7) * tt(tjb, 7) * tja3
      tj49 = 0.25 * (1 - tjk) * (2 * tjcy - (1 + tjk)) * (tjcy - tjk) * tt(tjb, 7)
      tj58 = 0.5 * tjom * tjom
      tj64 = tt(tjb, 7) * tt(tjb, 7) * tjom / 8

      tjr2 = tja3 * (12 * tj34 * tjiq + tjpo)
      tjr3 = 0.5 * tjiq * (tjpo * tja3 + tjr2) + tj48 + tj49
      tjr4 = 2 * (tjr3 - 2 * tj48 - tj49)
      tjbr = 12 * tja3 * tj34 * tjiq * tjiq + 3 * tjpo * tja3 * tjiq - 2 * tj48 + 2 * tj49
      tjr5 = tjbr * tjiq + tj58
      tjwr = 0.25 * tjiq * (2 * tj49 - 2 * tjr3 - tjr4) + tj58 + tjr5
      tjr6 = 0.5 * tjwr * tjiq + tj64

      tji2 = tjii * tjii
      sc(1, 1) = 3 * tja3 * tj34 * tjiq * tji2 * tji2
      sc(1, 2) = tjr2 * tjiq * tjii * tji2
      sc(1, 3) = tjr3 * tjiq * tji2
      sc(1, 4) = tjr4 * tjiq * tji2
      sc(1, 5) = tjr5 * tjiq * tjii
      sc(1, 6) = tjr6 * tjiq

      ! The spherical half: the dagger family on the incidence ratio.
      FOR tjm = 1, 9, 1
         sc(2, tjm) = tt(tjb, 24 + tjm)
      NEXT
      sc(3, 1) = tjs1
      sc(3, 2) = tjs2
      sc(3, 3) = tjs3
      sc(3, 4) = tjs4
      sc(3, 5) = tjs5
      sc(3, 6) = tjs6

      sca = tjap
      scab = rv(tjb, 11)
      sccc = rv(tjb, 4)
      scq2 = rv(tjb, 12)
      scqc = rv(tjb, 13)
      scqo = rv(tjb, 14)
      scst = 1
      GOSUB secnd

      FOR tjm = 1, 6, 1
         sc(8, tjm) = sc(4, tjm)
         sc(9, tjm) = sc(5, tjm)
         sc(10, tjm) = sc(6, tjm)
      NEXT

      ! The figured half. Its dagger family is built on the HEIGHT ratio: without
      ! its own set the induced term of a figured surface that has figuring upstream
      ! is wrong, which is exactly, and only, where the disagreement was.
      tjf0 = tjrr * tt(tjb, 15) - tt(tjb, 20)
      tjf1 = tjrr * tt(tjb, 16) - tt(tjb, 21)
      tjf2 = 2 * tjrr * tt(tjb, 16) - tt(tjb, 22)
      tjf3 = tjrr * tt(tjb, 17) - 2 * tt(tjb, 23)
      tjf4 = tjrr * tt(tjb, 18) - tt(tjb, 23)
      tjf5 = tjrr * tt(tjb, 19) - tt(tjb, 24)
      sc(2, 1) = tjf0
      sc(2, 2) = tjf1
      sc(2, 3) = tjf2
      sc(2, 4) = tjf3
      sc(2, 5) = tjf4
      sc(2, 6) = tjf5
      sc(2, 7) = -tjrr * tjf0 + tjf1
      sc(2, 8) = -tjrr * tjf2 + tjf3
      sc(2, 9) = -tjrr * tjf4 + tjf5

      FOR tjm = 1, 6, 1
         sc(3, tjm) = ai(tjb, 1 + tjm)
         sc(1, tjm) = tjrr * ai(tjb, 1 + tjm)
      NEXT

      sca = tjaf
      scab = tjrr * tjaf
      sccc = tjrr * tjrr * tjaf
      scq2 = tjrr * tjrr * tjaf
      scqc = tjrr * tjrr * tjrr * tjaf
      scqo = tjrr * tt(tjb, 8)
      scst = 0
      GOSUB secnd

      ! ---------------------------------------------------------------------------
      ! The figuring's D half travels on the INCIDENCE ratio, not the height ratio.
      ! (65.5) puts no figuring in D at first order, which is why the primary is
      ! exact with the whole increment on the height ratio; (65.6) puts it there at
      ! second order, so from the secondary on the increment is q D + rho L where
      ! this scheme forms rho (D + L). The difference is (q - rho) D and it lands on
      ! the BARRED entry alone - the unbarred total is D + L either way.
      ! ---------------------------------------------------------------------------
      IF (vfm(tji, 6) == 1)
         dhk = tjk
         dhy = tt(tjb, 1)
         dhv = tt(tjb, 2)
         dhyq = tt(tjb, 4)
         dhvq = tt(tjb, 5)
         dhs1 = tjs1
         dhc1 = pcf(tji, 1)
         dhc2 = pcf(tji, 2)
         dhc3 = pcf(tji, 3)

         IF (ABSO(tjc) > 1E-12)
            dhc = tjc
            GOSUB dhalf
            FOR tjm = 1, 6, 1
               sc(3, tjm) = sc(7, tjm)
            NEXT
         ELSE
            dhc = 1E-6
            GOSUB dhalf
            FOR tjm = 1, 6, 1
               sc(3, tjm) = sc(7, tjm)
            NEXT
            dhc = -1E-6
            GOSUB dhalf
            FOR tjm = 1, 6, 1
               sc(3, tjm) = 0.5 * (sc(3, tjm) + sc(7, tjm))
            NEXT
         ENDIF

         tjld = tt(tjb, 6) - tjrr
         FOR tjm = 1, 6, 1
            sc(5, tjm) = sc(5, tjm) + tjld * sc(3, tjm)
            rv(tjb, 33 + tjm) = sc(3, tjm)
         NEXT
      ENDIF

      FOR tjm = 1, 5, 1
         rv(tjb, 45 + tjm) = sc(10, tjm)
         rv(tjb, 50 + tjm) = sc(6, tjm)
      NEXT
      FOR tjm = 1, 6, 1
         rv(tjb, 39 + tjm) = tjrr * sc(4, tjm)
      NEXT

      tt(tjb, 45) = sc(10, 1) + sc(6, 1)
      tt(tjb, 51) = sc(10, 2) + sc(6, 2)
      tt(tjb, 55) = sc(10, 3) + sc(6, 3)
      tt(tjb, 61) = sc(10, 4) + sc(6, 4)
      tt(tjb, 66) = sc(10, 5) + sc(6, 5)

      tt(tjb, 39) = 2 * tt(tjb, 10) * tt(tjb, 25)
      tt(tjb, 40) = tt(tjb, 38) + tt(tjb, 39)
      tt(tjb, 41) = sc(8, 1) + sc(4, 1)
      tt(tjb, 42) = sc(9, 1) + sc(5, 1)
      tt(tjb, 46) = sc(8, 2) + sc(4, 2)
      tt(tjb, 47) = sc(9, 2) + sc(5, 2)
      tt(tjb, 52) = sc(8, 3) + sc(4, 3)
      tt(tjb, 53) = sc(9, 3) + sc(5, 3)
      tt(tjb, 56) = sc(8, 4) + sc(4, 4)
      tt(tjb, 57) = sc(9, 4) + sc(5, 4)
      tt(tjb, 62) = sc(8, 5) + sc(4, 5)
      tt(tjb, 63) = sc(9, 5) + sc(5, 5)
      tt(tjb, 67) = sc(8, 6) + sc(4, 6)
      tt(tjb, 68) = sc(9, 6) + sc(5, 6)
   NEXT

   ! ------------------------------------------------------------------------------
   ! LOOP E. Running sums of the secondary over the surfaces before this one, the
   ! unbarred and barred alternating. The sources are spaced 5, 6, 4, 6, 5 apart
   ! rather than evenly, so they are written out rather than indexed by a formula
   ! that would be wrong in the middle.
   ! ------------------------------------------------------------------------------
   FOR tji = 1, nsm, 1
      tjb = tjg + tji
      IF (tji > 1)
         FOR tjm = 1, tji - 1, 1
            tjp = tjg + tjm
            tt(tjb, 69) = tt(tjb, 69) + tt(tjp, 41)
            tt(tjb, 70) = tt(tjb, 70) + tt(tjp, 42)
            tt(tjb, 71) = tt(tjb, 71) + tt(tjp, 46)
            tt(tjb, 72) = tt(tjb, 72) + tt(tjp, 47)
            tt(tjb, 73) = tt(tjb, 73) + tt(tjp, 52)
            tt(tjb, 74) = tt(tjb, 74) + tt(tjp, 53)
            tt(tjb, 75) = tt(tjb, 75) + tt(tjp, 56)
            tt(tjb, 76) = tt(tjb, 76) + tt(tjp, 57)
            tt(tjb, 77) = tt(tjb, 77) + tt(tjp, 62)
            tt(tjb, 78) = tt(tjb, 78) + tt(tjp, 63)
            tt(tjb, 79) = tt(tjb, 79) + tt(tjp, 67)
            tt(tjb, 80) = tt(tjb, 80) + tt(tjp, 68)
         NEXT
      ENDIF
   NEXT

   ! ------------------------------------------------------------------------------
   ! LOOP F. The q-side accumulated secondaries as closed forms in the sums above,
   ! then the tertiary dagger family. Several of the recursions carry their own
   ! previous-surface value, so this must stay in surface order.
   ! ------------------------------------------------------------------------------
   FOR tji = 1, nsm, 1
      tjb = tjg + tji
      tjp = tjb - 1
      tj9s = rv(tjb, 20) * rv(tjb, 20)
      tj81 = rv(tjb, 20) * rv(tjb, 21)

      tt(tjb, 83) = tt(tjb, 16) - tt(tjb, 20)
      tt(tjb, 84) = tt(tjb, 17) - tt(tjb, 22)
      tt(tjb, 85) = tt(tjb, 19) - tt(tjb, 23)

      tt(tjb, 86) = -1.5 * tt(tjb, 83) * tt(tjb, 83) + tt(tjb, 9) * tt(tjb, 16) - tt(tjb, 16) * tt(tjb, 20)
      tt(tjb, 86) = tt(tjb, 86) + tt(tjb, 15) * tt(tjb, 21) - tt(tjb, 15) * tt(tjb, 81) + tt(tjb, 70)
      tt(tjb, 87) = (2 * tt(tjb, 21) - tt(tjb, 22) - tt(tjb, 81)) * tt(tjb, 16)
      tt(tjb, 87) = tt(tjb, 87) + tt(tjb, 9) * tt(tjb, 21) - tt(tjb, 20) * tt(tjb, 81)
      tt(tjb, 88) = (2 * tt(tjb, 23) - tt(tjb, 82)) * tt(tjb, 15) - tt(tjb, 17) * tt(tjb, 20) + tt(tjb, 9) * tt(tjb, 17)
      tt(tjb, 89) = -3 * tt(tjb, 83) * tt(tjb, 84) + tt(tjb, 87) + tt(tjb, 88) + tt(tjb, 72)
      tt(tjb, 90) = -0.5 * tt(tjb, 81) * tt(tjb, 84) + tt(tjb, 9) * tt(tjb, 19) - tt(tjb, 20) * tt(tjb, 82)
      tt(tjb, 91) = tt(tjb, 18) * tt(tjb, 21) + tt(tjb, 15) * tt(tjb, 24) - tt(tjb, 16) * tt(tjb, 23) - tt(tjb, 19) * tt(tjb, 20)
      tt(tjb, 92) = -3 * tt(tjb, 83) * tt(tjb, 85) + tt(tjb, 74) + tt(tjb, 90) + tt(tjb, 91)
      tt(tjb, 93) = 2 * (2 * tt(tjb, 16) * tt(tjb, 23) - tt(tjb, 16) * tt(tjb, 82) + tt(tjb, 9) * tt(tjb, 23))
      tt(tjb, 93) = tt(tjb, 93) - tt(tjb, 17) * tt(tjb, 22)
      tt(tjb, 94) = -1.5 * tt(tjb, 84) * tt(tjb, 84) + tt(tjb, 81) * tt(tjb, 84) + tt(tjb, 76) + tt(tjb, 93)
      tt(tjb, 95) = (2 * tt(tjb, 18) - tt(tjb, 17) + tt(tjb, 81)) * tt(tjb, 23)
      tt(tjb, 95) = tt(tjb, 95) + tt(tjb, 19) * tt(tjb, 81) - tt(tjb, 22) * tt(tjb, 82)
      tt(tjb, 96) = (2 * tt(tjb, 16) + tt(tjb, 9)) * tt(tjb, 24) - tt(tjb, 19) * tt(tjb, 22) - tt(tjb, 18) * tt(tjb, 82)
      tt(tjb, 97) = -3 * tt(tjb, 84) * tt(tjb, 85) + tt(tjb, 95) + tt(tjb, 96) + tt(tjb, 78)
      tt(tjb, 98) = -1.5 * tt(tjb, 85) * tt(tjb, 85) + tt(tjb, 24) * tt(tjb, 81) + tt(tjb, 18) * tt(tjb, 24)
      tt(tjb, 98) = tt(tjb, 98) - tt(tjb, 23) * tt(tjb, 82) - tt(tjb, 19) * tt(tjb, 23) + tt(tjb, 80)

      tt(tjb, 99) = 0.5 * (tt(tjb, 9) - tj9s) + tt(tjb, 11)
      tt(tjb, 100) = tt(tjb, 81) - tj81 + tt(tjb, 12)

      ! Each of the six recursions multiplies an INCREMENT in the accumulated q-side
      ! secondary by the PREVIOUS surface's q. That increment already carries surface
      ! i-1's own ratio inside it - (65.7) put the figured half of a q-side quantity
      ! on the height ratio when t42 and its partners were formed - so multiplying by
      ! q a second time gives the figured half q q~ where it should have q~ squared.
      ! Writing the term as -q_prev dQ - (q~_prev - q_prev) dQ_fig leaves the one
      ! extra product appended to each line, and dQ_fig is the previous surface's own
      ! figured barred secondary: its LIFT, not the whole of it.
      tjdr = 0
      IF (tji > 1) THEN tjdr = rv(tjp, 1) - tt(tjp, 6)

      tt(tjb, 101) = tt(tjb, 6) * tt(tjb, 69) - tt(tjb, 86)
      tt(tjb, 102) = -tt(tjp, 6) * (tt(tjp, 70) - tt(tjp, 86) + tt(tjb, 86)) + tt(tjb, 6) * tt(tjb, 70)
      tt(tjb, 102) = tt(tjb, 102) - tt(tjp, 31) * tt(tjp, 99) + tt(tjp, 102) - tjdr * rv(tjp, 40)
      tt(tjb, 103) = tt(tjb, 6) * tt(tjb, 71) - tt(tjb, 89)
      tt(tjb, 104) = -tt(tjp, 6) * (tt(tjp, 72) - tt(tjp, 89) + tt(tjb, 89)) + tt(tjb, 6) * tt(tjb, 72)
      tt(tjb, 104) = tt(tjb, 104) - tt(tjp, 31) * tt(tjp, 100) - tt(tjp, 32) * tt(tjp, 99) + tt(tjp, 104) - tjdr * rv(tjp, 41)
      tt(tjb, 105) = tt(tjb, 6) * tt(tjb, 73) - tt(tjb, 92)
      tt(tjb, 106) = -0.5 * tt(tjp, 100) * tt(tjp, 31) * tt(tjp, 6) + tt(tjb, 74) * tt(tjb, 6) - tt(tjp, 33) * tt(tjp, 99)
      tt(tjb, 107) = -tt(tjp, 6) * (tt(tjp, 74) - tt(tjp, 92) + tt(tjb, 92)) + tt(tjb, 106) + tt(tjp, 107) - tjdr * rv(tjp, 42)
      tt(tjb, 108) = tt(tjb, 6) * tt(tjb, 75) - tt(tjb, 94)
      tt(tjb, 109) = -tt(tjp, 6) * (tt(tjp, 76) - tt(tjp, 94) + tt(tjb, 94)) - tt(tjp, 32) * tt(tjp, 100)
      tt(tjb, 109) = tt(tjb, 109) + tt(tjb, 6) * tt(tjb, 76) + tt(tjp, 109) - tjdr * rv(tjp, 43)
      tt(tjb, 110) = tt(tjb, 6) * tt(tjb, 77) - tt(tjb, 97)
      tt(tjb, 111) = -((0.5 * tt(tjp, 6) * tt(tjp, 32) + tt(tjp, 33)) * tt(tjp, 100)) + tt(tjb, 6) * tt(tjb, 78)
      tt(tjb, 112) = -tt(tjp, 6) * (tt(tjp, 78) - tt(tjp, 97) + tt(tjb, 97)) + tt(tjb, 111) + tt(tjp, 112) - tjdr * rv(tjp, 44)
      tt(tjb, 113) = tt(tjb, 6) * tt(tjb, 79) - tt(tjb, 98)
      tjcar = tt(tjp, 6) * (-0.5 * tt(tjp, 33) * tt(tjp, 100) - tt(tjp, 80) + tt(tjp, 98) - tt(tjb, 98))
      tt(tjb, 114) = tjcar + tt(tjb, 6) * tt(tjb, 80) + tt(tjp, 114) - tjdr * rv(tjp, 45)

      tt(tjb, 115) = -tt(tjb, 6) * tt(tjb, 101) + tt(tjb, 102)
      tt(tjb, 116) = -tt(tjb, 6) * tt(tjb, 103) + tt(tjb, 104)
      tt(tjb, 117) = -tt(tjb, 6) * tt(tjb, 105) + tt(tjb, 107)
      tt(tjb, 118) = -tt(tjb, 6) * tt(tjb, 108) + tt(tjb, 109)
      tt(tjb, 119) = -tt(tjb, 6) * tt(tjb, 110) + tt(tjb, 112)
      tt(tjb, 120) = -tt(tjb, 6) * tt(tjb, 113) + tt(tjb, 114)

      ! The same six on the height ratio, (85.1). The (Y) recursion carries no lift
      ! correction: its family is already combined on that ratio.
      tjy = rv(tjb, 1)
      tjpy = 0
      IF (tji > 1) THEN tjpy = rv(tjp, 1)

      yf(tjb, 10) = tjy * tt(tjb, 69) - tt(tjb, 86)
      yf(tjb, 11) = -tjpy * (tt(tjp, 70) - tt(tjp, 86) + tt(tjb, 86)) + tjy * tt(tjb, 70)
      yf(tjb, 11) = yf(tjb, 11) - yf(tjp, 7) * tt(tjp, 99) + yf(tjp, 11)
      yf(tjb, 12) = tjy * tt(tjb, 71) - tt(tjb, 89)
      yf(tjb, 13) = -tjpy * (tt(tjp, 72) - tt(tjp, 89) + tt(tjb, 89)) + tjy * tt(tjb, 72)
      yf(tjb, 13) = yf(tjb, 13) - yf(tjp, 7) * tt(tjp, 100) - yf(tjp, 8) * tt(tjp, 99) + yf(tjp, 13)
      yf(tjb, 14) = tjy * tt(tjb, 73) - tt(tjb, 92)
      yf(tjb, 15) = -0.5 * tt(tjp, 100) * yf(tjp, 7) * tjpy + tt(tjb, 74) * tjy - yf(tjp, 9) * tt(tjp, 99)
      yf(tjb, 16) = -tjpy * (tt(tjp, 74) - tt(tjp, 92) + tt(tjb, 92)) + yf(tjb, 15) + yf(tjp, 16)
      yf(tjb, 17) = tjy * tt(tjb, 75) - tt(tjb, 94)
      yf(tjb, 18) = -tjpy * (tt(tjp, 76) - tt(tjp, 94) + tt(tjb, 94)) - yf(tjp, 8) * tt(tjp, 100)
      yf(tjb, 18) = yf(tjb, 18) + tjy * tt(tjb, 76) + yf(tjp, 18)
      yf(tjb, 19) = tjy * tt(tjb, 77) - tt(tjb, 97)
      yf(tjb, 20) = -((0.5 * tjpy * yf(tjp, 8) + yf(tjp, 9)) * tt(tjp, 100)) + tjy * tt(tjb, 78)
      yf(tjb, 21) = -tjpy * (tt(tjp, 78) - tt(tjp, 97) + tt(tjb, 97)) + yf(tjb, 20) + yf(tjp, 21)
      yf(tjb, 22) = tjy * tt(tjb, 79) - tt(tjb, 98)
      tjcar = tjpy * (-0.5 * yf(tjp, 9) * tt(tjp, 100) - tt(tjp, 80) + tt(tjp, 98) - tt(tjb, 98))
      yf(tjb, 23) = tjcar + tjy * tt(tjb, 80) + yf(tjp, 23)

      yf(tjb, 24) = -tjy * yf(tjb, 10) + yf(tjb, 11)
      yf(tjb, 25) = -tjy * yf(tjb, 12) + yf(tjb, 13)
      yf(tjb, 26) = -tjy * yf(tjb, 14) + yf(tjb, 16)
      yf(tjb, 27) = -tjy * yf(tjb, 17) + yf(tjb, 18)
      yf(tjb, 28) = -tjy * yf(tjb, 19) + yf(tjb, 21)
      yf(tjb, 29) = -tjy * yf(tjb, 22) + yf(tjb, 23)
   NEXT
RETURN


! ---------------------------------------------------------------------------------
! pass - ONE pass of the tertiary totals, M (84.23), from whatever is currently in
! row psb: the intrinsic chain t134..t152 and then the ten induced totals.
!
! (85.3) makes each total i_p(hat bracket) + y_p(check bracket), and (85.5) says both
! brackets obey the SAME equations - only the surface's own figuring halves differ,
! the accumulated coefficients being common. So this is called twice, once with the
! hat halves of a_p, c13, the intrinsic secondary, the M family and z, and once with
! their check halves. Because the induced terms are PRODUCTS of accumulated
! quantities with the surface's own, a single pass with mixed inputs is NOT the sum
! of the two, which is why no reference-swap on the barred entry alone could ever be
! right.
!
! In:  psb the row, pscar the ratio this pass travels on.
! Out: po(1, 1..10) the ten totals, po(2, 1..10) their barred partners.
! ---------------------------------------------------------------------------------
SUB pass
   psr = psb
   psq = pscar

   ! M (26.2) gives b_p = 2 q a_p, and that is how b reaches the arrangement: a bare
   ! ratio beside an accumulated family member. In the CHECK half the primary is a_v
   ! and its partner is b_v = 2 q~ a_v - the height ratio, not the incidence ratio.
   psbq = psq

   ! The ratio the INTRINSIC chain travels on is the surface's own q in both halves;
   ! in the hat pass the two are the same number.
   psch = tt(psr, 6)

   tt(psr, 134) = 6 * psch * tt(psr, 121) + tt(psr, 122)
   tt(psr, 136) = 0.5 * (tt(psr, 134) + tt(psr, 122)) * psch + tt(psr, 123) + tt(psr, 124)
   tt(psr, 138) = 4 * (tt(psr, 136) - tt(psr, 123) + tt(psr, 124))
   tt(psr, 140) = (-2 * psch * tt(psr, 134) + 4 * tt(psr, 136) + tt(psr, 138) + 8 * tt(psr, 124)) * psch + tt(psr, 125)
   tt(psr, 143) = ((tt(psr, 123) + tt(psr, 124) - tt(psr, 136)) * psch + 0.5 * tt(psr, 140) + 0.5 * tt(psr, 125)) * psch + tt(psr, 126)
   tt(psr, 145) = (2 / 3) * (4 * (tt(psr, 124) - tt(psr, 123)) * psch + tt(psr, 140) - tt(psr, 125)) + tt(psr, 127)
   tt(psr, 147) = (4 * (tt(psr, 124) - tt(psr, 123)) * psch + 3 * tt(psr, 127) - 2 * tt(psr, 125)) * psch
   tt(psr, 147) = tt(psr, 147) + tt(psr, 128) - 4 * tt(psr, 126) + 4 * tt(psr, 143)
   tt(psr, 149) = (4 * psch * tt(psr, 124) + tt(psr, 125) + 0.75 * tt(psr, 127) - 0.75 * tt(psr, 145)) * psch
   tt(psr, 149) = (tt(psr, 149) + tt(psr, 147) + 2 * tt(psr, 126) + tt(psr, 128)) * psch + tt(psr, 129)
   tt(psr, 151) = ((psch * tt(psr, 121) + tt(psr, 122)) * psch + tt(psr, 123) + 9 * tt(psr, 124)) * psch
   tt(psr, 151) = tt(psr, 151) + tt(psr, 125) + tt(psr, 127)
   tt(psr, 152) = ((psch * tt(psr, 151) + tt(psr, 126) + tt(psr, 128)) * psch + tt(psr, 129)) * psch + tt(psr, 130)

   tt(psr, 131) = 4 * ((-tt(psr, 8) * tt(psr, 15) + 2 * tt(psr, 44)) * 0.125 * tt(psr, 15) - tt(psr, 20) * tt(psr, 38))
   tt(psr, 132) = (tt(psr, 25) * tt(psr, 25) + 3 * tt(psr, 101)) * tt(psr, 10) + tt(psr, 25) * tt(psr, 40) + tt(psr, 121) + tt(psr, 131)

   psz = tt(psr, 134)
   psz = psz + 4 * (0.5 * (tt(psr, 26) + tt(psr, 27)) * tt(psr, 25) + psbq * tt(psr, 101) + 0.5 * tt(psr, 102) + 0.75 * tt(psr, 103)) * tt(psr, 10)
   psz = psz + 3 * (-(tt(psr, 15) * tt(psr, 16) + tt(psr, 69) / 3) * tt(psr, 8) + tt(psr, 44) * tt(psr, 83))
   psz = psz + 2 * (tt(psr, 50) + tt(psr, 54)) * tt(psr, 15) + tt(psr, 25) * tt(psr, 45) + tt(psr, 27) * tt(psr, 40)
   psz = psz - 4 * (tt(psr, 21) + tt(psr, 22)) * tt(psr, 38)
   tt(psr, 135) = psz

   psz = tt(psr, 136)
   psz = psz + (2 * (psbq * tt(psr, 102) + tt(psr, 25) * tt(psr, 29)) + tt(psr, 26) * tt(psr, 26) + 3 * tt(psr, 105)) * tt(psr, 10)
   psz = psz - (0.5 * tt(psr, 16) * tt(psr, 16) + tt(psr, 15) * tt(psr, 18) + tt(psr, 70)) * tt(psr, 8)
   psz = psz + tt(psr, 25) * tt(psr, 51) + tt(psr, 29) * tt(psr, 40)
   psz = psz + 2 * (-2 * tt(psr, 23) * tt(psr, 38) + tt(psr, 50) * tt(psr, 83)) + tt(psr, 15) * tt(psr, 59)
   psz = psz + 0.5 * tt(psr, 44) * tt(psr, 84) + tt(psr, 13) * tt(psr, 101)
   tt(psr, 137) = psz

   psz = tt(psr, 138)
   psz = psz + (2 * (2 * psbq * tt(psr, 103) + tt(psr, 25) * tt(psr, 28) + tt(psr, 26) * tt(psr, 27)) + tt(psr, 27) * tt(psr, 27) + 2 * tt(psr, 104) + 3 * tt(psr, 108)) * tt(psr, 10)
   psz = psz - (4 * tt(psr, 16) * tt(psr, 16) + tt(psr, 15) * tt(psr, 17) + tt(psr, 71)) * tt(psr, 8)
   psz = psz + 4 * (-2 * tt(psr, 23) * tt(psr, 38) + tt(psr, 16) * tt(psr, 50)) + tt(psr, 27) * tt(psr, 45) + tt(psr, 25) * tt(psr, 55)
   psz = psz + 2 * ((3 * tt(psr, 16) - tt(psr, 20)) * tt(psr, 54) + tt(psr, 15) * tt(psr, 59))
   psz = psz + (tt(psr, 17) - 2 * tt(psr, 21) - 3 * tt(psr, 22)) * tt(psr, 44)
   tt(psr, 139) = psz

   psz = tt(psr, 140)
   psz = psz + 2 * ((2 * tt(psr, 105) + tt(psr, 104)) * psbq + tt(psr, 107) + 1.5 * tt(psr, 110)) * tt(psr, 10)
   psz = psz + tt(psr, 13) * tt(psr, 103)
   psz = psz + 2 * ((tt(psr, 28) + tt(psr, 29)) * tt(psr, 26) + tt(psr, 25) * tt(psr, 30) + tt(psr, 27) * tt(psr, 29)) * tt(psr, 10)
   psz = psz - ((3 * tt(psr, 18) + tt(psr, 17)) * tt(psr, 16) + tt(psr, 15) * tt(psr, 19) + tt(psr, 72) + tt(psr, 73)) * tt(psr, 8)
   psz = psz + (tt(psr, 19) - 5 * tt(psr, 23)) * tt(psr, 44) + tt(psr, 29) * tt(psr, 45)
   psz = psz + (3 * tt(psr, 50) + tt(psr, 54)) * tt(psr, 84) + tt(psr, 27) * tt(psr, 51)
   psz = psz + (5 * tt(psr, 16) - tt(psr, 20)) * tt(psr, 59) + tt(psr, 25) * tt(psr, 61)
   psz = psz + 4 * (tt(psr, 15) * tt(psr, 65) - tt(psr, 24) * tt(psr, 38))
   tt(psr, 141) = psz

   psz = tt(psr, 143)
   psz = psz + (2 * (psbq * tt(psr, 107) + tt(psr, 26) * tt(psr, 30)) + tt(psr, 29) * tt(psr, 29) + 3 * tt(psr, 113)) * tt(psr, 10)
   psz = psz - (0.5 * tt(psr, 18) * tt(psr, 18) + tt(psr, 16) * tt(psr, 19) + tt(psr, 74)) * tt(psr, 8)
   psz = psz + tt(psr, 29) * tt(psr, 51) + tt(psr, 25) * tt(psr, 66)
   psz = psz + 2 * (2 * tt(psr, 16) * tt(psr, 65) + tt(psr, 50) * tt(psr, 85))
   psz = psz - tt(psr, 24) * tt(psr, 44) + tt(psr, 13) * tt(psr, 105)
   psz = psz + 0.5 * tt(psr, 59) * tt(psr, 84)
   tt(psr, 144) = psz

   psz = tt(psr, 145)
   psz = psz + 2 * ((2 * psbq * tt(psr, 108) + tt(psr, 27) * tt(psr, 28) + tt(psr, 109)) * tt(psr, 10) + tt(psr, 54) * tt(psr, 84))
   psz = psz - (2 * tt(psr, 16) * tt(psr, 17) + tt(psr, 75)) * tt(psr, 8) + tt(psr, 27) * tt(psr, 55)
   psz = psz + 4 * (tt(psr, 16) * tt(psr, 59) - tt(psr, 23) * tt(psr, 44))
   tt(psr, 146) = psz

   psz = tt(psr, 147)
   psz = psz - ((0.5 * tt(psr, 17) + tt(psr, 18)) * tt(psr, 17) + tt(psr, 76) + tt(psr, 77)) * tt(psr, 8) + tt(psr, 13) * tt(psr, 108)
   psz = psz + 2 * (-tt(psr, 8) * tt(psr, 16) + tt(psr, 54)) * tt(psr, 19) + tt(psr, 29) * tt(psr, 55) + tt(psr, 27) * tt(psr, 61)
   psz = psz + (2 * ((2 * tt(psr, 110) + tt(psr, 109)) * psbq + tt(psr, 28) * tt(psr, 29) + tt(psr, 27) * tt(psr, 30) + tt(psr, 112)) + tt(psr, 28) * tt(psr, 28)) * tt(psr, 10)
   psz = psz + 8 * (-0.25 * ((2 * tt(psr, 50) + 3 * tt(psr, 54)) * tt(psr, 23) + tt(psr, 24) * tt(psr, 44)) + tt(psr, 16) * tt(psr, 65))
   psz = psz + (3 * tt(psr, 17) + 2 * tt(psr, 18) - tt(psr, 22)) * tt(psr, 59)
   tt(psr, 148) = psz

   psz = tt(psr, 149)
   psz = psz + 2 * ((2 * tt(psr, 113) + tt(psr, 112)) * psbq + tt(psr, 28) * tt(psr, 30) + tt(psr, 29) * tt(psr, 30) + tt(psr, 114)) * tt(psr, 10)
   psz = psz - ((tt(psr, 17) + tt(psr, 18)) * tt(psr, 19) + tt(psr, 78) + tt(psr, 79)) * tt(psr, 8) + tt(psr, 13) * tt(psr, 110)
   psz = psz + 4 * ((tt(psr, 17) + tt(psr, 18)) * tt(psr, 65) + 0.75 * tt(psr, 59) * tt(psr, 85))
   psz = psz + tt(psr, 29) * tt(psr, 61) + tt(psr, 27) * tt(psr, 66)
   psz = psz - 2 * (tt(psr, 50) + tt(psr, 54)) * tt(psr, 24)
   tt(psr, 150) = psz

   tt(psr, 153) = (2 * psbq * tt(psr, 114) + tt(psr, 30) * tt(psr, 30)) * tt(psr, 10) + tt(psr, 13) * tt(psr, 113)
   tt(psr, 154) = -(0.5 * tt(psr, 19) * tt(psr, 19) + tt(psr, 80)) * tt(psr, 8) + tt(psr, 29) * tt(psr, 66) - tt(psr, 24) * tt(psr, 59)
   tt(psr, 155) = 4 * tt(psr, 19) * tt(psr, 65) + tt(psr, 152) + tt(psr, 153) + tt(psr, 154)

   po(1, 1) = tt(psr, 132)
   po(1, 2) = tt(psr, 135)
   po(1, 3) = tt(psr, 137)
   po(1, 4) = tt(psr, 139)
   po(1, 5) = tt(psr, 141)
   po(1, 6) = tt(psr, 144)
   po(1, 7) = tt(psr, 146)
   po(1, 8) = tt(psr, 148)
   po(1, 9) = tt(psr, 150)
   po(1, 10) = tt(psr, 155)

   ! M (84.23): the barred entries follow from the unbarred ones by replacing the
   ! intrinsic part with the ratio times itself and every intermediate by its barred
   ! form. Two families of term: the surface's own primary against the tertiary
   ! family, and the dagger against its own secondary.
   po(2, 1) = psq * tt(psr, 132) + tt(psr, 10) * tt(psr, 115) + tt(psr, 31) * tt(psr, 40)
   po(2, 2) = psq * (2 * tt(psr, 10) * tt(psr, 115) + tt(psr, 135)) + tt(psr, 10) * tt(psr, 116)
   po(2, 2) = po(2, 2) + tt(psr, 31) * tt(psr, 45) + tt(psr, 32) * tt(psr, 40)
   po(2, 3) = psq * tt(psr, 137) + tt(psr, 10) * tt(psr, 117) + tt(psr, 13) * tt(psr, 115)
   po(2, 3) = po(2, 3) + tt(psr, 31) * tt(psr, 51) + tt(psr, 33) * tt(psr, 40)
   po(2, 4) = psq * (2 * tt(psr, 116) * tt(psr, 10) + tt(psr, 139)) + tt(psr, 118) * tt(psr, 10)
   po(2, 4) = po(2, 4) + tt(psr, 31) * tt(psr, 55) + tt(psr, 32) * tt(psr, 45)
   po(2, 5) = psq * (2 * tt(psr, 117) * tt(psr, 10) + tt(psr, 141)) + tt(psr, 119) * tt(psr, 10) + tt(psr, 13) * tt(psr, 116)
   po(2, 5) = po(2, 5) + tt(psr, 31) * tt(psr, 61) + tt(psr, 32) * tt(psr, 51) + tt(psr, 33) * tt(psr, 45)
   po(2, 6) = psq * tt(psr, 144) + tt(psr, 10) * tt(psr, 120) + tt(psr, 13) * tt(psr, 117)
   po(2, 6) = po(2, 6) + tt(psr, 31) * tt(psr, 66) + tt(psr, 33) * tt(psr, 51)
   po(2, 7) = psq * (2 * tt(psr, 10) * tt(psr, 118) + tt(psr, 146)) + tt(psr, 32) * tt(psr, 55)
   po(2, 8) = psq * (2 * tt(psr, 10) * tt(psr, 119) + tt(psr, 148)) + tt(psr, 13) * tt(psr, 118)
   po(2, 8) = po(2, 8) + tt(psr, 32) * tt(psr, 61) + tt(psr, 33) * tt(psr, 55)
   po(2, 9) = psq * (2 * tt(psr, 10) * tt(psr, 120) + tt(psr, 150)) + tt(psr, 13) * tt(psr, 119)
   po(2, 9) = po(2, 9) + tt(psr, 32) * tt(psr, 66) + tt(psr, 33) * tt(psr, 61)
   po(2, 10) = psq * tt(psr, 155) + tt(psr, 13) * tt(psr, 120) + tt(psr, 33) * tt(psr, 66)
RETURN

! ---------------------------------------------------------------------------------
! sqid - the BARRED q-side secondary coefficients S-bar_1q .. S-bar_5q, recovered
! from Buchdahl's identities of M Sec. 22, into sc(11, 1..5).
!
! WHY THEY HAVE TO BE RECOVERED. The scheme produces the p coefficients and the
! unbarred q ones and stops. Buchdahl obtained the barred q ones "by transformation
! of coordinates from the corresponding canonical coefficients of M Table VII", which
! needs a table this macro does not have. The identities give them instead, from
! quantities the scheme already carries - and they come from the characteristic
! function, not from the surfaces being spheres, so they hold on a figured system.
!
! S-bar_6q cannot be had this way and is not wanted here: M Sec. 19 says that when
! all the p coefficients are known, all but one of the q ones follow from the
! identities, and the one left over is the last barred one. The sixth comes from the
! dual run instead.
!
! THE PAIRING. M (21.5) writes the identities in a bracket notation whose underlying
! pairing it does not define. M (20.21) applies the same pairing to paraxial rays -
! (v'|v) = -N1 pomega - which gives it away: it is the skew bracket over the p and q
! members, (X|Y) = X_p Y_q - X_q Y_p. Since [AB] = (1/N1)(A|B) is a direct evaluation
! rather than an accumulation, every bracket follows from the running sums.
!
! In: sqb the row, sqf the FIRST row of the same pass.
! ---------------------------------------------------------------------------------
SUB sqid
   sqr = sqb
   sqvp = tt(sqr, 2)
   sqvq = tt(sqr, 5)
   sqp1 = tt(sqf, 2)
   sqq1 = tt(sqf, 5)

   ! B_p = 2 Abar_p by (20.42) and Bbar_q = 2 C_q by (20.43); those two are not
   ! tabulated and follow from the primary identities.
   sqap = tt(sqr, 15)
   sqaq = tt(sqr, 20)
   sqbp = tt(sqr, 16)
   sqbq = tt(sqr, 21)
   sqcp = 2 * sqbp
   sqcq = tt(sqr, 22)
   sqdp = tt(sqr, 17)
   sqdq = 2 * tt(sqr, 23)
   sqep = tt(sqr, 18)
   sqeq = tt(sqr, 23)
   sqfp = tt(sqr, 19)
   sqfq = tt(sqr, 24)

   sqac = sqap * sqeq - sqaq * sqep
   sqbc = sqcp * sqeq - sqcq * sqep
   sqa2 = sqbp * sqeq - sqbq * sqep
   sqa3 = sqbp * sqfq - sqbq * sqfp
   sqd1 = sqdp * sqbq - sqdq * sqbp
   sqd2 = sqdp * sqeq - sqdq * sqep
   sqc2 = sqcp * sqfq - sqcq * sqfp
   sqd3 = sqdp * sqfq - sqdq * sqfp
   sqcc = sqfp * sqeq - sqfq * sqep

   sqba = sqap * sqvq - sqaq * sqvp
   sqbb = sqbp * sqvq - sqbq * sqvp
   sqbd = sqdp * sqvq - sqdq * sqvp
   sqbe = sqep * sqvq - sqeq * sqvp
   sqbf = sqfp * sqvq - sqfq * sqvp

   sqdp2 = sqvp * sqvp - sqp1 * sqp1
   sqdq2 = sqvq * sqvq - sqq1 * sqq1
   sqdpq = sqvp * sqvq - sqp1 * sqq1

   sqo8 = sqd1 + 2 * sqa2 + 2 * sqbc + sqvp * sqbd - sqvp * sqvq * sqdpq
   sqo9 = 6 * sqac - sqvq * sqba + sqvp * sqbb - 1.5 * sqdp2 * sqdpq
   sqo11 = 4 * sqd2 + 2 * sqc2 + sqvq * sqbd + 2 * sqvp * sqbf
   sqo11 = sqo11 - (2 * sqvp * sqvq * sqvq * sqvq - sqvp * sqvq * sqq1 * sqq1 - sqvq * sqvq * sqp1 * sqq1)
   sqo14 = 3 * sqd3 + 2 * sqcc + 2 * sqvq * sqbf - sqvq * sqvq * sqdq2
   sqo15 = 6 * sqa3 + sqvq * sqbe - sqvp * sqbf - 1.5 * sqdq2 * sqdpq

   sc(11, 1) = (sqo9 + 2 * tt(sqr, 73)) / 4
   sc(11, 2) = sqo8 + 2 * tt(sqr, 92)
   sc(11, 3) = (sqo15 + 4 * tt(sqr, 79)) / 2
   sc(11, 4) = (sqo11 + 2 * tt(sqr, 97)) / 2
   sc(11, 5) = sqo14 + 4 * tt(sqr, 98)
RETURN

! ---------------------------------------------------------------------------------
! fam - give one pass its own family, M (85.1).
!
! The hat pass is combined by the (I) family and the check pass by the (Y) family -
! the same p and q quantities on the height ratio instead of the incidence ratio.
! Both are built alongside each other by the scheme; here each pass is given the one
! that belongs to it, at BOTH orders. Then the two corrections the arrangement rests
! on are applied to whichever ratio this pass travels on:
!
!   members one to five of the barred q accumulation from the identities
!   the sixth from the dual run, whose accumulated s_1p is -S-bar_6q
!
! In: fmb the row, fmd the dual row, fmc 1 for the check half, fms the saved family.
! ---------------------------------------------------------------------------------
SUB fam
   fmr = fmb
   IF (fmc == 1)
      FOR fmm = 1, 9, 1
         tt(fmr, 24 + fmm) = yf(fmr, fmm)
      NEXT
      FOR fmm = 1, 20, 1
         tt(fmr, 100 + fmm) = yf(fmr, 9 + fmm)
      NEXT
      fmq = rv(fmr, 1)
   ELSE
      FOR fmm = 1, 9, 1
         tt(fmr, 24 + fmm) = sv(1, fmm)
      NEXT
      FOR fmm = 1, 20, 1
         tt(fmr, 100 + fmm) = sv(1, 9 + fmm)
      NEXT
      fmq = tt(fmr, 6)
   ENDIF

   ! Where the ratio is unbounded the members cannot be formed as products and are
   ! left as the recursion built them.
   IF (ABSO(fmq) < 1E6)
      sqb = fmr
      GOSUB sqid

      tt(fmr, 102) = fmq * tt(fmr, 70) - sc(11, 1)
      tt(fmr, 104) = fmq * tt(fmr, 72) - sc(11, 2)
      tt(fmr, 107) = fmq * tt(fmr, 74) - sc(11, 3)
      tt(fmr, 109) = fmq * tt(fmr, 76) - sc(11, 4)
      tt(fmr, 112) = fmq * tt(fmr, 78) - sc(11, 5)

      ! The sixth from the dual run: XII (6.2) makes its accumulated s_1p the dual of
      ! S-bar_6q, and the dual comes out with the sign reversed.
      IF (ABSO(tt(fmd, 6)) < 1E6)
         tt(fmr, 114) = fmq * tt(fmr, 80) + tt(fmd, 69)
      ENDIF

      tt(fmr, 115) = -fmq * tt(fmr, 101) + tt(fmr, 102)
      tt(fmr, 116) = -fmq * tt(fmr, 103) + tt(fmr, 104)
      tt(fmr, 117) = -fmq * tt(fmr, 105) + tt(fmr, 107)
      tt(fmr, 118) = -fmq * tt(fmr, 108) + tt(fmr, 109)
      tt(fmr, 119) = -fmq * tt(fmr, 110) + tt(fmr, 112)
      tt(fmr, 120) = -fmq * tt(fmr, 113) + tt(fmr, 114)
   ENDIF
RETURN

! ---------------------------------------------------------------------------------
! tdzero - load every one of the surface own quantities as zero, for the residue.
! ---------------------------------------------------------------------------------
SUB tdzero
   tt(tzb, 10) = 0
   tt(tzb, 13) = 0
   tt(tzb, 38) = 0
   tt(tzb, 44) = 0
   tt(tzb, 50) = 0
   tt(tzb, 54) = 0
   tt(tzb, 59) = 0
   tt(tzb, 65) = 0
   tt(tzb, 45) = 0
   tt(tzb, 51) = 0
   tt(tzb, 55) = 0
   tt(tzb, 61) = 0
   tt(tzb, 66) = 0
   tt(tzb, 40) = 0
   FOR tzm = 1, 10, 1
      tt(tzb, 120 + tzm) = 0
   NEXT
RETURN

LABEL 900
IF (ABSO(paraxmode) < eps) THEN PARAXIAL OFF

END
