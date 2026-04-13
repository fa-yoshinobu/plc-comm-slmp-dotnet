using System.Text.Json;

namespace PlcComm.Slmp.Tests;

internal static class SharedSpecLoader
{
    private static readonly string SharedSpecRoot = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "shared-spec");

    public static JsonDocument Load(string fileName)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(SharedSpecRoot, fileName)));
}
