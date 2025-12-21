#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Check build environment prerequisites for guideXOS

.DESCRIPTION
    Validates that all required tools are installed and properly configured
#>

$ErrorActionPreference = "Continue"

function Write-Check($text) {
    Write-Host "Checking $text..." -NoNewline
}

function Write-OK {
    Write-Host " ?" -ForegroundColor Green
}

function Write-Fail($msg) {
    Write-Host " ?" -ForegroundColor Red
    if ($msg) {
        Write-Host "  $msg" -ForegroundColor Yellow
    }
}

function Write-Warning($msg) {
    Write-Host "  ? $msg" -ForegroundColor Yellow
}

Write-Host "`n=== guideXOS Build Environment Check ===" -ForegroundColor Cyan
Write-Host ""

$allOk = $true

# Check .NET SDK
Write-Check ".NET 9 SDK"
try {
    $dotnetVersion = dotnet --version 2>$null
    if ($dotnetVersion -match "^9\.") {
        Write-OK
        Write-Host "    Version: $dotnetVersion" -ForegroundColor Gray
    } else {
        Write-Fail "Found version $dotnetVersion, need 9.x"
        Write-Warning "Download from: https://dotnet.microsoft.com/download/dotnet/9.0"
        $allOk = $false
    }
} catch {
    Write-Fail "Not found"
    Write-Warning "Download from: https://dotnet.microsoft.com/download/dotnet/9.0"
    $allOk = $false
}

# Check Python
Write-Check "Python 3.x"
try {
    $pythonVersion = python --version 2>$null
    if ($pythonVersion -match "Python 3\.") {
        Write-OK
        Write-Host "    Version: $pythonVersion" -ForegroundColor Gray
    } else {
        Write-Fail "Found $pythonVersion, need 3.x"
        Write-Warning "Download from: https://www.python.org/downloads/"
        $allOk = $false
    }
} catch {
    Write-Fail "Not found"
    Write-Warning "Download from: https://www.python.org/downloads/"
    $allOk = $false
}

# Check MSBuild
Write-Check "MSBuild (Visual Studio 2022)"
$msbuildPaths = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
)

$msbuildFound = $false
foreach ($path in $msbuildPaths) {
    if (Test-Path $path) {
        Write-OK
        Write-Host "    Path: $path" -ForegroundColor Gray
        $msbuildFound = $true
        break
    }
}

if (-not $msbuildFound) {
    if (Get-Command msbuild -ErrorAction SilentlyContinue) {
        Write-OK
        Write-Host "    Found in PATH" -ForegroundColor Gray
    } else {
        Write-Fail "Not found"
        Write-Warning "Install Visual Studio 2022"
        Write-Warning "Download from: https://visualstudio.microsoft.com/downloads/"
        $allOk = $false
    }
}

# Check objcopy (optional but recommended)
Write-Check "objcopy (MSYS2/LLVM)"
$objcopyPaths = @(
    "C:\msys64\mingw64\bin\objcopy.exe",
    "C:\msys64\usr\bin\objcopy.exe",
    "C:\Program Files\LLVM\bin\llvm-objcopy.exe"
)

$objcopyFound = $false
foreach ($path in $objcopyPaths) {
    if (Test-Path $path) {
        Write-OK
        Write-Host "    Path: $path" -ForegroundColor Gray
        $objcopyFound = $true
        break
    }
}

if (-not $objcopyFound) {
    if (Get-Command objcopy -ErrorAction SilentlyContinue) {
        Write-OK
        Write-Host "    Found in PATH" -ForegroundColor Gray
    } elseif (Get-Command llvm-objcopy -ErrorAction SilentlyContinue) {
        Write-OK
        Write-Host "    Found llvm-objcopy in PATH" -ForegroundColor Gray
    } else {
        Write-Host " ?" -ForegroundColor Yellow
        Write-Warning "Not found - kernel conversion will be manual"
        Write-Warning "Install MSYS2: https://www.msys2.org/"
        Write-Warning "Then run: pacman -S mingw-w64-x86_64-binutils"
        Write-Warning "Or use WSL for conversion"
    }
}

# Check WSL (alternative to objcopy)
Write-Check "WSL (Windows Subsystem for Linux)"
try {
    $wslVersion = wsl --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-OK
        Write-Host "    Available for kernel conversion" -ForegroundColor Gray
    } else {
        Write-Host " -" -ForegroundColor Gray
        Write-Host "    Not installed (optional)" -ForegroundColor Gray
    }
} catch {
    Write-Host " -" -ForegroundColor Gray
    Write-Host "    Not installed (optional)" -ForegroundColor Gray
}

# Check QEMU (for testing)
Write-Check "QEMU (for testing)"
if (Get-Command qemu-system-x86_64 -ErrorAction SilentlyContinue) {
    Write-OK
    $qemuVersion = qemu-system-x86_64 --version 2>$null | Select-Object -First 1
    Write-Host "    Version: $qemuVersion" -ForegroundColor Gray
} else {
    Write-Host " -" -ForegroundColor Gray
    Write-Host "    Not installed (optional, for testing)" -ForegroundColor Gray
    Write-Host "    Download from: https://www.qemu.org/download/" -ForegroundColor Gray
}

# Check project files
Write-Host ""
Write-Host "=== Project Files ===" -ForegroundColor Cyan

Write-Check "Bootloader project"
if (Test-Path "guideXOSBootLoader\guideXOSBootLoader.vcxproj") {
    Write-OK
} else {
    Write-Fail "Not found"
    $allOk = $false
}

Write-Check "Kernel project"
if (Test-Path "guideXOS\guideXOS.csproj") {
    Write-OK
} else {
    Write-Fail "Not found"
    $allOk = $false
}

Write-Check "Ramdisk builder"
if (Test-Path "tools\ramdisk_builder.py") {
    Write-OK
} else {
    Write-Fail "Not found"
    $allOk = $false
}

Write-Check "Build script"
if (Test-Path "build.ps1") {
    Write-OK
} else {
    Write-Fail "Not found"
    $allOk = $false
}

# Check ramdisk assets
Write-Host ""
Write-Host "=== Ramdisk Assets ===" -ForegroundColor Cyan

Write-Check "Ramdisk source directory"
if (Test-Path "ramdisk_src") {
    Write-OK
    
    # Check for required files
    $requiredFiles = @(
        "ramdisk_src\Images\Cursor.png",
        "ramdisk_src\Images\Grab.png",
        "ramdisk_src\Images\Busy.png",
        "ramdisk_src\Fonts\enludo.btf"
    )
    
    $missingAssets = @()
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path $file)) {
            $missingAssets += $file
        }
    }
    
    if ($missingAssets.Count -gt 0) {
        Write-Warning "Missing required assets:"
        foreach ($file in $missingAssets) {
            Write-Host "      - $file" -ForegroundColor Yellow
        }
        Write-Host "    See ramdisk_src\README.md for details" -ForegroundColor Gray
    } else {
        Write-Host "    ? All required assets present" -ForegroundColor Green
    }
} else {
    Write-Fail "Not found"
    Write-Warning "Run: New-Item -ItemType Directory ramdisk_src"
    $allOk = $false
}

# Summary
Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan

if ($allOk) {
    Write-Host "? All required tools are installed" -ForegroundColor Green
    Write-Host ""
    Write-Host "Ready to build!" -ForegroundColor Green
    Write-Host "  Run: .\build.ps1" -ForegroundColor Cyan
} else {
    Write-Host "? Some required tools are missing" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install missing tools and try again" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "For detailed build instructions, see:" -ForegroundColor Gray
Write-Host "  - BUILD_GUIDE.md" -ForegroundColor Cyan
Write-Host "  - NEXT_STEPS.md" -ForegroundColor Cyan
Write-Host ""
