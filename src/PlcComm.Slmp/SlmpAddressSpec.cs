using System.Globalization;

namespace PlcComm.Slmp;

/// <summary>
/// Represents one high-level SLMP address expression with an explicit data type or bit selection.
/// </summary>
/// <remarks>
/// Use <see cref="SlmpAddress"/> for direct device text such as <c>D100</c> or <c>X10</c>.
/// This type accepts only high-level expressions such as <c>D100:U</c> or <c>D50.A</c>.
/// Qualified routes such as <c>J1\X10</c> and <c>U0\G100</c> are not direct devices and are rejected.
/// </remarks>
public sealed class SlmpAddressSpec
{
    private SlmpAddressSpec(SlmpDeviceAddress deviceAddress, string dtype, int? bitIndex)
    {
        DeviceAddress = deviceAddress;
        DType = dtype;
        BitIndex = bitIndex;
    }

    /// <summary>Gets the profile-bound direct device portion of the expression.</summary>
    public SlmpDeviceAddress DeviceAddress { get; }

    /// <summary>
    /// Gets the canonical data type: <c>BIT</c>, <c>U</c>, <c>S</c>, <c>D</c>, <c>L</c>,
    /// <c>F</c>, or <c>BIT_IN_WORD</c> when <see cref="BitIndex"/> is present.
    /// </summary>
    public string DType { get; }

    /// <summary>Gets the selected bit index (0 through 15), or <see langword="null"/> for a typed expression.</summary>
    public int? BitIndex { get; }

    /// <summary>Parses one high-level address expression using the explicit PLC profile.</summary>
    public static SlmpAddressSpec Parse(string text, SlmpPlcProfile plcProfile)
    {
        ArgumentNullException.ThrowIfNull(text);
        var (baseAddress, dtype, bitIndex) = SlmpClientExtensions.ParseAddress(text);
        var device = SlmpAddress.Parse(baseAddress, plcProfile);

        if (bitIndex is int bit)
        {
            SlmpClientExtensions.ValidateBitInWordTarget(text, device);
            return new SlmpAddressSpec(device, "BIT_IN_WORD", bit);
        }

        SlmpClientExtensions.ValidateNamedDeviceDType(text, device, dtype);
        return new SlmpAddressSpec(device, dtype, null);
    }

    /// <summary>Attempts to parse one high-level address expression using the explicit PLC profile.</summary>
    public static bool TryParse(string text, SlmpPlcProfile plcProfile, out SlmpAddressSpec? addressSpec)
    {
        try
        {
            addressSpec = Parse(text, plcProfile);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException)
        {
            addressSpec = null;
            return false;
        }
    }

    /// <summary>Formats one parsed high-level address expression as canonical text.</summary>
    public static string Format(SlmpAddressSpec addressSpec)
    {
        ArgumentNullException.ThrowIfNull(addressSpec);
        var canonicalDevice = SlmpAddress.Format(addressSpec.DeviceAddress);
        return addressSpec.BitIndex is int bit
            ? $"{canonicalDevice}.{bit.ToString("X", CultureInfo.InvariantCulture)}"
            : $"{canonicalDevice}:{addressSpec.DType}";
    }

    /// <summary>Normalizes one high-level address expression using the explicit PLC profile.</summary>
    public static string Normalize(string text, SlmpPlcProfile plcProfile)
        => Format(Parse(text, plcProfile));
}
