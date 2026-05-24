using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using LightControls.Core.Models;
using LightControls.Core.OpenRgb;
using LightControls.Core.Settings;
using LightControls.Core.Setup;
using Forms = System.Windows.Forms;

namespace LightControls.Desktop;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private LightControlsSettings _settings = new();
    private OpenRgbBackend? _backend;
    private OpenRgbSetupManager? _setupManager;
    private RgbColor _selectedColor = RgbColor.FromHex("#00A8FF");
    private bool _busy;

    public ObservableCollection<DeviceItem> Devices { get; } = [];

    public ObservableCollection<PresetItem> Presets { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        ShowSetup("Checking lighting support...");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsStore.LoadAsync();
        _backend = new OpenRgbBackend(_settings);
        _setupManager = new OpenRgbSetupManager(_settings, _backend);

        LoadPresets();
        SetSelectedColor(_settings.LastColor);
        await InitializeLightingAsync();
    }

    private async Task InitializeLightingAsync()
    {
        if (_setupManager is null)
        {
            return;
        }

        await RunBusyAsync("Checking lighting support...", async () =>
        {
            var progress = CreateSetupProgress();
            var status = await _setupManager.GetStatusAsync();
            if (status.State == OpenRgbSetupState.ServerRunning)
            {
                ShowMain("Lighting support is ready.");
                await LoadDevicesAsync();
                return;
            }

            if (status.State == OpenRgbSetupState.InstalledButStopped)
            {
                var launchStatus = await _setupManager.EnsureServerRunningAsync(progress);
                await _settingsStore.SaveAsync(_settings);
                if (launchStatus.State == OpenRgbSetupState.ServerRunning)
                {
                    ShowMain("Lighting support is ready.");
                    await LoadDevicesAsync();
                    return;
                }

                ShowSetup(launchStatus.Message);
                return;
            }

            ShowSetup(status.Message);
        });
    }

    private async Task LoadDevicesAsync()
    {
        if (_backend is null)
        {
            return;
        }

        try
        {
            var devices = await _backend.GetDevicesAsync();
            Devices.Clear();
            var selectedIds = _settings.SelectedDeviceIds.ToHashSet(StringComparer.Ordinal);

            foreach (var device in devices)
            {
                var selected = selectedIds.Count == 0 ? device.IsSupported : selectedIds.Contains(device.Id);
                Devices.Add(new DeviceItem(device, selected));
            }

            StatusText.Text = Devices.Count == 0
                ? "No compatible devices were reported by OpenRGB."
                : $"{Devices.Count} device(s) detected.";
        }
        catch (Exception ex)
        {
            ShowSetup($"OpenRGB is installed, but the SDK server is not reachable. {ex.Message}");
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Refreshing devices...", LoadDevicesAsync);
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_backend is null)
        {
            return;
        }

        var selectedIds = Devices.Where(device => device.IsSelected && device.IsEnabled).Select(device => device.Id).ToList();
        if (selectedIds.Count == 0)
        {
            StatusText.Text = "Select at least one compatible device.";
            return;
        }

        await RunBusyAsync("Applying color...", async () =>
        {
            var result = await _backend.ApplyColorAsync(selectedIds, _selectedColor);
            _settings.LastColor = _selectedColor.ToHex();
            _settings.SelectedDeviceIds = selectedIds;
            _settings.Presets = Presets.Select(preset => preset.ToPreset()).ToList();
            await _settingsStore.SaveAsync(_settings);

            var failures = result.Devices.Where(device => !device.Succeeded).ToList();
            StatusText.Text = failures.Count == 0
                ? $"Applied {_selectedColor.ToHex()} to {result.Devices.Count} device(s)."
                : $"Applied with {failures.Count} device issue(s): {string.Join(", ", failures.Select(failure => failure.DeviceName))}";
        });
    }

    private async void SetupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_setupManager is null)
        {
            return;
        }

        await RunBusyAsync("Setting up lighting support...", async () =>
        {
            var progress = CreateSetupProgress();
            var status = await _setupManager.EnsureServerRunningAsync(progress);
            await _settingsStore.SaveAsync(_settings);
            if (status.State == OpenRgbSetupState.ServerRunning)
            {
                ShowMain("Lighting support is ready.");
                await LoadDevicesAsync();
            }
            else
            {
                ShowSetup(status.Message);
            }
        });
    }

    private async void RetrySetupButton_Click(object sender, RoutedEventArgs e)
    {
        await InitializeLightingAsync();
    }

    private void OpenReleasesButton_Click(object sender, RoutedEventArgs e)
    {
        OpenRgbSetupManager.OpenReleasesPage();
    }

    private void PickColorButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(_selectedColor.Red, _selectedColor.Green, _selectedColor.Blue)
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            SetSelectedColor(new RgbColor(dialog.Color.R, dialog.Color.G, dialog.Color.B).ToHex());
        }
    }

    private void SavePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(PresetNameBox.Text)
            ? $"Color {Presets.Count + 1}"
            : PresetNameBox.Text.Trim();

        Presets.Add(new PresetItem(new ColorPreset(name, _selectedColor.ToHex())));
        PresetNameBox.Text = string.Empty;
        _ = PersistCurrentSettingsAsync();
    }

    private void UsePresetButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PresetItem preset)
        {
            return;
        }

        SetSelectedColor(preset.HexColor);
        StatusText.Text = $"Selected preset {preset.Name}.";
    }

    private void DeletePresetButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PresetItem preset)
        {
            return;
        }

        Presets.Remove(preset);
        _ = PersistCurrentSettingsAsync();
    }

    private void LoadPresets()
    {
        Presets.Clear();
        foreach (var preset in _settings.Presets)
        {
            Presets.Add(new PresetItem(preset));
        }
    }

    private void SetSelectedColor(string hexColor)
    {
        _selectedColor = RgbColor.FromHex(hexColor);
        ColorText.Text = _selectedColor.ToHex();
        ColorPreview.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(_selectedColor.Red, _selectedColor.Green, _selectedColor.Blue));
    }

    private async Task PersistCurrentSettingsAsync()
    {
        _settings.LastColor = _selectedColor.ToHex();
        _settings.SelectedDeviceIds = Devices.Where(device => device.IsSelected && device.IsEnabled).Select(device => device.Id).ToList();
        _settings.Presets = Presets.Select(preset => preset.ToPreset()).ToList();
        await _settingsStore.SaveAsync(_settings);
    }

    private IProgress<string> CreateSetupProgress() =>
        new Progress<string>(message =>
        {
            SetupText.Text = message;
            StatusText.Text = message;
        });

    private void ShowSetup(string message)
    {
        SetupPanel.Visibility = Visibility.Visible;
        MainPanel.Visibility = Visibility.Collapsed;
        ApplyButton.IsEnabled = false;
        RefreshButton.IsEnabled = true;
        SetupText.Text = message;
        StatusText.Text = "Lighting support needs setup.";
    }

    private void ShowMain(string message)
    {
        SetupPanel.Visibility = Visibility.Collapsed;
        MainPanel.Visibility = Visibility.Visible;
        ApplyButton.IsEnabled = true;
        RefreshButton.IsEnabled = true;
        StatusText.Text = message;
    }

    private async Task RunBusyAsync(string message, Func<Task> action)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        StatusText.Text = message;
        ApplyButton.IsEnabled = false;
        RefreshButton.IsEnabled = false;
        SetupButton.IsEnabled = false;

        try
        {
            await action();
        }
        finally
        {
            _busy = false;
            ApplyButton.IsEnabled = MainPanel.Visibility == Visibility.Visible;
            RefreshButton.IsEnabled = true;
            SetupButton.IsEnabled = true;
        }
    }
}

public sealed class DeviceItem(RgbDevice device, bool isSelected) : INotifyPropertyChanged
{
    private bool _isSelected = isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id => device.Id;

    public string Name => string.IsNullOrWhiteSpace(device.Vendor) ? device.Name : $"{device.Vendor} {device.Name}";

    public string Details => $"{device.LedCount} LEDs - {device.Status}";

    public bool IsEnabled => device.IsSupported;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

public sealed class PresetItem(ColorPreset preset) : INotifyPropertyChanged
{
    private string _name = preset.Name;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public string HexColor { get; } = preset.HexColor;

    public System.Windows.Media.Brush SwatchBrush
    {
        get
        {
            var color = RgbColor.FromHex(HexColor);
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue));
        }
    }

    public ColorPreset ToPreset() => new(Name, HexColor);
}
