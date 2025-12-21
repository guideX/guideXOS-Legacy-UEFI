@echo off
REM Boot guideXOS in QEMU with UEFI firmware

echo ========================================
echo    Booting guideXOS in QEMU
echo ========================================
echo.

REM Change to the guideXOS.UEFI directory (where this script lives)
cd /d D:\devgitlab\guideXOS\guideXOS.UEFI

REM Verify files exist
echo Checking files...
if not exist "OVMF.fd" (
    REM Try to copy from guideXOS folder if it exists there
    if exist "D:\devgitlab\guideXOS\guideXOS\OVMF.fd" (
        copy "D:\devgitlab\guideXOS\guideXOS\OVMF.fd" "OVMF.fd"
        echo   [OK] Copied OVMF.fd from guideXOS folder
    ) else (
        echo ERROR: OVMF.fd not found!
        pause
        exit /b 1
    )
) else (
    echo   [OK] OVMF.fd found
)

if not exist "ESP\EFI\BOOT\BOOTX64.EFI" (
    echo ERROR: ESP\EFI\BOOT\BOOTX64.EFI not found!
    echo Run build.ps1 first to build the bootloader.
    pause
    exit /b 1
)
echo   [OK] BOOTX64.EFI found

if not exist "ESP\kernel.elf" (
    echo ERROR: ESP\kernel.elf not found!
    pause
    exit /b 1
)
echo   [OK] kernel.elf found

if not exist "ESP\ramdisk.img" (
    echo WARNING: ESP\ramdisk.img not found - continuing anyway
) else (
    echo   [OK] ramdisk.img found
)

echo.
echo Starting QEMU...
echo Press Ctrl+C in this window to exit QEMU
echo Serial output will appear below:
echo ----------------------------------------
echo.

REM Launch QEMU using pflash for UEFI firmware (more reliable)
"C:\Program Files\qemu\qemu-system-x86_64.exe" ^
    -drive if=pflash,format=raw,readonly=on,file=OVMF.fd ^
    -drive file=fat:rw:ESP,format=raw ^
    -m 1024M ^
    -serial stdio ^
    -no-reboot ^
    -name "guideXOS"

echo.
echo ----------------------------------------
echo QEMU exited.
pause
