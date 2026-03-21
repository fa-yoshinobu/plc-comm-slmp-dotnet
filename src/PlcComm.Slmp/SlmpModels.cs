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
/// Recommended protocol settings based on the target PLC model.
/// </summary>
public sealed record SlmpProfileRecommendation(
    SlmpFrameType FrameType,
    SlmpCompatibilityMode CompatibilityMode,
    SlmpProfileClass ProfileClass,
    bool Confident
);

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
/// Information about a node discovered via NODE_SEARCH (command 0x0E30).
/// </summary>
public sealed record SlmpNodeSearchInfo(
    string MacAddress,
    string IpAddress,
    string SubnetMask,
    string DefaultGateway,
    string HostName,
    ushort VendorCode,
    string ModelName,
    ushort ModelCode,
    string Version,
    ushort PortNo,
    ushort ProtocolSetting);

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
/// A decoded SLMP request frame received from the PLC (PLC-initiated communication).
/// </summary>
public sealed record SlmpRequestFrame(
    ushort Serial,
    SlmpTargetAddress Target,
    ushort MonitoringTimer,
    ushort Command,
    ushort Subcommand,
    byte[] Data,
    byte[] Raw);

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
        ("LZ", SlmpDeviceCode.LZ, false),
        ("ZR", SlmpDeviceCode.ZR, false),
        ("RD", SlmpDeviceCode.RD, false),
        ("HG", SlmpDeviceCode.HG, false),
        ("X", SlmpDeviceCode.X, true),
        ("Y", SlmpDeviceCode.Y, true),
        ("M", SlmpDeviceCode.M, false),
        ("L", SlmpDeviceCode.L, false),
        ("V", SlmpDeviceCode.V, false),
        ("B", SlmpDeviceCode.B, true),
        ("D", SlmpDeviceCode.D, false),
        ("W", SlmpDeviceCode.W, true),
        ("S", SlmpDeviceCode.S, false),
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
            if (uint.TryParse(numberPart, hexAddress ? NumberStyles.HexNumber : NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return new SlmpDeviceAddress(code, number);
            }
        }

        var validCodes = string.Join(", ", Prefixes.Select(static p => p.Prefix));
        throw new FormatException(
            $"Invalid SLMP device string '{text}'. " +
            $"Valid device codes: {validCodes}");
    }
}
