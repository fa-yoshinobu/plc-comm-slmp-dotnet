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
    public void TargetAddress_IsReadOnlyAfterConstruction()
    {
        Assert.False(typeof(SlmpClient).GetProperty(nameof(SlmpClient.TargetAddress))!.CanWrite);
        Assert.False(typeof(QueuedSlmpClient).GetProperty(nameof(QueuedSlmpClient.TargetAddress))!.CanWrite);
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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.RawCommandAsync(
                SlmpCommand.ReadTypeName,
                0x0000,
                ReadOnlyMemory<byte>.Empty));

        Assert.False(client.IsOpen);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.RawCommandAsync(
                SlmpCommand.ReadTypeName,
                0x0000,
                ReadOnlyMemory<byte>.Empty));

        await client.OpenAsync();
        Assert.True(client.IsOpen);
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

        Assert.False(client.IsOpen);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ClearErrorAsync());
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
    }

    private static SlmpClient CreateTcpClient()
        => new(
            "127.0.0.1",
            SlmpPlcProfile.IqR,
            1025,
            SlmpTransportMode.Tcp,
            SlmpTargetAddress.OwnStation);
}
