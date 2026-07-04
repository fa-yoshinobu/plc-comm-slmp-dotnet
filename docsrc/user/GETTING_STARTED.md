# Getting started

## Start here

Use this page when you want your first successful SLMP read from a MELSEC PLC. The recommended path is one explicit `SlmpPlcProfile`, one connected `QueuedSlmpClient`, and one safe `D` register.

## Prerequisites

| Requirement | Value |
| --- | --- |
| .NET SDK | .NET 8, 9, or 10 SDK for consuming the package; .NET 10 SDK recommended when building this repository |
| Package | `PlcComm.Slmp` |
| PLC endpoint | `192.168.250.100:1025` over TCP |
| First profile to try | `SlmpPlcProfile.IqR` when your PLC is iQ-R hardware |
| First read target | `D100` |

## Install

```powershell
dotnet add package PlcComm.Slmp
```

## Choose your PLC profile

`SlmpPlcProfile` is the .NET selector for the canonical profile. It controls frame type, compatibility mode, string address rules, and device range rules. Pick the selector that matches your PLC hardware.

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};
```

## First read

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("D100", "U");
Console.WriteLine($"D100 = {value}");
```

Expected output:

```text
D100 = 0
```

The number depends on your PLC data. A successful run prints a numeric value and does not throw an SLMP end-code exception.

## First write

Use only a test register that your PLC program allows you to change.

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var original = await client.ReadTypedAsync("D100", "U");
try
{
    await client.WriteTypedAsync("D100", "U", (ushort)123);
    var value = await client.ReadTypedAsync("D100", "U");
    Console.WriteLine($"D100 = {value}");
}
finally
{
    await client.WriteTypedAsync("D100", "U", original);
}
```

## Confirm success

1. Confirm the PLC is reachable at `192.168.250.100`.
2. Confirm TCP port `1025` is enabled for SLMP.
3. Confirm the PLC-side communication data code is Binary and the port/open setting matches your transport; see the [MELSEC SLMP PLC Setup Guide](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/slmp/).
4. Confirm PLC-side RUN-time write permission before running a write example where the PLC exposes that setting.
5. Confirm `SlmpPlcProfile.IqR` matches your actual PLC hardware, or change it to the correct profile.
6. Confirm `D100` is a safe test register in your PLC program and restore the original value after a write.
7. Confirm the read prints a value before you run a write.

## If it does not work

| Symptom | Check |
| --- | --- |
| Connection opens but every read returns an end code | `SlmpPlcProfile` must match the actual PLC hardware. The profile selects frame type and access mode. |
| Connection opens but all requests fail | Confirm Binary communication data code in the PLC setup guide. |
| Reads work but writes fail | Confirm RUN-time write permission in the PLC setup guide and the selected profile write policy. |
| First register read fails | Start with `D` word reads. Do not start with `G`, `HG`, `LTN`, or `LCN`. |
| Several callers share one connection | Use `QueuedSlmpClient`, returned by `SlmpClientFactory.OpenAndConnectAsync`. Raw `SlmpClient` is not thread-safe for concurrent callers. |
| Long timer or long counter values look wrong | See [Gotchas](GOTCHAS.md) before reading `LTN`, `LSTN`, `LCN`, or `LZ`. |

## Next steps

- Open the runnable samples: [samples README](https://github.com/fa-yoshinobu/plc-comm-slmp-dotnet/tree/main/samples).
- Continue with the [Usage guide](USAGE_GUIDE.md) and [Gotchas](GOTCHAS.md).
