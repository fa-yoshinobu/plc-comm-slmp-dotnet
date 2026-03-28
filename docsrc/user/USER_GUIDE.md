# SLMP .NET User Guide

This guide documents the recommended high-level API surface.

For normal application code, prefer the extension methods on `SlmpClient` and `QueuedSlmpClient`.

## Recommended Connection Pattern

Create a connected queued client with explicit stable settings:

```csharp
using PlcComm.Slmp;

await using var client = await SlmpClient.OpenAndConnectAsync(
    "192.168.250.100",
    1025,
    SlmpFrameType.Frame4E,
    SlmpCompatibilityMode.Iqr);
```

Typical pairs:

| PLC family | Frame | Compatibility |
| --- | --- | --- |
| iQ-R / iQ-F | `Frame4E` | `Iqr` |
| Q / L | `Frame3E` | `Legacy` |

Use the queued client as the default application object. It is the safest choice when more than one task may touch the same connection.

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

- plain `LTN`, `LSTN`, and `LCN` addresses default to 32-bit current-value access
- `LTS`, `LTC`, `LSTS`, and `LSTC` are resolved through the corresponding `ReadLongTimerAsync` / `ReadLongRetentiveTimerAsync` helper-backed 4-word decode instead of direct state reads

This is the recommended helper for dashboards, periodic snapshots, and application logic that needs mixed values.

### `ReadWordsAsync`

Read a contiguous word range.

```csharp
ushort[] words = await client.ReadWordsAsync("D0", 10);
ushort[] largeWords = await client.ReadWordsAsync("D0", 1000, allowSplit: true);
```

Use `allowSplit: true` when the request exceeds one SLMP frame.

### `ReadDWordsAsync`

Read contiguous 32-bit values.

```csharp
uint[] dwords = await client.ReadDWordsAsync("D200", 8);
uint[] largeDwords = await client.ReadDWordsAsync("D200", 200, allowSplit: true);
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
ushort[] historyWords = await client.ReadWordsAsync("D1000", 1200, allowSplit: true);
uint[] historyDwords = await client.ReadDWordsAsync("D2000", 240, allowSplit: true);
```

### Example 4: one shared connection for many tasks

```csharp
var first = client.ReadNamedAsync(["D100", "D200:F"]);
var second = client.ReadNamedAsync(["D300", "D50.3"]);

await Task.WhenAll(first, second);
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
