# LensSplitter

Cross-platform C# application (.NET 8.0) for splitting lens elements using power-preserving splitting to reduce aberrations. Supports glass optimization to find optimal glass combinations.

## Overview

Power-preserving splitting is an optical design technique for reducing spherical aberration by splitting a single lens element into two elements of equal power. This theoretical splitting reduces spherical aberration by a factor of 4.

## Features

- **File Parsing**: Support for ZEMAX ZMX and Optiland JSON formats
- **Glass Catalogs**: AGF glass catalog support with Schott and Sellmeier1 dispersion models
- **Paraxial Ray Tracing**: Marginal and chief ray tracing with EFL/BFL calculations
- **Seidel Aberration Analysis**: 3rd-order aberrations (S1-S5) with per-surface breakdown
- **Buchdahl 5th-Order Aberrations**: 6 primary coefficients (Ap, Bp, Cp, Aq, Bq, Cq) with per-surface contributions, EFL-normalized
- **Power-Preserving Splitting**: Split lens elements with power-preserving algorithm
- **Configurable Merit Function**: Weighted Seidel + Buchdahl/EFL optimization with interactive weight configuration
- **Glass Optimization**: Try different glass combinations to minimize aberrations
- **Multiple Interfaces**: CLI, REST API, and MCP Server

## Installation

### Prerequisites

- .NET 8.0 SDK or later
- (Optional) AGF glass catalog files for realistic glass materials

### Build

```bash
cd LensSplitter
dotnet build
```

### Run Tests

```bash
dotnet test
```

## Usage

### Command Line Interface

```bash
# Split element and output SVG + export files
lenssplitter split --input lens.zmx --output-dir ./output --element 0 --gap 0.1

# Analyze system
lenssplitter analyze --input lens.zmx

# Just render visualization (no splitting)
lenssplitter render --input lens.zmx --output lens.svg

# Add additional glass catalog
lenssplitter split --input lens.zmx --catalog ./catalogs/custom.agf --output-dir ./output
```

### REST API

Start the API server:

```bash
cd src/LensSplitter.Api
dotnet run
```

API endpoints:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/split` | POST | Split element, return JSON result |
| `/api/split/svg` | POST | Split element, return SVG visualization |
| `/api/split/export/zmx` | POST | Split element, return ZMX file |
| `/api/split/export/json` | POST | Split element, return Optiland JSON |
| `/api/system/analyze` | POST | Analyze uploaded system |
| `/api/catalogs` | GET | List loaded glass catalogs |
| `/api/catalogs` | POST | Add glass catalog |

Swagger documentation available at `/swagger` when running in development mode.

### MCP Server

Add to your Claude Code configuration:

```json
{
  "mcpServers": {
    "lenssplitter": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/LensSplitter.Mcp"]
    }
  }
}
```

Available tools:

- `load_system` - Load ZMX or Optiland JSON file
- `load_system_json` - Load from JSON content
- `split_element` - Perform power-preserving split
- `analyze_system` - Get system properties
- `render_visualization` - Generate SVG
- `export_zmx` - Export to ZMX format
- `export_optiland` - Export to Optiland JSON
- `list_catalogs` - List loaded glass catalogs
- `add_catalog` - Load additional AGF catalog
- `lookup_glass` - Look up glass properties

## File Formats

### ZMX (ZEMAX)

Standard ZEMAX lens file format. Supports:
- Surface definitions (SURF, CURV, THIC/DISZ, GLAS, DIAM, CONI)
- Wavelengths (WAVM, WAVE)
- Fields (XFLN, YFLN)
- Aperture (ENPD)

### Optiland JSON

JSON format used by the Optiland Python library. Supports:
- Surface groups with absolute z-positions
- Named materials (Material) and ideal materials (IdealMaterial)
- Fields and wavelengths

### AGF (Glass Catalog)

ZEMAX glass catalog format. Supports:
- Schott dispersion formula (model 1)
- Sellmeier1 dispersion formula (model 2)

## Key Formulas

### Paraxial Ray Tracing

```
Surface power:     φ = (n' - n) / R
Paraxial refract:  n'u' = nu - yφ
Paraxial transfer: y' = y + tu
```

### Lens Power

```
Thin lens:    P = (n-1) * [1/R1 - 1/R2]
Thick lens:   P = (n-1) * [1/R1 - 1/R2 + (n-1)d/(n*R1*R2)]
```

### Shape Factor

```
Shape factor:      X = (R2 + R1) / (R2 - R1)
Optimal shape:     X_opt = -2(n² - 1) / (n + 2)  [for min SA at infinite conjugate]
```

### Lagrange Invariant

```
H = n(u_m * y_c - u_c * y_m)
```

### Conic Surfaces

```
Surface sag:    z = c·r² / (1 + √(1-(1+K)c²r²))
Deformation:    Δz = (K/8)·c³·r⁴ + (K(2+K)/16)·c⁵·r⁶ + ...
Seidel a4:      a4 = K·(n'-n)·c³       (4th-order, implemented)
Buchdahl a6:    a6 = K·(2+K)·(n'-n)·c⁵  (6th-order, not yet implemented)
```

K=0 is a sphere, K=-1 is a paraboloid, K<-1 is a hyperboloid.

**Seidel (complete):** The a4 deformation coefficient corrects all five Seidel sums (S1-S5) with the standard h/h-bar power pattern: ΔS1 = a4·h⁴, ΔS2 = a4·h³·h-bar, etc.

**Buchdahl primary (partial):** The a4 coefficient corrects the 5th-order primary terms at the Seidel level. Cross-surface interactions are captured through prefix sums. However, two intrinsic 5th-order contributions are not yet implemented:

- Within-surface aspherical-spherical cross-term (a4 deformation x the surface's own Gaussian properties)
- Direct 6th-order aspherical contribution (a6)

These missing terms affect the secondary (intrinsic) portion of the Buchdahl computation. For systems with mild conics (|K| < 1), the impact is small. For extreme conics (|K| >> 1), the displayed Total (Primary + Secondary) may be inaccurate.

### Buchdahl 5th-Order Aberrations

Six primary coefficients computed from EFL-normalized paraxial ray data:

```
Sagittal:    Ap (spherical), Bp (oblique spherical), Cp (astigmatism)
Tangential:  Aq (coma), Bq (elliptical coma), Cq (distortion)
```

Effective coefficients incorporate the stop eccentricity parameter P:

```
Ap_eff = Ap + P·Āp    (bar coefficients account for stop shift)
```

### Merit Function

```
MF = Σ Wi·|Si| + WCL·|CL| + WCT·|CT| + Σ WBj·|Bj_eff| / EFL
```

Buchdahl terms are normalized by EFL to match the scale of Seidel coefficients.

## Glass Catalogs

Place AGF files in the `catalogs/` folder relative to the executable for automatic loading. Additional catalogs can be loaded via CLI, API, or MCP tools.

## Project Structure

```
LensSplitter/
├── src/
│   ├── LensSplitter.Core/         # Domain models, paraxial tracing, splitting
│   │   ├── Aberrations/           # Buchdahl 5th-order aberration calculator
│   │   ├── Models/                # Optical system models
│   │   ├── Paraxial/              # Ray tracing, Seidel calculations
│   │   └── Splitting/             # Element splitting, optimization
│   ├── LensSplitter.Parsing/      # ZMX, Optiland JSON, AGF parsers + exporters
│   ├── LensSplitter.Visualization/# SVG rendering
│   ├── LensSplitter.Api/          # REST API (ASP.NET Core)
│   ├── LensSplitter.Mcp/          # MCP Server
│   └── LensSplitter.Cli/          # CLI interface
├── tests/
│   ├── LensSplitter.Core.Tests/
│   ├── LensSplitter.Parsing.Tests/
│   └── LensSplitter.Integration.Tests/
├── catalogs/                       # Default AGF glass catalog folder
└── docs/
```

## Acknowledgments

- [Optiland](https://github.com/HarrisonKramer/optiland) - Open-source optical design software in Python by Kramer Harrison. LensSplitter supports Optiland's JSON file format for interoperability.
- [ZEMAX](https://www.ansys.com/products/optics/ansys-zemax-opticstudio) - Industry-standard optical design software. LensSplitter supports ZMX and AGF file formats.

## License

MIT License - Copyright (c) 2026 Javier A Ruiz

See [LICENSE](../LICENSE) for details.
