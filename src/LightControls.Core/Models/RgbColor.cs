using System.Globalization;

namespace LightControls.Core.Models;

public readonly record struct RgbColor(byte Red, byte Green, byte Blue)
{
    public static RgbColor FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            throw new ArgumentException("Color is required.", nameof(hex));
        }

        var normalized = hex.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length != 6)
        {
            throw new FormatException("Color must use the #RRGGBB format.");
        }

        return new RgbColor(
            byte.Parse(normalized[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(normalized[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(normalized[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    public string ToHex() => $"#{Red:X2}{Green:X2}{Blue:X2}";

    public uint ToOpenRgbColor() => (uint)(Red | (Green << 8) | (Blue << 16));
}
