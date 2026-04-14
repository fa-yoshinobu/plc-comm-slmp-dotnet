using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpDeviceRangeCatalogTests
{
    [Theory]
    [InlineData(" R120PCPU\0 ", "R120PCPU")]
    [InlineData("fx5u-32mr/ds", "FX5U-32MR/DS")]
    public void NormalizeModel_TrimsAndUppercases(string input, string expected)
    {
        Assert.Equal(expected, SlmpDeviceRangeResolver.NormalizeModel(input));
    }

    [Fact]
    public void ResolveFamily_UsesModelCodeAndModelPrefixRules()
    {
        var qnUdv = SlmpDeviceRangeResolver.ResolveFamily(new SlmpTypeNameInfo("Q03UDVCPU", 0x0366, true));
        var mxf = SlmpDeviceRangeResolver.ResolveFamily(new SlmpTypeNameInfo("MXF100-8-N32", 0, false));

        Assert.Equal(SlmpDeviceRangeFamily.QnUDV, qnUdv);
        Assert.Equal(SlmpDeviceRangeFamily.MxF, mxf);
    }

    [Fact]
    public void BuildCatalog_QCpuClipsAndLeavesConditionalBoundsOpen()
    {
        var typeInfo = new SlmpTypeNameInfo("Q00CPU", 0x0251, true);
        var profile = SlmpDeviceRangeResolver.ResolveProfile(typeInfo);
        var registers = CreateRegisterSnapshot(profile);
        registers[290] = 123;
        registers[292] = 50000;
        registers[299] = 90;
        registers[302] = 50000;
        registers[303] = 60000;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(typeInfo, profile, registers);

        Assert.Equal(SlmpDeviceRangeFamily.QCpu, catalog.Family);
        Assert.Equal(123u, GetEntry(catalog, "X").PointCount);
        Assert.Equal(122u, GetEntry(catalog, "X").UpperBound);
        Assert.Equal("X000-X07A", GetEntry(catalog, "X").AddressRange);
        Assert.Equal(32768u, GetEntry(catalog, "M").PointCount);
        Assert.Equal(32767u, GetEntry(catalog, "M").UpperBound);
        Assert.Equal(32768u, GetEntry(catalog, "D").PointCount);
        Assert.Equal(32767u, GetEntry(catalog, "D").UpperBound);
        Assert.Equal(90u, GetEntry(catalog, "TS").PointCount);
        Assert.Equal(89u, GetEntry(catalog, "TS").UpperBound);
        Assert.Equal(90u, GetEntry(catalog, "TN").PointCount);
        Assert.Equal(89u, GetEntry(catalog, "TN").UpperBound);
        Assert.True(GetEntry(catalog, "ZR").Supported);
        Assert.Null(GetEntry(catalog, "ZR").PointCount);
        Assert.Null(GetEntry(catalog, "ZR").UpperBound);
        Assert.Null(GetEntry(catalog, "ZR").AddressRange);
        Assert.Equal(10u, GetEntry(catalog, "Z").PointCount);
        Assert.Equal(9u, GetEntry(catalog, "Z").UpperBound);
        Assert.Equal("Z0-Z9", GetEntry(catalog, "Z").AddressRange);
    }

    [Fact]
    public void BuildCatalog_IqRReadsDwordRegistersAndExpandsLongFamilies()
    {
        var typeInfo = new SlmpTypeNameInfo("R120PCPU", 0x4844, true);
        var profile = SlmpDeviceRangeResolver.ResolveProfile(typeInfo);
        var registers = CreateRegisterSnapshot(profile);
        registers[260] = 0x5678;
        registers[261] = 0x1234;
        registers[294] = 0x4321;
        registers[295] = 0x0001;
        registers[306] = 0x0001;
        registers[307] = 0x0002;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(typeInfo, profile, registers);

        Assert.Equal(0x12345678u, GetEntry(catalog, "X").PointCount);
        Assert.Equal(0x12345677u, GetEntry(catalog, "X").UpperBound);
        Assert.Equal("X00000000-X12345677", GetEntry(catalog, "X").AddressRange);
        Assert.Equal(0x00014321u, GetEntry(catalog, "LTN").PointCount);
        Assert.Equal(0x00014320u, GetEntry(catalog, "LTN").UpperBound);
        Assert.Equal(0x00014321u, GetEntry(catalog, "LTS").PointCount);
        Assert.Equal(0x00014320u, GetEntry(catalog, "LTS").UpperBound);
        Assert.Equal(32768u, GetEntry(catalog, "R").PointCount);
        Assert.Equal(32767u, GetEntry(catalog, "R").UpperBound);
        Assert.Equal("R0-R32767", GetEntry(catalog, "R").AddressRange);
    }

    [Fact]
    public void BuildCatalog_IqFFormatsXAndYInOctal()
    {
        var typeInfo = new SlmpTypeNameInfo("FX5UC-32MT/D", 0x4A91, true);
        var profile = SlmpDeviceRangeResolver.ResolveProfile(typeInfo);
        var registers = CreateRegisterSnapshot(profile);
        registers[260] = 1024;
        registers[261] = 0;
        registers[262] = 1024;
        registers[263] = 0;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(typeInfo, profile, registers);

        Assert.Equal(SlmpDeviceRangeNotation.Base8, GetEntry(catalog, "X").Notation);
        Assert.Equal(1024u, GetEntry(catalog, "X").PointCount);
        Assert.Equal(1023u, GetEntry(catalog, "X").UpperBound);
        Assert.Equal("X0000-X1777", GetEntry(catalog, "X").AddressRange);

        Assert.Equal(SlmpDeviceRangeNotation.Base8, GetEntry(catalog, "Y").Notation);
        Assert.Equal(1024u, GetEntry(catalog, "Y").PointCount);
        Assert.Equal(1023u, GetEntry(catalog, "Y").UpperBound);
        Assert.Equal("Y0000-Y1777", GetEntry(catalog, "Y").AddressRange);
    }

    [Fact]
    public void BuildCatalog_QnUUsesSd300ForStFamilyAndSd305ForZ()
    {
        var typeInfo = new SlmpTypeNameInfo("Q03UDECPU", 0x0268, true);
        var profile = SlmpDeviceRangeResolver.ResolveProfile(typeInfo);
        var registers = CreateRegisterSnapshot(profile);
        registers[300] = 16;
        registers[301] = 1024;
        registers[305] = 20;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(typeInfo, profile, registers);

        Assert.Equal(SlmpDeviceRangeFamily.QnU, catalog.Family);
        Assert.Equal(16u, GetEntry(catalog, "STS").PointCount);
        Assert.Equal(15u, GetEntry(catalog, "STS").UpperBound);
        Assert.Equal("STS0-STS15", GetEntry(catalog, "STS").AddressRange);
        Assert.Equal(16u, GetEntry(catalog, "STC").PointCount);
        Assert.Equal(15u, GetEntry(catalog, "STC").UpperBound);
        Assert.Equal("STC0-STC15", GetEntry(catalog, "STC").AddressRange);
        Assert.Equal(16u, GetEntry(catalog, "STN").PointCount);
        Assert.Equal(15u, GetEntry(catalog, "STN").UpperBound);
        Assert.Equal("STN0-STN15", GetEntry(catalog, "STN").AddressRange);
        Assert.Equal(1024u, GetEntry(catalog, "CS").PointCount);
        Assert.Equal(1023u, GetEntry(catalog, "CS").UpperBound);
        Assert.Equal("CS0-CS1023", GetEntry(catalog, "CS").AddressRange);
        Assert.Equal(20u, GetEntry(catalog, "Z").PointCount);
        Assert.Equal(19u, GetEntry(catalog, "Z").UpperBound);
        Assert.Equal("Z0-Z19", GetEntry(catalog, "Z").AddressRange);
    }

    [Fact]
    public async Task ReadDeviceRangeCatalogAsync_WithoutConfiguredFamily_Throws()
    {
        await using var server = new MultiResponseSlmpServer([]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", server.Port)
        {
            CompatibilityMode = SlmpCompatibilityMode.Iqr,
            MonitoringTimer = 0x0010,
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadDeviceRangeCatalogAsync());
        Assert.Contains("requires an explicit PlcFamily", error.Message, StringComparison.Ordinal);
        Assert.Empty(server.RequestFrames);
    }

    [Fact]
    public async Task ReadDeviceRangeCatalogAsync_WithSelectedFamily_UsesOnlyFamilySpecificSdWindow()
    {
        var sdValues = new ushort[46];
        sdValues[0] = 1024;
        sdValues[2] = 1024;
        sdValues[4] = 7680;
        sdValues[10] = 8000;
        sdValues[20] = 10000;
        sdValues[22] = 12000;

        await using var server = new MultiResponseSlmpServer(
        [
            BuildWordPayload(sdValues),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", server.Port)
        {
            CompatibilityMode = SlmpCompatibilityMode.Iqr,
            FrameType = SlmpFrameType.Frame4E,
            MonitoringTimer = 0x0010,
        };

        var catalog = await client.ReadDeviceRangeCatalogAsync(SlmpDeviceRangeFamily.IqF);

        Assert.Single(server.RequestFrames);
        Assert.False(catalog.HasModelCode);
        Assert.Equal("IQ-F", catalog.Model);
        Assert.Equal(SlmpDeviceRangeFamily.IqF, catalog.Family);
        Assert.Equal("X0000-X1777", GetEntry(catalog, "X").AddressRange);
        Assert.Equal("D0-D9999", GetEntry(catalog, "D").AddressRange);
        Assert.Equal("SD0-SD11999", GetEntry(catalog, "SD").AddressRange);
    }

    [Fact]
    public async Task ReadCpuOperationStateAsync_ReadsSd203AndMasksUpperBits()
    {
        await using var server = new MultiResponseSlmpServer(
        [
            BuildWordPayload(new ushort[] { 0x00A2 }),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", server.Port)
        {
            CompatibilityMode = SlmpCompatibilityMode.Iqr,
            FrameType = SlmpFrameType.Frame4E,
            MonitoringTimer = 0x0010,
        };

        var state = await client.ReadCpuOperationStateAsync();

        Assert.Single(server.RequestFrames);
        Assert.Equal(SlmpCpuOperationStatus.Stop, state.Status);
        Assert.Equal((ushort)0x00A2, state.RawStatusWord);
        Assert.Equal((byte)0x02, state.RawCode);
    }

    [Fact]
    public async Task ReadCpuOperationStateAsync_ReturnsUnknownForUnhandledCode()
    {
        await using var server = new MultiResponseSlmpServer(
        [
            BuildWordPayload(new ushort[] { 0x00F5 }),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", server.Port)
        {
            CompatibilityMode = SlmpCompatibilityMode.Iqr,
            FrameType = SlmpFrameType.Frame4E,
            MonitoringTimer = 0x0010,
        };

        var state = await client.ReadCpuOperationStateAsync();

        Assert.Equal(SlmpCpuOperationStatus.Unknown, state.Status);
        Assert.Equal((ushort)0x00F5, state.RawStatusWord);
        Assert.Equal((byte)0x05, state.RawCode);
    }

    [Fact]
    public async Task ReadDeviceRangeCatalogWithThreeELegacyFallbackAsync_FallsBackWhenReadTypeNameDoesNotReturn()
    {
        var typePayload = BuildTypeNamePayload("FX5UC-32MT/D", 0x4A91);
        var sdValues = new ushort[46];
        sdValues[0] = 1024;
        sdValues[2] = 1024;
        sdValues[4] = 7680;
        sdValues[10] = 8000;
        sdValues[20] = 10000;
        sdValues[22] = 12000;

        await using var server = new FallbackDeviceRangeServer(typePayload, BuildWordPayload(sdValues));
        await server.StartAsync();

        var resolved = await SlmpConnectionProfileProbe.ReadDeviceRangeCatalogWithThreeELegacyFallbackAsync(
            new SlmpConnectionOptions("127.0.0.1", SlmpPlcFamily.IqR)
            {
                Port = server.Port,
                Timeout = TimeSpan.FromSeconds(1),
            });

        Assert.True(resolved.UsedThreeELegacyFallback);
        Assert.Equal(SlmpFrameType.Frame3E, resolved.FrameType);
        Assert.Equal(SlmpCompatibilityMode.Legacy, resolved.CompatibilityMode);
        Assert.Equal(SlmpDeviceRangeFamily.IqF, resolved.Catalog.Family);
        Assert.Equal("FX5UC-32MT/D", resolved.Catalog.Model);
        Assert.Equal("X0000-X1777", GetEntry(resolved.Catalog, "X").AddressRange);
    }

    private static Dictionary<int, ushort> CreateRegisterSnapshot(SlmpDeviceRangeProfile profile)
    {
        var snapshot = new Dictionary<int, ushort>(profile.RegisterCount);
        for (var i = 0; i < profile.RegisterCount; i++)
        {
            snapshot[profile.RegisterStart + i] = 0;
        }

        return snapshot;
    }

    private static SlmpDeviceRangeEntry GetEntry(SlmpDeviceRangeCatalog catalog, string device)
        => Assert.Single(catalog.Entries, entry => entry.Device == device);

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

    private sealed class MultiResponseSlmpServer : IAsyncDisposable
    {
        private readonly Queue<byte[]> _responsePayloads;
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private Task? _serverTask;

        public MultiResponseSlmpServer(IEnumerable<byte[]> responsePayloads)
        {
            _responsePayloads = new Queue<byte[]>(responsePayloads);
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

                while (_responsePayloads.Count > 0)
                {
                    var head = await ReadExactAsync(stream, 13).ConfigureAwait(false);
                    var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(11, 2));
                    var body = await ReadExactAsync(stream, bodyLength).ConfigureAwait(false);
                    var request = new byte[head.Length + body.Length];
                    Buffer.BlockCopy(head, 0, request, 0, head.Length);
                    Buffer.BlockCopy(body, 0, request, head.Length, body.Length);
                    RequestFrames.Add(request);

                    var response = Build4EResponse(request, _responsePayloads.Dequeue());
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

    private sealed class FallbackDeviceRangeServer : IAsyncDisposable
    {
        private readonly byte[] _typePayload;
        private readonly byte[] _sdPayload;
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private Task? _serverTask;

        public FallbackDeviceRangeServer(byte[] typePayload, byte[] sdPayload)
        {
            _typePayload = typePayload;
            _sdPayload = sdPayload;
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
                using (var first = await _listener.AcceptTcpClientAsync().ConfigureAwait(false))
                {
                    first.Close();
                }

                using var second = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using var stream = second.GetStream();

                var typeRequest = await Read3ERequestAsync(stream).ConfigureAwait(false);
                await stream.WriteAsync(Build3EResponse(typeRequest, _typePayload)).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);

                var sdRequest = await Read3ERequestAsync(stream).ConfigureAwait(false);
                await stream.WriteAsync(Build3EResponse(sdRequest, _sdPayload)).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
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

        private static async Task<byte[]> Read3ERequestAsync(NetworkStream stream)
        {
            var head = await ReadExactLocalAsync(stream, 15).ConfigureAwait(false);
            var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(7, 2)) - 6;
            var body = await ReadExactLocalAsync(stream, bodyLength).ConfigureAwait(false);
            var request = new byte[head.Length + body.Length];
            Buffer.BlockCopy(head, 0, request, 0, head.Length);
            Buffer.BlockCopy(body, 0, request, head.Length, body.Length);
            return request;
        }

        private static byte[] Build3EResponse(ReadOnlySpan<byte> request, ReadOnlySpan<byte> responsePayload)
        {
            var payload = new byte[2 + responsePayload.Length];
            responsePayload.CopyTo(payload.AsSpan(2));

            var response = new byte[9 + payload.Length];
            response[0] = 0xD0;
            response[1] = 0x00;
            request.Slice(2, 5).CopyTo(response.AsSpan(2));
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(7, 2), checked((ushort)payload.Length));
            payload.CopyTo(response.AsSpan(9));
            return response;
        }

        private static async Task<byte[]> ReadExactLocalAsync(NetworkStream stream, int size)
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
