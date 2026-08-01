using System.Reflection;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class QualityOverhaulContractTests
{
    private static SlmpClient Client()
        => new("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

    private static SlmpDeviceAddress D(uint number) => new(SlmpDeviceCode.D, number, SlmpPlcProfile.IqR);
    private static SlmpDeviceAddress M(uint number) => new(SlmpDeviceCode.M, number, SlmpPlcProfile.IqR);

    [Fact]
    public void RawExtendedWireModel_IsNotPublic()
    {
        var exportedNames = typeof(SlmpClient).Assembly.GetExportedTypes().Select(static type => type.Name);
        Assert.DoesNotContain("SlmpExtensionSpec", exportedNames);
        Assert.Null(typeof(SlmpQualifiedDeviceAddress).GetProperty(
            "DirectMemorySpecification",
            BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public async Task CategorySpecificAggregateApis_RejectEmptyCollectionsBeforeTransport()
    {
        using var client = Client();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadRandomWordsAsync([]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadRandomDWordsAsync([]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteRandomU16sAsync([]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteRandomU32sAsync([]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadWordBlocksAsync([]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadBitBlocksAsync([]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteWordBlocksAsync([]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteBitBlocksAsync([]));

        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task RandomWrites_RejectDuplicateAndOverlappingDestinationsBeforeTransport()
    {
        using var client = Client();

        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteRandomWordsAsync(
            [(D(100), (ushort)1)],
            [(D(99), 2u)]));
        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteRandomBitsAsync(
            [(M(100), true), (M(100), false)]));

        var u1d100 = new SlmpQualifiedDeviceAddress(D(100), 1);
        var u1d99 = new SlmpQualifiedDeviceAddress(D(99), 1);
        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteRandomWordsExtAsync(
            [(u1d100, (ushort)1)],
            [(u1d99, 2u)]));

        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task BlockWrites_RejectOverlappingRangesBeforeTransport()
    {
        using var client = Client();

        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteWordBlocksAsync(
        [
            new SlmpBlockWrite(D(100), [1, 2]),
            new SlmpBlockWrite(D(101), [3]),
        ]));
        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteBitBlocksAsync(
        [
            new SlmpBlockWrite(M(100), [1, 0]),
            new SlmpBlockWrite(M(101), [1]),
        ]));

        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task BlockApis_RejectWrongUnitCategoryBeforeTransport()
    {
        using var client = Client();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadBlockAsync([new SlmpBlockRead(M(0), 1)], []));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadBlockAsync([], [new SlmpBlockRead(D(0), 1)]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteBlockAsync([new SlmpBlockWrite(M(0), [1])], []));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteBlockAsync([], [new SlmpBlockWrite(D(0), [1])]));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void DeviceUnitClassifier_CoversEveryPublicDeviceCodeExactlyOnce()
    {
        var codes = Enum.GetValues<SlmpDeviceCode>();
        Assert.Equal(codes.Length, codes.Distinct().Count());
        foreach (var code in codes)
            Assert.True(SlmpDeviceUnits.Get(code) is SlmpDeviceUnit.Bit or SlmpDeviceUnit.Word, code.ToString());
    }

    [Fact]
    public async Task EveryWordDevice_IsRejectedBySemanticBitSurfacesBeforeTransport()
    {
        using var client = Client();
        foreach (var code in Enum.GetValues<SlmpDeviceCode>().Where(SlmpDeviceUnits.IsWord))
        {
            var device = new SlmpDeviceAddress(code, 0, SlmpPlcProfile.IqR);
            var qualified = new SlmpQualifiedDeviceAddress(device, 1);

            await Assert.ThrowsAsync<ArgumentException>(() => client.ReadBitsAsync(device, 1));
            await Assert.ThrowsAsync<ArgumentException>(() => client.WriteBitsAsync(device, [true]));
            await Assert.ThrowsAsync<ArgumentException>(() => client.ReadBitsExtendedAsync(qualified, 1));
            await Assert.ThrowsAsync<ArgumentException>(() => client.WriteBitsExtendedAsync(qualified, [true]));
            await Assert.ThrowsAsync<ArgumentException>(() => client.WriteRandomBitsAsync([(device, true)]));
            await Assert.ThrowsAsync<ArgumentException>(() => client.WriteRandomBitsExtAsync([(qualified, true)]));
            await Assert.ThrowsAsync<ArgumentException>(() => client.ReadBitBlocksAsync([new SlmpBlockRead(device, 1)]));
            await Assert.ThrowsAsync<ArgumentException>(() => client.WriteBitBlocksAsync([new SlmpBlockWrite(device, [1])]));
        }

        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
    }

    [Fact]
    public async Task EveryBitDevice_IsRejectedByWordBlocksButAllowedByExplicitLowLevelWordAccess()
    {
        using var client = Client();
        foreach (var code in Enum.GetValues<SlmpDeviceCode>().Where(SlmpDeviceUnits.IsBit))
        {
            var device = new SlmpDeviceAddress(code, 0, SlmpPlcProfile.IqR);
            await Assert.ThrowsAsync<ArgumentException>(() => client.ReadWordBlocksAsync([new SlmpBlockRead(device, 1)]));
            await Assert.ThrowsAsync<ArgumentException>(() => client.WriteWordBlocksAsync([new SlmpBlockWrite(device, [1])]));
            Assert.IsType<ArgumentException>(Record.Exception(() =>
            {
                _ = client.WriteBitInWordAsync(device, 0, true);
            }));
        }

        Assert.False(client.IsOpen);

        using var server = new System.Net.Sockets.UdpClient(
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var port = ((System.Net.IPEndPoint)server.Client.LocalEndPoint!).Port;
        using var packedClient = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation);
        var packedRead = packedClient.ReadWordsRawAsync(M(0), 1);
        var request = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var serial = BitConverter.ToUInt16(request.Buffer, 2);
        byte[] response =
        [
            0xD4, 0x00, (byte)serial, (byte)(serial >> 8), 0x00, 0x00,
            0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x04, 0x00, 0x00, 0x00, 0x34, 0x12,
        ];
        await server.SendAsync(response, request.RemoteEndPoint);

        var packedValues = await packedRead;
        Assert.Equal(new ushort[] { 0x1234 }, packedValues);
        Assert.Equal((ushort)SlmpCommand.DeviceRead, BitConverter.ToUInt16(request.Buffer, 15));
        Assert.Equal<ushort>(0x0002, BitConverter.ToUInt16(request.Buffer, 17));
    }

    [Theory]
    [InlineData("LTN10:D")]
    [InlineData("LSTN10:L")]
    [InlineData("LTS10:BIT")]
    [InlineData("LTC10:BIT")]
    [InlineData("LSTS10:BIT")]
    [InlineData("LSTC10:BIT")]
    public async Task NamedLongTimerDirectRoutes_AreRejectedBeforeTransport(string address)
    {
        using var client = Client();
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadNamedAsync(["D100:U", address]));
        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        Assert.Empty(client.LastRequestFrame);
    }

    [Fact]
    public async Task EmptyNamedRead_IsRejectedInsteadOfReturningAZeroRequestResult()
    {
        using var client = Client();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadNamedAsync([]));
        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
    }

    [Fact]
    public async Task InvalidTypedAndNamedWrites_FailBeforeFifoAdmission()
    {
        using var client = Client();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = client.ExecuteExclusiveAsync(async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsType<ArgumentException>(Record.Exception(() =>
        {
            _ = client.WriteTypedAsync(M(0), "U", 1);
        }));
        Assert.IsType<ArgumentException>(Record.Exception(() =>
        {
            _ = client.WriteNamedAsync(
                new Dictionary<string, object> { ["D0:BIT"] = true });
        }));

        Assert.False(blocker.IsCompleted);
        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        release.TrySetResult();
        await blocker.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task TypedAndNamedScalars_RequireExactSemanticDeviceUnitsBeforeTransport()
    {
        using var client = Client();
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadTypedAsync(D(0), "BIT"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadTypedAsync(M(0), "U"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteTypedAsync(D(0), "BIT", true));
        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteTypedAsync(M(0), "U", 1));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadNamedAsync(["D0:BIT"]));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadNamedAsync(["M0:U"]));
        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteNamedAsync(new Dictionary<string, object> { ["D0:BIT"] = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteNamedAsync(new Dictionary<string, object> { ["M0:U"] = 1 }));
        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)481)]
    [InlineData((ushort)32768)]
    [InlineData(ushort.MaxValue)]
    public async Task DWordAndFloatReads_RejectPublicUnitCountsBeforeConversion(ushort points)
    {
        using var client = Client();
        var dwordError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadDWordsRawAsync(D(0), points));
        var floatError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadFloat32sAsync(D(0), points));
        Assert.Equal("points", dwordError.ParamName);
        Assert.Equal("points", floatError.ParamName);
        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(481)]
    public async Task DWordAndFloatWrites_RejectCollectionCountBeforeMultiplicationOrAllocation(int count)
    {
        using var client = Client();
        var dwords = new uint[count];
        var floats = new float[count];
        var dwordError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteDWordsAsync(D(0), dwords));
        var floatError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteFloat32sAsync(D(0), floats));
        Assert.Equal("values", dwordError.ParamName);
        Assert.Equal("values", floatError.ParamName);
        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DWordAndFloatReads_AcceptExact480ValueBoundary(bool floats)
    {
        using var server = new System.Net.Sockets.UdpClient(
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            ((System.Net.IPEndPoint)server.Client.LocalEndPoint!).Port,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation);

        Task request = floats
            ? client.ReadFloat32sAsync(D(0), 480)
            : client.ReadDWordsRawAsync(D(0), 480);
        var received = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await server.SendAsync(BuildUdpResponse(received.Buffer, new byte[480 * 4]), received.RemoteEndPoint);
        await request.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal<ulong>(1, client.TrafficStats.RequestCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DWordAndFloatWrites_AcceptExact480ValueBoundary(bool floats)
    {
        using var server = new System.Net.Sockets.UdpClient(
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            ((System.Net.IPEndPoint)server.Client.LocalEndPoint!).Port,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation);

        Task request = floats
            ? client.WriteFloat32sAsync(D(0), new float[480])
            : client.WriteDWordsAsync(D(0), new uint[480]);
        var received = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await server.SendAsync(BuildUdpResponse(received.Buffer, []), received.RemoteEndPoint);
        await request.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal<ulong>(1, client.TrafficStats.RequestCount);
    }

    [Theory]
    [InlineData(SlmpPlcProfile.IqR)]
    [InlineData(SlmpPlcProfile.QnUQj71E71100)]
    public async Task DirectWordAndBitSpans_UseSelectedWireWidthBeforeAdmission(
        SlmpPlcProfile plcProfile)
    {
        using var client = new SlmpClient(
            "127.0.0.1",
            plcProfile,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);
        var maximum = plcProfile == SlmpPlcProfile.IqR ? uint.MaxValue : 0x00FF_FFFFU;
        var word = new SlmpDeviceAddress(SlmpDeviceCode.D, maximum, plcProfile);
        var bit = new SlmpDeviceAddress(SlmpDeviceCode.M, maximum, plcProfile);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadWordsRawAsync(word, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteWordsAsync(word, [1], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadBitsAsync(bit, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteBitsAsync(bit, [true], cancelled.Token));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadWordsRawAsync(word, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteWordsAsync(word, [1, 2]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadBitsAsync(bit, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteBitsAsync(bit, [true, false]));

        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        Assert.Empty(client.LastRequestFrame);
    }

    [Theory]
    [InlineData(SlmpPlcProfile.IqR)]
    [InlineData(SlmpPlcProfile.QnUQj71E71100)]
    public async Task PackedBitDeviceWordSpans_CountSixteenDeviceNumbersPerWord(
        SlmpPlcProfile plcProfile)
    {
        using var client = new SlmpClient(
            "127.0.0.1",
            plcProfile,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);
        var maximum = plcProfile == SlmpPlcProfile.IqR ? uint.MaxValue : 0x00FF_FFFFU;
        var validStart = new SlmpDeviceAddress(SlmpDeviceCode.M, maximum - 15U, plcProfile);
        var validDwordStart = new SlmpDeviceAddress(SlmpDeviceCode.M, maximum - 31U, plcProfile);
        var invalidStart = new SlmpDeviceAddress(SlmpDeviceCode.M, maximum, plcProfile);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadWordsRawAsync(validStart, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteWordsAsync(validStart, [1], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadDWordsRawAsync(validDwordStart, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteDWordsAsync(validDwordStart, [1U], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadFloat32sAsync(validDwordStart, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteFloat32sAsync(validDwordStart, [1.0F], cancelled.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadWordsRawAsync(validStart, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteWordsAsync(validStart, [1, 2]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadWordsRawAsync(invalidStart, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteWordsAsync(invalidStart, [1]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadDWordsRawAsync(validDwordStart, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteDWordsAsync(validDwordStart, [1U, 2U]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadFloat32sAsync(invalidStart, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteFloat32sAsync(invalidStart, [1.0F]));

        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        Assert.Empty(client.LastRequestFrame);
    }

    [Theory]
    [InlineData(SlmpPlcProfile.IqR)]
    public async Task LongTimerDirectBlocks_CountOneDevicePerFourWords(
        SlmpPlcProfile plcProfile)
    {
        using var client = new SlmpClient(
            "127.0.0.1",
            plcProfile,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);
        var maximum = plcProfile == SlmpPlcProfile.IqR ? uint.MaxValue : 0x00FF_FFFFU;
        var lastTimer = new SlmpDeviceAddress(SlmpDeviceCode.LTN, maximum, plcProfile);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadWordsRawAsync(lastTimer, 4, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadBlockAsync([new SlmpBlockRead(lastTimer, 4)], [], cancelled.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadWordsRawAsync(lastTimer, 8));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadBlockAsync([new SlmpBlockRead(lastTimer, 8)], []));

        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        Assert.Empty(client.LastRequestFrame);
    }

    [Theory]
    [InlineData(SlmpPlcProfile.IqR)]
    [InlineData(SlmpPlcProfile.QnUQj71E71100)]
    public async Task DWordAndFloatSpans_CountTwoWordAddressesPerValueBeforeAdmission(
        SlmpPlcProfile plcProfile)
    {
        using var client = new SlmpClient(
            "127.0.0.1",
            plcProfile,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);
        var maximum = plcProfile == SlmpPlcProfile.IqR ? uint.MaxValue : 0x00FF_FFFFU;
        var validStart = new SlmpDeviceAddress(SlmpDeviceCode.D, maximum - 1U, plcProfile);
        var invalidStart = new SlmpDeviceAddress(SlmpDeviceCode.D, maximum, plcProfile);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadDWordsRawAsync(validStart, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteDWordsAsync(validStart, [1U], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadFloat32sAsync(validStart, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteFloat32sAsync(validStart, [1.0F], cancelled.Token));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadDWordsRawAsync(validStart, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteDWordsAsync(validStart, [1U, 2U]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadFloat32sAsync(validStart, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteFloat32sAsync(validStart, [1.0F, 2.0F]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadDWordsRawAsync(invalidStart, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteDWordsAsync(invalidStart, [1U]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadFloat32sAsync(invalidStart, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteFloat32sAsync(invalidStart, [1.0F]));

        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        Assert.Empty(client.LastRequestFrame);
    }

    [Theory]
    [InlineData(SlmpPlcProfile.IqR)]
    [InlineData(SlmpPlcProfile.QnUQj71E71100)]
    public async Task RandomMonitorAndBlockRoutes_UseTheirConsumedDeviceSpansBeforeAdmission(
        SlmpPlcProfile plcProfile)
    {
        using var client = new SlmpClient(
            "127.0.0.1",
            plcProfile,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);
        var maximum = plcProfile == SlmpPlcProfile.IqR ? uint.MaxValue : 0x00FF_FFFFU;
        var validDword = new SlmpDeviceAddress(SlmpDeviceCode.D, maximum - 1U, plcProfile);
        var invalidDword = new SlmpDeviceAddress(SlmpDeviceCode.D, maximum, plcProfile);
        var lastWord = new SlmpDeviceAddress(SlmpDeviceCode.D, maximum, plcProfile);
        var lastBitBlock = new SlmpDeviceAddress(SlmpDeviceCode.M, maximum - 15U, plcProfile);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadRandomAsync([], [validDword], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteRandomWordsAsync([], [(validDword, 1U)], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.RegisterMonitorDevicesAsync([], [validDword], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadBlockAsync(
                [new SlmpBlockRead(lastWord, 1)],
                [new SlmpBlockRead(lastBitBlock, 1)],
                cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteBlockAsync(
                [new SlmpBlockWrite(lastWord, [1])],
                [new SlmpBlockWrite(lastBitBlock, [1])],
                cancelled.Token));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadRandomAsync([], [invalidDword]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteRandomWordsAsync([], [(invalidDword, 1U)]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.RegisterMonitorDevicesAsync([], [invalidDword]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadBlockAsync([new SlmpBlockRead(lastWord, 2)], []));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadBlockAsync([], [new SlmpBlockRead(lastBitBlock, 2)]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteBlockAsync([new SlmpBlockWrite(lastWord, [1, 2])], []));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteBlockAsync([], [new SlmpBlockWrite(lastBitBlock, [1, 2])]));

        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        Assert.Empty(client.LastRequestFrame);
    }

    [Fact]
    public async Task ExtendedRoutes_UseTheirSelectedWireWidthBeforeAdmission()
    {
        using var client = Client();
        var iqrMaximum = new SlmpQualifiedDeviceAddress(
            new SlmpDeviceAddress(SlmpDeviceCode.D, uint.MaxValue, SlmpPlcProfile.IqR),
            1);
        var linkMaximum = SlmpQualifiedDeviceParser.Parse(@"J2\SWFFFFFF", SlmpPlcProfile.IqR);
        var linkDword = SlmpQualifiedDeviceParser.Parse(@"J2\SWFFFFFE", SlmpPlcProfile.IqR);
        var iqrBitMaximum = new SlmpQualifiedDeviceAddress(
            new SlmpDeviceAddress(SlmpDeviceCode.M, uint.MaxValue, SlmpPlcProfile.IqR),
            1);
        var iqrPackedWord = new SlmpQualifiedDeviceAddress(
            new SlmpDeviceAddress(SlmpDeviceCode.M, uint.MaxValue - 15U, SlmpPlcProfile.IqR),
            1);
        var iqrPackedDword = new SlmpQualifiedDeviceAddress(
            new SlmpDeviceAddress(SlmpDeviceCode.M, uint.MaxValue - 31U, SlmpPlcProfile.IqR),
            1);
        var linkBitMaximum = SlmpQualifiedDeviceParser.Parse(@"J2\BFFFFFF", SlmpPlcProfile.IqR);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadWordsExtendedAsync(iqrMaximum, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteWordsExtendedAsync(iqrMaximum, [1], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadWordsExtendedAsync(linkMaximum, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadBitsExtendedAsync(iqrBitMaximum, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteBitsExtendedAsync(iqrBitMaximum, [true], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadBitsExtendedAsync(linkBitMaximum, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteBitsExtendedAsync(linkBitMaximum, [true], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadWordsExtendedAsync(iqrPackedWord, 1, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteWordsExtendedAsync(iqrPackedWord, [1], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadRandomExtAsync([], [linkDword], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteRandomWordsExtAsync([], [(linkDword, 1U)], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.RegisterMonitorDevicesExtAsync([], [linkDword], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadRandomExtAsync([], [iqrPackedDword], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteRandomWordsExtAsync([], [(iqrPackedDword, 1U)], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.RegisterMonitorDevicesExtAsync([], [iqrPackedDword], cancelled.Token));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadWordsExtendedAsync(iqrMaximum, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteWordsExtendedAsync(iqrMaximum, [1, 2]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadWordsExtendedAsync(linkMaximum, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadBitsExtendedAsync(iqrBitMaximum, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteBitsExtendedAsync(iqrBitMaximum, [true, false]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadBitsExtendedAsync(linkBitMaximum, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteBitsExtendedAsync(linkBitMaximum, [true, false]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadWordsExtendedAsync(iqrPackedWord, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteWordsExtendedAsync(iqrPackedWord, [1, 2]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadRandomExtAsync([], [linkMaximum]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteRandomWordsExtAsync([], [(linkMaximum, 1U)]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.RegisterMonitorDevicesExtAsync([], [linkMaximum]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadRandomExtAsync([], [iqrBitMaximum]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteRandomWordsExtAsync([], [(iqrBitMaximum, 1U)]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.RegisterMonitorDevicesExtAsync([], [iqrBitMaximum]));

        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        Assert.Empty(client.LastRequestFrame);
    }

    [Fact]
    public async Task PackedBitWriteOverlap_UsesConsumedDeviceSpans()
    {
        using var client = Client();
        var m0 = M(0);
        var m15 = M(15);
        var m31 = M(31);
        var q0 = new SlmpQualifiedDeviceAddress(m0, 1);
        var q15 = new SlmpQualifiedDeviceAddress(m15, 1);
        var qNull = new SlmpQualifiedDeviceAddress(m0, null);
        var qZero = new SlmpQualifiedDeviceAddress(m15, 0);
        var qSameZero = new SlmpQualifiedDeviceAddress(m0, 0);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteRandomWordsAsync([(m0, (ushort)1), (m15, (ushort)2)], []));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteRandomWordsAsync([(m31, (ushort)1)], [(m0, 2U)]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteRandomWordsExtAsync([(q0, (ushort)1), (q15, (ushort)2)], []));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteRandomWordsExtAsync([(qNull, (ushort)1), (qZero, (ushort)2)], []));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteRandomBitsExtAsync([(qNull, true), (qSameZero, false)]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteBlockAsync(
                [],
                [new SlmpBlockWrite(m0, [1]), new SlmpBlockWrite(m15, [2])]));

        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        Assert.Empty(client.LastRequestFrame);
    }

    [Fact]
    public async Task NativeDWordRandomAndMonitorEntries_ConsumeOneDeviceNumber()
    {
        using var client = Client();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        foreach (var code in new[]
                 {
                     SlmpDeviceCode.LTN,
                     SlmpDeviceCode.LSTN,
                     SlmpDeviceCode.LCN,
                     SlmpDeviceCode.LZ,
                 })
        {
            var device = new SlmpDeviceAddress(code, uint.MaxValue, SlmpPlcProfile.IqR);
            var qualified = new SlmpQualifiedDeviceAddress(device, null);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.ReadRandomAsync([], [device], cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.WriteRandomWordsAsync([], [(device, 1U)], cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.RegisterMonitorDevicesAsync([], [device], cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.ReadRandomExtAsync([], [qualified], cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.WriteRandomWordsExtAsync([], [(qualified, 1U)], cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.RegisterMonitorDevicesExtAsync([], [qualified], cancelled.Token));
        }

        var lz0 = new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0, SlmpPlcProfile.IqR);
        var lz1 = new SlmpDeviceAddress(SlmpDeviceCode.LZ, 1, SlmpPlcProfile.IqR);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteRandomWordsAsync([], [(lz0, 1U), (lz1, 2U)], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.WriteRandomWordsExtAsync(
                [],
                [
                    (new SlmpQualifiedDeviceAddress(lz0, null), 1U),
                    (new SlmpQualifiedDeviceAddress(lz1, 0), 2U),
                ],
                cancelled.Token));

        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        Assert.Empty(client.LastRequestFrame);
    }

    [Fact]
    public void StateChangingAndTargetSelectingParameters_AreRequired()
    {
        Assert.False(ParameterIsOptional(nameof(SlmpClient.RemoteRunAsync), "mode"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.RemoteRunAsync), "clearMode"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.RemotePauseAsync), "mode"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLongTimerAsync), "headNo"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLongTimerAsync), "points"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLongRetentiveTimerAsync), "headNo"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLongRetentiveTimerAsync), "points"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLtcStatesAsync), "headNo"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLtcStatesAsync), "points"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLtsStatesAsync), "headNo"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLtsStatesAsync), "points"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLstcStatesAsync), "headNo"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLstcStatesAsync), "points"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLstsStatesAsync), "headNo"));
        Assert.False(ParameterIsOptional(nameof(SlmpClient.ReadLstsStatesAsync), "points"));
    }

    [Fact]
    public void PublicCancellationTokens_RemainOptionalDotNetControls()
    {
        var offenders = new[] { typeof(SlmpClient), typeof(SlmpClientExtensions) }
            .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(static method => method.GetParameters().Select(parameter => (method, parameter)))
            .Where(static item => item.parameter.ParameterType == typeof(CancellationToken) && !item.parameter.IsOptional)
            .Select(static item => $"{item.method.DeclaringType?.Name}.{item.method.Name}({item.parameter.Name})")
            .Distinct()
            .Order()
            .ToArray();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 241)]
    [InlineData(0, int.MaxValue)]
    public async Task LongTimerHelpers_RejectInvalidHeadAndCountBeforeTransport(int headNo, int points)
    {
        using var client = Client();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadLongTimerAsync(headNo, points));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadLongRetentiveTimerAsync(headNo, points));

        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task LongTimerHelpers_ApplyFamilyAndWireWidthGuardsBeforeTransport()
    {
        var device = new SlmpDeviceAddress(SlmpDeviceCode.LTN, 0x0100_0000, SlmpPlcProfile.IqR);
        var wireError = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlmpClient.ValidateLongTimerDeviceForWireMode(
                device,
                SlmpCompatibilityMode.Legacy,
                "headNo"));
        Assert.Equal("headNo", wireError.ParamName);

        using var unsupported = new SlmpClient(
            "127.0.0.1", SlmpPlcProfile.IqF, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        await Assert.ThrowsAsync<NotSupportedException>(() => unsupported.ReadLongTimerAsync(0, 1));
        await Assert.ThrowsAsync<NotSupportedException>(() => unsupported.ReadLongRetentiveTimerAsync(0, 1));
        Assert.False(unsupported.IsOpen);
    }

    [Fact]
    public void CpuBufferAliasesAndEnum_AreNotPublic()
    {
        Assert.Null(typeof(SlmpClient).Assembly.GetType("PlcComm.Slmp.SlmpCpuModule"));
        var removed = new[]
        {
            "CpuBufferReadWordsAsync", "CpuBufferReadBytesAsync", "CpuBufferReadWordAsync", "CpuBufferReadDWordAsync",
            "CpuBufferWriteWordsAsync", "CpuBufferWriteBytesAsync", "CpuBufferWriteWordAsync", "CpuBufferWriteDWordAsync",
        };
        foreach (var type in new[] { typeof(SlmpClient) })
        {
            foreach (var method in removed)
                Assert.Null(type.GetMethod(method));
        }
    }

    [Fact]
    public async Task ExtendedDevice_RejectsAddressProfileMismatchBeforeTransport()
    {
        using var client = Client();
        var mismatched = new SlmpQualifiedDeviceAddress(
            new SlmpDeviceAddress(SlmpDeviceCode.D, 100, SlmpPlcProfile.IqF),
            extensionSpecification: 1);
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadWordsExtendedAsync(mismatched, 1));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public void LabelAbbreviations_ValidateEmptyMalformedAndCountLimits()
    {
        var withoutAbbreviations = SlmpPayloads.BuildLabelRandomReadPayload(["FullLabel"], []);
        Assert.Equal(0, withoutAbbreviations[2]);
        Assert.Equal(0, withoutAbbreviations[3]);

        _ = SlmpPayloads.BuildLabelRandomReadPayload(["%1.Member", "%2.Member"], ["RootA", "RootB"]);
        Assert.Throws<ArgumentException>(() => SlmpPayloads.BuildLabelRandomReadPayload(["%"], ["Root"]));
        Assert.Throws<ArgumentException>(() => SlmpPayloads.BuildLabelRandomReadPayload(["%2.Member"], ["Root"]));
        Assert.Throws<ArgumentException>(() => SlmpPayloads.BuildLabelRandomReadPayload(["   "], []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SlmpPayloads.BuildLabelRandomReadPayload([], []));
        Assert.Throws<ArgumentOutOfRangeException>(() => SlmpPayloads.BuildLabelRandomReadPayload(
            ["FullLabel"],
            Enumerable.Repeat("Root", ushort.MaxValue + 1).ToArray()));
    }

    private static bool ParameterIsOptional(string methodName, string parameterName)
        => typeof(SlmpClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == methodName)
            .GetParameters()
            .Single(parameter => parameter.Name == parameterName)
            .IsOptional;

    private static byte[] BuildUdpResponse(byte[] request, byte[] payload)
    {
        var response = new byte[15 + payload.Length];
        response[0] = 0xD4;
        response[1] = 0x00;
        response[2] = request[2];
        response[3] = request[3];
        response[6] = request[6];
        response[7] = request[7];
        response[8] = request[8];
        response[9] = request[9];
        response[10] = request[10];
        BitConverter.TryWriteBytes(response.AsSpan(11, 2), checked((ushort)(payload.Length + 2)));
        payload.CopyTo(response, 15);
        return response;
    }
}
