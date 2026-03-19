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
- `NWx-STy` (for example `NW2-ST1`)
- `name,0xNN,0xSS,0xIIII,0xMM`

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
```
