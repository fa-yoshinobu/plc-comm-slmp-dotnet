using System.Net;
using System.Net.Sockets;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpConnectionContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Constructor_RejectsInvalidPort(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SlmpClient(
                "127.0.0.1",
                SlmpPlcProfile.IqR,
                port,
                SlmpTransportMode.Tcp,
                SlmpTargetAddress.OwnStation));
    }

    [Fact]
    public void Constructor_RejectsUndefinedTransport()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SlmpClient(
                "127.0.0.1",
                SlmpPlcProfile.IqR,
                1025,
                (SlmpTransportMode)999,
                SlmpTargetAddress.OwnStation));
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("[::1]")]
    [InlineData("::ffff:127.0.0.1")]
    public void ConnectionInputs_RejectIPv6LiteralsBeforeTransport(string host)
    {
        Assert.Throws<ArgumentException>(() =>
            new SlmpClient(
                host,
                SlmpPlcProfile.IqR,
                1025,
                SlmpTransportMode.Tcp,
                SlmpTargetAddress.OwnStation));
        Assert.Throws<ArgumentException>(() =>
            new SlmpConnectionOptions(
                host,
                SlmpPlcProfile.IqR,
                1025,
                SlmpTransportMode.Udp,
                SlmpTargetAddress.OwnStation));
    }

    [Fact]
    public void AddressSelection_UsesFirstIPv4AndRejectsIPv6OnlyResults()
    {
        var first = IPAddress.Parse("192.0.2.10");
        var second = IPAddress.Parse("192.0.2.11");

        Assert.Equal(
            first,
            SlmpValidation.SelectFirstIpv4Address("plc.local", [IPAddress.IPv6Loopback, first, second]));
        Assert.Throws<SlmpError>(() =>
            SlmpValidation.SelectFirstIpv4Address("ipv6-only.local", [IPAddress.IPv6Loopback]));
    }

    [Fact]
    public async Task HostnameConnection_UsesIPv4ForTcpAndUdp()
    {
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync();
            using var client = new SlmpClient(
                "localhost",
                SlmpPlcProfile.IqR,
                port,
                SlmpTransportMode.Tcp,
                SlmpTargetAddress.OwnStation);

            await client.OpenAsync();
            using var accepted = await acceptTask;
            Assert.Equal(AddressFamily.InterNetwork, accepted.Client.RemoteEndPoint!.AddressFamily);
        }

        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var udpPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var serverTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            Assert.Equal(AddressFamily.InterNetwork, request.RemoteEndPoint.AddressFamily);
            byte[] response = [
                0xD4, 0x00, request.Buffer[2], request.Buffer[3], 0x00, 0x00,
                0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00];
            await server.SendAsync(response, request.RemoteEndPoint);
        });
        using var udpClient = new SlmpClient(
            "localhost",
            SlmpPlcProfile.IqR,
            udpPort,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation);

        await udpClient.ClearErrorAsync();
        await serverTask;
    }

    [Fact]
    public async Task ExplicitOpenTransportFailure_IsDedicatedTransportError()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);

        var error = await Assert.ThrowsAsync<SlmpTransportException>(() => client.OpenAsync());

        Assert.IsType<SocketException>(error.InnerException);
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Timeout_RejectsNonPositiveValues(double milliseconds)
    {
        using var client = CreateTcpClient();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            client.Timeout = TimeSpan.FromMilliseconds(milliseconds));
    }

    [Fact]
    public void Timeout_RejectsSubMillisecondAndAboveTimerMaximum()
    {
        using var client = CreateTcpClient();

        Assert.Throws<ArgumentOutOfRangeException>(() => client.Timeout = TimeSpan.FromTicks(1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            client.Timeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1));
        client.Timeout = TimeSpan.FromMilliseconds(1);
        client.Timeout = TimeSpan.FromMilliseconds(int.MaxValue);
    }

    [Fact]
    public void ConnectionOptions_UseTheSameTimeoutBoundariesAsClient()
    {
        SlmpConnectionOptions Create(TimeSpan timeout) => new(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation)
        {
            Timeout = timeout,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => Create(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(TimeSpan.FromTicks(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(TimeSpan.FromMilliseconds((double)int.MaxValue + 1)));
        Assert.Equal(TimeSpan.FromMilliseconds(1), Create(TimeSpan.FromMilliseconds(1)).Timeout);
        Assert.Equal(
            TimeSpan.FromMilliseconds(int.MaxValue),
            Create(TimeSpan.FromMilliseconds(int.MaxValue)).Timeout);
    }

    [Fact]
    public async Task Close_AllowsReopen_ButDisposePermanentlyRejectsUse()
    {
        using var sink = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)sink.Client.LocalEndPoint!).Port;
        var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation);

        await client.OpenAsync();
        client.Close();
        await client.OpenAsync();
        Assert.True(client.IsOpen);

        client.Dispose();
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.OpenAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR), 1));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.D, 0, SlmpPlcProfile.IqR), [1]));
        Assert.Equal(default, client.TrafficStats);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotentAndPermanentlyRejectsRequests()
    {
        var client = CreateTcpClient();

        await client.DisposeAsync();
        await client.DisposeAsync();
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ClearErrorAsync());
        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
    }

    [Fact]
    public async Task Dispose_DuringRequest_DoesNotDeadlock()
    {
        using var sink = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)sink.Client.LocalEndPoint!).Port;
        var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        var request = client.ClearErrorAsync();
        _ = await sink.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var waitingRequest = client.ClearErrorAsync();

        await Task.Run(client.Dispose).WaitAsync(TimeSpan.FromSeconds(2));
        var activeError = await Record.ExceptionAsync(async () =>
            await request.WaitAsync(TimeSpan.FromSeconds(2)));
        var outcomeUnknown = Assert.IsType<SlmpOperationOutcomeUnknownException>(activeError);
        Assert.Equal(SlmpOutcomeUnknownReason.Closed, outcomeUnknown.Reason);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await waitingRequest.WaitAsync(TimeSpan.FromSeconds(2)));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ClearErrorAsync());
    }

    [Fact]
    public void QueuedClient_PublicTypeIsRemoved()
    {
        Assert.Null(typeof(SlmpClient).Assembly.GetType("PlcComm.Slmp.QueuedSlmpClient"));
    }

    [Fact]
    public void TargetAddress_IsReadOnlyAfterConstruction()
    {
        Assert.False(typeof(SlmpClient).GetProperty(nameof(SlmpClient.TargetAddress))!.CanWrite);
    }

    [Fact]
    public void OrdinaryClient_ExposesFixedCommandSemanticApis()
    {
        Assert.NotNull(typeof(SlmpClient).GetMethod(nameof(SlmpClient.SelfTestLoopbackAsync)));
        Assert.NotNull(typeof(SlmpClient).GetMethod(nameof(SlmpClient.ClearErrorAsync)));
    }

    [Fact]
    public async Task UdpTimeout_ClosesSocketBeforeAnotherRequestCanReuseIt()
    {
        using var sink = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)sink.Client.LocalEndPoint!).Port;
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation)
        {
            Timeout = TimeSpan.FromMilliseconds(100),
        };

        await Assert.ThrowsAsync<SlmpTimeoutException>(() =>
            client.RawCommandAsync(
                SlmpCommand.ReadTypeName,
                0x0000,
                ReadOnlyMemory<byte>.Empty));

        Assert.Equal<ulong>(1, client.TrafficStats.RequestCount);
        Assert.Equal((ulong)client.LastRequestFrame.Length, client.TrafficStats.TxBytes);
        Assert.Equal<ulong>(0, client.TrafficStats.RxBytes);

        Assert.False(client.IsOpen);

        await Assert.ThrowsAsync<SlmpNotConnectedException>(() =>
            client.RawCommandAsync(
                SlmpCommand.ReadTypeName,
                0x0000,
                ReadOnlyMemory<byte>.Empty));

        await client.OpenAsync();
        Assert.True(client.IsOpen);
    }

    [Fact]
    public async Task PostSendStateChangingTimeout_IsOutcomeUnknownWithTimeoutCause()
    {
        using var sink = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)sink.Client.LocalEndPoint!).Port;
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation)
        {
            Timeout = TimeSpan.FromMilliseconds(100),
        };

        var error = await Assert.ThrowsAsync<SlmpOperationOutcomeUnknownException>(
            () => client.ClearErrorAsync());

        Assert.Equal(SlmpOutcomeUnknownReason.Timeout, error.Reason);
        Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
        Assert.Equal<ulong>(1, client.TrafficStats.RequestCount);
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task PostSendUnknownRawCommandTimeout_IsConservativelyOutcomeUnknown()
    {
        using var sink = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)sink.Client.LocalEndPoint!).Port;
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation)
        {
            Timeout = TimeSpan.FromMilliseconds(100),
        };

        var error = await Assert.ThrowsAsync<SlmpOperationOutcomeUnknownException>(() =>
            client.RawCommandAsync((SlmpCommand)0x7FFF, 0x0000, ReadOnlyMemory<byte>.Empty));

        Assert.Equal(SlmpOutcomeUnknownReason.Timeout, error.Reason);
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task PostSendStateChangingCancellation_IsOutcomeUnknownWithCancellationCause()
    {
        using var sink = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)sink.Client.LocalEndPoint!).Port;
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        using var cancellation = new CancellationTokenSource();

        var request = client.ClearErrorAsync(cancellation.Token);
        _ = await sink.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        var error = await Assert.ThrowsAsync<SlmpOperationOutcomeUnknownException>(() => request);
        Assert.Equal(SlmpOutcomeUnknownReason.Cancellation, error.Reason);
        Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task PostSendStateChangingTransportLoss_IsOutcomeUnknownWithTransportCause()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync();
            var request = new byte[256];
            _ = await accepted.GetStream().ReadAsync(request);
            accepted.Client.LingerState = new LingerOption(true, 0);
        });
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation)
        {
            Timeout = TimeSpan.FromSeconds(2),
        };

        var error = await Assert.ThrowsAsync<SlmpOperationOutcomeUnknownException>(
            () => client.ClearErrorAsync());

        Assert.Equal(SlmpOutcomeUnknownReason.Transport, error.Reason);
        Assert.NotNull(error.InnerException);
        Assert.False(client.IsOpen);
        await server;
        listener.Stop();
    }

    [Fact]
    public async Task PostSendReadTransportLoss_IsDedicatedTransportError()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync();
            var request = new byte[256];
            _ = await accepted.GetStream().ReadAsync(request);
            accepted.Client.LingerState = new LingerOption(true, 0);
        });
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation)
        {
            Timeout = TimeSpan.FromSeconds(2),
        };

        await Assert.ThrowsAsync<SlmpTransportException>(() =>
            client.RawCommandAsync(SlmpCommand.ReadTypeName, 0x0000, ReadOnlyMemory<byte>.Empty));

        Assert.False(client.IsOpen);
        await server;
        listener.Stop();
    }

    [Fact]
    public async Task RemoteReset_ClosesTransportAndRequiresExplicitOpen()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            port,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);
        var acceptTask = listener.AcceptTcpClientAsync();

        await client.OpenAsync();
        using var accepted = await acceptTask;
        await client.RemoteResetAsync();

        Assert.Equal<ulong>(1, client.TrafficStats.RequestCount);

        Assert.False(client.IsOpen);
        await Assert.ThrowsAsync<SlmpNotConnectedException>(() => client.ClearErrorAsync());
        listener.Stop();
    }

    [Fact]
    public async Task RemotePassword_RejectsNonAsciiBeforeTransport()
    {
        using var client = CreateTcpClient();

        await Assert.ThrowsAsync<ArgumentException>(() => client.RemotePasswordUnlockAsync("éééééé"));

        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task DeviceFromDifferentProfile_IsRejectedBeforeTransport()
    {
        using var client = CreateTcpClient();
        var iqfAddress = SlmpDeviceParser.Parse("D0", SlmpPlcProfile.IqF);

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadWordsRawAsync(iqfAddress, 1));

        Assert.Contains("does not match client PlcProfile", error.Message, StringComparison.Ordinal);
        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
    }

    [Fact]
    public async Task TrafficStats_CountCompleteResponseAndPersistAcrossClose()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync();
            var stream = accepted.GetStream();
            var request = new byte[256];
            var count = await stream.ReadAsync(request);
            var serial = request.AsSpan(2, 2).ToArray();
            byte[] response = [0xD4, 0x00, serial[0], serial[1], 0x00, 0x00,
                0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00];
            await stream.WriteAsync(response);
            return (count, response.Length);
        });
        await using var client = new SlmpClient(
            "127.0.0.1", SlmpPlcProfile.IqR, port, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        await client.RawCommandAsync(SlmpCommand.ClearError, 0, ReadOnlyMemory<byte>.Empty);
        var expected = await server;
        var stats = client.TrafficStats;
        Assert.Equal<ulong>(1, stats.RequestCount);
        Assert.Equal((ulong)expected.count, stats.TxBytes);
        Assert.Equal((ulong)expected.Length, stats.RxBytes);
        await client.CloseAsync();
        Assert.Equal(stats, client.TrafficStats);
        listener.Stop();
    }

    private static SlmpClient CreateTcpClient()
        => new(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);
}
