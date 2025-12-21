# GXM Examples

This archive contains small guideXOS executables and NASM sources.

Files:
- minimal.gxm : minimal binary that returns 0 from its entry.
- minimal.asm : NASM source for minimal.gxm
- hello.gxm   : binary that returns RVA of an embedded string.
- hello.asm   : NASM source for hello.gxm

Format (GXM header):
- 0x00: 4 bytes signature 'GXM\0' (47 58 4D 00)
- 0x04: 4 bytes version (1)
- 0x08: 4 bytes entry RVA (offset to entry, typically 0x10)
- 0x0C: 4 bytes image size (total bytes)
- 0x10: code + data

Testing:
1. Copy a .gxm file into guideXOS filesystem.
2. Loader should validate the header, allocate memory, copy the image, compute entry = base + EntryRVA, setup a stack, and call the entry.
3. For minimal.gxm, entry returns 0.
4. For hello.gxm, entry returns an RVA pointing to a null-terminated ASCII string inside the module; add the base address to the RVA to get the pointer.

Notes:
- These are 32-bit x86 binaries (code is position-dependent relative to base).
