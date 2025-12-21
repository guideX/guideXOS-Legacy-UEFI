#!/bin/bash
# WSL Helper Script for Kernel Conversion
# This script runs inside WSL to convert the kernel from PE to ELF format

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== guideXOS Kernel Converter (WSL) ===${NC}"

# Check for objcopy
if ! command -v objcopy &> /dev/null; then
    echo -e "${RED}Error: objcopy not found${NC}"
    echo "Install with: sudo apt install binutils"
    exit 1
fi

echo -e "${GREEN}? objcopy found${NC}"

# Get Windows path from argument or use default
if [ -z "$1" ]; then
    # Default path (adjust if needed)
    WIN_PATH="/mnt/d/devgitlab/guideXOS/guideXOS"
else
    WIN_PATH="$1"
fi

if [ ! -d "$WIN_PATH" ]; then
    echo -e "${RED}Error: Directory not found: $WIN_PATH${NC}"
    echo "Usage: $0 [windows_repo_path]"
    echo "Example: $0 /mnt/d/devgitlab/guideXOS/guideXOS"
    exit 1
fi

echo -e "${GREEN}Working directory: $WIN_PATH${NC}"

# Paths
PE_PATH="$WIN_PATH/guideXOS/bin/Release/net9.0/guidexos-x64/publish/kernel.bin"
ELF_PATH="$WIN_PATH/kernel.elf"

# Check if PE file exists
if [ ! -f "$PE_PATH" ]; then
    echo -e "${RED}Error: kernel.bin not found at: $PE_PATH${NC}"
    echo "Build the kernel first with: dotnet publish -c Release -r guidexos-x64"
    exit 1
fi

echo -e "${GREEN}? Found kernel.bin${NC}"

# Get file size
SIZE=$(stat -f "%z" "$PE_PATH" 2>/dev/null || stat -c "%s" "$PE_PATH")
SIZE_MB=$(echo "scale=2; $SIZE / 1048576" | bc)
echo -e "  Size: ${SIZE_MB} MB"

# Convert PE to ELF
echo -e "${YELLOW}Converting PE to ELF64...${NC}"
objcopy --input-target=pe-x86-64 \
        --output-target=elf64-x86-64 \
        "$PE_PATH" \
        "$ELF_PATH"

if [ $? -ne 0 ]; then
    echo -e "${RED}Error: Conversion failed${NC}"
    exit 1
fi

# Verify ELF file
if [ ! -f "$ELF_PATH" ]; then
    echo -e "${RED}Error: kernel.elf not created${NC}"
    exit 1
fi

echo -e "${GREEN}? Conversion successful${NC}"

# Get ELF file info
SIZE_ELF=$(stat -f "%z" "$ELF_PATH" 2>/dev/null || stat -c "%s" "$ELF_PATH")
SIZE_ELF_MB=$(echo "scale=2; $SIZE_ELF / 1048576" | bc)
echo -e "  Output: $ELF_PATH"
echo -e "  Size: ${SIZE_ELF_MB} MB"

# Verify it's actually ELF
FILE_TYPE=$(file "$ELF_PATH")
if [[ $FILE_TYPE == *"ELF 64-bit"* ]]; then
    echo -e "${GREEN}? Verified: ELF 64-bit format${NC}"
else
    echo -e "${RED}Warning: File may not be valid ELF${NC}"
    echo -e "  $FILE_TYPE"
fi

echo -e "${GREEN}=== Conversion Complete ===${NC}"
echo -e "Next steps:"
echo -e "  1. Return to Windows PowerShell"
echo -e "  2. Run: .\\build.ps1 -SkipKernel -SkipConversion"
