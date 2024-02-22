

using ProtoSharp.Tcp;

namespace LevelDeviceEmulator.Tcp.Commands;

public sealed class HelloCommand : ITcpCommand
{
    public uint CommandId => (uint) TcpCommands.Hello;
    mdsp_to_mio_tcp_pack pack = new mdsp_to_mio_tcp_pack();
}