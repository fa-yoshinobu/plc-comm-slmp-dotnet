using System.Globalization;

namespace PlcComm.Slmp;

/// <summary>
/// Public helpers for SLMP device address text.
/// </summary>
public static class SlmpAddress
{
    /// <summary>Parses one SLMP device string.</summary>
    public static SlmpDeviceAddress Parse(string text) => SlmpDeviceParser.Parse(text);

    /// <summary>Attempts to parse one SLMP device string.</summary>
    public static bool TryParse(string text, out SlmpDeviceAddress address)
    {
        try
        {
            address = Parse(text);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            address = default;
            return false;
        }
    }

    /// <summary>Formats one SLMP device address using canonical device text.</summary>
    public static string Format(SlmpDeviceAddress address)
    {
        string number = IsHexAddressed(address.Code)
            ? address.Number.ToString("X", CultureInfo.InvariantCulture)
            : address.Number.ToString(CultureInfo.InvariantCulture);
        return $"{address.Code}{number}";
    }

    /// <summary>Normalizes one SLMP device string to canonical text.</summary>
    public static string Normalize(string text) => Format(Parse(text));

    private static bool IsHexAddressed(SlmpDeviceCode code)
        => code is SlmpDeviceCode.X
            or SlmpDeviceCode.Y
            or SlmpDeviceCode.B
            or SlmpDeviceCode.W
            or SlmpDeviceCode.SB
            or SlmpDeviceCode.SW
            or SlmpDeviceCode.DX
            or SlmpDeviceCode.DY;
}
