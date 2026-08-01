using PlcComm.Slmp;
using System.Text.Json;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpParserTests
{
    [Fact]
    public void ParseDevice_DecimalWord_Succeeds()
    {
        var device = SlmpDeviceParser.Parse("D100", SlmpPlcProfile.IqR);
        Assert.Equal(SlmpDeviceCode.D, device.Code);
        Assert.Equal((uint)100, device.Number);
    }

    [Fact]
    public void ParseDevice_HexBit_Succeeds()
    {
        var device = SlmpDeviceParser.Parse("X10", SlmpPlcProfile.IqR);
        Assert.Equal(SlmpDeviceCode.X, device.Code);
        Assert.Equal((uint)0x10, device.Number);
    }

    [Fact]
    public void ParseDevice_HexNumberCanBeAllLetters()
    {
        var device = SlmpDeviceParser.Parse("XFF", SlmpPlcProfile.IqR);
        Assert.Equal(SlmpDeviceCode.X, device.Code);
        Assert.Equal((uint)0xFF, device.Number);
    }

    [Fact]
    public void ParseDevice_KnownCodeWithInvalidNumberDoesNotFallback()
    {
        var error = Assert.Throws<FormatException>(() => SlmpDeviceParser.Parse("DFFFF", SlmpPlcProfile.IqR));
        Assert.Contains("device code 'D'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseDevice_IqFXY_UsesOctal()
    {
        var device = SlmpDeviceParser.Parse("X100", SlmpPlcProfile.IqF);
        Assert.Equal(SlmpDeviceCode.X, device.Code);
        Assert.Equal((uint)0x40, device.Number);
        Assert.Equal("Y217", SlmpAddress.Format(new SlmpDeviceAddress(SlmpDeviceCode.Y, 0x8F, SlmpPlcProfile.IqF)));
    }

    [Fact]
    public void ParseDevice_X10_IsBoundToProfileAndUsesProfileRadix()
    {
        var iqf = SlmpDeviceParser.Parse("X10", SlmpPlcProfile.IqF);
        var iqr = SlmpDeviceParser.Parse("X10", SlmpPlcProfile.IqR);

        Assert.Equal((uint)8, iqf.Number);
        Assert.Equal(SlmpPlcProfile.IqF, iqf.PlcProfile);
        Assert.Equal("X10", iqf.ToString());
        Assert.Equal((uint)16, iqr.Number);
        Assert.Equal(SlmpPlcProfile.IqR, iqr.PlcProfile);
        Assert.Equal("X10", iqr.ToString());
        Assert.NotEqual(iqf, iqr);
    }

    [Fact]
    public void NumericAddress_IqFX16_FormatsAsX20()
    {
        var device = new SlmpDeviceAddress(SlmpDeviceCode.X, 16, SlmpPlcProfile.IqF);

        Assert.Equal("X20", device.ToString());
        Assert.Equal("X20", SlmpAddress.Format(device));
    }

    [Theory]
    [InlineData("DX10")]
    [InlineData("DY10")]
    [InlineData("V10")]
    [InlineData("LTS10")]
    [InlineData("ZR10")]
    [InlineData("RD10")]
    public void ParseDevice_IqFUnsupportedFamilies_Fail(string text)
    {
        var error = Assert.Throws<NotSupportedException>(() => SlmpDeviceParser.Parse(text, SlmpPlcProfile.IqF));
        Assert.Contains("not supported", error.Message, StringComparison.Ordinal);
        Assert.False(SlmpAddress.TryParse(text, SlmpPlcProfile.IqF, out _));
    }

    [Theory]
    [InlineData("LCS10", SlmpPlcProfile.QnUDV)]
    [InlineData("LZ0", SlmpPlcProfile.QnU)]
    [InlineData("RD0", SlmpPlcProfile.LCpu)]
    [InlineData("LTN0", SlmpPlcProfile.QCpuQj71E71100)]
    public void ParseDevice_MeasuredLegacyUnsupportedFamilies_Fail(string text, SlmpPlcProfile profile)
    {
        var error = Assert.Throws<NotSupportedException>(() => SlmpDeviceParser.Parse(text, profile));
        Assert.Contains("not supported", error.Message, StringComparison.Ordinal);
        Assert.False(SlmpAddress.TryParse(text, profile, out _));
    }

    [Fact]
    public void ParseDevice_ProfileUnsupportedFamiliesMatchCanonicalFixture()
    {
        using var document = LoadCanonicalDeviceRangeRules();
        var rows = document.RootElement.GetProperty("rows");
        var profiles = document.RootElement.GetProperty("profiles");

        foreach (var profileProperty in profiles.EnumerateObject())
        {
            var fixtureProfile = SlmpPlcProfiles.ParseKnownProfileId(profileProperty.Name);
            var plcProfile = fixtureProfile == SlmpPlcProfile.QCpu
                ? SlmpPlcProfile.QCpuQj71E71100
                : fixtureProfile;
            foreach (var ruleProperty in profileProperty.Value.GetProperty("rules").EnumerateObject())
            {
                var unsupported = ruleProperty.Value.GetProperty("kind").GetString() == "unsupported";
                var row = rows.GetProperty(ruleProperty.Name);
                foreach (var deviceElement in row.GetProperty("devices").EnumerateArray())
                {
                    var device = deviceElement.GetProperty("device").GetString()!;
                    var address = $"{device}10";

                    if (unsupported)
                    {
                        var error = Assert.Throws<NotSupportedException>(() =>
                            SlmpDeviceParser.Parse(address, plcProfile));
                        Assert.Contains("not supported", error.Message, StringComparison.Ordinal);
                    }
                    else
                    {
                        _ = SlmpDeviceParser.Parse(address, plcProfile);
                    }
                }
            }
        }

        Assert.Throws<NotSupportedException>(() => SlmpDeviceParser.Parse("DX10", SlmpPlcProfile.IqF));
        Assert.Throws<NotSupportedException>(() => SlmpDeviceParser.Parse("DY10", SlmpPlcProfile.IqF));
    }

    [Fact]
    public void ParseDevice_ProfilelessOverload_IsNotPublic()
    {
        Assert.DoesNotContain(
            typeof(SlmpDeviceParser).GetMethods(),
            method => method.Name == nameof(SlmpDeviceParser.Parse) && method.GetParameters().Length == 1);
    }

    [Fact]
    public void ParseDevice_StepRelay_Succeeds()
    {
        var device = SlmpDeviceParser.Parse("S0", SlmpPlcProfile.IqR);
        Assert.Equal(SlmpDeviceCode.S, device.Code);
        Assert.Equal((uint)0, device.Number);
        Assert.Equal("S0", SlmpAddress.Normalize("s0", SlmpPlcProfile.IqR));
    }

    [Theory]
    [InlineData("d100", "D100")]
    [InlineData("x1f", "X1F")]
    [InlineData("sw10", "SW10")]
    [InlineData("zr123", "ZR123")]
    public void SlmpAddress_Normalize_ReturnsCanonicalText(string input, string expected)
    {
        Assert.Equal(expected, SlmpAddress.Normalize(input, SlmpPlcProfile.IqR));
    }

    [Fact]
    public void ParseNamedTarget_SelfCpu_Succeeds()
    {
        var target = SlmpTargetParser.ParseNamed("SELF-MULTIPLE-CPU-2");
        Assert.Equal("SELF-MULTIPLE-CPU-2", target.Name);
        Assert.Equal(SlmpModuleIo.MultipleCpu2, target.Target.ModuleIo);
        Assert.Equal((byte)0x00, target.Target.Network);
        Assert.Equal((byte)0xFF, target.Target.Station);
    }

    [Fact]
    public void SlmpModuleIo_ConstantsMatchSlmpTargetValues()
    {
        Assert.Equal((ushort)0x03D0, SlmpModuleIo.ControlSystemCpu);
        Assert.Equal((ushort)0x03D1, SlmpModuleIo.StandbySystemCpu);
        Assert.Equal((ushort)0x03D2, SlmpModuleIo.SystemACpu);
        Assert.Equal((ushort)0x03D3, SlmpModuleIo.SystemBCpu);
        Assert.Equal((ushort)0x03E0, SlmpModuleIo.MultipleCpu1);
        Assert.Equal((ushort)0x03E1, SlmpModuleIo.MultipleCpu2);
        Assert.Equal((ushort)0x03E2, SlmpModuleIo.MultipleCpu3);
        Assert.Equal((ushort)0x03E3, SlmpModuleIo.MultipleCpu4);
        Assert.Equal(SlmpModuleIo.MultipleCpu1, SlmpModuleIo.RemoteHead1);
        Assert.Equal(SlmpModuleIo.MultipleCpu2, SlmpModuleIo.RemoteHead2);
        Assert.Equal(SlmpModuleIo.ControlSystemCpu, SlmpModuleIo.ControlSystemRemoteHead);
        Assert.Equal(SlmpModuleIo.StandbySystemCpu, SlmpModuleIo.StandbySystemRemoteHead);
        Assert.Equal((ushort)0x03FF, SlmpModuleIo.OwnStation);
    }

    [Fact]
    public void ParseNamedTarget_NetworkStationShortcut_IsRejected()
    {
        var ex = Assert.Throws<ArgumentException>(() => SlmpTargetParser.ParseNamed("NW1-ST2"));
        Assert.Contains("NAME,NETWORK,STATION,MODULE_IO,MULTIDROP", ex.Message);
    }

    [Theory]
    [InlineData(",1,2,3,4")]
    [InlineData("PLC-A,1,,2,3,4")]
    [InlineData("PLC-A,1,2,3,4,5")]
    public void ParseNamedTarget_RejectsEmptyNameAndExtraRouteFields(string text)
    {
        Assert.Throws<ArgumentException>(() => SlmpTargetParser.ParseNamed(text));
    }

    [Fact]
    public void ParseNamedTarget_AcceptsEveryRouteFieldBoundary()
    {
        var minimum = SlmpTargetParser.ParseNamed("MIN,0,0,0,0");
        Assert.Equal(new SlmpTargetAddress(0, 0, 0, 0), minimum.Target);

        var maximum = SlmpTargetParser.ParseNamed("MAX,255,255,65535,255");
        Assert.Equal(new SlmpTargetAddress(255, 255, 65535, 255), maximum.Target);

        var hexadecimal = SlmpTargetParser.ParseNamed("HEX,0xFF,0xFF,0xFFFF,0xFF");
        Assert.Equal(maximum.Target, hexadecimal.Target);
    }

    [Theory]
    [InlineData("PLC,,0,0,0", "NETWORK", "0..255")]
    [InlineData("PLC,-1,0,0,0", "NETWORK", "0..255")]
    [InlineData("PLC,256,0,0,0", "NETWORK", "0..255")]
    [InlineData("PLC,999999999999999999999999,0,0,0", "NETWORK", "0..255")]
    [InlineData("PLC,0,,0,0", "STATION", "0..255")]
    [InlineData("PLC,0,-1,0,0", "STATION", "0..255")]
    [InlineData("PLC,0,256,0,0", "STATION", "0..255")]
    [InlineData("PLC,0,AD,0,0", "STATION", "0..255")]
    [InlineData("PLC,0,0,,0", "MODULE_IO", "0..65535")]
    [InlineData("PLC,0,0,-1,0", "MODULE_IO", "0..65535")]
    [InlineData("PLC,0,0,65536,0", "MODULE_IO", "0..65535")]
    [InlineData("PLC,0,0,0,", "MULTIDROP", "0..255")]
    [InlineData("PLC,0,0,0,-1", "MULTIDROP", "0..255")]
    [InlineData("PLC,0,0,0,256", "MULTIDROP", "0..255")]
    public void ParseNamedTarget_InvalidNumericFields_AreStableFormatErrors(
        string text,
        string field,
        string range)
    {
        var error = Assert.Throws<FormatException>(() => SlmpTargetParser.ParseNamed(text));
        Assert.Contains(field, error.Message, StringComparison.Ordinal);
        Assert.Contains(range, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseQualifiedDevice_UsesExtensionSpec()
    {
        var qualified = SlmpQualifiedDeviceParser.Parse(@"U3E0\G10", SlmpPlcProfile.IqR);
        Assert.Equal((ushort)0x03E0, qualified.ExtensionSpecification);
        Assert.Equal(SlmpDeviceCode.G, qualified.Device.Code);
        Assert.Equal((uint)10, qualified.Device.Number);
        Assert.Equal((byte)0xF8, qualified.DirectMemorySpecification);
    }

    [Fact]
    public void ParseQualifiedDevice_RejectsHgOutsideIqrCpuBufferRange()
    {
        var qualified = SlmpQualifiedDeviceParser.Parse(@"U3E0\HG0", SlmpPlcProfile.IqR);
        Assert.Equal((ushort)0x03E0, qualified.ExtensionSpecification);
        Assert.Equal(SlmpDeviceCode.HG, qualified.Device.Code);
        Assert.Equal((byte)0xFA, qualified.DirectMemorySpecification);

        var ex = Assert.Throws<ArgumentException>(() => SlmpQualifiedDeviceParser.Parse(@"U1\HG0", SlmpPlcProfile.IqR));
        Assert.Contains(@"HG Extended Device access is valid only for U3E0\HG through U3E3\HG", ex.Message);
    }

    [Theory]
    [InlineData(@"U0000\G0", (ushort)0)]
    [InlineData(@"UFFFF\G0", ushort.MaxValue)]
    public void ParseQualifiedDevice_AcceptsExtensionFieldBoundaries(string text, ushort expected)
    {
        Assert.Equal(expected, SlmpQualifiedDeviceParser.Parse(text, SlmpPlcProfile.IqR).ExtensionSpecification);
    }

    [Theory]
    [InlineData(@"U10000\G0")]
    [InlineData(@"U12345\G0")]
    [InlineData(@"U-1\G0")]
    [InlineData(@"U+1\G0")]
    [InlineData(@"U\G0")]
    [InlineData(@"UZ1\G0")]
    [InlineData(@"UFFFFFFFFFFFFFFFFFFFFFFFF\G0")]
    public void ParseQualifiedDevice_InvalidExtensionFields_IdentifyFieldAndRange(string text)
    {
        var error = Assert.Throws<FormatException>(() =>
            SlmpQualifiedDeviceParser.Parse(text, SlmpPlcProfile.IqR));
        Assert.Contains("U extension", error.Message, StringComparison.Ordinal);
        Assert.Contains("0..65535", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"J0\SW0", (ushort)0)]
    [InlineData(@"J255\SW0", (ushort)255)]
    public void ParseQualifiedDevice_AcceptsJDirectNetworkBoundaries(
        string text,
        ushort expected)
    {
        Assert.Equal(
            expected,
            SlmpQualifiedDeviceParser.Parse(text, SlmpPlcProfile.IqR).ExtensionSpecification);
    }

    [Theory]
    [InlineData(@"J-1\SW0")]
    [InlineData(@"J+1\SW0")]
    [InlineData(@"J\SW0")]
    [InlineData(@"JAB\SW0")]
    [InlineData(@"J256\SW0")]
    [InlineData(@"J999999999999999999999999\SW0")]
    public void ParseQualifiedDevice_InvalidJDirectNetworks_IdentifyFieldAndRange(string text)
    {
        var error = Assert.Throws<FormatException>(() =>
            SlmpQualifiedDeviceParser.Parse(text, SlmpPlcProfile.IqR));
        Assert.Contains("J-direct network", error.Message, StringComparison.Ordinal);
        Assert.Contains("0..255", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryClient_IsTheOnlyPublicClientType()
    {
        Assert.Null(typeof(SlmpClient).Assembly.GetType("PlcComm.Slmp.QueuedSlmpClient"));
    }

    [Fact]
    public void OrdinaryClient_ExposesConfigurationProperties()
    {
        using var client = new SlmpClient(
            "127.0.0.1",
            SlmpPlcProfile.QCpuQj71E71100,
            1025,
            SlmpTransportMode.Tcp,
            new SlmpTargetAddress(0x01, 0x02, 0x03E0, 0x00))
        {
            MonitoringTimer = 0x0020,
            Timeout = TimeSpan.FromSeconds(5),
        };

        Assert.Equal(SlmpFrameType.Frame4E, client.FrameType);
        Assert.Equal(SlmpCompatibilityMode.Legacy, client.CompatibilityMode);
        Assert.Equal((byte)0x01, client.TargetAddress.Network);
        Assert.Equal((ushort)0x0020, client.MonitoringTimer);
        Assert.Equal(TimeSpan.FromSeconds(5), client.Timeout);
    }

    [Fact]
    public void ConnectionOptions_ResolveDefaultsFromPlcProfile()
    {
        var options = new SlmpConnectionOptions("127.0.0.1", SlmpPlcProfile.IqL, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        Assert.Equal(SlmpFrameType.Frame4E, options.ResolvedFrameType);
        Assert.Equal(SlmpCompatibilityMode.Iqr, options.ResolvedCompatibilityMode);
        Assert.Equal(SlmpPlcProfile.IqL, options.ResolvedAddressProfile);
        Assert.Equal(SlmpPlcProfile.IqL, options.ResolvedRangeProfile);
        Assert.Equal("X1A", SlmpAddress.Normalize("x1a", options.ResolvedAddressProfile));
    }

    [Fact]
    public void ConnectionOptions_ResolveUnitProfileWithIndependentFrameAndCompatibility()
    {
        var options = new SlmpConnectionOptions("127.0.0.1", SlmpPlcProfile.QCpuQj71E71100, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        Assert.Equal(SlmpFrameType.Frame4E, options.ResolvedFrameType);
        Assert.Equal(SlmpCompatibilityMode.Legacy, options.ResolvedCompatibilityMode);
        Assert.Equal(SlmpPlcProfile.QCpu, options.ResolvedAddressProfile);
        Assert.Equal(SlmpPlcProfile.QCpuQj71E71100, options.ResolvedRangeProfile);
    }

    [Fact]
    public void ConnectionOptions_ResolveIqRUnitProfileWithIqRAddressRules()
    {
        var options = new SlmpConnectionOptions("127.0.0.1", SlmpPlcProfile.IqRRj71En71, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);

        Assert.Equal(SlmpFrameType.Frame4E, options.ResolvedFrameType);
        Assert.Equal(SlmpCompatibilityMode.Iqr, options.ResolvedCompatibilityMode);
        Assert.Equal(SlmpPlcProfile.IqR, options.ResolvedAddressProfile);
        Assert.Equal(SlmpPlcProfile.IqRRj71En71, options.ResolvedRangeProfile);
    }

    [Fact]
    public void ConnectionOptions_RejectsUnspecifiedPlcProfile()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SlmpConnectionOptions("127.0.0.1", SlmpPlcProfile.Unspecified, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation));
    }

    [Fact]
    public void ConnectionOptions_RejectsBaseQcpuProfile()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SlmpConnectionOptions("127.0.0.1", SlmpPlcProfile.QCpu, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation));
        Assert.Contains("melsec:qcpu is a base profile", error.Message, StringComparison.Ordinal);
        Assert.Contains("melsec:qcpu:qj71e71-100", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SlmpPlcProfiles_Parse_AcceptsOnlyCanonicalProfileText()
    {
        Assert.Equal(SlmpPlcProfile.IqR, SlmpPlcProfiles.Parse("melsec:iq-r"));
        Assert.Equal(SlmpPlcProfile.IqRRj71En71, SlmpPlcProfiles.Parse("melsec:iq-r:rj71en71"));
        Assert.Equal(SlmpPlcProfile.QCpuQj71E71100, SlmpPlcProfiles.Parse("melsec:qcpu:qj71e71-100"));
        Assert.Equal(SlmpPlcProfile.LCpuLj71E71100, SlmpPlcProfiles.Parse("melsec:lcpu:lj71e71-100"));

        Assert.Throws<ArgumentException>(() => SlmpPlcProfiles.Parse(null));
        Assert.Throws<ArgumentException>(() => SlmpPlcProfiles.Parse(""));
        var baseError = Assert.Throws<ArgumentException>(() => SlmpPlcProfiles.Parse("melsec:qcpu"));
        Assert.Contains("melsec:qcpu is a base profile", baseError.Message, StringComparison.Ordinal);
        Assert.Contains("melsec:qcpu:qj71e71-100", baseError.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => SlmpPlcProfiles.Parse("MELSEC:IQ-F"));
        Assert.Throws<ArgumentException>(() => SlmpPlcProfiles.Parse("iq-r"));
        Assert.Throws<ArgumentException>(() => SlmpPlcProfiles.Parse("iqr"));
        Assert.Throws<ArgumentException>(() => SlmpPlcProfiles.Parse("q"));
        Assert.Throws<ArgumentException>(() => SlmpPlcProfiles.Parse("qnudvcpu"));
    }

    [Fact]
    public void ParseQualifiedDevice_LinkDirect_J2SW10_Succeeds()
    {
        var q = SlmpQualifiedDeviceParser.Parse(@"J2\SW10", SlmpPlcProfile.IqR);
        Assert.Equal(SlmpDeviceCode.SW, q.Device.Code);
        Assert.Equal((uint)0x10, q.Device.Number);  // SW is hex-addressed
        Assert.Equal((ushort)2, q.ExtensionSpecification);
        Assert.Equal((byte)0xF9, q.DirectMemorySpecification);
    }

    [Theory]
    [InlineData("D+5")]
    [InlineData("D 5")]
    [InlineData("W 1F")]
    public void ParseDevice_RejectsNonDigitCharactersInNumber(string text)
    {
        Assert.Throws<FormatException>(() => SlmpDeviceParser.Parse(text, SlmpPlcProfile.IqR));
    }

    [Fact]
    public void ParseQualifiedDevice_RejectsJNetworkBeyondByteField()
    {
        var error = Assert.Throws<FormatException>(() =>
            SlmpQualifiedDeviceParser.Parse(@"J256\SW10", SlmpPlcProfile.IqR));

        Assert.Contains("J-direct network", error.Message, StringComparison.Ordinal);
        Assert.Contains("0..255", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("Y")]
    public void ParseDevice_IqFRejectsMissingOctalNumber(string text)
    {
        Assert.Throws<FormatException>(() => SlmpDeviceParser.Parse(text, SlmpPlcProfile.IqF));
    }

    [Fact]
    public void EncodeExtendedDeviceSpec_LinkDirect_J2SW10_MatchesPcap()
    {
        // Verified by GOT pcap: J2\SW10 -> 00 00 10 00 00 b5 00 00 02 00 f9
        var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.QCpuQj71E71100, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var device = new SlmpDeviceAddress(SlmpDeviceCode.SW, 0x10, SlmpPlcProfile.IqR);
        var extension = new SlmpExtensionSpec(2, 0, 0, 0, 0xF9);
        var spec = client.EncodeExtendedDeviceSpec(device, extension);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x10, 0x00, 0x00, 0xB5, 0x00, 0x00, 0x02, 0x00, 0xF9 }, spec);
    }

    [Fact]
    public void EncodeExtendedDeviceSpec_LinkDirectRejectsNumberBeyondWireWidth()
    {
        using var client = new SlmpClient("127.0.0.1", SlmpPlcProfile.IqR, 1025, SlmpTransportMode.Tcp, SlmpTargetAddress.OwnStation);
        var device = new SlmpDeviceAddress(SlmpDeviceCode.SW, 0x0100_0000, SlmpPlcProfile.IqR);
        var extension = new SlmpExtensionSpec(2, 0, 0, 0, 0xF9);

        Assert.Throws<ArgumentOutOfRangeException>(() => client.EncodeExtendedDeviceSpec(device, extension));
    }

    private static JsonDocument LoadCanonicalDeviceRangeRules()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "slmp_device_range_rules.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
