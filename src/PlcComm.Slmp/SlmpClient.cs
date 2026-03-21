using System.Buffers.Binary;
using System.Linq;
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
    public static async Task<QueuedSlmpClient> OpenAndConnectAsync(
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
        if (payload.Length < 16) throw new SlmpError("read_type_name response too short");
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
            catch (SlmpError ex) when (ex.EndCode.HasValue)
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
    public async Task<ushort[]> ReadWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        var payload = BuildReadWritePayload(device, points, null, bitUnit: false);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        var data = await RequestAsync(SlmpCommand.DeviceRead, sub, payload, true, cancellationToken).ConfigureAwait(false);
        if (data.Length != points * 2) throw new SlmpError("read_words payload size mismatch");
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
        if (data.Length < need) throw new SlmpError("read_bits payload size mismatch");
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
        if (data.Length != points * 2) throw new SlmpError("read_words_ext payload size mismatch");
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

    public async Task<uint[]> ReadDWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        var words = await ReadWordsRawAsync(device, checked((ushort)(points * 2)), cancellationToken).ConfigureAwait(false);
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
        var dwords = await ReadDWordsRawAsync(device, points, cancellationToken).ConfigureAwait(false);
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
            throw new SlmpError($"read_random response size mismatch expected={expected} actual={data.Length}");
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

    public async Task<(ushort[] WordValues, uint[] DwordValues)> ReadRandomExtAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> wordDevices,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> dwordDevices,
        CancellationToken cancellationToken = default
    )
    {
        if (wordDevices.Count > 0xFF || dwordDevices.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordDevices), "random counts must be <= 255");
        }

        var payloadList = new List<byte> { (byte)wordDevices.Count, (byte)dwordDevices.Count };
        foreach (var (device, extension) in wordDevices)
        {
            payloadList.AddRange(EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension)));
        }
        foreach (var (device, extension) in dwordDevices)
        {
            payloadList.AddRange(EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension)));
        }

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        var data = await RequestAsync(SlmpCommand.DeviceReadRandom, sub, payloadList.ToArray(), true, cancellationToken).ConfigureAwait(false);
        var expected = (wordDevices.Count * 2) + (dwordDevices.Count * 4);
        if (data.Length != expected)
        {
            throw new SlmpError($"read_random_ext response size mismatch expected={expected} actual={data.Length}");
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

    public async Task WriteRandomWordsExtAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, ushort Value, SlmpExtensionSpec Extension)> wordEntries,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, uint Value, SlmpExtensionSpec Extension)> dwordEntries,
        CancellationToken cancellationToken = default
    )
    {
        if (wordEntries.Count > 0xFF || dwordEntries.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordEntries), "random counts must be <= 255");
        }

        var payloadList = new List<byte> { (byte)wordEntries.Count, (byte)dwordEntries.Count };
        foreach (var (device, value, extension) in wordEntries)
        {
            payloadList.AddRange(EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension)));
            payloadList.Add((byte)(value & 0xFF));
            payloadList.Add((byte)(value >> 8));
        }
        foreach (var (device, value, extension) in dwordEntries)
        {
            payloadList.AddRange(EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension)));
            payloadList.Add((byte)(value & 0xFF));
            payloadList.Add((byte)((value >> 8) & 0xFF));
            payloadList.Add((byte)((value >> 16) & 0xFF));
            payloadList.Add((byte)(value >> 24));
        }

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        _ = await RequestAsync(SlmpCommand.DeviceWriteRandom, sub, payloadList.ToArray(), true, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteRandomBitsExtAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, bool Value, SlmpExtensionSpec Extension)> bitEntries,
        CancellationToken cancellationToken = default
    )
    {
        if (bitEntries.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(bitEntries), "random bit count must be <= 255");
        }

        var payloadList = new List<byte> { (byte)bitEntries.Count };
        foreach (var (device, value, extension) in bitEntries)
        {
            payloadList.AddRange(EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension)));
            if (CompatibilityMode == SlmpCompatibilityMode.Legacy)
            {
                payloadList.Add(value ? (byte)1 : (byte)0);
            }
            else
            {
                payloadList.Add(value ? (byte)1 : (byte)0);
                payloadList.Add(0x00);
            }
        }

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0081 : (ushort)0x0083;
        _ = await RequestAsync(SlmpCommand.DeviceWriteRandom, sub, payloadList.ToArray(), true, cancellationToken).ConfigureAwait(false);
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
            throw new SlmpError($"read_block response size mismatch expected={expected} actual={data.Length}");
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
        catch (SlmpError ex) when (
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

    // -----------------------------------------------------------------------
    // Monitor register / execute (commands 0x0801 / 0x0802)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Registers a set of word and DWord devices for monitoring (command 0x0801).
    /// Call <see cref="RunMonitorCycleAsync"/> to read the registered devices.
    /// </summary>
    /// <param name="wordDevices">Word devices to monitor.</param>
    /// <param name="dwordDevices">DWord devices to monitor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RegisterMonitorDevicesAsync(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        CancellationToken cancellationToken = default)
    {
        if (wordDevices.Count == 0 && dwordDevices.Count == 0)
            throw new ArgumentException("wordDevices and dwordDevices must not both be empty.");
        if (wordDevices.Count > 0xFF || dwordDevices.Count > 0xFF)
            throw new ArgumentOutOfRangeException(nameof(wordDevices), "device counts must be <= 255.");

        var payload = new byte[2 + (wordDevices.Count + dwordDevices.Count) * DeviceSpecSize()];
        payload[0] = (byte)wordDevices.Count;
        payload[1] = (byte)dwordDevices.Count;
        var offset = 2;
        foreach (var device in wordDevices)
            offset += EncodeDeviceSpec(device, payload.AsSpan(offset));
        foreach (var device in dwordDevices)
            offset += EncodeDeviceSpec(device, payload.AsSpan(offset));

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        _ = await RequestAsync(SlmpCommand.MonitorRegister, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RegisterMonitorDevicesExtAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> wordDevices,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> dwordDevices,
        CancellationToken cancellationToken = default)
    {
        if (wordDevices.Count == 0 && dwordDevices.Count == 0)
            throw new ArgumentException("wordDevices and dwordDevices must not both be empty.");
        if (wordDevices.Count > 0xFF || dwordDevices.Count > 0xFF)
            throw new ArgumentOutOfRangeException(nameof(wordDevices), "device counts must be <= 255.");

        var payloadList = new List<byte> { (byte)wordDevices.Count, (byte)dwordDevices.Count };
        foreach (var (device, extension) in wordDevices)
            payloadList.AddRange(EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension)));
        foreach (var (device, extension) in dwordDevices)
            payloadList.AddRange(EncodeExtendedDeviceSpec(device.Device, ResolveEffectiveExtension(device, extension)));

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        _ = await RequestAsync(SlmpCommand.MonitorRegister, sub, payloadList.ToArray(), true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes one monitor cycle and returns the values of the previously registered devices (command 0x0802).
    /// </summary>
    /// <param name="wordPoints">Number of registered word devices.</param>
    /// <param name="dwordPoints">Number of registered DWord devices.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SlmpMonitorResult> RunMonitorCycleAsync(
        int wordPoints,
        int dwordPoints,
        CancellationToken cancellationToken = default)
    {
        if (wordPoints < 0 || dwordPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(wordPoints), "wordPoints and dwordPoints must be >= 0.");

        var data = await RequestAsync(SlmpCommand.Monitor, 0x0000, ReadOnlyMemory<byte>.Empty, true, cancellationToken).ConfigureAwait(false);
        var expected = wordPoints * 2 + dwordPoints * 4;
        if (data.Length != expected)
            throw new SlmpError($"monitor response size mismatch: expected={expected} actual={data.Length}");

        var words = new ushort[wordPoints];
        var dwords = new uint[dwordPoints];
        var cursor = 0;
        for (var i = 0; i < wordPoints; i++) { words[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor, 2)); cursor += 2; }
        for (var i = 0; i < dwordPoints; i++) { dwords[i] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor, 4)); cursor += 4; }
        return new SlmpMonitorResult(words, dwords);
    }

    // -----------------------------------------------------------------------
    // IP address set (command 0x0E31) — UDP only
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sets the IP address of a node discovered by <see cref="NodeSearchAsync"/> (command 0x0E31).
    /// Requires UDP transport.
    /// </summary>
    /// <param name="targetMac">MAC address of the target node (e.g. "AA:BB:CC:DD:EE:FF").</param>
    /// <param name="newIp">New IPv4 address to assign.</param>
    /// <param name="subnetMask">Subnet mask to assign.</param>
    /// <param name="defaultGateway">Default gateway to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task IpAddressSetAsync(
        string targetMac,
        string newIp,
        string subnetMask,
        string defaultGateway,
        CancellationToken cancellationToken = default)
    {
        if (_transportMode != SlmpTransportMode.Udp)
            throw new InvalidOperationException("IpAddressSetAsync requires UDP transport.");

        static byte[] ParseMac(string mac)
        {
            var hex = mac.Replace(":", "").Replace("-", "");
            if (hex.Length != 12) throw new ArgumentException($"MAC address must be 6 bytes: {mac}");
            return Convert.FromHexString(hex);
        }
        static byte[] ParseIp(string ip)
        {
            var parts = ip.Split('.');
            if (parts.Length != 4) throw new ArgumentException($"Invalid IPv4 address: {ip}");
            return parts.Select(byte.Parse).ToArray();
        }

        var macBytes = ParseMac(targetMac);
        var ipBytes = ParseIp(newIp);
        var maskBytes = ParseIp(subnetMask);
        var gwBytes = ParseIp(defaultGateway);

        var payload = new byte[6 + 2 + 4 + 2 + 4 + 2 + 4];
        var offset = 0;
        macBytes.CopyTo(payload, offset); offset += 6;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), 4); offset += 2;
        ipBytes.CopyTo(payload, offset); offset += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), 4); offset += 2;
        maskBytes.CopyTo(payload, offset); offset += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), 4); offset += 2;
        gwBytes.CopyTo(payload, offset);

        if (!IsOpen) await OpenAsync(cancellationToken).ConfigureAwait(false);
        var frame = BuildRequestFrame(SlmpCommand.IpAddressSet, 0x0000, payload);
        LastRequestFrame = frame;
        FireTrace(SlmpTraceDirection.Send, frame);
        if (_udp is null) throw new SlmpError("UDP not open");
        await _udp.SendAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Node search (command 0x0E30) — UDP only
    // -----------------------------------------------------------------------

    /// <summary>
    /// Broadcasts a NODE_SEARCH request and collects all responses within the current
    /// <see cref="Timeout"/> window. Requires UDP transport.
    /// </summary>
    /// <param name="myMac">Sender MAC address (e.g. "AA:BB:CC:DD:EE:FF").</param>
    /// <param name="myIp">Sender IPv4 address (e.g. "192.168.1.100").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SlmpNodeSearchInfo[]> NodeSearchAsync(
        string myMac,
        string myIp,
        CancellationToken cancellationToken = default)
    {
        if (_transportMode != SlmpTransportMode.Udp)
            throw new InvalidOperationException("NodeSearchAsync requires UDP transport.");

        var macHex = myMac.Replace(":", "").Replace("-", "");
        if (macHex.Length != 12)
            throw new ArgumentException("myMac must be a 6-byte MAC address.", nameof(myMac));
        var macBytes = Convert.FromHexString(macHex);

        var ipParts = myIp.Split('.');
        if (ipParts.Length != 4)
            throw new ArgumentException("myIp must be an IPv4 address.", nameof(myIp));
        var ipBytes = ipParts.Select(byte.Parse).ToArray();

        var payloadBytes = new byte[12];
        macBytes.CopyTo(payloadBytes, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(payloadBytes.AsSpan(6, 2), 4);
        ipBytes.CopyTo(payloadBytes, 8);

        if (!IsOpen) await OpenAsync(cancellationToken).ConfigureAwait(false);
        var frame = BuildRequestFrame(SlmpCommand.NodeSearch, 0x0000, payloadBytes);
        LastRequestFrame = frame;
        FireTrace(SlmpTraceDirection.Send, frame);

        if (_udp is null) throw new SlmpError("UDP not open");
        await _udp.SendAsync(frame, cancellationToken).ConfigureAwait(false);

        var results = new List<SlmpNodeSearchInfo>();
        var deadline = DateTimeOffset.UtcNow.Add(Timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) break;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(remaining);
            try
            {
                var datagram = await _udp.ReceiveAsync(linked.Token).ConfigureAwait(false);
                LastResponseFrame = datagram.Buffer;
                FireTrace(SlmpTraceDirection.Receive, datagram.Buffer);
                try
                {
                    var data = ParseResponse(SlmpCommand.NodeSearch, 0x0000, datagram.Buffer);
                    results.AddRange(ParseNodeSearchResponse(data));
                }
                catch (SlmpError) { /* ignore malformed responses */ }
            }
            catch (OperationCanceledException) { break; }
        }
        return [.. results];
    }

    private static SlmpNodeSearchInfo[] ParseNodeSearchResponse(byte[] data)
    {
        if (data.Length < 2) return [];
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        var nodes = new List<SlmpNodeSearchInfo>(count);
        var offset = 2;

        static (string Value, int Next)? ReadStr(byte[] d, int idx)
        {
            if (idx + 2 > d.Length) return null;
            var size = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(idx, 2));
            if (idx + 2 + size > d.Length) return null;
            var val = Encoding.ASCII.GetString(d, idx + 2, size).TrimEnd('\0');
            return (val, idx + 2 + size);
        }

        static (string Value, int Next)? ReadIp(byte[] d, int idx)
        {
            if (idx + 2 > d.Length) return null;
            var size = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(idx, 2));
            if (idx + 2 + size > d.Length) return null;
            var parts = d.Skip(idx + 2).Take(size).Select(static b => b.ToString());
            return (string.Join(".", parts), idx + 2 + size);
        }

        for (var i = 0; i < count; i++)
        {
            if (offset + 6 > data.Length) break;
            var mac = BitConverter.ToString(data, offset, 6).Replace("-", ":");
            offset += 6;

            var ipRes = ReadIp(data, offset); if (ipRes is null) break; var (ip, o1) = ipRes.Value; offset = o1;
            var maskRes = ReadIp(data, offset); if (maskRes is null) break; var (mask, o2) = maskRes.Value; offset = o2;
            var gwRes = ReadIp(data, offset); if (gwRes is null) break; var (gw, o3) = gwRes.Value; offset = o3;
            var hostRes = ReadStr(data, offset); if (hostRes is null) break; var (host, o4) = hostRes.Value; offset = o4;

            if (offset + 2 > data.Length) break;
            var vendor = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2)); offset += 2;

            var modelRes = ReadStr(data, offset); if (modelRes is null) break; var (model, o5) = modelRes.Value; offset = o5;

            if (offset + 2 > data.Length) break;
            var modelCode = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2)); offset += 2;

            var verRes = ReadStr(data, offset); if (verRes is null) break; var (version, o6) = verRes.Value; offset = o6;

            if (offset + 4 > data.Length) break;
            var port = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2)); offset += 2;
            var protocol = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2)); offset += 2;

            nodes.Add(new SlmpNodeSearchInfo(mac, ip, mask, gw, host, vendor, model, modelCode, version, port, protocol));
        }
        return [.. nodes];
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
            throw new SlmpError("self_test response too short");
        }

        var responseLength = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(0, 2));
        if (responseLength + 2 > response.Length)
        {
            throw new SlmpError("self_test response length mismatch");
        }

        return response.AsSpan(2, responseLength).ToArray();
    }

    public async Task ClearErrorAsync(CancellationToken cancellationToken = default) => _ = await RequestAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty, true, cancellationToken).ConfigureAwait(false);

    // -----------------------------------------------------------------------
    // PLC-initiated communication (OnDemand 0x2101)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Receives an SLMP request frame sent by the PLC (PLC-initiated). TCP only.
    /// </summary>
    public async Task<SlmpRequestFrame> ReceiveRequestAsync(CancellationToken cancellationToken = default)
    {
        if (_transportMode != SlmpTransportMode.Tcp || _tcpStream is null)
        {
            throw new SlmpError("ReceiveRequestAsync requires TCP transport");
        }

        var raw = await ReceiveTcpRequestFrameAsync(_tcpStream, cancellationToken).ConfigureAwait(false);
        FireTrace(SlmpTraceDirection.Receive, raw);
        return ParseRequestFrame(raw);
    }

    /// <summary>
    /// Receives an ONDEMAND (0x2101) request from the PLC and returns the payload data.
    /// </summary>
    public async Task<byte[]> ReceiveOnDemandAsync(CancellationToken cancellationToken = default)
    {
        var frame = await ReceiveRequestAsync(cancellationToken).ConfigureAwait(false);
        if (frame.Command != (ushort)SlmpCommand.OnDemand)
        {
            throw new SlmpError($"expected ONDEMAND request, got 0x{frame.Command:X4}");
        }

        return frame.Data;
    }

    /// <summary>
    /// Sends an ONDEMAND (0x2101) request to the PLC and waits for the PLC-initiated response.
    /// </summary>
    public async Task<byte[]> OnDemandAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        _ = await RequestAsync(SlmpCommand.OnDemand, 0x0000, payload, false, cancellationToken).ConfigureAwait(false);
        return await ReceiveOnDemandAsync(cancellationToken).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Label read / write (commands 0x041A / 0x141A / 0x041C / 0x141B)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads array labels from the PLC (command 0x041A).
    /// </summary>
    /// <param name="points">Labels to read, each with unit specification and array data length.</param>
    /// <param name="abbreviationLabels">Optional abbreviation label names (sent before regular points).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SlmpLabelArrayReadResult[]> ReadArrayLabelsAsync(
        IReadOnlyList<SlmpLabelArrayReadPoint> points,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
    {
        var abbrevs = abbreviationLabels ?? [];
        var payload = new List<byte>();
        WriteUInt16LE(payload, (ushort)points.Count);
        WriteUInt16LE(payload, (ushort)abbrevs.Count);
        foreach (var name in abbrevs)
            AppendLabelName(payload, name);
        foreach (var pt in points)
        {
            AppendLabelName(payload, pt.Label);
            payload.Add(pt.UnitSpecification);
            payload.Add(0x00);
            WriteUInt16LE(payload, pt.ArrayDataLength);
        }
        var data = await RequestAsync(SlmpCommand.LabelArrayRead, 0x0000, payload.ToArray(), true, cancellationToken).ConfigureAwait(false);
        return ParseArrayLabelReadResponse(data);
    }

    /// <summary>
    /// Writes array labels to the PLC (command 0x141A).
    /// </summary>
    public async Task WriteArrayLabelsAsync(
        IReadOnlyList<SlmpLabelArrayWritePoint> points,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
    {
        var abbrevs = abbreviationLabels ?? [];
        var payload = new List<byte>();
        WriteUInt16LE(payload, (ushort)points.Count);
        WriteUInt16LE(payload, (ushort)abbrevs.Count);
        foreach (var name in abbrevs)
            AppendLabelName(payload, name);
        foreach (var pt in points)
        {
            AppendLabelName(payload, pt.Label);
            payload.Add(pt.UnitSpecification);
            payload.Add(0x00);
            WriteUInt16LE(payload, pt.ArrayDataLength);
            payload.AddRange(pt.Data);
        }
        _ = await RequestAsync(SlmpCommand.LabelArrayWrite, 0x0000, payload.ToArray(), true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads random labels from the PLC (command 0x041C).
    /// </summary>
    public async Task<SlmpLabelRandomReadResult[]> ReadRandomLabelsAsync(
        IReadOnlyList<string> labels,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
    {
        var abbrevs = abbreviationLabels ?? [];
        var payload = new List<byte>();
        WriteUInt16LE(payload, (ushort)labels.Count);
        WriteUInt16LE(payload, (ushort)abbrevs.Count);
        foreach (var name in abbrevs)
            AppendLabelName(payload, name);
        foreach (var label in labels)
            AppendLabelName(payload, label);
        var data = await RequestAsync(SlmpCommand.LabelReadRandom, 0x0000, payload.ToArray(), true, cancellationToken).ConfigureAwait(false);
        return ParseRandomLabelReadResponse(data);
    }

    /// <summary>
    /// Writes random labels to the PLC (command 0x141B).
    /// </summary>
    public async Task WriteRandomLabelsAsync(
        IReadOnlyList<SlmpLabelRandomWritePoint> points,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
    {
        var abbrevs = abbreviationLabels ?? [];
        var payload = new List<byte>();
        WriteUInt16LE(payload, (ushort)points.Count);
        WriteUInt16LE(payload, (ushort)abbrevs.Count);
        foreach (var name in abbrevs)
            AppendLabelName(payload, name);
        foreach (var pt in points)
        {
            AppendLabelName(payload, pt.Label);
            WriteUInt16LE(payload, (ushort)pt.Data.Length);
            payload.AddRange(pt.Data);
        }
        _ = await RequestAsync(SlmpCommand.LabelWriteRandom, 0x0000, payload.ToArray(), true, cancellationToken).ConfigureAwait(false);
    }

    private static SlmpLabelArrayReadResult[] ParseArrayLabelReadResponse(byte[] data)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        var results = new SlmpLabelArrayReadResult[count];
        var offset = 2;
        for (var i = 0; i < count; i++)
        {
            var dtId = data[offset];
            var uSpec = data[offset + 1];
            var aLen = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 2, 2));
            offset += 4;
            var dataSize = uSpec == 0 ? aLen * 2 : aLen;
            results[i] = new SlmpLabelArrayReadResult(dtId, uSpec, aLen, data[offset..(offset + dataSize)]);
            offset += dataSize;
        }
        return results;
    }

    private static SlmpLabelRandomReadResult[] ParseRandomLabelReadResponse(byte[] data)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        var results = new SlmpLabelRandomReadResult[count];
        var offset = 2;
        for (var i = 0; i < count; i++)
        {
            var dtId = data[offset];
            var spare = data[offset + 1];
            var rLen = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 2, 2));
            offset += 4;
            results[i] = new SlmpLabelRandomReadResult(dtId, spare, rLen, data[offset..(offset + rLen)]);
            offset += rLen;
        }
        return results;
    }

    private static void AppendLabelName(List<byte> buffer, string label)
    {
        if (string.IsNullOrEmpty(label))
            throw new ArgumentException("Label name must not be empty.", nameof(label));
        var encoded = Encoding.Unicode.GetBytes(label);
        var charCount = (ushort)(encoded.Length / 2);
        WriteUInt16LE(buffer, charCount);
        buffer.AddRange(encoded);
    }

    private static void WriteUInt16LE(List<byte> buffer, ushort value)
    {
        buffer.Add((byte)(value & 0xFF));
        buffer.Add((byte)(value >> 8));
    }

    // -----------------------------------------------------------------------
    // Memory read / write (command 0x0613 / 0x1613)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads words from PLC memory (command 0x0613).
    /// </summary>
    /// <param name="headAddress">Starting memory address (32-bit).</param>
    /// <param name="wordLength">Number of words to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ushort[]> MemoryReadWordsAsync(
        uint headAddress,
        ushort wordLength,
        CancellationToken cancellationToken = default)
    {
        var payload = new byte[6];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), headAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), wordLength);
        var data = await RequestAsync(SlmpCommand.MemoryRead, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
        var result = new ushort[wordLength];
        for (var i = 0; i < wordLength; i++)
            result[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i * 2, 2));
        return result;
    }

    /// <summary>
    /// Writes words to PLC memory (command 0x1613).
    /// </summary>
    /// <param name="headAddress">Starting memory address (32-bit).</param>
    /// <param name="values">Word values to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task MemoryWriteWordsAsync(
        uint headAddress,
        IReadOnlyList<ushort> values,
        CancellationToken cancellationToken = default)
    {
        var payload = new byte[6 + values.Count * 2];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), headAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), (ushort)values.Count);
        for (var i = 0; i < values.Count; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6 + i * 2, 2), values[i]);
        _ = await RequestAsync(SlmpCommand.MemoryWrite, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Extend unit read / write (command 0x0601 / 0x1601)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads raw bytes from an extend unit (command 0x0601).
    /// </summary>
    /// <param name="headAddress">Starting address in the extend unit (32-bit).</param>
    /// <param name="byteLength">Number of bytes to read.</param>
    /// <param name="moduleNo">Extend unit module I/O number (e.g. 0x03E0 for CPU buffer).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<byte[]> ExtendUnitReadBytesAsync(
        uint headAddress,
        ushort byteLength,
        ushort moduleNo,
        CancellationToken cancellationToken = default)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), headAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), byteLength);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6, 2), moduleNo);
        return await RequestAsync(SlmpCommand.ExtendUnitRead, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads words from an extend unit (command 0x0601).
    /// </summary>
    /// <param name="headAddress">Starting address in the extend unit (32-bit).</param>
    /// <param name="wordLength">Number of words to read.</param>
    /// <param name="moduleNo">Extend unit module I/O number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ushort[]> ExtendUnitReadWordsAsync(
        uint headAddress,
        ushort wordLength,
        ushort moduleNo,
        CancellationToken cancellationToken = default)
    {
        var data = await ExtendUnitReadBytesAsync(headAddress, (ushort)(wordLength * 2), moduleNo, cancellationToken).ConfigureAwait(false);
        var result = new ushort[wordLength];
        for (var i = 0; i < wordLength; i++)
            result[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i * 2, 2));
        return result;
    }

    /// <summary>
    /// Reads a single word from an extend unit.
    /// </summary>
    public async Task<ushort> ExtendUnitReadWordAsync(uint headAddress, ushort moduleNo, CancellationToken cancellationToken = default)
        => (await ExtendUnitReadWordsAsync(headAddress, 1, moduleNo, cancellationToken).ConfigureAwait(false))[0];

    /// <summary>
    /// Reads a double word (32-bit) from an extend unit.
    /// </summary>
    public async Task<uint> ExtendUnitReadDWordAsync(uint headAddress, ushort moduleNo, CancellationToken cancellationToken = default)
    {
        var data = await ExtendUnitReadBytesAsync(headAddress, 4, moduleNo, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4));
    }

    /// <summary>
    /// Writes raw bytes to an extend unit (command 0x1601).
    /// </summary>
    /// <param name="headAddress">Starting address in the extend unit (32-bit).</param>
    /// <param name="moduleNo">Extend unit module I/O number.</param>
    /// <param name="data">Bytes to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExtendUnitWriteBytesAsync(
        uint headAddress,
        ushort moduleNo,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        var payload = new byte[8 + data.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), headAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), (ushort)data.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6, 2), moduleNo);
        data.Span.CopyTo(payload.AsSpan(8));
        _ = await RequestAsync(SlmpCommand.ExtendUnitWrite, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes words to an extend unit (command 0x1601).
    /// </summary>
    /// <param name="headAddress">Starting address in the extend unit (32-bit).</param>
    /// <param name="moduleNo">Extend unit module I/O number.</param>
    /// <param name="values">Word values to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExtendUnitWriteWordsAsync(
        uint headAddress,
        ushort moduleNo,
        IReadOnlyList<ushort> values,
        CancellationToken cancellationToken = default)
    {
        var wordBytes = new byte[values.Count * 2];
        for (var i = 0; i < values.Count; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(wordBytes.AsSpan(i * 2, 2), values[i]);
        await ExtendUnitWriteBytesAsync(headAddress, moduleNo, wordBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes a single word to an extend unit.</summary>
    public Task ExtendUnitWriteWordAsync(uint headAddress, ushort moduleNo, ushort value, CancellationToken cancellationToken = default)
        => ExtendUnitWriteWordsAsync(headAddress, moduleNo, [value], cancellationToken);

    /// <summary>Writes a double word (32-bit) to an extend unit.</summary>
    public async Task ExtendUnitWriteDWordAsync(uint headAddress, ushort moduleNo, uint value, CancellationToken cancellationToken = default)
    {
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, value);
        await ExtendUnitWriteBytesAsync(headAddress, moduleNo, data, cancellationToken).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // CPU buffer convenience wrappers (moduleNo = 0x03E0)
    // -----------------------------------------------------------------------

    /// <summary>Reads words from the CPU buffer (extend unit module 0x03E0).</summary>
    public Task<ushort[]> CpuBufferReadWordsAsync(uint headAddress, ushort wordLength, CancellationToken cancellationToken = default)
        => ExtendUnitReadWordsAsync(headAddress, wordLength, 0x03E0, cancellationToken);

    /// <summary>Reads bytes from the CPU buffer (extend unit module 0x03E0).</summary>
    public Task<byte[]> CpuBufferReadBytesAsync(uint headAddress, ushort byteLength, CancellationToken cancellationToken = default)
        => ExtendUnitReadBytesAsync(headAddress, byteLength, 0x03E0, cancellationToken);

    /// <summary>Reads a single word from the CPU buffer.</summary>
    public Task<ushort> CpuBufferReadWordAsync(uint headAddress, CancellationToken cancellationToken = default)
        => ExtendUnitReadWordAsync(headAddress, 0x03E0, cancellationToken);

    /// <summary>Reads a double word from the CPU buffer.</summary>
    public Task<uint> CpuBufferReadDWordAsync(uint headAddress, CancellationToken cancellationToken = default)
        => ExtendUnitReadDWordAsync(headAddress, 0x03E0, cancellationToken);

    /// <summary>Writes words to the CPU buffer (extend unit module 0x03E0).</summary>
    public Task CpuBufferWriteWordsAsync(uint headAddress, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
        => ExtendUnitWriteWordsAsync(headAddress, 0x03E0, values, cancellationToken);

    /// <summary>Writes bytes to the CPU buffer (extend unit module 0x03E0).</summary>
    public Task CpuBufferWriteBytesAsync(uint headAddress, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        => ExtendUnitWriteBytesAsync(headAddress, 0x03E0, data, cancellationToken);

    /// <summary>Writes a single word to the CPU buffer.</summary>
    public Task CpuBufferWriteWordAsync(uint headAddress, ushort value, CancellationToken cancellationToken = default)
        => ExtendUnitWriteWordAsync(headAddress, 0x03E0, value, cancellationToken);

    /// <summary>Writes a double word to the CPU buffer.</summary>
    public Task CpuBufferWriteDWordAsync(uint headAddress, uint value, CancellationToken cancellationToken = default)
        => ExtendUnitWriteDWordAsync(headAddress, 0x03E0, value, cancellationToken);

    // -----------------------------------------------------------------------
    // File commands (0x1810 – 0x182A)
    // -----------------------------------------------------------------------

    /// <summary>Opens a file on the PLC and returns a file-pointer handle (command 0x1827).</summary>
    /// <param name="filename">File name (ASCII for subcommand 0x0000; UTF-16-LE for 0x0040).</param>
    /// <param name="driveNo">Drive number.</param>
    /// <param name="subcommand">0x0000 (standard) or 0x0040 (extended Unicode).</param>
    /// <param name="password">Optional file password.</param>
    /// <param name="writeOpen">True to open for write; false for read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>File-pointer handle number.</returns>
    public async Task<ushort> FileOpenHandleAsync(
        string filename,
        ushort driveNo,
        ushort subcommand = 0x0000,
        string? password = null,
        bool writeOpen = true,
        CancellationToken ct = default)
    {
        var payload = new List<byte>();
        AppendFilePassword(payload, subcommand, password);
        WriteUInt16LE(payload, writeOpen ? (ushort)0x0001 : (ushort)0x0000);
        WriteUInt16LE(payload, driveNo);
        AppendFileName(payload, subcommand, filename);
        var data = await RequestAsync(SlmpCommand.FileOpen, subcommand, payload.ToArray(), true, ct).ConfigureAwait(false);
        if (data.Length < 2) throw new SlmpError("FileOpen response too short");
        return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
    }

    /// <summary>Closes a file-pointer handle (command 0x182A).</summary>
    /// <param name="filePointerNo">The handle returned by <see cref="FileOpenHandleAsync"/>.</param>
    /// <param name="closeType">0 = normal close, 1 = close and save, 2 = close without save.</param>
    /// <param name="subcommand">Subcommand (default 0x0000).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task FileCloseHandleAsync(
        ushort filePointerNo,
        int closeType = 0,
        ushort subcommand = 0x0000,
        CancellationToken ct = default)
    {
        if (closeType is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(closeType), "closeType must be 0, 1, or 2.");
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), filePointerNo);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), (ushort)closeType);
        _ = await RequestAsync(SlmpCommand.FileClose, subcommand, payload, true, ct).ConfigureAwait(false);
    }

    /// <summary>Reads a chunk of data from an open file (command 0x1828).</summary>
    /// <param name="filePointerNo">File handle.</param>
    /// <param name="offset">Read offset within the file (bytes).</param>
    /// <param name="size">Number of bytes to read (max 1920).</param>
    /// <param name="subcommand">Subcommand (default 0x0000).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<byte[]> FileReadChunkAsync(
        ushort filePointerNo,
        uint offset = 0,
        ushort size = 1920,
        ushort subcommand = 0x0000,
        CancellationToken ct = default)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), filePointerNo);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(2, 4), offset);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6, 2), size);
        var data = await RequestAsync(SlmpCommand.FileRead, subcommand, payload, true, ct).ConfigureAwait(false);
        if (data.Length < 2) throw new SlmpError("FileRead response too short");
        var readSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        var body = data[2..];
        if (readSize != body.Length)
            throw new SlmpError($"FileRead size mismatch: expected={readSize} actual={body.Length}");
        return body;
    }

    /// <summary>Writes a chunk of data to an open file (command 0x1829).</summary>
    /// <param name="filePointerNo">File handle.</param>
    /// <param name="offset">Write offset within the file (bytes).</param>
    /// <param name="data">Data to write.</param>
    /// <param name="subcommand">Subcommand (default 0x0000).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of bytes written.</returns>
    public async Task<ushort> FileWriteChunkAsync(
        ushort filePointerNo,
        uint offset,
        ReadOnlyMemory<byte> data,
        ushort subcommand = 0x0000,
        CancellationToken ct = default)
    {
        var payload = new byte[8 + data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), filePointerNo);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(2, 4), offset);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6, 2), (ushort)data.Length);
        data.Span.CopyTo(payload.AsSpan(8));
        var resp = await RequestAsync(SlmpCommand.FileWrite, subcommand, payload, true, ct).ConfigureAwait(false);
        if (resp.Length < 2) throw new SlmpError("FileWrite response too short");
        return BinaryPrimitives.ReadUInt16LittleEndian(resp.AsSpan(0, 2));
    }

    /// <summary>Creates a new file on the PLC (command 0x1820).</summary>
    public async Task FileNewFileAsync(
        string filename,
        uint fileSize,
        ushort driveNo,
        ushort subcommand = 0x0000,
        string? password = null,
        CancellationToken ct = default)
    {
        var payload = new List<byte>();
        AppendFilePassword(payload, subcommand, password);
        WriteUInt16LE(payload, driveNo);
        payload.AddRange(BitConverter.GetBytes(fileSize));  // 4 bytes LE
        AppendFileName(payload, subcommand, filename);
        _ = await RequestAsync(SlmpCommand.FileNew, subcommand, payload.ToArray(), true, ct).ConfigureAwait(false);
    }

    /// <summary>Deletes a file by name (command 0x1822).</summary>
    public async Task FileDeleteByNameAsync(
        string filename,
        ushort driveNo,
        ushort subcommand = 0x0000,
        string? password = null,
        CancellationToken ct = default)
    {
        var payload = new List<byte>();
        AppendFilePassword(payload, subcommand, password);
        WriteUInt16LE(payload, driveNo);
        AppendFileName(payload, subcommand, filename);
        _ = await RequestAsync(SlmpCommand.FileDelete, subcommand, payload.ToArray(), true, ct).ConfigureAwait(false);
    }

    /// <summary>Changes file attributes by name (command 0x1825).</summary>
    public async Task FileChangeStateByNameAsync(
        string filename,
        ushort driveNo,
        ushort attribute,
        ushort subcommand = 0x0000,
        string? password = null,
        CancellationToken ct = default)
    {
        var payload = new List<byte>();
        AppendFilePassword(payload, subcommand, password);
        WriteUInt16LE(payload, driveNo);
        WriteUInt16LE(payload, attribute);
        AppendFileName(payload, subcommand, filename);
        _ = await RequestAsync(SlmpCommand.FileChangeState, subcommand, payload.ToArray(), true, ct).ConfigureAwait(false);
    }

    /// <summary>Changes the modification date/time of a file by name (command 0x1826).</summary>
    public async Task FileChangeDateByNameAsync(
        string filename,
        ushort driveNo,
        DateTime changedAt,
        ushort subcommand = 0x0000,
        CancellationToken ct = default)
    {
        ushort dateRaw = (ushort)(((changedAt.Year - 1980) << 9) | (changedAt.Month << 5) | changedAt.Day);
        ushort timeRaw = (ushort)((changedAt.Hour << 11) | (changedAt.Minute << 5) | (changedAt.Second / 2));
        var payload = new List<byte>();
        WriteUInt16LE(payload, driveNo);
        WriteUInt16LE(payload, dateRaw);
        WriteUInt16LE(payload, timeRaw);
        AppendFileName(payload, subcommand, filename);
        _ = await RequestAsync(SlmpCommand.FileChangeDate, subcommand, payload.ToArray(), true, ct).ConfigureAwait(false);
    }

    /// <summary>Searches for a file by name (command 0x1811) and returns the raw response data.</summary>
    public async Task<byte[]> FileSearchByNameAsync(
        string filename,
        ushort driveNo,
        ushort subcommand = 0x0000,
        string? password = null,
        CancellationToken ct = default)
    {
        var payload = new List<byte>();
        AppendFilePassword(payload, subcommand, password);
        WriteUInt16LE(payload, driveNo);
        AppendFileName(payload, subcommand, filename);
        return await RequestAsync(SlmpCommand.FileSearchDirectory, subcommand, payload.ToArray(), true, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads directory entries (command 0x1810).
    /// Returns raw response bytes; use subcommand 0x0000 for standard or 0x0040 for extended (Unicode path).
    /// </summary>
    public async Task<byte[]> FileReadDirectoryEntriesAsync(
        ushort driveNo,
        int headFileNo,
        int requestedFiles,
        ushort subcommand = 0x0000,
        string directoryPath = "",
        CancellationToken ct = default)
    {
        if (requestedFiles < 1 || requestedFiles > 36)
            throw new ArgumentOutOfRangeException(nameof(requestedFiles), "requestedFiles must be 1–36.");
        var payload = new List<byte>();
        WriteUInt16LE(payload, driveNo);
        if (subcommand == 0x0040)
        {
            payload.AddRange(BitConverter.GetBytes((uint)headFileNo));  // 4 bytes
        }
        else
        {
            WriteUInt16LE(payload, (ushort)headFileNo);
        }
        WriteUInt16LE(payload, (ushort)requestedFiles);
        if (subcommand == 0x0040)
        {
            var pathBytes = Encoding.Unicode.GetBytes(directoryPath);
            WriteUInt16LE(payload, (ushort)(pathBytes.Length / 2));
            payload.AddRange(pathBytes);
        }
        return await RequestAsync(SlmpCommand.FileReadDirectory, subcommand, payload.ToArray(), true, ct).ConfigureAwait(false);
    }

    // Low-level raw file command passthrough methods

    /// <summary>Raw passthrough for FILE_READ_DIRECTORY (0x1810).</summary>
    public Task<byte[]> FileReadDirectoryAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileReadDirectory, subcommand, payload, true, ct);

    /// <summary>Raw passthrough for FILE_SEARCH_DIRECTORY (0x1811).</summary>
    public Task<byte[]> FileSearchDirectoryAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileSearchDirectory, subcommand, payload, true, ct);

    /// <summary>Raw passthrough for FILE_NEW (0x1820).</summary>
    public Task FileNewAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileNew, subcommand, payload, true, ct);

    /// <summary>Raw passthrough for FILE_DELETE (0x1822).</summary>
    public Task FileDeleteAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileDelete, subcommand, payload, true, ct);

    /// <summary>Raw passthrough for FILE_COPY (0x1824).</summary>
    public Task FileCopyAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileCopy, subcommand, payload, true, ct);

    /// <summary>Raw passthrough for FILE_CHANGE_STATE (0x1825).</summary>
    public Task FileChangeStateAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileChangeState, subcommand, payload, true, ct);

    /// <summary>Raw passthrough for FILE_CHANGE_DATE (0x1826).</summary>
    public Task FileChangeDateAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileChangeDate, subcommand, payload, true, ct);

    /// <summary>Raw passthrough for FILE_OPEN (0x1827).</summary>
    public Task<byte[]> FileOpenAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileOpen, subcommand, payload, true, ct);

    /// <summary>Raw passthrough for FILE_READ (0x1828).</summary>
    public Task<byte[]> FileReadAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileRead, subcommand, payload, true, ct);

    /// <summary>Raw passthrough for FILE_WRITE (0x1829).</summary>
    public Task<byte[]> FileWriteAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileWrite, subcommand, payload, true, ct);

    /// <summary>Raw passthrough for FILE_CLOSE (0x182A).</summary>
    public Task FileCloseAsync(ReadOnlyMemory<byte> payload, ushort subcommand = 0x0000, CancellationToken ct = default)
        => RequestAsync(SlmpCommand.FileClose, subcommand, payload, true, ct);

    private static void AppendFilePassword(List<byte> buffer, ushort subcommand, string? password)
    {
        if (subcommand == 0x0040)
        {
            var raw = Encoding.Unicode.GetBytes(password ?? "");
            var chars = (ushort)(raw.Length / 2);
            WriteUInt16LE(buffer, chars);
            buffer.AddRange(raw);
        }
        else
        {
            var raw = Encoding.ASCII.GetBytes(password ?? "");
            WriteUInt16LE(buffer, (ushort)raw.Length);
            buffer.AddRange(raw);
        }
    }

    private static void AppendFileName(List<byte> buffer, ushort subcommand, string filename)
    {
        if (subcommand == 0x0040)
        {
            var raw = Encoding.Unicode.GetBytes(filename);
            WriteUInt16LE(buffer, (ushort)(raw.Length / 2));
            buffer.AddRange(raw);
        }
        else
        {
            var raw = Encoding.ASCII.GetBytes(filename);
            WriteUInt16LE(buffer, (ushort)raw.Length);
            buffer.AddRange(raw);
        }
    }

    // -----------------------------------------------------------------------
    // Long timer / long retentive timer reads
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads one or more long timers starting at the given device number.
    /// Each timer occupies 4 consecutive words: [current_lo, current_hi, status, reserved].
    /// </summary>
    /// <param name="headNo">Starting LTN device number (e.g. 0 for LTN0).</param>
    /// <param name="points">Number of timers to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SlmpLongTimerResult[]> ReadLongTimerAsync(
        int headNo = 0,
        int points = 1,
        CancellationToken cancellationToken = default)
    {
        var device = new SlmpDeviceAddress(SlmpDeviceCode.LTN, (uint)headNo);
        var words = await ReadWordsRawAsync(device, (ushort)(points * 4), cancellationToken).ConfigureAwait(false);
        return ParseLongTimerWords(words, headNo, "LTN", points);
    }

    /// <summary>
    /// Reads one or more long retentive timers starting at the given device number.
    /// Each timer occupies 4 consecutive words: [current_lo, current_hi, status, reserved].
    /// </summary>
    /// <param name="headNo">Starting LSTN device number (e.g. 0 for LSTN0).</param>
    /// <param name="points">Number of timers to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SlmpLongTimerResult[]> ReadLongRetentiveTimerAsync(
        int headNo = 0,
        int points = 1,
        CancellationToken cancellationToken = default)
    {
        var device = new SlmpDeviceAddress(SlmpDeviceCode.LSTN, (uint)headNo);
        var words = await ReadWordsRawAsync(device, (ushort)(points * 4), cancellationToken).ConfigureAwait(false);
        return ParseLongTimerWords(words, headNo, "LSTN", points);
    }

    /// <summary>Returns the coil state of each long timer in the range.</summary>
    public async Task<bool[]> ReadLtcStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static t => t.Coil).ToArray();
    }

    /// <summary>Returns the contact state of each long timer in the range.</summary>
    public async Task<bool[]> ReadLtsStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static t => t.Contact).ToArray();
    }

    /// <summary>Returns the coil state of each long retentive timer in the range.</summary>
    public async Task<bool[]> ReadLstcStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongRetentiveTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static t => t.Coil).ToArray();
    }

    /// <summary>Returns the contact state of each long retentive timer in the range.</summary>
    public async Task<bool[]> ReadLstsStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongRetentiveTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static t => t.Contact).ToArray();
    }

    private static SlmpLongTimerResult[] ParseLongTimerWords(ushort[] words, int headNo, string prefix, int points)
    {
        var result = new SlmpLongTimerResult[points];
        for (var i = 0; i < points; i++)
        {
            var base4 = i * 4;
            var currentValue = (uint)(words[base4] | (words[base4 + 1] << 16));
            var statusWord = words[base4 + 2];
            result[i] = new SlmpLongTimerResult(
                Index: headNo + i,
                Device: $"{prefix}{headNo + i}",
                CurrentValue: currentValue,
                Contact: (statusWord & 0x0002) != 0,
                Coil: (statusWord & 0x0001) != 0,
                StatusWord: statusWord,
                RawWords: words[base4..(base4 + 4)]);
        }
        return result;
    }

    public async Task<byte[]> RequestAsync(SlmpCommand command, ushort subcommand, ReadOnlyMemory<byte> payload, bool expectResponse = true, CancellationToken cancellationToken = default)
    {
        if (!IsOpen) await OpenAsync(cancellationToken).ConfigureAwait(false);
        var frame = BuildRequestFrame(command, subcommand, payload.Span);
        LastRequestFrame = frame;
        FireTrace(SlmpTraceDirection.Send, frame);
        if (_transportMode == SlmpTransportMode.Tcp)
        {
            if (_tcpStream is null) throw new SlmpError("tcp not open");
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

        if (_udp is null) throw new SlmpError("udp not open");
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

        throw new SlmpError("invalid response subheader");
    }

    private byte[] ParseResponse(SlmpCommand command, ushort subcommand, byte[] response)
    {
        var is4E = response.Length >= 13 && response[0] == 0xD4 && response[1] == 0x00;
        var is3E = response.Length >= 9 && response[0] == 0xD0 && response[1] == 0x00;
        if (!is4E && !is3E) throw new SlmpError("invalid response header", command: command, subcommand: subcommand);
        var headerSize = is4E ? 13 : 9;
        var dataLength = is4E ? BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(11, 2)) : BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(7, 2));
        if (response.Length < headerSize + dataLength || dataLength < 2) throw new SlmpError("malformed response", command: command, subcommand: subcommand);
        var endCode = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(headerSize, 2));
        if (endCode != 0) throw new SlmpError($"SLMP error end_code=0x{endCode:X4} command=0x{(ushort)command:X4} subcommand=0x{subcommand:X4}", endCode, command, subcommand);
        return dataLength == 2 ? [] : response.AsSpan(headerSize + 2, dataLength - 2).ToArray();
    }

    private static async Task<byte[]> ReceiveTcpRequestFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var prefix2 = new byte[2];
        await ReadExactAsync(stream, prefix2, cancellationToken).ConfigureAwait(false);
        if (prefix2[0] == 0x54 && prefix2[1] == 0x00)
        {
            // 4E request frame: 13-byte header, length at bytes [11..12]
            var rest11 = new byte[11];
            await ReadExactAsync(stream, rest11, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(rest11.AsSpan(9, 2));
            var body = new byte[length];
            await ReadExactAsync(stream, body, cancellationToken).ConfigureAwait(false);
            var frame = new byte[13 + length];
            prefix2.CopyTo(frame, 0);
            rest11.CopyTo(frame, 2);
            body.CopyTo(frame, 13);
            return frame;
        }

        if (prefix2[0] == 0x50 && prefix2[1] == 0x00)
        {
            // 3E request frame: 9-byte header, length at bytes [7..8]
            var rest7 = new byte[7];
            await ReadExactAsync(stream, rest7, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(rest7.AsSpan(5, 2));
            var body = new byte[length];
            await ReadExactAsync(stream, body, cancellationToken).ConfigureAwait(false);
            var frame = new byte[9 + length];
            prefix2.CopyTo(frame, 0);
            rest7.CopyTo(frame, 2);
            body.CopyTo(frame, 9);
            return frame;
        }

        throw new SlmpError("invalid request subheader");
    }

    private static SlmpRequestFrame ParseRequestFrame(byte[] raw)
    {
        if (raw.Length >= 2 && raw[0] == 0x54 && raw[1] == 0x00)
        {
            // 4E request
            if (raw.Length < 19) throw new SlmpError("4E request frame too short");
            var serial = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(2, 2));
            var target = new SlmpTargetAddress(raw[6], raw[7], BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(8, 2)), raw[10]);
            var monitoringTimer = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(13, 2));
            var command = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(15, 2));
            var subcommand = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(17, 2));
            var data = raw.AsSpan(19).ToArray();
            return new SlmpRequestFrame(serial, target, monitoringTimer, command, subcommand, data, raw);
        }

        if (raw.Length >= 2 && raw[0] == 0x50 && raw[1] == 0x00)
        {
            // 3E request
            if (raw.Length < 15) throw new SlmpError("3E request frame too short");
            var target = new SlmpTargetAddress(raw[2], raw[3], BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(4, 2)), raw[6]);
            var monitoringTimer = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(9, 2));
            var command = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(11, 2));
            var subcommand = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(13, 2));
            var data = raw.AsSpan(15).ToArray();
            return new SlmpRequestFrame(0, target, monitoringTimer, command, subcommand, data, raw);
        }

        throw new SlmpError("invalid request frame subheader");
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new SlmpError("connection closed while reading response");
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
