# Virtual Disk Images - Usage Guide

## Overview
Your guideXOS now supports mounting `.img` disk image files as virtual disks! This allows you to:
- Test different filesystems without physical disks
- Create portable disk images
- Develop and test filesystem code safely

## Available Disk Images
- `/disks/test-fat32.img` - FAT32 formatted disk image
- `/disks/test-ext4.img` - EXT4 formatted disk image

## How It Works

### 1. FileDisk Driver (`Kernel/Drivers/FileDisk.cs`)
A new disk driver that reads `.img` files from the ramdisk and exposes them as virtual block devices.

**Features:**
- Reads entire image into memory
- Provides sector-based read/write access
- Can sync changes back to the image file
- Validates sector alignment

### 2. Virtual Disk Manager App (`guideXOS/DefaultApps/VirtualDiskManager.cs`)
A GUI application for managing virtual disk images.

**Controls:**
- `Up/Down` - Select disk image
- `M` - Mount selected image as FAT32
- `E` - Mount selected image as EXT4  
- `U` - Unmount and restore original disk
- `S` - Sync changes back to image file
- `R` - Refresh image list
- `L` - List files on current filesystem

## Programmatic Usage

### Mounting a FAT32 Image
```csharp
using guideXOS.FS;
using guideXOS.Kernel.Drivers;

// Save original disk/filesystem
var originalDisk = Disk.Instance;
var originalFS = File.Instance;

// Load and mount FAT32 image
var virtualDisk = new FileDisk("/disks/test-fat32.img");
Disk.Instance = virtualDisk;
File.Instance = new FAT(virtualDisk);

// Now you can use File.ReadAllBytes, File.GetFiles, etc.
var files = File.GetFiles("/");

// Restore original disk
Disk.Instance = originalDisk;
File.Instance = originalFS;
```

### Mounting an EXT4 Image
```csharp
using guideXOS.FS;
using guideXOS.Kernel.Drivers;

// Load and mount EXT4 image
var virtualDisk = new FileDisk("/disks/test-ext4.img");
Disk.Instance = virtualDisk;
File.Instance = new EXT2(virtualDisk); // EXT2 driver supports EXT4

// Use the filesystem
var files = File.GetFiles("/");
var data = File.ReadAllBytes("/some-file.txt");

// Save changes back to image (optional)
virtualDisk.Sync();
```

### Creating Your Own Disk Images

You can create disk images externally and place them in the `/disks/` directory:

**Creating FAT32 image (Linux/macOS):**
```bash
# Create 10MB image
dd if=/dev/zero of=test-fat32.img bs=1M count=10

# Format as FAT32
mkfs.vfat -F 32 test-fat32.img

# Mount and add files
mkdir /tmp/mnt
sudo mount test-fat32.img /tmp/mnt
sudo cp your-files /tmp/mnt/
sudo umount /tmp/mnt
```

**Creating EXT4 image (Linux):**
```bash
# Create 10MB image
dd if=/dev/zero of=test-ext4.img bs=1M count=10

# Format as EXT4
mkfs.ext4 test-ext4.img

# Mount and add files
mkdir /tmp/mnt
sudo mount test-ext4.img /tmp/mnt
sudo cp your-files /tmp/mnt/
sudo umount /tmp/mnt
```

## Architecture

```
???????????????????????????????????????
?   Application Layer                 ?
?   (File.ReadAllBytes, etc.)         ?
???????????????????????????????????????
              ?
???????????????????????????????????????
?   FileSystem Layer                  ?
?   - FAT (FAT12/16/32)              ?
?   - EXT2 (EXT2/3/4 read-mostly)    ?
???????????????????????????????????????
              ?
???????????????????????????????????????
?   Disk Abstraction Layer            ?
?   - Disk.Instance                   ?
???????????????????????????????????????
              ?
      ??????????????????
      ?                ?
?????????????  ???????????????
?  FileDisk  ?  ?  Physical   ?
?  (.img)    ?  ?  Disk (IDE) ?
??????????????  ???????????????
```

## Limitations

1. **Memory Usage**: Entire image is loaded into memory
   - For large images, consider implementing streaming reads
   
2. **EXT4 Support**: Currently read-mostly
   - Can overwrite existing files
   - Cannot create new files/directories yet
   
3. **Persistence**: Changes only persist if you call `Sync()`
   - Without sync, changes are lost when unmounting

## Testing Your Disk Images

1. **Launch Virtual Disk Manager**
   - Run the VirtualDiskManager app from your desktop

2. **Select an Image**
   - Use Up/Down arrows to select `test-fat32.img` or `test-ext4.img`

3. **Mount the Image**
   - Press `M` for FAT32 or `E` for EXT4

4. **List Files**
   - Press `L` to see files on the mounted image

5. **Test File Operations**
   - Use File.ReadAllBytes() to read files
   - Use File.WriteAllBytes() to write files (FAT32 supports full write)

6. **Unmount**
   - Press `U` to restore original filesystem

## Tips

- Always unmount before shutting down to avoid data loss
- Use `Sync()` periodically if making important changes
- Test with small images first (1-10MB)
- Keep backups of your disk images

## Future Enhancements

- [ ] Streaming disk access (reduce memory usage)
- [ ] Full EXT4 write support (create files/directories)
- [ ] Auto-detect filesystem type
- [ ] Multiple concurrent virtual disks
- [ ] Disk image compression support
- [ ] Network-backed disk images
