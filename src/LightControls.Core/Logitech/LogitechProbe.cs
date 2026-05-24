namespace LightControls.Core.Logitech;

public static class LogitechProbe
{
    public static string Run()
    {
        if (!Hidpp20.Hidpp20Session.TryOpen(out var session, out var error))
        {
            return "OPEN FAILED: " + error;
        }

        using (session)
        {
            var lines = new List<string> { "Mouse HID session opened." };
            lines.Add($"8090: {session.TryGetFeatureIndex(Hidpp20.Hidpp20Constants.FeatureModeStatus, out var modeStatus)} -> {modeStatus}");
            lines.Add($"8070: {session.TryGetFeatureIndex(Hidpp20.Hidpp20Constants.FeatureColorLedEffects, out var colorLed)} -> {colorLed}");
            lines.Add($"1300: {session.TryGetFeatureIndex(Hidpp20.Hidpp20Constants.FeatureLedSoftwareControl, out var swLed)} -> {swLed}");

            var ok = session.TrySetPowerLedColor(255, 0, 0, out var setError);
            lines.Add($"SET RED: {ok}" + (setError is null ? string.Empty : $" ({setError})"));
            return string.Join(Environment.NewLine, lines);
        }
    }
}
