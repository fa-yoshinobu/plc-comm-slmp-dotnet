using System.Globalization;
using PlcComm.Slmp;

if (args.Length > 0 && (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("Queued SLMP high-level sample");
    Console.WriteLine("  dotnet run --project samples/PlcComm.Slmp.QueuedSample -- [host] [port] [workers] [iterations]");
    return;
}

var host = args.Length > 0 ? args[0] : "192.168.250.100";
var port = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 1025;
var workers = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 4;
var iterations = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 10;

// This sample demonstrates the recommended application pattern:
// 1. open one queued client with explicit stable settings
// 2. share it across multiple tasks
// 3. use only the high-level helper APIs from SlmpClientExtensions
await using var client = await SlmpClient.OpenAndConnectAsync(
    host,
    port,
    SlmpFrameType.Frame4E,
    SlmpCompatibilityMode.Iqr).ConfigureAwait(false);

Console.WriteLine("[INFO] Using queued high-level client");
Console.WriteLine("[INFO] frame=4e series=iqr");
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
        var words = await client.ReadWordsAsync("D0", 4).ConfigureAwait(false);

        Console.WriteLine(
            $"[OK] worker={workerIndex + 1} iter={i + 1} " +
            $"D100={counter} D200:F={snapshot["D200:F"]} D50.3={snapshot["D50.3"]} " +
            $"D0..D3=[{string.Join(", ", words)}]");
    }
}).ToArray();

await Task.WhenAll(tasks).ConfigureAwait(false);
