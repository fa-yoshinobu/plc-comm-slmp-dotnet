namespace PlcComm.Slmp.Tests;

public sealed class SlmpEndCodesTests
{
    [Theory]
    [InlineData(0x1080, "slmp_end_code_1080", "The number of writes to the flash ROM has exceeded 100000.", "フラッシュROMへの書込み回数が10万回を超えた。")]
    [InlineData(0xC201, "slmp_end_code_c201", "The remote password status of the port used for communications is in the lock status.", "交信に使ったポートがリモートパスワードのロック状態である。")]
    [InlineData(0xC810, "slmp_end_code_c810", "Remote password authentication has failed when required. Set a correct password and retry.", "リモートパスワード認証が必要なアクセス時に，リモートパスワードのパスワード認証に失敗した。正しいパスワードを設定して再度実行してください。")]
    [InlineData(0xC811, "slmp_end_code_c811", "Remote password authentication has failed when required. Set a correct password and retry after 1 minute.", "リモートパスワード認証が必要なアクセス時に，リモートパスワードのパスワード認証に失敗した。1分後に正しいパスワードを設定して再度実行してください。")]
    [InlineData(0xC812, "slmp_end_code_c812", "Remote password authentication has failed when required. Set a correct password and retry after 5 minutes.", "リモートパスワード認証が必要なアクセス時に，リモートパスワードのパスワード認証に失敗した。5分後に正しいパスワードを設定して再度実行してください。")]
    [InlineData(0xC813, "slmp_end_code_c813", "Remote password authentication has failed when required. Set a correct password and retry after 15 minutes.", "リモートパスワード認証が必要なアクセス時に，リモートパスワードのパスワード認証に失敗した。15分後に正しいパスワードを設定して再度実行してください。")]
    [InlineData(0xC814, "slmp_end_code_c814", "Remote password authentication has failed when required. Set a correct password and retry after 60 minutes.", "リモートパスワード認証が必要なアクセス時に，リモートパスワードのパスワード認証に失敗した。60分後に正しいパスワードを設定して再度実行してください。")]
    [InlineData(0xC815, "slmp_end_code_c815", "Remote password authentication has failed when required. Set a correct password and retry after 60 minutes.", "リモートパスワード認証が必要なアクセス時に，リモートパスワードのパスワード認証に失敗した。60分後に正しいパスワードを設定して再度実行してください。")]
    [InlineData(0xCFBF, "slmp_end_code_cfbf", "The simple CPU communication cannot be executed.", "シンプルCPU通信を実行できない。")]
    [InlineData(0xD913, "slmp_end_code_d913", "An error was detected in the network module.", "ネットワークユニットの異常を検出した。")]
    [InlineData(0xE504, "slmp_end_code_e504", "Transient transmission (dedicated instruction, engineering tool connection) was executed while the own station did not perform baton pass.", "自局がバトンパス未実施中に，トランジェント伝送(専用命令，エンジニアリングツール接続)を実行した。")]
    public void GetNameAndMessage_KnownCodes_ReturnExpectedValues(int endCode, string expectedName, string expectedEnglish, string expectedJapanese)
    {
        var code = checked((ushort)endCode);

        Assert.Equal(expectedName, SlmpEndCodes.GetName(code));
        Assert.Equal(expectedEnglish, SlmpEndCodes.GetMessage(code));
        Assert.Equal(expectedJapanese, SlmpEndCodes.GetMessage(code, SlmpEndCodeLanguage.Japanese));
    }

    [Fact]
    public void GetNameAndMessage_UnknownCode_ReturnFallbacks()
    {
        Assert.Equal("unknown_plc_end_code", SlmpEndCodes.GetName(0xDEAD));
        Assert.Null(SlmpEndCodes.GetMessage(0xDEAD));
        Assert.Null(SlmpEndCodes.GetMessage(0xDEAD, SlmpEndCodeLanguage.Japanese));
    }

    [Fact]
    public void SlmpError_ExposesEndCodeHelpers()
    {
        var error = new SlmpError("raw", 0xC201, SlmpCommand.DeviceRead, 0x0000);

        Assert.Equal("slmp_end_code_c201", error.EndCodeName);
        Assert.Equal("The remote password status of the port used for communications is in the lock status.", error.EndCodeMessage);
        Assert.True(error.IsRemotePasswordError);
    }
}
