using System.Globalization;

namespace PlcComm.Slmp;

public readonly record struct SlmpTargetAddress(byte Network = 0x00, byte Station = 0xFF, ushort ModuleIo = 0x03FF, byte Multidrop = 0x00);

public readonly record struct SlmpDeviceAddress(SlmpDeviceCode Code, uint Number)
{
    public override string ToString() => $"{Code}{Number}";
}

public sealed record SlmpTypeNameInfo(string Model, ushort ModelCode, bool HasModelCode);

public sealed record SlmpProfileRecommendation(
    SlmpFrameType FrameType,
    SlmpCompatibilityMode CompatibilityMode,
    SlmpProfileClass ProfileClass,
    bool Confident
);

public sealed record SlmpBlockRead(SlmpDeviceAddress Device, ushort Points);

public sealed record SlmpBlockWrite(SlmpDeviceAddress Device, IReadOnlyList<ushort> Values);

public sealed record SlmpBlockWriteOptions(bool SplitMixedBlocks = false, bool RetryMixedOnError = false);

public sealed class SlmpException : Exception
{
    public SlmpException(string message, ushort? endCode = null, SlmpCommand? command = null, ushort? subcommand = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EndCode = endCode;
        Command = command;
        Subcommand = subcommand;
    }

    public ushort? EndCode { get; }

    public SlmpCommand? Command { get; }

    public ushort? Subcommand { get; }
}

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

            var numberText = token[prefix.Length..];
            if (numberText.Length == 0)
            {
                throw new FormatException($"Device number is missing: {text}");
            }

            var style = NumberStyles.AllowHexSpecifier;
            var culture = CultureInfo.InvariantCulture;
            uint number;
            if (hexAddress)
            {
                if (!uint.TryParse(numberText, style, culture, out number))
                {
                    throw new FormatException($"Invalid hex device number: {text}");
                }
            }
            else
            {
                if (!uint.TryParse(numberText, NumberStyles.Integer, culture, out number))
                {
                    throw new FormatException($"Invalid decimal device number: {text}");
                }
            }

            return new SlmpDeviceAddress(code, number);
        }

        throw new FormatException($"Unsupported device prefix: {text}");
    }
}

public static class SlmpProfileHeuristics
{
    public static SlmpProfileRecommendation Recommend(SlmpTypeNameInfo info)
    {
        if (info.HasModelCode)
        {
            if (IsLegacyQlByModelCode(info.ModelCode))
            {
                return new SlmpProfileRecommendation(SlmpFrameType.Frame3E, SlmpCompatibilityMode.Legacy, SlmpProfileClass.LegacyQl, true);
            }

            if (IsModernIqrByModelCode(info.ModelCode))
            {
                return new SlmpProfileRecommendation(SlmpFrameType.Frame4E, SlmpCompatibilityMode.Iqr, SlmpProfileClass.ModernIqr, true);
            }
        }

        var model = info.Model?.Trim().ToUpperInvariant() ?? string.Empty;
        if (model.StartsWith("Q", StringComparison.Ordinal) || model.StartsWith("L02", StringComparison.Ordinal) || model.StartsWith("L06", StringComparison.Ordinal) || model.StartsWith("L26", StringComparison.Ordinal))
        {
            return new SlmpProfileRecommendation(SlmpFrameType.Frame3E, SlmpCompatibilityMode.Legacy, SlmpProfileClass.LegacyQl, true);
        }

        if (model.StartsWith("R", StringComparison.Ordinal) || model.StartsWith("RJ", StringComparison.Ordinal) || model.StartsWith("FX5", StringComparison.Ordinal) || model.StartsWith("L16H", StringComparison.Ordinal))
        {
            return new SlmpProfileRecommendation(SlmpFrameType.Frame4E, SlmpCompatibilityMode.Iqr, SlmpProfileClass.ModernIqr, true);
        }

        return new SlmpProfileRecommendation(SlmpFrameType.Frame4E, SlmpCompatibilityMode.Iqr, SlmpProfileClass.Unknown, false);
    }

    private static bool IsLegacyQlByModelCode(ushort code)
    {
        return code is >= 0x0041 and <= 0x036C or 0x0250 or 0x0251 or 0x0252;
    }

    private static bool IsModernIqrByModelCode(ushort code)
    {
        return code is >= 0x4800 and <= 0x4894 or >= 0x48A0 and <= 0x48AF or 0x4860 or 0x4861 or 0x4862;
    }
}
