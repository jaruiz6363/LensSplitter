# FORBES7

The twenty seventh-order aberration coefficients of a centred system, per surface, split
into what each surface generates on its own and what it generates by acting on the
aberration already present when light reaches it.

Written by Javier Ruiz with Claude Code, September 2026.

G. W. Forbes, "Order doubling in the computation of aberration coefficients",
*J. Opt. Soc. Am.* **73**, 782 (1983).

## What this adds to BUCH7.ZPL

`macros/BUCH7.ZPL` does the same job by Buchdahl's computing scheme and is the better tool
where it applies: one text file, nothing to install, and its third and fifth orders can be
checked inside OpticStudio against the Seidel analysis and FIFTHORD without trusting
anything in this repository.

It applies to **spherical surfaces only**. Buchdahl gives the aspheric scheme in §85 of the
monograph but never published the arranged table for it; `BUCH7_ASPH.ZPL` now carries that
arrangement, reconstructed, as a macro. This program reaches the same figured systems by a
different route, sharing no arithmetic: a sphere, a conic and an even asphere differ only in
the coefficients of one power series and run through identical code. Its own limit is mirrors,
which it refuses (see *What it refuses*). A figured surface therefore also gets a third row,
the part the figuring itself contributes, which the spherical scheme cannot separate
because it never has it.

Both handle either conjugate.

## Why this is a separate solution

Two solutions, on purpose, because they cannot share a target framework.

| solution | projects | framework |
|---|---|---|
| `AberrationCalculator.sln` | the library, the CLI, the tests | .NET 8 |
| `ForbesAberrationCalculatorZOSAPI.sln` | `Forbes7`, `FixBinaries`, the library | .NET Framework 4.8 |

The ZOS-API talks over .NET Remoting, which exists on the Framework and not on .NET 8, so
anything driving OpticStudio from C# has to be a Framework program. `AberrationCalculator.Core`
is built for `netstandard2.0` as well as `net8.0` so that both solutions use exactly the
same validated code rather than two copies of it — the whole reason for doing this in C#
rather than as a second macro.

Note that `dotnet test` and `dotnet build` now need the solution named, since the
repository root holds two.

## Building

**Run `FixBinaries` first.** It finds OpticStudio, checks the three ZOS-API assemblies are
where it thinks, and writes `Forbes7\ZemaxPaths.props` with the install directory.

That file is **not in the repository and never will be**: it names where OpticStudio is
installed and which release it is, and neither is anybody else's business. There is no
default path in the project file either, for the same reason — and because a default would
be wrong on every machine but one, and would fail late and confusingly rather than early
and clearly. `Forbes7` stops the build with an instruction if the file is missing.

```
dotnet build ForbesAberrationCalculatorZOSAPI.sln
```

`FixBinaries` references nothing from OpticStudio, so it builds on a machine that has never
had OpticStudio installed. That is the point of it: it is what you run *before* the other
project can build.

## Running

**Standalone only.**

Run it with no arguments and it asks — for the lens, and for the truncation. Press Enter
at the file prompt to get a browser; a path dragged onto the window works too. The window
stays up at the end, because it is usually started by double-clicking it.

Most of the ZOS-API examples OpticStudio ships take no arguments, so that is what its users
expect. The switches are still there for anyone scripting it:

```
forbes7
forbes7 <file.zmx>
forbes7 <file.zmx> --degree N
```

It starts an OpticStudio of its own, opens the file, reports, and closes it again. It never
touches a session you are working in and cannot leave one in a state it did not find it in.
It does take a licence seat while it runs.

`--degree 3` is the seventh order and is the default. A higher degree costs time and **must
not change the answer** — worth checking once on a design you care about, because it is a
real test of whether the series has converged there.

## What it prints

All three orders, because a seventh-order coefficient means little on its own — it is the
size of the correction to the orders beneath it, and whether that correction matters is a
question about all three.

- **Third and fifth order**, by Buchdahl's scheme, per surface, split intrinsic / figuring /
  induced. Unconverted per surface, as the scheme produces them; the totals carry the
  F/number. These are what FIFTHORD prints and what the Seidel analysis prints, so they can
  be checked without trusting anything here.
- **Seventh order**, by the Forbes series trace, the same way.
- **A cross-check**, run every time, described below.

## Checking it

Three checks, in the order they are worth making.

1. **τ1 against B7 — the program now does this itself** and says so at the end. They are the
   same quantity by two routes sharing no code: a power-series trace from Forbes, and
   Buchdahl's fifth-order working. Nothing substitutes one for the other, so agreement means
   something. On the Cooke triplet both give `1.681450E-03` at infinite conjugate,
   `2.305287E-03` with the object at 250 mm, and `2.292415E-03` with a conic and two
   aspheric terms on the first surface.

   It holds on figured surfaces too. What Buchdahl left unpublished is the *tertiary*
   aspheric arrangement — the twenty τ — and B7 does not come from it; it falls out of the
   fifth-order working, which carries figuring.

2. **All twenty against BUCH7.ZPL**, on a spherical system. They agree to the last printed
   digit at both conjugates. The third and fifth orders agree with it too, and with FIFTHORD.

3. **The parts add up.** `int + fig + ind = tot` on every surface, and the reference plus
   every surface reproduces the system totals. Measured at 1.8E-14 or better on three
   systems including a figured one.

## The reference row

The reference is what the coefficients come to when every step is linearised, and it is
**not zero**. A paraxially perfect system launched on direction cosines sends a ray at angle
θ to about `efl sin θ`, while the coefficients are referred to `efl tan θ`. That difference
is odd and carries no aperture, so it lands wholly in τ20, seventh-order distortion. On the
Cooke triplet it is 1.3E-02 against a τ20 system total of 1.1E-03 — an order of magnitude
larger. It belongs to no surface, so it is printed as its own row rather than folded into
the first one, which would misreport that surface badly.

Both routes share the convention, which is why their totals agree; it is a property of the
reference and not of the optics.

## What it refuses

Standard and Even Asphere surfaces. Anything else — a mirror, a coordinate break, a surface
type there is no series for — stops the program with the reason rather than being quietly
approximated by the nearest thing it does understand.

**A mirror was not actually refused until September 2026**, whatever the line above said. The
surface-type check passes it, being a Standard surface, and `GetIndex` reports the magnitude
of the index, so nothing downstream learned that it reflects: the series trace took it as a
refraction into the same medium and returned near-zero coefficients. `LensBridge` now refuses a
`MIRROR` by name, and the shared trace declines one as well. Built and run on 22 September
2026: on `F10_spherical_mirror.zmx` it stops with "Surface 2 is a mirror" and the reason; on
`F3_conic_a4_a6_a8.zmx` it runs as before, all twenty tau equal to the C# program's to every
printed digit.

**A tilt or decentre on a Standard surface is NOT refused - it is not read.** Only a
coordinate-break SURFACE is turned away. The Tilt/Decenter properties of an ordinary surface
are ignored, so a design that carries them is analysed as if centred. That is a gap, recorded
and not yet fixed: the check has not been written.

Likewise an aperture that is not an entrance-pupil diameter, an image-space F/number or an
object-space NA, and a field that is not an angle or an object height. A wrong pupil or a
wrong field scales every coefficient here, and silently.

Refractive indices come from OpticStudio itself, so the coefficients are computed for the
same glass data the design was made with — including catalogues this repository does not
ship.
