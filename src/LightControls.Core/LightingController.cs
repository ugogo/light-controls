using LightControls.Core.Abstractions;
using LightControls.Core.Models;
using LightControls.Core.Settings;

namespace LightControls.Core;

public sealed class LightingController(IRgbBackend backend, SettingsStore settingsStore, LightControlsSettings settings)
{
    public async Task<ApplyColorResult> ApplyToSelectedDevicesAsync(RgbColor color, CancellationToken cancellationToken = default)
    {
        var result = await backend.ApplyColorAsync(settings.SelectedDeviceIds, color, settings.LastBrightness, cancellationToken);
        settings.LastColor = color.ToHex();
        await settingsStore.SaveAsync(settings, cancellationToken);
        return result;
    }
}
