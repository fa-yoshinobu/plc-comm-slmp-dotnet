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
}
