#!/usr/bin/env pwsh
# Watchdog Timer for guideXOS Boot
# Detects exact hang location by monitoring serial markers

param(
    [int]$WatchdogSeconds = 5,  # Timeout between markers
    [switch]$AutoKill
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   guideXOS Boot Watchdog" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Watchdog timeout: $WatchdogSeconds seconds between markers" -ForegroundColor Yellow
Write-Host ""

# Expected boot sequence with markers
$expectedMarkers = @(
    @{Name="Kernel Entry"; Pattern="KMAIN!"; Phase="EntryPoint"},
    @{Name="Kernel Main Call"; Pattern="\[KERN\]"; Phase="EntryPoint"},
    @{Name="Program.KMain Entry"; Pattern="\[KMAIN\]"; Phase="EntryPoint"},
    @{Name="Program Start"; Pattern="PROG"; Phase="Program.KMain"},
    @{Name="Animator Init"; Pattern="ANIM"; Phase="Program.KMain"},
    @{Name="Animator Done"; Pattern="anim"; Phase="Program.KMain"},
    @{Name="Keyboard Start"; Pattern="KBD"; Phase="Program.KMain"},
    @{Name="Keyboard Done"; Pattern="kbd"; Phase="Program.KMain"},
    @{Name="Mouse Start"; Pattern="MSE"; Phase="Program.KMain"},
    @{Name="Mouse Done"; Pattern="mse"; Phase="Program.KMain"},
    @{Name="VMware Skip"; Pattern="VMSKIP"; Phase="Program.KMain"},
    @{Name="USB Skip"; Pattern="USBSK"; Phase="Program.KMain"},
    @{Name="Image Load Start"; Pattern="IMG"; Phase="Program.KMain"},
    @{Name="Image 1"; Pattern="IMG.*1"; Phase="Program.KMain"},
    @{Name="Image 2"; Pattern="IMG.*2"; Phase="Program.KMain"},
    @{Name="Image 3"; Pattern="IMG.*3"; Phase="Program.KMain"},
    @{Name="Images Done"; Pattern="img"; Phase="Program.KMain"},
    @{Name="Font Start"; Pattern="FNT"; Phase="Program.KMain"},
    @{Name="Font Done"; Pattern="fnt"; Phase="Program.KMain"},
    @{Name="WindowManager Start"; Pattern="WM\["; Phase="Program.KMain"},
    @{Name="WindowManager Done"; Pattern="\]wm"; Phase="Program.KMain"},
    @{Name="Desktop Start"; Pattern="DS\["; Phase="Program.KMain"},
    @{Name="Desktop Done"; Pattern="\]ds"; Phase="Program.KMain"},
    @{Name="Subsystems Start"; Pattern="SUB\["; Phase="Program.KMain"},
    @{Name="Subsystems Done"; Pattern="\]sub"; Phase="Program.KMain"},
    @{Name="Config Start"; Pattern="CFG\["; Phase="Program.KMain"},
    @{Name="Config Done"; Pattern="\]cfg"; Phase="Program.KMain"},
    @{Name="Main Loop Entry"; Pattern="=LOOP="; Phase="Program.KMain"},
    @{Name="SMain Start"; Pattern="SMAIN"; Phase="SMain"},
    @{Name="Triple Buffer"; Pattern="SMAIN.*TB"; Phase="SMain"},
    @{Name="Wallpaper"; Pattern="SMAIN.*TB.*WL"; Phase="SMain"}
)

# Start monitoring
$serialFile = "serial_output.txt"
if (Test-Path $serialFile) {
    Remove-Item $serialFile -Force
}

Write-Host "Starting QEMU with watchdog monitoring..." -ForegroundColor Green
Write-Host ""

# Start QEMU
$qemuPath = "qemu-system-x86_64.exe"
$qemuArgs = @(
    "-bios", "OVMF.fd",
    "-hda", "fat:rw:ESP",
    "-m", "1024M",
    "-serial", "file:$serialFile",
    "-display", "gtk"
)

$qemu = Start-Process -FilePath $qemuPath -ArgumentList $qemuArgs -PassThru -NoNewWindow

$currentMarkerIndex = 0
$lastContent = ""
$lastMarkerTime = Get-Date
$hangLocation = $null

try {
    while (-not $qemu.HasExited -and $currentMarkerIndex -lt $expectedMarkers.Count) {
        Start-Sleep -Milliseconds 200
        
        if (Test-Path $serialFile) {
            $content = Get-Content $serialFile -Raw -ErrorAction SilentlyContinue
            
            if ($content -and $content -ne $lastContent) {
                # Check if we've reached the next expected marker
                $currentMarker = $expectedMarkers[$currentMarkerIndex]
                
                if ($content -match $currentMarker.Pattern) {
                    $elapsed = ((Get-Date) - $lastMarkerTime).TotalSeconds
                    Write-Host "[?] " -NoNewline -ForegroundColor Green
                    Write-Host "$($currentMarker.Name) " -NoNewline -ForegroundColor White
                    Write-Host "($($currentMarker.Phase)) " -NoNewline -ForegroundColor Gray
                    Write-Host "+$([Math]::Round($elapsed, 2))s" -ForegroundColor Cyan
                    
                    $currentMarkerIndex++
                    $lastMarkerTime = Get-Date
                }
                
                $lastContent = $content
            }
            
            # Check for watchdog timeout
            $timeSinceLastMarker = ((Get-Date) - $lastMarkerTime).TotalSeconds
            if ($timeSinceLastMarker -gt $WatchdogSeconds -and $currentMarkerIndex -gt 0) {
                $hangLocation = $expectedMarkers[$currentMarkerIndex - 1]
                Write-Host ""
                Write-Host "========================================" -ForegroundColor Red
                Write-Host "   WATCHDOG: HANG DETECTED!" -ForegroundColor Red
                Write-Host "========================================" -ForegroundColor Red
                Write-Host ""
                Write-Host "Last successful marker:" -ForegroundColor Yellow
                Write-Host "  Name: $($hangLocation.Name)" -ForegroundColor White
                Write-Host "  Phase: $($hangLocation.Phase)" -ForegroundColor White
                Write-Host "  Pattern: $($hangLocation.Pattern)" -ForegroundColor Gray
                Write-Host ""
                Write-Host "Expected next marker:" -ForegroundColor Yellow
                $nextMarker = $expectedMarkers[$currentMarkerIndex]
                Write-Host "  Name: $($nextMarker.Name)" -ForegroundColor White
                Write-Host "  Phase: $($nextMarker.Phase)" -ForegroundColor White
                Write-Host "  Pattern: $($nextMarker.Pattern)" -ForegroundColor Gray
                Write-Host ""
                Write-Host "Timeout: $([Math]::Round($timeSinceLastMarker, 2))s (limit: $WatchdogSeconds s)" -ForegroundColor Red
                Write-Host ""
                
                # Show recent serial output
                Write-Host "Recent serial output (last 300 chars):" -ForegroundColor Yellow
                Write-Host "----------------------------------------" -ForegroundColor Gray
                $recentOutput = if ($content.Length -gt 300) { $content.Substring($content.Length - 300) } else { $content }
                Write-Host $recentOutput -ForegroundColor White
                Write-Host "----------------------------------------" -ForegroundColor Gray
                
                if ($AutoKill) {
                    Write-Host ""
                    Write-Host "Auto-killing QEMU..." -ForegroundColor Yellow
                    Stop-Process -Id $qemu.Id -Force
                }
                break
            }
        }
    }
    
    # Check if we completed boot
    if ($currentMarkerIndex -ge $expectedMarkers.Count) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "   ALL MARKERS REACHED!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Boot sequence complete!" -ForegroundColor Green
    }
}
finally {
    if (-not $qemu.HasExited) {
        Write-Host ""
        Write-Host "Terminating QEMU..." -ForegroundColor Yellow
        Stop-Process -Id $qemu.Id -Force -ErrorAction SilentlyContinue
    }
    
    # Summary
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   Boot Sequence Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Progress: $currentMarkerIndex / $($expectedMarkers.Count) markers" -ForegroundColor Yellow
    Write-Host "Percentage: $([Math]::Round(($currentMarkerIndex / $expectedMarkers.Count) * 100, 1))%" -ForegroundColor Cyan
    Write-Host ""
    
    if ($hangLocation) {
        Write-Host "Hang detected after: $($hangLocation.Name)" -ForegroundColor Red
        Write-Host "Phase: $($hangLocation.Phase)" -ForegroundColor Yellow
    }
}
