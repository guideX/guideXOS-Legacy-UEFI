@echo off
REM Build and run guideXOS in one command

echo ========================================
echo    Build and Run guideXOS
echo ========================================
echo.

echo [1/2] Building kernel...
call build.ps1
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo [2/2] Running in QEMU...
call run_qemu.bat

