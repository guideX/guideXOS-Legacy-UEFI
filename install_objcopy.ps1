# Install objcopy via MSYS2 or manual conversion
# This script helps you get objcopy working

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   objcopy Installation Helper" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if MSYS2 is already installed
$msys2Paths = @(
    "C:\msys64",
    "C:\msys32",
    "$env:USERPROFILE\msys64",
    "$env:USERPROFILE\msys32"
)

$msys2Found = $false
$msys2Path = ""

foreach ($path in $msys2Paths) {
    if (Test-Path $path) {
        $msys2Found = $true
        $msys2Path = $path
        Write-Host "? Found MSYS2 at: $path" -ForegroundColor Green
        break
    }
}

if ($msys2Found) {
    Write-Host ""
    Write-Host "MSYS2 is installed. Installing objcopy..." -ForegroundColor Yellow
    Write-Host ""
    
    # Try to install binutils
    $objcopyPath = Join-Path $msys2Path "mingw64\bin\objcopy.exe"
    
    if (Test-Path $objcopyPath) {
        Write-Host "? objcopy already installed at: $objcopyPath" -ForegroundColor Green
    } else {
        Write-Host "Installing binutils package..." -ForegroundColor Yellow
        
        # Run MSYS2 command to install
        $msys2Bash = Join-Path $msys2Path "usr\bin\bash.exe"
        
        if (Test-Path $msys2Bash) {
            Write-Host "Running: pacman -S --noconfirm mingw-w64-x86_64-binutils"
            & $msys2Bash -lc "pacman -S --noconfirm mingw-w64-x86_64-binutils"
            
            if (Test-Path $objcopyPath) {
                Write-Host "? objcopy installed successfully!" -ForegroundColor Green
            } else {
                Write-Host "? Installation may have failed. Please try manually." -ForegroundColor Red
            }
        }
    }
    
    # Add to PATH
    Write-Host ""
    Write-Host "Adding objcopy to current session PATH..." -ForegroundColor Yellow
    $env:Path = "$msys2Path\mingw64\bin;$env:Path"
    Write-Host "? Added to PATH for this session" -ForegroundColor Green
    
} else {
    Write-Host "? MSYS2 not found" -ForegroundColor Red
    Write-Host ""
    Write-Host "OPTION 1: Install MSYS2 (Recommended)" -ForegroundColor Cyan
    Write-Host "==========================================="
    Write-Host "1. Download from: https://www.msys2.org/"
    Write-Host "2. Run the installer"
    Write-Host "3. After install, run this script again"
    Write-Host ""
    Write-Host "OPTION 2: Quick Manual Conversion" -ForegroundColor Cyan
    Write-Host "==========================================="
    Write-Host "Download objcopy.exe directly:"
    Write-Host "1. Go to: https://sourceforge.net/projects/mingw-w64/files/"
    Write-Host "2. Download x86_64-posix-seh"
    Write-Host "3. Extract objcopy.exe to: C:\Windows\System32\"
    Write-Host ""
    Write-Host "OPTION 3: Use Online Converter (Quick Test)" -ForegroundColor Cyan
    Write-Host "==========================================="
    Write-Host "I can create a PowerShell-based converter (limited functionality)"
    Write-Host ""
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($msys2Found) {
    Write-Host "1. Close and reopen PowerShell (to get objcopy in PATH)"
    Write-Host "2. Run: .\build.ps1"
    Write-Host "3. Kernel will be converted automatically!"
} else {
    Write-Host "1. Install MSYS2 from https://www.msys2.org/"
    Write-Host "2. Run this script again"
    Write-Host "3. Then run: .\build.ps1"
}

Write-Host ""
