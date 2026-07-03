namespace PlcComm.Slmp;

/// <summary>Resolved fixed defaults for one canonical PLC profile.</summary>
public sealed record SlmpPlcProfileDefaults(
    SlmpFrameType FrameType,
    SlmpCompatibilityMode CompatibilityMode,
    SlmpPlcProfile AddressProfile,
    SlmpPlcProfile RangeProfile);

/// <summary>Fixed high-level defaults driven by <see cref="SlmpPlcProfile"/>.</summary>
public static class SlmpPlcProfiles
{
    private static readonly string ValidProfileText = string.Join(", ", new[]
    {
        "melsec:iq-f",
        "melsec:iq-r",
        "melsec:iq-l",
        "melsec:mx-f",
        "melsec:mx-r",
        "melsec:qcpu",
        "melsec:lcpu",
        "melsec:qnu",
        "melsec:qnudv",
    });

    /// <summary>Parse a canonical PLC profile string.</summary>
    public static SlmpPlcProfile Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"PLC profile is required. Valid values: {ValidProfileText}.", nameof(text));
        }

        var normalized = text.Trim();
        return normalized switch
        {
            "melsec:iq-f" => SlmpPlcProfile.IqF,
            "melsec:iq-r" => SlmpPlcProfile.IqR,
            "melsec:iq-l" => SlmpPlcProfile.IqL,
            "melsec:mx-f" => SlmpPlcProfile.MxF,
            "melsec:mx-r" => SlmpPlcProfile.MxR,
            "melsec:qcpu" => SlmpPlcProfile.QCpu,
            "melsec:lcpu" => SlmpPlcProfile.LCpu,
            "melsec:qnu" => SlmpPlcProfile.QnU,
            "melsec:qnudv" => SlmpPlcProfile.QnUDV,
            _ => throw new ArgumentException($"Unsupported PLC profile '{text}'. Valid values: {ValidProfileText}.", nameof(text)),
        };
    }

    /// <summary>Return the canonical string form used in user-facing configuration.</summary>
    public static string ToCanonicalString(SlmpPlcProfile profile)
        => profile switch
        {
            SlmpPlcProfile.IqF => "melsec:iq-f",
            SlmpPlcProfile.IqR => "melsec:iq-r",
            SlmpPlcProfile.IqL => "melsec:iq-l",
            SlmpPlcProfile.MxF => "melsec:mx-f",
            SlmpPlcProfile.MxR => "melsec:mx-r",
            SlmpPlcProfile.QCpu => "melsec:qcpu",
            SlmpPlcProfile.LCpu => "melsec:lcpu",
            SlmpPlcProfile.QnU => "melsec:qnu",
            SlmpPlcProfile.QnUDV => "melsec:qnudv",
            SlmpPlcProfile.Unspecified => throw new ArgumentOutOfRangeException(
                nameof(profile),
                profile,
                $"PLC profile is required. Valid values: {ValidProfileText}."),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported PLC profile."),
        };

    /// <summary>Resolve the stable defaults for one explicit PLC profile.</summary>
    public static SlmpPlcProfileDefaults Resolve(SlmpPlcProfile profile)
        => profile switch
        {
            SlmpPlcProfile.IqF => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.IqF,
                SlmpPlcProfile.IqF),
            SlmpPlcProfile.IqR => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Iqr,
                SlmpPlcProfile.IqR,
                SlmpPlcProfile.IqR),
            SlmpPlcProfile.IqL => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Iqr,
                SlmpPlcProfile.IqL,
                SlmpPlcProfile.IqL),
            SlmpPlcProfile.MxF => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Iqr,
                SlmpPlcProfile.MxF,
                SlmpPlcProfile.MxF),
            SlmpPlcProfile.MxR => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Iqr,
                SlmpPlcProfile.MxR,
                SlmpPlcProfile.MxR),
            SlmpPlcProfile.QCpu => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.QCpu,
                SlmpPlcProfile.QCpu),
            SlmpPlcProfile.LCpu => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.LCpu,
                SlmpPlcProfile.LCpu),
            SlmpPlcProfile.QnU => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.QnU,
                SlmpPlcProfile.QnU),
            SlmpPlcProfile.QnUDV => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.QnUDV,
                SlmpPlcProfile.QnUDV),
            SlmpPlcProfile.Unspecified => throw new ArgumentOutOfRangeException(
                nameof(profile),
                profile,
                $"PLC profile is required. Valid values: {ValidProfileText}."),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported PLC profile."),
        };

    /// <summary>True when the selected profile uses iQ-R-compatible command subcommands and payloads.</summary>
    public static bool UsesIqrProtocol(SlmpPlcProfile profile)
        => Resolve(profile).CompatibilityMode == SlmpCompatibilityMode.Iqr;

    /// <summary>True when <c>X</c> and <c>Y</c> strings must be parsed as octal.</summary>
    public static bool UsesIqFXyOctal(SlmpPlcProfile profile)
        => Resolve(profile).AddressProfile == SlmpPlcProfile.IqF;

    internal static bool IsDeviceCodeUnsupported(SlmpDeviceCode code, SlmpPlcProfile profile)
        => profile == SlmpPlcProfile.Unspecified
            ? false
            : Resolve(profile).AddressProfile switch
        {
            SlmpPlcProfile.IqF => code is SlmpDeviceCode.DX
                or SlmpDeviceCode.DY
                or SlmpDeviceCode.V
                or SlmpDeviceCode.LTS
                or SlmpDeviceCode.LTC
                or SlmpDeviceCode.LTN
                or SlmpDeviceCode.LSTS
                or SlmpDeviceCode.LSTC
                or SlmpDeviceCode.LSTN
                or SlmpDeviceCode.ZR
                or SlmpDeviceCode.RD,
            SlmpPlcProfile.QCpu
                or SlmpPlcProfile.LCpu
                or SlmpPlcProfile.QnU
                or SlmpPlcProfile.QnUDV => code is SlmpDeviceCode.LTS
                    or SlmpDeviceCode.LTC
                    or SlmpDeviceCode.LTN
                    or SlmpDeviceCode.LSTS
                    or SlmpDeviceCode.LSTC
                    or SlmpDeviceCode.LSTN
                    or SlmpDeviceCode.LCS
                    or SlmpDeviceCode.LCC
                    or SlmpDeviceCode.LCN
                    or SlmpDeviceCode.LZ
                    or SlmpDeviceCode.RD,
            _ => false,
        };
}
