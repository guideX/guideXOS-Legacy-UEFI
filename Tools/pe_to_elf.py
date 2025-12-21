#!/usr/bin/env python3
"""pe_to_elf.py

Minimal PE/COFF (Windows x64) -> ELF64 converter for guideXOS.

Why this exists:
- Some llvm-objcopy builds on Windows claim to support --output-target=elf64-x86-64
  but still emit a COFF/PE image (MZ header) unchanged.
- The guideXOS UEFI bootloader expects a real ELF64 file (0x7F 'E' 'L' 'F').

What it does:
- Verifies input is PE (MZ + PE\0\0)
- Builds a minimal ELF64 ET_EXEC with PT_LOAD segments
- Maps each PE section having raw data into an ELF segment
- Sets e_entry to ImageBase + AddressOfEntryPoint (or custom entry if specified)

Limitations:
- Minimal conversion. Enough for loaders that just need PT_LOADs and e_entry.
- Does not generate section headers (e_shoff=0).
- Assumes x86_64, little-endian.

Usage:
  pe_to_elf.py input.exe output.elf                     # Use PE's default entry point (Entry)
  pe_to_elf.py input.exe output.elf --entry 0x10024204  # Override with KMain address
  pe_to_elf.py input.exe output.elf --map Kernel.map --symbol KMain  # Find KMain in map file
"""

from __future__ import annotations

import argparse
import struct
from dataclasses import dataclass
from pathlib import Path

ELF_MAGIC = b"\x7fELF"


def find_symbol_in_map(map_path: Path, symbol_name: str) -> int | None:
    """Parse a linker map file to find a symbol's address.
    
    MSVC map file format:
     0003:00021204       KMain                      0000000010024204     guideXOS.obj
     
    We want the SECOND hex number (the absolute address), not the first (section:offset).
    """
    if not map_path.exists():
        return None
    
    content = map_path.read_text(errors='replace')
    
    # Look for exact symbol name (not qualified) with the address after it
    # Format: "section:offset  symbol_name  address  object"
    for line in content.split('\n'):
        parts = line.split()
        if len(parts) >= 3:
            for i, part in enumerate(parts):
                if part == symbol_name:
                    # Look for a 16-digit hex address after the symbol
                    for j in range(i + 1, min(i + 3, len(parts))):
                        candidate = parts[j]
                        if len(candidate) == 16 and all(c in '0123456789abcdefABCDEF' for c in candidate):
                            return int(candidate, 16)
    
    return None


def u16(b: bytes, off: int) -> int:
    return struct.unpack_from("<H", b, off)[0]


def u32(b: bytes, off: int) -> int:
    return struct.unpack_from("<I", b, off)[0]


def u64(b: bytes, off: int) -> int:
    return struct.unpack_from("<Q", b, off)[0]


@dataclass
class PeSection:
    name: str
    vaddr: int
    vsize: int
    raw_ptr: int
    raw_size: int
    characteristics: int


def parse_pe_sections(pe: bytes) -> tuple[int, int, int, list[PeSection]]:
    if pe[:2] != b"MZ":
        raise SystemExit("Input is not PE (missing MZ)")

    pe_off = u32(pe, 0x3C)
    if pe[pe_off:pe_off + 4] != b"PE\x00\x00":
        raise SystemExit("Input is not PE (missing PE\\0\\0)")

    coff_off = pe_off + 4
    number_of_sections = u16(pe, coff_off + 2)
    size_of_optional_header = u16(pe, coff_off + 16)

    opt_off = coff_off + 20
    magic = u16(pe, opt_off)
    if magic != 0x20B:
        raise SystemExit(f"Unsupported PE optional header magic: 0x{magic:04X} (expected PE32+ 0x20B)")

    address_of_entry_point = u32(pe, opt_off + 16)
    image_base = u64(pe, opt_off + 24)

    sec_table_off = opt_off + size_of_optional_header

    sections: list[PeSection] = []
    for i in range(number_of_sections):
        off = sec_table_off + i * 40
        name = pe[off:off + 8].split(b"\x00", 1)[0].decode("ascii", errors="replace")
        vsize = u32(pe, off + 8)
        vaddr = u32(pe, off + 12)
        raw_size = u32(pe, off + 16)
        raw_ptr = u32(pe, off + 20)
        characteristics = u32(pe, off + 36)
        sections.append(PeSection(name, vaddr, vsize, raw_ptr, raw_size, characteristics))

    entry = image_base + address_of_entry_point
    return image_base, address_of_entry_point, entry, sections


def align_up(x: int, a: int) -> int:
    return (x + (a - 1)) & ~(a - 1)


def to_elf(pe: bytes, custom_entry: int | None = None) -> bytes:
    image_base, ep_rva, entry, sections = parse_pe_sections(pe)

    if custom_entry is not None:
        entry = custom_entry
        print(f"Using custom entry point: 0x{entry:X}")
    else:
        print(f"Using PE entry point: 0x{entry:X}")

    load_secs = [s for s in sections if s.raw_size > 0]
    if not load_secs:
        raise SystemExit("No loadable sections with raw data")

    e_ehsize = 64
    e_phentsize = 56
    e_phnum = len(load_secs)

    phoff = e_ehsize
    headers_size = e_ehsize + e_phentsize * e_phnum

    cur_off = align_up(headers_size, 0x1000)

    phdrs = []
    seg_blobs: list[tuple[int, bytes]] = []

    for s in load_secs:
        data = pe[s.raw_ptr:s.raw_ptr + s.raw_size]

        pf_x = 1 if (s.characteristics & 0x20000000) else 0
        pf_w = 1 if (s.characteristics & 0x80000000) else 0
        pf_r = 1 if (s.characteristics & 0x40000000) else 1
        p_flags = (pf_x * 1) | (pf_w * 2) | (pf_r * 4)

        file_off = cur_off
        cur_off = align_up(cur_off + len(data), 0x1000)

        vaddr = image_base + s.vaddr

        p_type = 1
        p_offset = file_off
        p_vaddr = vaddr
        p_paddr = vaddr
        p_filesz = len(data)
        p_memsz = max(p_filesz, s.vsize)
        p_align = 0x1000

        phdrs.append((p_type, p_flags, p_offset, p_vaddr, p_paddr, p_filesz, p_memsz, p_align))
        seg_blobs.append((file_off, data))

    e_ident = bytearray(16)
    e_ident[0:4] = ELF_MAGIC
    e_ident[4] = 2
    e_ident[5] = 1
    e_ident[6] = 1

    e_type = 2
    e_machine = 0x3E
    e_version = 1
    e_entry = entry
    e_phoff = phoff
    e_shoff = 0
    e_flags = 0
    e_shentsize = 0
    e_shnum = 0
    e_shstrndx = 0

    elf_hdr = struct.pack(
        "<16sHHIQQQIHHHHHH",
        bytes(e_ident),
        e_type,
        e_machine,
        e_version,
        e_entry,
        e_phoff,
        e_shoff,
        e_flags,
        e_ehsize,
        e_phentsize,
        e_phnum,
        e_shentsize,
        e_shnum,
        e_shstrndx,
    )

    phdr_bytes = b"".join(struct.pack("<IIQQQQQQ", *p) for p in phdrs)

    out = bytearray(cur_off)
    out[0:len(elf_hdr)] = elf_hdr
    out[phoff:phoff + len(phdr_bytes)] = phdr_bytes

    for off, data in seg_blobs:
        out[off:off + len(data)] = data

    return bytes(out)


def rebase_symbol_address(symbol_addr: int, pe_image_base: int) -> int:
    """Detect if a symbol address is from a stale map file (different image base)
    and rebase it to the current PE's image base.
    
    Common image bases:
    - 0x10000000: Legacy NativeAOT/CoreRT default
    - 0x140000000: Modern NativeAOT default (Windows default for 64-bit)
    - 0x400000: Standard EXE default
    
    This function detects mismatches and corrects them.
    """
    # Common stale image bases to check
    KNOWN_BASES = [0x10000000, 0x140000000, 0x400000, 0x100000000]
    
    # Check if symbol falls within expected range for pe_image_base
    # A valid symbol should be >= image_base and < image_base + reasonable_size
    reasonable_max_size = 0x10000000  # 256 MB is generous
    
    if pe_image_base <= symbol_addr < pe_image_base + reasonable_max_size:
        # Symbol is already in the right range, no rebasing needed
        return symbol_addr
    
    # Try to detect which old base was used
    for old_base in KNOWN_BASES:
        if old_base == pe_image_base:
            continue
        if old_base <= symbol_addr < old_base + reasonable_max_size:
            # Found the old base! Rebase to new base
            rva = symbol_addr - old_base
            new_addr = pe_image_base + rva
            print(f"WARNING: Rebasing symbol from old image base 0x{old_base:X} to 0x{pe_image_base:X}")
            print(f"         Symbol RVA: 0x{rva:X}, New address: 0x{new_addr:X}")
            return new_addr
    
    # Can't detect old base - warn and use as-is (will likely fail in bootloader)
    print(f"WARNING: Symbol address 0x{symbol_addr:X} doesn't match PE image base 0x{pe_image_base:X}")
    print(f"         The Kernel.map file may be stale. Consider deleting it and rebuilding.")
    return symbol_addr


def main() -> int:
    ap = argparse.ArgumentParser(
        description="Convert PE/COFF executable to ELF64 for guideXOS UEFI bootloader"
    )
    ap.add_argument("input", type=Path, help="Input PE executable")
    ap.add_argument("output", type=Path, help="Output ELF file")
    ap.add_argument("--entry", type=lambda x: int(x, 0), default=None,
                    help="Override entry point address (hex, e.g., 0x10024204)")
    ap.add_argument("--map", type=Path, default=None,
                    help="Linker map file to search for symbol")
    ap.add_argument("--symbol", type=str, default="KMain",
                    help="Symbol name to use as entry point (default: KMain)")
    ap.add_argument("--entry-bias", type=lambda x: int(x, 0), default=0,
                    help="Optional bias added to resolved entry address (default: 0). Useful for off-bytes in map symbols.")
    args = ap.parse_args()

    pe = args.input.read_bytes()
    
    # Parse PE to get the actual image base
    pe_image_base, _, pe_entry, _ = parse_pe_sections(pe)
    print(f"PE Image Base: 0x{pe_image_base:X}")
    print(f"PE Default Entry: 0x{pe_entry:X}")

    custom_entry = args.entry

    if args.map and custom_entry is None:
        symbol_addr = find_symbol_in_map(args.map, args.symbol)
        if symbol_addr:
            print(f"Found {args.symbol} in map file at 0x{symbol_addr:X}")
            
            # Rebase if necessary (detect stale map files)
            symbol_addr = rebase_symbol_address(symbol_addr, pe_image_base)
            
            custom_entry = symbol_addr + args.entry_bias
            if args.entry_bias != 0:
                print(f"Applying bias {args.entry_bias:+#x} => Final entry: 0x{custom_entry:X}")
        else:
            print(f"WARNING: Symbol '{args.symbol}' not found in {args.map}")
            print(f"         Using PE default entry point instead.")

    elf = to_elf(pe, custom_entry)
    args.output.write_bytes(elf)

    head = args.output.read_bytes()[:4]
    if head != ELF_MAGIC:
        raise SystemExit("Conversion failed: output is not ELF")

    print(f"Successfully created {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
