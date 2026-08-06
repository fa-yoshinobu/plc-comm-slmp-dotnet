# SLMP .NET API Reference

This page is generated from the `PlcComm.Slmp` assembly public API and XML documentation comments.

Run `python scripts/generate_api_reference.py --help` from the repository root to regenerate it.

## PlcComm.Slmp

### IndexLz

```csharp
public sealed class IndexLz
```

#### Members

##### IndexLz

```csharp
public IndexLz(byte index)
```

##### Index

```csharp
public byte Index { get; }
```

### IndexZ

```csharp
public sealed class IndexZ
```

#### Members

##### IndexZ

```csharp
public IndexZ(byte Index)
```

##### Index

```csharp
public byte Index { get; init; }
```

### Indirect

```csharp
public sealed class Indirect
```

#### Members

##### Indirect

```csharp
public Indirect()
```

### SlmpAddress

```csharp
public static class SlmpAddress
```

Public helpers for SLMP device address text.

Remarks: These helpers provide a small, documentation-friendly surface for parse, format, and normalization tasks. Use them when you want canonical address text in samples, generated docs, validation tooling, or UI layers.

#### Members

##### Format

```csharp
public static string Format(SlmpDeviceAddress address)
```

Formats one SLMP device address using canonical device text.

Remarks: Hex-addressed device families such as `X`, `Y`, `B`, and `W` are emitted in uppercase hexadecimal form.

Returns: Canonical uppercase address text.

Parameters:
- `address`: The parsed device address to format.

##### Normalize

```csharp
public static string Normalize(string text, SlmpPlcProfile plcProfile)
```

Normalizes one SLMP device string using the explicit PLC profile.

##### Parse

```csharp
public static SlmpDeviceAddress Parse(string text, SlmpPlcProfile plcProfile)
```

Parses one SLMP device string using the explicit PLC profile.

##### TryParse

```csharp
public static bool TryParse(string text, SlmpPlcProfile plcProfile, out SlmpDeviceAddress address)
```

Attempts to parse one SLMP device string using the explicit PLC profile.

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
public SlmpDeviceAddress Device { get; init; }
```

##### Points

```csharp
public ushort Points { get; init; }
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
public SlmpDeviceAddress Device { get; init; }
```

##### Values

```csharp
public IReadOnlyList<ushort> Values { get; init; }
```

### SlmpClient

```csharp
public sealed class SlmpClient
```

A high-performance, asynchronous SLMP (MC Protocol) client for .NET. Supports 3E and 4E frame formats over TCP and UDP.

Remarks: Public operations on one client enter one arrival-order FIFO queue, so one connection has at most one active wire transaction and 4E serial numbers remain associated with their responses. Queue waiting does not consume the transaction timeout. A waiting caller can cancel without sending. Unless a method explicitly documents a multi-step semantic operation, each request method emits exactly one SLMP request and never splits an oversized operation. Effective limits are validated before serial allocation or transport. Contiguous Direct, Random, Monitor-registration, Block, and applicable Extended Device routes validate their complete consumed device span against the selected 24-bit Q/L-compatible or 32-bit iQ-R wire address field. Link-direct Extended Device layouts remain 24-bit even on an iQ-R client. Packed word access to a bit device consumes 16 device numbers per word; ordinary DWord/Float32 access consumes two word devices per value, while packed DWord/Float32 access to a bit device consumes 32 device numbers per value; a bit-block point consumes 16 bit devices; and four words in a Direct long-timer status block consume one LTN/LSTN device. This representability check does not enforce configured PLC usable ranges. The factory `OpenAndConnectAsync` returns a ready-to-use `SlmpClient` and is the recommended entry point for most use cases. Concurrent close or disposal rejects incomplete active work and queued work. A success value or framed PLC end-code error that has completed command-specific decoding remains definitive and is not replaced by the later lifecycle transition.

#### Members

##### SlmpClient

```csharp
public SlmpClient(string host, SlmpPlcProfile plcProfile, int port, SlmpTransportMode transportMode, SlmpTargetAddress targetAddress)
```

Initializes a new instance of the `SlmpClient` class.

Parameters:
- `host`: The IPv4 address or hostname that resolves to IPv4 for the PLC. IPv6 is not supported.
- `plcProfile`: The PLC profile. This selection derives frame type and compatibility mode.
- `port`: The required port number.
- `transportMode`: The transport protocol (TCP or UDP).
- `targetAddress`: The complete destination route.

##### ClearErrorAsync

```csharp
public Task ClearErrorAsync(CancellationToken cancellationToken = default)
```

Sends the fixed Clear Error command as exactly one request.

##### Close

```csharp
public void Close()
```

Closes the connection and rejects the active and queued operations for this transport generation.

##### CloseAsync

```csharp
public Task CloseAsync()
```

Closes the connection to the PLC asynchronously.

##### Dispose

```csharp
public void Dispose()
```

Disposes the client and permanently closes the connection.

Remarks: Unlike `Close`, disposal is terminal. Later open and request operations throw `ObjectDisposedException`.

##### DisposeAsync

```csharp
public ValueTask DisposeAsync()
```

Asynchronously disposes the client and permanently closes the connection.

Remarks: Disposal is terminal and idempotent. Later open and request operations throw `ObjectDisposedException`.

##### ExtendUnitReadBytesAsync

```csharp
public Task<byte[]> ExtendUnitReadBytesAsync(uint headAddress, ushort byteLength, ushort moduleNo, CancellationToken cancellationToken = default)
```

Reads raw bytes from an extend unit (command 0x0601).

Parameters:
- `headAddress`: Starting address in the extend unit (32-bit).
- `byteLength`: Number of bytes to read.
- `moduleNo`: Configured Extend Unit module I/O number.
- `cancellationToken`: Cancellation token.

##### ExtendUnitReadDWordAsync

```csharp
public Task<uint> ExtendUnitReadDWordAsync(uint headAddress, ushort moduleNo, CancellationToken cancellationToken = default)
```

Reads a double word (32-bit) from an extend unit.

##### ExtendUnitReadWordAsync

```csharp
public Task<ushort> ExtendUnitReadWordAsync(uint headAddress, ushort moduleNo, CancellationToken cancellationToken = default)
```

Reads a single word from an extend unit.

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

##### ExtendUnitWriteDWordAsync

```csharp
public Task ExtendUnitWriteDWordAsync(uint headAddress, ushort moduleNo, uint value, CancellationToken cancellationToken = default)
```

Writes a double word (32-bit) to an extend unit.

##### ExtendUnitWriteWordAsync

```csharp
public Task ExtendUnitWriteWordAsync(uint headAddress, ushort moduleNo, ushort value, CancellationToken cancellationToken = default)
```

Writes a single word to an extend unit.

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

##### Open

```csharp
public void Open()
```

Opens the connection to the PLC synchronously.

##### OpenAndConnectAsync

```csharp
public static Task<SlmpClient> OpenAndConnectAsync(string host, int port, SlmpPlcProfile plcProfile, SlmpTransportMode transportMode, SlmpTargetAddress targetAddress, CancellationToken cancellationToken = default)
```

Opens a connection with explicit stable settings and returns a connected `SlmpClient`.

Remarks: This is the recommended entry point for application code because it combines one explicit PLC profile with the ordinary client's FIFO admission queue, which is safe to share across multiple tasks.

Returns: A connected client ready for high-level helpers such as `ReadTypedAsync`, `ReadNamedAsync`, and `PollAsync`.

Parameters:
- `host`: PLC IP address or hostname.
- `port`: SLMP port number such as 1025 for iQ-R/iQ-F or 5007 for Q/L.
- `plcProfile`: Canonical PLC profile used to derive the standard connection defaults.
- `transportMode`: Required TCP or UDP transport.
- `targetAddress`: Required complete destination route.
- `cancellationToken`: A token to cancel the operation.

##### OpenAsync

```csharp
public Task OpenAsync(CancellationToken cancellationToken = default)
```

Opens the connection to the PLC asynchronously.

Returns: A task representing the asynchronous operation.

Parameters:
- `cancellationToken`: A token to cancel the operation.

##### ReadArrayLabelsAsync

```csharp
public Task<SlmpLabelArrayReadResult[]> ReadArrayLabelsAsync(IReadOnlyList<SlmpLabelArrayReadPoint> points, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

Reads array labels from the PLC (command 0x041A).

Parameters:
- `points`: Labels to read, each with unit specification and array data length.
- `abbreviationLabels`: Optional abbreviation label names (sent before regular points).
- `cancellationToken`: Cancellation token.

##### ReadBitBlocksAsync

```csharp
public Task<ushort[]> ReadBitBlocksAsync(IReadOnlyList<SlmpBlockRead> bitBlocks, CancellationToken cancellationToken = default)
```

Reads only bit blocks in one block-read request.

##### ReadBitsAsync

```csharp
public Task<bool[]> ReadBitsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

##### ReadBitsExtendedAsync

```csharp
public Task<bool[]> ReadBitsExtendedAsync(SlmpQualifiedDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

##### ReadBlockAsync

```csharp
public Task<ValueTuple<ushort[], ushort[]>> ReadBlockAsync(IReadOnlyList<SlmpBlockRead> wordBlocks, IReadOnlyList<SlmpBlockRead> bitBlocks, CancellationToken cancellationToken = default)
```

##### ReadCpuOperationStateAsync

```csharp
public Task<SlmpCpuOperationState> ReadCpuOperationStateAsync(CancellationToken cancellationToken = default)
```

Reads `SD203` and decodes the CPU operation state from the lower 4 bits.

Returns: The decoded CPU operation state and raw masked code.

Parameters:
- `cancellationToken`: A token to cancel the operation.

##### ReadDWordsRawAsync

```csharp
public Task<uint[]> ReadDWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

Reads contiguous 32-bit values in one Direct Read request.

Parameters:
- `device`: Starting word-addressable device.
- `points`: Number of DWord values, in public 32-bit units; maximum 480 for a 960-word profile limit.
- `cancellationToken`: Cancellation token.

##### ReadDeviceRangeCatalogAsync

```csharp
public Task<SlmpDeviceRangeCatalog> ReadDeviceRangeCatalogAsync(CancellationToken cancellationToken = default)
```

Reads the configured profile-specific device upper-bound catalog from one canonical SD-register window.

Remarks: No address probe or error-derived boundary inference is performed. Acquisition errors propagate to the caller.

Returns: A catalog containing the configured profile and device upper-bound entries.

Parameters:
- `cancellationToken`: A token to cancel the operation.

##### ReadFloat32sAsync

```csharp
public Task<float[]> ReadFloat32sAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

Reads contiguous float32 values in one Direct Read request.

##### ReadLongRetentiveTimerAsync

```csharp
public Task<SlmpLongTimerResult[]> ReadLongRetentiveTimerAsync(int headNo, int points, CancellationToken cancellationToken = default)
```

Reads one or more long retentive timers starting at the given device number. Each timer occupies 4 consecutive words: [current_lo, current_hi, status, reserved].

Parameters:
- `headNo`: Starting LSTN device number (e.g. 0 for LSTN0).
- `points`: Number of timers to read.
- `cancellationToken`: Cancellation token.

##### ReadLongTimerAsync

```csharp
public Task<SlmpLongTimerResult[]> ReadLongTimerAsync(int headNo, int points, CancellationToken cancellationToken = default)
```

Reads one or more long timers starting at the given device number. Each timer occupies 4 consecutive words: [current_lo, current_hi, status, reserved].

Parameters:
- `headNo`: Starting LTN device number (e.g. 0 for LTN0).
- `points`: Number of timers to read.
- `cancellationToken`: Cancellation token.

##### ReadLstcStatesAsync

```csharp
public Task<bool[]> ReadLstcStatesAsync(int headNo, int points, CancellationToken cancellationToken = default)
```

Returns the coil state of each long retentive timer in the range.

##### ReadLstsStatesAsync

```csharp
public Task<bool[]> ReadLstsStatesAsync(int headNo, int points, CancellationToken cancellationToken = default)
```

Returns the contact state of each long retentive timer in the range.

##### ReadLtcStatesAsync

```csharp
public Task<bool[]> ReadLtcStatesAsync(int headNo, int points, CancellationToken cancellationToken = default)
```

Returns the coil state of each long timer in the range.

##### ReadLtsStatesAsync

```csharp
public Task<bool[]> ReadLtsStatesAsync(int headNo, int points, CancellationToken cancellationToken = default)
```

Returns the contact state of each long timer in the range.

##### ReadRandomAsync

```csharp
public Task<ValueTuple<ushort[], uint[]>> ReadRandomAsync(IReadOnlyList<SlmpDeviceAddress> wordDevices, IReadOnlyList<SlmpDeviceAddress> dwordDevices, CancellationToken cancellationToken = default)
```

##### ReadRandomDWordsAsync

```csharp
public Task<uint[]> ReadRandomDWordsAsync(IReadOnlyList<SlmpDeviceAddress> dwordDevices, CancellationToken cancellationToken = default)
```

Reads only DWord devices in one random-read request.

##### ReadRandomDWordsExtendedAsync

```csharp
public Task<uint[]> ReadRandomDWordsExtendedAsync(IReadOnlyList<SlmpQualifiedDeviceAddress> dwordDevices, CancellationToken cancellationToken = default)
```

Reads only DWord devices through semantic Extended Device routes.

##### ReadRandomExtAsync

```csharp
public Task<ValueTuple<ushort[], uint[]>> ReadRandomExtAsync(IReadOnlyList<SlmpQualifiedDeviceAddress> wordDevices, IReadOnlyList<SlmpQualifiedDeviceAddress> dwordDevices, CancellationToken cancellationToken = default)
```

##### ReadRandomLabelsAsync

```csharp
public Task<SlmpLabelRandomReadResult[]> ReadRandomLabelsAsync(IReadOnlyList<string> labels, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

Reads random labels from the PLC (command 0x041C).

##### ReadRandomWordsAsync

```csharp
public Task<ushort[]> ReadRandomWordsAsync(IReadOnlyList<SlmpDeviceAddress> wordDevices, CancellationToken cancellationToken = default)
```

Reads only word devices in one random-read request.

##### ReadRandomWordsExtendedAsync

```csharp
public Task<ushort[]> ReadRandomWordsExtendedAsync(IReadOnlyList<SlmpQualifiedDeviceAddress> wordDevices, CancellationToken cancellationToken = default)
```

Reads only word devices through semantic Extended Device routes.

##### ReadTypeNameAsync

```csharp
public Task<SlmpTypeNameInfo> ReadTypeNameAsync(CancellationToken cancellationToken = default)
```

Reads the PLC model and type name info asynchronously.

Returns: An object containing model name and code.

Parameters:
- `cancellationToken`: A token to cancel the operation.

##### ReadWordBlocksAsync

```csharp
public Task<ushort[]> ReadWordBlocksAsync(IReadOnlyList<SlmpBlockRead> wordBlocks, CancellationToken cancellationToken = default)
```

Reads only word blocks in one block-read request.

##### ReadWordsExtendedAsync

```csharp
public Task<ushort[]> ReadWordsExtendedAsync(SlmpQualifiedDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
```

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

##### RegisterMonitorDevicesAsync

```csharp
public Task RegisterMonitorDevicesAsync(IReadOnlyList<SlmpDeviceAddress> wordDevices, IReadOnlyList<SlmpDeviceAddress> dwordDevices, CancellationToken cancellationToken = default)
```

Registers a set of word and DWord devices for monitoring (command 0x0801). Call `RunMonitorCycleAsync` to read the registered devices.

Parameters:
- `wordDevices`: Word devices to monitor.
- `dwordDevices`: DWord devices to monitor.
- `cancellationToken`: Cancellation token.

##### RegisterMonitorDevicesExtAsync

```csharp
public Task RegisterMonitorDevicesExtAsync(IReadOnlyList<SlmpQualifiedDeviceAddress> wordDevices, IReadOnlyList<SlmpQualifiedDeviceAddress> dwordDevices, CancellationToken cancellationToken = default)
```

##### RemoteLatchClearAsync

```csharp
public Task RemoteLatchClearAsync(CancellationToken cancellationToken = default)
```

##### RemotePasswordLockAsync

```csharp
public Task RemotePasswordLockAsync(string password, CancellationToken cancellationToken = default)
```

##### RemotePasswordUnlockAsync

```csharp
public Task RemotePasswordUnlockAsync(string password, CancellationToken cancellationToken = default)
```

##### RemotePauseAsync

```csharp
public Task RemotePauseAsync(SlmpRemoteMode mode, CancellationToken cancellationToken = default)
```

##### RemoteResetAsync

```csharp
public Task RemoteResetAsync(CancellationToken cancellationToken = default)
```

Sends the fixed Remote RESET frame without waiting for a success response, then invalidates the transport. Call `OpenAsync` explicitly before another request and verify the PLC state.

##### RemoteRunAsync

```csharp
public Task RemoteRunAsync(SlmpRemoteMode mode, SlmpRemoteClearMode clearMode, CancellationToken cancellationToken = default)
```

##### RemoteStopAsync

```csharp
public Task RemoteStopAsync(CancellationToken cancellationToken = default)
```

##### RunMonitorCycleAsync

```csharp
public Task<SlmpMonitorResult> RunMonitorCycleAsync(int wordPoints, int dwordPoints, CancellationToken cancellationToken = default)
```

Executes one monitor cycle and returns the values of the previously registered devices (command 0x0802).

Parameters:
- `wordPoints`: Number of registered word devices. The combined count must be nonzero and within the active profile limit.
- `dwordPoints`: Number of registered DWord devices.
- `cancellationToken`: Cancellation token.

##### SelfTestLoopbackAsync

```csharp
public Task<byte[]> SelfTestLoopbackAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
```

Sends one self-test request and returns the echo only when declared length, actual length, and payload all match the supplied ASCII hexadecimal bytes.

##### WriteArrayLabelsAsync

```csharp
public Task WriteArrayLabelsAsync(IReadOnlyList<SlmpLabelArrayWritePoint> points, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

Writes array labels to the PLC (command 0x141A).

##### WriteBitBlocksAsync

```csharp
public Task WriteBitBlocksAsync(IReadOnlyList<SlmpBlockWrite> bitBlocks, CancellationToken cancellationToken = default)
```

Writes only bit blocks in one block-write request.

##### WriteBitsAsync

```csharp
public Task WriteBitsAsync(SlmpDeviceAddress device, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
```

##### WriteBitsExtendedAsync

```csharp
public Task WriteBitsExtendedAsync(SlmpQualifiedDeviceAddress device, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
```

##### WriteBlockAsync

```csharp
public Task WriteBlockAsync(IReadOnlyList<SlmpBlockWrite> wordBlocks, IReadOnlyList<SlmpBlockWrite> bitBlocks, CancellationToken cancellationToken = default)
```

##### WriteDWordsAsync

```csharp
public Task WriteDWordsAsync(SlmpDeviceAddress device, IReadOnlyList<uint> values, CancellationToken cancellationToken = default)
```

Writes contiguous 32-bit values in one Direct Write request.

##### WriteFloat32sAsync

```csharp
public Task WriteFloat32sAsync(SlmpDeviceAddress device, IReadOnlyList<float> values, CancellationToken cancellationToken = default)
```

Writes contiguous float32 values in one Direct Write request.

##### WriteRandomBitsAsync

```csharp
public Task WriteRandomBitsAsync(IReadOnlyList<ValueTuple<SlmpDeviceAddress, bool>> bitEntries, CancellationToken cancellationToken = default)
```

##### WriteRandomBitsExtAsync

```csharp
public Task WriteRandomBitsExtAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, bool>> bitEntries, CancellationToken cancellationToken = default)
```

##### WriteRandomLabelsAsync

```csharp
public Task WriteRandomLabelsAsync(IReadOnlyList<SlmpLabelRandomWritePoint> points, IReadOnlyList<string> abbreviationLabels = null, CancellationToken cancellationToken = default)
```

Writes random labels to the PLC (command 0x141B).

##### WriteRandomU16sAsync

```csharp
public Task WriteRandomU16sAsync(IReadOnlyList<ValueTuple<SlmpDeviceAddress, ushort>> wordEntries, CancellationToken cancellationToken = default)
```

Writes only 16-bit entries in one random-write request.

##### WriteRandomU16sExtendedAsync

```csharp
public Task WriteRandomU16sExtendedAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, ushort>> wordEntries, CancellationToken cancellationToken = default)
```

Writes only 16-bit entries through semantic Extended Device routes.

##### WriteRandomU32sAsync

```csharp
public Task WriteRandomU32sAsync(IReadOnlyList<ValueTuple<SlmpDeviceAddress, uint>> dwordEntries, CancellationToken cancellationToken = default)
```

Writes only 32-bit entries in one random-write request.

##### WriteRandomU32sExtendedAsync

```csharp
public Task WriteRandomU32sExtendedAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, uint>> dwordEntries, CancellationToken cancellationToken = default)
```

Writes only 32-bit entries through semantic Extended Device routes.

##### WriteRandomWordsAsync

```csharp
public Task WriteRandomWordsAsync(IReadOnlyList<ValueTuple<SlmpDeviceAddress, ushort>> wordEntries, IReadOnlyList<ValueTuple<SlmpDeviceAddress, uint>> dwordEntries, CancellationToken cancellationToken = default)
```

##### WriteRandomWordsExtAsync

```csharp
public Task WriteRandomWordsExtAsync(IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, ushort>> wordEntries, IReadOnlyList<ValueTuple<SlmpQualifiedDeviceAddress, uint>> dwordEntries, CancellationToken cancellationToken = default)
```

##### WriteWordBlocksAsync

```csharp
public Task WriteWordBlocksAsync(IReadOnlyList<SlmpBlockWrite> wordBlocks, CancellationToken cancellationToken = default)
```

Writes only word blocks in one block-write request.

##### WriteWordsAsync

```csharp
public Task WriteWordsAsync(SlmpDeviceAddress device, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
```

##### WriteWordsExtendedAsync

```csharp
public Task WriteWordsExtendedAsync(SlmpQualifiedDeviceAddress device, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
```

##### CompatibilityMode

```csharp
public SlmpCompatibilityMode CompatibilityMode { get; }
```

Gets the device access compatibility mode derived from `PlcProfile`.

##### FrameType

```csharp
public SlmpFrameType FrameType { get; }
```

Gets the SLMP frame format derived from `PlcProfile`.

##### IsOpen

```csharp
public bool IsOpen { get; }
```

Gets a value indicating whether the client is currently connected.

##### MonitoringTimer

```csharp
public ushort MonitoringTimer { get; set; }
```

Gets or sets the monitoring timer value (multiples of 250ms). Default is 0x0010 (4s).

##### PlcProfile

```csharp
public SlmpPlcProfile PlcProfile { get; }
```

Gets the PLC profile used to derive frame, compatibility, payload, and address behavior.

##### TargetAddress

```csharp
public SlmpTargetAddress TargetAddress { get; }
```

Gets the immutable destination routing information selected at construction.

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

Gets or sets the communication timeout. Values must be from 1 millisecond through `int.MaxValue` milliseconds.

##### TrafficStats

```csharp
public SlmpTrafficStats TrafficStats { get; }
```

Gets a read-only snapshot of cumulative traffic for this client lifetime.

### SlmpClientExtensions

```csharp
public static class SlmpClientExtensions
```

Extension methods for `SlmpClient` providing typed read/write helpers, single-request block access, named-device access, and polling.

Remarks: Typed, block, and named operations use one SLMP request unless the method explicitly documents a read-modify-write sequence. Named operations reject plans that require more than one request; polling performs a separate declared read cycle each interval. Typed, named, polling, long-timer, and bit-in-word helpers complete route, span, profile, and writable-target admission before waiting for the client FIFO.

#### Members

##### PollAsync

```csharp
public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(SlmpClient client, IEnumerable<string> addresses, TimeSpan interval, CancellationToken ct = default)
```

Continuously polls the specified logical snapshot at the requested interval.

Remarks: The address list, compact decode indexes, and immutable Random Read payload are validated and prepared once, then reused for every cycle. Each cycle retains the ordinary client FIFO, timeout, cancellation, close, and error contracts. This helper is suitable for periodic monitoring and historian ingestion.

Returns: An async stream of snapshot dictionaries.

Parameters:
- `client`: Connected SLMP client.
- `addresses`: Address list in the same format as `ReadNamedAsync`.
- `interval`: Delay between snapshots.
- `ct`: Cancellation token.

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

##### ReadNamedAsync

```csharp
public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(SlmpClient client, IEnumerable<string> addresses, CancellationToken ct = default)
```

Reads a mixed named value set and returns a dictionary keyed by the original addresses.

Remarks: The complete address list is compiled into exactly one random-read request. Entries that require another command family are rejected before transport. Use `ReadTypedAsync` or an explicit long-timer helper for LTN/LSTN current, contact, and coil routes.

Returns: A dictionary whose keys match the requested address strings.

Parameters:
- `client`: Connected SLMP client.
- `addresses`: Address list such as `D100:U`, `D200:F`, `D300:L`, `M1000:BIT`, or `D50.3`.
- `ct`: Cancellation token.

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

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(SlmpClient client, SlmpDeviceAddress device, int bitIndex, bool value, CancellationToken ct = default)
```

Performs a read-modify-write to set or clear one bit inside a word device.

Remarks: The read and write occupy one FIFO turn on this client, so its other operations cannot interleave. They remain two SLMP requests and are not PLC-atomic: another client, PLC logic, or external writer can change the word between them. Applications that require atomic coordination must implement it in the PLC contract. Bit-device packed-word access is not a bit-in-word operation and is rejected by this helper. Read and write admission both complete before FIFO waiting, so a read-only or wire-unrepresentable target sends neither request. One absolute deadline starts after FIFO admission and covers both requests. A successful read is always followed by the write, even when the selected bit is unchanged.

Parameters:
- `client`: Connected SLMP client.
- `device`: Word device address such as `D50`.
- `bitIndex`: Bit position within the word, from 0 to 15.
- `value`: New bit state.
- `ct`: Cancellation token.

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(SlmpClient client, SlmpQualifiedDeviceAddress device, int bitIndex, bool value, CancellationToken ct = default)
```

Performs the same explicit bit-in-word read-modify-write through one immutable qualified Extended Device route, including U module-buffer and J link-direct forms. Both requests use that exact route, occupy one FIFO turn and one absolute post-admission deadline, and the write is always sent after a successful read. The pair is not PLC-atomic and a possibly transmitted unconfirmed write is outcome unknown.

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(SlmpClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
```

Performs a read-modify-write using a string address.

Remarks: This overload has the same two-request, locally exclusive, non-PLC-atomic behavior as the typed-address overload.

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

##### WriteNamedAsync

```csharp
public static Task WriteNamedAsync(SlmpClient client, IReadOnlyDictionary<string, object> updates, CancellationToken ct = default)
```

Writes a mixed named value set by address string.

Remarks: The complete update set is sent as exactly one random-write request. Word and DWord entries may share that request; bit entries use one random-bit request. Mixing those command families or requesting bit-in-word read-modify-write is rejected before transport. The complete semantic plan is validated before FIFO admission.

Parameters:
- `client`: Connected SLMP client.
- `updates`: Mapping of address string to value, for example `"D100:U"`, `"D200:F"`, `"D50.3"`, or direct bit-device addresses such as `"M1000:BIT"`.
- `ct`: Cancellation token.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync(SlmpClient client, SlmpDeviceAddress device, string dtype, object value, CancellationToken ct = default)
```

Writes one logical value using strict dtype validation and encoding.

Remarks: Use this helper when application code wants strict typed writes without manually splitting words or packing float32 values. Values are not parsed from strings or converted between Boolean, floating, and integer types. Device unit, route, and value validation complete before FIFO admission.

Parameters:
- `client`: Connected SLMP client.
- `device`: Starting device address.
- `dtype`: Type code: `U` unsigned 16-bit, `S` signed 16-bit, `D` unsigned 32-bit, `L` signed 32-bit, or `F` float32.
- `value`: Value to encode and write. BIT requires Boolean; integer dtypes require an integral CLR type in range; F requires a finite numeric value within float32 range.
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

### SlmpClientFactory

```csharp
public static class SlmpClientFactory
```

Factory helpers for creating connected SLMP clients.

Remarks: This factory is the preferred high-level entry point for applications that want an already-connected client with explicit session settings captured by `SlmpConnectionOptions`.

#### Members

##### OpenAndConnectAsync

```csharp
public static Task<SlmpClient> OpenAndConnectAsync(SlmpConnectionOptions options, CancellationToken cancellationToken = default)
```

Creates, configures, and opens an SLMP client.

Remarks: The returned `SlmpClient` serializes complete operations through its arrival-order FIFO queue, including multi-step helpers.

Returns: A connected client with built-in FIFO operation admission.

Parameters:
- `options`: Explicit connection options.
- `cancellationToken`: Cancellation token.

### SlmpCommand

```csharp
public enum SlmpCommand
```

Standard SLMP command codes.

#### Members

##### ClearError

```csharp
public const SlmpCommand ClearError
```

##### DeviceRead

```csharp
public const SlmpCommand DeviceRead
```

##### DeviceReadBlock

```csharp
public const SlmpCommand DeviceReadBlock
```

##### DeviceReadRandom

```csharp
public const SlmpCommand DeviceReadRandom
```

##### DeviceWrite

```csharp
public const SlmpCommand DeviceWrite
```

##### DeviceWriteBlock

```csharp
public const SlmpCommand DeviceWriteBlock
```

##### DeviceWriteRandom

```csharp
public const SlmpCommand DeviceWriteRandom
```

##### ExtendUnitRead

```csharp
public const SlmpCommand ExtendUnitRead
```

##### ExtendUnitWrite

```csharp
public const SlmpCommand ExtendUnitWrite
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

##### Monitor

```csharp
public const SlmpCommand Monitor
```

##### MonitorRegister

```csharp
public const SlmpCommand MonitorRegister
```

##### ReadTypeName

```csharp
public const SlmpCommand ReadTypeName
```

##### RemoteLatchClear

```csharp
public const SlmpCommand RemoteLatchClear
```

##### RemotePasswordLock

```csharp
public const SlmpCommand RemotePasswordLock
```

##### RemotePasswordUnlock

```csharp
public const SlmpCommand RemotePasswordUnlock
```

##### RemotePause

```csharp
public const SlmpCommand RemotePause
```

##### RemoteReset

```csharp
public const SlmpCommand RemoteReset
```

##### RemoteRun

```csharp
public const SlmpCommand RemoteRun
```

##### RemoteStop

```csharp
public const SlmpCommand RemoteStop
```

##### SelfTest

```csharp
public const SlmpCommand SelfTest
```

### SlmpCompatibilityMode

```csharp
public enum SlmpCompatibilityMode
```

Specifies the device access subcommand compatibility mode.

#### Members

##### Iqr

```csharp
public const SlmpCompatibilityMode Iqr
```

Modern iQ-R series subcommands (0x0002/0x0003).

##### Legacy

```csharp
public const SlmpCompatibilityMode Legacy
```

Legacy Q/L series subcommands (0x0000/0x0001).

### SlmpConnectionClosedException

```csharp
public sealed class SlmpConnectionClosedException
```

Error thrown when `Close` retires the transport generation that owns an active or queued operation.

#### Members

##### SlmpConnectionClosedException

```csharp
public SlmpConnectionClosedException()
```

### SlmpConnectionOptions

```csharp
public sealed class SlmpConnectionOptions
```

Explicit connection options for a stable SLMP session profile.

Remarks: Use `PlcProfile` for the recommended high-level API. The library derives frame type, compatibility mode, string-address handling, and device-range handling from that explicit profile. This type is intended for the unified high-level entry point exposed by `OpenAndConnectAsync`.

#### Members

##### SlmpConnectionOptions

```csharp
public SlmpConnectionOptions(string Host, SlmpPlcProfile PlcProfile, int Port, SlmpTransportMode Transport, SlmpTargetAddress Target)
```

Explicit connection options for a stable SLMP session profile.

Remarks: Use `PlcProfile` for the recommended high-level API. The library derives frame type, compatibility mode, string-address handling, and device-range handling from that explicit profile. This type is intended for the unified high-level entry point exposed by `OpenAndConnectAsync`.

Parameters:
- `Host`: PLC IPv4 address or hostname that resolves to IPv4. IPv6 is not supported.
- `PlcProfile`: Canonical PLC profile for the high-level API.
- `Port`: PLC TCP or UDP port.
- `Transport`: Transport protocol.
- `Target`: Complete destination route.

##### Host

```csharp
public string Host { get; init; }
```

##### MonitoringTimer

```csharp
public ushort MonitoringTimer { get; init; }
```

Gets or initializes the SLMP monitoring timer value in 250 ms units.

Remarks: The monitoring timer is encoded into the request frame and tells the PLC how long it may spend processing the request before reporting a timeout.

##### PlcProfile

```csharp
public SlmpPlcProfile PlcProfile { get; init; }
```

Gets or initializes the canonical PLC profile for the high-level API.

##### Port

```csharp
public int Port { get; init; }
```

##### ResolvedAddressProfile

```csharp
public SlmpPlcProfile ResolvedAddressProfile { get; }
```

Gets the profile used for string device parsing.

##### ResolvedCompatibilityMode

```csharp
public SlmpCompatibilityMode ResolvedCompatibilityMode { get; }
```

Gets the effective compatibility mode after applying `PlcProfile` defaults.

##### ResolvedFrameType

```csharp
public SlmpFrameType ResolvedFrameType { get; }
```

Gets the effective frame type after applying `PlcProfile` defaults.

##### ResolvedRangeProfile

```csharp
public SlmpPlcProfile ResolvedRangeProfile { get; }
```

Gets the profile used by the high-level device-range helper layer.

##### Target

```csharp
public SlmpTargetAddress Target { get; init; }
```

Complete destination route.

##### Timeout

```csharp
public TimeSpan Timeout { get; init; }
```

Gets or initializes the deadline used to open the transport and complete each admitted request.

Remarks: An explicit open uses this value as its connection deadline. Each admitted request uses one absolute deadline from a lazy connection attempt through response receipt and decoding; partial progress does not restart the deadline. FIFO queue wait is excluded. Values must be from 1 millisecond through `int.MaxValue` milliseconds.

##### Transport

```csharp
public SlmpTransportMode Transport { get; init; }
```

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

##### RawCode

```csharp
public byte RawCode { get; init; }
```

Lower 4-bit masked status code from `SD203`.

##### RawStatusWord

```csharp
public ushort RawStatusWord { get; init; }
```

Full raw word read from `SD203`.

##### Status

```csharp
public SlmpCpuOperationStatus Status { get; init; }
```

Decoded PLC operation state.

### SlmpCpuOperationStatus

```csharp
public enum SlmpCpuOperationStatus
```

Decoded CPU operation state from the lower 4 bits of `SD203`.

#### Members

##### Pause

```csharp
public const SlmpCpuOperationStatus Pause
```

##### Run

```csharp
public const SlmpCpuOperationStatus Run
```

##### Stop

```csharp
public const SlmpCpuOperationStatus Stop
```

##### Unknown

```csharp
public const SlmpCpuOperationStatus Unknown
```

### SlmpDeviceAddress

```csharp
public struct SlmpDeviceAddress
```

Represents a specific PLC device and its numeric address.

#### Members

##### SlmpDeviceAddress

```csharp
public SlmpDeviceAddress(SlmpDeviceCode code, uint number, SlmpPlcProfile plcProfile)
```

Initializes and validates a profile-bound semantic device address.

##### ToString

```csharp
public virtual string ToString()
```

Returns the string representation of the device address (e.g., "D100").

##### Code

```csharp
public SlmpDeviceCode Code { get; }
```

Gets the device code.

##### Number

```csharp
public uint Number { get; }
```

Gets the wire-level numeric address.

##### PlcProfile

```csharp
public SlmpPlcProfile PlcProfile { get; }
```

Gets the canonical PLC profile bound to this address.

### SlmpDeviceCode

```csharp
public enum SlmpDeviceCode
```

Standard SLMP binary device codes.

#### Members

##### B

```csharp
public const SlmpDeviceCode B
```

Link Relay

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

##### CS

```csharp
public const SlmpDeviceCode CS
```

Counter Contact

##### D

```csharp
public const SlmpDeviceCode D
```

Data Register

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

##### F

```csharp
public const SlmpDeviceCode F
```

Annunciator

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

##### L

```csharp
public const SlmpDeviceCode L
```

Latch Relay

##### LCC

```csharp
public const SlmpDeviceCode LCC
```

Long Counter Coil

##### LCN

```csharp
public const SlmpDeviceCode LCN
```

Long Counter Current Value

##### LCS

```csharp
public const SlmpDeviceCode LCS
```

Long Counter Contact

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

##### LSTS

```csharp
public const SlmpDeviceCode LSTS
```

Long Retentive Timer Contact

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

##### LTS

```csharp
public const SlmpDeviceCode LTS
```

Long Timer Contact

##### LZ

```csharp
public const SlmpDeviceCode LZ
```

Long Index Register

##### M

```csharp
public const SlmpDeviceCode M
```

Internal Relay

##### R

```csharp
public const SlmpDeviceCode R
```

File Register

##### RD

```csharp
public const SlmpDeviceCode RD
```

Refresh Data Register

##### S

```csharp
public const SlmpDeviceCode S
```

Step Relay

##### SB

```csharp
public const SlmpDeviceCode SB
```

Link Special Relay

##### SD

```csharp
public const SlmpDeviceCode SD
```

Special Register

##### SM

```csharp
public const SlmpDeviceCode SM
```

Special Relay

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

##### STS

```csharp
public const SlmpDeviceCode STS
```

Retentive Timer Contact

##### SW

```csharp
public const SlmpDeviceCode SW
```

Link Special Register

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

##### TS

```csharp
public const SlmpDeviceCode TS
```

Timer Contact

##### V

```csharp
public const SlmpDeviceCode V
```

Edge Relay

##### W

```csharp
public const SlmpDeviceCode W
```

Link Register

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

##### Z

```csharp
public const SlmpDeviceCode Z
```

Index Register

##### ZR

```csharp
public const SlmpDeviceCode ZR
```

File Register (Continuous)

### SlmpDeviceModification

```csharp
public abstract class SlmpDeviceModification
```

Typed Extended Device modification.

### SlmpDeviceParser

```csharp
public static class SlmpDeviceParser
```

Utility for parsing device address strings into `SlmpDeviceAddress`.

#### Members

##### Parse

```csharp
public static SlmpDeviceAddress Parse(string text, SlmpPlcProfile plcProfile)
```

Parses a device string (e.g., "D100", "X1F") into a `SlmpDeviceAddress`.

Returns: A parsed device address object.

Parameters:
- `text`: The device string to parse.
- `plcProfile`: The canonical PLC profile that defines address interpretation.

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

##### Entries

```csharp
public IReadOnlyList<SlmpDeviceRangeEntry> Entries { get; init; }
```

Device entries for the resolved profile.

##### HasModelCode

```csharp
public bool HasModelCode { get; init; }
```

Always false because profile selection is explicit.

##### Model

```csharp
public string Model { get; init; }
```

Synthetic label for the explicitly selected PLC profile.

##### ModelCode

```csharp
public ushort ModelCode { get; init; }
```

Always zero because device-range catalogs do not infer profiles from type-name responses.

##### PlcProfile

```csharp
public SlmpPlcProfile PlcProfile { get; init; }
```

Resolved canonical PLC profile.

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

##### FileRegister

```csharp
public const SlmpDeviceRangeCategory FileRegister
```

##### Index

```csharp
public const SlmpDeviceRangeCategory Index
```

##### TimerCounter

```csharp
public const SlmpDeviceRangeCategory TimerCounter
```

##### Word

```csharp
public const SlmpDeviceRangeCategory Word
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

##### AddressRange

```csharp
public string AddressRange { get; init; }
```

Preformatted address range text such as `X000-X1FF` or `D0-D511`.

##### Category

```csharp
public SlmpDeviceRangeCategory Category { get; init; }
```

Logical category for grouping in monitor tools.

##### Device

```csharp
public string Device { get; init; }
```

Device code or address family string such as `D` or `TS`.

##### IsBitDevice

```csharp
public bool IsBitDevice { get; init; }
```

True when the device is bit-addressable in normal use.

##### LowerBound

```csharp
public uint LowerBound { get; init; }
```

Lower bound value. Current rules always use 0.

##### Notation

```csharp
public SlmpDeviceRangeNotation Notation { get; init; }
```

Recommended public address notation for this library.

##### Notes

```csharp
public string Notes { get; init; }
```

Optional profile-specific caveats.

##### PointCount

```csharp
public uint? PointCount { get; init; }
```

Usable point count read or resolved for the PLC profile. Null means no finite count is defined by the rule.

##### Source

```csharp
public string Source { get; init; }
```

Rule source used to build `UpperBound`.

##### Supported

```csharp
public bool Supported { get; init; }
```

True when the PLC profile supports this device.

##### UpperBound

```csharp
public uint? UpperBound { get; init; }
```

Inclusive last address. For a 0-based range this is `PointCount - 1`. Null means no finite bound is defined by the rule.

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

##### Base16

```csharp
public const SlmpDeviceRangeNotation Base16
```

##### Base8

```csharp
public const SlmpDeviceRangeNotation Base8
```

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

##### Command

```csharp
public SlmpCommand? Command { get; }
```

The SLMP command that triggered the error.

##### EndCode

```csharp
public ushort? EndCode { get; }
```

The end code returned by the PLC (0x0000 for success).

##### EndCodeName

```csharp
public string EndCodeName { get; }
```

Compact symbolic name for `EndCode`, or null when no end code is available.

##### ErrorInfo

```csharp
public SlmpErrorInfo ErrorInfo { get; }
```

Structured PLC error information from the response data, when present.

##### IsRemotePasswordError

```csharp
public bool IsRemotePasswordError { get; }
```

True when `EndCode` is a remote-password-related SLMP error.

##### Subcommand

```csharp
public ushort? Subcommand { get; }
```

The SLMP subcommand that triggered the error.

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
- `Raw`: Raw required 9-byte error-information prefix.

##### Parse

```csharp
public static SlmpErrorInfo Parse(ReadOnlySpan<byte> data)
```

Parse the required 9-byte SLMP error-information prefix and retain any following bytes, or return null when the complete prefix is not present.

##### Command

```csharp
public ushort Command { get; init; }
```

Command code associated with the PLC error.

##### Extra

```csharp
public byte[] Extra { get; init; }
```

Bytes following the required 9-byte error-information prefix.

##### ModuleIo

```csharp
public ushort ModuleIo { get; init; }
```

Module I/O number reported by the PLC.

##### Multidrop

```csharp
public byte Multidrop { get; init; }
```

Multidrop station number reported by the PLC.

##### Network

```csharp
public byte Network { get; init; }
```

Network number reported by the PLC.

##### Raw

```csharp
public byte[] Raw { get; init; }
```

Raw required 9-byte error-information prefix.

##### Station

```csharp
public byte Station { get; init; }
```

Station number reported by the PLC.

##### Subcommand

```csharp
public ushort Subcommand { get; init; }
```

Subcommand code associated with the PLC error.

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

##### ArrayDataLength

```csharp
public ushort ArrayDataLength { get; init; }
```

##### Label

```csharp
public string Label { get; init; }
```

##### UnitSpecification

```csharp
public byte UnitSpecification { get; init; }
```

### SlmpLabelArrayReadResult

```csharp
public sealed class SlmpLabelArrayReadResult
```

Result item returned by `ReadArrayLabelsAsync`. `Data` contains the protocol's two-byte-padded wire representation: bit units use `ceil(ArrayDataLength / 16) * 2` bytes and byte units use `ceil(ArrayDataLength / 2) * 2` bytes.

#### Members

##### SlmpLabelArrayReadResult

```csharp
public SlmpLabelArrayReadResult(byte DataTypeId, byte UnitSpecification, ushort ArrayDataLength, byte[] Data)
```

Result item returned by `ReadArrayLabelsAsync`. `Data` contains the protocol's two-byte-padded wire representation: bit units use `ceil(ArrayDataLength / 16) * 2` bytes and byte units use `ceil(ArrayDataLength / 2) * 2` bytes.

##### ArrayDataLength

```csharp
public ushort ArrayDataLength { get; init; }
```

##### Data

```csharp
public byte[] Data { get; init; }
```

##### DataTypeId

```csharp
public byte DataTypeId { get; init; }
```

##### UnitSpecification

```csharp
public byte UnitSpecification { get; init; }
```

### SlmpLabelArrayWritePoint

```csharp
public sealed class SlmpLabelArrayWritePoint
```

Describes one array label to write, including the raw wire data bytes.

Remarks: The PLC returns an end code when the unit does not match the configured label type.

#### Members

##### SlmpLabelArrayWritePoint

```csharp
public SlmpLabelArrayWritePoint(string Label, byte UnitSpecification, ushort ArrayDataLength, byte[] Data)
```

Describes one array label to write, including the raw wire data bytes.

Remarks: The PLC returns an end code when the unit does not match the configured label type.

Parameters:
- `Label`: Label name.
- `UnitSpecification`: Logical length unit: 0 for bits or 1 for bytes.
- `ArrayDataLength`: Logical length expressed in `UnitSpecification` units.
- `Data`: Raw data padded to a two-byte boundary. Its length must be exactly `ceil(ArrayDataLength / 16) * 2` for bit units or `ceil(ArrayDataLength / 2) * 2` for byte units.

##### ArrayDataLength

```csharp
public ushort ArrayDataLength { get; init; }
```

Logical length expressed in `UnitSpecification` units.

##### Data

```csharp
public byte[] Data { get; init; }
```

Raw data padded to a two-byte boundary. Its length must be exactly `ceil(ArrayDataLength / 16) * 2` for bit units or `ceil(ArrayDataLength / 2) * 2` for byte units.

##### Label

```csharp
public string Label { get; init; }
```

Label name.

##### UnitSpecification

```csharp
public byte UnitSpecification { get; init; }
```

Logical length unit: 0 for bits or 1 for bytes.

### SlmpLabelRandomReadResult

```csharp
public sealed class SlmpLabelRandomReadResult
```

Result item returned by `ReadRandomLabelsAsync`. `ReadDataLength` is a positive even wire-byte count, and `Spare` is preserved exactly as returned by the PLC.

#### Members

##### SlmpLabelRandomReadResult

```csharp
public SlmpLabelRandomReadResult(byte DataTypeId, byte Spare, ushort ReadDataLength, byte[] Data)
```

Result item returned by `ReadRandomLabelsAsync`. `ReadDataLength` is a positive even wire-byte count, and `Spare` is preserved exactly as returned by the PLC.

##### Data

```csharp
public byte[] Data { get; init; }
```

##### DataTypeId

```csharp
public byte DataTypeId { get; init; }
```

##### ReadDataLength

```csharp
public ushort ReadDataLength { get; init; }
```

##### Spare

```csharp
public byte Spare { get; init; }
```

### SlmpLabelRandomWritePoint

```csharp
public sealed class SlmpLabelRandomWritePoint
```

Describes one random label write point. `Data` must contain a positive, even number of raw wire bytes, including any required string terminator or padding.

#### Members

##### SlmpLabelRandomWritePoint

```csharp
public SlmpLabelRandomWritePoint(string Label, byte[] Data)
```

Describes one random label write point. `Data` must contain a positive, even number of raw wire bytes, including any required string terminator or padding.

##### Data

```csharp
public byte[] Data { get; init; }
```

##### Label

```csharp
public string Label { get; init; }
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

##### Coil

```csharp
public bool Coil { get; init; }
```

True when the timer coil is ON.

##### Contact

```csharp
public bool Contact { get; init; }
```

True when the timer contact is ON.

##### CurrentValue

```csharp
public uint CurrentValue { get; init; }
```

32-bit current value (two 16-bit words combined).

##### Device

```csharp
public string Device { get; init; }
```

The device address string (e.g. "LTN0").

##### Index

```csharp
public int Index { get; init; }
```

The device number (e.g. 0 for LTN0).

##### RawWords

```csharp
public ushort[] RawWords { get; init; }
```

The four raw 16-bit words that make up this timer entry.

##### StatusWord

```csharp
public ushort StatusWord { get; init; }
```

Raw status word (word index 2 in the 4-word block).

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

##### ControlSystemRemoteHead

```csharp
public const ushort ControlSystemRemoteHead
```

Control system remote head route.

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

##### OwnStation

```csharp
public const ushort OwnStation
```

Own station route.

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

##### StandbySystemCpu

```csharp
public const ushort StandbySystemCpu
```

Standby system CPU in a redundant CPU system.

##### StandbySystemRemoteHead

```csharp
public const ushort StandbySystemRemoteHead
```

Standby system remote head route.

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

##### DwordValues

```csharp
public uint[] DwordValues { get; init; }
```

32-bit values for the registered DWord devices (in registration order).

##### WordValues

```csharp
public ushort[] WordValues { get; init; }
```

16-bit word values for the registered word devices (in registration order).

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
public string Name { get; init; }
```

##### Target

```csharp
public SlmpTargetAddress Target { get; init; }
```

### SlmpNotConnectedException

```csharp
public sealed class SlmpNotConnectedException
```

Error thrown when an exchange requires an explicit open after transport retirement.

#### Members

##### SlmpNotConnectedException

```csharp
public SlmpNotConnectedException()
```

### SlmpOperationOutcomeUnknownException

```csharp
public sealed class SlmpOperationOutcomeUnknownException
```

Error thrown when a state-changing request may have reached the PLC but its final result is unknown.

#### Members

##### SlmpOperationOutcomeUnknownException

```csharp
public SlmpOperationOutcomeUnknownException(SlmpOutcomeUnknownReason reason, Exception innerException)
```

##### Reason

```csharp
public SlmpOutcomeUnknownReason Reason { get; }
```

Gets the structured reason the final PLC outcome could not be determined.

### SlmpOutcomeUnknownReason

```csharp
public enum SlmpOutcomeUnknownReason
```

Reason a state-changing request has an unknown PLC outcome.

#### Members

##### Cancellation

```csharp
public const SlmpOutcomeUnknownReason Cancellation
```

The caller canceled after request bytes may have been sent.

##### Closed

```csharp
public const SlmpOutcomeUnknownReason Closed
```

The client was closed after request bytes may have been sent.

##### MalformedResponse

```csharp
public const SlmpOutcomeUnknownReason MalformedResponse
```

A malformed PLC response occurred after request bytes may have been sent.

##### Timeout

```csharp
public const SlmpOutcomeUnknownReason Timeout
```

The single transaction deadline expired after request bytes may have been sent.

##### Transport

```csharp
public const SlmpOutcomeUnknownReason Transport
```

A transport failure occurred after request bytes may have been sent.

### SlmpPlcProfile

```csharp
public enum SlmpPlcProfile
```

Canonical PLC profile used by the high-level API.

#### Members

##### IqF

```csharp
public const SlmpPlcProfile IqF
```

##### IqL

```csharp
public const SlmpPlcProfile IqL
```

##### IqR

```csharp
public const SlmpPlcProfile IqR
```

##### IqRRj71En71

```csharp
public const SlmpPlcProfile IqRRj71En71
```

##### LCpu

```csharp
public const SlmpPlcProfile LCpu
```

##### LCpuLj71E71100

```csharp
public const SlmpPlcProfile LCpuLj71E71100
```

##### MxF

```csharp
public const SlmpPlcProfile MxF
```

##### MxR

```csharp
public const SlmpPlcProfile MxR
```

##### MxRRj71En71

```csharp
public const SlmpPlcProfile MxRRj71En71
```

##### QCpu

```csharp
public const SlmpPlcProfile QCpu
```

##### QCpuQj71E71100

```csharp
public const SlmpPlcProfile QCpuQj71E71100
```

##### QnU

```csharp
public const SlmpPlcProfile QnU
```

##### QnUDV

```csharp
public const SlmpPlcProfile QnUDV
```

##### QnUDVQj71E71100

```csharp
public const SlmpPlcProfile QnUDVQj71E71100
```

##### QnUQj71E71100

```csharp
public const SlmpPlcProfile QnUQj71E71100
```

##### Unspecified

```csharp
public const SlmpPlcProfile Unspecified
```

No PLC profile has been selected.

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

##### AddressProfile

```csharp
public SlmpPlcProfile AddressProfile { get; init; }
```

##### CompatibilityMode

```csharp
public SlmpCompatibilityMode CompatibilityMode { get; init; }
```

##### FrameType

```csharp
public SlmpFrameType FrameType { get; init; }
```

##### RangeProfile

```csharp
public SlmpPlcProfile RangeProfile { get; init; }
```

### SlmpPlcProfileDescriptor

```csharp
public sealed class SlmpPlcProfileDescriptor
```

Canonical metadata used to select and describe one PLC profile.

#### Members

##### SlmpPlcProfileDescriptor

```csharp
public SlmpPlcProfileDescriptor(string CanonicalName, string DisplayName, bool Connectable, string BaseProfile)
```

Canonical metadata used to select and describe one PLC profile.

##### BaseProfile

```csharp
public string BaseProfile { get; init; }
```

##### CanonicalName

```csharp
public string CanonicalName { get; init; }
```

##### Connectable

```csharp
public bool Connectable { get; init; }
```

##### DisplayName

```csharp
public string DisplayName { get; init; }
```

### SlmpPlcProfiles

```csharp
public static class SlmpPlcProfiles
```

Fixed high-level defaults driven by `SlmpPlcProfile`.

#### Members

##### AvailableProfiles

```csharp
public static IReadOnlyList<SlmpPlcProfile> AvailableProfiles()
```

Return the built-in profiles that can be used to open a connection.

##### GetDisplayName

```csharp
public static string GetDisplayName(SlmpPlcProfile profile)
```

Return the canonical human-readable display name for a PLC profile.

##### GetProfileDescriptors

```csharp
public static IReadOnlyList<SlmpPlcProfileDescriptor> GetProfileDescriptors()
```

Return all canonical profiles with display, connection, and base-profile metadata.

Remarks: The abstract `melsec:qcpu` entry is included with `Connectable` set to `false` so selectors can explain why it cannot be opened directly.

##### Parse

```csharp
public static SlmpPlcProfile Parse(string text)
```

Parse a canonical PLC profile string.

##### Resolve

```csharp
public static SlmpPlcProfileDefaults Resolve(SlmpPlcProfile profile)
```

Resolve the stable defaults for one explicit PLC profile.

##### ToCanonicalString

```csharp
public static string ToCanonicalString(SlmpPlcProfile profile)
```

Return the canonical string form used in user-facing configuration.

##### UsesIqFXyOctal

```csharp
public static bool UsesIqFXyOctal(SlmpPlcProfile profile)
```

True when `X` and `Y` strings must be parsed as octal.

##### UsesIqrProtocol

```csharp
public static bool UsesIqrProtocol(SlmpPlcProfile profile)
```

True when the selected profile uses iQ-R-compatible command subcommands and payloads.

##### ValidateConnectionProfile

```csharp
public static SlmpPlcProfile ValidateConnectionProfile(SlmpPlcProfile profile)
```

Validate that the profile can be used to open an SLMP connection.

### SlmpProfileFeatureException

```csharp
public sealed class SlmpProfileFeatureException
```

Error thrown before sending a high-level request when the selected PLC profile marks a feature as blocked or unverified.

#### Members

##### SlmpProfileFeatureException

```csharp
public SlmpProfileFeatureException(SlmpPlcProfile plcProfile, string featureKey, string state, string evidence)
```

##### Evidence

```csharp
public string Evidence { get; }
```

Evidence source or note that explains why the feature is guarded.

##### FeatureKey

```csharp
public string FeatureKey { get; }
```

Canonical feature key from the SLMP profile capability data.

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

##### State

```csharp
public string State { get; }
```

Canonical feature state, for example `blocked` or `unverified`.

### SlmpQualifiedDeviceAddress

```csharp
public struct SlmpQualifiedDeviceAddress
```

Represents a semantic Extended Device address. Protocol direct-memory bytes are derived internally.

#### Members

##### SlmpQualifiedDeviceAddress

```csharp
public SlmpQualifiedDeviceAddress(SlmpDeviceAddress device, ushort? extensionSpecification, SlmpDeviceModification modification = null)
```

##### Device

```csharp
public SlmpDeviceAddress Device { get; }
```

##### ExtensionSpecification

```csharp
public ushort? ExtensionSpecification { get; }
```

##### Modification

```csharp
public SlmpDeviceModification Modification { get; }
```

### SlmpQualifiedDeviceParser

```csharp
public static class SlmpQualifiedDeviceParser
```

Utility for parsing qualified device strings (e.g., "U01\G10", "J2\SW10") into `SlmpQualifiedDeviceAddress`.

#### Members

##### Parse

```csharp
public static SlmpQualifiedDeviceAddress Parse(string text, SlmpPlcProfile plcProfile)
```

Parses a qualified device string into a `SlmpQualifiedDeviceAddress`.

### SlmpRemoteClearMode

```csharp
public enum SlmpRemoteClearMode
```

Explicit device-clear policy for remote RUN.

#### Members

##### ClearAll

```csharp
public const SlmpRemoteClearMode ClearAll
```

##### ClearExceptLatch

```csharp
public const SlmpRemoteClearMode ClearExceptLatch
```

##### NoClear

```csharp
public const SlmpRemoteClearMode NoClear
```

### SlmpRemoteMode

```csharp
public enum SlmpRemoteMode
```

Explicit mode for remote RUN and PAUSE operations.

#### Members

##### Force

```csharp
public const SlmpRemoteMode Force
```

##### Normal

```csharp
public const SlmpRemoteMode Normal
```

### SlmpTargetAddress

```csharp
public struct SlmpTargetAddress
```

Represents the destination routing fields for an SLMP frame.

#### Members

##### SlmpTargetAddress

```csharp
public SlmpTargetAddress(byte Network, byte Station, ushort ModuleIo, byte Multidrop)
```

Represents the destination routing fields for an SLMP frame.

Parameters:
- `Network`: Network number (0x00 for local network).
- `Station`: Station number (0xFF for the connected station).
- `ModuleIo`: Module I/O number (0x03FF for own station).
- `Multidrop`: Multidrop station number (0x00 for no multidrop).

##### ModuleIo

```csharp
public ushort ModuleIo { get; init; }
```

Module I/O number (0x03FF for own station).

##### Multidrop

```csharp
public byte Multidrop { get; init; }
```

Multidrop station number (0x00 for no multidrop).

##### Network

```csharp
public byte Network { get; init; }
```

Network number (0x00 for local network).

##### OwnStation

```csharp
public static SlmpTargetAddress OwnStation { get; }
```

An explicit directly connected own-station route.

##### Station

```csharp
public byte Station { get; init; }
```

Station number (0xFF for the connected station).

### SlmpTargetParser

```csharp
public static class SlmpTargetParser
```

Utility for parsing target station descriptions into `SlmpNamedTarget`.

#### Members

##### ParseAutoNumber

```csharp
public static int ParseAutoNumber(string text)
```

Parses a number string, supporting both decimal and "0x" hexadecimal notation.

##### ParseMany

```csharp
public static IReadOnlyList<SlmpNamedTarget> ParseMany(IReadOnlyList<string> values)
```

Parses a list of target strings.

##### ParseNamed

```csharp
public static SlmpNamedTarget ParseNamed(string text)
```

Parses a single target string. Supports "SELF", "SELF-MULTIPLE-CPU-1..4", or "NAME,NETWORK,STATION,MODULE_IO,MULTIDROP".

### SlmpTimeoutException

```csharp
public sealed class SlmpTimeoutException
```

Error thrown when the configured connect or transaction deadline expires.

#### Members

##### SlmpTimeoutException

```csharp
public SlmpTimeoutException(string message, Exception innerException = null)
```

### SlmpTrafficStats

```csharp
public struct SlmpTrafficStats
```

Immutable lifetime traffic-counter snapshot for one SLMP client.

#### Members

##### SlmpTrafficStats

```csharp
public SlmpTrafficStats(ulong RequestCount, ulong TxBytes, ulong RxBytes)
```

Immutable lifetime traffic-counter snapshot for one SLMP client.

Parameters:
- `RequestCount`: Number of complete request frames accepted by the transport.
- `TxBytes`: Total bytes in complete request frames accepted by the transport.
- `RxBytes`: Total bytes in complete response frames or datagrams received.

##### RequestCount

```csharp
public ulong RequestCount { get; init; }
```

Number of complete request frames accepted by the transport.

##### RxBytes

```csharp
public ulong RxBytes { get; init; }
```

Total bytes in complete response frames or datagrams received.

##### TxBytes

```csharp
public ulong TxBytes { get; init; }
```

Total bytes in complete request frames accepted by the transport.

### SlmpTransportException

```csharp
public sealed class SlmpTransportException
```

Error thrown when IPv4 TCP/UDP connection or I/O fails.

#### Members

##### SlmpTransportException

```csharp
public SlmpTransportException(string message, Exception innerException = null)
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

##### HasModelCode

```csharp
public bool HasModelCode { get; init; }
```

True if the model code is valid.

##### Model

```csharp
public string Model { get; init; }
```

The model name string.

##### ModelCode

```csharp
public ushort ModelCode { get; init; }
```

Internal model code.
