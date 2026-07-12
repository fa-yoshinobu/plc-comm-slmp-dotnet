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
    /// <summary>Parses one SLMP device string using the explicit PLC profile.</summary>
    public static SlmpDeviceAddress Parse(string text, SlmpPlcProfile plcProfile) => SlmpDeviceParser.Parse(text, plcProfile);

    /// <summary>Attempts to parse one SLMP device string using the explicit PLC profile.</summary>
    public static bool TryParse(string text, SlmpPlcProfile plcProfile, out SlmpDeviceAddress address)
    {
        try
        {
            address = Parse(text, plcProfile);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException)
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
        string number = FormatNumber(address, address.PlcProfile);
        return $"{address.Code}{number}";
    }

    /// <summary>Normalizes one SLMP device string using the explicit PLC profile.</summary>
    public static string Normalize(string text, SlmpPlcProfile plcProfile) => Format(Parse(text, plcProfile));

    private static string FormatNumber(SlmpDeviceAddress address, SlmpPlcProfile plcProfile)
    {
        if (SlmpPlcProfiles.UsesIqFXyOctal(plcProfile) &&
            address.Code is SlmpDeviceCode.X or SlmpDeviceCode.Y)
        {
            return Convert.ToString(address.Number, 8)!.ToUpperInvariant();
        }

        return IsHexAddressed(address.Code)
            ? address.Number.ToString("X", CultureInfo.InvariantCulture)
            : address.Number.ToString(CultureInfo.InvariantCulture);
    }

    private static void ThrowIfDeviceCodeUnsupportedForProfile(SlmpDeviceCode code, SlmpPlcProfile PlcProfile)
    {
        if (SlmpPlcProfiles.IsDeviceCodeUnsupported(code, PlcProfile))
        {
            var profileId = SlmpPlcProfiles.ToCanonicalString(PlcProfile);
            throw new NotSupportedException(
                $"SLMP device code '{code}' is not supported for PlcProfile '{profileId}'.");
        }
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
