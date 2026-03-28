using System.Text.Json;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

/// <summary>
/// Cross-language spec compliance: verifies that .NET produces the same device spec
/// byte sequences as defined in slmp_device_vectors.json (shared with Python tests).
/// </summary>
public sealed class SlmpDeviceVectorTests
{
    private static readonly string VectorsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "vectors", "slmp_device_vectors.json");

    public static IEnumerable<object[]> Vectors()
    {
        var json = File.ReadAllText(VectorsPath);
        var doc = JsonDocument.Parse(json);
        foreach (var v in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            yield return [
                v.GetProperty("id").GetString()!,
                v.GetProperty("device").GetString()!,
                v.GetProperty("series").GetString()!,
                v.GetProperty("hex").GetString()!,
            ];
        }
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void EncodeDeviceSpec_MatchesVector(string id, string device, string series, string hex)
    {
        var mode = series == "iqr"
            ? SlmpCompatibilityMode.Iqr
            : SlmpCompatibilityMode.Legacy;

        using var client = new SlmpClient("127.0.0.1") { CompatibilityMode = mode };
        var addr = SlmpDeviceParser.Parse(device);
        var buf = new byte[client.DeviceSpecSize()];
        client.EncodeDeviceSpec(addr, buf);

        var expected = Convert.FromHexString(hex);
        Assert.True(expected.SequenceEqual(buf),
            $"[{id}] device={device} series={series}: got {Convert.ToHexString(buf)}, expected {hex}");
    }
}
