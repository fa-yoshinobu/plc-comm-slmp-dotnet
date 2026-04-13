using System.Text.Json;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpSharedAddressSpecTests
{
    public static IEnumerable<object?[]> NormalizeCases()
    {
        using var doc = SharedSpecLoader.Load("high_level_address_normalize_vectors.json");
        foreach (var entry in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (!Supports(entry, "dotnet"))
            {
                continue;
            }

            yield return new object?[]
            {
                entry.GetProperty("id").GetString()!,
                entry.GetProperty("input").GetString()!,
                entry.GetProperty("expected").GetString()!,
            };
        }
    }

    public static IEnumerable<object?[]> ParseCases()
    {
        using var doc = SharedSpecLoader.Load("high_level_address_parse_vectors.json");
        foreach (var entry in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (!Supports(entry, "dotnet"))
            {
                continue;
            }

            var expected = entry.GetProperty("expected");
            yield return new object?[]
            {
                entry.GetProperty("id").GetString()!,
                entry.GetProperty("input").GetString()!,
                expected.GetProperty("base").GetString()!,
                expected.GetProperty("dtype").GetString()!,
                expected.GetProperty("bit_index").ValueKind == JsonValueKind.Null
                    ? (int?)null
                    : expected.GetProperty("bit_index").GetInt32(),
            };
        }
    }

    [Theory]
    [MemberData(nameof(NormalizeCases))]
    public void NormalizeNamedAddress_MatchesSharedSpec(string id, string input, string expected)
    {
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Equal(expected, SlmpClientExtensions.NormalizeNamedAddress(input));
    }

    [Theory]
    [MemberData(nameof(ParseCases))]
    public void ParseAddress_MatchesSharedSpec(string id, string input, string expectedBase, string expectedDType, int? expectedBitIndex)
    {
        Assert.False(string.IsNullOrWhiteSpace(id));
        var parsed = SlmpClientExtensions.ParseAddress(input);
        Assert.Equal(expectedBase, parsed.Base);
        Assert.Equal(expectedDType, parsed.DType);
        Assert.Equal(expectedBitIndex, parsed.BitIdx);
    }

    private static bool Supports(JsonElement entry, string implementation)
        => entry.GetProperty("implementations").EnumerateArray()
            .Any(item => string.Equals(item.GetString(), implementation, StringComparison.Ordinal));
}
