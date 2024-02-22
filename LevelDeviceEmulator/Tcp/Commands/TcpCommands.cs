namespace LevelDeviceEmulator.Tcp.Commands;

public enum TcpCommands
{
    Hello       = 0x00001000,
    GetInfo     = 0x00001010,
    SetConfig   = 0x00002000,
    Marker      = 0x00003000,
}

public enum Status
{
    Processing = 0x102,
    Оk = 0x200,
    BadRequest = 0x400,
    NotFound = 0x404,
    NotAcceptable = 0x406,
    InternalError = 0x500
}