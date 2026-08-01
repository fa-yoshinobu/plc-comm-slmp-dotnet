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
