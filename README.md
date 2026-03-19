# (Mitsubishi MELSEC) SLMP .NET

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A modern .NET SLMP client for Mitsubishi PLC communication over Binary 3E/4E frames.

## Current Scope

This repository now contains a working bootstrap implementation with:

- Core library: `src/PlcComm.Slmp`
- CLI sample: `samples/PlcComm.Slmp.Cli`
- Unit tests: `tests/PlcComm.Slmp.Tests`

Implemented core features:

- TCP / UDP transport
- Binary 3E / 4E request-response framing
- Compatibility mode (`legacy` / `iqr`)
- Device parser (`D100`, `X10`, etc.)
- Read/Write words and bits
- Read type name
- Remote controls (`run/stop/pause/latch clear/reset`) and `clear error`
- Auto profile resolution (`3E/4E` + `legacy/iqr`) with simple probing
- Target parser (`SELF`, `SELF-CPU1..4`, `NWx-STy`, `NAME,NETWORK,STATION,MODULE_IO,MULTIDROP`)
- Compatibility probe CLI report output (`markdown` + `json`)
- Compatibility matrix renderer (`compatibility-matrix-render`)
- G/HG Appendix 1 coverage CLI (read/write-check)
- Appendix1 device recheck + read-soak + mixed-read-load + tcp-concurrency probes

## Quick Start

```bash
dotnet build PlcCommSlmp.sln
dotnet run --project samples/PlcComm.Slmp.Cli -- connection-check --host 192.168.250.101 --port 1025 --transport tcp --series auto --frame-type auto
```

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- other-station-check --host 192.168.250.101 --port 1025 --transport tcp --target NW1-ST2
```

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- compatibility-probe --host 192.168.250.101 --port 1025 --transport tcp --target SELF --write-check
dotnet run --project samples/PlcComm.Slmp.Cli -- g-hg-appendix1-coverage --host 192.168.250.101 --port 1025 --transport tcp --target SELF-CPU1 --device U3E0\\G10 --points 1 --write-check
dotnet run --project samples/PlcComm.Slmp.Cli -- compatibility-matrix-render --input docs/validation/reports/compatibility_probe_latest.json
```

`docs/validation/reports/PLC_COMPATIBILITY.md` remains Python-source-of-truth and should be regenerated from `plc-comm-slmp-python` probe JSON.

## Library Auto-Recommend Example

```csharp
using var client = new SlmpClient("192.168.250.101", 1025, SlmpTransportMode.Tcp)
{
    TargetAddress = SlmpTargetParser.ParseNamed("SELF").Target,
};

await client.OpenAsync();
var profile = await client.ResolveProfileAsync();
Console.WriteLine($"frame={profile.FrameType}, series={profile.CompatibilityMode}, class={profile.ProfileClass}");
```

## Documentation

- [User Guide](docs/user/USER_GUIDE.md)
- [Protocol Notes](docs/maintainer/PROTOCOL_SPEC.md)
- [Validation Report](docs/validation/reports/INITIAL_BOOTSTRAP_2026-03-19.md)

## Parity Status

Target is parity with `plc-comm-slmp-python`.
Current implementation is a functional core subset. Remaining parity work is tracked in [TODO.md](TODO.md).

## License

Distributed under the [MIT License](LICENSE).
