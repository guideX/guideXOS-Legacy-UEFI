# guideXOS Ramdisk Source

This directory contains the files that will be packed into `ramdisk.img`.

## Required Files (Minimum)

The kernel requires these files to boot properly:

### Images (Mouse Cursors)
- `Images/Cursor.png` - Default mouse cursor (16x16 PNG)
- `Images/Grab.png` - Dragging cursor (16x16 PNG)
- `Images/Busy.png` - Busy/loading cursor (16x16 PNG)

### Fonts
- `Fonts/enludo.btf` - System bitmap font (custom BTF format)

## Optional Files

### Applications
- `Applications/*.gxm` - Compiled guideXOS applications

### Configuration
- `boot/config.txt` - Boot configuration (for installed mode)
- `etc/guidexos/config.ini` - User settings

### Images (Additional)
- Wallpapers, icons, etc.

## Directory Structure

```
ramdisk_src/
??? Images/
?   ??? Cursor.png
?   ??? Grab.png
?   ??? Busy.png
??? Fonts/
?   ??? enludo.btf
??? Applications/
?   ??? (optional .gxm files)
??? boot/
?   ??? config.txt (optional)
??? etc/
    ??? guidexos/
        ??? config.ini (optional)
```

## Building

The ramdisk is built automatically by `build.ps1`:

```powershell
.\build.ps1
```

Or manually:

```powershell
python tools\ramdisk_builder.py ramdisk_src ramdisk.img
```

## File Format

The ramdisk uses a custom RDSK format:
- Magic: "RDSK" (4 bytes)
- Version: 1 (4 bytes)
- FileCount: N (4 bytes)
- Files: List of path + data entries

See `tools/ramdisk_builder.py` for implementation details.

## Notes

- All paths are relative to this directory
- Use forward slashes (/) in paths
- Files are loaded into RAM at boot
- Missing required files will cause boot to fail or display errors
