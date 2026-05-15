using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace SpotifyFavoritesTool;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\SpotifyFavoritesTool.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterCriticalBugLogging();

        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out _ownsSingleInstanceMutex);
        if (!_ownsSingleInstanceMutex)
        {
            NativeMethods.SignalExistingInstance();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
    }

    private void RegisterCriticalBugLogging()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CriticalBugLog.Write("DispatcherUnhandledException", e.Exception);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            CriticalBugLog.Write("AppDomain.UnhandledException", exception);
            return;
        }

        CriticalBugLog.Write(
            "AppDomain.UnhandledException",
            new InvalidOperationException($"Unhandled non-exception object: {e.ExceptionObject}"));
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CriticalBugLog.Write("TaskScheduler.UnobservedTaskException", e.Exception);
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
