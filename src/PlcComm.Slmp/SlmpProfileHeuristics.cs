namespace PlcComm.Slmp;

/// <summary>
/// Utility for analyzing PLC type names and recommending protocol profiles.
/// </summary>
public static class SlmpProfileHeuristics
{
    /// <summary>
    /// Recommends a protocol profile based on the PLC type name information.
    /// </summary>
    public static SlmpProfileRecommendation Recommend(SlmpTypeNameInfo info)
    {
        if (info.HasModelCode)
        {
            if (info.ModelCode >= 0x4800 && info.ModelCode < 0x5000)
            {
                return new SlmpProfileRecommendation(SlmpFrameType.Frame4E, SlmpCompatibilityMode.Iqr, SlmpProfileClass.ModernIqr, true);
            }
            if ((info.ModelCode >= 0x0000 && info.ModelCode < 0x0100) ||
                (info.ModelCode >= 0x0200 && info.ModelCode < 0x0400))
            {
                return new SlmpProfileRecommendation(SlmpFrameType.Frame3E, SlmpCompatibilityMode.Legacy, SlmpProfileClass.LegacyQl, true);
            }
        }

        var model = info.Model.ToUpperInvariant();
        if (model.StartsWith('R') && !model.StartsWith("RD", StringComparison.Ordinal) && !model.StartsWith("RX", StringComparison.Ordinal) && !model.StartsWith("RY", StringComparison.Ordinal))
        {
            return new SlmpProfileRecommendation(SlmpFrameType.Frame4E, SlmpCompatibilityMode.Iqr, SlmpProfileClass.ModernIqr, true);
        }

        if (model.StartsWith('Q') || model.StartsWith('L') || model.StartsWith("FX", StringComparison.Ordinal))
        {
            return new SlmpProfileRecommendation(SlmpFrameType.Frame3E, SlmpCompatibilityMode.Legacy, SlmpProfileClass.LegacyQl, true);
        }

        return new SlmpProfileRecommendation(SlmpFrameType.Frame4E, SlmpCompatibilityMode.Iqr, SlmpProfileClass.Unknown, false);
    }
}
