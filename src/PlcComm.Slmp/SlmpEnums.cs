namespace PlcComm.Slmp;

public enum SlmpTransportMode
{
    Tcp,
    Udp,
}

public enum SlmpFrameType : byte
{
    Frame3E = 0,
    Frame4E = 1,
}

public enum SlmpCompatibilityMode : byte
{
    Legacy = 0,
    Iqr = 1,
}

public enum SlmpProfileClass : byte
{
    Unknown = 0,
    LegacyQl = 1,
    ModernIqr = 2,
}

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

public enum SlmpDeviceCode : ushort
{
    SM = 0x0091,
    SD = 0x00A9,
    X = 0x009C,
    Y = 0x009D,
    M = 0x0090,
    L = 0x0092,
    V = 0x0094,
    B = 0x00A0,
    D = 0x00A8,
    W = 0x00B4,
    TS = 0x00C1,
    TC = 0x00C0,
    TN = 0x00C2,
    LTS = 0x0051,
    LTC = 0x0050,
    LTN = 0x0052,
    STS = 0x00C7,
    STC = 0x00C6,
    STN = 0x00C8,
    LSTS = 0x0059,
    LSTC = 0x0058,
    LSTN = 0x005A,
    CS = 0x00C4,
    CC = 0x00C3,
    CN = 0x00C5,
    SB = 0x00A1,
    SW = 0x00B5,
    S = 0x0098,
    DX = 0x00A2,
    DY = 0x00A3,
    Z = 0x00CC,
    LZ = 0x0062,
    R = 0x00AF,
    ZR = 0x00B0,
    RD = 0x002C,
    G = 0x00AB,
    HG = 0x002E,
}
