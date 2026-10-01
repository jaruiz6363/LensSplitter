# Buchdahl coefficients per surface in OSLO

This folder holds the OSLO ports of the two Buchdahl macros:

| File | Command | For | Beyond the other |
|---|---|---|---|
| `buch7_asph.ccl` | `buch7_asph` | spheres, conics and even aspheres | the figuring split out on every surface |
| `buch7.ccl` | `buch7` | spheres only | the seventh order split into intrinsic and induced, the ninth-order spherical aberration, and Table I entry by entry |

On a lens of spheres the two print the same coefficients. Both can be installed together.

## buch7_asph.ccl

`buch7_asph.ccl` is the OSLO port of [`macros/BUCH7_ASPH.ZPL`](../macros/BUCH7_ASPH.ZPL). It prints
the third-, fifth- and seventh-order Buchdahl coefficients of a system that may carry conics and even
aspheres, as system totals and **surface by surface**.

For each surface, the third order, the fifth order and B7 are split three ways:

| Part | What it is |
|---|---|
| **intrinsic** | what the surface generates out of its own curvature, indices and the two paraxial rays arriving at it |
| **figuring** | what its conic and its AD, AE, AF terms add; zero on a sphere |
| **induced** | what the aberration already accumulated ahead of the surface produces in it; there is none at third order |

Then each surface's induced share of the fifth-order magnitude it carries. Near 0, the surface is
on its own. Near 1, what it carries was handed to it, and the fix is upstream. Above 1, the
intrinsic and induced parts are cancelling each other there.

Last come the twenty seventh-order tau, as system totals and surface by surface. Here the
per-surface tau are totals, not split into intrinsic and induced: for a figured surface no
published derivation separates them, as the macro explains. (On spheres `buch7` does split them.)
tau1 is printed both as B7 from the fifth-order working and as Table I gives it; on a lens of
spheres the two are identical.

### Installing and running it

1. Copy `buch7_asph.ccl` into OSLO's CCL folder, `private\ccl` under the OSLO data folder (for OSLO
   EDU, `C:\Users\Public\Documents\OSLO66 EDU\private\ccl`).
2. Compile it: **Tools > Compile CCL**, and check the window shows `No errors detected`. OSLO keeps
   running the version it last compiled.
3. Open a lens and type `buch7_asph`.

```
buch7_asph               per surface, current wavelength, report file
buch7_asph 0             system totals only
buch7_asph 1 2           at wavelength 2
buch7_asph 1 0 0         without the report file
```

Each run is also added to the end of `B7_REPORT`, set at the top of the file to
`C:/GIT/AberrationCalculator/ccl/buch7_asph.txt`, so several lenses can be run into one file. Change
it to a folder that exists on your machine.

### What it accepts

- **Surfaces:** plane, sphere, conic and OSLO's standard asphere (AD, AE, AF). AG, the r^10 term,
  cannot reach the seventh order and is noted and left out. Any other type (toric, spline, general
  asphere, perfect lens) is declined by name. A figured flat facing collimated light is declined for
  the seventh order; its third and fifth order still stand.
- **Size:** at most 16 surfaces between object and image. The arrays are sized to fit the 1 MB of
  global storage CCL shares with OSLO's own CCL.
- **Rays:** the paraxial marginal and chief rays are traced from OSLO's surface data. They are
  launched as AberrationCalculator launches them: EBR at the entrance pupil, and the field as ANG at
  infinity or OBH at a finite object. The report prints them beside the index after each surface, so
  they can be set against OSLO's own `pxt all`.
- **Mirrors:** a reflecting surface negates the index from there on, as in the C#.

## buch7.ccl

`buch7.ccl` is the OSLO port of [`macros/BUCH7.ZPL`](../macros/BUCH7.ZPL), the macro for spherical
surfaces. On a lens of spheres it gives the same coefficients as `buch7_asph`, and adds three things:

- **The seventh order split too.** Each surface's twenty tau, and the system's, are split into
  intrinsic and induced, as the third and fifth order are.
- **The ninth-order spherical aberration**, per surface, intrinsic and total, by Buchdahl's paper IV
  (J. Opt. Soc. Am. 48, 757, 1958). Nobody has published the ninth order for a figured surface, so
  only this file has it.
- **Table I entry by entry**, surface by surface, in Buchdahl's own numbering.

The third and fifth order per surface are printed unconverted, as the macro prints them: multiply by
the F/number for a transverse aberration. Their totals, and all the tau, are in transverse measure.

### Installing and running it

Copy `buch7.ccl` into the same folder, compile, open a lens and type `buch7`.

```
buch7                    current wavelength, report file
buch7 1                  and the ninth-order spherical aberration
buch7 0 2                at wavelength 2
buch7 0 0 0              without the report file
```

Each run is also added to the end of `BS_REPORT`, `C:/GIT/AberrationCalculator/ccl/buch7.txt`.

### What it accepts

Plane and spherical surfaces only: a conic or any AD to AG term makes it stop and point to
`buch7_asph`. At least two surfaces between object and image (a dummy plane will do), at most 16.
Rays and mirrors are handled as in `buch7_asph`.

## How they are built

The front end, meaning the lens data and the two paraxial rays, is written for OSLO. Everything from
the macro's stage A on is translated from the macro statement for statement by
[`tools/zpl2ccl`](../tools/zpl2ccl), so the arithmetic is the macro's own:

- every ZPL variable becomes a global, since ZPL is case-insensitive and has no locals. The two files
  are compiled into one OSLO, so each has its own prefix: `z_` in `buch7_asph`, `s7_` in `buch7`;
- each `SUB` becomes a function;
- CCL refuses a function past an internal size, so the macro's main body is cut into functions of
  at most 200 lines at statement boundaries;
- what only makes sense in OpticStudio is left out: the notes pointing to FIFTHORD and FORBES, and
  `BUCH7`'s call buffer for `ROBB.ZPL`.

After changing a macro, run `python tools/zpl2ccl/regen.py buch7_asph` (or `buch7`), then compile in
OSLO and check it.

## Checking them

`reference/` holds this program's own per-surface output (`surfaces.tsv`) for three lenses, and its
ninth-order table for the double Gauss. Two scripts check one run of a report against them:

```
python tools/zpl2ccl/compare.py       <one run of buch7_asph's report> ccl/reference/<lens>.surfaces.tsv
python tools/zpl2ccl/compare_buch7.py <one run of buch7's report>      ccl/reference/<lens>.surfaces.tsv
```

Run in OSLO EDU 6.6 in September 2026, every number agreed with the C# to the last printed digit.

`buch7_asph`:

| Lens | Route through the macro | Per-surface split | Per-surface tau | 38 system totals |
|---|---|---|---|---|
| `F6_triplet_two_aspheres.len`: triplet, conic + AD, and AD + AE | figured | 469 entries, worst 4.4e-7 | 140, worst 4.5e-7 | worst 4.2e-7 |
| `F4_parabolic_mirror.len` | figured, reflecting | 134, worst 2.5e-7 | 40, worst 4.5e-7 | worst 4.5e-7 |
| `KingslakeDG.len`: double Gauss, all spheres | Table I alone | 603, worst 4.6e-7 | 180, worst 4.1e-7 | worst 3.4e-7 |

`buch7`, on `KingslakeDG.len`:

| Quantity | Compared | Worst relative difference |
|---|---|---|
| Third order per surface | 40 | 4.1e-7 |
| Fifth order and B7 per surface, intrinsic / induced / total | 299 | 4.1e-7 |
| 18 system totals | 18 | 3.4e-7 |
| tau per surface, and the 20 system tau | 180 | 4.1e-7 |
| Ninth order per surface: intrinsic, total, T1-dagger | 27 | 2.7e-5, within the C#'s five printed figures |

Within `buch7`'s report every tau's intrinsic and induced parts add to its total. The C# does not
split the tau, so that split rests on the translation of the macro, whose Table I was checked against
Buchdahl's own printed table.

The paraxial rays both files trace agree with the C#'s at every surface.

On F4 the conic's figuring cancels the sphere's spherical aberration at every order: B, B5 and B7
come out zero, and so does tau1 by both routes. The F/number there is -2.5, not 2.5. After an odd
number of reflections the marginal ray leaves with the opposite sign of slope, and the F/number that
scales the coefficients keeps that sign, as the C# and the macro do.
