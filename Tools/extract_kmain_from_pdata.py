#!/usr/bin/env python3
"""extract_kmain_from_pdata.py

Extract the actual KMain function address from PE PDATA (exception data) section.
The PDATA section contains accurate function boundaries, unlike the map file symbols.
"""

import struct
import sys
from pathlib import Path


def u16(b: bytes, off: int) -> int:
    return struct.unpack_from("<H", b, off)[0]


def u32(b: bytes, off: int) -> int:
    return struct.unpack_from("<I", b, off)[0]


def u64(b: bytes, off: int) -> int:
    return struct.unpack_from("<Q", b, off)[0]


def find_kmain_in_pdata(pe_path: Path) -> int | None:
    """Find KMain function start address from PE PDATA section."""
    pe = pe_path.read_bytes()
    
    if pe[:2] != b"MZ":
        print("Error: Not a PE file")
        return None
    
    pe_off = u32(pe, 0x3C)
    if pe[pe_off:pe_off + 4] != b"PE\x00\x00":
        print("Error: Invalid PE header")
        return None
    
    coff_off = pe_off + 4
    number_of_sections = u16(pe, coff_off + 2)
    size_of_optional_header = u16(pe, coff_off + 16)
    
    opt_off = coff_off + 20
    image_base = u64(pe, opt_off + 24)
    
    # Find exception directory (index 3 in data directories)
    # Data directories start at opt_off + 112 for PE32+
    exception_dir_rva = u32(pe, opt_off + 112 + 3 * 8)
    exception_dir_size = u32(pe, opt_off + 112 + 3 * 8 + 4)
    
    if exception_dir_rva == 0 or exception_dir_size == 0:
        print("Error: No exception directory found")
        return None
    
    # Find .pdata section to get raw offset
    sec_table_off = opt_off + size_of_optional_header
    pdata_raw_ptr = 0
    
    for i in range(number_of_sections):
        off = sec_table_off + i * 40
        name = pe[off:off + 8].split(b"\x00", 1)[0].decode("ascii", errors="replace")
        vaddr = u32(pe, off + 12)
        raw_ptr = u32(pe, off + 20)
        
        if name == ".pdata" or vaddr == exception_dir_rva:
            pdata_raw_ptr = raw_ptr
            break
    
    if pdata_raw_ptr == 0:
        print("Error: Could not find .pdata section")
        return None
    
    # Find symbol table in .rdata for function names (simplified)
    # Each RUNTIME_FUNCTION entry is 12 bytes: BeginAddress, EndAddress, UnwindInfoAddress
    num_entries = exception_dir_size // 12
    
    # We need to find function names. Look for "KMain" in the PE
    # Search for the string "KMain\0" in the file
    kmain_pattern = b"KMain\x00"
    kmain_str_offset = pe.find(kmain_pattern)
    
    if kmain_str_offset == -1:
        print("Warning: KMain string not found, searching by proximity to map symbol...")
        # Fall back to searching near the map file's KMain address
        return None
    
    # The string table entries point to function names
    # We need to correlate PDATA entries with symbol names
    # This is complex - let's use a simpler heuristic:
    # Find the PDATA entry whose BeginAddress matches what we expect for KMain
    
    # From previous analysis, KMain is around 0x208B0 (RVA)
    # Let's search for entries in that range
    
    print(f"Searching {num_entries} PDATA entries for KMain...")
    
    # Read symbol names from .rdata if available
    # For now, search for entries near expected KMain location
    target_rva_min = 0x20000  # Expect KMain somewhere in .managed section
    target_rva_max = 0x25000
    
    candidates = []
    
    for i in range(num_entries):
        entry_off = pdata_raw_ptr + i * 12
        begin_addr = u32(pe, entry_off)
        end_addr = u32(pe, entry_off + 4)
        unwind_info = u32(pe, entry_off + 8)
        
        if target_rva_min <= begin_addr <= target_rva_max:
            func_size = end_addr - begin_addr
            # KMain is a large function (several hundred bytes)
            if func_size > 100:
                candidates.append((begin_addr, end_addr, func_size))
    
    if not candidates:
        print("No candidates found in expected range")
        return None
    
    # Look for the KMain function specifically by checking the code
    for begin_addr, end_addr, func_size in candidates:
        # Calculate file offset
        # Need to find which section contains this RVA
        for i in range(number_of_sections):
            off = sec_table_off + i * 40
            sec_vaddr = u32(pe, off + 12)
            sec_vsize = u32(pe, off + 8)
            sec_raw_ptr = u32(pe, off + 20)
            
            if sec_vaddr <= begin_addr < sec_vaddr + sec_vsize:
                file_off = sec_raw_ptr + (begin_addr - sec_vaddr)
                
                # Check if this looks like KMain (checks bootInfo != null)
                code = pe[file_off:file_off + 32]
                
                # Look for: push rbp (55), test rcx,rcx (48 85 C9) pattern
                if code[0] == 0x55:  # push rbp
                    # Check for test rcx,rcx somewhere in first 32 bytes
                    if b"\x48\x85\xc9" in code:
                        va = image_base + begin_addr
                        print(f"Found KMain candidate at RVA 0x{begin_addr:X} (VA 0x{va:X})")
                        print(f"  Function size: {func_size} bytes")
                        print(f"  First bytes: {' '.join(f'{b:02X}' for b in code[:16])}")
                        return va
                break
    
    print("Could not identify KMain from PDATA")
    return None


def main():
    if len(sys.argv) < 2:
        print("Usage: extract_kmain_from_pdata.py <pe_file>")
        return 1
    
    pe_path = Path(sys.argv[1])
    if not pe_path.exists():
        print(f"Error: File not found: {pe_path}")
        return 1
    
    result = find_kmain_in_pdata(pe_path)
    if result:
        print(f"\nKMain entry point: 0x{result:X}")
        return 0
    else:
        print("\nFailed to find KMain")
        return 1


if __name__ == "__main__":
    sys.exit(main())
