using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using ClipDrop.Core;
using Microsoft.Win32;

namespace ClipDrop.Windows.Services;

// Owns views, never a second store, clipboard hook, tray or foreground tracker.
public sealed class DockCoordinator : IDisposable
{
    private readonly ShelfSession _session;
    private readonly MainWindow _library;
    private readonly IDisplaySource _displays;
    private readonly Dictionary<string, DockWindow> _views = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _displayChange = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private bool _sync, _disposed, _startHidden;
    private string _layoutSignature = "";
    private string LayoutSignature() => _session.State.Settings.HomeDisplayId + "|" + _session.State.Settings.MirrorDocks + "|" + string.Join(";", _session.State.Settings.Docks.Select(d => $"{d.DisplayId}:{d.Enabled}:{d.Visible}:{d.OffsetX}:{d.OffsetY}"));
    public IReadOnlyCollection<DockWindow> Views => _views.Values;
    public IReadOnlyList<DisplayArea> Displays { get; private set; } = Array.Empty<DisplayArea>();
    public event Action? DisplaysChanged;
    public DockCoordinator(ShelfSession session, MainWindow library, bool startHidden = false, IDisplaySource? displays = null)
    {
        _session = session; _library = library; _displays = displays ?? new DisplayService(); _startHidden = startHidden;
        _displayChange.Tick += (_, _) => { _displayChange.Stop(); Reconcile(); DisplaysChanged?.Invoke(); };
        SystemEvents.DisplaySettingsChanged += OnDisplayChange;
        SystemEvents.PowerModeChanged += OnPowerChange;
        _session.Changed += OnSessionChange;
        _library.Docks = this;
        Reconcile(); _startHidden = false;
    }
    private void OnSessionChange() { if (_layoutSignature != LayoutSignature()) Reconcile(false); }
    private void OnDisplayChange(object? sender, EventArgs e) => ScheduleDisplayChange();
    private void OnPowerChange(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) ScheduleDisplayChange(); }
    private void ScheduleDisplayChange() => _library.Dispatcher.BeginInvoke(() => {
        if (_disposed) return; _displayChange.Stop(); _displayChange.Start();
    });
    public void RefreshDisplays() { Reconcile(); DisplaysChanged?.Invoke(); }
    public void Reconcile(bool reloadDisplays = true)
    {
        if (_disposed || _sync) return;
        _sync = true;
        try
        {
            var oldVisible = _views.Values.Any(v => v.IsVisible);
            if (reloadDisplays) Displays = _displays.GetDisplays();
            var settings = _session.State.Settings;
            var initial = settings.HomeDisplayId.Length == 0;
            var previousRecords = settings.Docks.Count;
            if (initial && settings.DockLeft is double x && settings.DockTop is double y && double.IsFinite(x) && double.IsFinite(y))
            {
                // Interpret legacy WPF coordinates once using a native HWND, then
                // retain only the monitor identity and work-area-relative offsets.
                var probe = new System.Windows.Window { Left = x, Top = y, Width = 200, Height = 60, ShowActivated = false, ShowInTaskbar = false, WindowStyle = System.Windows.WindowStyle.None };
                try { settings.HomeDisplayId = _displays.TargetDisplay(new System.Windows.Interop.WindowInteropHelper(probe).EnsureHandle()) ?? ""; }
                finally { probe.Close(); }
            }
            var plan = DockPlanning.Plan(settings, Displays);
            var activeIds = plan.Select(a => a.Display.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in _views.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                var view = _views[id]; _library.DetachDock(view); _views.Remove(id); view.DisposeView();
            }
            var forceReachable = oldVisible && _views.Values.All(v => !v.IsVisible) && plan.All(a => !a.Layout.Visible);
            foreach (var assignment in plan)
            {
                if (forceReachable) { assignment.Layout.Visible = true; forceReachable = false; _session.Change(false); }
                if (!_views.TryGetValue(assignment.Display.Id, out var view))
                {
                    DockWindow? created = null;
                    created = new DockWindow(_session, () => ShowLibrary(created!), _library.OpenSettings, item => { ShowLibrary(created!); _library.EditItem(item); }, managed: true);
                    view = created; _views.Add(assignment.Display.Id, view);
                    view.GeometryCommitted += SaveGeometry;
                    view.IsVisibleChanged += (_, _) => SaveVisibility(created);
                    view.SizeChanged += (_, _) => QueuePlacement(created);
                    view.Assignment = assignment;
                    // Obtain a native handle before projection so WPF receives the target DPI.
                    WindowPlacement.PlaceOnDisplay(view, assignment.Display, assignment.Layout);
                    if (initial && settings.DockLeft is double left && settings.DockTop is double top && double.IsFinite(left) && double.IsFinite(top))
                    {
                        view.Left = left; view.Top = top; WindowPlacement.Clamp(view);
                        if (!assignment.Temporary) CaptureGeometry(view);
                    }
                }
                if (assignment.Temporary && view.Assignment is { Temporary: true } previous)
                { assignment.Layout.OffsetX = previous.Layout.OffsetX; assignment.Layout.OffsetY = previous.Layout.OffsetY; assignment.Layout.Visible = previous.Layout.Visible; }
                if (_startHidden) assignment.Layout.Visible = false;
                view.Assignment = assignment;
                view.RefreshDock(); Place(view);
                var show = !_startHidden && (assignment.Layout.Visible || assignment.Temporary && oldVisible);
                if (show) view.ShowDock(); else view.HideDock();
                Place(view);
            }
            if (initial || _startHidden || settings.Docks.Count != previousRecords) _session.Change(false);
        }
        finally { _layoutSignature = LayoutSignature(); _sync = false; }
    }
    private void QueuePlacement(DockWindow view)
    {
        if (_sync || _disposed) return;
        view.Dispatcher.BeginInvoke(() => { if (!_disposed && _views.Values.Contains(view)) Place(view); }, DispatcherPriority.Loaded);
    }
    private void Place(DockWindow view)
    {
        if (view.Assignment is { } assignment)
        {
            WindowPlacement.PlaceOnDisplay(view, assignment.Display, assignment.Layout);
            // Center only the first placement. Appending clips grows rightward
            // from this stable anchor instead of re-centering the entire dock.
            if (view.IsVisible && (assignment.Layout.OffsetX is null || assignment.Layout.OffsetY is null))
            { CaptureGeometry(view); _session.Change(false); }
        }
    }
    private void CaptureGeometry(DockWindow view)
    {
        if (view.Assignment is not { } assignment) return;
        var rect = WindowPlacement.PixelBounds(view); var scale = WindowPlacement.Scale(view);
        assignment.Layout.OffsetX = (rect.Left - assignment.Display.Left) / scale;
        assignment.Layout.OffsetY = (rect.Top - assignment.Display.Top) / scale;
    }
    private void SaveGeometry(DockWindow view)
    {
        if (_sync || _disposed) return;
        if (view.Assignment is { } assignment) WindowPlacement.ClampTo(view, assignment.Display);
        CaptureGeometry(view); _session.Change(false);
    }
    private void SaveVisibility(DockWindow view)
    {
        if (_sync || _disposed || view.Assignment is not { } assignment) return;
        assignment.Layout.Visible = view.IsVisible; _session.Change(false);
        if (view.IsVisible) QueuePlacement(view);
    }
    private DockWindow? Target()
    {
        var target = _session.Paster?.CaptureTarget();
        var id = _displays.TargetDisplay(target?.Window ?? IntPtr.Zero);
        return _views.GetValueOrDefault(id ?? "") ?? _views.Values.FirstOrDefault();
    }
    public void ToggleTarget() => Target()?.Toggle();
    public void ShowTarget() => Target()?.ShowDock();
    public void ShowLibrary() { var view = Target(); if (view is not null) ShowLibrary(view); else _library.ShowShelf(); }
    public void ShowLibrary(DockWindow view) { _library.AttachDock(view); _library.ShowShelf(); }
    public void ToggleAll() { if (_views.Values.Any(v => v.IsShown)) HideAll(); else ShowAll(); }
    public void ShowAll() { foreach (var view in _views.Values) view.ShowDock(); }
    public void HideAll() { foreach (var view in _views.Values) view.HideDock(); }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _displayChange.Stop(); SystemEvents.DisplaySettingsChanged -= OnDisplayChange; SystemEvents.PowerModeChanged -= OnPowerChange;
        _session.Changed -= OnSessionChange;
        foreach (var view in _views.Values) { _library.DetachDock(view); view.DisposeView(); }
        _views.Clear();
    }
}
