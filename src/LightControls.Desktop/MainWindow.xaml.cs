using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using LightControls.Core;
using LightControls.Core.Abstractions;
using LightControls.Core.Logitech;
using LightControls.Core.Models;
using LightControls.Core.OpenRgb;
using LightControls.Core.Settings;
using LightControls.Core.Setup;
using Forms = System.Windows.Forms;

namespace LightControls.Desktop;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly DispatcherTimer _brightnessApplyTimer;
    private LightControlsSettings _settings = new();
    private IRgbBackend? _backend;
    private OpenRgbSetupManager? _setupManager;
    private RgbColor _selectedColor = RgbColor.FromHex("#00A8FF");
    private bool _busy;
    private bool _suppressColorApply;

    public ObservableCollection<DeviceItem> Devices { get; } = [];

    public ObservableCollection<SwatchItem> BuiltInSwatches { get; } = [];

    public ObservableCollection<SwatchItem> RecentCustomSwatches { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        foreach (var hex in ColorSwatches.BuiltIn)
        {
            BuiltInSwatches.Add(new SwatchItem(hex));
        }

        _brightnessApplyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _brightnessApplyTimer.Tick += BrightnessApplyTimer_Tick;

        DataContext = this;
        ShowSetup("Checking lighting support...");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _suppressColorApply = true;
        _settings = await _settingsStore.LoadAsync();
        var openRgbBackend = new OpenRgbBackend(_settings);
        _backend = new CompositeRgbBackend(openRgbBackend, new LogitechDirectBackend(_settings));
        _setupManager = new OpenRgbSetupManager(_settings, openRgbBackend);

        LoadRecentCustomSwatches();
        SetSelectedColor(_settings.LastColor);
        BrightnessSlider.Value = Math.Clamp(_settings.LastBrightness, 1, 100);
        UpdateBrightnessLabel();
        UpdateRecentCustomEmptyState();
        await InitializeLightingAsync();
        _suppressColorApply = false;
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
            if (_backend is not null && await _backend.IsServerReachableAsync())
            {
                var status = await _setupManager.GetStatusAsync();
                if (status.State != OpenRgbSetupState.ServerRunning
                    && status.State == OpenRgbSetupState.InstalledButStopped)
                {
                    _ = await _setupManager.EnsureServerRunningAsync(progress);
                    await _settingsStore.SaveAsync(_settings);
                }

                ShowMain("Lighting support is ready.");
                await LoadDevicesAsync();
                return;
            }

            var setupStatus = await _setupManager.GetStatusAsync();
            if (setupStatus.State == OpenRgbSetupState.ServerRunning)
            {
                ShowMain("Lighting support is ready.");
                await LoadDevicesAsync();
                return;
            }

            if (setupStatus.State == OpenRgbSetupState.InstalledButStopped)
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

            ShowSetup(setupStatus.Message);
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

            foreach (var device in devices)
            {
                Devices.Add(new DeviceItem(device));
            }

            UpdateDevicesPresentation();
        }
        catch (Exception ex)
        {
            ShowSetup($"OpenRGB is installed, but the SDK server is not reachable. {ex.Message}");
        }
    }

    private void UpdateDevicesPresentation()
    {
        var count = Devices.Count;
        DevicesEmptyPanel.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DevicesListPanel.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
        StatusText.Text = count == 0
            ? "No compatible devices were reported."
            : $"{count} device(s) detected.";
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Refreshing devices...", LoadDevicesAsync);
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        await ApplySelectedColorAsync();
    }

    private async Task ApplySelectedColorAsync()
    {
        if (_backend is null)
        {
            return;
        }

        var deviceIds = Devices.Where(device => device.IsSupported).Select(device => device.Id).ToList();
        if (deviceIds.Count == 0)
        {
            StatusText.Text = "No compatible devices found.";
            return;
        }

        await RunBusyAsync("Applying color...", async () =>
        {
            var brightness = (int)Math.Round(BrightnessSlider.Value);
            var result = await _backend.ApplyColorAsync(deviceIds, _selectedColor, brightness);
            _settings.LastColor = _selectedColor.ToHex();
            _settings.LastBrightness = brightness;
            await _settingsStore.SaveAsync(_settings);

            var failures = result.Devices.Where(device => !device.Succeeded).ToList();
            StatusText.Text = failures.Count == 0
                ? $"Applied {_selectedColor.ToHex()} at {brightness}% to {result.Devices.Count} device(s)."
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

    private async void SwatchButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string hex)
        {
            return;
        }

        await SelectAndApplyColorAsync(hex);
    }

    private async void CustomSwatchButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenColorPickerAsync();
    }

    private async void ColorPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenColorPickerAsync();
    }

    private async Task OpenColorPickerAsync()
    {
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(_selectedColor.Red, _selectedColor.Green, _selectedColor.Blue)
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            var hex = new RgbColor(dialog.Color.R, dialog.Color.G, dialog.Color.B).ToHex();
            RecordRecentCustomColor(hex);
            SetSelectedColor(hex);

            if (MainPanel.Visibility == Visibility.Visible)
            {
                await ApplySelectedColorAsync();
            }
            else
            {
                await PersistCurrentSettingsAsync();
            }
        }
    }

    private async Task SelectAndApplyColorAsync(string hex)
    {
        SetSelectedColor(hex);

        if (_suppressColorApply || MainPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        await ApplySelectedColorAsync();
    }

    private void RecordRecentCustomColor(string hex)
    {
        var normalized = RgbColor.FromHex(hex).ToHex();
        _settings.RecentCustomColors.RemoveAll(color =>
            string.Equals(color, normalized, StringComparison.OrdinalIgnoreCase));
        _settings.RecentCustomColors.Insert(0, normalized);

        if (_settings.RecentCustomColors.Count > ColorSwatches.MaxRecentCustomColors)
        {
            _settings.RecentCustomColors.RemoveRange(
                ColorSwatches.MaxRecentCustomColors,
                _settings.RecentCustomColors.Count - ColorSwatches.MaxRecentCustomColors);
        }

        LoadRecentCustomSwatches();
        UpdateRecentCustomEmptyState();
    }

    private void LoadRecentCustomSwatches()
    {
        RecentCustomSwatches.Clear();
        foreach (var hex in _settings.RecentCustomColors)
        {
            RecentCustomSwatches.Add(new SwatchItem(hex));
        }

        UpdateSwatchSelection();
    }

    private void UpdateRecentCustomEmptyState()
    {
        RecentCustomEmptyText.Visibility = RecentCustomSwatches.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SetSelectedColor(string hexColor)
    {
        _selectedColor = RgbColor.FromHex(hexColor);
        var wpfColor = System.Windows.Media.Color.FromRgb(_selectedColor.Red, _selectedColor.Green, _selectedColor.Blue);
        var brush = new SolidColorBrush(wpfColor);
        ColorText.Text = _selectedColor.ToHex();
        ColorPreview.Background = brush;
        ColorGlowBrush.Color = wpfColor;
        if (ColorPreview.Effect is DropShadowEffect glow)
        {
            glow.Color = wpfColor;
        }

        UpdateSwatchSelection();
    }

    private void UpdateSwatchSelection()
    {
        var selectedHex = _selectedColor.ToHex();
        UpdateSwatchCollectionSelection(BuiltInSwatches, selectedHex);
        UpdateSwatchCollectionSelection(RecentCustomSwatches, selectedHex);
    }

    private static void UpdateSwatchCollectionSelection(IEnumerable<SwatchItem> swatches, string selectedHex)
    {
        foreach (var swatch in swatches)
        {
            swatch.IsSelected = string.Equals(swatch.Hex, selectedHex, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void BrightnessSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Slider slider || slider.ActualWidth <= 0)
        {
            return;
        }

        var clickRatio = Math.Clamp(e.GetPosition(slider).X / slider.ActualWidth, 0, 1);
        slider.Value = slider.Minimum + clickRatio * (slider.Maximum - slider.Minimum);
    }

    private void BrightnessSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ScheduleBrightnessApply();
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdateBrightnessLabel();
        ScheduleBrightnessApply();
    }

    private void ScheduleBrightnessApply()
    {
        if (_suppressColorApply || MainPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        _brightnessApplyTimer.Stop();
        _brightnessApplyTimer.Start();
    }

    private async void BrightnessApplyTimer_Tick(object? sender, EventArgs e)
    {
        _brightnessApplyTimer.Stop();
        await ApplySelectedColorAsync();
    }

    private void UpdateBrightnessLabel()
    {
        BrightnessText.Text = $"{(int)Math.Round(BrightnessSlider.Value)}%";
    }

    private async Task PersistCurrentSettingsAsync()
    {
        _settings.LastColor = _selectedColor.ToHex();
        _settings.LastBrightness = (int)Math.Round(BrightnessSlider.Value);
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
        SetStatusIndicator(StatusKind.Setup);
    }

    private void ShowMain(string message)
    {
        SetupPanel.Visibility = Visibility.Collapsed;
        MainPanel.Visibility = Visibility.Visible;
        ApplyButton.IsEnabled = true;
        RefreshButton.IsEnabled = true;
        StatusText.Text = message;
        SetStatusIndicator(StatusKind.Ready);
    }

    private async Task RunBusyAsync(string message, Func<Task> action)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _brightnessApplyTimer.Stop();
        StatusText.Text = message;
        SetStatusIndicator(StatusKind.Busy);
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
            SetStatusIndicator(MainPanel.Visibility == Visibility.Visible ? StatusKind.Ready : StatusKind.Setup);
        }
    }

    private enum StatusKind
    {
        Ready,
        Busy,
        Setup
    }

    private void SetStatusIndicator(StatusKind kind)
    {
        var color = kind switch
        {
            StatusKind.Ready => System.Windows.Media.Color.FromRgb(0x3D, 0xD6, 0x8C),
            StatusKind.Busy => System.Windows.Media.Color.FromRgb(0xF0, 0xA0, 0x30),
            _ => System.Windows.Media.Color.FromRgb(0xF0, 0x71, 0x78)
        };

        StatusDot.Fill = new SolidColorBrush(color);
    }
}

public sealed class DeviceItem(RgbDevice device)
{
    public string Id => device.Id;

    public string Name => string.IsNullOrWhiteSpace(device.Vendor) ? device.Name : $"{device.Vendor} {device.Name}";

    public string Details => device.Zones.Count > 0
        ? $"{FormatLedCount(device.LedCount)} · {string.Join(", ", device.Zones.Select(zone => $"{zone.Name} ({zone.LedCount})"))} · {device.Status}"
        : $"{FormatLedCount(device.LedCount)} - {device.Status}";

    private static string FormatLedCount(int count) => count == 1 ? "1 LED" : $"{count} LEDs";

    public bool IsSupported => device.IsSupported;
}

public sealed class SwatchItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public SwatchItem(string hex)
    {
        Hex = RgbColor.FromHex(hex).ToHex();
        Brush = CreateBrush(Hex);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Hex { get; }

    public System.Windows.Media.Brush Brush { get; }

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

    private static System.Windows.Media.Brush CreateBrush(string hex)
    {
        var color = RgbColor.FromHex(hex);
        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue));
    }
}
