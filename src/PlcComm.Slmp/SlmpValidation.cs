using System.Net;
using System.Net.Sockets;

namespace PlcComm.Slmp;

internal static class SlmpValidation
{
    internal const int MaxRequestPayloadLength = ushort.MaxValue - 6;
    internal const int MaxIpv4UdpDatagramLength = ushort.MaxValue - 28;

    internal static string ValidateIpv4Host(string host, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(host, parameterName);
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host must not be empty.", parameterName);

        var normalized = host.Trim();
        if (IPAddress.TryParse(normalized, out var literal) && literal.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException(
                "Host must be an IPv4 address or a hostname that resolves to IPv4. IPv6 is not supported.",
                parameterName);
        }

        return normalized;
    }

    internal static async ValueTask<IPAddress> ResolveIpv4AddressAsync(
        string host,
        CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var literal))
            return literal.AddressFamily == AddressFamily.InterNetwork
                ? literal
                : throw new SlmpError($"Host is not an IPv4 address: {host}");

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            throw new SlmpError($"Host resolution failed: {host}", innerException: exception);
        }

        return SelectFirstIpv4Address(host, addresses);
    }

    internal static IPAddress SelectFirstIpv4Address(string host, IEnumerable<IPAddress> addresses)
    {
        foreach (var address in addresses)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
                return address;
        }

        throw new SlmpError($"Host did not resolve to an IPv4 address: {host}");
    }

    internal static TimeSpan ValidateTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout < TimeSpan.FromMilliseconds(1) || timeout > TimeSpan.FromMilliseconds(int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Timeout must be at least 1 millisecond and within the supported timer range.");
        }

        return timeout;
    }

    internal static int GetMaxRequestPayloadLength(
        SlmpTransportMode transportMode,
        SlmpFrameType frameType)
        => transportMode == SlmpTransportMode.Udp
            ? MaxIpv4UdpDatagramLength - (frameType == SlmpFrameType.Frame4E ? 19 : 15)
            : MaxRequestPayloadLength;

    internal static int ValidateRequestPayloadLength(
        long payloadLength,
        string parameterName,
        int maximumLength = MaxRequestPayloadLength)
    {
        if (payloadLength < 0 || payloadLength > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                payloadLength,
                $"SLMP request payload length actual={payloadLength} exceeds maximum={maximumLength} bytes.");
        }

        return (int)payloadLength;
    }

    internal static int AddRequestPayloadLength(
        int currentLength,
        long componentLength,
        string parameterName)
        => ValidateRequestPayloadLength((long)currentLength + componentLength, parameterName);
}
