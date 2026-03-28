using System.Threading;

namespace PlcComm.Slmp;

/// <summary>
/// A wrapper for <see cref="SlmpClient"/> that serializes all operations using a semaphore.
/// Useful for environments where a single shared connection must handle multiple concurrent callers.
/// </summary>
public sealed class QueuedSlmpClient : IAsyncDisposable, IDisposable
{
    private readonly SlmpClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedSlmpClient"/> class.
    /// </summary>
    /// <param name="client">The underlying <see cref="SlmpClient"/> to wrap.</param>
    public QueuedSlmpClient(SlmpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>Gets the underlying client.</summary>
    public SlmpClient InnerClient => _client;

    /// <summary>Gets or sets the SLMP frame format (3E or 4E).</summary>
    public SlmpFrameType FrameType
    {
        get => _client.FrameType;
        set => _client.FrameType = value;
    }

    /// <summary>Gets or sets the device access compatibility mode (Legacy or iQ-R).</summary>
    public SlmpCompatibilityMode CompatibilityMode
    {
        get => _client.CompatibilityMode;
        set => _client.CompatibilityMode = value;
    }

    /// <summary>Gets or sets the destination routing information.</summary>
    public SlmpTargetAddress TargetAddress
    {
        get => _client.TargetAddress;
        set => _client.TargetAddress = value;
    }

    /// <summary>Gets or sets the monitoring timer value (multiples of 250ms).</summary>
    public ushort MonitoringTimer
    {
        get => _client.MonitoringTimer;
        set => _client.MonitoringTimer = value;
    }

    /// <summary>Gets or sets the communication timeout.</summary>
    public TimeSpan Timeout
    {
        get => _client.Timeout;
        set => _client.Timeout = value;
    }

    /// <summary>Gets a value indicating whether the client is currently connected.</summary>
    public bool IsOpen => _client.IsOpen;

    /// <summary>
    /// Opens the connection asynchronously, ensuring exclusive access during the operation.
    /// </summary>
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _client.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Executes a custom operation on the underlying client with exclusive access.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<SlmpClient, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation(_client).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Executes a custom action on the underlying client with exclusive access.
    /// </summary>
    public async Task ExecuteAsync(Func<SlmpClient, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation(_client).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc cref="SlmpClient.ReadTypeNameAsync"/>
    public Task<SlmpTypeNameInfo> ReadTypeNameAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadTypeNameAsync(cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadWordsRawAsync"/>
    public Task<ushort[]> ReadWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadWordsRawAsync(device, points, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteWordsAsync"/>
    public Task WriteWordsAsync(SlmpDeviceAddress device, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.WriteWordsAsync(device, values, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadBitsAsync"/>
    public Task<bool[]> ReadBitsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadBitsAsync(device, points, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteBitsAsync"/>
    public Task WriteBitsAsync(SlmpDeviceAddress device, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.WriteBitsAsync(device, values, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadDWordsRawAsync"/>
    public Task<uint[]> ReadDWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadDWordsRawAsync(device, points, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteDWordsAsync"/>
    public Task WriteDWordsAsync(SlmpDeviceAddress device, IReadOnlyList<uint> values, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.WriteDWordsAsync(device, values, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadFloat32sAsync"/>
    public Task<float[]> ReadFloat32sAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadFloat32sAsync(device, points, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteFloat32sAsync"/>
    public Task WriteFloat32sAsync(SlmpDeviceAddress device, IReadOnlyList<float> values, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.WriteFloat32sAsync(device, values, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadRandomAsync"/>
    public Task<(ushort[] WordValues, uint[] DwordValues)> ReadRandomAsync(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.ReadRandomAsync(wordDevices, dwordDevices, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteRandomWordsAsync"/>
    public Task WriteRandomWordsAsync(
        IReadOnlyList<(SlmpDeviceAddress Device, ushort Value)> wordEntries,
        IReadOnlyList<(SlmpDeviceAddress Device, uint Value)> dwordEntries,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.WriteRandomWordsAsync(wordEntries, dwordEntries, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteRandomBitsAsync"/>
    public Task WriteRandomBitsAsync(
        IReadOnlyList<(SlmpDeviceAddress Device, bool Value)> bitEntries,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.WriteRandomBitsAsync(bitEntries, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadBlockAsync"/>
    public Task<(ushort[] WordValues, ushort[] BitWordValues)> ReadBlockAsync(
        IReadOnlyList<SlmpBlockRead> wordBlocks,
        IReadOnlyList<SlmpBlockRead> bitBlocks,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.ReadBlockAsync(wordBlocks, bitBlocks, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteBlockAsync"/>
    public Task WriteBlockAsync(
        IReadOnlyList<SlmpBlockWrite> wordBlocks,
        IReadOnlyList<SlmpBlockWrite> bitBlocks,
        SlmpBlockWriteOptions? options = null,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.WriteBlockAsync(wordBlocks, bitBlocks, options, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadBitsExtendedAsync"/>
    public Task<bool[]> ReadBitsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        ushort points,
        SlmpExtensionSpec extension,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.ReadBitsExtendedAsync(device, points, extension, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteBitsExtendedAsync"/>
    public Task WriteBitsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        IReadOnlyList<bool> values,
        SlmpExtensionSpec extension,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.WriteBitsExtendedAsync(device, values, extension, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadWordsExtendedAsync"/>
    public Task<ushort[]> ReadWordsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        ushort points,
        SlmpExtensionSpec extension,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.ReadWordsExtendedAsync(device, points, extension, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteWordsExtendedAsync"/>
    public Task WriteWordsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        IReadOnlyList<ushort> values,
        SlmpExtensionSpec extension,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.WriteWordsExtendedAsync(device, values, extension, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadRandomExtAsync"/>
    public Task<(ushort[] WordValues, uint[] DwordValues)> ReadRandomExtAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> wordDevices,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> dwordDevices,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.ReadRandomExtAsync(wordDevices, dwordDevices, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteRandomWordsExtAsync"/>
    public Task WriteRandomWordsExtAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, ushort Value, SlmpExtensionSpec Extension)> wordEntries,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, uint Value, SlmpExtensionSpec Extension)> dwordEntries,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.WriteRandomWordsExtAsync(wordEntries, dwordEntries, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteRandomBitsExtAsync"/>
    public Task WriteRandomBitsExtAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, bool Value, SlmpExtensionSpec Extension)> bitEntries,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.WriteRandomBitsExtAsync(bitEntries, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadLongTimerAsync"/>
    public Task<SlmpLongTimerResult[]> ReadLongTimerAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadLongTimerAsync(headNo, points, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadLongRetentiveTimerAsync"/>
    public Task<SlmpLongTimerResult[]> ReadLongRetentiveTimerAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadLongRetentiveTimerAsync(headNo, points, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadLtcStatesAsync"/>
    public Task<bool[]> ReadLtcStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadLtcStatesAsync(headNo, points, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadLtsStatesAsync"/>
    public Task<bool[]> ReadLtsStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadLtsStatesAsync(headNo, points, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadLstcStatesAsync"/>
    public Task<bool[]> ReadLstcStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadLstcStatesAsync(headNo, points, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadLstsStatesAsync"/>
    public Task<bool[]> ReadLstsStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadLstsStatesAsync(headNo, points, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadArrayLabelsAsync"/>
    public Task<SlmpLabelArrayReadResult[]> ReadArrayLabelsAsync(
        IReadOnlyList<SlmpLabelArrayReadPoint> points,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadArrayLabelsAsync(points, abbreviationLabels, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteArrayLabelsAsync"/>
    public Task WriteArrayLabelsAsync(
        IReadOnlyList<SlmpLabelArrayWritePoint> points,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.WriteArrayLabelsAsync(points, abbreviationLabels, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ReadRandomLabelsAsync"/>
    public Task<SlmpLabelRandomReadResult[]> ReadRandomLabelsAsync(
        IReadOnlyList<string> labels,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadRandomLabelsAsync(labels, abbreviationLabels, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.WriteRandomLabelsAsync"/>
    public Task WriteRandomLabelsAsync(
        IReadOnlyList<SlmpLabelRandomWritePoint> points,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.WriteRandomLabelsAsync(points, abbreviationLabels, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.MemoryReadWordsAsync"/>
    public Task<ushort[]> MemoryReadWordsAsync(uint headAddress, ushort wordLength, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.MemoryReadWordsAsync(headAddress, wordLength, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.MemoryWriteWordsAsync"/>
    public Task MemoryWriteWordsAsync(uint headAddress, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.MemoryWriteWordsAsync(headAddress, values, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ExtendUnitReadBytesAsync"/>
    public Task<byte[]> ExtendUnitReadBytesAsync(uint headAddress, ushort byteLength, ushort moduleNo, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ExtendUnitReadBytesAsync(headAddress, byteLength, moduleNo, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ExtendUnitReadWordsAsync"/>
    public Task<ushort[]> ExtendUnitReadWordsAsync(uint headAddress, ushort wordLength, ushort moduleNo, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ExtendUnitReadWordsAsync(headAddress, wordLength, moduleNo, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ExtendUnitReadWordAsync"/>
    public Task<ushort> ExtendUnitReadWordAsync(uint headAddress, ushort moduleNo, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ExtendUnitReadWordAsync(headAddress, moduleNo, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ExtendUnitReadDWordAsync"/>
    public Task<uint> ExtendUnitReadDWordAsync(uint headAddress, ushort moduleNo, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ExtendUnitReadDWordAsync(headAddress, moduleNo, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ExtendUnitWriteBytesAsync"/>
    public Task ExtendUnitWriteBytesAsync(uint headAddress, ushort moduleNo, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ExtendUnitWriteBytesAsync(headAddress, moduleNo, data, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ExtendUnitWriteWordsAsync"/>
    public Task ExtendUnitWriteWordsAsync(uint headAddress, ushort moduleNo, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ExtendUnitWriteWordsAsync(headAddress, moduleNo, values, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ExtendUnitWriteWordAsync"/>
    public Task ExtendUnitWriteWordAsync(uint headAddress, ushort moduleNo, ushort value, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ExtendUnitWriteWordAsync(headAddress, moduleNo, value, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.ExtendUnitWriteDWordAsync"/>
    public Task ExtendUnitWriteDWordAsync(uint headAddress, ushort moduleNo, uint value, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ExtendUnitWriteDWordAsync(headAddress, moduleNo, value, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.CpuBufferReadWordsAsync"/>
    public Task<ushort[]> CpuBufferReadWordsAsync(uint headAddress, ushort wordLength, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.CpuBufferReadWordsAsync(headAddress, wordLength, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.CpuBufferReadBytesAsync"/>
    public Task<byte[]> CpuBufferReadBytesAsync(uint headAddress, ushort byteLength, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.CpuBufferReadBytesAsync(headAddress, byteLength, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.CpuBufferReadWordAsync"/>
    public Task<ushort> CpuBufferReadWordAsync(uint headAddress, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.CpuBufferReadWordAsync(headAddress, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.CpuBufferReadDWordAsync"/>
    public Task<uint> CpuBufferReadDWordAsync(uint headAddress, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.CpuBufferReadDWordAsync(headAddress, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.CpuBufferWriteWordsAsync"/>
    public Task CpuBufferWriteWordsAsync(uint headAddress, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.CpuBufferWriteWordsAsync(headAddress, values, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.CpuBufferWriteBytesAsync"/>
    public Task CpuBufferWriteBytesAsync(uint headAddress, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.CpuBufferWriteBytesAsync(headAddress, data, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.CpuBufferWriteWordAsync"/>
    public Task CpuBufferWriteWordAsync(uint headAddress, ushort value, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.CpuBufferWriteWordAsync(headAddress, value, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.CpuBufferWriteDWordAsync"/>
    public Task CpuBufferWriteDWordAsync(uint headAddress, uint value, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.CpuBufferWriteDWordAsync(headAddress, value, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.RegisterMonitorDevicesAsync"/>
    public Task RegisterMonitorDevicesAsync(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.RegisterMonitorDevicesAsync(wordDevices, dwordDevices, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.RegisterMonitorDevicesExtAsync"/>
    public Task RegisterMonitorDevicesExtAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> wordDevices,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> dwordDevices,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.RegisterMonitorDevicesExtAsync(wordDevices, dwordDevices, cancellationToken), cancellationToken);

    /// <inheritdoc cref="SlmpClient.RunMonitorCycleAsync"/>
    public Task<SlmpMonitorResult> RunMonitorCycleAsync(int wordPoints, int dwordPoints, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.RunMonitorCycleAsync(wordPoints, dwordPoints, cancellationToken), cancellationToken);

    /// <summary>Disposes the client and releases resources.</summary>
    public void Dispose()
    {
        _gate.Dispose();
        _client.Dispose();
    }

    /// <summary>Disposes the client asynchronously.</summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
