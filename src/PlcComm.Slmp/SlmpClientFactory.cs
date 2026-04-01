namespace PlcComm.Slmp;

/// <summary>
/// Factory helpers for creating connected queued SLMP clients.
/// </summary>
/// <remarks>
/// This factory is the preferred high-level entry point for applications that want an
/// already-connected client with explicit session settings captured by
/// <see cref="SlmpConnectionOptions"/>.
/// </remarks>
public static class SlmpClientFactory
{
    /// <summary>
    /// Creates, configures, and opens a queued SLMP client.
    /// </summary>
    /// <param name="options">Explicit connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected queued client.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The host name is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured port is outside the valid TCP/UDP range.</exception>
    /// <remarks>
    /// The returned <see cref="QueuedSlmpClient"/> serializes multi-step operations through a single gate,
    /// which makes it suitable for documentation samples and shared-session application code.
    /// </remarks>
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
