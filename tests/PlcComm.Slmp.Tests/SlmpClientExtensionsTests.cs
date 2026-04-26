using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpClientExtensionsTests
{
    [Fact]
    public async Task ReadWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadWordsSingleRequestAsync("D0", 961));
    }

    [Fact]
    public async Task ReadDWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadDWordsSingleRequestAsync("D0", 481));
    }

    [Fact]
    public async Task WriteWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1");
        ushort[] values = Enumerable.Repeat((ushort)1, 961).ToArray();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteWordsSingleRequestAsync("D0", values));
    }

    [Fact]
    public async Task WriteDWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1");
        uint[] values = Enumerable.Repeat((uint)1, 481).ToArray();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteDWordsSingleRequestAsync("D0", values));
    }

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
            "D",
            SlmpClientExtensions.ResolveDTypeForAddress("LZ0", new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0), "U", null));
        Assert.Equal(
            "BIT",
            SlmpClientExtensions.ResolveDTypeForAddress("LTS10", new SlmpDeviceAddress(SlmpDeviceCode.LTS, 10), "U", null));
    }

    [Fact]
    public async Task ReadTypedAsync_RejectsWordDTypeForLongCurrentValues()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadTypedAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 10), "U"));
        Assert.Contains("32-bit long current value", ex.Message);
    }

    [Fact]
    public async Task ReadTypedAsync_RejectsWordDTypeForLz()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadTypedAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 10), "U"));
        Assert.Contains("32-bit device", ex.Message);
    }

    [Fact]
    public void CompileReadPlan_RejectsWordDTypeForLz()
    {
        var ex = Assert.Throws<ArgumentException>(() => SlmpClientExtensions.CompileReadPlan(["LZ10:U"]));
        Assert.Contains("32-bit device", ex.Message);
    }

    [Fact]
    public async Task ReadDWordsAsync_LzUsesRandomDwordLimit()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadDWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0), 256, maxDwordsPerRequest: 255));
        Assert.Contains("count 256 exceeds maxDwordsPerRequest 255", ex.Message);
    }

    [Fact]
    public async Task WriteTypedAsync_RejectsWordDTypeForLongCurrentValues()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteTypedAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10), "S", (short)1));
        Assert.Contains("32-bit long current value", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTN, "D", (int)SlmpNamedWriteRoute.RandomDWords)]
    [InlineData(SlmpDeviceCode.LSTN, "L", (int)SlmpNamedWriteRoute.RandomDWords)]
    [InlineData(SlmpDeviceCode.LZ, "D", (int)SlmpNamedWriteRoute.RandomDWords)]
    [InlineData(SlmpDeviceCode.LCN, "D", (int)SlmpNamedWriteRoute.RandomDWords)]
    [InlineData(SlmpDeviceCode.LTC, "BIT", (int)SlmpNamedWriteRoute.RandomBits)]
    [InlineData(SlmpDeviceCode.LTS, "BIT", (int)SlmpNamedWriteRoute.RandomBits)]
    [InlineData(SlmpDeviceCode.LSTC, "BIT", (int)SlmpNamedWriteRoute.RandomBits)]
    [InlineData(SlmpDeviceCode.LSTS, "BIT", (int)SlmpNamedWriteRoute.RandomBits)]
    [InlineData(SlmpDeviceCode.LCS, "BIT", (int)SlmpNamedWriteRoute.RandomBits)]
    [InlineData(SlmpDeviceCode.LCC, "BIT", (int)SlmpNamedWriteRoute.RandomBits)]
    [InlineData(SlmpDeviceCode.D, "D", (int)SlmpNamedWriteRoute.ContiguousDWords)]
    public void ResolveWriteRoute_UsesLongFamilySpecialCases(
        SlmpDeviceCode code,
        string dtype,
        int expected)
    {
        var route = SlmpClientExtensions.ResolveWriteRoute(new SlmpDeviceAddress(code, 10), dtype);
        Assert.Equal((SlmpNamedWriteRoute)expected, route);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTN, "U")]
    [InlineData(SlmpDeviceCode.LSTN, "S")]
    [InlineData(SlmpDeviceCode.LCN, "F")]
    public void ResolveWriteRoute_RejectsInvalidLongCurrentDTypes(SlmpDeviceCode code, string dtype)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SlmpClientExtensions.ResolveWriteRoute(new SlmpDeviceAddress(code, 10), dtype));
        Assert.Contains("32-bit long current value", ex.Message);
    }

    [Theory]
    [InlineData("U")]
    [InlineData("S")]
    [InlineData("F")]
    [InlineData("BIT")]
    public void ResolveWriteRoute_RejectsInvalidLzDTypes(string dtype)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SlmpClientExtensions.ResolveWriteRoute(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 10), dtype));
        Assert.Contains("32-bit device", ex.Message);
    }
}
