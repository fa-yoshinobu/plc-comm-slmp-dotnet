using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpApiEvolutionTests
{
    private static readonly SlmpDeviceAddress D100 =
        new(SlmpDeviceCode.D, 100, SlmpPlcProfile.IqR);

    private static readonly SlmpQualifiedDeviceAddress ExtendedD100 =
        new(D100, 1);

    private static readonly SlmpQualifiedDeviceAddress ExtendedD200 =
        new(new SlmpDeviceAddress(SlmpDeviceCode.D, 200, SlmpPlcProfile.IqR), 1);

    private static readonly SlmpQualifiedDeviceAddress ExtendedM100 =
        new(new SlmpDeviceAddress(SlmpDeviceCode.M, 100, SlmpPlcProfile.IqR), 1);

    [Fact]
    public void RemovedMemoryAndExtendUnitMethods_AreAbsentFromPublicSurface()
    {
        string[] removedNames =
        [
            "MemoryReadWordsAsync",
            "MemoryWriteWordsAsync",
            "ExtendUnitReadBytesAsync",
            "ExtendUnitReadWordsAsync",
            "ExtendUnitReadWordAsync",
            "ExtendUnitReadDWordAsync",
            "ExtendUnitWriteBytesAsync",
            "ExtendUnitWriteWordsAsync",
            "ExtendUnitWriteWordAsync",
            "ExtendUnitWriteDWordAsync",
        ];

        var publicNames = typeof(SlmpClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(removedNames, name => Assert.DoesNotContain(name, publicNames));
    }

    [Fact]
    public async Task CanonicalWordReadNames_AndMigrationAliasesUseIdenticalWireAndResults()
    {
        var canonicalWords = await CaptureReadAsync(
            client => client.ReadWordsAsync(D100, 1),
            [0x34, 0x12]);
        var aliasWords = await CaptureReadAsync(
            client => client.ReadWordsRawAsync(D100, 1),
            [0x34, 0x12]);

        Assert.Equal(new ushort[] { 0x1234 }, canonicalWords.Result);
        Assert.Equal(canonicalWords.Result, aliasWords.Result);
        Assert.Equal(canonicalWords.Request, aliasWords.Request);

        var canonicalDWords = await CaptureReadAsync(
            client => client.ReadDWordsAsync(D100, 1),
            [0x78, 0x56, 0x34, 0x12]);
        var aliasDWords = await CaptureReadAsync(
            client => client.ReadDWordsRawAsync(D100, 1),
            [0x78, 0x56, 0x34, 0x12]);

        Assert.Equal(new uint[] { 0x12345678 }, canonicalDWords.Result);
        Assert.Equal(canonicalDWords.Result, aliasDWords.Result);
        Assert.Equal(canonicalDWords.Request, aliasDWords.Request);
    }

    [Fact]
    public async Task CanonicalExtendedNames_AndMigrationAliasesUseIdenticalWireAndResults()
    {
        var canonicalRead = await CaptureReadAsync(
            client => client.ReadRandomExtendedAsync([ExtendedD100], [ExtendedD200]),
            [0x34, 0x12, 0x78, 0x56, 0x34, 0x12]);
        var aliasRead = await CaptureReadAsync(
            client => client.ReadRandomExtAsync([ExtendedD100], [ExtendedD200]),
            [0x34, 0x12, 0x78, 0x56, 0x34, 0x12]);

        Assert.Equal(canonicalRead.Result.WordValues, aliasRead.Result.WordValues);
        Assert.Equal(canonicalRead.Result.DwordValues, aliasRead.Result.DwordValues);
        Assert.Equal(canonicalRead.Request, aliasRead.Request);

        var canonicalWordWrite = await CaptureWriteAsync(client =>
            client.WriteRandomWordsExtendedAsync([(ExtendedD100, (ushort)0x1234)], [(ExtendedD200, 0x12345678u)]));
        var aliasWordWrite = await CaptureWriteAsync(client =>
            client.WriteRandomWordsExtAsync([(ExtendedD100, (ushort)0x1234)], [(ExtendedD200, 0x12345678u)]));
        Assert.Equal(canonicalWordWrite, aliasWordWrite);

        var canonicalBitWrite = await CaptureWriteAsync(client =>
            client.WriteRandomBitsExtendedAsync([(ExtendedM100, true)]));
        var aliasBitWrite = await CaptureWriteAsync(client =>
            client.WriteRandomBitsExtAsync([(ExtendedM100, true)]));
        Assert.Equal(canonicalBitWrite, aliasBitWrite);

        var canonicalMonitor = await CaptureWriteAsync(client =>
            client.RegisterMonitorDevicesExtendedAsync([ExtendedD100], [ExtendedD200]));
        var aliasMonitor = await CaptureWriteAsync(client =>
            client.RegisterMonitorDevicesExtAsync([ExtendedD100], [ExtendedD200]));
        Assert.Equal(canonicalMonitor, aliasMonitor);
    }

    [Fact]
    public async Task WriteDWordsBlockCompatibilityOverloads_AreObsoleteDirectWireDelegates()
    {
        var methods = typeof(SlmpClientExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(static method => method.Name == "WriteDWordsBlockAsync")
            .ToArray();

        Assert.Equal(2, methods.Length);
        Assert.All(methods, method => Assert.Equal(
            "Use WriteDWordsSingleRequestAsync; WriteDWordsBlockAsync will be removed after one compatibility release.",
            method.GetCustomAttribute<ObsoleteAttribute>()?.Message));

        var values = new uint[] { 0x12345678 };
        var canonicalAddress = await CaptureWriteAsync(client =>
            client.WriteDWordsSingleRequestAsync(D100, values));
#pragma warning disable CS0618
        var aliasAddress = await CaptureWriteAsync(client =>
            client.WriteDWordsBlockAsync(D100, values));
#pragma warning restore CS0618
        Assert.Equal(canonicalAddress, aliasAddress);

        var canonicalText = await CaptureWriteAsync(client =>
            client.WriteDWordsSingleRequestAsync("D100", values));
#pragma warning disable CS0618
        var aliasText = await CaptureWriteAsync(client =>
            client.WriteDWordsBlockAsync("D100", values));
#pragma warning restore CS0618
        Assert.Equal(canonicalText, aliasText);

        using var invalidClient = CreateClient(1025);
        var oversized = new uint[481];
        var canonicalError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            invalidClient.WriteDWordsSingleRequestAsync(D100, oversized));
#pragma warning disable CS0618
        var aliasError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            invalidClient.WriteDWordsBlockAsync(D100, oversized));
#pragma warning restore CS0618
        Assert.Equal(canonicalError.ParamName, aliasError.ParamName);
        Assert.Equal(canonicalError.Message, aliasError.Message);
        Assert.Equal<ulong>(0, invalidClient.TrafficStats.RequestCount);
    }

    [Fact]
    public async Task ReadLatestSelfDiagnosisErrorCodeAsync_ReadsExactlySd0WordOnce()
    {
        var capture = await CaptureReadAsync(
            client => client.ReadLatestSelfDiagnosisErrorCodeAsync(),
            [0xEF, 0xBE]);

        Assert.Equal((ushort)0xBEEF, capture.Result);
        Assert.Equal((ushort)SlmpCommand.DeviceRead, BinaryPrimitives.ReadUInt16LittleEndian(capture.Request.AsSpan(15, 2)));
        Assert.Equal((ushort)0x0002, BinaryPrimitives.ReadUInt16LittleEndian(capture.Request.AsSpan(17, 2)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(capture.Request.AsSpan(19, 4)));
        Assert.Equal((ushort)SlmpDeviceCode.SD, BinaryPrimitives.ReadUInt16LittleEndian(capture.Request.AsSpan(23, 2)));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(capture.Request.AsSpan(25, 2)));
    }

    [Fact]
    public async Task ReadLatestSelfDiagnosisErrorCodeAsync_PreservesCancellationAndPlcErrors()
    {
        using (var cancelledClient = CreateClient(1025))
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                cancelledClient.ReadLatestSelfDiagnosisErrorCodeAsync(cancellation.Token));
            Assert.Equal<ulong>(0, cancelledClient.TrafficStats.RequestCount);
        }

        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var operation = client.ReadLatestSelfDiagnosisErrorCodeAsync();
        var request = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await server.SendAsync(BuildResponse(request.Buffer, [], 0xC051), request.RemoteEndPoint);

        var error = await Assert.ThrowsAsync<SlmpError>(() => operation);
        Assert.Equal((ushort)0xC051, error.EndCode);
        Assert.Equal<ulong>(1, client.TrafficStats.RequestCount);
    }

    private static async Task<(T Result, byte[] Request)> CaptureReadAsync<T>(
        Func<SlmpClient, Task<T>> operation,
        byte[] responseData)
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(((IPEndPoint)server.Client.LocalEndPoint!).Port);

        var operationTask = operation(client);
        var request = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await server.SendAsync(BuildResponse(request.Buffer, responseData), request.RemoteEndPoint);
        var result = await operationTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal<ulong>(1, client.TrafficStats.RequestCount);
        return (result, request.Buffer);
    }

    private static async Task<byte[]> CaptureWriteAsync(Func<SlmpClient, Task> operation)
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(((IPEndPoint)server.Client.LocalEndPoint!).Port);

        var operationTask = operation(client);
        var request = await server.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await server.SendAsync(BuildResponse(request.Buffer, []), request.RemoteEndPoint);
        await operationTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal<ulong>(1, client.TrafficStats.RequestCount);
        return request.Buffer;
    }

    private static SlmpClient CreateClient(int port)
        => new(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation);

    private static byte[] BuildResponse(byte[] request, byte[] responseData, ushort endCode = 0)
    {
        var response = new byte[15 + responseData.Length];
        response[0] = 0xD4;
        response[1] = 0x00;
        response[2] = request[2];
        response[3] = request[3];
        request.AsSpan(6, 5).CopyTo(response.AsSpan(6));
        BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(11, 2), checked((ushort)(responseData.Length + 2)));
        BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(13, 2), endCode);
        responseData.CopyTo(response, 15);
        return response;
    }
}
