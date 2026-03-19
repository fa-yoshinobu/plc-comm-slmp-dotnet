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
    public void RecommendProfile_RSeries_SuggestsIqr()
    {
        var info = new SlmpTypeNameInfo("R08CPU", 0x4801, true);
        var recommendation = SlmpProfileHeuristics.Recommend(info);
        Assert.Equal(SlmpFrameType.Frame4E, recommendation.FrameType);
        Assert.Equal(SlmpCompatibilityMode.Iqr, recommendation.CompatibilityMode);
        Assert.True(recommendation.Confident);
    }

    [Fact]
    public void RecommendProfile_QSeries_SuggestsLegacy()
    {
        var info = new SlmpTypeNameInfo("Q26UDEHCPU", 0x026C, true);
        var recommendation = SlmpProfileHeuristics.Recommend(info);
        Assert.Equal(SlmpFrameType.Frame3E, recommendation.FrameType);
        Assert.Equal(SlmpCompatibilityMode.Legacy, recommendation.CompatibilityMode);
        Assert.True(recommendation.Confident);
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
}
