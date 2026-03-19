using PlcComm.Slmp;

if (args.Length > 0 && (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("Queued SLMP sample");
    Console.WriteLine("  dotnet run --project samples/PlcComm.Slmp.QueuedSample -- [host] [port] [workers] [iterations]");
    return;
}

var host = args.Length > 0 ? args[0] : "192.168.250.101";
var port = args.Length > 1 ? int.Parse(args[1]) : 1025;
var workers = args.Length > 2 ? int.Parse(args[2]) : 4;
var iterations = args.Length > 3 ? int.Parse(args[3]) : 10;

using var rawClient = new SlmpClient(host, port, SlmpTransportMode.Tcp)
{
    TargetAddress = SlmpTargetParser.ParseNamed("SELF").Target,
};
await using var queuedClient = new QueuedSlmpClient(rawClient);

var profile = await queuedClient.ResolveProfileAsync().ConfigureAwait(false);
queuedClient.FrameType = profile.FrameType;
queuedClient.CompatibilityMode = profile.CompatibilityMode;
await queuedClient.OpenAsync().ConfigureAwait(false);

Console.WriteLine($"[INFO] Resolved frame={(profile.FrameType == SlmpFrameType.Frame3E ? "3e" : "4e")}, series={(profile.CompatibilityMode == SlmpCompatibilityMode.Legacy ? "ql" : "iqr")}");

var tasks = Enumerable.Range(0, workers).Select(async workerIndex =>
{
    for (var i = 0; i < iterations; i++)
    {
        var sm400 = await queuedClient.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1).ConfigureAwait(false);
        var d1000 = await queuedClient.ReadWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 1000), 1).ConfigureAwait(false);
        Console.WriteLine($"[OK] worker={workerIndex + 1} iter={i + 1} sm400={sm400[0]} d1000={d1000[0]}");
    }
}).ToArray();

await Task.WhenAll(tasks).ConfigureAwait(false);
