using System.Globalization;

namespace PlcComm.Slmp;

/// <summary>
/// Public helpers for SLMP device address text.
/// </summary>
/// <remarks>
/// These helpers provide a small, documentation-friendly surface for parse, format, and
/// normalization tasks. Use them when you want canonical address text in samples,
/// generated docs, validation tooling, or UI layers.
/// </remarks>
public static class SlmpAddress
{
    /// <summary>Parses one SLMP device string.</summary>
    /// <param name="text">Device text such as <c>D100</c>, <c>X1A</c>, or <c>ZR200</c>.</param>
    /// <returns>The parsed device address.</returns>
    public static SlmpDeviceAddress Parse(string text) => SlmpDeviceParser.Parse(text);

    /// <summary>Parses one SLMP device string using the explicit PLC family.</summary>
    public static SlmpDeviceAddress Parse(string text, SlmpPlcFamily plcFamily) => SlmpDeviceParser.Parse(text, plcFamily);

    /// <summary>Attempts to parse one SLMP device string.</summary>
    /// <param name="text">Device text to parse.</param>
    /// <param name="address">When this method returns <see langword="true"/>, receives the parsed address.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
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

    /// <summary>Attempts to parse one SLMP device string using the explicit PLC family.</summary>
    public static bool TryParse(string text, SlmpPlcFamily plcFamily, out SlmpDeviceAddress address)
    {
        try
        {
            address = Parse(text, plcFamily);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            address = default;
            return false;
        }
    }

    /// <summary>Formats one SLMP device address using canonical device text.</summary>
    /// <param name="address">The parsed device address to format.</param>
    /// <returns>Canonical uppercase address text.</returns>
    /// <remarks>
    /// Hex-addressed device families such as <c>X</c>, <c>Y</c>, <c>B</c>, and <c>W</c>
    /// are emitted in uppercase hexadecimal form.
    /// </remarks>
    public static string Format(SlmpDeviceAddress address)
    {
        string number = FormatNumber(address, null);
        return $"{address.Code}{number}";
    }

    /// <summary>Formats one parsed device address using the explicit PLC family.</summary>
    public static string Format(SlmpDeviceAddress address, SlmpPlcFamily plcFamily)
        => $"{address.Code}{FormatNumber(address, plcFamily)}";

    /// <summary>Normalizes one SLMP device string to canonical text.</summary>
    /// <param name="text">Input device text in any supported spelling.</param>
    /// <returns>The canonical uppercase representation returned by <see cref="Format(SlmpDeviceAddress)"/>.</returns>
    public static string Normalize(string text) => Format(Parse(text));

    /// <summary>Normalizes one SLMP device string using the explicit PLC family.</summary>
    public static string Normalize(string text, SlmpPlcFamily plcFamily) => Format(Parse(text, plcFamily), plcFamily);

    private static string FormatNumber(SlmpDeviceAddress address, SlmpPlcFamily? plcFamily)
    {
        if (plcFamily is SlmpPlcFamily family && SlmpPlcFamilyProfiles.UsesIqFXyOctal(family) &&
            address.Code is SlmpDeviceCode.X or SlmpDeviceCode.Y)
        {
            return Convert.ToString(address.Number, 8)!.ToUpperInvariant();
        }

        return IsHexAddressed(address.Code)
            ? address.Number.ToString("X", CultureInfo.InvariantCulture)
            : address.Number.ToString(CultureInfo.InvariantCulture);
    }

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
