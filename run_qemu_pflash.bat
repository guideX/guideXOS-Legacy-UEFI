@echo off
REM Boot guideXOS in QEMU with UEFI firmware (using pflash method)

echo ========================================
echo    Booting guideXOS in QEMU
echo    (Using pflash method)
echo ========================================
echo.

REM Change to the directory where this script lives.
cd /d "%~dp0"

REM Verify files exist
echo Checking files...
if not exist "OVMF.fd" (
    echo ERROR: OVMF.fd not found!
    pause
    exit /b 1
)
echo   [OK] OVMF.fd found

if not exist "ESP\EFI\BOOT\BOOTX64.EFI" (
    echo ERROR: ESP\EFI\BOOT\BOOTX64.EFI not found!
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

REM Create a writable copy of OVMF for variables
if not exist "OVMF_VARS.fd" (
    echo Creating OVMF_VARS.fd...
    copy /Y OVMF.fd OVMF_VARS.fd > nul
)

echo.
echo Starting QEMU...
echo Press Ctrl+C in this window to exit QEMU
echo Serial output will appear below:
echo ----------------------------------------
echo.

REM Launch QEMU using pflash (more reliable)
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
