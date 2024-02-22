using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Google.Protobuf;
using LevelDeviceEmulator.Tcp.Commands;
using ProtoSharp.Tcp;

namespace LevelDeviceEmulator.Tcp;

public sealed class TcpServer : IDisposable
{
    private readonly CancellationTokenSource _disposeServerTokenSource;
    private readonly CancellationToken _disposeServerToken;

    private TcpListener _tcpListener;
    private Socket _socket;

    private Task _connectionTask;

    private readonly ManualResetEvent _tcpServerStateLock;
    private readonly ManualResetEvent _clientConnected;

    private readonly IPAddress _hostAddress = IPAddress.Parse("127.0.0.1");

    private readonly int _port;
    private static int TaskDelayValue => 20;

    private ServerState TcpServerState { get; set; }

    private enum ServerState : byte
    {
        Init = 0,
        Listening,
        Connecting,
        Connected,
        Disconnected,
        Identified
    }

    private enum CommandParserState : byte
    {
        Configuring,
        Configured
    }

    public TcpServer(int port = 57005)
    {
        _disposeServerTokenSource = new CancellationTokenSource();
        _disposeServerToken = _disposeServerTokenSource.Token;
        _tcpServerStateLock = new ManualResetEvent(true);
        _clientConnected = new ManualResetEvent(true);

        _port = port;
    }

    public async Task Start()
    {
        await ServerTask();
    }

    private async Task ServerTask()
    {
        while (!_disposeServerToken.IsCancellationRequested)
        {
            _tcpServerStateLock.WaitOne();
            _tcpServerStateLock.Reset();

            switch (TcpServerState)
            {
                case ServerState.Init:
                {
                    await InitAsync();
                    TcpServerState = ServerState.Listening;
                    break;
                }
                case ServerState.Listening:
                {
                    await ListenAsync();
                    _clientConnected.Set();
                    TcpServerState = ServerState.Connecting;
                    break;
                }
                case ServerState.Connecting:
                {
                    await HelloSequenceAsync();
                    TcpServerState = ServerState.Connected;

                    break;
                }
                case ServerState.Connected:
                {
                    var configured = await GetConfiguration();

                    if (configured)
                    {
                        TcpServerState = ServerState.Identified;
                    }

                    break;
                }
                case ServerState.Disconnected:
                {
                    TcpServerState = ServerState.Listening;
                    break;
                }
                case ServerState.Identified:
                {
                    break;
                }
                default:
                {
                    break;
                }
            }

            _tcpServerStateLock.Set();

            await DelayedTask(TaskDelayValue, _disposeServerToken);
        }
    }

    private async Task ConnectionCheckingAsync()
    {
        await SocketInitializedAsync();

        while (!_disposeServerToken.IsCancellationRequested)
        {
            _tcpServerStateLock.WaitOne();
            _tcpServerStateLock.Reset();

            if (!await SocketInitializedAsync())
            {
                await DelayedTask(TaskDelayValue, _disposeServerToken);
                _tcpServerStateLock.Set();
                continue;
            }

            if (!await IsConnectedAsync() && _clientConnected.WaitOne(0))
            {
                _clientConnected.Reset();
                TcpServerState = ServerState.Disconnected;
            }

            await DelayedTask(TaskDelayValue, _disposeServerToken);
            _tcpServerStateLock.Set();
        }
    }

    private async Task<bool> SocketInitialized(CancellationToken token)
    {
        while (_socket is null || token.IsCancellationRequested)
        {
            await DelayedTask(TaskDelayValue, token);
        }

        return true;
    }

    private async Task<bool> SocketInitializedAsync()
    {
        return await Task.Run(() => SocketInitialized(_disposeServerToken), _disposeServerToken);
    }

    private bool Init()
    {
        _connectionTask = Task.Run(ConnectionCheckingAsync, _disposeServerToken);
        _tcpListener = new TcpListener(_hostAddress, _port);
        _tcpListener.Start();

        return true;
    }

    private async Task<bool> InitAsync()
    {
        return await Task.Run(Init, _disposeServerToken);
    }

    private async Task ListenAsync()
    {
        _socket = await _tcpListener.AcceptSocketAsync(_disposeServerToken);
    }

    private async Task HelloSequenceAsync()
    {
        await WaitClientHelloRequestAsync(_disposeServerToken);
        await RespondToClientHelloAsync(_disposeServerToken);
    }

    private async Task WaitClientHelloRequestAsync(CancellationToken token)
    {
        var buffer = new byte[512];
        var stream = new NetworkStream(_socket);

        while (!token.IsCancellationRequested)
        {
            var socketDataLength = _socket.Available;
            if (socketDataLength == 0)
            {
                await DelayedTask(TaskDelayValue, token);
                continue;
            }

            if (!stream.CanRead)
            {
                await DelayedTask(TaskDelayValue, token);
                continue;
            }

            var totalRead = stream.ReadAsync(buffer, 0, socketDataLength, token).Result;

            if (totalRead == 0)
            {
                await DelayedTask(TaskDelayValue, token);
                continue;
            }

            var pack = mio_to_mdsp_tcp_pack.Parser.ParseFrom(buffer[..totalRead]);
            if (pack.CommandID != (ulong) TcpCommands.Hello)
            {
                await DelayedTask(TaskDelayValue, token);
                continue;
            }

            await stream.DisposeAsync();
            return;
        }
    }

    private async Task RespondToClientHelloAsync(CancellationToken token)
    {
        var stream = new NetworkStream(_socket);
        var pack = new mdsp_to_mio_tcp_pack
        {
            Hello = new mdsp_to_mio_tcp_hello
            {
                Major = 0,
                Minor = 2
            },
            Status = (uint)Status.Оk
        };

        if (stream.CanWrite)
        {
            await stream.WriteAsync(pack.ToByteArray(), token);
            await stream.DisposeAsync();
        }
    }

    private async Task<bool> GetConfiguration()
    {
        return true;
    }

    private bool IsConnected()
    {
        try
        {
            var result = !(_socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0);
            return result;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private async Task<bool> IsConnectedAsync()
    {
        return await Task.Run(IsConnected, _disposeServerToken);
    }

    private static async Task DelayedTask(int interval, CancellationToken token)
    {
        using var t = Task.Delay(interval, token);
        await t.ConfigureAwait(false);
    }

    public void Dispose()
    {
        _disposeServerTokenSource.Cancel();
        _disposeServerTokenSource.Dispose();
        _socket.Close();
        _socket.Dispose();
    }
}