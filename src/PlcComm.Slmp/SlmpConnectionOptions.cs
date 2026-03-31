namespace PlcComm.Slmp;

/// <summary>
/// Explicit connection options for a stable SLMP session profile.
/// </summary>
/// <param name="Host">PLC IP address or hostname.</param>
public sealed record SlmpConnectionOptions(string Host)
{
    /// <summary>Gets or sets the SLMP port number. Defaults to 1025.</summary>
    public int Port { get; init; } = 1025;

    /// <summary>Gets or sets the communication timeout.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Gets or sets the transport protocol.</summary>
    public SlmpTransportMode Transport { get; init; } = SlmpTransportMode.Tcp;

    /// <summary>Gets or sets the SLMP frame format.</summary>
    public SlmpFrameType FrameType { get; init; } = SlmpFrameType.Frame4E;

    /// <summary>Gets or sets the device access compatibility mode.</summary>
    public SlmpCompatibilityMode CompatibilityMode { get; init; } = SlmpCompatibilityMode.Iqr;

    /// <summary>Gets or sets the destination route.</summary>
    public SlmpTargetAddress Target { get; init; } = new(Station: 0xFF, ModuleIo: 0x03FF);

    /// <summary>Gets or sets the monitoring timer value in 250 ms units.</summary>
    public ushort MonitoringTimer { get; init; } = 0x0010;
}
