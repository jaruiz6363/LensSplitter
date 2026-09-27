# How a split is made

LensSplitter replaces one element - a glass between two surfaces - with two, sharing its power,
and optimises the pair against a merit function while the rest of the lens stays as it was.

## The four faces

The element's front face becomes the first half's front face, and its rear face the second half's
rear face. Each keeps what the designer put there: its conic and aspheric terms, its surface type,
the stop if it carries it, its aperture. Only its curvature changes, and it is the **vertex**
curvature that is set - an r² aspheric term is a change of curvature, and is counted.

The two new faces between the halves are spheres, with air between them, and both halves are the
element's glass (the `glass` command chooses others).

The curvatures of a starting point come from thin-lens relations: for a share of the element's
power φ and a shape factor X = (c₁ + c₂)/(c₁ − c₂), in the media on either side. They are a starting
point only; the optimiser then moves all four freely.

## Thicknesses, and where everything else goes

Each half is as thick as its edge needs at the clear aperture - measured with the real sag of each
face, figuring included - and at least `--min-centre` at the centre. The gap is at least `--min-gap`,
and its edge clearance at least `--min-edge`.

The halves share the element's thickness, plus whatever **length the split borrows** from the air
after the element: starting points are tried borrowing none, half and all of the element's own
thickness. The air after the split is shortened by exactly what the split adds, so **every later
surface stays where it was**. A split that would bring the rear face into the next surface does not
fit, and is not tried.

The clear aperture is the larger of the element's declared apertures and its paraxial beam - the
marginal ray height plus the full-field chief ray height - with a twentieth to spare. No face may be
steeper than a hemisphere over it.

## The focal length

Every starting point has the lens's focal length exactly: the two halves' power is scaled together,
by secant iteration, until it is the original's to a part in 10¹².

The optimiser holds it as a target, deliberately lightly (weight 100). Held hard from the start - the
obvious choice - it made the problem stiff: with the thicknesses free, the optimiser stalled where it
began (predicted spot 0.070 on the Cooke triplet at weight 10⁶ and 10⁴, against 0.013 at 100). So the
result is made exact afterwards, by scaling the four faces together, and a **polish** follows from
there with the focal length held harder (10⁴), then made exact again.

## The optimisation

AberrationCalculator's optimiser - damped least squares with Dilworth's pseudo-second-derivative
damping and exact derivatives through the predicted spot - runs from the best starting points: the
best for each gap and borrowed length, then the best of the rest (six by default), with the four
faces' curvatures free, the merit function, the focal length held, and edge limits on the two halves
and the air on either side of them. Edge limits cost nothing while they are met.

The thicknesses are searched, by the starting points, rather than optimised. Letting the optimiser
change them too (`--vary-thickness`, their sum held so later surfaces stay put) converged to the best
split on the Cooke triplet at one setting of the weights and stalled at five others, where the
curvatures alone converged every time.

The element's figuring - its conic and aspheric terms - is never a variable: it stays as the
designer made it.

## The image

The split moves the paraxial focus. The image plane moves with it, keeping whatever defocus the
original lens had: zero for a lens imaged at its paraxial focus.

## Choosing the element

The analysis attributes the predicted spot to surfaces (AberrationCalculator's contribution analysis:
the mean-square spot is a quadratic form in the coefficients, and each surface's contribution to
every coefficient gives it a share, cross terms split evenly). An element's share is its two
surfaces'. The recommended element is the splittable one with the largest positive share - the one
making the spot biggest. A negative share is an element correcting the others, which splitting would
weaken.

## Choosing the glasses

Every ordered pair of candidate glasses is screened - a small set of starting points, focal length
exact, scored unoptimised by the merit function - in parallel. The best-screened pairs are split and
optimised in full, and ranked by the merit function, which by default includes the axial and lateral
colour that a choice of glass mostly moves.
