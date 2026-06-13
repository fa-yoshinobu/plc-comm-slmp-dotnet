[![CI](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.Slmp.svg)](https://www.nuget.org/packages/PlcComm.Slmp/)
[![Documentation](https://img.shields.io/badge/docs-GitHub_Pages-blue.svg)](https://fa-yoshinobu.github.io/plc-comm-slmp-dotnet/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/blob/main/LICENSE)
[![Release](https://img.shields.io/github/v/release/fa-yoshinobu/plc-comm-slmp-dotnet?label=release)](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/releases/latest)
[![.NET 9](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)

# SLMP Protocol for .NET

.NET library for Mitsubishi SLMP (Binary 3E/4E) PLC communication.

## Supported PLC profiles

| Profile string | Hardware | Frame | Notes |
| --- | --- | --- | --- |
| `melsec:iq-f` | MELSEC iQ-F / FX5 | 3E | `SlmpPlcProfile.IqF`; legacy mode; `X` and `Y` use iQ-F octal address notation. |
| `melsec:iq-r` | MELSEC iQ-R | 4E | `SlmpPlcProfile.IqR`; iQ-R mode. |
| `melsec:iq-l` | MELSEC iQ-L | 4E | `SlmpPlcProfile.IqL`; iQ-R mode with iQ-L range rules. |
| `melsec:mx-f` | MELSEC MX-F | 4E | `SlmpPlcProfile.MxF`; iQ-R mode. |
| `melsec:mx-r` | MELSEC MX-R | 4E | `SlmpPlcProfile.MxR`; iQ-R mode. |
| `melsec:qcpu` | MELSEC-Q CPU | 3E | `SlmpPlcProfile.QCpu`; legacy mode. |
| `melsec:lcpu` | MELSEC-L CPU | 3E | `SlmpPlcProfile.LCpu`; legacy mode. |
| `melsec:qnu` | MELSEC QnU CPU | 3E | `SlmpPlcProfile.QnU`; legacy mode. |
| `melsec:qnudv` | MELSEC QnUDV CPU | 3E | `SlmpPlcProfile.QnUDV`; legacy mode. |

## Supported device types

| Device | Use |
| --- | --- |
| `D` | Data registers for the first word, dword, and float reads. |
| `M` | Internal relay bits. |
| `X` | Input bits; profile-aware notation is required for iQ-F. |
| `Y` | Output bits; profile-aware notation is required for iQ-F. |
| `W` | Link registers with hexadecimal numbering. |
| `R` | File registers. |
| `LTN` | Long timer current values; use 32-bit `:D` or `:L` access. |
| `LCN` | Long counter current values; use 32-bit `:D` or `:L` access. |

See the full table in [Supported registers](docsrc/user/SUPPORTED_REGISTERS.md).

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

| Page | Link |
| --- | --- |
| Getting started | [docsrc/user/GETTING_STARTED.md](docsrc/user/GETTING_STARTED.md) |
| Usage guide | [docsrc/user/USAGE_GUIDE.md](docsrc/user/USAGE_GUIDE.md) |
| Supported registers | [docsrc/user/SUPPORTED_REGISTERS.md](docsrc/user/SUPPORTED_REGISTERS.md) |
| PLC profiles | [docsrc/user/PROFILES.md](docsrc/user/PROFILES.md) |
| Examples | [samples/README.md](samples/README.md) |

## Hardware verified

The retained public verification summary lists `iQ-R` and `iQ-L` as fully verified for the current helper surface. `MELSEC-Q`, `MELSEC-L`, `iQ-F`, and third-party MC-compatible endpoints are profile-limited or mixed in the retained summary. The recommended first public test is `D100`, `D200:F`, and `D50.3`.

## License and registry

This package is distributed under the [MIT License](LICENSE). The NuGet package is published as [`PlcComm.Slmp`](https://www.nuget.org/packages/PlcComm.Slmp/).
