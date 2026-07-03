using System.Buffers.Binary;

namespace PlcComm.Slmp;

/// <summary>
/// Structured SLMP error information returned after a non-zero end code.
/// </summary>
/// <param name="Network">Network number reported by the PLC.</param>
/// <param name="Station">Station number reported by the PLC.</param>
/// <param name="ModuleIo">Module I/O number reported by the PLC.</param>
/// <param name="Multidrop">Multidrop station number reported by the PLC.</param>
/// <param name="Command">Command code associated with the PLC error.</param>
/// <param name="Subcommand">Subcommand code associated with the PLC error.</param>
/// <param name="Raw">Raw 9-byte error information block.</param>
public sealed record SlmpErrorInfo(
    byte Network,
    byte Station,
    ushort ModuleIo,
    byte Multidrop,
    ushort Command,
    ushort Subcommand,
    byte[] Raw)
{
    /// <summary>
    /// Parse a 9-byte SLMP error information block, or return null when it is not present.
    /// </summary>
    public static SlmpErrorInfo? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 9)
        {
            return null;
        }

        var raw = data[..9].ToArray();
        return new SlmpErrorInfo(
            raw[0],
            raw[1],
            BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(2, 2)),
            raw[4],
            BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(5, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(7, 2)),
            raw);
    }
}

/// <summary>
/// Error thrown when an SLMP protocol error occurs or the PLC returns an error code.
/// </summary>
#pragma warning disable CA1710 // Intentionally named SlmpError (not SlmpException) for cross-language API consistency
public class SlmpError : Exception
#pragma warning restore CA1710
{
    public SlmpError(
        string message,
        ushort? endCode = null,
        SlmpCommand? command = null,
        ushort? subcommand = null,
        Exception? innerException = null,
        SlmpErrorInfo? errorInfo = null)
        : base(message, innerException)
    {
        EndCode = endCode;
        Command = command;
        Subcommand = subcommand;
        ErrorInfo = errorInfo;
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
    /// Structured PLC error information from the response data, when present.
    /// </summary>
    public SlmpErrorInfo? ErrorInfo { get; }

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
