using LevelDeviceEmulator.Tcp;

var result = await Main(Array.Empty<string>());
return result;

static async Task<int> Main(string[] args)
{
    var server = new TcpServer();
    await server.Start();
    while (true)
    {
        Console.ReadKey();
    }

    return 0;
}

