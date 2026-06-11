using System.Globalization;
using System.Text;
using PlcComm.Slmp;

string GetOption(IReadOnlyList<string> args, string name, string defaultValue)
{
    var idx = -1;
    for (var i = 0; i < args.Count; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            idx = i;
            break;
        }
    }
    return idx >= 0 && idx + 1 < args.Count ? args[idx + 1] : defaultValue;
}

string GetRequiredOption(IReadOnlyList<string> args, string name)
{
    var idx = -1;
    for (var i = 0; i < args.Count; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            idx = i;
            break;
        }
    }

    if (idx < 0 || idx + 1 >= args.Count || string.IsNullOrWhiteSpace(args[idx + 1]))
    {
        throw new ArgumentException($"{name} is required.");
    }

    return args[idx + 1];
}

string ParseSeriesOption(string series)
{
    if (series.Equals("ql", StringComparison.OrdinalIgnoreCase))
    {
        return "ql";
    }

    if (series.Equals("iqr", StringComparison.OrdinalIgnoreCase))
    {
        return "iqr";
    }

    throw new ArgumentException("--series must be ql or iqr.");
}

string ParseFrameTypeOption(string frame)
{
    if (frame.Equals("3e", StringComparison.OrdinalIgnoreCase))
    {
        return "3e";
    }

    if (frame.Equals("4e", StringComparison.OrdinalIgnoreCase))
    {
        return "4e";
    }

    throw new ArgumentException("--frame-type must be 3e or 4e.");
}

SlmpDeviceRangeFamily GetPlcTypeOption(IReadOnlyList<string> args)
{
    var raw = GetOption(args, "--plc-type", string.Empty);
    if (string.IsNullOrWhiteSpace(raw))
    {
        throw new ArgumentException("--plc-type is required. Use iq-r, iq-l, mx-f, mx-r, iq-f, qcpu, lcpu, qnu, or qnudv.");
    }

    var normalized = raw.Trim().ToLowerInvariant().Replace("-", string.Empty).Replace("_", string.Empty);
    return normalized switch
    {
        "iqr" => SlmpDeviceRangeFamily.IqR,
        "iql" => SlmpDeviceRangeFamily.IqL,
        "mxf" => SlmpDeviceRangeFamily.MxF,
        "mxr" => SlmpDeviceRangeFamily.MxR,
        "iqf" => SlmpDeviceRangeFamily.IqF,
        "qcpu" or "q" => SlmpDeviceRangeFamily.QCpu,
        "lcpu" or "l" => SlmpDeviceRangeFamily.LCpu,
        "qnu" => SlmpDeviceRangeFamily.QnU,
        "qnudv" or "qnudvcpu" => SlmpDeviceRangeFamily.QnUDV,
        _ => throw new ArgumentException("--plc-type must be iq-r, iq-l, mx-f, mx-r, iq-f, qcpu, lcpu, qnu, or qnudv."),
    };
}

List<string> GetOptions(IReadOnlyList<string> args, string name)
{
    var values = new List<string>();
    for (var i = 0; i < args.Count - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            values.Add(args[i + 1]);
        }
    }
    return values;
}

bool HasFlag(IReadOnlyList<string> args, string name) => args.Contains(name, StringComparer.OrdinalIgnoreCase);

string GetTimestamp() => DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

bool HasOption(IReadOnlyList<string> args, string name)
{
    for (var i = 0; i < args.Count; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

string GetRemotePassword(IReadOnlyList<string> args)
{
    var password = GetOption(args, "--remote-password", string.Empty);
    if (string.IsNullOrWhiteSpace(password))
    {
        password = GetOption(args, "--password", string.Empty);
    }

    if (string.IsNullOrWhiteSpace(password))
    {
        password = Environment.GetEnvironmentVariable("SLMP_REMOTE_PASSWORD") ?? string.Empty;
    }

    return password;
}

async Task<SlmpClient> CreateClientAsync(IReadOnlyList<string> args, SlmpTargetAddress target)
{
    var host = GetOption(args, "--host", "192.168.250.100");
    var port = int.Parse(GetOption(args, "--port", "1025"), CultureInfo.InvariantCulture);
    var transport = GetOption(args, "--transport", "tcp").Equals("udp", StringComparison.OrdinalIgnoreCase) ? SlmpTransportMode.Udp : SlmpTransportMode.Tcp;
    var frame = ParseFrameTypeOption(GetRequiredOption(args, "--frame-type"));
    var series = ParseSeriesOption(GetRequiredOption(args, "--series"));

    var client = new SlmpClient(host, port, transport) { TargetAddress = target };
    client.FrameType = frame.Equals("3e", StringComparison.OrdinalIgnoreCase) ? SlmpFrameType.Frame3E : SlmpFrameType.Frame4E;
    client.CompatibilityMode = series.Equals("ql", StringComparison.OrdinalIgnoreCase)
        ? SlmpCompatibilityMode.Legacy
        : SlmpCompatibilityMode.Iqr;

    await client.OpenAsync().ConfigureAwait(false);
    return client;
}

IReadOnlyList<SlmpNamedTarget> ParseTargets(IReadOnlyList<string> args)
{
    var targetInputs = GetOptions(args, "--target");
    var networks = GetOptions(args, "--network");
    var stations = GetOptions(args, "--station");
    var moduleIos = GetOptions(args, "--module-io");
    var multidrops = GetOptions(args, "--multidrop");
    var hasNetworkStationTarget = networks.Count > 0
        || stations.Count > 0
        || HasOption(args, "--module-io")
        || HasOption(args, "--multidrop");

    if (hasNetworkStationTarget)
    {
        if (targetInputs.Count > 0)
        {
            throw new ArgumentException("Use either --target or --network/--station, not both.");
        }

        if (networks.Count == 0 || stations.Count == 0)
        {
            throw new ArgumentException("--network and --station must be specified together.");
        }

        if (networks.Count != stations.Count)
        {
            throw new ArgumentException("--network and --station counts must match.");
        }

        return networks.Select((networkText, index) =>
        {
            var stationText = stations[index];
            var moduleIoText = moduleIos.Count == 0
                ? "0x03FF"
                : moduleIos.Count == 1
                    ? moduleIos[0]
                    : moduleIos.Count == networks.Count
                        ? moduleIos[index]
                        : throw new ArgumentException("--module-io count must be 1 or match --network/--station count.");
            var multidropText = multidrops.Count == 0
                ? "0x00"
                : multidrops.Count == 1
                    ? multidrops[0]
                    : multidrops.Count == networks.Count
                        ? multidrops[index]
                        : throw new ArgumentException("--multidrop count must be 1 or match --network/--station count.");
            var network = checked((byte)SlmpTargetParser.ParseAutoNumber(networkText));
            var station = checked((byte)SlmpTargetParser.ParseAutoNumber(stationText));
            var moduleIo = checked((ushort)SlmpTargetParser.ParseAutoNumber(moduleIoText));
            var multidrop = checked((byte)SlmpTargetParser.ParseAutoNumber(multidropText));
            var target = new SlmpTargetAddress(network, station, moduleIo, multidrop);
            return new SlmpNamedTarget(
                FormatTargetAddress(target),
                target);
        }).ToArray();
    }

    if (targetInputs.Count == 0)
    {
        targetInputs.Add("SELF");
    }
    return SlmpTargetParser.ParseMany(targetInputs);
}

async Task<int> RunConnectionCheckAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    using var client = await CreateClientAsync(args, target.Target).ConfigureAwait(false);
    var values = await client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1).ConfigureAwait(false);
    Console.WriteLine($"[OK] Read SM400 values=[{values[0]}]");
    return 0;
}

async Task<int> RunDeviceRangeCatalogAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    var plcType = GetPlcTypeOption(args);
    if (!HasFlag(args, "--series") || !HasFlag(args, "--frame-type"))
    {
        throw new ArgumentException("device-range-catalog requires explicit --series ql|iqr and --frame-type 3e|4e.");
    }

    using var client = await CreateClientAsync(args, target.Target).ConfigureAwait(false);
    var catalog = await client.ReadDeviceRangeCatalogAsync(plcType).ConfigureAwait(false);

    Console.WriteLine($"[OK] selected_family={plcType} connection_frame={client.FrameType} connection_compatibility={client.CompatibilityMode} family={catalog.Family}");
    foreach (var entry in catalog.Entries)
    {
        Console.WriteLine(
            $"{entry.Device} supported={entry.Supported} notation={entry.Notation} points={entry.PointCount?.ToString(CultureInfo.InvariantCulture) ?? "open"} range={entry.AddressRange ?? "-"}");
    }

    return 0;
}

string FormatTarget(SlmpNamedTarget row)
{
    var address = FormatTargetAddress(row.Target);
    return string.Equals(row.Name, address, StringComparison.Ordinal)
        ? address
        : $"name={row.Name}, {address}";
}

string FormatTargetAddress(SlmpTargetAddress target) =>
    $"network={target.Network}, station={target.Station}, module_io=0x{target.ModuleIo:X4}, multidrop=0x{target.Multidrop:X2}";

async Task<int> RunOtherStationCheckAsync(IReadOnlyList<string> args)
{
    var targets = ParseTargets(args);
    var anyNg = false;
    foreach (var namedTarget in targets)
    {
        try
        {
            using var client = await CreateClientAsync(args, namedTarget.Target).ConfigureAwait(false);
            var value = await client.ReadWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 1000), 1).ConfigureAwait(false);
            string typeInfo;
            try
            {
                var info = await client.ReadTypeNameAsync().ConfigureAwait(false);
                Console.WriteLine($"[INFO] Read Type Name: model={info.Model}, model_code=0x{info.ModelCode:X4} ({info.ModelCode})");
                typeInfo = $", model={info.Model}, model_code=0x{info.ModelCode:X4} ({info.ModelCode})";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[INFO] Read Type Name unavailable: error={ex.Message}");
                typeInfo = $", type_name_error={ex.Message}";
            }

            Console.WriteLine($"[OK] {FormatTarget(namedTarget)}, device=D1000, points=1, values=[{value[0]}]{typeInfo}");
        }
        catch (Exception ex)
        {
            anyNg = true;
            Console.WriteLine($"[NG] {FormatTarget(namedTarget)}, error={ex.Message}");
        }
    }

    return anyNg ? 1 : 0;
}

async Task<int> RunRandomCheckAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    using var client = await CreateClientAsync(args, target.Target).ConfigureAwait(false);
    var writeCheck = HasFlag(args, "--write-check");
    var wordDevice = SlmpDeviceParser.Parse(GetOption(args, "--word-device", "D100"));
    var dwordDevice = SlmpDeviceParser.Parse(GetOption(args, "--dword-device", "D200"));
    var bitDevice = SlmpDeviceParser.Parse(GetOption(args, "--bit-device", "M100"));

    var read = await client.ReadRandomAsync([wordDevice], [dwordDevice]).ConfigureAwait(false);
    Console.WriteLine($"[OK] random-read words=[{string.Join(", ", read.WordValues)}] dwords=[{string.Join(", ", read.DwordValues)}]");

    if (!writeCheck)
    {
        Console.WriteLine("[INFO] random-write skipped (use --write-check)");
        return 0;
    }

    await client.WriteRandomWordsAsync([(wordDevice, (ushort)0x1234)], [(dwordDevice, 0x12345678)]).ConfigureAwait(false);
    await client.WriteRandomBitsAsync([(bitDevice, true)]).ConfigureAwait(false);
    Console.WriteLine("[OK] random-write completed");
    return 0;
}

async Task<int> RunBlockCheckAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    using var client = await CreateClientAsync(args, target.Target).ConfigureAwait(false);
    var writeCheck = HasFlag(args, "--write-check");
    var wordDevice = SlmpDeviceParser.Parse(GetOption(args, "--word-device", "D300"));
    var bitDevice = SlmpDeviceParser.Parse(GetOption(args, "--bit-device", "M200"));
    var wordPoints = ushort.Parse(GetOption(args, "--word-points", "2"));
    var bitPoints = ushort.Parse(GetOption(args, "--bit-points", "1"));

    var read = await client.ReadBlockAsync(
        [new SlmpBlockRead(wordDevice, wordPoints)],
        [new SlmpBlockRead(bitDevice, bitPoints)]
    ).ConfigureAwait(false);
    Console.WriteLine($"[OK] block-read words=[{string.Join(", ", read.WordValues)}] bit_words=[{string.Join(", ", read.BitWordValues)}]");

    if (!writeCheck)
    {
        Console.WriteLine("[INFO] block-write skipped (use --write-check)");
        return 0;
    }

    var wordValues = Enumerable.Range(0, wordPoints).Select(i => (ushort)(0x1000 + i)).ToArray();
    var bitWordValues = Enumerable.Range(0, bitPoints).Select(i => (ushort)(0x0001 << (i % 15))).ToArray();
    await client.WriteBlockAsync(
        [new SlmpBlockWrite(wordDevice, wordValues)],
        [new SlmpBlockWrite(bitDevice, bitWordValues)],
        new SlmpBlockWriteOptions(SplitMixedBlocks: false)
    ).ConfigureAwait(false);
    Console.WriteLine("[OK] block-write completed");
    return 0;
}

static bool IsBitExtendedDevice(SlmpDeviceCode code)
    => code is SlmpDeviceCode.SM
        or SlmpDeviceCode.X
        or SlmpDeviceCode.Y
        or SlmpDeviceCode.M
        or SlmpDeviceCode.L
        or SlmpDeviceCode.F
        or SlmpDeviceCode.V
        or SlmpDeviceCode.B
        or SlmpDeviceCode.TS
        or SlmpDeviceCode.TC
        or SlmpDeviceCode.LTS
        or SlmpDeviceCode.LTC
        or SlmpDeviceCode.STS
        or SlmpDeviceCode.STC
        or SlmpDeviceCode.LSTS
        or SlmpDeviceCode.LSTC
        or SlmpDeviceCode.CS
        or SlmpDeviceCode.CC
        or SlmpDeviceCode.LCS
        or SlmpDeviceCode.LCC
        or SlmpDeviceCode.SB
        or SlmpDeviceCode.DX
        or SlmpDeviceCode.DY;

static string FormatBits(IEnumerable<bool> values)
    => string.Join(", ", values.Select(x => x ? "1" : "0"));

static string FormatWords(IEnumerable<ushort> values)
    => string.Join(", ", values.Select(x => $"0x{x:X4}"));

async Task<int> RunGhCoverageAsync(IReadOnlyList<string> args)
{
    var targets = ParseTargets(args);
    var transports = GetOptions(args, "--transport");
    if (transports.Count == 0) transports.Add("tcp");
    var deviceTexts = GetOptions(args, "--device");
    if (deviceTexts.Count == 0) deviceTexts.Add(@"U3E0\G10");
    var pointsTexts = GetOptions(args, "--points");
    if (pointsTexts.Count == 0) pointsTexts.Add("1");
    var pointList = pointsTexts.Select(x => checked((ushort)SlmpTargetParser.ParseAutoNumber(x))).ToArray();
    var directTexts = GetOptions(args, "--direct-memory");
    if (directTexts.Count == 0) directTexts.Add("0xF8");
    var directMemories = directTexts.Select(x => checked((byte)SlmpTargetParser.ParseAutoNumber(x))).ToArray();
    var writeCheck = HasFlag(args, "--write-check");
    var remotePassword = GetRemotePassword(args);
    var rows = new List<CoverageRow>();

    Console.WriteLine("=== Extended Device Coverage Sweep ===");
    Console.WriteLine($"Host={GetOption(args, "--host", "192.168.250.100")}, Port={GetOption(args, "--port", "1025")}, Transports=[{string.Join(", ", transports)}], Series={ParseSeriesOption(GetRequiredOption(args, "--series"))}");
    Console.WriteLine($"[INFO] Targets=[{string.Join(", ", targets.Select(x => x.Name))}]");
    Console.WriteLine($"[INFO] Devices=[{string.Join(", ", deviceTexts)}]");
    Console.WriteLine($"[INFO] Points=[{string.Join(", ", pointList)}]");
    Console.WriteLine($"[INFO] Direct memory=[{string.Join(", ", directMemories.Select(x => $"0x{x:X2}"))}]");

    foreach (var transport in transports)
    {
        var scopedArgs = args.ToList();
        scopedArgs.RemoveAll(x => x.Equals("--transport", StringComparison.OrdinalIgnoreCase));
        scopedArgs.RemoveAll(x => x.Equals("tcp", StringComparison.OrdinalIgnoreCase) || x.Equals("udp", StringComparison.OrdinalIgnoreCase));
        scopedArgs.Add("--transport");
        scopedArgs.Add(transport);

        foreach (var target in targets)
        {
            using var client = await CreateClientAsync(scopedArgs, target.Target).ConfigureAwait(false);
            var unlocked = false;
            try
            {
                if (!string.IsNullOrWhiteSpace(remotePassword))
                {
                    Console.WriteLine($"[INFO] Remote password unlock: target={target.Name}, transport={transport}");
                    await client.RemotePasswordUnlockAsync(remotePassword).ConfigureAwait(false);
                    unlocked = true;
                }

                Console.WriteLine($"[INFO] Sweep target={target.Name}, transport={transport}, {FormatTargetAddress(target.Target)}");
                try
                {
                    var info = await client.ReadTypeNameAsync().ConfigureAwait(false);
                    Console.WriteLine($"[OK] Read Type Name: target={target.Name}, transport={transport}, model={info.Model}, model_code=0x{info.ModelCode:X4} ({info.ModelCode})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[INFO] Read Type Name unavailable: target={target.Name}, transport={transport}, error={ex.Message}");
                }

                foreach (var deviceText in deviceTexts)
                {
                    var qualified = SlmpQualifiedDeviceParser.Parse(deviceText);
                    var unit = IsBitExtendedDevice(qualified.Device.Code) ? "bit" : "word";
                    foreach (var directMemory in directMemories)
                    {
                        var ext = new SlmpExtensionSpec(DirectMemorySpecification: directMemory);
                        var effectiveDirectMemory = qualified.DirectMemorySpecification ?? directMemory;
                        foreach (var points in pointList)
                        {
                            try
                            {
                                if (unit == "bit")
                                {
                                    var before = await client.ReadBitsExtendedAsync(qualified, points, ext).ConfigureAwait(false);
                                    if (!writeCheck)
                                    {
                                        var detail = $"device={deviceText}, points={points}, before=[{FormatBits(before)}], mode=read_only";
                                        Console.WriteLine($"[OK] {target.Name} {deviceText} points={points} unit={unit} direct=0x{effectiveDirectMemory:X2}: {detail}");
                                        rows.Add(new CoverageRow(target.Name, transport, deviceText, points, effectiveDirectMemory, unit, "OK", detail));
                                        continue;
                                    }

                                    var write = Enumerable.Range(0, points).Select(i => i % 2 == 0).ToArray();
                                    await client.WriteBitsExtendedAsync(qualified, write, ext).ConfigureAwait(false);
                                    var readback = await client.ReadBitsExtendedAsync(qualified, points, ext).ConfigureAwait(false);
                                    var restoredState = "ok";
                                    try
                                    {
                                        await client.WriteBitsExtendedAsync(qualified, before, ext).ConfigureAwait(false);
                                    }
                                    catch
                                    {
                                        restoredState = "failed";
                                    }

                                    var mismatch = !readback.SequenceEqual(write);
                                    var resultDetail = mismatch
                                        ? $"device={deviceText}, points={points}, before=[{FormatBits(before)}], write=[{FormatBits(write)}], readback=[{FormatBits(readback)}], readback_mismatch=yes, restore={restoredState}"
                                        : $"device={deviceText}, points={points}, before=[{FormatBits(before)}], write=[{FormatBits(write)}], readback=[{FormatBits(readback)}], restore={restoredState}";
                                    var status = mismatch ? "NG" : "OK";
                                    Console.WriteLine($"[{status}] {target.Name} {deviceText} points={points} unit={unit} direct=0x{effectiveDirectMemory:X2}: {resultDetail}");
                                    rows.Add(new CoverageRow(target.Name, transport, deviceText, points, effectiveDirectMemory, unit, status, resultDetail));
                                }
                                else
                                {
                                    var before = await client.ReadWordsExtendedAsync(qualified, points, ext).ConfigureAwait(false);
                                    if (!writeCheck)
                                    {
                                        var detail = $"device={deviceText}, points={points}, before=[{FormatWords(before)}], mode=read_only";
                                        Console.WriteLine($"[OK] {target.Name} {deviceText} points={points} unit={unit} direct=0x{effectiveDirectMemory:X2}: {detail}");
                                        rows.Add(new CoverageRow(target.Name, transport, deviceText, points, effectiveDirectMemory, unit, "OK", detail));
                                        continue;
                                    }

                                    var write = Enumerable.Range(0, points).Select(i => (ushort)(0x001E + i)).ToArray();
                                    await client.WriteWordsExtendedAsync(qualified, write, ext).ConfigureAwait(false);
                                    var readback = await client.ReadWordsExtendedAsync(qualified, points, ext).ConfigureAwait(false);
                                    var restoredState = "ok";
                                    try
                                    {
                                        await client.WriteWordsExtendedAsync(qualified, before, ext).ConfigureAwait(false);
                                    }
                                    catch
                                    {
                                        restoredState = "failed";
                                    }

                                    var mismatch = !readback.SequenceEqual(write);
                                    var resultDetail = mismatch
                                        ? $"device={deviceText}, points={points}, before=[{FormatWords(before)}], write=[{FormatWords(write)}], readback=[{FormatWords(readback)}], readback_mismatch=yes, restore={restoredState}"
                                        : $"device={deviceText}, points={points}, before=[{FormatWords(before)}], write=[{FormatWords(write)}], readback=[{FormatWords(readback)}], restore={restoredState}";
                                    var status = mismatch ? "NG" : "OK";
                                    Console.WriteLine($"[{status}] {target.Name} {deviceText} points={points} unit={unit} direct=0x{effectiveDirectMemory:X2}: {resultDetail}");
                                    rows.Add(new CoverageRow(target.Name, transport, deviceText, points, effectiveDirectMemory, unit, status, resultDetail));
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[NG] {target.Name} {deviceText} points={points} unit={unit} direct=0x{effectiveDirectMemory:X2}: {ex.Message}");
                                rows.Add(new CoverageRow(target.Name, transport, deviceText, points, effectiveDirectMemory, unit, "NG", ex.Message));
                            }
                        }
                    }
                }
            }
            finally
            {
                if (unlocked)
                {
                    try
                    {
                        await client.RemotePasswordLockAsync(remotePassword).ConfigureAwait(false);
                        Console.WriteLine($"[INFO] Remote password lock: target={target.Name}, transport={transport}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NG] Remote password lock failed: target={target.Name}, transport={transport}, error={ex.Message}");
                        rows.Add(new CoverageRow(target.Name, transport, "remote-password-lock", 0, 0, "control", "NG", ex.Message));
                    }
                }
            }
        }
    }

    var reportDir = GetOption(args, "--report-dir", "internal_docs/validation/reports");
    Directory.CreateDirectory(reportDir);
    var reportPath = Path.Combine(reportDir, "ExtendedDevice_coverage_latest.md");
    var report = new StringBuilder();
    report.AppendLine("# Extended Device Coverage Latest");
    report.AppendLine();
    report.AppendLine($"- Timestamp: {GetTimestamp()}");
    report.AppendLine($"- Host: {GetOption(args, "--host", "192.168.250.100")}");
    report.AppendLine($"- Port: {GetOption(args, "--port", "1025")}");
    report.AppendLine($"- Write check: {(writeCheck ? "enabled" : "disabled")}");
    report.AppendLine();
    report.AppendLine("| Target | Transport | Device | Points | Unit | Direct | Status | Detail |");
    report.AppendLine("|---|---|---|---|---|---|---|---|");
    foreach (var row in rows)
    {
        report.AppendLine($"| {row.Target} | {row.Transport} | {row.Device} | {row.Points} | {row.Unit} | 0x{row.DirectMemory:X2} | {row.Status} | {row.Detail.Replace("|", "/")} |");
    }
    await File.WriteAllTextAsync(reportPath, report.ToString(), Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine($"[DONE] report={reportPath}");
    return rows.Any(x => x.Status == "NG") ? 1 : 0;
}

async Task<int> RunExtendedDeviceDeviceRecheckAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    var deviceText = GetOption(args, "--device", @"U3E0\G10");
    var points = checked((ushort)SlmpTargetParser.ParseAutoNumber(GetOption(args, "--points", "1")));
    var directMemory = checked((byte)SlmpTargetParser.ParseAutoNumber(GetOption(args, "--direct-memory", "0xF8")));
    var writeCheck = HasFlag(args, "--write-check");
    using var client = await CreateClientAsync(args, target.Target).ConfigureAwait(false);
    var qualified = SlmpQualifiedDeviceParser.Parse(deviceText);
    var ext = new SlmpExtensionSpec(DirectMemorySpecification: directMemory);
    var before = await client.ReadWordsExtendedAsync(qualified, points, ext).ConfigureAwait(false);
    var status = "OK";
    var detail = $"device={deviceText}, points={points}, before=[{string.Join(", ", before.Select(x => $"0x{x:X4}"))}], mode=read_only";
    if (writeCheck)
    {
        var write = Enumerable.Range(0, points).Select(i => (ushort)(0x001E + i)).ToArray();
        await client.WriteWordsExtendedAsync(qualified, write, ext).ConfigureAwait(false);
        var readback = await client.ReadWordsExtendedAsync(qualified, points, ext).ConfigureAwait(false);
        await client.WriteWordsExtendedAsync(qualified, before, ext).ConfigureAwait(false);
        var mismatch = !readback.SequenceEqual(write);
        status = mismatch ? "NG" : "OK";
        detail = $"device={deviceText}, points={points}, before=[{string.Join(", ", before.Select(x => $"0x{x:X4}"))}], write=[{string.Join(", ", write.Select(x => $"0x{x:X4}"))}], readback=[{string.Join(", ", readback.Select(x => $"0x{x:X4}"))}]";
    }

    var reportDir = GetOption(args, "--report-dir", "internal_docs/validation/reports");
    Directory.CreateDirectory(reportDir);
    var reportPath = Path.Combine(reportDir, "ExtendedDevice_device_recheck_latest.md");
    var report = new StringBuilder();
    report.AppendLine("# ExtendedDevice Device Recheck Latest");
    report.AppendLine();
    report.AppendLine($"- Timestamp: {GetTimestamp()}");
    report.AppendLine($"- Target: {target.Name}");
    report.AppendLine($"- Transport: {GetOption(args, "--transport", "tcp")}");
    report.AppendLine($"- Result: {status}");
    report.AppendLine();
    report.AppendLine(detail);
    await File.WriteAllTextAsync(reportPath, report.ToString(), Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine($"[{status}] {detail}");
    Console.WriteLine($"[DONE] report={reportPath}");
    return status == "OK" ? 0 : 1;
}

async Task<int> RunReadSoakAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    var iterations = SlmpTargetParser.ParseAutoNumber(GetOption(args, "--iterations", "100"));
    var device = SlmpDeviceParser.Parse(GetOption(args, "--device", "D1000"));
    var points = checked((ushort)SlmpTargetParser.ParseAutoNumber(GetOption(args, "--points", "1")));
    var intervalMs = SlmpTargetParser.ParseAutoNumber(GetOption(args, "--interval-ms", "0"));
    using var client = await CreateClientAsync(args, target.Target).ConfigureAwait(false);
    var failures = 0;
    var latencies = new List<long>(iterations);
    for (var i = 0; i < iterations; i++)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            _ = await client.ReadWordsAsync(device, points).ConfigureAwait(false);
        }
        catch
        {
            failures++;
        }
        var elapsed = (DateTimeOffset.UtcNow - started).TotalMilliseconds;
        latencies.Add((long)elapsed);
        if (intervalMs > 0) await Task.Delay(intervalMs).ConfigureAwait(false);
    }

    var reportDir = GetOption(args, "--report-dir", "internal_docs/validation/reports");
    Directory.CreateDirectory(reportDir);
    var reportPath = Path.Combine(reportDir, "read_soak_latest.md");
    var report = new StringBuilder();
    report.AppendLine("# Read Soak Latest");
    report.AppendLine();
    report.AppendLine($"- Timestamp: {GetTimestamp()}");
    report.AppendLine($"- Iterations: {iterations}");
    report.AppendLine($"- Failures: {failures}");
    report.AppendLine($"- Min/Avg/Max latency (ms): {latencies.Min()}/{(long)latencies.Average()}/{latencies.Max()}");
    await File.WriteAllTextAsync(reportPath, report.ToString(), Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine($"[DONE] report={reportPath}");
    return failures == 0 ? 0 : 1;
}

async Task<int> RunMixedReadLoadAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    var iterations = SlmpTargetParser.ParseAutoNumber(GetOption(args, "--iterations", "100"));
    using var client = await CreateClientAsync(args, target.Target).ConfigureAwait(false);
    var failures = 0;
    for (var i = 0; i < iterations; i++)
    {
        try
        {
            _ = await client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1).ConfigureAwait(false);
            _ = await client.ReadRandomAsync([new SlmpDeviceAddress(SlmpDeviceCode.D, 100)], [new SlmpDeviceAddress(SlmpDeviceCode.D, 200)]).ConfigureAwait(false);
            _ = await client.ReadBlockAsync([new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.D, 300), 2)], [new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.M, 200), 1)]).ConfigureAwait(false);
        }
        catch
        {
            failures++;
        }
    }

    var reportDir = GetOption(args, "--report-dir", "internal_docs/validation/reports");
    Directory.CreateDirectory(reportDir);
    var reportPath = Path.Combine(reportDir, "mixed_read_load_latest.md");
    var report = $"# Mixed Read Load Latest{Environment.NewLine}{Environment.NewLine}- Timestamp: {GetTimestamp()}{Environment.NewLine}- Iterations: {iterations}{Environment.NewLine}- Failures: {failures}{Environment.NewLine}";
    await File.WriteAllTextAsync(reportPath, report, Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine($"[DONE] report={reportPath}");
    return failures == 0 ? 0 : 1;
}

async Task<int> RunTcpConcurrencyAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    var clients = SlmpTargetParser.ParseAutoNumber(GetOption(args, "--clients", "4"));
    var iterations = SlmpTargetParser.ParseAutoNumber(GetOption(args, "--iterations", "50"));
    var staggerMs = SlmpTargetParser.ParseAutoNumber(GetOption(args, "--stagger-ms", "50"));
    var quiet = HasFlag(args, "--quiet");
    var readFailures = 0;
    var connectFailures = 0;

    var openedClients = new List<SlmpClient>(clients);
    for (var clientIndex = 0; clientIndex < clients; clientIndex++)
    {
        if (staggerMs > 0 && clientIndex > 0)
        {
            await Task.Delay(staggerMs).ConfigureAwait(false);
        }
        try
        {
            openedClients.Add(await CreateClientAsync(args, target.Target).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            if (!quiet)
            {
                Console.WriteLine($"[INFO] tcp-concurrency connect failed client={clientIndex + 1}: {ex.Message}");
            }
            connectFailures++;
        }
    }

    try
    {
        var tasks = openedClients.Select(async client =>
        {
            var localReadFailures = 0;
            for (var i = 0; i < iterations; i++)
            {
                try
                {
                    _ = await client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1).ConfigureAwait(false);
                    _ = await client.ReadWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 1000), 1).ConfigureAwait(false);
                }
                catch
                {
                    localReadFailures++;
                }
            }

            return localReadFailures;
        }).ToArray();

        foreach (var result in await Task.WhenAll(tasks).ConfigureAwait(false))
        {
            readFailures += result;
        }
    }
    finally
    {
        foreach (var client in openedClients)
        {
            client.Dispose();
        }
    }

    var reportDir = GetOption(args, "--report-dir", "internal_docs/validation/reports");
    Directory.CreateDirectory(reportDir);
    var reportPath = Path.Combine(reportDir, "tcp_concurrency_latest.md");
    var totalFailures = connectFailures + readFailures;
    var report = $"# TCP Concurrency Latest{Environment.NewLine}{Environment.NewLine}- Timestamp: {GetTimestamp()}{Environment.NewLine}- Clients: {clients}{Environment.NewLine}- Iterations per client: {iterations}{Environment.NewLine}- Stagger ms: {staggerMs}{Environment.NewLine}- Connect failures: {connectFailures}{Environment.NewLine}- Read failures: {readFailures}{Environment.NewLine}- Total failures: {totalFailures}{Environment.NewLine}";
    await File.WriteAllTextAsync(reportPath, report, Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine($"[DONE] report={reportPath}");
    return totalFailures == 0 ? 0 : 1;
}

async Task<int> RunSingleConnectionLoadAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    var workers = SlmpTargetParser.ParseAutoNumber(GetOption(args, "--workers", "4"));
    var iterations = SlmpTargetParser.ParseAutoNumber(GetOption(args, "--iterations", "200"));
    var quiet = HasFlag(args, "--quiet");
    var readFailures = 0;

    using var client = await CreateClientAsync(args, target.Target).ConfigureAwait(false);
    await using var queuedClient = new QueuedSlmpClient(client);
    await queuedClient.OpenAsync().ConfigureAwait(false);

    var tasks = Enumerable.Range(0, workers).Select(async workerIndex =>
    {
        var localFailures = 0;
        for (var i = 0; i < iterations; i++)
        {
            try
            {
                _ = await queuedClient.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1).ConfigureAwait(false);
                _ = await queuedClient.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 1000), 1).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                localFailures++;
                if (!quiet)
                {
                    Console.WriteLine($"[INFO] single-connection-load read failed worker={workerIndex + 1}: {ex.Message}");
                }
            }
        }

        return localFailures;
    }).ToArray();

    foreach (var result in await Task.WhenAll(tasks).ConfigureAwait(false))
    {
        readFailures += result;
    }

    var reportDir = GetOption(args, "--report-dir", "internal_docs/validation/reports");
    Directory.CreateDirectory(reportDir);
    var reportPath = Path.Combine(reportDir, "single_connection_load_latest.md");
    var report = $"# Single Connection Load Latest{Environment.NewLine}{Environment.NewLine}- Timestamp: {GetTimestamp()}{Environment.NewLine}- Workers: {workers}{Environment.NewLine}- Iterations per worker: {iterations}{Environment.NewLine}- Read failures: {readFailures}{Environment.NewLine}";
    await File.WriteAllTextAsync(reportPath, report, Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine($"[DONE] report={reportPath}");
    return readFailures == 0 ? 0 : 1;
}

if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h"))
{
    Console.WriteLine("SLMP .NET CLI");
    Console.WriteLine("  connection-check --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp|udp --target SELF|SELF-CPU1|name,0x00,0xFF,0x03FF,0x00 --network ... --station ... --quiet]");
    Console.WriteLine("  device-range-catalog --plc-type iq-r|iq-l|mx-f|mx-r|iq-f|qcpu|lcpu|qnu|qnudv --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp|udp --target SELF --network ... --station ... --quiet]");
    Console.WriteLine("  other-station-check --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp|udp --network ... --station ... (repeatable) --quiet]");
    Console.WriteLine("  random-check --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp|udp --target ... --network ... --station ... --write-check --quiet]");
    Console.WriteLine("  block-check --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp|udp --target ... --network ... --station ... --write-check --quiet]");
    Console.WriteLine(@"  ExtendedDevice-device-recheck --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp|udp --target SELF --network ... --station ... --device U3E0\G10 --points 1 --direct-memory 0xF8 --write-check --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine(@"  extendeddevice-coverage --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp|udp (repeatable) --network ... --station ... (repeatable) --device U3E0\G10 (repeatable) --points 1 (repeatable) --direct-memory 0xF8 (repeatable) --write-check --remote-password ... --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine("  read-soak --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp|udp --target SELF --network ... --station ... --device D1000 --points 1 --iterations 100 --interval-ms 0 --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine("  mixed-read-load --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp|udp --target SELF --network ... --station ... --iterations 100 --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine("  tcp-concurrency --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp --target SELF --network ... --station ... --clients 4 --iterations 50 --stagger-ms 50 --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine("  single-connection-load --series ql|iqr --frame-type 3e|4e [--host ... --port ... --transport tcp --target SELF --network ... --station ... --workers 4 --iterations 200 --report-dir internal_docs/validation/reports --quiet]");
    return;
}

var command = args[0].ToLowerInvariant();
var argList = args.ToList();
argList.RemoveAt(0);
try
{
    var exitCode = command switch
    {
        "connection-check" => await RunConnectionCheckAsync(argList).ConfigureAwait(false),
        "device-range-catalog" => await RunDeviceRangeCatalogAsync(argList).ConfigureAwait(false),
        "other-station-check" => await RunOtherStationCheckAsync(argList).ConfigureAwait(false),
        "random-check" => await RunRandomCheckAsync(argList).ConfigureAwait(false),
        "block-check" => await RunBlockCheckAsync(argList).ConfigureAwait(false),
        "extendeddevice-device-recheck" => await RunExtendedDeviceDeviceRecheckAsync(argList).ConfigureAwait(false),
        "extendeddevice-coverage" or "g-hg-extendeddevice-coverage" => await RunGhCoverageAsync(argList).ConfigureAwait(false),
        "read-soak" => await RunReadSoakAsync(argList).ConfigureAwait(false),
        "mixed-read-load" => await RunMixedReadLoadAsync(argList).ConfigureAwait(false),
        "tcp-concurrency" => await RunTcpConcurrencyAsync(argList).ConfigureAwait(false),
        "single-connection-load" => await RunSingleConnectionLoadAsync(argList).ConfigureAwait(false),
        _ => 2,
    };

    if (exitCode == 2)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Environment.ExitCode = 2;
    }
    else
    {
        Environment.ExitCode = exitCode;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[NG] {ex.Message}");
    Environment.ExitCode = 1;
}

internal sealed record CoverageRow(
    string Target,
    string Transport,
    string Device,
    ushort Points,
    byte DirectMemory,
    string Unit,
    string Status,
    string Detail
);
