using LightControls.Core.Abstractions;
using LightControls.Core.Logitech.Hidpp20;
using LightControls.Core.Models;
using LightControls.Core.Settings;

namespace LightControls.Core.Logitech;

public sealed class LogitechDirectBackend(LightControlsSettings settings) : IRgbBackend
{
    public Task<bool> IsServerReachableAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.EnableLogitechDirect)
        {
            return Task.FromResult(false);
        }

        return Task.Run(() => Hidpp20Session.TryOpen(out _, out _), cancellationToken);
    }

    public Task<IReadOnlyList<RgbDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.EnableLogitechDirect)
        {
            return Task.FromResult<IReadOnlyList<RgbDevice>>([]);
        }

        return Task.Run<IReadOnlyList<RgbDevice>>(() =>
        {
            if (!Hidpp20Session.TryOpen(out _, out _))
            {
                return [];
            }

            return
            [
                new RgbDevice(
                    LogitechDeviceIds.ProX2Superlight2DeviceId,
                    -1,
                    LogitechDeviceIds.ProX2Superlight2Name,
                    "Logitech",
                    "RGB (direct HID++)",
                    string.Empty,
                    "HID",
                    1,
                    true,
                    "Ready")
            ];
        }, cancellationToken);
    }

    public Task<ApplyColorResult> ApplyColorAsync(
        IReadOnlyCollection<DeviceColorApply> applies,
        CancellationToken cancellationToken = default)
    {
        var apply = applies.FirstOrDefault(candidate =>
            string.Equals(candidate.DeviceId, LogitechDeviceIds.ProX2Superlight2DeviceId, StringComparison.Ordinal));
        if (!settings.EnableLogitechDirect || apply is null)
        {
            return Task.FromResult(ApplyColorResult.Empty);
        }

        return Task.Run(() =>
        {
            if (!Hidpp20Session.TryOpen(out var session, out var openError) || session is null)
            {
                return new ApplyColorResult(
                [
                    new DeviceApplyResult(
                        LogitechDeviceIds.ProX2Superlight2DeviceId,
                        LogitechDeviceIds.ProX2Superlight2Name,
                        false,
                        openError ?? "Mouse not found")
                ]);
            }

            using (session)
            {
                var adjusted = apply.Color.WithBrightness(apply.BrightnessPercent);
                var succeeded = session.TrySetPowerLedColor(adjusted.Red, adjusted.Green, adjusted.Blue, out var error);
                return new ApplyColorResult(
                [
                    new DeviceApplyResult(
                        LogitechDeviceIds.ProX2Superlight2DeviceId,
                        LogitechDeviceIds.ProX2Superlight2Name,
                        succeeded,
                        succeeded ? "Applied" : error ?? "Failed")
                ]);
            }
        }, cancellationToken);
    }
}
