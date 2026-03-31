[![CI](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.Slmp.svg)](https://www.nuget.org/packages/PlcComm.Slmp/)
[![Documentation](https://img.shields.io/badge/docs-GitHub_Pages-blue.svg)](https://fa-yoshinobu.github.io/plc-comm-slmp-dotnet/)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

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
- `PollAsync`
- `SlmpAddress.Normalize`

Low-level request builders and raw protocol methods remain available for maintainers, but they are not the primary user path.

## Quick Start

### Installation

- Package page: https://www.nuget.org/packages/PlcComm.Slmp/

```powershell
dotnet add package PlcComm.Slmp
```

Or add a package reference directly:

```xml
<PackageReference Include="PlcComm.Slmp" Version="0.1.3" />
```

You can also reference `src/PlcComm.Slmp/PlcComm.Slmp.csproj` directly during local development.

```bash
dotnet build PlcComm.Slmp.sln
```

Recommended high-level usage:

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100")
{
    Port = 1025,
    FrameType = SlmpFrameType.Frame4E,
    CompatibilityMode = SlmpCompatibilityMode.Iqr,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);

var snapshot = await client.ReadNamedAsync(["D100", "D200:F", "D50.3"]);
Console.WriteLine(snapshot["D100"]);
Console.WriteLine(snapshot["D200:F"]);
Console.WriteLine(snapshot["D50.3"]);
```

## High-Level API Guide

### Typed values

```csharp
float temperature = (float)await client.ReadTypedAsync("D200", "F");
int position = (int)await client.ReadTypedAsync("D300", "L");

await client.WriteTypedAsync("D100", "U", (ushort)42);
await client.WriteTypedAsync("D200", "F", 3.14f);
await client.WriteTypedAsync("D300", "L", -100);
```

### Mixed reads

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
Address bit devices directly as `M1000`, `M1001`, ... rather than `M1000.0`.

For long-device families in the high-level helper layer:

- plain `LTN`, `LSTN`, and `LCN` addresses default to 32-bit current-value access
- `LTS`, `LTC`, `LSTS`, and `LSTC` are resolved through the corresponding `ReadLongTimerAsync` / `ReadLongRetentiveTimerAsync` helper-backed 4-word decode instead of direct state reads

### Single-request contiguous reads

```csharp
ushort[] words = await client.ReadWordsSingleRequestAsync("D0", 32);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("D200", 8);
```

### Explicit chunked reads

```csharp
ushort[] longWords = await client.ReadWordsChunkedAsync("D0", 1000, maxWordsPerRequest: 480);
uint[] longDwords = await client.ReadDWordsChunkedAsync("D200", 120, maxDwordsPerRequest: 240);
```

### One-bit update inside a word

```csharp
await client.WriteBitInWordAsync("D50", bitIndex: 3, value: true);
await client.WriteBitInWordAsync("D50", bitIndex: 3, value: false);
```

### Polling

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

await foreach (var item in client.PollAsync(
    ["D100", "D200:F", "D50.3"],
    TimeSpan.FromSeconds(1),
    cts.Token))
{
    Console.WriteLine(item["D100"]);
}
```

### Address helper

```csharp
string canonical = SlmpAddress.Normalize("d100");
Console.WriteLine(canonical); // D100
```

Use `*SingleRequestAsync` when one PLC request is required. Use
`*ChunkedAsync` only when splitting across multiple protocol requests is
acceptable for that data.

## Sample Programs

The main user-facing sample projects are:

- [`samples/PlcComm.Slmp.HighLevelSample`](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/tree/main/samples/PlcComm.Slmp.HighLevelSample)
  - explicit high-level connect
  - typed reads and writes
  - chunked words and dwords
  - bit-in-word writes
  - mixed named reads
  - polling
- [`samples/PlcComm.Slmp.QueuedSample`](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/tree/main/samples/PlcComm.Slmp.QueuedSample)
  - one shared queued connection
  - concurrent workers using only high-level APIs
  - repeated typed and named reads

Run them from the repository root:

```powershell
dotnet run --project samples/PlcComm.Slmp.HighLevelSample -- 192.168.250.100 1025 iqr 4e
dotnet run --project samples/PlcComm.Slmp.QueuedSample -- 192.168.250.100 1025 4 10
```

## Documentation

User-facing documents:

- [User Guide](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/docsrc/user/USER_GUIDE.md)
- [Samples](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/samples/README.md)
- [High-Level API Contract](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/HIGH_LEVEL_API_CONTRACT.md)

Maintainer and validation material remains under `docsrc/maintainer/` and `docsrc/validation/`.

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

Distributed under the [MIT License](LICENSE).
