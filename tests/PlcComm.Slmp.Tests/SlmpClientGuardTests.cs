using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpClientGuardTests
{
    private static readonly bool[] SingleTrue = [true];

    [Fact]
    public async Task ReadWordsRawAsync_RejectsNonBlockLongTimerCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 0), 2));
        Assert.Contains("requires 4-word blocks", ex.Message);
    }

    [Fact]
    public async Task ReadWordsRawAsync_RejectsNonBlockLongCounterCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 0), 2));
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
    public async Task ReadWordsRawAsync_RejectsDirectLzWordReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0), 1));
        Assert.Contains("Direct word read is not supported for LZ", ex.Message);
    }

    [Fact]
    public async Task WriteWordsAsync_RejectsLongCurrentValueDevices()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTN, 0), new ushort[] { 1 }));
        Assert.Contains("Direct word write is not supported for LTN", ex.Message);
    }

    [Fact]
    public async Task WriteDWordsAsync_RejectsDirectLongCurrentWrites()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteDWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 0), new uint[] { 1 }));
        Assert.Contains("Direct DWord write is not supported for LCN", ex.Message);
    }

    [Fact]
    public async Task WriteWordsAsync_RejectsLzWordWrites()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0), new ushort[] { 1 }));
        Assert.Contains("Direct word write is not supported for LZ", ex.Message);
    }

    [Fact]
    public async Task ReadBitsAsync_RejectsDirectLongTimerStateReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBitsAsync(new SlmpDeviceAddress(SlmpDeviceCode.LTS, 10), 1));
        Assert.Contains("Direct bit read is not supported for LTS", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTC)]
    [InlineData(SlmpDeviceCode.LCC)]
    public async Task WriteBitsAsync_RejectsDirectLongFamilyStateWrites(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBitsAsync(new SlmpDeviceAddress(code, 10), SingleTrue));
        Assert.Contains($"Direct bit write is not supported for {code}", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LSTS)]
    [InlineData(SlmpDeviceCode.LCS)]
    public async Task WriteBitsExtendedAsync_RejectsDirectLongFamilyStateWrites(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBitsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(code, 10), null),
                SingleTrue,
                new SlmpExtensionSpec()));
        Assert.Contains($"Direct bit write is not supported for {code}", ex.Message);
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
    public async Task ReadRandomAsync_RejectsLongTimerStateDevices()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(SlmpDeviceCode.LTC, 10) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("Read Random (0x0403) does not support LTS/LTC/LSTS/LSTC", ex.Message);
    }

    [Fact]
    public async Task ReadRandomAsync_RejectsLzWordEntries()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(SlmpDeviceCode.LZ, 10) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("does not support LZ as a word entry", ex.Message);
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
    public async Task WriteBlockAsync_RejectsLongCurrentWordBlocks()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBlockAsync(
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10), new ushort[] { 1 }) },
                Array.Empty<SlmpBlockWrite>()));
        Assert.Contains("does not support LTN/LSTN/LCN as word blocks", ex.Message);
    }

    [Fact]
    public async Task WriteRandomWordsAsync_RejectsLongCurrentWordEntries()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomWordsAsync(
                new[] { (new SlmpDeviceAddress(SlmpDeviceCode.LTN, 10), (ushort)1) },
                Array.Empty<(SlmpDeviceAddress Device, uint Value)>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
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
