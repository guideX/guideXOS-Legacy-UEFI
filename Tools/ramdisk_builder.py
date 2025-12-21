#!/usr/bin/env python3
"""
guideXOS Ramdisk Builder
Creates a ramdisk image in the custom RDSK format expected by the kernel.

Format:
  Magic: "RDSK" (4 bytes)
  Version: 1 (4 bytes, little-endian)
  FileCount: N (4 bytes, little-endian)
  For each file:
    PathLength: 2 bytes (little-endian)
    Path: UTF-8 string
    DataLength: 4 bytes (little-endian)
    Data: raw bytes

Usage:
  python ramdisk_builder.py <source_directory> <output.img>

Example:
  python ramdisk_builder.py ramdisk_src ESP\ramdisk.img
"""

import sys
import os
import struct

def collect_files(root):
    """Recursively collect all files from the source directory."""
    files = []
    for dirpath, _, filenames in os.walk(root):
        for f in filenames:
            full_path = os.path.join(dirpath, f)
            # Convert to Unix-style path (forward slashes)
            rel_path = os.path.relpath(full_path, root).replace("\\", "/")
            files.append((rel_path, full_path))
    return files

def build_ramdisk(srcdir, outpath):
    """Build ramdisk image from source directory."""
    if not os.path.exists(srcdir):
        print(f"Error: Source directory '{srcdir}' does not exist")
        sys.exit(1)
    
    files = collect_files(srcdir)
    
    if len(files) == 0:
        print(f"Warning: No files found in '{srcdir}'")
    
    print(f"Building ramdisk with {len(files)} files...")
    
    total_size = 0
    with open(outpath, "wb") as out:
        # Write header
        out.write(b"RDSK")                          # Magic (4 bytes)
        out.write(struct.pack("<I", 1))            # Version = 1 (4 bytes)
        out.write(struct.pack("<I", len(files)))   # FileCount (4 bytes)
        
        # Write each file
        for rel_path, full_path in files:
            try:
                with open(full_path, "rb") as f:
                    file_data = f.read()
                
                path_bytes = rel_path.encode("utf-8")
                
                # Write path length (2 bytes)
                out.write(struct.pack("<H", len(path_bytes)))
                
                # Write path
                out.write(path_bytes)
                
                # Write data length (4 bytes)
                out.write(struct.pack("<I", len(file_data)))
                
                # Write data
                out.write(file_data)
                
                total_size += len(file_data)
                print(f"  Added: {rel_path} ({len(file_data)} bytes)")
                
            except Exception as e:
                print(f"  Error adding {rel_path}: {e}")
                sys.exit(1)
    
    final_size = os.path.getsize(outpath)
    print(f"\nRamdisk created successfully!")
    print(f"  Output: {outpath}")
    print(f"  Files: {len(files)}")
    print(f"  Content size: {total_size:,} bytes")
    print(f"  Total size: {final_size:,} bytes")

def main():
    if len(sys.argv) != 3:
        print("Usage: ramdisk_builder.py <source_directory> <output.img>")
        print()
        print("Example:")
        print("  python ramdisk_builder.py ramdisk_src ESP\\ramdisk.img")
        sys.exit(1)
    
    source_dir = sys.argv[1]
    output_file = sys.argv[2]
    
    # Create output directory if it doesn't exist
    output_dir = os.path.dirname(output_file)
    if output_dir and not os.path.exists(output_dir):
        os.makedirs(output_dir)
    
    build_ramdisk(source_dir, output_file)

if __name__ == "__main__":
    main()
