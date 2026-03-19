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
}
