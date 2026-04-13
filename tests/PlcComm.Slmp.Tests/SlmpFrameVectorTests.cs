using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpFrameVectorTests
{
    public static IEnumerable<object[]> FrameCases()
    {
        using var doc = SharedSpecLoader.Load("frame_golden_vectors.json");
        foreach (var entry in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (!Supports(entry, "dotnet"))
            {
                continue;
            }

            yield return [
                entry.GetProperty("id").GetString()!,
                entry.GetProperty("operation").GetString()!,
                entry.TryGetProperty("args", out var args) ? args.GetRawText() : "{}",
                entry.GetProperty("request_hex").GetString()!,
                entry.GetProperty("response_data_hex").GetString()!,
            ];
        }
    }

    [Theory]
    [MemberData(nameof(FrameCases))]
    public async Task Requests_MatchSharedGoldenFrames(
        string id,
        string operation,
        string argsJson,
        string expectedRequestHex,
        string responseDataHex)
    {
        Assert.False(string.IsNullOrWhiteSpace(id));
        await using var server = new SingleShotSlmpServer(Convert.FromHexString(responseDataHex));
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", server.Port)
        {
            CompatibilityMode = SlmpCompatibilityMode.Iqr,
            MonitoringTimer = 0x0010,
        };

        byte[]? capturedSend = null;
        client.TraceHook = frame =>
        {
            if (frame.Direction == SlmpTraceDirection.Send)
            {
                capturedSend = frame.Data;
            }
        };

        using var args = JsonDocument.Parse(argsJson);
        await DispatchAsync(client, operation, args.RootElement);

        Assert.NotNull(capturedSend);
        Assert.Equal(expectedRequestHex, Convert.ToHexString(capturedSend));
        Assert.Equal(expectedRequestHex, Convert.ToHexString(server.RequestFrame));
    }

    private static async Task DispatchAsync(SlmpClient client, string operation, JsonElement args)
    {
        switch (operation)
        {
            case "read_type_name":
                {
                    var info = await client.ReadTypeNameAsync();
                    Assert.Equal("Q03UDVCPU", info.Model);
                    return;
                }
            case "read_words":
                {
                    var values = await client.ReadWordsRawAsync(ParseDevice(args.GetProperty("device").GetString()!), (ushort)args.GetProperty("points").GetInt32());
                    Assert.Equal(new ushort[] { 0x1234, 0x5678 }, values);
                    return;
                }
            case "write_bits":
                {
                    var values = args.GetProperty("values").EnumerateArray().Select(item => item.GetBoolean()).ToArray();
                    await client.WriteBitsAsync(ParseDevice(args.GetProperty("device").GetString()!), values);
                    return;
                }
            case "read_random":
                {
                    var wordDevices = args.GetProperty("word_devices").EnumerateArray().Select(item => ParseDevice(item.GetString()!)).ToArray();
                    var dwordDevices = args.GetProperty("dword_devices").EnumerateArray().Select(item => ParseDevice(item.GetString()!)).ToArray();
                    var result = await client.ReadRandomAsync(wordDevices, dwordDevices);
                    Assert.Equal((ushort)0x1111, result.WordValues[0]);
                    Assert.Equal((ushort)0x2222, result.WordValues[1]);
                    Assert.Equal((uint)0x12345678, result.DwordValues[0]);
                    return;
                }
            case "write_random_bits":
                {
                    var entries = args.GetProperty("bit_values").EnumerateArray()
                        .Select(item => (ParseDevice(item.GetProperty("device").GetString()!), item.GetProperty("value").GetBoolean()))
                        .ToArray();
                    await client.WriteRandomBitsAsync(entries);
                    return;
                }
            case "read_block":
                {
                    var wordBlocks = args.GetProperty("word_blocks").EnumerateArray()
                        .Select(item => new SlmpBlockRead(ParseDevice(item.GetProperty("device").GetString()!), (ushort)item.GetProperty("points").GetInt32()))
                        .ToArray();
                    var bitBlocks = args.GetProperty("bit_blocks").EnumerateArray()
                        .Select(item => new SlmpBlockRead(ParseDevice(item.GetProperty("device").GetString()!), (ushort)item.GetProperty("points").GetInt32()))
                        .ToArray();
                    var result = await client.ReadBlockAsync(wordBlocks, bitBlocks);
                    Assert.Equal(new ushort[] { 0x1234, 0x5678 }, result.WordValues);
                    Assert.Equal(new ushort[] { 0x0005 }, result.BitWordValues);
                    return;
                }
            case "remote_password_unlock":
                await client.RemotePasswordUnlockAsync(args.GetProperty("password").GetString()!);
                return;
            default:
                throw new InvalidOperationException($"Unsupported shared frame operation: {operation}");
        }
    }

    private static SlmpDeviceAddress ParseDevice(string text) => SlmpDeviceParser.Parse(text);

    private static bool Supports(JsonElement entry, string implementation)
        => entry.GetProperty("implementations").EnumerateArray()
            .Any(item => string.Equals(item.GetString(), implementation, StringComparison.Ordinal));

    private sealed class SingleShotSlmpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly byte[] _responseData;
        private Task? _serverTask;

        public SingleShotSlmpServer(byte[] responseData)
        {
            _responseData = responseData;
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public byte[] RequestFrame { get; private set; } = [];

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
