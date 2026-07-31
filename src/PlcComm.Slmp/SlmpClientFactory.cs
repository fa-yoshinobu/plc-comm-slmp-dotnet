namespace PlcComm.Slmp;

/// <summary>
/// Factory helpers for creating connected SLMP clients.
/// </summary>
/// <remarks>
/// This factory is the preferred high-level entry point for applications that want an
/// already-connected client with explicit session settings captured by
/// <see cref="SlmpConnectionOptions"/>.
/// </remarks>
public static class SlmpClientFactory
{
    /// <summary>
    /// Creates, configures, and opens an SLMP client.
    /// </summary>
    /// <param name="options">Explicit connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected client with built-in FIFO operation admission.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The host is empty, whitespace, or an IPv6 literal.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured port is outside the valid TCP/UDP range.</exception>
    /// <remarks>
    /// The returned <see cref="SlmpClient"/> serializes complete operations through its
    /// arrival-order FIFO queue, including multi-step helpers.
    /// </remarks>
    public static async Task<SlmpClient> OpenAndConnectAsync(
        SlmpConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new ArgumentException("Host must not be empty.", nameof(options));
        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be in the range 1-65535.");
        if (!Enum.IsDefined(options.Transport))
            throw new ArgumentOutOfRangeException(nameof(options), "Transport must be TCP or UDP.");
        _ = SlmpValidation.ValidateTimeout(options.Timeout, nameof(options));

        var client = new SlmpClient(options.Host, options.PlcProfile, options.Port, options.Transport, options.Target)
        {
            Timeout = options.Timeout,
            MonitoringTimer = options.MonitoringTimer,
        };

        await client.OpenAsync(cancellationToken).ConfigureAwait(false);
        return client;
    }
}
