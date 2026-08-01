namespace PlcComm.Slmp;

internal enum SlmpDeviceUnit
{
    Bit,
    Word,
}

/// <summary>
/// Canonical semantic unit classification for every public SLMP device code.
/// </summary>
internal static class SlmpDeviceUnits
{
    internal static SlmpDeviceUnit Get(SlmpDeviceCode code)
        => code switch
        {
            SlmpDeviceCode.SM or
            SlmpDeviceCode.X or
            SlmpDeviceCode.Y or
            SlmpDeviceCode.M or
            SlmpDeviceCode.L or
            SlmpDeviceCode.F or
            SlmpDeviceCode.V or
            SlmpDeviceCode.B or
            SlmpDeviceCode.S or
            SlmpDeviceCode.TS or
            SlmpDeviceCode.TC or
            SlmpDeviceCode.LTS or
            SlmpDeviceCode.LTC or
            SlmpDeviceCode.STS or
            SlmpDeviceCode.STC or
            SlmpDeviceCode.LSTS or
            SlmpDeviceCode.LSTC or
            SlmpDeviceCode.CS or
            SlmpDeviceCode.CC or
            SlmpDeviceCode.LCS or
            SlmpDeviceCode.LCC or
            SlmpDeviceCode.SB or
            SlmpDeviceCode.DX or
            SlmpDeviceCode.DY => SlmpDeviceUnit.Bit,

            SlmpDeviceCode.SD or
            SlmpDeviceCode.D or
            SlmpDeviceCode.W or
            SlmpDeviceCode.TN or
            SlmpDeviceCode.LTN or
            SlmpDeviceCode.STN or
            SlmpDeviceCode.LSTN or
            SlmpDeviceCode.CN or
            SlmpDeviceCode.LCN or
            SlmpDeviceCode.SW or
            SlmpDeviceCode.Z or
            SlmpDeviceCode.LZ or
            SlmpDeviceCode.R or
            SlmpDeviceCode.ZR or
            SlmpDeviceCode.RD or
            SlmpDeviceCode.G or
            SlmpDeviceCode.HG => SlmpDeviceUnit.Word,

            _ => throw new ArgumentOutOfRangeException(
                nameof(code),
                code,
                "Undefined SLMP device codes do not have a semantic unit."),
        };

    internal static bool IsBit(SlmpDeviceCode code) => Get(code) == SlmpDeviceUnit.Bit;

    internal static bool IsWord(SlmpDeviceCode code) => Get(code) == SlmpDeviceUnit.Word;
}
