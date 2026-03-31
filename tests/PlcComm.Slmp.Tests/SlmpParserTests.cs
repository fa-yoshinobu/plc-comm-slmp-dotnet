using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpParserTests
{
    [Fact]
    public void ParseDevice_DecimalWord_Succeeds()
    {
        var device = SlmpDeviceParser.Parse("D100");
        Assert.Equal(SlmpDeviceCode.D, device.Code);
        Assert.Equal((uint)100, device.Number);
    }

    [Fact]
    public void ParseDevice_HexBit_Succeeds()
    {
        var device = SlmpDeviceParser.Parse("X10");
        Assert.Equal(SlmpDeviceCode.X, device.Code);
        Assert.Equal((uint)0x10, device.Number);
    }

    [Fact]
    public void ParseDevice_StepRelay_Fails()
    {
        Assert.Throws<FormatException>(() => SlmpDeviceParser.Parse("S0"));
    }

    [Theory]
    [InlineData("d100", "D100")]
    [InlineData("x1f", "X1F")]
    [InlineData("sw10", "SW10")]
    [InlineData("zr123", "ZR123")]
    public void SlmpAddress_Normalize_ReturnsCanonicalText(string input, string expected)
    {
        Assert.Equal(expected, SlmpAddress.Normalize(input));
    }

    [Fact]
    public void ParseNamedTarget_SelfCpu_Succeeds()
    {
        var target = SlmpTargetParser.ParseNamed("SELF-CPU2");
        Assert.Equal("SELF-CPU2", target.Name);
        Assert.Equal((ushort)0x03E1, target.Target.ModuleIo);
        Assert.Equal((byte)0x00, target.Target.Network);
        Assert.Equal((byte)0xFF, target.Target.Station);
    }

    [Fact]
    public void ParseQualifiedDevice_UsesExtensionSpec()
    {
        var qualified = SlmpQualifiedDeviceParser.Parse(@"U3E0\G10");
        Assert.Equal((ushort)0x03E0, qualified.ExtensionSpecification);
        Assert.Equal(SlmpDeviceCode.G, qualified.Device.Code);
        Assert.Equal((uint)10, qualified.Device.Number);
    }

    [Fact]
    public void QueuedClient_ConstructsWithInnerClient()
    {
        using var inner = new SlmpClient("127.0.0.1");
        using var queued = new QueuedSlmpClient(inner);
        Assert.Same(inner, queued.InnerClient);
    }

    [Fact]
    public void QueuedClient_ExposesConfigurationProperties()
    {
        using var inner = new SlmpClient("127.0.0.1");
        using var queued = new QueuedSlmpClient(inner)
        {
            FrameType = SlmpFrameType.Frame3E,
            CompatibilityMode = SlmpCompatibilityMode.Legacy,
            TargetAddress = new SlmpTargetAddress(0x01, 0x02, 0x03E0, 0x00),
            MonitoringTimer = 0x0020,
            Timeout = TimeSpan.FromSeconds(5),
        };

        Assert.Equal(SlmpFrameType.Frame3E, inner.FrameType);
        Assert.Equal(SlmpCompatibilityMode.Legacy, inner.CompatibilityMode);
        Assert.Equal((byte)0x01, inner.TargetAddress.Network);
        Assert.Equal((ushort)0x0020, inner.MonitoringTimer);
        Assert.Equal(TimeSpan.FromSeconds(5), inner.Timeout);
    }

    [Fact]
    public void ParseQualifiedDevice_LinkDirect_J2SW10_Succeeds()
    {
        var q = SlmpQualifiedDeviceParser.Parse(@"J2\SW10");
        Assert.Equal(SlmpDeviceCode.SW, q.Device.Code);
        Assert.Equal((uint)0x10, q.Device.Number);  // SW is hex-addressed
        Assert.Equal((ushort)2, q.ExtensionSpecification);
        Assert.Equal((byte)0xF9, q.DirectMemorySpecification);
    }

    [Fact]
    public void EncodeExtendedDeviceSpec_LinkDirect_J2SW10_MatchesPcap()
    {
        // Verified by GOT pcap: J2\SW10 -> 00 00 10 00 00 b5 00 00 02 00 f9
        var client = new SlmpClient("127.0.0.1") { CompatibilityMode = SlmpCompatibilityMode.Legacy };
        var device = new SlmpDeviceAddress(SlmpDeviceCode.SW, 0x10);
        var extension = new SlmpExtensionSpec(ExtensionSpecification: 2, DirectMemorySpecification: 0xF9);
        var spec = client.EncodeExtendedDeviceSpec(device, extension);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x10, 0x00, 0x00, 0xB5, 0x00, 0x00, 0x02, 0x00, 0xF9 }, spec);
    }
}
