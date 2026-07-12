namespace PlcComm.Slmp;

/// <summary>Helper methods for SLMP end-code keys and categories.</summary>
public static class SlmpEndCodes
{
    /// <summary>Returns the stable code-derived key for an SLMP end code.</summary>
    public static string GetName(ushort endCode) => $"slmp_end_code_{endCode:x4}";

    /// <summary>Returns whether the SLMP end code is related to remote password protection.</summary>
    public static bool IsRemotePasswordEndCode(ushort endCode) =>
        endCode is 0xC200 or 0xC201 or 0xC202 or 0xC203 or 0xC204 or 0xC205 or 0xC810 or 0xC811 or 0xC812 or 0xC813 or 0xC814 or 0xC815 or 0xC816;
}
