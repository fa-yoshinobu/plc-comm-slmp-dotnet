namespace PlcComm.Slmp;

/// <summary>
/// Explicit connection options for a stable SLMP session profile.
/// </summary>
/// <remarks>
/// Use <see cref="PlcFamily"/> for the recommended high-level API. The library derives
/// frame type, compatibility mode, string-address handling, and device-range handling
/// from that explicit family.
/// This type is intended for the unified high-level entry point exposed by
/// <see cref="SlmpClientFactory.OpenAndConnectAsync(SlmpConnectionOptions, CancellationToken)"/>.
/// </remarks>
/// <param name="Host">PLC IP address or hostname.</param>
/// <param name="PlcFamily">Canonical PLC family for the high-level API.</param>
public sealed record SlmpConnectionOptions(string Host, SlmpPlcFamily PlcFamily)
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

    /// <summary>Gets the effective frame type after applying <see cref="PlcFamily"/> defaults.</summary>
    public SlmpFrameType ResolvedFrameType => SlmpPlcFamilyProfiles.Resolve(PlcFamily).FrameType;

    /// <summary>Gets the effective compatibility mode after applying <see cref="PlcFamily"/> defaults.</summary>
    public SlmpCompatibilityMode ResolvedCompatibilityMode => SlmpPlcFamilyProfiles.Resolve(PlcFamily).CompatibilityMode;

    /// <summary>Gets the address family used for string device parsing.</summary>
    public SlmpPlcFamily ResolvedAddressFamily => SlmpPlcFamilyProfiles.Resolve(PlcFamily).AddressFamily;

    /// <summary>Gets the device-range family used by the high-level helper layer.</summary>
    public SlmpDeviceRangeFamily ResolvedRangeFamily => SlmpPlcFamilyProfiles.Resolve(PlcFamily).RangeFamily;
}
