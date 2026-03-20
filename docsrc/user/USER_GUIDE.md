# User Guide

## Build

```bash
dotnet build PlcCommSlmp.sln
```

## CLI Commands

### connection-check

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- connection-check \
  --host 192.168.250.101 --port 1025 --transport tcp \
  --series auto --frame-type auto
```

This command:

1. Opens SLMP transport.
2. Resolves profile (`3E/4E` + `legacy/iqr`) when `auto` is selected.
3. Reads `SM400` for a quick communication check.

### other-station-check

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- other-station-check \
  --host 192.168.250.101 --port 1025 --transport tcp --target NW1-ST2
```

Supported target forms:

- `SELF`
- `SELF-CPU1` .. `SELF-CPU4`
- `NWx-STy` (for example `NW2-ST1`)
- `name,0xNN,0xSS,0xIIII,0xMM`

### compatibility-probe

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- compatibility-probe \
  --host 192.168.250.101 --port 1025 --transport tcp \
  --target SELF --write-check
```

Outputs:

- `docsrc/validation/reports/compatibility_probe_latest.md`
- `docsrc/validation/reports/compatibility_probe_latest.json`

### g-hg-appendix1-coverage

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- g-hg-appendix1-coverage \
  --host 192.168.250.101 --port 1025 --transport tcp \
  --target SELF-CPU1 --device U3E0\\G10 --device U3E0\\HG20 \
  --points 1 --points 4 --direct-memory 0xFA --write-check
```

Output:

- `docsrc/validation/reports/g_hg_appendix1_coverage_latest.md`

### compatibility-matrix-render

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- compatibility-matrix-render \
  --input docsrc/validation/reports/compatibility_probe_latest.json
```

Output:

- `docsrc/validation/reports/PLC_COMPATIBILITY_DOTNET.md`

### appendix1-device-recheck

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- appendix1-device-recheck \
  --host 192.168.250.101 --port 1025 --transport tcp \
  --target SELF-CPU1 --device U3E0\\G10 --points 1 --direct-memory 0xFA --write-check
```

Output:

- `docsrc/validation/reports/appendix1_device_recheck_latest.md`

### read-soak / mixed-read-load / tcp-concurrency

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- read-soak --host 192.168.250.101 --target SELF --iterations 1000
dotnet run --project samples/PlcComm.Slmp.Cli -- mixed-read-load --host 192.168.250.101 --target SELF --iterations 1000
dotnet run --project samples/PlcComm.Slmp.Cli -- tcp-concurrency --host 192.168.250.101 --target SELF --clients 8 --iterations 200
```

Practical note for the currently verified environment: direct multi-connection TCP is unstable beyond one connection on TCP/1025. Prefer `single-connection-load` for parallel work on one PLC path. The CLI now caches auto profile resolution per process, so repeated operations can keep `--series auto --frame-type auto` without re-probing every client.

For app-side reuse, use `QueuedSlmpClient` to share one `SlmpClient` across multiple tasks while serializing requests on that one connection.

Standalone sample:

```bash
dotnet run --project samples/PlcComm.Slmp.QueuedSample -- 192.168.250.101 1025 4 10
```

## Library Example

```csharp
using PlcComm.Slmp;

using var client = new SlmpClient("192.168.250.101", 1025, SlmpTransportMode.Tcp)
{
    FrameType = SlmpFrameType.Frame4E,
    CompatibilityMode = SlmpCompatibilityMode.Iqr,
};

await client.OpenAsync();
var words = await client.ReadWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 100), 2);
await client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 100), new ushort[] { 1, 2 });

var profile = await client.ResolveProfileAsync();
Console.WriteLine($"recommended frame={profile.FrameType}, series={profile.CompatibilityMode}");
```

```csharp
using var rawClient = new SlmpClient("192.168.250.101", 1025, SlmpTransportMode.Tcp)
{
    TargetAddress = SlmpTargetParser.ParseNamed("SELF").Target,
};
await using var queuedClient = new QueuedSlmpClient(rawClient);

var profile = await queuedClient.ResolveProfileAsync();
queuedClient.FrameType = profile.FrameType;
queuedClient.CompatibilityMode = profile.CompatibilityMode;
await queuedClient.OpenAsync();

var readTasks = Enumerable.Range(0, 4)
    .Select(_ => queuedClient.ReadWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 1000), 1));
var reads = await Task.WhenAll(readTasks);
```

