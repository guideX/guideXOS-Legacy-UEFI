#include <Uefi.h>
#include <Library/UefiLib.h>
#include "elf.h"
#include "uefi_shim.h"

EFI_STATUS LoadElf(
    EFI_SYSTEM_TABLE* SystemTable,
    EFI_FILE_PROTOCOL* KernelFile,
    UINT64* kernelBase,
    UINT64* kernelEntryOffset,
    UINT64* outKernelTotalSizeBytes
)
{
    EFI_STATUS status;

    // --- 1. Read ELF header ---
    Elf64_Ehdr ehdr;
    UINTN readSize = sizeof(ehdr);

    status = KernelFile->SetPosition(KernelFile, 0);
    if (EFI_ERROR(status)) {
        Print((CONST CHAR16*)L"ELF: Failed to seek to start: %r\n", status);
        return status;
    }

    status = KernelFile->Read(KernelFile, &readSize, &ehdr);
    if (EFI_ERROR(status) || readSize != sizeof(ehdr)) {
        Print((CONST CHAR16*)L"ELF: Failed to read ELF header\n");
        return EFI_LOAD_ERROR;
    }

    // Basic ELF sanity checks (64-bit, etc.)
    if (ehdr.e_ident[0] != 0x7F ||
        ehdr.e_ident[1] != 'E'  ||
        ehdr.e_ident[2] != 'L'  ||
        ehdr.e_ident[3] != 'F') {
        Print((CONST CHAR16*)L"ELF: Not an ELF file\n");
        return EFI_LOAD_ERROR;
    }

    if (ehdr.e_ident[4] != 2) { // EI_CLASS == ELFCLASS64
        Print((CONST CHAR16*)L"ELF: Not ELF64\n");
        return EFI_LOAD_ERROR;
    }

    if (ehdr.e_phoff == 0 || ehdr.e_phnum == 0) {
        Print((CONST CHAR16*)L"ELF: No program headers\n");
        return EFI_LOAD_ERROR;
    }

    // --- 2. Read program headers ---
    UINTN phTableSize = (UINTN)ehdr.e_phnum * ehdr.e_phentsize;
    Elf64_Phdr* phdrs = (Elf64_Phdr*)LocalAllocatePool(SystemTable, phTableSize);
    if (!phdrs) {
        Print((CONST CHAR16*)L"ELF: Failed to allocate program header table\n");
        return EFI_OUT_OF_RESOURCES;
    }

    status = KernelFile->SetPosition(KernelFile, ehdr.e_phoff);
    if (EFI_ERROR(status)) {
        Print((CONST CHAR16*)L"ELF: Failed to seek to program headers: %r\n", status);
        LocalFreePool(SystemTable, phdrs, phTableSize);
        return status;
    }

    readSize = phTableSize;
    status = KernelFile->Read(KernelFile, &readSize, phdrs);
    if (EFI_ERROR(status) || readSize != phTableSize) {
        Print((CONST CHAR16*)L"ELF: Failed to read program header\n");
        LocalFreePool(SystemTable, phdrs, phTableSize);
        return EFI_LOAD_ERROR;
    }

    // --- 3. Find min/max vaddr to determine total image span ---
    // We preserve the relative layout of segments (including gaps) so that
    // the entry point offset calculation works correctly.
    UINT64 minLoadVaddr = (UINT64)-1;
    UINT64 maxLoadVaddr = 0;

    for (UINT16 i = 0; i < ehdr.e_phnum; ++i) {
        Elf64_Phdr* ph = (Elf64_Phdr*)((UINT8*)phdrs + i * ehdr.e_phentsize);

        if (ph->p_type != PT_LOAD || ph->p_memsz == 0) {
            continue;
        }

        if (ph->p_vaddr < minLoadVaddr) {
            minLoadVaddr = ph->p_vaddr;
        }

        UINT64 segEnd = ph->p_vaddr + ph->p_memsz;
        if (segEnd > maxLoadVaddr) {
            maxLoadVaddr = segEnd;
        }
    }

    if (minLoadVaddr == (UINT64)-1 || maxLoadVaddr == 0) {
        Print((CONST CHAR16*)L"ELF: No usable PT_LOAD segments\n");
        LocalFreePool(SystemTable, phdrs, phTableSize);
        return EFI_LOAD_ERROR;
    }

    // Total size spans from minLoadVaddr to maxLoadVaddr
    UINT64 totalSize = maxLoadVaddr - minLoadVaddr;

    Print((CONST CHAR16*)L"ELF: vaddr range 0x%lx - 0x%lx (size 0x%lx)\n", 
          minLoadVaddr, maxLoadVaddr, totalSize);

    // --- 4. Allocate contiguous physical memory ---
    UINTN numPages = (UINTN)((totalSize + EFI_PAGE_SIZE - 1) / EFI_PAGE_SIZE);
    EFI_PHYSICAL_ADDRESS physBase = 0;

    status = SystemTable->BootServices->AllocatePages(
        AllocateAnyPages,
        EfiLoaderData,
        numPages,
        &physBase
    );
    if (EFI_ERROR(status)) {
        Print((CONST CHAR16*)L"ELF: AllocatePages failed: %r\n", status);
        LocalFreePool(SystemTable, phdrs, phTableSize);
        return status;
    }

    UINT8* basePtr = (UINT8*)(UINTN)physBase;

    // Zero the entire region first (handles gaps and BSS)
    SetMem(basePtr, (UINTN)totalSize, 0);

    Print((CONST CHAR16*)L"ELF: Allocated %u pages at 0x%lx\n", (UINT32)numPages, physBase);

    // --- 5. Load each PT_LOAD segment at its relative offset ---
    // Each segment is placed at: physBase + (p_vaddr - minLoadVaddr)
    // This preserves the virtual address layout in physical memory.

    for (UINT16 i = 0; i < ehdr.e_phnum; ++i) {
        Elf64_Phdr* ph = (Elf64_Phdr*)((UINT8*)phdrs + i * ehdr.e_phentsize);

        if (ph->p_type != PT_LOAD || ph->p_memsz == 0) {
            continue;
        }

        // Calculate destination: preserve relative offset from minLoadVaddr
        UINT64 relativeOffset = ph->p_vaddr - minLoadVaddr;
        UINT8* segDest = basePtr + relativeOffset;

        Print((CONST CHAR16*)L"ELF: Loading seg %u: vaddr=0x%lx -> phys=0x%lx (filesz=0x%lx)\n",
              (UINT32)i, ph->p_vaddr, (UINT64)(UINTN)segDest, ph->p_filesz);

        // Read the file portion
        if (ph->p_filesz > 0) {
            status = KernelFile->SetPosition(KernelFile, ph->p_offset);
            if (EFI_ERROR(status)) {
                Print((CONST CHAR16*)L"ELF: Failed to seek to segment %u: %r\n", i, status);
                LocalFreePool(SystemTable, phdrs, phTableSize);
                return status;
            }

            UINTN segRead = (UINTN)ph->p_filesz;
            status = KernelFile->Read(KernelFile, &segRead, segDest);
            if (EFI_ERROR(status) || segRead != ph->p_filesz) {
                Print((CONST CHAR16*)L"ELF: Failed to read segment %u\n", i);
                LocalFreePool(SystemTable, phdrs, phTableSize);
                return EFI_LOAD_ERROR;
            }
        }

        // BSS is already zeroed by the SetMem above
    }

    // --- 6. Compute entry offset ---
    // Entry is at: e_entry (virtual) -> physBase + (e_entry - minLoadVaddr)
    UINT64 entryOffset = ehdr.e_entry - minLoadVaddr;

    Print((CONST CHAR16*)L"ELF: Entry vaddr=0x%lx, offset=0x%lx\n", ehdr.e_entry, entryOffset);

    *kernelBase        = (UINT64)physBase;
    *kernelEntryOffset = entryOffset;
    if (outKernelTotalSizeBytes) {
        *outKernelTotalSizeBytes = totalSize;
    }

    LocalFreePool(SystemTable, phdrs, phTableSize);
    return EFI_SUCCESS;
}

