using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpClientExtensionsTests
{
    [Fact]
    public async Task ReadWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadWordsSingleRequestAsync("D0", 961));
    }

    [Fact]
    public async Task ReadDWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadDWordsSingleRequestAsync("D0", 481));
    }

    [Fact]
    public async Task WriteWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);
        ushort[] values = Enumerable.Repeat((ushort)1, 961).ToArray();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteWordsSingleRequestAsync("D0", values));
    }

    [Fact]
    public async Task WriteDWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);
        uint[] values = Enumerable.Repeat((uint)1, 481).ToArray();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteDWordsSingleRequestAsync("D0", values));
    }

    [Fact]
    public void CompileReadPlan_BatchesWordDwordAndBitInWord()
    {
        var plan = SlmpClientExtensions.CompileReadPlan(["D100:U", "D100.3", "D101:F", "M10:BIT"]);

        Assert.Single(plan.WordDevices);
        Assert.Single(plan.DwordDevices);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 100), plan.WordDevices[0]);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 101), plan.DwordDevices[0]);

        Assert.Collection(
            plan.Entries,
            entry =>
            {
                Assert.Equal("D100:U", entry.Address);
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
                Assert.Equal("M10:BIT", entry.Address);
                Assert.Equal(SlmpNamedReadKind.Fallback, entry.Kind);
                Assert.Equal("BIT", entry.DType);
            });
    }

    [Fact]
    public void CompileReadPlan_DeduplicatesRepeatedBatchableDevices()
    {
        var plan = SlmpClientExtensions.CompileReadPlan(["D0:U", "D0:S", "D0.0", "D1:F", "D1:L"]);

        Assert.Single(plan.WordDevices);
        Assert.Single(plan.DwordDevices);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 0), plan.WordDevices[0]);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 1), plan.DwordDevices[0]);
    }

    [Fact]
    public void CompileReadPlan_UsesHelperKindsForLongTimerFamilies()
    {
        var plan = SlmpClientExtensions.CompileReadPlan(
            ["LTN10:D", "LTS10:BIT", "LTC10:BIT", "LSTN20:D", "LSTS20:BIT", "LSTC20:BIT", "LCN30:D", "LCS30:BIT", "LCC30:BIT"]);

        Assert.Empty(plan.WordDevices);
        Assert.Empty(plan.DwordDevices);

        Assert.Collection(
            plan.Entries,
            entry =>
            {
                Assert.Equal("LTN10:D", entry.Address);
                Assert.Equal("D", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LTN, SlmpLongTimerReadKind.Current), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LTS10:BIT", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LTN, SlmpLongTimerReadKind.Contact), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LTC10:BIT", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LTN, SlmpLongTimerReadKind.Coil), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LSTN20:D", entry.Address);
                Assert.Equal("D", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LSTN, SlmpLongTimerReadKind.Current), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LSTS20:BIT", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LSTN, SlmpLongTimerReadKind.Contact), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LSTC20:BIT", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LSTN, SlmpLongTimerReadKind.Coil), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LCN30:D", entry.Address);
                Assert.Equal("D", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LCN, SlmpLongTimerReadKind.Current), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LCS30:BIT", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LCS, SlmpLongTimerReadKind.Contact), entry.LongTimerRead);
            },
            entry =>
            {
                Assert.Equal("LCC30:BIT", entry.Address);
                Assert.Equal("BIT", entry.DType);
                Assert.Equal(SlmpNamedReadKind.LongTimer, entry.Kind);
                Assert.Equal(new SlmpLongTimerReadSpec(SlmpDeviceCode.LCC, SlmpLongTimerReadKind.Coil), entry.LongTimerRead);
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

        var bitD = SlmpClientExtensions.ParseAddress("D100.D");
        Assert.Equal("D100", bitD.Base);
        Assert.Equal("BIT_IN_WORD", bitD.DType);
        Assert.Equal(13, bitD.BitIdx);
    }

    [Fact]
    public void ParseAddress_InvalidBitInWordIndexThrows()
    {
        Assert.Throws<ArgumentException>(() => SlmpClientExtensions.ParseAddress("D100.10"));
    }

    [Fact]
    public void ParseAddress_BitInWordDTypeWithoutIndexThrows()
    {
        var ex = Assert.Throws<ArgumentException>(() => SlmpClientExtensions.ParseAddress("D100:BIT_IN_WORD"));
        Assert.Contains("no bit index", ex.Message);
    }

    [Fact]
    public void ParseAddress_RejectsMissingDType()
    {
        var ex = Assert.Throws<ArgumentException>(() => SlmpClientExtensions.ParseAddress("D100"));
        Assert.Contains("requires an explicit dtype", ex.Message);
    }

    [Fact]
    public void ResolveDTypeForAddress_RequiresExplicitDType()
    {
        Assert.Equal(
            "D",
            SlmpClientExtensions.ResolveDTypeForAddress("LTN10:D", new SlmpDeviceAddress(SlmpDeviceCode.LTN, 10), "D", null));
        Assert.Equal(
            "BIT",
            SlmpClientExtensions.ResolveDTypeForAddress("LTS10:BIT", new SlmpDeviceAddress(SlmpDeviceCode.LTS, 10), "BIT", null));
    }

    [Fact]
    public async Task ReadTypedAsync_RejectsWordDTypeForLongCurrentValues()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadTypedAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 10), "U"));
        Assert.Contains("32-bit long current value", ex.Message);
    }

    [Fact]
    public async Task ReadTypedAsync_RejectsWordDTypeForLz()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);
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
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadDWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0), 256, maxDwordsPerRequest: 255));
        Assert.Contains("count 256 exceeds maxDwordsPerRequest 255", ex.Message);
    }

    [Fact]
    public async Task WriteTypedAsync_RejectsWordDTypeForLongCurrentValues()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteTypedAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10), "S", (short)1));
        Assert.Contains("32-bit long current value", ex.Message);
    }

    [Fact]
    public async Task WriteTypedAsync_SignedWordNegative_DoesNotOverflowBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);
        var ex = await Record.ExceptionAsync(
            () => client.WriteTypedAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 0), "S", (short)-1));

        Assert.NotNull(ex);
        Assert.IsNotType<OverflowException>(ex);
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

    [Fact]
    public void ResolveWriteRoute_RejectsStepRelayWrites()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SlmpClientExtensions.ResolveWriteRoute(new SlmpDeviceAddress(SlmpDeviceCode.S, 10), "BIT"));
        Assert.Contains("S is read-only in SLMP", ex.Message);
    }
}
