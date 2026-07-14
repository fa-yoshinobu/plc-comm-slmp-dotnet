using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpDeviceRangeCatalogTests
{
    [Fact]
    public void BuildCatalog_MxRRj71En71UsesMxRRulesAndUnitLabel()
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(SlmpPlcProfile.MxRRj71En71);
        var registers = CreateRegisterSnapshot(profile);
        registers[260] = 321;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(SlmpPlcProfile.MxRRj71En71, registers);

        Assert.Equal(SlmpPlcProfile.MxRRj71En71, catalog.PlcProfile);
        Assert.Equal("MX-R via RJ71EN71", catalog.Model);
        Assert.Equal(321u, GetEntry(catalog, "X").PointCount);
    }

    [Fact]
    public void BuildCatalog_QCpuClipsAndLeavesConditionalBoundsOpen()
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(SlmpPlcProfile.QCpu);
        var registers = CreateRegisterSnapshot(profile);
        registers[290] = 123;
        registers[292] = 50000;
        registers[299] = 90;
        registers[302] = 50000;
        registers[303] = 60000;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(SlmpPlcProfile.QCpu, registers);

        Assert.Equal(SlmpPlcProfile.QCpu, catalog.PlcProfile);
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
    public void BuildCatalog_QCpuUnitUsesBaseRulesButReportsUnitProfile()
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(SlmpPlcProfile.QCpuQj71E71100);
        var registers = CreateRegisterSnapshot(profile);
        registers[290] = 123;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(SlmpPlcProfile.QCpuQj71E71100, registers);

        Assert.Equal(SlmpPlcProfile.QCpuQj71E71100, catalog.PlcProfile);
        Assert.Equal("QCPU via QJ71E71-100", catalog.Model);
        Assert.Equal(123u, GetEntry(catalog, "X").PointCount);
        Assert.Equal("X000-X07A", GetEntry(catalog, "X").AddressRange);
    }

    [Fact]
    public void BuildCatalog_IqRReadsDwordRegistersAndExpandsLongFamilies()
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(SlmpPlcProfile.IqR);
        var registers = CreateRegisterSnapshot(profile);
        registers[260] = 0x5678;
        registers[261] = 0x1234;
        registers[294] = 0x4321;
        registers[295] = 0x0001;
        registers[306] = 0x0001;
        registers[307] = 0x0002;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(SlmpPlcProfile.IqR, registers);

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
    public void BuildCatalog_IqRUnitUsesIqRRulesButReportsUnitProfile()
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(SlmpPlcProfile.IqRRj71En71);
        var registers = CreateRegisterSnapshot(profile);
        registers[280] = 0x0034;
        registers[281] = 0x0001;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(SlmpPlcProfile.IqRRj71En71, registers);

        Assert.Equal(SlmpPlcProfile.IqRRj71En71, catalog.PlcProfile);
        Assert.Equal("iQ-R via RJ71EN71", catalog.Model);
        Assert.Equal(0x00010034u, GetEntry(catalog, "D").PointCount);
        Assert.Equal("D0-D65587", GetEntry(catalog, "D").AddressRange);
    }

    [Fact]
    public void BuildCatalog_IqLUsesOwnProfileWithIqLRangeRegisters()
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(SlmpPlcProfile.IqL);
        var registers = CreateRegisterSnapshot(profile);
        registers[280] = 18432;
        registers[281] = 0;
        registers[306] = 0;
        registers[307] = 12;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(SlmpPlcProfile.IqL, registers);

        Assert.Equal(SlmpPlcProfile.IqL, catalog.PlcProfile);
        Assert.Equal(18432u, GetEntry(catalog, "D").PointCount);
        Assert.Equal(18431u, GetEntry(catalog, "D").UpperBound);
        Assert.Equal("D0-D18431", GetEntry(catalog, "D").AddressRange);
        Assert.Equal(32768u, GetEntry(catalog, "R").PointCount);
        Assert.Equal(786432u, GetEntry(catalog, "ZR").PointCount);
        Assert.Equal(4096u, GetEntry(catalog, "SM").PointCount);
        Assert.Equal(4095u, GetEntry(catalog, "SM").UpperBound);
        Assert.Equal("SM0-SM4095", GetEntry(catalog, "SM").AddressRange);
        Assert.Equal(4096u, GetEntry(catalog, "SD").PointCount);
        Assert.Equal(4095u, GetEntry(catalog, "SD").UpperBound);
        Assert.Equal("SD0-SD4095", GetEntry(catalog, "SD").AddressRange);
    }

    [Fact]
    public void BuildCatalog_IqFFormatsXAndYInOctal()
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(SlmpPlcProfile.IqF);
        var registers = CreateRegisterSnapshot(profile);
        registers[260] = 1024;
        registers[261] = 0;
        registers[262] = 1024;
        registers[263] = 0;
        registers[276] = 256;
        registers[277] = 0;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(SlmpPlcProfile.IqF, registers);

        Assert.Equal(SlmpDeviceRangeNotation.Base8, GetEntry(catalog, "X").Notation);
        Assert.Equal(1024u, GetEntry(catalog, "X").PointCount);
        Assert.Equal(1023u, GetEntry(catalog, "X").UpperBound);
        Assert.Equal("X0000-X1777", GetEntry(catalog, "X").AddressRange);

        Assert.Equal(SlmpDeviceRangeNotation.Base8, GetEntry(catalog, "Y").Notation);
        Assert.Equal(1024u, GetEntry(catalog, "Y").PointCount);
        Assert.Equal(1023u, GetEntry(catalog, "Y").UpperBound);
        Assert.Equal("Y0000-Y1777", GetEntry(catalog, "Y").AddressRange);

        Assert.True(GetEntry(catalog, "S").Supported);
        Assert.Equal(256u, GetEntry(catalog, "S").PointCount);
        Assert.Equal("S0-S255", GetEntry(catalog, "S").AddressRange);
    }

    [Theory]
    [InlineData(SlmpPlcProfile.MxF)]
    [InlineData(SlmpPlcProfile.MxR)]
    public void BuildCatalog_MxProfilesKeepSSupportedFromSd276(SlmpPlcProfile family)
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(family);
        var registers = CreateRegisterSnapshot(profile);
        registers[276] = 123;
        registers[277] = 0;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(family, registers);

        Assert.True(GetEntry(catalog, "S").Supported);
        Assert.Equal("SD276-SD277 (32-bit)", GetEntry(catalog, "S").Source);
        Assert.Equal(123u, GetEntry(catalog, "S").PointCount);
        Assert.Equal("S0-S122", GetEntry(catalog, "S").AddressRange);
    }

    [Fact]
    public void BuildCatalog_MatchesCanonicalDeviceRangeRulesFixture()
    {
        using var document = LoadCanonicalDeviceRangeRules();
        var root = document.RootElement;
        var rows = root.GetProperty("rows");
        var profiles = root.GetProperty("profiles");
        var notationOverrides = root.GetProperty("notation_overrides");

        foreach (var profileProperty in profiles.EnumerateObject())
        {
            var plcProfile = SlmpPlcProfiles.ParseKnownProfileId(profileProperty.Name);
            foreach (var ruleProperty in profileProperty.Value.GetProperty("rules").EnumerateObject())
            {
                var catalog = SlmpDeviceRangeResolver.BuildCatalog(
                    plcProfile,
                    CreateCanonicalRegisterSnapshot(profileProperty.Value, ruleProperty.Name));
                var row = rows.GetProperty(ruleProperty.Name);
                var rule = ruleProperty.Value;
                var expectedSupported = rule.GetProperty("kind").GetString() != "unsupported";
                var expectedPointCount = CanonicalExpectedPointCount(rule);
                var expectedNotationText = row.GetProperty("notation").GetString();
                if (notationOverrides.TryGetProperty(profileProperty.Name, out var profileOverrides)
                    && profileOverrides.TryGetProperty(ruleProperty.Name, out var overrideValue))
                {
                    expectedNotationText = overrideValue.GetString();
                }

                foreach (var device in row.GetProperty("devices").EnumerateArray())
                {
                    var deviceName = device.GetProperty("device").GetString()!;
                    var entry = GetEntry(catalog, deviceName);
                    Assert.Equal(expectedSupported, entry.Supported);
                    Assert.Equal(expectedPointCount, entry.PointCount);
                    Assert.Equal(CanonicalNotation(expectedNotationText!), entry.Notation);
                }
            }
        }
    }

    [Fact]
    public void BuildCatalog_QnUUsesSd300ForStFamilyAndFixedZRange()
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(SlmpPlcProfile.QnU);
        var registers = CreateRegisterSnapshot(profile);
        registers[300] = 16;
        registers[301] = 1024;
        registers[305] = 65535;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(SlmpPlcProfile.QnU, registers);

        Assert.Equal(SlmpPlcProfile.QnU, catalog.PlcProfile);
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

    [Theory]
    [InlineData(SlmpPlcProfile.LCpu)]
    [InlineData(SlmpPlcProfile.QnUDV)]
    public void BuildCatalog_QSeriesDerivedFamiliesUseFixedZRange(SlmpPlcProfile family)
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(family);
        var registers = CreateRegisterSnapshot(profile);
        registers[305] = 65535;

        var catalog = SlmpDeviceRangeResolver.BuildCatalog(family, registers);

        Assert.Equal(20u, GetEntry(catalog, "Z").PointCount);
        Assert.Equal(19u, GetEntry(catalog, "Z").UpperBound);
        Assert.Equal("Z0-Z19", GetEntry(catalog, "Z").AddressRange);
        Assert.Equal("Fixed family limit", GetEntry(catalog, "Z").Source);
    }

    [Fact]
    public async Task ReadDeviceRangeCatalogAsync_WithSelectedProfile_UsesOnlyProfileSpecificSdWindow()
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

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqF, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
            MonitoringTimer = 0x0010,
        };

        var catalog = await client.ReadDeviceRangeCatalogAsync();

        Assert.Single(server.RequestFrames);
        Assert.False(catalog.HasModelCode);
        Assert.Equal("IQ-F", catalog.Model);
        Assert.Equal(SlmpPlcProfile.IqF, catalog.PlcProfile);
        Assert.Equal("X0000-X1777", GetEntry(catalog, "X").AddressRange);
        Assert.Equal("D0-D9999", GetEntry(catalog, "D").AddressRange);
        Assert.Equal("SD0-SD11999", GetEntry(catalog, "SD").AddressRange);
    }

    [Fact]
    public async Task ReadDeviceRangeCatalogAsync_QCpuUnitUsesSixteenPointZWhenZ15IsReadable()
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(SlmpPlcProfile.QCpuQj71E71100);
        var sdValues = new ushort[profile.RegisterCount];

        await using var server = new MultiResponseSlmpServer(
        [
            (BuildWordPayload(sdValues), (ushort)0),
            (BuildWordPayload([0]), (ushort)0),
            (BuildWordPayload([0]), (ushort)0),
            (BuildWordPayload([0]), (ushort)0),
            (BuildWordPayload([0]), (ushort)0),
            (BuildWordPayload([0]), (ushort)0),
            (BuildWordPayload([0]), (ushort)0),
            (Array.Empty<byte>(), (ushort)0x4031),
            (Array.Empty<byte>(), (ushort)0x4031),
            (Array.Empty<byte>(), (ushort)0x4031),
            (Array.Empty<byte>(), (ushort)0x4031),
            (Array.Empty<byte>(), (ushort)0x4031),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QCpuQj71E71100, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
            MonitoringTimer = 0x0010,
        };

        var catalog = await client.ReadDeviceRangeCatalogAsync();

        Assert.Equal(12, server.RequestFrames.Count);
        Assert.Equal(SlmpPlcProfile.QCpuQj71E71100, catalog.PlcProfile);
        Assert.Equal(16u, GetEntry(catalog, "Z").PointCount);
        Assert.Equal(15u, GetEntry(catalog, "Z").UpperBound);
        Assert.Equal("Z0-Z15", GetEntry(catalog, "Z").AddressRange);
        Assert.Equal("Runtime access check", GetEntry(catalog, "Z").Source);
        Assert.Equal(16u, GetEntry(catalog, "ZR").PointCount);
        Assert.Equal(15u, GetEntry(catalog, "ZR").UpperBound);
        Assert.Equal("ZR0-ZR15", GetEntry(catalog, "ZR").AddressRange);
        Assert.Equal(16u, GetEntry(catalog, "R").PointCount);
        Assert.Equal(15u, GetEntry(catalog, "R").UpperBound);
        Assert.Equal("R0-R15", GetEntry(catalog, "R").AddressRange);
    }

    [Fact]
    public async Task ReadDeviceRangeCatalogAsync_QCpuUsesTenPointZWhenZ15IsRejected()
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(SlmpPlcProfile.QCpu);
        var sdValues = new ushort[profile.RegisterCount];

        await using var server = new MultiResponseSlmpServer(
        [
            (BuildWordPayload(sdValues), (ushort)0),
            (Array.Empty<byte>(), (ushort)0x4031),
            (Array.Empty<byte>(), (ushort)0x4031),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QCpuQj71E71100, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
            MonitoringTimer = 0x0010,
        };

        var catalog = await client.ReadDeviceRangeCatalogAsync();

        Assert.Equal(3, server.RequestFrames.Count);
        Assert.Equal(10u, GetEntry(catalog, "Z").PointCount);
        Assert.Equal(9u, GetEntry(catalog, "Z").UpperBound);
        Assert.Equal("Z0-Z9", GetEntry(catalog, "Z").AddressRange);
        Assert.Equal("Runtime access check", GetEntry(catalog, "Z").Source);
        Assert.Equal(0u, GetEntry(catalog, "ZR").PointCount);
        Assert.Null(GetEntry(catalog, "ZR").UpperBound);
        Assert.Null(GetEntry(catalog, "ZR").AddressRange);
        Assert.Equal(0u, GetEntry(catalog, "R").PointCount);
        Assert.Null(GetEntry(catalog, "R").UpperBound);
        Assert.Null(GetEntry(catalog, "R").AddressRange);
    }

    [Fact]
    public async Task ReadCpuOperationStateAsync_ReadsSd203AndMasksUpperBits()
    {
        await using var server = new MultiResponseSlmpServer(
        [
            BuildWordPayload(new ushort[] { 0x00A2 }),
        ]);
        await server.StartAsync();

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
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

        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, server.Port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation)
        {
            MonitoringTimer = 0x0010,
        };

        var state = await client.ReadCpuOperationStateAsync();

        Assert.Equal(SlmpCpuOperationStatus.Unknown, state.Status);
        Assert.Equal((ushort)0x00F5, state.RawStatusWord);
        Assert.Equal((byte)0x05, state.RawCode);
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

    private static JsonDocument LoadCanonicalDeviceRangeRules()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "slmp_device_range_rules.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static Dictionary<int, ushort> CreateCanonicalRegisterSnapshot(JsonElement profile, string? onlyItem = null)
    {
        var start = profile.GetProperty("register_start").GetInt32();
        var count = profile.GetProperty("register_count").GetInt32();
        var registers = new Dictionary<int, ushort>(count);
        for (var i = 0; i < count; i++)
        {
            registers[start + i] = 0;
        }

        var rules = profile.GetProperty("rules");
        var selectedRules = onlyItem is null
            ? rules.EnumerateObject().Select(static property => property.Value)
            : [rules.GetProperty(onlyItem)];

        foreach (var rule in selectedRules)
        {
            if (!rule.TryGetProperty("register", out var registerElement))
            {
                continue;
            }

            var register = registerElement.GetInt32();
            var value = CanonicalRuleValue(rule);
            var kind = rule.GetProperty("kind").GetString()!;
            if (kind.StartsWith("dword-register", StringComparison.Ordinal))
            {
                registers[register] = (ushort)(value & 0xFFFF);
                registers[register + 1] = (ushort)(value >> 16);
            }
            else if (kind.StartsWith("word-register", StringComparison.Ordinal))
            {
                registers[register] = (ushort)value;
            }
        }

        return registers;
    }

    private static uint CanonicalRuleValue(JsonElement rule)
    {
        var kind = rule.GetProperty("kind").GetString()!;
        return kind.EndsWith("clipped", StringComparison.Ordinal)
            ? rule.GetProperty("clip_value").GetUInt32() + 5
            : 123u;
    }

    private static uint? CanonicalExpectedPointCount(JsonElement rule)
    {
        var kind = rule.GetProperty("kind").GetString()!;
        return kind switch
        {
            "unsupported" or "undefined" => null,
            "fixed" => rule.GetProperty("fixed_value").GetUInt32(),
            _ when kind.EndsWith("clipped", StringComparison.Ordinal) => Math.Min(
                CanonicalRuleValue(rule),
                rule.GetProperty("clip_value").GetUInt32()),
            _ => CanonicalRuleValue(rule),
        };
    }

    private static SlmpDeviceRangeNotation CanonicalNotation(string notation)
        => notation switch
        {
            "base10" => SlmpDeviceRangeNotation.Base10,
            "base8" => SlmpDeviceRangeNotation.Base8,
            "base16" => SlmpDeviceRangeNotation.Base16,
            _ => throw new InvalidOperationException($"Unsupported notation '{notation}'."),
        };

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
        private readonly Queue<(byte[] Payload, ushort EndCode)> _responses;
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private Task? _serverTask;

        public MultiResponseSlmpServer(IEnumerable<byte[]> responsePayloads)
        {
            _responses = new Queue<(byte[] Payload, ushort EndCode)>(
                responsePayloads.Select(static payload => (payload, (ushort)0)));
        }

        public MultiResponseSlmpServer(IEnumerable<(byte[] Payload, ushort EndCode)> responses)
        {
            _responses = new Queue<(byte[] Payload, ushort EndCode)>(responses);
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
                    var prefix = await ReadExactAsync(stream, 2).ConfigureAwait(false);
                    var headerLength = prefix[0] switch
                    {
                        0x54 => 13,
                        0x50 => 9,
                        _ => throw new IOException("Unexpected SLMP request subheader."),
                    };
                    var remainder = await ReadExactAsync(stream, headerLength - 2).ConfigureAwait(false);
                    var head = prefix.Concat(remainder).ToArray();
                    var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(
                        head.AsSpan(headerLength == 13 ? 11 : 7, 2));
                    var body = await ReadExactAsync(stream, bodyLength).ConfigureAwait(false);
                    var request = new byte[head.Length + body.Length];
                    Buffer.BlockCopy(head, 0, request, 0, head.Length);
                    Buffer.BlockCopy(body, 0, request, head.Length, body.Length);
                    RequestFrames.Add(request);

                    var (payload, endCode) = _responses.Dequeue();
                    var response = request[0] == 0x54
                        ? Build4EResponse(request, payload, endCode)
                        : Build3EResponse(request, payload, endCode);
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

        private static byte[] Build3EResponse(ReadOnlySpan<byte> request, ReadOnlySpan<byte> responseData, ushort endCode = 0)
        {
            var payload = new byte[2 + responseData.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(payload, endCode);
            responseData.CopyTo(payload.AsSpan(2));

            var response = new byte[9 + payload.Length];
            response[0] = 0xD0;
            response[1] = 0x00;
            request.Slice(2, 5).CopyTo(response.AsSpan(2));
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(7, 2), checked((ushort)payload.Length));
            payload.CopyTo(response.AsSpan(9));
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
