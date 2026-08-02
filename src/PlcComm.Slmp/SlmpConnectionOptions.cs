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
/// <param name="Host">PLC IPv4 address or hostname that resolves to IPv4. IPv6 is not supported.</param>
/// <param name="PlcProfile">Canonical PLC profile for the high-level API.</param>
/// <param name="Port">PLC TCP or UDP port.</param>
/// <param name="Transport">Transport protocol.</param>
/// <param name="Target">Complete destination route.</param>
public sealed record SlmpConnectionOptions(
    string Host,
    SlmpPlcProfile PlcProfile,
    int Port,
    SlmpTransportMode Transport,
    SlmpTargetAddress Target)
{
    private string _host = ValidateHost(Host);
    private SlmpPlcProfile _plcProfile = ValidatePlcProfile(PlcProfile);
    private int _port = ValidatePort(Port);
    private SlmpTransportMode _transport = ValidateTransport(Transport);
    private TimeSpan _timeout = TimeSpan.FromSeconds(3);

    public string Host
    {
        get => _host;
        init => _host = ValidateHost(value);
    }

    /// <summary>Gets or initializes the canonical PLC profile for the high-level API.</summary>
    public SlmpPlcProfile PlcProfile
    {
        get => _plcProfile;
        init => _plcProfile = ValidatePlcProfile(value);
    }

    public int Port
    {
        get => _port;
        init => _port = ValidatePort(value);
    }

    public SlmpTransportMode Transport
    {
        get => _transport;
        init => _transport = ValidateTransport(value);
    }

    /// <summary>Gets or initializes the deadline used to open the transport and complete each admitted request.</summary>
    /// <remarks>
    /// An explicit open uses this value as its connection deadline. Each admitted request uses one
    /// absolute deadline from a lazy connection attempt through response receipt and decoding;
    /// partial progress does not restart the deadline. FIFO queue wait is excluded.
    /// Values must be from 1 millisecond through <c>int.MaxValue</c> milliseconds.
    /// </remarks>
    public TimeSpan Timeout
    {
        get => _timeout;
        init => _timeout = SlmpValidation.ValidateTimeout(value, nameof(Timeout));
    }

    /// <summary>Gets or initializes the SLMP monitoring timer value in 250 ms units.</summary>
    /// <remarks>
    /// The monitoring timer is encoded into the request frame and tells the PLC how long
    /// it may spend processing the request before reporting a timeout.
    /// </remarks>
    public ushort MonitoringTimer { get; init; } = 0x0010;

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

    private static string ValidateHost(string host)
        => SlmpValidation.ValidateIpv4Host(host, nameof(host));

    private static int ValidatePort(int port)
        => port is >= 1 and <= 65535
            ? port
            : throw new ArgumentOutOfRangeException(nameof(port));

    private static SlmpTransportMode ValidateTransport(SlmpTransportMode transport)
        => Enum.IsDefined(transport)
            ? transport
            : throw new ArgumentOutOfRangeException(nameof(transport));

}
