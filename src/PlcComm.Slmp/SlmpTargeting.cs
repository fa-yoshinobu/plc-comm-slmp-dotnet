using System.Globalization;
using System.Text.RegularExpressions;

namespace PlcComm.Slmp;

/// <summary>
/// Represents a target station with a human-readable name.
/// </summary>
public readonly record struct SlmpNamedTarget(string Name, SlmpTargetAddress Target);

/// <summary>
/// Represents Appendix 1 extension fields for device access.
/// </summary>
public readonly record struct SlmpExtensionSpec(
    ushort ExtensionSpecification = 0x0000,
    byte ExtensionSpecificationModification = 0x00,
    byte DeviceModificationIndex = 0x00,
    byte DeviceModificationFlags = 0x00,
    byte DirectMemorySpecification = 0x00
);

/// <summary>
/// Represents a device address that may include an explicit Appendix 1 extension specification.
/// </summary>
public readonly record struct SlmpQualifiedDeviceAddress(SlmpDeviceAddress Device, ushort? ExtensionSpecification);

/// <summary>
/// Utility for parsing qualified device strings (e.g., "U01\G10") into <see cref="SlmpQualifiedDeviceAddress"/>.
/// </summary>
public static class SlmpQualifiedDeviceParser
{
    private static readonly Regex QualifiedPattern = new(@"^U([0-9A-F]+)[\\/](.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a qualified device string into a <see cref="SlmpQualifiedDeviceAddress"/>.
    /// </summary>
    public static SlmpQualifiedDeviceAddress Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Device text is required.", nameof(text));
        }

        var token = text.Trim().ToUpperInvariant();
        var match = QualifiedPattern.Match(token);
        if (!match.Success)
        {
            return new SlmpQualifiedDeviceAddress(SlmpDeviceParser.Parse(token), null);
        }

        var extensionSpecification = ushort.Parse(match.Groups[1].Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var device = SlmpDeviceParser.Parse(match.Groups[2].Value);
        return new SlmpQualifiedDeviceAddress(device, extensionSpecification);
    }
}

/// <summary>
/// Utility for parsing target station descriptions into <see cref="SlmpNamedTarget"/>.
/// </summary>
public static class SlmpTargetParser
{
    private static readonly Regex NwStationPattern = new(@"^NW(?<network>\d+)-ST(?<station>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SelfCpuPattern = new(@"^SELF-CPU(?<cpu>[1-4])$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const byte DefaultSelfNetwork = 0x00;
    private const byte DefaultSelfStation = 0xFF;
    private const ushort DefaultModuleIo = 0x03FF;
    private const byte DefaultMultidrop = 0x00;

    /// <summary>
    /// Parses a single target string. 
    /// Supports "SELF", "SELF-CPU1..4", "NWx-STy", or "NAME,NETWORK,STATION,MODULE_IO,MULTIDROP".
    /// </summary>
    public static SlmpNamedTarget ParseNamed(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("target text is required", nameof(text));
        }

        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return ParseSingle(parts[0]);
        }

        if (parts.Length != 5)
        {
            throw new ArgumentException("target must be SELF, SELF-CPU1..4, NWx-STy, or NAME,NETWORK,STATION,MODULE_IO,MULTIDROP");
        }

        var name = parts[0];
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("target name must not be empty");
        }

        var network = checked((byte)ParseAutoNumber(parts[1]));
        var station = checked((byte)ParseAutoNumber(parts[2]));
        var moduleIo = checked((ushort)ParseAutoNumber(parts[3]));
        var multidrop = checked((byte)ParseAutoNumber(parts[4]));
        return new SlmpNamedTarget(name, new SlmpTargetAddress(network, station, moduleIo, multidrop));
    }

    /// <summary>
    /// Parses a list of target strings.
    /// </summary>
    public static IReadOnlyList<SlmpNamedTarget> ParseMany(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return [new SlmpNamedTarget("SELF", new SlmpTargetAddress(DefaultSelfNetwork, DefaultSelfStation, DefaultModuleIo, DefaultMultidrop))];
        }

        return values.Select(ParseNamed).ToArray();
    }

    private static SlmpNamedTarget ParseSingle(string token)
    {
        var name = token.Trim();
        if (name.Equals("SELF", StringComparison.OrdinalIgnoreCase))
        {
            return new SlmpNamedTarget("SELF", new SlmpTargetAddress(DefaultSelfNetwork, DefaultSelfStation, DefaultModuleIo, DefaultMultidrop));
        }

        var selfCpu = SelfCpuPattern.Match(name);
        if (selfCpu.Success)
        {
            var cpuIndex = int.Parse(selfCpu.Groups["cpu"].Value, CultureInfo.InvariantCulture);
            var moduleIo = checked((ushort)(0x03E0 + (cpuIndex - 1)));
            return new SlmpNamedTarget($"SELF-CPU{cpuIndex}", new SlmpTargetAddress(DefaultSelfNetwork, DefaultSelfStation, moduleIo, DefaultMultidrop));
        }

        var nwSt = NwStationPattern.Match(name);
        if (nwSt.Success)
        {
            var network = checked((byte)int.Parse(nwSt.Groups["network"].Value, CultureInfo.InvariantCulture));
            var station = checked((byte)int.Parse(nwSt.Groups["station"].Value, CultureInfo.InvariantCulture));
            return new SlmpNamedTarget($"NW{network}-ST{station}", new SlmpTargetAddress(network, station, DefaultModuleIo, DefaultMultidrop));
        }

        throw new ArgumentException("target must be SELF, SELF-CPU1..4, NWx-STy, or NAME,NETWORK,STATION,MODULE_IO,MULTIDROP");
    }

    /// <summary>
    /// Parses a number string, supporting both decimal and "0x" hexadecimal notation.
    /// </summary>
    public static int ParseAutoNumber(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(text[2..], 16);
        }

        return int.Parse(text, CultureInfo.InvariantCulture);
    }
}
