# Boot guideXOS in QEMU (PowerShell version)
# More reliable than batch file for OVMF paths

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Booting guideXOS in QEMU" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Change to project directory
Set-Location "D:\devgitlab\guideXOS\guideXOS.UEFI"

# Verify files exist
Write-Host "Checking files..." -ForegroundColor Yellow

$filesOK = $true

if (-not (Test-Path "OVMF.fd")) {
    Write-Host "  ? OVMF.fd not found!" -ForegroundColor Red
    $filesOK = $false
} else {
    Write-Host "  ? OVMF.fd found" -ForegroundColor Green
}

if (-not (Test-Path "ESP\EFI\BOOT\BOOTX64.EFI")) {
    Write-Host "  ? BOOTX64.EFI not found!" -ForegroundColor Red
    $filesOK = $false
} else {
    Write-Host "  ? BOOTX64.EFI found" -ForegroundColor Green
}

if (-not (Test-Path "ESP\kernel.elf")) {
    Write-Host "  ? kernel.elf not found!" -ForegroundColor Red
    $filesOK = $false
} else {
    # Verify ELF
    $bytes = [System.IO.File]::ReadAllBytes("ESP\kernel.elf")
    if ($bytes[0] -eq 0x7F -and $bytes[1] -eq 0x45 -and $bytes[2] -eq 0x4C -and $bytes[3] -eq 0x46) {
        Write-Host "  ? kernel.elf found (valid ELF)" -ForegroundColor Green
    } else {
        Write-Host "  ? kernel.elf is NOT valid ELF!" -ForegroundColor Red
        Write-Host "     Run: .\convert_kernel_manual.ps1" -ForegroundColor Yellow
        $filesOK = $false
    }
}

if (-not (Test-Path "ESP\ramdisk.img")) {
    Write-Host "  ??  ramdisk.img not found (optional)" -ForegroundColor Yellow
} else {
    Write-Host "  ? ramdisk.img found" -ForegroundColor Green
}

Write-Host ""

if (-not $filesOK) {
    Write-Host "? Missing required files!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Run these commands:" -ForegroundColor Yellow
    Write-Host "  1. .\build.ps1" -ForegroundColor Cyan
    Write-Host "  2. .\convert_kernel_manual.ps1" -ForegroundColor Cyan
    Write-Host ""
    pause
    exit 1
}

Write-Host "Starting QEMU..." -ForegroundColor Green
Write-Host "Press Ctrl+C in this window to exit QEMU" -ForegroundColor Yellow
Write-Host ""

# Launch QEMU with pflash (most reliable method)
try {
    & "C:\Program Files\qemu\qemu-system-x86_64.exe" `
        -drive if=pflash,format=raw,readonly=on,file=OVMF.fd `
        -drive file=fat:rw:ESP,format=raw `
        -m 1024M `
        -serial stdio `
        -name "guideXOS" -no-reboot `
        -display sdl
} catch {
    Write-Host ""
    Write-Host "? QEMU failed to start!" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure QEMU is installed at: C:\Program Files\qemu\" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "QEMU exited." -ForegroundColor Cyan
Write-Host ""
pause
