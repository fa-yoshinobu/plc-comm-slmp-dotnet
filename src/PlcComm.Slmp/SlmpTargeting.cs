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
    public sealed record IndexLz : SlmpDeviceModification
    {
        public IndexLz(byte index)
        {
            if (index > 1)
                throw new ArgumentOutOfRangeException(nameof(index), "LZ index must be 0 or 1.");
            Index = index;
        }

        public byte Index { get; }
    }
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
    private static readonly Regex QualifiedPattern = new(@"^U([^\\/]*)[\\/](.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LinkDirectPattern = new(@"^J([^\\/]*)[\\/](.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a qualified device string into a <see cref="SlmpQualifiedDeviceAddress"/>.
    /// </summary>
    /// <exception cref="FormatException">
    /// A U extension field is not hexadecimal 0000..FFFF (0..65535), or a J-direct
    /// network field is not decimal 0..255.
    /// </exception>
    public static SlmpQualifiedDeviceAddress Parse(string text, SlmpPlcProfile plcProfile)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Device text is required.", nameof(text));
        }

        var token = text.Trim().ToUpperInvariant();

        // J-format: link direct device (e.g. "J2\SW10")
        var jMatch = LinkDirectPattern.Match(token);
        if (jMatch.Success)
        {
            var networkText = jMatch.Groups[1].Value;
            if (!byte.TryParse(
                    networkText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var jNetwork))
            {
                throw new FormatException(
                    $"Invalid J-direct network field '{networkText}'; expected decimal 0..255.");
            }
            var device = SlmpDeviceParser.Parse(jMatch.Groups[2].Value, plcProfile);
            return new SlmpQualifiedDeviceAddress(device, jNetwork, 0xF9);
        }

        var match = QualifiedPattern.Match(token);
        if (!match.Success)
        {
            return new SlmpQualifiedDeviceAddress(SlmpDeviceParser.Parse(token, plcProfile), null);
        }

        var extensionText = match.Groups[1].Value;
        if (!ushort.TryParse(extensionText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var extensionSpecification))
        {
            throw new FormatException(
                $"Invalid U extension specification '{extensionText}'; expected hexadecimal 0000..FFFF (0..65535).");
        }
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
    /// <exception cref="FormatException">A route numeric field is malformed, negative, or outside its protocol field width.</exception>
    public static SlmpNamedTarget ParseNamed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("target text is required", nameof(text));
        }

        var parts = text.Split(',', StringSplitOptions.TrimEntries);
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
            throw new ArgumentException("target name must not be empty", nameof(text));
        }

        var network = (byte)ParseRouteField(parts[1], "NETWORK", byte.MaxValue);
        var station = (byte)ParseRouteField(parts[2], "STATION", byte.MaxValue);
        var moduleIo = (ushort)ParseRouteField(parts[3], "MODULE_IO", ushort.MaxValue);
        var multidrop = (byte)ParseRouteField(parts[4], "MULTIDROP", byte.MaxValue);
        return new SlmpNamedTarget(name, new SlmpTargetAddress(network, station, moduleIo, multidrop));
    }

    /// <summary>
    /// Parses a list of target strings.
    /// </summary>
    public static IReadOnlyList<SlmpNamedTarget> ParseMany(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one explicit target is required.", nameof(values));
        }

        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] is null)
                throw new ArgumentException($"Target collection contains null at index {index}.", nameof(values));
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
        ArgumentNullException.ThrowIfNull(text);
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(text[2..], 16);
        }

        return int.Parse(text, CultureInfo.InvariantCulture);
    }

    private static uint ParseRouteField(string text, string fieldName, uint maximum)
    {
        var isHex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        var digits = isHex ? text[2..] : text;
        var style = isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        if (digits.Length == 0 ||
            !uint.TryParse(digits, style, CultureInfo.InvariantCulture, out var value) ||
            value > maximum)
        {
            throw new FormatException(
                $"Invalid {fieldName} value '{text}'; expected {(isHex ? "hexadecimal" : "decimal or 0x hexadecimal")} 0..{maximum}.");
        }

        return value;
    }
}
