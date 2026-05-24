namespace LightControls.Core.Models;

public sealed record RgbDevice(
    string Id,
    int ControllerIndex,
    string Name,
    string Vendor,
    string Description,
    string Serial,
    string Location,
    int LedCount,
    bool IsSupported,
    string Status);
