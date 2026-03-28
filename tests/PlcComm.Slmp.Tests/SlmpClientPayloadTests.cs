using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpClientPayloadTests
{
    [Fact]
    public void BuildExtendedRandomReadPayload_UsesExactAssembly()
    {
        using var client = new SlmpClient("127.0.0.1");
        var word = (
            new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.D, 100), null),
            new SlmpExtensionSpec(ExtensionSpecification: 0x0001));
        var dword = (
            new SlmpQualifiedDeviceAddress(new SlmpDeviceAddress(SlmpDeviceCode.D, 200), null),
            new SlmpExtensionSpec(ExtensionSpecification: 0x0002));

        var payload = client.BuildExtendedRandomReadPayload([word], [dword]);

        var expectedWord = client.EncodeExtendedDeviceSpec(word.Item1.Device, word.Item2);
        var expectedDword = client.EncodeExtendedDeviceSpec(dword.Item1.Device, dword.Item2);
        var expected = new byte[2 + expectedWord.Length + expectedDword.Length];
        expected[0] = 0x01;
        expected[1] = 0x01;
        expectedWord.CopyTo(expected, 2);
        expectedDword.CopyTo(expected, 2 + expectedWord.Length);

        Assert.Equal(expected, payload);
    }

    [Fact]
    public void BuildLabelArrayReadPayload_MatchesKnownEncoding()
    {
        var payload = SlmpClient.BuildLabelArrayReadPayload(
            [new SlmpLabelArrayReadPoint("LabelW", 1, 2)],
            ["Typ1"]);

        Assert.Equal(
            Convert.FromHexString("010001000400540079007000310006004C006100620065006C00570001000200"),
            payload);
    }

    [Fact]
    public void BuildLabelRandomWritePayload_MatchesKnownEncoding()
    {
        var payload = SlmpClient.BuildLabelRandomWritePayload(
            [new SlmpLabelRandomWritePoint("LabelW", [0x31, 0x00])],
            []);

        Assert.Equal(
            Convert.FromHexString("0100000006004C006100620065006C00570002003100"),
            payload);
    }
}
