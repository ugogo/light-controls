namespace LightControls.Core.Logitech.Hidpp20;

internal static class Hidpp20Constants
{
    public const byte ReportIdShort = 0x10;
    public const byte ReportIdLong = 0x11;

    public const byte ReceiverDeviceIndex = 0xFF;
    public const byte DefaultMouseDeviceIndex = 0x01;

    public const ushort FeatureRoot = 0x0000;
    public const ushort FeatureColorLedEffects = 0x8070;
    public const ushort FeatureModeStatus = 0x8090;
    public const ushort FeatureLedSoftwareControl = 0x1300;

    public const byte CmdRootGetFeature = 0x00;

    /// <summary>Alternate root get-feature function seen on some Logitech receivers.</summary>
    public const byte CmdRootGetFeatureAlt = 0x08;
    public const byte CmdColorLedEffectsSetZoneEffect = 0x30;
    public const byte CmdLedSwControlSetLedState = 0x50;
    public const byte CmdModeStatusSetSolidColor = 0x30;
}
