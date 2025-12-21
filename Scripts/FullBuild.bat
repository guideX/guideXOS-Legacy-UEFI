# 1. Delete old kernel
del ..\Tools\grub2\boot\kernel.bin

# 2. Build
dotnet build ..\guideXOS\guideXOS.csproj

# 3. Create loader
..\Tools\nasm.exe -fbin ..\Tools\EntryPoint.asm -o ..\Tools\loader_temp.o

# 4. Combine into kernel.bin
copy /b ..\Tools\loader_temp.o+bin\Debug\net9.0\win-x64\native\guideXOS.exe ..\Tools\grub2\boot\kernel.bin

# 5. Update ramdisk
del ..\Tools\grub2\boot\ramdisk.tar
..\Tools\7-Zip\7z.exe a ..\Tools\grub2\boot\ramdisk.tar Ramdisk\*

# 6. Create ISO
..\Tools\mkisofs.exe -relaxed-filenames -J -R -o ..\bin\Debug\net9.0\native\guideXOS.iso -b boot/grub/i386-pc/eltorito.img -no-emul-boot -boot-load-size 4 -boot-info-table ..\Tools\grub2