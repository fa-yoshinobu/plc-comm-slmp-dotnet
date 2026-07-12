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
/// providing typed read/write helpers, single-request block access, named-device access, and polling.
/// </summary>
public static class SlmpClientExtensions
{
    private static SlmpDeviceAddress ParseDeviceForClient(SlmpClient client, string address)
        => SlmpDeviceParser.Parse(address, client.PlcProfile);

    private static SlmpDeviceAddress ParseDeviceForClient(QueuedSlmpClient client, string address)
        => SlmpDeviceParser.Parse(address, client.PlcProfile);

    private static string NormalizeDeviceForFamily(string address, SlmpPlcProfile plcProfile)
        => SlmpAddress.Normalize(address, plcProfile);

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
        var normalizedDType = RequireDType(dtype, nameof(dtype));
        var longRead = GetLongTimerReadSpec(device.Code);
        if (longRead is not null)
        {
            ValidateLongFamilyDType(device, normalizedDType, nameof(dtype));
            if (IsLongCounterStateDirectBitRead(longRead.Value))
            {
                var bits = await client.ReadBitsUncheckedAsync(device, 1, ct).ConfigureAwait(false);
                return bits[0];
            }

            if (longRead.Value.Kind == SlmpLongTimerReadKind.Current && device.Code == SlmpDeviceCode.LCN)
            {
                var value = await ReadRandomDWordValueAsync(client, device, ct).ConfigureAwait(false);
                return normalizedDType == "L" ? DecodeSignedDWord(value) : value;
            }

            var timer = await ReadLongLikePointAsync(client, longRead.Value.BaseCode, device.Number, ct).ConfigureAwait(false);
            return DecodeLongLikeValue(normalizedDType, longRead.Value, timer);
        }

        ValidateDWordOnlyDType(device, normalizedDType, nameof(dtype));

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
                    var dword = IsRandomDWordAddressedDevice(device.Code)
                        ? await ReadRandomDWordValueAsync(client, device, ct).ConfigureAwait(false)
                        : (await client.ReadDWordsRawAsync(device, 1, ct).ConfigureAwait(false))[0];
                    return normalizedDType switch
                    {
                        "F" => DecodeFloatDWord(dword),
                        "L" => DecodeSignedDWord(dword),
                        _ => dword,
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
    /// Writes one logical value using strict dtype validation and encoding.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="device">Starting device address.</param>
    /// <param name="dtype">
    /// Type code: <c>U</c> unsigned 16-bit, <c>S</c> signed 16-bit,
    /// <c>D</c> unsigned 32-bit, <c>L</c> signed 32-bit, or <c>F</c> float32.
    /// </param>
    /// <param name="value">Value to encode and write. BIT requires Boolean; integer dtypes require an integral CLR type in range; F requires a finite numeric value within float32 range.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// Use this helper when application code wants strict typed writes without
    /// manually splitting words or packing float32 values. Values are not parsed
    /// from strings or converted between Boolean, floating, and integer types.
    /// </remarks>
    public static async Task WriteTypedAsync(
        this SlmpClient client,
        SlmpDeviceAddress device,
        string dtype,
        object value,
        CancellationToken ct = default)
    {
        var normalizedDType = RequireDType(dtype, nameof(dtype));
        switch (ResolveWriteRoute(device, normalizedDType, client.PlcProfile))
        {
            case SlmpNamedWriteRoute.RandomBits:
                await client.WriteRandomBitsAsync(
                        [(device, RequireBooleanWriteValue(value))],
                        ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.ContiguousBits:
                await client.WriteBitsAsync(device, [RequireBooleanWriteValue(value)], ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.ContiguousDWords when normalizedDType == "F":
                await client.WriteDWordsAsync(
                        device,
                        [unchecked((uint)BitConverter.SingleToInt32Bits(RequireFloat32WriteValue(value)))],
                        ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.RandomDWords when normalizedDType == "L":
                await client.WriteRandomWordsAsync(
                        [],
                        [(device, unchecked((uint)RequireInt32WriteValue(value, "L")))],
                        ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.RandomDWords:
                await client.WriteRandomWordsAsync(
                        [],
                        [(device, RequireUInt32WriteValue(value, "D"))],
                        ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.ContiguousDWords when normalizedDType == "L":
                await client.WriteDWordsAsync(
                        device,
                        [unchecked((uint)RequireInt32WriteValue(value, "L"))],
                        ct)
                    .ConfigureAwait(false);
                break;
            case SlmpNamedWriteRoute.ContiguousDWords:
                await client.WriteDWordsAsync(device, [RequireUInt32WriteValue(value, "D")], ct)
                    .ConfigureAwait(false);
                break;
            default:
                var word = normalizedDType == "S"
                    ? unchecked((ushort)RequireInt16WriteValue(value, "S"))
                    : RequireUInt16WriteValue(value, "U");
                await client.WriteWordsAsync(device, [word], ct)
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
    // Named-device read
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads a mixed named value set and returns a dictionary keyed by the original addresses.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="addresses">
    /// Address list such as <c>D100:U</c>, <c>D200:F</c>, <c>D300:L</c>, <c>M1000:BIT</c>, or <c>D50.3</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary whose keys match the requested address strings.</returns>
    /// <remarks>
    /// The complete address list is compiled into exactly one random-read request.
    /// Entries that require another command family are rejected before transport.
    /// </remarks>
    public static async Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this SlmpClient client,
        IEnumerable<string> addresses,
        CancellationToken ct = default)
    {
        var plan = CompileReadPlan(addresses, client.PlcProfile);
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
        var plan = CompileReadPlan(addresses, client.PlcProfile);
        return client.ExecuteAsync(inner => ReadNamedCompiledAsync(inner, plan, ct), ct);
    }

    /// <summary>
    /// Writes a mixed named value set by address string.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="updates">
    /// Mapping of address string to value, for example <c>"D100:U"</c>, <c>"D200:F"</c>,
    /// <c>"D50.3"</c>, or direct bit-device addresses such as <c>"M1000:BIT"</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// The complete update set is sent as exactly one random-write request. Word and DWord
    /// entries may share that request; bit entries use one random-bit request. Mixing those
    /// command families or requesting bit-in-word read-modify-write is rejected before transport.
    /// </remarks>
    public static async Task WriteNamedAsync(
        this SlmpClient client,
        IReadOnlyDictionary<string, object> updates,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (updates.Count == 0)
            throw new ArgumentException("WriteNamedAsync requires at least one update.", nameof(updates));

        var wordEntries = new List<(SlmpDeviceAddress Device, ushort Value)>();
        var dwordEntries = new List<(SlmpDeviceAddress Device, uint Value)>();
        var bitEntries = new List<(SlmpDeviceAddress Device, bool Value)>();
        foreach (var pair in updates)
        {
            var (baseAddress, dtype, bitIdx) = ParseAddress(pair.Key);
            var device = ParseDeviceForClient(client, baseAddress);
            if (dtype == "BIT_IN_WORD")
            {
                ValidateBitInWordTarget(pair.Key, device);
                _ = RequireBitInWordIndex(pair.Key, bitIdx);
                _ = RequireBooleanWriteValue(pair.Value);
                throw new ArgumentException(
                    $"Address '{pair.Key}' requires read-modify-write and is not supported by WriteNamedAsync; call WriteBitInWordAsync explicitly.",
                    nameof(updates));
            }

            var resolvedDType = ResolveDTypeForAddress(pair.Key, device, dtype, bitIdx);
            ValidateNamedDeviceDType(pair.Key, device, resolvedDType);
            ValidateLongTimerEntry(pair.Key, device, resolvedDType);
            _ = ResolveWriteRoute(device, resolvedDType, client.PlcProfile);
            switch (resolvedDType)
            {
                case "BIT":
                    bitEntries.Add((device, RequireBooleanWriteValue(pair.Value)));
                    break;
                case "U":
                    wordEntries.Add((device, RequireUInt16WriteValue(pair.Value, resolvedDType)));
                    break;
                case "S":
                    wordEntries.Add((device, unchecked((ushort)RequireInt16WriteValue(pair.Value, resolvedDType))));
                    break;
                case "F":
                    dwordEntries.Add((device, unchecked((uint)BitConverter.SingleToInt32Bits(RequireFloat32WriteValue(pair.Value)))));
                    break;
                case "L":
                    dwordEntries.Add((device, unchecked((uint)RequireInt32WriteValue(pair.Value, resolvedDType))));
                    break;
                default:
                    dwordEntries.Add((device, RequireUInt32WriteValue(pair.Value, resolvedDType)));
                    break;
            }
        }

        if (bitEntries.Count != 0 && (wordEntries.Count != 0 || dwordEntries.Count != 0))
            throw new ArgumentException(
                "WriteNamedAsync cannot mix bit and word/DWord destinations because that requires multiple protocol requests.",
                nameof(updates));

        if (bitEntries.Count != 0)
            await client.WriteRandomBitsAsync(bitEntries, ct).ConfigureAwait(false);
        else
            await client.WriteRandomWordsAsync(wordEntries, dwordEntries, ct).ConfigureAwait(false);
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
        var plan = CompileReadPlan(addresses, client.PlcProfile);
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
        var plan = CompileReadPlan(addresses, client.PlcProfile);
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

    internal static (string Base, string DType, int? BitIdx) ParseAddress(string address)
    {
        address = address.Trim();
        if (address.Contains(':'))
        {
            int index = address.IndexOf(':');
            var dtype = address[(index + 1)..].Trim().ToUpperInvariant();
            if (dtype == "BIT_IN_WORD")
                throw new ArgumentException(
                    $"Address '{address}' uses BIT_IN_WORD but no bit index was specified. Use '.0' through '.F' notation.",
                    nameof(address));
            dtype = RequireDType(dtype, nameof(address));
            return (address[..index].Trim(), dtype, null);
        }

        if (address.Contains('.'))
        {
            int index = address.IndexOf('.');
            var bitText = address[(index + 1)..].Trim();
            if (bitText.Length == 1 &&
                int.TryParse(bitText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int bit))
                return (address[..index].Trim(), "BIT_IN_WORD", bit);
            throw new ArgumentException($"Invalid bit-in-word index in '{address}'. Use one hex digit 0-F or ':' for data type.", nameof(address));
        }

        throw new ArgumentException(
            $"Address '{address}' requires an explicit dtype such as ':U', ':D', or ':BIT'.",
            nameof(address));
    }

    internal static string NormalizeNamedAddress(string address, SlmpPlcProfile plcProfile)
    {
        var trimmed = address.Trim();
        var (baseAddress, dtype, bitIdx) = ParseAddress(trimmed);
        var canonicalBase = NormalizeDeviceForFamily(baseAddress, plcProfile);
        if (bitIdx is int bit)
        {
            return $"{canonicalBase}.{bit.ToString("X", CultureInfo.InvariantCulture)}";
        }

        var device = SlmpDeviceParser.Parse(baseAddress, plcProfile);
        ValidateNamedDeviceDType(trimmed, device, dtype);
        return $"{canonicalBase}:{dtype}";
    }

    internal static SlmpNamedReadPlan CompileReadPlan(IEnumerable<string> addresses, SlmpPlcProfile plcProfile)
    {
        var entries = new List<SlmpNamedReadEntry>();
        var wordDevices = new List<SlmpDeviceAddress>();
        var dwordDevices = new List<SlmpDeviceAddress>();
        var seenWords = new HashSet<SlmpDeviceAddress>();
        var seenDwords = new HashSet<SlmpDeviceAddress>();

        foreach (var address in addresses)
        {
            var (baseAddress, dtype, bitIdx) = ParseAddress(address);
            var device = SlmpDeviceParser.Parse(baseAddress, plcProfile);
            var kind = SlmpNamedReadKind.Fallback;
            var longTimerRead = GetLongTimerReadSpec(device.Code);

            if (dtype == "BIT_IN_WORD")
            {
                ValidateBitInWordTarget(address, device);
                bitIdx = RequireBitInWordIndex(address, bitIdx);
                if (IsWordBatchable(device.Code))
                {
                    kind = SlmpNamedReadKind.BitInWord;
                    if (seenWords.Add(device))
                        wordDevices.Add(device);
                }
            }
            else
            {
                dtype = ResolveDTypeForAddress(address, device, dtype, bitIdx);
                ValidateNamedDeviceDType(address, device, dtype);
                ValidateLongTimerEntry(address, device, dtype);
                ValidateDWordOnlyEntry(address, device, dtype);
            }

            if (longTimerRead is not null)
            {
                kind = SlmpNamedReadKind.LongTimer;
            }
            else if (dtype == "BIT" && TryPlainBitWordRead(device, out var wordDevice, out var plainBitIndex))
            {
                device = wordDevice;
                bitIdx = plainBitIndex;
                dtype = "BIT_IN_WORD";
                kind = SlmpNamedReadKind.BitInWord;
                if (seenWords.Add(device))
                    wordDevices.Add(device);
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

        var unsupported = entries
            .Where(entry => entry.Kind is SlmpNamedReadKind.Fallback or SlmpNamedReadKind.LongTimer)
            .Select(entry => entry.Address)
            .ToArray();
        if (unsupported.Length != 0)
        {
            throw new ArgumentException(
                $"ReadNamedAsync accepts only addresses that fit one random-read request; use explicit read calls for {string.Join(", ", unsupported)}.",
                nameof(addresses));
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
                    result[entry.Address] = ((wordValues[entry.Device] >> RequireBitInWordIndex(entry.Address, entry.BitIndex)) & 1) != 0;
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
                        result[entry.Address] = ((words[0] >> RequireBitInWordIndex(entry.Address, entry.BitIndex)) & 1) != 0;
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
        if (IsLongCounterStateDirectBitRead(spec))
        {
            var bits = await client.ReadBitsUncheckedAsync(entry.Device, 1, ct).ConfigureAwait(false);
            return bits[0];
        }

        if (spec.BaseCode == SlmpDeviceCode.LCN && spec.Kind == SlmpLongTimerReadKind.Current)
        {
            var value = await ReadRandomDWordValueAsync(client, entry.Device, ct).ConfigureAwait(false);
            return string.Equals(entry.DType, "L", StringComparison.OrdinalIgnoreCase) ? DecodeSignedDWord(value) : value;
        }

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
                await client.ReadLongStatusBlockWordsAsync(SlmpDeviceCode.LCN, number, ct).ConfigureAwait(false)),
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
                "D" => timer.CurrentValue,
                "L" => DecodeSignedDWord(timer.CurrentValue),
                _ => throw new ArgumentException($"{spec.BaseCode} current value requires dtype 'D' or 'L'.", nameof(dtype)),
            },
            SlmpLongTimerReadKind.Contact => string.Equals(dtype, "BIT", StringComparison.OrdinalIgnoreCase) ? timer.Contact : throw new ArgumentException($"{spec.BaseCode} contact requires dtype 'BIT'.", nameof(dtype)),
            SlmpLongTimerReadKind.Coil => string.Equals(dtype, "BIT", StringComparison.OrdinalIgnoreCase) ? timer.Coil : throw new ArgumentException($"{spec.BaseCode} coil requires dtype 'BIT'.", nameof(dtype)),
            _ => throw new InvalidOperationException($"Unsupported long timer read kind: {spec.Kind}"),
        };
    }

    private static async Task<uint> ReadRandomDWordValueAsync(
        SlmpClient client,
        SlmpDeviceAddress device,
        CancellationToken ct)
    {
        var (_, dwords) = await client.ReadRandomAsync([], [device], ct).ConfigureAwait(false);
        return dwords[0];
    }

    private static async Task<(Dictionary<SlmpDeviceAddress, ushort> Words, Dictionary<SlmpDeviceAddress, uint> DWords)> ReadRandomMapsAsync(
        SlmpClient client,
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        CancellationToken ct)
    {
        if (wordDevices.Count > 0xFF || dwordDevices.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordDevices),
                "Named read must fit in one random-read request (at most 255 word and 255 DWord devices). Split intentionally in application code if multiple request times are acceptable.");
        }

        var words = new Dictionary<SlmpDeviceAddress, ushort>();
        var dwords = new Dictionary<SlmpDeviceAddress, uint>();
        if (wordDevices.Count == 0 && dwordDevices.Count == 0)
            return (words, dwords);

        var random = await client.ReadRandomAsync(wordDevices, dwordDevices, ct).ConfigureAwait(false);
        for (int i = 0; i < wordDevices.Count; i++)
            words[wordDevices[i]] = random.WordValues[i];
        for (int i = 0; i < dwordDevices.Count; i++)
            dwords[dwordDevices[i]] = random.DwordValues[i];

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

    private static int RequireBitInWordIndex(string address, int? bitIndex)
    {
        if (bitIndex is >= 0 and <= 15)
            return bitIndex.Value;

        throw new ArgumentException(
            $"Address '{address}' uses BIT_IN_WORD but no bit index was specified. Use '.0' through '.F' notation.",
            nameof(address));
    }

    private static string RequireDType(string dtype, string paramName)
    {
        var normalized = dtype.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new ArgumentException("dtype is required; specify BIT/U/S/D/L/F explicitly.", paramName);
        }

        if (normalized == "BIT_IN_WORD")
        {
            throw new ArgumentException("BIT_IN_WORD requires '.bit' notation such as 'D50.A'.", paramName);
        }

        if (normalized is not "BIT" and not "U" and not "S" and not "D" and not "L" and not "F")
        {
            throw new ArgumentException($"Unsupported dtype '{normalized}'; expected BIT/U/S/D/L/F.", paramName);
        }

        return normalized;
    }

    private static void ValidateNamedDeviceDType(string address, SlmpDeviceAddress device, string dtype)
    {
        if (dtype == "BIT_IN_WORD")
            return;

        var isBitDevice = IsBitDevice(device.Code);
        if (isBitDevice && dtype != "BIT")
        {
            throw new ArgumentException(
                $"Address '{address}' is a bit device and requires ':BIT'.",
                nameof(address));
        }

        if (!isBitDevice && dtype == "BIT")
        {
            throw new ArgumentException(
                $"Address '{address}' uses ':BIT', which is only valid for bit devices. Use '.bit' notation for a bit inside a word device.",
                nameof(address));
        }
    }

    private static string NormalizeDTypeForDevice(SlmpDeviceAddress device, string dtype)
        => RequireDType(dtype, nameof(dtype));

    internal static string ResolveDTypeForAddress(string address, SlmpDeviceAddress device, string dtype, int? bitIdx)
    {
        if (bitIdx is not null)
            return "BIT_IN_WORD";
        return NormalizeDTypeForDevice(device, dtype);
    }

    internal static SlmpNamedWriteRoute ResolveWriteRoute(
        SlmpDeviceAddress device,
        string dtype,
        SlmpPlcProfile plcProfile = SlmpPlcProfile.Unspecified)
    {
        var normalized = NormalizeDTypeForDevice(device, dtype);
        ValidateLongFamilyDType(device, normalized, nameof(dtype));
        ValidateDWordOnlyDType(device, normalized, nameof(dtype));
        ValidateWritableDevice(device, plcProfile);
        return normalized switch
        {
            // Long-family state writes must use Device Write Random
            // (0x1402). Direct bit write (0x1401) is guarded in SlmpClient.
            "BIT" when IsRandomBitWriteDevice(device.Code) => SlmpNamedWriteRoute.RandomBits,
            "BIT" => SlmpNamedWriteRoute.ContiguousBits,
            "D" or "L" when device.Code is SlmpDeviceCode.LTN
                or SlmpDeviceCode.LSTN
                or SlmpDeviceCode.LCN
                or SlmpDeviceCode.LZ
                => SlmpNamedWriteRoute.RandomDWords,
            "D" or "L" or "F" => SlmpNamedWriteRoute.ContiguousDWords,
            _ => SlmpNamedWriteRoute.ContiguousWords,
        };
    }

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
            SlmpDeviceCode.LCS => new SlmpLongTimerReadSpec(SlmpDeviceCode.LCS, SlmpLongTimerReadKind.Contact),
            SlmpDeviceCode.LCC => new SlmpLongTimerReadSpec(SlmpDeviceCode.LCC, SlmpLongTimerReadKind.Coil),
            _ => null,
        };

    private static bool IsLongCounterStateDirectBitRead(SlmpLongTimerReadSpec spec)
        => spec.BaseCode is SlmpDeviceCode.LCS or SlmpDeviceCode.LCC
           && spec.Kind is SlmpLongTimerReadKind.Contact or SlmpLongTimerReadKind.Coil;

    private static bool IsReadOnlyForProfile(SlmpDeviceCode code, SlmpPlcProfile plcProfile)
        => SlmpCapabilityProfiles.IsReadOnly(plcProfile, code.ToString());

    private static void ValidateWritableDevice(SlmpDeviceAddress device, SlmpPlcProfile plcProfile)
    {
        if (IsReadOnlyForProfile(device.Code, plcProfile))
        {
            throw new ArgumentException(
                $"{device.Code} is read-only for PLC profile '{SlmpPlcProfiles.ToCanonicalString(plcProfile)}' and cannot be written.",
                nameof(device));
        }
    }

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
                    $"Address '{address}' uses a 32-bit long current value. Specify ':D' or ':L'.",
                    nameof(address));
            }
            return;
        }

        if (!string.Equals(dtype, "BIT", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Address '{address}' is a long timer state device. Specify ':BIT'.",
                nameof(address));
        }
    }

    private static void ValidateDWordOnlyEntry(string address, SlmpDeviceAddress device, string dtype)
    {
        if (!IsDWordOnlyScalarDevice(device.Code))
            return;

        if (dtype is not "D" and not "L")
        {
            throw new ArgumentException(
                $"Address '{address}' uses a 32-bit device. Specify ':D' or ':L'.",
                nameof(address));
        }
    }

    private static void ValidateLongFamilyDType(SlmpDeviceAddress device, string dtype, string paramName)
    {
        var spec = GetLongTimerReadSpec(device.Code);
        if (spec is null)
            return;

        if (spec.Value.Kind == SlmpLongTimerReadKind.Current)
        {
            if (dtype is not "D" and not "L")
            {
                throw new ArgumentException(
                    $"{device.Code} is a 32-bit long current value. Use dtype 'D' or 'L'.",
                    paramName);
            }
            return;
        }

        if (dtype != "BIT")
        {
            throw new ArgumentException(
                $"{device.Code} is a long-family state device. Use dtype 'BIT'.",
                paramName);
        }
    }

    private static void ValidateDWordOnlyDType(SlmpDeviceAddress device, string dtype, string paramName)
    {
        if (!IsDWordOnlyScalarDevice(device.Code))
            return;

        if (dtype is not "D" and not "L")
        {
            throw new ArgumentException(
                $"{device.Code} is a 32-bit device. Use dtype 'D' or 'L'.",
                paramName);
        }
    }

    private static bool IsDWordOnlyScalarDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.LZ;

    private static bool IsRandomDWordAddressedDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.LZ;

    private static bool IsRandomBitWriteDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.LTS
            or SlmpDeviceCode.LTC
            or SlmpDeviceCode.LSTS
            or SlmpDeviceCode.LSTC
            or SlmpDeviceCode.LCS
            or SlmpDeviceCode.LCC;

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
            or SlmpDeviceCode.S
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

    private static bool IsPlainBitWordBatchable(SlmpDeviceCode code)
        => code is SlmpDeviceCode.SM
            or SlmpDeviceCode.X
            or SlmpDeviceCode.Y
            or SlmpDeviceCode.M
            or SlmpDeviceCode.L
            or SlmpDeviceCode.F
            or SlmpDeviceCode.V
            or SlmpDeviceCode.B
            or SlmpDeviceCode.SB;

    private static bool TryPlainBitWordRead(SlmpDeviceAddress device, out SlmpDeviceAddress wordDevice, out int bitIndex)
    {
        if (!IsPlainBitWordBatchable(device.Code))
        {
            wordDevice = default;
            bitIndex = 0;
            return false;
        }

        bitIndex = (int)(device.Number % 16U);
        wordDevice = new SlmpDeviceAddress(device.Code, device.Number - (uint)bitIndex, device.PlcProfile);
        return true;
    }

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

    private static bool RequireBooleanWriteValue(object value)
        => value is bool result
            ? result
            : throw new ArgumentException("BIT value must be a Boolean.", nameof(value));

    private static ushort RequireUInt16WriteValue(object value, string dtype)
    {
        var number = RequireIntegralWriteValue(value, dtype);
        if (number is < ushort.MinValue or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{dtype} value must be in range 0..65535.");
        return (ushort)number;
    }

    private static short RequireInt16WriteValue(object value, string dtype)
    {
        var number = RequireIntegralWriteValue(value, dtype);
        if (number is < short.MinValue or > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{dtype} value must be in range -32768..32767.");
        return (short)number;
    }

    private static uint RequireUInt32WriteValue(object value, string dtype)
    {
        var number = RequireIntegralWriteValue(value, dtype);
        if (number is < uint.MinValue or > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{dtype} value must be in range 0..4294967295.");
        return (uint)number;
    }

    private static int RequireInt32WriteValue(object value, string dtype)
    {
        var number = RequireIntegralWriteValue(value, dtype);
        if (number is < int.MinValue or > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{dtype} value must be in range -2147483648..2147483647.");
        return (int)number;
    }

    private static decimal RequireIntegralWriteValue(object value, string dtype)
        => value switch
        {
            sbyte number => number,
            byte number => number,
            short number => number,
            ushort number => number,
            int number => number,
            uint number => number,
            long number => number,
            ulong number => number,
            _ => throw new ArgumentException($"{dtype} value must use an integer CLR type.", nameof(value)),
        };

    private static float RequireFloat32WriteValue(object value)
    {
        var number = value switch
        {
            sbyte v => (double)v,
            byte v => v,
            short v => v,
            ushort v => v,
            int v => v,
            uint v => v,
            long v => v,
            ulong v => v,
            float v => v,
            double v => v,
            decimal v => (double)v,
            _ => throw new ArgumentException("F value must use a numeric CLR type.", nameof(value)),
        };
        var result = (float)number;
        if (!double.IsFinite(number) || !float.IsFinite(result))
            throw new ArgumentOutOfRangeException(nameof(value), value, "F value must be finite and within the float32 range.");
        return result;
    }

    private static short DecodeSignedWord(ushort value) => unchecked((short)value);

    private static int DecodeSignedDWord(uint value) => unchecked((int)value);

    private static float DecodeFloatDWord(uint value) => BitConverter.Int32BitsToSingle(unchecked((int)value));
}
