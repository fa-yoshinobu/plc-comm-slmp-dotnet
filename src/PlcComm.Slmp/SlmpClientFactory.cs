namespace PlcComm.Slmp;

/// <summary>
/// Factory helpers for creating connected queued SLMP clients.
/// </summary>
public static class SlmpClientFactory
{
    /// <summary>
    /// Creates, configures, and opens a queued SLMP client.
    /// </summary>
    /// <param name="options">Explicit connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected queued client.</returns>
    public static async Task<QueuedSlmpClient> OpenAndConnectAsync(
        SlmpConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new ArgumentException("Host must not be empty.", nameof(options));
        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be in the range 1-65535.");

        var inner = new SlmpClient(options.Host, options.Port, options.Transport)
        {
            Timeout = options.Timeout,
            FrameType = options.FrameType,
            CompatibilityMode = options.CompatibilityMode,
            TargetAddress = options.Target,
            MonitoringTimer = options.MonitoringTimer,
        };

        var queued = new QueuedSlmpClient(inner);
        await queued.OpenAsync(cancellationToken).ConfigureAwait(false);
        return queued;
    }
}
