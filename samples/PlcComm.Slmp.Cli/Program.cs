using System.Text;
using System.Text.Json;
using PlcComm.Slmp;

static string GetOption(IReadOnlyList<string> args, string name, string defaultValue)
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

static List<string> GetOptions(IReadOnlyList<string> args, string name)
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

static bool HasFlag(IReadOnlyList<string> args, string name) => args.Contains(name, StringComparer.OrdinalIgnoreCase);

static string GetTimestamp() => DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");

static async Task<SlmpClient> CreateClientAsync(IReadOnlyList<string> args, SlmpTargetAddress target)
{
    var host = GetOption(args, "--host", "192.168.250.101");
    var port = int.Parse(GetOption(args, "--port", "1025"));
    var transport = GetOption(args, "--transport", "tcp").Equals("udp", StringComparison.OrdinalIgnoreCase) ? SlmpTransportMode.Udp : SlmpTransportMode.Tcp;
    var frame = GetOption(args, "--frame-type", "auto");
    var series = GetOption(args, "--series", "auto");

    var client = new SlmpClient(host, port, transport) { TargetAddress = target };
    if (!string.Equals(frame, "auto", StringComparison.OrdinalIgnoreCase))
    {
        client.FrameType = frame.Equals("3e", StringComparison.OrdinalIgnoreCase) ? SlmpFrameType.Frame3E : SlmpFrameType.Frame4E;
    }

    if (!string.Equals(series, "auto", StringComparison.OrdinalIgnoreCase))
    {
        client.CompatibilityMode = series.Equals("ql", StringComparison.OrdinalIgnoreCase) || series.Equals("legacy", StringComparison.OrdinalIgnoreCase)
            ? SlmpCompatibilityMode.Legacy
            : SlmpCompatibilityMode.Iqr;
    }

    await client.OpenAsync().ConfigureAwait(false);
    if (string.Equals(frame, "auto", StringComparison.OrdinalIgnoreCase) || string.Equals(series, "auto", StringComparison.OrdinalIgnoreCase))
    {
        var profile = await client.ResolveProfileAsync().ConfigureAwait(false);
        client.FrameType = profile.FrameType;
        client.CompatibilityMode = profile.CompatibilityMode;
        Console.WriteLine($"[INFO] Resolved frame={(profile.FrameType == SlmpFrameType.Frame3E ? "3e" : "4e")}, series={(profile.CompatibilityMode == SlmpCompatibilityMode.Legacy ? "ql" : "iqr")}");
    }

    return client;
}

static IReadOnlyList<SlmpNamedTarget> ParseTargets(IReadOnlyList<string> args)
{
    var targetInputs = GetOptions(args, "--target");
    if (targetInputs.Count == 0)
    {
        targetInputs.Add("SELF");
    }
    return SlmpTargetParser.ParseMany(targetInputs);
}

static async Task<int> RunConnectionCheckAsync(IReadOnlyList<string> args)
{
    var target = ParseTargets(args)[0];
    using var client = await CreateClientAsync(args, target.Target).ConfigureAwait(false);
    var values = await client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1).ConfigureAwait(false);
    Console.WriteLine($"[OK] Read SM400 values=[{values[0]}]");
    return 0;
}

static string FormatTarget(SlmpNamedTarget row)
{
    return $"name={row.Name}, network=0x{row.Target.Network:X2}, station=0x{row.Target.Station:X2}, module_io=0x{row.Target.ModuleIo:X4}, multidrop=0x{row.Target.Multidrop:X2}";
}

static async Task<int> RunOtherStationCheckAsync(IReadOnlyList<string> args)
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

static async Task<int> RunRandomCheckAsync(IReadOnlyList<string> args)
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

static async Task<int> RunBlockCheckAsync(IReadOnlyList<string> args)
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
        new SlmpBlockWriteOptions(SplitMixedBlocks: false, RetryMixedOnError: true)
    ).ConfigureAwait(false);
    Console.WriteLine("[OK] block-write completed");
    return 0;
}

static async Task<ProbeItemResult> RunProbeItemAsync(SlmpClient client, SlmpNamedTarget target, string transport, string name, Func<Task<string>> body)
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

static async Task<int> RunCompatibilityProbeAsync(IReadOnlyList<string> args)
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
            using var client = await CreateClientAsync(scopedArgs, target.Target).ConfigureAwait(false);
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

    var reportDir = GetOption(args, "--report-dir", "docs/validation/reports");
    Directory.CreateDirectory(reportDir);
    var mdPath = Path.Combine(reportDir, "compatibility_probe_latest.md");
    var jsonPath = Path.Combine(reportDir, "compatibility_probe_latest.json");
    var md = new StringBuilder();
    md.AppendLine("# Compatibility Probe Latest");
    md.AppendLine();
    md.AppendLine($"- Timestamp: {GetTimestamp()}");
    md.AppendLine($"- Host: {GetOption(args, "--host", "192.168.250.101")}");
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
    var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine($"[DONE] markdown={mdPath}");
    Console.WriteLine($"[DONE] json={jsonPath}");

    return results.Any(x => x.Status == "NG") ? 1 : 0;
}

static async Task<int> RunGhCoverageAsync(IReadOnlyList<string> args)
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

    Console.WriteLine("=== G/HG Appendix 1 Coverage Sweep ===");
    Console.WriteLine($"Host={GetOption(args, "--host", "192.168.250.101")}, Port={GetOption(args, "--port", "1025")}, Transports=[{string.Join(", ", transports)}], Series={GetOption(args, "--series", "auto")}");
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

    var reportDir = GetOption(args, "--report-dir", "docs/validation/reports");
    Directory.CreateDirectory(reportDir);
    var reportPath = Path.Combine(reportDir, "g_hg_appendix1_coverage_latest.md");
    var report = new StringBuilder();
    report.AppendLine("# G/HG Appendix 1 Coverage Latest");
    report.AppendLine();
    report.AppendLine($"- Timestamp: {GetTimestamp()}");
    report.AppendLine($"- Host: {GetOption(args, "--host", "192.168.250.101")}");
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

if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h"))
{
    Console.WriteLine("SLMP .NET CLI");
    Console.WriteLine("  connection-check [--host ... --port ... --transport tcp|udp --series auto|ql|iqr --frame-type auto|3e|4e --target SELF|SELF-CPU1|NW1-ST2|name,0x00,0xFF,0x03FF,0x00]");
    Console.WriteLine("  other-station-check [--host ... --port ... --transport tcp|udp --target ... (repeatable)]");
    Console.WriteLine("  random-check [--host ... --port ... --transport tcp|udp --target ... --write-check]");
    Console.WriteLine("  block-check [--host ... --port ... --transport tcp|udp --target ... --write-check]");
    Console.WriteLine("  compatibility-probe [--host ... --port ... --transport tcp|udp (repeatable) --target ... (repeatable) --write-check --report-dir docs/validation/reports]");
    Console.WriteLine(@"  g-hg-appendix1-coverage [--host ... --port ... --transport tcp|udp (repeatable) --target ... (repeatable) --device U3E0\G10 (repeatable) --points 1 (repeatable) --direct-memory 0xFA (repeatable) --write-check --report-dir docs/validation/reports]");
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
        "g-hg-appendix1-coverage" => await RunGhCoverageAsync(argList).ConfigureAwait(false),
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
