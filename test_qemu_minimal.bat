@echo off
REM Quick QEMU test for guideXOS

echo Testing guideXOS boot...
cd /d D:\devgitlab\guidexos\guideXOS.UEFI

REM Kill any existing QEMU processes
taskkill /f /im qemu-system-x86_64.exe >nul 2>&1

REM Start QEMU with timeout
timeout /t 3 /nobreak

qemu-system-x86_64.exe -bios OVMF.fd -hda fat:rw:ESP -m 1024M -serial stdio -no-reboot -display none 2>&1 | head -200

echo.
echo Test complete.
pause
