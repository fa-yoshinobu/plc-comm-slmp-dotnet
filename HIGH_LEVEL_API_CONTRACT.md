# High-Level API Contract

This document defines the target public API shape for the SLMP .NET library.
Backward compatibility is not a design constraint for this contract.

This contract is intentionally aligned with:

- `plc-comm-hostlink-dotnet`
- `plc-comm-computerlink-dotnet`

## 1. Design Goals

- keep one obvious high-level entry point for application code
- keep typed read/write helpers consistent across the three .NET PLC libraries
- make connection options explicit instead of implicit
- preserve semantic atomicity by default
- forbid hidden fallback splitting that changes the meaning of one logical request

## 2. Primary Client Shape

Application-facing code should use a connected, async-safe client wrapper as the primary entry point.

Target shape:

```csharp
public sealed record SlmpConnectionOptions(
    string Host,
    int Port = 1025,
    TimeSpan Timeout = default,
    SlmpFrameType FrameType = SlmpFrameType.Frame4E,
    SlmpCompatibilityMode CompatibilityMode = SlmpCompatibilityMode.Iqr,
    SlmpTargetAddress Target = default
);

public static class SlmpClientFactory
{
    public static Task<QueuedSlmpClient> OpenAndConnectAsync(
        SlmpConnectionOptions options,
        CancellationToken cancellationToken = default);
}
```

Notes:

- the returned client must be safe to share across multiple async callers
- frame type, compatibility mode, and target routing must stay explicit
- automatic profile probing is out of scope

## 3. Required High-Level Methods

The primary client should expose or clearly own these operations:

```csharp
Task<object> ReadTypedAsync(
    string address,
    string dtype,
    CancellationToken cancellationToken = default);

Task WriteTypedAsync(
    string address,
    string dtype,
    object value,
    CancellationToken cancellationToken = default);

Task WriteBitInWordAsync(
    string address,
    int bitIndex,
    bool value,
    CancellationToken cancellationToken = default);

Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
    IEnumerable<string> addresses,
    CancellationToken cancellationToken = default);

IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
    IEnumerable<string> addresses,
    TimeSpan interval,
    CancellationToken cancellationToken = default);
```

## 4. Contiguous Read/Write Contract

Contiguous block access must distinguish between three behaviors:

### 4.1 Single-request behavior

Use this when the caller requires one protocol request or an error.

```csharp
Task<ushort[]> ReadWordsSingleRequestAsync(
    string start,
    int count,
    CancellationToken cancellationToken = default);

Task<uint[]> ReadDWordsSingleRequestAsync(
    string start,
    int count,
    CancellationToken cancellationToken = default);

Task WriteWordsSingleRequestAsync(
    string start,
    IReadOnlyList<ushort> values,
    CancellationToken cancellationToken = default);

Task WriteDWordsSingleRequestAsync(
    string start,
    IReadOnlyList<uint> values,
    CancellationToken cancellationToken = default);
```

### 4.2 Semantic-atomic behavior

Use this when the caller cares about logical value integrity but accepts documented protocol boundaries.

- do not split one logical `DWord` / `Float32`
- do not split one caller-visible logical block through hidden fallback logic
- if one request cannot preserve semantics, return an error

### 4.3 Explicit chunked behavior

Use this only when the caller explicitly opts into segmentation.

```csharp
Task<ushort[]> ReadWordsChunkedAsync(
    string start,
    int count,
    int maxWordsPerRequest,
    CancellationToken cancellationToken = default);

Task<uint[]> ReadDWordsChunkedAsync(
    string start,
    int count,
    int maxDwordsPerRequest,
    CancellationToken cancellationToken = default);

Task WriteWordsChunkedAsync(
    string start,
    IReadOnlyList<ushort> values,
    int maxWordsPerRequest,
    CancellationToken cancellationToken = default);

Task WriteDWordsChunkedAsync(
    string start,
    IReadOnlyList<uint> values,
    int maxDwordsPerRequest,
    CancellationToken cancellationToken = default);
```

## 5. Atomicity Rules

These rules are normative.

- `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, and `ReadNamedAsync` must preserve logical value integrity
- default APIs must not silently split one logical request into different semantics after an error
- fallback retry with a different write shape must be opt-in and explicitly named
- if the library cannot preserve the requested semantics, it should return an error

For SLMP specifically:

- mixed block write retry logic must not be the default behavior
- any retry path that changes one mixed write into two separate writes must remain explicit and clearly documented

## 6. Address Helper Contract

String address handling should be public and reusable instead of duplicated in UI or adapter code.

Target shape:

```csharp
public static class SlmpAddress
{
    public static bool TryParse(string text, out SlmpDeviceAddress address);
    public static SlmpDeviceAddress Parse(string text);
    public static string Format(SlmpDeviceAddress address);
    public static string Normalize(string text);
}
```

High-level logical address helpers should remain available for:

- `D100`
- `D100:S`
- `D200:D`
- `D200:L`
- `D200:F`
- `D50.3`

## 7. Error Contract

- invalid address text should fail deterministically during parsing
- unsupported dtype should fail before any transport call
- operations that require preserved semantics should fail instead of silently degrading into chunked behavior
- protocol errors should keep the PLC end code visible to callers

## 8. Non-Goals

- no automatic profile detection
- no hidden retries that change request semantics
- no requirement to preserve old extension-method naming if a cleaner public surface is chosen
