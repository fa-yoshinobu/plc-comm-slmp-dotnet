[![CI](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.Slmp.svg)](https://www.nuget.org/packages/PlcComm.Slmp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

# MELSEC SLMP for .NET

.NET library for MELSEC SLMP (Binary 3E/4E) PLC communication.

## Supported PLC profiles

The maintained profile table is in [PLC profiles](docsrc/user/PROFILES.md). Choose one exact canonical PLC profile from that table.

## Supported device types

The maintained device and range tables are in [Supported registers](docsrc/user/SUPPORTED_REGISTERS.md). Use that page for supported device families, address syntax, and profile-specific notes.

## Installation

```powershell
dotnet add package PlcComm.Slmp
```

## Quick example

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR) { Port = 1025 };
await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("D100", "U");
Console.WriteLine($"D100 = {value}");
```

## Documentation

| Page | Use it for |
| --- | --- |
| [Full documentation site](https://fa-yoshinobu.github.io/plc-comm-docs-site/) | Unified docs for all PLC communication libraries. |
| [Getting started](docsrc/user/GETTING_STARTED.md) | Install the package, connect to your PLC, and run your first SLMP read/write. |
| [Usage guide](docsrc/user/USAGE_GUIDE.md) | Use the high-level API and common SLMP workflows. |
| [Supported registers](docsrc/user/SUPPORTED_REGISTERS.md) | Check supported device families, address syntax, and numbering rules. |
| [PLC profiles](docsrc/user/PROFILES.md) | Choose the canonical MELSEC profile and frame behavior. |
| [Examples](samples/README.md) | Run maintained .NET samples. |

## Hardware verified

Live-device verification is maintained in [Latest communication verification](docsrc/user/LATEST_COMMUNICATION_VERIFICATION.md).
See that page for verified PLC models, transports, dates, limitations, and retained validation notes.

## License and registry

| Item | Value |
| --- | --- |
| License | [MIT](LICENSE) |
| Registry | [NuGet](https://www.nuget.org/packages/PlcComm.Slmp/) |
| Package | `PlcComm.Slmp` |
