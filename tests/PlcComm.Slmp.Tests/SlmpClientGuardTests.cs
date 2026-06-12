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
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 0), 2));
        Assert.Contains("requires 4-word blocks", ex.Message);
    }

    [Fact]
    public async Task ReadWordsRawAsync_RejectsDirectLongCounterCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 0), 4));
        Assert.Contains("Direct word read is not supported for LCN", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTN)]
    [InlineData(SlmpDeviceCode.LCN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task ReadDWordsRawAsync_RejectsDirectLongCurrentAndLzReads(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadDWordsRawAsync(new SlmpDeviceAddress(code, 0), 1));
        Assert.Contains($"Direct DWord read is not supported for {code}", ex.Message);
    }

    [Fact]
    public async Task ReadWordsRawAsync_RejectsDirectLzWordReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0), 1));
        Assert.Contains("Direct word read is not supported for LZ", ex.Message);
    }

    [Fact]
    public async Task WriteWordsAsync_RejectsLongCurrentValueDevices()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 0), new ushort[] { 1 }));
        Assert.Contains("Direct word write is not supported for LTN", ex.Message);
    }

    [Fact]
    public async Task WriteDWordsAsync_RejectsDirectLongCurrentWrites()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteDWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 0), new uint[] { 1 }));
        Assert.Contains("Direct DWord write is not supported for LCN", ex.Message);
    }

    [Fact]
    public async Task WriteWordsAsync_RejectsLzWordWrites()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0), new ushort[] { 1 }));
        Assert.Contains("Direct word write is not supported for LZ", ex.Message);
    }

    [Fact]
    public async Task ReadBitsAsync_RejectsDirectLongTimerStateReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTS, 10), 1));
        Assert.Contains("Direct bit read is not supported for LTS", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTC)]
    [InlineData(SlmpDeviceCode.LCC)]
    public async Task WriteBitsAsync_RejectsDirectLongFamilyStateWrites(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBitsAsync(new SlmpDeviceAddress(code, 10), SingleTrue));
        Assert.Contains($"Direct bit write is not supported for {code}", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LSTS)]
    [InlineData(SlmpDeviceCode.LCS)]
    public async Task WriteBitsExtendedAsync_RejectsDirectLongFamilyStateWrites(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBitsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(code, 10), null),
                SingleTrue,
                new SlmpExtensionSpec()));
        Assert.Contains($"Direct bit write is not supported for {code}", ex.Message);
    }

    [Fact]
    public async Task ReadWordsExtendedAsync_RejectsDirectLongCounterCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10), null),
                4,
                new SlmpExtensionSpec()));
        Assert.Contains("Direct word read is not supported for LCN", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task WriteWordsExtendedAsync_RejectsLongCurrentAndLzDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(code, 10), null),
                new ushort[] { 1 },
                new SlmpExtensionSpec()));
        Assert.Contains($"Direct word write is not supported for {code}", ex.Message);
    }

    [Fact]
    public async Task ReadRandomAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("Read Random (0x0403) does not support LCS/LCC", ex.Message);
    }

    [Fact]
    public async Task ReadRandomAsync_RejectsLongTimerStateDevices()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(SlmpDeviceCode.LTC, 10) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("Read Random (0x0403) does not support LTS/LTC/LSTS/LSTC", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LCN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task ReadRandomAsync_RejectsLongCurrentAndLzWordEntries(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(code, 10) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
    }

    [Fact]
    public async Task ReadRandomExtAsync_RejectsLongCurrentWordEntries()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomExtAsync(
                new[] { (new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10), null), new SlmpExtensionSpec()) },
                Array.Empty<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
    }

    [Fact]
    public async Task ReadRandomExtAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomExtAsync(
                Array.Empty<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)>(),
                new[] { (new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10), null), new SlmpExtensionSpec()) }));
        Assert.Contains("Read Random (0x0403) does not support LCS/LCC", ex.Message);
    }

    [Fact]
    public async Task ReadBlockAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBlockAsync(
                Array.Empty<SlmpBlockRead>(),
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10), 1) }));
        Assert.Contains("Read Block (0x0406) does not support LCS/LCC", ex.Message);
    }

    [Fact]
    public async Task ReadBlockAsync_RejectsLongCounterCurrentAndLzBlocks()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBlockAsync(
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10), 4) },
                Array.Empty<SlmpBlockRead>()));
        Assert.Contains("does not support LCN/LZ", ex.Message);

        ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBlockAsync(
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0), 2) },
                Array.Empty<SlmpBlockRead>()));
        Assert.Contains("does not support LCN/LZ", ex.Message);
    }

    [Fact]
    public async Task WriteBlockAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBlockAsync(
                Array.Empty<SlmpBlockWrite>(),
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.LCC, 10), new ushort[] { 1 }) }));
        Assert.Contains("Write Block (0x1406) does not support LCS/LCC", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LCN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task WriteBlockAsync_RejectsLongCurrentAndLzBlocks(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBlockAsync(
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(code, 10), new ushort[] { 1 }) },
                Array.Empty<SlmpBlockWrite>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ", ex.Message);
    }

    [Fact]
    public async Task WriteBlockAsync_DoesNotRetryC05BAsSplitRequests()
    {
        await using var server = new MultiShotSlmpServer([
            (0xC05B, Array.Empty<byte>()),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", server.Port)
        {
            FrameType = SlmpFrameType.Frame4E,
            CompatibilityMode = SlmpCompatibilityMode.Iqr,
        };

        var ex = await Assert.ThrowsAsync<SlmpError>(() => client.WriteBlockAsync(
            [new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.D, 100), [0x1234])],
            [new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.M, 200), [0x0005])],
            new SlmpBlockWriteOptions(SplitMixedBlocks: false)));
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

        using var client = new SlmpClient("127.0.0.1", server.Port)
        {
            FrameType = SlmpFrameType.Frame4E,
            CompatibilityMode = SlmpCompatibilityMode.Iqr,
        };

        var ex = await Assert.ThrowsAsync<SlmpError>(() => client.WriteBlockAsync(
            [new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.D, 100), [0x1234])],
            [new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.M, 200), [0x0005])],
            new SlmpBlockWriteOptions(SplitMixedBlocks: false)));
        Assert.Equal((ushort)0xC056, ex.EndCode);

        var request = Assert.Single(server.RequestFrames);
        AssertBlockWriteShape(request, wordBlocks: 1, bitBlocks: 1);
    }

    [Fact]
    public async Task RemoteRunAsync_DefaultClearModeDoesNotClearDevices()
    {
        await using var server = new MultiShotSlmpServer([
            (0x0000, Array.Empty<byte>()),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", server.Port)
        {
            FrameType = SlmpFrameType.Frame4E,
            CompatibilityMode = SlmpCompatibilityMode.Iqr,
        };

        await client.RemoteRunAsync();

        var request = Assert.Single(server.RequestFrames);
        var body = request.AsSpan(13);
        Assert.Equal((ushort)0x1001, BinaryPrimitives.ReadUInt16LittleEndian(body[2..4]));
        Assert.Equal((ushort)0x0000, BinaryPrimitives.ReadUInt16LittleEndian(body[4..6]));
        Assert.Equal(new byte[] { 0x01, 0x00, 0x00, 0x00 }, body[6..10].ToArray());
    }

    [Fact]
    public async Task ForcedCpuOperations_UseForceWithoutLatchClear()
    {
        await using var server = new MultiShotSlmpServer([
            (0x0000, Array.Empty<byte>()),
            (0x0000, Array.Empty<byte>()),
            (0x0000, Array.Empty<byte>()),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", server.Port)
        {
            FrameType = SlmpFrameType.Frame4E,
            CompatibilityMode = SlmpCompatibilityMode.Iqr,
        };

        await client.RemoteRunAsync(force: true);
        await client.RemoteForceStopAsync();
        await client.RemotePauseAsync(force: true);

        Assert.Equal(3, server.RequestFrames.Count);
        AssertCpuOperationShape(server.RequestFrames[0], 0x1001, [0x03, 0x00, 0x00, 0x00]);
        AssertCpuOperationShape(server.RequestFrames[1], 0x1002, [0x03, 0x00]);
        AssertCpuOperationShape(server.RequestFrames[2], 0x1003, [0x03, 0x00]);
    }

    [Fact]
    public async Task WriteRandomWordsAsync_RejectsLongCurrentWordEntries()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomWordsAsync(
                new[] { (new SlmpDeviceAddress(SlmpDeviceCode.LTN, 10), (ushort)1) },
                Array.Empty<(SlmpDeviceAddress Device, uint Value)>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
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
                    var head = await ReadExactAsync(stream, 13).ConfigureAwait(false);
                    var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(11, 2));
                    var body = await ReadExactAsync(stream, bodyLength).ConfigureAwait(false);
                    var request = new byte[head.Length + body.Length];
                    head.CopyTo(request, 0);
                    body.CopyTo(request, head.Length);
                    RequestFrames.Add(request);

                    var (endCode, responseData) = _responses.Dequeue();
                    var response = Build4EResponse(request, responseData, endCode);
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
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomWordsExtAsync(
                new[] { (new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 1), null), (ushort)1, new SlmpExtensionSpec()) },
                Array.Empty<(SlmpQualifiedDeviceAddress Device, uint Value, SlmpExtensionSpec Extension)>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
    }

    [Fact]
    public async Task RegisterMonitorDevicesAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.RegisterMonitorDevicesAsync(
                new[] { new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("Entry Monitor Device (0x0801) does not support LCS/LCC", ex.Message);
    }

    [Fact]
    public async Task RegisterMonitorDevicesExtAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.RegisterMonitorDevicesExtAsync(
                new[] { (new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10), null), new SlmpExtensionSpec()) },
                Array.Empty<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)>()));
        Assert.Contains("Entry Monitor Device (0x0801) does not support LCS/LCC", ex.Message);
    }
}
