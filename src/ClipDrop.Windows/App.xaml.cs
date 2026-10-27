using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _wake;
    private CancellationTokenSource? _stop;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(false, "Local\\NasaatR.ClipDrop");
        try { _ownsMutex = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsMutex = true; }
        _wake = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\NasaatR.ClipDrop.Show");
        if (!_ownsMutex) { _wake.Set(); Shutdown(); return; }
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NasaatR", "ClipDrop");
        var store = new StateStore(root);
        try
        {
            var state = store.Load();
            var window = new MainWindow(store, state);
            MainWindow = window;
            window.Show();
            if (state.Settings.StartHidden || Array.IndexOf(e.Args, "--background") >= 0) window.Hide();
            _stop = new CancellationTokenSource();
            var token = _stop.Token;
            _ = Task.Run(() => {
                while (WaitHandle.WaitAny(new WaitHandle[] { _wake, token.WaitHandle }) == 0)
                    Dispatcher.BeginInvoke(new Action(window.ShowShelf));
            });
            DispatcherUnhandledException += (_, args) => {
                window.DiagnosticLog.Record("Unhandled application error", args.Exception);
                MessageBox.Show("ClipDrop encountered an unexpected error. Saved data is preserved. Reopen ClipDrop; copy diagnostics from Settings if the problem continues.", "ClipDrop");
                args.Handled = true;
                Shutdown(1);
            };
            SessionEnding += (_, _) => window.TrySave();
        }
        catch (Exception ex)
        {
            MessageBox.Show("ClipDrop could not open its data. No saved shelf has been overwritten.\n\n" + ex.Message,
                "ClipDrop — Recovery", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _stop?.Cancel();
        // Wake handle stays alive until process exit so the worker cannot race disposal.
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
