using System.Globalization;
using System.Runtime.CompilerServices;

namespace PlcComm.Slmp;

internal enum SlmpNamedReadKind
{
    Word,
    Dword,
    BitInWord,
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
    int DecodeIndex
);

internal sealed record SlmpNamedReadPlan(
    IReadOnlyList<SlmpNamedReadEntry> Entries,
    IReadOnlyList<SlmpDeviceAddress> WordDevices,
    IReadOnlyList<SlmpDeviceAddress> DwordDevices
);

internal readonly record struct SlmpPreparedTypedWrite(
    SlmpNamedWriteRoute Route,
    bool BitValue,
    ushort WordValue,
    uint DwordValue
);

internal sealed record SlmpNamedWritePlan(
    IReadOnlyList<(SlmpDeviceAddress Device, ushort Value)> WordEntries,
    IReadOnlyList<(SlmpDeviceAddress Device, uint Value)> DwordEntries,
    IReadOnlyList<(SlmpDeviceAddress Device, bool Value)> BitEntries
);

/// <summary>
/// Extension methods for <see cref="SlmpClient"/> providing typed read/write helpers,
/// single-request block access, named-device access, and polling.
/// </summary>
/// <remarks>
/// Typed, block, and named operations use one SLMP request unless the method explicitly
/// documents a read-modify-write sequence. Named operations reject plans that require
/// more than one request; polling performs a separate declared read cycle each interval.
/// Typed, named, polling, long-timer, and bit-in-word helpers complete route, span,
/// profile, and writable-target admission before waiting for the client FIFO.
/// </remarks>
public static class SlmpClientExtensions
{
    private static SlmpDeviceAddress ParseDeviceForClient(
        SlmpClient client,
        string address,
        [CallerArgumentExpression(nameof(address))] string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(address, parameterName);
        return SlmpDeviceParser.Parse(address, client.PlcProfile);
    }

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
    public static Task<object> ReadTypedAsync(
        this SlmpClient client,
        SlmpDeviceAddress device,
        string dtype,
        CancellationToken ct = default)
    {
        var normalizedDType = RequireDType(dtype, nameof(dtype));
        ValidateLongFamilyDType(device, normalizedDType, nameof(dtype));
        ValidateDWordOnlyDType(device, normalizedDType, nameof(dtype));
        ValidateDeviceUnitDType(device, normalizedDType, nameof(dtype));
        ValidateTypedReadAdmission(client, device, normalizedDType);
        return client.ExecuteExclusiveAsync(
            token => ReadTypedCoreAsync(client, device, normalizedDType, token),
            ct);
    }

    private static async Task<object> ReadTypedCoreAsync(
        SlmpClient client,
        SlmpDeviceAddress device,
        string normalizedDType,
        CancellationToken ct)
    {
        var longRead = GetLongTimerReadSpec(device.Code);
        if (longRead is not null)
        {
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
                        : (await client.ReadDWordsAsync(device, 1, ct).ConfigureAwait(false))[0];
                    return normalizedDType switch
                    {
                        "F" => DecodeFloatDWord(dword),
                        "L" => DecodeSignedDWord(dword),
                        _ => dword,
                    };
                }
            case "S":
                {
                    var words = await client.ReadWordsAsync(device, 1, ct).ConfigureAwait(false);
                    return DecodeSignedWord(words[0]);
                }
            default:
                {
                    var words = await client.ReadWordsAsync(device, 1, ct).ConfigureAwait(false);
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
    /// Device unit, route, and value validation complete before FIFO admission.
    /// </remarks>
    public static Task WriteTypedAsync(
        this SlmpClient client,
        SlmpDeviceAddress device,
        string dtype,
        object value,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalizedDType = RequireDType(dtype, nameof(dtype));
        var prepared = PrepareTypedWrite(device, normalizedDType, value, client.PlcProfile);
        return WriteTypedCoreAsync(client, device, prepared, ct);
    }

    private static Task WriteTypedCoreAsync(
        SlmpClient client,
        SlmpDeviceAddress device,
        SlmpPreparedTypedWrite prepared,
        CancellationToken ct)
    {
        return prepared.Route switch
        {
            SlmpNamedWriteRoute.RandomBits => client.WriteRandomBitsAsync(
                [(device, prepared.BitValue)],
                ct),
            SlmpNamedWriteRoute.ContiguousBits => client.WriteBitsAsync(
                device,
                [prepared.BitValue],
                ct),
            SlmpNamedWriteRoute.RandomDWords => client.WriteRandomWordsAsync(
                [],
                [(device, prepared.DwordValue)],
                ct),
            SlmpNamedWriteRoute.ContiguousDWords => client.WriteDWordsAsync(
                device,
                [prepared.DwordValue],
                ct),
            _ => client.WriteWordsAsync(device, [prepared.WordValue], ct),
        };
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
    /// Performs a read-modify-write to set or clear one bit inside a word device.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="device">Word device address such as <c>D50</c>.</param>
    /// <param name="bitIndex">Bit position within the word, from 0 to 15.</param>
    /// <param name="value">New bit state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// The read and write occupy one FIFO turn on this client, so its other
    /// operations cannot interleave. They remain two SLMP requests and are not
    /// PLC-atomic: another client, PLC logic, or external writer can change the
    /// word between them. Applications that require atomic coordination must
    /// implement it in the PLC contract. Bit-device packed-word access is not a
    /// bit-in-word operation and is rejected by this helper. Read and write
    /// admission both complete before FIFO waiting, so a read-only or
    /// wire-unrepresentable target sends neither request. One absolute deadline
    /// starts after FIFO admission and covers both requests. A successful read
    /// is always followed by the write, even when the selected bit is unchanged.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="device"/> is not word-addressable.</exception>
    public static Task WriteBitInWordAsync(
        this SlmpClient client,
        SlmpDeviceAddress device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
    {
        if (bitIndex is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "bitIndex must be 0-15.");
        if (!SlmpDeviceUnits.IsWord(device.Code))
        {
            throw new ArgumentException(
                $"WriteBitInWordAsync requires a word-addressable device; {device.Code} is bit-addressable.",
                nameof(device));
        }
        client.ValidateDirectWordReadAdmission(device, 1);
        client.ValidateDirectWordWriteAdmission(device, 1);
        return client.ExecuteExclusiveAsync(
            async operationToken =>
            {
                using var deadlineCancellation = new CancellationTokenSource();
                deadlineCancellation.CancelAfter(client.Timeout);
                using var compoundCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    operationToken,
                    deadlineCancellation.Token);
                try
                {
                    await WriteBitInWordCoreAsync(
                        client,
                        device,
                        bitIndex,
                        value,
                        compoundCancellation.Token).ConfigureAwait(false);
                }
                catch (SlmpOperationOutcomeUnknownException exception) when (
                    deadlineCancellation.IsCancellationRequested &&
                    !ct.IsCancellationRequested)
                {
                    throw new SlmpOperationOutcomeUnknownException(
                        SlmpOutcomeUnknownReason.Timeout,
                        exception.InnerException ?? exception);
                }
                catch (Exception exception) when (
                    deadlineCancellation.IsCancellationRequested &&
                    !ct.IsCancellationRequested)
                {
                    throw new SlmpTimeoutException(
                        "The SLMP bit-in-word read-modify-write deadline expired.",
                        exception);
                }
            },
            ct);
    }

    /// <summary>
    /// Performs the same explicit bit-in-word read-modify-write through one
    /// immutable qualified Extended Device route, including U module-buffer
    /// and J link-direct forms. Both requests use that exact route, occupy one
    /// FIFO turn and one absolute post-admission deadline, and the write is
    /// always sent after a successful read. The pair is not PLC-atomic and a
    /// possibly transmitted unconfirmed write is outcome unknown.
    /// </summary>
    public static Task WriteBitInWordAsync(
        this SlmpClient client,
        SlmpQualifiedDeviceAddress device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
    {
        if (bitIndex is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "bitIndex must be 0-15.");
        if (!SlmpDeviceUnits.IsWord(device.Device.Code))
        {
            throw new ArgumentException(
                $"WriteBitInWordAsync requires a word-addressable device; {device.Device.Code} is bit-addressable.",
                nameof(device));
        }
        client.ValidateExtendedWordReadAdmission(device, 1);
        client.ValidateExtendedWordWriteAdmission(device, 1);
        return client.ExecuteExclusiveAsync(
            async operationToken =>
            {
                using var deadlineCancellation = new CancellationTokenSource();
                deadlineCancellation.CancelAfter(client.Timeout);
                using var compoundCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    operationToken,
                    deadlineCancellation.Token);
                try
                {
                    var words = await client.ReadWordsExtendedAsync(
                        device, 1, compoundCancellation.Token).ConfigureAwait(false);
                    int current = words[0];
                    if (value)
                        current |= 1 << bitIndex;
                    else
                        current &= ~(1 << bitIndex);
                    await client.WriteWordsExtendedAsync(
                        device, [(ushort)(current & 0xFFFF)], compoundCancellation.Token).ConfigureAwait(false);
                }
                catch (SlmpOperationOutcomeUnknownException exception) when (
                    deadlineCancellation.IsCancellationRequested &&
                    !ct.IsCancellationRequested)
                {
                    throw new SlmpOperationOutcomeUnknownException(
                        SlmpOutcomeUnknownReason.Timeout,
                        exception.InnerException ?? exception);
                }
                catch (Exception exception) when (
                    deadlineCancellation.IsCancellationRequested &&
                    !ct.IsCancellationRequested)
                {
                    throw new SlmpTimeoutException(
                        "The SLMP bit-in-word read-modify-write deadline expired.",
                        exception);
                }
            },
            ct);
    }

    private static async Task WriteBitInWordCoreAsync(
        SlmpClient client,
        SlmpDeviceAddress device,
        int bitIndex,
        bool value,
        CancellationToken ct)
    {
        var words = await client.ReadWordsAsync(device, 1, ct).ConfigureAwait(false);
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
    /// <remarks>
    /// This overload has the same two-request, locally exclusive, non-PLC-atomic
    /// behavior as the typed-address overload.
    /// </remarks>
    public static Task WriteBitInWordAsync(
        this SlmpClient client,
        string device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
        => client.WriteBitInWordAsync(ParseDeviceForClient(client, device), bitIndex, value, ct);

    /// <summary>
    /// Reads a contiguous bit-device range using exactly one SLMP request.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="start">First bit device in the range.</param>
    /// <param name="count">Number of points to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Boolean values in PLC order.</returns>
    public static Task<bool[]> ReadBitsSingleRequestAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        int count,
        CancellationToken ct = default)
    {
        ValidateSingleRequestCount(count, start.PlcProfile == SlmpPlcProfile.IqF ? 3584 : 7168, nameof(count));
        return client.ReadBitsAsync(start, (ushort)count, ct);
    }

    /// <summary>
    /// Reads a contiguous bit-device range using a string address.
    /// </summary>
    public static Task<bool[]> ReadBitsSingleRequestAsync(
        this SlmpClient client,
        string start,
        int count,
        CancellationToken ct = default)
        => client.ReadBitsSingleRequestAsync(ParseDeviceForClient(client, start), count, ct);

    /// <summary>Deprecated compatibility name. Use <see cref="ReadBitsSingleRequestAsync(SlmpClient, SlmpDeviceAddress, int, CancellationToken)"/>.</summary>
    [Obsolete("Use ReadBitsSingleRequestAsync; ReadBitsBlockAsync will be removed after one compatibility release.")]
    public static Task<bool[]> ReadBitsBlockAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        ushort count,
        CancellationToken ct = default)
        => client.ReadBitsSingleRequestAsync(start, count, ct);

    /// <summary>Deprecated compatibility name. Use <see cref="ReadBitsSingleRequestAsync(SlmpClient, string, int, CancellationToken)"/>.</summary>
    [Obsolete("Use ReadBitsSingleRequestAsync; ReadBitsBlockAsync will be removed after one compatibility release.")]
    public static Task<bool[]> ReadBitsBlockAsync(
        this SlmpClient client,
        string start,
        ushort count,
        CancellationToken ct = default)
        => client.ReadBitsSingleRequestAsync(start, count, ct);

    /// <summary>
    /// Writes a contiguous bit-device range from boolean values.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="start">First bit device in the range.</param>
    /// <param name="values">Boolean values in PLC order.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task WriteBitsSingleRequestAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<bool> values,
        CancellationToken ct = default)
    {
        ValidateSingleRequestValues(values, start.PlcProfile == SlmpPlcProfile.IqF ? 3584 : 7168, nameof(values));
        return client.WriteBitsAsync(start, values, ct);
    }

    /// <summary>
    /// Writes a contiguous bit-device range using a string address.
    /// </summary>
    public static Task WriteBitsSingleRequestAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<bool> values,
        CancellationToken ct = default)
        => client.WriteBitsSingleRequestAsync(ParseDeviceForClient(client, start), values, ct);

    /// <summary>Deprecated compatibility name. Use <see cref="WriteBitsSingleRequestAsync(SlmpClient, SlmpDeviceAddress, IReadOnlyList{bool}, CancellationToken)"/>.</summary>
    [Obsolete("Use WriteBitsSingleRequestAsync; WriteBitsBlockAsync will be removed after one compatibility release.")]
    public static Task WriteBitsBlockAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<bool> values,
        CancellationToken ct = default)
        => client.WriteBitsSingleRequestAsync(start, values, ct);

    /// <summary>Deprecated compatibility name. Use <see cref="WriteBitsSingleRequestAsync(SlmpClient, string, IReadOnlyList{bool}, CancellationToken)"/>.</summary>
    [Obsolete("Use WriteBitsSingleRequestAsync; WriteBitsBlockAsync will be removed after one compatibility release.")]
    public static Task WriteBitsBlockAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<bool> values,
        CancellationToken ct = default)
        => client.WriteBitsSingleRequestAsync(start, values, ct);

    /// <summary>
    /// Writes a contiguous word-device range from 16-bit values.
    /// </summary>
    /// <param name="client">Connected SLMP client.</param>
    /// <param name="start">First word device in the range.</param>
    /// <param name="values">Word values in PLC order.</param>
    /// <param name="ct">Cancellation token.</param>
    [Obsolete("Use WriteWordsSingleRequestAsync; WriteWordsBlockAsync will be removed after one compatibility release.")]
    public static Task WriteWordsBlockAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
        => client.WriteWordsSingleRequestAsync(start, values, ct);

    /// <summary>
    /// Writes a contiguous word-device range using a string address.
    /// </summary>
    [Obsolete("Use WriteWordsSingleRequestAsync; WriteWordsBlockAsync will be removed after one compatibility release.")]
    public static Task WriteWordsBlockAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
        => client.WriteWordsSingleRequestAsync(start, values, ct);

    /// <summary>Deprecated compatibility name. Use <see cref="WriteDWordsSingleRequestAsync(SlmpClient, SlmpDeviceAddress, IReadOnlyList{uint}, CancellationToken)"/>. This overload will be removed after one compatibility release.</summary>
    [Obsolete("Use WriteDWordsSingleRequestAsync; WriteDWordsBlockAsync will be removed after one compatibility release.")]
    public static Task WriteDWordsBlockAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
        => client.WriteDWordsSingleRequestAsync(start, values, ct);

    /// <summary>Deprecated compatibility name. Use <see cref="WriteDWordsSingleRequestAsync(SlmpClient, string, IReadOnlyList{uint}, CancellationToken)"/>. This overload will be removed after one compatibility release.</summary>
    [Obsolete("Use WriteDWordsSingleRequestAsync; WriteDWordsBlockAsync will be removed after one compatibility release.")]
    public static Task WriteDWordsBlockAsync(
        this SlmpClient client,
        string start,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
        => client.WriteDWordsSingleRequestAsync(start, values, ct);

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
        return client.ReadWordsAsync(start, (ushort)count, ct);
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
    /// Reads contiguous DWord devices using one SLMP request or returns an error.
    /// </summary>
    public static Task<uint[]> ReadDWordsSingleRequestAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        int count,
        CancellationToken ct = default)
    {
        ValidateSingleRequestCount(count, 480, nameof(count));
        return client.ReadDWordsAsync(start, (ushort)count, ct);
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
    /// Use <see cref="ReadTypedAsync(SlmpClient,SlmpDeviceAddress,string,CancellationToken)"/>
    /// or an explicit long-timer helper for LTN/LSTN current, contact, and coil routes.
    /// </remarks>
    public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this SlmpClient client,
        IEnumerable<string> addresses,
        CancellationToken ct = default)
    {
        var plan = CompileReadPlan(addresses, client.PlcProfile);
        var prepared = PrepareNamedRead(client, plan);
        return client.ExecuteExclusiveAsync(
            token => ReadNamedCompiledAsync(client, plan, prepared, token),
            ct);
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
    /// The complete semantic plan is validated before FIFO admission.
    /// </remarks>
    public static Task WriteNamedAsync(
        this SlmpClient client,
        IReadOnlyDictionary<string, object> updates,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (updates.Count == 0)
            throw new ArgumentException("WriteNamedAsync requires at least one update.", nameof(updates));

        var snapshot = updates.ToArray();
        var plan = CompileNamedWritePlan(client, snapshot);
        return plan.BitEntries.Count != 0
            ? client.WriteRandomBitsAsync(plan.BitEntries, ct)
            : client.WriteRandomWordsAsync(plan.WordEntries, plan.DwordEntries, ct);
    }

    private static SlmpNamedWritePlan CompileNamedWritePlan(
        SlmpClient client,
        IReadOnlyList<KeyValuePair<string, object>> updates)
    {
        var wordEntries = new List<(SlmpDeviceAddress Device, ushort Value)>();
        var dwordEntries = new List<(SlmpDeviceAddress Device, uint Value)>();
        var bitEntries = new List<(SlmpDeviceAddress Device, bool Value)>();
        foreach (var pair in updates)
        {
            if (pair.Key is null)
                throw new ArgumentException("Update collection contains a null address.", nameof(updates));
            if (pair.Value is null)
                throw new ArgumentException($"Update '{pair.Key}' has a null value.", nameof(updates));
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

        return new SlmpNamedWritePlan(wordEntries, dwordEntries, bitEntries);
    }

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
    /// The address list, compact decode indexes, and immutable Random Read
    /// payload are validated and prepared once, then reused for every cycle.
    /// Each cycle retains the ordinary client FIFO, timeout, cancellation,
    /// close, and error contracts. This helper is suitable for periodic
    /// monitoring and historian ingestion.
    /// </remarks>
    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this SlmpClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var plan = CompileReadPlan(addresses, client.PlcProfile);
        var prepared = PrepareNamedRead(client, plan);
        while (!ct.IsCancellationRequested)
        {
            yield return await client.ExecuteExclusiveAsync(
                token => ReadNamedCompiledAsync(client, plan, prepared, token),
                ct).ConfigureAwait(false);
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

    private static void ValidateTypedReadAdmission(
        SlmpClient client,
        SlmpDeviceAddress device,
        string normalizedDType)
    {
        var longRead = GetLongTimerReadSpec(device.Code);
        if (longRead is not null)
        {
            if (IsLongCounterStateDirectBitRead(longRead.Value))
            {
                client.ValidateDirectBitReadUncheckedAdmission(device, 1);
                return;
            }

            if (longRead.Value.Kind == SlmpLongTimerReadKind.Current && device.Code == SlmpDeviceCode.LCN)
            {
                client.ValidateRandomReadAdmission([], [device]);
                return;
            }

            if (device.Number > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(device),
                    device,
                    "Long-timer typed reads require a device number no greater than Int32.MaxValue.");
            }
            _ = client.PrepareLongTimerRead(longRead.Value.BaseCode, (int)device.Number, 1);
            return;
        }

        switch (normalizedDType)
        {
            case "BIT":
                client.ValidateDirectBitReadAdmission(device, 1);
                break;
            case "F":
            case "D":
            case "L":
                if (IsRandomDWordAddressedDevice(device.Code))
                    client.ValidateRandomReadAdmission([], [device]);
                else
                    client.ValidateDirectDWordReadAdmission(device, 1);
                break;
            default:
                client.ValidateDirectWordReadAdmission(device, 1);
                break;
        }
    }

    private static SlmpPreparedRandomRead PrepareNamedRead(SlmpClient client, SlmpNamedReadPlan plan)
    {
        ValidateCompiledReadPlan(plan);
        if (plan.WordDevices.Count > 0xFF || plan.DwordDevices.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plan),
                "Named read must fit in one random-read request (at most 255 word and 255 DWord devices). Split intentionally in application code if multiple request times are acceptable.");
        }
        return client.PrepareRandomRead(plan.WordDevices, plan.DwordDevices);
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
        ArgumentNullException.ThrowIfNull(addresses);
        var entries = new List<SlmpNamedReadEntry>();
        var wordDevices = new List<SlmpDeviceAddress>();
        var dwordDevices = new List<SlmpDeviceAddress>();
        var wordIndexes = new Dictionary<SlmpDeviceAddress, int>();
        var dwordIndexes = new Dictionary<SlmpDeviceAddress, int>();

        foreach (var address in addresses)
        {
            if (address is null)
                throw new ArgumentException("Address collection contains null.", nameof(addresses));
            var (baseAddress, dtype, bitIdx) = ParseAddress(address);
            var device = SlmpDeviceParser.Parse(baseAddress, plcProfile);
            var kind = SlmpNamedReadKind.Fallback;

            if (dtype == "BIT_IN_WORD")
            {
                ValidateBitInWordTarget(address, device);
                bitIdx = RequireBitInWordIndex(address, bitIdx);
                if (IsWordBatchable(device.Code))
                {
                    kind = SlmpNamedReadKind.BitInWord;
                }
            }
            else
            {
                dtype = ResolveDTypeForAddress(address, device, dtype, bitIdx);
                ValidateNamedDeviceDType(address, device, dtype);
                ValidateLongTimerEntry(address, device, dtype);
                ValidateDWordOnlyEntry(address, device, dtype);
            }

            if (IsLongTimerDirectNamedDevice(device.Code))
            {
                kind = SlmpNamedReadKind.Fallback;
            }
            else if (dtype == "BIT" && TryPlainBitWordRead(device, out var wordDevice, out var plainBitIndex))
            {
                device = wordDevice;
                bitIdx = plainBitIndex;
                dtype = "BIT_IN_WORD";
                kind = SlmpNamedReadKind.BitInWord;
            }
            else if ((dtype == "U" || dtype == "S") && IsWordBatchable(device.Code))
            {
                kind = SlmpNamedReadKind.Word;
            }
            else if ((dtype == "D" || dtype == "L" || dtype == "F") && IsWordBatchable(device.Code))
            {
                kind = SlmpNamedReadKind.Dword;
            }

            var decodeIndex = -1;
            if (kind is SlmpNamedReadKind.Word or SlmpNamedReadKind.BitInWord)
            {
                if (!wordIndexes.TryGetValue(device, out decodeIndex))
                {
                    decodeIndex = wordDevices.Count;
                    wordIndexes.Add(device, decodeIndex);
                    wordDevices.Add(device);
                }
            }
            else if (kind is SlmpNamedReadKind.Dword)
            {
                if (!dwordIndexes.TryGetValue(device, out decodeIndex))
                {
                    decodeIndex = dwordDevices.Count;
                    dwordIndexes.Add(device, decodeIndex);
                    dwordDevices.Add(device);
                }
            }

            entries.Add(new SlmpNamedReadEntry(address, device, dtype, bitIdx, kind, decodeIndex));
        }

        if (entries.Count == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(addresses),
                "ReadNamedAsync requires at least one address for its single Random Read request.");
        }

        var unsupported = entries
            .Where(entry => entry.Kind is SlmpNamedReadKind.Fallback)
            .Select(entry => entry.Address)
            .ToArray();
        if (unsupported.Length != 0)
        {
            throw new ArgumentException(
                $"ReadNamedAsync accepts only addresses that fit one random-read request; use ReadTypedAsync or an explicit long-timer helper for long-timer Direct Read routes and explicit read calls for other unsupported entries: {string.Join(", ", unsupported)}.",
                nameof(addresses));
        }

        return new SlmpNamedReadPlan(entries, wordDevices, dwordDevices);
    }

    private static async Task<IReadOnlyDictionary<string, object>> ReadNamedCompiledAsync(
        SlmpClient client,
        SlmpNamedReadPlan plan,
        SlmpPreparedRandomRead prepared,
        CancellationToken ct)
    {
        var result = new Dictionary<string, object>(plan.Entries.Count);
        var (wordValues, dwordValues) = await client.ExecutePreparedRandomReadAsync(prepared, ct).ConfigureAwait(false);
        foreach (var entry in plan.Entries)
        {
            switch (entry.Kind)
            {
                case SlmpNamedReadKind.Word:
                    result[entry.Address] = entry.DType.Equals("S", StringComparison.OrdinalIgnoreCase)
                        ? (object)DecodeSignedWord(wordValues[entry.DecodeIndex])
                        : wordValues[entry.DecodeIndex];
                    break;
                case SlmpNamedReadKind.BitInWord:
                    result[entry.Address] = ((wordValues[entry.DecodeIndex] >> entry.BitIndex!.Value) & 1) != 0;
                    break;
                case SlmpNamedReadKind.Dword:
                    result[entry.Address] = entry.DType.ToUpperInvariant() switch
                    {
                        "F" => (object)DecodeFloatDWord(dwordValues[entry.DecodeIndex]),
                        "L" => DecodeSignedDWord(dwordValues[entry.DecodeIndex]),
                        _ => dwordValues[entry.DecodeIndex],
                    };
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported named-read plan kind: {entry.Kind}.");
            }
        }

        return result;
    }

    private static void ValidateCompiledReadPlan(SlmpNamedReadPlan plan)
    {
        if (plan.Entries.Count == 0)
            throw new ArgumentException("The named read plan must contain at least one entry.", nameof(plan));

        foreach (var entry in plan.Entries)
        {
            switch (entry.Kind)
            {
                case SlmpNamedReadKind.Word when
                    entry.DType is "U" or "S" &&
                    SlmpDeviceUnits.IsWord(entry.Device.Code) &&
                    entry.DecodeIndex >= 0 &&
                    entry.DecodeIndex < plan.WordDevices.Count &&
                    plan.WordDevices[entry.DecodeIndex] == entry.Device:
                    break;
                case SlmpNamedReadKind.BitInWord when
                    entry.DType == "BIT_IN_WORD" &&
                    entry.BitIndex is >= 0 and <= 15 &&
                    entry.DecodeIndex >= 0 &&
                    entry.DecodeIndex < plan.WordDevices.Count &&
                    plan.WordDevices[entry.DecodeIndex] == entry.Device:
                    break;
                case SlmpNamedReadKind.Dword when
                    entry.DType is "D" or "L" or "F" &&
                    SlmpDeviceUnits.IsWord(entry.Device.Code) &&
                    entry.DecodeIndex >= 0 &&
                    entry.DecodeIndex < plan.DwordDevices.Count &&
                    plan.DwordDevices[entry.DecodeIndex] == entry.Device:
                    break;
                default:
                    throw new ArgumentException(
                        $"Named read entry '{entry.Address}' is not a valid member of the single Random Read plan.",
                        nameof(plan));
            }
        }
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

    internal static void ValidateBitInWordTarget(string address, SlmpDeviceAddress device)
    {
        if (!SlmpDeviceUnits.IsWord(device.Code))
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
        ArgumentNullException.ThrowIfNull(dtype, paramName);
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

    internal static void ValidateNamedDeviceDType(string address, SlmpDeviceAddress device, string dtype)
    {
        if (dtype == "BIT_IN_WORD")
            return;

        var isBitDevice = SlmpDeviceUnits.IsBit(device.Code);
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

    private static void ValidateDeviceUnitDType(SlmpDeviceAddress device, string dtype, string paramName)
    {
        var isBitDevice = SlmpDeviceUnits.IsBit(device.Code);
        if (isBitDevice && dtype != "BIT")
        {
            throw new ArgumentException(
                $"{device.Code} is a bit device and requires dtype 'BIT'.",
                paramName);
        }

        if (!isBitDevice && dtype == "BIT")
        {
            throw new ArgumentException(
                $"{device.Code} is a word device and cannot use dtype 'BIT'. Use bit-in-word notation for a bit inside a word device.",
                paramName);
        }
    }

    internal static string ResolveDTypeForAddress(string address, SlmpDeviceAddress device, string dtype, int? bitIdx)
    {
        if (bitIdx is not null)
            return "BIT_IN_WORD";
        return RequireDType(dtype, nameof(dtype));
    }

    internal static SlmpNamedWriteRoute ResolveWriteRoute(
        SlmpDeviceAddress device,
        string dtype,
        SlmpPlcProfile plcProfile = SlmpPlcProfile.Unspecified)
    {
        var normalized = RequireDType(dtype, nameof(dtype));
        ValidateLongFamilyDType(device, normalized, nameof(dtype));
        ValidateDWordOnlyDType(device, normalized, nameof(dtype));
        ValidateDeviceUnitDType(device, normalized, nameof(dtype));
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

    private static SlmpPreparedTypedWrite PrepareTypedWrite(
        SlmpDeviceAddress device,
        string normalizedDType,
        object value,
        SlmpPlcProfile plcProfile)
    {
        var route = ResolveWriteRoute(device, normalizedDType, plcProfile);
        return normalizedDType switch
        {
            "BIT" => new SlmpPreparedTypedWrite(
                route,
                RequireBooleanWriteValue(value),
                default,
                default),
            "U" => new SlmpPreparedTypedWrite(
                route,
                default,
                RequireUInt16WriteValue(value, normalizedDType),
                default),
            "S" => new SlmpPreparedTypedWrite(
                route,
                default,
                unchecked((ushort)RequireInt16WriteValue(value, normalizedDType)),
                default),
            "F" => new SlmpPreparedTypedWrite(
                route,
                default,
                default,
                unchecked((uint)BitConverter.SingleToInt32Bits(RequireFloat32WriteValue(value)))),
            "L" => new SlmpPreparedTypedWrite(
                route,
                default,
                default,
                unchecked((uint)RequireInt32WriteValue(value, normalizedDType))),
            "D" => new SlmpPreparedTypedWrite(
                route,
                default,
                default,
                RequireUInt32WriteValue(value, normalizedDType)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(normalizedDType),
                normalizedDType,
                "Unsupported prepared write dtype."),
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

    private static bool IsLongTimerDirectNamedDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.LTN
            or SlmpDeviceCode.LSTN
            or SlmpDeviceCode.LTS
            or SlmpDeviceCode.LTC
            or SlmpDeviceCode.LSTS
            or SlmpDeviceCode.LSTC;

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
