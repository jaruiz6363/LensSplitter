# Building LensSplitter

This guide explains how to install the .NET SDK and build LensSplitter from source.

## Prerequisites

LensSplitter requires **.NET 8.0 SDK** or later.

### Installing .NET SDK

#### Windows

**Option 1: Download Installer (Recommended)**
1. Go to https://dotnet.microsoft.com/download/dotnet/8.0
2. Download the **.NET SDK** installer for Windows (not just the Runtime)
3. Run the installer and follow the prompts

**Option 2: Using winget**
```powershell
winget install Microsoft.DotNet.SDK.8
```

**Option 3: Using Chocolatey**
```powershell
choco install dotnet-sdk
```

#### macOS

**Option 1: Download Installer**
1. Go to https://dotnet.microsoft.com/download/dotnet/8.0
2. Download the .NET SDK installer for macOS
3. Run the `.pkg` installer

**Option 2: Using Homebrew**
```bash
brew install dotnet-sdk
```

#### Linux

**Ubuntu/Debian:**
```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

**Fedora:**
```bash
sudo dnf install dotnet-sdk-8.0
```

**Arch Linux:**
```bash
sudo pacman -S dotnet-sdk
```

### Verify Installation

After installation, verify .NET is available:

```bash
dotnet --version
```

You should see version 8.0.x or higher.

## Building

### Clone the Repository

```bash
git clone https://github.com/jaruiz6363/LensSplitter.git
cd LensSplitter
```

### Restore Dependencies

```bash
dotnet restore
```

### Build

**Debug build (default):**
```bash
dotnet build
```

**Release build (optimized):**
```bash
dotnet build -c Release
```

### Run

```bash
dotnet run --project src/LensSplitter.Cli
```

Or with arguments:
```bash
dotnet run --project src/LensSplitter.Cli -- info -i myfile.zmx
```

Note: Use `--` to separate `dotnet run` arguments from application arguments.

### Run Tests

```bash
dotnet test
```

## Publishing a Self-Contained Executable

To create a standalone executable that doesn't require .NET to be installed:

**Windows:**
```bash
dotnet publish src/LensSplitter.Cli -c Release -r win-x64 --self-contained -o publish/win-x64
```

**macOS (Intel):**
```bash
dotnet publish src/LensSplitter.Cli -c Release -r osx-x64 --self-contained -o publish/osx-x64
```

**macOS (Apple Silicon):**
```bash
dotnet publish src/LensSplitter.Cli -c Release -r osx-arm64 --self-contained -o publish/osx-arm64
```

**Linux:**
```bash
dotnet publish src/LensSplitter.Cli -c Release -r linux-x64 --self-contained -o publish/linux-x64
```

The executable will be in the `publish/` directory.

## Troubleshooting

### "dotnet: command not found"

The .NET SDK is not in your PATH. Try:
- **Windows:** Restart your terminal or log out and back in
- **macOS/Linux:** Add to your shell profile:
  ```bash
  export PATH="$PATH:$HOME/.dotnet"
  ```

### Build errors about missing SDK

Ensure you installed the **SDK**, not just the Runtime. The SDK includes the compiler and build tools.

### Permission denied on Linux/macOS

Make the published executable runnable:
```bash
chmod +x publish/linux-x64/LensSplitter.Cli
```

## IDE Support (Optional)

For the best development experience, consider using:

- **Visual Studio 2022** (Windows) - Full-featured IDE with built-in .NET support
- **Visual Studio Code** - Cross-platform, lightweight editor
  - Install the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension
- **JetBrains Rider** - Cross-platform .NET IDE
