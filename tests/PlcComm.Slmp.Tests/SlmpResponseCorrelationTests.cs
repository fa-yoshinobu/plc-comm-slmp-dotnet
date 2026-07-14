using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpResponseCorrelationTests
{
    private static readonly SlmpTargetAddress TestTarget = new(0x12, 0x34, 0x5678, 0x9A);

    public static TheoryData<SlmpTransportMode, SlmpFrameType, RouteField> RouteCases
    {
        get
        {
            var cases = new TheoryData<SlmpTransportMode, SlmpFrameType, RouteField>();
            foreach (var transport in Enum.GetValues<SlmpTransportMode>())
            {
                foreach (var frame in Enum.GetValues<SlmpFrameType>())
                {
                    foreach (var routeField in Enum.GetValues<RouteField>())
                    {
                        cases.Add(transport, frame, routeField);
                    }
                }
            }
            return cases;
        }
    }

    public static TheoryData<SlmpTransportMode, SlmpFrameType> TransportFrameCases
    {
        get
        {
            var cases = new TheoryData<SlmpTransportMode, SlmpFrameType>();
            foreach (var transport in Enum.GetValues<SlmpTransportMode>())
            {
                foreach (var frame in Enum.GetValues<SlmpFrameType>())
                {
                    cases.Add(transport, frame);
                }
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RouteCases))]
    public async Task ForeignRouteIsDiscardedUntilMatchingResponseArrives(
        SlmpTransportMode transport,
        SlmpFrameType frameType,
        RouteField routeField)
    {
        await using var server = new ScriptedSlmpServer(
            transport,
            frameType,
            async (request, send, cancellationToken) =>
            {
                await send(BuildResponse(request, frameType, routeField, payload: [0xAA])).ConfigureAwait(false);
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                await send(BuildResponse(request, frameType, payload: [0xBB])).ConfigureAwait(false);
            });
        await server.StartAsync();

        using var client = CreateClient(server.Port, transport, frameType, TimeSpan.FromMilliseconds(500));

        var response = await client.RawCommandAsync(
            SlmpCommand.ClearError,
            0x0000,
            ReadOnlyMemory<byte>.Empty);

        Assert.Equal(new byte[] { 0xBB }, response);
        Assert.Equal(2UL * (frameType == SlmpFrameType.Frame4E ? 16UL : 12UL), client.TrafficStats.RxBytes);
    }

    [Theory]
    [MemberData(nameof(TransportFrameCases))]
    public async Task ForeignRouteFloodCannotExtendTheAbsoluteDeadline(
        SlmpTransportMode transport,
        SlmpFrameType frameType)
    {
        await using var server = new ScriptedSlmpServer(
            transport,
            frameType,
            async (request, send, cancellationToken) =>
            {
                for (var index = 0; index < 20; index++)
                {
                    await send(BuildResponse(request, frameType, RouteField.Station)).ConfigureAwait(false);
                    await Task.Delay(30, cancellationToken).ConfigureAwait(false);
                }
            });
        await server.StartAsync();

        using var client = CreateClient(server.Port, transport, frameType, TimeSpan.FromMilliseconds(120));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.RawCommandAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty));

        stopwatch.Stop();
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromMilliseconds(105), TimeSpan.FromMilliseconds(500));
        Assert.True(client.TrafficStats.RxBytes > 0);
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData(SlmpTransportMode.Tcp)]
    [InlineData(SlmpTransportMode.Udp)]
    public async Task WrongSerialFloodCannotExtendTheAbsoluteDeadline(SlmpTransportMode transport)
    {
        await using var server = new ScriptedSlmpServer(
            transport,
            SlmpFrameType.Frame4E,
            async (request, send, cancellationToken) =>
            {
                var requestSerial = BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(2, 2));
                for (var index = 0; index < 20; index++)
                {
                    await send(BuildResponse(
                        request,
                        SlmpFrameType.Frame4E,
                        serial: unchecked((ushort)(requestSerial + 1)))).ConfigureAwait(false);
                    await Task.Delay(30, cancellationToken).ConfigureAwait(false);
                }
            });
        await server.StartAsync();

        using var client = CreateClient(server.Port, transport, SlmpFrameType.Frame4E, TimeSpan.FromMilliseconds(120));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.RawCommandAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty));

        stopwatch.Stop();
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromMilliseconds(105), TimeSpan.FromMilliseconds(500));
        Assert.True(client.TrafficStats.RxBytes > 0);
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData(SlmpTransportMode.Tcp)]
    [InlineData(SlmpTransportMode.Udp)]
    public async Task WrongSerialIsDiscardedUntilMatchingResponseArrives(SlmpTransportMode transport)
    {
        await using var server = new ScriptedSlmpServer(
            transport,
            SlmpFrameType.Frame4E,
            async (request, send, cancellationToken) =>
            {
                var requestSerial = BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(2, 2));
                await send(BuildResponse(
                    request,
                    SlmpFrameType.Frame4E,
                    serial: unchecked((ushort)(requestSerial + 1)))).ConfigureAwait(false);
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                await send(BuildResponse(request, SlmpFrameType.Frame4E)).ConfigureAwait(false);
            });
        await server.StartAsync();

        using var client = CreateClient(
            server.Port,
            transport,
            SlmpFrameType.Frame4E,
            TimeSpan.FromMilliseconds(500));

        var response = await client.RawCommandAsync(
            SlmpCommand.ClearError,
            0x0000,
            ReadOnlyMemory<byte>.Empty);

        Assert.Empty(response);
        Assert.Equal(30UL, client.TrafficStats.RxBytes);
    }

    [Theory]
    [InlineData(SlmpFrameType.Frame3E)]
    [InlineData(SlmpFrameType.Frame4E)]
    public async Task TcpHeaderAndBodyMayBeSplitWithoutRestartingReceiveState(SlmpFrameType frameType)
    {
        await using var server = new ScriptedSlmpServer(
            SlmpTransportMode.Tcp,
            frameType,
            async (request, send, _) =>
                await send(BuildResponse(request, frameType, payload: [0x12, 0x34])).ConfigureAwait(false),
            splitTcpResponses: true);
        await server.StartAsync();
        using var client = CreateClient(server.Port, SlmpTransportMode.Tcp, frameType, TimeSpan.FromMilliseconds(500));

        var response = await client.RawCommandAsync(
            SlmpCommand.ClearError,
            0x0000,
            ReadOnlyMemory<byte>.Empty);

        Assert.Equal(new byte[] { 0x12, 0x34 }, response);
    }

    [Theory]
    [InlineData(SlmpFrameType.Frame3E)]
    [InlineData(SlmpFrameType.Frame4E)]
    public async Task TcpHeaderAndBodyShareOneAbsoluteDeadline(SlmpFrameType frameType)
    {
        await using var server = new ScriptedSlmpServer(
            SlmpTransportMode.Tcp,
            frameType,
            async (request, send, cancellationToken) =>
            {
                await Task.Delay(60, cancellationToken).ConfigureAwait(false);
                await send(BuildResponse(request, frameType, payload: [0x12, 0x34])).ConfigureAwait(false);
            },
            splitTcpResponses: true,
            splitTcpDelay: TimeSpan.FromMilliseconds(60));
        await server.StartAsync();
        using var client = CreateClient(server.Port, SlmpTransportMode.Tcp, frameType, TimeSpan.FromMilliseconds(100));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.RawCommandAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty));

        stopwatch.Stop();
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromMilliseconds(85), TimeSpan.FromMilliseconds(500));
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData(SlmpFrameType.Frame3E)]
    [InlineData(SlmpFrameType.Frame4E)]
    public async Task UdpTimeoutRequiresExplicitReopenAndNextExchangeUsesACleanSession(SlmpFrameType frameType)
    {
        var exchange = 0;
        await using var server = new ScriptedSlmpServer(
            SlmpTransportMode.Udp,
            frameType,
            async (request, send, _) =>
            {
                if (Interlocked.Increment(ref exchange) == 1)
                {
                    return;
                }

                await send(BuildResponse(request, frameType, payload: [0x5A])).ConfigureAwait(false);
            },
            exchangeCount: 2);
        await server.StartAsync();
        using var client = CreateClient(server.Port, SlmpTransportMode.Udp, frameType, TimeSpan.FromMilliseconds(80));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.RawCommandAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty));

        Assert.False(client.IsOpen);
        Assert.Equal(1UL, client.TrafficStats.RequestCount);
        Assert.Equal(0UL, client.TrafficStats.RxBytes);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.RawCommandAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty));

        await client.OpenAsync();
        var response = await client.RawCommandAsync(
            SlmpCommand.ClearError,
            0x0000,
            ReadOnlyMemory<byte>.Empty);

        Assert.Equal(new byte[] { 0x5A }, response);
        Assert.Equal(2UL, client.TrafficStats.RequestCount);
        Assert.True(client.IsOpen);
    }

    [Theory]
    [MemberData(nameof(TransportFrameCases))]
    public async Task MalformedResponseIsAProtocolErrorAndInvalidatesTransport(
        SlmpTransportMode transport,
        SlmpFrameType frameType)
    {
        await using var server = new ScriptedSlmpServer(
            transport,
            frameType,
            async (request, send, _) =>
            {
                await send(BuildMalformedResponse(request, frameType)).ConfigureAwait(false);
            });
        await server.StartAsync();

        using var client = CreateClient(server.Port, transport, frameType, TimeSpan.FromMilliseconds(500));

        var error = await Assert.ThrowsAsync<SlmpError>(
            () => client.RawCommandAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty));

        Assert.Contains("malformed response", error.Message, StringComparison.Ordinal);
        Assert.False(client.IsOpen);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.RawCommandAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty));
    }

    [Theory]
    [InlineData(SlmpFrameType.Frame3E)]
    [InlineData(SlmpFrameType.Frame4E)]
    public async Task ShortUdpDatagramIsReportedAsMalformedAndInvalidatesTransport(SlmpFrameType frameType)
    {
        await using var server = new ScriptedSlmpServer(
            SlmpTransportMode.Udp,
            frameType,
            async (request, send, _) =>
            {
                var headerSize = frameType == SlmpFrameType.Frame4E ? 13 : 9;
                var response = BuildResponse(request, frameType);
                await send(response.AsSpan(0, headerSize - 1).ToArray()).ConfigureAwait(false);
            });
        await server.StartAsync();
        using var client = CreateClient(server.Port, SlmpTransportMode.Udp, frameType, TimeSpan.FromMilliseconds(500));

        var error = await Assert.ThrowsAsync<SlmpError>(
            () => client.RawCommandAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty));

        Assert.Contains("malformed response", error.Message, StringComparison.Ordinal);
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData(SlmpTransportMode.Tcp)]
    [InlineData(SlmpTransportMode.Udp)]
    public async Task NonZero4EReservedBytesAreMalformedAndInvalidateTransport(SlmpTransportMode transport)
    {
        await using var server = new ScriptedSlmpServer(
            transport,
            SlmpFrameType.Frame4E,
            async (request, send, _) =>
            {
                var response = BuildResponse(request, SlmpFrameType.Frame4E);
                response[4] = 1;
                await send(response).ConfigureAwait(false);
            });
        await server.StartAsync();
        using var client = CreateClient(server.Port, transport, SlmpFrameType.Frame4E, TimeSpan.FromMilliseconds(500));

        var error = await Assert.ThrowsAsync<SlmpError>(
            () => client.RawCommandAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty));

        Assert.Contains("malformed response", error.Message, StringComparison.Ordinal);
        Assert.False(client.IsOpen);
    }

    private static SlmpClient CreateClient(
        int port,
        SlmpTransportMode transport,
        SlmpFrameType frameType,
        TimeSpan timeout)
        => new(
            "127.0.0.1",
            frameType == SlmpFrameType.Frame4E ? SlmpPlcProfile.IqR : SlmpPlcProfile.IqF,
            port,
            transport,
            TestTarget)
        {
            Timeout = timeout,
        };

    private static byte[] BuildResponse(
        ReadOnlySpan<byte> request,
        SlmpFrameType frameType,
        RouteField? foreignField = null,
        ushort? serial = null,
        ReadOnlySpan<byte> payload = default)
    {
        var headerSize = frameType == SlmpFrameType.Frame4E ? 13 : 9;
        var requestRouteOffset = frameType == SlmpFrameType.Frame4E ? 6 : 2;
        var responseRouteOffset = requestRouteOffset;
        var response = new byte[headerSize + 2 + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(
            response.AsSpan(0, 2),
            frameType == SlmpFrameType.Frame4E ? (ushort)0x00D4 : (ushort)0x00D0);
        if (frameType == SlmpFrameType.Frame4E)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                response.AsSpan(2, 2),
                serial ?? BinaryPrimitives.ReadUInt16LittleEndian(request.Slice(2, 2)));
        }
        request.Slice(requestRouteOffset, 5).CopyTo(response.AsSpan(responseRouteOffset, 5));
        MutateRoute(response, responseRouteOffset, foreignField);
        BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(headerSize - 2, 2), checked((ushort)(2 + payload.Length)));
        payload.CopyTo(response.AsSpan(headerSize + 2));
        return response;
    }

    private static byte[] BuildMalformedResponse(ReadOnlySpan<byte> request, SlmpFrameType frameType)
    {
        var headerSize = frameType == SlmpFrameType.Frame4E ? 13 : 9;
        var requestRouteOffset = frameType == SlmpFrameType.Frame4E ? 6 : 2;
        var response = new byte[headerSize + 1];
        BinaryPrimitives.WriteUInt16LittleEndian(
            response.AsSpan(0, 2),
            frameType == SlmpFrameType.Frame4E ? (ushort)0x00D4 : (ushort)0x00D0);
        if (frameType == SlmpFrameType.Frame4E)
        {
            request.Slice(2, 2).CopyTo(response.AsSpan(2, 2));
        }
        request.Slice(requestRouteOffset, 5).CopyTo(response.AsSpan(requestRouteOffset, 5));
        BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(headerSize - 2, 2), 1);
        return response;
    }

    private static void MutateRoute(byte[] response, int routeOffset, RouteField? field)
    {
        switch (field)
        {
            case RouteField.Network:
                response[routeOffset]++;
                break;
            case RouteField.Station:
                response[routeOffset + 1]++;
                break;
            case RouteField.ModuleIo:
                var moduleIo = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(routeOffset + 2, 2));
                BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(routeOffset + 2, 2), unchecked((ushort)(moduleIo + 1)));
                break;
            case RouteField.Multidrop:
                response[routeOffset + 4]++;
                break;
        }
    }

    public enum RouteField
    {
        Network,
        Station,
        ModuleIo,
        Multidrop,
    }

    private sealed class ScriptedSlmpServer : IAsyncDisposable
    {
        private readonly SlmpTransportMode _transport;
        private readonly SlmpFrameType _frameType;
        private readonly Func<byte[], Func<byte[], Task>, CancellationToken, Task> _script;
        private readonly bool _splitTcpResponses;
        private readonly TimeSpan _splitTcpDelay;
        private readonly int _exchangeCount;
        private readonly CancellationTokenSource _stopping = new();
        private TcpListener? _tcpListener;
        private UdpClient? _udp;
        private Task? _runTask;

        public ScriptedSlmpServer(
            SlmpTransportMode transport,
            SlmpFrameType frameType,
            Func<byte[], Func<byte[], Task>, CancellationToken, Task> script,
            bool splitTcpResponses = false,
            TimeSpan? splitTcpDelay = null,
            int exchangeCount = 1)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(exchangeCount, 1);

            _transport = transport;
            _frameType = frameType;
            _script = script;
            _splitTcpResponses = splitTcpResponses;
            _splitTcpDelay = splitTcpDelay ?? TimeSpan.FromMilliseconds(10);
            _exchangeCount = exchangeCount;
        }

        public int Port { get; private set; }

        public Task StartAsync()
        {
            if (_transport == SlmpTransportMode.Tcp)
            {
                _tcpListener = new TcpListener(IPAddress.Loopback, 0);
                _tcpListener.Start();
                Port = ((IPEndPoint)_tcpListener.LocalEndpoint).Port;
                _runTask = RunTcpAsync();
            }
            else
            {
                _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                Port = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
                _runTask = RunUdpAsync();
            }
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _stopping.Cancel();
            _tcpListener?.Stop();
            _udp?.Dispose();
            if (_runTask is not null)
            {
                await _runTask.ConfigureAwait(false);
            }
            _stopping.Dispose();
        }

        private async Task RunTcpAsync()
        {
            try
            {
                using var client = await _tcpListener!.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
                using var stream = client.GetStream();
                var request = await ReadTcpRequestAsync(stream, _frameType, _stopping.Token).ConfigureAwait(false);
                await _script(
                    request,
                    async response =>
                    {
                        if (_splitTcpResponses && response.Length > 1)
                        {
                            var split = response.Length - 1;
                            await stream.WriteAsync(response.AsMemory(0, split), _stopping.Token).ConfigureAwait(false);
                            await stream.FlushAsync(_stopping.Token).ConfigureAwait(false);
                            await Task.Delay(_splitTcpDelay, _stopping.Token).ConfigureAwait(false);
                            await stream.WriteAsync(response.AsMemory(split), _stopping.Token).ConfigureAwait(false);
                        }
                        else
                        {
                            await stream.WriteAsync(response, _stopping.Token).ConfigureAwait(false);
                        }
                        await stream.FlushAsync(_stopping.Token).ConfigureAwait(false);
                    },
                    _stopping.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or IOException or ObjectDisposedException)
            {
            }
        }

        private async Task RunUdpAsync()
        {
            try
            {
                for (var exchange = 0; exchange < _exchangeCount; exchange++)
                {
                    var datagram = await _udp!.ReceiveAsync(_stopping.Token).ConfigureAwait(false);
                    await _script(
                        datagram.Buffer,
                        async response =>
                        {
                            await _udp.SendAsync(response, datagram.RemoteEndPoint, _stopping.Token).ConfigureAwait(false);
                        },
                        _stopping.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or IOException or ObjectDisposedException)
            {
            }
        }

        private static async Task<byte[]> ReadTcpRequestAsync(
            NetworkStream stream,
            SlmpFrameType frameType,
            CancellationToken cancellationToken)
        {
            var headerSize = frameType == SlmpFrameType.Frame4E ? 13 : 9;
            var header = await ReadExactAsync(stream, headerSize, cancellationToken).ConfigureAwait(false);
            var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(headerSize - 2, 2));
            var body = await ReadExactAsync(stream, bodyLength, cancellationToken).ConfigureAwait(false);
            return [.. header, .. body];
        }

        private static async Task<byte[]> ReadExactAsync(
            NetworkStream stream,
            int length,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[length];
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new IOException("Unexpected end of request stream.");
                }
                offset += read;
            }
            return buffer;
        }
    }
}
