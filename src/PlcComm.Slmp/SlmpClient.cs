using System.Buffers.Binary;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace PlcComm.Slmp;

/// <summary>
/// A high-performance, asynchronous SLMP (MC Protocol) client for .NET.
/// Supports 3E and 4E frame formats over TCP and UDP.
/// </summary>
/// <remarks>
/// <para>
/// This class is <b>not thread-safe</b>. Concurrent calls to <see cref="RequestAsync"/>
/// will interleave send/receive bytes on the same connection.
/// For concurrent or shared-connection scenarios, wrap this client in a
/// <see cref="QueuedSlmpClient"/>, which serializes all operations with a semaphore.
/// </para>
/// <para>
/// The factory <see cref="SlmpClientFactory.OpenAndConnectAsync(SlmpConnectionOptions, CancellationToken)"/>
/// returns a ready-to-use <see cref="QueuedSlmpClient"/> and is the recommended
/// entry point for most use cases.
/// </para>
/// </remarks>
public sealed class SlmpClient : IDisposable, IAsyncDisposable
{
    private const uint MaxRuntimeRangeProbeCount = 1_048_576;
    private const int DirectWordPointLimit = 960;
    private const int DirectBitPointLimit = 7168;
    private const int MemoryWordLimit = 480;
    private const int ExtendUnitByteLimit = 1920;
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
    /// <param name="plcProfile">The PLC profile. This selection derives frame type and compatibility mode.</param>
    /// <param name="port">The port number. Defaults to 1025.</param>
    /// <param name="transportMode">The transport protocol (TCP or UDP).</param>
    public SlmpClient(
        string host,
        SlmpPlcProfile plcProfile,
        int port = 1025,
        SlmpTransportMode transportMode = SlmpTransportMode.Tcp)
    {
        _host = host;
        _port = port;
        _transportMode = transportMode;
        PlcProfile = plcProfile;
        var defaults = SlmpPlcProfiles.Resolve(plcProfile);
        FrameType = defaults.FrameType;
        CompatibilityMode = defaults.CompatibilityMode;
    }

    /// <summary>Gets the SLMP frame format derived from <see cref="PlcProfile"/>.</summary>
    public SlmpFrameType FrameType { get; }
    /// <summary>Gets the device access compatibility mode derived from <see cref="PlcProfile"/>.</summary>
    public SlmpCompatibilityMode CompatibilityMode { get; }
    /// <summary>Gets the PLC profile used to derive frame, compatibility, payload, and address behavior.</summary>
    public SlmpPlcProfile PlcProfile { get; }
    /// <summary>Gets or sets the destination routing information.</summary>
    public SlmpTargetAddress TargetAddress { get; set; } = new(Station: 0xFF, ModuleIo: 0x03FF);
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
            _tcp.NoDelay = true;
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
    /// Opens a connection with explicit stable settings and returns a connected <see cref="QueuedSlmpClient"/>.
    /// </summary>
    /// <param name="host">PLC IP address or hostname.</param>
    /// <param name="port">SLMP port number such as 1025 for iQ-R/iQ-F or 5007 for Q/L.</param>
    /// <param name="plcProfile">Canonical PLC profile used to derive the standard connection defaults.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A connected queued client ready for high-level helpers such as
    /// <c>ReadTypedAsync</c>, <c>ReadNamedAsync</c>, and <c>PollAsync</c>.
    /// </returns>
    /// <remarks>
    /// This is the recommended entry point for application code because it
    /// combines one explicit PLC profile with a queued wrapper that is safe
    /// to share across multiple tasks.
    /// </remarks>
    public static async Task<QueuedSlmpClient> OpenAndConnectAsync(
        string host,
        int port,
        SlmpPlcProfile plcProfile,
        CancellationToken cancellationToken = default)
        => await SlmpClientFactory.OpenAndConnectAsync(
            new SlmpConnectionOptions(host, plcProfile)
            {
                Port = port,
            },
            cancellationToken).ConfigureAwait(false);

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
    /// Reads <c>SD203</c> and decodes the CPU operation state from the lower 4 bits.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded CPU operation state and raw masked code.</returns>
    public async Task<SlmpCpuOperationState> ReadCpuOperationStateAsync(CancellationToken cancellationToken = default)
    {
        var statusWord = (await ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.SD, 203), 1, cancellationToken).ConfigureAwait(false))[0];
        return DecodeCpuOperationState(statusWord);
    }

    /// <summary>
    /// Reads the configured profile-specific device upper-bound catalog.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A catalog containing the configured profile and device upper-bound entries.</returns>
    public async Task<SlmpDeviceRangeCatalog> ReadDeviceRangeCatalogAsync(CancellationToken cancellationToken = default)
    {
        var rangeProfile = SlmpPlcProfiles.Resolve(PlcProfile).RangeProfile;
        var deviceRangeProfile = SlmpDeviceRangeResolver.ResolveProfile(rangeProfile);
        var registers = await SlmpDeviceRangeResolver.ReadRegistersAsync(this, deviceRangeProfile, cancellationToken).ConfigureAwait(false);
        var catalog = SlmpDeviceRangeResolver.BuildCatalog(rangeProfile, registers);
        return await ResolveDeviceRangeCatalogRuntimeLimitsAsync(catalog, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the profile-specific device upper-bound catalog without querying the PLC model name.
    /// </summary>
    /// <param name="plcProfile">User-selected PLC profile.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A catalog containing the selected profile and device upper-bound entries.</returns>
    public async Task<SlmpDeviceRangeCatalog> ReadDeviceRangeCatalogAsync(
        SlmpPlcProfile plcProfile,
        CancellationToken cancellationToken = default)
    {
        var profile = SlmpDeviceRangeResolver.ResolveProfile(plcProfile);
        var registers = await SlmpDeviceRangeResolver.ReadRegistersAsync(this, profile, cancellationToken).ConfigureAwait(false);
        var catalog = SlmpDeviceRangeResolver.BuildCatalog(plcProfile, registers);
        return await ResolveDeviceRangeCatalogRuntimeLimitsAsync(catalog, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SlmpDeviceRangeCatalog> ResolveDeviceRangeCatalogRuntimeLimitsAsync(
        SlmpDeviceRangeCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (catalog.PlcProfile is not (SlmpPlcProfile.QCpu or SlmpPlcProfile.LCpu or SlmpPlcProfile.QnU or SlmpPlcProfile.QnUDV))
            return catalog;

        if (catalog.PlcProfile == SlmpPlcProfile.QCpu)
        {
            var zCount = await CanReadWordAddressAsync(SlmpDeviceCode.Z, 15, cancellationToken).ConfigureAwait(false)
                ? 16u
                : 10u;
            catalog = SlmpDeviceRangeResolver.ReplaceFixedPointCount(
                catalog,
                "Z",
                zCount,
                "Runtime access check",
                "QCPU Z register count is selected by probing Z15.");
        }

        var zrCount = await ResolveReadablePointCountAsync(SlmpDeviceCode.ZR, cancellationToken).ConfigureAwait(false);
        catalog = SlmpDeviceRangeResolver.ReplaceFixedPointCount(
            catalog,
            "ZR",
            zrCount,
            "Runtime access check",
            "ZR register count is selected by probing readable ZR addresses.");
        return SlmpDeviceRangeResolver.ReplaceFixedPointCount(
            catalog,
            "R",
            Math.Min(zrCount, 32_768u),
            "Runtime access check",
            "R register count follows the probed ZR count and is capped at R32767.");
    }

    private async Task<uint> ResolveReadablePointCountAsync(
        SlmpDeviceCode device,
        CancellationToken cancellationToken)
    {
        if (!await CanReadWordAddressAsync(device, 0, cancellationToken).ConfigureAwait(false))
            return 0;

        var upperLimit = MaxRuntimeRangeProbeCount - 1;
        var low = 0u;
        var high = 1u;
        while (high < upperLimit && await CanReadWordAddressAsync(device, high, cancellationToken).ConfigureAwait(false))
        {
            low = high;
            high = Math.Min(upperLimit, checked((high * 2) + 1));
        }

        if (high == upperLimit && await CanReadWordAddressAsync(device, high, cancellationToken).ConfigureAwait(false))
            return MaxRuntimeRangeProbeCount;

        var left = low + 1;
        var right = high - 1;
        while (left <= right)
        {
            var mid = left + ((right - left) / 2);
            if (await CanReadWordAddressAsync(device, mid, cancellationToken).ConfigureAwait(false))
            {
                low = mid;
                left = mid + 1;
            }
            else
            {
                if (mid == 0)
                    break;

                right = mid - 1;
            }
        }

        return low + 1;
    }

    private async Task<bool> CanReadWordAddressAsync(
        SlmpDeviceCode device,
        uint number,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await ReadWordsRawAsync(new SlmpDeviceAddress(device, number), 1, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SlmpError exception) when (exception.Command == SlmpCommand.DeviceRead)
        {
            return false;
        }
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
        ValidateDirectAccessPoints(points, bitUnit: false, "read_words");
        ValidateDirectWordReadDevice(device, points);
        return await ReadWordsRawUncheckedAsync(device, points, cancellationToken).ConfigureAwait(false);
    }

    internal Task<ushort[]> ReadLongStatusBlockWordsAsync(SlmpDeviceCode currentValueDevice, uint number, CancellationToken cancellationToken = default)
    {
        if (!IsLongCurrentValueDevice(currentValueDevice))
        {
            throw new ArgumentException(
                $"{currentValueDevice} is not a long-family current value device.",
                nameof(currentValueDevice));
        }

        return ReadWordsRawUncheckedAsync(new SlmpDeviceAddress(currentValueDevice, number), 4, cancellationToken);
    }

    private async Task<ushort[]> ReadWordsRawUncheckedAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
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
        ValidateDirectWordWriteDevice(device);
        await WriteWordsUncheckedAsync(device, values, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteWordsUncheckedAsync(SlmpDeviceAddress device, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
    {
        ValidateDirectAccessPoints(values.Count, bitUnit: false, "write_words");
        var payload = BuildReadWritePayload(device, checked((ushort)values.Count), values, bitUnit: false);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        _ = await RequestAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool[]> ReadBitsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        ValidateDirectAccessPoints(points, bitUnit: true, "read_bits");
        ValidateDirectBitReadDevice(device);
        return await ReadBitsUncheckedAsync(device, points, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<bool[]> ReadBitsUncheckedAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        ValidateDirectAccessPoints(points, bitUnit: true, "read_bits");
        var payload = BuildReadWritePayload(device, points, null, bitUnit: true);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0001 : (ushort)0x0003;
        var data = await RequestAsync(SlmpCommand.DeviceRead, sub, payload, true, cancellationToken).ConfigureAwait(false);
        return UnpackBitValues(data, points);
    }

    private static SlmpCpuOperationState DecodeCpuOperationState(ushort statusWord)
    {
        var rawCode = (byte)(statusWord & 0x0F);
        var status = rawCode switch
        {
            0x00 => SlmpCpuOperationStatus.Run,
            0x02 => SlmpCpuOperationStatus.Stop,
            0x03 => SlmpCpuOperationStatus.Pause,
            _ => SlmpCpuOperationStatus.Unknown,
        };
        return new SlmpCpuOperationState(status, statusWord, rawCode);
    }

    public async Task<ushort[]> ReadWordsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        ushort points,
        SlmpExtensionSpec extension,
        CancellationToken cancellationToken = default
    )
    {
        ValidateDirectAccessPoints(points, bitUnit: false, "read_words_ext");
        ValidateDirectWordReadDevice(device.Device, points);
        var effectiveExtension = SlmpPayloads.ResolveEffectiveExtension(device, extension);
        var payload = SlmpPayloads.BuildReadWritePayloadExtended(device.Device, points, null, effectiveExtension, bitUnit: false, CompatibilityMode);
        var sub = effectiveExtension.DirectMemorySpecification == 0xF9 ? (ushort)0x0080
            : CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
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
        ValidateDirectAccessPoints(values.Count, bitUnit: false, "write_words_ext");
        ValidateDirectWordWriteDevice(device.Device);
        var effectiveExtension = SlmpPayloads.ResolveEffectiveExtension(device, extension);
        var payload = SlmpPayloads.BuildReadWritePayloadExtended(device.Device, checked((ushort)values.Count), values, effectiveExtension, bitUnit: false, CompatibilityMode);
        var sub = effectiveExtension.DirectMemorySpecification == 0xF9 ? (ushort)0x0080
            : CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        _ = await RequestAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool[]> ReadBitsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        ushort points,
        SlmpExtensionSpec extension,
        CancellationToken cancellationToken = default
    )
    {
        ValidateDirectAccessPoints(points, bitUnit: true, "read_bits_ext");
        ValidateDirectBitReadDevice(device.Device);
        var effectiveExtension = SlmpPayloads.ResolveEffectiveExtension(device, extension);
        var payload = SlmpPayloads.BuildReadWritePayloadExtended(device.Device, points, null, effectiveExtension, bitUnit: true, CompatibilityMode);
        var sub = effectiveExtension.DirectMemorySpecification == 0xF9 ? (ushort)0x0081
            : CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0081 : (ushort)0x0083;
        var data = await RequestAsync(SlmpCommand.DeviceRead, sub, payload, true, cancellationToken).ConfigureAwait(false);
        return UnpackBitValues(data, points);
    }

    public async Task WriteBitsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        IReadOnlyList<bool> values,
        SlmpExtensionSpec extension,
        CancellationToken cancellationToken = default
    )
    {
        ValidateDirectAccessPoints(values.Count, bitUnit: true, "write_bits_ext");
        ValidateDirectBitWriteDevice(device.Device);
        var effectiveExtension = SlmpPayloads.ResolveEffectiveExtension(device, extension);
        var wordValues = new ushort[values.Count];
        for (var i = 0; i < values.Count; i++) wordValues[i] = values[i] ? (ushort)1 : (ushort)0;
        var payload = SlmpPayloads.BuildReadWritePayloadExtended(device.Device, checked((ushort)values.Count), wordValues, effectiveExtension, bitUnit: true, CompatibilityMode);
        var sub = effectiveExtension.DirectMemorySpecification == 0xF9 ? (ushort)0x0081
            : CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0081 : (ushort)0x0083;
        _ = await RequestAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteBitsAsync(SlmpDeviceAddress device, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
    {
        ValidateDirectAccessPoints(values.Count, bitUnit: true, "write_bits");
        ValidateDirectBitWriteDevice(device);
        var wordValues = new ushort[values.Count];
        for (var i = 0; i < values.Count; i++) wordValues[i] = values[i] ? (ushort)1 : (ushort)0;
        var payload = BuildReadWritePayload(device, checked((ushort)values.Count), wordValues, bitUnit: true);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0001 : (ushort)0x0003;
        _ = await RequestAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<uint[]> ReadDWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        ValidateDirectDWordReadDevice(device);
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
        ValidateDirectDWordWriteDevice(device);
        ValidateDirectAccessPoints(checked(values.Count * 2), bitUnit: false, "write_dwords");
        var words = new ushort[values.Count * 2];
        for (var i = 0; i < values.Count; i++)
        {
            words[i * 2] = (ushort)(values[i] & 0xFFFF);
            words[(i * 2) + 1] = (ushort)((values[i] >> 16) & 0xFFFF);
        }
        await WriteWordsUncheckedAsync(device, words, cancellationToken).ConfigureAwait(false);
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
        ValidateRandomReadLikeCounts(wordDevices.Count, dwordDevices.Count, "read_random");
        ValidateRandomReadDevices(wordDevices, dwordDevices);

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
        ValidateRandomWriteWordCounts(wordEntries.Count, dwordEntries.Count, "write_random_words");
        ValidateRandomWriteDevices(wordEntries);

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
        ValidateRandomBitWriteCount(bitEntries.Count, "write_random_bits");
        ValidateRandomBitWriteDevices(bitEntries);

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
        ValidateRandomReadLikeCounts(wordDevices.Count, dwordDevices.Count, "read_random_ext");
        ValidateRandomReadDevices(
            wordDevices.Select(entry => entry.Device.Device).ToArray(),
            dwordDevices.Select(entry => entry.Device.Device).ToArray());

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        var payload = SlmpPayloads.BuildExtendedRandomReadPayload(wordDevices, dwordDevices, CompatibilityMode);
        var data = await RequestAsync(SlmpCommand.DeviceReadRandom, sub, payload, true, cancellationToken).ConfigureAwait(false);
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
        ValidateRandomWriteWordCounts(wordEntries.Count, dwordEntries.Count, "write_random_words_ext");
        ValidateRandomWriteDevices(wordEntries.Select(entry => (entry.Device.Device, entry.Value)).ToArray());

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        var payload = SlmpPayloads.BuildExtendedRandomWordWritePayload(wordEntries, dwordEntries, CompatibilityMode);
        _ = await RequestAsync(SlmpCommand.DeviceWriteRandom, sub, payload, true, cancellationToken).ConfigureAwait(false);
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
        ValidateRandomBitWriteCount(bitEntries.Count, "write_random_bits_ext");
        ValidateRandomBitWriteDevices(bitEntries.Select(entry => (entry.Device.Device, entry.Value)).ToArray());

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0081 : (ushort)0x0083;
        var payload = SlmpPayloads.BuildExtendedRandomBitWritePayload(bitEntries, CompatibilityMode);
        _ = await RequestAsync(SlmpCommand.DeviceWriteRandom, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    internal byte[] BuildExtendedRandomReadPayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> wordDevices,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> dwordDevices
    )
        => SlmpPayloads.BuildExtendedRandomReadPayload(wordDevices, dwordDevices, CompatibilityMode);

    internal byte[] BuildExtendedRandomWordWritePayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, ushort Value, SlmpExtensionSpec Extension)> wordEntries,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, uint Value, SlmpExtensionSpec Extension)> dwordEntries
    )
        => SlmpPayloads.BuildExtendedRandomWordWritePayload(wordEntries, dwordEntries, CompatibilityMode);

    internal byte[] BuildExtendedRandomBitWritePayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, bool Value, SlmpExtensionSpec Extension)> bitEntries
    )
        => SlmpPayloads.BuildExtendedRandomBitWritePayload(bitEntries, CompatibilityMode);

    internal byte[] BuildExtendedMonitorRegisterPayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> wordDevices,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)> dwordDevices
    )
        => SlmpPayloads.BuildExtendedMonitorRegisterPayload(wordDevices, dwordDevices, CompatibilityMode);

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
        ValidateBlockReadLimits(wordBlocks, bitBlocks);
        ValidateBlockReadDevices(wordBlocks, bitBlocks);

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
            await WriteBlockAsync(wordBlocks, [], new SlmpBlockWriteOptions(false), cancellationToken).ConfigureAwait(false);
            await WriteBlockAsync([], bitBlocks, new SlmpBlockWriteOptions(false), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (wordBlocks.Count > 0xFF || bitBlocks.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordBlocks), "block counts must be <= 255");
        }
        ValidateBlockWriteLimits(wordBlocks, bitBlocks);
        ValidateBlockWriteDevices(wordBlocks, bitBlocks);

        var specSize = DeviceSpecSize();
        var totalWordPoints = wordBlocks.Sum(static x => x.Values.Count);
        var totalBitPoints = bitBlocks.Sum(static x => x.Values.Count);
        var payload = new byte[2 + ((wordBlocks.Count + bitBlocks.Count) * (specSize + 2)) + ((totalWordPoints + totalBitPoints) * 2)];
        payload[0] = (byte)wordBlocks.Count;
        payload[1] = (byte)bitBlocks.Count;
        // Each block's write data follows that block's own spec (SLMP
        // reference manual Write Block request format); data must not be
        // batched after the block specs, or multi-block/mixed requests
        // misparse on the PLC.
        var offset = 2;
        foreach (var block in wordBlocks.Concat(bitBlocks))
        {
            offset += EncodeDeviceSpec(block.Device, payload.AsSpan(offset));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), checked((ushort)block.Values.Count));
            offset += 2;
            foreach (var value in block.Values)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), value);
                offset += 2;
            }
        }

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        _ = await RequestAsync(SlmpCommand.DeviceWriteBlock, sub, payload, true, cancellationToken).ConfigureAwait(false);
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
        ValidateRandomReadLikeCounts(wordDevices.Count, dwordDevices.Count, "register_monitor_devices");
        ValidateMonitorRegisterDevices(wordDevices, dwordDevices);

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
        ValidateRandomReadLikeCounts(wordDevices.Count, dwordDevices.Count, "register_monitor_devices_ext");
        ValidateMonitorRegisterDevices(
            wordDevices.Select(entry => entry.Device.Device).ToArray(),
            dwordDevices.Select(entry => entry.Device.Device).ToArray());

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        var payload = SlmpPayloads.BuildExtendedMonitorRegisterPayload(wordDevices, dwordDevices, CompatibilityMode);
        _ = await RequestAsync(SlmpCommand.MonitorRegister, sub, payload, true, cancellationToken).ConfigureAwait(false);
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

    public async Task RemoteRunAsync(bool force = false, ushort clearMode = 0, CancellationToken cancellationToken = default)
    {
        if (clearMode > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(clearMode), "remote run clearMode must be 0, 1, or 2.");
        }

        var payload = force ? new byte[] { 0x03, 0x00, (byte)clearMode, 0x00 } : new byte[] { 0x01, 0x00, (byte)clearMode, 0x00 };
        _ = await RequestAsync(SlmpCommand.RemoteRun, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoteStopAsync(CancellationToken cancellationToken = default)
    {
        _ = await RequestAsync(SlmpCommand.RemoteStop, 0x0000, new byte[] { 0x01, 0x00 }, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemotePauseAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        var mode = force ? (ushort)0x0003 : (ushort)0x0001;
        _ = await RequestAsync(SlmpCommand.RemotePause, 0x0000, new byte[] { (byte)mode, 0x00 }, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoteLatchClearAsync(CancellationToken cancellationToken = default) => _ = await RequestAsync(SlmpCommand.RemoteLatchClear, 0x0000, new byte[] { 0x01, 0x00 }, true, cancellationToken).ConfigureAwait(false);

    public async Task RemoteResetAsync(ushort subcommand = 0x0000, bool expectResponse = false, CancellationToken cancellationToken = default)
    {
        if (subcommand != 0x0000)
        {
            throw new ArgumentOutOfRangeException(nameof(subcommand), "remote reset subcommand must be 0x0000");
        }

        _ = await RequestAsync(SlmpCommand.RemoteReset, subcommand, new byte[] { 0x01, 0x00 }, expectResponse, cancellationToken).ConfigureAwait(false);
    }

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
        if (data.Length < 1 || data.Length > 960)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "loopback payload size out of range (1..960 bytes)");
        }

        foreach (var value in data.Span)
        {
            if (!IsSelfTestHexByte(value))
            {
                throw new ArgumentException("loopback payload must contain only ASCII 0-9/A-F bytes", nameof(data));
            }
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

    private static bool IsSelfTestHexByte(byte value)
        => value is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'F';

    public async Task ClearErrorAsync(CancellationToken cancellationToken = default) => _ = await RequestAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty, true, cancellationToken).ConfigureAwait(false);

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
        var payload = SlmpPayloads.BuildLabelArrayReadPayload(points, abbrevs);
        var data = await RequestAsync(SlmpCommand.LabelArrayRead, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
        return SlmpPayloads.ParseArrayLabelReadResponse(data);
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
        var payload = SlmpPayloads.BuildLabelArrayWritePayload(points, abbrevs);
        _ = await RequestAsync(SlmpCommand.LabelArrayWrite, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
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
        var payload = SlmpPayloads.BuildLabelRandomReadPayload(labels, abbrevs);
        var data = await RequestAsync(SlmpCommand.LabelReadRandom, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
        return SlmpPayloads.ParseRandomLabelReadResponse(data);
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
        var payload = SlmpPayloads.BuildLabelRandomWritePayload(points, abbrevs);
        _ = await RequestAsync(SlmpCommand.LabelWriteRandom, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
    }

    internal static byte[] BuildLabelArrayReadPayload(IReadOnlyList<SlmpLabelArrayReadPoint> points, IReadOnlyList<string> abbreviationLabels)
        => SlmpPayloads.BuildLabelArrayReadPayload(points, abbreviationLabels);

    internal static byte[] BuildLabelArrayWritePayload(IReadOnlyList<SlmpLabelArrayWritePoint> points, IReadOnlyList<string> abbreviationLabels)
        => SlmpPayloads.BuildLabelArrayWritePayload(points, abbreviationLabels);

    internal static byte[] BuildLabelRandomReadPayload(IReadOnlyList<string> labels, IReadOnlyList<string> abbreviationLabels)
        => SlmpPayloads.BuildLabelRandomReadPayload(labels, abbreviationLabels);

    internal static byte[] BuildLabelRandomWritePayload(IReadOnlyList<SlmpLabelRandomWritePoint> points, IReadOnlyList<string> abbreviationLabels)
        => SlmpPayloads.BuildLabelRandomWritePayload(points, abbreviationLabels);

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
        ValidateMemoryWordLength(wordLength, "memory_read");
        var payload = new byte[6];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), headAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), wordLength);
        var data = await RequestAsync(SlmpCommand.MemoryRead, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
        if (data.Length != wordLength * 2)
            throw new SlmpError($"memory read size mismatch: expected={wordLength} actual={data.Length / 2}");
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
        ValidateMemoryWordLength(values.Count, "memory_write");
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
        ValidateExtendUnitByteLength(byteLength, "extend_unit_read");
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), headAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), byteLength);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6, 2), moduleNo);
        var data = await RequestAsync(SlmpCommand.ExtendUnitRead, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
        if (data.Length != byteLength)
            throw new SlmpError($"extend unit read size mismatch: expected={byteLength} actual={data.Length}");
        return data;
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
        ValidateExtendUnitWordLength(wordLength, "extend_unit_read_words");
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
        ValidateExtendUnitByteLength(data.Length, "extend_unit_write");
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
        ValidateExtendUnitWordLength(values.Count, "extend_unit_write_words");
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
        var words = await ReadLongTimerStatusWordsAsync(SlmpDeviceCode.LTN, headNo, points, cancellationToken).ConfigureAwait(false);
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
        var words = await ReadLongTimerStatusWordsAsync(SlmpDeviceCode.LSTN, headNo, points, cancellationToken).ConfigureAwait(false);
        return ParseLongTimerWords(words, headNo, "LSTN", points);
    }

    /// <summary>Returns the coil state of each long timer in the range.</summary>
    public async Task<bool[]> ReadLtcStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static timer => timer.Coil).ToArray();
    }

    /// <summary>Returns the contact state of each long timer in the range.</summary>
    public async Task<bool[]> ReadLtsStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static timer => timer.Contact).ToArray();
    }

    /// <summary>Returns the coil state of each long retentive timer in the range.</summary>
    public async Task<bool[]> ReadLstcStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongRetentiveTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static timer => timer.Coil).ToArray();
    }

    /// <summary>Returns the contact state of each long retentive timer in the range.</summary>
    public async Task<bool[]> ReadLstsStatesAsync(int headNo = 0, int points = 1, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongRetentiveTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static timer => timer.Contact).ToArray();
    }

    private async Task<ushort[]> ReadLongTimerStatusWordsAsync(
        SlmpDeviceCode currentValueDevice,
        int headNo,
        int points,
        CancellationToken cancellationToken)
    {
        if (headNo < 0)
            throw new ArgumentOutOfRangeException(nameof(headNo), "headNo must be >= 0.");
        if (points < 0)
            throw new ArgumentOutOfRangeException(nameof(points), "points must be >= 0.");

        var words = new ushort[checked(points * 4)];
        for (var index = 0; index < points; index++)
        {
            var device = new SlmpDeviceAddress(currentValueDevice, checked((uint)(headNo + index)));
            var block = await ReadLongStatusBlockWordsAsync(currentValueDevice, device.Number, cancellationToken).ConfigureAwait(false);
            Array.Copy(block, 0, words, index * 4, block.Length);
        }

        return words;
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
        var header = new byte[13];
        await ReadExactAsync(stream, header.AsMemory(0, 2), cancellationToken).ConfigureAwait(false);

        if (header[0] == 0xD4 && header[1] == 0x00)
        {
            // 4E response: subheader(2) + serial(2) + reserved(2) + net(1) + sta(1) + mod(2) + multi(1) + len(2) = 13 bytes header
            await ReadExactAsync(stream, header.AsMemory(2, 11), cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(11, 2));
            var frame = new byte[13 + length];
            header.AsSpan(0, 13).CopyTo(frame);
            await ReadExactAsync(stream, frame.AsMemory(13, length), cancellationToken).ConfigureAwait(false);
            return frame;
        }

        if (header[0] == 0xD0 && header[1] == 0x00)
        {
            // 3E response: subheader(2) + net(1) + sta(1) + mod(2) + multi(1) + len(2) = 9 bytes header
            await ReadExactAsync(stream, header.AsMemory(2, 7), cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(7, 2));
            var frame = new byte[9 + length];
            header.AsSpan(0, 9).CopyTo(frame);
            await ReadExactAsync(stream, frame.AsMemory(9, length), cancellationToken).ConfigureAwait(false);
            return frame;
        }

        throw new SlmpError("invalid response subheader");
    }

    private static byte[] ParseResponse(SlmpCommand command, ushort subcommand, byte[] response)
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

    private static async Task ReadExactAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (!buffer.IsEmpty)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new SlmpError("connection closed while reading response");
            buffer = buffer[read..];
        }
    }

    internal int DeviceSpecSize() => SlmpPayloads.DeviceSpecSize(CompatibilityMode);

    internal int EncodeDeviceSpec(SlmpDeviceAddress device, Span<byte> output)
        => SlmpPayloads.EncodeDeviceSpec(device, output, CompatibilityMode);

    private static void ValidateDirectAccessPoints(int points, bool bitUnit, string name)
    {
        var limit = bitUnit ? DirectBitPointLimit : DirectWordPointLimit;
        var unit = bitUnit ? "bit" : "word";
        if (points < 1 || points > limit)
            throw new ArgumentOutOfRangeException(name, $"{name} {unit} access points out of range (1..{limit}): {points}");
    }

    private void ValidateRandomReadLikeCounts(int wordPoints, int dwordPoints, string name)
    {
        var total = wordPoints + dwordPoints;
        var limit = CompatibilityMode == SlmpCompatibilityMode.Legacy ? 192 : 96;
        if (total < 1 || total > limit)
            throw new ArgumentOutOfRangeException(name, $"{name} total access points out of range (1..{limit}): word={wordPoints}, dword={dwordPoints}");
    }

    private void ValidateRandomWriteWordCounts(int wordPoints, int dwordPoints, string name)
    {
        var total = wordPoints + dwordPoints;
        if (total < 1)
            throw new ArgumentOutOfRangeException(name, $"{name} word/dword access points out of range: word={wordPoints}, dword={dwordPoints}");

        var weighted = (wordPoints * 12) + (dwordPoints * 14);
        var limit = CompatibilityMode == SlmpCompatibilityMode.Legacy ? 1920 : 960;
        if (weighted > limit)
            throw new ArgumentOutOfRangeException(
                name,
                $"{name} word/dword access points out of range: word={wordPoints}, dword={dwordPoints}, weighted={weighted}, limit={limit}");
    }

    private void ValidateRandomBitWriteCount(int points, string name)
    {
        var limit = CompatibilityMode == SlmpCompatibilityMode.Legacy ? 188 : 94;
        if (points < 1 || points > limit)
            throw new ArgumentOutOfRangeException(name, $"{name} bit access points out of range (1..{limit}): {points}");
    }

    private void ValidateBlockReadLimits(IReadOnlyList<SlmpBlockRead> wordBlocks, IReadOnlyList<SlmpBlockRead> bitBlocks)
    {
        var totalBlocks = wordBlocks.Count + bitBlocks.Count;
        ValidateBlockCount(totalBlocks, "read_block");
        var totalPoints = wordBlocks.Sum(static block => ValidateBlockPointCount(block.Points, "read_block word")) +
                          bitBlocks.Sum(static block => ValidateBlockPointCount(block.Points, "read_block bit"));
        if (totalPoints > DirectWordPointLimit)
            throw new ArgumentOutOfRangeException(nameof(wordBlocks), $"read_block total device points out of range (<=960): total_points={totalPoints}");
    }

    private void ValidateBlockWriteLimits(IReadOnlyList<SlmpBlockWrite> wordBlocks, IReadOnlyList<SlmpBlockWrite> bitBlocks)
    {
        var totalBlocks = wordBlocks.Count + bitBlocks.Count;
        ValidateBlockCount(totalBlocks, "write_block");
        var totalPoints = wordBlocks.Sum(static block => ValidateBlockPointCount(block.Values.Count, "write_block word")) +
                          bitBlocks.Sum(static block => ValidateBlockPointCount(block.Values.Count, "write_block bit"));
        var perBlockOverhead = CompatibilityMode == SlmpCompatibilityMode.Legacy ? 4 : 9;
        var weighted = totalPoints + (totalBlocks * perBlockOverhead);
        if (weighted > DirectWordPointLimit)
            throw new ArgumentOutOfRangeException(
                nameof(wordBlocks),
                $"write_block total device points out of range (<=960): weighted={weighted}, total_points={totalPoints}");
    }

    private void ValidateBlockCount(int totalBlocks, string name)
    {
        var limit = CompatibilityMode == SlmpCompatibilityMode.Legacy ? 120 : 60;
        if (totalBlocks < 1 || totalBlocks > limit)
            throw new ArgumentOutOfRangeException(name, $"{name} total block count out of range (1..{limit}): {totalBlocks}");
    }

    private static int ValidateBlockPointCount(int points, string name)
    {
        if (points < 1 || points > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(name, $"{name} block points out of range (1..65535): {points}");
        return points;
    }

    private static void ValidateMemoryWordLength(int wordLength, string name)
    {
        if (wordLength < 1 || wordLength > MemoryWordLimit)
            throw new ArgumentOutOfRangeException(name, $"{name} word length out of range (1..480): {wordLength}");
    }

    private static void ValidateExtendUnitByteLength(int byteLength, string name)
    {
        if (byteLength < 2 || byteLength > ExtendUnitByteLimit)
            throw new ArgumentOutOfRangeException(name, $"{name} byte length out of range (2..1920): {byteLength}");
    }

    private static void ValidateExtendUnitWordLength(int wordLength, string name)
    {
        if (wordLength < 1 || wordLength > DirectWordPointLimit)
            throw new ArgumentOutOfRangeException(name, $"{name} word length out of range (1..960): {wordLength}");
    }

    private static void ValidateDirectBitReadDevice(SlmpDeviceAddress device)
    {
        // Long-family state bits must enter through the typed/named helpers. Some devices
        // use status blocks internally, and LCS/LCC use direct bit read only inside the helper.
        if (IsLongTimerStateDevice(device.Code) || IsLongCounterContactDevice(device))
        {
            throw new ArgumentException(
                $"Direct bit read is not supported for {device.Code}. Use ReadTypedAsync/ReadNamedAsync or the long-family helpers.",
                nameof(device));
        }
    }

    private static void ValidateDirectBitWriteDevice(SlmpDeviceAddress device)
    {
        ValidateWritableDevice(device);

        // PLCs reject direct bit write (0x1401) for these state bits. The
        // supported write path is the typed/named route, which selects 0x1402.
        if (RequiresRandomBitWrite(device.Code))
        {
            throw new ArgumentException(
                $"Direct bit write is not supported for {device.Code}. Use WriteTypedAsync/WriteNamedAsync with dtype 'BIT' so random bit write (0x1402) is selected.",
                nameof(device));
        }
    }

    private static void ValidateDirectWordReadDevice(SlmpDeviceAddress device, ushort points)
    {
        if (IsRandomDWordOnlyReadDevice(device.Code))
        {
            throw new ArgumentException(
                $"Direct word read is not supported for {device.Code}. {device.Code} is a 32-bit device; use ReadTypedAsync/ReadNamedAsync with ':D' or ':L' instead.",
                nameof(device));
        }

        if (IsLongTimerCurrentBlockDevice(device.Code))
        {
            if (points == 0 || points % 4 != 0)
            {
                throw new ArgumentException(
                    $"Direct read of {device.Code} requires 4-word blocks. Requested points={points}; use a multiple of 4 or the long timer helpers.",
                    nameof(points));
            }
        }
    }

    private static void ValidateDirectWordWriteDevice(SlmpDeviceAddress device)
    {
        ValidateWritableDevice(device);

        if (IsLongCurrentValueDevice(device.Code) || IsDWordOnlyScalarDevice(device.Code))
        {
            throw new ArgumentException(
                $"Direct word write is not supported for {device.Code}. {device.Code} is a 32-bit device; use WriteTypedAsync/WriteNamedAsync with ':D' or ':L' instead.",
                nameof(device));
        }
    }

    private static void ValidateDirectDWordWriteDevice(SlmpDeviceAddress device)
    {
        ValidateWritableDevice(device);

        if (IsLongCurrentValueDevice(device.Code) || IsDWordOnlyScalarDevice(device.Code))
        {
            throw new ArgumentException(
                $"Direct DWord write is not supported for {device.Code}. Use WriteTypedAsync/WriteNamedAsync with ':D' or ':L' so the 32-bit write route is selected.",
                nameof(device));
        }
    }

    private static void ValidateDirectDWordReadDevice(SlmpDeviceAddress device)
    {
        if (IsLongCurrentValueDevice(device.Code) || IsDWordOnlyScalarDevice(device.Code))
        {
            throw new ArgumentException(
                $"Direct DWord read is not supported for {device.Code}. Use ReadTypedAsync/ReadNamedAsync so the supported 32-bit route is selected.",
                nameof(device));
        }
    }

    private static void ValidateRandomReadDevices(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices)
    {
        // LTS/LTC/LSTS/LSTC can be written by random bit write, but they are not
        // readable by Read Random (0x0403); use the status-block helpers instead.
        if (wordDevices.Any(device => IsLongTimerStateDevice(device.Code)) || dwordDevices.Any(device => IsLongTimerStateDevice(device.Code)))
        {
            throw new ArgumentException(
                "Read Random (0x0403) does not support LTS/LTC/LSTS/LSTC. Use ReadTypedAsync/ReadNamedAsync or the long timer status helpers instead.",
                nameof(wordDevices));
        }

        if (wordDevices.Any(IsLongCounterContactDevice) || dwordDevices.Any(IsLongCounterContactDevice))
        {
            throw new ArgumentException(
                "Read Random (0x0403) does not support LCS/LCC. Use ReadTypedAsync/ReadNamedAsync so the long counter bit helper is selected.",
                nameof(wordDevices));
        }

        if (wordDevices.Any(device => IsLongCurrentValueDevice(device.Code) || IsDWordOnlyScalarDevice(device.Code)))
        {
            throw new ArgumentException(
                "Read Random (0x0403) does not support LTN/LSTN/LCN/LZ as word entries. Use dword entries or ReadTypedAsync/ReadNamedAsync with ':D' or ':L' instead.",
                nameof(wordDevices));
        }
    }

    private static void ValidateBlockReadDevices(
        IReadOnlyList<SlmpBlockRead> wordBlocks,
        IReadOnlyList<SlmpBlockRead> bitBlocks)
    {
        if (wordBlocks.Any(block => IsRandomDWordOnlyReadDevice(block.Device.Code)) ||
            bitBlocks.Any(block => IsRandomDWordOnlyReadDevice(block.Device.Code)))
        {
            throw new ArgumentException(
                "Read Block (0x0406) does not support LCN/LZ as word or bit blocks. Use ReadTypedAsync/ReadNamedAsync so random dword read is selected.",
                nameof(wordBlocks));
        }

        var invalidLongCurrentBlock = wordBlocks.FirstOrDefault(block =>
            IsLongTimerCurrentBlockDevice(block.Device.Code) && (block.Points == 0 || block.Points % 4 != 0));
        if (invalidLongCurrentBlock is not null)
        {
            throw new ArgumentException(
                $"Read Block (0x0406) direct read of {invalidLongCurrentBlock.Device.Code} requires 4-word blocks. Requested points={invalidLongCurrentBlock.Points}; use ReadTypedAsync/ReadNamedAsync for 32-bit current values.",
                nameof(wordBlocks));
        }

        if (wordBlocks.Any(block => IsLongCounterContactDevice(block.Device)) ||
            bitBlocks.Any(block => IsLongCounterContactDevice(block.Device)))
        {
            throw new ArgumentException(
                "Read Block (0x0406) does not support LCS/LCC. Use ReadTypedAsync/ReadNamedAsync so the long counter bit helper is selected.",
                nameof(wordBlocks));
        }
    }

    private static void ValidateBlockWriteDevices(
        IReadOnlyList<SlmpBlockWrite> wordBlocks,
        IReadOnlyList<SlmpBlockWrite> bitBlocks)
    {
        var readOnlyBlock = wordBlocks.Concat(bitBlocks).FirstOrDefault(block => IsSlmpReadOnlyDevice(block.Device.Code));
        if (readOnlyBlock is not null)
        {
            throw new ArgumentException(
                $"{readOnlyBlock.Device.Code} is read-only in SLMP and cannot be written.",
                nameof(wordBlocks));
        }

        if (wordBlocks.Any(block => IsLongCurrentValueDevice(block.Device.Code) || IsDWordOnlyScalarDevice(block.Device.Code)) ||
            bitBlocks.Any(block => IsLongCurrentValueDevice(block.Device.Code) || IsDWordOnlyScalarDevice(block.Device.Code)))
        {
            throw new ArgumentException(
                "Write Block (0x1406) does not support LTN/LSTN/LCN/LZ as word or bit blocks. Use WriteTypedAsync/WriteNamedAsync with ':D' or ':L' instead.",
                nameof(wordBlocks));
        }

        if (wordBlocks.Any(block => IsLongCounterContactDevice(block.Device)) ||
            bitBlocks.Any(block => IsLongCounterContactDevice(block.Device)))
        {
            throw new ArgumentException(
                "Write Block (0x1406) does not support LCS/LCC. Use WriteTypedAsync/WriteNamedAsync so random bit write (0x1402) is selected.",
                nameof(wordBlocks));
        }
    }

    private static void ValidateMonitorRegisterDevices(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices)
    {
        if (wordDevices.Any(IsLongCounterContactDevice) || dwordDevices.Any(IsLongCounterContactDevice))
        {
            throw new ArgumentException(
                "Entry Monitor Device (0x0801) does not support LCS/LCC.",
                nameof(wordDevices));
        }
    }

    private static bool IsLongCounterContactDevice(SlmpDeviceAddress device)
        => device.Code is SlmpDeviceCode.LCS or SlmpDeviceCode.LCC;

    private static bool RequiresRandomBitWrite(SlmpDeviceCode code)
        => IsLongTimerStateDevice(code)
            || code is SlmpDeviceCode.LCS or SlmpDeviceCode.LCC;

    private static bool IsSlmpReadOnlyDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.S;

    private static bool IsLongTimerStateDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.LTS
            or SlmpDeviceCode.LTC
            or SlmpDeviceCode.LSTS
            or SlmpDeviceCode.LSTC;

    private static bool IsLongCurrentValueDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.LTN or SlmpDeviceCode.LSTN or SlmpDeviceCode.LCN;

    private static bool IsLongTimerCurrentBlockDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.LTN or SlmpDeviceCode.LSTN;

    private static bool IsDWordOnlyScalarDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.LZ;

    private static bool IsRandomDWordOnlyReadDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.LCN or SlmpDeviceCode.LZ;

    private static void ValidateRandomWriteDevices(IReadOnlyList<(SlmpDeviceAddress Device, ushort Value)> wordEntries)
    {
        var readOnlyEntry = wordEntries.FirstOrDefault(entry => IsSlmpReadOnlyDevice(entry.Device.Code));
        if (readOnlyEntry.Device.Code != default)
        {
            throw new ArgumentException(
                $"{readOnlyEntry.Device.Code} is read-only in SLMP and cannot be written.",
                nameof(wordEntries));
        }

        if (wordEntries.Any(entry => IsLongCurrentValueDevice(entry.Device.Code) || IsDWordOnlyScalarDevice(entry.Device.Code)))
        {
            throw new ArgumentException(
                "Write Random (0x1402) does not support LTN/LSTN/LCN/LZ as word entries. Use dword entries or WriteTypedAsync/WriteNamedAsync with ':D' or ':L' instead.",
                nameof(wordEntries));
        }
    }

    private static void ValidateRandomBitWriteDevices(IReadOnlyList<(SlmpDeviceAddress Device, bool Value)> bitEntries)
    {
        var readOnlyEntry = bitEntries.FirstOrDefault(entry => IsSlmpReadOnlyDevice(entry.Device.Code));
        if (readOnlyEntry.Device.Code != default)
        {
            throw new ArgumentException(
                $"{readOnlyEntry.Device.Code} is read-only in SLMP and cannot be written.",
                nameof(bitEntries));
        }
    }

    private static void ValidateWritableDevice(SlmpDeviceAddress device)
    {
        if (IsSlmpReadOnlyDevice(device.Code))
        {
            throw new ArgumentException(
                $"{device.Code} is read-only in SLMP and cannot be written.",
                nameof(device));
        }
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

    private static bool[] UnpackBitValues(byte[] data, int points)
    {
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

    internal byte[] EncodeExtendedDeviceSpec(SlmpDeviceAddress device, SlmpExtensionSpec extension)
        => SlmpPayloads.EncodeExtendedDeviceSpec(device, extension, CompatibilityMode);

    private byte[] EncodePassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var raw = Encoding.ASCII.GetBytes(password);

        if (SlmpPlcProfiles.UsesIqrProtocol(PlcProfile))
        {
            // iQ-R: 2-byte LE length prefix followed by raw bytes (max 32 bytes)
            if (raw.Length is < 6 or > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(password), "iQ-R password length must be 6..32");
            }

            var result = new byte[2 + raw.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0, 2), (ushort)raw.Length);
            raw.CopyTo(result.AsSpan(2));
            return result;
        }
        else
        {
            // Q/L Legacy: 2-byte LE length prefix followed by the 4-byte password.
            if (raw.Length != 4)
            {
                throw new ArgumentOutOfRangeException(nameof(password), "Q/L password length must be exactly 4");
            }

            var result = new byte[2 + raw.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0, 2), (ushort)raw.Length);
            raw.CopyTo(result.AsSpan(2));
            return result;
        }
    }
}
