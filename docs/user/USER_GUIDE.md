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

- `docs/validation/reports/compatibility_probe_latest.md`
- `docs/validation/reports/compatibility_probe_latest.json`

### g-hg-appendix1-coverage

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- g-hg-appendix1-coverage \
  --host 192.168.250.101 --port 1025 --transport tcp \
  --target SELF-CPU1 --device U3E0\\G10 --device U3E0\\HG20 \
  --points 1 --points 4 --direct-memory 0xFA --write-check
```

Output:

- `docs/validation/reports/g_hg_appendix1_coverage_latest.md`

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
