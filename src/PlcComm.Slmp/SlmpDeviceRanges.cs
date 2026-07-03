using System.Collections.ObjectModel;
using System.Globalization;
namespace PlcComm.Slmp;

/// <summary>Logical device category used by the range catalog.</summary>
public enum SlmpDeviceRangeCategory
{
    Bit,
    Word,
    TimerCounter,
    Index,
    FileRegister,
}

/// <summary>Number notation used by the public address text for the device.</summary>
public enum SlmpDeviceRangeNotation
{
    Base10,
    Base8,
    Base16,
}

/// <summary>One device entry returned by <see cref="SlmpDeviceRangeCatalog"/>.</summary>
/// <param name="Device">Device code or address family string such as <c>D</c> or <c>TS</c>.</param>
/// <param name="Category">Logical category for grouping in monitor tools.</param>
/// <param name="IsBitDevice">True when the device is bit-addressable in normal use.</param>
/// <param name="Supported">True when the PLC profile supports this device.</param>
/// <param name="LowerBound">Lower bound value. Current rules always use 0.</param>
/// <param name="UpperBound">Inclusive last address. For a 0-based range this is <c>PointCount - 1</c>. Null means no finite bound is defined by the rule.</param>
/// <param name="PointCount">Usable point count read or resolved for the PLC profile. Null means no finite count is defined by the rule.</param>
/// <param name="AddressRange">Preformatted address range text such as <c>X000-X1FF</c> or <c>D0-D511</c>.</param>
/// <param name="Notation">Recommended public address notation for this library.</param>
/// <param name="Source">Rule source used to build <paramref name="UpperBound"/>.</param>
/// <param name="Notes">Optional profile-specific caveats.</param>
public sealed record SlmpDeviceRangeEntry(
    string Device,
    SlmpDeviceRangeCategory Category,
    bool IsBitDevice,
    bool Supported,
    uint LowerBound,
    uint? UpperBound,
    uint? PointCount,
    string? AddressRange,
    SlmpDeviceRangeNotation Notation,
    string Source,
    string? Notes);

/// <summary>Result returned by <c>ReadDeviceRangeCatalogAsync</c>.</summary>
/// <param name="Model">Synthetic label for the explicitly selected PLC profile.</param>
/// <param name="ModelCode">Always zero because device-range catalogs do not infer profiles from type-name responses.</param>
/// <param name="HasModelCode">Always false because profile selection is explicit.</param>
/// <param name="PlcProfile">Resolved canonical PLC profile.</param>
/// <param name="Entries">Device entries for the resolved profile.</param>
public sealed record SlmpDeviceRangeCatalog(
    string Model,
    ushort ModelCode,
    bool HasModelCode,
    SlmpPlcProfile PlcProfile,
    IReadOnlyList<SlmpDeviceRangeEntry> Entries);

internal enum SlmpRangeValueKind
{
    Unsupported,
    Undefined,
    Fixed,
    WordRegister,
    DWordRegister,
    WordRegisterClipped,
    DWordRegisterClipped,
}

internal sealed record SlmpRangeValueSpec(
    SlmpRangeValueKind Kind,
    int Register,
    uint FixedValue,
    uint ClipValue,
    string Source,
    string? Notes = null);

internal sealed record SlmpDeviceRangeRow(
    string Item,
    SlmpDeviceRangeCategory Category,
    IReadOnlyList<(string Device, bool IsBitDevice)> Devices,
    SlmpDeviceRangeNotation Notation);

internal sealed record SlmpDeviceRangeProfile(
    SlmpPlcProfile PlcProfile,
    int RegisterStart,
    int RegisterCount,
    IReadOnlyDictionary<string, SlmpRangeValueSpec> Rules);

internal static class SlmpDeviceRangeResolver
{
    private static readonly string[] OrderedItems =
    [
        "X", "Y", "M", "B", "SB", "F", "V", "L", "S",
        "D", "W", "SW", "R",
        "T", "ST", "C", "LT", "LST", "LC",
        "Z", "LZ", "ZR", "RD",
        "SM", "SD",
    ];

    private static readonly ReadOnlyDictionary<string, SlmpDeviceRangeRow> Rows =
        new ReadOnlyDictionary<string, SlmpDeviceRangeRow>(
            new Dictionary<string, SlmpDeviceRangeRow>(StringComparer.Ordinal)
            {
                ["X"] = Single("X", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base16),
                ["Y"] = Single("Y", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base16),
                ["M"] = Single("M", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base10),
                ["B"] = Single("B", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base16),
                ["SB"] = Single("SB", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base16),
                ["F"] = Single("F", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base10),
                ["V"] = Single("V", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base10),
                ["L"] = Single("L", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base10),
                ["S"] = Single("S", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base10),
                ["D"] = Single("D", SlmpDeviceRangeCategory.Word, isBitDevice: false, SlmpDeviceRangeNotation.Base10),
                ["W"] = Single("W", SlmpDeviceRangeCategory.Word, isBitDevice: false, SlmpDeviceRangeNotation.Base16),
                ["SW"] = Single("SW", SlmpDeviceRangeCategory.Word, isBitDevice: false, SlmpDeviceRangeNotation.Base16),
                ["R"] = Single("R", SlmpDeviceRangeCategory.Word, isBitDevice: false, SlmpDeviceRangeNotation.Base10),
                ["T"] = Multi("T", SlmpDeviceRangeCategory.TimerCounter, SlmpDeviceRangeNotation.Base10, ("TS", true), ("TC", true), ("TN", false)),
                ["ST"] = Multi("ST", SlmpDeviceRangeCategory.TimerCounter, SlmpDeviceRangeNotation.Base10, ("STS", true), ("STC", true), ("STN", false)),
                ["C"] = Multi("C", SlmpDeviceRangeCategory.TimerCounter, SlmpDeviceRangeNotation.Base10, ("CS", true), ("CC", true), ("CN", false)),
                ["LT"] = Multi("LT", SlmpDeviceRangeCategory.TimerCounter, SlmpDeviceRangeNotation.Base10, ("LTS", true), ("LTC", true), ("LTN", false)),
                ["LST"] = Multi("LST", SlmpDeviceRangeCategory.TimerCounter, SlmpDeviceRangeNotation.Base10, ("LSTS", true), ("LSTC", true), ("LSTN", false)),
                ["LC"] = Multi("LC", SlmpDeviceRangeCategory.TimerCounter, SlmpDeviceRangeNotation.Base10, ("LCS", true), ("LCC", true), ("LCN", false)),
                ["Z"] = Single("Z", SlmpDeviceRangeCategory.Index, isBitDevice: false, SlmpDeviceRangeNotation.Base10),
                ["LZ"] = Single("LZ", SlmpDeviceRangeCategory.Index, isBitDevice: false, SlmpDeviceRangeNotation.Base10),
                ["ZR"] = Single("ZR", SlmpDeviceRangeCategory.FileRegister, isBitDevice: false, SlmpDeviceRangeNotation.Base10),
                ["RD"] = Single("RD", SlmpDeviceRangeCategory.FileRegister, isBitDevice: false, SlmpDeviceRangeNotation.Base10),
                ["SM"] = Single("SM", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base10),
                ["SD"] = Single("SD", SlmpDeviceRangeCategory.Word, isBitDevice: false, SlmpDeviceRangeNotation.Base10),
            });

    private static readonly ReadOnlyDictionary<SlmpPlcProfile, SlmpDeviceRangeProfile> Profiles =
        new ReadOnlyDictionary<SlmpPlcProfile, SlmpDeviceRangeProfile>(
            new Dictionary<SlmpPlcProfile, SlmpDeviceRangeProfile>
            {
                [SlmpPlcProfile.IqR] = CreateProfile(
                    SlmpPlcProfile.IqR,
                    260,
                    50,
                    ("X", DWordRegister(260, "SD260-SD261 (32-bit)")),
                    ("Y", DWordRegister(262, "SD262-SD263 (32-bit)")),
                    ("M", DWordRegister(264, "SD264-SD265 (32-bit)")),
                    ("B", DWordRegister(266, "SD266-SD267 (32-bit)")),
                    ("SB", DWordRegister(268, "SD268-SD269 (32-bit)")),
                    ("F", DWordRegister(270, "SD270-SD271 (32-bit)")),
                    ("V", DWordRegister(272, "SD272-SD273 (32-bit)")),
                    ("L", DWordRegister(274, "SD274-SD275 (32-bit)")),
                    ("S", DWordRegister(276, "SD276-SD277 (32-bit)")),
                    ("D", DWordRegister(280, "SD280-SD281 (32-bit)")),
                    ("W", DWordRegister(282, "SD282-SD283 (32-bit)")),
                    ("SW", DWordRegister(284, "SD284-SD285 (32-bit)")),
                    ("R", DWordRegisterClipped(306, 32768, "SD306-SD307 (32-bit)", "Upper bound is clipped to 32768.")),
                    ("T", DWordRegister(288, "SD288-SD289 (32-bit)")),
                    ("ST", DWordRegister(290, "SD290-SD291 (32-bit)")),
                    ("C", DWordRegister(292, "SD292-SD293 (32-bit)")),
                    ("LT", DWordRegister(294, "SD294-SD295 (32-bit)")),
                    ("LST", DWordRegister(296, "SD296-SD297 (32-bit)")),
                    ("LC", DWordRegister(298, "SD298-SD299 (32-bit)")),
                    ("Z", WordRegister(300, "SD300")),
                    ("LZ", WordRegister(302, "SD302")),
                    ("ZR", DWordRegister(306, "SD306-SD307 (32-bit)")),
                    ("RD", DWordRegister(308, "SD308-SD309 (32-bit)")),
                    ("SM", Fixed(4096, "Fixed family limit")),
                    ("SD", Fixed(4096, "Fixed family limit"))),
                [SlmpPlcProfile.IqL] = CreateProfile(
                    SlmpPlcProfile.IqL,
                    260,
                    50,
                    ("X", DWordRegister(260, "SD260-SD261 (32-bit)")),
                    ("Y", DWordRegister(262, "SD262-SD263 (32-bit)")),
                    ("M", DWordRegister(264, "SD264-SD265 (32-bit)")),
                    ("B", DWordRegister(266, "SD266-SD267 (32-bit)")),
                    ("SB", DWordRegister(268, "SD268-SD269 (32-bit)")),
                    ("F", DWordRegister(270, "SD270-SD271 (32-bit)")),
                    ("V", DWordRegister(272, "SD272-SD273 (32-bit)")),
                    ("L", DWordRegister(274, "SD274-SD275 (32-bit)")),
                    ("S", DWordRegister(276, "SD276-SD277 (32-bit)")),
                    ("D", DWordRegister(280, "SD280-SD281 (32-bit)")),
                    ("W", DWordRegister(282, "SD282-SD283 (32-bit)")),
                    ("SW", DWordRegister(284, "SD284-SD285 (32-bit)")),
                    ("R", DWordRegisterClipped(306, 32768, "SD306-SD307 (32-bit)", "Upper bound is clipped to 32768.")),
                    ("T", DWordRegister(288, "SD288-SD289 (32-bit)")),
                    ("ST", DWordRegister(290, "SD290-SD291 (32-bit)")),
                    ("C", DWordRegister(292, "SD292-SD293 (32-bit)")),
                    ("LT", DWordRegister(294, "SD294-SD295 (32-bit)")),
                    ("LST", DWordRegister(296, "SD296-SD297 (32-bit)")),
                    ("LC", DWordRegister(298, "SD298-SD299 (32-bit)")),
                    ("Z", WordRegister(300, "SD300")),
                    ("LZ", WordRegister(302, "SD302")),
                    ("ZR", DWordRegister(306, "SD306-SD307 (32-bit)")),
                    ("RD", DWordRegister(308, "SD308-SD309 (32-bit)")),
                    ("SM", Fixed(4096, "Fixed family limit")),
                    ("SD", Fixed(4096, "Fixed family limit"))),
                [SlmpPlcProfile.MxF] = CreateProfile(
                    SlmpPlcProfile.MxF,
                    260,
                    50,
                    ("X", DWordRegister(260, "SD260-SD261 (32-bit)")),
                    ("Y", DWordRegister(262, "SD262-SD263 (32-bit)")),
                    ("M", DWordRegister(264, "SD264-SD265 (32-bit)")),
                    ("B", DWordRegister(266, "SD266-SD267 (32-bit)")),
                    ("SB", DWordRegister(268, "SD268-SD269 (32-bit)")),
                    ("F", DWordRegister(270, "SD270-SD271 (32-bit)")),
                    ("V", DWordRegister(272, "SD272-SD273 (32-bit)")),
                    ("L", DWordRegister(274, "SD274-SD275 (32-bit)")),
                    ("S", DWordRegister(276, "SD276-SD277 (32-bit)")),
                    ("D", DWordRegister(280, "SD280-SD281 (32-bit)")),
                    ("W", DWordRegister(282, "SD282-SD283 (32-bit)")),
                    ("SW", DWordRegister(284, "SD284-SD285 (32-bit)")),
                    ("R", DWordRegisterClipped(306, 32768, "SD306-SD307 (32-bit)", "Upper bound is clipped to 32768.")),
                    ("T", DWordRegister(288, "SD288-SD289 (32-bit)")),
                    ("ST", DWordRegister(290, "SD290-SD291 (32-bit)")),
                    ("C", DWordRegister(292, "SD292-SD293 (32-bit)")),
                    ("LT", DWordRegister(294, "SD294-SD295 (32-bit)")),
                    ("LST", DWordRegister(296, "SD296-SD297 (32-bit)")),
                    ("LC", DWordRegister(298, "SD298-SD299 (32-bit)")),
                    ("Z", WordRegister(300, "SD300")),
                    ("LZ", WordRegister(302, "SD302")),
                    ("ZR", DWordRegister(306, "SD306-SD307 (32-bit)")),
                    ("RD", DWordRegister(308, "SD308-SD309 (32-bit)")),
                    ("SM", Fixed(10000, "Fixed family limit")),
                    ("SD", Fixed(10000, "Fixed family limit"))),
                [SlmpPlcProfile.MxR] = CreateProfile(
                    SlmpPlcProfile.MxR,
                    260,
                    50,
                    ("X", DWordRegister(260, "SD260-SD261 (32-bit)")),
                    ("Y", DWordRegister(262, "SD262-SD263 (32-bit)")),
                    ("M", DWordRegister(264, "SD264-SD265 (32-bit)")),
                    ("B", DWordRegister(266, "SD266-SD267 (32-bit)")),
                    ("SB", DWordRegister(268, "SD268-SD269 (32-bit)")),
                    ("F", DWordRegister(270, "SD270-SD271 (32-bit)")),
                    ("V", DWordRegister(272, "SD272-SD273 (32-bit)")),
                    ("L", DWordRegister(274, "SD274-SD275 (32-bit)")),
                    ("S", DWordRegister(276, "SD276-SD277 (32-bit)")),
                    ("D", DWordRegister(280, "SD280-SD281 (32-bit)")),
                    ("W", DWordRegister(282, "SD282-SD283 (32-bit)")),
                    ("SW", DWordRegister(284, "SD284-SD285 (32-bit)")),
                    ("R", DWordRegisterClipped(306, 32768, "SD306-SD307 (32-bit)", "Upper bound is clipped to 32768.")),
                    ("T", DWordRegister(288, "SD288-SD289 (32-bit)")),
                    ("ST", DWordRegister(290, "SD290-SD291 (32-bit)")),
                    ("C", DWordRegister(292, "SD292-SD293 (32-bit)")),
                    ("LT", DWordRegister(294, "SD294-SD295 (32-bit)")),
                    ("LST", DWordRegister(296, "SD296-SD297 (32-bit)")),
                    ("LC", DWordRegister(298, "SD298-SD299 (32-bit)")),
                    ("Z", WordRegister(300, "SD300")),
                    ("LZ", WordRegister(302, "SD302")),
                    ("ZR", DWordRegister(306, "SD306-SD307 (32-bit)")),
                    ("RD", DWordRegister(308, "SD308-SD309 (32-bit)")),
                    ("SM", Fixed(4496, "Fixed family limit")),
                    ("SD", Fixed(4496, "Fixed family limit"))),
                [SlmpPlcProfile.IqF] = CreateProfile(
                    SlmpPlcProfile.IqF,
                    260,
                    46,
                    ("X", DWordRegister(260, "SD260-SD261 (32-bit)", "Manual addressing for iQ-F X devices is octal.")),
                    ("Y", DWordRegister(262, "SD262-SD263 (32-bit)", "Manual addressing for iQ-F Y devices is octal.")),
                    ("M", DWordRegister(264, "SD264-SD265 (32-bit)")),
                    ("B", DWordRegister(266, "SD266-SD267 (32-bit)")),
                    ("SB", DWordRegister(268, "SD268-SD269 (32-bit)")),
                    ("F", DWordRegister(270, "SD270-SD271 (32-bit)")),
                    ("V", Unsupported("Not supported on iQ-F.")),
                    ("L", DWordRegister(274, "SD274-SD275 (32-bit)")),
                    ("S", DWordRegister(276, "SD276-SD277 (32-bit)")),
                    ("D", DWordRegister(280, "SD280-SD281 (32-bit)")),
                    ("W", DWordRegister(282, "SD282-SD283 (32-bit)")),
                    ("SW", DWordRegister(284, "SD284-SD285 (32-bit)")),
                    ("R", DWordRegister(304, "SD304-SD305 (32-bit)")),
                    ("T", DWordRegister(288, "SD288-SD289 (32-bit)")),
                    ("ST", DWordRegister(290, "SD290-SD291 (32-bit)")),
                    ("C", DWordRegister(292, "SD292-SD293 (32-bit)")),
                    ("LT", Unsupported("Not supported on iQ-F.")),
                    ("LST", Unsupported("Not supported on iQ-F.")),
                    ("LC", DWordRegister(298, "SD298-SD299 (32-bit)")),
                    ("Z", WordRegister(300, "SD300")),
                    ("LZ", WordRegister(302, "SD302")),
                    ("ZR", Unsupported("Not supported on iQ-F.")),
                    ("RD", Unsupported("Not supported on iQ-F.")),
                    ("SM", Fixed(10000, "Fixed family limit")),
                    ("SD", Fixed(12000, "Fixed family limit"))),
                [SlmpPlcProfile.QCpu] = CreateProfile(
                    SlmpPlcProfile.QCpu,
                    290,
                    15,
                    ("X", WordRegister(290, "SD290")),
                    ("Y", WordRegister(291, "SD291")),
                    ("M", WordRegisterClipped(292, 32768, "SD292", "Upper bound is clipped to 32768.")),
                    ("B", WordRegisterClipped(294, 32768, "SD294", "Upper bound is clipped to 32768.")),
                    ("SB", WordRegister(296, "SD296")),
                    ("F", WordRegister(295, "SD295")),
                    ("V", WordRegister(297, "SD297")),
                    ("L", WordRegister(293, "SD293")),
                    ("S", WordRegister(298, "SD298")),
                    ("D", WordRegisterClipped(302, 32768, "SD302", "Upper bound is clipped to 32768 and excludes extended area.")),
                    ("W", WordRegisterClipped(303, 32768, "SD303", "Upper bound is clipped to 32768 and excludes extended area.")),
                    ("SW", WordRegister(304, "SD304")),
                    ("R", Fixed(32768, "Fixed family limit")),
                    ("T", WordRegister(299, "SD299")),
                    ("ST", WordRegister(300, "SD300")),
                    ("C", WordRegister(301, "SD301")),
                    ("LT", Unsupported("Not supported on QCPU.")),
                    ("LST", Unsupported("Not supported on QCPU.")),
                    ("LC", Unsupported("Not supported on QCPU.")),
                    ("Z", Fixed(10, "Fixed family limit")),
                    ("LZ", Unsupported("Not supported on QCPU.")),
                    ("ZR", Undefined("No finite upper-bound register is defined for QCPU ZR.")),
                    ("RD", Unsupported("Not supported on QCPU.")),
                    ("SM", Fixed(1024, "Fixed family limit")),
                    ("SD", Fixed(1024, "Fixed family limit"))),
                [SlmpPlcProfile.LCpu] = CreateProfile(
                    SlmpPlcProfile.LCpu,
                    286,
                    26,
                    ("X", WordRegister(290, "SD290")),
                    ("Y", WordRegister(291, "SD291")),
                    ("M", DWordRegister(286, "SD286-SD287 (32-bit)")),
                    ("B", DWordRegister(288, "SD288-SD289 (32-bit)")),
                    ("SB", WordRegister(296, "SD296")),
                    ("F", WordRegister(295, "SD295")),
                    ("V", WordRegister(297, "SD297")),
                    ("L", WordRegister(293, "SD293")),
                    ("S", WordRegister(298, "SD298")),
                    ("D", DWordRegister(308, "SD308-SD309 (32-bit)")),
                    ("W", DWordRegister(310, "SD310-SD311 (32-bit)")),
                    ("SW", WordRegister(304, "SD304")),
                    ("R", DWordRegisterClipped(306, 32768, "SD306-SD307 (32-bit)", "Upper bound is clipped to 32768.")),
                    ("T", WordRegister(299, "SD299")),
                    ("ST", WordRegister(300, "SD300")),
                    ("C", WordRegister(301, "SD301")),
                    ("LT", Unsupported("Not supported on LCPU.")),
                    ("LST", Unsupported("Not supported on LCPU.")),
                    ("LC", Unsupported("Not supported on LCPU.")),
                    ("Z", Fixed(20, "Fixed family limit")),
                    ("LZ", Unsupported("Not supported on LCPU.")),
                    ("ZR", DWordRegister(306, "SD306-SD307 (32-bit)")),
                    ("RD", Unsupported("Not supported on LCPU.")),
                    ("SM", Fixed(2048, "Fixed family limit")),
                    ("SD", Fixed(2048, "Fixed family limit"))),
                [SlmpPlcProfile.QnU] = CreateProfile(
                    SlmpPlcProfile.QnU,
                    286,
                    26,
                    ("X", WordRegister(290, "SD290")),
                    ("Y", WordRegister(291, "SD291")),
                    ("M", DWordRegister(286, "SD286-SD287 (32-bit)")),
                    ("B", DWordRegister(288, "SD288-SD289 (32-bit)")),
                    ("SB", WordRegister(296, "SD296")),
                    ("F", WordRegister(295, "SD295")),
                    ("V", WordRegister(297, "SD297")),
                    ("L", WordRegister(293, "SD293")),
                    ("S", WordRegister(298, "SD298")),
                    ("D", DWordRegister(308, "SD308-SD309 (32-bit)")),
                    ("W", DWordRegister(310, "SD310-SD311 (32-bit)")),
                    ("SW", WordRegister(304, "SD304")),
                    ("R", DWordRegisterClipped(306, 32768, "SD306-SD307 (32-bit)", "Upper bound is clipped to 32768.")),
                    ("T", WordRegister(299, "SD299")),
                    ("ST", WordRegister(300, "SD300")),
                    ("C", WordRegister(301, "SD301")),
                    ("LT", Unsupported("Not supported on QnU.")),
                    ("LST", Unsupported("Not supported on QnU.")),
                    ("LC", Unsupported("Not supported on QnU.")),
                    ("Z", Fixed(20, "Fixed family limit")),
                    ("LZ", Unsupported("Not supported on QnU.")),
                    ("ZR", DWordRegister(306, "SD306-SD307 (32-bit)")),
                    ("RD", Unsupported("Not supported on QnU.")),
                    ("SM", Fixed(2048, "Fixed family limit")),
                    ("SD", Fixed(2048, "Fixed family limit"))),
                [SlmpPlcProfile.QnUDV] = CreateProfile(
                    SlmpPlcProfile.QnUDV,
                    286,
                    26,
                    ("X", WordRegister(290, "SD290")),
                    ("Y", WordRegister(291, "SD291")),
                    ("M", DWordRegister(286, "SD286-SD287 (32-bit)")),
                    ("B", DWordRegister(288, "SD288-SD289 (32-bit)")),
                    ("SB", WordRegister(296, "SD296")),
                    ("F", WordRegister(295, "SD295")),
                    ("V", WordRegister(297, "SD297")),
                    ("L", WordRegister(293, "SD293")),
                    ("S", WordRegister(298, "SD298")),
                    ("D", DWordRegister(308, "SD308-SD309 (32-bit)")),
                    ("W", DWordRegister(310, "SD310-SD311 (32-bit)")),
                    ("SW", WordRegister(304, "SD304")),
                    ("R", DWordRegisterClipped(306, 32768, "SD306-SD307 (32-bit)", "Upper bound is clipped to 32768.")),
                    ("T", WordRegister(299, "SD299")),
                    ("ST", WordRegister(300, "SD300")),
                    ("C", WordRegister(301, "SD301")),
                    ("LT", Unsupported("Not supported on QnUDV.")),
                    ("LST", Unsupported("Not supported on QnUDV.")),
                    ("LC", Unsupported("Not supported on QnUDV.")),
                    ("Z", Fixed(20, "Fixed family limit")),
                    ("LZ", Unsupported("Not supported on QnUDV.")),
                    ("ZR", DWordRegister(306, "SD306-SD307 (32-bit)")),
                    ("RD", Unsupported("Not supported on QnUDV.")),
                    ("SM", Fixed(2048, "Fixed family limit")),
                    ("SD", Fixed(2048, "Fixed family limit"))),
            });

    public static SlmpDeviceRangeProfile ResolveProfile(SlmpPlcProfile plcProfile)
    {
        return Profiles[plcProfile];
    }

    public static async Task<IReadOnlyDictionary<int, ushort>> ReadRegistersAsync(
        SlmpClient client,
        SlmpDeviceRangeProfile profile,
        CancellationToken cancellationToken)
    {
        if (profile.RegisterCount <= 0)
        {
            return new Dictionary<int, ushort>();
        }

        var values = await client.ReadWordsRawAsync(
            new SlmpDeviceAddress(SlmpDeviceCode.SD, checked((uint)profile.RegisterStart)),
            checked((ushort)profile.RegisterCount),
            cancellationToken).ConfigureAwait(false);

        var map = new Dictionary<int, ushort>(profile.RegisterCount);
        for (var i = 0; i < values.Length; i++)
        {
            map[profile.RegisterStart + i] = values[i];
        }

        return map;
    }

    private static SlmpDeviceRangeCatalog BuildCatalog(
        SlmpDeviceRangeProfile profile,
        IReadOnlyDictionary<int, ushort> registers)
    {
        var entries = new List<SlmpDeviceRangeEntry>(64);
        foreach (var item in OrderedItems)
        {
            var row = Rows[item];
            var spec = profile.Rules[item];
            var pointCount = EvaluatePointCount(spec, registers);
            var upperBound = ToUpperBound(pointCount);
            var supported = spec.Kind != SlmpRangeValueKind.Unsupported;

            foreach (var (device, isBitDevice) in row.Devices)
            {
                var notation = ResolveNotation(profile.PlcProfile, device, row.Notation);
                entries.Add(new SlmpDeviceRangeEntry(
                    device,
                    row.Category,
                    isBitDevice,
                    supported,
                    0,
                    upperBound,
                    pointCount,
                    FormatAddressRange(device, notation, upperBound),
                    notation,
                    spec.Source,
                    spec.Notes));
            }
        }

        return new SlmpDeviceRangeCatalog(
            GetProfileLabel(profile.PlcProfile),
            0,
            false,
            profile.PlcProfile,
            entries);
    }

    public static SlmpDeviceRangeCatalog BuildCatalog(
        SlmpPlcProfile plcProfile,
        IReadOnlyDictionary<int, ushort> registers)
    {
        return BuildCatalog(ResolveProfile(plcProfile), registers);
    }

    internal static SlmpDeviceRangeCatalog ReplaceFixedPointCount(
        SlmpDeviceRangeCatalog catalog,
        string device,
        uint pointCount,
        string source,
        string note)
    {
        var upperBound = ToUpperBound(pointCount);
        var entries = catalog.Entries
            .Select(entry =>
            {
                if (!string.Equals(entry.Device, device, StringComparison.Ordinal))
                    return entry;

                return entry with
                {
                    UpperBound = upperBound,
                    PointCount = pointCount,
                    AddressRange = FormatAddressRange(entry.Device, entry.Notation, upperBound),
                    Source = source,
                    Notes = string.IsNullOrWhiteSpace(entry.Notes)
                        ? note
                        : $"{entry.Notes} {note}",
                };
            })
            .ToArray();

        return catalog with { Entries = entries };
    }

    public static string GetProfileLabel(SlmpPlcProfile plcProfile)
    {
        return plcProfile switch
        {
            SlmpPlcProfile.IqR => "IQ-R",
            SlmpPlcProfile.IqL => "iQ-L",
            SlmpPlcProfile.MxF => "MX-F",
            SlmpPlcProfile.MxR => "MX-R",
            SlmpPlcProfile.IqF => "IQ-F",
            SlmpPlcProfile.QCpu => "QCPU",
            SlmpPlcProfile.LCpu => "LCPU",
            SlmpPlcProfile.QnU => "QnU",
            SlmpPlcProfile.QnUDV => "QnUDV",
            _ => plcProfile.ToString(),
        };
    }

    private static uint? EvaluatePointCount(SlmpRangeValueSpec spec, IReadOnlyDictionary<int, ushort> registers)
    {
        return spec.Kind switch
        {
            SlmpRangeValueKind.Unsupported => null,
            SlmpRangeValueKind.Undefined => null,
            SlmpRangeValueKind.Fixed => spec.FixedValue,
            SlmpRangeValueKind.WordRegister => ReadWord(registers, spec.Register),
            SlmpRangeValueKind.DWordRegister => ReadDWord(registers, spec.Register),
            SlmpRangeValueKind.WordRegisterClipped => Math.Min(ReadWord(registers, spec.Register), spec.ClipValue),
            SlmpRangeValueKind.DWordRegisterClipped => Math.Min(ReadDWord(registers, spec.Register), spec.ClipValue),
            _ => throw new InvalidOperationException($"Unsupported range rule kind: {spec.Kind}."),
        };
    }

    private static uint? ToUpperBound(uint? pointCount)
        => pointCount switch
        {
            null => null,
            0 => null,
            _ => pointCount.Value - 1,
        };

    private static SlmpDeviceRangeNotation ResolveNotation(
        SlmpPlcProfile plcProfile,
        string device,
        SlmpDeviceRangeNotation defaultNotation)
    {
        if (plcProfile == SlmpPlcProfile.IqF &&
            (string.Equals(device, "X", StringComparison.Ordinal) || string.Equals(device, "Y", StringComparison.Ordinal)))
        {
            return SlmpDeviceRangeNotation.Base8;
        }

        return defaultNotation;
    }

    private static string? FormatAddressRange(string device, SlmpDeviceRangeNotation notation, uint? upperBound)
    {
        if (upperBound is null)
        {
            return null;
        }

        return notation switch
        {
            SlmpDeviceRangeNotation.Base10 => string.Create(
                CultureInfo.InvariantCulture,
                $"{device}0-{device}{upperBound.Value}"),
            SlmpDeviceRangeNotation.Base8 => FormatOctalAddressRange(device, upperBound.Value),
            SlmpDeviceRangeNotation.Base16 => FormatHexAddressRange(device, upperBound.Value),
            _ => throw new InvalidOperationException($"Unsupported address notation: {notation}."),
        };
    }

    private static string FormatOctalAddressRange(string device, uint upperBound)
    {
        var upperText = Convert.ToString(upperBound, 8) ?? throw new InvalidOperationException("Octal conversion failed.");
        var width = Math.Max(3, upperText.Length);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{device}{new string('0', width)}-{device}{upperText.PadLeft(width, '0')}");
    }

    private static string FormatHexAddressRange(string device, uint upperBound)
    {
        var width = Math.Max(3, upperBound.ToString("X", CultureInfo.InvariantCulture).Length);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{device}{0u.ToString($"X{width}", CultureInfo.InvariantCulture)}-{device}{upperBound.ToString($"X{width}", CultureInfo.InvariantCulture)}");
    }

    private static uint ReadWord(IReadOnlyDictionary<int, ushort> registers, int register)
    {
        if (!registers.TryGetValue(register, out var value))
        {
            throw new SlmpError($"Device-range resolver is missing SD{register}.");
        }

        return value;
    }

    private static uint ReadDWord(IReadOnlyDictionary<int, ushort> registers, int register)
    {
        if (!registers.TryGetValue(register, out var low))
        {
            throw new SlmpError($"Device-range resolver is missing SD{register}.");
        }

        if (!registers.TryGetValue(register + 1, out var high))
        {
            throw new SlmpError($"Device-range resolver is missing SD{register + 1}.");
        }

        return low | ((uint)high << 16);
    }

    private static SlmpDeviceRangeRow Single(
        string item,
        SlmpDeviceRangeCategory category,
        bool isBitDevice,
        SlmpDeviceRangeNotation notation)
        => new(item, category, [(item, isBitDevice)], notation);

    private static SlmpDeviceRangeRow Multi(
        string item,
        SlmpDeviceRangeCategory category,
        SlmpDeviceRangeNotation notation,
        params (string Device, bool IsBitDevice)[] devices)
        => new(item, category, devices, notation);

    private static SlmpDeviceRangeProfile CreateProfile(
        SlmpPlcProfile plcProfile,
        int registerStart,
        int registerCount,
        params (string Item, SlmpRangeValueSpec Spec)[] rules)
    {
        var map = new Dictionary<string, SlmpRangeValueSpec>(StringComparer.Ordinal);
        foreach (var (item, spec) in rules)
        {
            map[item] = spec;
        }

        return new SlmpDeviceRangeProfile(plcProfile, registerStart, registerCount, new ReadOnlyDictionary<string, SlmpRangeValueSpec>(map));
    }

    private static SlmpRangeValueSpec Fixed(uint value, string source, string? notes = null)
        => new(SlmpRangeValueKind.Fixed, 0, value, 0, source, notes);

    private static SlmpRangeValueSpec WordRegister(int register, string source, string? notes = null)
        => new(SlmpRangeValueKind.WordRegister, register, 0, 0, source, notes);

    private static SlmpRangeValueSpec DWordRegister(int register, string source, string? notes = null)
        => new(SlmpRangeValueKind.DWordRegister, register, 0, 0, source, notes);

    private static SlmpRangeValueSpec WordRegisterClipped(int register, uint clipValue, string source, string? notes = null)
        => new(SlmpRangeValueKind.WordRegisterClipped, register, 0, clipValue, source, notes);

    private static SlmpRangeValueSpec DWordRegisterClipped(int register, uint clipValue, string source, string? notes = null)
        => new(SlmpRangeValueKind.DWordRegisterClipped, register, 0, clipValue, source, notes);

    private static SlmpRangeValueSpec Undefined(string notes)
        => new(SlmpRangeValueKind.Undefined, 0, 0, 0, "No finite upper-bound register", notes);

    private static SlmpRangeValueSpec Unsupported(string notes)
        => new(SlmpRangeValueKind.Unsupported, 0, 0, 0, "Not supported", notes);
}
