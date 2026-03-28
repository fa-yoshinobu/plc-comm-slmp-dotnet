namespace PlcComm.Slmp;

/// <summary>Specifies the transport protocol used for SLMP communication.</summary>
public enum SlmpTransportMode
{
    /// <summary>Transmission Control Protocol (Connection-oriented).</summary>
    Tcp,
    /// <summary>User Datagram Protocol (Connectionless).</summary>
    Udp,
}

/// <summary>Specifies the SLMP frame format header.</summary>
public enum SlmpFrameType : byte
{
    /// <summary>3E Frame (Standard subheader 0x5000).</summary>
    Frame3E = 0,
    /// <summary>4E Frame (Serial-based subheader 0x5400).</summary>
    Frame4E = 1,
}

/// <summary>Specifies the device access subcommand compatibility mode.</summary>
public enum SlmpCompatibilityMode : byte
{
    /// <summary>Legacy Q/L series subcommands (0x0000/0x0001).</summary>
    Legacy = 0,
    /// <summary>Modern iQ-R series subcommands (0x0002/0x0003).</summary>
    Iqr = 1,
}

/// <summary>Direction of a frame captured by <see cref="SlmpClient.TraceHook"/>.</summary>
public enum SlmpTraceDirection
{
    /// <summary>Frame sent to the PLC.</summary>
    Send,
    /// <summary>Frame received from the PLC.</summary>
    Receive,
}

/// <summary>Standard SLMP command codes.</summary>
public enum SlmpCommand : ushort
{
    DeviceRead = 0x0401,
    DeviceWrite = 0x1401,
    DeviceReadRandom = 0x0403,
    DeviceWriteRandom = 0x1402,
    DeviceReadBlock = 0x0406,
    DeviceWriteBlock = 0x1406,
    MonitorRegister = 0x0801,
    Monitor = 0x0802,
    ReadTypeName = 0x0101,
    LabelArrayRead = 0x041A,
    LabelArrayWrite = 0x141A,
    LabelReadRandom = 0x041C,
    LabelWriteRandom = 0x141B,
    MemoryRead = 0x0613,
    MemoryWrite = 0x1613,
    ExtendUnitRead = 0x0601,
    ExtendUnitWrite = 0x1601,
    RemoteRun = 0x1001,
    RemoteStop = 0x1002,
    RemotePause = 0x1003,
    RemoteLatchClear = 0x1005,
    RemoteReset = 0x1006,
    RemotePasswordUnlock = 0x1630,
    RemotePasswordLock = 0x1631,
    SelfTest = 0x0619,
    ClearError = 0x1617,
}

/// <summary>Standard SLMP binary device codes.</summary>
public enum SlmpDeviceCode : ushort
{
    /// <summary>Special Relay</summary>
    SM = 0x0091,
    /// <summary>Special Register</summary>
    SD = 0x00A9,
    /// <summary>Input</summary>
    X = 0x009C,
    /// <summary>Output</summary>
    Y = 0x009D,
    /// <summary>Internal Relay</summary>
    M = 0x0090,
    /// <summary>Latch Relay</summary>
    L = 0x0092,
    /// <summary>Annunciator</summary>
    F = 0x0093,
    /// <summary>Edge Relay</summary>
    V = 0x0094,
    /// <summary>Link Relay</summary>
    B = 0x00A0,
    /// <summary>Data Register</summary>
    D = 0x00A8,
    /// <summary>Link Register</summary>
    W = 0x00B4,
    /// <summary>Timer Contact</summary>
    TS = 0x00C1,
    /// <summary>Timer Coil</summary>
    TC = 0x00C0,
    /// <summary>Timer Current Value</summary>
    TN = 0x00C2,
    /// <summary>Long Timer Contact</summary>
    LTS = 0x0051,
    /// <summary>Long Timer Coil</summary>
    LTC = 0x0050,
    /// <summary>Long Timer Current Value</summary>
    LTN = 0x0052,
    /// <summary>Retentive Timer Contact</summary>
    STS = 0x00C7,
    /// <summary>Retentive Timer Coil</summary>
    STC = 0x00C6,
    /// <summary>Retentive Timer Current Value</summary>
    STN = 0x00C8,
    /// <summary>Long Retentive Timer Contact</summary>
    LSTS = 0x0059,
    /// <summary>Long Retentive Timer Coil</summary>
    LSTC = 0x0058,
    /// <summary>Long Retentive Timer Current Value</summary>
    LSTN = 0x005A,
    /// <summary>Long Counter Coil</summary>
    LCC = 0x0054,
    /// <summary>Long Counter Contact</summary>
    LCS = 0x0055,
    /// <summary>Long Counter Current Value</summary>
    LCN = 0x0056,
    /// <summary>Counter Contact</summary>
    CS = 0x00C4,
    /// <summary>Counter Coil</summary>
    CC = 0x00C3,
    /// <summary>Counter Current Value</summary>
    CN = 0x00C5,
    /// <summary>Link Special Relay</summary>
    SB = 0x00A1,
    /// <summary>Link Special Register</summary>
    SW = 0x00B5,
    /// <summary>Direct Input</summary>
    DX = 0x00A2,
    /// <summary>Direct Output</summary>
    DY = 0x00A3,
    /// <summary>Index Register</summary>
    Z = 0x00CC,
    /// <summary>Long Index Register</summary>
    LZ = 0x0062,
    /// <summary>File Register</summary>
    R = 0x00AF,
    /// <summary>File Register (Continuous)</summary>
    ZR = 0x00B0,
    /// <summary>Refresh Data Register</summary>
    RD = 0x002C,
    /// <summary>Buffer Memory</summary>
    G = 0x00AB,
    /// <summary>Long Buffer Memory</summary>
    HG = 0x002E,
}
