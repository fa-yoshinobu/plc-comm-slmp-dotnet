using System.Buffers.Binary;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text;

namespace PlcComm.Slmp;

/// <summary>
/// A high-performance, asynchronous SLMP (MC Protocol) client for .NET.
/// Supports 3E and 4E frame formats over TCP and UDP.
/// </summary>
/// <remarks>
/// <para>
/// Public operations on one client enter one arrival-order FIFO queue, so one
/// connection has at most one active wire transaction and 4E serial numbers remain
/// associated with their responses. Queue waiting does not consume the transaction
/// timeout. A waiting caller can cancel without sending.
/// </para>
/// <para>
/// Unless a method explicitly documents a multi-step semantic operation, each
/// request method emits exactly one SLMP request and never splits an oversized
/// operation. Effective limits are validated before serial allocation or transport.
/// </para>
/// <para>
/// Contiguous Direct, Random, Monitor-registration, Block, and applicable Extended
/// Device routes validate their complete consumed device span against the selected
/// 24-bit Q/L-compatible or 32-bit iQ-R wire address field. Link-direct Extended
/// Device layouts remain 24-bit even on an iQ-R client. Packed word access to a
/// bit device consumes 16 device numbers per word; ordinary DWord/Float32 access
/// consumes two word devices per value, while packed DWord/Float32 access to a bit
/// device consumes 32 device numbers per value; a bit-block point consumes 16 bit devices;
/// and four words in a Direct long-timer status block consume one LTN/LSTN device.
/// This representability check does not enforce configured PLC usable ranges.
/// </para>
/// <para>
/// The factory <see cref="SlmpClientFactory.OpenAndConnectAsync(SlmpConnectionOptions, CancellationToken)"/>
/// returns a ready-to-use <see cref="SlmpClient"/> and is the recommended entry
/// point for most use cases.
/// </para>
/// <para>
/// Concurrent close or disposal rejects incomplete active work and queued work. A success value or
/// framed PLC end-code error that has completed command-specific decoding remains definitive and is
/// not replaced by the later lifecycle transition.
/// </para>
/// </remarks>
public sealed class SlmpClient : IDisposable, IAsyncDisposable
{
    private const int DirectWordPointLimit = 960;
    private const int DirectBitPointLimit = 7168;
    private const int DirectIqFBitPointLimit = 3584;
    private const int MemoryWordLimit = 480;
    private const int ExtendUnitByteLimit = 1920;
    private readonly string _host;
    private readonly int _port;
    private readonly SlmpTransportMode _transportMode;
    private TcpClient? _tcp;
    private NetworkStream? _tcpStream;
    private UdpClient? _udp;
    private ushort _serial;
    private readonly SlmpTargetAddress _targetAddress;
    private long _timeoutTicks = TimeSpan.FromSeconds(3).Ticks;
    private int _monitoringTimer = 0x0010;
    private readonly SemaphoreSlim _openGate = new(1, 1);
    private readonly object _operationSync = new();
    private readonly LinkedList<OperationWaiter> _operationWaiters = new();
    private readonly AsyncLocal<OperationContext?> _operationContext = new();
    private OperationGeneration _operationGeneration = new();
    private bool _operationActive;
    private int _lifecycleTransitions;
    private int _disposed;
    private bool _requiresExplicitOpen;
    private long _requestCount;
    private long _txBytes;
    private long _rxBytes;

    private sealed class OperationGeneration
    {
        internal CancellationTokenSource Cancellation { get; } = new();
        internal bool Disposed { get; set; }
        internal bool IsRetired => Cancellation.IsCancellationRequested;

        internal Exception CreateFailure(object client)
            => Disposed
                ? new ObjectDisposedException(client.GetType().FullName)
                : new SlmpConnectionClosedException();
    }

    private sealed class OperationWaiter(OperationGeneration generation)
    {
        internal OperationGeneration Generation { get; } = generation;
        internal TaskCompletionSource<OperationLease> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LinkedListNode<OperationWaiter>? Node { get; set; }
        internal CancellationTokenRegistration CancellationRegistration { get; set; }
    }

    private readonly record struct OperationLease(OperationGeneration Generation, bool OwnsTurn);
    private sealed record OperationContext(SlmpClient Client, OperationGeneration Generation);

    private sealed class SlmpCommandDecodeException(Exception innerException) : Exception(null, innerException);

    /// <summary>
    /// Initializes a new instance of the <see cref="SlmpClient"/> class.
    /// </summary>
    /// <param name="host">The IPv4 address or hostname that resolves to IPv4 for the PLC. IPv6 is not supported.</param>
    /// <param name="plcProfile">The PLC profile. This selection derives frame type and compatibility mode.</param>
    /// <param name="port">The required port number.</param>
    /// <param name="transportMode">The transport protocol (TCP or UDP).</param>
    /// <param name="targetAddress">The complete destination route.</param>
    public SlmpClient(
        string host,
        SlmpPlcProfile plcProfile,
        int port,
        SlmpTransportMode transportMode,
        SlmpTargetAddress targetAddress)
    {
        host = SlmpValidation.ValidateIpv4Host(host, nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (!Enum.IsDefined(transportMode)) throw new ArgumentOutOfRangeException(nameof(transportMode));
        plcProfile = SlmpPlcProfiles.ValidateConnectionProfile(plcProfile);
        _host = host;
        _port = port;
        _transportMode = transportMode;
        PlcProfile = plcProfile;
        _targetAddress = targetAddress;
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
    /// <summary>Gets the immutable destination routing information selected at construction.</summary>
    public SlmpTargetAddress TargetAddress => _targetAddress;
    /// <summary>Gets a read-only snapshot of cumulative traffic for this client lifetime.</summary>
    public SlmpTrafficStats TrafficStats => new(
        unchecked((ulong)Interlocked.Read(ref _requestCount)),
        unchecked((ulong)Interlocked.Read(ref _txBytes)),
        unchecked((ulong)Interlocked.Read(ref _rxBytes)));
    /// <summary>Gets or sets the monitoring timer value (multiples of 250ms). Default is 0x0010 (4s).</summary>
    public ushort MonitoringTimer
    {
        get => checked((ushort)Volatile.Read(ref _monitoringTimer));
        set => Volatile.Write(ref _monitoringTimer, value);
    }
    /// <summary>Gets or sets the communication timeout. Values must be from 1 millisecond through <c>int.MaxValue</c> milliseconds.</summary>
    public TimeSpan Timeout
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _timeoutTicks));
        set => Interlocked.Exchange(
            ref _timeoutTicks,
            SlmpValidation.ValidateTimeout(value, nameof(value)).Ticks);
    }
    internal byte[] LastRequestFrame { get; private set; } = [];
    internal byte[] LastResponseFrame { get; private set; } = [];
    internal Action<SlmpTraceFrame>? MaintainerTraceHook { get; set; }
    internal Func<Task>? BeforeCommandDecodeBarrier { get; set; }
    internal Func<Task>? DefinitiveResultBarrier { get; set; }

    /// <summary>Gets a value indicating whether the client is currently connected.</summary>
    public bool IsOpen => _transportMode == SlmpTransportMode.Tcp ? _tcp?.Connected == true : _udp is not null;

    private ValueTask<OperationLease> EnterOperationAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var nested = _operationContext.Value;
        if (nested is not null && ReferenceEquals(nested.Client, this))
        {
            if (nested.Generation.IsRetired)
                throw nested.Generation.CreateFailure(this);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new OperationLease(nested.Generation, OwnsTurn: false));
        }

        lock (_operationSync)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            var generation = _operationGeneration;
            if (!_operationActive && _lifecycleTransitions == 0)
            {
                _operationActive = true;
                return ValueTask.FromResult(new OperationLease(generation, OwnsTurn: true));
            }

            var waiter = new OperationWaiter(generation);
            waiter.Node = _operationWaiters.AddLast(waiter);
            if (cancellationToken.CanBeCanceled)
            {
                waiter.CancellationRegistration = cancellationToken.UnsafeRegister(
                    static state =>
                    {
                        var (client, queued, token) = ((SlmpClient, OperationWaiter, CancellationToken))state!;
                        lock (client._operationSync)
                        {
                            if (queued.Node is null)
                                return;
                            client._operationWaiters.Remove(queued.Node);
                            queued.Node = null;
                            queued.Completion.TrySetCanceled(token);
                        }
                    },
                    (this, waiter, cancellationToken));
            }
            return new ValueTask<OperationLease>(waiter.Completion.Task);
        }
    }

    private void ExitOperation(OperationLease lease)
    {
        if (!lease.OwnsTurn)
            return;

        OperationWaiter? next = null;
        lock (_operationSync)
        {
            if (_lifecycleTransitions != 0)
            {
                _operationActive = false;
            }
            else if (_operationWaiters.First is { } first)
            {
                next = first.Value;
                _operationWaiters.RemoveFirst();
                next.Node = null;
            }
            else
            {
                _operationActive = false;
            }
        }

        if (next is not null)
        {
            next.CancellationRegistration.Dispose();
            next.Completion.TrySetResult(new OperationLease(next.Generation, OwnsTurn: true));
        }
    }

    private void RetireOperationGeneration(bool disposed)
    {
        OperationGeneration retired;
        OperationWaiter[] rejected;
        lock (_operationSync)
        {
            retired = _operationGeneration;
            retired.Disposed |= disposed;
            _operationGeneration = new OperationGeneration();
            _lifecycleTransitions++;
            rejected = _operationWaiters
                .Where(waiter => ReferenceEquals(waiter.Generation, retired))
                .ToArray();
            foreach (var waiter in rejected)
            {
                if (waiter.Node is not null)
                {
                    _operationWaiters.Remove(waiter.Node);
                    waiter.Node = null;
                }
            }
        }

        retired.Cancellation.Cancel();
        foreach (var waiter in rejected)
        {
            waiter.CancellationRegistration.Dispose();
            waiter.Completion.TrySetException(retired.CreateFailure(this));
        }
    }

    private void CompleteLifecycleTransition()
    {
        OperationWaiter? next = null;
        lock (_operationSync)
        {
            _lifecycleTransitions--;
            if (_lifecycleTransitions < 0)
                throw new InvalidOperationException("Unbalanced SLMP lifecycle transition.");
            if (_lifecycleTransitions == 0 && !_operationActive && _operationWaiters.First is { } first)
            {
                next = first.Value;
                _operationWaiters.RemoveFirst();
                next.Node = null;
                _operationActive = true;
            }
        }

        if (next is not null)
        {
            next.CancellationRegistration.Dispose();
            next.Completion.TrySetResult(new OperationLease(next.Generation, OwnsTurn: true));
        }
    }

    internal async Task<T> ExecuteExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var lease = await EnterOperationAsync(cancellationToken).ConfigureAwait(false);
        var priorContext = _operationContext.Value;
        if (lease.OwnsTurn)
            _operationContext.Value = new OperationContext(this, lease.Generation);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.Generation.Cancellation.Token);
        try
        {
            var result = await operation(linked.Token).ConfigureAwait(false);
            if (lease.OwnsTurn && DefinitiveResultBarrier is { } resultBarrier)
                await resultBarrier().ConfigureAwait(false);
            return result;
        }
        catch (SlmpError exception) when (exception.EndCode is not null)
        {
            if (lease.OwnsTurn && DefinitiveResultBarrier is { } resultBarrier)
                await resultBarrier().ConfigureAwait(false);
            throw;
        }
        catch (SlmpOperationOutcomeUnknownException)
        {
            throw;
        }
        catch when (lease.Generation.IsRetired)
        {
            throw lease.Generation.CreateFailure(this);
        }
        finally
        {
            if (lease.OwnsTurn)
                _operationContext.Value = priorContext;
            ExitOperation(lease);
        }
    }

    internal async Task ExecuteExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteExclusiveAsync(
            async token =>
            {
                await operation(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the connection to the PLC asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var timeoutSnapshot = Timeout;
        return ExecuteExclusiveAsync(
            token => OpenWithTimeoutCoreAsync(timeoutSnapshot, token),
            cancellationToken);
    }

    private async Task OpenWithTimeoutCoreAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadlineCancellation = new CancellationTokenSource();
        deadlineCancellation.CancelAfter(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadlineCancellation.Token);
        try
        {
            await OpenCoreAsync(timeout, linked.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            deadlineCancellation.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            CloseTransport();
            throw new SlmpTimeoutException("The SLMP connection deadline expired.", exception);
        }
        catch (Exception exception) when (IsNativeTransportFailure(exception))
        {
            CloseTransport();
            throw new SlmpTransportException("The SLMP connection attempt failed.", exception);
        }
    }

    private async Task OpenCoreAsync(TimeSpan effectiveTimeout, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _openGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (IsOpen)
            {
                _requiresExplicitOpen = false;
                return;
            }
            var remoteAddress = await SlmpValidation.ResolveIpv4AddressAsync(_host, cancellationToken).ConfigureAwait(false);
            if (_transportMode == SlmpTransportMode.Tcp)
            {
                var tcp = new TcpClient(AddressFamily.InterNetwork);
                try
                {
                    await tcp.ConnectAsync(remoteAddress, _port, cancellationToken).ConfigureAwait(false);
                    tcp.NoDelay = true;
                    tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
                    tcp.ReceiveTimeout = (int)effectiveTimeout.TotalMilliseconds;
                    tcp.SendTimeout = (int)effectiveTimeout.TotalMilliseconds;
                    _tcp = tcp;
                    _tcpStream = tcp.GetStream();
                    _requiresExplicitOpen = false;
                }
                catch
                {
                    tcp.Dispose();
                    throw;
                }
                return;
            }

            var udp = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                udp.Client.ReceiveTimeout = (int)effectiveTimeout.TotalMilliseconds;
                udp.Client.SendTimeout = (int)effectiveTimeout.TotalMilliseconds;
                udp.Connect(new IPEndPoint(remoteAddress, _port));
                _udp = udp;
                _requiresExplicitOpen = false;
            }
            catch
            {
                udp.Dispose();
                throw;
            }
        }
        finally
        {
            _openGate.Release();
        }
    }

    /// <summary>Opens the connection to the PLC synchronously.</summary>
    public void Open() => OpenAsync().GetAwaiter().GetResult();

    /// <summary>Closes the connection and rejects the active and queued operations for this transport generation.</summary>
    public void Close()
    {
        RetireOperationGeneration(disposed: false);
        try
        {
            _openGate.Wait();
            try
            {
                CloseTransport();
            }
            finally
            {
                _openGate.Release();
            }
        }
        finally
        {
            CompleteLifecycleTransition();
        }
    }

    private void CloseTransport()
    {
        _tcpStream?.Dispose();
        _tcpStream = null;
        _tcp?.Close();
        _tcp = null;
        _udp?.Dispose();
        _udp = null;
    }

    private void InvalidateTransport()
    {
        _openGate.Wait();
        try
        {
            CloseTransport();
            _requiresExplicitOpen = true;
        }
        finally
        {
            _openGate.Release();
        }
    }

    /// <summary>Closes the connection to the PLC asynchronously.</summary>
    public Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    /// <summary>Disposes the client and permanently closes the connection.</summary>
    /// <remarks>
    /// Unlike <see cref="Close"/>, disposal is terminal. Later open and request operations
    /// throw <see cref="ObjectDisposedException"/>.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        RetireOperationGeneration(disposed: true);
        try
        {
            _openGate.Wait();
            try
            {
                CloseTransport();
            }
            finally
            {
                _openGate.Release();
            }
        }
        finally
        {
            CompleteLifecycleTransition();
        }
    }

    /// <summary>Asynchronously disposes the client and permanently closes the connection.</summary>
    /// <remarks>
    /// Disposal is terminal and idempotent. Later open and request operations throw
    /// <see cref="ObjectDisposedException"/>.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        RetireOperationGeneration(disposed: true);
        try
        {
            await _openGate.WaitAsync().ConfigureAwait(false);
            try
            {
                CloseTransport();
            }
            finally
            {
                _openGate.Release();
            }
        }
        finally
        {
            CompleteLifecycleTransition();
        }
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    private void FireTrace(SlmpTraceDirection direction, byte[] data)
    {
        var hook = MaintainerTraceHook;
        if (hook is null)
            return;

        try
        {
            hook(new SlmpTraceFrame(direction, data.ToArray(), DateTime.UtcNow));
        }
        catch
        {
            // Maintainer diagnostics must never change the communication result.
        }
    }

    /// <summary>
    /// Opens a connection with explicit stable settings and returns a connected <see cref="SlmpClient"/>.
    /// </summary>
    /// <param name="host">PLC IP address or hostname.</param>
    /// <param name="port">SLMP port number such as 1025 for iQ-R/iQ-F or 5007 for Q/L.</param>
    /// <param name="plcProfile">Canonical PLC profile used to derive the standard connection defaults.</param>
    /// <param name="transportMode">Required TCP or UDP transport.</param>
    /// <param name="targetAddress">Required complete destination route.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A connected client ready for high-level helpers such as
    /// <c>ReadTypedAsync</c>, <c>ReadNamedAsync</c>, and <c>PollAsync</c>.
    /// </returns>
    /// <remarks>
    /// This is the recommended entry point for application code because it
    /// combines one explicit PLC profile with the ordinary client's FIFO admission
    /// queue, which is safe to share across multiple tasks.
    /// </remarks>
    public static async Task<SlmpClient> OpenAndConnectAsync(
        string host,
        int port,
        SlmpPlcProfile plcProfile,
        SlmpTransportMode transportMode,
        SlmpTargetAddress targetAddress,
        CancellationToken cancellationToken = default)
        => await SlmpClientFactory.OpenAndConnectAsync(
            new SlmpConnectionOptions(host, plcProfile, port, transportMode, targetAddress),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Reads the PLC model and type name info asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An object containing model name and code.</returns>
    public async Task<SlmpTypeNameInfo> ReadTypeNameAsync(CancellationToken cancellationToken = default)
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.TypeName);
        return await RequestCoreAsync(
            SlmpCommand.ReadTypeName,
            0x0000,
            ReadOnlyMemory<byte>.Empty,
            true,
            static payload =>
            {
                if (payload.Length < 16) throw new SlmpError("read_type_name response too short");
                var model = Encoding.ASCII.GetString(payload.AsSpan(0, 16)).TrimEnd('\0', ' ');
                return payload.Length >= 18
                    ? new SlmpTypeNameInfo(model, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(16, 2)), true)
                    : new SlmpTypeNameInfo(model, 0, false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads <c>SD203</c> and decodes the CPU operation state from the lower 4 bits.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded CPU operation state and raw masked code.</returns>
    public async Task<SlmpCpuOperationState> ReadCpuOperationStateAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteExclusiveAsync(
            async token =>
            {
                var statusWord = (await ReadWordsRawAsync(
                    new SlmpDeviceAddress(SlmpDeviceCode.SD, 203, PlcProfile),
                    1,
                    token).ConfigureAwait(false))[0];
                return DecodeCpuOperationState(statusWord);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the configured profile-specific device upper-bound catalog from one canonical SD-register window.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A catalog containing the configured profile and device upper-bound entries.</returns>
    /// <remarks>No address probe or error-derived boundary inference is performed. Acquisition errors propagate to the caller.</remarks>
    public async Task<SlmpDeviceRangeCatalog> ReadDeviceRangeCatalogAsync(CancellationToken cancellationToken = default)
    {
        var rangeProfile = SlmpPlcProfiles.Resolve(PlcProfile).RangeProfile;
        var deviceRangeProfile = SlmpDeviceRangeResolver.ResolveProfile(rangeProfile);
        return await ExecuteExclusiveAsync(
            async token =>
            {
                var registers = await SlmpDeviceRangeResolver.ReadRegistersAsync(this, deviceRangeProfile, token).ConfigureAwait(false);
                return SlmpDeviceRangeResolver.BuildCatalog(rangeProfile, registers);
            },
            cancellationToken).ConfigureAwait(false);
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
        ValidateDirectWordReadAdmission(device, points);
        return await ReadWordsRawUncheckedAsync(device, points, cancellationToken).ConfigureAwait(false);
    }

    internal void ValidateDirectWordReadAdmission(SlmpDeviceAddress device, ushort points)
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectAccessPoints(points, bitUnit: false, "read_words", SlmpProfileLimit.DirectWordRead);
        ValidateDirectWordReadDevice(device, points);
        ValidateDirectDeviceSpan(device, points, bitUnit: false, nameof(device), longCurrentBlock: true);
    }

    internal Task<ushort[]> ReadLongStatusBlockWordsAsync(SlmpDeviceCode currentValueDevice, uint number, CancellationToken cancellationToken = default)
    {
        if (!IsLongCurrentValueDevice(currentValueDevice))
        {
            throw new ArgumentException(
                $"{currentValueDevice} is not a long-family current value device.",
                nameof(currentValueDevice));
        }

        return ReadWordsRawUncheckedAsync(new SlmpDeviceAddress(currentValueDevice, number, PlcProfile), 4, cancellationToken);
    }

    private async Task<ushort[]> ReadWordsRawUncheckedAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        var payload = BuildReadWritePayload(device, points, null, bitUnit: false);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        return await RequestCoreAsync(
            SlmpCommand.DeviceRead,
            sub,
            payload,
            true,
            data => DecodeWords(data, points, "read_words"),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteWordsAsync(SlmpDeviceAddress device, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ValidateDirectWordWriteAdmission(device, values.Count);
        await WriteWordsUncheckedAsync(device, values, cancellationToken).ConfigureAwait(false);
    }

    internal void ValidateDirectWordWriteAdmission(SlmpDeviceAddress device, int points)
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectWordWriteDevice(device);
        ValidateDirectAccessPoints(points, bitUnit: false, "write_words", SlmpProfileLimit.DirectWordWrite);
        ValidateDirectDeviceSpan(device, points, bitUnit: false, nameof(device));
    }

    private async Task WriteWordsUncheckedAsync(SlmpDeviceAddress device, IReadOnlyList<ushort> values, CancellationToken cancellationToken = default)
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectAccessPoints(values.Count, bitUnit: false, "write_words", SlmpProfileLimit.DirectWordWrite);
        var payload = BuildReadWritePayload(device, checked((ushort)values.Count), values, bitUnit: false);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        _ = await RequestCoreAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool[]> ReadBitsAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        ValidateDirectBitReadAdmission(device, points);
        return await ReadBitsUncheckedAsync(device, points, cancellationToken).ConfigureAwait(false);
    }

    internal void ValidateDirectBitReadAdmission(SlmpDeviceAddress device, ushort points)
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectAccessPoints(points, bitUnit: true, "read_bits", SlmpProfileLimit.DirectBitRead);
        ValidateDirectBitReadDevice(device);
        ValidateDirectDeviceSpan(device, points, bitUnit: true, nameof(device));
    }

    internal void ValidateDirectBitReadUncheckedAdmission(SlmpDeviceAddress device, ushort points)
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectAccessPoints(points, bitUnit: true, "read_bits", SlmpProfileLimit.DirectBitRead);
        if (!SlmpDeviceUnits.IsBit(device.Code))
        {
            throw new ArgumentException(
                $"Bit-unit reads require a bit-addressable device; {device.Code} is word-addressable.",
                nameof(device));
        }
        ValidateDirectDeviceSpan(device, points, bitUnit: true, nameof(device));
    }

    internal async Task<bool[]> ReadBitsUncheckedAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectAccessPoints(points, bitUnit: true, "read_bits", SlmpProfileLimit.DirectBitRead);
        if (!SlmpDeviceUnits.IsBit(device.Code))
        {
            throw new ArgumentException(
                $"Bit-unit reads require a bit-addressable device; {device.Code} is word-addressable.",
                nameof(device));
        }
        ValidateDirectDeviceSpan(device, points, bitUnit: true, nameof(device));
        var payload = BuildReadWritePayload(device, points, null, bitUnit: true);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0001 : (ushort)0x0003;
        return await RequestCoreAsync(
            SlmpCommand.DeviceRead,
            sub,
            payload,
            true,
            data => UnpackBitValues(data, points),
            cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken = default
    )
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectAccessPoints(points, bitUnit: false, "read_words_ext", SlmpProfileLimit.DirectWordRead);
        ValidateDirectWordReadDevice(device.Device, points, allowQualifiedOnlyDevice: true);
        var effectiveExtension = SlmpPayloads.ResolveEffectiveExtension(device, PlcProfile);
        EnsureExtendedProfileFeatureAllowed(device, effectiveExtension);
        ValidateDirectDeviceSpan(
            device.Device,
            points,
            bitUnit: false,
            nameof(device),
            effectiveExtension.DirectMemorySpecification == 0xF9 ? 0x00FF_FFFFUL : null,
            longCurrentBlock: true);
        var payload = SlmpPayloads.BuildReadWritePayloadExtended(device.Device, points, null, effectiveExtension, bitUnit: false, CompatibilityMode);
        var sub = effectiveExtension.DirectMemorySpecification == 0xF9 ? (ushort)0x0080
            : CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        return await RequestCoreAsync(
            SlmpCommand.DeviceRead,
            sub,
            payload,
            true,
            data => DecodeWords(data, points, "read_words_ext"),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteWordsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        IReadOnlyList<ushort> values,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(values);
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectAccessPoints(values.Count, bitUnit: false, "write_words_ext", SlmpProfileLimit.DirectWordWrite);
        ValidateDirectWordWriteDevice(device.Device, allowQualifiedOnlyDevice: true);
        var effectiveExtension = SlmpPayloads.ResolveEffectiveExtension(device, PlcProfile);
        EnsureExtendedProfileFeatureAllowed(device, effectiveExtension);
        ValidateDirectDeviceSpan(
            device.Device,
            values.Count,
            bitUnit: false,
            nameof(device),
            effectiveExtension.DirectMemorySpecification == 0xF9 ? 0x00FF_FFFFUL : null);
        var payload = SlmpPayloads.BuildReadWritePayloadExtended(device.Device, checked((ushort)values.Count), values, effectiveExtension, bitUnit: false, CompatibilityMode);
        var sub = effectiveExtension.DirectMemorySpecification == 0xF9 ? (ushort)0x0080
            : CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        _ = await RequestCoreAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool[]> ReadBitsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        ushort points,
        CancellationToken cancellationToken = default
    )
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectAccessPoints(points, bitUnit: true, "read_bits_ext", SlmpProfileLimit.DirectBitRead);
        ValidateDirectBitReadDevice(device.Device);
        var effectiveExtension = SlmpPayloads.ResolveEffectiveExtension(device, PlcProfile);
        EnsureExtendedProfileFeatureAllowed(device, effectiveExtension);
        ValidateDirectDeviceSpan(
            device.Device,
            points,
            bitUnit: true,
            nameof(device),
            effectiveExtension.DirectMemorySpecification == 0xF9 ? 0x00FF_FFFFUL : null);
        var payload = SlmpPayloads.BuildReadWritePayloadExtended(device.Device, points, null, effectiveExtension, bitUnit: true, CompatibilityMode);
        var sub = effectiveExtension.DirectMemorySpecification == 0xF9 ? (ushort)0x0081
            : CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0081 : (ushort)0x0083;
        return await RequestCoreAsync(
            SlmpCommand.DeviceRead,
            sub,
            payload,
            true,
            data => UnpackBitValues(data, points),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteBitsExtendedAsync(
        SlmpQualifiedDeviceAddress device,
        IReadOnlyList<bool> values,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(values);
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectAccessPoints(values.Count, bitUnit: true, "write_bits_ext", SlmpProfileLimit.DirectBitWrite);
        ValidateDirectBitWriteDevice(device.Device);
        var effectiveExtension = SlmpPayloads.ResolveEffectiveExtension(device, PlcProfile);
        EnsureExtendedProfileFeatureAllowed(device, effectiveExtension);
        ValidateDirectDeviceSpan(
            device.Device,
            values.Count,
            bitUnit: true,
            nameof(device),
            effectiveExtension.DirectMemorySpecification == 0xF9 ? 0x00FF_FFFFUL : null);
        var wordValues = new ushort[values.Count];
        for (var i = 0; i < values.Count; i++) wordValues[i] = values[i] ? (ushort)1 : (ushort)0;
        var payload = SlmpPayloads.BuildReadWritePayloadExtended(device.Device, checked((ushort)values.Count), wordValues, effectiveExtension, bitUnit: true, CompatibilityMode);
        var sub = effectiveExtension.DirectMemorySpecification == 0xF9 ? (ushort)0x0081
            : CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0081 : (ushort)0x0083;
        _ = await RequestCoreAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteBitsAsync(SlmpDeviceAddress device, IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectAccessPoints(values.Count, bitUnit: true, "write_bits", SlmpProfileLimit.DirectBitWrite);
        ValidateDirectBitWriteDevice(device);
        ValidateDirectDeviceSpan(device, values.Count, bitUnit: true, nameof(device));
        var wordValues = new ushort[values.Count];
        for (var i = 0; i < values.Count; i++) wordValues[i] = values[i] ? (ushort)1 : (ushort)0;
        var payload = BuildReadWritePayload(device, checked((ushort)values.Count), wordValues, bitUnit: true);
        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0001 : (ushort)0x0003;
        _ = await RequestCoreAsync(SlmpCommand.DeviceWrite, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads contiguous 32-bit values in one Direct Read request.</summary>
    /// <param name="device">Starting word-addressable device.</param>
    /// <param name="points">Number of DWord values, in public 32-bit units; maximum 480 for a 960-word profile limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value count is outside the active profile's DWord limit.</exception>
    public async Task<uint[]> ReadDWordsRawAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        ValidateDirectDWordReadAdmission(device, points);
        return await ExecuteExclusiveAsync(
            async token =>
            {
                var words = await ReadWordsRawAsync(device, (ushort)(points * 2), token).ConfigureAwait(false);
                var result = new uint[points];
                for (var index = 0; index < points; index++)
                    result[index] = (uint)(words[index * 2] | (words[(index * 2) + 1] << 16));
                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    internal void ValidateDirectDWordReadAdmission(SlmpDeviceAddress device, ushort points)
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        ValidateDirectDWordPoints(points, nameof(points), SlmpProfileLimit.DirectWordRead);
        ValidateDirectDWordReadDevice(device);
        ValidateDirectDeviceSpan(device, points * 2, bitUnit: false, nameof(device));
    }

    /// <summary>Writes contiguous 32-bit values in one Direct Write request.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The collection count is outside the active profile's DWord limit.</exception>
    public async Task WriteDWordsAsync(SlmpDeviceAddress device, IReadOnlyList<uint> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ValidateDirectDWordPoints(values.Count, nameof(values), SlmpProfileLimit.DirectWordWrite);
        ValidateDirectDWordWriteDevice(device);
        ValidateDirectDeviceSpan(device, values.Count * 2, bitUnit: false, nameof(device));
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        var words = new ushort[values.Count * 2];
        for (var i = 0; i < values.Count; i++)
        {
            words[i * 2] = (ushort)(values[i] & 0xFFFF);
            words[(i * 2) + 1] = (ushort)((values[i] >> 16) & 0xFFFF);
        }
        await WriteWordsUncheckedAsync(device, words, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads contiguous float32 values in one Direct Read request.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value count is outside the active profile's Float32 limit.</exception>
    public async Task<float[]> ReadFloat32sAsync(SlmpDeviceAddress device, ushort points, CancellationToken cancellationToken = default)
    {
        ValidateDirectDWordPoints(points, nameof(points), SlmpProfileLimit.DirectWordRead);
        ValidateDirectDWordReadDevice(device);
        ValidateDirectDeviceSpan(device, points * 2, bitUnit: false, nameof(device));
        return await ExecuteExclusiveAsync(
            async token =>
            {
                var dwords = await ReadDWordsRawAsync(device, points, token).ConfigureAwait(false);
                var values = new float[dwords.Length];
                for (var index = 0; index < dwords.Length; index++)
                    values[index] = BitConverter.Int32BitsToSingle(unchecked((int)dwords[index]));
                return values;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes contiguous float32 values in one Direct Write request.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The collection count is outside the active profile's Float32 limit.</exception>
    public async Task WriteFloat32sAsync(SlmpDeviceAddress device, IReadOnlyList<float> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ValidateDirectDWordPoints(values.Count, nameof(values), SlmpProfileLimit.DirectWordWrite);
        ValidateDirectDWordWriteDevice(device);
        ValidateDirectDeviceSpan(device, values.Count * 2, bitUnit: false, nameof(device));
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
        ArgumentNullException.ThrowIfNull(wordDevices);
        ArgumentNullException.ThrowIfNull(dwordDevices);
        ValidateRandomReadAdmission(wordDevices, dwordDevices);

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
        return await RequestCoreAsync(
            SlmpCommand.DeviceReadRandom,
            sub,
            payload,
            true,
            data => DecodeRandomReadResponse(data, wordDevices.Count, dwordDevices.Count, "read_random"),
            cancellationToken).ConfigureAwait(false);
    }

    internal void ValidateRandomReadAdmission(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices)
    {
        if (wordDevices.Count > 0xFF || dwordDevices.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordDevices), "random counts must be <= 255");
        }
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Random);
        ValidateRandomReadLikeCounts(wordDevices.Count, dwordDevices.Count, "read_random");
        ValidateRandomReadDevices(wordDevices, dwordDevices);
        foreach (var device in wordDevices)
            ValidateDirectDeviceSpan(device, 1, bitUnit: false, nameof(wordDevices));
        foreach (var device in dwordDevices)
            ValidateDirectDeviceSpan(device, GetDWordEntryWirePoints(device.Code), bitUnit: false, nameof(dwordDevices));
    }

    /// <summary>Reads only word devices in one random-read request.</summary>
    public async Task<ushort[]> ReadRandomWordsAsync(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        CancellationToken cancellationToken = default)
        => (await ReadRandomAsync(wordDevices, Array.Empty<SlmpDeviceAddress>(), cancellationToken).ConfigureAwait(false)).WordValues;

    /// <summary>Reads only DWord devices in one random-read request.</summary>
    public async Task<uint[]> ReadRandomDWordsAsync(
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        CancellationToken cancellationToken = default)
        => (await ReadRandomAsync(Array.Empty<SlmpDeviceAddress>(), dwordDevices, cancellationToken).ConfigureAwait(false)).DwordValues;

    public async Task WriteRandomWordsAsync(
        IReadOnlyList<(SlmpDeviceAddress Device, ushort Value)> wordEntries,
        IReadOnlyList<(SlmpDeviceAddress Device, uint Value)> dwordEntries,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(wordEntries);
        ArgumentNullException.ThrowIfNull(dwordEntries);
        if (wordEntries.Count > 0xFF || dwordEntries.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordEntries), "random counts must be <= 255");
        }
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Random);
        ValidateRandomWriteWordCounts(wordEntries.Count, dwordEntries.Count, "write_random_words");
        ValidateRandomWriteDevices(
            wordEntries.Select(static entry => entry.Device).ToArray(),
            dwordEntries.Select(static entry => entry.Device).ToArray());
        ValidateNoOverlappingRandomWriteTargets(wordEntries, dwordEntries);
        foreach (var entry in wordEntries)
            ValidateDirectDeviceSpan(entry.Device, 1, bitUnit: false, nameof(wordEntries));
        foreach (var entry in dwordEntries)
            ValidateDirectDeviceSpan(entry.Device, GetDWordEntryWirePoints(entry.Device.Code), bitUnit: false, nameof(dwordEntries));

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
        _ = await RequestCoreAsync(SlmpCommand.DeviceWriteRandom, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes only 16-bit entries in one random-write request.</summary>
    public Task WriteRandomU16sAsync(
        IReadOnlyList<(SlmpDeviceAddress Device, ushort Value)> wordEntries,
        CancellationToken cancellationToken = default)
        => WriteRandomWordsAsync(wordEntries, Array.Empty<(SlmpDeviceAddress Device, uint Value)>(), cancellationToken);

    /// <summary>Writes only 32-bit entries in one random-write request.</summary>
    public Task WriteRandomU32sAsync(
        IReadOnlyList<(SlmpDeviceAddress Device, uint Value)> dwordEntries,
        CancellationToken cancellationToken = default)
        => WriteRandomWordsAsync(Array.Empty<(SlmpDeviceAddress Device, ushort Value)>(), dwordEntries, cancellationToken);

    public async Task WriteRandomBitsAsync(
        IReadOnlyList<(SlmpDeviceAddress Device, bool Value)> bitEntries,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(bitEntries);
        if (bitEntries.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(bitEntries), "random bit count must be <= 255");
        }
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Random);
        ValidateRandomBitWriteCount(bitEntries.Count, "write_random_bits");
        ValidateRandomBitWriteDevices(bitEntries);
        ValidateNoDuplicateBitWriteTargets(bitEntries.Select(static entry => entry.Device));
        foreach (var entry in bitEntries)
            ValidateDirectDeviceSpan(entry.Device, 1, bitUnit: true, nameof(bitEntries));

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
        _ = await RequestCoreAsync(SlmpCommand.DeviceWriteRandom, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(ushort[] WordValues, uint[] DwordValues)> ReadRandomExtAsync(
        IReadOnlyList<SlmpQualifiedDeviceAddress> wordDevices,
        IReadOnlyList<SlmpQualifiedDeviceAddress> dwordDevices,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(wordDevices);
        ArgumentNullException.ThrowIfNull(dwordDevices);
        if (wordDevices.Count > 0xFF || dwordDevices.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordDevices), "random counts must be <= 255");
        }
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Random);
        ValidateRandomReadLikeCounts(wordDevices.Count, dwordDevices.Count, "read_random_ext", extended: true);
        ValidateRandomReadDevices(
            wordDevices.Select(static entry => entry.Device).ToArray(),
            dwordDevices.Select(static entry => entry.Device).ToArray(),
            allowQualifiedOnlyDevices: true);
        foreach (var entry in wordDevices)
        {
            EnsureExtendedProfileFeatureAllowed(entry, SlmpPayloads.ResolveEffectiveExtension(entry, PlcProfile));
            ValidateExtendedDeviceSpan(entry, 1, bitUnit: false, nameof(wordDevices));
        }
        foreach (var entry in dwordDevices)
        {
            EnsureExtendedProfileFeatureAllowed(entry, SlmpPayloads.ResolveEffectiveExtension(entry, PlcProfile));
            ValidateExtendedDeviceSpan(entry, GetDWordEntryWirePoints(entry.Device.Code), bitUnit: false, nameof(dwordDevices));
        }

        var linkDirect = SelectExtendedQlLayout(wordDevices.Concat(dwordDevices), "read_random_ext");
        var sub = linkDirect || CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        var payload = SlmpPayloads.BuildExtendedRandomReadPayload(wordDevices, dwordDevices, CompatibilityMode, PlcProfile);
        return await RequestCoreAsync(
            SlmpCommand.DeviceReadRandom,
            sub,
            payload,
            true,
            data => DecodeRandomReadResponse(data, wordDevices.Count, dwordDevices.Count, "read_random_ext"),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads only word devices through semantic Extended Device routes.</summary>
    public async Task<ushort[]> ReadRandomWordsExtendedAsync(
        IReadOnlyList<SlmpQualifiedDeviceAddress> wordDevices,
        CancellationToken cancellationToken = default)
        => (await ReadRandomExtAsync(wordDevices, Array.Empty<SlmpQualifiedDeviceAddress>(), cancellationToken).ConfigureAwait(false)).WordValues;

    /// <summary>Reads only DWord devices through semantic Extended Device routes.</summary>
    public async Task<uint[]> ReadRandomDWordsExtendedAsync(
        IReadOnlyList<SlmpQualifiedDeviceAddress> dwordDevices,
        CancellationToken cancellationToken = default)
        => (await ReadRandomExtAsync(Array.Empty<SlmpQualifiedDeviceAddress>(), dwordDevices, cancellationToken).ConfigureAwait(false)).DwordValues;

    public async Task WriteRandomWordsExtAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, ushort Value)> wordEntries,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, uint Value)> dwordEntries,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(wordEntries);
        ArgumentNullException.ThrowIfNull(dwordEntries);
        if (wordEntries.Count > 0xFF || dwordEntries.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordEntries), "random counts must be <= 255");
        }
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Random);
        ValidateRandomWriteWordCounts(wordEntries.Count, dwordEntries.Count, "write_random_words_ext", extended: true);
        ValidateRandomWriteDevices(
            wordEntries.Select(static entry => entry.Device.Device).ToArray(),
            dwordEntries.Select(static entry => entry.Device.Device).ToArray(),
            allowQualifiedOnlyDevices: true);
        ValidateNoOverlappingExtendedRandomWriteTargets(wordEntries, dwordEntries);
        foreach (var entry in wordEntries)
        {
            EnsureExtendedProfileFeatureAllowed(entry.Device, SlmpPayloads.ResolveEffectiveExtension(entry.Device, PlcProfile));
            ValidateExtendedDeviceSpan(entry.Device, 1, bitUnit: false, nameof(wordEntries));
        }
        foreach (var entry in dwordEntries)
        {
            EnsureExtendedProfileFeatureAllowed(entry.Device, SlmpPayloads.ResolveEffectiveExtension(entry.Device, PlcProfile));
            ValidateExtendedDeviceSpan(entry.Device, GetDWordEntryWirePoints(entry.Device.Device.Code), bitUnit: false, nameof(dwordEntries));
        }

        var linkDirect = SelectExtendedQlLayout(
            wordEntries.Select(static entry => entry.Device).Concat(dwordEntries.Select(static entry => entry.Device)),
            "write_random_words_ext");
        var sub = linkDirect || CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        var payload = SlmpPayloads.BuildExtendedRandomWordWritePayload(wordEntries, dwordEntries, CompatibilityMode, PlcProfile);
        _ = await RequestCoreAsync(SlmpCommand.DeviceWriteRandom, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes only 16-bit entries through semantic Extended Device routes.</summary>
    public Task WriteRandomU16sExtendedAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, ushort Value)> wordEntries,
        CancellationToken cancellationToken = default)
        => WriteRandomWordsExtAsync(wordEntries, Array.Empty<(SlmpQualifiedDeviceAddress Device, uint Value)>(), cancellationToken);

    /// <summary>Writes only 32-bit entries through semantic Extended Device routes.</summary>
    public Task WriteRandomU32sExtendedAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, uint Value)> dwordEntries,
        CancellationToken cancellationToken = default)
        => WriteRandomWordsExtAsync(Array.Empty<(SlmpQualifiedDeviceAddress Device, ushort Value)>(), dwordEntries, cancellationToken);

    public async Task WriteRandomBitsExtAsync(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, bool Value)> bitEntries,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(bitEntries);
        if (bitEntries.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(bitEntries), "random bit count must be <= 255");
        }
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Random);
        ValidateRandomBitWriteCount(bitEntries.Count, "write_random_bits_ext", extended: true);
        ValidateRandomBitWriteDevices(bitEntries.Select(entry => (entry.Device.Device, entry.Value)).ToArray());
        ValidateNoDuplicateExtendedBitWriteTargets(bitEntries.Select(static entry => entry.Device));
        foreach (var entry in bitEntries)
        {
            EnsureExtendedProfileFeatureAllowed(entry.Device, SlmpPayloads.ResolveEffectiveExtension(entry.Device, PlcProfile));
            ValidateExtendedDeviceSpan(entry.Device, 1, bitUnit: true, nameof(bitEntries));
        }

        var linkDirect = SelectExtendedQlLayout(
            bitEntries.Select(static entry => entry.Device),
            "write_random_bits_ext");
        var sub = linkDirect || CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0081 : (ushort)0x0083;
        var payload = SlmpPayloads.BuildExtendedRandomBitWritePayload(bitEntries, CompatibilityMode, PlcProfile);
        _ = await RequestCoreAsync(SlmpCommand.DeviceWriteRandom, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    internal byte[] BuildExtendedRandomReadPayload(
        IReadOnlyList<SlmpQualifiedDeviceAddress> wordDevices,
        IReadOnlyList<SlmpQualifiedDeviceAddress> dwordDevices
    )
        => SlmpPayloads.BuildExtendedRandomReadPayload(wordDevices, dwordDevices, CompatibilityMode, PlcProfile);

    internal byte[] BuildExtendedRandomWordWritePayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, ushort Value)> wordEntries,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, uint Value)> dwordEntries
    )
        => SlmpPayloads.BuildExtendedRandomWordWritePayload(wordEntries, dwordEntries, CompatibilityMode, PlcProfile);

    internal byte[] BuildExtendedRandomBitWritePayload(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, bool Value)> bitEntries
    )
        => SlmpPayloads.BuildExtendedRandomBitWritePayload(bitEntries, CompatibilityMode, PlcProfile);

    internal byte[] BuildExtendedMonitorRegisterPayload(
        IReadOnlyList<SlmpQualifiedDeviceAddress> wordDevices,
        IReadOnlyList<SlmpQualifiedDeviceAddress> dwordDevices
    )
        => SlmpPayloads.BuildExtendedMonitorRegisterPayload(wordDevices, dwordDevices, CompatibilityMode, PlcProfile);

    public async Task<(ushort[] WordValues, ushort[] BitWordValues)> ReadBlockAsync(
        IReadOnlyList<SlmpBlockRead> wordBlocks,
        IReadOnlyList<SlmpBlockRead> bitBlocks,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(wordBlocks);
        ArgumentNullException.ThrowIfNull(bitBlocks);
        ValidateNoNullBlockReadElements(wordBlocks, nameof(wordBlocks));
        ValidateNoNullBlockReadElements(bitBlocks, nameof(bitBlocks));
        if (wordBlocks.Count > 0xFF || bitBlocks.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordBlocks), "block counts must be <= 255");
        }
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Block);
        ValidateBlockRouteForProfile("Read Block (0x0406)");
        ValidateBlockReadLimits(wordBlocks, bitBlocks);
        ValidateBlockReadDevices(wordBlocks, bitBlocks);
        foreach (var block in wordBlocks)
            ValidateDirectDeviceSpan(
                block.Device,
                block.Points,
                bitUnit: false,
                nameof(wordBlocks),
                longCurrentBlock: true);
        foreach (var block in bitBlocks)
            ValidateDirectDeviceSpan(block.Device, block.Points, bitUnit: false, nameof(bitBlocks));

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
        return await RequestCoreAsync(
            SlmpCommand.DeviceReadBlock,
            sub,
            payload,
            true,
            data => DecodeBlockReadResponse(data, totalWordPoints, totalBitPoints),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads only word blocks in one block-read request.</summary>
    public async Task<ushort[]> ReadWordBlocksAsync(
        IReadOnlyList<SlmpBlockRead> wordBlocks,
        CancellationToken cancellationToken = default)
        => (await ReadBlockAsync(wordBlocks, Array.Empty<SlmpBlockRead>(), cancellationToken).ConfigureAwait(false)).WordValues;

    /// <summary>Reads only bit blocks in one block-read request.</summary>
    public async Task<ushort[]> ReadBitBlocksAsync(
        IReadOnlyList<SlmpBlockRead> bitBlocks,
        CancellationToken cancellationToken = default)
        => (await ReadBlockAsync(Array.Empty<SlmpBlockRead>(), bitBlocks, cancellationToken).ConfigureAwait(false)).BitWordValues;

    public async Task WriteBlockAsync(
        IReadOnlyList<SlmpBlockWrite> wordBlocks,
        IReadOnlyList<SlmpBlockWrite> bitBlocks,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(wordBlocks);
        ArgumentNullException.ThrowIfNull(bitBlocks);
        ValidateNoNullBlockWriteElements(wordBlocks, nameof(wordBlocks));
        ValidateNoNullBlockWriteElements(bitBlocks, nameof(bitBlocks));
        if (wordBlocks.Count > 0xFF || bitBlocks.Count > 0xFF)
        {
            throw new ArgumentOutOfRangeException(nameof(wordBlocks), "block counts must be <= 255");
        }
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Block);
        ValidateBlockRouteForProfile("Write Block (0x1406)");
        ValidateBlockWriteLimits(wordBlocks, bitBlocks);
        ValidateBlockWriteDevices(wordBlocks, bitBlocks);
        ValidateNoOverlappingBlockWriteTargets(wordBlocks, bitBlocks);
        foreach (var block in wordBlocks)
            ValidateDirectDeviceSpan(block.Device, block.Values.Count, bitUnit: false, nameof(wordBlocks));
        foreach (var block in bitBlocks)
            ValidateDirectDeviceSpan(block.Device, block.Values.Count, bitUnit: false, nameof(bitBlocks));

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
        _ = await RequestCoreAsync(SlmpCommand.DeviceWriteBlock, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes only word blocks in one block-write request.</summary>
    public Task WriteWordBlocksAsync(
        IReadOnlyList<SlmpBlockWrite> wordBlocks,
        CancellationToken cancellationToken = default)
        => WriteBlockAsync(wordBlocks, Array.Empty<SlmpBlockWrite>(), cancellationToken);

    /// <summary>Writes only bit blocks in one block-write request.</summary>
    public Task WriteBitBlocksAsync(
        IReadOnlyList<SlmpBlockWrite> bitBlocks,
        CancellationToken cancellationToken = default)
        => WriteBlockAsync(Array.Empty<SlmpBlockWrite>(), bitBlocks, cancellationToken);

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
        ArgumentNullException.ThrowIfNull(wordDevices);
        ArgumentNullException.ThrowIfNull(dwordDevices);
        if (wordDevices.Count == 0 && dwordDevices.Count == 0)
            throw new ArgumentException("wordDevices and dwordDevices must not both be empty.");
        if (wordDevices.Count > 0xFF || dwordDevices.Count > 0xFF)
            throw new ArgumentOutOfRangeException(nameof(wordDevices), "device counts must be <= 255.");
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Monitor);
        ValidateRandomReadLikeCounts(
            wordDevices.Count,
            dwordDevices.Count,
            "register_monitor_devices",
            limitKey: SlmpProfileLimit.MonitorRegisterWord);
        ValidateMonitorRegisterDevices(wordDevices, dwordDevices);
        foreach (var device in wordDevices)
            ValidateDirectDeviceSpan(device, 1, bitUnit: false, nameof(wordDevices));
        foreach (var device in dwordDevices)
            ValidateDirectDeviceSpan(device, GetDWordEntryWirePoints(device.Code), bitUnit: false, nameof(dwordDevices));

        var payload = new byte[2 + (wordDevices.Count + dwordDevices.Count) * DeviceSpecSize()];
        payload[0] = (byte)wordDevices.Count;
        payload[1] = (byte)dwordDevices.Count;
        var offset = 2;
        foreach (var device in wordDevices)
            offset += EncodeDeviceSpec(device, payload.AsSpan(offset));
        foreach (var device in dwordDevices)
            offset += EncodeDeviceSpec(device, payload.AsSpan(offset));

        var sub = CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0000 : (ushort)0x0002;
        _ = await RequestCoreAsync(SlmpCommand.MonitorRegister, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RegisterMonitorDevicesExtAsync(
        IReadOnlyList<SlmpQualifiedDeviceAddress> wordDevices,
        IReadOnlyList<SlmpQualifiedDeviceAddress> dwordDevices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wordDevices);
        ArgumentNullException.ThrowIfNull(dwordDevices);
        if (wordDevices.Count == 0 && dwordDevices.Count == 0)
            throw new ArgumentException("wordDevices and dwordDevices must not both be empty.");
        if (wordDevices.Count > 0xFF || dwordDevices.Count > 0xFF)
            throw new ArgumentOutOfRangeException(nameof(wordDevices), "device counts must be <= 255.");
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Monitor);
        ValidateRandomReadLikeCounts(
            wordDevices.Count,
            dwordDevices.Count,
            "register_monitor_devices_ext",
            extended: true,
            limitKey: SlmpProfileLimit.MonitorRegisterWord);
        ValidateMonitorRegisterDevices(
            wordDevices.Select(static entry => entry.Device).ToArray(),
            dwordDevices.Select(static entry => entry.Device).ToArray(),
            allowQualifiedOnlyDevices: true);
        foreach (var entry in wordDevices)
        {
            EnsureExtendedProfileFeatureAllowed(entry, SlmpPayloads.ResolveEffectiveExtension(entry, PlcProfile));
            ValidateExtendedDeviceSpan(entry, 1, bitUnit: false, nameof(wordDevices));
        }
        foreach (var entry in dwordDevices)
        {
            EnsureExtendedProfileFeatureAllowed(entry, SlmpPayloads.ResolveEffectiveExtension(entry, PlcProfile));
            ValidateExtendedDeviceSpan(entry, GetDWordEntryWirePoints(entry.Device.Code), bitUnit: false, nameof(dwordDevices));
        }

        var linkDirect = SelectExtendedQlLayout(
            wordDevices.Concat(dwordDevices),
            "register_monitor_devices_ext");
        var sub = linkDirect || CompatibilityMode == SlmpCompatibilityMode.Legacy ? (ushort)0x0080 : (ushort)0x0082;
        var payload = SlmpPayloads.BuildExtendedMonitorRegisterPayload(wordDevices, dwordDevices, CompatibilityMode, PlcProfile);
        _ = await RequestCoreAsync(SlmpCommand.MonitorRegister, sub, payload, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes one monitor cycle and returns the values of the previously registered devices (command 0x0802).
    /// </summary>
    /// <param name="wordPoints">Number of registered word devices. The combined count must be nonzero and within the active profile limit.</param>
    /// <param name="dwordPoints">Number of registered DWord devices.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SlmpMonitorResult> RunMonitorCycleAsync(
        int wordPoints,
        int dwordPoints,
        CancellationToken cancellationToken = default)
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Monitor);
        ValidateRandomReadLikeCounts(
            wordPoints,
            dwordPoints,
            "run_monitor_cycle",
            limitKey: SlmpProfileLimit.MonitorRegisterWord);
        return await RequestCoreAsync(
            SlmpCommand.Monitor,
            0x0000,
            ReadOnlyMemory<byte>.Empty,
            true,
            data =>
            {
                var decoded = DecodeRandomReadResponse(data, wordPoints, dwordPoints, "monitor");
                return new SlmpMonitorResult(decoded.WordValues, decoded.DwordValues);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoteRunAsync(
        SlmpRemoteMode mode,
        SlmpRemoteClearMode clearMode,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Enum.IsDefined(clearMode)) throw new ArgumentOutOfRangeException(nameof(clearMode));
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), (ushort)mode);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), (ushort)clearMode);
        _ = await RequestCoreAsync(SlmpCommand.RemoteRun, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoteStopAsync(CancellationToken cancellationToken = default)
    {
        _ = await RequestCoreAsync(SlmpCommand.RemoteStop, 0x0000, new byte[] { 0x01, 0x00 }, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemotePauseAsync(SlmpRemoteMode mode, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var payload = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)mode);
        _ = await RequestCoreAsync(SlmpCommand.RemotePause, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoteLatchClearAsync(CancellationToken cancellationToken = default) => _ = await RequestCoreAsync(SlmpCommand.RemoteLatchClear, 0x0000, new byte[] { 0x01, 0x00 }, true, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Sends the fixed Remote RESET frame without waiting for a success response,
    /// then invalidates the transport. Call <see cref="OpenAsync(CancellationToken)"/>
    /// explicitly before another request and verify the PLC state.
    /// </summary>
    public async Task RemoteResetAsync(CancellationToken cancellationToken = default)
    {
        _ = await RequestCoreAsync(SlmpCommand.RemoteReset, 0x0000, new byte[] { 0x01, 0x00 }, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemotePasswordUnlockAsync(string password, CancellationToken cancellationToken = default)
    {
        _ = await RequestCoreAsync(SlmpCommand.RemotePasswordUnlock, 0x0000, EncodePassword(password), true, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemotePasswordLockAsync(string password, CancellationToken cancellationToken = default)
    {
        _ = await RequestCoreAsync(SlmpCommand.RemotePasswordLock, 0x0000, EncodePassword(password), true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends one self-test request and returns the echo only when declared length,
    /// actual length, and payload all match the supplied ASCII hexadecimal bytes.
    /// </summary>
    public async Task<byte[]> SelfTestLoopbackAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (data.Length < 1 || data.Length > 960)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "loopback payload size out of range (1..960 bytes)");
        }

        var snapshot = data.ToArray();
        foreach (var value in snapshot)
        {
            if (!IsSelfTestHexByte(value))
            {
                throw new ArgumentException("loopback payload must contain only ASCII 0-9/A-F bytes", nameof(data));
            }
        }

        var payload = new byte[2 + snapshot.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), checked((ushort)snapshot.Length));
        snapshot.CopyTo(payload.AsSpan(2));
        return await RequestCoreAsync(
            SlmpCommand.SelfTest,
            0x0000,
            payload,
            true,
            response => DecodeSelfTestResponse(response, snapshot),
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsSelfTestHexByte(byte value)
        => value is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'F';

    /// <summary>Sends the fixed Clear Error command as exactly one request.</summary>
    public async Task ClearErrorAsync(CancellationToken cancellationToken = default) => _ = await RequestCoreAsync(SlmpCommand.ClearError, 0x0000, ReadOnlyMemory<byte>.Empty, true, cancellationToken).ConfigureAwait(false);

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
        ArgumentNullException.ThrowIfNull(points);
        var requestedPoints = points.ToArray();
        var abbrevs = abbreviationLabels ?? [];
        var payload = SlmpPayloads.BuildLabelArrayReadPayload(requestedPoints, abbrevs);
        return await RequestCoreAsync(
            SlmpCommand.LabelArrayRead,
            0x0000,
            payload,
            true,
            data => SlmpPayloads.ParseArrayLabelReadResponse(data, requestedPoints),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes array labels to the PLC (command 0x141A).
    /// </summary>
    public async Task WriteArrayLabelsAsync(
        IReadOnlyList<SlmpLabelArrayWritePoint> points,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);
        var abbrevs = abbreviationLabels ?? [];
        var payload = SlmpPayloads.BuildLabelArrayWritePayload(points, abbrevs);
        _ = await RequestCoreAsync(SlmpCommand.LabelArrayWrite, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads random labels from the PLC (command 0x041C).
    /// </summary>
    public async Task<SlmpLabelRandomReadResult[]> ReadRandomLabelsAsync(
        IReadOnlyList<string> labels,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var requestedLabels = labels.ToArray();
        var abbrevs = abbreviationLabels ?? [];
        var payload = SlmpPayloads.BuildLabelRandomReadPayload(requestedLabels, abbrevs);
        return await RequestCoreAsync(
            SlmpCommand.LabelReadRandom,
            0x0000,
            payload,
            true,
            data => SlmpPayloads.ParseRandomLabelReadResponse(data, requestedLabels.Length),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes random labels to the PLC (command 0x141B).
    /// </summary>
    public async Task WriteRandomLabelsAsync(
        IReadOnlyList<SlmpLabelRandomWritePoint> points,
        IReadOnlyList<string>? abbreviationLabels = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);
        var abbrevs = abbreviationLabels ?? [];
        var payload = SlmpPayloads.BuildLabelRandomWritePayload(points, abbrevs);
        _ = await RequestCoreAsync(SlmpCommand.LabelWriteRandom, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
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
        return await RequestCoreAsync(
            SlmpCommand.MemoryRead,
            0x0000,
            payload,
            true,
            data => DecodeWords(data, wordLength, "memory read"),
            cancellationToken).ConfigureAwait(false);
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
        ArgumentNullException.ThrowIfNull(values);
        ValidateMemoryWordLength(values.Count, "memory_write");
        var payload = new byte[6 + values.Count * 2];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), headAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), (ushort)values.Count);
        for (var i = 0; i < values.Count; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6 + i * 2, 2), values[i]);
        _ = await RequestCoreAsync(SlmpCommand.MemoryWrite, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Extend unit read / write (command 0x0601 / 0x1601)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads raw bytes from an extend unit (command 0x0601).
    /// </summary>
    /// <param name="headAddress">Starting address in the extend unit (32-bit).</param>
    /// <param name="byteLength">Number of bytes to read.</param>
    /// <param name="moduleNo">Configured Extend Unit module I/O number.</param>
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
        return await RequestCoreAsync(
            SlmpCommand.ExtendUnitRead,
            0x0000,
            payload,
            true,
            data => data.Length == byteLength
                ? data
                : throw new SlmpError($"extend unit read size mismatch: expected={byteLength} actual={data.Length}"),
            cancellationToken).ConfigureAwait(false);
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
        return await ExecuteExclusiveAsync(
            async token =>
            {
                var data = await ExtendUnitReadBytesAsync(headAddress, (ushort)(wordLength * 2), moduleNo, token).ConfigureAwait(false);
                return DecodeWords(data, wordLength, "extend unit word read");
            },
            cancellationToken).ConfigureAwait(false);
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
        return await ExecuteExclusiveAsync(
            async token =>
            {
                var data = await ExtendUnitReadBytesAsync(headAddress, 4, moduleNo, token).ConfigureAwait(false);
                return BinaryPrimitives.ReadUInt32LittleEndian(data);
            },
            cancellationToken).ConfigureAwait(false);
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
        _ = await RequestCoreAsync(SlmpCommand.ExtendUnitWrite, 0x0000, payload, true, cancellationToken).ConfigureAwait(false);
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
        ArgumentNullException.ThrowIfNull(values);
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
        int headNo,
        int points,
        CancellationToken cancellationToken = default)
    {
        var prepared = PrepareLongTimerRead(SlmpDeviceCode.LTN, headNo, points);
        return await ExecuteExclusiveAsync(
            async token =>
            {
                var words = await ReadLongTimerStatusWordsAsync(prepared.Device, prepared.WordCount, token).ConfigureAwait(false);
                return ParseLongTimerWords(words, headNo, "LTN", points);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one or more long retentive timers starting at the given device number.
    /// Each timer occupies 4 consecutive words: [current_lo, current_hi, status, reserved].
    /// </summary>
    /// <param name="headNo">Starting LSTN device number (e.g. 0 for LSTN0).</param>
    /// <param name="points">Number of timers to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SlmpLongTimerResult[]> ReadLongRetentiveTimerAsync(
        int headNo,
        int points,
        CancellationToken cancellationToken = default)
    {
        var prepared = PrepareLongTimerRead(SlmpDeviceCode.LSTN, headNo, points);
        return await ExecuteExclusiveAsync(
            async token =>
            {
                var words = await ReadLongTimerStatusWordsAsync(prepared.Device, prepared.WordCount, token).ConfigureAwait(false);
                return ParseLongTimerWords(words, headNo, "LSTN", points);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns the coil state of each long timer in the range.</summary>
    public async Task<bool[]> ReadLtcStatesAsync(int headNo, int points, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static timer => timer.Coil).ToArray();
    }

    /// <summary>Returns the contact state of each long timer in the range.</summary>
    public async Task<bool[]> ReadLtsStatesAsync(int headNo, int points, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static timer => timer.Contact).ToArray();
    }

    /// <summary>Returns the coil state of each long retentive timer in the range.</summary>
    public async Task<bool[]> ReadLstcStatesAsync(int headNo, int points, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongRetentiveTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static timer => timer.Coil).ToArray();
    }

    /// <summary>Returns the contact state of each long retentive timer in the range.</summary>
    public async Task<bool[]> ReadLstsStatesAsync(int headNo, int points, CancellationToken cancellationToken = default)
    {
        var timers = await ReadLongRetentiveTimerAsync(headNo, points, cancellationToken).ConfigureAwait(false);
        return timers.Select(static timer => timer.Contact).ToArray();
    }

    internal (SlmpDeviceAddress Device, ushort WordCount) PrepareLongTimerRead(
        SlmpDeviceCode currentValueDevice,
        int headNo,
        int points)
    {
        EnsureProfileFeatureAllowed(SlmpProfileFeature.Direct);
        if (headNo < 0)
            throw new ArgumentOutOfRangeException(nameof(headNo), "headNo must be >= 0.");
        if (points < 1 || points > DirectWordPointLimit / 4)
            throw new ArgumentOutOfRangeException(nameof(points), $"points must be <= {DirectWordPointLimit / 4} for one request.");
        var wordCount = points * 4;
        var device = new SlmpDeviceAddress(currentValueDevice, checked((uint)headNo), PlcProfile);
        EnsureDeviceProfile(device);
        ValidateLongTimerDeviceForWireMode(device, CompatibilityMode, nameof(headNo));
        ValidateDirectDeviceSpan(device, wordCount, bitUnit: false, nameof(headNo), longCurrentBlock: true);
        return (device, checked((ushort)wordCount));
    }

    private async Task<ushort[]> ReadLongTimerStatusWordsAsync(
        SlmpDeviceAddress device,
        ushort wordCount,
        CancellationToken cancellationToken)
    {
        return await ReadWordsRawUncheckedAsync(
            device,
            wordCount,
            cancellationToken).ConfigureAwait(false);
    }

    internal static void ValidateLongTimerDeviceForWireMode(
        SlmpDeviceAddress device,
        SlmpCompatibilityMode compatibilityMode,
        string parameterName)
    {
        if (compatibilityMode == SlmpCompatibilityMode.Legacy && device.Number > 0x00FF_FFFF)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Legacy device numbers must fit the 24-bit wire field (0..16777215).");
        }
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

    /// <summary>Runs one explicit maintainer raw command and returns its response payload.</summary>
    /// <param name="command">Command code.</param>
    /// <param name="subcommand">Subcommand code.</param>
    /// <param name="payload">
    /// Command payload. Over TCP its length must not exceed 65,529 bytes because the request
    /// data-length field also contains the six-byte monitoring timer, command, and subcommand
    /// prefix. IPv4 UDP limits are 65,492 bytes for 3E and 65,488 bytes for 4E.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>Oversized commands are rejected before transport and are not split automatically.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<byte[]> RawCommandAsync(
        SlmpCommand command,
        ushort subcommand,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
        => RequestCoreAsync(
            command,
            subcommand,
            payload,
            expectResponse: true,
            static response => response,
            cancellationToken);

    private Task<byte[]> RequestCoreAsync(
        SlmpCommand command,
        ushort subcommand,
        ReadOnlyMemory<byte> payload,
        bool expectResponse,
        CancellationToken cancellationToken)
    {
        Func<byte[], byte[]> decodeResponse = expectResponse
            ? response => DecodeEmptyAcknowledgement(response, command, subcommand)
            : static response => response;
        return RequestCoreAsync(
            command,
            subcommand,
            payload,
            expectResponse,
            decodeResponse,
            cancellationToken);
    }

    private static byte[] DecodeEmptyAcknowledgement(
        byte[] response,
        SlmpCommand command,
        ushort subcommand)
    {
        if (response.Length != 0)
        {
            throw new SlmpError(
                $"SLMP acknowledgement payload must be empty; actual length={response.Length}.",
                command: command,
                subcommand: subcommand);
        }

        return response;
    }

    private Task<T> RequestCoreAsync<T>(
        SlmpCommand command,
        ushort subcommand,
        ReadOnlyMemory<byte> payload,
        bool expectResponse,
        Func<byte[], T> decodeResponse,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(decodeResponse);
        _ = ValidateRequestPayloadLength(payload.Length, nameof(payload));
        var payloadSnapshot = payload.ToArray();
        var timeoutSnapshot = Timeout;
        var monitoringTimerSnapshot = MonitoringTimer;
        return ExecuteExclusiveAsync(
            token => RequestCoreWithinOperationAsync(
                command,
                subcommand,
                payloadSnapshot,
                expectResponse,
                timeoutSnapshot,
                monitoringTimerSnapshot,
                decodeResponse,
                token),
            cancellationToken);
    }

    private async Task<T> RequestCoreWithinOperationAsync<T>(
        SlmpCommand command,
        ushort subcommand,
        ReadOnlyMemory<byte> payload,
        bool expectResponse,
        TimeSpan timeout,
        ushort monitoringTimer,
        Func<byte[], T> decodeResponse,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (_requiresExplicitOpen)
            throw new SlmpNotConnectedException();

        var stateChanging = IsStateChangingCommand(command);
        var requestMayHaveBeenSent = false;
        using var deadlineCancellation = new CancellationTokenSource();
        deadlineCancellation.CancelAfter(timeout);
        using var transactionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadlineCancellation.Token);
        try
        {
            if (!IsOpen)
                await OpenCoreAsync(timeout, transactionCancellation.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (deadlineCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested)
                InvalidateTransport();
            throw ClassifyTransactionFailure(
                exception,
                stateChanging,
                requestMayHaveBeenSent,
                deadlineCancellation.IsCancellationRequested,
                cancellationToken);
        }

        var frame = BuildRequestFrame(command, subcommand, payload.Span, monitoringTimer);
        ushort? expectedSerial = FrameType == SlmpFrameType.Frame4E
            ? BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2, 2))
            : null;
        LastRequestFrame = frame;
        FireTrace(SlmpTraceDirection.Send, frame);
        if (_transportMode == SlmpTransportMode.Tcp)
        {
            if (_tcpStream is null) throw new SlmpTransportException("The SLMP TCP transport is not open.");
            try
            {
                transactionCancellation.Token.ThrowIfCancellationRequested();
                requestMayHaveBeenSent = true;
                await _tcpStream.WriteAsync(frame, transactionCancellation.Token).ConfigureAwait(false);
                RecordSend(frame.Length);
                if (!expectResponse)
                {
                    LastResponseFrame = [];
                    InvalidateTransport();
                    return await DecodeCommandResponseAsync([], decodeResponse, transactionCancellation.Token).ConfigureAwait(false);
                }

                while (true)
                {
                    transactionCancellation.Token.ThrowIfCancellationRequested();
                    var response = await ReceiveTcpFrameAsync(
                        _tcpStream,
                        FrameType,
                        transactionCancellation.Token).ConfigureAwait(false);
                    transactionCancellation.Token.ThrowIfCancellationRequested();
                    RecordReceive(response.Length);
                    LastResponseFrame = response;
                    FireTrace(SlmpTraceDirection.Receive, response);
                    transactionCancellation.Token.ThrowIfCancellationRequested();
                    ValidateResponseEnvelope(response, FrameType);
                    if (!HasExpectedResponseSerial(response, expectedSerial))
                        continue;
                    if (!HasExpectedResponseRoute(response, FrameType, TargetAddress))
                        continue;
                    var parsed = ParseResponse(command, subcommand, response);
                    transactionCancellation.Token.ThrowIfCancellationRequested();
                    return await DecodeCommandResponseAsync(parsed, decodeResponse, transactionCancellation.Token).ConfigureAwait(false);
                }
            }
            catch (SlmpCommandDecodeException exception)
            {
                var decodeFailure = exception.InnerException!;
                if (stateChanging || transactionCancellation.IsCancellationRequested)
                {
                    InvalidateTransport();
                    throw ClassifyTransactionFailure(
                        decodeFailure,
                        stateChanging,
                        requestMayHaveBeenSent,
                        deadlineCancellation.IsCancellationRequested,
                        cancellationToken);
                }

                ExceptionDispatchInfo.Capture(decodeFailure).Throw();
                throw;
            }
            catch (SlmpError ex) when (ex.EndCode is not null)
            {
                // A PLC end code is an application-level response. Keep the
                // connection usable so callers can issue the next request.
                throw;
            }
            catch (SlmpOperationOutcomeUnknownException)
            {
                throw;
            }
            catch (Exception exception)
            {
                InvalidateTransport();
                throw ClassifyTransactionFailure(
                    exception,
                    stateChanging,
                    requestMayHaveBeenSent,
                    deadlineCancellation.IsCancellationRequested,
                    cancellationToken);
            }
        }

        if (_udp is null) throw new SlmpTransportException("The SLMP UDP transport is not open.");
        try
        {
            transactionCancellation.Token.ThrowIfCancellationRequested();
            requestMayHaveBeenSent = true;
            await _udp.SendAsync(frame, transactionCancellation.Token).ConfigureAwait(false);
            RecordSend(frame.Length);
            if (!expectResponse)
            {
                LastResponseFrame = [];
                InvalidateTransport();
                return await DecodeCommandResponseAsync([], decodeResponse, transactionCancellation.Token).ConfigureAwait(false);
            }

            while (true)
            {
                transactionCancellation.Token.ThrowIfCancellationRequested();
                var datagram = await _udp.ReceiveAsync(transactionCancellation.Token).ConfigureAwait(false);
                transactionCancellation.Token.ThrowIfCancellationRequested();
                RecordReceive(datagram.Buffer.Length);
                LastResponseFrame = datagram.Buffer;
                FireTrace(SlmpTraceDirection.Receive, datagram.Buffer);
                transactionCancellation.Token.ThrowIfCancellationRequested();
                ValidateResponseEnvelope(datagram.Buffer, FrameType);
                if (!HasExpectedResponseSerial(datagram.Buffer, expectedSerial))
                    continue;
                if (!HasExpectedResponseRoute(datagram.Buffer, FrameType, TargetAddress))
                    continue;
                var parsed = ParseResponse(command, subcommand, datagram.Buffer);
                transactionCancellation.Token.ThrowIfCancellationRequested();
                return await DecodeCommandResponseAsync(parsed, decodeResponse, transactionCancellation.Token).ConfigureAwait(false);
            }
        }
        catch (SlmpCommandDecodeException exception)
        {
            var decodeFailure = exception.InnerException!;
            if (stateChanging || transactionCancellation.IsCancellationRequested)
            {
                InvalidateTransport();
                throw ClassifyTransactionFailure(
                    decodeFailure,
                    stateChanging,
                    requestMayHaveBeenSent,
                    deadlineCancellation.IsCancellationRequested,
                    cancellationToken);
            }

            ExceptionDispatchInfo.Capture(decodeFailure).Throw();
            throw;
        }
        catch (SlmpError ex) when (ex.EndCode is not null)
        {
            throw;
        }
        catch (SlmpOperationOutcomeUnknownException)
        {
            throw;
        }
        catch (Exception exception)
        {
            InvalidateTransport();
            throw ClassifyTransactionFailure(
                exception,
                stateChanging,
                requestMayHaveBeenSent,
                deadlineCancellation.IsCancellationRequested,
                cancellationToken);
        }
    }

    private async Task<T> DecodeCommandResponseAsync<T>(
        byte[] response,
        Func<byte[], T> decodeResponse,
        CancellationToken cancellationToken)
    {
        if (BeforeCommandDecodeBarrier is { } decodeBarrier)
            await decodeBarrier().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return decodeResponse(response);
        }
        catch (Exception exception)
        {
            throw new SlmpCommandDecodeException(exception);
        }
    }

    private Exception ClassifyTransactionFailure(
        Exception exception,
        bool stateChanging,
        bool requestMayHaveBeenSent,
        bool deadlineExpired,
        CancellationToken operationCancellation)
    {
        var generationRetired = _operationContext.Value is { } context &&
            ReferenceEquals(context.Client, this) &&
            context.Generation.IsRetired;

        if (stateChanging && requestMayHaveBeenSent)
        {
            var reason = generationRetired
                ? SlmpOutcomeUnknownReason.Closed
                : deadlineExpired && !operationCancellation.IsCancellationRequested
                    ? SlmpOutcomeUnknownReason.Timeout
                    : operationCancellation.IsCancellationRequested
                        ? SlmpOutcomeUnknownReason.Cancellation
                        : exception is SlmpError
                            ? SlmpOutcomeUnknownReason.MalformedResponse
                            : SlmpOutcomeUnknownReason.Transport;
            return new SlmpOperationOutcomeUnknownException(reason, NormalizeTransportFailure(exception));
        }

        if (generationRetired)
            return new SlmpConnectionClosedException();
        if (deadlineExpired && !operationCancellation.IsCancellationRequested)
            return new SlmpTimeoutException("The SLMP transaction deadline expired.", exception);
        if (operationCancellation.IsCancellationRequested)
            return exception;
        return NormalizeTransportFailure(exception);
    }

    private static bool IsNativeTransportFailure(Exception exception)
        => exception is SocketException or IOException or ObjectDisposedException;

    private static Exception NormalizeTransportFailure(Exception exception)
        => exception is SlmpTransportException
            ? exception
            : IsNativeTransportFailure(exception)
                ? new SlmpTransportException("The SLMP transport failed.", exception)
                : exception;

    private static bool IsStateChangingCommand(SlmpCommand command)
        => command is not (
            SlmpCommand.DeviceRead or
            SlmpCommand.DeviceReadRandom or
            SlmpCommand.DeviceReadBlock or
            SlmpCommand.Monitor or
            SlmpCommand.ReadTypeName or
            SlmpCommand.LabelArrayRead or
            SlmpCommand.LabelReadRandom or
            SlmpCommand.MemoryRead or
            SlmpCommand.ExtendUnitRead or
            SlmpCommand.SelfTest);

    private void RecordSend(int frameLength)
    {
        Interlocked.Increment(ref _requestCount);
        Interlocked.Add(ref _txBytes, frameLength);
    }

    private void RecordReceive(int frameLength) => Interlocked.Add(ref _rxBytes, frameLength);

    private byte[] BuildRequestFrame(
        SlmpCommand command,
        ushort subcommand,
        ReadOnlySpan<byte> payload,
        ushort monitoringTimer)
    {
        var payloadLength = ValidateRequestPayloadLength(payload.Length, nameof(payload));
        var headerSize = FrameType == SlmpFrameType.Frame4E ? 19 : 15;
        var frame = new byte[headerSize + payloadLength];
        if (FrameType == SlmpFrameType.Frame4E)
        {
            frame[0] = 0x54; frame[1] = 0x00;
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2, 2), _serial++);
            frame[6] = TargetAddress.Network;
            frame[7] = TargetAddress.Station;
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8, 2), TargetAddress.ModuleIo);
            frame[10] = TargetAddress.Multidrop;
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(11, 2), checked((ushort)(6 + payloadLength)));
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(13, 2), monitoringTimer);
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
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(7, 2), checked((ushort)(6 + payloadLength)));
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(9, 2), monitoringTimer);
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(11, 2), (ushort)command);
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(13, 2), subcommand);
        }

        payload.CopyTo(frame.AsSpan(headerSize));
        return frame;
    }

    private int ValidateRequestPayloadLength(long payloadLength, string parameterName)
        => SlmpValidation.ValidateRequestPayloadLength(
            payloadLength,
            parameterName,
            SlmpValidation.GetMaxRequestPayloadLength(_transportMode, FrameType));

    private static async Task<byte[]> ReceiveTcpFrameAsync(
        NetworkStream stream,
        SlmpFrameType expectedFrameType,
        CancellationToken cancellationToken)
    {
        var header = new byte[13];
        await ReadExactAsync(stream, header.AsMemory(0, 2), cancellationToken).ConfigureAwait(false);

        if (expectedFrameType == SlmpFrameType.Frame4E && header[0] == 0xD4 && header[1] == 0x00)
        {
            // 4E response: subheader(2) + serial(2) + reserved(2) + net(1) + sta(1) + mod(2) + multi(1) + len(2) = 13 bytes header
            await ReadExactAsync(stream, header.AsMemory(2, 11), cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(11, 2));
            var frame = new byte[13 + length];
            header.AsSpan(0, 13).CopyTo(frame);
            await ReadExactAsync(stream, frame.AsMemory(13, length), cancellationToken).ConfigureAwait(false);
            return frame;
        }

        if (expectedFrameType == SlmpFrameType.Frame3E && header[0] == 0xD0 && header[1] == 0x00)
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
        if (endCode != 0)
        {
            var errorInfo = SlmpErrorInfo.Parse(response.AsSpan(headerSize + 2, dataLength - 2));
            throw new SlmpError(
                $"SLMP error end_code=0x{endCode:X4} command=0x{(ushort)command:X4} subcommand=0x{subcommand:X4}",
                endCode,
                command,
                subcommand,
                errorInfo: errorInfo);
        }
        return dataLength == 2 ? [] : response.AsSpan(headerSize + 2, dataLength - 2).ToArray();
    }

    private static bool HasExpectedResponseSerial(byte[] response, ushort? expectedSerial)
    {
        if (expectedSerial is null)
        {
            return true;
        }
        if (response.Length < 4 || response[0] != 0xD4 || response[1] != 0x00)
        {
            return false;
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(2, 2)) == expectedSerial.Value;
    }

    private static bool HasExpectedResponseRoute(
        byte[] response,
        SlmpFrameType frameType,
        SlmpTargetAddress expectedTarget)
    {
        var routeOffset = frameType == SlmpFrameType.Frame4E ? 6 : 2;
        return response[routeOffset] == expectedTarget.Network
            && response[routeOffset + 1] == expectedTarget.Station
            && BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(routeOffset + 2, 2)) == expectedTarget.ModuleIo
            && response[routeOffset + 4] == expectedTarget.Multidrop;
    }

    private static void ValidateResponseEnvelope(byte[] response, SlmpFrameType expectedFrameType)
    {
        var headerSize = expectedFrameType == SlmpFrameType.Frame4E ? 13 : 9;
        var expectedSubheader = expectedFrameType == SlmpFrameType.Frame4E ? (ushort)0x00D4 : (ushort)0x00D0;
        if (response.Length < headerSize)
        {
            throw new SlmpError("malformed response");
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(0, 2)) != expectedSubheader)
        {
            throw new SlmpError("unexpected response frame type");
        }
        if (expectedFrameType == SlmpFrameType.Frame4E && (response[4] != 0 || response[5] != 0))
        {
            throw new SlmpError("malformed response");
        }

        var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(headerSize - 2, 2));
        if (dataLength < 2 || response.Length != headerSize + dataLength)
        {
            throw new SlmpError("malformed response");
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (!buffer.IsEmpty)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new SlmpTransportException("The SLMP connection closed while reading a response.");
            buffer = buffer[read..];
        }
    }

    internal int DeviceSpecSize() => SlmpPayloads.DeviceSpecSize(CompatibilityMode);

    internal int EncodeDeviceSpec(SlmpDeviceAddress device, Span<byte> output)
    {
        EnsureDeviceProfile(device);

        return SlmpPayloads.EncodeDeviceSpec(device, output, CompatibilityMode);
    }

    private void EnsureDeviceProfile(SlmpDeviceAddress device)
    {
        if (device.PlcProfile != PlcProfile)
            throw new ArgumentException(
                $"Device PlcProfile '{SlmpPlcProfiles.ToCanonicalString(device.PlcProfile)}' does not match client PlcProfile '{SlmpPlcProfiles.ToCanonicalString(PlcProfile)}'.",
                nameof(device));
    }

    private void EnsureProfileFeatureAllowed(SlmpProfileFeature feature)
    {
        if (!SlmpCapabilityProfiles.TryGetFeature(PlcProfile, feature, out var capabilityFeature))
            return;

        if (capabilityFeature.State is not (SlmpProfileFeatureState.Blocked or SlmpProfileFeatureState.Unverified))
            return;

        var featureKey = SlmpCapabilityProfiles.ToCanonicalFeatureKey(feature);
        var state = SlmpCapabilityProfiles.ToCanonicalState(capabilityFeature.State);
        var evidence = capabilityFeature.Note is null
            ? $"{capabilityFeature.Source}; {SlmpCapabilityProfiles.CanonicalSource}"
            : $"{capabilityFeature.Source}: {capabilityFeature.Note}; {SlmpCapabilityProfiles.CanonicalSource}";
        throw new SlmpProfileFeatureException(PlcProfile, featureKey, state, evidence);
    }

    private void EnsureExtendedProfileFeatureAllowed(SlmpQualifiedDeviceAddress device, SlmpExtensionSpec effectiveExtension)
    {
        EnsureDeviceProfile(device.Device);
        if (effectiveExtension.DirectMemorySpecification == 0xF9)
        {
            EnsureProfileFeatureAllowed(SlmpProfileFeature.ExtLinkDirect);
            return;
        }

        if (device.Device.Code == SlmpDeviceCode.HG || effectiveExtension.DirectMemorySpecification == 0xFA)
        {
            EnsureProfileFeatureAllowed(SlmpProfileFeature.HgCpuBuffer);
            return;
        }

        if (device.Device.Code == SlmpDeviceCode.G || effectiveExtension.DirectMemorySpecification == 0xF8)
        {
            EnsureProfileFeatureAllowed(SlmpProfileFeature.ExtModuleAccess);
        }
    }

    private bool SelectExtendedQlLayout(
        IEnumerable<SlmpQualifiedDeviceAddress> devices,
        string operation)
    {
        var hasLinkDirect = false;
        var hasIqrLayout = false;
        foreach (var device in devices)
        {
            if (SlmpPayloads.ResolveEffectiveExtension(device, PlcProfile).DirectMemorySpecification == 0xF9)
                hasLinkDirect = true;
            else
                hasIqrLayout = true;
        }

        if (CompatibilityMode != SlmpCompatibilityMode.Legacy && hasLinkDirect && hasIqrLayout)
        {
            throw new ArgumentException(
                $"{operation} cannot mix J link-direct Q/L entries with 13-byte iQ-R extended entries in one request.");
        }
        return hasLinkDirect;
    }

    private int DirectPointLimit(bool bitUnit, SlmpProfileLimit limitKey)
    {
        if (SlmpCapabilityProfiles.TryGetLimit(PlcProfile, limitKey, out var profileLimit))
            return profileLimit.Max;

        return bitUnit
            ? (PlcProfile == SlmpPlcProfile.IqF ? DirectIqFBitPointLimit : DirectBitPointLimit)
            : DirectWordPointLimit;
    }

    private void ValidateDirectAccessPoints(int points, bool bitUnit, string name, SlmpProfileLimit limitKey)
    {
        var limit = DirectPointLimit(bitUnit, limitKey);
        var unit = bitUnit ? "bit" : "word";
        if (points < 1 || points > limit)
            throw new ArgumentOutOfRangeException(name, $"{name} {unit} access points out of range (1..{limit}): {points}");
    }

    private void ValidateDirectDWordPoints(int points, string parameterName, SlmpProfileLimit wordLimitKey)
    {
        var limit = DirectPointLimit(bitUnit: false, wordLimitKey) / 2;
        if (points < 1 || points > limit)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                points,
                $"DWord/Float32 access points must be in public value units (1..{limit}).");
        }
    }

    private void ValidateDirectDeviceSpan(
        SlmpDeviceAddress device,
        int wirePoints,
        bool bitUnit,
        string parameterName,
        ulong? maximumDeviceNumber = null,
        bool longCurrentBlock = false)
    {
        EnsureDeviceProfile(device);
        var consumedDeviceNumbers = GetConsumedDirectDeviceNumbers(device.Code, wirePoints, bitUnit, longCurrentBlock);
        var maximum = maximumDeviceNumber ??
            (CompatibilityMode == SlmpCompatibilityMode.Legacy ? 0x00FF_FFFFUL : uint.MaxValue);
        var end = (ulong)device.Number + consumedDeviceNumbers - 1UL;
        if (end > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                device,
                $"The contiguous device span exceeds the selected wire address field: " +
                $"start={device.Number}, consumed_device_numbers={consumedDeviceNumbers}, " +
                $"end={end}, maximum={maximum}.");
        }
    }

    private static ulong GetConsumedDirectDeviceNumbers(
        SlmpDeviceCode deviceCode,
        int wirePoints,
        bool bitUnit,
        bool longCurrentBlock = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(wirePoints, 1);
        if (bitUnit)
            return (ulong)wirePoints;
        if (longCurrentBlock && IsLongTimerCurrentBlockDevice(deviceCode))
            return (ulong)wirePoints / 4UL;
        return SlmpDeviceUnits.IsBit(deviceCode)
            ? (ulong)wirePoints * 16UL
            : (ulong)wirePoints;
    }

    private static int GetDWordEntryWirePoints(SlmpDeviceCode deviceCode)
        => IsLongCurrentValueDevice(deviceCode) || IsDWordOnlyScalarDevice(deviceCode) ? 1 : 2;

    private void ValidateExtendedDeviceSpan(
        SlmpQualifiedDeviceAddress device,
        int wirePoints,
        bool bitUnit,
        string parameterName)
    {
        var extension = SlmpPayloads.ResolveEffectiveExtension(device, PlcProfile);
        ValidateDirectDeviceSpan(
            device.Device,
            wirePoints,
            bitUnit,
            parameterName,
            extension.DirectMemorySpecification == 0xF9 ? 0x00FF_FFFFUL : null);
    }

    private void ValidateRandomReadLikeCounts(
        int wordPoints,
        int dwordPoints,
        string name,
        bool extended = false,
        SlmpProfileLimit limitKey = SlmpProfileLimit.RandomReadWord)
    {
        if (wordPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(wordPoints), $"{name} word access points must be non-negative: {wordPoints}");
        if (dwordPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(dwordPoints), $"{name} dword access points must be non-negative: {dwordPoints}");
        var total = (long)wordPoints + dwordPoints;
        var fallbackLimit = extended || CompatibilityMode != SlmpCompatibilityMode.Legacy ? 96 : 192;
        var limit = fallbackLimit;
        if (SlmpCapabilityProfiles.TryGetLimit(PlcProfile, extended ? ExtendedLimitKey(limitKey) : limitKey, out var profileLimit))
        {
            limit = profileLimit.Max;
        }
        if (total < 1 || total > limit)
            throw new ArgumentOutOfRangeException(name, $"{name} total access points out of range (1..{limit}): word={wordPoints}, dword={dwordPoints}");
    }

    private void ValidateRandomWriteWordCounts(int wordPoints, int dwordPoints, string name, bool extended = false)
    {
        var total = wordPoints + dwordPoints;
        if (total < 1)
            throw new ArgumentOutOfRangeException(name, $"{name} word/dword access points out of range: word={wordPoints}, dword={dwordPoints}");

        var weighted = (wordPoints * 12) + (dwordPoints * 14);
        var profileLimitKey = extended ? SlmpProfileLimit.RandomWriteWordExt : SlmpProfileLimit.RandomWriteWord;
        if (SlmpCapabilityProfiles.TryGetLimit(PlcProfile, profileLimitKey, out var profileLimit))
        {
            var countLimit = profileLimit.Max;
            if (total > countLimit)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    $"{name} word/dword access points out of range (1..{countLimit}): word={wordPoints}, dword={dwordPoints}");
            }

            int? weightedLimit = profileLimit.WeightedMax;

            if (weightedLimit is { } effectiveWeightedLimit && weighted > effectiveWeightedLimit)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    $"{name} word/dword access points out of range: word={wordPoints}, dword={dwordPoints}, weighted={weighted}, limit={effectiveWeightedLimit}");
            }

            return;
        }

        var limit = extended || CompatibilityMode != SlmpCompatibilityMode.Legacy ? 960 : 1920;
        if (weighted > limit)
            throw new ArgumentOutOfRangeException(
                name,
                $"{name} word/dword access points out of range: word={wordPoints}, dword={dwordPoints}, weighted={weighted}, limit={limit}");
    }

    private void ValidateRandomBitWriteCount(int points, string name, bool extended = false)
    {
        var fallbackLimit = extended || CompatibilityMode != SlmpCompatibilityMode.Legacy ? 94 : 188;
        var limit = fallbackLimit;
        var profileLimitKey = extended ? SlmpProfileLimit.RandomWriteBitExt : SlmpProfileLimit.RandomWriteBit;
        if (SlmpCapabilityProfiles.TryGetLimit(PlcProfile, profileLimitKey, out var profileLimit))
        {
            limit = profileLimit.Max;
        }
        if (points < 1 || points > limit)
            throw new ArgumentOutOfRangeException(name, $"{name} bit access points out of range (1..{limit}): {points}");
    }

    private static SlmpProfileLimit ExtendedLimitKey(SlmpProfileLimit limitKey)
        => limitKey switch
        {
            SlmpProfileLimit.RandomReadWord => SlmpProfileLimit.RandomReadWordExt,
            SlmpProfileLimit.MonitorRegisterWord => SlmpProfileLimit.MonitorRegisterWordExt,
            _ => throw new ArgumentOutOfRangeException(nameof(limitKey), limitKey, "Unsupported extended profile limit."),
        };

    private void ValidateBlockReadLimits(IReadOnlyList<SlmpBlockRead> wordBlocks, IReadOnlyList<SlmpBlockRead> bitBlocks)
    {
        var totalBlocks = wordBlocks.Count + bitBlocks.Count;
        ValidateBlockCount(totalBlocks, "read_block");
        var totalPoints = wordBlocks.Sum(static block => ValidateBlockPointCount(block.Points, "read_block word")) +
                          bitBlocks.Sum(static block => ValidateBlockPointCount(block.Points, "read_block bit"));
        var limit = DirectPointLimit(bitUnit: false, SlmpProfileLimit.DirectWordRead);
        if (totalPoints > limit)
            throw new ArgumentOutOfRangeException(nameof(wordBlocks), $"read_block total device points out of range (<={limit}): total_points={totalPoints}");
    }

    private static void ValidateNoNullBlockReadElements(IReadOnlyList<SlmpBlockRead> blocks, string parameterName)
    {
        for (var index = 0; index < blocks.Count; index++)
        {
            if (blocks[index] is null)
                throw new ArgumentException($"Block collection contains null at index {index}.", parameterName);
        }
    }

    private static void ValidateNoNullBlockWriteElements(IReadOnlyList<SlmpBlockWrite> blocks, string parameterName)
    {
        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];
            if (block is null)
                throw new ArgumentException($"Block collection contains null at index {index}.", parameterName);
            if (block.Values is null)
                throw new ArgumentException($"Block at index {index} has null Values.", parameterName);
        }
    }

    private void ValidateBlockWriteLimits(IReadOnlyList<SlmpBlockWrite> wordBlocks, IReadOnlyList<SlmpBlockWrite> bitBlocks)
    {
        var totalBlocks = wordBlocks.Count + bitBlocks.Count;
        ValidateBlockCount(totalBlocks, "write_block");
        var totalPoints = wordBlocks.Sum(static block => ValidateBlockPointCount(block.Values.Count, "write_block word")) +
                          bitBlocks.Sum(static block => ValidateBlockPointCount(block.Values.Count, "write_block bit"));
        var perBlockOverhead = CompatibilityMode == SlmpCompatibilityMode.Legacy ? 4 : 9;
        var weighted = totalPoints + (totalBlocks * perBlockOverhead);
        var limit = DirectPointLimit(bitUnit: false, SlmpProfileLimit.DirectWordWrite);
        if (weighted > limit)
            throw new ArgumentOutOfRangeException(
                nameof(wordBlocks),
                $"write_block total device points out of range (<={limit}): weighted={weighted}, total_points={totalPoints}");
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

    private static void ValidateBlockRouteForProfile(string commandLabel)
    {
        _ = commandLabel;
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
        if (!SlmpDeviceUnits.IsBit(device.Code))
        {
            throw new ArgumentException(
                $"Bit-unit reads require a bit-addressable device; {device.Code} is word-addressable.",
                nameof(device));
        }

        // Long-family state bits must enter through the typed/named helpers. Some devices
        // use status blocks internally, and LCS/LCC use direct bit read only inside the helper.
        if (IsLongTimerStateDevice(device.Code) || IsLongCounterContactDevice(device))
        {
            throw new ArgumentException(
                $"Direct bit read is not supported for {device.Code}. Use ReadTypedAsync or the explicit long-family helper.",
                nameof(device));
        }
    }

    private void ValidateDirectBitWriteDevice(SlmpDeviceAddress device)
    {
        if (!SlmpDeviceUnits.IsBit(device.Code))
        {
            throw new ArgumentException(
                $"Bit-unit writes require a bit-addressable device; {device.Code} is word-addressable. Use an explicit bit-in-word helper for one bit inside a word device.",
                nameof(device));
        }

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

    private static void ValidateDirectWordReadDevice(
        SlmpDeviceAddress device,
        ushort points,
        bool allowQualifiedOnlyDevice = false)
    {
        if (!allowQualifiedOnlyDevice && IsQualifiedOnlyDevice(device.Code))
        {
            throw new ArgumentException(
                $"{device.Code} cannot be accessed as a standalone device. Use U-qualified access such as U4\\G10 or U3E0\\HG0.",
                nameof(device));
        }

        if (IsRandomDWordOnlyReadDevice(device.Code))
        {
            throw new ArgumentException(
                $"Direct word read is not supported for {device.Code}. {device.Code} is a 32-bit device; use ReadTypedAsync or an explicit route-appropriate helper instead.",
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

    private void ValidateDirectWordWriteDevice(
        SlmpDeviceAddress device,
        bool allowQualifiedOnlyDevice = false)
    {
        ValidateWritableDevice(device);

        if (!allowQualifiedOnlyDevice && IsQualifiedOnlyDevice(device.Code))
        {
            throw new ArgumentException(
                $"{device.Code} cannot be accessed as a standalone device. Use U-qualified access such as U4\\G10 or U3E0\\HG0.",
                nameof(device));
        }

        if (IsLongCurrentValueDevice(device.Code) || IsDWordOnlyScalarDevice(device.Code))
        {
            throw new ArgumentException(
                $"Direct word write is not supported for {device.Code}. {device.Code} is a 32-bit device; use WriteTypedAsync/WriteNamedAsync with ':D' or ':L' instead.",
                nameof(device));
        }
    }

    private void ValidateDirectDWordWriteDevice(SlmpDeviceAddress device)
    {
        ValidateWritableDevice(device);

        if (IsQualifiedOnlyDevice(device.Code))
        {
            throw new ArgumentException(
                $"{device.Code} cannot be accessed as a standalone device. Use U-qualified access such as U4\\G10 or U3E0\\HG0.",
                nameof(device));
        }

        if (IsLongCurrentValueDevice(device.Code) || IsDWordOnlyScalarDevice(device.Code))
        {
            throw new ArgumentException(
                $"Direct DWord write is not supported for {device.Code}. Use WriteTypedAsync/WriteNamedAsync with ':D' or ':L' so the 32-bit write route is selected.",
                nameof(device));
        }
    }

    private static void ValidateDirectDWordReadDevice(SlmpDeviceAddress device)
    {
        if (IsQualifiedOnlyDevice(device.Code))
        {
            throw new ArgumentException(
                $"{device.Code} cannot be accessed as a standalone device. Use U-qualified access such as U4\\G10 or U3E0\\HG0.",
                nameof(device));
        }

        if (IsLongCurrentValueDevice(device.Code) || IsDWordOnlyScalarDevice(device.Code))
        {
            throw new ArgumentException(
                $"Direct DWord read is not supported for {device.Code}. Use ReadTypedAsync or the explicit long-family helper so the supported 32-bit route is selected.",
                nameof(device));
        }
    }

    private static void ValidateRandomReadDevices(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        bool allowQualifiedOnlyDevices = false)
    {
        // LTS/LTC/LSTS/LSTC can be written by random bit write, but they are not
        // readable by Read Random (0x0403); use the status-block helpers instead.
        if (wordDevices.Any(device => IsLongTimerStateDevice(device.Code)) || dwordDevices.Any(device => IsLongTimerStateDevice(device.Code)))
        {
            throw new ArgumentException(
                "Read Random (0x0403) does not support LTS/LTC/LSTS/LSTC. Use ReadTypedAsync or the explicit long timer status helpers instead.",
                nameof(wordDevices));
        }

        if (wordDevices.Any(IsLongCounterContactDevice) || dwordDevices.Any(IsLongCounterContactDevice))
        {
            throw new ArgumentException(
                "Read Random (0x0403) does not support LCS/LCC. Use ReadTypedAsync so the long counter bit helper is selected.",
                nameof(wordDevices));
        }

        if (wordDevices.Any(device => IsLongCurrentValueDevice(device.Code) || IsDWordOnlyScalarDevice(device.Code)))
        {
            throw new ArgumentException(
                "Read Random (0x0403) does not support LTN/LSTN/LCN/LZ as word entries. Use dword entries, ReadTypedAsync, or the explicit long timer helper instead.",
                nameof(wordDevices));
        }

        if (!allowQualifiedOnlyDevices &&
            (wordDevices.Any(device => IsQualifiedOnlyDevice(device.Code)) ||
             dwordDevices.Any(device => IsQualifiedOnlyDevice(device.Code))))
        {
            throw new ArgumentException(
                "Read Random (0x0403) does not support standalone G/HG. Use U-qualified extended access instead.",
                nameof(wordDevices));
        }
    }

    private static void ValidateBlockReadDevices(
        IReadOnlyList<SlmpBlockRead> wordBlocks,
        IReadOnlyList<SlmpBlockRead> bitBlocks)
    {
        ValidateBlockUnitCategories(wordBlocks.Select(static block => block.Device), bitBlocks.Select(static block => block.Device));
        if (wordBlocks.Any(block => IsRandomDWordOnlyReadDevice(block.Device.Code)) ||
            bitBlocks.Any(block => IsRandomDWordOnlyReadDevice(block.Device.Code)))
        {
            throw new ArgumentException(
                "Read Block (0x0406) does not support LCN/LZ as word or bit blocks. Use ReadTypedAsync/ReadNamedAsync so random dword read is selected.",
                nameof(wordBlocks));
        }

        if (wordBlocks.Any(block => IsQualifiedOnlyDevice(block.Device.Code)) ||
            bitBlocks.Any(block => IsQualifiedOnlyDevice(block.Device.Code)))
        {
            throw new ArgumentException(
                "Read Block (0x0406) does not support standalone G/HG. Use U-qualified extended access instead.",
                nameof(wordBlocks));
        }

        var invalidLongCurrentBlock = wordBlocks.FirstOrDefault(block =>
            IsLongTimerCurrentBlockDevice(block.Device.Code) && (block.Points == 0 || block.Points % 4 != 0));
        if (invalidLongCurrentBlock is not null)
        {
            throw new ArgumentException(
                $"Read Block (0x0406) direct read of {invalidLongCurrentBlock.Device.Code} requires 4-word blocks. Requested points={invalidLongCurrentBlock.Points}; use ReadTypedAsync or the explicit long timer helper for 32-bit current values.",
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

    private void ValidateBlockWriteDevices(
        IReadOnlyList<SlmpBlockWrite> wordBlocks,
        IReadOnlyList<SlmpBlockWrite> bitBlocks)
    {
        ValidateBlockUnitCategories(wordBlocks.Select(static block => block.Device), bitBlocks.Select(static block => block.Device));
        var readOnlyBlock = wordBlocks.Concat(bitBlocks).FirstOrDefault(block => IsReadOnlyForProfile(block.Device.Code));
        if (readOnlyBlock is not null)
        {
            throw new ArgumentException(
                ReadOnlyForProfileMessage(readOnlyBlock.Device.Code),
                nameof(wordBlocks));
        }

        if (wordBlocks.Any(block => IsLongCurrentValueDevice(block.Device.Code) || IsDWordOnlyScalarDevice(block.Device.Code)) ||
            bitBlocks.Any(block => IsLongCurrentValueDevice(block.Device.Code) || IsDWordOnlyScalarDevice(block.Device.Code)))
        {
            throw new ArgumentException(
                "Write Block (0x1406) does not support LTN/LSTN/LCN/LZ as word or bit blocks. Use WriteTypedAsync/WriteNamedAsync with ':D' or ':L' instead.",
                nameof(wordBlocks));
        }

        if (wordBlocks.Any(block => IsQualifiedOnlyDevice(block.Device.Code)) ||
            bitBlocks.Any(block => IsQualifiedOnlyDevice(block.Device.Code)))
        {
            throw new ArgumentException(
                "Write Block (0x1406) does not support standalone G/HG. Use U-qualified extended access instead.",
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
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        bool allowQualifiedOnlyDevices = false)
    {
        if (wordDevices.Any(IsLongCounterContactDevice) || dwordDevices.Any(IsLongCounterContactDevice))
        {
            throw new ArgumentException(
                "Entry Monitor Device (0x0801) does not support LCS/LCC.",
                nameof(wordDevices));
        }

        if (!allowQualifiedOnlyDevices &&
            (wordDevices.Any(device => IsQualifiedOnlyDevice(device.Code)) ||
             dwordDevices.Any(device => IsQualifiedOnlyDevice(device.Code))))
        {
            throw new ArgumentException(
                "Entry Monitor Device (0x0801) does not support standalone G/HG. Use U-qualified extended access instead.",
                nameof(wordDevices));
        }
    }

    private static bool IsLongCounterContactDevice(SlmpDeviceAddress device)
        => device.Code is SlmpDeviceCode.LCS or SlmpDeviceCode.LCC;

    private static bool RequiresRandomBitWrite(SlmpDeviceCode code)
        => IsLongTimerStateDevice(code)
            || code is SlmpDeviceCode.LCS or SlmpDeviceCode.LCC;

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

    private static void ValidateBlockUnitCategories(
        IEnumerable<SlmpDeviceAddress> wordDevices,
        IEnumerable<SlmpDeviceAddress> bitDevices)
    {
        if (wordDevices.Any(device => !SlmpDeviceUnits.IsWord(device.Code)))
            throw new ArgumentException("Word blocks require word-addressable device codes.", nameof(wordDevices));
        if (bitDevices.Any(device => !SlmpDeviceUnits.IsBit(device.Code)))
            throw new ArgumentException("Bit blocks require bit-addressable device codes.", nameof(bitDevices));
    }

    private static bool IsRandomDWordOnlyReadDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.LCN or SlmpDeviceCode.LZ;

    private static bool IsQualifiedOnlyDevice(SlmpDeviceCode code)
        => code is SlmpDeviceCode.G or SlmpDeviceCode.HG;

    private bool IsReadOnlyForProfile(SlmpDeviceCode code)
        => SlmpCapabilityProfiles.IsReadOnly(PlcProfile, code.ToString());

    private void ValidateRandomWriteDevices(
        IReadOnlyList<SlmpDeviceAddress> wordDevices,
        IReadOnlyList<SlmpDeviceAddress> dwordDevices,
        bool allowQualifiedOnlyDevices = false)
    {
        var readOnlyDevice = wordDevices.Concat(dwordDevices).FirstOrDefault(device => IsReadOnlyForProfile(device.Code));
        if (readOnlyDevice.Code != default)
        {
            throw new ArgumentException(
                ReadOnlyForProfileMessage(readOnlyDevice.Code),
                nameof(wordDevices));
        }

        if (wordDevices.Any(device => IsLongCurrentValueDevice(device.Code) || IsDWordOnlyScalarDevice(device.Code)))
        {
            throw new ArgumentException(
                "Write Random (0x1402) does not support LTN/LSTN/LCN/LZ as word entries. Use dword entries or WriteTypedAsync/WriteNamedAsync with ':D' or ':L' instead.",
                nameof(wordDevices));
        }

        if (!allowQualifiedOnlyDevices &&
            (wordDevices.Any(device => IsQualifiedOnlyDevice(device.Code)) ||
             dwordDevices.Any(device => IsQualifiedOnlyDevice(device.Code))))
        {
            throw new ArgumentException(
                "Write Random (0x1402) does not support standalone G/HG. Use U-qualified extended access instead.",
                nameof(wordDevices));
        }
    }

    private void ValidateRandomBitWriteDevices(IReadOnlyList<(SlmpDeviceAddress Device, bool Value)> bitEntries)
    {
        if (bitEntries.Any(entry => IsQualifiedOnlyDevice(entry.Device.Code)))
        {
            throw new ArgumentException(
                "Write Random bits (0x1402) does not support G/HG devices because those routes are word-addressable. Use U-qualified word access instead.",
                nameof(bitEntries));
        }

        if (bitEntries.Any(entry => !SlmpDeviceUnits.IsBit(entry.Device.Code)))
        {
            throw new ArgumentException(
                "Random bit writes require bit-addressable device codes.",
                nameof(bitEntries));
        }

        var readOnlyEntry = bitEntries.FirstOrDefault(entry => IsReadOnlyForProfile(entry.Device.Code));
        if (readOnlyEntry.Device.Code != default)
        {
            throw new ArgumentException(
                ReadOnlyForProfileMessage(readOnlyEntry.Device.Code),
                nameof(bitEntries));
        }

    }

    private static void ValidateNoOverlappingRandomWriteTargets(
        IReadOnlyList<(SlmpDeviceAddress Device, ushort Value)> wordEntries,
        IReadOnlyList<(SlmpDeviceAddress Device, uint Value)> dwordEntries)
    {
        var ranges = wordEntries
            .Select(static entry => (
                entry.Device,
                Width: checked((uint)GetConsumedDirectDeviceNumbers(entry.Device.Code, 1, bitUnit: false))))
            .Concat(dwordEntries.Select(static entry => (
                entry.Device,
                Width: checked((uint)GetConsumedDirectDeviceNumbers(
                    entry.Device.Code,
                    GetDWordEntryWirePoints(entry.Device.Code),
                    bitUnit: false)))))
            .ToArray();
        for (var i = 0; i < ranges.Length; i++)
        {
            for (var j = i + 1; j < ranges.Length; j++)
            {
                if (SameDeviceSpace(ranges[i].Device, ranges[j].Device) &&
                    RangesOverlap(ranges[i].Device.Number, ranges[i].Width, ranges[j].Device.Number, ranges[j].Width))
                {
                    throw new ArgumentException("Random word write destinations must not overlap.", nameof(wordEntries));
                }
            }
        }
    }

    private void ValidateNoOverlappingExtendedRandomWriteTargets(
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, ushort Value)> wordEntries,
        IReadOnlyList<(SlmpQualifiedDeviceAddress Device, uint Value)> dwordEntries)
    {
        var ranges = wordEntries
            .Select(entry => (
                entry.Device,
                Extension: SlmpPayloads.ResolveEffectiveExtension(entry.Device, PlcProfile),
                Width: checked((uint)GetConsumedDirectDeviceNumbers(entry.Device.Device.Code, 1, bitUnit: false))))
            .Concat(dwordEntries.Select(entry => (
                entry.Device,
                Extension: SlmpPayloads.ResolveEffectiveExtension(entry.Device, PlcProfile),
                Width: checked((uint)GetConsumedDirectDeviceNumbers(
                    entry.Device.Device.Code,
                    GetDWordEntryWirePoints(entry.Device.Device.Code),
                    bitUnit: false)))))
            .ToArray();
        for (var i = 0; i < ranges.Length; i++)
        {
            for (var j = i + 1; j < ranges.Length; j++)
            {
                if (SameDeviceSpace(ranges[i].Device.Device, ranges[j].Device.Device) &&
                    ranges[i].Extension == ranges[j].Extension &&
                    RangesOverlap(ranges[i].Device.Device.Number, ranges[i].Width, ranges[j].Device.Device.Number, ranges[j].Width))
                {
                    throw new ArgumentException("Extended random word write destinations must not overlap.", nameof(wordEntries));
                }
            }
        }
    }

    private void ValidateNoDuplicateExtendedBitWriteTargets(
        IEnumerable<SlmpQualifiedDeviceAddress> devices)
    {
        var seen = new HashSet<(SlmpDeviceAddress Device, SlmpExtensionSpec Extension)>();
        foreach (var device in devices)
        {
            var target = (device.Device, SlmpPayloads.ResolveEffectiveExtension(device, PlcProfile));
            if (!seen.Add(target))
            {
                throw new ArgumentException(
                    "Extended random bit write destinations must not contain duplicates.",
                    nameof(devices));
            }
        }
    }

    private static void ValidateNoDuplicateBitWriteTargets<TDevice>(IEnumerable<TDevice> devices)
        where TDevice : notnull
    {
        var seen = new HashSet<TDevice>();
        if (devices.Any(device => !seen.Add(device)))
            throw new ArgumentException("Random bit write destinations must not contain duplicates.", nameof(devices));
    }

    private static void ValidateNoOverlappingBlockWriteTargets(
        IReadOnlyList<SlmpBlockWrite> wordBlocks,
        IReadOnlyList<SlmpBlockWrite> bitBlocks)
    {
        ValidateNoOverlappingBlocks(wordBlocks, nameof(wordBlocks));
        ValidateNoOverlappingBlocks(bitBlocks, nameof(bitBlocks));
    }

    private static void ValidateNoOverlappingBlocks(IReadOnlyList<SlmpBlockWrite> blocks, string parameterName)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            for (var j = i + 1; j < blocks.Count; j++)
            {
                if (SameDeviceSpace(blocks[i].Device, blocks[j].Device) &&
                    RangesOverlap(
                        blocks[i].Device.Number,
                        checked((uint)GetConsumedDirectDeviceNumbers(
                            blocks[i].Device.Code,
                            blocks[i].Values.Count,
                            bitUnit: false)),
                        blocks[j].Device.Number,
                        checked((uint)GetConsumedDirectDeviceNumbers(
                            blocks[j].Device.Code,
                            blocks[j].Values.Count,
                            bitUnit: false))))
                {
                    throw new ArgumentException("Block write ranges must not overlap within the same unit category.", parameterName);
                }
            }
        }
    }

    private static bool SameDeviceSpace(SlmpDeviceAddress left, SlmpDeviceAddress right)
        => left.Code == right.Code && left.PlcProfile == right.PlcProfile;

    private static bool RangesOverlap(uint leftStart, uint leftWidth, uint rightStart, uint rightWidth)
    {
        var leftEnd = checked((ulong)leftStart + leftWidth);
        var rightEnd = checked((ulong)rightStart + rightWidth);
        return leftStart < rightEnd && rightStart < leftEnd;
    }

    private void ValidateWritableDevice(SlmpDeviceAddress device)
    {
        if (IsReadOnlyForProfile(device.Code))
        {
            throw new ArgumentException(
                ReadOnlyForProfileMessage(device.Code),
                nameof(device));
        }
    }

    private string ReadOnlyForProfileMessage(SlmpDeviceCode code)
        => $"{code} is read-only for PLC profile '{SlmpPlcProfiles.ToCanonicalString(PlcProfile)}' and cannot be written.";

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
        if (data.Length != need) throw new SlmpError("read_bits payload size mismatch");
        var idx = 0;
        for (var i = 0; i < need && idx < points; i++)
        {
            var high = (data[i] >> 4) & 0x0F;
            if (high > 1) throw new SlmpError("read_bits payload contains a non-binary high nibble");
            result[idx++] = high == 1;
            if (idx < points)
            {
                var low = data[i] & 0x0F;
                if (low > 1) throw new SlmpError("read_bits payload contains a non-binary low nibble");
                result[idx++] = low == 1;
            }
        }
        return result;
    }

    internal byte[] EncodeExtendedDeviceSpec(SlmpDeviceAddress device, SlmpExtensionSpec extension)
        => SlmpPayloads.EncodeExtendedDeviceSpec(device, extension, CompatibilityMode);

    private byte[] EncodePassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (password.Any(character => character is < ' ' or > '~'))
            throw new ArgumentException("Password must contain printable ASCII characters only.", nameof(password));

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

    private static ushort[] DecodeWords(byte[] data, int points, string operationName)
    {
        if (data.Length != points * 2)
            throw new SlmpError($"{operationName} payload size mismatch");

        var values = new ushort[points];
        for (var index = 0; index < points; index++)
            values[index] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(index * 2, 2));
        return values;
    }

    private static (ushort[] WordValues, uint[] DwordValues) DecodeRandomReadResponse(
        byte[] data,
        int wordCount,
        int dwordCount,
        string operationName)
    {
        var expected = (wordCount * 2) + (dwordCount * 4);
        if (data.Length != expected)
        {
            throw new SlmpError(
                $"{operationName} response size mismatch expected={expected} actual={data.Length}");
        }

        var words = new ushort[wordCount];
        var dwords = new uint[dwordCount];
        var cursor = 0;
        for (var index = 0; index < words.Length; index++)
        {
            words[index] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor, 2));
            cursor += 2;
        }
        for (var index = 0; index < dwords.Length; index++)
        {
            dwords[index] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor, 4));
            cursor += 4;
        }
        return (words, dwords);
    }

    private static (ushort[] WordValues, ushort[] BitWordValues) DecodeBlockReadResponse(
        byte[] data,
        int wordCount,
        int bitWordCount)
    {
        var expected = (wordCount + bitWordCount) * 2;
        if (data.Length != expected)
        {
            throw new SlmpError(
                $"read_block response size mismatch expected={expected} actual={data.Length}");
        }

        var words = new ushort[wordCount];
        var bits = new ushort[bitWordCount];
        var cursor = 0;
        for (var index = 0; index < words.Length; index++)
        {
            words[index] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor, 2));
            cursor += 2;
        }
        for (var index = 0; index < bits.Length; index++)
        {
            bits[index] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor, 2));
            cursor += 2;
        }
        return (words, bits);
    }

    private static byte[] DecodeSelfTestResponse(byte[] response, byte[] expectedEcho)
    {
        if (response.Length < 2)
            throw new SlmpError("self_test response too short");

        var responseLength = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(0, 2));
        if (responseLength != expectedEcho.Length)
        {
            throw new SlmpError(
                $"self_test response declared length mismatch: expected={expectedEcho.Length}, declared={responseLength}");
        }
        if (response.Length != responseLength + 2)
        {
            throw new SlmpError(
                $"self_test response size mismatch: expected={responseLength + 2}, actual={response.Length}");
        }
        if (!response.AsSpan(2).SequenceEqual(expectedEcho))
            throw new SlmpError("self_test response payload mismatch");

        return response.AsSpan(2).ToArray();
    }
}
