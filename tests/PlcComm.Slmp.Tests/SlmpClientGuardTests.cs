using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpClientGuardTests
{
    private static readonly bool[] SingleTrue = [true];

    [Fact]
    public async Task ReadWordsRawAsync_RejectsNonBlockLongTimerCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 0, SlmpPlcProfile.IqR), 2));
        Assert.Contains("requires 4-word blocks", ex.Message);
    }

    [Fact]
    public async Task ReadWordsRawAsync_RejectsDirectLongCounterCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 0, SlmpPlcProfile.IqR), 4));
        Assert.Contains("Direct word read is not supported for LCN", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTN)]
    [InlineData(SlmpDeviceCode.LCN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task ReadDWordsRawAsync_RejectsDirectLongCurrentAndLzReads(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadDWordsRawAsync(new SlmpDeviceAddress(code, 0, SlmpPlcProfile.IqR), 1));
        Assert.Contains($"Direct DWord read is not supported for {code}", ex.Message);
    }

    [Fact]
    public async Task ReadWordsRawAsync_RejectsDirectLzWordReads()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0, SlmpPlcProfile.IqR), 1));
        Assert.Contains("Direct word read is not supported for LZ", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.G)]
    [InlineData(SlmpDeviceCode.HG)]
    public async Task ReadWordsRawAsync_RejectsStandaloneQualifiedOnlyDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), 1));
        Assert.Contains($"{code} cannot be accessed as a standalone device", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.G)]
    [InlineData(SlmpDeviceCode.HG)]
    public async Task ReadDWordsRawAsync_RejectsStandaloneQualifiedOnlyDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadDWordsRawAsync(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), 1));
        Assert.Contains($"{code} cannot be accessed as a standalone device", ex.Message);
    }

    [Fact]
    public async Task WriteWordsAsync_RejectsLongCurrentValueDevices()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 0, SlmpPlcProfile.IqR), new ushort[] { 1 }));
        Assert.Contains("Direct word write is not supported for LTN", ex.Message);
    }

    [Fact]
    public async Task WriteDWordsAsync_RejectsDirectLongCurrentWrites()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteDWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 0, SlmpPlcProfile.IqR), new uint[] { 1 }));
        Assert.Contains("Direct DWord write is not supported for LCN", ex.Message);
    }

    [Fact]
    public async Task WriteWordsAsync_RejectsLzWordWrites()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0, SlmpPlcProfile.IqR), new ushort[] { 1 }));
        Assert.Contains("Direct word write is not supported for LZ", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.G)]
    [InlineData(SlmpDeviceCode.HG)]
    public async Task DirectWrites_RejectStandaloneQualifiedOnlyDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        var wordEx = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsAsync(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), new ushort[] { 1 }));
        Assert.Contains($"{code} cannot be accessed as a standalone device", wordEx.Message);

        var dwordEx = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteDWordsAsync(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), new uint[] { 1 }));
        Assert.Contains($"{code} cannot be accessed as a standalone device", dwordEx.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTS)]
    [InlineData(SlmpDeviceCode.LTC)]
    [InlineData(SlmpDeviceCode.LSTS)]
    [InlineData(SlmpDeviceCode.LSTC)]
    [InlineData(SlmpDeviceCode.LCS)]
    [InlineData(SlmpDeviceCode.LCC)]
    public async Task ReadBitsAsync_RejectsDirectLongFamilyStateReads(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBitsAsync(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), 1));
        Assert.Contains($"Direct bit read is not supported for {code}", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LCS)]
    [InlineData(SlmpDeviceCode.LCC)]
    public async Task ReadTypedAsync_LongCounterStatesUseDirectBitReadInsideHelper(SlmpDeviceCode code)
    {
        await using var server = new MultiShotSlmpServer([(0, new byte[] { 0x10 })]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
            MonitoringTimer = 0x0010,
        };

        var value = await client.ReadTypedAsync(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), "BIT");

        Assert.True(Assert.IsType<bool>(value));
        var request = Assert.Single(server.RequestFrames);
        AssertDeviceReadBitShape(request, code, 10, 1);
    }

    [Fact]
    public async Task Frame4E_IgnoresMismatchedSerialResponses()
    {
        await using var server = new SerialSkewSlmpServer(
            staleResponseData: BuildWordPayload(0x1111),
            matchingResponseData: BuildWordPayload(0x2222));
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        var words = await client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR), 1);

        Assert.Equal(new ushort[] { 0x2222 }, words);
        Assert.Single(server.RequestFrames);
    }

    [Fact]
    public async Task TargetAddress_AcceptsModuleIoConstantsInRequestHeader()
    {
        await using var server = new MultiShotSlmpServer([(0, BuildWordPayload(0x1234))]);
        await server.StartAsync();

        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            server.Port,
            SlmpTransportMode.Tcp,
            new SlmpTargetAddress(0x00, 0xFF, SlmpModuleIo.MultipleCpu2, 0x00))
        {
            MonitoringTimer = 0x0010,
        };

        var words = await client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR), 1);

        Assert.Equal(new ushort[] { 0x1234 }, words);
        var request = Assert.Single(server.RequestFrames);
        Assert.Equal(SlmpModuleIo.MultipleCpu2, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(8, 2)));
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTC)]
    [InlineData(SlmpDeviceCode.LCC)]
    public async Task WriteBitsAsync_RejectsDirectLongFamilyStateWrites(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBitsAsync(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), SingleTrue));
        Assert.Contains($"Direct bit write is not supported for {code}", ex.Message);
    }

    [Fact]
    public async Task WriteBitsAsync_RejectsStepRelayWrites()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.S, 10, SlmpPlcProfile.IqR), SingleTrue));
        Assert.Contains("S is read-only for PLC profile", ex.Message);
    }

    [Fact]
    public async Task WriteWordsAsync_RejectsStepRelayWrites()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.S, 10, SlmpPlcProfile.IqR), new ushort[] { 1 }));
        Assert.Contains("S is read-only for PLC profile", ex.Message);
    }

    [Fact]
    public async Task DirectAccess_RejectsManualPointLimitOverruns()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR), 961));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR), new ushort[961]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.M, 0, SlmpPlcProfile.IqR), 7169));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.M, 0, SlmpPlcProfile.IqR), new bool[7169]));
    }

    [Fact]
    public async Task DirectAccess_IqFRejectsFxBitPointLimitOverruns()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqF, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        var readError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.M, 0, SlmpPlcProfile.IqR), 3585));
        var writeError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.M, 0, SlmpPlcProfile.IqR), new bool[3585]));

        Assert.Contains("1..3584", readError.Message);
        Assert.Contains("1..3584", writeError.Message);
    }

    [Fact]
    public async Task ReadTypeNameAsync_QnUDVProfileRejectsBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QnUDV, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        var ex = await Assert.ThrowsAsync<SlmpProfileFeatureException>(
            () => client.ReadTypeNameAsync());

        Assert.Equal("melsec:qnudv", ex.ProfileId);
        Assert.Equal("type_name", ex.FeatureKey);
        Assert.Equal("blocked", ex.State);
        Assert.Contains("C059", ex.Message);
        Assert.DoesNotContain("StrictProfile", ex.Message);
    }

    [Fact]
    public async Task ReadBlockAsync_QnUDVProfileRejectsBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QnUDV, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        var ex = await Assert.ThrowsAsync<SlmpProfileFeatureException>(
            () => client.ReadBlockAsync(
                [new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.D, 100, SlmpPlcProfile.IqR), 1)],
                []));

        Assert.Equal("block", ex.FeatureKey);
        Assert.Equal("blocked", ex.State);
        Assert.Contains("melsec:qnudv", ex.Message);
    }

    [Fact]
    public async Task RegisterMonitorDevicesAsync_IqFProfileRejectsBlockedMonitor()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqF, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        var ex = await Assert.ThrowsAsync<SlmpProfileFeatureException>(
            () => client.RegisterMonitorDevicesAsync(
                [new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR)],
                []));

        Assert.Equal("monitor", ex.FeatureKey);
        Assert.Equal("blocked", ex.State);
    }

    [Fact]
    public async Task ReadWordsExtendedAsync_IqFProfileRejectsBlockedLinkDirect()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqF, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var device = SlmpQualifiedDeviceParser.Parse(@"J2\SW10", SlmpPlcProfile.IqF);

        var ex = await Assert.ThrowsAsync<SlmpProfileFeatureException>(
            () => client.ReadWordsExtendedAsync(device, 1));

        Assert.Equal("ext_link_direct", ex.FeatureKey);
        Assert.Equal("blocked", ex.State);
    }

    [Fact]
    public async Task ReadWordsExtendedAsync_IqLProfileRejectsCpuBufferHg()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqL, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var device = SlmpQualifiedDeviceParser.Parse(@"U3E0\HG20", SlmpPlcProfile.IqL);

        var ex = await Assert.ThrowsAsync<SlmpProfileFeatureException>(
            () => client.ReadWordsExtendedAsync(device, 1));

        Assert.Equal("hg_cpu_buffer", ex.FeatureKey);
        Assert.Equal("blocked", ex.State);
    }

    [Fact]
    public async Task ReadWordsExtendedAsync_IqFDoesNotGuardConfigDependentModuleAccess()
    {
        await using var server = new MultiShotSlmpServer([(0, new byte[] { 0x78, 0x56 })]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqF, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        await client.OpenAsync();

        var values = await client.ReadWordsExtendedAsync(
            SlmpQualifiedDeviceParser.Parse(@"U1\G0", SlmpPlcProfile.IqF),
            1);

        Assert.Equal(new ushort[] { 0x5678 }, values);
        Assert.Single(server.RequestFrames);
    }

    [Fact]
    public async Task WriteBitsAsync_IqRRejectsStepRelayByWritePolicy()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.S, 0, SlmpPlcProfile.IqR), SingleTrue));

        Assert.Contains("S is read-only for PLC profile", ex.Message);
    }

    [Fact]
    public async Task WriteBitsAsync_IqFAllowsStepRelayWritesByWritePolicy()
    {
        await using var server = new MultiShotSlmpServer([(0, Array.Empty<byte>())]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqF, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
            MonitoringTimer = 0x0010,
        };

        await client.WriteBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.S, 0, SlmpPlcProfile.IqF), SingleTrue);

        Assert.Single(server.RequestFrames);
    }

    [Fact]
    public async Task DirectAccess_DoesNotUseDeviceRangeUpperBoundsAsSendGuard()
    {
        await using var server = new MultiShotSlmpServer([
            (0, new byte[] { 0x34, 0x12 }),
            (0, Array.Empty<byte>()),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
            MonitoringTimer = 0x0010,
        };

        var values = await client.ReadWordsSingleRequestAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 999_999, SlmpPlcProfile.IqR), 1);
        Assert.Equal(new ushort[] { 0x1234 }, values);

        await client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 999_999, SlmpPlcProfile.IqR), [0x5678]);
        Assert.Equal(2, server.RequestFrames.Count);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LSTS)]
    [InlineData(SlmpDeviceCode.LCS)]
    public async Task WriteBitsExtendedAsync_RejectsDirectLongFamilyStateWrites(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBitsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), null),
                SingleTrue));
        Assert.Contains($"Direct bit write is not supported for {code}", ex.Message);
    }

    [Fact]
    public async Task WriteBitsExtendedAsync_RejectsStepRelayWrites()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBitsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.S, 10, SlmpPlcProfile.IqR), null),
                SingleTrue));
        Assert.Contains("S is read-only for PLC profile", ex.Message);
    }

    [Fact]
    public async Task ReadWordsExtendedAsync_RejectsDirectLongCounterCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10, SlmpPlcProfile.IqR), null),
                4));
        Assert.Contains("Direct word read is not supported for LCN", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task WriteWordsExtendedAsync_RejectsLongCurrentAndLzDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), null),
                new ushort[] { 1 }));
        Assert.Contains($"Direct word write is not supported for {code}", ex.Message);
    }

    [Fact]
    public async Task ReadWordsExtendedAsync_RejectsUnqualifiedGAndHg()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        var g = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.G, 0, SlmpPlcProfile.IqR), null),
                1));
        Assert.Contains("G Extended Device access requires U-qualified", g.Message);

        var hg = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.HG, 0, SlmpPlcProfile.IqR), null),
                1));
        Assert.Contains("HG Extended Device access requires U-qualified", hg.Message);
    }

    [Fact]
    public async Task ReadWordsExtendedAsync_AllowsQualifiedGAndHg()
    {
        await using var server = new MultiShotSlmpServer([
            (0, BuildWordPayload(0x1A0A)),
            (0, BuildWordPayload(0x1234)),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        var g = await client.ReadWordsExtendedAsync(
            SlmpQualifiedDeviceParser.Parse(@"U4\G10", SlmpPlcProfile.IqR),
            1);
        var hg = await client.ReadWordsExtendedAsync(
            SlmpQualifiedDeviceParser.Parse(@"U3E0\HG0", SlmpPlcProfile.IqR),
            1);

        Assert.Equal(new ushort[] { 0x1A0A }, g);
        Assert.Equal(new ushort[] { 0x1234 }, hg);
        Assert.Equal(2, server.RequestFrames.Count);
    }

    [Fact]
    public async Task ReadRandomAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10, SlmpPlcProfile.IqR) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("Read Random (0x0403) does not support LCS/LCC", ex.Message);
    }

    [Fact]
    public async Task ReadRandomAsync_RejectsLongTimerStateDevices()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(SlmpDeviceCode.LTC, 10, SlmpPlcProfile.IqR) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("Read Random (0x0403) does not support LTS/LTC/LSTS/LSTC", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LCN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task ReadRandomAsync_RejectsLongCurrentAndLzWordEntries(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.G)]
    [InlineData(SlmpDeviceCode.HG)]
    public async Task ReadRandomAsync_RejectsStandaloneQualifiedOnlyDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("does not support standalone G/HG", ex.Message);
    }

    [Fact]
    public async Task WriteRandomWordsAsync_IqLRejectsWeightedLimitOverrun()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqL, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var words = Enumerable.Range(0, 40)
            .Select(index => (new SlmpDeviceAddress(SlmpDeviceCode.D, (uint)(8100 + index), SlmpPlcProfile.IqR), (ushort)0))
            .ToArray();
        var dwords = Enumerable.Range(0, 40)
            .Select(index => (new SlmpDeviceAddress(SlmpDeviceCode.D, (uint)(8200 + (index * 2)), SlmpPlcProfile.IqR), (uint)0))
            .ToArray();

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteRandomWordsAsync(words, dwords));
        Assert.Contains("limit=960", ex.Message);

        using var iqf = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqF, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var iqfDwords = Enumerable.Range(0, 138)
            .Select(index => (new SlmpDeviceAddress(SlmpDeviceCode.D, (uint)(9000 + (index * 2)), SlmpPlcProfile.IqR), (uint)0))
            .ToArray();

        ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => iqf.WriteRandomWordsAsync(
                Array.Empty<(SlmpDeviceAddress Device, ushort Value)>(),
                iqfDwords));
        Assert.Contains("limit=1920", ex.Message);
    }

    [Fact]
    public async Task ReadRandomExtAsync_RejectsLongCurrentWordEntries()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomExtAsync(
                [new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10, SlmpPlcProfile.IqR), null)],
                Array.Empty<SlmpQualifiedDeviceAddress>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
    }

    [Fact]
    public async Task ReadRandomExtAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomExtAsync(
                Array.Empty<SlmpQualifiedDeviceAddress>(),
                [new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10, SlmpPlcProfile.IqR), null)]));
        Assert.Contains("Read Random (0x0403) does not support LCS/LCC", ex.Message);
    }

    [Fact]
    public async Task ReadRandomExtAsync_UsesProfileExtendedPointLimit()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QnU, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var devices = Enumerable.Range(0, 193)
            .Select(index => new SlmpQualifiedDeviceAddress(
                new SlmpDeviceAddress(SlmpDeviceCode.D, (uint)index, SlmpPlcProfile.IqR), null))
            .ToArray();

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadRandomExtAsync(
                devices,
                Array.Empty<SlmpQualifiedDeviceAddress>()));
        Assert.Contains("(1..192)", ex.Message);
    }

    [Fact]
    public async Task WriteRandomWordsExtAsync_UsesProfileExtendedWeightedLimit()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QnU, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var dwordEntries = Enumerable.Range(0, 138)
            .Select(index => (
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.D, (uint)(200 + (index * 2)), SlmpPlcProfile.IqR), null),
                (uint)index))
            .ToArray();

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteRandomWordsExtAsync(
                Array.Empty<(SlmpQualifiedDeviceAddress Device, ushort Value)>(),
                dwordEntries));
        Assert.Contains("limit=1920", ex.Message);
    }

    [Fact]
    public async Task WriteRandomBitsExtAsync_UsesProfileExtendedBitLimit()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QnU, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var entries = Enumerable.Range(0, 189)
            .Select(index => (
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.M, (uint)index, SlmpPlcProfile.IqR), null),
                true))
            .ToArray();

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteRandomBitsExtAsync(entries));
        Assert.Contains("(1..188)", ex.Message);
    }

    [Fact]
    public async Task RegisterMonitorDevicesExtAsync_UsesProfileExtendedPointLimit()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QnU, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var devices = Enumerable.Range(0, 193)
            .Select(index => new SlmpQualifiedDeviceAddress(
                new SlmpDeviceAddress(SlmpDeviceCode.D, (uint)index, SlmpPlcProfile.IqR), null))
            .ToArray();

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.RegisterMonitorDevicesExtAsync(
                devices,
                Array.Empty<SlmpQualifiedDeviceAddress>()));
        Assert.Contains("(1..192)", ex.Message);
    }

    [Fact]
    public async Task ReadBlockAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBlockAsync(
                Array.Empty<SlmpBlockRead>(),
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10, SlmpPlcProfile.IqR), 1) }));
        Assert.Contains("Read Block (0x0406) does not support LCS/LCC", ex.Message);
    }

    [Theory]
    [InlineData(SlmpPlcProfile.LCpu, "melsec:lcpu")]
    [InlineData(SlmpPlcProfile.QnU, "melsec:qnu")]
    public async Task ReadBlockAsync_RejectsQSeriesProfiles(SlmpPlcProfile profile, string profileText)
    {
        using var client = new SlmpClient("127.0.0.1", profile, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<SlmpProfileFeatureException>(
            () => client.ReadBlockAsync(
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.D, 100, SlmpPlcProfile.IqR), 1) },
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.M, 100, SlmpPlcProfile.IqR), 1) }));
        Assert.Equal(profileText, ex.ProfileId);
        Assert.Equal("block", ex.FeatureKey);
        Assert.Equal("blocked", ex.State);
    }

    [Fact]
    public async Task ReadBlockAsync_RejectsLongCounterCurrentAndLzBlocks()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBlockAsync(
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10, SlmpPlcProfile.IqR), 4) },
                Array.Empty<SlmpBlockRead>()));
        Assert.Contains("does not support LCN/LZ", ex.Message);

        ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBlockAsync(
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0, SlmpPlcProfile.IqR), 2) },
                Array.Empty<SlmpBlockRead>()));
        Assert.Contains("does not support LCN/LZ", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.G)]
    [InlineData(SlmpDeviceCode.HG)]
    public async Task ReadBlockAsync_RejectsStandaloneQualifiedOnlyDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBlockAsync(
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), 1) },
                Array.Empty<SlmpBlockRead>()));
        Assert.Contains("does not support standalone G/HG", ex.Message);
    }

    [Fact]
    public async Task WriteBlockAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBlockAsync(
                Array.Empty<SlmpBlockWrite>(),
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.LCC, 10, SlmpPlcProfile.IqR), new ushort[] { 1 }) }));
        Assert.Contains("Write Block (0x1406) does not support LCS/LCC", ex.Message);
    }

    [Theory]
    [InlineData(SlmpPlcProfile.LCpu, "melsec:lcpu")]
    [InlineData(SlmpPlcProfile.QnU, "melsec:qnu")]
    public async Task WriteBlockAsync_RejectsQSeriesProfiles(SlmpPlcProfile profile, string profileText)
    {
        using var client = new SlmpClient("127.0.0.1", profile, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<SlmpProfileFeatureException>(
            () => client.WriteBlockAsync(
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.D, 100, SlmpPlcProfile.IqR), new ushort[] { 1 }) },
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.M, 100, SlmpPlcProfile.IqR), new ushort[] { 1 }) }));
        Assert.Equal(profileText, ex.ProfileId);
        Assert.Equal("block", ex.FeatureKey);
        Assert.Equal("blocked", ex.State);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LCN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task WriteBlockAsync_RejectsLongCurrentAndLzBlocks(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBlockAsync(
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), new ushort[] { 1 }) },
                Array.Empty<SlmpBlockWrite>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.G)]
    [InlineData(SlmpDeviceCode.HG)]
    public async Task WriteBlockAsync_RejectsStandaloneQualifiedOnlyDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBlockAsync(
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), new ushort[] { 1 }) },
                Array.Empty<SlmpBlockWrite>()));
        Assert.Contains("does not support standalone G/HG", ex.Message);
    }

    [Fact]
    public async Task WriteBlockAsync_DoesNotRetryC05BAsSplitRequests()
    {
        await using var server = new MultiShotSlmpServer([
            (0xC05B, Array.Empty<byte>()),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
        };

        var ex = await Assert.ThrowsAsync<SlmpError>(() => client.WriteBlockAsync(
            [new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.D, 100, SlmpPlcProfile.IqR), [0x1234])],
            [new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.M, 200, SlmpPlcProfile.IqR), [0x0005])]));
        Assert.Equal((ushort)0xC05B, ex.EndCode);

        Assert.Single(server.RequestFrames);
        AssertBlockWriteShape(server.RequestFrames[0], wordBlocks: 1, bitBlocks: 1);
        // Manual-conformant layout: each block's data follows its own spec.
        Assert.Equal(
            new byte[]
            {
                0x01, 0x01,
                0x64, 0x00, 0x00, 0x00, 0xA8, 0x00, 0x01, 0x00, 0x34, 0x12, // D100 x1 + data
                0xC8, 0x00, 0x00, 0x00, 0x90, 0x00, 0x01, 0x00, 0x05, 0x00, // M200 x1 + data
            },
            server.RequestFrames[0].AsSpan(13 + 6).ToArray());
    }

    [Fact]
    public async Task WriteBlockAsync_DoesNotRetryC056AsSplitRequests()
    {
        await using var server = new MultiShotSlmpServer([
            (0xC056, Array.Empty<byte>()),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
        };

        var ex = await Assert.ThrowsAsync<SlmpError>(() => client.WriteBlockAsync(
            [new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.D, 100, SlmpPlcProfile.IqR), [0x1234])],
            [new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.M, 200, SlmpPlcProfile.IqR), [0x0005])]));
        Assert.Equal((ushort)0xC056, ex.EndCode);

        var request = Assert.Single(server.RequestFrames);
        AssertBlockWriteShape(request, wordBlocks: 1, bitBlocks: 1);
    }

    [Fact]
    public async Task PlcError_ExposesStructuredErrorInformation()
    {
        byte[] errorData = [0x00, 0xFF, 0xFF, 0x03, 0x00, 0x01, 0x04, 0x01, 0x00];
        await using var server = new MultiShotSlmpServer([
            (0xC051, errorData),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
        };

        var ex = await Assert.ThrowsAsync<SlmpError>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR), 1));

        Assert.Equal((ushort)0xC051, ex.EndCode);
        Assert.NotNull(ex.ErrorInfo);
        Assert.Equal((byte)0x00, ex.ErrorInfo.Network);
        Assert.Equal((byte)0xFF, ex.ErrorInfo.Station);
        Assert.Equal((ushort)0x03FF, ex.ErrorInfo.ModuleIo);
        Assert.Equal((byte)0x00, ex.ErrorInfo.Multidrop);
        Assert.Equal((ushort)0x0401, ex.ErrorInfo.Command);
        Assert.Equal((ushort)0x0001, ex.ErrorInfo.Subcommand);
        Assert.Equal(errorData, ex.ErrorInfo.Raw);
    }

    [Fact]
    public async Task RemoteRunAsync_ClearModesHaveExactWireValues()
    {
        await using var server = new MultiShotSlmpServer([
            (0x0000, Array.Empty<byte>()),
            (0x0000, Array.Empty<byte>()),
            (0x0000, Array.Empty<byte>()),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
        };

        await client.RemoteRunAsync(SlmpRemoteMode.Normal, SlmpRemoteClearMode.NoClear);
        await client.RemoteRunAsync(SlmpRemoteMode.Normal, SlmpRemoteClearMode.ClearExceptLatch);
        await client.RemoteRunAsync(SlmpRemoteMode.Normal, SlmpRemoteClearMode.ClearAll);

        Assert.Equal(3, server.RequestFrames.Count);
        AssertCpuOperationShape(server.RequestFrames[0], 0x1001, [0x01, 0x00, 0x00, 0x00]);
        AssertCpuOperationShape(server.RequestFrames[1], 0x1001, [0x01, 0x00, 0x01, 0x00]);
        AssertCpuOperationShape(server.RequestFrames[2], 0x1001, [0x01, 0x00, 0x02, 0x00]);
    }

    [Theory]
    [InlineData((ushort)3)]
    [InlineData(ushort.MaxValue)]
    public async Task RemoteRunAsync_RejectsInvalidClearMode(ushort clearMode)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.RemoteRunAsync(SlmpRemoteMode.Normal, (SlmpRemoteClearMode)clearMode));
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)2)]
    [InlineData(ushort.MaxValue)]
    public async Task RemoteRunAndPause_RejectInvalidMode(ushort mode)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.RemoteRunAsync((SlmpRemoteMode)mode, SlmpRemoteClearMode.NoClear));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.RemotePauseAsync((SlmpRemoteMode)mode));
    }

    [Fact]
    public async Task CpuOperations_KeepRemoteStopOnManualFixedMode()
    {
        await using var server = new MultiShotSlmpServer([
            (0x0000, Array.Empty<byte>()),
            (0x0000, Array.Empty<byte>()),
            (0x0000, Array.Empty<byte>()),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
        };

        await client.RemoteRunAsync(SlmpRemoteMode.Force, SlmpRemoteClearMode.NoClear);
        await client.RemoteStopAsync();
        await client.RemotePauseAsync(SlmpRemoteMode.Force);

        Assert.Equal(3, server.RequestFrames.Count);
        AssertCpuOperationShape(server.RequestFrames[0], 0x1001, [0x03, 0x00, 0x00, 0x00]);
        AssertCpuOperationShape(server.RequestFrames[1], 0x1002, [0x01, 0x00]);
        AssertCpuOperationShape(server.RequestFrames[2], 0x1003, [0x03, 0x00]);
    }

    [Fact]
    public async Task WriteRandomWordsAsync_RejectsLongCurrentWordEntries()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomWordsAsync(
                new[] { (new SlmpDeviceAddress(SlmpDeviceCode.LTN, 10, SlmpPlcProfile.IqR), (ushort)1) },
                Array.Empty<(SlmpDeviceAddress Device, uint Value)>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
    }

    [Fact]
    public async Task WriteRandomWordsAsync_RejectsStepRelayWrites()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomWordsAsync(
                new[] { (new SlmpDeviceAddress(SlmpDeviceCode.S, 10, SlmpPlcProfile.IqR), (ushort)1) },
                Array.Empty<(SlmpDeviceAddress Device, uint Value)>()));
        Assert.Contains("S is read-only for PLC profile", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.G)]
    [InlineData(SlmpDeviceCode.HG)]
    public async Task WriteRandomWordsAsync_RejectsStandaloneQualifiedOnlyDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomWordsAsync(
                new[] { (new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), (ushort)1) },
                Array.Empty<(SlmpDeviceAddress Device, uint Value)>()));
        Assert.Contains("does not support standalone G/HG", ex.Message);
    }

    [Fact]
    public async Task WriteRandomBitsAsync_RejectsStepRelayWrites()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomBitsAsync(
                new[] { (new SlmpDeviceAddress(SlmpDeviceCode.S, 10, SlmpPlcProfile.IqR), true) }));
        Assert.Contains("S is read-only for PLC profile", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.G)]
    [InlineData(SlmpDeviceCode.HG)]
    public async Task WriteRandomBitsAsync_RejectsQualifiedOnlyDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomBitsAsync(
                new[] { (new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), true) }));
        Assert.Contains("does not support G/HG devices", ex.Message);
    }

    [Fact]
    public async Task WriteRandomBitsExtAsync_RejectsStepRelayWrites()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomBitsExtAsync(
                [(new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.S, 10, SlmpPlcProfile.IqR), null), true)]));
        Assert.Contains("S is read-only for PLC profile", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.G)]
    [InlineData(SlmpDeviceCode.HG)]
    public async Task WriteRandomBitsExtAsync_RejectsQualifiedOnlyDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomBitsExtAsync(
                [(new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR), null), true)]));
        Assert.Contains("does not support G/HG devices", ex.Message);
    }

    [Fact]
    public async Task WriteBlockAsync_RejectsStepRelayWrites()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBlockAsync(
                Array.Empty<SlmpBlockWrite>(),
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.S, 10, SlmpPlcProfile.IqR), new ushort[] { 1 }) }));
        Assert.Contains("S is read-only for PLC profile", ex.Message);
    }

    [Fact]
    public async Task RandomAndBlockAccess_RejectManualPointLimitOverruns()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadRandomAsync(RandomWordDevices(97), Array.Empty<SlmpDeviceAddress>()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteRandomWordsAsync(RandomWordEntries(81), Array.Empty<(SlmpDeviceAddress Device, uint Value)>()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteRandomWordsAsync(Array.Empty<(SlmpDeviceAddress Device, ushort Value)>(), RandomDwordEntries(69)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteRandomBitsAsync(RandomBitEntries(95)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.ReadBlockAsync([new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR), 961)], []));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.WriteBlockAsync([new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.D, 8000, SlmpPlcProfile.IqR), new ushort[952])], []));
    }

    [Fact]
    public async Task AggregateCollections_RejectNullOrBothEmptyBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ReadRandomAsync(null!, Array.Empty<SlmpDeviceAddress>()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ReadRandomAsync(Array.Empty<SlmpDeviceAddress>(), null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadRandomAsync([], []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ReadRandomExtAsync(null!, Array.Empty<SlmpQualifiedDeviceAddress>()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ReadRandomExtAsync(Array.Empty<SlmpQualifiedDeviceAddress>(), null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadRandomExtAsync([], []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.WriteRandomWordsAsync(null!, Array.Empty<(SlmpDeviceAddress Device, uint Value)>()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.WriteRandomWordsAsync(Array.Empty<(SlmpDeviceAddress Device, ushort Value)>(), null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteRandomWordsAsync([], []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.WriteRandomWordsExtAsync(null!, Array.Empty<(SlmpQualifiedDeviceAddress Device, uint Value)>()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.WriteRandomWordsExtAsync(Array.Empty<(SlmpQualifiedDeviceAddress Device, ushort Value)>(), null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteRandomWordsExtAsync([], []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ReadBlockAsync(null!, Array.Empty<SlmpBlockRead>()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ReadBlockAsync(Array.Empty<SlmpBlockRead>(), null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadBlockAsync([], []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.WriteBlockAsync(null!, Array.Empty<SlmpBlockWrite>()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.WriteBlockAsync(Array.Empty<SlmpBlockWrite>(), null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteBlockAsync([], []));
    }

    [Fact]
    public async Task MemoryAndExtendUnit_RejectManualPointLimitOverruns()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.MemoryReadWordsAsync(0, 481));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.MemoryWriteWordsAsync(0, new ushort[481]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ExtendUnitReadBytesAsync(0, 1921, 0x03E0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ExtendUnitWriteBytesAsync(0, 0x03E0, new byte[1921]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ExtendUnitReadWordsAsync(0, 961, 0x03E0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ExtendUnitWriteWordsAsync(0, 0x03E0, new ushort[961]));
    }

    private static void AssertBlockWriteShape(byte[] request, byte wordBlocks, byte bitBlocks)
    {
        var body = request.AsSpan(13);
        Assert.Equal((ushort)0x1406, BinaryPrimitives.ReadUInt16LittleEndian(body[2..4]));
        Assert.Equal((ushort)0x0002, BinaryPrimitives.ReadUInt16LittleEndian(body[4..6]));
        Assert.Equal(wordBlocks, body[6]);
        Assert.Equal(bitBlocks, body[7]);
    }

    private static void AssertCpuOperationShape(byte[] request, ushort command, byte[] payload)
    {
        var body = request.AsSpan(13);
        Assert.Equal(command, BinaryPrimitives.ReadUInt16LittleEndian(body[2..4]));
        Assert.Equal((ushort)0x0000, BinaryPrimitives.ReadUInt16LittleEndian(body[4..6]));
        Assert.Equal(payload, body[6..(6 + payload.Length)].ToArray());
    }

    private static void AssertDeviceReadWordShape(byte[] request, SlmpDeviceCode code, uint number, ushort points)
    {
        var body = request.AsSpan(13);
        Assert.Equal((ushort)0x0401, BinaryPrimitives.ReadUInt16LittleEndian(body[2..4]));
        Assert.Equal((ushort)0x0002, BinaryPrimitives.ReadUInt16LittleEndian(body[4..6]));
        var payload = body[6..];
        Assert.Equal(number, BinaryPrimitives.ReadUInt32LittleEndian(payload[..4]));
        Assert.Equal((ushort)code, BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]));
        Assert.Equal(points, BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]));
    }

    private static void AssertDeviceReadBitShape(byte[] request, SlmpDeviceCode code, uint number, ushort points)
    {
        var body = request.AsSpan(13);
        Assert.Equal((ushort)0x0401, BinaryPrimitives.ReadUInt16LittleEndian(body[2..4]));
        Assert.Equal((ushort)0x0003, BinaryPrimitives.ReadUInt16LittleEndian(body[4..6]));
        var payload = body[6..];
        Assert.Equal(number, BinaryPrimitives.ReadUInt32LittleEndian(payload[..4]));
        Assert.Equal((ushort)code, BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]));
        Assert.Equal(points, BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]));
    }

    [Fact]
    public async Task BaseClient_SerializesConcurrentRequestsAndUsesUnique4ESerials()
    {
        const int requestCount = 16;
        await using var server = new MultiShotSlmpServer(
            Enumerable.Range(0, requestCount)
                .Select(index => (EndCode: (ushort)0, ResponseData: BuildWordPayload((ushort)index))));
        await server.StartAsync();

        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            server.Port,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);

        var tasks = Enumerable.Range(0, requestCount)
            .Select(index => client.ReadWordsRawAsync(
                new SlmpDeviceAddress(SlmpDeviceCode.D, (uint)index, SlmpPlcProfile.IqR),
                1))
            .ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(requestCount, server.RequestFrames.Count);
        var serials = server.RequestFrames
            .Select(frame => BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2, 2)))
            .ToArray();
        Assert.Equal(requestCount, serials.Distinct().Count());
    }

    private static byte[] BuildWordPayload(params ushort[] values)
    {
        var payload = new byte[values.Length * 2];
        for (var index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(index * 2, 2), values[index]);
        }

        return payload;
    }

    private static SlmpDeviceAddress[] RandomWordDevices(int count)
        => Enumerable.Range(0, count).Select(i => new SlmpDeviceAddress(SlmpDeviceCode.D, (uint)(8000 + i), SlmpPlcProfile.IqR)).ToArray();

    private static (SlmpDeviceAddress Device, ushort Value)[] RandomWordEntries(int count)
        => Enumerable.Range(0, count).Select(i => (new SlmpDeviceAddress(SlmpDeviceCode.D, (uint)(8000 + i), SlmpPlcProfile.IqR), (ushort)0)).ToArray();

    private static (SlmpDeviceAddress Device, uint Value)[] RandomDwordEntries(int count)
        => Enumerable.Range(0, count).Select(i => (new SlmpDeviceAddress(SlmpDeviceCode.D, (uint)(8000 + (i * 2)), SlmpPlcProfile.IqR), 0u)).ToArray();

    private static (SlmpDeviceAddress Device, bool Value)[] RandomBitEntries(int count)
        => Enumerable.Range(0, count).Select(i => (new SlmpDeviceAddress(SlmpDeviceCode.M, (uint)(4000 + i), SlmpPlcProfile.IqR), false)).ToArray();

    private sealed class MultiShotSlmpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Queue<(ushort EndCode, byte[] ResponseData)> _responses;
        private Task? _serverTask;

        public MultiShotSlmpServer(IEnumerable<(ushort EndCode, byte[] ResponseData)> responses)
        {
            _responses = new Queue<(ushort EndCode, byte[] ResponseData)>(responses);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public List<byte[]> RequestFrames { get; } = [];

        public Task StartAsync()
        {
            _listener.Start();
            _serverTask = RunAsync();
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            if (_serverTask is not null)
            {
                await _serverTask.ConfigureAwait(false);
            }
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using var stream = client.GetStream();
                while (_responses.Count > 0)
                {
                    var request = await ReadRequestFrameAsync(stream).ConfigureAwait(false);
                    RequestFrames.Add(request);

                    var (endCode, responseData) = _responses.Dequeue();
                    var response = BuildResponse(request, responseData, endCode);
                    await stream.WriteAsync(response).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static async Task<byte[]> ReadRequestFrameAsync(NetworkStream stream)
        {
            var head = await ReadExactAsync(stream, 2).ConfigureAwait(false);
            if (head[0] == 0x54 && head[1] == 0x00)
            {
                var rest = await ReadExactAsync(stream, 11).ConfigureAwait(false);
                var header = head.Concat(rest).ToArray();
                var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(11, 2));
                var body = await ReadExactAsync(stream, bodyLength).ConfigureAwait(false);
                return header.Concat(body).ToArray();
            }

            if (head[0] == 0x50 && head[1] == 0x00)
            {
                var rest = await ReadExactAsync(stream, 7).ConfigureAwait(false);
                var header = head.Concat(rest).ToArray();
                var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(7, 2));
                var body = await ReadExactAsync(stream, bodyLength).ConfigureAwait(false);
                return header.Concat(body).ToArray();
            }

            throw new IOException("Unexpected SLMP request subheader.");
        }

        private static byte[] BuildResponse(ReadOnlySpan<byte> request, ReadOnlySpan<byte> responseData, ushort endCode)
            => request[0] switch
            {
                0x54 => Build4EResponse(request, responseData, endCode),
                0x50 => Build3EResponse(request, responseData, endCode),
                _ => throw new IOException("Unexpected SLMP request subheader."),
            };

        private static byte[] Build4EResponse(ReadOnlySpan<byte> request, ReadOnlySpan<byte> responseData, ushort endCode)
        {
            var payload = new byte[2 + responseData.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(payload, endCode);
            responseData.CopyTo(payload.AsSpan(2));

            var response = new byte[13 + payload.Length];
            response[0] = 0xD4;
            response[1] = 0x00;
            request.Slice(2, 2).CopyTo(response.AsSpan(2));
            request.Slice(6, 5).CopyTo(response.AsSpan(6));
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(11, 2), checked((ushort)payload.Length));
            payload.CopyTo(response.AsSpan(13));
            return response;
        }

        private static byte[] Build3EResponse(ReadOnlySpan<byte> request, ReadOnlySpan<byte> responseData, ushort endCode)
        {
            var payload = new byte[2 + responseData.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(payload, endCode);
            responseData.CopyTo(payload.AsSpan(2));

            var response = new byte[9 + payload.Length];
            response[0] = 0xD0;
            response[1] = 0x00;
            request.Slice(2, 5).CopyTo(response.AsSpan(2));
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(7, 2), checked((ushort)payload.Length));
            payload.CopyTo(response.AsSpan(9));
            return response;
        }

        private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int size)
        {
            var buffer = new byte[size];
            var read = 0;
            while (read < size)
            {
                var chunk = await stream.ReadAsync(buffer.AsMemory(read, size - read)).ConfigureAwait(false);
                if (chunk == 0)
                {
                    throw new IOException("Unexpected end of stream.");
                }
                read += chunk;
            }
            return buffer;
        }
    }

    private sealed class SerialSkewSlmpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly byte[] _staleResponseData;
        private readonly byte[] _matchingResponseData;
        private Task? _serverTask;

        public SerialSkewSlmpServer(byte[] staleResponseData, byte[] matchingResponseData)
        {
            _staleResponseData = staleResponseData;
            _matchingResponseData = matchingResponseData;
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public List<byte[]> RequestFrames { get; } = [];

        public Task StartAsync()
        {
            _listener.Start();
            _serverTask = RunAsync();
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            if (_serverTask is not null)
            {
                await _serverTask.ConfigureAwait(false);
            }
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using var stream = client.GetStream();
                var head = await ReadExactAsync(stream, 13).ConfigureAwait(false);
                var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(11, 2));
                var body = await ReadExactAsync(stream, bodyLength).ConfigureAwait(false);
                var request = new byte[head.Length + body.Length];
                head.CopyTo(request, 0);
                body.CopyTo(request, head.Length);
                RequestFrames.Add(request);

                var requestSerial = BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(2, 2));
                var stale = Build4EResponse(request, _staleResponseData, unchecked((ushort)(requestSerial + 1)));
                var matching = Build4EResponse(request, _matchingResponseData, requestSerial);
                await stream.WriteAsync(stale).ConfigureAwait(false);
                await stream.WriteAsync(matching).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
        }

        private static byte[] Build4EResponse(ReadOnlySpan<byte> request, ReadOnlySpan<byte> responseData, ushort serial)
        {
            var payload = new byte[2 + responseData.Length];
            responseData.CopyTo(payload.AsSpan(2));

            var response = new byte[13 + payload.Length];
            response[0] = 0xD4;
            response[1] = 0x00;
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(2, 2), serial);
            request.Slice(6, 5).CopyTo(response.AsSpan(6));
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(11, 2), checked((ushort)payload.Length));
            payload.CopyTo(response.AsSpan(13));
            return response;
        }

        private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int size)
        {
            var buffer = new byte[size];
            var read = 0;
            while (read < size)
            {
                var chunk = await stream.ReadAsync(buffer.AsMemory(read, size - read)).ConfigureAwait(false);
                if (chunk == 0)
                {
                    throw new IOException("Unexpected end of stream.");
                }
                read += chunk;
            }
            return buffer;
        }
    }

    [Fact]
    public async Task WriteRandomWordsExtAsync_RejectsLongCurrentWordEntries()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomWordsExtAsync(
                [(new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 1, SlmpPlcProfile.IqR), null), (ushort)1)],
                Array.Empty<(SlmpQualifiedDeviceAddress Device, uint Value)>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
    }

    [Fact]
    public async Task RegisterMonitorDevicesAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.RegisterMonitorDevicesAsync(
                new[] { new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10, SlmpPlcProfile.IqR) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("Entry Monitor Device (0x0801) does not support LCS/LCC", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.G)]
    [InlineData(SlmpDeviceCode.HG)]
    public async Task RegisterMonitorDevicesAsync_RejectsStandaloneQualifiedOnlyDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.RegisterMonitorDevicesAsync(
                new[] { new SlmpDeviceAddress(code, 10, SlmpPlcProfile.IqR) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("does not support standalone G/HG", ex.Message);
    }

    [Fact]
    public async Task RegisterMonitorDevicesExtAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.RegisterMonitorDevicesExtAsync(
                [new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10, SlmpPlcProfile.IqR), null)],
                Array.Empty<SlmpQualifiedDeviceAddress>()));
        Assert.Contains("Entry Monitor Device (0x0801) does not support LCS/LCC", ex.Message);
    }
}
