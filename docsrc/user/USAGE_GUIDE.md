# Usage guide

## Recommended entry points

| API | Use |
| --- | --- |
| `SlmpConnectionOptions` | Holds host, profile, port, transport, timeout, target, and monitoring timer settings. |
| `SlmpClientFactory.OpenAndConnectAsync` | Opens a connected `QueuedSlmpClient` from `SlmpConnectionOptions`. |
| `ReadTypedAsync` | Reads one typed scalar such as `D100` as `BIT`, `U`, `S`, `D`, `L`, or `F`. |
| `WriteTypedAsync` | Writes one typed scalar. |
| `ReadNamedAsync` | Reads a mixed named value set; different command families are not atomic. |
| `WriteNamedAsync` | Writes a named set of values. |
| `ReadWordsSingleRequestAsync` / `ReadDWordsSingleRequestAsync` | Reads one contiguous block in one protocol request. |
| `WriteBitInWordAsync` | Sets or clears one bit in a word device. |
| `PollAsync` | Repeats a named value-set read on an async interval. |
| `SlmpAddress` | Parses, formats, and normalizes SLMP address text. |
| `SlmpQualifiedDeviceParser` | Parses extended device text such as `U3\G100`, `U3E0\HG0`, and `J2\SW10`. |
| `ReadWordsExtendedAsync` / `WriteWordsExtendedAsync` | Reads or writes routed `U...` / `J...` word devices. |
| `ReadBitsExtendedAsync` / `WriteBitsExtendedAsync` | Reads or writes routed `U...` / `J...` bit devices. |

## Connection

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions(
    "192.168.250.100",
    SlmpPlcProfile.IqR,
    1025,
    SlmpTransportMode.Tcp,
    SlmpTargetAddress.OwnStation)
{
    Timeout = TimeSpan.FromSeconds(3),
    MonitoringTimer = 0x0010,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
Console.WriteLine($"{client.FrameType} {client.CompatibilityMode}");
```

## Remote password

Remote password lock/unlock commands are available on the underlying `SlmpClient`.
The .NET high-level connection does not automatically unlock or lock a remote password.
If your PLC route uses remote password protection, unlock after opening the connection
and lock before closing it. Passwords must contain printable ASCII characters only;
non-ASCII text is rejected rather than replaced during encoding.

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
[SLMP Troubleshooting & Codes](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/slmp/troubleshooting-codes/)
page.

## Routing / target station

Every connection explicitly selects a target. Use `SlmpTargetAddress.OwnStation`
for the directly connected station, or provide the complete configured route for
another station, multi-CPU module I/O, or multidrop access.

`SlmpTargetAddress` controls the SLMP destination header. It is not a device
family selector; routed devices such as `Un\Gn` and `Jn\...` still need their
own address syntax.

```csharp
var options = new SlmpConnectionOptions(
    "192.168.250.100",
    SlmpPlcProfile.IqR,
    1025,
    SlmpTransportMode.Tcp,
    new SlmpTargetAddress(
        Network: 0x01,
        Station: 0x02,
        ModuleIo: 0x03FF,
        Multidrop: 0x00));
```

Use `SlmpTargetAddress.OwnStation` only when the intended route is the directly connected station. The constructor always requires a complete target.

## Extended device access

`G`, `HG`, and `J` devices are not normal standalone addresses. Use the
extended device APIs with a qualified address:

| Address form | Meaning |
| --- | --- |
| `U3\G100` | Module access buffer memory `G100` on unit `U3`. |
| `U3E0\HG0` | CPU buffer memory `HG0` on `U3E0`, when the selected profile supports it. |
| `J2\SW10` | Link direct `SW10` on J network `2`. |
| `J1\X10` | Link direct `X10` on J network `1`. |

The selected PLC profile and the actual PLC configuration still decide whether
the route is accepted.

```csharp
await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);

var module = SlmpQualifiedDeviceParser.Parse(@"U3\G100", client.PlcProfile);
ushort[] moduleWords = await client.ReadWordsExtendedAsync(module, 4);
await client.WriteWordsExtendedAsync(module, new ushort[] { 1, 2, 3, 4 });

var cpuBuffer = SlmpQualifiedDeviceParser.Parse(@"U3E0\HG0", client.PlcProfile);
ushort[] cpuBufferWords = await client.ReadWordsExtendedAsync(cpuBuffer, 2);

var linkWord = SlmpQualifiedDeviceParser.Parse(@"J2\SW10", client.PlcProfile);
ushort[] linkWords = await client.ReadWordsExtendedAsync(linkWord, 1);

var linkBits = SlmpQualifiedDeviceParser.Parse(@"J1\X10", client.PlcProfile);
bool[] bits = await client.ReadBitsExtendedAsync(linkBits, 16);
```

For iQ-R multi-CPU `U3En\HG...` access, the qualified device never changes the
immutable SLMP request target. Create a client with the destination CPU target
when a write must be reflected there. A write can return a normal end code
without changing the intended CPU buffer when the selected request target
identifies a different CPU or Own Station. Cross-CPU reads remain valid. See the
shared [iQ-R target guidance](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/slmp/iq-r/#multi-cpu-cpu-buffer-target).

## Monitor, self-test, and Clear Error

Monitor registration and every cycle are separate one-request operations.
Supply the registered Word and DWord counts to each cycle; the client does not
auto-register, retry, or infer them. Calling a cycle before PLC registration
sends one cycle request and returns the PLC response or error. The combined
expected count must be nonzero and cannot exceed the selected profile's
monitor-registration limit.

```csharp
await client.RegisterMonitorDevicesAsync(
    [SlmpDeviceParser.Parse("D120", client.PlcProfile)],
    [SlmpDeviceParser.Parse("D200", client.PlcProfile)]);
SlmpMonitorResult cycle = await client.RunMonitorCycleAsync(1, 1);

byte[] echo = await client.SelfTestLoopbackAsync("A1B2C3D4"u8.ToArray());
await client.ClearErrorAsync();
```

These methods are also exposed directly by `QueuedSlmpClient`. Self-test
accepts only 1–960 ASCII `0-9/A-F` bytes and requires exact declared length,
actual length, and echo equality. Clear Error always uses the fixed empty
payload command.

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

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("D100", "U");
Console.WriteLine($"D100 = {value}");
```

## Write a single value

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

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

## Named values

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var snapshot = await client.ReadNamedAsync(["D100:U", "D200:F", "D300:L", "D50.3"]);

foreach (var (address, value) in snapshot)
{
    Console.WriteLine($"{address} = {value}");
}
```

`ReadNamedAsync` emits exactly one random-read request. Every entry must fit
that request; direct/block/long-timer fallback routes are rejected before
transport. `WriteNamedAsync` emits one random word/DWord request or one random
bit request and rejects mixed families and bit-in-word read-modify-write.

Typed writes do not parse strings or convert Boolean and floating-point values into
integers. `BIT` requires `bool`; U/S/D/L require integral CLR values in their exact
ranges; F requires a finite numeric value within the float32 range.

Communication timeout values must be at least 1 millisecond. After a request is sent
and then times out, is cancelled, or loses transport ownership, the client remains
invalidated until `OpenAsync` is called explicitly. `RemoteResetAsync` also closes and
invalidates its send-only transport; its completion confirms transmission, not PLC
execution. Reopen and verify PLC state before continuing.

## Block reads

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);

ushort[] words = await client.ReadWordsSingleRequestAsync("D0", 10);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("D200", 4);

Console.WriteLine($"words={words.Length}, dwords={dwords.Length}");
```

These helpers issue exactly one PLC request and reject counts above the
protocol limit. Applications that intentionally issue multiple requests must
make the boundaries and different acquisition times explicit.

## Bit in word

Use `WriteBitInWordAsync` when a PLC stores flags inside a word. Use `.n` notation such as `D50.3` when reading the same bit in a named snapshot.

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

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

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

await foreach (var snapshot in client.PollAsync(["D100:U", "D200:F", "D50.3"], TimeSpan.FromSeconds(1), cts.Token))
{
    Console.WriteLine($"D100:U = {snapshot["D100:U"]}");
}
```

## Operational recipes

The samples include two read-only operational recipes for applications that need
repeatable collection rather than one-off reads:

- `PlcComm.Slmp.MultiPlcMonitorSample` monitors multiple PLC endpoints at the
  same time. Each PLC has its own task, connection, and reconnect loop, so one
  offline PLC does not block the others.
- `PlcComm.Slmp.ConfigPollingSample` runs periodic collection from a JSON
  config file and can append long-form CSV rows as
  `timestamp,plc,tag,value`.

Both samples use the same reconnect states as the polling reconnect sample:
`connected`, `lost`, `reconnecting`, and `recovered`, with 1 second initial
backoff, exponential delay, and a 30 second default maximum. YAML config is
available only in the Python sample; the .NET sample uses JSON.

```powershell
dotnet run --project samples/PlcComm.Slmp.MultiPlcMonitorSample -- --plc line-a=192.168.250.101,melsec:iq-r,1035,udp --plc line-b=192.168.250.100,melsec:iq-f,1025,tcp --tag d100=D100:U
dotnet run --project samples/PlcComm.Slmp.ConfigPollingSample -- --config samples/PlcComm.Slmp.ConfigPollingSample/config_polling.example.json --dry-run
```

## Device range catalog

`ReadDeviceRangeCatalogAsync` reads live device range bounds from your PLC after you connect with an explicit PLC profile. It does not auto-discover the profile.
The source rules for this catalog are maintained in the shared [SLMP device ranges](https://fa-yoshinobu.github.io/plc-comm-docs-site/slmp/profile-reference/device-ranges/) reference.

```csharp
using System;
using System.Linq;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

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

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

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
