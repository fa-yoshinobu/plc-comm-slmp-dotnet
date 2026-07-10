namespace PlcComm.Slmp;

/// <summary>Resolved fixed defaults for one canonical PLC profile.</summary>
public sealed record SlmpPlcProfileDefaults(
    SlmpFrameType FrameType,
    SlmpCompatibilityMode CompatibilityMode,
    SlmpPlcProfile AddressProfile,
    SlmpPlcProfile RangeProfile);

/// <summary>Canonical metadata used to select and describe one PLC profile.</summary>
public sealed record SlmpPlcProfileDescriptor(
    string CanonicalName,
    string DisplayName,
    bool Connectable,
    string? BaseProfile);

/// <summary>Fixed high-level defaults driven by <see cref="SlmpPlcProfile"/>.</summary>
public static class SlmpPlcProfiles
{
    private const string QCpuBaseProfileMessage =
        "melsec:qcpu is a base profile; use melsec:qcpu:qj71e71-100.";

    private static readonly string ValidProfileText = string.Join(", ", new[]
    {
        "melsec:iq-f",
        "melsec:iq-r",
        "melsec:iq-r:rj71en71",
        "melsec:iq-l",
        "melsec:mx-f",
        "melsec:mx-r",
        "melsec:qcpu:qj71e71-100",
        "melsec:lcpu",
        "melsec:lcpu:lj71e71-100",
        "melsec:qnu",
        "melsec:qnu:qj71e71-100",
        "melsec:qnudv",
        "melsec:qnudv:qj71e71-100",
    });

    private static readonly IReadOnlyList<SlmpPlcProfile> ConnectionProfiles =
        new[]
        {
            SlmpPlcProfile.IqF,
            SlmpPlcProfile.IqR,
            SlmpPlcProfile.IqRRj71En71,
            SlmpPlcProfile.IqL,
            SlmpPlcProfile.MxF,
            SlmpPlcProfile.MxR,
            SlmpPlcProfile.QCpuQj71E71100,
            SlmpPlcProfile.LCpu,
            SlmpPlcProfile.LCpuLj71E71100,
            SlmpPlcProfile.QnU,
            SlmpPlcProfile.QnUQj71E71100,
            SlmpPlcProfile.QnUDV,
            SlmpPlcProfile.QnUDVQj71E71100,
        };

    private static readonly IReadOnlyList<SlmpPlcProfileDescriptor> ProfileDescriptors =
        new SlmpPlcProfileDescriptor[]
        {
            new("melsec:iq-f", "MELSEC iQ-F (built-in)", true, null),
            new("melsec:iq-r", "MELSEC iQ-R (built-in)", true, null),
            new("melsec:iq-r:rj71en71", "MELSEC iQ-R (RJ71EN71)", true, "melsec:iq-r"),
            new("melsec:iq-l", "MELSEC iQ-L (built-in)", true, null),
            new("melsec:mx-f", "MELSEC MX-F (built-in)", true, "melsec:iq-r"),
            new("melsec:mx-r", "MELSEC MX-R (built-in)", true, "melsec:iq-r"),
            new("melsec:qcpu", "MELSEC-Q (base profile)", false, "melsec:qnu"),
            new("melsec:qcpu:qj71e71-100", "MELSEC-Q (QJ71E71-100)", true, "melsec:qcpu"),
            new("melsec:lcpu", "MELSEC-L (built-in)", true, null),
            new("melsec:lcpu:lj71e71-100", "MELSEC-L (LJ71E71-100)", true, "melsec:lcpu"),
            new("melsec:qnu", "MELSEC QnU (built-in)", true, null),
            new("melsec:qnu:qj71e71-100", "MELSEC QnU (QJ71E71-100)", true, "melsec:qnu"),
            new("melsec:qnudv", "MELSEC QnUDV (built-in)", true, null),
            new("melsec:qnudv:qj71e71-100", "MELSEC QnUDV (QJ71E71-100)", true, "melsec:qnudv"),
        };

    /// <summary>Return the built-in profiles that can be used to open a connection.</summary>
    public static IReadOnlyList<SlmpPlcProfile> AvailableProfiles() => ConnectionProfiles;

    /// <summary>
    /// Return all canonical profiles with display, connection, and base-profile metadata.
    /// </summary>
    /// <remarks>
    /// The abstract <c>melsec:qcpu</c> entry is included with <see cref="SlmpPlcProfileDescriptor.Connectable"/>
    /// set to <see langword="false"/> so selectors can explain why it cannot be opened directly.
    /// </remarks>
    public static IReadOnlyList<SlmpPlcProfileDescriptor> GetProfileDescriptors() => ProfileDescriptors;

    /// <summary>Parse a canonical PLC profile string.</summary>
    public static SlmpPlcProfile Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"PLC profile is required. Valid values: {ValidProfileText}.", nameof(text));
        }

        var normalized = text.Trim();
        if (string.Equals(normalized, "melsec:qcpu", StringComparison.Ordinal))
        {
            throw new ArgumentException(QCpuBaseProfileMessage, nameof(text));
        }

        return normalized switch
        {
            "melsec:iq-f" => SlmpPlcProfile.IqF,
            "melsec:iq-r" => SlmpPlcProfile.IqR,
            "melsec:iq-r:rj71en71" => SlmpPlcProfile.IqRRj71En71,
            "melsec:iq-l" => SlmpPlcProfile.IqL,
            "melsec:mx-f" => SlmpPlcProfile.MxF,
            "melsec:mx-r" => SlmpPlcProfile.MxR,
            "melsec:qcpu:qj71e71-100" => SlmpPlcProfile.QCpuQj71E71100,
            "melsec:lcpu" => SlmpPlcProfile.LCpu,
            "melsec:lcpu:lj71e71-100" => SlmpPlcProfile.LCpuLj71E71100,
            "melsec:qnu" => SlmpPlcProfile.QnU,
            "melsec:qnu:qj71e71-100" => SlmpPlcProfile.QnUQj71E71100,
            "melsec:qnudv" => SlmpPlcProfile.QnUDV,
            "melsec:qnudv:qj71e71-100" => SlmpPlcProfile.QnUDVQj71E71100,
            _ => throw new ArgumentException($"Unsupported PLC profile '{text}'. Valid values: {ValidProfileText}.", nameof(text)),
        };
    }

    internal static SlmpPlcProfile ParseKnownProfileId(string text)
        => text switch
        {
            "melsec:iq-f" => SlmpPlcProfile.IqF,
            "melsec:iq-r" => SlmpPlcProfile.IqR,
            "melsec:iq-r:rj71en71" => SlmpPlcProfile.IqRRj71En71,
            "melsec:iq-l" => SlmpPlcProfile.IqL,
            "melsec:mx-f" => SlmpPlcProfile.MxF,
            "melsec:mx-r" => SlmpPlcProfile.MxR,
            "melsec:qcpu" => SlmpPlcProfile.QCpu,
            "melsec:qcpu:qj71e71-100" => SlmpPlcProfile.QCpuQj71E71100,
            "melsec:lcpu" => SlmpPlcProfile.LCpu,
            "melsec:lcpu:lj71e71-100" => SlmpPlcProfile.LCpuLj71E71100,
            "melsec:qnu" => SlmpPlcProfile.QnU,
            "melsec:qnu:qj71e71-100" => SlmpPlcProfile.QnUQj71E71100,
            "melsec:qnudv" => SlmpPlcProfile.QnUDV,
            "melsec:qnudv:qj71e71-100" => SlmpPlcProfile.QnUDVQj71E71100,
            _ => throw new ArgumentException($"Unsupported PLC profile '{text}'.", nameof(text)),
        };

    /// <summary>Return the canonical string form used in user-facing configuration.</summary>
    public static string ToCanonicalString(SlmpPlcProfile profile)
        => profile switch
        {
            SlmpPlcProfile.IqF => "melsec:iq-f",
            SlmpPlcProfile.IqR => "melsec:iq-r",
            SlmpPlcProfile.IqRRj71En71 => "melsec:iq-r:rj71en71",
            SlmpPlcProfile.IqL => "melsec:iq-l",
            SlmpPlcProfile.MxF => "melsec:mx-f",
            SlmpPlcProfile.MxR => "melsec:mx-r",
            SlmpPlcProfile.QCpu => "melsec:qcpu",
            SlmpPlcProfile.QCpuQj71E71100 => "melsec:qcpu:qj71e71-100",
            SlmpPlcProfile.LCpu => "melsec:lcpu",
            SlmpPlcProfile.LCpuLj71E71100 => "melsec:lcpu:lj71e71-100",
            SlmpPlcProfile.QnU => "melsec:qnu",
            SlmpPlcProfile.QnUQj71E71100 => "melsec:qnu:qj71e71-100",
            SlmpPlcProfile.QnUDV => "melsec:qnudv",
            SlmpPlcProfile.QnUDVQj71E71100 => "melsec:qnudv:qj71e71-100",
            SlmpPlcProfile.Unspecified => throw new ArgumentOutOfRangeException(
                nameof(profile),
                profile,
                $"PLC profile is required. Valid values: {ValidProfileText}."),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported PLC profile."),
        };

    /// <summary>Return the canonical human-readable display name for a PLC profile.</summary>
    public static string GetDisplayName(SlmpPlcProfile profile)
        => profile switch
        {
            SlmpPlcProfile.IqF => "MELSEC iQ-F (built-in)",
            SlmpPlcProfile.IqR => "MELSEC iQ-R (built-in)",
            SlmpPlcProfile.IqRRj71En71 => "MELSEC iQ-R (RJ71EN71)",
            SlmpPlcProfile.IqL => "MELSEC iQ-L (built-in)",
            SlmpPlcProfile.MxF => "MELSEC MX-F (built-in)",
            SlmpPlcProfile.MxR => "MELSEC MX-R (built-in)",
            SlmpPlcProfile.QCpu => "MELSEC-Q (base profile)",
            SlmpPlcProfile.QCpuQj71E71100 => "MELSEC-Q (QJ71E71-100)",
            SlmpPlcProfile.LCpu => "MELSEC-L (built-in)",
            SlmpPlcProfile.LCpuLj71E71100 => "MELSEC-L (LJ71E71-100)",
            SlmpPlcProfile.QnU => "MELSEC QnU (built-in)",
            SlmpPlcProfile.QnUQj71E71100 => "MELSEC QnU (QJ71E71-100)",
            SlmpPlcProfile.QnUDV => "MELSEC QnUDV (built-in)",
            SlmpPlcProfile.QnUDVQj71E71100 => "MELSEC QnUDV (QJ71E71-100)",
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
            SlmpPlcProfile.IqRRj71En71 => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Iqr,
                SlmpPlcProfile.IqR,
                SlmpPlcProfile.IqRRj71En71),
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
            SlmpPlcProfile.QCpuQj71E71100 => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.QCpu,
                SlmpPlcProfile.QCpuQj71E71100),
            SlmpPlcProfile.LCpu => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.LCpu,
                SlmpPlcProfile.LCpu),
            SlmpPlcProfile.LCpuLj71E71100 => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.LCpu,
                SlmpPlcProfile.LCpuLj71E71100),
            SlmpPlcProfile.QnU => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.QnU,
                SlmpPlcProfile.QnU),
            SlmpPlcProfile.QnUQj71E71100 => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.QnU,
                SlmpPlcProfile.QnUQj71E71100),
            SlmpPlcProfile.QnUDV => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.QnUDV,
                SlmpPlcProfile.QnUDV),
            SlmpPlcProfile.QnUDVQj71E71100 => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcProfile.QnUDV,
                SlmpPlcProfile.QnUDVQj71E71100),
            SlmpPlcProfile.Unspecified => throw new ArgumentOutOfRangeException(
                nameof(profile),
                profile,
                $"PLC profile is required. Valid values: {ValidProfileText}."),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported PLC profile."),
        };

    /// <summary>Validate that the profile can be used to open an SLMP connection.</summary>
    public static SlmpPlcProfile ValidateConnectionProfile(SlmpPlcProfile profile)
    {
        _ = Resolve(profile);
        if (profile == SlmpPlcProfile.QCpu)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), profile, QCpuBaseProfileMessage);
        }

        return profile;
    }

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
