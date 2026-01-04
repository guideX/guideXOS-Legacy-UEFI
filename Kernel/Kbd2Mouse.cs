using guideXOS.Kernel.Drivers;
using System;
using System.Windows.Forms;

namespace guideXOS {
    /// <summary>
    /// Keyboard-to-Mouse Emulation - Arrow keys control mouse pointer
    /// 
    /// ============================================================================
    /// UEFI COMPATIBILITY AUDIT
    /// ============================================================================
    /// 
    /// CLASSIFICATION: UEFI-COMPATIBLE (Pure software)
    /// 
    /// This class does NOT access any hardware directly.
    /// It receives keyboard events and updates mouse position.
    /// 
    /// ENTRY POINT:
    /// - PS2Keyboard.OnInterrupt() -> Kbd2Mouse.OnKeyChanged()
    /// - USB keyboard via Keyboard.InvokeOnKeyChanged() chain
    /// 
    /// REUSABLE FOR ALL INPUT SOURCES:
    /// - Works with any keyboard input that provides ConsoleKeyInfo
    /// - Provides accessibility for systems without mouse
    /// - Useful for UEFI testing when mouse driver not ready
    /// 
    /// USES:
    /// - Arrow keys: Move mouse cursor
    /// - F1: Left mouse button
    /// - F2: Right mouse button
    /// 
    /// ============================================================================
    /// </summary>
    internal static class Kbd2Mouse {
        static bool up = false, down = false, left = false, right = false, f1 = false, f2 = false;

        internal static void OnKeyChanged(ConsoleKeyInfo keyInfo) {
            const int ratio = 10;

            if (keyInfo.Key == ConsoleKey.Up)
                up = keyInfo.KeyState == ConsoleKeyState.Pressed;
            if (keyInfo.Key == ConsoleKey.Down)
                down = keyInfo.KeyState == ConsoleKeyState.Pressed;
            if (keyInfo.Key == ConsoleKey.Left)
                left = keyInfo.KeyState == ConsoleKeyState.Pressed;
            if (keyInfo.Key == ConsoleKey.Right)
                right = keyInfo.KeyState == ConsoleKeyState.Pressed;

            if (
                keyInfo.Key == ConsoleKey.Up ||
                keyInfo.Key == ConsoleKey.Down ||
                keyInfo.Key == ConsoleKey.Left ||
                keyInfo.Key == ConsoleKey.Right
                ) {
                int axisX = 0, axisY = 0;
                if (left)
                    axisX -= ratio;
                if (right)
                    axisX += ratio;
                if (up)
                    axisY -= ratio;
                if (down)
                    axisY += ratio;

                Control.MousePosition.X = Math.Clamp(Control.MousePosition.X + axisX, 0, Framebuffer.Width);
                Control.MousePosition.Y = Math.Clamp(Control.MousePosition.Y + axisY, 0, Framebuffer.Height);
            }

            if (keyInfo.Key == ConsoleKey.F1)
                f1 = keyInfo.KeyState == ConsoleKeyState.Pressed;
            if (keyInfo.Key == ConsoleKey.F2)
                f2 = keyInfo.KeyState == ConsoleKeyState.Pressed;

            if (f1)
                Control.MouseButtons |= MouseButtons.Left;
            else
                Control.MouseButtons &= ~MouseButtons.Left;
            if (f2)
                Control.MouseButtons |= MouseButtons.Right;
            else
                Control.MouseButtons &= ~MouseButtons.Right;
        }
    }
}
