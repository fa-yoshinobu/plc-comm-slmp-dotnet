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
        var offenders = new[] { typeof(SlmpClient), typeof(QueuedSlmpClient), typeof(SlmpClientExtensions) }
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
    public void CpuBufferAliasesAndEnum_AreNotPublic()
    {
        Assert.Null(typeof(SlmpClient).Assembly.GetType("PlcComm.Slmp.SlmpCpuModule"));
        var removed = new[]
        {
            "CpuBufferReadWordsAsync", "CpuBufferReadBytesAsync", "CpuBufferReadWordAsync", "CpuBufferReadDWordAsync",
            "CpuBufferWriteWordsAsync", "CpuBufferWriteBytesAsync", "CpuBufferWriteWordAsync", "CpuBufferWriteDWordAsync",
        };
        foreach (var type in new[] { typeof(SlmpClient), typeof(QueuedSlmpClient) })
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
}
