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
/// Exception thrown when an SLMP protocol error occurs or the PLC returns an error code.
/// </summary>
public sealed class SlmpException : Exception
{
    public SlmpException(string message, ushort? endCode = null, SlmpCommand? command = null, ushort? subcommand = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EndCode = endCode;
        Command = command;
        Subcommand = subcommand;
    }

    /// <summary>
    /// The end code returned by the PLC (0x0000 for success).
    /// </summary>
    public ushort? EndCode { get; }

    /// <summary>
    /// The SLMP command that triggered the error.
    /// </summary>
    public SlmpCommand? Command { get; }

    /// <summary>
    /// The SLMP subcommand that triggered the error.
    /// </summary>
    public ushort? Subcommand { get; }
}

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
