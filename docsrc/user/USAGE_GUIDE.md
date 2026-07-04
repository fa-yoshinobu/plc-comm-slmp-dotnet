# Usage guide

## Recommended entry points

| API | Use |
| --- | --- |
| `SlmpConnectionOptions` | Holds host, profile, port, transport, timeout, target, and monitoring timer settings. |
| `SlmpClientFactory.OpenAndConnectAsync` | Opens a connected `QueuedSlmpClient` from `SlmpConnectionOptions`. |
| `ReadTypedAsync` | Reads one typed scalar such as `D100` as `BIT`, `U`, `S`, `D`, `L`, or `F`. |
| `WriteTypedAsync` | Writes one typed scalar. |
| `ReadNamedAsync` | Reads a mixed snapshot of named addresses. |
| `WriteNamedAsync` | Writes a named set of values. |
| `ReadWordsSingleRequestAsync` / `ReadDWordsSingleRequestAsync` | Reads one contiguous block in one protocol request. |
| `ReadWordsChunkedAsync` / `ReadDWordsChunkedAsync` | Reads an explicitly chunked contiguous block. |
| `WriteBitInWordAsync` | Sets or clears one bit in a word device. |
| `PollAsync` | Repeats a named snapshot on an async interval. |
| `SlmpAddress` | Parses, formats, and normalizes SLMP address text. |

## Connection

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
    Timeout = TimeSpan.FromSeconds(3),
    Transport = SlmpTransportMode.Tcp,
    Target = new SlmpTargetAddress(Station: 0xFF, ModuleIo: 0x03FF),
    MonitoringTimer = 0x0010,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
Console.WriteLine($"{client.FrameType} {client.CompatibilityMode}");
```

## Remote password

Remote password lock/unlock commands are available on the underlying `SlmpClient`.
The .NET high-level connection does not automatically unlock or lock a remote password.
If your PLC route uses remote password protection, unlock after opening the connection
and lock before closing it.

```csharp
await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
await client.ExecuteAsync(inner => inner.RemotePasswordUnlockAsync("secret"));
try
{
    var value = await client.ReadTypedAsync("D100", "U");
}
finally
{
    await client.ExecuteAsync(inner => inner.RemotePasswordLockAsync("secret"));
}
```

For `C200`-series password end codes, see the shared
[SLMP Troubleshooting & End Codes](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/slmp/troubleshooting-end-codes/)
page.

## Routing / target station

Most applications keep the default target, which means the directly connected
own station/control CPU. Change the target only when your PLC network is
configured for another station, multi-CPU module I/O, or multidrop access.

`SlmpTargetAddress` controls the SLMP destination header. It is not a device
family selector; routed devices such as `Un\Gn` and `Jn\...` still need their
own address syntax.

```csharp
var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
    Target = new SlmpTargetAddress(
        Network: 0x01,
        Station: 0x02,
        ModuleIo: 0x03FF,
        Multidrop: 0x00),
};
```

Use the default target unless the PLC routing setup gives you specific values.

## SLMP response end codes

When the PLC returns a non-zero SLMP end code, the high-level APIs throw `SlmpError`.
Read `EndCode` for the PLC response code and `ErrorInfo` when the PLC returned the structured error-information block.

```csharp
try
{
    var value = await client.ReadTypedAsync("D100", "U");
    Console.WriteLine($"D100={value}");
}
catch (SlmpError ex) when (ex.EndCode is ushort endCode)
{
    Console.WriteLine($"SLMP end_code=0x{endCode:X4}");

    if (ex.ErrorInfo is not null)
    {
        Console.WriteLine($"command=0x{ex.ErrorInfo.Command:X4}");
        Console.WriteLine($"subcommand=0x{ex.ErrorInfo.Subcommand:X4}");
    }
}
```

## Read a single value

| Type suffix | .NET value | PLC size |
| --- | --- | --- |
| `U` | `ushort` | 1 word |
| `S` | `short` | 1 word |
| `D` | `uint` | 2 words |
| `L` | `int` | 2 words |
| `F` | `float` | 2 words |

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

## Write a single value

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
    Console.WriteLine("Wrote D100.");
}
finally
{
    await client.WriteTypedAsync("D100", "U", original);
}
```

## Named snapshot

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var snapshot = await client.ReadNamedAsync(["D100:U", "D200:F", "D300:L", "D50.3"]);

foreach (var (address, value) in snapshot)
{
    Console.WriteLine($"{address} = {value}");
}
```

## Block reads

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);

ushort[] words = await client.ReadWordsSingleRequestAsync("D0", 10);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("D200", 4);
ushort[] chunkedWords = await client.ReadWordsChunkedAsync("D1000", 1000, maxWordsPerRequest: 480);
uint[] chunkedDwords = await client.ReadDWordsChunkedAsync("D2000", 200, maxDwordsPerRequest: 240);

Console.WriteLine($"words={words.Length}, dwords={dwords.Length}");
Console.WriteLine($"chunked words={chunkedWords.Length}, chunked dwords={chunkedDwords.Length}");
```

## Bit in word

Use `WriteBitInWordAsync` when a PLC stores flags inside a word. Use `.n` notation such as `D50.3` when reading the same bit in a named snapshot.

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var original = await client.ReadNamedAsync(["D50.3"]);
try
{
    await client.WriteBitInWordAsync("D50", bitIndex: 3, value: true);
    var snapshot = await client.ReadNamedAsync(["D50.3"]);
    Console.WriteLine($"D50.3 = {snapshot["D50.3"]}");
}
finally
{
    await client.WriteBitInWordAsync("D50", bitIndex: 3, value: (bool)original["D50.3"]);
}
```

## Polling

```csharp
using System;
using System.Threading;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

await foreach (var snapshot in client.PollAsync(["D100:U", "D200:F", "D50.3"], TimeSpan.FromSeconds(1), cts.Token))
{
    Console.WriteLine($"D100:U = {snapshot["D100:U"]}");
}
```

## Device range catalog

`ReadDeviceRangeCatalogAsync` reads live device range bounds from your PLC after you connect with an explicit PLC profile. It does not auto-discover the profile.
The source rules for this catalog are maintained in the shared [SLMP device ranges](https://fa-yoshinobu.github.io/plc-comm-docs-site/slmp/profile-reference/device-ranges/) reference.

```csharp
using System;
using System.Linq;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var catalog = await client.ReadDeviceRangeCatalogAsync();
var row = catalog.Entries.First(entry => entry.Device == "D");

Console.WriteLine($"{row.Device}: supported={row.Supported}, range={row.AddressRange}");
```

## Long device families

`LTN`, `LSTN`, `LCN`, and `LZ` are 32-bit families. Always use `:D` or `:L` in named addresses, or pass `D` or `L` as the `ReadTypedAsync` / `WriteTypedAsync` dtype.

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var current = await client.ReadTypedAsync("LTN0", "D");
var snapshot = await client.ReadNamedAsync(["LTN0:D", "LSTN0:L", "LCN0:D", "LZ0:L"]);

Console.WriteLine($"LTN0 = {current}");
Console.WriteLine($"LCN0:D = {snapshot["LCN0:D"]}");
```

> **Caution:** Plain word access to `LTN`, `LSTN`, `LCN`, and `LZ` is rejected by the library.

## Address reference

| Form | Example | Meaning |
| --- | --- | --- |
| `:U` | `D100:U` | Unsigned 16-bit word. |
| `:S` | `D100:S` | Signed 16-bit word. |
| `:D` | `D200:D` | Unsigned 32-bit value. |
| `:L` | `D200:L` | Signed 32-bit value. |
| `:F` | `D200:F` | Float32 value. |
| `:BIT` | `M1000:BIT` | Boolean bit device value in named addresses. |
| `.n` | `D50.3` | Bit `n` inside one word, where `n` is hexadecimal `0` to `F`. |

Named addresses used with `ReadNamedAsync`, `WriteNamedAsync`, and `PollAsync` must include the intended type, for example `D100:U` or `M1000:BIT`.
