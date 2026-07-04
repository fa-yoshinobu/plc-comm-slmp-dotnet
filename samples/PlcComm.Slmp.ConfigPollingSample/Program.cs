using System.Text.Json;
using PlcComm.Slmp;
using PlcComm.Slmp.Samples;

var options = ConfigPollingOptions.Parse(args);
if (options is null)
    return 2;

var plan = PollingPlan.Load(options);
if (plan.MaxBackoff < plan.InitialBackoff)
    throw new ArgumentException("max_backoff must be greater than or equal to initial_backoff.");

if (options.DryRun)
{
    foreach (var endpoint in plan.Endpoints)
    {
        Console.WriteLine($"{endpoint.Name}: {OperationalCommon.TransportLabel(endpoint.Transport)} {endpoint.Host}:{endpoint.Port} profile={SlmpPlcProfiles.ToCanonicalString(endpoint.PlcProfile)} interval={endpoint.Interval.TotalSeconds:0.###}s");
        Console.WriteLine("  tags: " + string.Join(", ", plan.TagsByPlc[endpoint.Name].Select(tag => $"{tag.Name}={tag.Address}")));
    }
    Console.WriteLine($"cycles: {(plan.Cycles is null ? "forever" : plan.Cycles.Value.ToString())}");
    if (plan.CsvPath is not null)
        Console.WriteLine($"csv: {plan.CsvPath}");
    return 0;
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

using var writer = plan.CsvPath is null ? null : new CsvSnapshotWriter(plan.CsvPath);
Func<PlcEndpoint, IReadOnlyDictionary<string, object>, CancellationToken, Task> handler =
    writer is null ? OperationalCommon.IgnoreSnapshotAsync : writer.WriteSnapshotAsync;

await Task.WhenAll(plan.Endpoints.Select(endpoint =>
    OperationalCommon.MonitorEndpointAsync(
        endpoint,
        plan.TagsByPlc[endpoint.Name],
        plan.Cycles,
        plan.InitialBackoff,
        plan.MaxBackoff,
        handler,
        shutdown.Token)));

return 0;

internal sealed record ConfigPollingOptions(
    string ConfigPath,
    int? CyclesOverride,
    bool Once,
    TimeSpan? InitialBackoffOverride,
    TimeSpan? MaxBackoffOverride,
    bool DryRun)
{
    public static ConfigPollingOptions? Parse(string[] args)
    {
        string? configPath = null;
        int? cycles = null;
        var once = false;
        TimeSpan? initialBackoff = null;
        TimeSpan? maxBackoff = null;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    PrintUsage();
                    return null;
                case "--config":
                    configPath = RequireValue(args, ref i, arg);
                    break;
                case "--cycles":
                    cycles = OperationalCommon.ParsePositiveInt(RequireValue(args, ref i, arg), arg);
                    break;
                case "--once":
                    once = true;
                    break;
                case "--initial-backoff":
                    initialBackoff = TimeSpan.FromSeconds(OperationalCommon.ParsePositiveDouble(RequireValue(args, ref i, arg), arg));
                    break;
                case "--max-backoff":
                    maxBackoff = TimeSpan.FromSeconds(OperationalCommon.ParsePositiveDouble(RequireValue(args, ref i, arg), arg));
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentException("--config is required.");
        return new ConfigPollingOptions(configPath, cycles, once, initialBackoff, maxBackoff, dryRun);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{option} requires a value.");
        index++;
        return args[index];
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Run read-only SLMP polling from a JSON config file.");
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project samples/PlcComm.Slmp.ConfigPollingSample -- --config samples/PlcComm.Slmp.ConfigPollingSample/config_polling.example.json --dry-run");
        Console.WriteLine("YAML config is available only in the Python sample.");
    }
}

internal sealed record PollingPlan(
    IReadOnlyList<PlcEndpoint> Endpoints,
    IReadOnlyDictionary<string, IReadOnlyList<TagSpec>> TagsByPlc,
    string? CsvPath,
    int? Cycles,
    TimeSpan InitialBackoff,
    TimeSpan MaxBackoff)
{
    public static PollingPlan Load(ConfigPollingOptions options)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(options.ConfigPath));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("config root must be an object.");

        var defaults = root.TryGetProperty("defaults", out var defaultsElement) ? RequireObject(defaultsElement, "defaults") : default;
        var output = root.TryGetProperty("output", out var outputElement) ? RequireObject(outputElement, "output") : default;
        var defaultTransport = GetString(defaults, "transport", "tcp");
        var defaultPort = GetOptionalInt(defaults, "port") ?? 1025;
        var defaultTimeout = TimeSpan.FromSeconds(GetPositiveDouble(defaults, "timeout", 3));
        var defaultInterval = TimeSpan.FromSeconds(GetPositiveDouble(defaults, "interval", 1));
        var defaultProfile = GetOptionalString(defaults, "plc_profile");

        if (!root.TryGetProperty("plcs", out var plcsElement) || plcsElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("plcs must be a list.");

        var endpoints = new List<PlcEndpoint>();
        var tagsByPlc = new Dictionary<string, IReadOnlyList<TagSpec>>(StringComparer.Ordinal);
        var index = 0;
        foreach (var plcElement in plcsElement.EnumerateArray())
        {
            var plc = RequireObject(plcElement, $"plcs[{index}]");
            var name = GetRequiredString(plc, "name", $"plcs[{index}].name");
            var host = GetRequiredString(plc, "host", $"plcs[{index}].host");
            var profileText = GetOptionalString(plc, "plc_profile") ?? defaultProfile
                ?? throw new ArgumentException($"plcs[{index}].plc_profile is required.");
            var endpoint = new PlcEndpoint(
                name,
                host,
                SlmpPlcProfiles.Parse(profileText),
                GetOptionalInt(plc, "port") ?? defaultPort,
                OperationalCommon.ParseTransport(GetString(plc, "transport", defaultTransport)),
                TimeSpan.FromSeconds(GetPositiveDouble(plc, "timeout", defaultTimeout.TotalSeconds)),
                TimeSpan.FromSeconds(GetPositiveDouble(plc, "interval", defaultInterval.TotalSeconds)));
            endpoints.Add(endpoint);
            tagsByPlc[name] = ParseTags(plc, name);
            index++;
        }

        if (endpoints.Count == 0)
            throw new ArgumentException("plcs must contain at least one PLC.");

        var cycles = options.Once ? 1 : options.CyclesOverride ?? GetOptionalInt(root, "cycles");
        var initialBackoff = options.InitialBackoffOverride ?? TimeSpan.FromSeconds(GetPositiveDouble(root, "initial_backoff", 1));
        var maxBackoff = options.MaxBackoffOverride ?? TimeSpan.FromSeconds(GetPositiveDouble(root, "max_backoff", 30));
        var csvPath = ResolveOutputPath(options.ConfigPath, GetOptionalString(output, "csv"));

        return new PollingPlan(endpoints, tagsByPlc, csvPath, cycles, initialBackoff, maxBackoff);
    }

    private static List<TagSpec> ParseTags(JsonElement plc, string plcName)
    {
        if (!plc.TryGetProperty("tags", out var tagsElement) || tagsElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException($"plcs[{plcName}].tags must be a list.");

        var tags = new List<TagSpec>();
        var index = 0;
        foreach (var item in tagsElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                tags.Add(OperationalCommon.ParseTagSpec(item.GetString() ?? string.Empty));
            }
            else
            {
                var tag = RequireObject(item, $"plcs[{plcName}].tags[{index}]");
                tags.Add(new TagSpec(
                    GetRequiredString(tag, "name", $"plcs[{plcName}].tags[{index}].name"),
                    GetRequiredString(tag, "address", $"plcs[{plcName}].tags[{index}].address")));
            }
            index++;
        }

        if (tags.Count == 0)
            throw new ArgumentException($"plcs[{plcName}].tags must contain at least one tag.");
        return tags;
    }

    private static JsonElement RequireObject(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"{name} must be an object.");
        return element;
    }

    private static string GetRequiredString(JsonElement element, string propertyName, string displayName)
        => GetOptionalString(element, propertyName) ?? throw new ArgumentException($"{displayName} is required.");

    private static string GetString(JsonElement element, string propertyName, string defaultValue)
        => GetOptionalString(element, propertyName) ?? defaultValue;

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"{propertyName} must be a string.");
        return value.GetString();
    }

    private static int? GetOptionalInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var value))
            return null;
        if (!value.TryGetInt32(out var parsed))
            throw new ArgumentException($"{propertyName} must be an integer.");
        if (parsed <= 0)
            throw new ArgumentException($"{propertyName} must be greater than zero.");
        return parsed;
    }

    private static double GetPositiveDouble(JsonElement element, string propertyName, double defaultValue)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var value))
            return defaultValue;
        if (!value.TryGetDouble(out var parsed))
            throw new ArgumentException($"{propertyName} must be a number.");
        if (parsed <= 0)
            throw new ArgumentException($"{propertyName} must be greater than zero.");
        return parsed;
    }

    private static string? ResolveOutputPath(string configPath, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return null;
        if (Path.IsPathRooted(outputPath))
            return outputPath;
        var directory = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(directory, outputPath));
    }
}

internal sealed class CsvSnapshotWriter(string path) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task WriteSnapshotAsync(
        PlcEndpoint endpoint,
        IReadOnlyDictionary<string, object> snapshot,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream);
            if (writeHeader)
                await writer.WriteLineAsync("timestamp,plc,tag,value").ConfigureAwait(false);

            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            foreach (var item in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(string.Join(
                    ",",
                    CsvEscape(timestamp),
                    CsvEscape(endpoint.Name),
                    CsvEscape(item.Key),
                    CsvEscape(OperationalCommon.FormatValue(item.Value)))).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string CsvEscape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public void Dispose() => _gate.Dispose();
}
