namespace WindowsTranslator.App.Infrastructure.Windows;

internal sealed class ConsoleShutdownService : IDisposable
{
    private readonly NativeMethods.ConsoleCtrlHandler _handler;
    private bool _attached;
    private bool _registered;
    private bool _shuttingDown;

    public ConsoleShutdownService()
    {
        _handler = HandleConsoleSignal;
    }

    public void Start()
    {
        _attached = NativeMethods.AttachConsole(NativeMethods.AttachParentProcess);
        _registered = NativeMethods.SetConsoleCtrlHandler(_handler, add: true);
    }

    public void Dispose()
    {
        if (_registered)
        {
            NativeMethods.SetConsoleCtrlHandler(_handler, add: false);
            _registered = false;
        }

        if (_attached)
        {
            NativeMethods.FreeConsole();
            _attached = false;
        }
    }

    private bool HandleConsoleSignal(NativeMethods.ConsoleCtrlEvent ctrlType)
    {
        if (ctrlType is not (NativeMethods.ConsoleCtrlEvent.CtrlC or NativeMethods.ConsoleCtrlEvent.CtrlBreak))
        {
            return false;
        }

        if (_shuttingDown)
        {
            return true;
        }

        _shuttingDown = true;
        var application = System.Windows.Application.Current;
        if (application is null)
        {
            Environment.Exit(0);
            return true;
        }

        application.Dispatcher.BeginInvoke(() => application.Shutdown());
        return true;
    }
}
