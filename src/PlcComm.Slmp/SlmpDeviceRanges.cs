using System.Collections.ObjectModel;
using System.Globalization;

namespace PlcComm.Slmp;

/// <summary>Resolved PLC family used for device-range rules.</summary>
public enum SlmpDeviceRangeFamily
{
    /// <summary>MELSEC iQ-R family.</summary>
    IqR,
    /// <summary>MELSEC MX-F family.</summary>
    MxF,
    /// <summary>MELSEC MX-R family.</summary>
    MxR,
    /// <summary>MELSEC iQ-F family.</summary>
    IqF,
    /// <summary>MELSEC QCPU family.</summary>
    QCpu,
    /// <summary>MELSEC LCPU family.</summary>
    LCpu,
    /// <summary>MELSEC QnU family.</summary>
    QnU,
    /// <summary>MELSEC QnUDV family.</summary>
    QnUDV,
}

/// <summary>Logical device category used by the range catalog.</summary>
public enum SlmpDeviceRangeCategory
{
    Bit,
    Word,
    TimerCounter,
    Index,
    FileRefresh,
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
/// <param name="Supported">True when the family supports this device.</param>
/// <param name="LowerBound">Lower bound value. Current rules always use 0.</param>
/// <param name="UpperBound">Inclusive last address. For a 0-based range this is <c>PointCount - 1</c>. Null means no finite bound is defined by the rule.</param>
/// <param name="PointCount">Usable point count read or resolved for the family. Null means no finite count is defined by the rule.</param>
/// <param name="AddressRange">Preformatted address range text such as <c>X000-X1FF</c> or <c>D0-D511</c>.</param>
/// <param name="Notation">Recommended public address notation for this library.</param>
/// <param name="Source">Rule source used to build <paramref name="UpperBound"/>.</param>
/// <param name="Notes">Optional family-specific caveats.</param>
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
/// <param name="Model">PLC model text when known, or a user-selected family label.</param>
/// <param name="ModelCode">Model code when known. Zero when the caller selected the family explicitly.</param>
/// <param name="HasModelCode">True when <paramref name="ModelCode"/> came from the PLC response.</param>
/// <param name="Family">Resolved device-range family.</param>
/// <param name="Entries">Device entries for the resolved family.</param>
public sealed record SlmpDeviceRangeCatalog(
    string Model,
    ushort ModelCode,
    bool HasModelCode,
    SlmpDeviceRangeFamily Family,
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
    SlmpDeviceRangeFamily Family,
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
                ["ZR"] = Single("ZR", SlmpDeviceRangeCategory.FileRefresh, isBitDevice: false, SlmpDeviceRangeNotation.Base10),
                ["RD"] = Single("RD", SlmpDeviceRangeCategory.FileRefresh, isBitDevice: false, SlmpDeviceRangeNotation.Base10),
                ["SM"] = Single("SM", SlmpDeviceRangeCategory.Bit, isBitDevice: true, SlmpDeviceRangeNotation.Base10),
                ["SD"] = Single("SD", SlmpDeviceRangeCategory.Word, isBitDevice: false, SlmpDeviceRangeNotation.Base10),
            });

    private static readonly ReadOnlyDictionary<ushort, SlmpDeviceRangeFamily> ModelCodeFamilies =
        new ReadOnlyDictionary<ushort, SlmpDeviceRangeFamily>(
            new Dictionary<ushort, SlmpDeviceRangeFamily>
            {
                [0x0250] = SlmpDeviceRangeFamily.QCpu,
                [0x0251] = SlmpDeviceRangeFamily.QCpu,
                [0x0041] = SlmpDeviceRangeFamily.QCpu,
                [0x0042] = SlmpDeviceRangeFamily.QCpu,
                [0x0043] = SlmpDeviceRangeFamily.QCpu,
                [0x0044] = SlmpDeviceRangeFamily.QCpu,
                [0x004B] = SlmpDeviceRangeFamily.QCpu,
                [0x004C] = SlmpDeviceRangeFamily.QCpu,
                [0x0230] = SlmpDeviceRangeFamily.QCpu,
                [0x0260] = SlmpDeviceRangeFamily.QnU,
                [0x0261] = SlmpDeviceRangeFamily.QnU,
                [0x0262] = SlmpDeviceRangeFamily.QnU,
                [0x0263] = SlmpDeviceRangeFamily.QnU,
                [0x0268] = SlmpDeviceRangeFamily.QnU,
                [0x0269] = SlmpDeviceRangeFamily.QnU,
                [0x026A] = SlmpDeviceRangeFamily.QnU,
                [0x0266] = SlmpDeviceRangeFamily.QnU,
                [0x026B] = SlmpDeviceRangeFamily.QnU,
                [0x0267] = SlmpDeviceRangeFamily.QnU,
                [0x026C] = SlmpDeviceRangeFamily.QnU,
                [0x026D] = SlmpDeviceRangeFamily.QnU,
                [0x026E] = SlmpDeviceRangeFamily.QnU,
                [0x0366] = SlmpDeviceRangeFamily.QnUDV,
                [0x0367] = SlmpDeviceRangeFamily.QnUDV,
                [0x0368] = SlmpDeviceRangeFamily.QnUDV,
                [0x036A] = SlmpDeviceRangeFamily.QnUDV,
                [0x036C] = SlmpDeviceRangeFamily.QnUDV,
                [0x0543] = SlmpDeviceRangeFamily.LCpu,
                [0x0541] = SlmpDeviceRangeFamily.LCpu,
                [0x0544] = SlmpDeviceRangeFamily.LCpu,
                [0x0545] = SlmpDeviceRangeFamily.LCpu,
                [0x0542] = SlmpDeviceRangeFamily.LCpu,
                [0x48C0] = SlmpDeviceRangeFamily.LCpu,
                [0x48C1] = SlmpDeviceRangeFamily.LCpu,
                [0x48C2] = SlmpDeviceRangeFamily.LCpu,
                [0x48C3] = SlmpDeviceRangeFamily.LCpu,
                [0x0641] = SlmpDeviceRangeFamily.LCpu,
                [0x48A0] = SlmpDeviceRangeFamily.IqR,
                [0x48A1] = SlmpDeviceRangeFamily.IqR,
                [0x48A2] = SlmpDeviceRangeFamily.IqR,
                [0x4800] = SlmpDeviceRangeFamily.IqR,
                [0x4801] = SlmpDeviceRangeFamily.IqR,
                [0x4802] = SlmpDeviceRangeFamily.IqR,
                [0x4803] = SlmpDeviceRangeFamily.IqR,
                [0x4804] = SlmpDeviceRangeFamily.IqR,
                [0x4805] = SlmpDeviceRangeFamily.IqR,
                [0x4806] = SlmpDeviceRangeFamily.IqR,
                [0x4807] = SlmpDeviceRangeFamily.IqR,
                [0x4808] = SlmpDeviceRangeFamily.IqR,
                [0x4809] = SlmpDeviceRangeFamily.IqR,
                [0x4841] = SlmpDeviceRangeFamily.IqR,
                [0x4842] = SlmpDeviceRangeFamily.IqR,
                [0x4843] = SlmpDeviceRangeFamily.IqR,
                [0x4844] = SlmpDeviceRangeFamily.IqR,
                [0x4851] = SlmpDeviceRangeFamily.IqR,
                [0x4852] = SlmpDeviceRangeFamily.IqR,
                [0x4853] = SlmpDeviceRangeFamily.IqR,
                [0x4854] = SlmpDeviceRangeFamily.IqR,
                [0x4891] = SlmpDeviceRangeFamily.IqR,
                [0x4892] = SlmpDeviceRangeFamily.IqR,
                [0x4893] = SlmpDeviceRangeFamily.IqR,
                [0x4894] = SlmpDeviceRangeFamily.IqR,
                [0x4820] = SlmpDeviceRangeFamily.IqR,
                [0x4E01] = SlmpDeviceRangeFamily.IqR,
                [0x4860] = SlmpDeviceRangeFamily.IqR,
                [0x4861] = SlmpDeviceRangeFamily.IqR,
                [0x4862] = SlmpDeviceRangeFamily.IqR,
                [0x0642] = SlmpDeviceRangeFamily.IqR,
                [0x48E9] = SlmpDeviceRangeFamily.MxR,
                [0x48EA] = SlmpDeviceRangeFamily.MxR,
                [0x48EB] = SlmpDeviceRangeFamily.MxR,
                [0x48EE] = SlmpDeviceRangeFamily.MxR,
                [0x48EF] = SlmpDeviceRangeFamily.MxR,
                [0x4A21] = SlmpDeviceRangeFamily.IqF,
                [0x4A23] = SlmpDeviceRangeFamily.IqF,
                [0x4A24] = SlmpDeviceRangeFamily.IqF,
                [0x4A29] = SlmpDeviceRangeFamily.IqF,
                [0x4A2B] = SlmpDeviceRangeFamily.IqF,
                [0x4A2C] = SlmpDeviceRangeFamily.IqF,
                [0x4A31] = SlmpDeviceRangeFamily.IqF,
                [0x4A33] = SlmpDeviceRangeFamily.IqF,
                [0x4A34] = SlmpDeviceRangeFamily.IqF,
                [0x4A41] = SlmpDeviceRangeFamily.IqF,
                [0x4A43] = SlmpDeviceRangeFamily.IqF,
                [0x4A44] = SlmpDeviceRangeFamily.IqF,
                [0x4A49] = SlmpDeviceRangeFamily.IqF,
                [0x4A4B] = SlmpDeviceRangeFamily.IqF,
                [0x4A4C] = SlmpDeviceRangeFamily.IqF,
                [0x4A51] = SlmpDeviceRangeFamily.IqF,
                [0x4A53] = SlmpDeviceRangeFamily.IqF,
                [0x4A54] = SlmpDeviceRangeFamily.IqF,
                [0x4A91] = SlmpDeviceRangeFamily.IqF,
                [0x4A92] = SlmpDeviceRangeFamily.IqF,
                [0x4A93] = SlmpDeviceRangeFamily.IqF,
                [0x4A99] = SlmpDeviceRangeFamily.IqF,
                [0x4A9A] = SlmpDeviceRangeFamily.IqF,
                [0x4A9B] = SlmpDeviceRangeFamily.IqF,
                [0x4AA9] = SlmpDeviceRangeFamily.IqF,
                [0x4AB1] = SlmpDeviceRangeFamily.IqF,
                [0x4AB9] = SlmpDeviceRangeFamily.IqF,
                [0x4B0D] = SlmpDeviceRangeFamily.IqF,
                [0x4B0E] = SlmpDeviceRangeFamily.IqF,
                [0x4B0F] = SlmpDeviceRangeFamily.IqF,
                [0x4B14] = SlmpDeviceRangeFamily.IqF,
                [0x4B15] = SlmpDeviceRangeFamily.IqF,
                [0x4B16] = SlmpDeviceRangeFamily.IqF,
                [0x4B1B] = SlmpDeviceRangeFamily.IqF,
                [0x4B1C] = SlmpDeviceRangeFamily.IqF,
                [0x4B1D] = SlmpDeviceRangeFamily.IqF,
                [0x4B4E] = SlmpDeviceRangeFamily.IqF,
                [0x4B4F] = SlmpDeviceRangeFamily.IqF,
                [0x4B50] = SlmpDeviceRangeFamily.IqF,
                [0x4B51] = SlmpDeviceRangeFamily.IqF,
                [0x4B55] = SlmpDeviceRangeFamily.IqF,
                [0x4B56] = SlmpDeviceRangeFamily.IqF,
                [0x4B57] = SlmpDeviceRangeFamily.IqF,
                [0x4B58] = SlmpDeviceRangeFamily.IqF,
                [0x4B5C] = SlmpDeviceRangeFamily.IqF,
                [0x4B5D] = SlmpDeviceRangeFamily.IqF,
                [0x4B5E] = SlmpDeviceRangeFamily.IqF,
                [0x4B5F] = SlmpDeviceRangeFamily.IqF,
            });

    private static readonly (string Prefix, SlmpDeviceRangeFamily Family)[] ModelPrefixes =
    [
        ("Q04UDPV", SlmpDeviceRangeFamily.QnUDV),
        ("Q06UDPV", SlmpDeviceRangeFamily.QnUDV),
        ("Q13UDPV", SlmpDeviceRangeFamily.QnUDV),
        ("Q26UDPV", SlmpDeviceRangeFamily.QnUDV),
        ("Q03UDV", SlmpDeviceRangeFamily.QnUDV),
        ("Q04UDV", SlmpDeviceRangeFamily.QnUDV),
        ("Q06UDV", SlmpDeviceRangeFamily.QnUDV),
        ("Q13UDV", SlmpDeviceRangeFamily.QnUDV),
        ("Q26UDV", SlmpDeviceRangeFamily.QnUDV),
        ("Q00UJ", SlmpDeviceRangeFamily.QnU),
        ("Q00U", SlmpDeviceRangeFamily.QnU),
        ("Q01U", SlmpDeviceRangeFamily.QnU),
        ("Q02U", SlmpDeviceRangeFamily.QnU),
        ("Q03UD", SlmpDeviceRangeFamily.QnU),
        ("Q04UD", SlmpDeviceRangeFamily.QnU),
        ("Q06UD", SlmpDeviceRangeFamily.QnU),
        ("Q10UD", SlmpDeviceRangeFamily.QnU),
        ("Q13UD", SlmpDeviceRangeFamily.QnU),
        ("Q20UD", SlmpDeviceRangeFamily.QnU),
        ("Q26UD", SlmpDeviceRangeFamily.QnU),
        ("Q50UDEH", SlmpDeviceRangeFamily.QnU),
        ("Q100UDEH", SlmpDeviceRangeFamily.QnU),
        ("FX5UC", SlmpDeviceRangeFamily.IqF),
        ("FX5UJ", SlmpDeviceRangeFamily.IqF),
        ("FX5U", SlmpDeviceRangeFamily.IqF),
        ("FX5S", SlmpDeviceRangeFamily.IqF),
        ("MXF100-", SlmpDeviceRangeFamily.MxF),
        ("MXF", SlmpDeviceRangeFamily.MxF),
        ("MXR", SlmpDeviceRangeFamily.MxR),
        ("LJ72GF15-T2", SlmpDeviceRangeFamily.LCpu),
        ("L02SCPU", SlmpDeviceRangeFamily.LCpu),
        ("L02CPU", SlmpDeviceRangeFamily.LCpu),
        ("L06CPU", SlmpDeviceRangeFamily.LCpu),
        ("L26CPU", SlmpDeviceRangeFamily.LCpu),
        ("L04HCPU", SlmpDeviceRangeFamily.LCpu),
        ("L08HCPU", SlmpDeviceRangeFamily.LCpu),
        ("L16HCPU", SlmpDeviceRangeFamily.LCpu),
        ("L32HCPU", SlmpDeviceRangeFamily.LCpu),
        ("RJ72GF15-T2", SlmpDeviceRangeFamily.IqR),
        ("NZ2GF-ETB", SlmpDeviceRangeFamily.IqR),
        ("MI5122-VW", SlmpDeviceRangeFamily.IqR),
        ("QS001CPU", SlmpDeviceRangeFamily.QCpu),
        ("Q00JCPU", SlmpDeviceRangeFamily.QCpu),
        ("Q00CPU", SlmpDeviceRangeFamily.QCpu),
        ("Q01CPU", SlmpDeviceRangeFamily.QCpu),
        ("Q02", SlmpDeviceRangeFamily.QCpu),
        ("Q06", SlmpDeviceRangeFamily.QCpu),
        ("Q12", SlmpDeviceRangeFamily.QCpu),
        ("Q25", SlmpDeviceRangeFamily.QCpu),
        ("R", SlmpDeviceRangeFamily.IqR),
    ];

    private static readonly ReadOnlyDictionary<SlmpDeviceRangeFamily, SlmpDeviceRangeProfile> Profiles =
        new ReadOnlyDictionary<SlmpDeviceRangeFamily, SlmpDeviceRangeProfile>(
            new Dictionary<SlmpDeviceRangeFamily, SlmpDeviceRangeProfile>
            {
                [SlmpDeviceRangeFamily.IqR] = CreateProfile(
                    SlmpDeviceRangeFamily.IqR,
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
                [SlmpDeviceRangeFamily.MxF] = CreateProfile(
                    SlmpDeviceRangeFamily.MxF,
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
                    ("S", Unsupported("Not supported on MX-F.")),
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
                [SlmpDeviceRangeFamily.MxR] = CreateProfile(
                    SlmpDeviceRangeFamily.MxR,
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
                    ("S", Unsupported("Not supported on MX-R.")),
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
                [SlmpDeviceRangeFamily.IqF] = CreateProfile(
                    SlmpDeviceRangeFamily.IqF,
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
                    ("S", Unsupported("Not supported on iQ-F.")),
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
                [SlmpDeviceRangeFamily.QCpu] = CreateProfile(
                    SlmpDeviceRangeFamily.QCpu,
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
                [SlmpDeviceRangeFamily.LCpu] = CreateProfile(
                    SlmpDeviceRangeFamily.LCpu,
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
                    ("Z", WordRegister(305, "SD305", "Requires ZZ = FFFFh for the reported upper bound.")),
                    ("LZ", Unsupported("Not supported on LCPU.")),
                    ("ZR", DWordRegister(306, "SD306-SD307 (32-bit)")),
                    ("RD", Unsupported("Not supported on LCPU.")),
                    ("SM", Fixed(2048, "Fixed family limit")),
                    ("SD", Fixed(2048, "Fixed family limit"))),
                [SlmpDeviceRangeFamily.QnU] = CreateProfile(
                    SlmpDeviceRangeFamily.QnU,
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
                    ("Z", WordRegister(305, "SD305", "Requires ZZ = FFFFh for the reported upper bound.")),
                    ("LZ", Unsupported("Not supported on QnU.")),
                    ("ZR", DWordRegister(306, "SD306-SD307 (32-bit)")),
                    ("RD", Unsupported("Not supported on QnU.")),
                    ("SM", Fixed(2048, "Fixed family limit")),
                    ("SD", Fixed(2048, "Fixed family limit"))),
                [SlmpDeviceRangeFamily.QnUDV] = CreateProfile(
                    SlmpDeviceRangeFamily.QnUDV,
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
                    ("Z", WordRegister(305, "SD305", "Requires ZZ = FFFFh for the reported upper bound.")),
                    ("LZ", Unsupported("Not supported on QnUDV.")),
                    ("ZR", DWordRegister(306, "SD306-SD307 (32-bit)")),
                    ("RD", Unsupported("Not supported on QnUDV.")),
                    ("SM", Fixed(2048, "Fixed family limit")),
                    ("SD", Fixed(2048, "Fixed family limit"))),
            });

    public static string NormalizeModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return string.Empty;
        }

        return model.Trim().TrimEnd('\0').Trim().ToUpperInvariant();
    }

    public static SlmpDeviceRangeFamily ResolveFamily(SlmpTypeNameInfo typeInfo)
    {
        if (typeInfo.HasModelCode && ModelCodeFamilies.TryGetValue(typeInfo.ModelCode, out var familyFromCode))
        {
            return familyFromCode;
        }

        var normalizedModel = NormalizeModel(typeInfo.Model);
        foreach (var (prefix, family) in ModelPrefixes)
        {
            if (normalizedModel.StartsWith(prefix, StringComparison.Ordinal))
            {
                return family;
            }
        }

        var codeText = typeInfo.HasModelCode ? $"0x{typeInfo.ModelCode:X4}" : "none";
        throw new SlmpError($"Unsupported PLC model for device-range rules: model='{normalizedModel}', model_code={codeText}.");
    }

    public static SlmpDeviceRangeProfile ResolveProfile(SlmpTypeNameInfo typeInfo)
    {
        return Profiles[ResolveFamily(typeInfo)];
    }

    public static SlmpDeviceRangeProfile ResolveProfile(SlmpDeviceRangeFamily family)
    {
        return Profiles[family];
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

    public static SlmpDeviceRangeCatalog BuildCatalog(
        SlmpTypeNameInfo typeInfo,
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
                var notation = ResolveNotation(profile.Family, device, row.Notation);
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
            NormalizeModel(typeInfo.Model),
            typeInfo.ModelCode,
            typeInfo.HasModelCode,
            profile.Family,
            entries);
    }

    public static SlmpDeviceRangeCatalog BuildCatalog(
        SlmpDeviceRangeFamily family,
        IReadOnlyDictionary<int, ushort> registers)
    {
        return BuildCatalog(
            new SlmpTypeNameInfo(GetFamilyLabel(family), 0, false),
            ResolveProfile(family),
            registers);
    }

    public static string GetFamilyLabel(SlmpDeviceRangeFamily family)
    {
        return family switch
        {
            SlmpDeviceRangeFamily.IqR => "IQ-R",
            SlmpDeviceRangeFamily.MxF => "MX-F",
            SlmpDeviceRangeFamily.MxR => "MX-R",
            SlmpDeviceRangeFamily.IqF => "IQ-F",
            SlmpDeviceRangeFamily.QCpu => "QCPU",
            SlmpDeviceRangeFamily.LCpu => "LCPU",
            SlmpDeviceRangeFamily.QnU => "QnU",
            SlmpDeviceRangeFamily.QnUDV => "QnUDV",
            _ => family.ToString(),
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
        SlmpDeviceRangeFamily family,
        string device,
        SlmpDeviceRangeNotation defaultNotation)
    {
        if (family == SlmpDeviceRangeFamily.IqF &&
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
        SlmpDeviceRangeFamily family,
        int registerStart,
        int registerCount,
        params (string Item, SlmpRangeValueSpec Spec)[] rules)
    {
        var map = new Dictionary<string, SlmpRangeValueSpec>(StringComparer.Ordinal);
        foreach (var (item, spec) in rules)
        {
            map[item] = spec;
        }

        return new SlmpDeviceRangeProfile(family, registerStart, registerCount, new ReadOnlyDictionary<string, SlmpRangeValueSpec>(map));
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
