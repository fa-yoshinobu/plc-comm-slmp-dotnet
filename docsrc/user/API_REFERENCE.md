# SLMP .NET API Reference

This page is generated from the `PlcComm.Slmp` assembly public API and XML documentation comments.

Run `python scripts/generate_api_reference.py --help` from the repository root to regenerate it.

## PlcComm.Slmp

### QueuedSlmpClient

```csharp
public sealed class QueuedSlmpClient
```

A wrapper for `SlmpClient` that serializes all operations using a semaphore. Useful for environments where a single shared connection must handle multiple concurrent callers.

Remarks: This type is intentionally thin: it keeps the low-level client visible through `InnerClient`, but ensures that compound async helper flows can reuse a single transport session without overlapping request lifetimes.

#### Members

##### QueuedSlmpClient

```csharp
public QueuedSlmpClient(SlmpClient client)
```

Initializes a new instance of the `QueuedSlmpClient` class.

Parameters:
- `client`: The underlying `SlmpClient` to wrap.

##### OpenAsync

```csharp
public Task OpenAsync(CancellationToken cancellationToken = default)
```

Opens the connection asynchronously, ensuring exclusive access during the operation.

Remarks: Repeated calls are safe as long as the underlying client supports reopening the session.

##### ExecuteAsync

```csharp
public Task<T> ExecuteAsync<T>(Func<SlmpClient, Task<T>> operation, CancellationToken cancellationToken = default)
```

Executes a custom operation on the underlying client with exclusive access.

Returns: The value returned by `operation`.

Parameters:
- `operation`: Delegate that receives the wrapped `SlmpClient`.
- `cancellationToken`: Cancellation token used while waiting for the queue gate.

##### ExecuteAsync

```csharp
public Task ExecuteAsync(Func<SlmpClient, Task> operation, CancellationToken cancellationToken = default)
```

Executes a custom action on the underlying client with exclusive access.

Parameters:
- `operation`: Delegate that receives the wrapped `SlmpClient`.
- `cancellationToken`: Cancellation token used while waiting for the queue gate.

##### ReadTypeNameAsync

```csharp
public Task<SlmpTypeNameInfo> ReadTypeNameAsync(CancellationToken cancellationToken = default)
```

##### ReadCpuOperationStateAsync

```csharp
public Task<SlmpCpuOperationState> ReadCpuOperationStateAsync(CancellationToken cancellationToken = default)
```

##### ReadDeviceRangeCatalogAsync

```csharp
public Task<SlmpDeviceRangeCatalog> ReadDeviceRangeCatalogAsync(CancellationToken cancellationToken = default)
```

##### ReadDeviceRangeCatalogAsync

```csharp
public Task<SlmpDeviceRangeCatalog> ReadDeviceRangeCatalogAsync(SlmpPlcProfile plcProfile, CancellationToken cancellationToken = default)
```

##### ReadWordsRawAsync

```csharp
public Task<ushort[]> ReadWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

##### WriteWordsAsync

```csharp
public Task WriteWordsAsync(SlmpDeviceAddress device, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
```

##### ReadBitsAsync

```csharp
public Task<bool[]> ReadBitsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

##### WriteBitsAsync

```csharp
public Task WriteBitsAsync(SlmpDeviceAddress device, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
```

##### ReadDWordsRawAsync

```csharp
public Task<uint[]> ReadDWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

##### WriteDWordsAsync

```csharp
public Task WriteDWordsAsync(SlmpDeviceAddress device, IReadOnlyList<uint> values, CancellationToken cancellationToken = default)
```

##### ReadFloat32sAsync

```csharp
public Task<float[]> ReadFloat32sAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

##### WriteFloat32sAsync

```csharp
public Task WriteFloat32sAsync(SlmpDeviceAddress device, IReadOnlyList<float> values, CancellationToken cancellationToken = default)
```

##### ReadRandomAsync

```csharp
public Task<ValueTuple<ushort[], uint[]>> ReadRandomAsync(IReadOnlyList<SlmpDeviceAddress> wordDevices, IReadOnlyList<SlmpDeviceAddress> dwordDevices, CancellationToken cancellationToken = default)
```

##### WriteRandomWordsAsync

```csharp
public Task WriteRandomWordsAsync(IReadOnlyList<ValueTuple<SlmpDeviceAddress, ushort>> wordEntries, IReadOnlyList<ValueTuple<SlmpDeviceAddress, uint>> dwordEntries, CancellationToken cancellationToken = default)
```

##### WriteRandomBitsAsync

```csharp
public Task WriteRandomBitsAsync(IReadOnlyList<ValueTuple<SlmpDeviceAddress, bool>> bitEntries, CancellationToken cancellationToken = default)
```

##### ReadBlockAsync

```csharp
public Task<ValueTuple<ushort[], ushort[]>> ReadBlockAsync(IReadOnlyList<SlmpBlockRead> wordBlocks, IReadOnlyList<SlmpBlockRead> bitBlocks, CancellationToken cancellationToken = default)
```

##### WriteBlockAsync

```csharp
public Task WriteBlockAsync(IReadOnlyList<SlmpBlockWrite> wordBlocks, IReadOnlyList<SlmpBlockWrite> bitBlocks, SlmpBlockWriteOptions options = null, CancellationToken cancellationToken = default)
```

##### ReadBitsExtendedAsync

```csharp
public Task<bool[]> ReadBitsExtendedAsync(SlmpQualifiedDeviceAddress device, ushort points, SlmpExtensionSpec extension, CancellationToken cancellationToken = default)
```

##### WriteBitsExtendedAsync

```csharp
public Task WriteBitsExtendedAsync(SlmpQualifiedDeviceAddress device, IReadOnlyList<bool> values, SlmpExtensionSpec extension, CancellationToken cancellationToken = default)
```

##### ReadWordsExtendedAsync

```csharp
public Task<ushort[]> ReadWordsExtendedAsync(SlmpQualifiedDeviceAddress device, ushort points, SlmpExtensionSpec extension, CancellationToken cancellationToken = default)
```

##### WriteWordsExtendedAsync

```csharp
public Task WriteWordsExtendedAsync(SlmpQualifiedDeviceAddress device, IReadOnlyList<ushort> values, SlmpExtensionSpec extension, CancellationToken cancellationToken = default)
```

##### ReadRandomExtAsync

```csharp
public Task<ValueTuple<ushort[], uint[]>> ReadRandomExtAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, SlmpExtensionSpec>> wordDevices, IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, SlmpExtensionSpec>> dwordDevices, CancellationToken cancellationToken = default)
```

##### WriteRandomWordsExtAsync

```csharp
public Task WriteRandomWordsExtAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, ushort, SlmpExtensionSpec>> wordEntries, IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, uint, SlmpExtensionSpec>> dwordEntries, CancellationToken cancellationToken = default)
```

##### WriteRandomBitsExtAsync

```csharp
public Task WriteRandomBitsExtAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, bool, SlmpExtensionSpec>> bitEntries, CancellationToken cancellationToken = default)
```

##### ReadLongTimerAsync

```csharp
public Task<SlmpLongTimerResult[]> ReadLongTimerAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

##### ReadLongRetentiveTimerAsync

```csharp
public Task<SlmpLongTimerResult[]> ReadLongRetentiveTimerAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

##### ReadLtcStatesAsync

```csharp
public Task<bool[]> ReadLtcStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

##### ReadLtsStatesAsync

```csharp
public Task<bool[]> ReadLtsStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

##### ReadLstcStatesAsync

```csharp
public Task<bool[]> ReadLstcStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

##### ReadLstsStatesAsync

```csharp
public Task<bool[]> ReadLstsStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

##### ReadArrayLabelsAsync

```csharp
public Task<SlmpLabelArrayReadResult[]> ReadArrayLabelsAsync(IReadOnlyList<SlmpLabelArrayReadPoint> points, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

##### WriteArrayLabelsAsync

```csharp
public Task WriteArrayLabelsAsync(IReadOnlyList<SlmpLabelArrayWritePoint> points, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

##### ReadRandomLabelsAsync

```csharp
public Task<SlmpLabelRandomReadResult[]> ReadRandomLabelsAsync(IReadOnlyList<string> labels, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

##### WriteRandomLabelsAsync

```csharp
public Task WriteRandomLabelsAsync(IReadOnlyList<SlmpLabelRandomWritePoint> points, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

##### MemoryReadWordsAsync

```csharp
public Task<ushort[]> MemoryReadWordsAsync(uint headAddress, ushort wordLength, CancellationToken cancellationToken = default)
```

##### MemoryWriteWordsAsync

```csharp
public Task MemoryWriteWordsAsync(uint headAddress, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
```

##### ExtendUnitReadBytesAsync

```csharp
public Task<byte[]> ExtendUnitReadBytesAsync(uint headAddress, ushort byteLength, ushort moduleNo, CancellationToken cancellationToken = default)
```

##### ExtendUnitReadWordsAsync

```csharp
public Task<ushort[]> ExtendUnitReadWordsAsync(uint headAddress, ushort wordLength, ushort moduleNo, CancellationToken cancellationToken = default)
```

##### ExtendUnitReadWordAsync

```csharp
public Task<ushort> ExtendUnitReadWordAsync(uint headAddress, ushort moduleNo, CancellationToken cancellationToken = default)
```

##### ExtendUnitReadDWordAsync

```csharp
public Task<uint> ExtendUnitReadDWordAsync(uint headAddress, ushort moduleNo, CancellationToken cancellationToken = default)
```

##### ExtendUnitWriteBytesAsync

```csharp
public Task ExtendUnitWriteBytesAsync(uint headAddress, ushort moduleNo, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
```

##### ExtendUnitWriteWordsAsync

```csharp
public Task ExtendUnitWriteWordsAsync(uint headAddress, ushort moduleNo, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
```

##### ExtendUnitWriteWordAsync

```csharp
public Task ExtendUnitWriteWordAsync(uint headAddress, ushort moduleNo, ushort value, CancellationToken cancellationToken = default)
```

##### ExtendUnitWriteDWordAsync

```csharp
public Task ExtendUnitWriteDWordAsync(uint headAddress, ushort moduleNo, uint value, CancellationToken cancellationToken = default)
```

##### CpuBufferReadWordsAsync

```csharp
public Task<ushort[]> CpuBufferReadWordsAsync(uint headAddress, ushort wordLength, CancellationToken cancellationToken = default)
```

##### CpuBufferReadBytesAsync

```csharp
public Task<byte[]> CpuBufferReadBytesAsync(uint headAddress, ushort byteLength, CancellationToken cancellationToken = default)
```

##### CpuBufferReadWordAsync

```csharp
public Task<ushort> CpuBufferReadWordAsync(uint headAddress, CancellationToken cancellationToken = default)
```

##### CpuBufferReadDWordAsync

```csharp
public Task<uint> CpuBufferReadDWordAsync(uint headAddress, CancellationToken cancellationToken = default)
```

##### CpuBufferWriteWordsAsync

```csharp
public Task CpuBufferWriteWordsAsync(uint headAddress, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
```

##### CpuBufferWriteBytesAsync

```csharp
public Task CpuBufferWriteBytesAsync(uint headAddress, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
```

##### CpuBufferWriteWordAsync

```csharp
public Task CpuBufferWriteWordAsync(uint headAddress, ushort value, CancellationToken cancellationToken = default)
```

##### CpuBufferWriteDWordAsync

```csharp
public Task CpuBufferWriteDWordAsync(uint headAddress, uint value, CancellationToken cancellationToken = default)
```

##### RegisterMonitorDevicesAsync

```csharp
public Task RegisterMonitorDevicesAsync(IReadOnlyList<SlmpDeviceAddress> wordDevices, IReadOnlyList<SlmpDeviceAddress> dwordDevices, CancellationToken cancellationToken = default)
```

##### RegisterMonitorDevicesExtAsync

```csharp
public Task RegisterMonitorDevicesExtAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, SlmpExtensionSpec>> wordDevices, IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, SlmpExtensionSpec>> dwordDevices, CancellationToken cancellationToken = default)
```

##### RunMonitorCycleAsync

```csharp
public Task<SlmpMonitorResult> RunMonitorCycleAsync(int wordPoints, int dwordPoints, CancellationToken cancellationToken = default)
```

##### Dispose

```csharp
public void Dispose()
```

Disposes the client and releases resources.

##### DisposeAsync

```csharp
public ValueTask DisposeAsync()
```

Disposes the client asynchronously.

##### InnerClient

```csharp
public SlmpClient InnerClient { get; }
```

Gets the underlying low-level SLMP client.

Remarks: Advanced callers can use this property for APIs that are not surfaced directly on `QueuedSlmpClient`, while still using `CancellationToken)` to preserve serialized access.

##### FrameType

```csharp
public SlmpFrameType FrameType { get; }
```

Gets the SLMP frame format derived from `PlcProfile`.

##### PlcProfile

```csharp
public SlmpPlcProfile PlcProfile { get; }
```

Gets the canonical PLC profile used by this session.

##### CompatibilityMode

```csharp
public SlmpCompatibilityMode CompatibilityMode { get; }
```

Gets the device access compatibility mode derived from `PlcProfile`.

##### TargetAddress

```csharp
public SlmpTargetAddress TargetAddress { get; set; }
```

Gets or sets the destination routing information.

##### MonitoringTimer

```csharp
public ushort MonitoringTimer { get; set; }
```

Gets or sets the monitoring timer value (multiples of 250ms).

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

Gets or sets the communication timeout.

##### IsOpen

```csharp
public bool IsOpen { get; }
```

Gets a value indicating whether the client is currently connected.

### SlmpAddress

```csharp
public static class SlmpAddress
```

Public helpers for SLMP device address text.

Remarks: These helpers provide a small, documentation-friendly surface for parse, format, and normalization tasks. Use them when you want canonical address text in samples, generated docs, validation tooling, or UI layers.

#### Members

##### Parse

```csharp
public static SlmpDeviceAddress Parse(string text)
```

Parses one SLMP device string.

Returns: The parsed device address.

Parameters:
- `text`: Device text such as `D100`, `X1A`, or `ZR200`.

##### Parse

```csharp
public static SlmpDeviceAddress Parse(string text, SlmpPlcProfile PlcProfile)
```

Parses one SLMP device string using the explicit PLC profile.

##### TryParse

```csharp
public static bool TryParse(string text, out SlmpDeviceAddress address)
```

Attempts to parse one SLMP device string.

Returns: `true` when parsing succeeds; otherwise `false`.

Parameters:
- `text`: Device text to parse.
- `address`: When this method returns `true`, receives the parsed address.

##### TryParse

```csharp
public static bool TryParse(string text, SlmpPlcProfile PlcProfile, out SlmpDeviceAddress address)
```

Attempts to parse one SLMP device string.

Returns: `true` when parsing succeeds; otherwise `false`.

Parameters:
- `text`: Device text to parse.
- `address`: When this method returns `true`, receives the parsed address.

##### Format

```csharp
public static string Format(SlmpDeviceAddress address)
```

Formats one SLMP device address using canonical device text.

Remarks: Hex-addressed device families such as `X`, `Y`, `B`, and `W` are emitted in uppercase hexadecimal form.

Returns: Canonical uppercase address text.

Parameters:
- `address`: The parsed device address to format.

##### Format

```csharp
public static string Format(SlmpDeviceAddress address, SlmpPlcProfile PlcProfile)
```

Formats one parsed device address using the explicit PLC profile.

##### Normalize

```csharp
public static string Normalize(string text)
```

Normalizes one SLMP device string to canonical text.

Returns: The canonical uppercase representation returned by `SlmpDeviceAddress)`.

Parameters:
- `text`: Input device text in any supported spelling.

##### Normalize

```csharp
public static string Normalize(string text, SlmpPlcProfile PlcProfile)
```

Normalizes one SLMP device string using the explicit PLC profile.

### SlmpBlockRead

```csharp
public sealed class SlmpBlockRead
```

Description for a contiguous block of devices to read.

#### Members

##### SlmpBlockRead

```csharp
public SlmpBlockRead(SlmpDeviceAddress Device, ushort Points)
```

Description for a contiguous block of devices to read.

##### Device

```csharp
public SlmpDeviceAddress Device { get; set; }
```

##### Points

```csharp
public ushort Points { get; set; }
```

### SlmpBlockWrite

```csharp
public sealed class SlmpBlockWrite
```

Description for a contiguous block of devices to write.

#### Members

##### SlmpBlockWrite

```csharp
public SlmpBlockWrite(SlmpDeviceAddress Device, IReadOnlyList<ushort> Values)
```

Description for a contiguous block of devices to write.

##### Device

```csharp
public SlmpDeviceAddress Device { get; set; }
```

##### Values

```csharp
public IReadOnlyList<ushort> Values { get; set; }
```

### SlmpBlockWriteOptions

```csharp
public sealed class SlmpBlockWriteOptions
```

Configuration for block write operations.

#### Members

##### SlmpBlockWriteOptions

```csharp
public SlmpBlockWriteOptions(bool SplitMixedBlocks = false)
```

Configuration for block write operations.

Parameters:
- `SplitMixedBlocks`: When true, send separate word-only and bit-only block writes.

##### SplitMixedBlocks

```csharp
public bool SplitMixedBlocks { get; set; }
```

When true, send separate word-only and bit-only block writes.

### SlmpClient

```csharp
public sealed class SlmpClient
```

A high-performance, asynchronous SLMP (MC Protocol) client for .NET. Supports 3E and 4E frame formats over TCP and UDP.

Remarks: This class is not thread-safe. Concurrent calls to `CancellationToken)` will interleave send/receive bytes on the same connection. For concurrent or shared-connection scenarios, wrap this client in a `QueuedSlmpClient`, which serializes all operations with a semaphore. The factory `CancellationToken)` returns a ready-to-use `QueuedSlmpClient` and is the recommended entry point for most use cases.

#### Members

##### SlmpClient

```csharp
public SlmpClient(string host, SlmpPlcProfile plcProfile, int port = 1025, SlmpTransportMode transportMode = Tcp, bool strictProfile = true)
```

Initializes a new instance of the `SlmpClient` class.

Parameters:
- `host`: The IP address or hostname of the PLC.
- `plcProfile`: The PLC profile. This selection derives frame type and compatibility mode.
- `port`: The port number. Defaults to 1025.
- `transportMode`: The transport protocol (TCP or UDP).
- `strictProfile`: When true, high-level APIs reject profile-blocked or unverified features before transport.

##### OpenAsync

```csharp
public Task OpenAsync(CancellationToken cancellationToken = default)
```

Opens the connection to the PLC asynchronously.

Returns: A task representing the asynchronous operation.

Parameters:
- `cancellationToken`: A token to cancel the operation.

##### Open

```csharp
public void Open()
```

Opens the connection to the PLC synchronously.

##### Close

```csharp
public void Close()
```

Closes the connection to the PLC.

##### CloseAsync

```csharp
public Task CloseAsync()
```

Closes the connection to the PLC asynchronously.

##### Dispose

```csharp
public void Dispose()
```

Disposes the client and closes the connection.

##### DisposeAsync

```csharp
public ValueTask DisposeAsync()
```

##### OpenAndConnectAsync

```csharp
public static Task<QueuedSlmpClient> OpenAndConnectAsync(string host, int port, SlmpPlcProfile plcProfile, CancellationToken cancellationToken = default)
```

Opens a connection with explicit stable settings and returns a connected `QueuedSlmpClient`.

Remarks: This is the recommended entry point for application code because it combines one explicit PLC profile with a queued wrapper that is safe to share across multiple tasks.

Returns: A connected queued client ready for high-level helpers such as `ReadTypedAsync`, `ReadNamedAsync`, and `PollAsync`.

Parameters:
- `host`: PLC IP address or hostname.
- `port`: SLMP port number such as 1025 for iQ-R/iQ-F or 5007 for Q/L.
- `plcProfile`: Canonical PLC profile used to derive the standard connection defaults.
- `cancellationToken`: A token to cancel the operation.

##### ReadTypeNameAsync

```csharp
public Task<SlmpTypeNameInfo> ReadTypeNameAsync(CancellationToken cancellationToken = default)
```

Reads the PLC model and type name info asynchronously.

Returns: An object containing model name and code.

Parameters:
- `cancellationToken`: A token to cancel the operation.

##### ReadCpuOperationStateAsync

```csharp
public Task<SlmpCpuOperationState> ReadCpuOperationStateAsync(CancellationToken cancellationToken = default)
```

Reads `SD203` and decodes the CPU operation state from the lower 4 bits.

Returns: The decoded CPU operation state and raw masked code.

Parameters:
- `cancellationToken`: A token to cancel the operation.

##### ReadDeviceRangeCatalogAsync

```csharp
public Task<SlmpDeviceRangeCatalog> ReadDeviceRangeCatalogAsync(CancellationToken cancellationToken = default)
```

Reads the configured profile-specific device upper-bound catalog.

Returns: A catalog containing the configured profile and device upper-bound entries.

Parameters:
- `cancellationToken`: A token to cancel the operation.

##### ReadDeviceRangeCatalogAsync

```csharp
public Task<SlmpDeviceRangeCatalog> ReadDeviceRangeCatalogAsync(SlmpPlcProfile plcProfile, CancellationToken cancellationToken = default)
```

Reads the profile-specific device upper-bound catalog without querying the PLC model name.

Returns: A catalog containing the selected profile and device upper-bound entries.

Parameters:
- `plcProfile`: User-selected PLC profile.
- `cancellationToken`: A token to cancel the operation.

##### ReadWordsRawAsync

```csharp
public Task<ushort[]> ReadWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

Reads word device values asynchronously.

Returns: An array of word values (ushort).

Parameters:
- `device`: The starting device address.
- `points`: Number of words to read.
- `cancellationToken`: A token to cancel the operation.

##### WriteWordsAsync

```csharp
public Task WriteWordsAsync(SlmpDeviceAddress device, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
```

##### ReadBitsAsync

```csharp
public Task<bool[]> ReadBitsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

##### ReadWordsExtendedAsync

```csharp
public Task<ushort[]> ReadWordsExtendedAsync(SlmpQualifiedDeviceAddress device, ushort points, SlmpExtensionSpec extension, CancellationToken cancellationToken = default)
```

##### WriteWordsExtendedAsync

```csharp
public Task WriteWordsExtendedAsync(SlmpQualifiedDeviceAddress device, IReadOnlyList<ushort> values, SlmpExtensionSpec extension, CancellationToken cancellationToken = default)
```

##### ReadBitsExtendedAsync

```csharp
public Task<bool[]> ReadBitsExtendedAsync(SlmpQualifiedDeviceAddress device, ushort points, SlmpExtensionSpec extension, CancellationToken cancellationToken = default)
```

##### WriteBitsExtendedAsync

```csharp
public Task WriteBitsExtendedAsync(SlmpQualifiedDeviceAddress device, IReadOnlyList<bool> values, SlmpExtensionSpec extension, CancellationToken cancellationToken = default)
```

##### WriteBitsAsync

```csharp
public Task WriteBitsAsync(SlmpDeviceAddress device, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
```

##### ReadDWordsRawAsync

```csharp
public Task<uint[]> ReadDWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

##### WriteDWordsAsync

```csharp
public Task WriteDWordsAsync(SlmpDeviceAddress device, IReadOnlyList<uint> values, CancellationToken cancellationToken = default)
```

##### ReadFloat32sAsync

```csharp
public Task<float[]> ReadFloat32sAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

##### WriteFloat32sAsync

```csharp
public Task WriteFloat32sAsync(SlmpDeviceAddress device, IReadOnlyList<float> values, CancellationToken cancellationToken = default)
```

##### ReadRandomAsync

```csharp
public Task<ValueTuple<ushort[], uint[]>> ReadRandomAsync(IReadOnlyList<SlmpDeviceAddress> wordDevices, IReadOnlyList<SlmpDeviceAddress> dwordDevices, CancellationToken cancellationToken = default)
```

##### WriteRandomWordsAsync

```csharp
public Task WriteRandomWordsAsync(IReadOnlyList<ValueTuple<SlmpDeviceAddress, ushort>> wordEntries, IReadOnlyList<ValueTuple<SlmpDeviceAddress, uint>> dwordEntries, CancellationToken cancellationToken = default)
```

##### WriteRandomBitsAsync

```csharp
public Task WriteRandomBitsAsync(IReadOnlyList<ValueTuple<SlmpDeviceAddress, bool>> bitEntries, CancellationToken cancellationToken = default)
```

##### ReadRandomExtAsync

```csharp
public Task<ValueTuple<ushort[], uint[]>> ReadRandomExtAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, SlmpExtensionSpec>> wordDevices, IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, SlmpExtensionSpec>> dwordDevices, CancellationToken cancellationToken = default)
```

##### WriteRandomWordsExtAsync

```csharp
public Task WriteRandomWordsExtAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, ushort, SlmpExtensionSpec>> wordEntries, IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, uint, SlmpExtensionSpec>> dwordEntries, CancellationToken cancellationToken = default)
```

##### WriteRandomBitsExtAsync

```csharp
public Task WriteRandomBitsExtAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, bool, SlmpExtensionSpec>> bitEntries, CancellationToken cancellationToken = default)
```

##### ReadBlockAsync

```csharp
public Task<ValueTuple<ushort[], ushort[]>> ReadBlockAsync(IReadOnlyList<SlmpBlockRead> wordBlocks, IReadOnlyList<SlmpBlockRead> bitBlocks, CancellationToken cancellationToken = default)
```

##### WriteBlockAsync

```csharp
public Task WriteBlockAsync(IReadOnlyList<SlmpBlockWrite> wordBlocks, IReadOnlyList<SlmpBlockWrite> bitBlocks, SlmpBlockWriteOptions options = null, CancellationToken cancellationToken = default)
```

##### RegisterMonitorDevicesAsync

```csharp
public Task RegisterMonitorDevicesAsync(IReadOnlyList<SlmpDeviceAddress> wordDevices, IReadOnlyList<SlmpDeviceAddress> dwordDevices, CancellationToken cancellationToken = default)
```

Registers a set of word and DWord devices for monitoring (command 0x0801). Call `CancellationToken)` to read the registered devices.

Parameters:
- `wordDevices`: Word devices to monitor.
- `dwordDevices`: DWord devices to monitor.
- `cancellationToken`: Cancellation token.

##### RegisterMonitorDevicesExtAsync

```csharp
public Task RegisterMonitorDevicesExtAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, SlmpExtensionSpec>> wordDevices, IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, SlmpExtensionSpec>> dwordDevices, CancellationToken cancellationToken = default)
```

##### RunMonitorCycleAsync

```csharp
public Task<SlmpMonitorResult> RunMonitorCycleAsync(int wordPoints, int dwordPoints, CancellationToken cancellationToken = default)
```

Executes one monitor cycle and returns the values of the previously registered devices (command 0x0802).

Parameters:
- `wordPoints`: Number of registered word devices.
- `dwordPoints`: Number of registered DWord devices.
- `cancellationToken`: Cancellation token.

##### RemoteRunAsync

```csharp
public Task RemoteRunAsync(bool force = false, ushort clearMode = 0, CancellationToken cancellationToken = default)
```

##### RemoteStopAsync

```csharp
public Task RemoteStopAsync(CancellationToken cancellationToken = default)
```

##### RemotePauseAsync

```csharp
public Task RemotePauseAsync(bool force = false, CancellationToken cancellationToken = default)
```

##### RemoteLatchClearAsync

```csharp
public Task RemoteLatchClearAsync(CancellationToken cancellationToken = default)
```

##### RemoteResetAsync

```csharp
public Task RemoteResetAsync(ushort subcommand = 0, bool expectResponse = false, CancellationToken cancellationToken = default)
```

##### RemotePasswordUnlockAsync

```csharp
public Task RemotePasswordUnlockAsync(string password, CancellationToken cancellationToken = default)
```

##### RemotePasswordLockAsync

```csharp
public Task RemotePasswordLockAsync(string password, CancellationToken cancellationToken = default)
```

##### SelfTestLoopbackAsync

```csharp
public Task<byte[]> SelfTestLoopbackAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
```

##### ClearErrorAsync

```csharp
public Task ClearErrorAsync(CancellationToken cancellationToken = default)
```

##### ReadArrayLabelsAsync

```csharp
public Task<SlmpLabelArrayReadResult[]> ReadArrayLabelsAsync(IReadOnlyList<SlmpLabelArrayReadPoint> points, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

Reads array labels from the PLC (command 0x041A).

Parameters:
- `points`: Labels to read, each with unit specification and array data length.
- `abbreviationLabels`: Optional abbreviation label names (sent before regular points).
- `cancellationToken`: Cancellation token.

##### WriteArrayLabelsAsync

```csharp
public Task WriteArrayLabelsAsync(IReadOnlyList<SlmpLabelArrayWritePoint> points, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

Writes array labels to the PLC (command 0x141A).

##### ReadRandomLabelsAsync

```csharp
public Task<SlmpLabelRandomReadResult[]> ReadRandomLabelsAsync(IReadOnlyList<string> labels, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

Reads random labels from the PLC (command 0x041C).

##### WriteRandomLabelsAsync

```csharp
public Task WriteRandomLabelsAsync(IReadOnlyList<SlmpLabelRandomWritePoint> points, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

Writes random labels to the PLC (command 0x141B).

##### MemoryReadWordsAsync

```csharp
public Task<ushort[]> MemoryReadWordsAsync(uint headAddress, ushort wordLength, CancellationToken cancellationToken = default)
```

Reads words from PLC memory (command 0x0613).

Parameters:
- `headAddress`: Starting memory address (32-bit).
- `wordLength`: Number of words to read.
- `cancellationToken`: Cancellation token.

##### MemoryWriteWordsAsync

```csharp
public Task MemoryWriteWordsAsync(uint headAddress, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
```

Writes words to PLC memory (command 0x1613).

Parameters:
- `headAddress`: Starting memory address (32-bit).
- `values`: Word values to write.
- `cancellationToken`: Cancellation token.

##### ExtendUnitReadBytesAsync

```csharp
public Task<byte[]> ExtendUnitReadBytesAsync(uint headAddress, ushort byteLength, ushort moduleNo, CancellationToken cancellationToken = default)
```

Reads raw bytes from an extend unit (command 0x0601).

Parameters:
- `headAddress`: Starting address in the extend unit (32-bit).
- `byteLength`: Number of bytes to read.
- `moduleNo`: Extend unit module I/O number (e.g. 0x03E0 for CPU buffer).
- `cancellationToken`: Cancellation token.

##### ExtendUnitReadWordsAsync

```csharp
public Task<ushort[]> ExtendUnitReadWordsAsync(uint headAddress, ushort wordLength, ushort moduleNo, CancellationToken cancellationToken = default)
```

Reads words from an extend unit (command 0x0601).

Parameters:
- `headAddress`: Starting address in the extend unit (32-bit).
- `wordLength`: Number of words to read.
- `moduleNo`: Extend unit module I/O number.
- `cancellationToken`: Cancellation token.

##### ExtendUnitReadWordAsync

```csharp
public Task<ushort> ExtendUnitReadWordAsync(uint headAddress, ushort moduleNo, CancellationToken cancellationToken = default)
```

Reads a single word from an extend unit.

##### ExtendUnitReadDWordAsync

```csharp
public Task<uint> ExtendUnitReadDWordAsync(uint headAddress, ushort moduleNo, CancellationToken cancellationToken = default)
```

Reads a double word (32-bit) from an extend unit.

##### ExtendUnitWriteBytesAsync

```csharp
public Task ExtendUnitWriteBytesAsync(uint headAddress, ushort moduleNo, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
```

Writes raw bytes to an extend unit (command 0x1601).

Parameters:
- `headAddress`: Starting address in the extend unit (32-bit).
- `moduleNo`: Extend unit module I/O number.
- `data`: Bytes to write.
- `cancellationToken`: Cancellation token.

##### ExtendUnitWriteWordsAsync

```csharp
public Task ExtendUnitWriteWordsAsync(uint headAddress, ushort moduleNo, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
```

Writes words to an extend unit (command 0x1601).

Parameters:
- `headAddress`: Starting address in the extend unit (32-bit).
- `moduleNo`: Extend unit module I/O number.
- `values`: Word values to write.
- `cancellationToken`: Cancellation token.

##### ExtendUnitWriteWordAsync

```csharp
public Task ExtendUnitWriteWordAsync(uint headAddress, ushort moduleNo, ushort value, CancellationToken cancellationToken = default)
```

Writes a single word to an extend unit.

##### ExtendUnitWriteDWordAsync

```csharp
public Task ExtendUnitWriteDWordAsync(uint headAddress, ushort moduleNo, uint value, CancellationToken cancellationToken = default)
```

Writes a double word (32-bit) to an extend unit.

##### CpuBufferReadWordsAsync

```csharp
public Task<ushort[]> CpuBufferReadWordsAsync(uint headAddress, ushort wordLength, CancellationToken cancellationToken = default)
```

Reads words from the CPU buffer (extend unit module 0x03E0).

##### CpuBufferReadBytesAsync

```csharp
public Task<byte[]> CpuBufferReadBytesAsync(uint headAddress, ushort byteLength, CancellationToken cancellationToken = default)
```

Reads bytes from the CPU buffer (extend unit module 0x03E0).

##### CpuBufferReadWordAsync

```csharp
public Task<ushort> CpuBufferReadWordAsync(uint headAddress, CancellationToken cancellationToken = default)
```

Reads a single word from the CPU buffer.

##### CpuBufferReadDWordAsync

```csharp
public Task<uint> CpuBufferReadDWordAsync(uint headAddress, CancellationToken cancellationToken = default)
```

Reads a double word from the CPU buffer.

##### CpuBufferWriteWordsAsync

```csharp
public Task CpuBufferWriteWordsAsync(uint headAddress, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
```

Writes words to the CPU buffer (extend unit module 0x03E0).

##### CpuBufferWriteBytesAsync

```csharp
public Task CpuBufferWriteBytesAsync(uint headAddress, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
```

Writes bytes to the CPU buffer (extend unit module 0x03E0).

##### CpuBufferWriteWordAsync

```csharp
public Task CpuBufferWriteWordAsync(uint headAddress, ushort value, CancellationToken cancellationToken = default)
```

Writes a single word to the CPU buffer.

##### CpuBufferWriteDWordAsync

```csharp
public Task CpuBufferWriteDWordAsync(uint headAddress, uint value, CancellationToken cancellationToken = default)
```

Writes a double word to the CPU buffer.

##### ReadLongTimerAsync

```csharp
public Task<SlmpLongTimerResult[]> ReadLongTimerAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

Reads one or more long timers starting at the given device number. Each timer occupies 4 consecutive words: [current_lo, current_hi, status, reserved].

Parameters:
- `headNo`: Starting LTN device number (e.g. 0 for LTN0).
- `points`: Number of timers to read.
- `cancellationToken`: Cancellation token.

##### ReadLongRetentiveTimerAsync

```csharp
public Task<SlmpLongTimerResult[]> ReadLongRetentiveTimerAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

Reads one or more long retentive timers starting at the given device number. Each timer occupies 4 consecutive words: [current_lo, current_hi, status, reserved].

Parameters:
- `headNo`: Starting LSTN device number (e.g. 0 for LSTN0).
- `points`: Number of timers to read.
- `cancellationToken`: Cancellation token.

##### ReadLtcStatesAsync

```csharp
public Task<bool[]> ReadLtcStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

Returns the coil state of each long timer in the range.

##### ReadLtsStatesAsync

```csharp
public Task<bool[]> ReadLtsStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

Returns the contact state of each long timer in the range.

##### ReadLstcStatesAsync

```csharp
public Task<bool[]> ReadLstcStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

Returns the coil state of each long retentive timer in the range.

##### ReadLstsStatesAsync

```csharp
public Task<bool[]> ReadLstsStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
```

Returns the contact state of each long retentive timer in the range.

##### RequestAsync

```csharp
public Task<byte[]> RequestAsync(SlmpCommand command, ushort subcommand, ReadOnlyMemory<byte> payload, bool expectResponse = true, CancellationToken cancellationToken = default)
```

##### FrameType

```csharp
public SlmpFrameType FrameType { get; }
```

Gets the SLMP frame format derived from `PlcProfile`.

##### CompatibilityMode

```csharp
public SlmpCompatibilityMode CompatibilityMode { get; }
```

Gets the device access compatibility mode derived from `PlcProfile`.

##### PlcProfile

```csharp
public SlmpPlcProfile PlcProfile { get; }
```

Gets the PLC profile used to derive frame, compatibility, payload, and address behavior.

##### StrictProfile

```csharp
public bool StrictProfile { get; }
```

Gets whether high-level APIs reject profile-blocked or unverified features before transport.

##### TargetAddress

```csharp
public SlmpTargetAddress TargetAddress { get; set; }
```

Gets or sets the destination routing information.

##### MonitoringTimer

```csharp
public ushort MonitoringTimer { get; set; }
```

Gets or sets the monitoring timer value (multiples of 250ms). Default is 0x0010 (4s).

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

Gets or sets the communication timeout.

##### LastRequestFrame

```csharp
public byte[] LastRequestFrame { get; }
```

Gets the raw binary content of the last sent request frame.

##### LastResponseFrame

```csharp
public byte[] LastResponseFrame { get; }
```

Gets the raw binary content of the last received response frame.

##### TraceHook

```csharp
public Action<SlmpTraceFrame> TraceHook { get; set; }
```

Optional hook called for every raw frame sent and received. Useful for protocol tracing and debugging.

##### IsOpen

```csharp
public bool IsOpen { get; }
```

Gets a value indicating whether the client is currently connected.

### SlmpClientExtensions

```csharp
public static class SlmpClientExtensions
```

Extension methods for `SlmpClient` and `QueuedSlmpClient` providing typed read/write helpers, chunked reads, named-device access, and polling.

#### Members

##### ReadTypedAsync

```csharp
public static Task<object> ReadTypedAsync(SlmpClient client, SlmpDeviceAddress device, string dtype, CancellationToken ct = default)
```

Reads one logical value and converts it to the requested application type.

Remarks: This is the main single-value read helper for user code. Prefer it over raw word access when the PLC data should be treated as a typed scalar.

Returns: A boxed `UInt16`, `Int16`, `UInt32`, `Int32`, or `Single`.

Parameters:
- `client`: Connected SLMP client.
- `device`: Starting device address.
- `dtype`: Type code: `U` unsigned 16-bit, `S` signed 16-bit, `D` unsigned 32-bit, `L` signed 32-bit, or `F` float32.
- `ct`: Cancellation token.

##### ReadTypedAsync

```csharp
public static Task<object> ReadTypedAsync(SlmpClient client, string device, string dtype, CancellationToken ct = default)
```

Reads one device value using a string address.

Returns: A boxed scalar matching the requested type.

Parameters:
- `client`: Connected SLMP client.
- `device`: Device string such as `D100` or `M1000`.
- `dtype`: Requested application type such as `U`, `F`, or `BIT`.
- `ct`: Cancellation token.

##### ReadTypedAsync

```csharp
public static Task<object> ReadTypedAsync(QueuedSlmpClient client, SlmpDeviceAddress device, string dtype, CancellationToken ct = default)
```

Reads one device value and converts it to the specified type through a queued client.

Returns: A boxed scalar matching the requested type.

Parameters:
- `client`: Queued SLMP client safe for shared use.
- `device`: Starting device address.
- `dtype`: Requested application type.
- `ct`: Cancellation token.

##### ReadTypedAsync

```csharp
public static Task<object> ReadTypedAsync(QueuedSlmpClient client, string device, string dtype, CancellationToken ct = default)
```

Reads one device value using a string address through a queued client.

Returns: A boxed scalar matching the requested type.

Parameters:
- `client`: Queued SLMP client safe for shared use.
- `device`: Device string such as `D100` or `M1000`.
- `dtype`: Requested application type.
- `ct`: Cancellation token.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync(SlmpClient client, SlmpDeviceAddress device, string dtype, object value, CancellationToken ct = default)
```

Writes one logical value using the requested type conversion.

Remarks: Use this helper when application code wants to write typed values without manually splitting words or packing float32 values.

Parameters:
- `client`: Connected SLMP client.
- `device`: Starting device address.
- `dtype`: Type code: `U` unsigned 16-bit, `S` signed 16-bit, `D` unsigned 32-bit, `L` signed 32-bit, or `F` float32.
- `value`: Value to encode and write.
- `ct`: Cancellation token.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync(SlmpClient client, string device, string dtype, object value, CancellationToken ct = default)
```

Writes one device value using a string address.

Parameters:
- `client`: Connected SLMP client.
- `device`: Device string such as `D100`, `D200:F`, or `M1000`.
- `dtype`: Requested application type.
- `value`: Application value to encode and write.
- `ct`: Cancellation token.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync(QueuedSlmpClient client, SlmpDeviceAddress device, string dtype, object value, CancellationToken ct = default)
```

Writes one device value through a queued client.

Parameters:
- `client`: Queued SLMP client safe for shared use.
- `device`: Starting device address.
- `dtype`: Requested application type.
- `value`: Application value to encode and write.
- `ct`: Cancellation token.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync(QueuedSlmpClient client, string device, string dtype, object value, CancellationToken ct = default)
```

Writes one device value using a string address through a queued client.

Parameters:
- `client`: Queued SLMP client safe for shared use.
- `device`: Device string such as `D100`, `D200:F`, or `M1000`.
- `dtype`: Requested application type.
- `value`: Application value to encode and write.
- `ct`: Cancellation token.

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(SlmpClient client, SlmpDeviceAddress device, int bitIndex, bool value, CancellationToken ct = default)
```

Performs a read-modify-write to set or clear one bit inside a word device.

Remarks: This helper is useful when a PLC stores request and status flags inside one control word and only one flag should change.

Parameters:
- `client`: Connected SLMP client.
- `device`: Word device address such as `D50`.
- `bitIndex`: Bit position within the word, from 0 to 15.
- `value`: New bit state.
- `ct`: Cancellation token.

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(SlmpClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
```

Performs a read-modify-write using a string address.

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(QueuedSlmpClient client, SlmpDeviceAddress device, int bitIndex, bool value, CancellationToken ct = default)
```

Performs a read-modify-write through a queued client.

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(QueuedSlmpClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
```

Performs a read-modify-write using a string address through a queued client.

##### ReadBitsBlockAsync

```csharp
public static Task<bool[]> ReadBitsBlockAsync(SlmpClient client, SlmpDeviceAddress start, ushort count, CancellationToken ct = default)
```

Reads a contiguous bit-device range and returns boolean values.

Returns: Boolean values in PLC order.

Parameters:
- `client`: Connected SLMP client.
- `start`: First bit device in the range.
- `count`: Number of points to read.
- `ct`: Cancellation token.

##### ReadBitsBlockAsync

```csharp
public static Task<bool[]> ReadBitsBlockAsync(SlmpClient client, string start, ushort count, CancellationToken ct = default)
```

Reads a contiguous bit-device range using a string address.

##### ReadBitsBlockAsync

```csharp
public static Task<bool[]> ReadBitsBlockAsync(QueuedSlmpClient client, SlmpDeviceAddress start, ushort count, CancellationToken ct = default)
```

Reads a contiguous bit-device range through a queued client.

##### ReadBitsBlockAsync

```csharp
public static Task<bool[]> ReadBitsBlockAsync(QueuedSlmpClient client, string start, ushort count, CancellationToken ct = default)
```

Reads a contiguous bit-device range using a string address through a queued client.

##### WriteBitsBlockAsync

```csharp
public static Task WriteBitsBlockAsync(SlmpClient client, SlmpDeviceAddress start, IReadOnlyList<bool> values, CancellationToken ct = default)
```

Writes a contiguous bit-device range from boolean values.

Parameters:
- `client`: Connected SLMP client.
- `start`: First bit device in the range.
- `values`: Boolean values in PLC order.
- `ct`: Cancellation token.

##### WriteBitsBlockAsync

```csharp
public static Task WriteBitsBlockAsync(SlmpClient client, string start, IReadOnlyList<bool> values, CancellationToken ct = default)
```

Writes a contiguous bit-device range using a string address.

##### WriteBitsBlockAsync

```csharp
public static Task WriteBitsBlockAsync(QueuedSlmpClient client, SlmpDeviceAddress start, IReadOnlyList<bool> values, CancellationToken ct = default)
```

Writes a contiguous bit-device range through a queued client.

##### WriteBitsBlockAsync

```csharp
public static Task WriteBitsBlockAsync(QueuedSlmpClient client, string start, IReadOnlyList<bool> values, CancellationToken ct = default)
```

Writes a contiguous bit-device range using a string address through a queued client.

##### WriteWordsBlockAsync

```csharp
public static Task WriteWordsBlockAsync(SlmpClient client, SlmpDeviceAddress start, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes a contiguous word-device range from 16-bit values.

Parameters:
- `client`: Connected SLMP client.
- `start`: First word device in the range.
- `values`: Word values in PLC order.
- `ct`: Cancellation token.

##### WriteWordsBlockAsync

```csharp
public static Task WriteWordsBlockAsync(SlmpClient client, string start, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes a contiguous word-device range using a string address.

##### WriteWordsBlockAsync

```csharp
public static Task WriteWordsBlockAsync(QueuedSlmpClient client, SlmpDeviceAddress start, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes a contiguous word-device range through a queued client.

##### WriteWordsBlockAsync

```csharp
public static Task WriteWordsBlockAsync(QueuedSlmpClient client, string start, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes a contiguous word-device range using a string address through a queued client.

##### WriteDWordsBlockAsync

```csharp
public static Task WriteDWordsBlockAsync(SlmpClient client, SlmpDeviceAddress start, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes a contiguous DWord-device range from 32-bit values.

##### WriteDWordsBlockAsync

```csharp
public static Task WriteDWordsBlockAsync(SlmpClient client, string start, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes a contiguous DWord-device range using a string address.

##### WriteDWordsBlockAsync

```csharp
public static Task WriteDWordsBlockAsync(QueuedSlmpClient client, SlmpDeviceAddress start, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes a contiguous DWord-device range through a queued client.

##### WriteDWordsBlockAsync

```csharp
public static Task WriteDWordsBlockAsync(QueuedSlmpClient client, string start, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes a contiguous DWord-device range using a string address through a queued client.

##### ReadWordsSingleRequestAsync

```csharp
public static Task<ushort[]> ReadWordsSingleRequestAsync(SlmpClient client, SlmpDeviceAddress start, int count, CancellationToken ct = default)
```

Reads contiguous word devices using one SLMP request or returns an error.

##### ReadWordsSingleRequestAsync

```csharp
public static Task<ushort[]> ReadWordsSingleRequestAsync(SlmpClient client, string start, int count, CancellationToken ct = default)
```

Reads contiguous word devices using one SLMP request or returns an error.

##### ReadWordsSingleRequestAsync

```csharp
public static Task<ushort[]> ReadWordsSingleRequestAsync(QueuedSlmpClient client, SlmpDeviceAddress start, int count, CancellationToken ct = default)
```

Reads contiguous word devices using one SLMP request or returns an error through a queued client.

##### ReadWordsSingleRequestAsync

```csharp
public static Task<ushort[]> ReadWordsSingleRequestAsync(QueuedSlmpClient client, string start, int count, CancellationToken ct = default)
```

Reads contiguous word devices using one SLMP request or returns an error through a queued client.

##### ReadDWordsSingleRequestAsync

```csharp
public static Task<uint[]> ReadDWordsSingleRequestAsync(SlmpClient client, SlmpDeviceAddress start, int count, CancellationToken ct = default)
```

Reads contiguous DWord devices using one SLMP request or returns an error.

##### ReadDWordsSingleRequestAsync

```csharp
public static Task<uint[]> ReadDWordsSingleRequestAsync(SlmpClient client, string start, int count, CancellationToken ct = default)
```

Reads contiguous DWord devices using one SLMP request or returns an error.

##### ReadDWordsSingleRequestAsync

```csharp
public static Task<uint[]> ReadDWordsSingleRequestAsync(QueuedSlmpClient client, SlmpDeviceAddress start, int count, CancellationToken ct = default)
```

Reads contiguous DWord devices using one SLMP request or returns an error through a queued client.

##### ReadDWordsSingleRequestAsync

```csharp
public static Task<uint[]> ReadDWordsSingleRequestAsync(QueuedSlmpClient client, string start, int count, CancellationToken ct = default)
```

Reads contiguous DWord devices using one SLMP request or returns an error through a queued client.

##### WriteWordsSingleRequestAsync

```csharp
public static Task WriteWordsSingleRequestAsync(SlmpClient client, SlmpDeviceAddress start, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes contiguous word devices using one SLMP request or returns an error.

##### WriteWordsSingleRequestAsync

```csharp
public static Task WriteWordsSingleRequestAsync(SlmpClient client, string start, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes contiguous word devices using one SLMP request or returns an error.

##### WriteWordsSingleRequestAsync

```csharp
public static Task WriteWordsSingleRequestAsync(QueuedSlmpClient client, SlmpDeviceAddress start, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes contiguous word devices using one SLMP request or returns an error through a queued client.

##### WriteWordsSingleRequestAsync

```csharp
public static Task WriteWordsSingleRequestAsync(QueuedSlmpClient client, string start, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes contiguous word devices using one SLMP request or returns an error through a queued client.

##### WriteDWordsSingleRequestAsync

```csharp
public static Task WriteDWordsSingleRequestAsync(SlmpClient client, SlmpDeviceAddress start, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes contiguous DWord devices using one SLMP request or returns an error.

##### WriteDWordsSingleRequestAsync

```csharp
public static Task WriteDWordsSingleRequestAsync(SlmpClient client, string start, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes contiguous DWord devices using one SLMP request or returns an error.

##### WriteDWordsSingleRequestAsync

```csharp
public static Task WriteDWordsSingleRequestAsync(QueuedSlmpClient client, SlmpDeviceAddress start, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes contiguous DWord devices using one SLMP request or returns an error through a queued client.

##### WriteDWordsSingleRequestAsync

```csharp
public static Task WriteDWordsSingleRequestAsync(QueuedSlmpClient client, string start, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes contiguous DWord devices using one SLMP request or returns an error through a queued client.

##### ReadWordsAsync

```csharp
public static Task<ushort[]> ReadWordsAsync(SlmpClient client, SlmpDeviceAddress start, int count, int maxPerRequest = 960, bool allowSplit = false, CancellationToken ct = default)
```

Reads a contiguous word range in one or more SLMP requests.

Remarks: Chunk boundaries are aligned to 2-word boundaries so that 32-bit values are not torn across split requests.

Returns: Flat array of word values.

Parameters:
- `client`: Connected SLMP client.
- `start`: Starting word device address.
- `count`: Total number of words to read.
- `maxPerRequest`: Maximum words per request. The protocol limit is 960.
- `allowSplit`: When true, large reads are automatically split across multiple SLMP requests.
- `ct`: Cancellation token.

##### ReadWordsAsync

```csharp
public static Task<ushort[]> ReadWordsAsync(SlmpClient client, string start, int count, int maxPerRequest = 960, bool allowSplit = false, CancellationToken ct = default)
```

Reads word devices using a string address.

Returns: Flat array of word values.

Parameters:
- `client`: Connected SLMP client.
- `start`: Word device string such as `D0`.
- `count`: Total number of words to read.
- `maxPerRequest`: Maximum words per request.
- `allowSplit`: When true, oversized reads are split across requests.
- `ct`: Cancellation token.

##### ReadWordsAsync

```csharp
public static Task<ushort[]> ReadWordsAsync(QueuedSlmpClient client, SlmpDeviceAddress start, int count, int maxPerRequest = 960, bool allowSplit = false, CancellationToken ct = default)
```

Reads word devices through a queued client.

##### ReadWordsAsync

```csharp
public static Task<ushort[]> ReadWordsAsync(QueuedSlmpClient client, string start, int count, int maxPerRequest = 960, bool allowSplit = false, CancellationToken ct = default)
```

Reads word devices using a string address through a queued client.

##### ReadDWordsAsync

```csharp
public static Task<uint[]> ReadDWordsAsync(SlmpClient client, SlmpDeviceAddress start, int count, int maxDwordsPerRequest = 480, bool allowSplit = false, CancellationToken ct = default)
```

Reads a contiguous range of 32-bit unsigned values.

Remarks: Each result consumes two underlying words in low-word-first order.

Returns: Array of 32-bit unsigned values.

Parameters:
- `client`: Connected SLMP client.
- `start`: Starting device address.
- `count`: Number of 32-bit values to read.
- `maxDwordsPerRequest`: Maximum DWords per request.
- `allowSplit`: When true, large reads are automatically split across multiple SLMP requests.
- `ct`: Cancellation token.

##### ReadDWordsAsync

```csharp
public static Task<uint[]> ReadDWordsAsync(SlmpClient client, string start, int count, int maxDwordsPerRequest = 480, bool allowSplit = false, CancellationToken ct = default)
```

Reads DWord devices using a string address.

Returns: Array of 32-bit unsigned values.

Parameters:
- `client`: Connected SLMP client.
- `start`: Starting word device string such as `D200`.
- `count`: Number of 32-bit values to read.
- `maxDwordsPerRequest`: Maximum DWords per request.
- `allowSplit`: When true, oversized reads are split across requests.
- `ct`: Cancellation token.

##### ReadDWordsAsync

```csharp
public static Task<uint[]> ReadDWordsAsync(QueuedSlmpClient client, SlmpDeviceAddress start, int count, int maxDwordsPerRequest = 480, bool allowSplit = false, CancellationToken ct = default)
```

Reads DWord devices through a queued client.

##### ReadDWordsAsync

```csharp
public static Task<uint[]> ReadDWordsAsync(QueuedSlmpClient client, string start, int count, int maxDwordsPerRequest = 480, bool allowSplit = false, CancellationToken ct = default)
```

Reads DWord devices using a string address through a queued client.

##### ReadWordsChunkedAsync

```csharp
public static Task<ushort[]> ReadWordsChunkedAsync(SlmpClient client, SlmpDeviceAddress start, int count, int maxWordsPerRequest, CancellationToken ct = default)
```

Reads contiguous word devices using explicit chunking.

##### ReadWordsChunkedAsync

```csharp
public static Task<ushort[]> ReadWordsChunkedAsync(SlmpClient client, string start, int count, int maxWordsPerRequest, CancellationToken ct = default)
```

Reads contiguous word devices using explicit chunking.

##### ReadWordsChunkedAsync

```csharp
public static Task<ushort[]> ReadWordsChunkedAsync(QueuedSlmpClient client, SlmpDeviceAddress start, int count, int maxWordsPerRequest, CancellationToken ct = default)
```

Reads contiguous word devices using explicit chunking through a queued client.

##### ReadWordsChunkedAsync

```csharp
public static Task<ushort[]> ReadWordsChunkedAsync(QueuedSlmpClient client, string start, int count, int maxWordsPerRequest, CancellationToken ct = default)
```

Reads contiguous word devices using explicit chunking through a queued client.

##### ReadDWordsChunkedAsync

```csharp
public static Task<uint[]> ReadDWordsChunkedAsync(SlmpClient client, SlmpDeviceAddress start, int count, int maxDwordsPerRequest, CancellationToken ct = default)
```

Reads contiguous DWord devices using explicit chunking.

##### ReadDWordsChunkedAsync

```csharp
public static Task<uint[]> ReadDWordsChunkedAsync(SlmpClient client, string start, int count, int maxDwordsPerRequest, CancellationToken ct = default)
```

Reads contiguous DWord devices using explicit chunking.

##### ReadDWordsChunkedAsync

```csharp
public static Task<uint[]> ReadDWordsChunkedAsync(QueuedSlmpClient client, SlmpDeviceAddress start, int count, int maxDwordsPerRequest, CancellationToken ct = default)
```

Reads contiguous DWord devices using explicit chunking through a queued client.

##### ReadDWordsChunkedAsync

```csharp
public static Task<uint[]> ReadDWordsChunkedAsync(QueuedSlmpClient client, string start, int count, int maxDwordsPerRequest, CancellationToken ct = default)
```

Reads contiguous DWord devices using explicit chunking through a queued client.

##### WriteWordsChunkedAsync

```csharp
public static Task WriteWordsChunkedAsync(SlmpClient client, SlmpDeviceAddress start, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct = default)
```

Writes contiguous word devices using explicit chunking.

##### WriteWordsChunkedAsync

```csharp
public static Task WriteWordsChunkedAsync(SlmpClient client, string start, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct = default)
```

Writes contiguous word devices using explicit chunking.

##### WriteWordsChunkedAsync

```csharp
public static Task WriteWordsChunkedAsync(QueuedSlmpClient client, SlmpDeviceAddress start, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct = default)
```

Writes contiguous word devices using explicit chunking through a queued client.

##### WriteWordsChunkedAsync

```csharp
public static Task WriteWordsChunkedAsync(QueuedSlmpClient client, string start, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct = default)
```

Writes contiguous word devices using explicit chunking through a queued client.

##### WriteDWordsChunkedAsync

```csharp
public static Task WriteDWordsChunkedAsync(SlmpClient client, SlmpDeviceAddress start, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct = default)
```

Writes contiguous DWord devices using explicit chunking.

##### WriteDWordsChunkedAsync

```csharp
public static Task WriteDWordsChunkedAsync(SlmpClient client, string start, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct = default)
```

Writes contiguous DWord devices using explicit chunking.

##### WriteDWordsChunkedAsync

```csharp
public static Task WriteDWordsChunkedAsync(QueuedSlmpClient client, SlmpDeviceAddress start, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct = default)
```

Writes contiguous DWord devices using explicit chunking through a queued client.

##### WriteDWordsChunkedAsync

```csharp
public static Task WriteDWordsChunkedAsync(QueuedSlmpClient client, string start, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct = default)
```

Writes contiguous DWord devices using explicit chunking through a queued client.

##### ReadNamedAsync

```csharp
public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(SlmpClient client, IEnumerable<string> addresses, CancellationToken ct = default)
```

Reads a mixed logical snapshot by address string and returns a dictionary keyed by the original addresses.

Remarks: This is the recommended high-level helper for dashboards, snapshots, and mixed-value reads. The address list is compiled and batched internally.

Returns: A dictionary whose keys match the requested address strings.

Parameters:
- `client`: Connected SLMP client.
- `addresses`: Address list such as `D100:U`, `D200:F`, `D300:L`, `M1000:BIT`, or `D50.3`.
- `ct`: Cancellation token.

##### ReadNamedAsync

```csharp
public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(QueuedSlmpClient client, IEnumerable<string> addresses, CancellationToken ct = default)
```

Reads multiple devices by address string through a queued client.

##### WriteNamedAsync

```csharp
public static Task WriteNamedAsync(SlmpClient client, IReadOnlyDictionary<string, object> updates, CancellationToken ct = default)
```

Writes a mixed logical snapshot by address string.

Parameters:
- `client`: Connected SLMP client.
- `updates`: Mapping of address string to value, for example `"D100:U"`, `"D200:F"`, `"D50.3"`, or direct bit-device addresses such as `"M1000:BIT"`.
- `ct`: Cancellation token.

##### WriteNamedAsync

```csharp
public static Task WriteNamedAsync(QueuedSlmpClient client, IReadOnlyDictionary<string, object> updates, CancellationToken ct = default)
```

Writes multiple named values through a queued client.

Parameters:
- `client`: Queued SLMP client safe for shared use.
- `updates`: Address-to-value map in the same format as `CancellationToken)`.
- `ct`: Cancellation token.

##### PollAsync

```csharp
public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(SlmpClient client, IEnumerable<string> addresses, TimeSpan interval, CancellationToken ct = default)
```

Continuously polls the specified logical snapshot at the requested interval.

Remarks: The address list is compiled once and reused for every cycle, making this helper suitable for periodic monitoring and historian ingestion.

Returns: An async stream of snapshot dictionaries.

Parameters:
- `client`: Connected SLMP client.
- `addresses`: Address list in the same format as `CancellationToken)`.
- `interval`: Delay between snapshots.
- `ct`: Cancellation token.

##### PollAsync

```csharp
public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(QueuedSlmpClient client, IEnumerable<string> addresses, TimeSpan interval, CancellationToken ct = default)
```

Continuously polls the specified devices at the given interval through a queued client.

Returns: An async stream of snapshot dictionaries.

Parameters:
- `client`: Queued SLMP client safe for shared use.
- `addresses`: Address list in the same format as `CancellationToken)`.
- `interval`: Delay between snapshots.
- `ct`: Cancellation token.

### SlmpClientFactory

```csharp
public static class SlmpClientFactory
```

Factory helpers for creating connected queued SLMP clients.

Remarks: This factory is the preferred high-level entry point for applications that want an already-connected client with explicit session settings captured by `SlmpConnectionOptions`.

#### Members

##### OpenAndConnectAsync

```csharp
public static Task<QueuedSlmpClient> OpenAndConnectAsync(SlmpConnectionOptions options, CancellationToken cancellationToken = default)
```

Creates, configures, and opens a queued SLMP client.

Remarks: The returned `QueuedSlmpClient` serializes multi-step operations through a single gate, which makes it suitable for documentation samples and shared-session application code.

Returns: A connected queued client.

Parameters:
- `options`: Explicit connection options.
- `cancellationToken`: Cancellation token.

### SlmpCommand

```csharp
public enum SlmpCommand
```

Standard SLMP command codes.

#### Members

##### DeviceRead

```csharp
public const SlmpCommand DeviceRead
```

##### DeviceWrite

```csharp
public const SlmpCommand DeviceWrite
```

##### DeviceReadRandom

```csharp
public const SlmpCommand DeviceReadRandom
```

##### DeviceWriteRandom

```csharp
public const SlmpCommand DeviceWriteRandom
```

##### DeviceReadBlock

```csharp
public const SlmpCommand DeviceReadBlock
```

##### DeviceWriteBlock

```csharp
public const SlmpCommand DeviceWriteBlock
```

##### MonitorRegister

```csharp
public const SlmpCommand MonitorRegister
```

##### Monitor

```csharp
public const SlmpCommand Monitor
```

##### ReadTypeName

```csharp
public const SlmpCommand ReadTypeName
```

##### LabelArrayRead

```csharp
public const SlmpCommand LabelArrayRead
```

##### LabelArrayWrite

```csharp
public const SlmpCommand LabelArrayWrite
```

##### LabelReadRandom

```csharp
public const SlmpCommand LabelReadRandom
```

##### LabelWriteRandom

```csharp
public const SlmpCommand LabelWriteRandom
```

##### MemoryRead

```csharp
public const SlmpCommand MemoryRead
```

##### MemoryWrite

```csharp
public const SlmpCommand MemoryWrite
```

##### ExtendUnitRead

```csharp
public const SlmpCommand ExtendUnitRead
```

##### ExtendUnitWrite

```csharp
public const SlmpCommand ExtendUnitWrite
```

##### RemoteRun

```csharp
public const SlmpCommand RemoteRun
```

##### RemoteStop

```csharp
public const SlmpCommand RemoteStop
```

##### RemotePause

```csharp
public const SlmpCommand RemotePause
```

##### RemoteLatchClear

```csharp
public const SlmpCommand RemoteLatchClear
```

##### RemoteReset

```csharp
public const SlmpCommand RemoteReset
```

##### RemotePasswordUnlock

```csharp
public const SlmpCommand RemotePasswordUnlock
```

##### RemotePasswordLock

```csharp
public const SlmpCommand RemotePasswordLock
```

##### SelfTest

```csharp
public const SlmpCommand SelfTest
```

##### ClearError

```csharp
public const SlmpCommand ClearError
```

### SlmpCompatibilityMode

```csharp
public enum SlmpCompatibilityMode
```

Specifies the device access subcommand compatibility mode.

#### Members

##### Legacy

```csharp
public const SlmpCompatibilityMode Legacy
```

Legacy Q/L series subcommands (0x0000/0x0001).

##### Iqr

```csharp
public const SlmpCompatibilityMode Iqr
```

Modern iQ-R series subcommands (0x0002/0x0003).

### SlmpConnectionOptions

```csharp
public sealed class SlmpConnectionOptions
```

Explicit connection options for a stable SLMP session profile.

Remarks: Use `PlcProfile` for the recommended high-level API. The library derives frame type, compatibility mode, string-address handling, and device-range handling from that explicit profile. This type is intended for the unified high-level entry point exposed by `CancellationToken)`.

#### Members

##### SlmpConnectionOptions

```csharp
public SlmpConnectionOptions(string Host, SlmpPlcProfile PlcProfile)
```

Explicit connection options for a stable SLMP session profile.

Remarks: Use `PlcProfile` for the recommended high-level API. The library derives frame type, compatibility mode, string-address handling, and device-range handling from that explicit profile. This type is intended for the unified high-level entry point exposed by `CancellationToken)`.

Parameters:
- `Host`: PLC IP address or hostname.
- `PlcProfile`: Canonical PLC profile for the high-level API.

##### Host

```csharp
public string Host { get; set; }
```

PLC IP address or hostname.

##### PlcProfile

```csharp
public SlmpPlcProfile PlcProfile { get; set; }
```

Gets or sets the canonical PLC profile for the high-level API.

##### Port

```csharp
public int Port { get; set; }
```

Gets or sets the SLMP port number.

Remarks: The default SLMP TCP/UDP port is `1025`.

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

Gets or sets the communication timeout for the underlying transport.

Remarks: This timeout applies to individual request/response exchanges after the session is opened.

##### Transport

```csharp
public SlmpTransportMode Transport { get; set; }
```

Gets or sets the transport protocol used for the session.

Remarks: SLMP typically uses TCP for stable sessions and UDP for lightweight request patterns.

##### Target

```csharp
public SlmpTargetAddress Target { get; set; }
```

Gets or sets the destination route.

Remarks: The default value targets the directly connected local CPU. Override this when routing through a specific network, station, or module path.

##### MonitoringTimer

```csharp
public ushort MonitoringTimer { get; set; }
```

Gets or sets the SLMP monitoring timer value in 250 ms units.

Remarks: The monitoring timer is encoded into the request frame and tells the PLC how long it may spend processing the request before reporting a timeout.

##### StrictProfile

```csharp
public bool StrictProfile { get; set; }
```

Gets or sets whether high-level APIs reject profile-blocked or unverified features before transport.

Remarks: The default is `true`. Limits and write policies are always enforced. Set this to `false` only when intentionally probing a PLC feature.

##### ResolvedFrameType

```csharp
public SlmpFrameType ResolvedFrameType { get; }
```

Gets the effective frame type after applying `PlcProfile` defaults.

##### ResolvedCompatibilityMode

```csharp
public SlmpCompatibilityMode ResolvedCompatibilityMode { get; }
```

Gets the effective compatibility mode after applying `PlcProfile` defaults.

##### ResolvedAddressProfile

```csharp
public SlmpPlcProfile ResolvedAddressProfile { get; }
```

Gets the profile used for string device parsing.

##### ResolvedRangeProfile

```csharp
public SlmpPlcProfile ResolvedRangeProfile { get; }
```

Gets the profile used by the high-level device-range helper layer.

### SlmpCpuOperationState

```csharp
public sealed class SlmpCpuOperationState
```

Decoded CPU operation state read from `SD203`.

#### Members

##### SlmpCpuOperationState

```csharp
public SlmpCpuOperationState(SlmpCpuOperationStatus Status, ushort RawStatusWord, byte RawCode)
```

Decoded CPU operation state read from `SD203`.

Parameters:
- `Status`: Decoded PLC operation state.
- `RawStatusWord`: Full raw word read from `SD203`.
- `RawCode`: Lower 4-bit masked status code from `SD203`.

##### Status

```csharp
public SlmpCpuOperationStatus Status { get; set; }
```

Decoded PLC operation state.

##### RawStatusWord

```csharp
public ushort RawStatusWord { get; set; }
```

Full raw word read from `SD203`.

##### RawCode

```csharp
public byte RawCode { get; set; }
```

Lower 4-bit masked status code from `SD203`.

### SlmpCpuOperationStatus

```csharp
public enum SlmpCpuOperationStatus
```

Decoded CPU operation state from the lower 4 bits of `SD203`.

#### Members

##### Unknown

```csharp
public const SlmpCpuOperationStatus Unknown
```

##### Run

```csharp
public const SlmpCpuOperationStatus Run
```

##### Stop

```csharp
public const SlmpCpuOperationStatus Stop
```

##### Pause

```csharp
public const SlmpCpuOperationStatus Pause
```

### SlmpDeviceAddress

```csharp
public struct SlmpDeviceAddress
```

Represents a specific PLC device and its numeric address.

#### Members

##### SlmpDeviceAddress

```csharp
public SlmpDeviceAddress(SlmpDeviceCode Code, uint Number)
```

Represents a specific PLC device and its numeric address.

Parameters:
- `Code`: The device type code (e.g., D, M, X, Y).
- `Number`: The numeric address of the device.

##### ToString

```csharp
public virtual string ToString()
```

Returns the string representation of the device address (e.g., "D100").

##### Code

```csharp
public SlmpDeviceCode Code { get; set; }
```

The device type code (e.g., D, M, X, Y).

##### Number

```csharp
public uint Number { get; set; }
```

The numeric address of the device.

### SlmpDeviceCode

```csharp
public enum SlmpDeviceCode
```

Standard SLMP binary device codes.

#### Members

##### SM

```csharp
public const SlmpDeviceCode SM
```

Special Relay

##### SD

```csharp
public const SlmpDeviceCode SD
```

Special Register

##### X

```csharp
public const SlmpDeviceCode X
```

Input

##### Y

```csharp
public const SlmpDeviceCode Y
```

Output

##### M

```csharp
public const SlmpDeviceCode M
```

Internal Relay

##### L

```csharp
public const SlmpDeviceCode L
```

Latch Relay

##### F

```csharp
public const SlmpDeviceCode F
```

Annunciator

##### V

```csharp
public const SlmpDeviceCode V
```

Edge Relay

##### B

```csharp
public const SlmpDeviceCode B
```

Link Relay

##### S

```csharp
public const SlmpDeviceCode S
```

Step Relay

##### D

```csharp
public const SlmpDeviceCode D
```

Data Register

##### W

```csharp
public const SlmpDeviceCode W
```

Link Register

##### TS

```csharp
public const SlmpDeviceCode TS
```

Timer Contact

##### TC

```csharp
public const SlmpDeviceCode TC
```

Timer Coil

##### TN

```csharp
public const SlmpDeviceCode TN
```

Timer Current Value

##### LTS

```csharp
public const SlmpDeviceCode LTS
```

Long Timer Contact

##### LTC

```csharp
public const SlmpDeviceCode LTC
```

Long Timer Coil

##### LTN

```csharp
public const SlmpDeviceCode LTN
```

Long Timer Current Value

##### STS

```csharp
public const SlmpDeviceCode STS
```

Retentive Timer Contact

##### STC

```csharp
public const SlmpDeviceCode STC
```

Retentive Timer Coil

##### STN

```csharp
public const SlmpDeviceCode STN
```

Retentive Timer Current Value

##### LSTS

```csharp
public const SlmpDeviceCode LSTS
```

Long Retentive Timer Contact

##### LSTC

```csharp
public const SlmpDeviceCode LSTC
```

Long Retentive Timer Coil

##### LSTN

```csharp
public const SlmpDeviceCode LSTN
```

Long Retentive Timer Current Value

##### LCC

```csharp
public const SlmpDeviceCode LCC
```

Long Counter Coil

##### LCS

```csharp
public const SlmpDeviceCode LCS
```

Long Counter Contact

##### LCN

```csharp
public const SlmpDeviceCode LCN
```

Long Counter Current Value

##### CS

```csharp
public const SlmpDeviceCode CS
```

Counter Contact

##### CC

```csharp
public const SlmpDeviceCode CC
```

Counter Coil

##### CN

```csharp
public const SlmpDeviceCode CN
```

Counter Current Value

##### SB

```csharp
public const SlmpDeviceCode SB
```

Link Special Relay

##### SW

```csharp
public const SlmpDeviceCode SW
```

Link Special Register

##### DX

```csharp
public const SlmpDeviceCode DX
```

Direct Input

##### DY

```csharp
public const SlmpDeviceCode DY
```

Direct Output

##### Z

```csharp
public const SlmpDeviceCode Z
```

Index Register

##### LZ

```csharp
public const SlmpDeviceCode LZ
```

Long Index Register

##### R

```csharp
public const SlmpDeviceCode R
```

File Register

##### ZR

```csharp
public const SlmpDeviceCode ZR
```

File Register (Continuous)

##### RD

```csharp
public const SlmpDeviceCode RD
```

Refresh Data Register

##### G

```csharp
public const SlmpDeviceCode G
```

Buffer Memory

##### HG

```csharp
public const SlmpDeviceCode HG
```

Long Buffer Memory

### SlmpDeviceParser

```csharp
public static class SlmpDeviceParser
```

Utility for parsing device address strings into `SlmpDeviceAddress`.

#### Members

##### Parse

```csharp
public static SlmpDeviceAddress Parse(string text)
```

Parses a device string (e.g., "D100", "X1F") into a `SlmpDeviceAddress`.

Returns: A parsed device address object.

Parameters:
- `text`: The device string to parse.

##### Parse

```csharp
public static SlmpDeviceAddress Parse(string text, SlmpPlcProfile PlcProfile)
```

Parses a device string using one explicit PLC profile.

### SlmpDeviceRangeCatalog

```csharp
public sealed class SlmpDeviceRangeCatalog
```

Result returned by `ReadDeviceRangeCatalogAsync`.

#### Members

##### SlmpDeviceRangeCatalog

```csharp
public SlmpDeviceRangeCatalog(string Model, ushort ModelCode, bool HasModelCode, SlmpPlcProfile PlcProfile, IReadOnlyList<SlmpDeviceRangeEntry> Entries)
```

Result returned by `ReadDeviceRangeCatalogAsync`.

Parameters:
- `Model`: Synthetic label for the explicitly selected PLC profile.
- `ModelCode`: Always zero because device-range catalogs do not infer profiles from type-name responses.
- `HasModelCode`: Always false because profile selection is explicit.
- `PlcProfile`: Resolved canonical PLC profile.
- `Entries`: Device entries for the resolved profile.

##### Model

```csharp
public string Model { get; set; }
```

Synthetic label for the explicitly selected PLC profile.

##### ModelCode

```csharp
public ushort ModelCode { get; set; }
```

Always zero because device-range catalogs do not infer profiles from type-name responses.

##### HasModelCode

```csharp
public bool HasModelCode { get; set; }
```

Always false because profile selection is explicit.

##### PlcProfile

```csharp
public SlmpPlcProfile PlcProfile { get; set; }
```

Resolved canonical PLC profile.

##### Entries

```csharp
public IReadOnlyList<SlmpDeviceRangeEntry> Entries { get; set; }
```

Device entries for the resolved profile.

### SlmpDeviceRangeCategory

```csharp
public enum SlmpDeviceRangeCategory
```

Logical device category used by the range catalog.

#### Members

##### Bit

```csharp
public const SlmpDeviceRangeCategory Bit
```

##### Word

```csharp
public const SlmpDeviceRangeCategory Word
```

##### TimerCounter

```csharp
public const SlmpDeviceRangeCategory TimerCounter
```

##### Index

```csharp
public const SlmpDeviceRangeCategory Index
```

##### FileRegister

```csharp
public const SlmpDeviceRangeCategory FileRegister
```

### SlmpDeviceRangeEntry

```csharp
public sealed class SlmpDeviceRangeEntry
```

One device entry returned by `SlmpDeviceRangeCatalog`.

#### Members

##### SlmpDeviceRangeEntry

```csharp
public SlmpDeviceRangeEntry(string Device, SlmpDeviceRangeCategory Category, bool IsBitDevice, bool Supported, uint LowerBound, uint? UpperBound, uint? PointCount, string AddressRange, SlmpDeviceRangeNotation Notation, string Source, string Notes)
```

One device entry returned by `SlmpDeviceRangeCatalog`.

Parameters:
- `Device`: Device code or address family string such as `D` or `TS`.
- `Category`: Logical category for grouping in monitor tools.
- `IsBitDevice`: True when the device is bit-addressable in normal use.
- `Supported`: True when the PLC profile supports this device.
- `LowerBound`: Lower bound value. Current rules always use 0.
- `UpperBound`: Inclusive last address. For a 0-based range this is `PointCount - 1`. Null means no finite bound is defined by the rule.
- `PointCount`: Usable point count read or resolved for the PLC profile. Null means no finite count is defined by the rule.
- `AddressRange`: Preformatted address range text such as `X000-X1FF` or `D0-D511`.
- `Notation`: Recommended public address notation for this library.
- `Source`: Rule source used to build `UpperBound`.
- `Notes`: Optional profile-specific caveats.

##### Device

```csharp
public string Device { get; set; }
```

Device code or address family string such as `D` or `TS`.

##### Category

```csharp
public SlmpDeviceRangeCategory Category { get; set; }
```

Logical category for grouping in monitor tools.

##### IsBitDevice

```csharp
public bool IsBitDevice { get; set; }
```

True when the device is bit-addressable in normal use.

##### Supported

```csharp
public bool Supported { get; set; }
```

True when the PLC profile supports this device.

##### LowerBound

```csharp
public uint LowerBound { get; set; }
```

Lower bound value. Current rules always use 0.

##### UpperBound

```csharp
public uint? UpperBound { get; set; }
```

Inclusive last address. For a 0-based range this is `PointCount - 1`. Null means no finite bound is defined by the rule.

##### PointCount

```csharp
public uint? PointCount { get; set; }
```

Usable point count read or resolved for the PLC profile. Null means no finite count is defined by the rule.

##### AddressRange

```csharp
public string AddressRange { get; set; }
```

Preformatted address range text such as `X000-X1FF` or `D0-D511`.

##### Notation

```csharp
public SlmpDeviceRangeNotation Notation { get; set; }
```

Recommended public address notation for this library.

##### Source

```csharp
public string Source { get; set; }
```

Rule source used to build `UpperBound`.

##### Notes

```csharp
public string Notes { get; set; }
```

Optional profile-specific caveats.

### SlmpDeviceRangeNotation

```csharp
public enum SlmpDeviceRangeNotation
```

Number notation used by the public address text for the device.

#### Members

##### Base10

```csharp
public const SlmpDeviceRangeNotation Base10
```

##### Base8

```csharp
public const SlmpDeviceRangeNotation Base8
```

##### Base16

```csharp
public const SlmpDeviceRangeNotation Base16
```

### SlmpEndCodeLanguage

```csharp
public enum SlmpEndCodeLanguage
```

Language selector retained for optional external SLMP end-code catalogs.

#### Members

##### English

```csharp
public const SlmpEndCodeLanguage English
```

English.

##### Japanese

```csharp
public const SlmpEndCodeLanguage Japanese
```

Japanese.

### SlmpEndCodes

```csharp
public static class SlmpEndCodes
```

Helper methods for SLMP end-code keys and categories.

#### Members

##### GetName

```csharp
public static string GetName(ushort endCode)
```

Returns the stable code-derived key for an SLMP end code.

##### GetMessage

```csharp
public static string GetMessage(ushort endCode, SlmpEndCodeLanguage language = English)
```

Returns a user-facing message for an SLMP end code. Localized message text is not embedded in this library; resolve `UInt16)` in an application-owned catalog.

##### IsRemotePasswordEndCode

```csharp
public static bool IsRemotePasswordEndCode(ushort endCode)
```

Returns whether the SLMP end code is related to remote password protection.

### SlmpError

```csharp
public class SlmpError
```

Error thrown when an SLMP protocol error occurs or the PLC returns an error code.

#### Members

##### SlmpError

```csharp
public SlmpError(string message, ushort? endCode = null, SlmpCommand? command = null, ushort? subcommand = null, Exception innerException = null, SlmpErrorInfo errorInfo = null)
```

##### EndCode

```csharp
public ushort? EndCode { get; }
```

The end code returned by the PLC (0x0000 for success).

##### Command

```csharp
public SlmpCommand? Command { get; }
```

The SLMP command that triggered the error.

##### Subcommand

```csharp
public ushort? Subcommand { get; }
```

The SLMP subcommand that triggered the error.

##### ErrorInfo

```csharp
public SlmpErrorInfo ErrorInfo { get; }
```

Structured PLC error information from the response data, when present.

##### EndCodeName

```csharp
public string EndCodeName { get; }
```

Compact symbolic name for `EndCode`, or null when no end code is available.

##### EndCodeMessage

```csharp
public string EndCodeMessage { get; }
```

English error detail/cause message for `EndCode`, or null when unknown.

##### IsRemotePasswordError

```csharp
public bool IsRemotePasswordError { get; }
```

True when `EndCode` is a remote-password-related SLMP error.

### SlmpErrorInfo

```csharp
public sealed class SlmpErrorInfo
```

Structured SLMP error information returned after a non-zero end code.

#### Members

##### SlmpErrorInfo

```csharp
public SlmpErrorInfo(byte Network, byte Station, ushort ModuleIo, byte Multidrop, ushort Command, ushort Subcommand, byte[] Raw)
```

Structured SLMP error information returned after a non-zero end code.

Parameters:
- `Network`: Network number reported by the PLC.
- `Station`: Station number reported by the PLC.
- `ModuleIo`: Module I/O number reported by the PLC.
- `Multidrop`: Multidrop station number reported by the PLC.
- `Command`: Command code associated with the PLC error.
- `Subcommand`: Subcommand code associated with the PLC error.
- `Raw`: Raw 9-byte error information block.

##### Parse

```csharp
public static SlmpErrorInfo Parse(ReadOnlySpan<byte> data)
```

Parse a 9-byte SLMP error information block, or return null when it is not present.

##### Network

```csharp
public byte Network { get; set; }
```

Network number reported by the PLC.

##### Station

```csharp
public byte Station { get; set; }
```

Station number reported by the PLC.

##### ModuleIo

```csharp
public ushort ModuleIo { get; set; }
```

Module I/O number reported by the PLC.

##### Multidrop

```csharp
public byte Multidrop { get; set; }
```

Multidrop station number reported by the PLC.

##### Command

```csharp
public ushort Command { get; set; }
```

Command code associated with the PLC error.

##### Subcommand

```csharp
public ushort Subcommand { get; set; }
```

Subcommand code associated with the PLC error.

##### Raw

```csharp
public byte[] Raw { get; set; }
```

Raw 9-byte error information block.

### SlmpExtensionSpec

```csharp
public struct SlmpExtensionSpec
```

Represents Extended Device extension fields for device access.

#### Members

##### SlmpExtensionSpec

```csharp
public SlmpExtensionSpec(ushort ExtensionSpecification = 0, byte ExtensionSpecificationModification = 0, byte DeviceModificationIndex = 0, byte DeviceModificationFlags = 0, byte DirectMemorySpecification = 0)
```

Represents Extended Device extension fields for device access.

##### ExtensionSpecification

```csharp
public ushort ExtensionSpecification { get; set; }
```

##### ExtensionSpecificationModification

```csharp
public byte ExtensionSpecificationModification { get; set; }
```

##### DeviceModificationIndex

```csharp
public byte DeviceModificationIndex { get; set; }
```

##### DeviceModificationFlags

```csharp
public byte DeviceModificationFlags { get; set; }
```

##### DirectMemorySpecification

```csharp
public byte DirectMemorySpecification { get; set; }
```

### SlmpFrameType

```csharp
public enum SlmpFrameType
```

Specifies the SLMP frame format header.

#### Members

##### Frame3E

```csharp
public const SlmpFrameType Frame3E
```

3E Frame (Standard subheader 0x5000).

##### Frame4E

```csharp
public const SlmpFrameType Frame4E
```

4E Frame (Serial-based subheader 0x5400).

### SlmpLabelArrayReadPoint

```csharp
public sealed class SlmpLabelArrayReadPoint
```

Describes one array label to read. `UnitSpecification`: 0 = bit, 1 = byte. `ArrayDataLength` is in units defined by `UnitSpecification`.

#### Members

##### SlmpLabelArrayReadPoint

```csharp
public SlmpLabelArrayReadPoint(string Label, byte UnitSpecification, ushort ArrayDataLength)
```

Describes one array label to read. `UnitSpecification`: 0 = bit, 1 = byte. `ArrayDataLength` is in units defined by `UnitSpecification`.

##### Label

```csharp
public string Label { get; set; }
```

##### UnitSpecification

```csharp
public byte UnitSpecification { get; set; }
```

##### ArrayDataLength

```csharp
public ushort ArrayDataLength { get; set; }
```

### SlmpLabelArrayReadResult

```csharp
public sealed class SlmpLabelArrayReadResult
```

Result item returned by `ReadArrayLabelsAsync`.

#### Members

##### SlmpLabelArrayReadResult

```csharp
public SlmpLabelArrayReadResult(byte DataTypeId, byte UnitSpecification, ushort ArrayDataLength, byte[] Data)
```

Result item returned by `ReadArrayLabelsAsync`.

##### DataTypeId

```csharp
public byte DataTypeId { get; set; }
```

##### UnitSpecification

```csharp
public byte UnitSpecification { get; set; }
```

##### ArrayDataLength

```csharp
public ushort ArrayDataLength { get; set; }
```

##### Data

```csharp
public byte[] Data { get; set; }
```

### SlmpLabelArrayWritePoint

```csharp
public sealed class SlmpLabelArrayWritePoint
```

Describes one array label to write, including the raw data bytes.

#### Members

##### SlmpLabelArrayWritePoint

```csharp
public SlmpLabelArrayWritePoint(string Label, byte UnitSpecification, ushort ArrayDataLength, byte[] Data)
```

Describes one array label to write, including the raw data bytes.

##### Label

```csharp
public string Label { get; set; }
```

##### UnitSpecification

```csharp
public byte UnitSpecification { get; set; }
```

##### ArrayDataLength

```csharp
public ushort ArrayDataLength { get; set; }
```

##### Data

```csharp
public byte[] Data { get; set; }
```

### SlmpLabelRandomReadResult

```csharp
public sealed class SlmpLabelRandomReadResult
```

Result item returned by `ReadRandomLabelsAsync`.

#### Members

##### SlmpLabelRandomReadResult

```csharp
public SlmpLabelRandomReadResult(byte DataTypeId, byte Spare, ushort ReadDataLength, byte[] Data)
```

Result item returned by `ReadRandomLabelsAsync`.

##### DataTypeId

```csharp
public byte DataTypeId { get; set; }
```

##### Spare

```csharp
public byte Spare { get; set; }
```

##### ReadDataLength

```csharp
public ushort ReadDataLength { get; set; }
```

##### Data

```csharp
public byte[] Data { get; set; }
```

### SlmpLabelRandomWritePoint

```csharp
public sealed class SlmpLabelRandomWritePoint
```

Describes one random label write point.

#### Members

##### SlmpLabelRandomWritePoint

```csharp
public SlmpLabelRandomWritePoint(string Label, byte[] Data)
```

Describes one random label write point.

##### Label

```csharp
public string Label { get; set; }
```

##### Data

```csharp
public byte[] Data { get; set; }
```

### SlmpLongTimerResult

```csharp
public sealed class SlmpLongTimerResult
```

Represents the decoded state of a single long timer or long retentive timer device.

#### Members

##### SlmpLongTimerResult

```csharp
public SlmpLongTimerResult(int Index, string Device, uint CurrentValue, bool Contact, bool Coil, ushort StatusWord, ushort[] RawWords)
```

Represents the decoded state of a single long timer or long retentive timer device.

Parameters:
- `Index`: The device number (e.g. 0 for LTN0).
- `Device`: The device address string (e.g. "LTN0").
- `CurrentValue`: 32-bit current value (two 16-bit words combined).
- `Contact`: True when the timer contact is ON.
- `Coil`: True when the timer coil is ON.
- `StatusWord`: Raw status word (word index 2 in the 4-word block).
- `RawWords`: The four raw 16-bit words that make up this timer entry.

##### Index

```csharp
public int Index { get; set; }
```

The device number (e.g. 0 for LTN0).

##### Device

```csharp
public string Device { get; set; }
```

The device address string (e.g. "LTN0").

##### CurrentValue

```csharp
public uint CurrentValue { get; set; }
```

32-bit current value (two 16-bit words combined).

##### Contact

```csharp
public bool Contact { get; set; }
```

True when the timer contact is ON.

##### Coil

```csharp
public bool Coil { get; set; }
```

True when the timer coil is ON.

##### StatusWord

```csharp
public ushort StatusWord { get; set; }
```

Raw status word (word index 2 in the 4-word block).

##### RawWords

```csharp
public ushort[] RawWords { get; set; }
```

The four raw 16-bit words that make up this timer entry.

### SlmpModuleIo

```csharp
public static class SlmpModuleIo
```

Named SLMP request-header module I/O numbers for CPU routing.

Remarks: Use these constants with `ModuleIo` when routing a request to a multi-CPU or redundant CPU target. Values are from the SLMP specification SH080956 request destination module I/O number field. The default own-station target remains `OwnStation`.

#### Members

##### ControlSystemCpu

```csharp
public const ushort ControlSystemCpu
```

Control system CPU in a redundant CPU system.

##### StandbySystemCpu

```csharp
public const ushort StandbySystemCpu
```

Standby system CPU in a redundant CPU system.

##### SystemACpu

```csharp
public const ushort SystemACpu
```

System A CPU in a redundant CPU system.

##### SystemBCpu

```csharp
public const ushort SystemBCpu
```

System B CPU in a redundant CPU system.

##### MultipleCpu1

```csharp
public const ushort MultipleCpu1
```

CPU No. 1 in a multi-CPU system.

##### MultipleCpu2

```csharp
public const ushort MultipleCpu2
```

CPU No. 2 in a multi-CPU system.

##### MultipleCpu3

```csharp
public const ushort MultipleCpu3
```

CPU No. 3 in a multi-CPU system.

##### MultipleCpu4

```csharp
public const ushort MultipleCpu4
```

CPU No. 4 in a multi-CPU system.

##### RemoteHead1

```csharp
public const ushort RemoteHead1
```

Remote head No. 1 route.

##### RemoteHead2

```csharp
public const ushort RemoteHead2
```

Remote head No. 2 route.

##### ControlSystemRemoteHead

```csharp
public const ushort ControlSystemRemoteHead
```

Control system remote head route.

##### StandbySystemRemoteHead

```csharp
public const ushort StandbySystemRemoteHead
```

Standby system remote head route.

##### OwnStation

```csharp
public const ushort OwnStation
```

Own station route.

### SlmpMonitorResult

```csharp
public sealed class SlmpMonitorResult
```

Result returned by `RunMonitorCycleAsync`.

#### Members

##### SlmpMonitorResult

```csharp
public SlmpMonitorResult(ushort[] WordValues, uint[] DwordValues)
```

Result returned by `RunMonitorCycleAsync`.

Parameters:
- `WordValues`: 16-bit word values for the registered word devices (in registration order).
- `DwordValues`: 32-bit values for the registered DWord devices (in registration order).

##### WordValues

```csharp
public ushort[] WordValues { get; set; }
```

16-bit word values for the registered word devices (in registration order).

##### DwordValues

```csharp
public uint[] DwordValues { get; set; }
```

32-bit values for the registered DWord devices (in registration order).

### SlmpNamedTarget

```csharp
public struct SlmpNamedTarget
```

Represents a target station with a human-readable name.

#### Members

##### SlmpNamedTarget

```csharp
public SlmpNamedTarget(string Name, SlmpTargetAddress Target)
```

Represents a target station with a human-readable name.

##### Name

```csharp
public string Name { get; set; }
```

##### Target

```csharp
public SlmpTargetAddress Target { get; set; }
```

### SlmpPlcProfile

```csharp
public enum SlmpPlcProfile
```

Canonical PLC profile used by the high-level API.

#### Members

##### Unspecified

```csharp
public const SlmpPlcProfile Unspecified
```

No PLC profile has been selected.

##### IqF

```csharp
public const SlmpPlcProfile IqF
```

##### IqR

```csharp
public const SlmpPlcProfile IqR
```

##### IqRRj71En71

```csharp
public const SlmpPlcProfile IqRRj71En71
```

##### IqL

```csharp
public const SlmpPlcProfile IqL
```

##### MxF

```csharp
public const SlmpPlcProfile MxF
```

##### MxR

```csharp
public const SlmpPlcProfile MxR
```

##### QCpu

```csharp
public const SlmpPlcProfile QCpu
```

##### LCpu

```csharp
public const SlmpPlcProfile LCpu
```

##### QnU

```csharp
public const SlmpPlcProfile QnU
```

##### QnUDV

```csharp
public const SlmpPlcProfile QnUDV
```

##### QCpuQj71E71100

```csharp
public const SlmpPlcProfile QCpuQj71E71100
```

##### LCpuLj71E71100

```csharp
public const SlmpPlcProfile LCpuLj71E71100
```

##### QnUQj71E71100

```csharp
public const SlmpPlcProfile QnUQj71E71100
```

##### QnUDVQj71E71100

```csharp
public const SlmpPlcProfile QnUDVQj71E71100
```

### SlmpPlcProfileDefaults

```csharp
public sealed class SlmpPlcProfileDefaults
```

Resolved fixed defaults for one canonical PLC profile.

#### Members

##### SlmpPlcProfileDefaults

```csharp
public SlmpPlcProfileDefaults(SlmpFrameType FrameType, SlmpCompatibilityMode CompatibilityMode, SlmpPlcProfile AddressProfile, SlmpPlcProfile RangeProfile)
```

Resolved fixed defaults for one canonical PLC profile.

##### FrameType

```csharp
public SlmpFrameType FrameType { get; set; }
```

##### CompatibilityMode

```csharp
public SlmpCompatibilityMode CompatibilityMode { get; set; }
```

##### AddressProfile

```csharp
public SlmpPlcProfile AddressProfile { get; set; }
```

##### RangeProfile

```csharp
public SlmpPlcProfile RangeProfile { get; set; }
```

### SlmpPlcProfiles

```csharp
public static class SlmpPlcProfiles
```

Fixed high-level defaults driven by `SlmpPlcProfile`.

#### Members

##### Parse

```csharp
public static SlmpPlcProfile Parse(string text)
```

Parse a canonical PLC profile string.

##### ToCanonicalString

```csharp
public static string ToCanonicalString(SlmpPlcProfile profile)
```

Return the canonical string form used in user-facing configuration.

##### GetDisplayName

```csharp
public static string GetDisplayName(SlmpPlcProfile profile)
```

Return the canonical human-readable display name for a PLC profile.

##### Resolve

```csharp
public static SlmpPlcProfileDefaults Resolve(SlmpPlcProfile profile)
```

Resolve the stable defaults for one explicit PLC profile.

##### ValidateConnectionProfile

```csharp
public static SlmpPlcProfile ValidateConnectionProfile(SlmpPlcProfile profile)
```

Validate that the profile can be used to open an SLMP connection.

##### UsesIqrProtocol

```csharp
public static bool UsesIqrProtocol(SlmpPlcProfile profile)
```

True when the selected profile uses iQ-R-compatible command subcommands and payloads.

##### UsesIqFXyOctal

```csharp
public static bool UsesIqFXyOctal(SlmpPlcProfile profile)
```

True when `X` and `Y` strings must be parsed as octal.

### SlmpProfileFeatureException

```csharp
public sealed class SlmpProfileFeatureException
```

Error thrown before sending a high-level request when the selected PLC profile marks a feature as blocked or unverified and strict profile checks are enabled.

#### Members

##### SlmpProfileFeatureException

```csharp
public SlmpProfileFeatureException(SlmpPlcProfile plcProfile, string featureKey, string state, string evidence)
```

##### PlcProfile

```csharp
public SlmpPlcProfile PlcProfile { get; }
```

Selected PLC profile.

##### ProfileId

```csharp
public string ProfileId { get; }
```

Canonical profile identifier such as `melsec:qnudv`.

##### FeatureKey

```csharp
public string FeatureKey { get; }
```

Canonical feature key from the SLMP profile capability data.

##### State

```csharp
public string State { get; }
```

Canonical feature state, for example `blocked` or `unverified`.

##### Evidence

```csharp
public string Evidence { get; }
```

Evidence source or note that explains why the feature is guarded.

##### DisableHint

```csharp
public string DisableHint { get; }
```

Hint for intentionally bypassing the feature guard.

### SlmpQualifiedDeviceAddress

```csharp
public struct SlmpQualifiedDeviceAddress
```

Represents a device address that may include an explicit Extended Device extension specification.

#### Members

##### SlmpQualifiedDeviceAddress

```csharp
public SlmpQualifiedDeviceAddress(SlmpDeviceAddress Device, ushort? ExtensionSpecification, byte? DirectMemorySpecification = null)
```

Represents a device address that may include an explicit Extended Device extension specification.

##### Device

```csharp
public SlmpDeviceAddress Device { get; set; }
```

##### ExtensionSpecification

```csharp
public ushort? ExtensionSpecification { get; set; }
```

##### DirectMemorySpecification

```csharp
public byte? DirectMemorySpecification { get; set; }
```

### SlmpQualifiedDeviceParser

```csharp
public static class SlmpQualifiedDeviceParser
```

Utility for parsing qualified device strings (e.g., "U01\G10", "J2\SW10") into `SlmpQualifiedDeviceAddress`.

#### Members

##### Parse

```csharp
public static SlmpQualifiedDeviceAddress Parse(string text)
```

Parses a qualified device string into a `SlmpQualifiedDeviceAddress`.

### SlmpTargetAddress

```csharp
public struct SlmpTargetAddress
```

Represents the destination routing fields for an SLMP frame.

#### Members

##### SlmpTargetAddress

```csharp
public SlmpTargetAddress(byte Network = 0, byte Station = 255, ushort ModuleIo = 1023, byte Multidrop = 0)
```

Represents the destination routing fields for an SLMP frame.

Parameters:
- `Network`: Network number (0x00 for local network).
- `Station`: Station number (0xFF for the connected station).
- `ModuleIo`: Module I/O number (0x03FF for own station).
- `Multidrop`: Multidrop station number (0x00 for no multidrop).

##### Network

```csharp
public byte Network { get; set; }
```

Network number (0x00 for local network).

##### Station

```csharp
public byte Station { get; set; }
```

Station number (0xFF for the connected station).

##### ModuleIo

```csharp
public ushort ModuleIo { get; set; }
```

Module I/O number (0x03FF for own station).

##### Multidrop

```csharp
public byte Multidrop { get; set; }
```

Multidrop station number (0x00 for no multidrop).

### SlmpTargetParser

```csharp
public static class SlmpTargetParser
```

Utility for parsing target station descriptions into `SlmpNamedTarget`.

#### Members

##### ParseNamed

```csharp
public static SlmpNamedTarget ParseNamed(string text)
```

Parses a single target string. Supports "SELF", "SELF-MULTIPLE-CPU-1..4", or "NAME,NETWORK,STATION,MODULE_IO,MULTIDROP".

##### ParseMany

```csharp
public static IReadOnlyList<SlmpNamedTarget> ParseMany(IReadOnlyList<string> values)
```

Parses a list of target strings.

##### ParseAutoNumber

```csharp
public static int ParseAutoNumber(string text)
```

Parses a number string, supporting both decimal and "0x" hexadecimal notation.

### SlmpTraceDirection

```csharp
public enum SlmpTraceDirection
```

Direction of a frame captured by `TraceHook`.

#### Members

##### Send

```csharp
public const SlmpTraceDirection Send
```

Frame sent to the PLC.

##### Receive

```csharp
public const SlmpTraceDirection Receive
```

Frame received from the PLC.

### SlmpTraceFrame

```csharp
public class SlmpTraceFrame
```

A raw frame captured by `TraceHook`.

#### Members

##### SlmpTraceFrame

```csharp
public SlmpTraceFrame(SlmpTraceDirection Direction, byte[] Data, DateTime Timestamp)
```

A raw frame captured by `TraceHook`.

##### Direction

```csharp
public SlmpTraceDirection Direction { get; set; }
```

##### Data

```csharp
public byte[] Data { get; set; }
```

##### Timestamp

```csharp
public DateTime Timestamp { get; set; }
```

### SlmpTransportMode

```csharp
public enum SlmpTransportMode
```

Specifies the transport protocol used for SLMP communication.

#### Members

##### Tcp

```csharp
public const SlmpTransportMode Tcp
```

Transmission Control Protocol (Connection-oriented).

##### Udp

```csharp
public const SlmpTransportMode Udp
```

User Datagram Protocol (Connectionless).

### SlmpTypeNameInfo

```csharp
public sealed class SlmpTypeNameInfo
```

Information about the PLC model and type name.

#### Members

##### SlmpTypeNameInfo

```csharp
public SlmpTypeNameInfo(string Model, ushort ModelCode, bool HasModelCode)
```

Information about the PLC model and type name.

Parameters:
- `Model`: The model name string.
- `ModelCode`: Internal model code.
- `HasModelCode`: True if the model code is valid.

##### Model

```csharp
public string Model { get; set; }
```

The model name string.

##### ModelCode

```csharp
public ushort ModelCode { get; set; }
```

Internal model code.

##### HasModelCode

```csharp
public bool HasModelCode { get; set; }
```

True if the model code is valid.
