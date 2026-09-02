using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpAddressSpecTests
{
    [Fact]
    public void ParseFormatNormalize_RoundTripTypedAndBitSelectionExpressions()
    {
        var typed = SlmpAddressSpec.Parse(" d100:u ", SlmpPlcProfile.IqR);
        Assert.Equal(SlmpDeviceCode.D, typed.DeviceAddress.Code);
        Assert.Equal((uint)100, typed.DeviceAddress.Number);
        Assert.Equal(SlmpPlcProfile.IqR, typed.DeviceAddress.PlcProfile);
        Assert.Equal("U", typed.DType);
        Assert.Null(typed.BitIndex);
        Assert.Equal("D100:U", SlmpAddressSpec.Format(typed));
        Assert.Equal("D100:U", SlmpAddressSpec.Normalize(" d100:u ", SlmpPlcProfile.IqR));

        var bit = SlmpAddressSpec.Parse("d50.a", SlmpPlcProfile.IqR);
        Assert.Equal(SlmpDeviceCode.D, bit.DeviceAddress.Code);
        Assert.Equal((uint)50, bit.DeviceAddress.Number);
        Assert.Equal("BIT_IN_WORD", bit.DType);
        Assert.Equal(0xA, bit.BitIndex);
        Assert.Equal("D50.A", SlmpAddressSpec.Format(bit));
        Assert.Equal("D50.A", SlmpAddressSpec.Normalize("d50.a", SlmpPlcProfile.IqR));
    }

    [Fact]
    public void DeviceAddressAndAddressSpec_GrammarsRemainDistinct()
    {
        foreach (var text in new[] { "D100", "X10" })
        {
            var direct = SlmpAddress.Parse(text, SlmpPlcProfile.IqR);
            Assert.Equal(text, SlmpAddress.Format(direct));
            Assert.False(SlmpAddressSpec.TryParse(text, SlmpPlcProfile.IqR, out _));
        }

        foreach (var text in new[] { "D100:U", "D50.A" })
        {
            Assert.False(SlmpAddress.TryParse(text, SlmpPlcProfile.IqR, out _));
            Assert.True(SlmpAddressSpec.TryParse(text, SlmpPlcProfile.IqR, out _));
        }
    }

    [Theory]
    [InlineData(@"J1\X10:BIT")]
    [InlineData(@"U0\G100:U")]
    public void Parse_RejectsQualifiedRoutes(string text)
    {
        Assert.False(SlmpAddress.TryParse(text[..text.IndexOf(':')], SlmpPlcProfile.IqR, out _));
        Assert.False(SlmpAddressSpec.TryParse(text, SlmpPlcProfile.IqR, out _));
    }

    [Theory]
    [InlineData("M100.0")]
    [InlineData("D100:BIT")]
    [InlineData("X10:U")]
    [InlineData("D50.10")]
    [InlineData("D100:BIT_IN_WORD")]
    public void TryParse_RejectsInvalidUnitOrSuffix(string text)
    {
        Assert.False(SlmpAddressSpec.TryParse(text, SlmpPlcProfile.IqR, out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void Parse_PreservesProfileSpecificDeviceRadix()
    {
        var iqf = SlmpAddressSpec.Parse("X10:BIT", SlmpPlcProfile.IqF);
        var iqr = SlmpAddressSpec.Parse("X10:BIT", SlmpPlcProfile.IqR);

        Assert.Equal((uint)8, iqf.DeviceAddress.Number);
        Assert.Equal((uint)16, iqr.DeviceAddress.Number);
        Assert.Equal("X10:BIT", SlmpAddressSpec.Format(iqf));
        Assert.Equal("X10:BIT", SlmpAddressSpec.Format(iqr));
    }

    [Fact]
    public void PublicSurface_HasOneCanonicalAddressSpecOperationSet()
    {
        var operationNames = typeof(SlmpAddressSpec)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(["Format", "Normalize", "Parse", "TryParse"], operationNames);
    }
}
