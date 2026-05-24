using LightControls.Core.Models;

namespace LightControls.Core.Settings;

public sealed class LightControlsSettings
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 6742;

    public string? OpenRgbExecutablePath { get; set; }

    /// <summary>
    /// When true, detects and controls Logitech PRO X Superlight 2 power LED via direct HID++ (no OpenRGB).
    /// </summary>
    public bool EnableLogitechDirect { get; set; } = true;

    public string LastColor { get; set; } = "#00A8FF";

    /// <summary>Brightness level applied to devices, 0–100.</summary>
    public int LastBrightness { get; set; } = 100;

    public List<string> SelectedDeviceIds { get; set; } = [];

    /// <summary>Recently picked custom colors, most recent first.</summary>
    public List<string> RecentCustomColors { get; set; } = [];
}
