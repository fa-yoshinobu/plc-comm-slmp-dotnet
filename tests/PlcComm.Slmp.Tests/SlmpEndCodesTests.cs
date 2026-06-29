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
    public void GetMessage_ReturnsNullBecauseMessagesAreNotEmbedded()
    {
        Assert.Null(SlmpEndCodes.GetMessage(0xC201));
        Assert.Null(SlmpEndCodes.GetMessage(0xC201, SlmpEndCodeLanguage.Japanese));
        Assert.Null(SlmpEndCodes.GetMessage(0xDEAD));
    }

    [Fact]
    public void SlmpError_ExposesEndCodeHelpers()
    {
        var error = new SlmpError("raw", 0xC201, SlmpCommand.DeviceRead, 0x0000);

        Assert.Equal("slmp_end_code_c201", error.EndCodeName);
        Assert.Null(error.EndCodeMessage);
        Assert.True(error.IsRemotePasswordError);
    }
}
