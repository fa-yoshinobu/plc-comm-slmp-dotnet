namespace PlcComm.Slmp;

internal enum SlmpProfileFeatureState
{
    Supported,
    Blocked,
    ConfigDependent,
    Unverified,
    Delegated,
}

internal enum SlmpProfileFeature
{
    TypeName,
    Direct,
    Random,
    Block,
    Monitor,
    ExtModuleAccess,
    ExtLinkDirect,
    HgCpuBuffer,
    LongDevicePath,
    Lz32BitPath,
}

internal enum SlmpProfileLimit
{
    DirectWordRead,
    DirectWordWrite,
    DirectBitRead,
    DirectBitWrite,
    RandomReadWord,
    RandomWriteWord,
    RandomWriteBit,
    MonitorRegisterWord,
    RandomReadWordExt,
    RandomWriteWordExt,
    RandomWriteBitExt,
    MonitorRegisterWordExt,
}

internal sealed record SlmpCapabilityFeature(
    SlmpProfileFeatureState State,
    string Source,
    string? Note = null);

internal sealed record SlmpCapabilityLimit(
    int Max,
    string? OverEndCode,
    string Source,
    int? WeightedMax = null,
    string? Note = null);

internal sealed record SlmpCapabilityProfile(
    SlmpPlcProfile Profile,
    string ProfileId,
    string Frame,
    string Compat,
    IReadOnlyDictionary<SlmpProfileFeature, SlmpCapabilityFeature> Features,
    IReadOnlyDictionary<SlmpProfileLimit, SlmpCapabilityLimit> Limits,
    IReadOnlyDictionary<string, string> WritePolicy);

internal static class SlmpCapabilityProfiles
{
    internal const string CanonicalSource =
        "plc-comm-slmp-profiles v1.2.2 capability/slmp_builtin_ethernet_profiles.json";

    private static readonly Dictionary<SlmpPlcProfile, SlmpCapabilityProfile> Profiles =
        new Dictionary<SlmpPlcProfile, SlmpCapabilityProfile>
        {
            [SlmpPlcProfile.IqR] = Profile(
                SlmpPlcProfile.IqR,
                "melsec:iq-r",
                "4E",
                "iQ-R",
                CommonIqrFeatures(SlmpProfileFeatureState.Supported),
                IqrLimits(),
                WritePolicy("S")),
            [SlmpPlcProfile.IqRRj71En71] = Profile(
                SlmpPlcProfile.IqRRj71En71,
                "melsec:iq-r:rj71en71",
                "4E",
                "iQ-R",
                CommonIqrFeatures(SlmpProfileFeatureState.Supported),
                IqrLimits(),
                WritePolicy("S")),
            [SlmpPlcProfile.IqL] = Profile(
                SlmpPlcProfile.IqL,
                "melsec:iq-l",
                "4E",
                "iQ-R",
                CommonIqrFeatures(SlmpProfileFeatureState.Blocked),
                IqlMxLimits(),
                WritePolicy("S")),
            [SlmpPlcProfile.MxR] = Profile(
                SlmpPlcProfile.MxR,
                "melsec:mx-r",
                "4E",
                "iQ-R",
                CommonIqrFeatures(SlmpProfileFeatureState.Blocked),
                IqrLimits(),
                WritePolicy("S")),
            [SlmpPlcProfile.MxF] = Profile(
                SlmpPlcProfile.MxF,
                "melsec:mx-f",
                "4E",
                "iQ-R",
                CommonIqrFeatures(SlmpProfileFeatureState.Blocked),
                IqrLimits(),
                WritePolicy("S")),
            [SlmpPlcProfile.IqF] = Profile(
                SlmpPlcProfile.IqF,
                "melsec:iq-f",
                "3E",
                "Q/L",
                Features(
                    (SlmpProfileFeature.TypeName, SlmpProfileFeatureState.Supported, "live", null),
                    (SlmpProfileFeature.Direct, SlmpProfileFeatureState.Supported, "live", null),
                    (SlmpProfileFeature.Random, SlmpProfileFeatureState.Supported, "live", null),
                    (SlmpProfileFeature.Block, SlmpProfileFeatureState.Supported, "live", null),
                    (SlmpProfileFeature.Monitor, SlmpProfileFeatureState.Blocked, "live", "0x0801/0x0802 returned C059 on FX5U."),
                    (SlmpProfileFeature.ExtModuleAccess, SlmpProfileFeatureState.ConfigDependent, "live", "U1\\G0 depends on the installed special module."),
                    (SlmpProfileFeature.ExtLinkDirect, SlmpProfileFeatureState.Blocked, "live", "J1 link-direct access returned a PLC error."),
                    (SlmpProfileFeature.HgCpuBuffer, SlmpProfileFeatureState.Blocked, "spec", "CPU-buffer HG is an iQ-R-only path."),
                    (SlmpProfileFeature.LongDevicePath, SlmpProfileFeatureState.Supported, "live", null),
                    (SlmpProfileFeature.Lz32BitPath, SlmpProfileFeatureState.Supported, "live", null)),
                IqFLimits(),
                Policy(("S", "read-write"))),
            [SlmpPlcProfile.QCpu] = Profile(
                SlmpPlcProfile.QCpu,
                "melsec:qcpu",
                "3E",
                "Q/L",
                QlMeasuredFeatures("policy"),
                QlLimits("inferred", extReadMax: 185, extReadOverEndCode: "4080"),
                WritePolicy("S")),
            [SlmpPlcProfile.QCpuQj71E71100] = Profile(
                SlmpPlcProfile.QCpuQj71E71100,
                "melsec:qcpu:qj71e71-100",
                "4E",
                "Q/L",
                QlUnitFeatures(),
                QlUnitLimits(extReadMax: 185, includeBitExt: true),
                Policy(("S", "read-write"))),
            [SlmpPlcProfile.LCpu] = Profile(
                SlmpPlcProfile.LCpu,
                "melsec:lcpu",
                "3E",
                "Q/L",
                QlMeasuredFeatures("live"),
                QlLimits("live"),
                WritePolicy("S")),
            [SlmpPlcProfile.LCpuLj71E71100] = Profile(
                SlmpPlcProfile.LCpuLj71E71100,
                "melsec:lcpu:lj71e71-100",
                "4E",
                "Q/L",
                QlUnitFeatures(),
                QlUnitLimits(extReadMax: 192, includeBitExt: true, bitExtSource: "inferred"),
                Policy(("S", "read-write"))),
            [SlmpPlcProfile.QnU] = Profile(
                SlmpPlcProfile.QnU,
                "melsec:qnu",
                "3E",
                "Q/L",
                QlMeasuredFeatures("live"),
                QlLimits("live"),
                WritePolicy("S")),
            [SlmpPlcProfile.QnUQj71E71100] = Profile(
                SlmpPlcProfile.QnUQj71E71100,
                "melsec:qnu:qj71e71-100",
                "4E",
                "Q/L",
                QlUnitFeatures(),
                QlUnitLimits(extReadMax: 192, includeBitExt: true),
                Policy(("S", "read-write"))),
            [SlmpPlcProfile.QnUDV] = Profile(
                SlmpPlcProfile.QnUDV,
                "melsec:qnudv",
                "3E",
                "Q/L",
                QlMeasuredFeatures("live"),
                QlLimits("live"),
                WritePolicy("S")),
            [SlmpPlcProfile.QnUDVQj71E71100] = Profile(
                SlmpPlcProfile.QnUDVQj71E71100,
                "melsec:qnudv:qj71e71-100",
                "4E",
                "Q/L",
                QlUnitFeatures(),
                QlUnitLimits(extReadMax: 192, includeBitExt: true),
                Policy(("S", "read-write"))),
        };

    internal static IReadOnlyDictionary<SlmpPlcProfile, SlmpCapabilityProfile> All => Profiles;

    internal static bool TryGetProfile(SlmpPlcProfile profile, out SlmpCapabilityProfile capabilityProfile)
        => Profiles.TryGetValue(profile, out capabilityProfile!);

    internal static bool TryGetFeature(
        SlmpPlcProfile profile,
        SlmpProfileFeature feature,
        out SlmpCapabilityFeature capabilityFeature)
    {
        if (Profiles.TryGetValue(profile, out var capabilityProfile) &&
            capabilityProfile.Features.TryGetValue(feature, out capabilityFeature!))
        {
            return true;
        }

        capabilityFeature = null!;
        return false;
    }

    internal static bool TryGetLimit(
        SlmpPlcProfile profile,
        SlmpProfileLimit limit,
        out SlmpCapabilityLimit capabilityLimit)
    {
        if (Profiles.TryGetValue(profile, out var capabilityProfile) &&
            capabilityProfile.Limits.TryGetValue(limit, out capabilityLimit!))
        {
            return true;
        }

        capabilityLimit = null!;
        return false;
    }

    internal static bool IsReadOnly(SlmpPlcProfile profile, string deviceCode)
        => Profiles.TryGetValue(profile, out var capabilityProfile) &&
           capabilityProfile.WritePolicy.TryGetValue(deviceCode, out var policy) &&
           string.Equals(policy, "read-only", StringComparison.Ordinal);

    internal static string ToCanonicalFeatureKey(SlmpProfileFeature feature)
        => feature switch
        {
            SlmpProfileFeature.TypeName => "type_name",
            SlmpProfileFeature.Direct => "direct",
            SlmpProfileFeature.Random => "random",
            SlmpProfileFeature.Block => "block",
            SlmpProfileFeature.Monitor => "monitor",
            SlmpProfileFeature.ExtModuleAccess => "ext_module_access",
            SlmpProfileFeature.ExtLinkDirect => "ext_link_direct",
            SlmpProfileFeature.HgCpuBuffer => "hg_cpu_buffer",
            SlmpProfileFeature.LongDevicePath => "long_device_path",
            SlmpProfileFeature.Lz32BitPath => "lz_32bit_path",
            _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, "Unsupported profile feature."),
        };

    internal static string ToCanonicalLimitKey(SlmpProfileLimit limit)
        => limit switch
        {
            SlmpProfileLimit.DirectWordRead => "direct_word_read",
            SlmpProfileLimit.DirectWordWrite => "direct_word_write",
            SlmpProfileLimit.DirectBitRead => "direct_bit_read",
            SlmpProfileLimit.DirectBitWrite => "direct_bit_write",
            SlmpProfileLimit.RandomReadWord => "random_read_word",
            SlmpProfileLimit.RandomWriteWord => "random_write_word",
            SlmpProfileLimit.RandomWriteBit => "random_write_bit",
            SlmpProfileLimit.MonitorRegisterWord => "monitor_register_word",
            SlmpProfileLimit.RandomReadWordExt => "random_read_word_ext",
            SlmpProfileLimit.RandomWriteWordExt => "random_write_word_ext",
            SlmpProfileLimit.RandomWriteBitExt => "random_write_bit_ext",
            SlmpProfileLimit.MonitorRegisterWordExt => "monitor_register_word_ext",
            _ => throw new ArgumentOutOfRangeException(nameof(limit), limit, "Unsupported profile limit."),
        };

    internal static string ToCanonicalState(SlmpProfileFeatureState state)
        => state switch
        {
            SlmpProfileFeatureState.Supported => "supported",
            SlmpProfileFeatureState.Blocked => "blocked",
            SlmpProfileFeatureState.ConfigDependent => "config-dependent",
            SlmpProfileFeatureState.Unverified => "unverified",
            SlmpProfileFeatureState.Delegated => "delegated",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported profile feature state."),
        };

    private static SlmpCapabilityProfile Profile(
        SlmpPlcProfile profile,
        string profileId,
        string frame,
        string compat,
        IReadOnlyDictionary<SlmpProfileFeature, SlmpCapabilityFeature> features,
        IReadOnlyDictionary<SlmpProfileLimit, SlmpCapabilityLimit> limits,
        IReadOnlyDictionary<string, string> writePolicy)
        => new(profile, profileId, frame, compat, features, limits, writePolicy);

    private static Dictionary<SlmpProfileFeature, SlmpCapabilityFeature> CommonIqrFeatures(
        SlmpProfileFeatureState hgState)
        => Features(
            (SlmpProfileFeature.TypeName, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.Direct, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.Random, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.Block, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.Monitor, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.ExtModuleAccess, SlmpProfileFeatureState.ConfigDependent, "live", "Module access depends on the installed special module."),
            (SlmpProfileFeature.ExtLinkDirect, SlmpProfileFeatureState.ConfigDependent, "policy", "Link-direct access depends on the network/module configuration."),
            (SlmpProfileFeature.HgCpuBuffer, hgState, hgState == SlmpProfileFeatureState.Supported ? "live" : "manual", hgState == SlmpProfileFeatureState.Supported ? "U3E0\\HG direct/random/monitor succeeded." : "CPU-buffer HG is an iQ-R-only path."),
            (SlmpProfileFeature.LongDevicePath, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.Lz32BitPath, SlmpProfileFeatureState.Supported, "live", null));

    private static Dictionary<SlmpProfileFeature, SlmpCapabilityFeature> QlMeasuredFeatures(string source)
        => Features(
            (SlmpProfileFeature.TypeName, SlmpProfileFeatureState.Blocked, source, "Read Type Name returned C059."),
            (SlmpProfileFeature.Direct, SlmpProfileFeatureState.Supported, source, null),
            (SlmpProfileFeature.Random, SlmpProfileFeatureState.Supported, source, null),
            (SlmpProfileFeature.Block, SlmpProfileFeatureState.Blocked, source, "Read/Write Block returned C059."),
            (SlmpProfileFeature.Monitor, SlmpProfileFeatureState.Supported, source, null),
            (SlmpProfileFeature.ExtModuleAccess, SlmpProfileFeatureState.Blocked, source, "U\\G access is not available on the tested built-in CPU port."),
            (SlmpProfileFeature.ExtLinkDirect, SlmpProfileFeatureState.Blocked, source, "Link-direct access is not available on the tested built-in CPU port."),
            (SlmpProfileFeature.HgCpuBuffer, SlmpProfileFeatureState.Blocked, "spec", "CPU-buffer HG is an iQ-R-only path."),
            (SlmpProfileFeature.LongDevicePath, SlmpProfileFeatureState.Delegated, source, "Existing long-device route rules decide this feature."),
            (SlmpProfileFeature.Lz32BitPath, SlmpProfileFeatureState.Delegated, source, "Existing 32-bit route rules decide this feature."));

    private static Dictionary<SlmpProfileFeature, SlmpCapabilityFeature> QlUnitFeatures()
        => Features(
            (SlmpProfileFeature.TypeName, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.Direct, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.Random, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.Block, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.Monitor, SlmpProfileFeatureState.Supported, "live", null),
            (SlmpProfileFeature.ExtModuleAccess, SlmpProfileFeatureState.ConfigDependent, "live", null),
            (SlmpProfileFeature.ExtLinkDirect, SlmpProfileFeatureState.ConfigDependent, "live", null),
            (SlmpProfileFeature.HgCpuBuffer, SlmpProfileFeatureState.Blocked, "spec", "CPU-buffer HG is an iQ-R-only path."),
            (SlmpProfileFeature.LongDevicePath, SlmpProfileFeatureState.Blocked, "live", null),
            (SlmpProfileFeature.Lz32BitPath, SlmpProfileFeatureState.Blocked, "live", null));

    private static Dictionary<SlmpProfileFeature, SlmpCapabilityFeature> Features(
        params (SlmpProfileFeature Feature, SlmpProfileFeatureState State, string Source, string? Note)[] entries)
        => entries.ToDictionary(
            static entry => entry.Feature,
            static entry => new SlmpCapabilityFeature(entry.State, entry.Source, entry.Note));

    private static Dictionary<SlmpProfileLimit, SlmpCapabilityLimit> IqrLimits()
        => Limits(
            (SlmpProfileLimit.DirectWordRead, 960, "C051", "live", null, null),
            (SlmpProfileLimit.DirectWordWrite, 960, "C051", "live", null, null),
            (SlmpProfileLimit.DirectBitRead, 7168, "C052", "live", null, null),
            (SlmpProfileLimit.DirectBitWrite, 7168, "C052", "live", null, null),
            (SlmpProfileLimit.RandomReadWord, 96, "C054", "live", null, null),
            (SlmpProfileLimit.RandomWriteWord, 80, "C054", "live", 960, null),
            (SlmpProfileLimit.RandomWriteBit, 94, "C053", "live", null, null),
            (SlmpProfileLimit.MonitorRegisterWord, 96, "C054", "live", null, null),
            (SlmpProfileLimit.RandomReadWordExt, 96, "C054", "live", null, null),
            (SlmpProfileLimit.RandomWriteWordExt, 80, "C054", "live", 960, null),
            (SlmpProfileLimit.RandomWriteBitExt, 94, "C053", "live", null, null),
            (SlmpProfileLimit.MonitorRegisterWordExt, 96, "C054", "live", null, null));

    private static Dictionary<SlmpProfileLimit, SlmpCapabilityLimit> IqlMxLimits()
        => Limits(
            (SlmpProfileLimit.DirectWordRead, 960, "C051", "live", null, null),
            (SlmpProfileLimit.DirectWordWrite, 960, "C051", "live", null, null),
            (SlmpProfileLimit.DirectBitRead, 7168, "C052", "live", null, null),
            (SlmpProfileLimit.DirectBitWrite, 7168, "C052", "live", null, null),
            (SlmpProfileLimit.RandomReadWord, 96, "C054", "live", null, null),
            (SlmpProfileLimit.RandomWriteWord, 80, "C054", "live", 960, null),
            (SlmpProfileLimit.RandomWriteBit, 94, "C053", "live", null, null),
            (SlmpProfileLimit.MonitorRegisterWord, 96, "C054", "live", null, null),
            (SlmpProfileLimit.RandomReadWordExt, 96, "C054", "live", null, null),
            (SlmpProfileLimit.RandomWriteWordExt, 80, "C054", "live", 960, null),
            (SlmpProfileLimit.RandomWriteBitExt, 94, "C053", "live", null, null),
            (SlmpProfileLimit.MonitorRegisterWordExt, 96, "C054", "live", null, null));

    private static Dictionary<SlmpProfileLimit, SlmpCapabilityLimit> IqFLimits()
        => Limits(
            (SlmpProfileLimit.DirectWordRead, 960, "C051", "live", null, null),
            (SlmpProfileLimit.DirectWordWrite, 960, "C051", "live", null, null),
            (SlmpProfileLimit.DirectBitRead, 3584, "C052", "live", null, null),
            (SlmpProfileLimit.DirectBitWrite, 3584, "C052", "live", null, null),
            (SlmpProfileLimit.RandomReadWord, 192, "C054", "live", null, null),
            (SlmpProfileLimit.RandomWriteWord, 160, "C054", "live", 1920, null),
            (SlmpProfileLimit.RandomWriteBit, 188, "C053", "live", null, null),
            (SlmpProfileLimit.MonitorRegisterWord, 192, "C054", "not-adopted", null, null),
            (SlmpProfileLimit.RandomReadWordExt, 96, "C054", "live", null, null),
            (SlmpProfileLimit.RandomWriteWordExt, 80, "C054", "live", 960, null),
            (SlmpProfileLimit.RandomWriteBitExt, 94, "C053", "live", null, null),
            (SlmpProfileLimit.MonitorRegisterWordExt, 96, "C054", "not-adopted", null, null));

    private static Dictionary<SlmpProfileLimit, SlmpCapabilityLimit> QlLimits(
        string source,
        int extReadMax = 192,
        string extReadOverEndCode = "C054")
        => Limits(
            (SlmpProfileLimit.DirectWordRead, 960, "C051", source, null, null),
            (SlmpProfileLimit.DirectWordWrite, 960, "C051", source, null, null),
            (SlmpProfileLimit.DirectBitRead, 7168, "C052", source, null, null),
            (SlmpProfileLimit.DirectBitWrite, 7168, "C052", source, null, null),
            (SlmpProfileLimit.RandomReadWord, 192, "C054", source, null, null),
            (SlmpProfileLimit.RandomWriteWord, 160, "C054", source, 1920, null),
            (SlmpProfileLimit.RandomWriteBit, 188, "C053", source, null, null),
            (SlmpProfileLimit.MonitorRegisterWord, 192, "C054", source, null, null),
            (SlmpProfileLimit.RandomReadWordExt, extReadMax, extReadOverEndCode, "inferred", null, null),
            (SlmpProfileLimit.RandomWriteWordExt, 160, "4080", "inferred", 1920, null),
            (SlmpProfileLimit.RandomWriteBitExt, 188, "C053", "inferred", null, null),
            (SlmpProfileLimit.MonitorRegisterWordExt, 192, "C054", "inferred", null, null));

    private static Dictionary<SlmpProfileLimit, SlmpCapabilityLimit> QlUnitLimits(
        int extReadMax,
        bool includeBitExt,
        string bitExtSource = "live")
    {
        var entries = new List<(SlmpProfileLimit Limit, int Max, string? OverEndCode, string Source, int? WeightedMax, string? Note)>
        {
            (SlmpProfileLimit.DirectWordRead, 960, "C051", "live", null, null),
            (SlmpProfileLimit.DirectWordWrite, 960, "C051", "live", null, null),
            (SlmpProfileLimit.DirectBitRead, 7168, "C052", "live", null, null),
            (SlmpProfileLimit.DirectBitWrite, 7168, "C052", "live", null, null),
            (SlmpProfileLimit.RandomReadWord, 192, "C054", "live", null, null),
            (SlmpProfileLimit.RandomWriteWord, 160, "4080", "live", 1920, null),
            (SlmpProfileLimit.RandomWriteBit, 188, "C053", "live", null, null),
            (SlmpProfileLimit.MonitorRegisterWord, 192, "C054", "live", null, null),
            (SlmpProfileLimit.RandomReadWordExt, extReadMax, extReadMax == 185 ? "4080" : "C054", "live", null, null),
            (SlmpProfileLimit.RandomWriteWordExt, 160, "4080", "live", 1920, null),
            (SlmpProfileLimit.MonitorRegisterWordExt, 192, "C054", "live", null, null),
        };

        if (includeBitExt)
        {
            entries.Add((SlmpProfileLimit.RandomWriteBitExt, 188, "C053", bitExtSource, null, null));
        }

        return Limits(entries.ToArray());
    }

    private static Dictionary<SlmpProfileLimit, SlmpCapabilityLimit> Limits(
        params (SlmpProfileLimit Limit, int Max, string? OverEndCode, string Source, int? WeightedMax, string? Note)[] entries)
        => entries.ToDictionary(
            static entry => entry.Limit,
            static entry => new SlmpCapabilityLimit(entry.Max, entry.OverEndCode, entry.Source, entry.WeightedMax, entry.Note));

    private static Dictionary<string, string> WritePolicy(params string[] readOnlyDeviceCodes)
        => readOnlyDeviceCodes.ToDictionary(static deviceCode => deviceCode, static _ => "read-only");

    private static Dictionary<string, string> Policy(params (string DeviceCode, string Policy)[] entries)
        => entries.ToDictionary(static entry => entry.DeviceCode, static entry => entry.Policy);
}
