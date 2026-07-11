using System.Globalization;
using PlcComm.Slmp;

// Demonstrates the recommended shared-connection pattern:
// pass a canonical profile string such as "melsec:iq-r", open one
// QueuedSlmpClient, and let concurrent workers use high-level helpers.
if (args.Length > 0 && (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("Queued SLMP high-level sample");
    Console.WriteLine("  dotnet run --project samples/PlcComm.Slmp.QueuedSample -- <host> <port> <plc-profile> <tcp|udp> <target> [workers] [iterations]");
    return;
}

if (args.Length < 5)
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/PlcComm.Slmp.QueuedSample -- <host> <port> <plc-profile> <tcp|udp> <target> [workers] [iterations]");
    Environment.ExitCode = 2;
    return;
}
var host = args[0];
var port = int.Parse(args[1], CultureInfo.InvariantCulture);
var plcProfileArg = args[2];
var transport = args[3].Equals("tcp", StringComparison.OrdinalIgnoreCase) ? SlmpTransportMode.Tcp
    : args[3].Equals("udp", StringComparison.OrdinalIgnoreCase) ? SlmpTransportMode.Udp
    : throw new ArgumentException("transport must be tcp or udp");
var target = SlmpTargetParser.ParseNamed(args[4]).Target;
var workers = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 4;
var iterations = args.Length > 6 ? int.Parse(args[6], CultureInfo.InvariantCulture) : 10;
var plcProfile = SlmpPlcProfiles.Parse(plcProfileArg);

// This sample demonstrates the recommended application pattern:
// 1. open one queued client with one explicit PLC profile
// 2. share it across multiple tasks
// 3. use only the high-level helper APIs from SlmpClientExtensions
var options = new SlmpConnectionOptions(host, plcProfile, port, transport, target);
await using var client = await SlmpClientFactory.OpenAndConnectAsync(options).ConfigureAwait(false);

Console.WriteLine("[INFO] Using queued high-level client");
Console.WriteLine($"[INFO] plc_profile={SlmpPlcProfiles.ToCanonicalString(plcProfile)} frame={client.FrameType} compatibility={client.CompatibilityMode}");
Console.WriteLine($"[INFO] workers={workers} iterations={iterations}");

var tasks = Enumerable.Range(0, workers).Select(async workerIndex =>
{
    for (var i = 0; i < iterations; i++)
    {
        // Example 1: typed scalar read
        var counter = await client.ReadTypedAsync("D100", "U").ConfigureAwait(false);

        // Example 2: mixed named read (not atomic when command families differ)
        var snapshot = await client.ReadNamedAsync(["D100:U", "D200:F", "D50.3"]).ConfigureAwait(false);

        // Example 3: one explicit single-request block read
        var words = await client.ReadWordsSingleRequestAsync("D0", 4).ConfigureAwait(false);

        Console.WriteLine(
            $"[OK] worker={workerIndex + 1} iter={i + 1} " +
            $"D100={counter} D100:U={snapshot["D100:U"]} D200:F={snapshot["D200:F"]} D50.3={snapshot["D50.3"]} " +
            $"D0..D3=[{string.Join(", ", words)}]");
    }
}).ToArray();

await Task.WhenAll(tasks).ConfigureAwait(false);
