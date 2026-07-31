using System.Buffers.Binary;
using System.Globalization;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpClientPayloadTests
{
    private static SlmpQualifiedDeviceAddress Qualified(SlmpDeviceCode code, uint number)
        => new(new SlmpDeviceAddress(code, number, SlmpPlcProfile.IqR), null);

    private static SlmpQualifiedDeviceAddress Extended(
        SlmpDeviceCode code,
        uint number,
        ushort extensionSpecification,
        byte? directMemorySpecification = null,
        SlmpDeviceModification? modification = null)
        => new(
            new SlmpDeviceAddress(code, number, SlmpPlcProfile.IqR),
            extensionSpecification,
            directMemorySpecification,
            modification);

    [Fact]
    public void BuildExtendedRandomReadPayload_UsesExactAssembly()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var word = Extended(SlmpDeviceCode.D, 100, 0x0001);
        var dword = Extended(SlmpDeviceCode.D, 200, 0x0002);

        var payload = client.BuildExtendedRandomReadPayload([word], [dword]);

        var expectedWord = client.EncodeExtendedDeviceSpec(word.Device, SlmpPayloads.ResolveEffectiveExtension(word, SlmpPlcProfile.IqR));
        var expectedDword = client.EncodeExtendedDeviceSpec(dword.Device, SlmpPayloads.ResolveEffectiveExtension(dword, SlmpPlcProfile.IqR));
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
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QCpuQj71E71100, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var device = new SlmpDeviceAddress(SlmpDeviceCode.D, 100, SlmpPlcProfile.IqR);

        Assert.Equal(
            Convert.FromHexString("0000640000A80000000000"),
            client.EncodeExtendedDeviceSpec(device, new SlmpExtensionSpec(0, 0, 0, 0, 0)));
        Assert.Equal(
            Convert.FromHexString("0440640000A80000000000"),
            client.EncodeExtendedDeviceSpec(
                device,
                new SlmpExtensionSpec(0, 0, 0x04, 0x40, 0)));
    }

    [Fact]
    public void LegacyDeviceSpec_RejectsNumberOutside24BitField()
    {
        var output = new byte[4];
        var device = new SlmpRawDeviceAddress(SlmpDeviceCode.D, 0x0100_0000);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlmpPayloads.EncodeRawDeviceSpec(device, output, SlmpCompatibilityMode.Legacy));
    }

    [Theory]
    [InlineData(SlmpCompatibilityMode.Legacy, 4)]
    [InlineData(SlmpCompatibilityMode.Iqr, 6)]
    public void DeviceSpec_AllowsRAboveProfileCatalogBound(
        SlmpCompatibilityMode compatibilityMode,
        int size)
    {
        var output = new byte[size];

        SlmpPayloads.EncodeRawDeviceSpec(
            new SlmpRawDeviceAddress(SlmpDeviceCode.R, 32768),
            output,
            compatibilityMode);

        Assert.Equal(32768u, compatibilityMode == SlmpCompatibilityMode.Legacy
            ? (uint)(output[0] | (output[1] << 8) | (output[2] << 16))
            : BinaryPrimitives.ReadUInt32LittleEndian(output));
    }

    [Fact]
    public void LzModification_RejectsIndexesAboveOne()
    {
        Assert.Equal((byte)1, new SlmpDeviceModification.IndexLz(1).Index);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlmpDeviceModification.IndexLz(2));
    }

    [Fact]
    public void BuildExtendedRandomReadPayload_UsesManualLayoutForRegularAndQualifiedBufferMemory()
    {
        var payload = SlmpPayloads.BuildExtendedRandomReadPayload(
            [Extended(SlmpDeviceCode.D, 100, 0x0102, 0x06, new SlmpDeviceModification.IndexZ(0x04))],
            [SlmpQualifiedDeviceParser.Parse("U01\\G10", SlmpPlcProfile.IqR)],
            SlmpCompatibilityMode.Iqr,
            SlmpPlcProfile.IqR);

        Assert.Equal(
            Convert.FromHexString("0101044064000000A800000002010600000A000000AB0000000100F8"),
            payload);
    }

    [Fact]
    public void BuildExtendedRandomWordWritePayload_UsesManualLayout()
    {
        var payload = SlmpPayloads.BuildExtendedRandomWordWritePayload(
            [(Extended(SlmpDeviceCode.D, 10, 0x0001), (ushort)0x1234)],
            [(Extended(SlmpDeviceCode.W, 0x20, 0x0002), 0x89ABCDEFu)],
            SlmpCompatibilityMode.Iqr,
            SlmpPlcProfile.IqR);

        Assert.Equal(
            Convert.FromHexString("010100000A000000A80000000100003412000020000000B4000000020000EFCDAB89"),
            payload);
    }

    [Fact]
    public void BuildExtendedRandomBitWritePayload_UsesCompatibilitySpecificValueWidth()
    {
        (SlmpQualifiedDeviceAddress Device, bool Value)[] entries =
        [
            (Extended(SlmpDeviceCode.M, 7, 0x0003), true),
            (Extended(SlmpDeviceCode.M, 8, 0x0004), false),
        ];

        Assert.Equal(
            Convert.FromHexString("02000007000000900000000300000100000008000000900000000400000000"),
            SlmpPayloads.BuildExtendedRandomBitWritePayload(entries, SlmpCompatibilityMode.Iqr, SlmpPlcProfile.IqR));
        Assert.Equal(
            Convert.FromHexString("02000007000090000003000001000008000090000004000000"),
            SlmpPayloads.BuildExtendedRandomBitWritePayload(entries, SlmpCompatibilityMode.Legacy, SlmpPlcProfile.IqR));
    }

    [Fact]
    public void BuildExtendedRandomBitWritePayload_UsesQlValueWidthForLinkDirect()
    {
        var entry = SlmpQualifiedDeviceParser.Parse(@"J2\B10", SlmpPlcProfile.IqR);

        Assert.Equal(
            Convert.FromHexString("010000100000A000000200F901"),
            SlmpPayloads.BuildExtendedRandomBitWritePayload(
                [(entry, true)],
                SlmpCompatibilityMode.Iqr,
                SlmpPlcProfile.IqR));
    }

    [Fact]
    public void BuildExtendedMonitorRegisterPayload_MatchesCurrentEncodingForLinkDirect()
    {
        var payload = SlmpPayloads.BuildExtendedMonitorRegisterPayload(
            [SlmpQualifiedDeviceParser.Parse("J2\\SW10", SlmpPlcProfile.IqR)],
            [Extended(SlmpDeviceCode.D, 200, 0x0005)],
            SlmpCompatibilityMode.Iqr,
            SlmpPlcProfile.IqR);

        Assert.Equal(
            Convert.FromHexString("01010000100000B500000200F90000C8000000A8000000050000"),
            payload);
    }

    [Fact]
    public async Task SelfTestLoopbackAsync_RejectsManualInvalidPayloadsBeforeTransport()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SelfTestLoopbackAsync(new byte[] { (byte)'H', (byte)'E', (byte)'L', (byte)'L', (byte)'O' }));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SelfTestLoopbackAsync(new byte[] { 0x00, 0xFF }));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SelfTestLoopbackAsync("ab12"u8.ToArray()));
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
                new SlmpLabelArrayWritePoint("LabelA", 1, 3, [0x11, 0x22, 0x33, 0x00]),
                new SlmpLabelArrayWritePoint("LabelB", 0, 2, [0x44, 0x55]),
            ],
            ["TypA"]);

        Assert.Equal(
            Convert.FromHexString("020001000400540079007000410006004C006100620065006C004100010003001122330006004C006100620065006C004200000002004455"),
            payload);
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(0, 6, 2)]
    [InlineData(0, 16, 2)]
    [InlineData(0, 17, 4)]
    [InlineData(0, 32, 4)]
    [InlineData(1, 1, 2)]
    [InlineData(1, 2, 2)]
    [InlineData(1, 3, 4)]
    [InlineData(1, 4, 4)]
    public void GetArrayWireByteCount_UsesTwoByteWireUnits(byte unit, ushort logicalLength, int expected)
        => Assert.Equal(expected, SlmpPayloads.GetArrayWireByteCount(unit, logicalLength, "value"));

    [Fact]
    public void BuildLabelArrayPayloads_RejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlmpPayloads.BuildLabelArrayReadPayload([new("Label", 2, 1)], []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlmpPayloads.BuildLabelArrayReadPayload([new("Label", 0, 0)], []));
        Assert.Throws<ArgumentException>(() =>
            SlmpPayloads.BuildLabelArrayReadPayload([null!], []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlmpPayloads.BuildLabelArrayWritePayload([new("Label", 2, 1, [0x00, 0x00])], []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlmpPayloads.BuildLabelArrayWritePayload([new("Label", 1, 0, [])], []));
        Assert.Throws<ArgumentException>(() =>
            SlmpPayloads.BuildLabelArrayWritePayload([new("Label", 0, 17, [0x00, 0x00])], []));
        Assert.Throws<ArgumentException>(() =>
            SlmpPayloads.BuildLabelArrayWritePayload([new("Label", 1, 3, [0x00, 0x00, 0x00])], []));
        Assert.Throws<ArgumentException>(() =>
            SlmpPayloads.BuildLabelArrayWritePayload([new("Label", 1, 3, [0x00, 0x00, 0x00, 0x00, 0x00])], []));
        Assert.Throws<ArgumentException>(() =>
            SlmpPayloads.BuildLabelArrayWritePayload([null!], []));
        Assert.Throws<ArgumentException>(() =>
            SlmpPayloads.BuildLabelArrayWritePayload([new("Label", 1, 2, null!)], []));
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
                new SlmpLabelRandomWritePoint("LabelB", [0x03, 0x04, 0x05, 0x00]),
            ],
            ["TypA"]);

        Assert.Equal(
            Convert.FromHexString("020001000400540079007000410006004C006100620065006C0041000200010206004C006100620065006C004200040003040500"),
            payload);
    }

    [Fact]
    public void BuildLabelRandomWritePayload_RejectsNullEmptyAndOddData()
    {
        Assert.Throws<ArgumentException>(() => SlmpPayloads.BuildLabelRandomWritePayload([null!], []));
        Assert.Throws<ArgumentException>(() =>
            SlmpPayloads.BuildLabelRandomWritePayload([new("Label", null!)], []));
        Assert.Throws<ArgumentException>(() =>
            SlmpPayloads.BuildLabelRandomWritePayload([new("Label", [])], []));
        Assert.Throws<ArgumentException>(() =>
            SlmpPayloads.BuildLabelRandomWritePayload([new("Label", [0x01])], []));
        Assert.Throws<ArgumentException>(() =>
            SlmpPayloads.BuildLabelRandomWritePayload([new("Label", [0x01, 0x02, 0x03])], []));
    }

    [Fact]
    public void LabelPayloadBuilders_EnforceTheAggregateRequestBoundaryBeforeAllocation()
    {
        static string Label(int characters) => new('L', characters);

        Assert.Equal(
            SlmpValidation.MaxRequestPayloadLength - 1,
            SlmpPayloads.BuildLabelArrayReadPayload([new(Label(32759), 1, 2)], []).Length);
        Assert.Equal(
            SlmpValidation.MaxRequestPayloadLength - 1,
            SlmpPayloads.BuildLabelArrayWritePayload([new(Label(32758), 1, 2, [0x00, 0x00])], []).Length);
        Assert.Equal(
            SlmpValidation.MaxRequestPayloadLength - 1,
            SlmpPayloads.BuildLabelRandomReadPayload([Label(32761)], []).Length);
        Assert.Equal(
            SlmpValidation.MaxRequestPayloadLength - 1,
            SlmpPayloads.BuildLabelRandomWritePayload([new(Label(32759), [0x00, 0x00])], []).Length);

        AssertPayloadTooLong(
            () => SlmpPayloads.BuildLabelArrayReadPayload([new(Label(32760), 1, 2)], []),
            "points",
            65530);
        AssertPayloadTooLong(
            () => SlmpPayloads.BuildLabelArrayWritePayload([new(Label(32759), 1, 2, [0x00, 0x00])], []),
            "points",
            65530);
        AssertPayloadTooLong(
            () => SlmpPayloads.BuildLabelRandomReadPayload([Label(32762)], []),
            "labels",
            65530);
        AssertPayloadTooLong(
            () => SlmpPayloads.BuildLabelRandomWritePayload([new(Label(32760), [0x00, 0x00])], []),
            "points",
            65530);
    }

    [Fact]
    public void LabelPayloadBuilders_RejectAggregateAbbreviationMultiplePointAndWriteDataOverflow()
    {
        static string Label(int characters) => new('L', characters);

        AssertPayloadTooLong(
            () => SlmpPayloads.BuildLabelRandomReadPayload(["L"], [Label(32762)]),
            "abbreviationLabels",
            65530);
        AssertPayloadTooLong(
            () => SlmpPayloads.BuildLabelArrayReadPayload(
                [new(Label(16379), 1, 2), new(Label(16379), 1, 2)],
                []),
            "points",
            65532);
        AssertPayloadTooLong(
            () => SlmpPayloads.BuildLabelArrayWritePayload(
                [new("A", 1, ushort.MaxValue, new byte[65536])],
                []),
            "points",
            65548);

        foreach (var itemLength in new[] { 65536, 65537 })
        {
            var itemLengthError = Assert.Throws<ArgumentOutOfRangeException>(() =>
                SlmpPayloads.BuildLabelRandomWritePayload(
                    [new("A", new byte[itemLength])],
                    []));
            Assert.Equal("points", itemLengthError.ParamName);
            Assert.Equal(itemLength, Assert.IsType<int>(itemLengthError.ActualValue));
            Assert.Contains(
                itemLength.ToString(CultureInfo.InvariantCulture),
                itemLengthError.Message,
                StringComparison.Ordinal);
            Assert.Contains("65535", itemLengthError.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ParseArrayLabelReadResponse_UsesLogicalUnitWireLengths()
    {
        var results = SlmpPayloads.ParseArrayLabelReadResponse(
            Convert.FromHexString("020010000200112220010300AABBCCDD"),
            [new("LabelBits", 0, 2), new("LabelBytes", 1, 3)]);

        Assert.Collection(
            results,
            first =>
            {
                Assert.Equal(0x10, first.DataTypeId);
                Assert.Equal(0x00, first.UnitSpecification);
                Assert.Equal(2, first.ArrayDataLength);
                Assert.Equal([0x11, 0x22], first.Data);
            },
            second =>
            {
                Assert.Equal(0x20, second.DataTypeId);
                Assert.Equal(0x01, second.UnitSpecification);
                Assert.Equal(3, second.ArrayDataLength);
                Assert.Equal([0xAA, 0xBB, 0xCC, 0xDD], second.Data);
            });
    }

    [Fact]
    public void ParseArrayLabelReadResponse_AcceptsOfficialSixBitShape()
    {
        var result = Assert.Single(SlmpPayloads.ParseArrayLabelReadResponse(
            Convert.FromHexString("0100010006000000"),
            [new("LabelBits", 0, 6)]));

        Assert.Equal(2, result.Data.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("01")]
    [InlineData("0100010006")]
    [InlineData("01000100060000")]
    [InlineData("010001000600000000")]
    [InlineData("0100010206000000")]
    [InlineData("010001000000")]
    public void ParseArrayLabelReadResponse_RejectsMalformedPayloads(string payloadHex)
        => Assert.Throws<SlmpError>(() => SlmpPayloads.ParseArrayLabelReadResponse(
            Convert.FromHexString(payloadHex),
            [new("LabelBits", 0, 6)]));

    [Fact]
    public void ParseArrayLabelReadResponse_RejectsCountAndEchoMismatch()
    {
        Assert.Throws<SlmpError>(() => SlmpPayloads.ParseArrayLabelReadResponse(
            Convert.FromHexString("0000"),
            [new("LabelBits", 0, 6)]));
        Assert.Throws<SlmpError>(() => SlmpPayloads.ParseArrayLabelReadResponse(
            Convert.FromHexString("010001010600000000000000"),
            [new("LabelBits", 0, 6)]));
        Assert.Throws<SlmpError>(() => SlmpPayloads.ParseArrayLabelReadResponse(
            Convert.FromHexString("0100010007000000"),
            [new("LabelBits", 0, 6)]));
    }

    [Fact]
    public void ParseRandomLabelReadResponse_PreservesUnknownTypeAndSpare()
    {
        var results = SlmpPayloads.ParseRandomLabelReadResponse(
            Convert.FromHexString("020011000200AABBFEFF0200CCDD"),
            2);

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
                Assert.Equal(0xFE, second.DataTypeId);
                Assert.Equal(0xFF, second.Spare);
                Assert.Equal(2, second.ReadDataLength);
                Assert.Equal([0xCC, 0xDD], second.Data);
            });
    }

    [Theory]
    [InlineData("")]
    [InlineData("01")]
    [InlineData("0100110002")]
    [InlineData("010011000200AA")]
    [InlineData("010011000200AABB00")]
    [InlineData("010011000300AABBCC")]
    [InlineData("010011000000")]
    public void ParseRandomLabelReadResponse_RejectsMalformedPayloads(string payloadHex)
        => Assert.Throws<SlmpError>(() => SlmpPayloads.ParseRandomLabelReadResponse(
            Convert.FromHexString(payloadHex),
            1));

    [Fact]
    public void ParseRandomLabelReadResponse_RejectsCountMismatch()
        => Assert.Throws<SlmpError>(() => SlmpPayloads.ParseRandomLabelReadResponse(
            Convert.FromHexString("0000"),
            1));

    private static void AssertPayloadTooLong(Action action, string parameterName, long actualLength)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal(parameterName, error.ParamName);
        Assert.Equal(actualLength, Assert.IsType<long>(error.ActualValue));
        Assert.Contains(actualLength.ToString(CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        Assert.Contains(
            SlmpValidation.MaxRequestPayloadLength.ToString(CultureInfo.InvariantCulture),
            error.Message,
            StringComparison.Ordinal);
    }
}
