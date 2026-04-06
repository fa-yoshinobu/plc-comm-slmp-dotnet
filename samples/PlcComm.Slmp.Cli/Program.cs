using System.Globalization;
using System.Text;
using System.Text.Json;
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

string GetSeriesOption(IReadOnlyList<string> args, string defaultValue)
{
    var series = GetOption(args, "--series", defaultValue);
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

string GetFrameTypeOption(IReadOnlyList<string> args, string defaultValue)
{
    var frame = GetOption(args, "--frame-type", defaultValue);
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

async Task<SlmpClient> CreateClientAsync(IReadOnlyList<string> args, SlmpTargetAddress target, string defaultSeries, string defaultFrame)
{
    var host = GetOption(args, "--host", "192.168.250.100");
    var port = int.Parse(GetOption(args, "--port", "1025"), CultureInfo.InvariantCulture);
    var transport = GetOption(args, "--transport", "tcp").Equals("udp", StringComparison.OrdinalIgnoreCase) ? SlmpTransportMode.Udp : SlmpTransportMode.Tcp;
    var frame = GetFrameTypeOption(args, defaultFrame);
    var series = GetSeriesOption(args, defaultSeries);

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
    if (targetInputs.Count == 0)
    {
        targetInputs.Add("SELF");
    }
    return SlmpTargetParser.ParseMany(targetInputs);
}

async Task<int> RunConnectionCheckAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    using var client = await CreateClientAsync(args, target.Target, "ql", "3e").ConfigureAwait(false);
    var values = await client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1).ConfigureAwait(false);
    Console.WriteLine($"[OK] Read SM400 values=[{values[0]}]");
    return 0;
}

string FormatTarget(SlmpNamedTarget row)
{
    return $"name={row.Name}, network=0x{row.Target.Network:X2}, station=0x{row.Target.Station:X2}, module_io=0x{row.Target.ModuleIo:X4}, multidrop=0x{row.Target.Multidrop:X2}";
}

async Task<int> RunOtherStationCheckAsync(IReadOnlyList<string> args)
{
    var targets = ParseTargets(args);
    var anyNg = false;
    foreach (var namedTarget in targets)
    {
        try
        {
            using var client = await CreateClientAsync(args, namedTarget.Target, "iqr", "4e").ConfigureAwait(false);
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

            Console.WriteLine($"[OK] {namedTarget.Name}: {FormatTarget(namedTarget)}, device=D1000, points=1, values=[{value[0]}]{typeInfo}");
        }
        catch (Exception ex)
        {
            anyNg = true;
            Console.WriteLine($"[NG] {namedTarget.Name}: {FormatTarget(namedTarget)}, error={ex.Message}");
        }
    }

    return anyNg ? 1 : 0;
}

async Task<int> RunRandomCheckAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    using var client = await CreateClientAsync(args, target.Target, "iqr", "4e").ConfigureAwait(false);
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
    using var client = await CreateClientAsync(args, target.Target, "iqr", "4e").ConfigureAwait(false);
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
        new SlmpBlockWriteOptions(SplitMixedBlocks: false, RetryMixedOnError: true)
    ).ConfigureAwait(false);
    Console.WriteLine("[OK] block-write completed");
    return 0;
}

async Task<ProbeItemResult> RunProbeItemAsync(SlmpClient client, SlmpNamedTarget target, string transport, string name, Func<Task<string>> body)
{
    try
    {
        var detail = await body().ConfigureAwait(false);
        Console.WriteLine($"[OK] {target.Name} {transport} {name}: {detail}");
        return new ProbeItemResult(target.Name, transport, name, "OK", detail);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[NG] {target.Name} {transport} {name}: {ex.Message}");
        return new ProbeItemResult(target.Name, transport, name, "NG", ex.Message);
    }
}

async Task<int> RunCompatibilityProbeAsync(IReadOnlyList<string> args)
{
    var targets = ParseTargets(args);
    var transports = GetOptions(args, "--transport");
    if (transports.Count == 0) transports.Add("tcp");
    var writeCheck = HasFlag(args, "--write-check");

    var results = new List<ProbeItemResult>();
    foreach (var transport in transports)
    {
        var scopedArgs = args.ToList();
        scopedArgs.RemoveAll(x => x.Equals("--transport", StringComparison.OrdinalIgnoreCase));
        scopedArgs.RemoveAll(x => x.Equals("tcp", StringComparison.OrdinalIgnoreCase) || x.Equals("udp", StringComparison.OrdinalIgnoreCase));
        scopedArgs.Add("--transport");
        scopedArgs.Add(transport);

        foreach (var target in targets)
        {
            using var client = await CreateClientAsync(scopedArgs, target.Target, "iqr", "4e").ConfigureAwait(false);
            results.Add(await RunProbeItemAsync(client, target, transport, "0101 read_type_name", async () =>
            {
                var info = await client.ReadTypeNameAsync().ConfigureAwait(false);
                return $"model={info.Model}, model_code=0x{info.ModelCode:X4}";
            }).ConfigureAwait(false));

            results.Add(await RunProbeItemAsync(client, target, transport, "0401 read_sm400", async () =>
            {
                var v = await client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1).ConfigureAwait(false);
                return $"values=[{v[0]}]";
            }).ConfigureAwait(false));

            results.Add(await RunProbeItemAsync(client, target, transport, "0403 random_read", async () =>
            {
                var rr = await client.ReadRandomAsync([new SlmpDeviceAddress(SlmpDeviceCode.D, 100)], [new SlmpDeviceAddress(SlmpDeviceCode.D, 200)]).ConfigureAwait(false);
                return $"words=[{string.Join(", ", rr.WordValues)}], dwords=[{string.Join(", ", rr.DwordValues)}]";
            }).ConfigureAwait(false));

            results.Add(await RunProbeItemAsync(client, target, transport, "0406 block_read", async () =>
            {
                var br = await client.ReadBlockAsync([new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.D, 300), 2)], [new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.M, 200), 1)]).ConfigureAwait(false);
                return $"words=[{string.Join(", ", br.WordValues)}], bit_words=[{string.Join(", ", br.BitWordValues)}]";
            }).ConfigureAwait(false));

            if (writeCheck)
            {
                results.Add(await RunProbeItemAsync(client, target, transport, "1402 random_write", async () =>
                {
                    await client.WriteRandomWordsAsync([(new SlmpDeviceAddress(SlmpDeviceCode.D, 135), (ushort)0x1234)], [(new SlmpDeviceAddress(SlmpDeviceCode.D, 235), 0x12345678)]).ConfigureAwait(false);
                    await client.WriteRandomBitsAsync([(new SlmpDeviceAddress(SlmpDeviceCode.M, 125), true)]).ConfigureAwait(false);
                    return "completed";
                }).ConfigureAwait(false));

                results.Add(await RunProbeItemAsync(client, target, transport, "1406 block_write", async () =>
                {
                    await client.WriteBlockAsync(
                        [new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.D, 140), [0x0001])],
                        [new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.M, 220), [0x0001])],
                        new SlmpBlockWriteOptions(SplitMixedBlocks: false, RetryMixedOnError: true)
                    ).ConfigureAwait(false);
                    return "completed";
                }).ConfigureAwait(false));
            }
        }
    }

    var reportDir = GetOption(args, "--report-dir", "internal_docs/validation/reports");
    Directory.CreateDirectory(reportDir);
    var mdPath = Path.Combine(reportDir, "compatibility_probe_latest.md");
    var jsonPath = Path.Combine(reportDir, "compatibility_probe_latest.json");
    var md = new StringBuilder();
    md.AppendLine("# Compatibility Probe Latest");
    md.AppendLine();
    md.AppendLine($"- Timestamp: {GetTimestamp()}");
    md.AppendLine($"- Host: {GetOption(args, "--host", "192.168.250.100")}");
    md.AppendLine($"- Port: {GetOption(args, "--port", "1025")}");
    md.AppendLine($"- Write check: {(writeCheck ? "enabled" : "disabled")}");
    md.AppendLine();
    md.AppendLine("| Target | Transport | Command | Status | Detail |");
    md.AppendLine("|---|---|---|---|---|");
    foreach (var row in results)
    {
        md.AppendLine($"| {row.Target} | {row.Transport} | {row.Name} | {row.Status} | {row.Detail.Replace("|", "/")} |");
    }
    await File.WriteAllTextAsync(mdPath, md.ToString(), Encoding.UTF8).ConfigureAwait(false);
#pragma warning disable CA1869 // One-time serialization in a CLI tool; caching not beneficial here
    var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
#pragma warning restore CA1869
    await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine($"[DONE] markdown={mdPath}");
    Console.WriteLine($"[DONE] json={jsonPath}");

    return results.Any(x => x.Status == "NG") ? 1 : 0;
}

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
    if (directTexts.Count == 0) directTexts.Add("0xFA");
    var directMemories = directTexts.Select(x => checked((byte)SlmpTargetParser.ParseAutoNumber(x))).ToArray();
    var writeCheck = HasFlag(args, "--write-check");
    var rows = new List<CoverageRow>();

    Console.WriteLine("=== G/HG Extended Device Coverage Sweep ===");
    Console.WriteLine($"Host={GetOption(args, "--host", "192.168.250.100")}, Port={GetOption(args, "--port", "1025")}, Transports=[{string.Join(", ", transports)}], Series={GetSeriesOption(args, "iqr")}");
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
            using var client = await CreateClientAsync(scopedArgs, target.Target, "iqr", "4e").ConfigureAwait(false);
            Console.WriteLine($"[INFO] Sweep target={target.Name}, transport={transport}, network=0x{target.Target.Network:X2}, station=0x{target.Target.Station:X2}, module_io=0x{target.Target.ModuleIo:X4}, multidrop=0x{target.Target.Multidrop:X2}");
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
                foreach (var directMemory in directMemories)
                {
                    var ext = new SlmpExtensionSpec(DirectMemorySpecification: directMemory);
                    foreach (var points in pointList)
                    {
                        try
                        {
                            var before = await client.ReadWordsExtendedAsync(qualified, points, ext).ConfigureAwait(false);
                            if (!writeCheck)
                            {
                                var detail = $"device={deviceText}, points={points}, before=[{string.Join(", ", before.Select(x => $"0x{x:X4}"))}], mode=read_only";
                                Console.WriteLine($"[OK] {target.Name} {deviceText} points={points} direct=0x{directMemory:X2}: {detail}");
                                rows.Add(new CoverageRow(target.Name, transport, deviceText, points, directMemory, "OK", detail));
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
                                ? $"device={deviceText}, points={points}, before=[{string.Join(", ", before.Select(x => $"0x{x:X4}"))}], write=[{string.Join(", ", write.Select(x => $"0x{x:X4}"))}], readback=[{string.Join(", ", readback.Select(x => $"0x{x:X4}"))}], readback_mismatch=yes, restore={restoredState}"
                                : $"device={deviceText}, points={points}, before=[{string.Join(", ", before.Select(x => $"0x{x:X4}"))}], write=[{string.Join(", ", write.Select(x => $"0x{x:X4}"))}], readback=[{string.Join(", ", readback.Select(x => $"0x{x:X4}"))}], restore={restoredState}";
                            var status = mismatch ? "NG" : "OK";
                            Console.WriteLine($"[{status}] {target.Name} {deviceText} points={points} direct=0x{directMemory:X2}: {resultDetail}");
                            rows.Add(new CoverageRow(target.Name, transport, deviceText, points, directMemory, status, resultDetail));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[NG] {target.Name} {deviceText} points={points} direct=0x{directMemory:X2}: {ex.Message}");
                            rows.Add(new CoverageRow(target.Name, transport, deviceText, points, directMemory, "NG", ex.Message));
                        }
                    }
                }
            }
        }
    }

    var reportDir = GetOption(args, "--report-dir", "internal_docs/validation/reports");
    Directory.CreateDirectory(reportDir);
    var reportPath = Path.Combine(reportDir, "g_hg_ExtendedDevice_coverage_latest.md");
    var report = new StringBuilder();
    report.AppendLine("# G/HG Extended Device Coverage Latest");
    report.AppendLine();
    report.AppendLine($"- Timestamp: {GetTimestamp()}");
    report.AppendLine($"- Host: {GetOption(args, "--host", "192.168.250.100")}");
    report.AppendLine($"- Port: {GetOption(args, "--port", "1025")}");
    report.AppendLine($"- Write check: {(writeCheck ? "enabled" : "disabled")}");
    report.AppendLine();
    report.AppendLine("| Target | Transport | Device | Points | Direct | Status | Detail |");
    report.AppendLine("|---|---|---|---|---|---|---|");
    foreach (var row in rows)
    {
        report.AppendLine($"| {row.Target} | {row.Transport} | {row.Device} | {row.Points} | 0x{row.DirectMemory:X2} | {row.Status} | {row.Detail.Replace("|", "/")} |");
    }
    await File.WriteAllTextAsync(reportPath, report.ToString(), Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine($"[DONE] report={reportPath}");
    return rows.Any(x => x.Status == "NG") ? 1 : 0;
}

async Task<int> RunCompatibilityMatrixRenderAsync(IReadOnlyList<string> args)
{
    var inputPaths = GetOptions(args, "--input");
    if (inputPaths.Count == 0)
    {
        inputPaths.Add("internal_docs/validation/reports/compatibility_probe_latest.json");
    }

    var loaded = new List<ProbeItemResult>();
    foreach (var input in inputPaths)
    {
        var text = await File.ReadAllTextAsync(input, Encoding.UTF8).ConfigureAwait(false);
        var items = JsonSerializer.Deserialize<List<ProbeItemResult>>(text) ?? [];
        loaded.AddRange(items);
    }

    var commands = loaded.Select(x => x.Name).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    var targets = loaded.Select(x => x.Target).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    var statusMap = new Dictionary<(string Target, string Command), string>();
    foreach (var row in loaded)
    {
        var key = (row.Target, row.Name);
        if (!statusMap.TryGetValue(key, out var existing))
        {
            statusMap[key] = row.Status;
            continue;
        }

        if (existing != "NG" && row.Status == "NG")
        {
            statusMap[key] = "NG";
        }
    }

    var outputPath = GetOption(args, "--output", "internal_docs/validation/reports/PLC_COMPATIBILITY_DOTNET.md");
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
    var sb = new StringBuilder();
    sb.AppendLine("# PLC Compatibility (Dotnet Probe)");
    sb.AppendLine();
    sb.AppendLine($"- Timestamp: {GetTimestamp()}");
    sb.AppendLine($"- Sources: {string.Join(", ", inputPaths)}");
    sb.AppendLine();
    sb.Append("| Target |");
    foreach (var command in commands) sb.Append($" {command} |");
    sb.AppendLine();
    sb.Append("|---|");
    foreach (var _unused in commands) sb.Append("---|");
    sb.AppendLine();
    foreach (var target in targets)
    {
        sb.Append($"| {target} |");
        foreach (var command in commands)
        {
            var status = statusMap.TryGetValue((target, command), out var s) ? s : "PENDING";
            sb.Append($" {status} |");
        }
        sb.AppendLine();
    }

    await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine($"[DONE] output={outputPath}");
    return 0;
}

async Task<int> RunExtendedDeviceDeviceRecheckAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    var deviceText = GetOption(args, "--device", @"U3E0\G10");
    var points = checked((ushort)SlmpTargetParser.ParseAutoNumber(GetOption(args, "--points", "1")));
    var directMemory = checked((byte)SlmpTargetParser.ParseAutoNumber(GetOption(args, "--direct-memory", "0xFA")));
    var writeCheck = HasFlag(args, "--write-check");
    using var client = await CreateClientAsync(args, target.Target, "iqr", "4e").ConfigureAwait(false);
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
    using var client = await CreateClientAsync(args, target.Target, "iqr", "4e").ConfigureAwait(false);
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
    using var client = await CreateClientAsync(args, target.Target, "iqr", "4e").ConfigureAwait(false);
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
            openedClients.Add(await CreateClientAsync(args, target.Target, "iqr", "4e").ConfigureAwait(false));
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

    using var client = await CreateClientAsync(args, target.Target, "iqr", "4e").ConfigureAwait(false);
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
    Console.WriteLine("  connection-check [--host ... --port ... --transport tcp|udp --series ql|iqr --frame-type 3e|4e --target SELF|SELF-CPU1|NW1-ST2|name,0x00,0xFF,0x03FF,0x00 --quiet]");
    Console.WriteLine("  other-station-check [--host ... --port ... --transport tcp|udp --series ql|iqr --frame-type 3e|4e --target ... (repeatable) --quiet]");
    Console.WriteLine("  random-check [--host ... --port ... --transport tcp|udp --target ... --write-check --quiet]");
    Console.WriteLine("  block-check [--host ... --port ... --transport tcp|udp --target ... --write-check --quiet]");
    Console.WriteLine("  compatibility-probe [--host ... --port ... --transport tcp|udp (repeatable) --target ... (repeatable) --write-check --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine("  compatibility-matrix-render [--input ... (repeatable) --output internal_docs/validation/reports/PLC_COMPATIBILITY_DOTNET.md --quiet]");
    Console.WriteLine(@"  ExtendedDevice-device-recheck [--host ... --port ... --transport tcp|udp --target SELF --device U3E0\G10 --points 1 --direct-memory 0xFA --write-check --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine(@"  g-hg-ExtendedDevice-coverage [--host ... --port ... --transport tcp|udp (repeatable) --target ... (repeatable) --device U3E0\G10 (repeatable) --points 1 (repeatable) --direct-memory 0xFA (repeatable) --write-check --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine("  read-soak [--host ... --port ... --transport tcp|udp --target SELF --device D1000 --points 1 --iterations 100 --interval-ms 0 --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine("  mixed-read-load [--host ... --port ... --transport tcp|udp --target SELF --iterations 100 --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine("  tcp-concurrency [--host ... --port ... --transport tcp --target SELF --clients 4 --iterations 50 --stagger-ms 50 --report-dir internal_docs/validation/reports --quiet]");
    Console.WriteLine("  single-connection-load [--host ... --port ... --transport tcp --target SELF --workers 4 --iterations 200 --report-dir internal_docs/validation/reports --quiet]");
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
        "other-station-check" => await RunOtherStationCheckAsync(argList).ConfigureAwait(false),
        "random-check" => await RunRandomCheckAsync(argList).ConfigureAwait(false),
        "block-check" => await RunBlockCheckAsync(argList).ConfigureAwait(false),
        "compatibility-probe" => await RunCompatibilityProbeAsync(argList).ConfigureAwait(false),
        "compatibility-matrix-render" => await RunCompatibilityMatrixRenderAsync(argList).ConfigureAwait(false),
        "ExtendedDevice-device-recheck" => await RunExtendedDeviceDeviceRecheckAsync(argList).ConfigureAwait(false),
        "g-hg-ExtendedDevice-coverage" => await RunGhCoverageAsync(argList).ConfigureAwait(false),
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

internal sealed record ProbeItemResult(string Target, string Transport, string Name, string Status, string Detail);

internal sealed record CoverageRow(
    string Target,
    string Transport,
    string Device,
    ushort Points,
    byte DirectMemory,
    string Status,
    string Detail
);
