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
    private EventWaitHandle? _quitSignal;
    private CancellationTokenSource? _stop;
    private DesktopIntegration? _desktop;
    private DesktopPaste? _paste;
    private ShelfSession? _session;
    private DockCoordinator? _docks;
    public bool IsQuitting { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(false, "Local\\NasaatR.ClipDrop");
        try { _ownsMutex = _mutex.WaitOne(0); } catch (AbandonedMutexException) { _ownsMutex = true; }
        _wake = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\NasaatR.ClipDrop.Show");
        _quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\NasaatR.ClipDrop.Quit");
        if (!_ownsMutex) { if (Array.IndexOf(e.Args, "--quit") >= 0) _quitSignal.Set(); else _wake.Set(); Shutdown(); return; }
        if (Array.IndexOf(e.Args, "--quit") >= 0) { Shutdown(); return; }
        var store = new StateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NasaatR", "ClipDrop"));
        try
        {
            var state = store.Load();
            _paste = new DesktopPaste();
            _session = new ShelfSession(store, state) { Paster = _paste };
            var library = new MainWindow(_session);
            _docks = new DockCoordinator(_session, library, state.Settings.StartHidden || Array.IndexOf(e.Args, "--background") >= 0);
            MainWindow = library;
            _session.QuitRequested += () => {
                if (!_session.TrySave()) { library.ShowShelf(); return; }
                IsQuitting = true; Shutdown();
            };
            _desktop = new DesktopIntegration(_docks.ToggleTarget, _docks.ShowTarget, _docks.ShowLibrary, _session.SaveClipboard, library.OpenSettings, _session.RequestQuit, _docks.ShowAll, _docks.HideAll, _docks.ToggleAll);
            _desktop.ShakeEnabled = state.Settings.ShakeToShow;
            _session.Changed += () => _desktop.ShakeEnabled = state.Settings.ShakeToShow;
            if (!_desktop.HotkeyAvailable || !_desktop.LibraryHotkeyAvailable) _session.Notify("A shortcut is in use. Open dock or Library from the tray.");
            _stop = new CancellationTokenSource();
            var token = _stop.Token;
            var wake = _wake; var quitSignal = _quitSignal;
            _ = Task.Run(() => {
                while (true)
                {
                    var command = WaitHandle.WaitAny(new WaitHandle[] { wake, quitSignal, token.WaitHandle });
                    if (command == 0) Dispatcher.BeginInvoke(new Action(_docks.ShowTarget));
                    else if (command == 1) { Dispatcher.BeginInvoke(new Action(_session.RequestQuit)); break; }
                    else break;
                }
            });
            DispatcherUnhandledException += (_, args) => {
                _session.Diagnostics.Record("Unhandled application error", args.Exception);
                MessageBox.Show("ClipDrop encountered an unexpected error. Saved data is preserved. Reopen ClipDrop and copy diagnostics from Settings if the problem continues.", "ClipDrop");
                args.Handled = true; IsQuitting = true; Shutdown(1);
            };
            SessionEnding += (_, _) => _session.TrySave();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            if (Array.IndexOf(e.Args, "--check-startup") < 0) MessageBox.Show("ClipDrop could not open. No saved library has been overwritten.\n\n" + ex.Message, "ClipDrop — Recovery", MessageBoxButton.OK, MessageBoxImage.Error);
            IsQuitting = true; Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _stop?.Cancel(); _desktop?.Dispose(); _docks?.Dispose(); _paste?.Dispose(); _session?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex(); _mutex?.Dispose();
        base.OnExit(e);
    }
}
