# Fix Unicode characters in Wiki files
$files = Get-ChildItem -Path "..\guideXOS.com\Views\Wiki\*.cshtml"

foreach ($file in $files) {
    Write-Host "Processing: $($file.Name)"
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    
    # Replace Unicode characters with HTML entities
    $content = $content -replace '?', '&#10003;'
    $content = $content -replace '?', '&#10007;'
    $content = $content -replace '?', '&#9888;'
    $content = $content -replace '??', '&#128161;'
    $content = $content -replace '×', '&#215;'
    $content = $content -replace '?', '&#8594;'
    $content = $content -replace '?', '&#9500;'
    $content = $content -replace '?', '&#9472;'
    $content = $content -replace '?', '&#9492;'
    $content = $content -replace '?', '&#8968;'
    $content = $content -replace '?', '&#8969;'
    $content = $content -replace '?', '&#10233;'
    $content = $content -replace '?', '&#8595;'
    $content = $content -replace '?', '&#10060;'
    $content = $content -replace '?', '&#9989;'
    $content = $content -replace '?', '&#11014;'
    $content = $content -replace '?', '&#11015;'
    $content = $content -replace '?', '&#11088;'
    $content = $content -replace '??', '&#128274;'
    $content = $content -replace '??', '&#128275;'
    $content = $content -replace '??', '&#128640;'
    $content = $content -replace '?', '&#9889;'
    $content = $content -replace '??', '&#128221;'
    $content = $content -replace '??', '&#128193;'
    $content = $content -replace '??', '&#128194;'
    $content = $content -replace '??', '&#128190;'
    $content = $content -replace '??', '&#128421;'
    $content = $content -replace '?', '&#9000;'
    $content = $content -replace '??', '&#128433;'
    $content = $content -replace '??', '&#128202;'
    $content = $content -replace '??', '&#128200;'
    $content = $content -replace '??', '&#128201;'
    $content = $content -replace '??', '&#128295;'
    $content = $content -replace '??', '&#128296;'
    $content = $content -replace '??', '&#128736;'
    $content = $content -replace '??', '&#128027;'
    $content = $content -replace '?', '&#9881;'
    $content = $content -replace '?', '&#8593;'
    $content = $content -replace '?', '&#8592;'
    $content = $content -replace '?', '&#8596;'
    $content = $content -replace '?', '&#8597;'
    $content = $content -replace '?', '&#8658;'
    $content = $content -replace '?', '&#8656;'
    $content = $content -replace '?', '&#8660;'
    $content = $content -replace '?', '&#8734;'
    $content = $content -replace '?', '&#8776;'
    $content = $content -replace '?', '&#8800;'
    $content = $content -replace '?', '&#8804;'
    $content = $content -replace '?', '&#8805;'
    $content = $content -replace '±', '&#177;'
    $content = $content -replace '°', '&#176;'
    $content = $content -replace '•', '&#8226;'
    $content = $content -replace '?', '&#9702;'
    $content = $content -replace '?', '&#9642;'
    $content = $content -replace '?', '&#9643;'
    $content = $content -replace '?', '&#9658;'
    $content = $content -replace '?', '&#9668;'
    $content = $content -replace '?', '&#9650;'
    $content = $content -replace '?', '&#9660;'
    
    [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.Encoding]::UTF8)
    Write-Host "Fixed: $($file.Name)" -ForegroundColor Green
}

Write-Host "`nAll files processed successfully!" -ForegroundColor Cyan
