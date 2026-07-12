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
                entry.TryGetProperty("plc_profile", out var plcProfile) ? plcProfile.GetString()! : "melsec:iq-r",
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
        string plcProfile,
        string argsJson,
        string expectedRequestHex,
        string responseDataHex)
    {
        Assert.False(string.IsNullOrWhiteSpace(id));
        await using var server = new SingleShotSlmpServer(Convert.FromHexString(responseDataHex));
        await server.StartAsync();

        var profile = plcProfile switch
        {
            "melsec:iq-r" => SlmpPlcProfile.IqR,
            "melsec:qnu:qj71e71-100" => SlmpPlcProfile.QnUQj71E71100,
            _ => throw new InvalidOperationException($"Unsupported shared frame PLC profile: {plcProfile}"),
        };
        using var client = new SlmpClient("127.0.0.1", profile, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
            MonitoringTimer = 0x0010,
        };

        byte[]? capturedSend = null;
        client.MaintainerTraceHook = frame =>
        {
            if (frame.Direction == SlmpTraceDirection.Send)
            {
                capturedSend = frame.Data;
            }
        };

        using var args = JsonDocument.Parse(argsJson);
        await DispatchAsync(client, operation, args.RootElement);
        await server.WaitForRequestAsync();

        Assert.NotNull(capturedSend);
        Assert.Equal(expectedRequestHex, Convert.ToHexString(capturedSend));
        Assert.Equal(expectedRequestHex, Convert.ToHexString(server.RequestFrame));
    }

    [Theory]
    [InlineData(SlmpCpuModule.Cpu1, 0x03E0, false)]
    [InlineData(SlmpCpuModule.Cpu2, 0x03E1, false)]
    [InlineData(SlmpCpuModule.Cpu3, 0x03E2, false)]
    [InlineData(SlmpCpuModule.Cpu4, 0x03E3, false)]
    [InlineData(SlmpCpuModule.Cpu1, 0x03E0, true)]
    [InlineData(SlmpCpuModule.Cpu2, 0x03E1, true)]
    [InlineData(SlmpCpuModule.Cpu3, 0x03E2, true)]
    [InlineData(SlmpCpuModule.Cpu4, 0x03E3, true)]
    public async Task CpuBufferHelpers_EncodeExplicitModuleForReadAndWrite(
        SlmpCpuModule module,
        ushort expectedModuleIo,
        bool write)
    {
        await using var server = new SingleShotSlmpServer(write ? [] : [0x34, 0x12]);
        await server.StartAsync();
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            server.Port,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);

        if (write)
        {
            await client.CpuBufferWriteWordAsync(0, 0x1234, module);
        }
        else
        {
            Assert.Equal((ushort)0x1234, await client.CpuBufferReadWordAsync(0, module));
        }

        await server.WaitForRequestAsync();
        Assert.Equal(expectedModuleIo, BinaryPrimitives.ReadUInt16LittleEndian(server.RequestFrame.AsSpan(25, 2)));
    }

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
            case "remote_reset":
                {
                    await client.RemoteResetAsync();
                    return;
                }
            default:
                throw new InvalidOperationException($"Unsupported shared frame operation: {operation}");
        }
    }

    private static SlmpDeviceAddress ParseDevice(string text) => SlmpDeviceParser.Parse(text, SlmpPlcProfile.IqR);

    private static bool Supports(JsonElement entry, string implementation)
        => entry.GetProperty("implementations").EnumerateArray()
            .Any(item => string.Equals(item.GetString(), implementation, StringComparison.Ordinal));

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
