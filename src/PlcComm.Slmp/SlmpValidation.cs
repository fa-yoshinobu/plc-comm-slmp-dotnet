namespace PlcComm.Slmp;

internal static class SlmpValidation
{
    internal const int MaxRequestPayloadLength = ushort.MaxValue - 6;
    internal const int MaxIpv4UdpDatagramLength = ushort.MaxValue - 28;

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
