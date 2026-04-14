using System.Globalization;
using PlcComm.Slmp;

if (args.Length > 0 && (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("Queued SLMP high-level sample");
    Console.WriteLine("  dotnet run --project samples/PlcComm.Slmp.QueuedSample -- [host] [port] [plc-family] [workers] [iterations]");
    return;
}

var host = args.Length > 0 ? args[0] : "192.168.250.100";
var port = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 1025;
var plcFamilyArg = args.Length > 2 ? args[2].ToLowerInvariant() : "iq-r";
var workers = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 4;
var iterations = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 10;
var plcFamily = plcFamilyArg switch
{
    "iq-f" => SlmpPlcFamily.IqF,
    "iq-r" => SlmpPlcFamily.IqR,
    "iq-l" => SlmpPlcFamily.IqL,
    "mx-f" => SlmpPlcFamily.MxF,
    "mx-r" => SlmpPlcFamily.MxR,
    "qcpu" => SlmpPlcFamily.QCpu,
    "lcpu" => SlmpPlcFamily.LCpu,
    "qnu" => SlmpPlcFamily.QnU,
    "qnudv" => SlmpPlcFamily.QnUDV,
    _ => throw new ArgumentException("plc-family must be iq-f, iq-r, iq-l, mx-f, mx-r, qcpu, lcpu, qnu, or qnudv"),
};

// This sample demonstrates the recommended application pattern:
// 1. open one queued client with one explicit PLC family
// 2. share it across multiple tasks
// 3. use only the high-level helper APIs from SlmpClientExtensions
var options = new SlmpConnectionOptions(host, plcFamily)
{
    Port = port,
};
await using var client = await SlmpClientFactory.OpenAndConnectAsync(options).ConfigureAwait(false);

Console.WriteLine("[INFO] Using queued high-level client");
Console.WriteLine($"[INFO] plc_family={plcFamilyArg} frame={client.FrameType} series={client.CompatibilityMode}");
Console.WriteLine($"[INFO] workers={workers} iterations={iterations}");

var tasks = Enumerable.Range(0, workers).Select(async workerIndex =>
{
    for (var i = 0; i < iterations; i++)
    {
        // Example 1: typed scalar read
        var counter = await client.ReadTypedAsync("D100", "U").ConfigureAwait(false);

        // Example 2: mixed snapshot read
        var snapshot = await client.ReadNamedAsync(["D100", "D200:F", "D50.3"]).ConfigureAwait(false);

        // Example 3: chunked helper call
        var words = await client.ReadWordsSingleRequestAsync("D0", 4).ConfigureAwait(false);

        Console.WriteLine(
            $"[OK] worker={workerIndex + 1} iter={i + 1} " +
            $"D100={counter} D200:F={snapshot["D200:F"]} D50.3={snapshot["D50.3"]} " +
            $"D0..D3=[{string.Join(", ", words)}]");
    }
}).ToArray();

await Task.WhenAll(tasks).ConfigureAwait(false);
