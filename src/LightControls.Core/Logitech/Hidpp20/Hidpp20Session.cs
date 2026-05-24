using HidSharp;

namespace LightControls.Core.Logitech.Hidpp20;

internal sealed class Hidpp20Session : IDisposable
{
    private const int ShortReportLength = 7;
    private const int LongReportLength = 20;

    private readonly HidDevice _device;
    private readonly HidStream _stream;
    private byte _deviceIndex;
    private readonly Dictionary<ushort, byte> _featureIndexCache = [];

    public Hidpp20Session(HidDevice device, byte deviceIndex)
    {
        _device = device;
        _stream = device.Open();
        _deviceIndex = deviceIndex;
    }

    public static IReadOnlyList<HidDevice> FindCandidateDevices()
    {
        return DeviceList.Local
            .GetHidDevices(LogitechDeviceIds.VendorId)
            .Where(IsCandidateInterface)
            .Where(device => device.GetMaxOutputReportLength() >= LongReportLength)
            .OrderByDescending(device => device.GetMaxOutputReportLength())
            .ToList();
    }

    public static bool TryOpen(out Hidpp20Session? session, out string? error)
    {
        session = null;
        error = null;

        foreach (var device in FindCandidateDevices())
        {
            try
            {
                var deviceIndex = Hidpp20Constants.DefaultMouseDeviceIndex;

                var candidate = new Hidpp20Session(device, deviceIndex);
                if (candidate.ProbeProX2Mouse())
                {
                    session = candidate;
                    return true;
                }

                candidate.Dispose();
            }
            catch
            {
                // Try the next HID interface.
            }
        }

        error = "Logitech G Pro X Superlight 2 was not found on a compatible HID interface. "
                + "Connect the mouse (USB or LIGHTSPEED dongle) and close other RGB apps if detection fails.";
        return false;
    }

    public bool ProbeProX2Mouse()
    {
        return TryGetFeatureIndex(Hidpp20Constants.FeatureModeStatus, out _)
            || TryGetFeatureIndex(Hidpp20Constants.FeatureColorLedEffects, out _)
            || TryGetFeatureIndex(Hidpp20Constants.FeatureLedSoftwareControl, out _);
    }

    public bool TryGetFeatureIndex(ushort featureId, out byte featureIndex)
    {
        if (_featureIndexCache.TryGetValue(featureId, out featureIndex))
        {
            return featureIndex != 0;
        }

        foreach (var command in new[] { Hidpp20Constants.CmdRootGetFeature, Hidpp20Constants.CmdRootGetFeatureAlt })
        {
            var request = CreateShortReport(command);
            request[4] = (byte)(featureId >> 8);
            request[5] = (byte)(featureId & 0xFF);

            if (TryRequest(request, out var response) && response[4] != 0)
            {
                featureIndex = response[4];
                _featureIndexCache[featureId] = featureIndex;
                return true;
            }
        }

        var savedIndex = _deviceIndex;
        foreach (var deviceIndex in new[] { savedIndex, Hidpp20Constants.ReceiverDeviceIndex })
        {
            _deviceIndex = deviceIndex;
            foreach (var command in new[] { Hidpp20Constants.CmdRootGetFeature, Hidpp20Constants.CmdRootGetFeatureAlt })
            {
                var request = CreateShortReport(command);
                request[4] = (byte)(featureId >> 8);
                request[5] = (byte)(featureId & 0xFF);

                if (TryRequest(request, out var response) && response[4] != 0)
                {
                    _deviceIndex = savedIndex;
                    featureIndex = response[4];
                    _featureIndexCache[featureId] = featureIndex;
                    return true;
                }
            }
        }

        _deviceIndex = savedIndex;
        _featureIndexCache[featureId] = 0;
        featureIndex = 0;
        return false;
    }

    public bool TrySetPowerLedColor(byte red, byte green, byte blue, out string? error)
    {
        if (TrySetViaModeStatus(red, green, blue, out error))
        {
            return true;
        }

        if (TrySetViaColorLedEffects(red, green, blue, out error))
        {
            return true;
        }

        if (TrySetViaLedSoftwareControl(red, green, blue, out error))
        {
            return true;
        }

        error ??= "No supported HID++ lighting feature responded on this mouse.";
        return false;
    }

    private bool TrySetViaModeStatus(byte red, byte green, byte blue, out string? error)
    {
        error = null;
        if (!TryGetFeatureIndex(Hidpp20Constants.FeatureModeStatus, out var featureIndex))
        {
            error = "MODE STATUS (0x8090) is not available.";
            return false;
        }

        var report = CreateLongReport(Hidpp20Constants.CmdModeStatusSetSolidColor);
        report[2] = featureIndex;
        report[4] = 0x00;
        report[5] = 0x01;
        report[6] = red;
        report[7] = green;
        report[8] = blue;

        if (TryRequest(report, out _))
        {
            return true;
        }

        error = "MODE STATUS color command was rejected by the mouse.";
        return false;
    }

    private bool TrySetViaColorLedEffects(byte red, byte green, byte blue, out string? error)
    {
        error = null;
        if (!TryGetFeatureIndex(Hidpp20Constants.FeatureColorLedEffects, out var featureIndex))
        {
            error = "COLOR_LED_EFFECTS (0x8070) is not available.";
            return false;
        }

        var report = CreateLongReport(Hidpp20Constants.CmdColorLedEffectsSetZoneEffect);
        report[2] = featureIndex;
        report[4] = 0x00;
        report[12] = 0x01;
        report[5] = 0x01;
        report[6] = red;
        report[7] = green;
        report[8] = blue;
        report[9] = 0xFF;

        if (TryRequest(report, out _))
        {
            return true;
        }

        error = "COLOR_LED_EFFECTS command was rejected by the mouse.";
        return false;
    }

    private bool TrySetViaLedSoftwareControl(byte red, byte green, byte blue, out string? error)
    {
        error = null;
        if (!TryGetFeatureIndex(Hidpp20Constants.FeatureLedSoftwareControl, out var featureIndex))
        {
            error = "LED software control (0x1300) is not available.";
            return false;
        }

        var enable = CreateShortReport(0x30);
        enable[2] = featureIndex;
        enable[4] = 0x01;
        TryRequest(enable, out _);

        var report = CreateShortReport(Hidpp20Constants.CmdLedSwControlSetLedState);
        report[2] = featureIndex;
        report[4] = 0x00;
        report[5] = 0x00;
        report[6] = 0x01;
        report[7] = red;
        report[8] = green;
        report[9] = blue;

        if (TryRequest(report, out _))
        {
            return true;
        }

        error = "LED software control command was rejected by the mouse.";
        return false;
    }

    private byte[] CreateShortReport(byte command) =>
    [
        Hidpp20Constants.ReportIdShort,
        _deviceIndex,
        0x00,
        command
    ];

    private byte[] CreateLongReport(byte command)
    {
        var report = new byte[LongReportLength];
        report[0] = Hidpp20Constants.ReportIdLong;
        report[1] = _deviceIndex;
        report[3] = command;
        return report;
    }

    private bool TryRequest(byte[] request, out byte[] response)
    {
        response = new byte[_device.GetMaxInputReportLength()];

        var writeBuffer = PadReport(request);
        _stream.Write(writeBuffer);

        var readBuffer = new byte[response.Length];
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var read = _stream.Read(readBuffer, 0, readBuffer.Length);
            if (read > 0)
            {
                Array.Copy(readBuffer, response, Math.Min(readBuffer.Length, response.Length));
                return response[0] == request[0] + 1 || response[0] == Hidpp20Constants.ReportIdLong;
            }

            Thread.Sleep(5);
        }

        return false;
    }

    private byte[] PadReport(byte[] report)
    {
        var reportLength = Math.Max(_device.GetMaxOutputReportLength(), report.Length);
        if (report.Length >= reportLength)
        {
            return report;
        }

        var padded = new byte[reportLength];
        Array.Copy(report, padded, report.Length);
        return padded;
    }

    private static bool IsCandidateInterface(HidDevice device)
    {
        if (device.ProductID == LogitechDeviceIds.ProX2MouseProductId)
        {
            return true;
        }

        foreach (var receiverId in LogitechDeviceIds.LightspeedReceiverProductIds)
        {
            if (device.ProductID == receiverId)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
