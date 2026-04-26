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
    public async Task ReadWordsRawAsync_RejectsDirectLongCounterCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsRawAsync(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 0), 4));
        Assert.Contains("Direct word read is not supported for LCN", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTN)]
    [InlineData(SlmpDeviceCode.LCN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task ReadDWordsRawAsync_RejectsDirectLongCurrentAndLzReads(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadDWordsRawAsync(new SlmpDeviceAddress(code, 0), 1));
        Assert.Contains($"Direct DWord read is not supported for {code}", ex.Message);
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
    public async Task ReadWordsExtendedAsync_RejectsDirectLongCounterCurrentReads()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadWordsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10), null),
                4,
                new SlmpExtensionSpec()));
        Assert.Contains("Direct word read is not supported for LCN", ex.Message);
    }

    [Theory]
    [InlineData(SlmpDeviceCode.LTN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task WriteWordsExtendedAsync_RejectsLongCurrentAndLzDevices(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteWordsExtendedAsync(
                new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(code, 10), null),
                new ushort[] { 1 },
                new SlmpExtensionSpec()));
        Assert.Contains($"Direct word write is not supported for {code}", ex.Message);
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

    [Theory]
    [InlineData(SlmpDeviceCode.LCN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task ReadRandomAsync_RejectsLongCurrentAndLzWordEntries(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomAsync(
                new[] { new SlmpDeviceAddress(code, 10) },
                Array.Empty<SlmpDeviceAddress>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
    }

    [Fact]
    public async Task ReadRandomExtAsync_RejectsLongCurrentWordEntries()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomExtAsync(
                new[] { (new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10), null), new SlmpExtensionSpec()) },
                Array.Empty<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ as word entries", ex.Message);
    }

    [Fact]
    public async Task ReadRandomExtAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadRandomExtAsync(
                Array.Empty<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)>(),
                new[] { (new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10), null), new SlmpExtensionSpec()) }));
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
    public async Task ReadBlockAsync_RejectsLongCounterCurrentAndLzBlocks()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBlockAsync(
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.LCN, 10), 4) },
                Array.Empty<SlmpBlockRead>()));
        Assert.Contains("does not support LCN/LZ", ex.Message);

        ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ReadBlockAsync(
                new[] { new SlmpBlockRead(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 0), 2) },
                Array.Empty<SlmpBlockRead>()));
        Assert.Contains("does not support LCN/LZ", ex.Message);
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

    [Theory]
    [InlineData(SlmpDeviceCode.LCN)]
    [InlineData(SlmpDeviceCode.LZ)]
    public async Task WriteBlockAsync_RejectsLongCurrentAndLzBlocks(SlmpDeviceCode code)
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteBlockAsync(
                new[] { new SlmpBlockWrite(new SlmpDeviceAddress(code, 10), new ushort[] { 1 }) },
                Array.Empty<SlmpBlockWrite>()));
        Assert.Contains("does not support LTN/LSTN/LCN/LZ", ex.Message);
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
    public async Task WriteRandomWordsExtAsync_RejectsLongCurrentWordEntries()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.WriteRandomWordsExtAsync(
                new[] { (new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LZ, 1), null), (ushort)1, new SlmpExtensionSpec()) },
                Array.Empty<(SlmpQualifiedDeviceAddress Device, uint Value, SlmpExtensionSpec Extension)>()));
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

    [Fact]
    public async Task RegisterMonitorDevicesExtAsync_RejectsLongCounterContacts()
    {
        using var client = new SlmpClient("127.0.0.1");
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.RegisterMonitorDevicesExtAsync(
                new[] { (new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.LCS, 10), null), new SlmpExtensionSpec()) },
                Array.Empty<(SlmpQualifiedDeviceAddress Device, SlmpExtensionSpec Extension)>()));
        Assert.Contains("Entry Monitor Device (0x0801) does not support LCS/LCC", ex.Message);
    }
}
