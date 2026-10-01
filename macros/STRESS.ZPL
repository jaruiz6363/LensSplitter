! STRESS - lens stress and relaxation parameters W, S and R, per surface and as
! system totals, after Sasian.
!
! Written by Javier Ruiz with Claude Code, September 2026.
!
! J. Sasian, "Introduction to Lens Design", Cambridge University Press, section
! 12.2 "Lens Stress and Relaxation". The quantities are his (12.5) to (12.8),
! his Table 12.1 and his Figure 12.3. Sasian says in that section that the W and
! S values and the n sin(I) plots were produced by writing a macro inside a lens
! design program; this is such a macro.
!
! WHAT THE THREE NUMBERS ARE FOR
!
!   A lens gets its power either gradually, spread over several weak surfaces, or
!   violently, by pitting strong positive against strong negative. The second buys
!   degrees of freedom - a flat field, mainly - and pays for them in higher-order
!   aberration and in tolerances. Nothing in a spot diagram says which of the two
!   you are looking at, and nothing in a Seidel table does either, because both
!   report the aberration that survived rather than the effort spent producing it.
!
!   W  (12.6) measures how hard the surfaces are working: the RMS of the weighted
!      power w = phi*y, normalised so that it does not move when you rescale the
!      lens or change the conjugate it works at.
!
!   S  (12.7) measures how far the lens departs from symmetry about the stop. The
!      odd aberrations - coma, distortion, lateral colour - cancel in a symmetric
!      lens, so a small S says those are cheap to correct here. Normalised against
!      the stop, so it does not move with field, scale or F/#.
!
!   R  (12.8) is the real-ray version of W: the RMS of n sin(I) over the surfaces
!      for a chosen ray. Large angles of incidence are what actually generate the
!      higher orders, and unlike W this one knows about the aperture and the field
!      you really use.
!
!   Smaller is more relaxed in every case. Sasian's Table 12.1 gives, for reference,
!   W and S of eight classical forms - Cooke triplet 1.12 and 0.89, double Gauss
!   1.01 and 0.16, telephoto 0.72 and 1.30, microscope objective 0.23 and 0.11.
!   Those are his lenses, not yours, so read them as the scale rather than as a
!   target: a triplet of your own is not wrong for coming out at 1.05.
!
! WHAT THIS MACRO DOES THAT THE PARAMETERS ALONE DO NOT
!
!   W and S are each one number for a whole lens, which tells you that a lens is
!   stressed without telling you where. Both are RMS over surfaces, so the tables
!   below print the per-surface term that goes into each sum. The surface with the
!   largest term is the one carrying the stress, and it is usually not the surface
!   a spot diagram would make you suspect.
!
! LIMITS, WHICH THE MACRO ENFORCES RATHER THAN DOCUMENTS
!
!   Rotationally symmetric and sequential. A non-axial system is refused: the
!   paraxial y and u basis these are built on does not mean what it should there.
!
!   Not afocal. W and S both divide by n_k' u_k', which is zero in afocal image
!   space. R alone would still be defined, but the macro declines as a whole
!   rather than print a table with two thirds of it missing.
!
!   Unit magnification, m = +1, makes Sasian's normalising factor (1 - m) vanish
!   and W and S with it. That is a property of his definition, not of the lens,
!   so the macro says so and prints the unnormalised RMS instead of a division by
!   zero.
!
!   Aspheres are fine here, which is worth saying because BUCH7 in this folder
!   refuses them. W and S are built on the paraxial trace and a conic or an even
!   asphere term does not touch it - such a surface carries no vertex power, which
!   is the same reason Sasian gives for aspheres being absent from the Petzval
!   sum. R is a real-ray quantity and OpticStudio's own angle of incidence carries
!   the figuring correctly.
!
!   Vignetting factors move the real rays, so they move R and the n sin(I) table
!   but not W and S. If the factors are set, the Py = +/-1 rays are at the
!   vignetted edge of the pupil rather than the full one, which is usually what
!   you want and is never what you expect. It is reported below.
!
! HOW IT WAS CHECKED, AND WHAT THAT IS WORTH
!
!   The two arrangements were confirmed on the OpticStudio sample "Cooke 40 degree
!   field" at 0.55 um against an independent implementation outside OpticStudio,
!   working from the same prescription. That gave W = 1.0559 and S = 0.9174,
!   against Sasian's 1.12 and 0.89 for his Cooke triplet in Table 12.1. Those are
!   two different triplets, so the agreement is a check on the conventions - the
!   sign of the chief-ray invariant, what k counts, where the stop factors go -
!   and not on the arithmetic, which no published number for this lens can settle.
!
!   Two checks the macro makes on itself every run, both printed:
!
!     w = phi*y must equal n'u' - nu up to sign, which is Sasian's (12.5). This is
!     an identity of the paraxial refraction equation, so it holding says the
!     index handling, the mirror sign flips and the ray basis are all consistent.
!     OpticStudio's sign convention gives n'u' - nu = -phi*y; Sasian writes them
!     equal, which is the same statement in his convention.
!
!     n sin(I) from the RAID operand must equal n sin(I) worked out from the ray
!     direction and the surface normal, which come from a different part of the
!     program. They will part company on a system with coordinate breaks, which
!     is one of the things that makes such a system not the sort meant here.
!
! ONE CHOICE THAT IS A CHOICE, MADE VISIBLE RATHER THAN BURIED
!
!   Sasian writes "a system of k surfaces" and does not say what to do about
!   surfaces that are not optical surfaces at all - a dummy plane, a stop floating
!   in air. They contribute nothing to either sum, because phi is zero and so is
!   delta(u/n), but counting them still shrinks W and S by sqrt(kept/counted).
!
!   This macro counts a surface when the index changes across it, so refraction
!   and reflection count and dummies do not. That makes W and S properties of the
!   lens rather than of how the file was typed. The value on the other convention
!   is printed underneath, because the choice is arbitrary enough to be worth
!   seeing both ways: on a lens with no dummy surfaces the two lines are equal,
!   and where they differ the reader can decide.
!
! THE ZPL TRAPS OF BUCH7 ALL APPLY - see macros/README.md. The one that
! bites here is that variable names are case-insensitive, so no variable in this
! file is named i, cosi, magn or curv: the first would collide with the loop
! counter, the rest with the built-in functions COSI, MAGN and CURV.
!
! AND ANOTHER, WHICH THIS FILE FOUND
!
!   OPEV traces rays of its own. It evaluates an optimization operand, and the
!   ray operands trace to do it, so RAYL, RAYM, RANX and the rest - which report
!   the ray traced LAST - come back describing the operand's ray rather than
!   yours. Reading them after an OPEV call returns plausible numbers for the
!   wrong ray. Everything wanted from a RAYTRACE is therefore read out below
!   before the first OPEV of that pass, not interleaved with it.
! ---------------------------------------------------------------------------------

paraxmode = PMOD()
PARAXIAL ON
eps = 1E-10
torad = ACOS(0) / 90
wl = PWAV()

ns = NSUR()
nsm = ns - 1
IF (nsm < 1)
   PRINT "STRESS needs at least one optical surface."
   GOTO 999
ENDIF
IF (nsm > 240)
   PRINT "STRESS is arranged for up to 240 surfaces and this system has more."
   GOTO 999
ENDIF

SETVECSIZE 1500

! System data. This fills VEC1, so everything wanted from it is taken out here,
! before VEC1 is needed for anything else.
GETSYSTEMDATA 1
efl = VEC1(7)
fnum = VEC1(10)
pmag = VEC1(16)
stopsf = INTE(VEC1(23) + 0.5)
afoc = VEC1(30)
nsyst = VEC1(33)

IF (nsyst > 0.5)
   PRINT "STRESS handles rotationally symmetric systems only, and OpticStudio"
   PRINT "reports this one as non-axial. The parameters are built on a paraxial"
   PRINT "y and u basis that does not describe such a system, so the macro"
   PRINT "declines rather than return numbers with no meaning behind them."
   GOTO 999
ENDIF

IF (afoc > 0.5)
   PRINT "STRESS cannot work in afocal image space. Both W and S are normalised"
   PRINT "by n_k' u_k', which is zero when the image space is afocal, so neither"
   PRINT "parameter exists for this system as Sasian defines it."
   GOTO 999
ENDIF

! ---------------------------------------------------------------------------------
! The paraxial rays. Each is traced once and stored, because RAYY and RAYM report
! only the ray traced last.
!
!   VEC2(k + 1)     marginal height at surface k
!   VEC2(501 + k)   marginal angle in the space AFTER surface k, so 500 + k is the
!                   angle in the space BEFORE surface k
!   VEC3            the same for the chief ray
! ---------------------------------------------------------------------------------

RAYTRACE 0, 0, 0, 1, wl
IF (RAYE() != 0)
   PRINT "The paraxial marginal ray will not trace. Is the aperture set?"
   GOTO 999
ENDIF
FOR j = 0, nsm, 1
   VEC2(j + 1) = RAYY(j)
   VEC2(501 + j) = RAYM(j) / RAYN(j)
NEXT

RAYTRACE 0, 1, 0, 0, wl
IF (RAYE() != 0)
   PRINT "The paraxial chief ray will not trace. Is a field defined?"
   GOTO 999
ENDIF
FOR j = 0, nsm, 1
   VEC3(j + 1) = RAYY(j)
   VEC3(501 + j) = RAYM(j) / RAYN(j)
NEXT

! The Lagrange invariant, formed in the space before the first surface. If it is
! zero there is no chief ray to speak of and S has nothing to normalise against.
n0 = INDX(0)
IF (ISMS(0)) THEN n0 = -n0
lag = n0 * (VEC3(501) * VEC2(2) - VEC2(501) * VEC3(2))
IF (ABSO(lag) < eps)
   PRINT "The Lagrange invariant is zero. Is the field zero, or the aperture?"
   GOTO 999
ENDIF

! n_k' u_k', the image-space refraction invariant of the marginal ray. Both W and
! S are divided by it, and it is what makes them independent of the F/# the lens
! happens to be drawn at.
nkuk = INDX(nsm)
IF (ISMS(nsm)) THEN nkuk = -nkuk
nkuk = nkuk * VEC2(501 + nsm)
IF (ABSO(nkuk) < eps)
   PRINT "n_k' u_k' is zero, so the image space is afocal in all but name and"
   PRINT "neither W nor S is defined. Nothing further is printed."
   GOTO 999
ENDIF

! The magnification, taken from the system data and, where there is an object-space
! marginal angle to do it with, derived a second way from the rays as traced. The
! two rest on different conventions and W and S are both built on the result, so a
! silent disagreement would be worth catching. At infinite conjugate u0 is zero,
! the second route says nothing, and m is zero as it should be.
pmchk = 0
IF (ABSO(VEC2(501)) > eps)
   pmchk = n0 * VEC2(501) / nkuk
   IF (ABSO(pmchk - pmag) > 1E-6 * (1 + ABSO(pmag)))
      PRINT "WARNING: the system data and the marginal ray disagree about the"
      PRINT "magnification - system data ", pmag
      PRINT "                 marginal ray ", pmchk
      PRINT "W and S are both normalised by (1 - m), so treat them as suspect."
      PRINT " "
   ENDIF
ENDIF

! Sasian's (1 - m) factor, which is what carries W and S across conjugates. It
! is singular at unit magnification, which is a fact about the definition rather
! than about the lens, so it is reported and worked around instead of divided by.
conjf = 1 - pmag
degen = 0
IF (ABSO(conjf) < 1E-6)
   degen = 1
   conjf = 1
ENDIF

! The stop factors for S. At the stop the chief ray crosses the axis, so its
! height there is zero and Abar_stop is just n times its angle; that is exactly
! what makes S independent of the field the lens is used at.
IF (stopsf < 1)
   PRINT "No stop surface is defined, so S has nothing to normalise against."
   GOTO 999
ENDIF
IF (stopsf > nsm)
   PRINT "The stop is on or beyond the image surface, which S cannot use."
   GOTO 999
ENDIF

nbef = INDX(stopsf - 1)
IF (ISMS(stopsf - 1)) THEN nbef = -nbef
astp = nbef * (VEC3(500 + stopsf) + VEC3(stopsf + 1) * CURV(stopsf))
ystp = VEC2(stopsf + 1)
IF (ABSO(astp) < eps)
   PRINT "The chief ray refraction invariant at the stop is zero, so S is not"
   PRINT "defined for this system. A telecentric stop space will do this."
   GOTO 999
ENDIF
IF (ABSO(ystp) < eps)
   PRINT "The marginal ray height at the stop is zero, so S is not defined."
   GOTO 999
ENDIF

! ---------------------------------------------------------------------------------
! Header
! ---------------------------------------------------------------------------------

FORMAT 12.6
PRINT "STRESS - lens stress and relaxation, after Sasian section 12.2"
PRINT " "
PRINT "Wavelength number      ", wl
PRINT "Effective focal length ", efl
PRINT "Working F/number       ", fnum
PRINT "Lagrange invariant     ", lag
PRINT "Paraxial magnification ", pmag
PRINT "n_k' u_k'              ", nkuk
FORMAT 5.0 INT
PRINT "Stop surface           ", stopsf
FORMAT 12.6
PRINT "Abar at the stop       ", astp
PRINT "Marginal y at the stop ", ystp
IF (degen == 1)
   PRINT " "
   PRINT "NOTE: the magnification is 1, so Sasian's (1 - m) factor is zero and his"
   PRINT "W and S are singular here. The sums below are printed with that factor"
   PRINT "left out, which makes them comparable with each other but NOT with the"
   PRINT "values in his Table 12.1."
ENDIF
PRINT " "

! ---------------------------------------------------------------------------------
! W, the power distribution parameter, (12.5) and (12.6).
!
! The weighted power w = phi*y is the change of ray slope the surface imposes, and
! for a thin lens in air with the stop at it, spherical aberration, coma and
! astigmatism go as w^3, w^2 and w. So a surface with a large w is a surface doing
! violence to the beam, whatever its Seidel contributions happen to add up to.
! ---------------------------------------------------------------------------------

PRINT "Power distribution.  w = phi*y is the weighted power, Sasian (12.5); the"
PRINT "W term is w / (n_k' u_k'), and W is its RMS over the counted surfaces."
PRINT " "
PRINT " Surf            phi              y        w = phi y         W term"

wsum = 0
kact = 0
maxdev = 0
FOR j = 1, nsm, 1
   nbef = INDX(j - 1)
   IF (ISMS(j - 1)) THEN nbef = -nbef
   naft = INDX(j)
   IF (ISMS(j)) THEN naft = -naft
   cv = CURV(j)
   ymar = VEC2(j + 1)
   umb = VEC2(500 + j)
   uma = VEC2(501 + j)

   phi = (naft - nbef) * cv
   wgtp = phi * ymar
   wtrm = wgtp / nkuk

   ! Sasian (12.5) as an identity: n'u' - nu is -phi*y in OpticStudio's sign
   ! convention. If this parts company the ray basis or the index handling is
   ! wrong, and every number below it is wrong with it.
   wref = naft * uma - nbef * umb
   dev = ABSO(wref + wgtp) / (1 + ABSO(wgtp))
   IF (dev > maxdev) THEN maxdev = dev

   ! A surface counts when the index changes across it. A mirror changes it by
   ! sign, so mirrors count; a dummy plane does not.
   IF (ABSO(naft - nbef) > eps)
      kact = kact + 1
      wsum = wsum + wtrm * wtrm
   ENDIF

   FORMAT 5.0 INT
   PRINT j,
   FORMAT 15.6 EXP
   PRINT phi, ymar, wgtp, wtrm
NEXT

IF (kact < 1)
   PRINT " "
   PRINT "No surface in this system changes the index of refraction, so there is"
   PRINT "nothing for W or S to be an RMS over."
   GOTO 999
ENDIF

! ---------------------------------------------------------------------------------
! S, the symmetry parameter, (12.7).
!
! A surface contributes field aberration when it is not concentric to the stop
! (Abar is not zero) or not aplanatic (delta(u/n) is not zero). The product of the
! two, weighted by the marginal height and referred to the stop, is what Sasian
! sums. In a lens symmetric about the stop the odd aberrations cancel, and a small
! S says how nearly that is the case here.
! ---------------------------------------------------------------------------------

PRINT " "
PRINT "Symmetry about the stop.  Abar is the chief ray refraction invariant and"
PRINT "delta(u/n) the aplanatic factor; the S term is their product times y, over"
PRINT "n_k' u_k' Abar_stop y_stop, and S is its RMS.  Sasian (12.7)."
PRINT " "
PRINT " Surf           Abar     delta(u/n)              y         S term"

ssum = 0
FOR j = 1, nsm, 1
   nbef = INDX(j - 1)
   IF (ISMS(j - 1)) THEN nbef = -nbef
   naft = INDX(j)
   IF (ISMS(j)) THEN naft = -naft
   cv = CURV(j)
   ymar = VEC2(j + 1)
   umb = VEC2(500 + j)
   uma = VEC2(501 + j)
   ycrf = VEC3(j + 1)
   ucb = VEC3(500 + j)

   abar = nbef * (ucb + ycrf * cv)
   dun = uma / naft - umb / nbef
   strm = abar * dun * ymar / (nkuk * astp * ystp)

   IF (ABSO(naft - nbef) > eps) THEN ssum = ssum + strm * strm

   FORMAT 5.0 INT
   PRINT j,
   FORMAT 15.6 EXP
   PRINT abar, dun, ymar, strm
NEXT

! ---------------------------------------------------------------------------------
! The two parameters, on both counting conventions.
! ---------------------------------------------------------------------------------

kall = nsm
wpar = SQRT(wsum / (kact * conjf * conjf))
spar = SQRT(ssum / (kact * conjf * conjf))
wpalt = SQRT(wsum / (kall * conjf * conjf))
spalt = SQRT(ssum / (kall * conjf * conjf))

PRINT " "
FORMAT 5.0 INT
PRINT "Surfaces that change the index  ", kact
PRINT "Surfaces in the file            ", kall
FORMAT 12.6
PRINT " "
PRINT "W  (index-changing surfaces)  ", wpar
PRINT "S  (index-changing surfaces)  ", spar
PRINT "W  (all surfaces counted)     ", wpalt
PRINT "S  (all surfaces counted)     ", spalt
PRINT " "
PRINT "The first pair is the one to quote. The second differs only when the file"
PRINT "carries dummy surfaces, which contribute nothing to either sum but do"
PRINT "enlarge k; see the note at the head of this macro."
PRINT " "
PRINT "For scale, Sasian's Table 12.1 gives W and S for eight forms: Cooke triplet"
PRINT "1.12 and 0.89, Petzval objective 0.63 and 0.62, double Gauss 1.01 and 0.16,"
PRINT "telephoto 0.72 and 1.30, reversed telephoto 1.16 and 0.98, fisheye 0.23 and"
PRINT "0.19, micro-lithography relay 0.28 and 0.19, microscope objective 0.23 and"
PRINT "0.11. They are his lenses; read them as the scale, not as a target."
PRINT " "

FORMAT 12.6
IF (maxdev > 1E-8)
   PRINT "WARNING: w = phi*y and n'u' - nu disagree by a relative ", maxdev
   PRINT "These are the same quantity by the paraxial refraction equation, so the"
   PRINT "index handling or the ray basis is wrong and W and S are wrong with it."
   PRINT " "
ELSE
   PRINT "Check: w = phi*y equals n'u' - nu on every surface, Sasian (12.5)."
   PRINT " "
ENDIF

! ---------------------------------------------------------------------------------
! R, the real-ray stress metric, (12.8), and the n sin(I) table of Figure 12.3.
!
! W is paraxial and so knows nothing about the aperture and field the lens is
! actually used over. n sin(I) does. It is the surface refraction invariant of a
! real ray, it is conserved across the surface, and it is large exactly where the
! higher orders are generated. Sasian plots it for four meridional rays; those are
! the four columns below.
! ---------------------------------------------------------------------------------

PARAXIAL OFF
rcode = OCOD("RAID")

PRINT "Real ray surface refraction invariant n sin(I), Sasian Figure 12.3. The"
PRINT "four rays are the marginal ray, the chief ray, and the two meridional rim"
PRINT "rays at full field. R is the RMS down each column, Sasian (12.8)."
PRINT " "
PRINT " Surf   marginal(0,1)     chief(1,0)         (1,+1)         (1,-1)"

nrmdev = 0
FOR r = 1, 4, 1
   hyv = 1
   pyv = 0
   IF (r == 1)
      hyv = 0
      pyv = 1
   ENDIF
   IF (r == 3) THEN pyv = 1
   IF (r == 4) THEN pyv = -1

   ! Traced once, for the error flag and for the second route to n sin(I). The
   ! whole of that route is read out here, BEFORE the first OPEV call: OPEV
   ! evaluates an optimization operand and so traces rays of its own, and RAYM
   ! and RANY report only the ray traced last. Reading them after the operand
   ! would silently be reading the operand's ray, not this one.
   RAYTRACE 0, hyv, 0, pyv, wl
   VEC2(801 + r) = RAYE()
   VEC2(811 + r) = RAYV()

   ! Route two: the incident direction is the one leaving the previous surface,
   ! and the normal is the program's own at this intercept. The two part company
   ! only when the surfaces do not share an orientation - coordinate breaks,
   ! tilts - which is a system this arrangement is not for.
   FOR j = 1, nsm, 1
      dirl = RAYL(j - 1)
      dirm = RAYM(j - 1)
      dirn = RAYN(j - 1)
      nrmx = RANX(j)
      nrmy = RANY(j)
      nrmz = RANZ(j)
      cangl = ABSO(dirl * nrmx + dirm * nrmy + dirn * nrmz)
      IF (cangl > 1) THEN cangl = 1
      VEC2(1001 + j) = ABSO(INDX(j - 1)) * SINE(ACOS(cangl))
   NEXT

   ! Route one, the program's own angle of incidence, which is what is reported.
   FOR j = 1, nsm, 1
      ang = OPEV(rcode, j, wl, 0, hyv, 0, pyv)
      nsin = ABSO(INDX(j - 1)) * SINE(ang * torad)
      VEC4((r - 1) * 250 + j) = nsin
      dev = ABSO(VEC2(1001 + j) - nsin)
      IF (dev > nrmdev) THEN nrmdev = dev
   NEXT
NEXT

FOR j = 1, nsm, 1
   FORMAT 5.0 INT
   PRINT j,
   FORMAT 15.6 EXP
   PRINT VEC4(j), VEC4(250 + j), VEC4(500 + j), VEC4(750 + j)
NEXT

! The four totals are worked out into store first and printed in one statement,
! rather than appended to an open print line across a loop.
FOR r = 1, 4, 1
   rsum = 0
   FOR j = 1, nsm, 1
      nbef = INDX(j - 1)
      IF (ISMS(j - 1)) THEN nbef = -nbef
      naft = INDX(j)
      IF (ISMS(j)) THEN naft = -naft
      IF (ABSO(naft - nbef) > eps)
         nsin = VEC4((r - 1) * 250 + j)
         rsum = rsum + nsin * nsin
      ENDIF
   NEXT
   VEC2(821 + r) = SQRT(rsum / kact)
NEXT

PRINT " "
FORMAT 15.6 EXP
PRINT "    R", VEC2(822), VEC2(823), VEC2(824), VEC2(825)

PRINT " "
PRINT "R is over the same index-changing surfaces as W and S, so the three are"
PRINT "comparable with one another. Sasian takes R for the marginal, chief or"
PRINT "any other chosen ray; all four are above."
PRINT " "

! Ray health. A vignetted or failed ray makes its whole column meaningless, and
! the RAID operand returns zero rather than complaining, so it is said here.
FOR r = 1, 4, 1
   IF (VEC2(801 + r) != 0)
      FORMAT 5.0 INT
      PRINT "WARNING: ray ", r, " failed to trace; its column above is not real data."
   ENDIF
   IF (VEC2(811 + r) != 0)
      FORMAT 5.0 INT
      PRINT "NOTE: ray ", r, " is vignetted at surface ", VEC2(811 + r)
   ENDIF
NEXT

FORMAT 12.6
IF (nrmdev > 1E-6)
   PRINT " "
   PRINT "WARNING: the RAID operand and the surface normal disagree about n sin(I)"
   PRINT "by up to ", nrmdev
   PRINT "That happens when the surfaces do not share an orientation - coordinate"
   PRINT "breaks, tilts - and this arrangement is not meant for such a system."
ELSE
   PRINT "Check: n sin(I) from the RAID operand agrees with the same quantity"
   PRINT "worked out from the ray direction and the surface normal."
ENDIF

! Put the paraxial mode back as it was found. Unlike BUCH7, this macro turns the
! mode OFF partway through - the real-ray section needs it off - so restoring it
! has to be able to turn it back ON, not only off.
LABEL 999
IF (ABSO(paraxmode) < eps) THEN PARAXIAL OFF
IF (ABSO(paraxmode) > eps) THEN PARAXIAL ON
END
