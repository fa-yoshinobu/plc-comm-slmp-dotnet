# SLMP .NET User Guide

This guide documents the recommended high-level API surface.

For normal application code, prefer the extension methods on `SlmpClient` and `QueuedSlmpClient`.

## Recommended Connection Pattern

Create a connected queued client with one explicit PLC family:

```csharp
using PlcComm.Slmp;

var options = new SlmpConnectionOptions("192.168.250.100", SlmpPlcFamily.IqR)
{
    Port = 1025,
};

await using var client = await SlmpClientFactory.OpenAndConnectAsync(options);
```

`SlmpConnectionOptions` derives frame type, compatibility mode, string `X/Y`
rules, and the device-range family from the explicit `SlmpPlcFamily`.

Use the queued client as the default application object. It is the safest choice when more than one task may touch the same connection.

Use `*SingleRequestAsync` when one protocol request is required. Use
`*ChunkedAsync` only when it is acceptable to split the operation across
multiple requests at word or dword boundaries.

## High-Level Helpers

### `ReadTypedAsync` / `WriteTypedAsync`

Read or write one logical value with conversion.

| dtype | Meaning | Words |
| --- | --- | --- |
| `U` | unsigned 16-bit | 1 |
| `S` | signed 16-bit | 1 |
| `D` | unsigned 32-bit | 2 |
| `L` | signed 32-bit | 2 |
| `F` | float32 | 2 |

```csharp
ushort counter = (ushort)await client.ReadTypedAsync("D100", "U");
float temperature = (float)await client.ReadTypedAsync("D200", "F");
int position = (int)await client.ReadTypedAsync("D300", "L");

await client.WriteTypedAsync("D100", "U", (ushort)42);
await client.WriteTypedAsync("D200", "F", 3.14f);
await client.WriteTypedAsync("D300", "L", -100);
```

### `WriteBitInWordAsync`

Set or clear one bit inside a word device.

```csharp
await client.WriteBitInWordAsync("D50", bitIndex: 3, value: true);
await client.WriteBitInWordAsync("D50", bitIndex: 3, value: false);
```

Use this when a PLC exposes individual flags inside one control word.

### `ReadNamedAsync`

Read a mixed snapshot with one call.

Address notation:

| Form | Meaning |
| --- | --- |
| `D100` | one unsigned 16-bit word |
| `D200:S` | signed 16-bit |
| `D300:D` | unsigned 32-bit |
| `D400:L` | signed 32-bit |
| `D500:F` | float32 |
| `D50.3` | bit 3 inside D50 |

```csharp
var snapshot = await client.ReadNamedAsync(
[
    "D100",
    "D200:S",
    "D300:D",
    "D400:L",
    "D500:F",
    "D50.3",
]);
```

Use `.bit` notation only with word devices such as `D50.3`.
Address bit devices directly as `M1000`, `M1001`, `X20`, or `Y20`.

Long-device notes for the high-level helper layer:

- plain `LTN`, `LSTN`, `LCN`, and `LZ` addresses default to 32-bit access
- `LCN` current-value reads and writes use random dword access in the high-level helpers
- `LTS`, `LTC`, `LSTS`, and `LSTC` are resolved through the corresponding `ReadLongTimerAsync` / `ReadLongRetentiveTimerAsync` helper-backed 4-word decode instead of direct state reads
- `LCS` and `LCC` use direct bit read, and high-level state writes use random bit write (`0x1402`)

This is the recommended helper for dashboards, periodic snapshots, and application logic that needs mixed values.

### `ReadWordsSingleRequestAsync`

Read a contiguous word range.

```csharp
ushort[] words = await client.ReadWordsSingleRequestAsync("D0", 10);
```

This helper does not silently split one logical request.

### `ReadDWordsSingleRequestAsync`

Read contiguous 32-bit values.

```csharp
uint[] dwords = await client.ReadDWordsSingleRequestAsync("D200", 8);
```

### `ReadWordsChunkedAsync` / `ReadDWordsChunkedAsync`

Use explicit chunked helpers when the range is too large for one request and
splitting is acceptable:

```csharp
ushort[] largeWords = await client.ReadWordsChunkedAsync("D0", 1000, maxWordsPerRequest: 480);
uint[] largeDwords = await client.ReadDWordsChunkedAsync("D200", 200, maxDwordsPerRequest: 240);
```

### `PollAsync`

Read the same logical snapshot repeatedly.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

await foreach (var snapshot in client.PollAsync(
    ["D100", "D200:F", "D50.3"],
    TimeSpan.FromSeconds(1),
    cts.Token))
{
    Console.WriteLine(snapshot["D100"]);
}
```

## Practical Example Sets

### Example 1: process values

```csharp
var snapshot = await client.ReadNamedAsync(
[
    "D100:F",
    "D102:F",
    "D200",
    "D50.0",
    "D50.1",
]);
```

### Example 2: recipe download

```csharp
await client.WriteTypedAsync("D100", "U", (ushort)10);
await client.WriteTypedAsync("D101", "U", (ushort)20);
await client.WriteTypedAsync("D102", "U", (ushort)30);
await client.WriteTypedAsync("D200", "F", 12.5f);
await client.WriteTypedAsync("D202", "F", 6.75f);
```

### Example 3: historian-style reads

```csharp
ushort[] historyWords = await client.ReadWordsChunkedAsync("D1000", 1200, maxWordsPerRequest: 480);
uint[] historyDwords = await client.ReadDWordsChunkedAsync("D2000", 240, maxDwordsPerRequest: 240);
```

### Example 4: one shared connection for many tasks

```csharp
var first = client.ReadNamedAsync(["D100", "D200:F"]);
var second = client.ReadNamedAsync(["D300", "D50.3"]);

await Task.WhenAll(first, second);
```

## Address Normalization

Use `SlmpAddress.Normalize` when you need one stable string form:

```csharp
string canonical = SlmpAddress.Normalize("d100");
Console.WriteLine(canonical); // D100
```

## Sample Projects

The main user-facing sample projects are:

- `samples/PlcComm.Slmp.HighLevelSample`
- `samples/PlcComm.Slmp.QueuedSample`

Run them from the repository root:

```powershell
dotnet run --project samples/PlcComm.Slmp.HighLevelSample -- 192.168.250.100 1025 iqr 4e
dotnet run --project samples/PlcComm.Slmp.QueuedSample -- 192.168.250.100 1025 4 10
```
