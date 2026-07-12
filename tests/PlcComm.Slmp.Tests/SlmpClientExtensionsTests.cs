using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpClientExtensionsTests
{
    [Fact]
    public async Task ReadWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadWordsSingleRequestAsync("D0", 961));
    }

    [Fact]
    public async Task ReadDWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadDWordsSingleRequestAsync("D0", 481));
    }

    [Fact]
    public async Task WriteWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        ushort[] values = Enumerable.Repeat((ushort)1, 961).ToArray();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteWordsSingleRequestAsync("D0", values));
    }

    [Fact]
    public async Task WriteDWordsSingleRequestAsync_RejectsOversizedRangeBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        uint[] values = Enumerable.Repeat((uint)1, 481).ToArray();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteDWordsSingleRequestAsync("D0", values));
    }

    [Fact]
    public async Task ReadNamedAsync_RejectsMoreThanOneRandomRequestBeforeTransport()
    {
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);
        var addresses = Enumerable.Range(0, 256).Select(index => $"D{index}:U").ToArray();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadNamedAsync(addresses));

        Assert.Contains("one random-read request", error.Message, StringComparison.Ordinal);
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void CompileReadPlan_BatchesWordDwordAndBitInWord()
    {
        var plan = SlmpClientExtensions.CompileReadPlan(["D100:U", "D100.3", "D101:F", "M10:BIT"], SlmpPlcProfile.IqR);

        Assert.Equal(2, plan.WordDevices.Count);
        Assert.Single(plan.DwordDevices);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 100, SlmpPlcProfile.IqR), plan.WordDevices[0]);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.M, 0, SlmpPlcProfile.IqR), plan.WordDevices[1]);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 101, SlmpPlcProfile.IqR), plan.DwordDevices[0]);

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
                Assert.Equal(SlmpNamedReadKind.BitInWord, entry.Kind);
                Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.M, 0, SlmpPlcProfile.IqR), entry.Device);
                Assert.Equal("BIT_IN_WORD", entry.DType);
                Assert.Equal(10, entry.BitIndex);
            });
    }

    [Fact]
    public void CompileReadPlan_RejectsDirectReadFallbackRoutes()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SlmpClientExtensions.CompileReadPlan(["TS10:BIT", "DX10:BIT"], SlmpPlcProfile.IqR));
        Assert.Contains("one random-read request", ex.Message);
    }

    [Fact]
    public void CompileReadPlan_DeduplicatesRepeatedBatchableDevices()
    {
        var plan = SlmpClientExtensions.CompileReadPlan(["D0:U", "D0:S", "D0.0", "D1:F", "D1:L"], SlmpPlcProfile.IqR);

        Assert.Single(plan.WordDevices);
        Assert.Single(plan.DwordDevices);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR), plan.WordDevices[0]);
        Assert.Equal(new SlmpDeviceAddress(SlmpDeviceCode.D, 1, SlmpPlcProfile.IqR), plan.DwordDevices[0]);
    }

    [Fact]
    public void CompileReadPlan_RejectsLongTimerHelperRoutes()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SlmpClientExtensions.CompileReadPlan(["LTN10:D", "LTS10:BIT", "LCN30:D"], SlmpPlcProfile.IqR));
        Assert.Contains("one random-read request", ex.Message);
    }

    [Fact]
    public void CompileReadPlan_BitDeviceBitSuffixThrows()
    {
        var ex = Assert.Throws<ArgumentException>(() => SlmpClientExtensions.CompileReadPlan(["M100.0"], SlmpPlcProfile.IqR));
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
            SlmpClientExtensions.ResolveDTypeForAddress("LTN10:D", new SlmpDeviceAddress(SlmpDeviceCode.LTN, 10, SlmpPlcProfile.IqR), "D", null));
        Assert.Equal(
            "BIT",
            SlmpClientExtensions.ResolveDTypeForAddress("LTS10:BIT", new SlmpDeviceAddress(SlmpDeviceCode.LTS, 10, SlmpPlcProfile.IqR), "BIT", null));
    }

    [Fact]
    public async Task ReadTypedAsync_RejectsWordDTypeForLongCurrentValues()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadTypedAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 10, SlmpPlcProfile.IqR), "U"));
        Assert.Contains("32-bit long current value", ex.Message);
    }

    [Fact]
    public async Task ReadTypedAsync_RejectsWordDTypeForLz()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadTypedAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 10, SlmpPlcProfile.IqR), "U"));
        Assert.Contains("32-bit device", ex.Message);
    }

    [Fact]
    public void CompileReadPlan_RejectsWordDTypeForLz()
    {
        var ex = Assert.Throws<ArgumentException>(() => SlmpClientExtensions.CompileReadPlan(["LZ10:U"], SlmpPlcProfile.IqR));
        Assert.Contains("32-bit device", ex.Message);
    }

    [Fact]
    public async Task ReadDWordsSingleRequestAsync_RejectsMoreThanOneRequestCanHold()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadDWordsSingleRequestAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0, SlmpPlcProfile.IqR), 481));
        Assert.Contains("1-480", ex.Message);
    }

    [Fact]
    public async Task WriteTypedAsync_RejectsWordDTypeForLongCurrentValues()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteTypedAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10, SlmpPlcProfile.IqR), "S", (short)1));
        Assert.Contains("32-bit long current value", ex.Message);
    }

    [Fact]
    public async Task WriteTypedAsync_SignedWordNegative_DoesNotOverflowBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Record.ExceptionAsync(
            () => client.WriteTypedAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR), "S", (short)-1));

        Assert.NotNull(ex);
        Assert.IsNotType<OverflowException>(ex);
    }

    public static TheoryData<string, object> InvalidTypedWriteValues => new()
    {
        { "U", true },
        { "U", 1.0 },
        { "U", "1" },
        { "U", -1 },
        { "U", 65536 },
        { "S", 32768 },
        { "D", -1L },
        { "D", 4_294_967_296UL },
        { "L", 2_147_483_648L },
        { "F", double.PositiveInfinity },
        { "F", double.MaxValue },
        { "BIT", 1 },
    };

    [Theory]
    [MemberData(nameof(InvalidTypedWriteValues))]
    public async Task WriteTypedAsync_RejectsCoercionAndOutOfRangeBeforeTransport(string dtype, object value)
    {
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);
        var code = dtype == "BIT" ? SlmpDeviceCode.M : SlmpDeviceCode.D;

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => client.WriteTypedAsync(new SlmpDeviceAddress(code, 0, SlmpPlcProfile.IqR), dtype, value));

        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task WriteNamedAsync_BitInWordRequiresBooleanBeforeTransport()
    {
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteNamedAsync(new Dictionary<string, object> { ["D0.1"] = 1 }));

        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task WriteNamedAsync_RejectsHiddenMultiRequestRoutesBeforeTransport()
    {
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteNamedAsync(new Dictionary<string, object>
            {
                ["D0:U"] = (ushort)1,
                ["M0:BIT"] = true,
            }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteNamedAsync(new Dictionary<string, object> { ["D0.1"] = true }));

        Assert.False(client.IsOpen);
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
        var route = SlmpClientExtensions.ResolveWriteRoute(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), dtype);
        Assert.Equal((SlmpNamedWriteRoute)expected, route);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTN, "U")]
    [InlineData(SlmpDeviceCode.LSTN, "S")]
    [InlineData(SlmpDeviceCode.LCN, "F")]
    public void ResolveWriteRoute_RejectsInvalidLongCurrentDTypes(SlmpDeviceCode code, string dtype)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SlmpClientExtensions.ResolveWriteRoute(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), dtype));
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
            () => SlmpClientExtensions.ResolveWriteRoute(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 10, SlmpPlcProfile.IqR), dtype));
        Assert.Contains("32-bit device", ex.Message);
    }

    [Fact]
    public void ResolveWriteRoute_IqRRejectsStepRelayWrites()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SlmpClientExtensions.ResolveWriteRoute(new SlmpDeviceAddress(SlmpDeviceCode.S, 10, SlmpPlcProfile.IqR), "BIT", SlmpPlcProfile.IqR));
        Assert.Contains("S is read-only for PLC profile", ex.Message);
    }

    [Fact]
    public void ResolveWriteRoute_IqFAllowsStepRelayWrites()
    {
        var route = SlmpClientExtensions.ResolveWriteRoute(new SlmpDeviceAddress(SlmpDeviceCode.S, 10, SlmpPlcProfile.IqR), "BIT", SlmpPlcProfile.IqF);
        Assert.Equal(SlmpNamedWriteRoute.ContiguousBits, route);
    }
}
