! ROBB - the RMS spot radius predicted from Buchdahl/Rimmer aberration coefficients,
! with no rays traced. Robb's analytic merit function.
!
! Written by Javier Ruiz with Claude Code, September 2026.
!
! Robb, P. N., "Analytic merit function based on Buchdahl's aberration coefficients,"
! J. Opt. Soc. Am. 66(10), 1037-1041 (1976). His Eq. (2) writes where a ray meets the
! Gaussian image plane as polynomials in the pupil radius rho, the pupil azimuth theta
! and the field H; his Eq. (4) is the variance of that over the pupil.
!
! RUN THIS MACRO DIRECTLY. It calls BUCH7.ZPL to get the coefficients it needs, so
! BUCH7's own output appears first and the spot table follows it. Both files are
! runnable on their own: BUCH7 alone prints the coefficients, this one prints the
! coefficients and then what they imply about the spot.
!
! The call goes in one direction only. BUCH7 publishes its totals into the call
! buffer and does not call anything; this macro calls BUCH7 and reads them back.
! Were both to call each other the pair would recurse until OpticStudio gave up.
!
! WHY THE SPOT AND NOT JUST THE COEFFICIENTS
!
!   Thirty-four coefficients are hard to read as a statement about image quality. One
!   RMS radius is easy to read and throws away most of what they say. Printing both,
!   truncated four ways plus ninth-order spherical (five columns, below), says
!   something neither gives alone: how much of the spot each
!   ORDER accounts for, and therefore whether the design is limited by aberration the
!   third order already describes or by something only the seventh reaches.
!
! HOW IT WORKS, WHICH IS WHY THERE IS NO TABLE OF MAGIC CONSTANTS HERE
!
!   Every term of eps_y and eps_z has the form  coefficient * rho^a * H^b * f(theta),
!   so the pupil average of a product of two terms separates completely:
!
!     <eps_i eps_j> = c_i c_j * <f_i f_j>_theta * H^(b_i + b_j) * 2/(a_i + a_j + 2)
!
!   The radial factor is the average of rho^(a_i+a_j) over the unit disc with weight
!   2 rho drho, in closed form. The theta factor takes one of eleven values, because
!   only eleven of the eighty-one pairings of the nine theta functions survive a full
!   turn; the rest average to zero. So the whole double sum is elementary, and the
!   pupil is not sampled at all. There are no rings and no spokes and no convergence
!   to worry about: the only approximation is the truncation of the series itself.
!
! FIVE COLUMNS, AND THE FIFTH IS NOT LIKE THE OTHER FOUR
!
!   Columns one to four are nested truncations of ONE series - third, third plus fifth,
!   plus B7, full seventh. The fifth adds ninth-order SPHERICAL alone, which COMPLETES
!   the ninth order on axis, where every other term of that order carries a power of the
!   field and vanishes, and is a fragment of it everywhere else. It is printed apart for
!   that reason rather than folded into the fourth; see the note printed above the table.
!
! REFERENCED TO THE CENTROID, as Robb specifies - the variance about the mean
! intersection, not about the chief ray. That is what the mean subtraction near the
! end is for. The mean of eps_z is zero because every one of its terms is odd in
! theta, so only eps_y needs it, and within eps_y only the terms whose theta function
! is the constant survive the average.
!
! DISTORTION IS DELIBERATELY ABSENT. E, E5 and tau20 displace the whole patch without
! changing its size, so they cannot appear in a spot radius, and Robb's corresponding
! terms vanish identically. Thirty-four coefficients enter here, not thirty-seven.
!
! WHAT IT IS BLIND TO, WHICH ITS AUTHOR SAID FIRST
!
!   The reference is the GAUSSIAN image plane, so this is not the spot at best focus
!   and moving the image plane does not change it. Robb says so in his Conclusions:
!   the image plane "ceases to become a design variable", optimising the last thickness
!   "will not have the slightest effect on the solution", and focus must be adjusted
!   afterwards by the method of Sands (1973). A design whose spot is dominated by
!   defocus will read better here than it deserves.
!
! HOW GOOD THE PREDICTION IS, MEASURED RATHER THAN ASSERTED
!
!   docs/spot-prediction.md in this repository compares it against traced rays on five
!   lenses at both conjugates. On a Cooke triplet at infinite conjugate the full
!   seventh order is within 0.7 per cent on axis and about 2 per cent out to nine
!   tenths of the field, and falls apart at the very edge - minus 13 per cent at H = 1.
!   The third order alone is out by a quarter on axis and by more than a factor of two
!   at the edge, which is why the truncations are printed side by side rather than one.
!
! THE ZPL TRAPS: see macros/README.md. Every variable here is distinct ignoring case
! and none collides with a built-in function name - in particular the field-count is
! nsteps and not nfld, because NFLD() is a function.
! ---------------------------------------------------------------------------------

! ---------------------------------------------------------------------------------
! Get the coefficients. BUCH7 does the whole of the work - the paraxial basis, the
! third and fifth orders, Buchdahl's Table I and the twenty tau - and publishes its
! totals into the call buffer in its stage E. Its output is copied into this window
! when it returns, so what the reader sees is BUCH7's tables followed by the spot.
!
! This call comes BEFORE this macro touches any vector. BUCH7 uses VEC1 to VEC4
! heavily and sizes them to its own needs, so the tables built below are built after
! it has finished with them rather than around it. The call buffer is untouched by
! any of that, which is why the coefficients survive where vector contents would not.
! ---------------------------------------------------------------------------------

CALLMACRO BUCH7.ZPL

! ---------------------------------------------------------------------------------
! Settings. THESE SIT BELOW THE CALL ON PURPOSE. ZPL does not document whether a child
! macro shares the parent's ordinary variables - the call buffer exists precisely so
! that data can be passed without relying on it - and BUCH7 happens to use the names
! eps, dbg and kk itself. Assigning them after BUCH7 has finished makes the question
! moot instead of resting on an answer that is nowhere written down.
! ---------------------------------------------------------------------------------

eps = 1E-10

! Set to 1 to also integrate Robb's Eq. (2) numerically over the pupil and print the
! two side by side. They must agree; the analytic route is exact and the numerical one
! carries sampling error, so a disagreement beyond about 1E-4 relative means a term is
! wrong rather than that the grid is coarse. It is slow, which is why it is off.
dbg = 0

! Field steps printed, from 0 to 1 inclusive.
nsteps = 10

SETVECSIZE 300

! ---------------------------------------------------------------------------------
! The handshake. BUCH7 writes slot 45 last of all, so it is set only if BUCH7 got as
! far as having a full set of coefficients to write. If it declined - an aspheric
! surface, an afocal system, a chief ray carrying no field - it will have said so in
! its own output above, and the slot is still zero.
!
! This is worth distinguishing from coefficients that are genuinely zero, which is
! what a well-corrected system would legitimately produce.
! ---------------------------------------------------------------------------------

magic = CALD(45)
IF (ABSO(magic - 761976) > 0.5)
   PRINT " "
   PRINT "ROBB got no coefficients from BUCH7, so there is no spot to report."
   PRINT " "
   PRINT "BUCH7 ran but did not reach the end. Its own output above says why - it"
   PRINT "declines an aspheric surface, an afocal system, and a chief ray carrying"
   PRINT "no field, and each of those stops it before the coefficients are complete."
   PRINT " "
   PRINT "This is not the same as a lens whose coefficients are all zero, which is"
   PRINT "why the handshake exists rather than reading the coefficients directly."
   GOTO 999
ENDIF

! ---------------------------------------------------------------------------------
! The buffer, which is the interface between BUCH7 and this macro. BUCH7's stage E
! writes it; everything here reads it.
!
!   1-4    B F C Pi          third order, distortion E excluded
!   5-15   B5 F1 F2 M1 M2 M3 N1 N2 N3 C5 Pi5     fifth, E5 excluded
!   16     B7, which is Robb's tau1
!   17-34  tau2 to tau19,    seventh, tau20 excluded
!   45     761976, the handshake
!   46     1 if the seventh order is present, 0 if only third and fifth were computed
!   47     B9, ninth-order spherical, in transverse measure
!   48     1 if the ninth order is present
!
! All in TRANSVERSE measure and in lens units, which is what BUCH7 prints as its
! system totals. Unconverted coefficients would give a number in no units at all.
! ---------------------------------------------------------------------------------

FOR kk = 1, 34, 1
   VEC4(kk) = CALD(kk)
NEXT
has7 = CALD(46)

! The ninth order arrives in its own slot rather than as coefficient 35 of a set,
! because it is not part of the same truncation: see the +B9 column below for what it
! is and is not. A BUCH7 that never reached stage F leaves slot 48 clear, and the
! coefficient is then zeroed so the extra column simply repeats the full seventh.
has9 = CALD(48)
VEC4(35) = CALD(47)
IF (ABSO(has9 - 1) > 0.5) THEN VEC4(35) = 0

! ---------------------------------------------------------------------------------
! WHERE THE IMAGE PLANE ACTUALLY IS.
!
! This table is computed at the Gaussian image plane and nowhere else. A lens file
! whose image surface sits somewhere else - which is most of them, because that is
! where an optimiser puts it - will show a smaller spot in a spot diagram than this
! macro reports, and the difference is not an error in either.
!
! Saying so in prose is not enough: it is the one misreading this output invites, and
! it is easy to skip past. So the distance is measured and the blur it accounts for
! is put in lens units beside the table.
!
! The paraxial focus is at -y/u from the last surface, from the marginal ray. A ray
! at normalised pupil height rho misses that plane by defocus * u * rho, and the
! average of rho^2 over the pupil is 1/2, so a PERFECT lens at a plane this far off
! would show an RMS radius of |defocus * u| / sqrt(2). That is a real number to
! compare the table against rather than a caution to bear in mind.
! ---------------------------------------------------------------------------------

dfmode = PMOD()
PARAXIAL ON
dfnsm = NSUR() - 1
RAYTRACE 0, 0, 0, 1, PWAV()
dfy = RAYY(dfnsm)
dfu = RAYM(dfnsm) / RAYN(dfnsm)
IF (ABSO(dfmode) < eps) THEN PARAXIAL OFF

dfpar = 0
dfact = THIC(dfnsm)
dfoc = 0
dfblur = 0
IF (ABSO(dfu) > eps)
   dfpar = -dfy / dfu
   dfoc = dfpar - dfact
   dfblur = ABSO(dfoc * dfu) / SQRT(2)
ENDIF

! ---------------------------------------------------------------------------------
! The theta averages. Nine functions appear in Eq. (2):
!
!   1 one    2 cos(t)   3 cos(2t)   4 cos^3(t)
!   5 sin(t) 6 sin(2t)  7 cos^2(t)sin(t)   8 cos(4t)   9 sin(4t)
!
! Of the eighty-one pairings only these eleven have a non-zero average over a full
! turn: everything odd in theta vanishes, the cosine family never pairs with the sine
! family, and the constant pairs only with itself. The values are exact rationals.
! ---------------------------------------------------------------------------------

FOR kk = 1, 81, 1
   VEC3(kk) = 0
NEXT

VEC3(1) = 1
VEC3(11) = 0.5
VEC3(13) = 0.375
VEC3(29) = 0.375
VEC3(21) = 0.5
VEC3(31) = 0.3125
VEC3(41) = 0.5
VEC3(43) = 0.125
VEC3(59) = 0.125
VEC3(51) = 0.5
VEC3(61) = 0.0625
VEC3(71) = 0.5
VEC3(81) = 0.5

! ---------------------------------------------------------------------------------
! Robb Eq. (2), the meridional component eps_y. Twenty-six terms - twenty-five of
! Robb's own, and B9 appended, which is not his.
!
!   VEC1(k)         the coefficient combination, evaluated
!   VEC1(50 + k)    the power of rho
!   VEC1(100 + k)   the power of H
!   VEC1(150 + k)   which theta function, indexed as above
!
! The bridge from Robb's own notation to the Rimmer names this program reports is
! sigma1 = B, sigma2 = F, sigma3 = C, sigma4 = Pi; mu1 = B5, mu2 = F1, mu3 = F2,
! mu4 = M1+M2, mu5 = M2, mu6 = M3, mu7 = N1+N2/2, mu8 = N2/2, mu9 = N3,
! mu10 = 5*C5+Pi5, mu11 = C5+Pi5; tau1 = B7. The seventh-order lines are Buchdahl's
! own, J. Opt. Soc. Am. 48, 747 (1958) p.753, which is where Robb took them from.
! ---------------------------------------------------------------------------------

kk = 0

kk = kk + 1
VEC1(kk) = VEC4(1)
VEC1(50 + kk) = 3
VEC1(100 + kk) = 0
VEC1(150 + kk) = 2

kk = kk + 1
VEC1(kk) = 2 * VEC4(2)
VEC1(50 + kk) = 2
VEC1(100 + kk) = 1
VEC1(150 + kk) = 1

kk = kk + 1
VEC1(kk) = VEC4(2)
VEC1(50 + kk) = 2
VEC1(100 + kk) = 1
VEC1(150 + kk) = 3

kk = kk + 1
VEC1(kk) = 3 * VEC4(3) + VEC4(4)
VEC1(50 + kk) = 1
VEC1(100 + kk) = 2
VEC1(150 + kk) = 2

kk = kk + 1
VEC1(kk) = VEC4(5)
VEC1(50 + kk) = 5
VEC1(100 + kk) = 0
VEC1(150 + kk) = 2

kk = kk + 1
VEC1(kk) = VEC4(6)
VEC1(50 + kk) = 4
VEC1(100 + kk) = 1
VEC1(150 + kk) = 1

kk = kk + 1
VEC1(kk) = VEC4(7)
VEC1(50 + kk) = 4
VEC1(100 + kk) = 1
VEC1(150 + kk) = 3

kk = kk + 1
VEC1(kk) = VEC4(8) + VEC4(9)
VEC1(50 + kk) = 3
VEC1(100 + kk) = 2
VEC1(150 + kk) = 2

kk = kk + 1
VEC1(kk) = VEC4(10)
VEC1(50 + kk) = 3
VEC1(100 + kk) = 2
VEC1(150 + kk) = 4

kk = kk + 1
VEC1(kk) = VEC4(11) + 0.5 * VEC4(12)
VEC1(50 + kk) = 2
VEC1(100 + kk) = 3
VEC1(150 + kk) = 1

kk = kk + 1
VEC1(kk) = 0.5 * VEC4(12)
VEC1(50 + kk) = 2
VEC1(100 + kk) = 3
VEC1(150 + kk) = 3

kk = kk + 1
VEC1(kk) = 5 * VEC4(14) + VEC4(15)
VEC1(50 + kk) = 1
VEC1(100 + kk) = 4
VEC1(150 + kk) = 2

kk = kk + 1
VEC1(kk) = VEC4(16)
VEC1(50 + kk) = 7
VEC1(100 + kk) = 0
VEC1(150 + kk) = 2

kk = kk + 1
VEC1(kk) = VEC4(17)
VEC1(50 + kk) = 6
VEC1(100 + kk) = 1
VEC1(150 + kk) = 1

kk = kk + 1
VEC1(kk) = VEC4(18)
VEC1(50 + kk) = 6
VEC1(100 + kk) = 1
VEC1(150 + kk) = 3

kk = kk + 1
VEC1(kk) = VEC4(19)
VEC1(50 + kk) = 5
VEC1(100 + kk) = 2
VEC1(150 + kk) = 2

kk = kk + 1
VEC1(kk) = VEC4(21)
VEC1(50 + kk) = 5
VEC1(100 + kk) = 2
VEC1(150 + kk) = 4

kk = kk + 1
VEC1(kk) = VEC4(22)
VEC1(50 + kk) = 4
VEC1(100 + kk) = 3
VEC1(150 + kk) = 1

kk = kk + 1
VEC1(kk) = VEC4(23)
VEC1(50 + kk) = 4
VEC1(100 + kk) = 3
VEC1(150 + kk) = 3

kk = kk + 1
VEC1(kk) = VEC4(25)
VEC1(50 + kk) = 4
VEC1(100 + kk) = 3
VEC1(150 + kk) = 8

kk = kk + 1
VEC1(kk) = VEC4(26)
VEC1(50 + kk) = 3
VEC1(100 + kk) = 4
VEC1(150 + kk) = 2

kk = kk + 1
VEC1(kk) = VEC4(27)
VEC1(50 + kk) = 3
VEC1(100 + kk) = 4
VEC1(150 + kk) = 4

kk = kk + 1
VEC1(kk) = VEC4(30)
VEC1(50 + kk) = 2
VEC1(100 + kk) = 5
VEC1(150 + kk) = 1

kk = kk + 1
VEC1(kk) = VEC4(31)
VEC1(50 + kk) = 2
VEC1(100 + kk) = 5
VEC1(150 + kk) = 3

kk = kk + 1
VEC1(kk) = VEC4(33)
VEC1(50 + kk) = 1
VEC1(100 + kk) = 6
VEC1(150 + kk) = 2

kk = kk + 1
VEC1(kk) = VEC4(35)
VEC1(50 + kk) = 9
VEC1(100 + kk) = 0
VEC1(150 + kk) = 2

ny = kk
IF (ny != 26)
   PRINT "ROBB built the wrong number of eps_y terms; the file has been edited badly."
   GOTO 999
ENDIF

! ---------------------------------------------------------------------------------
! Robb Eq. (2), the sagittal component eps_z. Twenty terms - nineteen of Robb's and
! B9 appended - in VEC2 with the same
! layout. It has fewer terms because the constant theta function does not appear:
! every term of eps_z is odd in theta, which is also why its mean over the pupil is
! zero and why the centroid subtraction below touches only eps_y.
! ---------------------------------------------------------------------------------

kk = 0

kk = kk + 1
VEC2(kk) = VEC4(1)
VEC2(50 + kk) = 3
VEC2(100 + kk) = 0
VEC2(150 + kk) = 5

kk = kk + 1
VEC2(kk) = VEC4(2)
VEC2(50 + kk) = 2
VEC2(100 + kk) = 1
VEC2(150 + kk) = 6

kk = kk + 1
VEC2(kk) = VEC4(3) + VEC4(4)
VEC2(50 + kk) = 1
VEC2(100 + kk) = 2
VEC2(150 + kk) = 5

kk = kk + 1
VEC2(kk) = VEC4(5)
VEC2(50 + kk) = 5
VEC2(100 + kk) = 0
VEC2(150 + kk) = 5

kk = kk + 1
VEC2(kk) = VEC4(7)
VEC2(50 + kk) = 4
VEC2(100 + kk) = 1
VEC2(150 + kk) = 6

kk = kk + 1
VEC2(kk) = VEC4(9)
VEC2(50 + kk) = 3
VEC2(100 + kk) = 2
VEC2(150 + kk) = 5

kk = kk + 1
VEC2(kk) = VEC4(10)
VEC2(50 + kk) = 3
VEC2(100 + kk) = 2
VEC2(150 + kk) = 7

kk = kk + 1
VEC2(kk) = VEC4(13)
VEC2(50 + kk) = 2
VEC2(100 + kk) = 3
VEC2(150 + kk) = 6

kk = kk + 1
VEC2(kk) = VEC4(14) + VEC4(15)
VEC2(50 + kk) = 1
VEC2(100 + kk) = 4
VEC2(150 + kk) = 5

kk = kk + 1
VEC2(kk) = VEC4(16)
VEC2(50 + kk) = 7
VEC2(100 + kk) = 0
VEC2(150 + kk) = 5

kk = kk + 1
VEC2(kk) = VEC4(18)
VEC2(50 + kk) = 6
VEC2(100 + kk) = 1
VEC2(150 + kk) = 6

kk = kk + 1
VEC2(kk) = VEC4(20)
VEC2(50 + kk) = 5
VEC2(100 + kk) = 2
VEC2(150 + kk) = 5

kk = kk + 1
VEC2(kk) = VEC4(21)
VEC2(50 + kk) = 5
VEC2(100 + kk) = 2
VEC2(150 + kk) = 7

kk = kk + 1
VEC2(kk) = VEC4(24)
VEC2(50 + kk) = 4
VEC2(100 + kk) = 3
VEC2(150 + kk) = 6

kk = kk + 1
VEC2(kk) = VEC4(25)
VEC2(50 + kk) = 4
VEC2(100 + kk) = 3
VEC2(150 + kk) = 9

kk = kk + 1
VEC2(kk) = VEC4(28)
VEC2(50 + kk) = 3
VEC2(100 + kk) = 4
VEC2(150 + kk) = 5

kk = kk + 1
VEC2(kk) = VEC4(29)
VEC2(50 + kk) = 3
VEC2(100 + kk) = 4
VEC2(150 + kk) = 7

kk = kk + 1
VEC2(kk) = VEC4(32)
VEC2(50 + kk) = 2
VEC2(100 + kk) = 5
VEC2(150 + kk) = 6

kk = kk + 1
VEC2(kk) = VEC4(34)
VEC2(50 + kk) = 1
VEC2(100 + kk) = 6
VEC2(150 + kk) = 5

kk = kk + 1
VEC2(kk) = VEC4(35)
VEC2(50 + kk) = 9
VEC2(100 + kk) = 0
VEC2(150 + kk) = 5

nz = kk
IF (nz != 20)
   PRINT "ROBB built the wrong number of eps_z terms; the file has been edited badly."
   GOTO 999
ENDIF

! ---------------------------------------------------------------------------------
! Which column each term first appears in.
!
!   1   3rd        the five primary coefficients
!   2   3rd+5th    and the twelve secondary
!   3   +B7 only   and seventh-order SPHERICAL aberration alone, which is Robb's tau1
!   4   full 7th   and the other eighteen tau
!
! THE THIRD COLUMN IS THERE BECAUSE B7 IS NOT NEW. FIFTHORD prints it, and so does
! BUCH7's own fifth-order working, so it was available long before the Table I
! arrangement existed. Separating it says what the eighteen tau that were NOT already
! available actually buy - which is the question the seventh order exists to answer.
!
! On the Cooke triplet the answer is stark and is in docs/spot-prediction.md: on axis
! B7 is the whole of the seventh order and the other eighteen add nothing, because on
! axis there is no field. At nine tenths of the field B7 moves the error from +14.5
! per cent to +13.9, near enough to nothing, and the other eighteen take it to +0.4.
!
! A term's order is its total degree a + b. B7 is picked out as the term of degree
! seven whose degree is entirely in the aperture, a = 7, which is what seventh-order
! spherical aberration means. There is exactly one such term in each of eps_y and
! eps_z, and that is checked below rather than assumed.
! ---------------------------------------------------------------------------------

nb7y = 0
nb9y = 0
FOR ia = 1, ny, 1
   VEC1(200 + ia) = 4
   IF ((VEC1(50 + ia) + VEC1(100 + ia)) <= 3) THEN VEC1(200 + ia) = 1
   IF ((VEC1(50 + ia) + VEC1(100 + ia)) == 5) THEN VEC1(200 + ia) = 2
   IF (VEC1(50 + ia) == 7) THEN VEC1(200 + ia) = 3
   IF (VEC1(50 + ia) == 9) THEN VEC1(200 + ia) = 5
   IF (VEC1(200 + ia) == 3) THEN nb7y = nb7y + 1
   IF (VEC1(200 + ia) == 5) THEN nb9y = nb9y + 1
NEXT

nb7z = 0
nb9z = 0
FOR ia = 1, nz, 1
   VEC2(200 + ia) = 4
   IF ((VEC2(50 + ia) + VEC2(100 + ia)) <= 3) THEN VEC2(200 + ia) = 1
   IF ((VEC2(50 + ia) + VEC2(100 + ia)) == 5) THEN VEC2(200 + ia) = 2
   IF (VEC2(50 + ia) == 7) THEN VEC2(200 + ia) = 3
   IF (VEC2(50 + ia) == 9) THEN VEC2(200 + ia) = 5
   IF (VEC2(200 + ia) == 3) THEN nb7z = nb7z + 1
   IF (VEC2(200 + ia) == 5) THEN nb9z = nb9z + 1
NEXT

IF ((nb9y != 1) | (nb9z != 1))
   PRINT "ROBB cannot identify ninth-order spherical aberration uniquely; there should"
   PRINT "be exactly one term of aperture degree nine in each of eps_y and eps_z."
   GOTO 999
ENDIF

IF ((nb7y != 1) | (nb7z != 1))
   PRINT "ROBB cannot identify seventh-order spherical aberration uniquely; there"
   PRINT "should be exactly one term of aperture degree seven in each of eps_y and"
   PRINT "eps_z, and the tables above no longer have that shape."
   GOTO 999
ENDIF

! ---------------------------------------------------------------------------------
! Report
! ---------------------------------------------------------------------------------

PRINT " "
PRINT "==================================================================="
PRINT "ROBB - RMS spot radius from the coefficients, no rays traced"
PRINT "Robb, J. Opt. Soc. Am. 66, 1037 (1976), Eq. (2) and Eq. (4)"
PRINT "==================================================================="
PRINT " "
PRINT "Coefficients received from BUCH7 above, transverse measure, lens units."
PRINT "They must match the system totals BUCH7 printed for itself:"
FORMAT 15.6 EXP
PRINT "   B  ", VEC4(1), "   F  ", VEC4(2), "   C  ", VEC4(3)
PRINT "   Pi ", VEC4(4)
PRINT "   B5 ", VEC4(5), "   F1 ", VEC4(6), "   F2 ", VEC4(7)
PRINT "   M1 ", VEC4(8), "   M2 ", VEC4(9), "   M3 ", VEC4(10)
PRINT "   N1 ", VEC4(11), "   N2 ", VEC4(12), "   N3 ", VEC4(13)
PRINT "   C5 ", VEC4(14), "   Pi5", VEC4(15)
PRINT "   B7 ", VEC4(16)
PRINT "   B9 ", VEC4(35)
IF (has9 > 0.5)
   PRINT " "
   PRINT "THE +B9 COLUMN IS NOT A NINTH-ORDER SPOT, AND MUST NOT BE READ AS ONE."
   PRINT " "
   PRINT "ON AXIS it is exact and it completes the order: every other ninth-order term"
   PRINT "carries a power of the field, so at H = 0 spherical IS the whole ninth order,"
   PRINT "and the difference between the last two columns there is real."
   PRINT " "
   PRINT "OFF AXIS it is a PARTIAL ninth order - some twenty other terms of that order"
   PRINT "are absent - and a partial order is not guaranteed to be an improvement. Terms"
   PRINT "of one order routinely cancel against each other, so adding one of them can"
   PRINT "move the answer either way. This repository has measured that even COMPLETE"
   PRINT "orders are not monotone: on the worst design in docs/spot-prediction.md the"
   PRINT "seventh-order truncation misses by 134 um, the ninth by 11, and the eleventh"
   PRINT "by 11.8 - worse than the ninth."
   PRINT " "
   PRINT "Its conversion to transverse measure WAS the one number BUCH7 published without"
   PRINT "a second route. It now has one - see stage F - and it held to six figures."
ELSE
   PRINT " "
   PRINT "NOTE: BUCH7 reported no ninth order, so B9 is zero and the '+B9' column below"
   PRINT "is IDENTICAL to 'full 7th'. Two columns reading the same is the symptom."
ENDIF
IF (has7 > 0.5)
   PRINT "   tau2 to tau19 also received."
ELSE
   PRINT " "
   PRINT "NOTE: BUCH7 reported no seventh order beyond B7, so tau2 to tau19 are zero"
   PRINT "and the 'full 7th' column below is IDENTICAL to '+B7 only'. Two columns"
   PRINT "reading the same is the symptom, and it means the last column is not the"
   PRINT "full seventh order and must not be read as one. On a system with figured"
   PRINT "surfaces that is expected - BUCH7's Table I arrangement is for spheres."
ENDIF
PRINT " "
PRINT "Distortion is absent by construction: E, E5 and tau20 move the patch without"
PRINT "resizing it, so they cannot enter a spot radius. Thirty-four coefficients are"
PRINT "used here, not thirty-seven."
PRINT " "
! ---------------------------------------------------------------------------------
! The image plane, reported before the table rather than after it, because it decides
! what the table can honestly be compared with.
! ---------------------------------------------------------------------------------

FORMAT 12.6
PRINT "Image plane:"
PRINT "   paraxial focus, from the last surface  ", dfpar
PRINT "   where this lens file puts it           ", dfact
PRINT "   defocus                                ", dfoc
FORMAT 15.6 EXP
PRINT "   RMS radius a PERFECT lens would show at that plane ", dfblur
PRINT " "

IF (ABSO(dfoc) > 1E-4 * (1 + ABSO(dfpar)))
   PRINT "*** WARNING - THIS LENS IS NOT AT PARAXIAL FOCUS. ***"
   PRINT " "
   PRINT "The table below is computed at the PARAXIAL focus, because that is the plane"
   PRINT "Robb's coefficients refer to, and this lens file is not there. So the table"
   PRINT "INCLUDES the defocus above and a spot diagram at the drawn image plane does"
   PRINT "NOT, and the two are not comparable. Expect this table to read the larger of"
   PRINT "the two, with the figure above as the SCALE of the difference rather than as"
   PRINT "a correction to apply: defocus and aberration vary together across the pupil,"
   PRINT "so they do not simply add in quadrature."
   PRINT " "
   PRINT "To compare them properly, set the last thickness to the paraxial focus"
   PRINT "distance above and take the spot diagram again. Both should then agree to a"
   PRINT "few per cent - on the OpticStudio Double Gauss 28 degree sample, which sits"
   PRINT "0.1834 from paraxial focus, they come to within 0.4 per cent on axis."
   PRINT " "
   PRINT "Neither number is wrong. Robb's spot is blind to defocus by construction -"
   PRINT "he says so himself, and adds that focus must be adjusted afterwards by the"
   PRINT "method of Sands (1973) - so it cannot be used to choose an image plane."
ELSE
   PRINT "This lens is at paraxial focus, so the table below is directly comparable"
   PRINT "with a spot diagram taken at the drawn image plane."
ENDIF
PRINT " "

PRINT "RMS spot RADIUS in lens units, referenced to the CENTROID, at the GAUSSIAN"
PRINT "image plane."
PRINT " "
PRINT "     H            3rd        3rd+5th       +B7 only       full 7th            +B9"

! ---------------------------------------------------------------------------------
! The double sum. Terms are selected by total degree a + b, which is 3 for the third
! order, 5 for the fifth and 7 for the seventh, so one cut-off serves all three
! columns and the three are the same arithmetic on nested subsets.
! ---------------------------------------------------------------------------------

hstep = 1 / nsteps
FOR istep = 0, nsteps, 1
   hh = istep * hstep

   FOR icol = 1, 5, 1
      acc = 0

      ! eps_y against itself
      FOR ia = 1, ny, 1
         IF (VEC1(200 + ia) <= icol)
            FOR ib = 1, ny, 1
               IF (VEC1(200 + ib) <= icol)
                  thv = VEC3((VEC1(150 + ia) - 1) * 9 + VEC1(150 + ib))
                  IF (ABSO(thv) > eps)
                     radf = 2 / (VEC1(50 + ia) + VEC1(50 + ib) + 2)
                     hpw = VEC1(100 + ia) + VEC1(100 + ib)
                     hfac = 1
                     IF (hpw > 0) THEN hfac = POWR(hh, hpw)
                     acc = acc + VEC1(ia) * VEC1(ib) * thv * radf * hfac
                  ENDIF
               ENDIF
            NEXT
         ENDIF
      NEXT

      ! eps_z against itself
      FOR ia = 1, nz, 1
         IF (VEC2(200 + ia) <= icol)
            FOR ib = 1, nz, 1
               IF (VEC2(200 + ib) <= icol)
                  thv = VEC3((VEC2(150 + ia) - 1) * 9 + VEC2(150 + ib))
                  IF (ABSO(thv) > eps)
                     radf = 2 / (VEC2(50 + ia) + VEC2(50 + ib) + 2)
                     hpw = VEC2(100 + ia) + VEC2(100 + ib)
                     hfac = 1
                     IF (hpw > 0) THEN hfac = POWR(hh, hpw)
                     acc = acc + VEC2(ia) * VEC2(ib) * thv * radf * hfac
                  ENDIF
               ENDIF
            NEXT
         ENDIF
      NEXT

      ! Centroid reference. Only eps_y terms whose theta function is the constant
      ! survive a pupil average, and the mean of eps_z is zero by symmetry.
      mnc = 0
      FOR ia = 1, ny, 1
         IF (VEC1(200 + ia) <= icol)
            IF (VEC1(150 + ia) == 1)
               hpw = VEC1(100 + ia)
               hfac = 1
               IF (hpw > 0) THEN hfac = POWR(hh, hpw)
               mnc = mnc + VEC1(ia) * (2 / (VEC1(50 + ia) + 2)) * hfac
            ENDIF
         ENDIF
      NEXT

      msq = acc - mnc * mnc

      ! The series is a truncation, so on a well-corrected design the surviving terms
      ! can cancel to a very small negative number. That is dust, not a quantity.
      rmsv = 0
      IF (msq > 0) THEN rmsv = SQRT(msq)
      VEC3(120 + icol) = rmsv
   NEXT

   ! Held and printed in one statement rather than appended to an open print line
   ! across the loop above.
   FORMAT 6.2
   PRINT hh,
   FORMAT 15.6 EXP
   PRINT VEC3(121), VEC3(122), VEC3(123), VEC3(124), VEC3(125)
NEXT

PRINT " "
PRINT "The four columns are the same sum over NESTED subsets of the same terms, so the"
PRINT "difference between two adjacent ones is exactly what that step buys."
PRINT " "
PRINT "The one to read is '+B7 only' against 'full 7th'. B7 was available before any of"
PRINT "the Table I work - FIFTHORD prints it, and so does BUCH7's fifth-order working -"
PRINT "so that gap is what the eighteen tau add that nothing else could give you. On a"
PRINT "Cooke triplet it is nothing on axis, where B7 is the whole of the seventh order,"
PRINT "and a factor of thirty-five at nine tenths of the field."
PRINT " "
PRINT "docs/spot-prediction.md measures all four against traced rays on five lenses."

! ---------------------------------------------------------------------------------
! The optional second route. Robb's Eq. (2) is evaluated ray by ray on an equal-area
! pupil grid and the variance taken numerically. It must agree with the analytic sum
! above; it is here because a table of powers and indices transcribed by hand is
! exactly the sort of thing that can be wrong in a way that still looks plausible.
!
! The kth ring sits at rho = sqrt((k - 1/2)/n), the equal-area midpoint rather than
! the outer edge. Sampling at the edge weights the pupil outward and converges from
! above; docs/spot-prediction.md has the numbers.
! ---------------------------------------------------------------------------------

IF (dbg == 1)
   PRINT " "
   PRINT "Second route: Eq. (2) integrated numerically over the pupil, 24 rings by 48"
   PRINT "spokes. Sampling error is around 1E-5 relative, so agreement to about four"
   PRINT "figures is the most this can show, and is enough to catch a wrong term."
   PRINT " "
   PRINT "     H       analytic      numerical"

   nring = 24
   nspoke = 48
   twopi = 4 * ACOS(0)

   FOR istep = 0, nsteps, 1
      hh = istep * hstep

      sy = 0
      syy = 0
      szz = 0
      FOR iring = 1, nring, 1
         rho = SQRT((iring - 0.5) / nring)
         FOR ispoke = 1, nspoke, 1
            th = twopi * (ispoke - 1) / nspoke

            ! the nine theta functions at this azimuth
            VEC3(101) = 1
            VEC3(102) = COSI(th)
            VEC3(103) = COSI(2 * th)
            VEC3(104) = COSI(th) * COSI(th) * COSI(th)
            VEC3(105) = SINE(th)
            VEC3(106) = SINE(2 * th)
            VEC3(107) = COSI(th) * COSI(th) * SINE(th)
            VEC3(108) = COSI(4 * th)
            VEC3(109) = SINE(4 * th)

            ey = 0
            FOR ia = 1, ny, 1
               hfac = 1
               IF (VEC1(100 + ia) > 0) THEN hfac = POWR(hh, VEC1(100 + ia))
               ey = ey + VEC1(ia) * POWR(rho, VEC1(50 + ia)) * hfac * VEC3(100 + VEC1(150 + ia))
            NEXT
            ez = 0
            FOR ia = 1, nz, 1
               hfac = 1
               IF (VEC2(100 + ia) > 0) THEN hfac = POWR(hh, VEC2(100 + ia))
               ez = ez + VEC2(ia) * POWR(rho, VEC2(50 + ia)) * hfac * VEC3(100 + VEC2(150 + ia))
            NEXT

            sy = sy + ey
            syy = syy + ey * ey
            szz = szz + ez * ez
         NEXT
      NEXT

      npt = nring * nspoke
      msqn = (syy + szz) / npt - (sy / npt) * (sy / npt)
      rmsn = 0
      IF (msqn > 0) THEN rmsn = SQRT(msqn)

      ! the analytic value again, full seventh order
      acc = 0
      FOR ia = 1, ny, 1
         FOR ib = 1, ny, 1
            thv = VEC3((VEC1(150 + ia) - 1) * 9 + VEC1(150 + ib))
            IF (ABSO(thv) > eps)
               hpw = VEC1(100 + ia) + VEC1(100 + ib)
               hfac = 1
               IF (hpw > 0) THEN hfac = POWR(hh, hpw)
               acc = acc + VEC1(ia) * VEC1(ib) * thv * (2 / (VEC1(50 + ia) + VEC1(50 + ib) + 2)) * hfac
            ENDIF
         NEXT
      NEXT
      FOR ia = 1, nz, 1
         FOR ib = 1, nz, 1
            thv = VEC3((VEC2(150 + ia) - 1) * 9 + VEC2(150 + ib))
            IF (ABSO(thv) > eps)
               hpw = VEC2(100 + ia) + VEC2(100 + ib)
               hfac = 1
               IF (hpw > 0) THEN hfac = POWR(hh, hpw)
               acc = acc + VEC2(ia) * VEC2(ib) * thv * (2 / (VEC2(50 + ia) + VEC2(50 + ib) + 2)) * hfac
            ENDIF
         NEXT
      NEXT
      mnc = 0
      FOR ia = 1, ny, 1
         IF (VEC1(150 + ia) == 1)
            hfac = 1
            IF (VEC1(100 + ia) > 0) THEN hfac = POWR(hh, VEC1(100 + ia))
            mnc = mnc + VEC1(ia) * (2 / (VEC1(50 + ia) + 2)) * hfac
         ENDIF
      NEXT
      msq = acc - mnc * mnc
      rmsv = 0
      IF (msq > 0) THEN rmsv = SQRT(msq)

      FORMAT 6.2
      PRINT hh,
      FORMAT 15.6 EXP
      PRINT rmsv, rmsn
   NEXT

   PRINT " "
   PRINT "The theta lookup table was overwritten above index 100 by this check, so if"
   PRINT "anything is added after this block it must rebuild it."
ENDIF

LABEL 999
END
