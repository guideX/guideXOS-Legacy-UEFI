# Update breadcrumb styling in all Wiki files
$files = Get-ChildItem -Path "..\guideXOS.com\Views\Wiki\*.cshtml"

foreach ($file in $files) {
    Write-Host "Processing: $($file.Name)"
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    
    # Remove inline breadcrumb styling
    $content = $content -replace '<ol class="breadcrumb" style="background: transparent; padding: 0;">', '<ol class="breadcrumb">'
    $content = $content -replace '<li class="breadcrumb-item"><a asp-area="" asp-controller="Wiki" asp-action="Index" style="color: #00d9ff;">Wiki</a></li>', '<li class="breadcrumb-item"><a asp-area="" asp-controller="Wiki" asp-action="Index">Wiki</a></li>'
    $content = $content -replace 'asp-action="([^"]+)" style="color: #00d9ff;">', 'asp-action="$1">'
    
    [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.Encoding]::UTF8)
    Write-Host "Updated: $($file.Name)" -ForegroundColor Green
}

Write-Host "`nAll breadcrumb styles updated successfully!" -ForegroundColor Cyan
