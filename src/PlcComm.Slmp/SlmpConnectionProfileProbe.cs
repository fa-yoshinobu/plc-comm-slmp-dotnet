namespace PlcComm.Slmp;

/// <summary>Outcome of one connection-profile probe attempt.</summary>
public enum SlmpConnectionProfileProbeStatus
{
    /// <summary><c>ReadTypeNameAsync</c> and the family-specific <c>SD</c> block read both succeeded.</summary>
    Validated,

    /// <summary><c>ReadTypeNameAsync</c> succeeded, but the family-specific <c>SD</c> validation read could not be completed.</summary>
    TypeNameOnly,

    /// <summary><c>ReadTypeNameAsync</c> did not return, so this feature treats the candidate as an unsupported PLC/profile.</summary>
    UnsupportedPlc,

    /// <summary>The profile could not be opened far enough to issue <c>ReadTypeNameAsync</c>.</summary>
    Failed,
}

/// <summary>Result for one probed frame/compatibility candidate.</summary>
/// <param name="Transport">Transport used for the attempt.</param>
/// <param name="FrameType">Frame type used for the attempt.</param>
/// <param name="CompatibilityMode">Compatibility mode used for the attempt.</param>
/// <param name="Status">Probe outcome.</param>
/// <param name="TypeNameInfo">PLC type information when <c>ReadTypeNameAsync</c> succeeded.</param>
/// <param name="Family">Resolved family when the model is known to the device-range resolver.</param>
/// <param name="SdRegisterStart">First <c>SD</c> register used for validation when the PLC family is known.</param>
/// <param name="SdRegisterCount">Number of contiguous <c>SD</c> registers used for validation when the PLC family is known.</param>
/// <param name="SdReadSucceeded">True when the family-specific <c>SD</c> block read succeeded for this profile.</param>
/// <param name="ErrorMessage">Failure detail for the first failed step, when any.</param>
public sealed record SlmpConnectionProfileProbeResult(
    SlmpTransportMode Transport,
    SlmpFrameType FrameType,
    SlmpCompatibilityMode CompatibilityMode,
    SlmpConnectionProfileProbeStatus Status,
    SlmpTypeNameInfo? TypeNameInfo,
    SlmpDeviceRangeFamily? Family,
    int? SdRegisterStart,
    int? SdRegisterCount,
    bool SdReadSucceeded,
    string? ErrorMessage);

/// <summary>Device-range catalog resolved from a chosen connection profile.</summary>
public sealed record SlmpResolvedDeviceRangeCatalog(
    SlmpTransportMode Transport,
    SlmpFrameType FrameType,
    SlmpCompatibilityMode CompatibilityMode,
    bool UsedThreeELegacyFallback,
    SlmpDeviceRangeCatalog Catalog);

/// <summary>
/// Probes the standard frame/compatibility candidates against one PLC endpoint.
/// </summary>
/// <remarks>
/// This helper only reports which profiles succeed. It does not automatically
/// choose one for application code.
/// </remarks>
public static class SlmpConnectionProfileProbe
{
    private static readonly (SlmpFrameType FrameType, SlmpCompatibilityMode CompatibilityMode)[] DefaultProfiles =
    [
        (SlmpFrameType.Frame4E, SlmpCompatibilityMode.Iqr),
        (SlmpFrameType.Frame3E, SlmpCompatibilityMode.Iqr),
        (SlmpFrameType.Frame4E, SlmpCompatibilityMode.Legacy),
        (SlmpFrameType.Frame3E, SlmpCompatibilityMode.Legacy),
    ];

    /// <summary>
    /// Probes the standard frame/compatibility candidates using the host, port,
    /// route, timeout, transport, and monitoring timer from <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Base connection options. Frame and compatibility are overridden per candidate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Probe results in fixed candidate order.</returns>
    public static async Task<SlmpConnectionProfileProbeResult[]> ProbeAsync(
        SlmpConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var results = new SlmpConnectionProfileProbeResult[DefaultProfiles.Length];
        for (var i = 0; i < DefaultProfiles.Length; i++)
        {
            var candidate = DefaultProfiles[i];
            results[i] = await ProbeProfileAsync(options, candidate.FrameType, candidate.CompatibilityMode, cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    /// <summary>
    /// Reads the device-range catalog using the supplied settings first and retries with
    /// <c>3E + Legacy</c> only when <c>ReadTypeNameAsync</c> does not return.
    /// </summary>
    public static async Task<SlmpResolvedDeviceRangeCatalog> ReadDeviceRangeCatalogWithThreeELegacyFallbackAsync(
        SlmpConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var initialFailure = await TryReadDeviceRangeCatalogAsync(
            options,
            options.ResolvedFrameType,
            options.ResolvedCompatibilityMode,
            cancellationToken).ConfigureAwait(false);
        if (initialFailure.Result is not null)
        {
            return initialFailure.Result;
        }

        if (!initialFailure.ReadTypeNameFailed ||
            (options.ResolvedFrameType == SlmpFrameType.Frame3E && options.ResolvedCompatibilityMode == SlmpCompatibilityMode.Legacy))
        {
            throw initialFailure.Error ?? new SlmpError("Device-range catalog read failed.");
        }

        var fallback = await TryReadDeviceRangeCatalogAsync(
            options,
            SlmpFrameType.Frame3E,
            SlmpCompatibilityMode.Legacy,
            cancellationToken).ConfigureAwait(false);
        if (fallback.Result is not null)
        {
            return fallback.Result with { UsedThreeELegacyFallback = true };
        }

        throw fallback.Error ?? initialFailure.Error ?? new SlmpError("Device-range catalog read failed.");
    }

    internal static async Task<SlmpConnectionProfileProbeResult> ProbeProfileAsync(
        SlmpConnectionOptions options,
        SlmpFrameType frameType,
        SlmpCompatibilityMode compatibilityMode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        SlmpTypeNameInfo? typeNameInfo = null;
        SlmpDeviceRangeFamily? family = null;
        SlmpDeviceRangeProfile? profile = null;
        var sdReadSucceeded = false;
        string? errorMessage = null;

        try
        {
            using var client = new SlmpClient(options.Host, options.Port, options.Transport)
            {
                Timeout = options.Timeout,
                FrameType = frameType,
                CompatibilityMode = compatibilityMode,
                TargetAddress = options.Target,
                MonitoringTimer = options.MonitoringTimer,
            };

            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                typeNameInfo = await client.ReadTypeNameAsync(cancellationToken).ConfigureAwait(false);
                family = TryResolveFamily(typeNameInfo);
            }
            catch (Exception ex)
            {
                errorMessage = $"read_type_name: {ex.Message}";
                return new SlmpConnectionProfileProbeResult(
                    options.Transport,
                    frameType,
                    compatibilityMode,
                    SlmpConnectionProfileProbeStatus.UnsupportedPlc,
                    null,
                    null,
                    null,
                    null,
                    false,
                    errorMessage);
            }

            try
            {
                profile = SlmpDeviceRangeResolver.ResolveProfile(typeNameInfo);
                family = profile.Family;
            }
            catch (SlmpError ex)
            {
                errorMessage = $"resolve_family: {ex.Message}";
            }

            if (profile is not null && profile.RegisterCount > 0)
            {
                if (!IsFrameSupportedByFamily(profile.Family, frameType))
                {
                    errorMessage = $"frame_support: {profile.Family} does not support {frameType} in this library.";
                }
                else
                {
                    try
                    {
                        _ = await client.ReadWordsRawAsync(
                            new SlmpDeviceAddress(SlmpDeviceCode.SD, checked((uint)profile.RegisterStart)),
                            checked((ushort)profile.RegisterCount),
                            cancellationToken).ConfigureAwait(false);
                        sdReadSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        errorMessage = $"read_sd_block: {ex.Message}";
                    }
                }
            }
            else if (profile is null)
            {
                errorMessage ??= "resolve_family: device-range SD block is unavailable for this PLC model.";
            }

            if (profile is null)
            {
                return new SlmpConnectionProfileProbeResult(
                    options.Transport,
                    frameType,
                    compatibilityMode,
                    SlmpConnectionProfileProbeStatus.TypeNameOnly,
                    typeNameInfo,
                    family,
                    null,
                    null,
                    false,
                    errorMessage);
            }

            if (profile.RegisterCount <= 0)
            {
                sdReadSucceeded = true;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"open: {ex.Message}";
        }

        var status = typeNameInfo is null
            ? SlmpConnectionProfileProbeStatus.Failed
            : sdReadSucceeded
                ? SlmpConnectionProfileProbeStatus.Validated
                : SlmpConnectionProfileProbeStatus.TypeNameOnly;

        return new SlmpConnectionProfileProbeResult(
            options.Transport,
            frameType,
            compatibilityMode,
            status,
            typeNameInfo,
            family,
            profile?.RegisterStart,
            profile?.RegisterCount,
            sdReadSucceeded,
            errorMessage);
    }

    private static bool IsFrameSupportedByFamily(SlmpDeviceRangeFamily family, SlmpFrameType frameType)
    {
        return family switch
        {
            SlmpDeviceRangeFamily.IqF => frameType == SlmpFrameType.Frame3E,
            _ => true,
        };
    }

    private static SlmpDeviceRangeFamily? TryResolveFamily(SlmpTypeNameInfo typeNameInfo)
    {
        try
        {
            return SlmpDeviceRangeResolver.ResolveFamily(typeNameInfo);
        }
        catch (SlmpError)
        {
            return null;
        }
    }

    private static async Task<(SlmpResolvedDeviceRangeCatalog? Result, Exception? Error, bool ReadTypeNameFailed)> TryReadDeviceRangeCatalogAsync(
        SlmpConnectionOptions options,
        SlmpFrameType frameType,
        SlmpCompatibilityMode compatibilityMode,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SlmpClient(options.Host, options.Port, options.Transport)
            {
                Timeout = options.Timeout,
                FrameType = frameType,
                CompatibilityMode = compatibilityMode,
                TargetAddress = options.Target,
                MonitoringTimer = options.MonitoringTimer,
            };

            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

            SlmpTypeNameInfo typeInfo;
            try
            {
                typeInfo = await client.ReadTypeNameAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return (null, new SlmpError($"read_type_name: {ex.Message}"), true);
            }

            var profile = SlmpDeviceRangeResolver.ResolveProfile(typeInfo);
            var registers = await SlmpDeviceRangeResolver.ReadRegistersAsync(client, profile, cancellationToken).ConfigureAwait(false);
            var catalog = SlmpDeviceRangeResolver.BuildCatalog(typeInfo, profile, registers);
            return (new SlmpResolvedDeviceRangeCatalog(options.Transport, frameType, compatibilityMode, false, catalog), null, false);
        }
        catch (Exception ex)
        {
            return (null, ex, false);
        }
    }
}
