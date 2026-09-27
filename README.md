# LensSplitter

Splits one lens element into two, sharing its power, and optimises the pair.

A strongly curved element is often where a lens's aberration comes from. Splitting it into two
weaker elements - the same total power, each bent less - usually lets the design do better. That
is the idea; LensSplitter finds the best such split, holds the lens's focal length exactly, and
writes the result back out in the format you gave it.

It is built on [AberrationCalculator](https://github.com/jaruiz6363/AberrationCalculator), which
supplies the optics: the lens model, readers and writers for every format, glass, Seidel sums,
Buchdahl's aberration coefficients through seventh order, Robb's predicted spot size, and a
damped-least-squares optimiser with exact derivatives. LensSplitter adds what is its own: choosing
an element, building its split, and searching for the best one.

## What is new in 2.0

- **Every format.** Reads and writes ZEMAX `.zmx`, CODE V `.seq`, OSLO `.len`, OPTALIX `.otx`,
  Optiland `.json` and LensHH-LT `.lhlt`. (1.x read and wrote `.zmx` and Optiland only.)
- **Aspherics.** Conic and even-asphere surfaces are carried at every order, in the analysis and in
  the optimisation. An element with a figured face can be split: the figuring stays on its outer
  face. (1.x read only the conic, and would not split a figured element.)
- **Robb's predicted spot in the merit function.** The default merit is the RMS spot radius
  predicted from the aberration coefficients through seventh order, over every field and
  wavelength - one number that weighs every aberration by what it does to the image. Any
  coefficient, colour, distortion or ray target can be added in a merit-function file.
- **The focal length is held exactly**, to about ten digits. (1.x allowed it to drift by up to 3 %.)
- **A real optimiser.** The split is optimised by AberrationCalculator's optimiser with exact
  derivatives, from the best of a few hundred starting points, instead of a grid search.
- **Element choice by the spot.** The element recommended for splitting is the one adding most to
  the predicted spot. Elements that correct the others are left alone.
- **Every glass catalog formula**, from AberrationCalculator's glass library (1.x handled two of the
  thirteen AGF formulas, and gave any other glass an index of 0).

## Quick start

```bash
git clone --recursive https://github.com/jaruiz6363/LensSplitter.git
cd LensSplitter
dotnet build -c Release

# What is in the lens, and which element to split
dotnet run --project src/LensSplitter.Cli -- analyze -i mylens.zmx

# Split the recommended element, and write the result as ZEMAX and CODE V
dotnet run --project src/LensSplitter.Cli -- split -i mylens.zmx -o out --format zmx,seq

# Choose the glasses for the two halves
dotnet run --project src/LensSplitter.Cli -- glass -i mylens.zmx -o out -e 3
```

Run with no arguments for an interactive menu. See [BUILDING.md](BUILDING.md) for installing .NET 8
and publishing a standalone executable.

## Commands

### `analyze`

```
lenssplitter analyze -i <lens> [--catalogs <folder>]
```

The lens's first-order numbers, its predicted spot, and a table of its elements: power, share of
the predicted spot, Seidel S1-S3, and whether it can be split.

The **share of the spot** is the element's part of the mean-square predicted spot, with the cross
terms between elements split evenly, so the shares add up to the whole. A large positive share is an
element making the spot bigger; a negative one is an element correcting the others. The element
recommended is the splittable one with the largest positive share.

Not split: a **negative** element, an element **cemented** to another, and one of a closely
**air-spaced achromatic pair** (opposite powers, Abbe numbers more than 15 apart, less than 2 mm of
air) - these are corrected as a unit. `--element` overrides the recommendation, not these rules for
cemented groups.

### `split`

```
lenssplitter split -i <lens> -o <folder> [-e <element>] [--merit <file.mf>] [--format <list>|all]
                   [--min-edge 0.5] [--min-centre 1.0] [--min-gap 0.1] [--iterations 300] [--vary-thickness]
```

Splits the element (`-e`, counted from 1; the recommended one if left off) and optimises the pair.
Writes, in `<folder>`:

| File | |
|---|---|
| `<lens>_split<N>.<ext>` | the split lens, in each format asked for (the input's own by default) |
| `<lens>_split<N>.txt` | the report: the lens, its elements, the split, and a before/after table |
| `<lens>_split<N>.svg` | the lens and its split, drawn to the same scale, with the paraxial rays |
| `<lens>_split<N>_glass/` | for Optiland, the glasses' own data (see below) |

A format that cannot carry the lens says why and is skipped; the others are still written. CODE V,
OSLO and OPTALIX have no r² aspheric term, so a lens with one is not written in those.

### `glass`

```
lenssplitter glass -i <lens> -o <folder> [-e <element>] [-g <glass,glass,...>] [--refine 8] [--top 3]
                   [--merit <file.mf>] [--format <list>|all]
```

Chooses the glasses for the two halves. Every ordered pair of candidate glasses - the element's own
included - is first **screened**: given a small set of starting points, focal length exact, and
scored by its best. The best `--refine` pairs are then split and optimised in full, and ranked by
the merit function. Candidates are AberrationCalculator's **CoreSet28** working set unless `-g`
names others: a search free to choose from every vendor's whole catalogue settles on glasses nobody
stocks.

Writes `<lens>_glass<N>.csv` (every pair, ranked), `<lens>_glass<N>.txt` (the ranking, and a full
report for each written split), and the best `--top` splits as lenses.

### `merit`

```
lenssplitter merit -i <lens> -o <file.mf>
```

Writes the lens's default merit function to a file, to edit and pass back with `--merit`.

## The merit function

The merit function is AberrationCalculator's, in its own text format - one operand per line, which
can be commented, diffed and kept beside a lens. The default is:

```
PRMSA, 1, TAR 0                 # the predicted RMS spot, every field and wavelength
AXC,   w, TAR 0                 # real axial colour, for a lens with more than one wavelength
LCF,   1, TAR 0, 1.0            # real lateral colour at the full field
```

The predicted spot measures each wavelength at its own focus, so it cannot see colour: axial colour
is a focus that moves with wavelength, lateral colour an image that grows with it. So for a lens with
more than one wavelength the default adds both. Axial colour is a length along the axis; it blurs
the image by about that length times the image-space marginal ray angle u′, so its weight `w` is u′²,
to count as a spot radius does.

### Writing one

Each line is `TYPE, WEIGHT, TAR x, INPUTS` - driven to a target - or `TYPE, WEIGHT, MIN x, INPUTS`,
`MAX x`, or both: a limit, which costs nothing at all while it is met. Inputs are positional, and
trailing ones may be left off. `#` starts a comment.

```
PRMSA, 1, TAR 0
B7,    1, TAR 0                 # seventh-order spherical
DISTF, 1, MIN -1, MAX 1, 1.0    # distortion within one per cent at the full field
```

`lenssplitter merit -i <lens> -o <file.mf>` writes the lens's default to start from; pass the file
back with `--merit` to `split` or `glass`.

### Every operand

Anything AberrationCalculator's optimiser takes:

| Operand | What it is | Inputs |
|---|---|---|
| `PRMSA` | Robb's predicted RMS spot radius, over every field and wavelength | none |
| `B` `F` `C` `Pi` `E` | third-order aberration coefficients: spherical, coma, astigmatism, Petzval, distortion | `surface, wave` |
| `B5` `F1` `F2` `M1` `M2` `M3` `N1` `N2` `N3` `C5` `Pi5` `E5` | fifth-order coefficients | `surface, wave` |
| `B7` `Tau2` ... `Tau20` | seventh-order coefficients (`B7` is seventh-order spherical) | `surface, wave` |
| `AXC` | real axial colour | none |
| `LCF` | real lateral colour | `hy` |
| `DISTF` | real distortion, per cent | `hy` |
| `EFL` | effective focal length | `wave` |
| `TTL` | total track, first surface to image | none |
| `EGT` | edge thickness of each glass in a span of surfaces | `surface, surface2` |
| `EAT` | edge thickness of each air space in a span | `surface, surface2` |
| `DTRGT` | diameter-to-thickness ratio | `surface, surface2` |
| `PX` `PY` `PZ` `PL` `PM` `PN` | a paraxial ray's position and direction cosines | `surface, wave, hy, px, py` |
| `RX` `RY` `RZ` `RL` `RM` `RN` | the same for a real ray | `surface, wave, hy, px, py` |
| `ASBLT` | the wavefront error a build tolerance would induce | `decentre, tilt, wave` |

- `hy` is a fraction of the full field (0 on axis, 1 at the edge), and `px`, `py` fractions of the
  pupil radius. `wave` counts from 1 in the lens's own order; left off, it is the primary.
- An aberration coefficient is the system's total, or - with a surface number - that surface's
  share: `B, 1, TAR 0, 5` is surface 5's third-order spherical. The coefficient names are those the
  analysis prints.
- Coefficients are transverse, in lens units, at the full field and aperture, as the predicted spot
  combines them.

See AberrationCalculator's [docs/optimizer.md](https://github.com/jaruiz6363/AberrationCalculator/blob/main/docs/optimizer.md)
for each operand in full.

### What LensSplitter adds

Whatever the file says, a split also carries:

| Operand | Held at |
|---|---|
| `EFL` | the original focal length (made exact afterwards) - unless the file has its own |
| `EGT` | at least `--min-edge` on the two halves' glass edges |
| `EAT` | at least `--min-edge` on the air from the space before the split to the one after it, the new gap included |

With `--vary-thickness`, `TTL` too, held at the original track so every later surface stays put -
unless the file has its own.

## How a split is made

See [docs/how-it-works.md](docs/how-it-works.md). In short:

1. **Starting points.** For each power ratio, bending of each half, gap, and length borrowed from the
   air after the element, the four faces are built: the element's two outer faces keep their conic
   and aspheric terms, the two new inner faces are spheres, and the two halves' power is scaled until
   the lens's focal length is exactly the original's. Every later surface stays where it was.
2. **Optimisation.** The best starting points - the best for each thickness, then the best of the
   rest - are optimised: the four faces' curvatures free, the merit function, the focal length held,
   edges kept.
3. **Finishing.** The focal length is made exact, a polish follows with it held harder, and the image
   plane moves with the paraxial focus, keeping whatever defocus the original lens had.

## Glass catalogs

The glass catalogs are AberrationCalculator's, in its `catalogs` folder: every major vendor's for
reading lenses, and the CoreSet28 set the glass search chooses from. Add your own `.agf` files with
`--catalogs <folder>`.

For **Optiland**, each glass is written with its own dispersion data, into a `<lens>_glass` folder
beside the `.json` and installed in `~/.optiland/catalogs`, and named strictly with its catalog - so
Optiland uses exactly the glass the lens was designed with, and never substitutes a near name.

## Limitations

- The lens must be rotationally symmetric: no tilts, decentres or coordinate breaks.
- An OSLO file keeps the full field but not the list of field points; read back, its predicted spot
  is averaged over OSLO's own. At a finite object OSLO takes the field as an object height, on the
  other side of the axis from a positive field angle, so coma and distortion change sign. The lens is
  the same.
- Optiland traces an r² aspheric term in real rays but leaves it out of its paraxial values, so its
  focal length and pupils differ from the lens's. The written file is right; a real ray near the axis
  gives the true focal length.
- The predicted spot is a geometric one, and a truncated series: see AberrationCalculator's
  documentation for where it holds.

## License

MIT - see [LICENSE](LICENSE).
