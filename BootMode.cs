namespace guideXOS;

/// <summary>
/// Centralized boot mode switch.
/// Ordering is intentional: higher value => more features enabled.
/// </summary>
public enum BootMode
{
    UltraMinimal = 0,
    MinimalGui = 1,
    Normal = 2,
}

/// <summary>
/// Global boot configuration.
/// Default mode must be UltraMinimal.
/// </summary>
public static class BootConfig
{
    public static BootMode CurrentMode { get; set; } = BootMode.UltraMinimal;
}