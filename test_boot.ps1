# Quick Test: Boot guideXOS in QEMU
# Run this to test your OS after kernel conversion

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Testing guideXOS Boot" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Set-Location $PSScriptRoot

# Verify all required files
$allGood = $true

if (-not (Test-Path "OVMF.fd")) {
    Write-Host "? OVMF.fd not found!" -ForegroundColor Red
    $allGood = $false
} else {
    Write-Host "? OVMF.fd found" -ForegroundColor Green
}

if (-not (Test-Path "ESP\EFI\BOOT\BOOTX64.EFI")) {
    Write-Host "? BOOTX64.EFI not found!" -ForegroundColor Red
    $allGood = $false
} else {
    Write-Host "? BOOTX64.EFI found" -ForegroundColor Green
}

if (-not (Test-Path "ESP\kernel.elf")) {
    Write-Host "? kernel.elf not found!" -ForegroundColor Red
    $allGood = $false
} else {
    # Verify it's ELF
    $bytes = [System.IO.File]::ReadAllBytes("ESP\kernel.elf")
    if ($bytes[0] -eq 0x7F -and $bytes[1] -eq 0x45 -and $bytes[2] -eq 0x4C -and $bytes[3] -eq 0x46) {
        Write-Host "? kernel.elf found (valid ELF)" -ForegroundColor Green
    } else {
        Write-Host "? kernel.elf is NOT a valid ELF file!" -ForegroundColor Red
        $allGood = $false
    }
}

if (-not (Test-Path "ESP\ramdisk.img")) {
    Write-Host "??  ramdisk.img not found (optional)" -ForegroundColor Yellow
} else {
    Write-Host "? ramdisk.img found" -ForegroundColor Green
}

Write-Host ""

if (-not $allGood) {
    Write-Host "? Missing required files. Run: .\build.ps1" -ForegroundColor Red
    exit 1
}

Write-Host "Starting QEMU..." -ForegroundColor Cyan
Write-Host "Press Ctrl+C to exit" -ForegroundColor Yellow
Write-Host ""

# Launch QEMU
& "C:\Program Files\qemu\qemu-system-x86_64.exe" `
    -L . `
    -bios OVMF.fd `
    -drive file=fat:rw:ESP,format=raw `
    -m 1024M `
    -serial stdio `
    -name "guideXOS"

Write-Host ""
Write-Host "QEMU exited." -ForegroundColor Cyan
