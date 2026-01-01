using System;
using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    /// <summary>
    /// PNG Asset Preloader for UEFI boot environment.
    /// 
    /// PURPOSE:
    /// Loads and decodes PNG files BEFORE ExitBootServices is called,
    /// storing decoded RGBA pixel data in kernel-owned memory that
    /// survives the boot services exit.
    /// 
    /// BOOT PHASE BOUNDARY:
    /// =====================================================================
    /// PHASE 1 (UEFI Boot Services Active):
    ///   - All PreloadAsset() calls MUST happen here
    ///   - UEFI filesystem access is available
    ///   - PNG decoding happens using boot services memory
    ///   - Decoded data is copied to EfiLoaderData pages
    /// 
    /// --- ExitBootServices() boundary ---
    /// 
    /// PHASE 2 (Runtime - No Boot Services):
    ///   - Only GetPreloadedAsset() calls are allowed
    ///   - Returns previously decoded RGBA data from kernel memory
    ///   - NO filesystem access, NO new allocations via UEFI
    ///   - Attempting to preload here will fail
    /// =====================================================================
    /// 
    /// HARD CONSTRAINTS:
    ///   - No exceptions (all errors return false/null)
    ///   - No UEFI calls after ExitBootServices
    ///   - Fixed maximum asset count (no dynamic reallocation)
    ///   - All preloaded data must be in EfiLoaderData pages
    /// 
    /// USAGE:
    ///   // BEFORE ExitBootServices (in bootloader or early kernel init):
    ///   PngAssetPreloader.Initialize(bootInfo);
    ///   PngAssetPreloader.PreloadAsset("boot_logo", logoFileData);
    ///   PngAssetPreloader.PreloadAsset("cursor", cursorFileData);
    ///   PngAssetPreloader.FinalizePreload(); // Marks end of preload phase
    ///   
    ///   // AFTER ExitBootServices (anywhere in kernel):
    ///   var logo = PngAssetPreloader.GetPreloadedAsset("boot_logo");
    ///   if (logo != null) {
    ///       // Use logo.RgbaData, logo.Width, logo.Height
    ///   }
    /// </summary>
    public static unsafe class PngAssetPreloader {
        // ============================================================
        // CONSTANTS
        // ============================================================
        
        /// <summary>
        /// Maximum number of assets that can be preloaded.
        /// Fixed to avoid dynamic allocation after initialization.
        /// </summary>
        private const int MAX_PRELOADED_ASSETS = 32;
        
        /// <summary>
        /// Maximum asset name length in bytes (ASCII).
        /// </summary>
        private const int MAX_ASSET_NAME_LENGTH = 64;
        
        /// <summary>
        /// Maximum total preloaded data size (64MB).
        /// Prevents excessive memory consumption.
        /// </summary>
        private const ulong MAX_TOTAL_PRELOAD_SIZE = 64 * 1024 * 1024;
        
        // ============================================================
        // PRELOADED ASSET STRUCTURE
        // ============================================================
        
        /// <summary>
        /// Represents a preloaded PNG asset with decoded RGBA data.
        /// This structure is stored in kernel-owned memory.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct PreloadedAsset {
            /// <summary>
            /// Asset name (null-terminated ASCII, max 64 chars).
            /// Used as lookup key after boot.
            /// </summary>
            public fixed byte Name[MAX_ASSET_NAME_LENGTH];
            
            /// <summary>
            /// Pointer to decoded RGBA pixel data.
            /// Format: 4 bytes per pixel (R, G, B, A).
            /// </summary>
            public ulong RgbaDataPtr;
            
            /// <summary>
            /// Size of RGBA data in bytes (Width * Height * 4).
            /// </summary>
            public uint RgbaDataSize;
            
            /// <summary>
            /// Image width in pixels.
            /// </summary>
            public uint Width;
            
            /// <summary>
            /// Image height in pixels.
            /// </summary>
            public uint Height;
            
            /// <summary>
            /// Flags (reserved for future use).
            /// Bit 0: Valid entry
            /// Bit 1-7: Reserved
            /// </summary>
            public byte Flags;
            
            /// <summary>
            /// Reserved for alignment.
            /// </summary>
            public fixed byte Reserved[7];
        }
        
        // ============================================================
        // PRELOADER STATE STRUCTURE
        // ============================================================
        
        /// <summary>
        /// Preloader state stored in kernel memory.
        /// This entire structure is allocated in EfiLoaderData pages.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct PreloaderState {
            /// <summary>
            /// Magic number for validation ('PREL' = 0x4C455250).
            /// </summary>
            public uint Magic;
            
            /// <summary>
            /// Version of the preloader state structure.
            /// </summary>
            public uint Version;
            
            /// <summary>
            /// Number of assets currently preloaded.
            /// </summary>
            public uint AssetCount;
            
            /// <summary>
            /// Total bytes of RGBA data preloaded.
            /// </summary>
            public ulong TotalDataSize;
            
            /// <summary>
            /// True if preload phase is complete (ExitBootServices called).
            /// </summary>
            public byte PreloadComplete;
            
            /// <summary>
            /// True if initialization succeeded.
            /// </summary>
            public byte Initialized;
            
            /// <summary>
            /// Reserved for alignment.
            /// </summary>
            public fixed byte Reserved[6];
            
            // Assets array follows this header in memory
            // public PreloadedAsset[MAX_PRELOADED_ASSETS] Assets;
        }
        
        // ============================================================
        // STATIC STATE
        // ============================================================
        
        /// <summary>
        /// Pointer to preloader state in kernel memory.
        /// This is set during Initialize() and remains valid after ExitBootServices.
        /// </summary>
        private static PreloaderState* _state;
        
        /// <summary>
        /// Pointer to assets array (immediately follows PreloaderState header).
        /// </summary>
        private static PreloadedAsset* _assets;
        
        /// <summary>
        /// Base pointer for RGBA data allocation.
        /// Points to a large contiguous region for pixel data.
        /// </summary>
        private static byte* _dataRegionBase;
        
        /// <summary>
        /// Current offset within the data region for next allocation.
        /// </summary>
        private static ulong _dataRegionOffset;
        
        /// <summary>
        /// Total size of the data region in bytes.
        /// </summary>
        private static ulong _dataRegionSize;
        
        // Magic value for validation
        private const uint PRELOADER_MAGIC = 0x4C455250; // 'PREL'
        private const uint PRELOADER_VERSION = 1;
        
        // ============================================================
        // INITIALIZATION (BEFORE ExitBootServices)
        // ============================================================
        
        /// <summary>
        /// Initialize the PNG asset preloader.
        /// 
        /// MUST BE CALLED BEFORE ExitBootServices!
        /// 
        /// This method allocates kernel-owned memory for:
        ///   - PreloaderState header
        ///   - PreloadedAsset array
        ///   - RGBA data region
        /// 
        /// Parameters:
        ///   stateMemory    - Pointer to pre-allocated memory for state + assets
        ///                    (must be at least GetRequiredStateSize() bytes)
        ///   dataMemory     - Pointer to pre-allocated memory for RGBA data
        ///   dataMemorySize - Size of data memory region in bytes
        /// 
        /// Returns:
        ///   true  - Initialization successful
        ///   false - Invalid parameters or already initialized
        /// 
        /// Assumptions:
        ///   - Memory regions are in EfiLoaderData pages (survive ExitBootServices)
        ///   - Memory regions are identity-mapped
        ///   - Called exactly once before any PreloadAsset() calls
        /// </summary>
        public static bool Initialize(void* stateMemory, void* dataMemory, ulong dataMemorySize) {
            // ============================================================
            // BOOT PHASE: UEFI Boot Services Active
            // ============================================================
            
            // Validate parameters
            if (stateMemory == null) return false;
            if (dataMemory == null) return false;
            if (dataMemorySize == 0) return false;
            if (dataMemorySize > MAX_TOTAL_PRELOAD_SIZE) return false;
            
            // Check not already initialized
            if (_state != null && _state->Initialized != 0) return false;
            
            // Set up state pointer
            _state = (PreloaderState*)stateMemory;
            
            // Zero the state header
            byte* stateBytes = (byte*)_state;
            for (int i = 0; i < sizeof(PreloaderState); i++) {
                stateBytes[i] = 0;
            }
            
            // Initialize state header
            _state->Magic = PRELOADER_MAGIC;
            _state->Version = PRELOADER_VERSION;
            _state->AssetCount = 0;
            _state->TotalDataSize = 0;
            _state->PreloadComplete = 0;
            _state->Initialized = 1;
            
            // Assets array immediately follows the state header
            _assets = (PreloadedAsset*)((byte*)_state + sizeof(PreloaderState));
            
            // Zero the assets array
            byte* assetsBytes = (byte*)_assets;
            int assetsSize = sizeof(PreloadedAsset) * MAX_PRELOADED_ASSETS;
            for (int i = 0; i < assetsSize; i++) {
                assetsBytes[i] = 0;
            }
            
            // Set up data region
            _dataRegionBase = (byte*)dataMemory;
            _dataRegionOffset = 0;
            _dataRegionSize = dataMemorySize;
            
            // Zero first page of data region (helps with debugging)
            ulong zeroSize = dataMemorySize < 4096 ? dataMemorySize : 4096;
            for (ulong i = 0; i < zeroSize; i++) {
                _dataRegionBase[i] = 0;
            }
            
            return true;
        }
        
        /// <summary>
        /// Calculate the required size for state memory.
        /// 
        /// Returns:
        ///   Size in bytes needed for Initialize() stateMemory parameter.
        /// </summary>
        public static ulong GetRequiredStateSize() {
            return (ulong)(sizeof(PreloaderState) + sizeof(PreloadedAsset) * MAX_PRELOADED_ASSETS);
        }
        
        // ============================================================
        // PRELOAD METHODS (BEFORE ExitBootServices)
        // ============================================================
        
        /// <summary>
        /// Preload a PNG asset by decoding it and storing the RGBA data.
        /// 
        /// MUST BE CALLED BEFORE ExitBootServices!
        /// 
        /// This method:
        ///   1. Validates the PNG data (signature, IHDR)
        ///   2. Decodes the PNG to RGBA pixel data
        ///   3. Copies RGBA data to kernel-owned memory
        ///   4. Registers the asset for later retrieval
        /// 
        /// Parameters:
        ///   name    - Asset name for later lookup (max 63 chars, ASCII)
        ///   pngData - Raw PNG file data
        ///   pngSize - Size of PNG data in bytes
        /// 
        /// Returns:
        ///   true  - Asset preloaded successfully
        ///   false - Decode failed, out of space, or invalid parameters
        /// 
        /// Assumptions:
        ///   - Initialize() has been called
        ///   - FinalizePreload() has NOT been called yet
        ///   - PNG is RGBA (color type 6), 8-bit, non-interlaced
        /// </summary>
        public static bool PreloadAsset(string name, byte[] pngData, int pngSize) {
            // ============================================================
            // BOOT PHASE: UEFI Boot Services Active
            // ============================================================
            
            // Validate state
            if (_state == null) return false;
            if (_state->Initialized == 0) return false;
            if (_state->PreloadComplete != 0) return false; // Too late!
            
            // Validate parameters
            if (name == null) return false;
            if (name.Length == 0) return false;
            if (name.Length >= MAX_ASSET_NAME_LENGTH) return false;
            if (pngData == null) return false;
            if (pngSize <= 0) return false;
            
            // Check capacity
            if (_state->AssetCount >= MAX_PRELOADED_ASSETS) return false;
            
            // ============================================================
            // STEP 1: Initialize PngLoader if needed
            // ============================================================
            if (!PngLoader.Initialize()) {
                return false;
            }
            
            // ============================================================
            // STEP 2: Validate PNG and get dimensions
            // ============================================================
            int width, height;
            if (!PngLoader.ValidateSignatureAndParseIHDR(pngData, out width, out height)) {
                return false;
            }
            
            // Calculate required RGBA buffer size
            uint rgbaSize = (uint)(width * height * 4);
            
            // Check if we have space in data region
            if (_dataRegionOffset + rgbaSize > _dataRegionSize) {
                return false; // Out of space
            }
            
            // Check total limit
            if (_state->TotalDataSize + rgbaSize > MAX_TOTAL_PRELOAD_SIZE) {
                return false;
            }
            
            // ============================================================
            // STEP 3: Decode PNG to RGBA
            // ============================================================
            
            // Allocate temporary buffers for decoding
            // These can be on the heap since boot services are still active
            
            // Calculate IDAT size
            int idatSize;
            if (!PngLoader.CalculateIdatSize(pngData, out idatSize)) {
                return false;
            }
            
            // Allocate compressed buffer
            byte[] compressedBuffer = new byte[idatSize];
            if (compressedBuffer == null) return false;
            
            // Collect IDAT chunks
            int bytesWritten;
            if (!PngLoader.IterateChunks(pngData, compressedBuffer, idatSize, out bytesWritten)) {
                compressedBuffer.Dispose();
                return false;
            }
            
            // Calculate decompressed size
            int bytesPerPixel = 4;
            int scanlineBytes = width * bytesPerPixel;
            int expectedDecompressedSize = height * (scanlineBytes + 1); // +1 for filter byte per row
            
            // Allocate decompression buffer
            byte[] decompressedBuffer = new byte[expectedDecompressedSize];
            if (decompressedBuffer == null) {
                compressedBuffer.Dispose();
                return false;
            }
            
            // Decompress zlib data
            if (!PngLoader.InflateZlib(compressedBuffer, bytesWritten, decompressedBuffer, expectedDecompressedSize)) {
                decompressedBuffer.Dispose();
                compressedBuffer.Dispose();
                return false;
            }
            
            // Done with compressed buffer
            compressedBuffer.Dispose();
            
            // Allocate scanline buffers
            byte[] prevScanline = new byte[scanlineBytes];
            byte[] currScanline = new byte[scanlineBytes];
            if (prevScanline == null || currScanline == null) {
                decompressedBuffer.Dispose();
                if (prevScanline != null) prevScanline.Dispose();
                if (currScanline != null) currScanline.Dispose();
                return false;
            }
            
            // ============================================================
            // STEP 4: Allocate RGBA data in kernel-owned memory
            // ============================================================
            byte* rgbaPtr = _dataRegionBase + _dataRegionOffset;
            
            // Allocate temporary RGBA buffer for reconstruction
            byte[] tempRgba = new byte[rgbaSize];
            if (tempRgba == null) {
                decompressedBuffer.Dispose();
                prevScanline.Dispose();
                currScanline.Dispose();
                return false;
            }
            
            // Reconstruct scanlines
            if (!PngLoader.ReconstructScanlines(decompressedBuffer, expectedDecompressedSize,
                                                 tempRgba, width, height, prevScanline, currScanline)) {
                tempRgba.Dispose();
                decompressedBuffer.Dispose();
                prevScanline.Dispose();
                currScanline.Dispose();
                return false;
            }
            
            // Clean up temporary buffers
            decompressedBuffer.Dispose();
            prevScanline.Dispose();
            currScanline.Dispose();
            
            // ============================================================
            // STEP 5: Copy RGBA data to kernel memory
            // ============================================================
            fixed (byte* srcPtr = tempRgba) {
                for (uint i = 0; i < rgbaSize; i++) {
                    rgbaPtr[i] = srcPtr[i];
                }
            }
            
            tempRgba.Dispose();
            
            // ============================================================
            // STEP 6: Register the asset
            // ============================================================
            uint assetIndex = _state->AssetCount;
            PreloadedAsset* asset = &_assets[assetIndex];
            
            // Copy name (null-terminated)
            for (int i = 0; i < name.Length && i < MAX_ASSET_NAME_LENGTH - 1; i++) {
                asset->Name[i] = (byte)name[i];
            }
            asset->Name[name.Length < MAX_ASSET_NAME_LENGTH - 1 ? name.Length : MAX_ASSET_NAME_LENGTH - 1] = 0;
            
            // Set asset properties
            asset->RgbaDataPtr = (ulong)rgbaPtr;
            asset->RgbaDataSize = rgbaSize;
            asset->Width = (uint)width;
            asset->Height = (uint)height;
            asset->Flags = 1; // Valid
            
            // Update state
            _dataRegionOffset += rgbaSize;
            
            // Align to 16 bytes for next allocation
            ulong alignment = 16;
            _dataRegionOffset = (_dataRegionOffset + alignment - 1) & ~(alignment - 1);
            
            _state->AssetCount++;
            _state->TotalDataSize += rgbaSize;
            
            return true;
        }
        
        /// <summary>
        /// Mark the end of preload phase.
        /// 
        /// Call this just before ExitBootServices to lock the preloader.
        /// After this, no more PreloadAsset() calls are allowed.
        /// 
        /// Returns:
        ///   true  - Preload phase finalized successfully
        ///   false - Not initialized
        /// </summary>
        public static bool FinalizePreload() {
            // ============================================================
            // BOOT PHASE: UEFI Boot Services Active (last call before exit)
            // ============================================================
            
            if (_state == null) return false;
            if (_state->Initialized == 0) return false;
            
            _state->PreloadComplete = 1;
            return true;
        }
        
        // ============================================================
        // RETRIEVAL METHODS (AFTER ExitBootServices)
        // ============================================================
        
        /// <summary>
        /// Get a preloaded asset by name.
        /// 
        /// SAFE TO CALL AFTER ExitBootServices!
        /// 
        /// This method only reads from kernel-owned memory.
        /// No UEFI calls are made.
        /// 
        /// Parameters:
        ///   name - Asset name (must match name used in PreloadAsset)
        /// 
        /// Returns:
        ///   Pointer to PreloadedAsset, or null if not found
        /// 
        /// Assumptions:
        ///   - FinalizePreload() has been called
        ///   - Asset was successfully preloaded
        /// </summary>
        public static PreloadedAsset* GetPreloadedAsset(string name) {
            // ============================================================
            // BOOT PHASE: Runtime (No Boot Services)
            // No UEFI calls allowed here!
            // ============================================================
            
            if (_state == null) return null;
            if (_state->Initialized == 0) return null;
            if (name == null) return null;
            if (name.Length == 0) return null;
            
            // Search for asset by name
            uint count = _state->AssetCount;
            for (uint i = 0; i < count; i++) {
                PreloadedAsset* asset = &_assets[i];
                
                // Check if valid
                if ((asset->Flags & 1) == 0) continue;
                
                // Compare names
                bool match = true;
                for (int j = 0; j < name.Length; j++) {
                    if (j >= MAX_ASSET_NAME_LENGTH - 1) {
                        match = false;
                        break;
                    }
                    if (asset->Name[j] != (byte)name[j]) {
                        match = false;
                        break;
                    }
                }
                
                // Check null terminator
                if (match && name.Length < MAX_ASSET_NAME_LENGTH) {
                    if (asset->Name[name.Length] != 0) {
                        match = false;
                    }
                }
                
                if (match) {
                    return asset;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Get RGBA pixel data for a preloaded asset.
        /// 
        /// SAFE TO CALL AFTER ExitBootServices!
        /// 
        /// Parameters:
        ///   name   - Asset name
        ///   width  - Output: image width
        ///   height - Output: image height
        /// 
        /// Returns:
        ///   Pointer to RGBA data, or null if not found
        /// </summary>
        public static byte* GetPreloadedRgbaData(string name, out int width, out int height) {
            // ============================================================
            // BOOT PHASE: Runtime (No Boot Services)
            // ============================================================
            
            width = 0;
            height = 0;
            
            PreloadedAsset* asset = GetPreloadedAsset(name);
            if (asset == null) return null;
            
            width = (int)asset->Width;
            height = (int)asset->Height;
            
            return (byte*)asset->RgbaDataPtr;
        }
        
        /// <summary>
        /// Get the number of preloaded assets.
        /// 
        /// SAFE TO CALL AFTER ExitBootServices!
        /// 
        /// Returns:
        ///   Number of assets that were preloaded, or 0 if not initialized
        /// </summary>
        public static uint GetPreloadedAssetCount() {
            // ============================================================
            // BOOT PHASE: Runtime (No Boot Services)
            // ============================================================
            
            if (_state == null) return 0;
            if (_state->Initialized == 0) return 0;
            return _state->AssetCount;
        }
        
        /// <summary>
        /// Check if preload phase is complete.
        /// 
        /// SAFE TO CALL AFTER ExitBootServices!
        /// 
        /// Returns:
        ///   true if FinalizePreload() was called, false otherwise
        /// </summary>
        public static bool IsPreloadComplete() {
            // ============================================================
            // BOOT PHASE: Runtime (No Boot Services)
            // ============================================================
            
            if (_state == null) return false;
            if (_state->Initialized == 0) return false;
            return _state->PreloadComplete != 0;
        }
        
        /// <summary>
        /// Check if the preloader is initialized.
        /// 
        /// SAFE TO CALL ANYTIME!
        /// 
        /// Returns:
        ///   true if Initialize() was called successfully
        /// </summary>
        public static bool IsInitialized() {
            if (_state == null) return false;
            return _state->Initialized != 0;
        }
        
        // ============================================================
        // UTILITY METHODS
        // ============================================================
        
        /// <summary>
        /// Create an Image object from preloaded asset data.
        /// 
        /// SAFE TO CALL AFTER ExitBootServices!
        /// 
        /// This allocates a new Image using the kernel's allocator
        /// and copies the preloaded RGBA data (converted to ARGB format).
        /// 
        /// Parameters:
        ///   name - Asset name
        /// 
        /// Returns:
        ///   New Image object, or null if asset not found
        /// 
        /// Note: Caller is responsible for disposing the returned Image.
        /// </summary>
        public static System.Drawing.Image CreateImageFromPreloaded(string name) {
            // ============================================================
            // BOOT PHASE: Runtime (No Boot Services)
            // ============================================================
            
            int width, height;
            byte* rgbaData = GetPreloadedRgbaData(name, out width, out height);
            if (rgbaData == null) return null;
            if (width <= 0 || height <= 0) return null;
            
            // Create new image
            var image = new System.Drawing.Image(width, height);
            if (image == null || image.RawData == null) return null;
            
            // Convert RGBA to ARGB and copy to image
            int pixelCount = width * height;
            if (!PngLoader.ConvertRgbaToArgb(rgbaData, pixelCount, image.RawData)) {
                image.Dispose();
                return null;
            }
            
            return image;
        }
        
        /// <summary>
        /// Convert RGBA byte array to ARGB int array.
        /// Helper method for CreateImageFromPreloaded.
        /// </summary>
        private static bool ConvertRgbaToArgb(byte* rgbaInput, int pixelCount, int[] argbOutput) {
            if (rgbaInput == null) return false;
            if (argbOutput == null) return false;
            if (pixelCount <= 0) return false;
            if (argbOutput.Length < pixelCount) return false;
            
            int rgbaPos = 0;
            for (int i = 0; i < pixelCount; i++) {
                byte r = rgbaInput[rgbaPos];
                byte g = rgbaInput[rgbaPos + 1];
                byte b = rgbaInput[rgbaPos + 2];
                byte a = rgbaInput[rgbaPos + 3];
                rgbaPos += 4;
                
                // Pack as ARGB: 0xAARRGGBB
                argbOutput[i] = (int)(((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b);
            }
            
            return true;
        }
        
        // ============================================================
        // DEBUG / DIAGNOSTICS
        // ============================================================
        
        /// <summary>
        /// Print preloader statistics to serial console (debug).
        /// 
        /// SAFE TO CALL AFTER ExitBootServices!
        /// </summary>
        public static void PrintStats() {
            if (_state == null) {
                BootConsole.WriteLine("[PngPreloader] Not initialized");
                return;
            }
            
            BootConsole.WriteLine("[PngPreloader] Stats:");
            BootConsole.Write("  Assets: ");
            BootConsole.WriteLine(_state->AssetCount.ToString());
            BootConsole.Write("  Total Size: ");
            BootConsole.Write(_state->TotalDataSize.ToString());
            BootConsole.WriteLine(" bytes");
            BootConsole.Write("  Preload Complete: ");
            BootConsole.WriteLine(_state->PreloadComplete != 0 ? "Yes" : "No");
        }
    }
}
