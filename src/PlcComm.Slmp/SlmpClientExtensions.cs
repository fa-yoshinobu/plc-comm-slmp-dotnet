using System.Globalization;
using System.Runtime.CompilerServices;

namespace PlcComm.Slmp;

/// <summary>
/// Extension methods for <see cref="SlmpClient"/> providing typed read/write helpers,
/// chunked reads, named-device access, and polling.
/// </summary>
public static class SlmpClientExtensions
{
    // -----------------------------------------------------------------------
    // Typed read / write
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads one device value and converts it to the specified type.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Device address.</param>
    /// <param name="dtype">
    /// Type code: "W" = ushort, "I" = short (signed 16-bit),
    /// "D" = uint (32-bit), "L" = int (signed 32-bit), "F" = float32.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<object> ReadTypedAsync(
        this SlmpClient client,
        SlmpDeviceAddress device,
        string dtype,
        CancellationToken ct = default)
    {
        switch (dtype.ToUpperInvariant())
        {
            case "F":
            case "D":
            case "L":
            {
                var dwords = await client.ReadDWordsAsync(device, 1, ct).ConfigureAwait(false);
                return dtype.ToUpperInvariant() switch
                {
                    "F" => BitConverter.Int32BitsToSingle(unchecked((int)dwords[0])),
                    "L" => (object)unchecked((int)dwords[0]),
                    _   => dwords[0],
                };
            }
            case "I":
            {
                var words = await client.ReadWordsAsync(device, 1, ct).ConfigureAwait(false);
                return unchecked((short)words[0]);
            }
            default: // "W"
            {
                var words = await client.ReadWordsAsync(device, 1, ct).ConfigureAwait(false);
                return words[0];
            }
        }
    }

    /// <summary>
    /// Writes one device value using the specified type.
    /// <paramref name="value"/> should be ushort, short, uint, int, or float depending on <paramref name="dtype"/>.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Device address.</param>
    /// <param name="dtype">Type code — same as <see cref="ReadTypedAsync"/>.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteTypedAsync(
        this SlmpClient client,
        SlmpDeviceAddress device,
        string dtype,
        object value,
        CancellationToken ct = default)
    {
        switch (dtype.ToUpperInvariant())
        {
            case "F":
                await client.WriteDWordsAsync(device,
                    [unchecked((uint)BitConverter.SingleToInt32Bits(Convert.ToSingle(value, CultureInfo.InvariantCulture)))], ct)
                    .ConfigureAwait(false);
                break;
            case "D":
                await client.WriteDWordsAsync(device, [Convert.ToUInt32(value)], ct).ConfigureAwait(false);
                break;
            case "L":
                await client.WriteDWordsAsync(device,
                    [unchecked((uint)Convert.ToInt32(value))], ct).ConfigureAwait(false);
                break;
            default: // "W" / "I"
                await client.WriteWordsAsync(device, [Convert.ToUInt16(value)], ct).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Performs a read-modify-write to set a single bit within a word device.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Word device address.</param>
    /// <param name="bitIndex">Bit position within the word (0–15).</param>
    /// <param name="value">New bit value.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteBitInWordAsync(
        this SlmpClient client,
        SlmpDeviceAddress device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
    {
        if (bitIndex is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "bitIndex must be 0-15.");
        var words = await client.ReadWordsAsync(device, 1, ct).ConfigureAwait(false);
        int cur = words[0];
        if (value) cur |=   1 << bitIndex;
        else       cur &= ~(1 << bitIndex);
        await client.WriteWordsAsync(device, [(ushort)(cur & 0xFFFF)], ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Chunked reads
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads word devices in multiple requests when <paramref name="count"/> exceeds
    /// <paramref name="maxPerRequest"/> (SLMP limit: 960 words).
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="start">Starting device address.</param>
    /// <param name="count">Total number of words to read.</param>
    /// <param name="maxPerRequest">Maximum words per SLMP request. Defaults to 960.</param>
    /// <param name="alignToDwords">
    /// When <see langword="true"/>, chunk boundaries are aligned to 2-word boundaries to prevent
    /// DWord / Float32 data tearing across separate requests. Default is <see langword="false"/>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<ushort[]> ReadWordsChunkedAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        int count,
        int maxPerRequest = 960,
        bool alignToDwords = false,
        CancellationToken ct = default)
    {
        int effectiveMax = alignToDwords ? (maxPerRequest / 2) * 2 : maxPerRequest;
        if (effectiveMax <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPerRequest), "maxPerRequest must be at least 2.");

        var result = new List<ushort>(count);
        int remaining = count;
        uint offset = 0;
        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, effectiveMax);
            if (alignToDwords && chunk % 2 != 0 && chunk > 1) chunk--;
            var addr = new SlmpDeviceAddress(start.Code, start.Number + offset);
            var words = await client.ReadWordsAsync(addr, (ushort)chunk, ct).ConfigureAwait(false);
            result.AddRange(words);
            offset += (uint)chunk;
            remaining -= chunk;
        }
        return [.. result];
    }

    /// <summary>
    /// Reads DWord (32-bit) devices in multiple requests. DWord boundaries are always aligned
    /// to prevent data tearing across requests.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="start">Starting device address.</param>
    /// <param name="count">Number of DWords to read.</param>
    /// <param name="maxDwordsPerRequest">Maximum DWords per request. Defaults to 480 (= 960 words / 2).</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<uint[]> ReadDWordsChunkedAsync(
        this SlmpClient client,
        SlmpDeviceAddress start,
        int count,
        int maxDwordsPerRequest = 480,
        CancellationToken ct = default)
    {
        var words = await client.ReadWordsChunkedAsync(
            start, count * 2,
            maxPerRequest: maxDwordsPerRequest * 2,
            alignToDwords: true,
            ct: ct).ConfigureAwait(false);

        var result = new uint[count];
        for (int i = 0; i < count; i++)
            result[i] = (uint)(words[i * 2] | (words[i * 2 + 1] << 16));
        return result;
    }

    // -----------------------------------------------------------------------
    // Named-device read
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads multiple devices by address string and returns results in a dictionary.
    /// </summary>
    /// <remarks>
    /// Address format examples:
    /// <list type="bullet">
    ///   <item><description>"D100" — ushort</description></item>
    ///   <item><description>"D100:F" — float32</description></item>
    ///   <item><description>"D100:I" — signed short</description></item>
    ///   <item><description>"D100:D" — unsigned 32-bit</description></item>
    ///   <item><description>"D100:L" — signed 32-bit</description></item>
    ///   <item><description>"D100.3" — bit 3 within word (bool)</description></item>
    /// </list>
    /// </remarks>
    public static async Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this SlmpClient client,
        IEnumerable<string> addresses,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, object>();
        foreach (var address in addresses)
        {
            var (baseAddr, dtype, bitIdx) = ParseScopeAddress(address);
            var device = SlmpDeviceParser.Parse(baseAddr);
            if (dtype == "BIT_IN_WORD")
            {
                var words = await client.ReadWordsAsync(device, 1, ct).ConfigureAwait(false);
                result[address] = ((words[0] >> (bitIdx ?? 0)) & 1) != 0;
            }
            else
            {
                result[address] = await client.ReadTypedAsync(device, dtype, ct).ConfigureAwait(false);
            }
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // Polling
    // -----------------------------------------------------------------------

    /// <summary>
    /// Continuously polls the specified devices at the given interval, yielding a snapshot each cycle.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="addresses">Device addresses to poll (same format as <see cref="ReadNamedAsync"/>).</param>
    /// <param name="interval">Time between polls.</param>
    /// <param name="ct">Cancellation token to stop polling.</param>
    /// <example>
    /// <code>
    /// await using var client = await SlmpClient.QuickConnectAsync("192.168.1.10");
    /// await foreach (var snapshot in client.PollAsync(["D100", "D200:F"], TimeSpan.FromSeconds(1)))
    /// {
    ///     Console.WriteLine($"D100={snapshot["D100"]}, D200:F={snapshot["D200:F"]}");
    /// }
    /// </code>
    /// </example>
    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this SlmpClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var addrList = addresses.ToList();
        while (!ct.IsCancellationRequested)
        {
            yield return await client.ReadNamedAsync(addrList, ct).ConfigureAwait(false);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    // "D100:F" → ("D100", "F", null),  "D100.3" → ("D100", "BIT_IN_WORD", 3)
    private static (string Base, string DType, int? BitIdx) ParseScopeAddress(string address)
    {
        if (address.Contains(':'))
        {
            int i = address.IndexOf(':');
            return (address[..i], address[(i + 1)..].ToUpperInvariant(), null);
        }
        if (address.Contains('.'))
        {
            int i = address.IndexOf('.');
            if (int.TryParse(address[(i + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bit))
                return (address[..i], "BIT_IN_WORD", bit);
        }
        return (address, "W", null);
    }
}
