namespace PlcComm.Slmp;

/// <summary>
/// Explicit connection options for a stable SLMP session profile.
/// </summary>
/// <remarks>
/// Use this type when you want the chosen frame type, compatibility mode, target route,
/// and timeout to remain explicit in generated documentation and call sites.
/// It is intended for the unified high-level entry point exposed by
/// <see cref="SlmpClientFactory.OpenAndConnectAsync(SlmpConnectionOptions, CancellationToken)"/>.
/// </remarks>
/// <param name="Host">PLC IP address or hostname.</param>
public sealed record SlmpConnectionOptions(string Host)
{
    /// <summary>Gets or sets the SLMP port number.</summary>
    /// <remarks>The default SLMP TCP/UDP port is <c>1025</c>.</remarks>
    public int Port { get; init; } = 1025;

    /// <summary>Gets or sets the communication timeout for the underlying transport.</summary>
    /// <remarks>
    /// This timeout applies to individual request/response exchanges after the session is opened.
    /// </remarks>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Gets or sets the transport protocol used for the session.</summary>
    /// <remarks>SLMP typically uses TCP for stable sessions and UDP for lightweight request patterns.</remarks>
    public SlmpTransportMode Transport { get; init; } = SlmpTransportMode.Tcp;

    /// <summary>Gets or sets the SLMP frame format.</summary>
    /// <remarks>Choose between 3E and 4E explicitly instead of relying on automatic fallback behavior.</remarks>
    public SlmpFrameType FrameType { get; init; } = SlmpFrameType.Frame4E;

    /// <summary>Gets or sets the device access compatibility mode.</summary>
    /// <remarks>
    /// This controls how device names are interpreted for legacy MELSEC-compatible layouts
    /// versus iQ-R style layouts.
    /// </remarks>
    public SlmpCompatibilityMode CompatibilityMode { get; init; } = SlmpCompatibilityMode.Iqr;

    /// <summary>Gets or sets the destination route.</summary>
    /// <remarks>
    /// The default value targets the directly connected local CPU.
    /// Override this when routing through a specific network, station, or module path.
    /// </remarks>
    public SlmpTargetAddress Target { get; init; } = new(Station: 0xFF, ModuleIo: 0x03FF);

    /// <summary>Gets or sets the SLMP monitoring timer value in 250 ms units.</summary>
    /// <remarks>
    /// The monitoring timer is encoded into the request frame and tells the PLC how long
    /// it may spend processing the request before reporting a timeout.
    /// </remarks>
    public ushort MonitoringTimer { get; init; } = 0x0010;
}
