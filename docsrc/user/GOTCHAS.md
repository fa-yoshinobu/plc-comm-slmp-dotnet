# Gotchas

## LTN/LSTN/LCN/LZ returns wrong values

| Item | Detail |
| --- | --- |
| Symptom | Long timer, long counter, or long index values look truncated or shifted. |
| Root cause | `LTN`, `LSTN`, `LCN`, and `LZ` are 32-bit logical families. |
| Fix | Use `:D` or `:L` in named addresses, or pass `D` or `L` to `ReadTypedAsync`. |

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("LTN0", "D");
Console.WriteLine($"LTN0 = {value}");
```

## LCS/LCC reads incorrect

| Item | Detail |
| --- | --- |
| Symptom | Long counter state reads do not match the expected contact or coil state. |
| Root cause | `LCS` and `LCC` are state bits; they are not 16-bit word current values. |
| Fix | Read them as bit values and let `WriteTypedAsync` / `WriteNamedAsync` route writes through random bit write. |

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var state = await client.ReadTypedAsync("LCS0", "BIT");
await client.WriteTypedAsync("LCC0", "BIT", true);
Console.WriteLine($"LCS0 = {state}");
```

## LTS/LTC/LSTS/LSTC write rejected

| Item | Detail |
| --- | --- |
| Symptom | Writing a long timer contact or coil through a normal direct bit route is rejected. |
| Root cause | Long timer state writes use SLMP random bit write, not ordinary direct bit write. |
| Fix | Use `WriteTypedAsync` or `WriteNamedAsync` so the library selects the correct route. |

```csharp
using System;
using System.Collections.Generic;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
await client.WriteTypedAsync("LTC0", "BIT", true);
var state = await client.ReadTypedAsync("LTC0", "BIT");
Console.WriteLine($"LTC0 = {state}");
```

## S write rejected

| Item | Detail |
| --- | --- |
| Symptom | `S10:BIT` can be read, but write routes reject it. |
| Root cause | The selected profile marks step relay `S` as read-only. iQ-F profiles allow `S` writes. |
| Fix | Keep `S` out of write lists and use it only for reads. |

## G/HG fails

| Item | Detail |
| --- | --- |
| Symptom | `G` or `HG` fails in the high-level typed or named API. |
| Root cause | Module buffer memory is outside the public high-level device surface. |
| Fix | Use low-level module-buffer APIs on the raw `SlmpClient` when you need module buffer access. |

```csharp
using System;
using System.Threading.Tasks;
using PlcComm.Slmp;

await using var raw = new SlmpClient("192.168.250.100", SlmpPlcProfile.IqR, port: 1025);
await raw.OpenAsync();

var device = SlmpQualifiedDeviceParser.Parse(@"U3\G100");
var extension = new SlmpExtensionSpec();
ushort[] words = await raw.ReadWordsExtendedAsync(device, points: 4, extension);
Console.WriteLine($"G100 words = {words.Length}");
```

## Mixed write fails

| Item | Detail |
| --- | --- |
| Symptom | One mixed write containing word values and bit values fails. |
| Root cause | The PLC rejects SLMP command `0x1406` for a mixed word and bit request. |
| Fix | Send word writes and bit writes as separate calls. |

## Q-series profiles reject block commands

| Item | Detail |
| --- | --- |
| Symptom | `ReadBlockAsync` or `WriteBlockAsync` throws when the client is configured for `melsec:qcpu`, `melsec:qnu`, or `melsec:qnudv`. |
| Root cause | These Q-series profiles reject SLMP Read Block (`0x0406`) and Write Block (`0x1406`) before transport. |
| Fix | Use direct or random device commands for those profiles. |

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
await client.WriteNamedAsync(new Dictionary<string, object>
{
    ["D9000:U"] = (ushort)1234,
});
await client.WriteNamedAsync(new Dictionary<string, object>
{
    ["M9000:BIT"] = true,
});
Console.WriteLine("Separated word and bit writes.");
```

## DX/DY fails on iQ-F

| Item | Detail |
| --- | --- |
| Symptom | `DX` or `DY` is rejected when the selected profile is `SlmpPlcProfile.IqF`. |
| Root cause | iQ-F profile rules do not support `DX` and `DY`. |
| Fix | Use `X` and `Y` with the iQ-F profile. |

```csharp
using System;
using PlcComm.Slmp;

var parsed = SlmpAddress.Parse("X20", SlmpPlcProfile.IqF);
Console.WriteLine(SlmpAddress.Format(parsed, SlmpPlcProfile.IqF));
```

## All reads return end code

| Item | Detail |
| --- | --- |
| Symptom | The connection opens, but every read returns an SLMP end-code error. |
| Root cause | The selected `SlmpPlcProfile` does not match the actual PLC hardware. |
| Fix | Choose the profile that matches the PLC; the profile determines frame type and compatibility mode. |

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
Console.WriteLine($"{client.FrameType} {client.CompatibilityMode}");
```

## SlmpPlcProfile.Unspecified throws immediately

| Item | Detail |
| --- | --- |
| Symptom | Creating connection options or resolving defaults with `SlmpPlcProfile.Unspecified` fails before any PLC request. |
| Root cause | `Unspecified` is not a concrete PLC profile and cannot select frame type or compatibility mode. |
| Fix | Use a concrete .NET selector such as `SlmpPlcProfile.IqR`. |

```csharp
using System;
using PlcComm.Slmp;

try
{
    _ = SlmpPlcProfiles.Resolve(SlmpPlcProfile.Unspecified);
}
catch (ArgumentOutOfRangeException)
{
    var defaults = SlmpPlcProfiles.Resolve(SlmpPlcProfile.IqR);
    Console.WriteLine($"{defaults.FrameType} {defaults.CompatibilityMode}");
}
```

## Concurrent callers crash

| Item | Detail |
| --- | --- |
| Symptom | Multiple async callers using one raw `SlmpClient` cause overlapping requests or inconsistent responses. |
| Root cause | Raw `SlmpClient` is not thread-safe for concurrent callers. |
| Fix | Use the `QueuedSlmpClient` returned by `SlmpClientFactory.OpenAndConnectAsync`. |

```csharp
using System;
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcProfile.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
var first = client.ReadTypedAsync("D100", "U");
var second = client.ReadTypedAsync("D101", "U");
var values = await Task.WhenAll(first, second);
Console.WriteLine($"{values[0]}, {values[1]}");
```

## SlmpEndCodes.GetMessage returns null

| Item | Detail |
| --- | --- |
| Symptom | Code that calls `SlmpEndCodes.GetMessage(endCode)` to display a human-readable error receives `null`. |
| Root cause | The library no longer embeds localized SLMP end-code descriptions. |
| Fix | Call `SlmpEndCodes.GetName(endCode)` to get a stable key such as `slmp_end_code_c201`, then look it up in an application-owned message catalog. |

```csharp
using System;
using PlcComm.Slmp;

ushort endCode = 0xC201;
string key = SlmpEndCodes.GetName(endCode);
// key == "slmp_end_code_c201"
// Resolve the display text from your own resource file or dictionary.
Console.WriteLine(key);
```

## X or Y addresses do not match an iQ-F manual

| Item | Detail |
| --- | --- |
| Symptom | `X` or `Y` addresses look shifted on iQ-F. |
| Root cause | iQ-F uses octal notation for `X` and `Y`; other supported profiles use hexadecimal. |
| Fix | Parse or normalize `X` and `Y` with the explicit iQ-F profile. |

```csharp
using System;
using PlcComm.Slmp;

var address = SlmpAddress.Parse("Y217", SlmpPlcProfile.IqF);
Console.WriteLine(SlmpAddress.Format(address, SlmpPlcProfile.IqF));
```
