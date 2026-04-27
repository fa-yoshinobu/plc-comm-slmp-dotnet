using System.Globalization;

namespace PlcComm.Slmp;

/// <summary>
/// Represents the destination routing fields for an SLMP frame.
/// </summary>
/// <param name="Network">Network number (0x00 for local network).</param>
/// <param name="Station">Station number (0xFF for control CPU).</param>
/// <param name="ModuleIo">Module I/O number (0x03FF for own station).</param>
/// <param name="Multidrop">Multidrop station number (0x00 for no multidrop).</param>
public readonly record struct SlmpTargetAddress(byte Network = 0x00, byte Station = 0xFF, ushort ModuleIo = 0x03FF, byte Multidrop = 0x00);

/// <summary>
/// Represents a specific PLC device and its numeric address.
/// </summary>
/// <param name="Code">The device type code (e.g., D, M, X, Y).</param>
/// <param name="Number">The numeric address of the device.</param>
public readonly record struct SlmpDeviceAddress(SlmpDeviceCode Code, uint Number)
{
    /// <summary>
    /// Returns the string representation of the device address (e.g., "D100").
    /// </summary>
    public override string ToString() => $"{Code}{Number}";
}

/// <summary>
/// Information about the PLC model and type name.
/// </summary>
/// <param name="Model">The model name string.</param>
/// <param name="ModelCode">Internal model code.</param>
/// <param name="HasModelCode">True if the model code is valid.</param>
public sealed record SlmpTypeNameInfo(string Model, ushort ModelCode, bool HasModelCode);

/// <summary>
/// Decoded CPU operation state read from <c>SD203</c>.
/// </summary>
/// <param name="Status">Decoded PLC operation state.</param>
/// <param name="RawStatusWord">Full raw word read from <c>SD203</c>.</param>
/// <param name="RawCode">Lower 4-bit masked status code from <c>SD203</c>.</param>
public sealed record SlmpCpuOperationState(
    SlmpCpuOperationStatus Status,
    ushort RawStatusWord,
    byte RawCode);

/// <summary>
/// Description for a contiguous block of devices to read.
/// </summary>
public sealed record SlmpBlockRead(SlmpDeviceAddress Device, ushort Points);

/// <summary>
/// Description for a contiguous block of devices to write.
/// </summary>
public sealed record SlmpBlockWrite(SlmpDeviceAddress Device, IReadOnlyList<ushort> Values);

/// <summary>
/// Configuration for block write operations.
/// </summary>
public sealed record SlmpBlockWriteOptions(bool SplitMixedBlocks = false, bool RetryMixedOnError = false);

/// <summary>
/// A raw frame captured by <see cref="SlmpClient.TraceHook"/>.
/// </summary>
public record SlmpTraceFrame(SlmpTraceDirection Direction, byte[] Data, DateTime Timestamp);

/// <summary>
/// Result returned by <c>RunMonitorCycleAsync</c>.
/// </summary>
/// <param name="WordValues">16-bit word values for the registered word devices (in registration order).</param>
/// <param name="DwordValues">32-bit values for the registered DWord devices (in registration order).</param>
public sealed record SlmpMonitorResult(ushort[] WordValues, uint[] DwordValues);

/// <summary>
/// Describes one array label to read. <see cref="UnitSpecification"/>: 0 = bit, 1 = byte.
/// <see cref="ArrayDataLength"/> is in units defined by <see cref="UnitSpecification"/>.
/// </summary>
public sealed record SlmpLabelArrayReadPoint(string Label, byte UnitSpecification, ushort ArrayDataLength);

/// <summary>
/// Describes one array label to write, including the raw data bytes.
/// </summary>
public sealed record SlmpLabelArrayWritePoint(string Label, byte UnitSpecification, ushort ArrayDataLength, byte[] Data);

/// <summary>
/// Describes one random label write point.
/// </summary>
public sealed record SlmpLabelRandomWritePoint(string Label, byte[] Data);

/// <summary>
/// Result item returned by <c>ReadArrayLabelsAsync</c>.
/// </summary>
public sealed record SlmpLabelArrayReadResult(byte DataTypeId, byte UnitSpecification, ushort ArrayDataLength, byte[] Data);

/// <summary>
/// Result item returned by <c>ReadRandomLabelsAsync</c>.
/// </summary>
public sealed record SlmpLabelRandomReadResult(byte DataTypeId, byte Spare, ushort ReadDataLength, byte[] Data);

/// <summary>
/// Represents the decoded state of a single long timer or long retentive timer device.
/// </summary>
/// <param name="Index">The device number (e.g. 0 for LTN0).</param>
/// <param name="Device">The device address string (e.g. "LTN0").</param>
/// <param name="CurrentValue">32-bit current value (two 16-bit words combined).</param>
/// <param name="Contact">True when the timer contact is ON.</param>
/// <param name="Coil">True when the timer coil is ON.</param>
/// <param name="StatusWord">Raw status word (word index 2 in the 4-word block).</param>
/// <param name="RawWords">The four raw 16-bit words that make up this timer entry.</param>
public sealed record SlmpLongTimerResult(
    int Index,
    string Device,
    uint CurrentValue,
    bool Contact,
    bool Coil,
    ushort StatusWord,
    ushort[] RawWords);

/// <summary>
/// Utility for parsing device address strings into <see cref="SlmpDeviceAddress"/>.
/// </summary>
public static class SlmpDeviceParser
{
    private static readonly (string Prefix, SlmpDeviceCode Code, bool HexAddress)[] Prefixes =
    [
        ("LSTS", SlmpDeviceCode.LSTS, false),
        ("LSTC", SlmpDeviceCode.LSTC, false),
        ("LSTN", SlmpDeviceCode.LSTN, false),
        ("LTS", SlmpDeviceCode.LTS, false),
        ("LTC", SlmpDeviceCode.LTC, false),
        ("LTN", SlmpDeviceCode.LTN, false),
        ("STS", SlmpDeviceCode.STS, false),
        ("STC", SlmpDeviceCode.STC, false),
        ("STN", SlmpDeviceCode.STN, false),
        ("SM", SlmpDeviceCode.SM, false),
        ("SD", SlmpDeviceCode.SD, false),
        ("TS", SlmpDeviceCode.TS, false),
        ("TC", SlmpDeviceCode.TC, false),
        ("TN", SlmpDeviceCode.TN, false),
        ("CS", SlmpDeviceCode.CS, false),
        ("CC", SlmpDeviceCode.CC, false),
        ("CN", SlmpDeviceCode.CN, false),
        ("SB", SlmpDeviceCode.SB, true),
        ("SW", SlmpDeviceCode.SW, true),
        ("DX", SlmpDeviceCode.DX, true),
        ("DY", SlmpDeviceCode.DY, true),
        ("LCS", SlmpDeviceCode.LCS, false),
        ("LCC", SlmpDeviceCode.LCC, false),
        ("LCN", SlmpDeviceCode.LCN, false),
        ("LZ", SlmpDeviceCode.LZ, false),
        ("ZR", SlmpDeviceCode.ZR, false),
        ("RD", SlmpDeviceCode.RD, false),
        ("HG", SlmpDeviceCode.HG, false),
        ("X", SlmpDeviceCode.X, true),
        ("Y", SlmpDeviceCode.Y, true),
        ("M", SlmpDeviceCode.M, false),
        ("L", SlmpDeviceCode.L, false),
        ("F", SlmpDeviceCode.F, false),
        ("V", SlmpDeviceCode.V, false),
        ("B", SlmpDeviceCode.B, true),
        ("D", SlmpDeviceCode.D, false),
        ("W", SlmpDeviceCode.W, true),
        ("Z", SlmpDeviceCode.Z, false),
        ("R", SlmpDeviceCode.R, false),
        ("G", SlmpDeviceCode.G, false),
    ];

    /// <summary>
    /// Parses a device string (e.g., "D100", "X1F") into a <see cref="SlmpDeviceAddress"/>.
    /// </summary>
    /// <param name="text">The device string to parse.</param>
    /// <returns>A parsed device address object.</returns>
    /// <exception cref="ArgumentException">Thrown when text is null or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when the device format is invalid.</exception>
    public static SlmpDeviceAddress Parse(string text)
        => Parse(text, null);

    /// <summary>
    /// Parses a device string using one explicit PLC family.
    /// </summary>
    public static SlmpDeviceAddress Parse(string text, SlmpPlcFamily plcFamily)
        => Parse(text, (SlmpPlcFamily?)plcFamily);

    internal static SlmpDeviceAddress ParseForHighLevel(string text, SlmpPlcFamily? plcFamily)
    {
        var device = Parse(text, plcFamily);
        if (plcFamily is null && device.Code is SlmpDeviceCode.X or SlmpDeviceCode.Y)
        {
            throw new FormatException(
                "X/Y string addresses require explicit PlcFamily. Use IqF for FX/iQ-F targets, choose an explicit non-iQ-F family, or pass a numeric SlmpDeviceAddress.");
        }

        return device;
    }

    private static SlmpDeviceAddress Parse(string text, SlmpPlcFamily? plcFamily)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Device text is required.", nameof(text));
        }

        var token = text.Trim().ToUpperInvariant();
        foreach (var (prefix, code, hexAddress) in Prefixes)
        {
            if (!token.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var numberPart = token[prefix.Length..];
            if (TryParseDeviceNumber(numberPart, code, hexAddress, plcFamily, out var number))
            {
                return new SlmpDeviceAddress(code, number);
            }

            throw new FormatException(
                $"Invalid SLMP device number '{numberPart}' for device code '{prefix}' in '{text}'.");
        }

        var validCodes = string.Join(", ", Prefixes.Select(static p => p.Prefix));
        throw new FormatException(
            $"Invalid SLMP device string '{text}'. " +
            $"Valid device codes: {validCodes}");
    }

    private static bool TryParseDeviceNumber(
        string text,
        SlmpDeviceCode code,
        bool hexAddress,
        SlmpPlcFamily? plcFamily,
        out uint number)
    {
        if (plcFamily is SlmpPlcFamily family &&
            SlmpPlcFamilyProfiles.UsesIqFXyOctal(family) &&
            code is SlmpDeviceCode.X or SlmpDeviceCode.Y)
        {
            return TryConvertFromOctal(text, out number);
        }

        return uint.TryParse(
            text,
            hexAddress ? NumberStyles.HexNumber : NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static bool TryConvertFromOctal(string text, out uint number)
    {
        number = 0;
        foreach (var ch in text)
        {
            if (ch is < '0' or > '7')
            {
                return false;
            }

            var digit = (uint)(ch - '0');
            if (number > ((uint.MaxValue - digit) / 8))
            {
                return false;
            }

            number = (number * 8) + digit;
        }

        return true;
    }
}
