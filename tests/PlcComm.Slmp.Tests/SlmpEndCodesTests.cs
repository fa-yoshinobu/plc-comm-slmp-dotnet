namespace PlcComm.Slmp.Tests;

public sealed class SlmpEndCodesTests
{
    [Theory]
    [InlineData(0x1080, "slmp_end_code_1080")]
    [InlineData(0xC201, "slmp_end_code_c201")]
    [InlineData(0xC810, "slmp_end_code_c810")]
    [InlineData(0xCFBF, "slmp_end_code_cfbf")]
    [InlineData(0xD913, "slmp_end_code_d913")]
    [InlineData(0xE504, "slmp_end_code_e504")]
    [InlineData(0xDEAD, "slmp_end_code_dead")]
    public void GetName_ReturnsStableCodeDerivedKey(int endCode, string expectedName)
    {
        var code = checked((ushort)endCode);

        Assert.Equal(expectedName, SlmpEndCodes.GetName(code));
    }

    [Fact]
    public void SlmpError_ExposesEndCodeHelpers()
    {
        var error = new SlmpError("raw", 0xC201, SlmpCommand.DeviceRead, 0x0000);

        Assert.Equal("slmp_end_code_c201", error.EndCodeName);
        Assert.True(error.IsRemotePasswordError);
    }

    [Fact]
    public void SlmpErrorInfo_ParseDecodesManualErrorInformationBlock()
    {
        byte[] data = [0x00, 0xFF, 0xFF, 0x03, 0x00, 0x01, 0x04, 0x01, 0x00];

        var info = SlmpErrorInfo.Parse(data);

        Assert.NotNull(info);
        Assert.Equal((byte)0x00, info.Network);
        Assert.Equal((byte)0xFF, info.Station);
        Assert.Equal((ushort)0x03FF, info.ModuleIo);
        Assert.Equal((byte)0x00, info.Multidrop);
        Assert.Equal((ushort)0x0401, info.Command);
        Assert.Equal((ushort)0x0001, info.Subcommand);
        Assert.Equal(data, info.Raw);
    }
}
