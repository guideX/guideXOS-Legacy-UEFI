# Manual Kernel Converter (PE to ELF)
# Use this if objcopy is not available
# This creates a minimal ELF wrapper around the PE kernel

param(
    [string]$InputPE = "guideXOS\bin\Release\net9.0\guidexos-x64\publish\kernel.bin",
    [string]$OutputELF = "ESP\kernel.elf"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Manual PE?ELF Converter" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $InputPE)) {
    Write-Host "? Input file not found: $InputPE" -ForegroundColor Red
    exit 1
}

Write-Host "Input:  $InputPE" -ForegroundColor Yellow
Write-Host "Output: $OutputELF" -ForegroundColor Yellow
Write-Host ""

# Read PE file
$peData = [System.IO.File]::ReadAllBytes($InputPE)
$peSize = $peData.Length
Write-Host "PE file size: $peSize bytes" -ForegroundColor Cyan

# Create minimal ELF64 header
$elf = New-Object System.Collections.Generic.List[byte]

# ELF Header (64 bytes)
# e_ident (16 bytes)
$elf.Add(0x7F); $elf.Add(0x45); $elf.Add(0x4C); $elf.Add(0x46)  # Magic: 7F 45 4C 46 (ELF)
$elf.Add(0x02)  # 64-bit
$elf.Add(0x01)  # Little endian
$elf.Add(0x01)  # ELF version
$elf.Add(0x00)  # Generic ABI
for ($i = 0; $i -lt 8; $i++) { $elf.Add(0x00) }  # Padding

# e_type (2 bytes) - ET_EXEC (executable)
$elf.Add(0x02); $elf.Add(0x00)

# e_machine (2 bytes) - EM_X86_64
$elf.Add(0x3E); $elf.Add(0x00)

# e_version (4 bytes)
$elf.Add(0x01); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)

# e_entry (8 bytes) - Entry point at 0x100000 (1MB mark - where bootloader loads it)
$elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x10); $elf.Add(0x00)
$elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)

# e_phoff (8 bytes) - Program header offset (right after ELF header = 64)
$elf.Add(0x40); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)
$elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)

# e_shoff (8 bytes) - Section header offset (0 = none)
for ($i = 0; $i -lt 8; $i++) { $elf.Add(0x00) }

# e_flags (4 bytes)
for ($i = 0; $i -lt 4; $i++) { $elf.Add(0x00) }

# e_ehsize (2 bytes) - ELF header size (64)
$elf.Add(0x40); $elf.Add(0x00)

# e_phentsize (2 bytes) - Program header entry size (56 for 64-bit)
$elf.Add(0x38); $elf.Add(0x00)

# e_phnum (2 bytes) - Number of program headers (1)
$elf.Add(0x01); $elf.Add(0x00)

# e_shentsize (2 bytes) - Section header entry size (0)
$elf.Add(0x00); $elf.Add(0x00)

# e_shnum (2 bytes) - Number of section headers (0)
$elf.Add(0x00); $elf.Add(0x00)

# e_shstrndx (2 bytes) - Section header string table index (0)
$elf.Add(0x00); $elf.Add(0x00)

Write-Host "? Created ELF header (64 bytes)" -ForegroundColor Green

# Program Header (56 bytes for 64-bit)
# p_type (4 bytes) - PT_LOAD (1)
$elf.Add(0x01); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)

# p_flags (4 bytes) - PF_X | PF_W | PF_R (7)
$elf.Add(0x07); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)

# p_offset (8 bytes) - Offset in file (120 = after headers)
$elf.Add(0x78); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)
$elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)

# p_vaddr (8 bytes) - Virtual address (0x100000)
$elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x10); $elf.Add(0x00)
$elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)

# p_paddr (8 bytes) - Physical address (same)
$elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x10); $elf.Add(0x00)
$elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)

# p_filesz (8 bytes) - Size in file
$filesz = $peSize
$elf.Add($filesz -band 0xFF); $elf.Add(($filesz -shr 8) -band 0xFF)
$elf.Add(($filesz -shr 16) -band 0xFF); $elf.Add(($filesz -shr 24) -band 0xFF)
for ($i = 0; $i -lt 4; $i++) { $elf.Add(0x00) }

# p_memsz (8 bytes) - Size in memory (same + extra for BSS)
$memsz = $peSize + 0x100000  # Extra 1MB for BSS
$elf.Add($memsz -band 0xFF); $elf.Add(($memsz -shr 8) -band 0xFF)
$elf.Add(($memsz -shr 16) -band 0xFF); $elf.Add(($memsz -shr 24) -band 0xFF)
for ($i = 0; $i -lt 4; $i++) { $elf.Add(0x00) }

# p_align (8 bytes) - Alignment (4KB)
$elf.Add(0x00); $elf.Add(0x10); $elf.Add(0x00); $elf.Add(0x00)
$elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00); $elf.Add(0x00)

Write-Host "? Created program header (56 bytes)" -ForegroundColor Green

# Append PE data
$elf.AddRange($peData)

Write-Host "? Appended PE data ($peSize bytes)" -ForegroundColor Green

# Ensure output directory exists
$outputDir = Split-Path $OutputELF -Parent
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Write ELF file
[System.IO.File]::WriteAllBytes($OutputELF, $elf.ToArray())

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "? Conversion Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output file: $OutputELF" -ForegroundColor Cyan
Write-Host "Total size:  $($elf.Count) bytes" -ForegroundColor Cyan
Write-Host ""

# Verify
$verify = [System.IO.File]::ReadAllBytes($OutputELF)
if ($verify[0] -eq 0x7F -and $verify[1] -eq 0x45 -and $verify[2] -eq 0x4C -and $verify[3] -eq 0x46) {
    Write-Host "? Valid ELF header detected!" -ForegroundColor Green
} else {
    Write-Host "? ELF header verification failed!" -ForegroundColor Red
}

Write-Host ""
Write-Host "??  WARNING: This is a BASIC ELF wrapper" -ForegroundColor Yellow
Write-Host "   For production, use proper objcopy conversion" -ForegroundColor Yellow
Write-Host ""
