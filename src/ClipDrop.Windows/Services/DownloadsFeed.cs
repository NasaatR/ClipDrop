using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public sealed class DownloadsFeed : IDisposable
{
    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out IntPtr path);
    public static string DefaultFolder()
    {
        var pointer = IntPtr.Zero;
        try
        {
            var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            if (SHGetKnownFolderPath(in id, 0, IntPtr.Zero, out pointer) == 0)
                return Marshal.PtrToStringUni(pointer) ?? "";
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
        finally { if (pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(pointer); }
    }
    private static readonly SemaphoreSlim Workers = new(1);
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _debounce;
    private readonly CancellationTokenSource _stop = new();
    private FileSystemWatcher? _watcher;
    private bool _disposed, _running, _again;
    private int _settleRetries;
    private int _queued, _reconnect;
    public string Folder { get; }
    public DownloadSnapshot Snapshot { get; private set; } = new(Array.Empty<DownloadFile>(), "Loading recent files…");
    public event Action? Changed;
    public DownloadsFeed(string folder, Dispatcher dispatcher)
    {
        Folder = folder; _dispatcher = dispatcher;
        _debounce = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(1000) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = Refresh(); };
        RequestRefresh();
    }
    private FileSystemWatcher? CreateWatcher()
    {
        if (!Directory.Exists(Folder)) return null;
        try
        {
            var watcher = new FileSystemWatcher(Folder) { IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
            watcher.Changed += OnFile; watcher.Created += OnFile; watcher.Deleted += OnFile; watcher.Renamed += OnFile;
            watcher.Error += (_, _) => Queue(reconnect: true);
            watcher.EnableRaisingEvents = true; return watcher;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        return null;
    }
    private void OnFile(object sender, FileSystemEventArgs e)
    {
        if (RecentDownloads.IsTemporary(e.Name ?? "") &&
            (e is not RenamedEventArgs renamed || RecentDownloads.IsTemporary(renamed.OldName ?? ""))) return;
        Queue();
    }
    private void Queue(bool reconnect = false)
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        if (reconnect) Interlocked.Exchange(ref _reconnect, 1);
        if (Interlocked.Exchange(ref _queued, 1) == 1) return;
        _dispatcher.BeginInvoke(() => {
            Interlocked.Exchange(ref _queued, 0);
            if (_disposed) return;
            if (Interlocked.Exchange(ref _reconnect, 0) == 1) { _watcher?.Dispose(); _watcher = null; }
            _settleRetries = 0; RequestRefresh();
        }, DispatcherPriority.Background);
    }
    public void RequestRefresh()
    {
        if (_disposed) return;
        if (_running) { _again = true; return; }
        if (!_debounce.IsEnabled) _debounce.Start();
    }
    private async Task Refresh()
    {
        if (_disposed || _running) return;
        _running = true;
        try
        {
            var token = _stop.Token;
            var watch = _watcher is null;
            var result = await Task.Run(async () => {
                await Workers.WaitAsync(token);
                try
                {
                    var watcher = watch ? CreateWatcher() : null;
                    try { return (Snapshot: RecentDownloads.Scan(Folder, token), Watcher: watcher); }
                    catch { watcher?.Dispose(); throw; }
                }
                finally { Workers.Release(); }
            }, token);
            if (_disposed) { result.Watcher?.Dispose(); return; }
            if (_watcher is null) _watcher = result.Watcher; else result.Watcher?.Dispose();
            var snapshot = result.Snapshot;
            if (Snapshot.Notice != snapshot.Notice || !System.Linq.Enumerable.SequenceEqual(Snapshot.Files, snapshot.Files))
            { Snapshot = snapshot; Changed?.Invoke(); }
            if (snapshot.Settling && _settleRetries++ < 3) _again = true;
        }
        catch (OperationCanceledException) { }
        finally
        {
            _running = false;
            if (!_disposed && _again) { _again = false; RequestRefresh(); }
        }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _debounce.Stop(); _watcher?.Dispose(); _stop.Cancel();
        // A blocked filesystem call keeps its one worker; cancellation never
        // creates replacement threads or publishes into a disposed view.
    }
}
