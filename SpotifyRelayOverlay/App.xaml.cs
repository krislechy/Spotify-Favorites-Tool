using System.Threading;
using System.Windows;

namespace SpotifyRelayOverlay;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\SpotifyRelayOverlay.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out _ownsSingleInstanceMutex);
        if (!_ownsSingleInstanceMutex)
        {
            NativeMethods.SignalExistingInstance();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
