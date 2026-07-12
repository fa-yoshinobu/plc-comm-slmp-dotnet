using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpFrameVectorTests
{
    [Theory]
    [InlineData(false, 10u, SlmpDeviceCode.LTN)]
    [InlineData(true, 0u, SlmpDeviceCode.LSTN)]
    public async Task LongTimerHelpers_EncodeExplicitHeadFamilyAndCount(
        bool retentive,
        uint expectedHead,
        SlmpDeviceCode expectedCode)
    {
        await using var server = new SingleShotSlmpServer([0x34, 0x12, 0x01, 0x00, 0x03, 0x00, 0x00, 0x00]);
        await server.StartAsync();
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            server.Port,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);

        var result = retentive
            ? await client.ReadLongRetentiveTimerAsync(checked((int)expectedHead), 1)
            : await client.ReadLongTimerAsync(checked((int)expectedHead), 1);

        Assert.Single(result);
        await server.WaitForRequestAsync();
        Assert.Equal(expectedHead, BinaryPrimitives.ReadUInt32LittleEndian(server.RequestFrame.AsSpan(19, 4)));
        Assert.Equal((ushort)expectedCode, BinaryPrimitives.ReadUInt16LittleEndian(server.RequestFrame.AsSpan(23, 2)));
        Assert.Equal((ushort)4, BinaryPrimitives.ReadUInt16LittleEndian(server.RequestFrame.AsSpan(25, 2)));
    }

    private sealed class SingleShotSlmpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly byte[] _responseData;
        private readonly TaskCompletionSource _requestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task? _serverTask;

        public SingleShotSlmpServer(byte[] responseData)
        {
            _responseData = responseData;
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public byte[] RequestFrame { get; private set; } = [];

        public Task WaitForRequestAsync() => _requestReceived.Task;

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
                RequestFrame = [.. head, .. body];
                _requestReceived.TrySetResult();
                var response = Build4EResponse(RequestFrame, _responseData);
                await stream.WriteAsync(response).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static byte[] Build4EResponse(ReadOnlySpan<byte> request, ReadOnlySpan<byte> responseData, ushort endCode = 0)
        {
            var payload = new byte[2 + responseData.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(payload, endCode);
            responseData.CopyTo(payload.AsSpan(2));

            var response = new byte[13 + payload.Length];
            response[0] = 0xD4;
            response[1] = 0x00;
            request.Slice(2, 2).CopyTo(response.AsSpan(2));
            response[4] = 0x00;
            response[5] = 0x00;
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
}
