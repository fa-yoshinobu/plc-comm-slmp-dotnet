# (Mitsubishi MELSEC) SLMP .NET

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A modern .NET SLMP client for Mitsubishi PLC communication over Binary 3E/4E frames.

## Why This Library

- Fast binary 3E/4E transport with auto profile resolution
- End-user friendly API + CLI for quick validation
- Single-connection queue (`QueuedSlmpClient`) for unstable TCP environments
- Clear device-code support list (see below)

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

## Device Support (PLC Device Codes)

This list reflects device codes accepted by the parser and typed APIs. Actual availability depends on PLC model, firmware, and access settings.

| Group | Codes | Status | Notes |
| --- | --- | --- | --- |
| Bit devices (direct) | SM, X, Y, M, L, F, V, B, TS, TC, STS, STC, CS, CC, SB, DX, DY | Supported | `X/Y/B/SB/DX/DY` use hexadecimal numbering. |
| Word devices (direct) | SD, D, W, SW, TN, STN, CN, Z, LZ, R, ZR, RD | Supported | `W/SW` use hexadecimal numbering. |
| Long timer / counter families | LTS, LTC, LTN, LSTS, LSTC, LSTN, LCS, LCC, LCN | Supported (direct) | Some PLCs reject direct access; validate on the target. |
| Appendix 1 qualified devices | `Uxx\\Gyy`, `Uxx\\HGyy` | Supported via Appendix 1 APIs | Direct `G/HG` access is not supported. |
| Not supported | S, `Jn\\Xn` | Not supported | `S` is intentionally disabled; linked direct devices are out of scope. |

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

- [User Guide](docsrc/user/USER_GUIDE.md)
- [Protocol Notes](docsrc/maintainer/PROTOCOL_SPEC.md)
- [Validation Report](docsrc/validation/reports/INITIAL_BOOTSTRAP_2026-03-19.md)

## Parity Status

Target is parity with `plc-comm-slmp-python`.
Current implementation is a functional core subset. Remaining parity work is tracked in [TODO.md](TODO.md).

## License

Distributed under the [MIT License](LICENSE).

