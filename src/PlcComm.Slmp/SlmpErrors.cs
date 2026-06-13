namespace PlcComm.Slmp;

/// <summary>
/// Error thrown when an SLMP protocol error occurs or the PLC returns an error code.
/// </summary>
#pragma warning disable CA1710 // Intentionally named SlmpError (not SlmpException) for cross-language API consistency
public class SlmpError : Exception
#pragma warning restore CA1710
{
    public SlmpError(string message, ushort? endCode = null, SlmpCommand? command = null, ushort? subcommand = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EndCode = endCode;
        Command = command;
        Subcommand = subcommand;
    }

    /// <summary>
    /// The end code returned by the PLC (0x0000 for success).
    /// </summary>
    public ushort? EndCode { get; }

    /// <summary>
    /// The SLMP command that triggered the error.
    /// </summary>
    public SlmpCommand? Command { get; }

    /// <summary>
    /// The SLMP subcommand that triggered the error.
    /// </summary>
    public ushort? Subcommand { get; }

    /// <summary>
    /// Compact symbolic name for <see cref="EndCode"/>, or null when no end code is available.
    /// </summary>
    public string? EndCodeName => EndCode is { } endCode ? SlmpEndCodes.GetName(endCode) : null;

    /// <summary>
    /// English error detail/cause message for <see cref="EndCode"/>, or null when unknown.
    /// </summary>
    public string? EndCodeMessage => EndCode is { } endCode ? SlmpEndCodes.GetMessage(endCode) : null;

    /// <summary>
    /// True when <see cref="EndCode"/> is a remote-password-related SLMP error.
    /// </summary>
    public bool IsRemotePasswordError => EndCode is { } endCode && SlmpEndCodes.IsRemotePasswordEndCode(endCode);
}

