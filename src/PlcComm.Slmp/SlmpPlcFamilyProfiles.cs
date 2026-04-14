namespace PlcComm.Slmp;

/// <summary>Resolved fixed defaults for one canonical PLC family.</summary>
public sealed record SlmpPlcFamilyDefaults(
    SlmpFrameType FrameType,
    SlmpCompatibilityMode CompatibilityMode,
    SlmpPlcFamily AddressFamily,
    SlmpDeviceRangeFamily RangeFamily);

/// <summary>Fixed high-level defaults driven by <see cref="SlmpPlcFamily"/>.</summary>
public static class SlmpPlcFamilyProfiles
{
    /// <summary>Resolve the stable defaults for one explicit PLC family.</summary>
    public static SlmpPlcFamilyDefaults Resolve(SlmpPlcFamily family)
        => family switch
        {
            SlmpPlcFamily.IqF => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcFamily.IqF,
                SlmpDeviceRangeFamily.IqF),
            SlmpPlcFamily.IqR => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Iqr,
                SlmpPlcFamily.IqR,
                SlmpDeviceRangeFamily.IqR),
            SlmpPlcFamily.IqL => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Iqr,
                SlmpPlcFamily.IqR,
                SlmpDeviceRangeFamily.IqR),
            SlmpPlcFamily.MxF => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Iqr,
                SlmpPlcFamily.MxF,
                SlmpDeviceRangeFamily.MxF),
            SlmpPlcFamily.MxR => new(
                SlmpFrameType.Frame4E,
                SlmpCompatibilityMode.Iqr,
                SlmpPlcFamily.MxR,
                SlmpDeviceRangeFamily.MxR),
            SlmpPlcFamily.QCpu => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcFamily.QCpu,
                SlmpDeviceRangeFamily.QCpu),
            SlmpPlcFamily.LCpu => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcFamily.LCpu,
                SlmpDeviceRangeFamily.LCpu),
            SlmpPlcFamily.QnU => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcFamily.QnU,
                SlmpDeviceRangeFamily.QnU),
            SlmpPlcFamily.QnUDV => new(
                SlmpFrameType.Frame3E,
                SlmpCompatibilityMode.Legacy,
                SlmpPlcFamily.QnUDV,
                SlmpDeviceRangeFamily.QnUDV),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unsupported PLC family."),
        };

    /// <summary>True when <c>X</c> and <c>Y</c> strings must be parsed as octal.</summary>
    public static bool UsesIqFXyOctal(SlmpPlcFamily family)
        => Resolve(family).AddressFamily == SlmpPlcFamily.IqF;
}
