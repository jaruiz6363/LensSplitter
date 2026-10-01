! RAYINV - third-, fifth- and seventh-order aberration coefficients recovered from
! REAL TRACED RAYS, by inverting the transverse aberration rather than by any series.
!
! Written by Javier Ruiz with Claude Code, September 2026.
!
! WHY THIS EXISTS, WHICH IS A NARROW AND SPECIFIC REASON
!
!   BUCH7 computes the coefficients from Buchdahl's arranged tables. FORBES computes
!   them from Forbes' series trace. On a system of spheres the two agree on all
!   thirty-seven to every printed digit, and FIFTHORD - which ships with OpticStudio
!   and needs nothing from this repository - agrees with both on eighteen of them.
!
!   On an ASPHERIC system that structure falls apart:
!
!     BUCH7 declines outright.
!     FIFTHORD reaches B7 and stops, so it covers the third order, the fifth, and
!       tau1 - eighteen quantities - and nothing above.
!     The Seidel analysis reaches the third order and stops.
!
!   So tau2 to tau20 on a figured lens - NINETEEN COEFFICIENTS, and precisely the
!   ones Buchdahl's aspheric arrangement gets wrong - have nothing to be checked
!   against except a second implementation of the same series method. That is not a
!   check; it is the same argument twice.
!
!   THIS MACRO IS THE THIRD OPINION. It uses no series at all. It traces real rays
!   through OpticStudio, measures where they land, and recovers the coefficients
!   from the measurements. Where it and FORBES agree, they agree by two routes with
!   nothing whatever in common except the definition of the coefficients.
!
! HOW IT WORKS
!
!   A traced ray gives no way to separate the seventh order from the ninth except by
!   watching how they shrink. So each ray shape is traced at a LADDER OF SCALES -
!   shrink the ray by s and the pupil goes to s rho, the field to s h - and an odd
!   polynomial in s is fitted through the landings. The coefficient of s^3, s^5 and
!   s^7 is then the third-, fifth- and seventh-order transverse aberration of that
!   shape. Seventy-five shapes give an overdetermined system for the coefficients.
!
!   THE LADDER IS THE WHOLE GAME AND IS NOT ARBITRARY. Measured against B7 on an
!   axial fan - where the degree-seven part is that coefficient alone and B7 is known
!   exactly - a ladder running out to s = 1 with powers to s^11 recovers it only to
!   about 1.5 per cent, because the ninth order and above are still large at full
!   aperture and the fit cannot separate them from the seventh. Twelve points out to
!   six tenths, with powers to s^15 to absorb the tail, recovers it to better than
!   one part in a million. Those two numbers are the reason for every constant below.
!
!   THE PARAXIAL IMAGE HEIGHT IS SUBTRACTED BEFORE FITTING. It is the whole of the
!   degree-one term, and leaving it in is fatal rather than untidy: at seven tenths
!   of the field it is some twelve millimetres sitting on a third-order term of two
!   hundredths, so the fit would have to cancel three orders of magnitude to reach
!   the aberration at all.
!
!   THE FIT IS DONE IN t = s / s_max, NOT IN s. The powers run to fifteen, so in s
!   the normal-equations matrix spans thirty orders of magnitude and its diagonal
!   ratio is 7E+06; rescaled it is 4. That turns a solve which needs a QR into one
!   an ordinary Gaussian elimination handles, which is what ZPL can carry. The
!   recovered coefficients are divided back by the corresponding power of s_max.
!
! THE MODEL IS SHARED WITH FORBES ON PURPOSE, AND THAT IS NOT A COMPROMISE
!
!   Both routes fit the same twenty coefficients through the same linear model -
!   Robb's Eq. (2) with one coefficient set to one at a time. The INDEPENDENCE lives
!   entirely in the right-hand side: series arithmetic there, OpticStudio's own ray
!   trace here. Were the two to use different bases they would not be computing the
!   same quantity and the comparison would mean nothing, so the shared model is what
!   makes the check a check.
!
! THE FIELD VARIABLE, WHICH IS WHERE THE CONJUGATES WOULD DIFFER AND DO NOT
!
!   The ladder shrinks the field along with the pupil, so the macro has to hand
!   OpticStudio a NORMALISED field coordinate that moves the aberration-theory field
!   variable - the paraxial image height - in exact proportion to the scale. Whether
!   the normalised coordinate already does that turns on the FIELD TYPE, and not on
!   the conjugate, which is why carrying the finite conjugate costs so little here:
!
!     ANGLE. The normalised coordinate is a fraction of the ANGLE while the field
!       variable goes as its TANGENT, so the scale must be run through
!       atan(s h tan a_max) / a_max. This holds at EITHER conjugate, and the reason
!       is worth stating because it is what makes the finite conjugate nearly free:
!       OpticStudio gives the object-space CHIEF RAY the field angle itself, at both
!       conjugates, so a finite object sits at height -(t + ep) tan(field) - still
!       exactly linear in the tangent, only with a different constant in front of it,
!       and the constant divides out of a ratio. That was measured against
!       OpticStudio rather than taken on trust, and then wired into the check below.
!     OBJECT HEIGHT, and PARAXIAL IMAGE HEIGHT. Both are already linear in the
!       normalised coordinate, so the scale is handed over unchanged.
!     REAL IMAGE HEIGHT. REFUSED. OpticStudio iterates the chief ray until it lands
!       where it was asked to, which makes the image height linear in the coordinate
!       BY CONSTRUCTION and folds the distortion into the field variable itself. The
!       ladder would then be running a field of s + O(s^3) rather than s, and every
!       order above the third would take a share of the order below it. The third
!       order would survive and the rest would be quietly wrong, which is the worse
!       of the two ways to fail.
!
!   The earlier version of this macro applied the angle mapping whatever the field
!   type was. That was right for the one conjugate it accepted and wrong for paraxial
!   image height, which it would have taken without comment.
!
!   THE PARAXIAL IMAGE HEIGHT IS MEASURED, NOT ASSUMED. It is taken by tracing the
!   paraxial chief ray at full field and projecting it to the same plane the real
!   rays are caught on, which makes it the right thing to subtract by definition
!   rather than by a formula that happens to agree. The old efl tan(field) is that
!   same quantity divided by the image space index, so the two part company when the
!   image sits in glass; projecting costs one paraxial ray and is right in both.
!
! LIMITS
!
!   Either conjugate, but field type angle, object height or paraxial image height
!   only - real image height and theodolite angles are refused, for the reason above.
!
!   REAL RAY AIMING IS REFUSED. With it on the normalised pupil coordinate aims at
!   the real stop rather than at the paraxial entrance pupil, so rho stops being the
!   variable these coefficients are defined in: it differs from it by the pupil
!   aberration. The third order would be untouched, because that enters at degree
!   three and shifts the third order only at degree five, and the fifth and seventh
!   would disagree with FORBES by a few per cent. That reads exactly like a fault in
!   one of the two routes and is not one, which is why it is refused rather than
!   noted. Ray aiming is far likelier to be on at a finite conjugate than at
!   infinity, so this guard earns its keep mostly on the path just added.
!
!   Rotationally symmetric and sequential. It is NOT slow: nine hundred real ray traces,
!   and each of the seventy-five shapes then takes two eight-by-eight solves, which is
!   cheap next to FORBES' series arithmetic.
!
!   It is less accurate than either series route and is meant to be. A traced ray
!   carries every order at once and the fit has to separate them, so the answer
!   arrives with a fitting residual where a series arrives exactly. Expect agreement
!   with FORBES to a few parts in ten thousand, not to machine precision - and read a
!   disagreement at that level as the fit, not as either route being wrong.
!
! THE ZPL TRAPS: see macros/README.md. All of them apply. In particular every loop whose
! bound can fall below its start is guarded, because ZPL reports an empty range as an
! infinite loop rather than executing it zero times.
! ---------------------------------------------------------------------------------

eps = 1E-10
paraxmode = PMOD()

! The system data is read BEFORE the vectors are sized, and the four numbers wanted
! from it are taken out into scalars immediately. SETVECSIZE resizes all four
! vectors together and DISCARDS EVERYTHING IN THEM, so a GETSYSTEMDATA on the far
! side of it would be reading a vector that is about to be thrown away - or, worse,
! would be fine here and not somewhere else. The guards that use these four are
! further down, with the other guards, where they read better.
GETSYSTEMDATA 3
sysefl = VEC3(7)
sysmag = VEC3(16)
rayaim = VEC3(19)
nonaxi = VEC3(33)

SETVECSIZE 4000

! ---------------------------------------------------------------------------------
! Storage.
!
!   VEC1  the fit: the scale ladder, the landings, the eight-by-eight normal matrix
!   VEC2  the shapes, the model rows, and the three coefficient systems
!   VEC4  the odd powers
! ---------------------------------------------------------------------------------

nsc = 12
npw = 8

! ZPL COMMENTS ARE WHOLE-LINE ONLY. There is no trailing comment: a ! after a
! statement is not the start of a comment, so the parser reads on and reports the
! next word as an unknown symbol. These eight lines carried their descriptions on
! the right and the macro would not run because of it.
!
!   vsc  the scale ladder, 12
!   vtt  the same rescaled to t = s / s_max, 12
!   vey  meridional landings, 12
!   vez  sagittal landings, 12
!   vfm  master fit normal matrix, 8 by 8
!   vfw  a working copy of it, since the solve destroys what it is given
!   vfb  the fit right-hand side, and afterwards the solution
!   vfx  the recovered odd coefficients, divided back into the s variable

vsc = 1
vtt = 21
vey = 41
vez = 61
vfm = 101
vfw = 201
vfb = 301
vfx = 321

mrowy = 3101
mrowz = 3121

PRINT "==================================================================="
PRINT "RAYINV - coefficients from REAL TRACED RAYS, no series"
PRINT "==================================================================="
PRINT " "

! ---------------------------------------------------------------------------------
! The system, and the ray basis.
! ---------------------------------------------------------------------------------

PARAXIAL ON
wl = PWAV()
ns = NSUR()
nsm = ns - 1

IF (nsm < 1)
   PRINT "RAYINV needs at least one optical surface."
   GOTO 900
ENDIF

IF (nonaxi > 0.5)
   PRINT "This system is not rotationally symmetric. Every coefficient below is"
   PRINT "defined only for a system that is, so there is nothing here to compute."
   GOTO 900
ENDIF

IF (rayaim > 1.5)
   PRINT "REAL RAY AIMING IS ON, and this macro cannot be trusted with it."
   PRINT " "
   PRINT "The normalised pupil coordinate then aims at the real stop rather than at"
   PRINT "the paraxial entrance pupil, so rho stops being the variable in which these"
   PRINT "coefficients are defined - it differs from it by the pupil aberration. The"
   PRINT "third order would still come out right and the fifth and seventh would"
   PRINT "disagree with FORBES by a few per cent, which reads exactly like a fault in"
   PRINT "one of the two routes and is not one."
   PRINT " "
   PRINT "Turn ray aiming off and run it again."
   GOTO 900
ENDIF
IF (rayaim > 0.5)
   PRINT "NOTE: paraxial ray aiming is on. On a centred system with no pupil shift"
   PRINT "that leaves the pupil coordinate where this macro needs it, but if the"
   PRINT "fifth order disagrees with FORBES it is the first thing to turn off."
   PRINT " "
ENDIF

! Which conjugate, and which field type. The conjugate decides only what gets
! printed; the FIELD TYPE decides how the scale ladder reaches the field. Those two
! are deliberately kept apart - see the head of this file.
!
! THE BOUNDARY GOES ON THE INFINITE SIDE, and that is not fussiness. OpticStudio
! returns EXACTLY 1E10 for an object thickness of INFINITY, so a test written as
! "> 1E10" is false for a lens at infinity and reads it as finite. This macro shipped
! that way for one run: on an infinite-conjugate asphere it printed "finite", an
! object distance of ten thousand million, an object height of -4.09 that means
! nothing, and a magnification of zero. BUCH7 has always had the polarity below,
! which is why it never showed there.
infconj = 1
IF (ABSO(THIC(0)) < 1E10) THEN infconj = 0

fldtyp = FTYP()
fangle = 0
IF (fldtyp < 0.5) THEN fangle = 1

IF (fldtyp > 2.5)
   PRINT "The field type is real image height or theodolite angles, and neither can"
   PRINT "be used here."
   PRINT " "
   PRINT "With real image height OpticStudio iterates the chief ray until it lands"
   PRINT "where it was asked to, so the image height is linear in the field"
   PRINT "coordinate BY CONSTRUCTION and the distortion is folded into the field"
   PRINT "variable itself. The scale ladder would then be running a field of"
   PRINT "s + O(s^3) rather than s, and every order above the third would take a"
   PRINT "share of the order below it - the third order right, the rest quietly"
   PRINT "wrong, which is the worse of the two ways to fail."
   PRINT " "
   PRINT "Convert the fields to angle or object height and run it again."
   GOTO 900
ENDIF

fdesc$ = "angle"
IF (fldtyp > 0.5) THEN fdesc$ = "object height"
IF (fldtyp > 1.5) THEN fdesc$ = "paraxial image height"

RAYTRACE 0, 0, 0, 1, wl
IF (RAYE() != 0)
   PRINT "The paraxial marginal ray will not trace. Is the aperture set?"
   GOTO 900
ENDIF
mgy = RAYY(1)
mgu = RAYM(nsm) / RAYN(nsm)
IF (ABSO(mgu) < eps)
   PRINT "The system is afocal; there is no focus to catch the rays on."
   GOTO 900
ENDIF

! THE FRAME. Buchdahl's coefficients carry a reflection in the sign of the index, and his
! image-space transverse aberration is measured along an axis each mirror reverses. RAYY
! does not reverse: OpticStudio keeps one frame through a mirror. So after an odd number of
! reflections every landing is the negative of what the coefficients describe, and every
! recovered coefficient would come out negated - measured on the parabola in the C#
! program, where all seventeen third- and fifth-order ones did exactly that, zeros
! included, until the landings were turned.
!
! The parity is counted from the glass names: a surface whose glass is MIRROR reflects,
! which is the test the C# program uses too. It was first read from the marginal ray's
! direction cosine after the last surface, and on F4 that did not fire: the ray is traced
! with PARAXIAL ON, and there RAYN is not the direction of travel. The coefficients came
! out negated, which is how it was found. A glass name has no such ambiguity.
fsgn = 1
FOR fi = 1, nsm, 1
   fglas$ = $GLASS(fi)
   IF (SCOM(fglas$, "MIRROR") == 0) THEN fsgn = -fsgn
NEXT

! The paraxial focus, where the axial marginal ray crosses the axis. It is the same
! construction at either conjugate, and it is where the rays are caught - not the
! image surface the file happens to carry - so that the answer does not move when
! someone refocuses the lens.
gpfd = -RAYY(nsm) / mgu

! -mgy / mgu is the focal length only at the infinite conjugate, where the marginal
! ray arrives collimated and its height at surface one IS the pupil height it was
! launched at. At a finite conjugate the same ratio is a working distance and means
! nothing, so the focal length is taken from the system data there. Nothing below
! depends on this either way; it is printed for orientation and no more.
efl = sysefl
IF (infconj == 1) THEN efl = -mgy / mgu

RAYTRACE 0, 1, 0, 0, wl
IF (RAYE() != 0)
   PRINT "The paraxial chief ray will not trace. Is a field defined?"
   GOTO 900
ENDIF
hmax = RAYM(0) / RAYN(0)
objh = RAYY(0)

amax = 0
IF (fangle == 1)
   IF (ABSO(hmax) < eps)
      PRINT "The chief ray carries no field angle. Is the field zero?"
      GOTO 900
   ENDIF
   amax = ATAN(hmax)
ENDIF

! THE PARAXIAL IMAGE HEIGHT AT FULL FIELD, MEASURED rather than assumed: the
! paraxial chief ray projected to the very plane the real rays are caught on. That
! is what makes it the right thing to subtract by definition instead of by a formula
! which happens to agree - efl tan(field) is this divided by the image space index.
! gpfd is used here bare, where the real trace below subtracts RAYZ from it. That is
! not an oversight: a paraxial ray lands on the TANGENT PLANE by construction, so
! there is no sag to take off, and writing it this way keeps hpmax right whatever
! OpticStudio chooses to report for RAYZ in paraxial mode. The real rays do have a
! sag and do subtract it.
hpmax = RAYY(nsm) + gpfd * RAYM(nsm) / RAYN(nsm)
IF (ABSO(hpmax) < eps)
   PRINT "The paraxial image height at full field is zero, so there is no field with"
   PRINT "which to separate the field-dependent coefficients."
   GOTO 900
ENDIF

! ---------------------------------------------------------------------------------
! THE FIELD MAPPING, PROVED ON THIS SYSTEM RATHER THAN ASSUMED.
!
! Everything below rests on one claim: that the normalised coordinate shoot builds
! puts the paraxial image height at exactly its intended fraction of full field. That
! claim is about OpticStudio's field conventions and not about optics, and it is the
! kind of thing which is easy to get wrong and impossible to see in the output - a
! field mapping off by half a per cent moves every field-dependent coefficient and
! reads as a fault in FORBES.
!
! It cost nothing to prove instead. The chief ray is sent at the coordinate the
! ladder would use for seven tenths of full field, and the paraxial image height has
! to come back as seven tenths of hpmax. One paraxial ray settles it on THIS system,
! whatever the field type and whichever conjugate.
! ---------------------------------------------------------------------------------

qfr = 0.7
qhy = qfr
IF (fangle == 1) THEN qhy = ATAN(qfr * hmax) / amax

RAYTRACE 0, qhy, 0, 0, wl
IF (RAYE() != 0)
   PRINT "The paraxial chief ray will not trace at seven tenths of full field."
   GOTO 900
ENDIF
qchk = RAYY(nsm) + gpfd * RAYM(nsm) / RAYN(nsm)
qerr = ABSO(qchk - qfr * hpmax) / ABSO(hpmax)

IF (qerr > 1E-8)
   FORMAT 12.4 EXP
   PRINT "THE FIELD MAPPING IS NOT LINEAR IN THE SCALE. Relative error ", qerr
   PRINT " "
   PRINT "At seven tenths of full field the paraxial image height should be seven"
   PRINT "tenths of its full-field value and it is not, so the ladder would be"
   PRINT "shrinking a field of s + O(s^3) rather than s. Every order above the third"
   PRINT "would then take a share of the order below it - the third order right, the"
   PRINT "rest quietly wrong. Whatever this system's field convention is, it is not"
   PRINT "one this macro knows how to scale."
   GOTO 900
ENDIF

FORMAT 12.6
PRINT "Field type                  ", fdesc$
IF (infconj == 1)
   PRINT "Conjugate                   infinite"
   PRINT "Effective focal length     ", efl
   PRINT "tan of the maximum field   ", hmax
ELSE
   PRINT "Conjugate                   finite"
   PRINT "Object distance            ", THIC(0)
   PRINT "Object height at full field", objh
   PRINT "Paraxial magnification     ", sysmag
   PRINT "Effective focal length     ", efl
ENDIF
PRINT "Paraxial focus distance    ", gpfd
PRINT "Paraxial image height      ", hpmax
FORMAT 12.4 EXP
PRINT "Field mapping residual     ", qerr
PRINT " "

! ---------------------------------------------------------------------------------
! The ladder, and the fit matrix it induces. The matrix depends only on the scales,
! so it is built once and copied for each of the hundred and fifty solves.
! ---------------------------------------------------------------------------------

VEC4(1) = 1
VEC4(2) = 3
VEC4(3) = 5
VEC4(4) = 7
VEC4(5) = 9
VEC4(6) = 11
VEC4(7) = 13
VEC4(8) = 15

smax = 0
FOR jsc = 1, nsc, 1
   VEC1(vsc + jsc - 1) = 0.6 * jsc / nsc
   IF (VEC1(vsc + jsc - 1) > smax) THEN smax = VEC1(vsc + jsc - 1)
NEXT
FOR jsc = 1, nsc, 1
   VEC1(vtt + jsc - 1) = VEC1(vsc + jsc - 1) / smax
NEXT

FOR ji = 0, npw - 1, 1
   FOR jj = 0, npw - 1, 1
      vsum = 0
      FOR jsc = 0, nsc - 1, 1
         vsum = vsum + POWR(VEC1(vtt + jsc), VEC4(1 + ji)) * POWR(VEC1(vtt + jsc), VEC4(1 + jj))
      NEXT
      VEC1(vfm + ji * npw + jj) = vsum
   NEXT
NEXT

! ---------------------------------------------------------------------------------
! The seventy-five shapes, the same set FORBES uses.
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

PARAXIAL OFF

PRINT "Tracing 75 shapes at 12 scales - 900 real rays. This is slow."
PRINT " "

nbad = 0
FOR jsh = 0, nsh - 1, 1
   srho = VEC2(3201 + jsh * 3)
   sthe = VEC2(3202 + jsh * 3)
   shh = VEC2(3203 + jsh * 3)

   GOSUB shoot

   IF (sok == 1)
      GOSUB fitshape

      mord = 3
      nunk = 5
      gata = 2001
      gatb = 2101
      GOSUB mkmod
      ry = c3y
      rz = c3z
      GOSUB accum

      mord = 5
      nunk = 12
      gata = 2201
      gatb = 2401
      GOSUB mkmod
      ry = c5y
      rz = c5z
      GOSUB accum

      mord = 7
      nunk = 20
      gata = 2501
      gatb = 3001
      GOSUB mkmod
      ry = c7y
      rz = c7z
      GOSUB accum
   ELSE
      nbad = nbad + 1
   ENDIF
NEXT

PARAXIAL ON

IF (nsh - nbad < 25)
   PRINT "Too few shapes traced to determine twenty coefficients. Rays are being"
   PRINT "lost - check the apertures and the vignetting factors."
   GOTO 900
ENDIF

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
IF (ABSO(paraxmode) > eps) THEN PARAXIAL ON
IF (ABSO(paraxmode) < eps) THEN PARAXIAL OFF

END

! =================================================================================
! SUBROUTINES
! =================================================================================

! ---------------------------------------------------------------------------------
! shoot - trace the current shape at all twelve scales and store the transverse
! aberration, meridional in VEC1(vey..) and sagittal in VEC1(vez..).
!
! The field is set through the NORMALISED coordinate, and kfr - the fraction of full
! field this rung is meant to sit at - is what the coefficients are defined against.
! Turning kfr into that coordinate is the ONE place the field type enters:
!
!   angle           the coordinate is a fraction of the ANGLE and kfr is a fraction
!                   of its TANGENT, so it goes through atan(kfr tan a_max) / a_max
!   object height,  already linear in the coordinate, so kfr is handed over as it is
!   paraxial image
!
! Neither branch asks which conjugate it is on, and that is the point: a finite
! object at distance t sits at height -t tan(field), so the angle mapping is the same
! there as at infinity. See the head of this file.
!
! hpmax kfr is the paraxial image height for this rung, and subtracting it is fatal
! rather than untidy to skip - it is the whole of the degree-one term.
!
! The landing is taken at the PARAXIAL FOCUS, not at the image surface the file
! happens to carry, so that the answer does not move when someone refocuses the lens.
! ---------------------------------------------------------------------------------
SUB shoot
   sok = 1
   FOR ki = 0, nsc - 1, 1
      ksc = VEC1(vsc + ki)
      kpy = ksc * srho * COSI(sthe)
      kpx = ksc * srho * SINE(sthe)
      kfr = ksc * shh
      khy = kfr
      IF (fangle == 1) THEN khy = ATAN(kfr * hmax) / amax

      RAYTRACE 0, khy, kpx, kpy, wl
      IF (RAYE() != 0)
         sok = 0
      ELSE
         kdz = gpfd - RAYZ(nsm)
         kyy = RAYY(nsm) + kdz * RAYM(nsm) / RAYN(nsm)
         kxx = RAYX(nsm) + kdz * RAYL(nsm) / RAYN(nsm)
         VEC1(vey + ki) = fsgn * (kyy - hpmax * kfr)
         VEC1(vez + ki) = fsgn * kxx
      ENDIF
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! fitshape - fit the odd polynomial through both sets of landings and read off the
! third-, fifth- and seventh-order coefficients.
!
! Fitted in t = s / s_max and divided back afterwards; see the head of the file for
! why that matters more than it looks.
! ---------------------------------------------------------------------------------
SUB fitshape
   fsrc = vey
   GOSUB fitone
   c3y = VEC1(vfx + 1)
   c5y = VEC1(vfx + 2)
   c7y = VEC1(vfx + 3)

   fsrc = vez
   GOSUB fitone
   c3z = VEC1(vfx + 1)
   c5z = VEC1(vfx + 2)
   c7z = VEC1(vfx + 3)
RETURN

! fitone - least squares of the data at fsrc onto the eight odd powers, leaving the
! coefficients in VEC1(vfx..) already divided back into the s variable.
SUB fitone
   FOR gi = 0, npw * npw - 1, 1
      VEC1(vfw + gi) = VEC1(vfm + gi)
   NEXT
   FOR gi = 0, npw - 1, 1
      gsum = 0
      FOR gj = 0, nsc - 1, 1
         gsum = gsum + POWR(VEC1(vtt + gj), VEC4(1 + gi)) * VEC1(fsrc + gj)
      NEXT
      VEC1(vfb + gi) = gsum
   NEXT

   nunk = npw
   gata = vfw
   gatb = vfb
   GOSUB gsolv1

   FOR gi = 0, npw - 1, 1
      VEC1(vfx + gi) = VEC1(vfb + gi) / POWR(smax, VEC4(1 + gi))
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! gsolv1 - Gaussian elimination with partial pivoting on VEC1. A second copy of
! gsolve because the fit lives in VEC1 and the coefficient systems live in VEC2, and
! ZPL has no way to say "whichever vector".
!
! Every loop whose bound can fall below its start is guarded: ZPL reports an empty
! range as an infinite loop rather than executing it zero times.
! ---------------------------------------------------------------------------------
SUB gsolv1
   FOR hc = 0, nunk - 1, 1
      hp = hc
      hbest = ABSO(VEC1(gata + hc * nunk + hc))
      IF (hc < nunk - 1)
         FOR hr = hc + 1, nunk - 1, 1
            hv = ABSO(VEC1(gata + hr * nunk + hc))
            IF (hv > hbest)
               hbest = hv
               hp = hr
            ENDIF
         NEXT
      ENDIF
      IF (hp != hc)
         FOR hk = 0, nunk - 1, 1
            htmp = VEC1(gata + hc * nunk + hk)
            VEC1(gata + hc * nunk + hk) = VEC1(gata + hp * nunk + hk)
            VEC1(gata + hp * nunk + hk) = htmp
         NEXT
         htmp = VEC1(gatb + hc)
         VEC1(gatb + hc) = VEC1(gatb + hp)
         VEC1(gatb + hp) = htmp
      ENDIF
      hd = VEC1(gata + hc * nunk + hc)
      IF (ABSO(hd) > 1E-300)
         IF (hc < nunk - 1)
            FOR hr = hc + 1, nunk - 1, 1
               hf = VEC1(gata + hr * nunk + hc) / hd
               IF (hf != 0)
                  FOR hk = hc, nunk - 1, 1
                     VEC1(gata + hr * nunk + hk) = VEC1(gata + hr * nunk + hk) - hf * VEC1(gata + hc * nunk + hk)
                  NEXT
                  VEC1(gatb + hr) = VEC1(gatb + hr) - hf * VEC1(gatb + hc)
               ENDIF
            NEXT
         ENDIF
      ENDIF
   NEXT
   FOR hr = nunk - 1, 0, -1
      hs = VEC1(gatb + hr)
      IF (hr < nunk - 1)
         FOR hk = hr + 1, nunk - 1, 1
            hs = hs - VEC1(gata + hr * nunk + hk) * VEC1(gatb + hk)
         NEXT
      ENDIF
      VEC1(gatb + hr) = hs / VEC1(gata + hr * nunk + hr)
   NEXT
RETURN

! ---------------------------------------------------------------------------------
! gsolve - the same, on VEC2, for the coefficient systems.
! ---------------------------------------------------------------------------------
SUB gsolve
   FOR gc = 0, nunk - 1, 1
      gp = gc
      gbest = ABSO(VEC2(gata + gc * nunk + gc))
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
      IF (ABSO(gd) > 1E-300)
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
! mkmod - the model row for the current shape at order mord. This is Robb's Eq. (2)
! with one coefficient set to one at a time, keeping the terms of total degree mord,
! and it is DELIBERATELY the same model FORBES.ZPL uses - see the head of this file.
! The last column of each is distortion, which Robb's spot-size polynomial omits
! because it moves the patch without resizing it, and which a landing ray does carry.
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
! showco - the coefficients, and what they should be held to.
! ---------------------------------------------------------------------------------
SUB showco
   PRINT " "
   PRINT "==================================================================="
   PRINT "Coefficients from real rays, transverse measure"
   PRINT "==================================================================="
   PRINT " "
   FORMAT 5.0 INT
   PRINT "Shapes traced              ", nsh - nbad
   IF (nbad > 0)
      PRINT "Shapes LOST to failed rays ", nbad
      PRINT "   Rays that will not trace are dropped rather than fudged. If many are"
      PRINT "   lost the remaining shapes may not span the coefficients."
   ENDIF
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
   PRINT "WHAT THIS IS FOR"
   PRINT " "
   PRINT "Nothing here is a series. These came from real rays traced by OpticStudio,"
   PRINT "so where they agree with FORBES.ZPL the two agree by routes with nothing"
   PRINT "in common except the definition of the coefficients."
   PRINT " "
   PRINT "That matters in exactly one place. On a system of spheres FORBES can already"
   PRINT "be checked against BUCH7 and against FIFTHORD, which ships with OpticStudio."
   PRINT "On a FIGURED system BUCH7 declines, FIFTHORD reaches B7 and stops, and the"
   PRINT "Seidel analysis reaches the third order and stops - so tau2 to tau20 there"
   PRINT "have nothing else to be held to. THIS IS THAT CHECK."
   PRINT " "
   PRINT "EXPECT A FEW PARTS IN TEN THOUSAND, NOT MACHINE PRECISION. A traced ray"
   PRINT "carries every order at once and the fit has to separate them, so this"
   PRINT "arrives with a fitting residual where a series arrives exactly. A"
   PRINT "disagreement at that level is the fit. A disagreement at per cent level is"
   PRINT "not, and on an aspheric system it would be worth taking seriously, because"
   PRINT "that is the size of the error the aspheric Buchdahl arrangement carries."
RETURN
