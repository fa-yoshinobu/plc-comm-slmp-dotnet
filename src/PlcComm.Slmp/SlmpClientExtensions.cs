using System.Globalization;
using System.Runtime.CompilerServices;

namespace PlcComm.Slmp;

internal enum SlmpNamedReadKind
{
    Word,
    Dword,
    BitInWord,
    LongTimer,
    Fallback,
}

internal enum SlmpLongTimerReadKind
{
    Current,
    Contact,
    Coil,
}

internal enum SlmpNamedWriteRoute
{
    ContiguousBits,
    ContiguousWords,
    ContiguousDWords,
    RandomBits,
    RandomDWords,
}

internal readonly record struct SlmpLongTimerReadSpec(
    SlmpDeviceCode BaseCode,
    SlmpLongTimerReadKind Kind
);

internal sealed record SlmpNamedReadEntry(
    string Address,
    SlmpDeviceAddress Device,
    string DType,
    int? BitIndex,
    SlmpNamedReadKind Kind,
    SlmpLongTimerReadSpec? LongTimerRead
);

internal sealed record SlmpNamedReadPlan(
    IReadOnlyList<SlmpNamedReadEntry> Entries,
    IReadOnlyList<SlmpDeviceAddress> WordDevices,
    IReadOnlyList<SlmpDeviceAddress> DwordDevices
);

/// <summary>
/// Extension methods for <see cref="SlmpClient"/> and <see cref="QueuedSlmpClient"/>
/// providing typed read/write helpers, chunked reads, named-device access, and polling.
/// </summary>
public static class SlmpClientExtensions
{
    private static SlmpDeviceAddress ParseDeviceForClient(SlmpClient client, string address)
        => SlmpDeviceParser.ParseForHighLevel(address, client.PlcFamily);

    private static SlmpDeviceAddress ParseDeviceForClient(QueuedSlmpClient client, string address)
        => SlmpDeviceParser.ParseForHighLevel(address, client.PlcFamily);

    private static string NormalizeDeviceForFamily(string address, SlmpPlcFamily? plcFamily)
        => plcFamily is SlmpPlcFamily family ? SlmpAddress.Normalize(address, family) : SlmpAddress.Normalize(address);

    // -----------------------------------------------------------------------
    // Typed read / write
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads one logical value and converts it to the requested application type.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="device">Starting device address.</param>
    /// <param name="dtype">
    /// Type code: <c>U</c> unsigned 16-bit, <c>S</c> signed 16-bit,
    /// <c>D</c> unsigned 32-bit, <c>L</c> signed 32-bit, or <c>F</c> float32.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A boxed <see cref="ushort"/>, <see cref="short"/>, <see cref="uint"/>, <see cref="int"/>, or <see cref="float"/>.</returns>
    /// <remarks>
    /// This is the main single-value read helper for user code. Prefer it over
    /// raw word access when the PLC data should be treated as a typed scalar.
    /// </remarks>
    public static async Task<object> ReadTypedAsync(
        this SlmpClient client,
        SlmpDeviceAddress device,
        string dtype,
        CancellationToken ct = default)
    {
        var normalizedDType = dtype.ToUpperInvariant();
        var longRead = GetLongTimerReadSpec(device.Code);
        if (longRead is not null)
        {
            var timer = await ReadLongLikePointAsync(client, longRead.Value.BaseCode, device.Number, ct).ConfigureAwait(false);
            return DecodeLongLikeValue(normalizedDType, longRead.Value, timer);
        }

        switch (normalizedDType)
        {
            case "BIT":
                {
                    var bits = await client.ReadBitsAsync(device, 1, ct).ConfigureAwait(false);
                    return bits[0];
                }
            case "F":
            case "D":
            case "L":
                {
                    var dwords = await client.ReadDWordsRawAsync(device, 1, ct).ConfigureAwait(false);
                    return dtype.ToUpperInvariant() switch
                    {
                        "F" => DecodeFloatDWord(dwords[0]),
                        "L" => DecodeSignedDWord(dwords[0]),
                        _ => dwords[0],
                    };
                }
            case "S":
                {
                    var words = await client.ReadWordsRawAsync(device, 1, ct).ConfigureAwait(false);
                    return DecodeSignedWord(words[0]);
                }
            default:
                {
                    var words = await client.ReadWordsRawAsync(device, 1, ct).ConfigureAwait(false);
                    return words[0];
                }
        }
    }

    /// <summary>
    /// Reads one device value using a string address.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="device">Device string such as <c>D100</c> or <c>M1000</c>.</param>
    /// <param name="dtype">Requested application type such as <c>U</c>, <c>F</c>, or <c>BIT</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A boxed scalar matching the requested type.</returns>
    public static Task<object> ReadTypedAsync(
        this SlmpClient client,
        string device,
        string dtype,
        CancellationToken ct = default)
        => client.ReadTypedAsync(ParseDeviceForClient(client, device), dtype, ct);

    /// <summary>
    /// Reads one device value and converts it to the specified type through a queued client.
    /// </summary>
    /// <param name="client">Queued SLMP client safe for shared use.</param>
    /// <param name="device">Starting device address.</param>
    /// <param name="dtype">Requested application type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A boxed scalar matching the requested type.</returns>
    public static Task<object> ReadTypedAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress device,
        string dtype,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.ReadTypedAsync(device, dtype, ct), ct);

    /// <summary>
    /// Reads one device value using a string address through a queued client.
    /// </summary>
    /// <param name="client">Queued SLMP client safe for shared use.</param>
    /// <param name="device">Device string such as <c>D100</c> or <c>M1000</c>.</param>
    /// <param name="dtype">Requested application type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A boxed scalar matching the requested type.</returns>
    public static Task<object> ReadTypedAsync(
        this QueuedSlmpClient client,
        string device,
        string dtype,
        CancellationToken ct = default)
        => client.ReadTypedAsync(ParseDeviceForClient(client, device), dtype, ct);

    /// <summary>
    /// Writes one logical value using the requested type conversion.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="device">Starting device address.</param>
    /// <param name="dtype">
    /// Type code: <c>U</c> unsigned 16-bit, <c>S</c> signed 16-bit,
    /// <c>D</c> unsigned 32-bit, <c>L</c> signed 32-bit, or <c>F</c> float32.
    /// </param>
    /// <param name="value">Value to encode and write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// Use this helper when application code wants to write typed values
    /// without manually splitting words or packing float32 values.
    /// </remarks>
    public static async Task WriteTypedAsync(
        this SlmpClient client,
        SlmpDeviceAddress device,
        string dtype,
        object value,
        CancellationToken ct = default)
    {
        switch (ResolveWriteRoute(device, dtype))
        {
            case SlmpNamedWriteRoute.RandomBits:
                await client.WriteRandomBitsAsync(
                        [(device, Convert.ToBoolean(value, CultureInfo.InvariantCulture))],
                        ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.ContiguousBits:
                await client.WriteBitsAsync(device, [Convert.ToBoolean(value, CultureInfo.InvariantCulture)], ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.ContiguousDWords when string.Equals(dtype, "F", StringComparison.OrdinalIgnoreCase):
                await client.WriteDWordsAsync(
                        device,
                        [unchecked((uint)BitConverter.SingleToInt32Bits(Convert.ToSingle(value, CultureInfo.InvariantCulture)))],
                        ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.RandomDWords when string.Equals(dtype, "L", StringComparison.OrdinalIgnoreCase):
                await client.WriteRandomWordsAsync(
                        [],
                        [(device, unchecked((uint)Convert.ToInt32(value, CultureInfo.InvariantCulture)))],
                        ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.RandomDWords:
                await client.WriteRandomWordsAsync(
                        [],
                        [(device, Convert.ToUInt32(value, CultureInfo.InvariantCulture))],
                        ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.ContiguousDWords when string.Equals(dtype, "L", StringComparison.OrdinalIgnoreCase):
                await client.WriteDWordsAsync(
                        device,
                        [unchecked((uint)Convert.ToInt32(value, CultureInfo.InvariantCulture))],
                        ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.ContiguousDWords:
                await client.WriteDWordsAsync(device, [Convert.ToUInt32(value, CultureInfo.InvariantCulture)], ct)
                    .ConfigureAwait(false);
                break;
            default:
                await client.WriteWordsAsync(device, [Convert.ToUInt16(value, CultureInfo.InvariantCulture)], ct)
                    .ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Writes one device value using a string address.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="device">Device string such as <c>D100</c>, <c>D200:F</c>, or <c>M1000</c>.</param>
    /// <param name="dtype">Requested application type.</param>
    /// <param name="value">Application value to encode and write.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task WriteTypedAsync(
        this SlmpClient client,
        string device,
        string dtype,
        object value,
        CancellationToken ct = default)
        => client.WriteTypedAsync(ParseDeviceForClient(client, device), dtype, value, ct);

    /// <summary>
    /// Writes one device value through a queued client.
    /// </summary>
    /// <param name="client">Queued SLMP client safe for shared use.</param>
    /// <param name="device">Starting device address.</param>
    /// <param name="dtype">Requested application type.</param>
    /// <param name="value">Application value to encode and write.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task WriteTypedAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress device,
        string dtype,
        object value,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.WriteTypedAsync(device, dtype, value, ct), ct);

    /// <summary>
    /// Writes one device value using a string address through a queued client.
    /// </summary>
    /// <param name="client">Queued SLMP client safe for shared use.</param>
    /// <param name="device">Device string such as <c>D100</c>, <c>D200:F</c>, or <c>M1000</c>.</param>
    /// <param name="dtype">Requested application type.</param>
    /// <param name="value">Application value to encode and write.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task WriteTypedAsync(
        this QueuedSlmpClient client,
        string device,
        string dtype,
        object value,
        CancellationToken ct = default)
        => client.WriteTypedAsync(ParseDeviceForClient(client, device), dtype, value, ct);

    /// <summary>
    /// Performs a read-modify-write to set or clear one bit inside a word device.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="device">Word device address such as <c>D50</c>.</param>
    /// <param name="bitIndex">Bit position within the word, from 0 to 15.</param>
    /// <param name="value">New bit state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// This helper is useful when a PLC stores request and status flags inside
    /// one control word and only one flag should change.
    /// </remarks>
    public static async Task WriteBitInWordAsync(
        this SlmpClient client,
        SlmpDeviceAddress device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
    {
        if (bitIndex is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "bitIndex must be 0-15.");
        var words = await client.ReadWordsRawAsync(device, 1, ct).ConfigureAwait(false);
        int current = words[0];
        if (value)
            current |= 1 << bitIndex;
        else
            current &= ~(1 << bitIndex);
        await client.WriteWordsAsync(device, [(ushort)(current & 0xFFFF)], ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs a read-modify-write using a string address.
    /// </summary>
    public static Task WriteBitInWordAsync(
        this SlmpClient client,
        string device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
        => client.WriteBitInWordAsync(ParseDeviceForClient(client, device), bitIndex, value, ct);

    /// <summary>
    /// Performs a read-modify-write through a queued client.
    /// </summary>
    public static Task WriteBitInWordAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.WriteBitInWordAsync(device, bitIndex, value, ct), ct);

    /// <summary>
    /// Performs a read-modify-write using a string address through a queued client.
    /// </summary>
    public static Task WriteBitInWordAsync(
        this QueuedSlmpClient client,
        string device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
        => client.WriteBitInWordAsync(ParseDeviceForClient(client, device), bitIndex, value, ct);

    /// <summary>
    /// Reads a contiguous bit-device range and returns boolean values.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="start">First bit device in the range.</param>
    /// <param name="count">Number of points to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Boolean values in PLC order.</returns>
    public static Task<bool[]> ReadBitsBlockAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        ushort count,
        CancellationToken ct = default)
        => client.ReadBitsAsync(start, count, ct);

    /// <summary>
    /// Reads a contiguous bit-device range using a string address.
    /// </summary>
    public static Task<bool[]> ReadBitsBlockAsync(
        this SlmpClient client,
        string start,
        ushort count,
        CancellationToken ct = default)
        => client.ReadBitsBlockAsync(ParseDeviceForClient(client, start), count, ct);

    /// <summary>
    /// Reads a contiguous bit-device range through a queued client.
    /// </summary>
    public static Task<bool[]> ReadBitsBlockAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        ushort count,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.ReadBitsBlockAsync(start, count, ct), ct);

    /// <summary>
    /// Reads a contiguous bit-device range using a string address through a queued client.
    /// </summary>
    public static Task<bool[]> ReadBitsBlockAsync(
        this QueuedSlmpClient client,
        string start,
        ushort count,
        CancellationToken ct = default)
        => client.ReadBitsBlockAsync(ParseDeviceForClient(client, start), count, ct);

    /// <summary>
    /// Writes a contiguous bit-device range from boolean values.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="start">First bit device in the range.</param>
    /// <param name="values">Boolean values in PLC order.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task WriteBitsBlockAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<bool> values,
        CancellationToken ct = default)
        => client.WriteBitsAsync(start, values, ct);

    /// <summary>
    /// Writes a contiguous bit-device range using a string address.
    /// </summary>
    public static Task WriteBitsBlockAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<bool> values,
        CancellationToken ct = default)
        => client.WriteBitsBlockAsync(ParseDeviceForClient(client, start), values, ct);

    /// <summary>
    /// Writes a contiguous bit-device range through a queued client.
    /// </summary>
    public static Task WriteBitsBlockAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<bool> values,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.WriteBitsBlockAsync(start, values, ct), ct);

    /// <summary>
    /// Writes a contiguous bit-device range using a string address through a queued client.
    /// </summary>
    public static Task WriteBitsBlockAsync(
        this QueuedSlmpClient client,
        string start,
        IReadOnlyList<bool> values,
        CancellationToken ct = default)
        => client.WriteBitsBlockAsync(ParseDeviceForClient(client, start), values, ct);

    /// <summary>
    /// Writes a contiguous word-device range from 16-bit values.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="start">First word device in the range.</param>
    /// <param name="values">Word values in PLC order.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task WriteWordsBlockAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
        => client.WriteWordsAsync(start, values, ct);

    /// <summary>
    /// Writes a contiguous word-device range using a string address.
    /// </summary>
    public static Task WriteWordsBlockAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
        => client.WriteWordsBlockAsync(ParseDeviceForClient(client, start), values, ct);

    /// <summary>
    /// Writes a contiguous word-device range through a queued client.
    /// </summary>
    public static Task WriteWordsBlockAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.WriteWordsBlockAsync(start, values, ct), ct);

    /// <summary>
    /// Writes a contiguous word-device range using a string address through a queued client.
    /// </summary>
    public static Task WriteWordsBlockAsync(
        this QueuedSlmpClient client,
        string start,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
        => client.WriteWordsBlockAsync(ParseDeviceForClient(client, start), values, ct);

    /// <summary>
    /// Writes a contiguous DWord-device range from 32-bit values.
    /// </summary>
    public static Task WriteDWordsBlockAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
        => client.WriteDWordsAsync(start, values, ct);

    /// <summary>
    /// Writes a contiguous DWord-device range using a string address.
    /// </summary>
    public static Task WriteDWordsBlockAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
        => client.WriteDWordsBlockAsync(ParseDeviceForClient(client, start), values, ct);

    /// <summary>
    /// Writes a contiguous DWord-device range through a queued client.
    /// </summary>
    public static Task WriteDWordsBlockAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.WriteDWordsBlockAsync(start, values, ct), ct);

    /// <summary>
    /// Writes a contiguous DWord-device range using a string address through a queued client.
    /// </summary>
    public static Task WriteDWordsBlockAsync(
        this QueuedSlmpClient client,
        string start,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
        => client.WriteDWordsBlockAsync(ParseDeviceForClient(client, start), values, ct);

    /// <summary>
    /// Reads contiguous word devices using one SLMP request or returns an error.
    /// </summary>
    public static Task<ushort[]> ReadWordsSingleRequestAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        int count,
        CancellationToken ct = default)
    {
        ValidateSingleRequestCount(count, 960, nameof(count));
        return client.ReadWordsRawAsync(start, (ushort)count, ct);
    }

    /// <summary>
    /// Reads contiguous word devices using one SLMP request or returns an error.
    /// </summary>
    public static Task<ushort[]> ReadWordsSingleRequestAsync(
        this SlmpClient client,
        string start,
        int count,
        CancellationToken ct = default)
        => client.ReadWordsSingleRequestAsync(ParseDeviceForClient(client, start), count, ct);

    /// <summary>
    /// Reads contiguous word devices using one SLMP request or returns an error through a queued client.
    /// </summary>
    public static Task<ushort[]> ReadWordsSingleRequestAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        int count,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.ReadWordsSingleRequestAsync(start, count, ct), ct);

    /// <summary>
    /// Reads contiguous word devices using one SLMP request or returns an error through a queued client.
    /// </summary>
    public static Task<ushort[]> ReadWordsSingleRequestAsync(
        this QueuedSlmpClient client,
        string start,
        int count,
        CancellationToken ct = default)
        => client.ReadWordsSingleRequestAsync(ParseDeviceForClient(client, start), count, ct);

    /// <summary>
    /// Reads contiguous DWord devices using one SLMP request or returns an error.
    /// </summary>
    public static Task<uint[]> ReadDWordsSingleRequestAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        int count,
        CancellationToken ct = default)
    {
        ValidateSingleRequestCount(count, 480, nameof(count));
        return client.ReadDWordsRawAsync(start, (ushort)count, ct);
    }

    /// <summary>
    /// Reads contiguous DWord devices using one SLMP request or returns an error.
    /// </summary>
    public static Task<uint[]> ReadDWordsSingleRequestAsync(
        this SlmpClient client,
        string start,
        int count,
        CancellationToken ct = default)
        => client.ReadDWordsSingleRequestAsync(ParseDeviceForClient(client, start), count, ct);

    /// <summary>
    /// Reads contiguous DWord devices using one SLMP request or returns an error through a queued client.
    /// </summary>
    public static Task<uint[]> ReadDWordsSingleRequestAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        int count,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.ReadDWordsSingleRequestAsync(start, count, ct), ct);

    /// <summary>
    /// Reads contiguous DWord devices using one SLMP request or returns an error through a queued client.
    /// </summary>
    public static Task<uint[]> ReadDWordsSingleRequestAsync(
        this QueuedSlmpClient client,
        string start,
        int count,
        CancellationToken ct = default)
        => client.ReadDWordsSingleRequestAsync(ParseDeviceForClient(client, start), count, ct);

    /// <summary>
    /// Writes contiguous word devices using one SLMP request or returns an error.
    /// </summary>
    public static Task WriteWordsSingleRequestAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
    {
        ValidateSingleRequestValues(values, 960, nameof(values));
        return client.WriteWordsAsync(start, values, ct);
    }

    /// <summary>
    /// Writes contiguous word devices using one SLMP request or returns an error.
    /// </summary>
    public static Task WriteWordsSingleRequestAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
        => client.WriteWordsSingleRequestAsync(ParseDeviceForClient(client, start), values, ct);

    /// <summary>
    /// Writes contiguous word devices using one SLMP request or returns an error through a queued client.
    /// </summary>
    public static Task WriteWordsSingleRequestAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.WriteWordsSingleRequestAsync(start, values, ct), ct);

    /// <summary>
    /// Writes contiguous word devices using one SLMP request or returns an error through a queued client.
    /// </summary>
    public static Task WriteWordsSingleRequestAsync(
        this QueuedSlmpClient client,
        string start,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
        => client.WriteWordsSingleRequestAsync(ParseDeviceForClient(client, start), values, ct);

    /// <summary>
    /// Writes contiguous DWord devices using one SLMP request or returns an error.
    /// </summary>
    public static Task WriteDWordsSingleRequestAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
    {
        ValidateSingleRequestValues(values, 480, nameof(values));
        return client.WriteDWordsAsync(start, values, ct);
    }

    /// <summary>
    /// Writes contiguous DWord devices using one SLMP request or returns an error.
    /// </summary>
    public static Task WriteDWordsSingleRequestAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
        => client.WriteDWordsSingleRequestAsync(ParseDeviceForClient(client, start), values, ct);

    /// <summary>
    /// Writes contiguous DWord devices using one SLMP request or returns an error through a queued client.
    /// </summary>
    public static Task WriteDWordsSingleRequestAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.WriteDWordsSingleRequestAsync(start, values, ct), ct);

    /// <summary>
    /// Writes contiguous DWord devices using one SLMP request or returns an error through a queued client.
    /// </summary>
    public static Task WriteDWordsSingleRequestAsync(
        this QueuedSlmpClient client,
        string start,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
        => client.WriteDWordsSingleRequestAsync(ParseDeviceForClient(client, start), values, ct);

    // -----------------------------------------------------------------------
    // Chunked reads
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads a contiguous word range in one or more SLMP requests.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="start">Starting word device address.</param>
    /// <param name="count">Total number of words to read.</param>
    /// <param name="maxPerRequest">Maximum words per request. The protocol limit is 960.</param>
    /// <param name="allowSplit">When true, large reads are automatically split across multiple SLMP requests.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Flat array of word values.</returns>
    /// <remarks>
    /// Chunk boundaries are aligned to 2-word boundaries so that 32-bit values
    /// are not torn across split requests.
    /// </remarks>
    public static async Task<ushort[]> ReadWordsAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        int count,
        int maxPerRequest = 960,
        bool allowSplit = false,
        CancellationToken ct = default)
    {
        int effectiveMax = (maxPerRequest / 2) * 2;
        if (effectiveMax <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPerRequest), "maxPerRequest must be at least 2.");

        if (!allowSplit)
        {
            if (count > effectiveMax)
            {
                throw new ArgumentException(
                    $"count {count} exceeds maxPerRequest {effectiveMax}; pass allowSplit: true to split the read across multiple requests.",
                    nameof(count));
            }

            return await client.ReadWordsRawAsync(start, (ushort)count, ct).ConfigureAwait(false);
        }

        var result = new List<ushort>(count);
        int remaining = count;
        uint offset = 0;
        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, effectiveMax);
            var address = new SlmpDeviceAddress(start.Code, start.Number + offset);
            var words = await client.ReadWordsRawAsync(address, (ushort)chunk, ct).ConfigureAwait(false);
            result.AddRange(words);
            offset += (uint)chunk;
            remaining -= chunk;
        }

        return [.. result];
    }

    /// <summary>
    /// Reads word devices using a string address.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="start">Word device string such as <c>D0</c>.</param>
    /// <param name="count">Total number of words to read.</param>
    /// <param name="maxPerRequest">Maximum words per request.</param>
    /// <param name="allowSplit">When true, oversized reads are split across requests.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Flat array of word values.</returns>
    public static Task<ushort[]> ReadWordsAsync(
        this SlmpClient client,
        string start,
        int count,
        int maxPerRequest = 960,
        bool allowSplit = false,
        CancellationToken ct = default)
        => client.ReadWordsAsync(ParseDeviceForClient(client, start), count, maxPerRequest, allowSplit, ct);

    /// <summary>
    /// Reads word devices through a queued client.
    /// </summary>
    public static Task<ushort[]> ReadWordsAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        int count,
        int maxPerRequest = 960,
        bool allowSplit = false,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.ReadWordsAsync(start, count, maxPerRequest, allowSplit, ct), ct);

    /// <summary>
    /// Reads word devices using a string address through a queued client.
    /// </summary>
    public static Task<ushort[]> ReadWordsAsync(
        this QueuedSlmpClient client,
        string start,
        int count,
        int maxPerRequest = 960,
        bool allowSplit = false,
        CancellationToken ct = default)
        => client.ReadWordsAsync(ParseDeviceForClient(client, start), count, maxPerRequest, allowSplit, ct);

    /// <summary>
    /// Reads a contiguous range of 32-bit unsigned values.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="start">Starting device address.</param>
    /// <param name="count">Number of 32-bit values to read.</param>
    /// <param name="maxDwordsPerRequest">Maximum DWords per request.</param>
    /// <param name="allowSplit">When true, large reads are automatically split across multiple SLMP requests.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of 32-bit unsigned values.</returns>
    /// <remarks>
    /// Each result consumes two underlying words in low-word-first order.
    /// </remarks>
    public static async Task<uint[]> ReadDWordsAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        int count,
        int maxDwordsPerRequest = 480,
        bool allowSplit = false,
        CancellationToken ct = default)
    {
        var words = await client.ReadWordsAsync(
                start,
                count * 2,
                maxPerRequest: maxDwordsPerRequest * 2,
                allowSplit: allowSplit,
                ct: ct)
            .ConfigureAwait(false);

        var result = new uint[count];
        for (int i = 0; i < count; i++)
            result[i] = (uint)(words[i * 2] | (words[(i * 2) + 1] << 16));
        return result;
    }

    /// <summary>
    /// Reads DWord devices using a string address.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="start">Starting word device string such as <c>D200</c>.</param>
    /// <param name="count">Number of 32-bit values to read.</param>
    /// <param name="maxDwordsPerRequest">Maximum DWords per request.</param>
    /// <param name="allowSplit">When true, oversized reads are split across requests.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of 32-bit unsigned values.</returns>
    public static Task<uint[]> ReadDWordsAsync(
        this SlmpClient client,
        string start,
        int count,
        int maxDwordsPerRequest = 480,
        bool allowSplit = false,
        CancellationToken ct = default)
        => client.ReadDWordsAsync(ParseDeviceForClient(client, start), count, maxDwordsPerRequest, allowSplit, ct);

    /// <summary>
    /// Reads DWord devices through a queued client.
    /// </summary>
    public static Task<uint[]> ReadDWordsAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        int count,
        int maxDwordsPerRequest = 480,
        bool allowSplit = false,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.ReadDWordsAsync(start, count, maxDwordsPerRequest, allowSplit, ct), ct);

    /// <summary>
    /// Reads DWord devices using a string address through a queued client.
    /// </summary>
    public static Task<uint[]> ReadDWordsAsync(
        this QueuedSlmpClient client,
        string start,
        int count,
        int maxDwordsPerRequest = 480,
        bool allowSplit = false,
        CancellationToken ct = default)
        => client.ReadDWordsAsync(ParseDeviceForClient(client, start), count, maxDwordsPerRequest, allowSplit, ct);

    /// <summary>
    /// Reads contiguous word devices using explicit chunking.
    /// </summary>
    public static Task<ushort[]> ReadWordsChunkedAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        int count,
        int maxWordsPerRequest,
        CancellationToken ct = default)
        => client.ReadWordsAsync(start, count, maxWordsPerRequest, allowSplit: true, ct);

    /// <summary>
    /// Reads contiguous word devices using explicit chunking.
    /// </summary>
    public static Task<ushort[]> ReadWordsChunkedAsync(
        this SlmpClient client,
        string start,
        int count,
        int maxWordsPerRequest,
        CancellationToken ct = default)
        => client.ReadWordsChunkedAsync(ParseDeviceForClient(client, start), count, maxWordsPerRequest, ct);

    /// <summary>
    /// Reads contiguous word devices using explicit chunking through a queued client.
    /// </summary>
    public static Task<ushort[]> ReadWordsChunkedAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        int count,
        int maxWordsPerRequest,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.ReadWordsChunkedAsync(start, count, maxWordsPerRequest, ct), ct);

    /// <summary>
    /// Reads contiguous word devices using explicit chunking through a queued client.
    /// </summary>
    public static Task<ushort[]> ReadWordsChunkedAsync(
        this QueuedSlmpClient client,
        string start,
        int count,
        int maxWordsPerRequest,
        CancellationToken ct = default)
        => client.ReadWordsChunkedAsync(ParseDeviceForClient(client, start), count, maxWordsPerRequest, ct);

    /// <summary>
    /// Reads contiguous DWord devices using explicit chunking.
    /// </summary>
    public static Task<uint[]> ReadDWordsChunkedAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        int count,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
        => client.ReadDWordsAsync(start, count, maxDwordsPerRequest, allowSplit: true, ct);

    /// <summary>
    /// Reads contiguous DWord devices using explicit chunking.
    /// </summary>
    public static Task<uint[]> ReadDWordsChunkedAsync(
        this SlmpClient client,
        string start,
        int count,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
        => client.ReadDWordsChunkedAsync(ParseDeviceForClient(client, start), count, maxDwordsPerRequest, ct);

    /// <summary>
    /// Reads contiguous DWord devices using explicit chunking through a queued client.
    /// </summary>
    public static Task<uint[]> ReadDWordsChunkedAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        int count,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.ReadDWordsChunkedAsync(start, count, maxDwordsPerRequest, ct), ct);

    /// <summary>
    /// Reads contiguous DWord devices using explicit chunking through a queued client.
    /// </summary>
    public static Task<uint[]> ReadDWordsChunkedAsync(
        this QueuedSlmpClient client,
        string start,
        int count,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
        => client.ReadDWordsChunkedAsync(ParseDeviceForClient(client, start), count, maxDwordsPerRequest, ct);

    /// <summary>
    /// Writes contiguous word devices using explicit chunking.
    /// </summary>
    public static async Task WriteWordsChunkedAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<ushort> values,
        int maxWordsPerRequest,
        CancellationToken ct = default)
    {
        ValidateChunkedValues(values, nameof(values));
        ValidateChunkSize(maxWordsPerRequest, nameof(maxWordsPerRequest));

        int remaining = values.Count;
        int offset = 0;
        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, maxWordsPerRequest);
            var address = new SlmpDeviceAddress(start.Code, start.Number + (uint)offset);
            await client.WriteWordsAsync(address, values.Skip(offset).Take(chunk).ToArray(), ct).ConfigureAwait(false);
            offset += chunk;
            remaining -= chunk;
        }
    }

    /// <summary>
    /// Writes contiguous word devices using explicit chunking.
    /// </summary>
    public static Task WriteWordsChunkedAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<ushort> values,
        int maxWordsPerRequest,
        CancellationToken ct = default)
        => client.WriteWordsChunkedAsync(ParseDeviceForClient(client, start), values, maxWordsPerRequest, ct);

    /// <summary>
    /// Writes contiguous word devices using explicit chunking through a queued client.
    /// </summary>
    public static Task WriteWordsChunkedAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<ushort> values,
        int maxWordsPerRequest,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.WriteWordsChunkedAsync(start, values, maxWordsPerRequest, ct), ct);

    /// <summary>
    /// Writes contiguous word devices using explicit chunking through a queued client.
    /// </summary>
    public static Task WriteWordsChunkedAsync(
        this QueuedSlmpClient client,
        string start,
        IReadOnlyList<ushort> values,
        int maxWordsPerRequest,
        CancellationToken ct = default)
        => client.WriteWordsChunkedAsync(ParseDeviceForClient(client, start), values, maxWordsPerRequest, ct);

    /// <summary>
    /// Writes contiguous DWord devices using explicit chunking.
    /// </summary>
    public static async Task WriteDWordsChunkedAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<uint> values,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
    {
        ValidateChunkedValues(values, nameof(values));
        ValidateChunkSize(maxDwordsPerRequest, nameof(maxDwordsPerRequest));

        int remaining = values.Count;
        int offset = 0;
        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, maxDwordsPerRequest);
            var address = new SlmpDeviceAddress(start.Code, start.Number + (uint)(offset * 2));
            await client.WriteDWordsAsync(address, values.Skip(offset).Take(chunk).ToArray(), ct).ConfigureAwait(false);
            offset += chunk;
            remaining -= chunk;
        }
    }

    /// <summary>
    /// Writes contiguous DWord devices using explicit chunking.
    /// </summary>
    public static Task WriteDWordsChunkedAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<uint> values,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
        => client.WriteDWordsChunkedAsync(ParseDeviceForClient(client, start), values, maxDwordsPerRequest, ct);

    /// <summary>
    /// Writes contiguous DWord devices using explicit chunking through a queued client.
    /// </summary>
    public static Task WriteDWordsChunkedAsync(
        this QueuedSlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<uint> values,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.WriteDWordsChunkedAsync(start, values, maxDwordsPerRequest, ct), ct);

    /// <summary>
    /// Writes contiguous DWord devices using explicit chunking through a queued client.
    /// </summary>
    public static Task WriteDWordsChunkedAsync(
        this QueuedSlmpClient client,
        string start,
        IReadOnlyList<uint> values,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
        => client.WriteDWordsChunkedAsync(ParseDeviceForClient(client, start), values, maxDwordsPerRequest, ct);

    // -----------------------------------------------------------------------
    // Named-device read
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads a mixed logical snapshot by address string and returns a dictionary keyed by the original addresses.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="addresses">
    /// Address list such as <c>D100</c>, <c>D200:F</c>, <c>D300:L</c>, or <c>D50.3</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary whose keys match the requested address strings.</returns>
    /// <remarks>
    /// This is the recommended high-level helper for dashboards, snapshots, and
    /// mixed-value reads. The address list is compiled and batched internally.
    /// </remarks>
    public static async Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this SlmpClient client,
        IEnumerable<string> addresses,
        CancellationToken ct = default)
    {
        var plan = CompileReadPlan(addresses, client.PlcFamily);
        return await ReadNamedCompiledAsync(client, plan, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads multiple devices by address string through a queued client.
    /// </summary>
    public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this QueuedSlmpClient client,
        IEnumerable<string> addresses,
        CancellationToken ct = default)
    {
        var plan = CompileReadPlan(addresses, client.PlcFamily);
        return client.ExecuteAsync(inner => ReadNamedCompiledAsync(inner, plan, ct), ct);
    }

    /// <summary>
    /// Writes a mixed logical snapshot by address string.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="updates">
    /// Mapping of address string to value, for example <c>"D100"</c>, <c>"D200:F"</c>,
    /// <c>"D50.3"</c>, or direct bit-device addresses such as <c>"M1000"</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteNamedAsync(
        this SlmpClient client,
        IReadOnlyDictionary<string, object> updates,
        CancellationToken ct = default)
    {
        foreach (var pair in updates)
        {
            var (baseAddress, dtype, bitIdx) = ParseAddress(pair.Key);
            var device = ParseDeviceForClient(client, baseAddress);
            var resolvedDType = ResolveDTypeForAddress(pair.Key, device, dtype, bitIdx);
            ValidateLongTimerEntry(pair.Key, device, resolvedDType);
            if (dtype == "BIT_IN_WORD")
            {
                ValidateBitInWordTarget(pair.Key, device);
                await client.WriteBitInWordAsync(device, bitIdx ?? 0, Convert.ToBoolean(pair.Value, CultureInfo.InvariantCulture), ct)
                    .ConfigureAwait(false);
                continue;
            }

            await client.WriteTypedAsync(device, resolvedDType, pair.Value, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes multiple named values through a queued client.
    /// </summary>
    /// <param name="client">Queued SLMP client safe for shared use.</param>
    /// <param name="updates">Address-to-value map in the same format as <see cref="WriteNamedAsync(SlmpClient,System.Collections.Generic.IReadOnlyDictionary{string,object},System.Threading.CancellationToken)"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task WriteNamedAsync(
        this QueuedSlmpClient client,
        IReadOnlyDictionary<string, object> updates,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => inner.WriteNamedAsync(updates, ct), ct);

    // -----------------------------------------------------------------------
    // Polling
    // -----------------------------------------------------------------------

    /// <summary>
    /// Continuously polls the specified logical snapshot at the requested interval.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="addresses">Address list in the same format as <see cref="ReadNamedAsync(SlmpClient,System.Collections.Generic.IEnumerable{string},System.Threading.CancellationToken)"/>.</param>
    /// <param name="interval">Delay between snapshots.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async stream of snapshot dictionaries.</returns>
    /// <remarks>
    /// The address list is compiled once and reused for every cycle, making
    /// this helper suitable for periodic monitoring and historian ingestion.
    /// </remarks>
    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this SlmpClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var plan = CompileReadPlan(addresses, client.PlcFamily);
        while (!ct.IsCancellationRequested)
        {
            yield return await ReadNamedCompiledAsync(client, plan, ct).ConfigureAwait(false);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Continuously polls the specified devices at the given interval through a queued client.
    /// </summary>
    /// <param name="client">Queued SLMP client safe for shared use.</param>
    /// <param name="addresses">Address list in the same format as <see cref="ReadNamedAsync(SlmpClient,System.Collections.Generic.IEnumerable{string},System.Threading.CancellationToken)"/>.</param>
    /// <param name="interval">Delay between snapshots.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async stream of snapshot dictionaries.</returns>
    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this QueuedSlmpClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var plan = CompileReadPlan(addresses, client.PlcFamily);
        while (!ct.IsCancellationRequested)
        {
            yield return await client.ExecuteAsync(inner => ReadNamedCompiledAsync(inner, plan, ct), ct).ConfigureAwait(false);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private static void ValidateSingleRequestCount(int count, int maxCount, string paramName)
    {
        if (count < 1 || count > maxCount)
            throw new ArgumentOutOfRangeException(paramName, $"count must be in the range 1-{maxCount}.");
    }

    private static void ValidateSingleRequestValues<T>(IReadOnlyList<T> values, int maxCount, string paramName)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0 || values.Count > maxCount)
            throw new ArgumentOutOfRangeException(paramName, $"values.Count must be in the range 1-{maxCount}.");
    }

    private static void ValidateChunkedValues<T>(IReadOnlyList<T> values, string paramName)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ArgumentOutOfRangeException(paramName, "values.Count must be 1 or greater.");
    }

    private static void ValidateChunkSize(int chunkSize, string paramName)
    {
        if (chunkSize < 1)
            throw new ArgumentOutOfRangeException(paramName, "Chunk size must be 1 or greater.");
    }

    internal static (string Base, string DType, int? BitIdx) ParseAddress(string address)
    {
        address = address.Trim();
        if (address.Contains(':'))
        {
            int index = address.IndexOf(':');
            return (address[..index].Trim(), address[(index + 1)..].Trim().ToUpperInvariant(), null);
        }

        if (address.Contains('.'))
        {
            int index = address.IndexOf('.');
            if (int.TryParse(address[(index + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int bit))
                return (address[..index].Trim(), "BIT_IN_WORD", bit);
        }

        return (address.Trim(), "U", null);
    }

    internal static string NormalizeNamedAddress(string address)
    {
        return NormalizeNamedAddress(address, null);
    }

    internal static string NormalizeNamedAddress(string address, SlmpPlcFamily? plcFamily)
    {
        var trimmed = address.Trim();
        var (baseAddress, dtype, bitIdx) = ParseAddress(trimmed);
        var canonicalBase = NormalizeDeviceForFamily(baseAddress, plcFamily);
        if (bitIdx is int bit)
        {
            return $"{canonicalBase}.{bit.ToString("X", CultureInfo.InvariantCulture)}";
        }

        return trimmed.Contains(':', StringComparison.Ordinal)
            ? $"{canonicalBase}:{dtype}"
            : canonicalBase;
    }

    internal static SlmpNamedReadPlan CompileReadPlan(IEnumerable<string> addresses, SlmpPlcFamily? plcFamily = null)
    {
        var entries = new List<SlmpNamedReadEntry>();
        var wordDevices = new List<SlmpDeviceAddress>();
        var dwordDevices = new List<SlmpDeviceAddress>();
        var seenWords = new HashSet<SlmpDeviceAddress>();
        var seenDwords = new HashSet<SlmpDeviceAddress>();

        foreach (var address in addresses)
        {
            var (baseAddress, dtype, bitIdx) = ParseAddress(address);
            var device = SlmpDeviceParser.ParseForHighLevel(baseAddress, plcFamily);
            dtype = ResolveDTypeForAddress(address, device, dtype, bitIdx);
            ValidateLongTimerEntry(address, device, dtype);
            var kind = SlmpNamedReadKind.Fallback;
            var longTimerRead = GetLongTimerReadSpec(device.Code);

            if (longTimerRead is not null)
            {
                kind = SlmpNamedReadKind.LongTimer;
            }
            else if (dtype == "BIT_IN_WORD")
            {
                ValidateBitInWordTarget(address, device);
                if (IsWordBatchable(device.Code))
                {
                    kind = SlmpNamedReadKind.BitInWord;
                    if (seenWords.Add(device))
                        wordDevices.Add(device);
                }
            }
            else if ((dtype == "U" || dtype == "S") && IsWordBatchable(device.Code))
            {
                kind = SlmpNamedReadKind.Word;
                if (seenWords.Add(device))
                    wordDevices.Add(device);
            }
            else if ((dtype == "D" || dtype == "L" || dtype == "F") && IsWordBatchable(device.Code))
            {
                kind = SlmpNamedReadKind.Dword;
                if (seenDwords.Add(device))
                    dwordDevices.Add(device);
            }

            entries.Add(new SlmpNamedReadEntry(address, device, dtype, bitIdx, kind, longTimerRead));
        }

        return new SlmpNamedReadPlan(entries, wordDevices, dwordDevices);
    }

    private static async Task<IReadOnlyDictionary<string, object>> ReadNamedCompiledAsync(
        SlmpClient client,
        SlmpNamedReadPlan plan,
        CancellationToken ct)
    {
        var result = new Dictionary<string, object>(plan.Entries.Count);
        var (wordValues, dwordValues) = await ReadRandomMapsAsync(client, plan.WordDevices, plan.DwordDevices, ct).ConfigureAwait(false);
        var longTimerCache = new Dictionary<(SlmpDeviceCode BaseCode, uint Number), SlmpLongTimerResult>();

        foreach (var entry in plan.Entries)
        {
            switch (entry.Kind)
            {
                case SlmpNamedReadKind.Word:
                    result[entry.Address] = entry.DType.Equals("S", StringComparison.OrdinalIgnoreCase)
                        ? (object)DecodeSignedWord(wordValues[entry.Device])
                        : wordValues[entry.Device];
                    break;
                case SlmpNamedReadKind.BitInWord:
                    result[entry.Address] = ((wordValues[entry.Device] >> (entry.BitIndex ?? 0)) & 1) != 0;
                    break;
                case SlmpNamedReadKind.Dword:
                    result[entry.Address] = entry.DType.ToUpperInvariant() switch
                    {
                        "F" => (object)DecodeFloatDWord(dwordValues[entry.Device]),
                        "L" => DecodeSignedDWord(dwordValues[entry.Device]),
                        _ => dwordValues[entry.Device],
                    };
                    break;
                case SlmpNamedReadKind.LongTimer:
                    result[entry.Address] = await ReadLongTimerValueAsync(client, entry, longTimerCache, ct).ConfigureAwait(false);
                    break;
                default:
                    if (entry.DType == "BIT_IN_WORD")
                    {
                        var words = await client.ReadWordsRawAsync(entry.Device, 1, ct).ConfigureAwait(false);
                        result[entry.Address] = ((words[0] >> (entry.BitIndex ?? 0)) & 1) != 0;
                    }
                    else
                    {
                        result[entry.Address] = await client.ReadTypedAsync(entry.Device, entry.DType, ct).ConfigureAwait(false);
                    }
                    break;
            }
        }

        return result;
    }

    private static async Task<object> ReadLongTimerValueAsync(
        SlmpClient client,
        SlmpNamedReadEntry entry,
        Dictionary<(SlmpDeviceCode BaseCode, uint Number), SlmpLongTimerResult> cache,
        CancellationToken ct)
    {
        var spec = entry.LongTimerRead ?? throw new InvalidOperationException("Long timer read metadata is missing.");
        var key = (spec.BaseCode, entry.Device.Number);
        if (!cache.TryGetValue(key, out var timer))
        {
            timer = await ReadLongLikePointAsync(client, spec.BaseCode, entry.Device.Number, ct).ConfigureAwait(false);
            cache[key] = timer;
        }

        return DecodeLongLikeValue(entry.DType, spec, timer);
    }

    private static async Task<SlmpLongTimerResult> ReadLongLikePointAsync(
        SlmpClient client,
        SlmpDeviceCode baseCode,
        uint number,
        CancellationToken ct)
    {
        return baseCode switch
        {
            SlmpDeviceCode.LTN => (await client.ReadLongTimerAsync((int)number, 1, ct).ConfigureAwait(false))[0],
            SlmpDeviceCode.LSTN => (await client.ReadLongRetentiveTimerAsync((int)number, 1, ct).ConfigureAwait(false))[0],
            SlmpDeviceCode.LCN => DecodeLongLikeWords(
                baseCode,
                number,
                await client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, number), 4, ct).ConfigureAwait(false)),
            _ => throw new InvalidOperationException($"Unsupported long-family base code: {baseCode}"),
        };
    }

    private static SlmpLongTimerResult DecodeLongLikeWords(
        SlmpDeviceCode baseCode,
        uint number,
        ushort[] rawWords)
    {
        if (rawWords.Length < 4)
            throw new InvalidOperationException($"Long-family read size mismatch: expected=4 actual={rawWords.Length}");

        var currentValue = (uint)(rawWords[0] | (rawWords[1] << 16));
        var statusWord = rawWords[2];
        var raw = rawWords.Take(4).ToArray();
        return new SlmpLongTimerResult(
            (int)number,
            $"{baseCode}{number}",
            currentValue,
            (statusWord & 0x0002) != 0,
            (statusWord & 0x0001) != 0,
            statusWord,
            raw);
    }

    private static object DecodeLongLikeValue(
        string dtype,
        SlmpLongTimerReadSpec spec,
        SlmpLongTimerResult timer)
    {
        return spec.Kind switch
        {
            SlmpLongTimerReadKind.Current => dtype.ToUpperInvariant() switch
            {
                "L" => DecodeSignedDWord(timer.CurrentValue),
                _ => timer.CurrentValue,
            },
            SlmpLongTimerReadKind.Contact => timer.Contact,
            SlmpLongTimerReadKind.Coil => timer.Coil,
            _ => throw new InvalidOperationException($"Unsupported long timer read kind: {spec.Kind}"),
        };
    }

    private static async Task<(Dictionary<SlmpDeviceAddress, ushort> Words, Dictionary<SlmpDeviceAddress, uint> DWords)> ReadRandomMapsAsync(
        SlmpClient client,
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        CancellationToken ct)
    {
        var words = new Dictionary<SlmpDeviceAddress, ushort>();
        var dwords = new Dictionary<SlmpDeviceAddress, uint>();
        var wordIndex = 0;
        var dwordIndex = 0;

        while (wordIndex < wordDevices.Count || dwordIndex < dwordDevices.Count)
        {
            var wordChunk = wordDevices.Skip(wordIndex).Take(0xFF).ToArray();
            var dwordChunk = dwordDevices.Skip(dwordIndex).Take(0xFF).ToArray();
            wordIndex += wordChunk.Length;
            dwordIndex += dwordChunk.Length;

            if (wordChunk.Length == 0 && dwordChunk.Length == 0)
                break;

            var random = await client.ReadRandomAsync(wordChunk, dwordChunk, ct).ConfigureAwait(false);
            for (int i = 0; i < wordChunk.Length; i++)
                words[wordChunk[i]] = random.WordValues[i];
            for (int i = 0; i < dwordChunk.Length; i++)
                dwords[dwordChunk[i]] = random.DwordValues[i];
        }

        return (words, dwords);
    }

    private static void ValidateBitInWordTarget(string address, SlmpDeviceAddress device)
    {
        if (!IsWordDevice(device.Code))
        {
            throw new ArgumentException(
                $"Address '{address}' uses '.bit' notation, which is only valid for word devices. " +
                "Address bit devices directly, for example 'M1000' instead of 'M1000.0'.",
                nameof(address));
        }
    }

    internal static bool HasExplicitDType(string address)
        => address.Contains(':', StringComparison.Ordinal);

    private static string NormalizeDTypeForDevice(SlmpDeviceAddress device, string dtype)
        => dtype == "U" && IsBitDevice(device.Code) ? "BIT" : dtype;

    internal static string ResolveDTypeForAddress(string address, SlmpDeviceAddress device, string dtype, int? bitIdx)
    {
        var normalized = NormalizeDTypeForDevice(device, dtype);
        if (!HasExplicitDType(address) && bitIdx is null && device.Code is SlmpDeviceCode.LTN or SlmpDeviceCode.LSTN or SlmpDeviceCode.LCN or SlmpDeviceCode.LZ)
            return "D";
        return normalized;
    }

    internal static SlmpNamedWriteRoute ResolveWriteRoute(SlmpDeviceAddress device, string dtype)
        => NormalizeDTypeForDevice(device, dtype) switch
        {
            "BIT" when device.Code is SlmpDeviceCode.LTS
                or SlmpDeviceCode.LTC
                or SlmpDeviceCode.LSTS
                or SlmpDeviceCode.LSTC
                => SlmpNamedWriteRoute.RandomBits,
            "BIT" => SlmpNamedWriteRoute.ContiguousBits,
            "D" or "L" when device.Code is SlmpDeviceCode.LTN
                or SlmpDeviceCode.LSTN
                or SlmpDeviceCode.LZ
                => SlmpNamedWriteRoute.RandomDWords,
            "D" or "L" or "F" => SlmpNamedWriteRoute.ContiguousDWords,
            _ => SlmpNamedWriteRoute.ContiguousWords,
        };

    internal static SlmpLongTimerReadSpec? GetLongTimerReadSpec(SlmpDeviceCode code)
        => code switch
        {
            SlmpDeviceCode.LTN => new SlmpLongTimerReadSpec(SlmpDeviceCode.LTN, SlmpLongTimerReadKind.Current),
            SlmpDeviceCode.LTS => new SlmpLongTimerReadSpec(SlmpDeviceCode.LTN, SlmpLongTimerReadKind.Contact),
            SlmpDeviceCode.LTC => new SlmpLongTimerReadSpec(SlmpDeviceCode.LTN, SlmpLongTimerReadKind.Coil),
            SlmpDeviceCode.LSTN => new SlmpLongTimerReadSpec(SlmpDeviceCode.LSTN, SlmpLongTimerReadKind.Current),
            SlmpDeviceCode.LSTS => new SlmpLongTimerReadSpec(SlmpDeviceCode.LSTN, SlmpLongTimerReadKind.Contact),
            SlmpDeviceCode.LSTC => new SlmpLongTimerReadSpec(SlmpDeviceCode.LSTN, SlmpLongTimerReadKind.Coil),
            SlmpDeviceCode.LCN => new SlmpLongTimerReadSpec(SlmpDeviceCode.LCN, SlmpLongTimerReadKind.Current),
            SlmpDeviceCode.LCS => new SlmpLongTimerReadSpec(SlmpDeviceCode.LCN, SlmpLongTimerReadKind.Contact),
            SlmpDeviceCode.LCC => new SlmpLongTimerReadSpec(SlmpDeviceCode.LCN, SlmpLongTimerReadKind.Coil),
            _ => null,
        };

    internal static void ValidateLongTimerEntry(string address, SlmpDeviceAddress device, string dtype)
    {
        var spec = GetLongTimerReadSpec(device.Code);
        if (spec is null)
            return;

        if (spec.Value.Kind == SlmpLongTimerReadKind.Current)
        {
            if (dtype is not "D" and not "L")
            {
                throw new ArgumentException(
                    $"Address '{address}' uses a 32-bit long current value. Use the plain form or ':D' / ':L'.",
                    nameof(address));
            }
            return;
        }

        if (!string.Equals(dtype, "BIT", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Address '{address}' is a long timer state device. Use the plain device form without a dtype override.",
                nameof(address));
        }
    }

    private static bool IsWordDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.SD
            or SlmpDeviceCode.D
            or SlmpDeviceCode.W
            or SlmpDeviceCode.TN
            or SlmpDeviceCode.LTN
            or SlmpDeviceCode.STN
            or SlmpDeviceCode.LSTN
            or SlmpDeviceCode.CN
            or SlmpDeviceCode.LCN
            or SlmpDeviceCode.SW
            or SlmpDeviceCode.Z
            or SlmpDeviceCode.LZ
            or SlmpDeviceCode.R
            or SlmpDeviceCode.ZR
            or SlmpDeviceCode.RD
            or SlmpDeviceCode.G
            or SlmpDeviceCode.HG;

    private static bool IsBitDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.SM
            or SlmpDeviceCode.X
            or SlmpDeviceCode.Y
            or SlmpDeviceCode.M
            or SlmpDeviceCode.L
            or SlmpDeviceCode.F
            or SlmpDeviceCode.V
            or SlmpDeviceCode.B
            or SlmpDeviceCode.TS
            or SlmpDeviceCode.TC
            or SlmpDeviceCode.LTS
            or SlmpDeviceCode.LTC
            or SlmpDeviceCode.STS
            or SlmpDeviceCode.STC
            or SlmpDeviceCode.LSTS
            or SlmpDeviceCode.LSTC
            or SlmpDeviceCode.CS
            or SlmpDeviceCode.CC
            or SlmpDeviceCode.LCS
            or SlmpDeviceCode.LCC
            or SlmpDeviceCode.SB
            or SlmpDeviceCode.DX
            or SlmpDeviceCode.DY;

    private static bool IsWordBatchable(SlmpDeviceCode code)
        => code is SlmpDeviceCode.SD
            or SlmpDeviceCode.D
            or SlmpDeviceCode.W
            or SlmpDeviceCode.TN
            or SlmpDeviceCode.LTN
            or SlmpDeviceCode.STN
            or SlmpDeviceCode.LSTN
            or SlmpDeviceCode.CN
            or SlmpDeviceCode.LCN
            or SlmpDeviceCode.SW
            or SlmpDeviceCode.Z
            or SlmpDeviceCode.LZ
            or SlmpDeviceCode.R
            or SlmpDeviceCode.ZR
            or SlmpDeviceCode.RD;

    private static short DecodeSignedWord(ushort value) => unchecked((short)value);

    private static int DecodeSignedDWord(uint value) => unchecked((int)value);

    private static float DecodeFloatDWord(uint value) => BitConverter.Int32BitsToSingle(unchecked((int)value));
}
