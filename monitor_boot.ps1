#!/usr/bin/env pwsh
# Real-time QEMU Boot Monitor for guideXOS
# Watches serial output and detects hang conditions

param(
    [int]$TimeoutSeconds = 30,
    [switch]$Verbose
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   guideXOS Real-Time Boot Monitor" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Timeout: $TimeoutSeconds seconds" -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop monitoring" -ForegroundColor Yellow
Write-Host ""

# Start QEMU in background
$qemuPath = "qemu-system-x86_64.exe"
$qemuArgs = @(
    "-bios", "OVMF.fd",
    "-hda", "fat:rw:ESP",
    "-m", "1024M",
    "-serial", "file:serial_output.txt",
    "-display", "gtk",
    "-d", "cpu_reset",
    "-no-reboot",
    "-no-shutdown"
)

Write-Host "[START] Launching QEMU..." -ForegroundColor Green

# Clear old serial output
if (Test-Path "serial_output.txt") {
    Remove-Item "serial_output.txt" -Force
}
New-Item "serial_output.txt" -ItemType File -Force | Out-Null

# Start QEMU process
$qemuProcess = Start-Process -FilePath $qemuPath -ArgumentList $qemuArgs -PassThru -NoNewWindow

Write-Host "[QEMU] PID: $($qemuProcess.Id)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Monitoring serial output..." -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray

# Track important milestones
$milestones = @{
    "KMAIN!" = $false
    "[KERN]" = $false
    "[KMAIN]" = $false
    "PROG" = $false
    "VMSKIP" = $false
    "USBSK" = $false
    "IMG" = $false
    "FNT" = $false
    "WM[" = $false
    "DS[" = $false
    "=LOOP=" = $false
    "SMAIN" = $false
}

$lastOutput = ""
$lastChangeTime = Get-Date
$hangDetected = $false
$bootComplete = $false

try {
    while (-not $qemuProcess.HasExited) {
        Start-Sleep -Milliseconds 100
        
        if (Test-Path "serial_output.txt") {
            $currentOutput = Get-Content "serial_output.txt" -Raw -ErrorAction SilentlyContinue
            
            if ($currentOutput -and $currentOutput -ne $lastOutput) {
                # New output detected
                $newContent = $currentOutput.Substring($lastOutput.Length)
                Write-Host $newContent -NoNewline -ForegroundColor White
                
                $lastOutput = $currentOutput
                $lastChangeTime = Get-Date
                
                # Check for milestones
                foreach ($milestone in $milestones.Keys) {
                    if ($currentOutput -match [regex]::Escape($milestone) -and -not $milestones[$milestone]) {
                        $milestones[$milestone] = $true
                        Write-Host ""
                        Write-Host "[MILESTONE] $milestone reached!" -ForegroundColor Green
                    }
                }
                
                # Check for boot completion
                if ($currentOutput -match "=LOOP=") {
                    Write-Host ""
                    Write-Host "========================================" -ForegroundColor Green
                    Write-Host "   BOOT COMPLETE - Entered Main Loop!" -ForegroundColor Green
                    Write-Host "========================================" -ForegroundColor Green
                    $bootComplete = $true
                    break
                }
            }
            
            # Check for hang (no output for timeout period)
            $timeSinceLastChange = (Get-Date) - $lastChangeTime
            if ($timeSinceLastChange.TotalSeconds -gt $TimeoutSeconds) {
                Write-Host ""
                Write-Host "========================================" -ForegroundColor Red
                Write-Host "   HANG DETECTED!" -ForegroundColor Red
                Write-Host "========================================" -ForegroundColor Red
                Write-Host "No output for $TimeoutSeconds seconds" -ForegroundColor Yellow
                Write-Host ""
                Write-Host "Last output received:" -ForegroundColor Yellow
                Write-Host $lastOutput.Substring([Math]::Max(0, $lastOutput.Length - 200)) -ForegroundColor Gray
                Write-Host ""
                Write-Host "Milestones reached:" -ForegroundColor Yellow
                foreach ($milestone in $milestones.Keys | Sort-Object) {
                    $status = if ($milestones[$milestone]) { "?" } else { "?" }
                    $color = if ($milestones[$milestone]) { "Green" } else { "Red" }
                    Write-Host "  $status $milestone" -ForegroundColor $color
                }
                $hangDetected = $true
                break
            }
        }
    }
}
finally {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   Monitoring Complete" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    # Summary
    Write-Host ""
    Write-Host "Boot Status:" -ForegroundColor Yellow
    if ($bootComplete) {
        Write-Host "  Status: COMPLETE ?" -ForegroundColor Green
    }
    elseif ($hangDetected) {
        Write-Host "  Status: HUNG ?" -ForegroundColor Red
    }
    else {
        Write-Host "  Status: TERMINATED" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "Milestones Reached:" -ForegroundColor Yellow
    foreach ($milestone in $milestones.Keys | Sort-Object) {
        $status = if ($milestones[$milestone]) { "?" } else { "?" }
        $color = if ($milestones[$milestone]) { "Green" } else { "Red" }
        Write-Host "  $status $milestone" -ForegroundColor $color
    }
    
    # Kill QEMU if still running
    if (-not $qemuProcess.HasExited) {
        Write-Host ""
        Write-Host "Terminating QEMU..." -ForegroundColor Yellow
        Stop-Process -Id $qemuProcess.Id -Force
    }
    
    # Save detailed log
    $logFile = "boot_monitor_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
    if (Test-Path "serial_output.txt") {
        Copy-Item "serial_output.txt" $logFile
        Write-Host ""
        Write-Host "Full boot log saved to: $logFile" -ForegroundColor Cyan
    }
    
    Write-Host ""
}
