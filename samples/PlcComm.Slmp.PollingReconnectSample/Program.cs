using System.Globalization;
using System.IO;
using System.Net.Sockets;
using PlcComm.Slmp;

if (args.Length < 7)
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/PlcComm.Slmp.PollingReconnectSample -- <host> <port> <plc-profile> <device> <dtype> <tcp|udp> <target> [interval-seconds]");
    return;
}

var host = args[0];
var port = int.Parse(args[1], CultureInfo.InvariantCulture);
var plcProfile = SlmpPlcProfiles.Parse(args[2]);
var device = args[3];
var dtype = args[4];
var transport = ParseTransport(args[5]);
var target = SlmpTargetParser.ParseNamed(args[6]).Target;
var interval = TimeSpan.FromSeconds(ParseDouble(args.ElementAtOrDefault(7), 1.0));
var initialBackoff = TimeSpan.FromSeconds(1);
var maxBackoff = TimeSpan.FromSeconds(30);

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

QueuedSlmpClient? client = null;
var backoff = initialBackoff;
var connectedOnce = false;

try
{
    while (!shutdown.IsCancellationRequested)
    {
        if (client is null)
        {
            Log("reconnecting", $"{TransportLabel(transport)} {host}:{port} profile={SlmpPlcProfiles.ToCanonicalString(plcProfile)}");
            try
            {
                var options = new SlmpConnectionOptions(host, plcProfile, port, transport, target)
                {
                    Timeout = TimeSpan.FromSeconds(3),
                };
                client = await SlmpClientFactory.OpenAndConnectAsync(options, shutdown.Token);
            }
            catch (Exception ex) when (IsRetryable(ex) && !shutdown.IsCancellationRequested)
            {
                Log("reconnecting", $"connect failed: {Describe(ex)}; retry in {backoff.TotalSeconds:0.0}s");
                await Delay(backoff, shutdown.Token);
                backoff = NextBackoff(backoff, maxBackoff);
                continue;
            }

            Log(connectedOnce ? "recovered" : "connected", $"{device}:{dtype}");
            connectedOnce = true;
            backoff = initialBackoff;
        }

        try
        {
            var value = await client.ReadTypedAsync(device, dtype, shutdown.Token);
            Log("read", $"{device}:{dtype}={FormatValue(value)}");
            await Task.Delay(interval, shutdown.Token);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex) when (IsRetryable(ex) && !shutdown.IsCancellationRequested)
        {
            Log("lost", Describe(ex));
            await DisposeClientAsync(client);
            client = null;
            Log("reconnecting", $"retry in {backoff.TotalSeconds:0.0}s");
            await Delay(backoff, shutdown.Token);
            backoff = NextBackoff(backoff, maxBackoff);
        }
    }
}
finally
{
    await DisposeClientAsync(client);
}

Log("closed", "stopped");

static bool IsRetryable(Exception ex)
    => ex is IOException or SocketException or TimeoutException or OperationCanceledException
       || ex is SlmpError { EndCode: null };

static string Describe(Exception ex)
    => ex is SlmpError { EndCode: { } endCode } ? $"{ex.Message} (end_code=0x{endCode:X4})" : ex.Message;

static async Task DisposeClientAsync(QueuedSlmpClient? client)
{
    if (client is not null)
    {
        await client.DisposeAsync();
    }
}

static async Task Delay(TimeSpan delay, CancellationToken cancellationToken)
{
    try
    {
        await Task.Delay(delay, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
}

static TimeSpan NextBackoff(TimeSpan current, TimeSpan max)
    => TimeSpan.FromSeconds(Math.Min(current.TotalSeconds * 2.0, max.TotalSeconds));

static double ParseDouble(string? value, double fallback)
    => string.IsNullOrWhiteSpace(value) ? fallback : double.Parse(value, CultureInfo.InvariantCulture);

static SlmpTransportMode ParseTransport(string value)
{
    if (value.Equals("tcp", StringComparison.OrdinalIgnoreCase))
    {
        return SlmpTransportMode.Tcp;
    }
    if (value.Equals("udp", StringComparison.OrdinalIgnoreCase))
    {
        return SlmpTransportMode.Udp;
    }
    throw new ArgumentException("Transport must be tcp or udp.", nameof(value));
}

static string TransportLabel(SlmpTransportMode transport)
    => transport == SlmpTransportMode.Udp ? "udp" : "tcp";

static string FormatValue(object value)
    => value switch
    {
        float f => f.ToString("G9", CultureInfo.InvariantCulture),
        double d => d.ToString("G17", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

static void Log(string state, string message)
    => Console.WriteLine($"{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss} [{state}] {message}");
