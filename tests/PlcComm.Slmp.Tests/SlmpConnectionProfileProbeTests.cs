using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpConnectionProfileProbeTests
{
    [Fact]
    public async Task ProbeProfileAsync_ReturnsValidatedWhenTypeNameAndSdReadSucceed()
    {
        await using var server = new ProbeServer(
        [
            new ProbeResponse(BuildTypeNamePayload("R120PCPU", 0x4844)),
            new ProbeResponse(BuildWordPayload(new ushort[50])),
        ]);
        await server.StartAsync();

        var options = new SlmpConnectionOptions("127.0.0.1", SlmpPlcFamily.IqR)
        {
            Port = server.Port,
        };

        var result = await SlmpConnectionProfileProbe.ProbeProfileAsync(
            options,
            SlmpFrameType.Frame4E,
            SlmpCompatibilityMode.Iqr,
            CancellationToken.None);

        Assert.Equal(SlmpConnectionProfileProbeStatus.Validated, result.Status);
        Assert.True(result.SdReadSucceeded);
        Assert.NotNull(result.TypeNameInfo);
        Assert.Equal("R120PCPU", result.TypeNameInfo!.Model);
        Assert.Equal(0x4844, result.TypeNameInfo.ModelCode);
        Assert.Equal(SlmpDeviceRangeFamily.IqR, result.Family);
        Assert.Equal(260, result.SdRegisterStart);
        Assert.Equal(50, result.SdRegisterCount);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ProbeProfileAsync_ReturnsTypeNameOnlyWhenSdReadFails()
    {
        await using var server = new ProbeServer(
        [
            new ProbeResponse(BuildTypeNamePayload("FX5UC-32MT/D", 0x4A91)),
            new ProbeResponse([], 0xC059),
        ]);
        await server.StartAsync();

        var options = new SlmpConnectionOptions("127.0.0.1", SlmpPlcFamily.IqR)
        {
            Port = server.Port,
        };

        var result = await SlmpConnectionProfileProbe.ProbeProfileAsync(
            options,
            SlmpFrameType.Frame4E,
            SlmpCompatibilityMode.Iqr,
            CancellationToken.None);

        Assert.Equal(SlmpConnectionProfileProbeStatus.TypeNameOnly, result.Status);
        Assert.False(result.SdReadSucceeded);
        Assert.NotNull(result.TypeNameInfo);
        Assert.Equal("FX5UC-32MT/D", result.TypeNameInfo!.Model);
        Assert.Equal(0x4A91, result.TypeNameInfo.ModelCode);
        Assert.Equal(SlmpDeviceRangeFamily.IqF, result.Family);
        Assert.Equal(260, result.SdRegisterStart);
        Assert.Equal(46, result.SdRegisterCount);
        Assert.Contains("frame_support:", result.ErrorMessage);
    }

    private static byte[] BuildTypeNamePayload(string model, ushort modelCode)
    {
        var payload = new byte[18];
        Encoding.ASCII.GetBytes(model, payload);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(16, 2), modelCode);
        return payload;
    }

    private static byte[] BuildWordPayload(ushort[] values)
    {
        var payload = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(i * 2, 2), values[i]);
        }

        return payload;
    }

    private sealed record ProbeResponse(byte[] Payload, ushort EndCode = 0);

    private sealed class ProbeServer : IAsyncDisposable
    {
        private readonly Queue<ProbeResponse> _responses;
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private Task? _serverTask;

        public ProbeServer(IEnumerable<ProbeResponse> responses)
        {
            _responses = new Queue<ProbeResponse>(responses);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

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
                    var head = await ReadExactAsync(stream, 19).ConfigureAwait(false);
                    var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(11, 2)) - 6;
                    var body = await ReadExactAsync(stream, bodyLength).ConfigureAwait(false);
                    var request = new byte[head.Length + body.Length];
                    Buffer.BlockCopy(head, 0, request, 0, head.Length);
                    Buffer.BlockCopy(body, 0, request, head.Length, body.Length);

                    var response = Build4EResponse(request, _responses.Dequeue());
                    await stream.WriteAsync(response).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }
            }
            catch (SocketException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static byte[] Build4EResponse(ReadOnlySpan<byte> request, ProbeResponse responseInfo)
        {
            var payload = new byte[2 + responseInfo.Payload.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(payload, responseInfo.EndCode);
            responseInfo.Payload.CopyTo(payload.AsSpan(2));

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
