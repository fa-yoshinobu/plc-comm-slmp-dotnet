using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpClientExtensionsTests
{
    [Fact]
    public void CompileReadPlan_BatchesWordDwordAndBitInWord()
    {
        var plan = SlmpClientExtensions.CompileReadPlan(["D100", "D100.3", "D101:F", "M10"]);

        Assert.Single(plan.WordDevices);
        Assert.Single(plan.DwordDevices);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 100), plan.WordDevices[0]);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 101), plan.DwordDevices[0]);

        Assert.Collection(
            plan.Entries,
            entry =>
            {
                Assert.Equal("D100", entry.Address);
                Assert.Equal(SlmpNamedReadKind.Word, entry.Kind);
            },
            entry =>
            {
                Assert.Equal("D100.3", entry.Address);
                Assert.Equal(SlmpNamedReadKind.BitInWord, entry.Kind);
                Assert.Equal(3, entry.BitIndex);
            },
            entry =>
            {
                Assert.Equal("D101:F", entry.Address);
                Assert.Equal(SlmpNamedReadKind.Dword, entry.Kind);
            },
            entry =>
            {
                Assert.Equal("M10", entry.Address);
                Assert.Equal(SlmpNamedReadKind.Fallback, entry.Kind);
                Assert.Equal("BIT", entry.DType);
            });
    }

    [Fact]
    public void CompileReadPlan_DeduplicatesRepeatedBatchableDevices()
    {
        var plan = SlmpClientExtensions.CompileReadPlan(["D0", "D0:S", "D0.0", "D1:F", "D1:L"]);

        Assert.Single(plan.WordDevices);
        Assert.Single(plan.DwordDevices);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 0), plan.WordDevices[0]);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 1), plan.DwordDevices[0]);
    }

    [Fact]
    public void CompileReadPlan_UsesHelperKindsForLongTimerFamilies()
    {
        var plan = SlmpClientExtensions.CompileReadPlan(["LTN10", "LTS10", "LTC10", "LSTN20", "LSTS20", "LSTC20", "LCN30", "LCS30", "LCC30"]);

        Assert.Empty(plan.WordDevices);
        Assert.Empty(plan.DwordDevices);

        Assert.Collection(
            plan.Entries,
            entry =>
            {
                Assert.Equal("LTN10", entry.Address);
                Assert.Equal("D", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LTN, SlmpLongTimerReadKind.Current), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LTS10", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LTN, SlmpLongTimerReadKind.Contact), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LTC10", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LTN, SlmpLongTimerReadKind.Coil), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LSTN20", entry.Address);
                Assert.Equal("D", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LSTN, SlmpLongTimerReadKind.Current), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LSTS20", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LSTN, SlmpLongTimerReadKind.Contact), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LSTC20", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LSTN, SlmpLongTimerReadKind.Coil), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LCN30", entry.Address);
                Assert.Equal("D", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LCN, SlmpLongTimerReadKind.Current), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LCS30", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LCN, SlmpLongTimerReadKind.Contact), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LCC30", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LCN, SlmpLongTimerReadKind.Coil), entry.LongTimerRead);
            });
    }

    [Fact]
    public void CompileReadPlan_BitDeviceBitSuffixThrows()
    {
        var ex = Assert.Throws<ArgumentException>(() => SlmpClientExtensions.CompileReadPlan(["M100.0"]));
        Assert.Contains("only valid for word devices", ex.Message);
    }

    [Fact]
    public void ParseAddress_ParsesBitInWordHexIndex()
    {
        var parsed = SlmpClientExtensions.ParseAddress("D100.A");
        Assert.Equal("D100", parsed.Base);
        Assert.Equal("BIT_IN_WORD", parsed.DType);
        Assert.Equal(10, parsed.BitIdx);
    }

    [Fact]
    public void ResolveDTypeForAddress_DefaultsLongCurrentsToDword()
    {
        Assert.Equal(
            "D",
            SlmpClientExtensions.ResolveDTypeForAddress("LTN10", new SlmpDeviceAddress(SlmpDeviceCode.LTN, 10), "U", null));
        Assert.Equal(
            "D",
            SlmpClientExtensions.ResolveDTypeForAddress("LSTN20", new SlmpDeviceAddress(SlmpDeviceCode.LSTN, 20), "U", null));
        Assert.Equal(
            "D",
            SlmpClientExtensions.ResolveDTypeForAddress("LCN30", new SlmpDeviceAddress(SlmpDeviceCode.LCN, 30), "U", null));
        Assert.Equal(
            "BIT",
            SlmpClientExtensions.ResolveDTypeForAddress("LTS10", new SlmpDeviceAddress(SlmpDeviceCode.LTS, 10), "U", null));
    }
}
