using LightControls.Core.Abstractions;
using LightControls.Core.Models;

namespace LightControls.Tests.Fakes;

internal sealed class FakeRgbBackend : IRgbBackend
{
    public bool ServerReachable { get; set; }

    public List<RgbDevice> Devices { get; } = [];

    public IReadOnlyCollection<string> LastDeviceIds { get; private set; } = [];

    public RgbColor LastColor { get; private set; }

    public Task<bool> IsServerReachableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ServerReachable);

    public Task<IReadOnlyList<RgbDevice>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RgbDevice>>(Devices);

    public Task<ApplyColorResult> ApplyColorAsync(
        IReadOnlyCollection<string> deviceIds,
        RgbColor color,
        CancellationToken cancellationToken = default)
    {
        LastDeviceIds = deviceIds.ToArray();
        LastColor = color;

        var results = deviceIds
            .Select(id => new DeviceApplyResult(id, id, true, "Applied"))
            .ToArray();

        return Task.FromResult(new ApplyColorResult(results));
    }
}
