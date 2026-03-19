using System.Threading;

namespace PlcComm.Slmp;

public sealed class QueuedSlmpClient : IAsyncDisposable, IDisposable
{
    private readonly SlmpClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public QueuedSlmpClient(SlmpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public SlmpClient InnerClient => _client;

    public SlmpFrameType FrameType
    {
        get => _client.FrameType;
        set => _client.FrameType = value;
    }

    public SlmpCompatibilityMode CompatibilityMode
    {
        get => _client.CompatibilityMode;
        set => _client.CompatibilityMode = value;
    }

    public SlmpTargetAddress TargetAddress
    {
        get => _client.TargetAddress;
        set => _client.TargetAddress = value;
    }

    public ushort MonitoringTimer
    {
        get => _client.MonitoringTimer;
        set => _client.MonitoringTimer = value;
    }

    public TimeSpan Timeout
    {
        get => _client.Timeout;
        set => _client.Timeout = value;
    }

    public bool IsOpen => _client.IsOpen;

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

    public Task<SlmpProfileRecommendation> ResolveProfileAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ResolveProfileAsync(cancellationToken), cancellationToken);

    public Task<SlmpTypeNameInfo> ReadTypeNameAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadTypeNameAsync(cancellationToken), cancellationToken);

    public Task<ushort[]> ReadWordsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadWordsAsync(device, points, cancellationToken), cancellationToken);

    public Task WriteWordsAsync(SlmpDeviceAddress device, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.WriteWordsAsync(device, values, cancellationToken), cancellationToken);

    public Task<bool[]> ReadBitsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadBitsAsync(device, points, cancellationToken), cancellationToken);

    public Task WriteBitsAsync(SlmpDeviceAddress device, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.WriteBitsAsync(device, values, cancellationToken), cancellationToken);

    public Task<uint[]> ReadDWordsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadDWordsAsync(device, points, cancellationToken), cancellationToken);

    public Task WriteDWordsAsync(SlmpDeviceAddress device, IReadOnlyList<uint> values, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.WriteDWordsAsync(device, values, cancellationToken), cancellationToken);

    public Task<(ushort[] WordValues, uint[] DwordValues)> ReadRandomAsync(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.ReadRandomAsync(wordDevices, dwordDevices, cancellationToken), cancellationToken);

    public Task WriteRandomWordsAsync(
        IReadOnlyList<(SlmpDeviceAddress Device, ushort Value)> wordEntries,
        IReadOnlyList<(SlmpDeviceAddress Device, uint Value)> dwordEntries,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.WriteRandomWordsAsync(wordEntries, dwordEntries, cancellationToken), cancellationToken);

    public Task WriteRandomBitsAsync(
        IReadOnlyList<(SlmpDeviceAddress Device, bool Value)> bitEntries,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.WriteRandomBitsAsync(bitEntries, cancellationToken), cancellationToken);

    public Task<(ushort[] WordValues, ushort[] BitWordValues)> ReadBlockAsync(
        IReadOnlyList<SlmpBlockRead> wordBlocks,
        IReadOnlyList<SlmpBlockRead> bitBlocks,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.ReadBlockAsync(wordBlocks, bitBlocks, cancellationToken), cancellationToken);

    public Task WriteBlockAsync(
        IReadOnlyList<SlmpBlockWrite> wordBlocks,
        IReadOnlyList<SlmpBlockWrite> bitBlocks,
        SlmpBlockWriteOptions? options = null,
        CancellationToken cancellationToken = default
    )
        => ExecuteAsync(client => client.WriteBlockAsync(wordBlocks, bitBlocks, options, cancellationToken), cancellationToken);

    public void Dispose()
    {
        _gate.Dispose();
        _client.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
