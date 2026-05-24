using LightControls.Core.Abstractions;
using LightControls.Core.Models;
using LightControls.Core.Settings;

namespace LightControls.Core.OpenRgb;

public sealed class OpenRgbBackend(LightControlsSettings settings) : IRgbBackend
{
    public async Task<bool> IsServerReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var client = new OpenRgbProtocolClient();
            await client.ConnectAsync(settings.Host, settings.Port, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<RgbDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        await using var client = new OpenRgbProtocolClient();
        await client.ConnectAsync(settings.Host, settings.Port, cancellationToken);
        return await client.GetDevicesAsync(cancellationToken);
    }

    public async Task<ApplyColorResult> ApplyColorAsync(
        IReadOnlyCollection<string> deviceIds,
        RgbColor color,
        int brightnessPercent = 100,
        CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0)
        {
            return ApplyColorResult.Empty;
        }

        await using var client = new OpenRgbProtocolClient();
        await client.ConnectAsync(settings.Host, settings.Port, cancellationToken);

        var devices = await client.GetDevicesAsync(cancellationToken);
        var selected = devices.Where(device => deviceIds.Contains(device.Id)).ToList();
        var results = new List<DeviceApplyResult>();

        foreach (var device in selected)
        {
            if (!device.IsSupported)
            {
                results.Add(new DeviceApplyResult(device.Id, device.Name, false, device.Status));
                continue;
            }

            try
            {
                await client.ApplyColorAsync(device, color, brightnessPercent, cancellationToken);
                results.Add(new DeviceApplyResult(device.Id, device.Name, true, "Applied"));
            }
            catch (Exception ex)
            {
                results.Add(new DeviceApplyResult(device.Id, device.Name, false, ex.Message));
            }
        }

        return new ApplyColorResult(results);
    }
}
