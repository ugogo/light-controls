using LightControls.Core;
using LightControls.Core.Models;
using LightControls.Core.Settings;
using LightControls.Tests.Fakes;

namespace LightControls.Tests;

public sealed class LightingControllerTests
{
    [Fact]
    public async Task ApplyToSelectedDevicesAsync_UsesSelectedDevicesAndPersistsLastColor()
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "settings.json");
        var store = new SettingsStore(path);
        var settings = new LightControlsSettings
        {
            SelectedDeviceIds = ["keyboard", "mouse"]
        };
        var backend = new FakeRgbBackend();
        var controller = new LightingController(backend, store, settings);

        var color = new RgbColor(1, 2, 3);
        var result = await controller.ApplyToSelectedDevicesAsync(color);
        var persisted = await store.LoadAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(["keyboard", "mouse"], backend.LastDeviceIds);
        Assert.Equal(color, backend.LastColor);
        Assert.Equal("#010203", persisted.LastColor);
    }
}
