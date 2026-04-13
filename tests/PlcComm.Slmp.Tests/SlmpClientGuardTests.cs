using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpClientGuardTests
{
    [Fact]
    public async Task ReadBitsAsync_RejectsLongTimerStateDevices()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTC, 0), 1));
        Assert.Contains("Direct bit read is not supported for LTC", ex.Message);
    }

    [Fact]
    public async Task ReadWordsRawAsync_RejectsNonBlockLongTimerCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 0), 2));
        Assert.Contains("requires 4-word blocks", ex.Message);
    }

    [Fact]
    public async Task ReadDWordsRawAsync_RejectsDirectLongTimerCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadDWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 0), 1));
        Assert.Contains("requires 4-word blocks", ex.Message);
    }

    [Fact]
    public async Task ReadRandomAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("Read Random (0x0403) does not support LCS/LCC", ex.Message);
    }

    [Fact]
    public async Task ReadBlockAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBlockAsync(
                Array.Empty<SlmpBlockRead>(),
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10), 1) }));
        Assert.Contains("Read Block (0x0406) does not support LCS/LCC", ex.Message);
    }

    [Fact]
    public async Task WriteBlockAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBlockAsync(
                Array.Empty<SlmpBlockWrite>(),
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.LCC, 10), new ushort[] { 1 }) }));
        Assert.Contains("Write Block (0x1406) does not support LCS/LCC", ex.Message);
    }

    [Fact]
    public async Task RegisterMonitorDevicesAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.RegisterMonitorDevicesAsync(
                new[] { new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("Entry Monitor Device (0x0801) does not support LCS/LCC", ex.Message);
    }
}
