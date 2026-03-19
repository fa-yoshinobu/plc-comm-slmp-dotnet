# (Mitsubishi MELSEC) SLMP .NET

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A modern .NET SLMP client for Mitsubishi PLC communication over Binary 3E/4E frames.

## Why This Library

- Fast binary 3E/4E transport with auto profile resolution
- End-user friendly API + CLI for quick validation
- Single-connection queue (`QueuedSlmpClient`) for unstable TCP environments
- Hardware-verified compatibility snapshot (see table below)

## Quick Start (Copy/Paste)

```bash
dotnet build PlcCommSlmp.sln
dotnet run --project samples/PlcComm.Slmp.Cli -- connection-check --host 192.168.250.101 --series auto --frame-type auto
```

Optional UDP check:

```bash
dotnet run --project samples/PlcComm.Slmp.Cli -- connection-check --host 192.168.250.101 --port 1027 --transport udp --series auto --frame-type auto
```

## Core Features

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
- `QueuedSlmpClient` for single-connection serialized app-side reuse

## Device Compatibility Snapshot (Hardware-Verified)

This table is a readable snapshot. The full matrix lives in `docs/validation/reports/PLC_COMPATIBILITY.md` (Python is the source of truth).

| Family | Verified Models | Status | Recommended Profile |
| --- | --- | --- | --- |
| iQ-R | R00CPU, R08CPU, R08PCPU, R120PCPU, RJ71EN71 | YES (core commands) | 3e/ql, 3e/iqr, 4e/ql, 4e/iqr |
| iQ-L | L16HCPU | YES (core commands) | 3e/ql, 3e/iqr, 4e/ql, 4e/iqr |
| MELSEC-Q | Q06UDVCPU, Q26UDEHCPU, QJ71E71-100 | PARTIAL | 3e/ql (4e/ql for QJ71E71) |
| MELSEC-L | L26CPU-BT | PARTIAL | 3e/ql |
| iQ-F | FX5U, FX5UC | PARTIAL | 3e/ql (4e/ql for FX5U) |
| Third-Party MC | KV-7500, KV-XLE02 | PARTIAL (MC compatible) | 3e/ql, 4e/ql |

Notes:

- Q/L series often reject `0101` (type name) and may require 3E/QL.
- Third-party MC-compatible endpoints are not Mitsubishi native; results describe MC compatibility.

## Use Cases

- Quick on-site health check of PLCs (SM/D reads, type name when supported).
- Gateway services that normalize SLMP data for MES/SCADA or data lakes.
- Multi-station diagnostics with a single shared connection (`QueuedSlmpClient`) for unstable TCP environments.

TCP concurrency practical note (current environment): direct multi-connection TCP is unstable beyond one connection on TCP/1025. Prefer single-connection sharing. Auto profile resolution is cached per process, so `--series auto --frame-type auto` no longer adds repeated probe overhead within one run.

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

## Library Single-Connection Queue Example

```csharp
using var client = new SlmpClient("192.168.250.101", 1025, SlmpTransportMode.Tcp)
{
    TargetAddress = SlmpTargetParser.ParseNamed("SELF").Target,
};
await using var queued = new QueuedSlmpClient(client);

var profile = await queued.ResolveProfileAsync();
queued.FrameType = profile.FrameType;
queued.CompatibilityMode = profile.CompatibilityMode;
await queued.OpenAsync();

var workers = Enumerable.Range(0, 4).Select(async _ =>
{
    var sm400 = await queued.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1);
    var d1000 = await queued.ReadWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 1000), 1);
});
await Task.WhenAll(workers);
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
