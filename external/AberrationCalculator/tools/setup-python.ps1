# setup-python.ps1
# Downloads an embeddable Python, bootstraps pip and installs optiland into it, so that
# abcalc's coefficients can be cross-checked against Optiland without a system Python.
#
# Adapted from tools/setup-python.ps1 in the RelativeIlluminationCalculator repository.
#
#   .\tools\setup-python.ps1
#
# The environment lands in python-embed\ (gitignored). ABCALC_PYTHON_HOME overrides where
# it is looked for.

param(
    [string]$PythonVersion = "3.12.8",
    [string]$TargetDir = (Join-Path $PSScriptRoot "..\python-embed")
)

$ErrorActionPreference = "Stop"
# Native tools (pip) write notices to stderr, which PowerShell would turn into terminating
# errors under Stop. Exit codes are checked explicitly instead.
$PSNativeCommandUseErrorActionPreference = $false

$majorMinor = ($PythonVersion -split '\.')[0..1] -join ''
$zipUrl = "https://www.python.org/ftp/python/$PythonVersion/python-$PythonVersion-embed-amd64.zip"
$zipFile = Join-Path $env:TEMP "python-$PythonVersion-embed-amd64.zip"
$TargetDir = [System.IO.Path]::GetFullPath($TargetDir)

Write-Host "=== Embedded Python for abcalc's Optiland cross-check ===" -ForegroundColor Cyan
Write-Host "Python version  : $PythonVersion"
Write-Host "Target directory: $TargetDir"
Write-Host ""

$pythonExe = Join-Path $TargetDir "python.exe"

if (Test-Path $pythonExe) {
    Write-Host "[1/5] Python already extracted." -ForegroundColor Green
} else {
    Write-Host "[1/5] Downloading Python $PythonVersion embeddable zip..." -ForegroundColor Yellow
    if (-not (Test-Path $zipFile)) {
        Invoke-WebRequest -Uri $zipUrl -OutFile $zipFile -UseBasicParsing
    }
    Write-Host "[2/5] Extracting..." -ForegroundColor Yellow
    if (Test-Path $TargetDir) { Remove-Item $TargetDir -Recurse -Force }
    Expand-Archive -Path $zipFile -DestinationPath $TargetDir -Force
}

# An embeddable Python ships with 'import site' commented out, which leaves pip and
# site-packages unreachable.
$pthFile = Join-Path $TargetDir "python$majorMinor._pth"
if (Test-Path $pthFile) {
    $content = Get-Content $pthFile -Raw
    if ($content -match '#\s*import site') {
        Write-Host "[3/5] Enabling 'import site'..." -ForegroundColor Yellow
        Set-Content -Path $pthFile -Value ($content -replace '#\s*import site', 'import site') -NoNewline
    } else {
        Write-Host "[3/5] 'import site' already enabled." -ForegroundColor Green
    }
} else {
    Write-Host "[3/5] WARNING: $pthFile not found." -ForegroundColor Red
}

$hasPip = $false
try {
    $ErrorActionPreference = "Continue"
    $pipCheck = & $pythonExe -m pip --version 2>&1
    if ($LASTEXITCODE -eq 0) { $hasPip = $true }
    $ErrorActionPreference = "Stop"
} catch { $ErrorActionPreference = "Stop" }

if ($hasPip) {
    Write-Host "[4/5] pip already available: $pipCheck" -ForegroundColor Green
} else {
    Write-Host "[4/5] Bootstrapping pip..." -ForegroundColor Yellow
    $getPipPath = Join-Path $TargetDir "get-pip.py"
    if (-not (Test-Path $getPipPath)) {
        Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile $getPipPath -UseBasicParsing
    }
    $ErrorActionPreference = "Continue"
    & $pythonExe $getPipPath
    $rc = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    if ($rc -ne 0) { Write-Error "Failed to bootstrap pip." }
}

Write-Host "[5/5] Installing optiland..." -ForegroundColor Yellow
$ErrorActionPreference = "Continue"
& $pythonExe -m pip install optiland
$rc = $LASTEXITCODE
$ErrorActionPreference = "Stop"
if ($rc -ne 0) { Write-Error "Failed to install optiland." }

Write-Host ""
Write-Host "=== Setup complete ===" -ForegroundColor Green
& $pythonExe -c "import optiland, numpy; print('optiland', optiland.__version__, '| numpy', numpy.__version__)"
