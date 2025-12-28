using System;
namespace guideXOS;
/// <summary>
/// Boot Mode
/// </summary>
public enum BootMode {
    /// <summary>
    /// Minimal
    /// </summary>
    UEFI = 0,
    /// <summary>
    /// Normal
    /// </summary>
    Legacy = 1
}
/// <summary>
/// Boot Console Mode
/// </summary>
public enum BootConsoleMode {
    /// <summary>
    /// None
    /// </summary>
    None = 0,
    /// <summary>
    /// Normal
    /// </summary>
    Normal = 1,
}
/// <summary>
/// Global boot configuration.
/// Default is UltraMinimal (safe mode).
/// </summary>
public static class BootConsole {
    /// <summary>
    /// Current Mode
    /// </summary>
    public static guideXOS.BootMode CurrentMode { get; set; } = guideXOS.BootMode.UEFI;
    /// <summary>
    /// Current Console Mode
    /// </summary>
    public static guideXOS.BootConsoleMode CurrentConsoleMode { get; set; } = guideXOS.BootConsoleMode.Normal;
    /// <summary>
    /// Draw Debug Lines
    /// </summary>
    public static bool DrawDebugLines { get; set; } = false;
    /// <summary>
    /// Write
    /// </summary>
    /// <param name="str"></param>
    public static void Write(string str) {
        WriteLine(str, false);
    }
    /// <summary>
    /// Write
    /// </summary>
    /// <param name="str"></param>
    public static void Write(char str) {
        WriteLine(str.ToString(), false);
    }
    /// <summary>
    /// Write
    /// </summary>
    /// <param name="str"></param>
    public static void WriteLine(string str, bool newLine = true) {
        if (BootConsole.CurrentConsoleMode == BootConsoleMode.None) return;
        if (BootConsole.CurrentMode == BootMode.Legacy) {
            BootConsole.WriteLine(str);
            return;
        }
        var chars = ConvertStringToCharArray(str);
        for (int i = 0; i < chars.Length; i++) {
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)chars[i]);
        }
        Allocator.Free(chars);
        if (newLine) 
            NewLine();
    }
    /// <summary>
    /// New Line
    /// </summary>
    public static void NewLine() {
        while ((Native.In8(0x3FD) & 0x20) == 0) { }
        Native.Out8(0x3F8, (byte)'\r');
        while ((Native.In8(0x3FD) & 0x20) == 0) { }
        Native.Out8(0x3F8, (byte)'\n');
    }
    /// <summary>
    /// Convert String to Char Array
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static char[] ConvertStringToCharArray(string input) {
        if (string.IsNullOrEmpty(input)) return Array.Empty<char>();
        char[] charArray = new char[input.Length]; // Allocate the array with the exact length of the string.
        for (int i = 0; i < input.Length; i++) { // Manually iterate through the string and assign characters to the array.
            charArray[i] = input[i];
        }
        return charArray;
    }
}