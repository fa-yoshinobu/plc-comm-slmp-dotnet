namespace PlcComm.Slmp;

/// <summary>
/// Error thrown when an SLMP protocol error occurs or the PLC returns an error code.
/// </summary>
public sealed class SlmpError : Exception
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
