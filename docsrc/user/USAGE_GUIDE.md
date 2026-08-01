# Usage guide

## Recommended entry points

| API | Use |
| --- | --- |
| `SlmpConnectionOptions` | Holds host, profile, port, transport, timeout, target, and monitoring timer settings. |
| `SlmpClientFactory.OpenAndConnectAsync` | Opens a connected `SlmpClient` from `SlmpConnectionOptions`. |
| `ReadTypedAsync` | Reads one typed scalar such as `D100` as `BIT`, `U`, `S`, `D`, `L`, or `F`. |
| `WriteTypedAsync` | Writes one typed scalar. |

Every semantic `SlmpDeviceAddress` or qualified address is bound to the exact canonical PLC profile used to create it. Passing it to a client configured for any other profile is rejected before request construction or transport activity, including when a unit-specific profile shares a base family with the client. Parse or construct the address again with the destination client's profile instead of reusing it across profiles.
| `ReadNamedAsync` | Reads one named value set that fits exactly one Random Read request. |
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
await client.RemotePasswordUnlockAsync("secret");
try
{
    var value = await client.ReadTypedAsync("D100", "U");
}
finally
{
    await client.RemotePasswordLockAsync("secret");
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

These methods are exposed directly by `SlmpClient`. Self-test accepts only
1–960 ASCII `0-9/A-F` bytes and requires exact declared length,
actual length, and echo equality. Clear Error always uses the fixed empty
payload command.

## Label wire data

Array-label lengths are logical lengths, while `Data` is the raw SLMP wire
representation padded to a two-byte boundary. For `UnitSpecification = 0`
(bit), the exact byte length is `ceil(ArrayDataLength / 16) * 2`. For
`UnitSpecification = 1` (byte), it is
`ceil(ArrayDataLength / 2) * 2`. Array writes reject any other data length.

Random-label write data must contain a positive even number of bytes. The
library does not infer a PLC label's configured type from its name; a unit or
type mismatch that cannot be known locally is returned as a PLC end code.
Malformed label responses, including count mismatches, invalid units,
truncation, odd random-data lengths, and trailing bytes, raise `SlmpError`.

The complete command payload must fit the request data-length field. Over TCP
the maximum command payload is 65,529 bytes. This client uses IPv4 UDP, whose
complete datagram limit makes the command-payload maximum 65,492 bytes for 3E
and 65,488 bytes for 4E. Label command payloads are always even-sized, so their
largest protocol-level payload is 65,528 bytes before applying the smaller UDP
limit. Oversized inputs raise `ArgumentOutOfRangeException` before opening or
sending, and the library does not split one label command into multiple frames.

## Close and disposal

Every ordinary client has one arrival-order FIFO operation queue. One complete
operation owns the connection at a time; this includes both requests in
`WriteBitInWordAsync`. Arguments are validated and snapshotted when submitted.
The submitted `Timeout` and `MonitoringTimer` values are also snapshotted, so
later property changes affect only calls submitted later.
Canceling while waiting removes that operation without sending, and queue wait
does not consume the transaction timeout. Use separate clients for independent
parallel sessions.

`Close` ends the current transport generation, rejects its active and queued
operations, and permits a later `OpenAsync`. A queued or read-only active operation
reports `SlmpConnectionClosedException`; an active state-changing request whose
bytes may already have been sent reports `SlmpOperationOutcomeUnknownException`
with reason `Closed`. If the matching response has already passed route/serial,
protocol, end-code, length, and command-specific decoding, that definitive success
or PLC end-code remains the result even when close or disposal occurs concurrently.
`Dispose` and `DisposeAsync` are terminal and idempotent: later open, read, or
write operations throw `ObjectDisposedException`. A client should not be
reused after leaving a `using` or `await using` scope.

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

Semantic `BIT` operations accept only bit-addressable families such as `M`, `X`,
and `Y`. Numeric and string scalar types accept only word-addressable families.
Use `ReadWordsRawAsync` or `WriteWordsAsync` explicitly when packed 16-bit access
to a bit-device range is intentional. Use `.0` through `.F` or
`ReadNamedAsync(["D100.0"])` to read one bit inside a word device, and use
`WriteBitInWordAsync` for the corresponding explicit non-atomic read-modify-write.
An invalid `D100:BIT` call is never translated automatically, and
`WriteBitInWordAsync` rejects bit-device families. Its read and write admission,
including writable-target and complete-span checks, finishes before FIFO waiting;
an invalid target sends neither request. Typed, named, polling, and long-timer
reads likewise complete their full route/span admission before FIFO waiting.

Typed writes do not parse strings or convert Boolean and floating-point values into
integers. `BIT` requires `bool`; U/S/D/L require integral CLR values in their exact
ranges; F requires a finite numeric value within the float32 range.

The same Boolean-only contract applies to direct, extended, random, named, and
bit-in-word writes. There is no numeric or string compatibility overload.
Packed bit-block words are a distinct wire-level API and remain `ushort` values.

Communication timeout values must be at least 1 millisecond. The transaction uses
one absolute deadline from a lazy connection attempt through send, complete TCP/UDP
response framing, route/serial filtering, and response decoding. Partial progress,
foreign responses, and response fragments do not restart it. FIFO queue wait is not
part of the transaction deadline. Explicit `OpenAsync` uses the same configured value
as its connection deadline.

A read deadline expires as `SlmpTimeoutException`; connection and I/O failures use
`SlmpTransportException` with the native failure retained as its inner exception.
Caller cancellation remains an `OperationCanceledException`, and local close remains distinct. After a request is
sent and then times out, is cancelled, receives a malformed response, or loses
transport ownership, the client remains invalidated until `OpenAsync` is called.
If the request can change PLC state, the public error is
`SlmpOperationOutcomeUnknownException`; inspect its `Reason` but do not retry the
operation automatically. First reconcile PLC/application state using the controlled
read or handshake appropriate to the process. `RemoteResetAsync` also closes and
invalidates its send-only transport; its completion confirms transmission, not PLC
execution.

Single-request limits are the minimum of the selected profile/command point limit,
the 16-bit SLMP data-length field, and the IPv4 TCP/UDP frame capacity. The managed
client has dynamic receive/result storage and no caller-owned output buffer limit;
the response length is still bounded by SLMP framing. Maximum-size requests remain
one request, while maximum-plus-one is rejected before serial allocation, trace,
counters, connection opening, or send. `ReadNamedAsync` and `WriteNamedAsync` also
remain single-request APIs and reject plans that need another command/request.

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

`WriteBitInWordAsync` holds one FIFO turn on this client, but it remains two
SLMP requests: one word read and one word write. It is not PLC-atomic against
PLC logic, another connection, or another controller. Treat a post-send write
failure as outcome-unknown and reconcile PLC state before retrying.

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

`ReadDeviceRangeCatalogAsync` reads the canonical profile's required SD-register window after you connect with an explicit PLC profile. It does not auto-discover the profile, probe candidate addresses, or infer a smaller range from a failed PLC request. Any timeout, cancellation, transport, protocol, route, password, busy, or other PLC error is returned to the caller; a range without an authoritative value remains unknown.
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

`LTN`, `LSTN`, `LCN`, and `LZ` are 32-bit families. Pass `D` or `L` as the
`ReadTypedAsync` / `WriteTypedAsync` dtype. Ordinary named reads reject `LTN`,
`LSTN`, and their contact/coil families because those values require a Direct
Read helper rather than the single Random Read used by `ReadNamedAsync`.

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var current = await client.ReadTypedAsync("LTN0", "D");
var timers = await client.ReadLongTimerAsync(0, 1);
var snapshot = await client.ReadNamedAsync(["LCN0:D", "LZ0:L"]);

Console.WriteLine($"LTN0 = {current}");
Console.WriteLine($"LTN0 status = 0x{timers[0].StatusWord:X4}");
Console.WriteLine($"LCN0:D = {snapshot["LCN0:D"]}");
```

> **Caution:** Plain word access to `LTN`, `LSTN`, `LCN`, and `LZ` is rejected by the library.

Direct DWord and Float32 reads/writes accept `1..480` public values when the
active profile permits 960 Direct Word points. Invalid numeric counts throw
`ArgumentOutOfRangeException` before multiplication, allocation, or transport.
Every contiguous request must also fit the address field selected by the wire
format: Q/L-compatible and link-direct layouts use 24 bits, while iQ-R layouts
use 32 bits. Admission uses the complete consumed span, not the configured PLC
device-range catalog. Word-unit access to a word device consumes one device per
word; word-unit packed access to a bit device consumes 16 bit devices per word;
ordinary DWord/Float32 access consumes two word devices per value (32 bit-device
numbers per value when packed through a bit family); and one bit-block point
consumes 16 bit devices. The long-timer Direct status block is the explicit
exception: four returned words consume one `LTN`/`LSTN` device. Random and
monitor DWord entries use the same route-specific logical widths. A span that
crosses the wire maximum is rejected before connection, frame publication,
request counters, or transport; the library does not substitute profile
usable-range policy for this wire-representability check.
Malformed, negative, or out-of-range numeric fields in named targets and
qualified `U`/`J` device text throw field-specific `FormatException` without
truncation. U extension fields are hexadecimal `0000..FFFF` (`0..65535`), and
J-direct network fields are decimal `0..255`.

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
## Traffic statistics

Read `client.TrafficStats` for a client-lifetime
snapshot of `RequestCount`, `TxBytes`, and `RxBytes`. Complete sends and complete received frames
are counted; close and reconnect do not reset the snapshot.
