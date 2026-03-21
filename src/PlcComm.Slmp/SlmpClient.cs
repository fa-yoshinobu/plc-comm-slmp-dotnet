using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace PlcComm.Slmp;

/// <summary>
/// A high-performance, asynchronous SLMP (MC Protocol) client for .NET.
/// Supports 3E and 4E frame formats over TCP and UDP.
/// </summary>
public sealed class SlmpClient : IDisposable, IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly SlmpTransportMode _transportMode;
    private TcpClient? _tcp;
    private NetworkStream? _tcpStream;
    private UdpClient? _udp;
    private ushort _serial;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlmpClient"/> class.
    /// </summary>
    /// <param name="host">The IP address or hostname of the PLC.</param>
    /// <param name="port">The port number. Defaults to 5000 (standard SLMP) or 1025 (custom).</param>
    /// <param name="transportMode">The transport protocol (TCP or UDP).</param>
    public SlmpClient(string host, int port = 1025, SlmpTransportMode transportMode = SlmpTransportMode.Tcp)
    {
        _host = host;
        _port = port;
        _transportMode = transportMode;
    }

    /// <summary>Gets or sets the SLMP frame format (3E or 4E).</summary>
    public SlmpFrameType FrameType { get; set; } = SlmpFrameType.Frame4E;
    /// <summary>Gets or sets the device access compatibility mode (Legacy or iQ-R).</summary>
    public SlmpCompatibilityMode CompatibilityMode { get; set; } = SlmpCompatibilityMode.Iqr;
    /// <summary>Gets or sets the destination routing information.</summary>
    public SlmpTargetAddress TargetAddress { get; set; } = new();
    /// <summary>Gets or sets the monitoring timer value (multiples of 250ms). Default is 0x0010 (4s).</summary>
    public ushort MonitoringTimer { get; set; } = 0x0010;
    /// <summary>Gets or sets the communication timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
    /// <summary>Gets the raw binary content of the last sent request frame.</summary>
    public byte[] LastRequestFrame { get; private set; } = [];
    /// <summary>Gets the raw binary content of the last received response frame.</summary>
    public byte[] LastResponseFrame { get; private set; } = [];
    /// <summary>
    /// Optional hook called for every raw frame sent and received.
    /// Useful for protocol tracing and debugging.
    /// </summary>
    public Action<SlmpTraceFrame>? TraceHook { get; set; }

    /// <summary>Gets a value indicating whether the client is currently connected.</summary>
    public bool IsOpen => _transportMode == SlmpTransportMode.Tcp ? _tcp?.Connected == true : _udp is not null;

    /// <summary>
    /// Opens the connection to the PLC asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (IsOpen) return;
        if (_transportMode == SlmpTransportMode.Tcp)
        {
            _tcp = new TcpClient();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(Timeout);
            await _tcp.ConnectAsync(_host, _port, linked.Token).ConfigureAwait(false);
            _tcp.ReceiveTimeout = (int)Timeout.TotalMilliseconds;
            _tcp.SendTimeout = (int)Timeout.TotalMilliseconds;
            _tcpStream = _tcp.GetStream();
            return;
        }

        _udp = new UdpClient();
        _udp.Client.ReceiveTimeout = (int)Timeout.TotalMilliseconds;
        _udp.Client.SendTimeout = (int)Timeout.TotalMilliseconds;
        _udp.Connect(_host, _port);
    }

    /// <summary>Opens the connection to the PLC synchronously.</summary>
    public void Open() => OpenAsync().GetAwaiter().GetResult();

    /// <summary>Closes the connection to the PLC.</summary>
    public void Close()
    {
        _tcpStream?.Dispose();
        _tcpStream = null;
        _tcp?.Close();
        _tcp = null;
        _udp?.Dispose();
        _udp = null;
    }

    /// <summary>Closes the connection to the PLC asynchronously.</summary>
    public Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    /// <summary>Disposes the client and closes the connection.</summary>
    public void Dispose() => Close();

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }

    private void FireTrace(SlmpTraceDirection direction, byte[] data)
        => TraceHook?.Invoke(new SlmpTraceFrame(direction, data, DateTime.UtcNow));

    /// <summary>
    /// Auto-detects the optimal protocol settings and returns a connected <see cref="QueuedSlmpClient"/>.
    /// Internally calls <see cref="ResolveProfileAsync"/> then <see cref="OpenAsync"/>.
    /// </summary>
    public static async Task<QueuedSlmpClient> QuickConnectAsync(
        string host,
        int port = 1025,
        CancellationToken cancellationToken = default)
    {
        var inner = new SlmpClient(host, port);
        var queued = new QueuedSlmpClient(inner);
        var profile = await queued.ResolveProfileAsync(cancellationToken).ConfigureAwait(false);
        queued.FrameType = profile.FrameType;
        queued.CompatibilityMode = profile.CompatibilityMode;
        await queued.OpenAsync(cancellationToken).ConfigureAwait(false);
        return queued;
    }

    /// <summary>
    /// Reads the PLC model and type name info asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An object containing model name and code.</returns>
    public async Task<SlmpTypeNameInfo> ReadTypeNameAsync(CancellationToken cancellationToken = default)
    {
        var payload = await RequestAsync(SlmpCommand.ReadTypeName, 0x0000, ReadOnlyMemory<byte>.Empty, true, cancellationToken).ConfigureAwait(false);
        if (payload.Length < 16) throw new SlmpException("read_type_name response too short");
        var model = Encoding.ASCII.GetString(payload.AsSpan(0, 16)).TrimEnd('\0', ' ');
        if (payload.Length >= 18)
        {
            return new SlmpTypeNameInfo(model, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(16, 2)), true);
        }
        return new SlmpTypeNameInfo(model, 0, false);
    }

    /// <summary>
    /// Attempts to automatically detect the optimal protocol settings (3E/4E, iQ-R/Legacy) for the target PLC.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Recommended settings based on heuristic checks.</returns>
    public async Task<SlmpProfileRecommendation> ResolveProfileAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new[]
        {
            (Frame: SlmpFrameType.Frame4E, Compat: SlmpCompatibilityMode.Iqr),
            (Frame: SlmpFrameType.Frame3E, Compat: SlmpCompatibilityMode.Legacy),
            (Frame: SlmpFrameType.Frame3E, Compat: SlmpCompatibilityMode.Iqr),
            (Frame: SlmpFrameType.Frame4E, Compat: SlmpCompatibilityMode.Legacy),
        };

        foreach (var candidate in candidates)
        {
            FrameType = candidate.Frame;
            CompatibilityMode = candidate.Compat;
            try
            {
                _ = await ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.SM, 400), 1, cancellationToken).ConfigureAwait(false);
                try
                {
                    var info = await ReadTypeNameAsync(cancellationToken).ConfigureAwait(false);
                    var rec = SlmpProfileHeuristics.Recommend(info);
                    return rec with { FrameType = candidate.Frame, CompatibilityMode = candidate.Compat };
                }
                catch
                {
                    return new SlmpProfileRecommendation(candidate.Frame, candidate.Compat, SlmpProfileClass.Unknown, true);
                }
            }
            catch (SlmpException ex) when (ex.EndCode.HasValue)
            {
                // PLC-level error still means the route/frame is reachable.
                return new SlmpProfileRecommendation(candidate.Frame, candidate.Compat, SlmpProfileClass.Unknown, true);
            }
            catch
            {
            }
        }

        return new SlmpProfileRecommendation(FrameType, CompatibilityMode, SlmpProfileClass.Unknown, false);
    }

    /// <summary>
    /// Reads word device values asynchronously.
    /// </summary>
    /// <param name="device">The starting device address.</param>
    /// <param name="points">Number of words to read.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An array of word values (ushort).</returns>
    public async Task<ushort[]> ReadWordsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        var payload = BuildReadWritePayload(device, points, null, bitUnit: false);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        var data = await RequestAsync(SlmpCommand.DeviceRead, sub, payload, true, cancellationToken).ConfigureAwait(false);
        if (data.Length != points * 2) throw new SlmpException("read_words payload size mismatch");
        var values = new ushort[points];
        for (var i = 0; i < points; i++) values[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i * 2, 2));
        return values;
    }

    public async Task WriteWordsAsync(SlmpDeviceAddress device, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
    {
        var payload = BuildReadWritePayload(device, checked((ushort)values.Count), values, bitUnit: false);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        _ = await RequestAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool[]> ReadBitsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        var payload = BuildReadWritePayload(device, points, null, bitUnit: true);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0001 : (ushort)0x0003;
        var data = await RequestAsync(SlmpCommand.DeviceRead, sub, payload, true, cancellationToken).ConfigureAwait(false);
        var result = new bool[points];
        var need = (points + 1) / 2;
        if (data.Length < need) throw new SlmpException("read_bits payload size mismatch");
        var idx = 0;
        for (var i = 0; i < need && idx < points; i++)
        {
            result[idx++] = ((data[i] >> 4) & 0x1) != 0;
            if (idx < points) result[idx++] = (data[i] & 0x1) != 0;
        }
        return result;
    }

    public async Task<ushort[]> ReadWordsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        ushort points,
        SlmpExtensionSpec extension,
        CancellationToken cancellationToken = default
    )
    {
        var effectiveExtension = ResolveEffectiveExtension(device, extension);
        var payload = BuildReadWritePayloadExtended(device.Device, points, null, effectiveExtension, bitUnit: false);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        var data = await RequestAsync(SlmpCommand.DeviceRead, sub, payload, true, cancellationToken).ConfigureAwait(false);
        if (data.Length != points * 2) throw new SlmpException("read_words_ext payload size mismatch");
        var values = new ushort[points];
        for (var i = 0; i < points; i++) values[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i * 2, 2));
        return values;
    }

    public async Task WriteWordsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        IReadOnlyList<ushort> values,
        SlmpExtensionSpec extension,
        CancellationToken cancellationToken = default
    )
    {
        var effectiveExtension = ResolveEffectiveExtension(device, extension);
        var payload = BuildReadWritePayloadExtended(device.Device, checked((ushort)values.Count), values, effectiveExtension, bitUnit: false);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        _ = await RequestAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteBitsAsync(SlmpDeviceAddress device, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
    {
        var wordValues = new ushort[values.Count];
        for (var i = 0; i < values.Count; i++) wordValues[i] = values[i] ? (ushort)1 : (ushort)0;
        var payload = BuildReadWritePayload(device, checked((ushort)values.Count), wordValues, bitUnit: true);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0001 : (ushort)0x0003;
        _ = await RequestAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<uint[]> ReadDWordsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        var words = await ReadWordsAsync(device, checked((ushort)(points * 2)), cancellationToken).ConfigureAwait(false);
        var result = new uint[points];
        for (var i = 0; i < points; i++)
        {
            result[i] = (uint)(words[i * 2] | (words[(i * 2) + 1] << 16));
        }
        return result;
    }

    public async Task WriteDWordsAsync(SlmpDeviceAddress device, IReadOnlyList<uint> values, CancellationToken cancellationToken = default)
    {
        var words = new ushort[values.Count * 2];
        for (var i = 0; i < values.Count; i++)
        {
            words[i * 2] = (ushort)(values[i] & 0xFFFF);
            words[(i * 2) + 1] = (ushort)((values[i] >> 16) & 0xFFFF);
        }
        await WriteWordsAsync(device, words, cancellationToken).ConfigureAwait(false);
    }

    public async Task<float[]> ReadFloat32sAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        var dwords = await ReadDWordsAsync(device, points, cancellationToken).ConfigureAwait(false);
        var values = new float[dwords.Length];
        for (var i = 0; i < dwords.Length; i++)
        {
            values[i] = BitConverter.Int32BitsToSingle(unchecked((int)dwords[i]));
        }
        return values;
    }

    public async Task WriteFloat32sAsync(SlmpDeviceAddress device, IReadOnlyList<float> values, CancellationToken cancellationToken = default)
    {
        var dwords = new uint[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            dwords[i] = unchecked((uint)BitConverter.SingleToInt32Bits(values[i]));
        }
        await WriteDWordsAsync(device, dwords, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(ushort[] WordValues, uint[] DwordValues)> ReadRandomAsync(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        CancellationToken cancellationToken = default
    )
    {
        if (wordDevices.Count > 0xFF || dwordDevices.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordDevices), "random counts must be <= 255");
        }

        var payload = new byte[2 + ((wordDevices.Count + dwordDevices.Count) * DeviceSpecSize())];
        payload[0] = (byte)wordDevices.Count;
        payload[1] = (byte)dwordDevices.Count;
        var offset = 2;
        foreach (var device in wordDevices)
        {
            offset += EncodeDeviceSpec(device, payload.AsSpan(offset));
        }
        foreach (var device in dwordDevices)
        {
            offset += EncodeDeviceSpec(device, payload.AsSpan(offset));
        }

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        var data = await RequestAsync(SlmpCommand.DeviceReadRandom, sub, payload, true, cancellationToken).ConfigureAwait(false);
        var expected = (wordDevices.Count * 2) + (dwordDevices.Count * 4);
        if (data.Length != expected)
        {
            throw new SlmpException($"read_random response size mismatch expected={expected} actual={data.Length}");
        }

        var words = new ushort[wordDevices.Count];
        var dwords = new uint[dwordDevices.Count];
        var cursor = 0;
        for (var i = 0; i < words.Length; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor, 2));
            cursor += 2;
        }
        for (var i = 0; i < dwords.Length; i++)
        {
            dwords[i] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor, 4));
            cursor += 4;
        }
        return (words, dwords);
    }

    public async Task WriteRandomWordsAsync(
        IReadOnlyList<(SlmpDeviceAddress Device, ushort Value)> wordEntries,
        IReadOnlyList<(SlmpDeviceAddress Device, uint Value)> dwordEntries,
        CancellationToken cancellationToken = default
    )
    {
        if (wordEntries.Count > 0xFF || dwordEntries.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordEntries), "random counts must be <= 255");
        }

        var payload = new byte[2 + (wordEntries.Count * (DeviceSpecSize() + 2)) + (dwordEntries.Count * (DeviceSpecSize() + 4))];
        payload[0] = (byte)wordEntries.Count;
        payload[1] = (byte)dwordEntries.Count;
        var offset = 2;
        foreach (var entry in wordEntries)
        {
            offset += EncodeDeviceSpec(entry.Device, payload.AsSpan(offset));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), entry.Value);
            offset += 2;
        }
        foreach (var entry in dwordEntries)
        {
            offset += EncodeDeviceSpec(entry.Device, payload.AsSpan(offset));
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), entry.Value);
            offset += 4;
        }

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        _ = await RequestAsync(SlmpCommand.DeviceWriteRandom, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteRandomBitsAsync(
        IReadOnlyList<(SlmpDeviceAddress Device, bool Value)> bitEntries,
        CancellationToken cancellationToken = default
    )
    {
        if (bitEntries.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(bitEntries), "random bit count must be <= 255");
        }

        var bitValueSize = CompatibilityMode == SlmpCompatibilityMode.Legacy ? 1 : 2;
        var payload = new byte[1 + (bitEntries.Count * (DeviceSpecSize() + bitValueSize))];
        payload[0] = (byte)bitEntries.Count;
        var offset = 1;
        foreach (var entry in bitEntries)
        {
            offset += EncodeDeviceSpec(entry.Device, payload.AsSpan(offset));
            if (CompatibilityMode == SlmpCompatibilityMode.Legacy)
            {
                payload[offset++] = entry.Value ? (byte)1 : (byte)0;
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), entry.Value ? (ushort)1 : (ushort)0);
                offset += 2;
            }
        }

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0001 : (ushort)0x0003;
        _ = await RequestAsync(SlmpCommand.DeviceWriteRandom, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(ushort[] WordValues, ushort[] BitWordValues)> ReadBlockAsync(
        IReadOnlyList<SlmpBlockRead> wordBlocks,
        IReadOnlyList<SlmpBlockRead> bitBlocks,
        CancellationToken cancellationToken = default
    )
    {
        if (wordBlocks.Count > 0xFF || bitBlocks.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordBlocks), "block counts must be <= 255");
        }

        var specSize = DeviceSpecSize();
        var totalWordPoints = wordBlocks.Sum(static x => (int)x.Points);
        var totalBitPoints = bitBlocks.Sum(static x => (int)x.Points);
        var payload = new byte[2 + ((wordBlocks.Count + bitBlocks.Count) * (specSize + 2))];
        payload[0] = (byte)wordBlocks.Count;
        payload[1] = (byte)bitBlocks.Count;
        var offset = 2;
        foreach (var block in wordBlocks)
        {
            offset += EncodeDeviceSpec(block.Device, payload.AsSpan(offset));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), block.Points);
            offset += 2;
        }
        foreach (var block in bitBlocks)
        {
            offset += EncodeDeviceSpec(block.Device, payload.AsSpan(offset));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), block.Points);
            offset += 2;
        }

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        var data = await RequestAsync(SlmpCommand.DeviceReadBlock, sub, payload, true, cancellationToken).ConfigureAwait(false);
        var expected = (totalWordPoints + totalBitPoints) * 2;
        if (data.Length != expected)
        {
            throw new SlmpException($"read_block response size mismatch expected={expected} actual={data.Length}");
        }

        var words = new ushort[totalWordPoints];
        var bits = new ushort[totalBitPoints];
        var cursor = 0;
        for (var i = 0; i < words.Length; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor, 2));
            cursor += 2;
        }
        for (var i = 0; i < bits.Length; i++)
        {
            bits[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor, 2));
            cursor += 2;
        }
        return (words, bits);
    }

    public async Task WriteBlockAsync(
        IReadOnlyList<SlmpBlockWrite> wordBlocks,
        IReadOnlyList<SlmpBlockWrite> bitBlocks,
        SlmpBlockWriteOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var writeOptions = options ?? new SlmpBlockWriteOptions();
        if (writeOptions.SplitMixedBlocks && wordBlocks.Count > 0 && bitBlocks.Count > 0)
        {
            await WriteBlockAsync(wordBlocks, [], new SlmpBlockWriteOptions(false, false), cancellationToken).ConfigureAwait(false);
            await WriteBlockAsync([], bitBlocks, new SlmpBlockWriteOptions(false, false), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (wordBlocks.Count > 0xFF || bitBlocks.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordBlocks), "block counts must be <= 255");
        }

        var specSize = DeviceSpecSize();
        var totalWordPoints = wordBlocks.Sum(static x => x.Values.Count);
        var totalBitPoints = bitBlocks.Sum(static x => x.Values.Count);
        var payload = new byte[2 + ((wordBlocks.Count + bitBlocks.Count) * (specSize + 2)) + ((totalWordPoints + totalBitPoints) * 2)];
        payload[0] = (byte)wordBlocks.Count;
        payload[1] = (byte)bitBlocks.Count;
        var offset = 2;
        foreach (var block in wordBlocks)
        {
            offset += EncodeDeviceSpec(block.Device, payload.AsSpan(offset));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), checked((ushort)block.Values.Count));
            offset += 2;
        }
        foreach (var block in bitBlocks)
        {
            offset += EncodeDeviceSpec(block.Device, payload.AsSpan(offset));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), checked((ushort)block.Values.Count));
            offset += 2;
        }
        foreach (var block in wordBlocks)
        {
            foreach (var value in block.Values)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), value);
                offset += 2;
            }
        }
        foreach (var block in bitBlocks)
        {
            foreach (var value in block.Values)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), value);
                offset += 2;
            }
        }

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        try
        {
            _ = await RequestAsync(SlmpCommand.DeviceWriteBlock, sub, payload, true, cancellationToken).ConfigureAwait(false);
        }
        catch (SlmpException ex) when (
            writeOptions.RetryMixedOnError
            && wordBlocks.Count > 0
            && bitBlocks.Count > 0
            && ex.EndCode is 0xC056 or 0xC05B or 0xC061 or 0x414A
        )
        {
            await WriteBlockAsync(wordBlocks, [], new SlmpBlockWriteOptions(false, false), cancellationToken).ConfigureAwait(false);
            await WriteBlockAsync([], bitBlocks, new SlmpBlockWriteOptions(false, false), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RemoteRunAsync(bool force = false, ushort clearMode = 2, CancellationToken cancellationToken = default)
    {
        var payload = force ? new byte[] { 0x03, 0x00, (byte)clearMode, 0x00 } : new byte[] { 0x01, 0x00, (byte)clearMode, 0x00 };
        _ = await RequestAsync(SlmpCommand.RemoteRun, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoteStopAsync(CancellationToken cancellationToken = default) => _ = await RequestAsync(SlmpCommand.RemoteStop, 0x0000, new byte[] { 0x01, 0x00 }, true, cancellationToken).ConfigureAwait(false);

    public async Task RemotePauseAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        var mode = force ? (ushort)0x0003 : (ushort)0x0001;
        _ = await RequestAsync(SlmpCommand.RemotePause, 0x0000, new byte[] { (byte)mode, 0x00 }, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoteLatchClearAsync(CancellationToken cancellationToken = default) => _ = await RequestAsync(SlmpCommand.RemoteLatchClear, 0x0000, new byte[] { 0x01, 0x00 }, true, cancellationToken).ConfigureAwait(false);

    public async Task RemoteResetAsync(ushort subcommand = 0x0000, bool expectResponse = true, CancellationToken cancellationToken = default) => _ = await RequestAsync(SlmpCommand.RemoteReset, subcommand, ReadOnlyMemory<byte>.Empty, expectResponse, cancellationToken).ConfigureAwait(false);

    public async Task RemotePasswordUnlockAsync(string password, CancellationToken cancellationToken = default)
    {
        _ = await RequestAsync(SlmpCommand.RemotePasswordUnlock, 0x0000, EncodePassword(password), true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemotePasswordLockAsync(string password, CancellationToken cancellationToken = default)
    {
        _ = await RequestAsync(SlmpCommand.RemotePasswordLock, 0x0000, EncodePassword(password), true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> SelfTestLoopbackAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (data.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "loopback payload must be <= 65535 bytes");
        }

        var payload = new byte[2 + data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), checked((ushort)data.Length));
        data.Span.CopyTo(payload.AsSpan(2));
        var response = await RequestAsync(SlmpCommand.SelfTest, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
        if (response.Length < 2)
        {
            throw new SlmpException("self_test response too short");
        }

        var responseLength = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(0, 2));
        if (responseLength + 2 > response.Length)
        {
            throw new SlmpException("self_test response length mismatch");
        }

        return response.AsSpan(2, responseLength).ToArray();
    }

    public async Task ClearErrorAsync(CancellationToken cancellationToken = default) => _ = await RequestAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty, true, cancellationToken).ConfigureAwait(false);

    public async Task<byte[]> RequestAsync(SlmpCommand command, ushort subcommand, ReadOnlyMemory<byte> payload, bool expectResponse = true, CancellationToken cancellationToken = default)
    {
        if (!IsOpen) await OpenAsync(cancellationToken).ConfigureAwait(false);
        var frame = BuildRequestFrame(command, subcommand, payload.Span);
        LastRequestFrame = frame;
        FireTrace(SlmpTraceDirection.Send, frame);
        if (_transportMode == SlmpTransportMode.Tcp)
        {
            if (_tcpStream is null) throw new SlmpException("tcp not open");
            await _tcpStream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await _tcpStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (!expectResponse)
            {
                LastResponseFrame = [];
                return [];
            }

            var response = await ReceiveTcpFrameAsync(_tcpStream, cancellationToken).ConfigureAwait(false);
            LastResponseFrame = response;
            FireTrace(SlmpTraceDirection.Receive, response);
            return ParseResponse(command, subcommand, response);
        }

        if (_udp is null) throw new SlmpException("udp not open");
        await _udp.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!expectResponse)
        {
            LastResponseFrame = [];
            return [];
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(Timeout);
        var datagram = await _udp.ReceiveAsync(linked.Token).ConfigureAwait(false);
        LastResponseFrame = datagram.Buffer;
        FireTrace(SlmpTraceDirection.Receive, datagram.Buffer);
        return ParseResponse(command, subcommand, datagram.Buffer);
    }

    private byte[] BuildRequestFrame(SlmpCommand command, ushort subcommand, ReadOnlySpan<byte> payload)
    {
        var headerSize = FrameType == SlmpFrameType.Frame4E ? 19 : 15;
        var frame = new byte[headerSize + payload.Length];
        if (FrameType == SlmpFrameType.Frame4E)
        {
            frame[0] = 0x54; frame[1] = 0x00;
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2, 2), _serial++);
            frame[6] = TargetAddress.Network;
            frame[7] = TargetAddress.Station;
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8, 2), TargetAddress.ModuleIo);
            frame[10] = TargetAddress.Multidrop;
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(11, 2), checked((ushort)(6 + payload.Length)));
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(13, 2), MonitoringTimer);
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(15, 2), (ushort)command);
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(17, 2), subcommand);
        }
        else
        {
            frame[0] = 0x50; frame[1] = 0x00;
            frame[2] = TargetAddress.Network;
            frame[3] = TargetAddress.Station;
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4, 2), TargetAddress.ModuleIo);
            frame[6] = TargetAddress.Multidrop;
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(7, 2), checked((ushort)(6 + payload.Length)));
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(9, 2), MonitoringTimer);
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(11, 2), (ushort)command);
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(13, 2), subcommand);
        }

        payload.CopyTo(frame.AsSpan(headerSize));
        return frame;
    }

    private static async Task<byte[]> ReceiveTcpFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var prefix13 = new byte[13];
        await ReadExactAsync(stream, prefix13, cancellationToken).ConfigureAwait(false);
        if (prefix13[0] == 0xD4 && prefix13[1] == 0x00)
        {
            var length = BinaryPrimitives.ReadUInt16LittleEndian(prefix13.AsSpan(11, 2));
            var body = new byte[length];
            await ReadExactAsync(stream, body, cancellationToken).ConfigureAwait(false);
            return prefix13.Concat(body).ToArray();
        }

        if (prefix13[0] == 0xD0 && prefix13[1] == 0x00)
        {
            var prefix9 = prefix13.AsSpan(0, 9).ToArray();
            var length = BinaryPrimitives.ReadUInt16LittleEndian(prefix9.AsSpan(7, 2));
            var body = new byte[length];
            await ReadExactAsync(stream, body, cancellationToken).ConfigureAwait(false);
            return prefix9.Concat(body).ToArray();
        }

        throw new SlmpException("invalid response subheader");
    }

    private byte[] ParseResponse(SlmpCommand command, ushort subcommand, byte[] response)
    {
        var is4E = response.Length >= 13 && response[0] == 0xD4 && response[1] == 0x00;
        var is3E = response.Length >= 9 && response[0] == 0xD0 && response[1] == 0x00;
        if (!is4E && !is3E) throw new SlmpException("invalid response header", command: command, subcommand: subcommand);
        var headerSize = is4E ? 13 : 9;
        var dataLength = is4E ? BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(11, 2)) : BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(7, 2));
        if (response.Length < headerSize + dataLength || dataLength < 2) throw new SlmpException("malformed response", command: command, subcommand: subcommand);
        var endCode = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(headerSize, 2));
        if (endCode != 0) throw new SlmpException($"SLMP error end_code=0x{endCode:X4} command=0x{(ushort)command:X4} subcommand=0x{subcommand:X4}", endCode, command, subcommand);
        return dataLength == 2 ? [] : response.AsSpan(headerSize + 2, dataLength - 2).ToArray();
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new SlmpException("connection closed while reading response");
            offset += read;
        }
    }

    private int DeviceSpecSize() => CompatibilityMode == SlmpCompatibilityMode.Legacy ? 4 : 6;

    private int EncodeDeviceSpec(SlmpDeviceAddress device, Span<byte> output)
    {
        if (CompatibilityMode == SlmpCompatibilityMode.Legacy)
        {
            output[0] = (byte)(device.Number & 0xFF);
            output[1] = (byte)((device.Number >> 8) & 0xFF);
            output[2] = (byte)((device.Number >> 16) & 0xFF);
            output[3] = (byte)((ushort)device.Code & 0xFF);
            return 4;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(output[..4], device.Number);
        BinaryPrimitives.WriteUInt16LittleEndian(output.Slice(4, 2), (ushort)device.Code);
        return 6;
    }

    private byte[] BuildReadWritePayload(SlmpDeviceAddress device, ushort points, IReadOnlyList<ushort>? values, bool bitUnit)
    {
        var writeBytes = values is null ? 0 : bitUnit ? (values.Count + 1) / 2 : values.Count * 2;
        var payload = new byte[DeviceSpecSize() + 2 + writeBytes];
        var offset = EncodeDeviceSpec(device, payload);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), points);
        offset += 2;
        if (values is null) return payload;

        if (bitUnit)
        {
            var idx = 0;
            while (idx < values.Count)
            {
                var high = values[idx] != 0 ? 0x10 : 0x00;
                idx++;
                var low = idx < values.Count && values[idx] != 0 ? 0x01 : 0x00;
                if (idx < values.Count) idx++;
                payload[offset++] = (byte)(high | low);
            }
            return payload;
        }

        foreach (var value in values)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), value);
            offset += 2;
        }

        return payload;
    }

    private SlmpExtensionSpec ResolveEffectiveExtension(SlmpQualifiedDeviceAddress device, SlmpExtensionSpec extension)
    {
        if (device.ExtensionSpecification is null || device.ExtensionSpecification.Value == extension.ExtensionSpecification)
        {
            return extension;
        }

        return extension with { ExtensionSpecification = device.ExtensionSpecification.Value };
    }

    private byte[] BuildReadWritePayloadExtended(
        SlmpDeviceAddress device,
        ushort points,
        IReadOnlyList<ushort>? values,
        SlmpExtensionSpec extension,
        bool bitUnit
    )
    {
        var extendedSpec = EncodeExtendedDeviceSpec(device, extension);
        var writeBytes = values is null ? 0 : bitUnit ? (values.Count + 1) / 2 : values.Count * 2;
        var payload = new byte[extendedSpec.Length + 2 + writeBytes];
        extendedSpec.CopyTo(payload, 0);
        var offset = extendedSpec.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), points);
        offset += 2;
        if (values is null) return payload;

        if (bitUnit)
        {
            var idx = 0;
            while (idx < values.Count)
            {
                var high = values[idx] != 0 ? 0x10 : 0x00;
                idx++;
                var low = idx < values.Count && values[idx] != 0 ? 0x01 : 0x00;
                if (idx < values.Count) idx++;
                payload[offset++] = (byte)(high | low);
            }
            return payload;
        }

        foreach (var value in values)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), value);
            offset += 2;
        }

        return payload;
    }

    private byte[] EncodeExtendedDeviceSpec(SlmpDeviceAddress device, SlmpExtensionSpec extension)
    {
        var captureAligned = (device.Code is SlmpDeviceCode.G or SlmpDeviceCode.HG) && (extension.DirectMemorySpecification is 0xF8 or 0xFA);
        var deviceSpec = new byte[DeviceSpecSize()];
        _ = EncodeDeviceSpec(device, deviceSpec);
        if (captureAligned)
        {
            var payload = new byte[2 + deviceSpec.Length + 1 + 1 + 2 + 1];
            var offset = 0;
            payload[offset++] = extension.ExtensionSpecificationModification;
            payload[offset++] = extension.DeviceModificationIndex;
            deviceSpec.CopyTo(payload, offset);
            offset += deviceSpec.Length;
            payload[offset++] = extension.DeviceModificationFlags;
            payload[offset++] = 0x00;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), extension.ExtensionSpecification);
            offset += 2;
            payload[offset] = extension.DirectMemorySpecification;
            return payload;
        }

        var data = new byte[2 + 1 + 1 + 1 + deviceSpec.Length + 1];
        var cursor = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(cursor, 2), extension.ExtensionSpecification);
        cursor += 2;
        data[cursor++] = extension.ExtensionSpecificationModification;
        data[cursor++] = extension.DeviceModificationIndex;
        data[cursor++] = extension.DeviceModificationFlags;
        deviceSpec.CopyTo(data, cursor);
        cursor += deviceSpec.Length;
        data[cursor] = extension.DirectMemorySpecification;
        return data;
    }

    private static byte[] EncodePassword(string password)
    {
        if (password is null)
        {
            throw new ArgumentNullException(nameof(password));
        }

        if (password.Length is < 6 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(password), "password length must be 6..32");
        }

        return Encoding.ASCII.GetBytes(password);
    }
}
