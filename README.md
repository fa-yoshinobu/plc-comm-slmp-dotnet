[![CI](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.Slmp.svg)](https://www.nuget.org/packages/PlcComm.Slmp/)
[![Documentation](https://img.shields.io/badge/docs-GitHub_Pages-blue.svg)](https://fa-yoshinobu.github.io/plc-comm-slmp-dotnet/)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/LICENSE)

# SLMP Protocol for .NET

![Illustration](https://raw.githubusercontent.com/fa-yoshinobu/plc-comm-slmp-dotnet/main/docsrc/assets/melsec.png)

High-level SLMP helpers for Mitsubishi PLC communication over Binary 3E and 4E frames.

The recommended user surface is the extension-method layer:

- `SlmpClientFactory.OpenAndConnectAsync`
- `SlmpConnectionOptions`
- `ReadTypedAsync` / `WriteTypedAsync`
- `ReadWordsSingleRequestAsync` / `ReadDWordsSingleRequestAsync`
- `ReadWordsChunkedAsync` / `ReadDWordsChunkedAsync`
- `WriteBitInWordAsync`
- `ReadNamedAsync`
- `WriteNamedAsync`
- `PollAsync`
- `SlmpAddress.Normalize`
- `ReadDeviceRangeCatalogAsync`

## Quick Start

### Installation

- Package page: <https://www.nuget.org/packages/PlcComm.Slmp/>

```powershell
dotnet add package PlcComm.Slmp
```

Or add a package reference directly:

```xml
<PackageReference Include="PlcComm.Slmp" Version="0.1.9" />
```

### Recommended High-Level Usage

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcFamily.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);

var snapshot = await client.ReadNamedAsync(["D100", "D200:F", "D50.3"]);
Console.WriteLine(snapshot["D100"]);
Console.WriteLine(snapshot["D200:F"]);
Console.WriteLine(snapshot["D50.3"]);
```

## Supported PLC Registers

Start with these public high-level families first:

- word devices: `D`, `SD`, `R`, `ZR`, `TN`, `CN`
- bit devices: `M`, `X`, `Y`, `SM`, `B`
- typed forms: `D200:F`, `D300:L`, `D100:S`
- mixed snapshot forms: `D50.3`, `D100`, `D200:F`
- current-value long families: `LTN`, `LSTN`, `LCN`

See the full public table in [Supported PLC Registers](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/docsrc/user/SUPPORTED_REGISTERS.md).

For live PLC-dependent device limits resolved from a user-selected PLC family
plus family `SD` registers, see [Device Range Catalog](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/docsrc/user/DEVICE_RANGES.md).

### Long Current Values

`LTN`, `LSTN`, and `LCN` are not normal 16-bit word devices in the high-level
API. They represent 32-bit current values. Use the typed or named API with `:D`
or `:L`:

```csharp
var timer = await client.ReadTypedAsync("LTN100", "L");
await client.WriteTypedAsync("LTN100", "L", 1234);

var snapshot = await client.ReadNamedAsync(["LTN100:L", "LSTN10:D", "LCN0:L"]);
await client.WriteNamedAsync(new Dictionary<string, object>
{
    ["LCN0:L"] = 10,
});
```

The low-level word block APIs intentionally reject `LTN` / `LSTN` / `LCN` as
word writes. Direct DWord writes are also rejected for these families so the
long-current write route is selected explicitly through `WriteTypedAsync` or
`WriteNamedAsync`.

Contact and coil devices in the long families, such as `LTS`, `LTC`, `LSTS`,
`LSTC`, `LCS`, and `LCC`, are bit-style addresses. Do not treat them as
current-value words.

### Device Range Catalog

Use `ReadDeviceRangeCatalogAsync` after opening a connection with a selected
`SlmpPlcFamily`. The catalog reports whether each device is supported, its
point count, lower bound, upper bound, display range, notation, and source.

```csharp
var catalog = await client.ReadDeviceRangeCatalogAsync();
var stn = catalog.Entries.First(entry => entry.Device == "STN");

if (!stn.Supported || stn.PointCount == 0)
{
    Console.WriteLine("STN is unavailable in the current PLC settings.");
}
```

Applications should use this catalog to clamp monitor scroll ranges and reject
out-of-range reads or writes before issuing SLMP commands.

## Public Documentation

- [Getting Started](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/docsrc/user/GETTING_STARTED.md)
- [Supported PLC Registers](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/docsrc/user/SUPPORTED_REGISTERS.md)
- [Device Range Catalog](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/docsrc/user/DEVICE_RANGES.md)
- [Latest Communication Verification](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/docsrc/user/LATEST_COMMUNICATION_VERIFICATION.md)
- [User Guide](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/docsrc/user/USER_GUIDE.md)
- [Samples](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/samples/README.md)
- [High-Level API Contract](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/HIGH_LEVEL_API_CONTRACT.md)

Maintainer-only notes and retained evidence live under `internal_docs/`.

## High-Level API Guide

### Typed Values

```csharp
float temperature = (float)await client.ReadTypedAsync("D200", "F");
int position = (int)await client.ReadTypedAsync("D300", "L");

await client.WriteTypedAsync("D100", "U", (ushort)42);
await client.WriteTypedAsync("D200", "F", 3.14f);
await client.WriteTypedAsync("D300", "L", -100);
await client.WriteTypedAsync("LTN100", "L", 1000);
```

### Mixed Reads

```csharp
var snapshot = await client.ReadNamedAsync(
[
    "D100",
    "D200:F",
    "D300:L",
    "D50.3",
]);
```

Use `.bit` notation only with word devices such as `D50.3`.
Address bit devices directly as `M1000`, `M1001`, `X20`, or `Y20`.

Use `:D` or `:L` with `LTN`, `LSTN`, and `LCN`.

## Development

```powershell
run_ci.bat
build_docs.bat
release_check.bat
```

Pack the NuGet package locally:

```powershell
dotnet pack src\PlcComm.Slmp\PlcComm.Slmp.csproj -c Release
```

## License

Distributed under the [MIT License](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/LICENSE).
