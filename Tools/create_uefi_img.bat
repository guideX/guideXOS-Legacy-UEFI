@echo off
setlocal

rem Set paths
set TOOLS_DIR=%~dp0..
set GRUB_DIR=%TOOLS_DIR%\grub2
set EFI_IMG_DIR=%GRUB_DIR%\EFI\BOOT
set EFI_IMG=%GRUB_DIR%\efi.img

rem Create directories for EFI image
if not exist "%EFI_IMG_DIR%" mkdir "%EFI_IMG_DIR%"

rem Generate grubx64.efi
echo Generating GRUB EFI boot file...
"%TOOLS_DIR%\mkimage.exe" -o "%EFI_IMG_DIR%\bootx64.efi" -p /boot/grub -O x86_64-efi boot efi_uga efi_gop efi_uga_text fat part_gpt part_msdos normal configfile search search_fs_file search_label gfxterm gfxmenu all_video loadenv echo truecrypt iso9660 xhci ohci uhci usb usb_keyboard usbms

rem Check if bootx64.efi was created
if not exist "%EFI_IMG_DIR%\bootx64.efi" (
    echo Failed to generate bootx64.efi.
    exit /b 1
)

rem Create EFI image
echo Creating EFI image...
"%TOOLS_DIR%\mkisofs.exe" -o "%EFI_IMG%" -V "UEFI_IMG" -sysid "EL TORITO" -C 0 -no-emul-boot -r -J -graft-points "/EFI/BOOT=%EFI_IMG_DIR%"

rem Check if efi.img was created
if not exist "%EFI_IMG%" (
    echo Failed to create efi.img.
    exit /b 1
)

echo UEFI image created successfully at %EFI_IMG%
endlocal
