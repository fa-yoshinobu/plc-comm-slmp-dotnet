using System.Globalization;
using System.Text.RegularExpressions;

namespace PlcComm.Slmp;

public readonly record struct SlmpNamedTarget(string Name, SlmpTargetAddress Target);

public readonly record struct SlmpExtensionSpec(
    ushort ExtensionSpecification = 0x0000,
    byte ExtensionSpecificationModification = 0x00,
    byte DeviceModificationIndex = 0x00,
    byte DeviceModificationFlags = 0x00,
    byte DirectMemorySpecification = 0x00
);

public readonly record struct SlmpQualifiedDeviceAddress(SlmpDeviceAddress Device, ushort? ExtensionSpecification);

public static class SlmpQualifiedDeviceParser
{
    private static readonly Regex QualifiedPattern = new(@"^U([0-9A-F]+)[\\/](.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

public static class SlmpTargetParser
{
    private static readonly Regex NwStationPattern = new(@"^NW(?<network>\d+)-ST(?<station>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SelfCpuPattern = new(@"^SELF-CPU(?<cpu>[1-4])$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const byte DefaultSelfNetwork = 0x00;
    private const byte DefaultSelfStation = 0xFF;
    private const ushort DefaultModuleIo = 0x03FF;
    private const byte DefaultMultidrop = 0x00;

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

    public static int ParseAutoNumber(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(text[2..], 16);
        }

        return int.Parse(text, CultureInfo.InvariantCulture);
    }
}
