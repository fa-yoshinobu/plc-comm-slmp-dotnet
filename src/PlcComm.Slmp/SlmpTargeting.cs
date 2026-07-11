using System.Globalization;
using System.Text.RegularExpressions;

namespace PlcComm.Slmp;

/// <summary>
/// Represents a target station with a human-readable name.
/// </summary>
public readonly record struct SlmpNamedTarget(string Name, SlmpTargetAddress Target);

internal readonly record struct SlmpExtensionSpec(
    ushort ExtensionSpecification,
    byte ExtensionSpecificationModification,
    byte DeviceModificationIndex,
    byte DeviceModificationFlags,
    byte DirectMemorySpecification
);

/// <summary>Typed Extended Device modification.</summary>
public abstract record SlmpDeviceModification
{
    private SlmpDeviceModification() { }

    public sealed record IndexZ(byte Index) : SlmpDeviceModification;
    public sealed record IndexLz(byte Index) : SlmpDeviceModification;
    public sealed record Indirect : SlmpDeviceModification;
}

/// <summary>
/// Represents a semantic Extended Device address. Protocol direct-memory bytes are derived internally.
/// </summary>
public readonly record struct SlmpQualifiedDeviceAddress
{
    public SlmpQualifiedDeviceAddress(
        SlmpDeviceAddress device,
        ushort? extensionSpecification,
        SlmpDeviceModification? modification = null)
        : this(device, extensionSpecification, DeriveDirectMemory(device.Code, extensionSpecification), modification)
    {
    }

    internal SlmpQualifiedDeviceAddress(
        SlmpDeviceAddress device,
        ushort? extensionSpecification,
        byte? directMemorySpecification,
        SlmpDeviceModification? modification = null)
    {
        Device = device;
        ExtensionSpecification = extensionSpecification;
        DirectMemorySpecification = directMemorySpecification;
        Modification = modification;
    }

    public SlmpDeviceAddress Device { get; }
    public ushort? ExtensionSpecification { get; }
    public SlmpDeviceModification? Modification { get; }
    internal byte? DirectMemorySpecification { get; }

    private static byte? DeriveDirectMemory(SlmpDeviceCode code, ushort? extensionSpecification)
        => code switch
        {
            SlmpDeviceCode.G when extensionSpecification is not null => 0xF8,
            SlmpDeviceCode.HG when extensionSpecification is >= 0x03E0 and <= 0x03E3 => 0xFA,
            _ => null,
        };
}

/// <summary>
/// Utility for parsing qualified device strings (e.g., "U01\G10", "J2\SW10") into <see cref="SlmpQualifiedDeviceAddress"/>.
/// </summary>
public static class SlmpQualifiedDeviceParser
{
    private static readonly Regex QualifiedPattern = new(@"^U([0-9A-F]+)[\\/](.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LinkDirectPattern = new(@"^J(\d+)[\\/](.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a qualified device string into a <see cref="SlmpQualifiedDeviceAddress"/>.
    /// </summary>
    public static SlmpQualifiedDeviceAddress Parse(string text, SlmpPlcProfile plcProfile)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Device text is required.", nameof(text));
        }

        var token = text.Trim().ToUpperInvariant();

        // J-format: link direct device (e.g. "J2\SW10")
        var jMatch = LinkDirectPattern.Match(token);
        if (jMatch.Success)
        {
            var jNetwork = byte.Parse(jMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var device = SlmpDeviceParser.Parse(jMatch.Groups[2].Value, plcProfile);
            return new SlmpQualifiedDeviceAddress(device, jNetwork, 0xF9);
        }

        var match = QualifiedPattern.Match(token);
        if (!match.Success)
        {
            return new SlmpQualifiedDeviceAddress(SlmpDeviceParser.Parse(token, plcProfile), null);
        }

        var extensionSpecification = ushort.Parse(match.Groups[1].Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var dev = SlmpDeviceParser.Parse(match.Groups[2].Value, plcProfile);
        // G/HG buffer memory devices have a fixed DM by device code (matches GOT pcap-verified format)
        byte? dm = dev.Code switch
        {
            SlmpDeviceCode.G => (byte)0xF8,
            SlmpDeviceCode.HG => IsValidHgExtensionSpecification(extensionSpecification)
                ? (byte)0xFA
                : throw new ArgumentException(
                    @"HG Extended Device access is valid only for U3E0\HG through U3E3\HG.",
                    nameof(text)),
            _ => (byte?)null,
        };
        return new SlmpQualifiedDeviceAddress(dev, extensionSpecification, dm);
    }

    private static bool IsValidHgExtensionSpecification(ushort extensionSpecification)
        => extensionSpecification is >= 0x03E0 and <= 0x03E3;
}

/// <summary>
/// Utility for parsing target station descriptions into <see cref="SlmpNamedTarget"/>.
/// </summary>
public static class SlmpTargetParser
{
    private static readonly Regex SelfMultipleCpuPattern = new(@"^SELF-MULTIPLE-CPU-(?<cpu>[1-4])$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const byte DefaultSelfNetwork = 0x00;
    private const byte DefaultSelfStation = 0xFF;
    private const ushort DefaultModuleIo = SlmpModuleIo.OwnStation;
    private const byte DefaultMultidrop = 0x00;

    /// <summary>
    /// Parses a single target string. 
    /// Supports "SELF", "SELF-MULTIPLE-CPU-1..4", or "NAME,NETWORK,STATION,MODULE_IO,MULTIDROP".
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
            throw new ArgumentException("target must be SELF, SELF-MULTIPLE-CPU-1..4, or NAME,NETWORK,STATION,MODULE_IO,MULTIDROP");
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
            throw new ArgumentException("At least one explicit target is required.", nameof(values));
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

        var selfCpu = SelfMultipleCpuPattern.Match(name);
        if (selfCpu.Success)
        {
            var cpuIndex = int.Parse(selfCpu.Groups["cpu"].Value, CultureInfo.InvariantCulture);
            var moduleIo = checked((ushort)(SlmpModuleIo.MultipleCpu1 + (cpuIndex - 1)));
            return new SlmpNamedTarget($"SELF-MULTIPLE-CPU-{cpuIndex}", new SlmpTargetAddress(DefaultSelfNetwork, DefaultSelfStation, moduleIo, DefaultMultidrop));
        }

        throw new ArgumentException("target must be SELF, SELF-MULTIPLE-CPU-1..4, or NAME,NETWORK,STATION,MODULE_IO,MULTIDROP");
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
