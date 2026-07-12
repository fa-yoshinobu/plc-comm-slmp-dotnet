using System.Globalization;

namespace PlcComm.Slmp;

/// <summary>
/// Named SLMP request-header module I/O numbers for CPU routing.
/// </summary>
/// <remarks>
/// Use these constants with <see cref="SlmpTargetAddress.ModuleIo"/> when routing
/// a request to a multi-CPU or redundant CPU target. Values are from the SLMP
/// specification SH080956 request destination module I/O number field. The
/// default own-station target remains <see cref="OwnStation"/>.
/// </remarks>
public static class SlmpModuleIo
{
    /// <summary>Control system CPU in a redundant CPU system.</summary>
    public const ushort ControlSystemCpu = 0x03D0;

    /// <summary>Standby system CPU in a redundant CPU system.</summary>
    public const ushort StandbySystemCpu = 0x03D1;

    /// <summary>System A CPU in a redundant CPU system.</summary>
    public const ushort SystemACpu = 0x03D2;

    /// <summary>System B CPU in a redundant CPU system.</summary>
    public const ushort SystemBCpu = 0x03D3;

    /// <summary>CPU No. 1 in a multi-CPU system.</summary>
    public const ushort MultipleCpu1 = 0x03E0;

    /// <summary>CPU No. 2 in a multi-CPU system.</summary>
    public const ushort MultipleCpu2 = 0x03E1;

    /// <summary>CPU No. 3 in a multi-CPU system.</summary>
    public const ushort MultipleCpu3 = 0x03E2;

    /// <summary>CPU No. 4 in a multi-CPU system.</summary>
    public const ushort MultipleCpu4 = 0x03E3;

    /// <summary>Remote head No. 1 route.</summary>
    public const ushort RemoteHead1 = MultipleCpu1;

    /// <summary>Remote head No. 2 route.</summary>
    public const ushort RemoteHead2 = MultipleCpu2;

    /// <summary>Control system remote head route.</summary>
    public const ushort ControlSystemRemoteHead = ControlSystemCpu;

    /// <summary>Standby system remote head route.</summary>
    public const ushort StandbySystemRemoteHead = StandbySystemCpu;

    /// <summary>Own station route.</summary>
    public const ushort OwnStation = 0x03FF;
}

/// <summary>
/// Represents the destination routing fields for an SLMP frame.
/// </summary>
/// <param name="Network">Network number (0x00 for local network).</param>
/// <param name="Station">Station number (0xFF for the connected station).</param>
/// <param name="ModuleIo">Module I/O number (0x03FF for own station).</param>
/// <param name="Multidrop">Multidrop station number (0x00 for no multidrop).</param>
public readonly record struct SlmpTargetAddress(byte Network, byte Station, ushort ModuleIo, byte Multidrop)
{
    /// <summary>An explicit directly connected own-station route.</summary>
    public static SlmpTargetAddress OwnStation { get; } = new(0x00, 0xFF, SlmpModuleIo.OwnStation, 0x00);
}

/// <summary>
/// Represents a specific PLC device and its numeric address.
/// </summary>
public readonly record struct SlmpDeviceAddress
{
    /// <summary>Initializes and validates a profile-bound semantic device address.</summary>
    public SlmpDeviceAddress(SlmpDeviceCode code, uint number, SlmpPlcProfile plcProfile)
    {
        Code = code;
        Number = number;
        PlcProfile = SlmpPlcProfiles.ValidateConnectionProfile(plcProfile);
        if (SlmpPlcProfiles.IsDeviceCodeUnsupported(code, PlcProfile))
        {
            throw new NotSupportedException(
                $"SLMP device code '{code}' is not supported for PlcProfile '{SlmpPlcProfiles.ToCanonicalString(PlcProfile)}'.");
        }
    }

    /// <summary>Gets the device code.</summary>
    public SlmpDeviceCode Code { get; }

    /// <summary>Gets the wire-level numeric address.</summary>
    public uint Number { get; }

    /// <summary>Gets the canonical PLC profile bound to this address.</summary>
    public SlmpPlcProfile PlcProfile { get; }

    /// <summary>
    /// Returns the string representation of the device address (e.g., "D100").
    /// </summary>
    public override string ToString() => SlmpAddress.Format(this);
}

/// <summary>
/// Profile-independent wire address for internal frame vectors and maintainer diagnostics.
/// It is intentionally not accepted by the semantic client APIs.
/// </summary>
internal readonly record struct SlmpRawDeviceAddress(SlmpDeviceCode Code, uint Number);

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
/// A raw frame captured by the internal maintainer trace hook.
/// </summary>
internal sealed record SlmpTraceFrame(SlmpTraceDirection Direction, byte[] Data, DateTime Timestamp);

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
        ("S", SlmpDeviceCode.S, false),
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
    /// <param name="plcProfile">The canonical PLC profile that defines address interpretation.</param>
    /// <returns>A parsed device address object.</returns>
    /// <exception cref="ArgumentException">Thrown when text is null or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when the device format is invalid.</exception>
    public static SlmpDeviceAddress Parse(string text, SlmpPlcProfile plcProfile)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Device text is required.", nameof(text));
        }

        plcProfile = SlmpPlcProfiles.ValidateConnectionProfile(plcProfile);

        var token = text.Trim().ToUpperInvariant();
        foreach (var (prefix, code, hexAddress) in Prefixes)
        {
            if (!token.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var numberPart = token[prefix.Length..];
            ThrowIfDeviceCodeUnsupportedForProfile(prefix, code, plcProfile);
            if (TryParseDeviceNumber(numberPart, code, hexAddress, plcProfile, out var number))
            {
                return new SlmpDeviceAddress(code, number, plcProfile);
            }

            throw new FormatException(
                $"Invalid SLMP device number '{numberPart}' for device code '{prefix}' in '{text}'.");
        }

        var validCodes = string.Join(", ", Prefixes.Select(static p => p.Prefix));
        throw new FormatException(
            $"Invalid SLMP device string '{text}'. " +
            $"Valid device codes: {validCodes}");
    }

    private static void ThrowIfDeviceCodeUnsupportedForProfile(
        string prefix,
        SlmpDeviceCode code,
        SlmpPlcProfile plcProfile)
    {
        if (SlmpPlcProfiles.IsDeviceCodeUnsupported(code, plcProfile))
        {
            var profileId = SlmpPlcProfiles.ToCanonicalString(plcProfile);
            throw new NotSupportedException(
                $"SLMP device code '{prefix}' is not supported for PlcProfile '{profileId}'.");
        }
    }

    private static bool TryParseDeviceNumber(
        string text,
        SlmpDeviceCode code,
        bool hexAddress,
        SlmpPlcProfile plcProfile,
        out uint number)
    {
        if (SlmpPlcProfiles.UsesIqFXyOctal(plcProfile) &&
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
