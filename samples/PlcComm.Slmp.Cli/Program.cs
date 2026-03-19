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

static bool HasFlag(IReadOnlyList<string> args, string name) => args.Contains(name, StringComparer.OrdinalIgnoreCase);

static int ParseNumber(string text)
{
    if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        return Convert.ToInt32(text[2..], 16);
    }

    return int.Parse(text);
}

static SlmpTargetAddress ParseTarget(string text)
{
    if (string.Equals(text, "SELF", StringComparison.OrdinalIgnoreCase))
    {
        return new SlmpTargetAddress(0x00, 0xFF, 0x03FF, 0x00);
    }

    var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 5)
    {
        return new SlmpTargetAddress(
            checked((byte)ParseNumber(parts[1])),
            checked((byte)ParseNumber(parts[2])),
            checked((ushort)ParseNumber(parts[3])),
            checked((byte)ParseNumber(parts[4]))
        );
    }

    if (text.StartsWith("NW", StringComparison.OrdinalIgnoreCase) && text.Contains("-ST", StringComparison.OrdinalIgnoreCase))
    {
        var normalized = text.ToUpperInvariant();
        var st = normalized.IndexOf("-ST", StringComparison.Ordinal);
        var nw = byte.Parse(normalized[2..st]);
        var station = byte.Parse(normalized[(st + 3)..]);
        return new SlmpTargetAddress(nw, station, 0x03FF, 0x00);
    }

    throw new ArgumentException($"Unsupported target: {text}");
}

static async Task<SlmpClient> CreateClientAsync(IReadOnlyList<string> args)
{
    var host = GetOption(args, "--host", "192.168.250.101");
    var port = int.Parse(GetOption(args, "--port", "1025"));
    var transport = GetOption(args, "--transport", "tcp").Equals("udp", StringComparison.OrdinalIgnoreCase) ? SlmpTransportMode.Udp : SlmpTransportMode.Tcp;
    var frame = GetOption(args, "--frame-type", "auto");
    var series = GetOption(args, "--series", "auto");
    var targetText = GetOption(args, "--target", "SELF");

    var client = new SlmpClient(host, port, transport)
    {
        TargetAddress = ParseTarget(targetText),
    };

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

    try
    {
        await client.OpenAsync().ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        client.Dispose();
        throw new Exception($"open failed host={host} port={port} transport={transport}: {ex.Message}", ex);
    }

    if (string.Equals(frame, "auto", StringComparison.OrdinalIgnoreCase) || string.Equals(series, "auto", StringComparison.OrdinalIgnoreCase))
    {
        var profile = await client.ResolveProfileAsync().ConfigureAwait(false);
        client.FrameType = profile.FrameType;
        client.CompatibilityMode = profile.CompatibilityMode;
        Console.WriteLine($"[INFO] Resolved frame={(profile.FrameType == SlmpFrameType.Frame3E ? "3e" : "4e")}, series={(profile.CompatibilityMode == SlmpCompatibilityMode.Legacy ? "ql" : "iqr")}");
    }

    return client;
}

static async Task<int> RunConnectionCheckAsync(IReadOnlyList<string> args)
{
    using var client = await CreateClientAsync(args).ConfigureAwait(false);
    var values = await client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1).ConfigureAwait(false);
    Console.WriteLine($"[OK] Read SM400 values=[{values[0]}]");
    return 0;
}

static async Task<int> RunOtherStationCheckAsync(IReadOnlyList<string> args)
{
    var targetRaw = GetOption(args, "--target", "SELF");
    using var client = await CreateClientAsync(args).ConfigureAwait(false);

    var value = await client.ReadWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 1000), 1).ConfigureAwait(false);
    Console.WriteLine($"[OK] {targetRaw}: values=[{value[0]}]");
    try
    {
        var info = await client.ReadTypeNameAsync().ConfigureAwait(false);
        Console.WriteLine($"[INFO] Read Type Name: model={info.Model}, model_code=0x{info.ModelCode:X4} ({info.ModelCode})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[INFO] Read Type Name unavailable: {ex.Message}");
    }

    return 0;
}

static async Task<int> RunRandomCheckAsync(IReadOnlyList<string> args)
{
    using var client = await CreateClientAsync(args).ConfigureAwait(false);
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
    using var client = await CreateClientAsync(args).ConfigureAwait(false);
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

if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h"))
{
    Console.WriteLine("SLMP .NET CLI");
    Console.WriteLine("  connection-check [--host ... --port ... --transport tcp|udp --series auto|ql|iqr --frame-type auto|3e|4e]");
    Console.WriteLine("  other-station-check [--host ... --port ... --transport tcp|udp --target SELF|NW1-ST2|name,0x00,0xFF,0x03FF,0x00]");
    Console.WriteLine("  random-check [--host ... --port ... --transport tcp|udp --target ... --write-check]");
    Console.WriteLine("  block-check [--host ... --port ... --transport tcp|udp --target ... --write-check]");
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
