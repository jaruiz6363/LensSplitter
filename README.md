# LensSplitter

Power-preserving lens element splitting with Seidel and Buchdahl aberration optimization.

LensSplitter takes an optical system, splits a selected lens element into two elements while preserving the total power, and optimizes the split to minimize aberrations. It calculates full system Seidel aberrations (S1-S5), Buchdahl 5th-order aberrations, and chromatic aberrations (longitudinal and lateral color).

## Features

- **Power-preserving splitting** - Splits a thick lens into two thinner lenses with the same combined power
- **Seidel aberration analysis** - Calculates S1 (spherical), S2 (coma), S3 (astigmatism), S4 (field curvature), S5 (distortion) with per-surface breakdown. Full conic constant support via a4 deformation coefficient
- **Buchdahl 5th-order aberrations** - Calculates 6 primary coefficients (spherical, oblique spherical, astigmatism, coma, elliptical coma, distortion) with per-surface contributions. Conic support at Seidel level (a4 primary corrections); intrinsic 5th-order aspherical terms (a6, within-surface cross-terms) not yet implemented
- **Chromatic aberration analysis** - Longitudinal color and lateral color
- **Merit function optimization** - Finds optimal power ratio and air gap to minimize weighted Seidel + Buchdahl aberrations
- **Configurable weights** - Interactive weight configuration for all Seidel and Buchdahl aberration terms
- **Glass optimization** - Searches glass catalogs for optimal glass combinations
- **File format support** - ZEMAX ZMX and Optiland JSON formats

## Building

Requires .NET 8.0 SDK or later. See [BUILDING.md](BUILDING.md) for detailed installation instructions.

```bash
git clone <repository>
cd LensSplitter
dotnet restore
dotnet build
```

## Usage

### Interactive Mode

Run without arguments for interactive mode:

```bash
dotnet run --project src/LensSplitter.Cli
```

This presents a menu-driven interface:

```
╔══════════════════════════════════════════════════════════════════════╗
║                         LENSSPLITTER v1.2                            ║
║            Power-Preserving Optical Element Splitting                ║
╚══════════════════════════════════════════════════════════════════════╝

Current Settings:
  Input:   (not set)
  Output:  (not set)
  Catalog: (not set)

Select an option:
  [1] Split - Power-preserving element splitting with optimization
  [2] Info - Analyze optical system
  [3] Glass - Optimize glass selection for split elements
  [W] Weights - Configure aberration weights
  [S] Settings - Configure paths
  [Q] Quit
```

### Command Line

```bash
# Split with optimization (auto-selects best element)
dotnet run --project src/LensSplitter.Cli -- split -i input.zmx -o output/

# Split specific element (1-based index)
dotnet run --project src/LensSplitter.Cli -- split -i input.zmx -o output/ --element 1

# Analyze system
dotnet run --project src/LensSplitter.Cli -- info -i input.zmx

# Glass optimization
dotnet run --project src/LensSplitter.Cli -- glass -i input.zmx -o output/ --glasses "N-SK16,N-BK7,N-LAK22,N-SSK8"
```

## Supported File Formats

### Input

| Format | Extension | Description |
|--------|-----------|-------------|
| ZEMAX | `.zmx` | ZEMAX OpticStudio lens files (UTF-16 LE) |
| Optiland | `.json` | Optiland JSON format |

### Output

Both formats are exported for each split result:
- `split.zmx` - ZEMAX format
- `split.json` - Optiland JSON format

## Supported Configurations

### Field Types
- **Angle** - Field angles in degrees (infinite conjugate)
- **Object Height** - Object heights in mm (finite conjugate)

Unsupported: Paraxial Image Height, Real Image Height 

### Units
- Lens units must be **millimeters** (MM)

### Aperture
- **Entrance Pupil Diameter (EPD)** - Only supported aperture type

Unsupported: Image F/#, Object Space NA, Float by Stop Size

## Output Example

```
Optimized split of element 1 (SK16)
  Power ratio: 0.5000 (equal split)
  Air gap: 0.39 mm

Aberration Comparison (Original → Split):
╔═══════════════════╦═══════════════╦═══════════════╦═══════════════╗
║ Aberration        ║ Original      ║ Split         ║ Improvement   ║
╠═══════════════════╬═══════════════╬═══════════════╬═══════════════╣
║ S1 (Spherical)    ║  -1.2045E-002 ║  -9.8823E-003 ║     1.22x     ║
║ S2 (Coma)         ║   5.6721E-003 ║   4.9876E-003 ║     1.14x     ║
║ S3 (Astigmatism)  ║  -2.3415E-003 ║  -2.1567E-003 ║     1.09x     ║
║ S4 (Field Curv)   ║  -1.8934E-003 ║  -1.8756E-003 ║     1.01x     ║
║ S5 (Distortion)   ║   8.4523E-004 ║   7.9812E-004 ║     1.06x     ║
╠═══════════════════╬═══════════════╬═══════════════╬═══════════════╣
║ CL (Long. Color)  ║    0.0523 mm  ║    0.0498 mm  ║     1.05x     ║
║ CT (Lat. Color)   ║    0.0234 mm  ║    0.0221 mm  ║     1.06x     ║
╠═══════════════════╬═══════════════╬═══════════════╬═══════════════╣
║ Merit Function    ║    0.0156     ║    0.0134     ║     1.16x     ║
╚═══════════════════╩═══════════════╩═══════════════╩═══════════════╝
```

## How It Works

### Power-Preserving Splitting

Given a thick lens with power φ, LensSplitter finds two lenses with powers φ₁ and φ₂ such that:

```
φ = φ₁ + φ₂ - d·φ₁·φ₂
```

where d is the air gap between the elements. The power ratio k = φ₁/φ determines how power is distributed.

### Optimization

The optimizer searches for the power ratio and air gap that minimize a weighted merit function combining Seidel (3rd-order) and Buchdahl (5th-order) aberrations while preserving the system EFL:

```
MF = W1·|S1| + W2·|S2| + W3·|S3| + W4·|S4| + W5·|S5| + WCL·|CL| + WCT·|CT|
   + (WBSph·|Ap_eff| + WBCma·|Aq_eff| + WBObl·|Bp_eff| + WBEll·|Bq_eff| + WBAst·|Cp_eff| + WBDst·|Cq_eff|) / EFL
```

Buchdahl coefficients are normalized by EFL to bring them to the same scale as the Seidel coefficients.

**Default Seidel weights:**
| Weight | Aberration | Default | Notes |
|--------|-----------|---------|-------|
| W1 | Spherical (S1) | 1.0 | |
| W2 | Coma (S2) | 1.0 | |
| W3 | Astigmatism (S3) | 1.0 | |
| W4 | Field Curvature (S4) | 1.0 | |
| W5 | Distortion (S5) | 0.0 | Often less critical |
| WCL | Longitudinal Color | 1.0 | Requires multiple wavelengths |
| WCT | Lateral Color | 1.0 | Requires multiple wavelengths |

**Default Buchdahl weights:**
| Weight | Aberration | Default | Notes |
|--------|-----------|---------|-------|
| WBSph | 5th-order Spherical (Ap) | 1.0 | |
| WBCma | 5th-order Coma (Aq) | 1.0 | |
| WBObl | Oblique Spherical (Bp) | 1.0 | |
| WBEll | Elliptical Coma (Bq) | 1.0 | |
| WBAst | 5th-order Astigmatism (Cp) | 1.0 | |
| WBDst | 5th-order Distortion (Cq) | 0.0 | Often less critical |

All weights can be configured interactively using the **W** command. Weights persist for the duration of the session. Set a weight to 0.0 to exclude that term from the merit function.

**EFL Preservation:** The merit function includes a quadratic EFL penalty to maintain the original system focal length:

```
eflDeviation = |EFL_split - EFL_original| / |EFL_original|
eflPenalty = 1.0 - (eflDeviation / MaxEflDeviation)²
```

The default `MaxEflDeviation` is 3%. Configurations that deviate more from the target EFL receive progressively worse merit scores, with severe penalties as deviation approaches the maximum allowed

### Element Selection

When no element is specified, LensSplitter analyzes all elements and recommends the best candidate for splitting. Elements are first filtered by eligibility, then ranked by a weighted aberration scoring that matches the optimization merit function.

**Eligibility criteria (elements must have all of these):**
- Positive power (splitting negative elements is typically not beneficial)
- Not part of a cemented or air-spaced doublet/triplet
- No aspheric (conic) surfaces

**Scoring:** Eligible elements are ranked by their weighted contribution to system aberrations, computed from full Seidel ray-traced coefficients (S1, S2, S3) summed per element:

```
Score = W1·|S1_element| + W2·|S2_element| + W3·|S3_element|
```

This uses the same weights as the optimization merit function (W1=1.0, W2=1.0, W3=1.0), ensuring the element selected for splitting is the one whose splitting most improves overall system performance. Including coma (S2) in the scoring is particularly important for systems like Cooke triplets, where elements far from the stop contribute significant coma that S1-only scoring would miss. Buchdahl 5th-order aberrations are not used for element selection because they are system-level quantities that do not decompose cleanly into per-element contributions.

## Glass Catalogs

LensSplitter includes built-in glass catalogs:
- Schott
- Ohara
- CDGM

Additional AGF catalogs can be loaded via the `--catalog` option.

### Default Glass List for Optimization

The glass optimization command uses a default list of 28 glasses from the Schott S1_GLASS (preferred) catalog:

| Category | Glasses |
|----------|---------|
| **Flint** (high dispersion) | F2, F5, SF1, SF2, SF4, SF5, LF5 |
| **Crown** (low dispersion) | K7, N-BK7, N-K5, N-SK2, N-SK5, N-SK16, N-SSK5 |
| **Barium** | N-BAF51, N-BAF52, N-BALF4, N-BASF2 |
| **Lanthanum** (high index) | N-LAF2, LAFN7, LASF35, N-LAK9, N-LAK10 |
| **Special** | N-FK58, N-PK51, N-PSK53A, N-KZFS4, N-SF57 |

### Customizing the Glass List

Use the `--glasses` or `-g` option to specify a custom list:

```bash
# Use specific glasses
dotnet run --project src/LensSplitter.Cli -- glass -i input.zmx -o output/ --glasses "N-BK7,N-SK16,F2,SF2"

# Use only crown glasses
dotnet run --project src/LensSplitter.Cli -- glass -i input.zmx -o output/ -g "N-BK7,N-K5,N-SK2,N-SK5,N-SK16"

# Use high-index glasses for compact designs
dotnet run --project src/LensSplitter.Cli -- glass -i input.zmx -o output/ -g "N-LAK9,N-LAK10,LASF35,N-LAF2"
```

In interactive mode, press Enter at the glass prompt to use the default 28 glasses, or type a comma-separated list of glass names.

## Project Structure

```
LensSplitter/
├── src/
│   ├── LensSplitter.Core/       # Core algorithms
│   │   ├── Aberrations/         # Buchdahl 5th-order aberration calculator
│   │   ├── Models/              # Optical system models
│   │   ├── Paraxial/            # Ray tracing, Seidel calculations
│   │   └── Splitting/           # Element splitting, optimization
│   ├── LensSplitter.Parsing/    # File format support
│   │   ├── Zmx/                 # ZEMAX parser
│   │   ├── Optiland/            # JSON parser
│   │   ├── Export/              # Exporters
│   │   └── Glass/               # Glass catalog management
│   ├── LensSplitter.Cli/        # Command-line interface
│   └── LensSplitter.Visualization/  # SVG rendering
└── tests/
    ├── LensSplitter.Core.Tests/
    ├── LensSplitter.Parsing.Tests/
    └── LensSplitter.Integration.Tests/
```

## License

MIT License - see [LICENSE](LICENSE) for details.

## Acknowledgements

- **[Optiland](https://github.com/HarrisonKramer/optiland)** - Open-source Python lens design and analysis tool by Harrison Kramer. LensSplitter supports Optiland's JSON file format for interoperability.

## References

- Smith, W. J. "Modern Optical Engineering" - Seidel aberration theory
- Buchdahl, H. A. "Optical Aberration Coefficients" - 5th-order aberration theory
- Kingslake, R. "Lens Design Fundamentals" - Lens splitting techniques
- ZEMAX OpticStudio User Manual - File format specification
- Kramer, H. "Optiland" - https://github.com/HarrisonKramer/optiland
