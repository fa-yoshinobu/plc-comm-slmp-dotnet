using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpClientPayloadTests
{
    private static SlmpQualifiedDeviceAddress Qualified(SlmpDeviceCode code, uint number)
        => new(new SlmpDeviceAddress(code, number), null);

    private static SlmpExtensionSpec Extension(
        ushort extensionSpecification,
        byte extensionSpecificationModification = 0x00,
        byte deviceModificationIndex = 0x00,
        byte deviceModificationFlags = 0x00,
        byte directMemorySpecification = 0x00)
        => new(
            extensionSpecification,
            extensionSpecificationModification,
            deviceModificationIndex,
            deviceModificationFlags,
            directMemorySpecification);

    [Fact]
    public void BuildExtendedRandomReadPayload_UsesExactAssembly()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);
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
    public void EncodeExtendedDeviceSpec_RegularDevice_UsesManualExtendedLayout()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QCpuQj71E71100);
        var device = new SlmpDeviceAddress(SlmpDeviceCode.D, 100);

        Assert.Equal(
            Convert.FromHexString("0000640000A80000000000"),
            client.EncodeExtendedDeviceSpec(device, new SlmpExtensionSpec()));
        Assert.Equal(
            Convert.FromHexString("0440640000A80000000000"),
            client.EncodeExtendedDeviceSpec(
                device,
                new SlmpExtensionSpec(DeviceModificationIndex: 0x04, DeviceModificationFlags: 0x40)));
    }

    [Fact]
    public void BuildExtendedRandomReadPayload_UsesManualLayoutForRegularAndQualifiedBufferMemory()
    {
        var payload = SlmpPayloads.BuildExtendedRandomReadPayload(
            [(Qualified(SlmpDeviceCode.D, 100), Extension(0x0102, 0x03, 0x04, 0x05, 0x06))],
            [(SlmpQualifiedDeviceParser.Parse("U01\\G10"), Extension(0x9999, 0x07, 0x08, 0x09))],
            SlmpCompatibilityMode.Iqr);

        Assert.Equal(
            Convert.FromHexString("0101040564000000A800030002010608090A000000AB0007000100F8"),
            payload);
    }

    [Fact]
    public void BuildExtendedRandomWordWritePayload_UsesManualLayout()
    {
        var payload = SlmpPayloads.BuildExtendedRandomWordWritePayload(
            [(Qualified(SlmpDeviceCode.D, 10), (ushort)0x1234, Extension(0x0001))],
            [(Qualified(SlmpDeviceCode.W, 0x20), 0x89ABCDEFu, Extension(0x0002))],
            SlmpCompatibilityMode.Iqr);

        Assert.Equal(
            Convert.FromHexString("010100000A000000A80000000100003412000020000000B4000000020000EFCDAB89"),
            payload);
    }

    [Fact]
    public void BuildExtendedRandomBitWritePayload_UsesCompatibilitySpecificValueWidth()
    {
        (SlmpQualifiedDeviceAddress Device, bool Value, SlmpExtensionSpec Extension)[] entries =
        [
            (Qualified(SlmpDeviceCode.M, 7), true, Extension(0x0003)),
            (Qualified(SlmpDeviceCode.M, 8), false, Extension(0x0004)),
        ];

        Assert.Equal(
            Convert.FromHexString("02000007000000900000000300000100000008000000900000000400000000"),
            SlmpPayloads.BuildExtendedRandomBitWritePayload(entries, SlmpCompatibilityMode.Iqr));
        Assert.Equal(
            Convert.FromHexString("02000007000090000003000001000008000090000004000000"),
            SlmpPayloads.BuildExtendedRandomBitWritePayload(entries, SlmpCompatibilityMode.Legacy));
    }

    [Fact]
    public void BuildExtendedMonitorRegisterPayload_MatchesCurrentEncodingForLinkDirect()
    {
        var payload = SlmpPayloads.BuildExtendedMonitorRegisterPayload(
            [(SlmpQualifiedDeviceParser.Parse("J2\\SW10"), Extension(0xFFFF))],
            [(Qualified(SlmpDeviceCode.D, 200), Extension(0x0005))],
            SlmpCompatibilityMode.Iqr);

        Assert.Equal(
            Convert.FromHexString("01010000100000B500000200F90000C8000000A8000000050000"),
            payload);
    }

    [Fact]
    public async Task SelfTestLoopbackAsync_RejectsManualInvalidPayloadsBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SelfTestLoopbackAsync(new byte[] { (byte)'H', (byte)'E', (byte)'L', (byte)'L', (byte)'O' }));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SelfTestLoopbackAsync(new byte[] { 0x00, 0xFF }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.SelfTestLoopbackAsync(Array.Empty<byte>()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.SelfTestLoopbackAsync(new byte[961]));
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
    public void BuildLabelArrayReadPayload_WithoutAbbreviations_MatchesKnownEncoding()
    {
        var payload = SlmpPayloads.BuildLabelArrayReadPayload(
            [
                new SlmpLabelArrayReadPoint("LabelA", 0, 1),
                new SlmpLabelArrayReadPoint("LabelB", 1, 4),
            ],
            []);

        Assert.Equal(
            Convert.FromHexString("0200000006004C006100620065006C0041000000010006004C006100620065006C00420001000400"),
            payload);
    }

    [Fact]
    public void BuildLabelArrayWritePayload_WithAbbreviationAndData_MatchesKnownEncoding()
    {
        var payload = SlmpPayloads.BuildLabelArrayWritePayload(
            [
                new SlmpLabelArrayWritePoint("LabelA", 1, 3, [0x11, 0x22, 0x33]),
                new SlmpLabelArrayWritePoint("LabelB", 0, 2, [0x44, 0x55]),
            ],
            ["TypA"]);

        Assert.Equal(
            Convert.FromHexString("020001000400540079007000410006004C006100620065006C0041000100030011223306004C006100620065006C004200000002004455"),
            payload);
    }

    [Fact]
    public void BuildLabelRandomReadPayload_WithAbbreviations_MatchesKnownEncoding()
    {
        var payload = SlmpPayloads.BuildLabelRandomReadPayload(
            ["LabelA", "LabelB"],
            ["TypA", "TypB"]);

        Assert.Equal(
            Convert.FromHexString("02000200040054007900700041000400540079007000420006004C006100620065006C00410006004C006100620065006C004200"),
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

    [Fact]
    public void BuildLabelRandomWritePayload_WithAbbreviationAndMultiplePoints_MatchesKnownEncoding()
    {
        var payload = SlmpPayloads.BuildLabelRandomWritePayload(
            [
                new SlmpLabelRandomWritePoint("LabelA", [0x01, 0x02]),
                new SlmpLabelRandomWritePoint("LabelB", [0x03, 0x04, 0x05]),
            ],
            ["TypA"]);

        Assert.Equal(
            Convert.FromHexString("020001000400540079007000410006004C006100620065006C0041000200010206004C006100620065006C0042000300030405"),
            payload);
    }

    [Fact]
    public void ParseArrayLabelReadResponse_MatchesCurrentDecoding()
    {
        var results = SlmpPayloads.ParseArrayLabelReadResponse(
            Convert.FromHexString("0200100002001122334420010300AABBCC"));

        Assert.Collection(
            results,
            first =>
            {
                Assert.Equal(0x10, first.DataTypeId);
                Assert.Equal(0x00, first.UnitSpecification);
                Assert.Equal(2, first.ArrayDataLength);
                Assert.Equal([0x11, 0x22, 0x33, 0x44], first.Data);
            },
            second =>
            {
                Assert.Equal(0x20, second.DataTypeId);
                Assert.Equal(0x01, second.UnitSpecification);
                Assert.Equal(3, second.ArrayDataLength);
                Assert.Equal([0xAA, 0xBB, 0xCC], second.Data);
            });
    }

    [Fact]
    public void ParseRandomLabelReadResponse_MatchesCurrentDecoding()
    {
        var results = SlmpPayloads.ParseRandomLabelReadResponse(
            Convert.FromHexString("020011000200AABB22010300CCDDEE"));

        Assert.Collection(
            results,
            first =>
            {
                Assert.Equal(0x11, first.DataTypeId);
                Assert.Equal(0x00, first.Spare);
                Assert.Equal(2, first.ReadDataLength);
                Assert.Equal([0xAA, 0xBB], first.Data);
            },
            second =>
            {
                Assert.Equal(0x22, second.DataTypeId);
                Assert.Equal(0x01, second.Spare);
                Assert.Equal(3, second.ReadDataLength);
                Assert.Equal([0xCC, 0xDD, 0xEE], second.Data);
            });
    }
}
