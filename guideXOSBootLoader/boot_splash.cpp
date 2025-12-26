#include "boot_splash.h"
#include <Library/BaseMemoryLib.h>

namespace guideXOS {
namespace boot {

// Simple 8x8 bitmap font (basic ASCII)
const UINT8 BootSplash::SimpleFontData[256 * 8] = {
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,  // All zeros for simplicity
};

void BootSplash::DrawSplash(
    EFI_PHYSICAL_ADDRESS framebufferBase,
    UINT32 framebufferWidth,
    UINT32 framebufferHeight,
    UINT32 framebufferPitch
) {
    if (framebufferBase == 0 || framebufferWidth == 0 || framebufferHeight == 0) {
        return;
    }
    
    UINT32 *fb = (UINT32 *)(UINTN)framebufferBase;
    
    // Draw teal gradient background (matching guideXOS desktop theme)
    UINT32 topTealColor = 0xFF5FD4C4;      // Light teal
    UINT32 bottomTealColor = 0xFF0D7D77;   // Dark teal
    
    for (UINT32 y = 0; y < framebufferHeight; y++) {
        // Interpolate gradient using integer math (256 scale)
        UINT32 t256 = (y * 256) / framebufferHeight;
        int topR = (int)((topTealColor >> 16) & 0xFF);
        int topG = (int)((topTealColor >> 8) & 0xFF);
        int topB = (int)(topTealColor & 0xFF);
        int bottomR = (int)((bottomTealColor >> 16) & 0xFF);
        int bottomG = (int)((bottomTealColor >> 8) & 0xFF);
        int bottomB = (int)(bottomTealColor & 0xFF);
        
        int r = (topR * (256 - t256) + bottomR * t256) / 256;
        int g = (topG * (256 - t256) + bottomG * t256) / 256;
        int b = (topB * (256 - t256) + bottomB * t256) / 256;
        UINT32 color = (UINT32)(0xFF000000 | (r << 16) | (g << 8) | b);
        
        // Fill row
        UINT32 pitchPixels = framebufferPitch / 4;
        for (UINT32 x = 0; x < framebufferWidth; x++) {
            fb[y * pitchPixels + x] = color;
        }
    }
}

} // namespace boot
} // namespace guideXOS



