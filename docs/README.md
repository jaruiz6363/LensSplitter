# LensSplitter

Cross-platform C# application (.NET 8.0) for splitting lens elements using power-preserving splitting to reduce aberrations. Supports glass optimization to find optimal glass combinations.

## Overview

Power-preserving splitting is an optical design technique for reducing spherical aberration by splitting a single lens element into two elements of equal power. This theoretical splitting reduces spherical aberration by a factor of 4.

## Features

- **File Parsing**: Support for ZEMAX ZMX and Optiland JSON formats
- **Glass Catalogs**: AGF glass catalog support with Schott and Sellmeier1 dispersion models
- **Paraxial Ray Tracing**: Marginal and chief ray tracing with EFL/BFL calculations
- **Power-Preserving Splitting**: Split lens elements with power-preserving algorithm
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

## Glass Catalogs

Place AGF files in the `catalogs/` folder relative to the executable for automatic loading. Additional catalogs can be loaded via CLI, API, or MCP tools.

## Project Structure

```
LensSplitter/
├── src/
│   ├── LensSplitter.Core/         # Domain models, paraxial tracing, splitting
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
