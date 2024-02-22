namespace LevelDeviceEmulator.UserInput;

public sealed class UserInput : IDisposable
{
    private CancellationTokenSource _disposeTokenSource;
    private CancellationToken _disposeToken;

    public UserInput()
    {
        _disposeTokenSource = new();
        _disposeToken = _disposeTokenSource.Token;
    }

    public async void Listen()
    {
        while (!_disposeToken.IsCancellationRequested)
        {

        }
    }

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
    }
}