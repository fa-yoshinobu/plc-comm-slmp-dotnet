namespace PlcComm.Slmp;

/// <summary>
/// Explicit connection options for a stable SLMP session profile.
/// </summary>
/// <remarks>
/// Use <see cref="PlcProfile"/> for the recommended high-level API. The library derives
/// frame type, compatibility mode, string-address handling, and device-range handling
/// from that explicit profile.
/// This type is intended for the unified high-level entry point exposed by
/// <see cref="SlmpClientFactory.OpenAndConnectAsync(SlmpConnectionOptions, CancellationToken)"/>.
/// </remarks>
/// <param name="Host">PLC IP address or hostname.</param>
/// <param name="PlcProfile">Canonical PLC profile for the high-level API.</param>
public sealed record SlmpConnectionOptions(string Host, SlmpPlcProfile PlcProfile)
{
    private SlmpPlcProfile _plcProfile = ValidatePlcProfile(PlcProfile);

    /// <summary>Gets or sets the canonical PLC profile for the high-level API.</summary>
    public SlmpPlcProfile PlcProfile
    {
        get => _plcProfile;
        init => _plcProfile = ValidatePlcProfile(value);
    }

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

    /// <summary>
    /// Gets or sets whether high-level APIs reject profile-blocked or unverified features before transport.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true"/>. Limits and write policies are always enforced.
    /// Set this to <see langword="false"/> only when intentionally probing a PLC feature.
    /// </remarks>
    public bool StrictProfile { get; init; } = true;

    /// <summary>Gets the effective frame type after applying <see cref="PlcProfile"/> defaults.</summary>
    public SlmpFrameType ResolvedFrameType => SlmpPlcProfiles.Resolve(PlcProfile).FrameType;

    /// <summary>Gets the effective compatibility mode after applying <see cref="PlcProfile"/> defaults.</summary>
    public SlmpCompatibilityMode ResolvedCompatibilityMode => SlmpPlcProfiles.Resolve(PlcProfile).CompatibilityMode;

    /// <summary>Gets the profile used for string device parsing.</summary>
    public SlmpPlcProfile ResolvedAddressProfile => SlmpPlcProfiles.Resolve(PlcProfile).AddressProfile;

    /// <summary>Gets the profile used by the high-level device-range helper layer.</summary>
    public SlmpPlcProfile ResolvedRangeProfile => SlmpPlcProfiles.Resolve(PlcProfile).RangeProfile;

    private static SlmpPlcProfile ValidatePlcProfile(SlmpPlcProfile profile)
    {
        return SlmpPlcProfiles.ValidateConnectionProfile(profile);
    }
}
