using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpFrameVectorTests
{
    public static IEnumerable<object[]> RepositoryGoldenFrameCases()
    {
        yield return ["read_type_name", "melsec:iq-r", "{}", "54000000000000FFFF03000600100001010000", "513033554456435055202020202020203412"];
        yield return ["read_words", "melsec:iq-r", "{\"device\":\"D100\",\"points\":2}", "54000000000000FFFF03000E0010000104020064000000A8000200", "34127856"];
        yield return ["read_words", "melsec:iq-r", "{\"device\":\"RD524286\",\"points\":2}", "54000000000000FFFF03000E00100001040200FEFF07002C000200", "34127856"];
        yield return ["write_bits", "melsec:iq-r", "{\"device\":\"M101\",\"values\":[true]}", "54000000000000FFFF03000F00100001140300650000009000010010", ""];
        yield return ["read_random", "melsec:iq-r", "{\"word_devices\":[\"D100\",\"D101\"],\"dword_devices\":[\"D200\"]}", "54000000000000FFFF03001A00100003040200020164000000A80065000000A800C8000000A800", "1111222278563412"];
        yield return ["write_random_bits", "melsec:iq-r", "{\"bit_values\":[{\"device\":\"M100\",\"value\":true},{\"device\":\"Y20\",\"value\":false}]}", "54000000000000FFFF03001700100002140300026400000090000100200000009D000000", ""];
        yield return ["read_block", "melsec:iq-r", "{\"word_blocks\":[{\"device\":\"D300\",\"points\":2}],\"bit_blocks\":[{\"device\":\"M200\",\"points\":1}]}", "54000000000000FFFF0300180010000604020001012C010000A8000200C800000090000100", "341278560500"];
        yield return ["remote_password_unlock", "melsec:iq-r", "{\"password\":\"secret1\"}", "54000000000000FFFF03000F00100030160000070073656372657431", ""];
        yield return ["remote_password_unlock", "melsec:qnu:qj71e71-100", "{\"password\":\"1234\"}", "54000000000000FFFF03000C00100030160000040031323334", ""];
        yield return ["remote_reset", "melsec:iq-r", "{}", "54000000000000FFFF030008001000061000000100", ""];
    }

    [Theory]
    [MemberData(nameof(RepositoryGoldenFrameCases))]
    public async Task BasicOperations_MatchRepositoryOwnedFullFrames(
        string operation,
        string plcProfile,
        string argsJson,
        string expectedRequestHex,
        string responseDataHex)
    {
        await using var server = new SingleShotSlmpServer(Convert.FromHexString(responseDataHex));
        await server.StartAsync();
        var profile = plcProfile == "melsec:iq-r" ? SlmpPlcProfile.IqR : SlmpPlcProfile.QnUQj71E71100;
        using var client = new SlmpClient("127.0.0.1", profile, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
            MonitoringTimer = 0x0010,
        };
        using var args = JsonDocument.Parse(argsJson);

        await DispatchAsync(client, operation, args.RootElement);
        await server.WaitForRequestAsync();

        Assert.Equal(expectedRequestHex, Convert.ToHexString(server.RequestFrame));
    }

    [Theory]
    [InlineData(SlmpCompatibilityMode.Legacy, SlmpDeviceCode.D, 0u, "000000A8")]
    [InlineData(SlmpCompatibilityMode.Legacy, SlmpDeviceCode.M, 500u, "F4010090")]
    [InlineData(SlmpCompatibilityMode.Legacy, SlmpDeviceCode.X, 0x10u, "1000009C")]
    [InlineData(SlmpCompatibilityMode.Legacy, SlmpDeviceCode.RD, 524287u, "FFFF072C")]
    [InlineData(SlmpCompatibilityMode.Iqr, SlmpDeviceCode.D, 100u, "64000000A800")]
    [InlineData(SlmpCompatibilityMode.Iqr, SlmpDeviceCode.Y, 0x20u, "200000009D00")]
    [InlineData(SlmpCompatibilityMode.Iqr, SlmpDeviceCode.W, 0x1A0u, "A0010000B400")]
    [InlineData(SlmpCompatibilityMode.Iqr, SlmpDeviceCode.B, 0x10u, "10000000A000")]
    [InlineData(SlmpCompatibilityMode.Iqr, SlmpDeviceCode.TN, 5u, "05000000C200")]
    [InlineData(SlmpCompatibilityMode.Iqr, SlmpDeviceCode.SD, 0u, "00000000A900")]
    [InlineData(SlmpCompatibilityMode.Iqr, SlmpDeviceCode.RD, 0u, "000000002C00")]
    [InlineData(SlmpCompatibilityMode.Iqr, SlmpDeviceCode.RD, 524287u, "FFFF07002C00")]
    public void EncodeRawDeviceSpec_MatchesRepositoryOwnedVectors(
        SlmpCompatibilityMode compatibility,
        SlmpDeviceCode code,
        uint number,
        string expectedHex)
    {
        var destination = new byte[SlmpPayloads.DeviceSpecSize(compatibility)];
        SlmpPayloads.EncodeRawDeviceSpec(new SlmpRawDeviceAddress(code, number), destination, compatibility);
        Assert.Equal(expectedHex, Convert.ToHexString(destination));
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
                _ = await client.ReadTypeNameAsync();
                return;
            case "read_words":
                _ = await client.ReadWordsRawAsync(ParseDevice(args.GetProperty("device").GetString()!), (ushort)args.GetProperty("points").GetInt32());
                return;
            case "write_bits":
                await client.WriteBitsAsync(ParseDevice(args.GetProperty("device").GetString()!), args.GetProperty("values").EnumerateArray().Select(item => item.GetBoolean()).ToArray());
                return;
            case "read_random":
                _ = await client.ReadRandomAsync(
                    args.GetProperty("word_devices").EnumerateArray().Select(item => ParseDevice(item.GetString()!)).ToArray(),
                    args.GetProperty("dword_devices").EnumerateArray().Select(item => ParseDevice(item.GetString()!)).ToArray());
                return;
            case "write_random_bits":
                await client.WriteRandomBitsAsync(args.GetProperty("bit_values").EnumerateArray()
                    .Select(item => (ParseDevice(item.GetProperty("device").GetString()!), item.GetProperty("value").GetBoolean()))
                    .ToArray());
                return;
            case "read_block":
                _ = await client.ReadBlockAsync(
                    args.GetProperty("word_blocks").EnumerateArray().Select(item => new SlmpBlockRead(ParseDevice(item.GetProperty("device").GetString()!), (ushort)item.GetProperty("points").GetInt32())).ToArray(),
                    args.GetProperty("bit_blocks").EnumerateArray().Select(item => new SlmpBlockRead(ParseDevice(item.GetProperty("device").GetString()!), (ushort)item.GetProperty("points").GetInt32())).ToArray());
                return;
            case "remote_password_unlock":
                await client.RemotePasswordUnlockAsync(args.GetProperty("password").GetString()!);
                return;
            case "remote_reset":
                await client.RemoteResetAsync();
                return;
            default:
                throw new InvalidOperationException($"Unknown repository frame case: {operation}");
        }
    }

    private static SlmpDeviceAddress ParseDevice(string text) => SlmpDeviceParser.Parse(text, SlmpPlcProfile.IqR);

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
