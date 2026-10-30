using System;
using System.Collections.Generic;
using System.Linq;
using ClipDrop.Core;
using ClipDrop.Windows.Views;

namespace ClipDrop.Windows.Services;

public sealed partial class DockCoordinator
{
    private DownloadsFeed? _downloadsFeed;
    private readonly Dictionary<string, DownloadsWindow> _downloadsViews = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<DownloadsWindow> DownloadsViews => _downloadsViews.Values.ToArray();
    public DownloadsWindow? DownloadsView => _downloadsViews.GetValueOrDefault(_session.State.Settings.DownloadsLayout.DisplayId) ?? _downloadsViews.Values.FirstOrDefault();
    private string DownloadsSignature()
    {
        var s = _session.State.Settings; var l = s.DownloadsLayout;
        return $"|downloads:{s.DownloadsEnabled}:{s.MirrorDownloads}:{s.EffectiveDownloadsSource}:{s.DownloadsFolder}:{l.DisplayId}:{l.Visible}:{l.OffsetX}:{l.OffsetY}|" +
            string.Join(";", s.DownloadsLayouts.Select(d => $"{d.DisplayId}:{d.Visible}:{d.OffsetX}:{d.OffsetY}"));
    }
    public void OpenDownloads()
    {
        var settings = _session.State.Settings;
        settings.DownloadsEnabled = true; settings.DownloadsLayout.Visible = true;
        foreach (var layout in settings.DownloadsLayouts) layout.Visible = true;
        _session.Change();
        foreach (var view in DownloadsViews) view.ShowStack();
    }
    public void CloseDownloads()
    { _session.State.Settings.DownloadsEnabled = false; _session.Change(); }
    private void DisposeDownloadsViews()
    {
        foreach (var window in DownloadsViews) window.DisposeView();
        _downloadsViews.Clear();
    }
    private static void FitDownloads(DownloadsWindow window, DisplayArea display)
    {
        var scale = WindowPlacement.Scale(window);
        window.Height = Math.Max(1, Math.Min(508, display.Height / scale));
        window.Width = Math.Max(1, Math.Min(344, display.Width / scale));
        window.UpdateLayout();
    }
    private void ReconcileDownloads()
    {
        var settings = _session.State.Settings;
        if (!settings.DownloadsEnabled || Displays.Count == 0)
        {
            DisposeDownloadsViews(); _downloadsFeed?.Dispose(); _downloadsFeed = null; return;
        }
        var chrome = settings.EffectiveDownloadsSource == "Chrome";
        var folder = chrome ? ChromeConnection.CatalogFolder : settings.DownloadsFolder.Length > 0 ? settings.DownloadsFolder : DownloadsFeed.DefaultFolder();
        if (_downloadsFeed is null || !string.Equals(_downloadsFeed.Folder, folder, StringComparison.OrdinalIgnoreCase) || _downloadsFeed.IsChrome != chrome)
        {
            DisposeDownloadsViews(); _downloadsFeed?.Dispose();
            _downloadsFeed = new DownloadsFeed(folder, _library.Dispatcher, chrome);
        }
        var home = settings.DownloadsLayout;
        var changed = false;
        if (home.DisplayId.Length == 0)
        { home.DisplayId = Target()?.Assignment?.Display.Id ?? Displays[0].Id; changed = true; }
        var records = settings.DownloadsLayouts.Count;
        var plan = DockPlanning.Downloads(settings, Displays);
        changed |= records != settings.DownloadsLayouts.Count;
        var wanted = plan.Select(a => a.Display.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in _downloadsViews.Keys.Where(id => !wanted.Contains(id)).ToArray())
        { var old = _downloadsViews[id]; _downloadsViews.Remove(id); old.DisposeView(); }
        foreach (var assignment in plan)
        {
            if (!_downloadsViews.TryGetValue(assignment.Display.Id, out var window))
            {
                window = new DownloadsWindow(_session, _downloadsFeed, _library.OpenSettings, CloseDownloads);
                _downloadsViews.Add(assignment.Display.Id, window);
                var captured = window;
                window.GeometryCommitted += () => SaveDownloadsGeometry(captured);
                window.MoveCompleted += () => CompleteMove();
                window.IsVisibleChanged += (_, _) => SaveDownloadsVisibility(captured);
            }
            else if (assignment.Temporary && window.Assignment is { Temporary: true } old)
            { assignment.Layout.OffsetX = old.Layout.OffsetX; assignment.Layout.OffsetY = old.Layout.OffsetY; assignment.Layout.Visible = old.Layout.Visible; }
            window.Assignment = assignment;
            var display = assignment.Display; var layout = assignment.Layout;
            // Prime the target HWND DPI before deriving DIP dimensions/default offsets.
            WindowPlacement.PlaceOnDisplay(window, display, layout);
            var scale = WindowPlacement.Scale(window);
            FitDownloads(window, display);
            if (layout.OffsetX is null || layout.OffsetY is null)
            {
                layout.OffsetX ??= Math.Max(0, display.Width / scale - window.Width - 16);
                layout.OffsetY ??= Math.Max(0, display.Height / scale - window.Height - 16);
                changed |= !assignment.Temporary;
            }
            if (_startHidden) { home.Visible = layout.Visible = false; changed = true; }
            WindowPlacement.PlaceOnDisplay(window, display, layout);
            if (!_startHidden && layout.Visible) { if (!window.IsVisible) window.ShowStack(); }
            else if (window.IsVisible) window.HideStack();
            WindowPlacement.PlaceOnDisplay(window, display, layout);
        }
        if (changed) _session.Change(false);
    }
    private void SaveDownloadsVisibility(DownloadsWindow window)
    {
        if (_sync || _disposed || window.Assignment is not { } a) return;
        a.Layout.Visible = window.IsVisible;
        _layoutSignature = LayoutSignature(); _session.Change(false);
    }
    private void SaveDownloadsGeometry(DownloadsWindow window)
    {
        if (_sync || _disposed || window.Assignment is not { } old) return;
        var settings = _session.State.Settings;
        var display = settings.MirrorDownloads ? old.Display : MovedDisplay(window, old.Display.Id) ?? old.Display;
        var layout = old.Layout;
        if (!settings.MirrorDownloads && !(old.Temporary && DockPlanning.Same(display.Id, old.Display.Id)))
        {
            layout = settings.DownloadsLayout; layout.DisplayId = display.Id;
            var key = _downloadsViews.First(p => ReferenceEquals(p.Value, window)).Key;
            if (_downloadsViews.TryGetValue(display.Id, out var displaced) && !ReferenceEquals(displaced, window))
            { _downloadsViews.Remove(display.Id); displaced.DisposeView(); }
            _downloadsViews.Remove(key); _downloadsViews[display.Id] = window;
        }
        var temporary = old.Temporary && ReferenceEquals(layout, old.Layout) && DockPlanning.Same(display.Id, old.Display.Id);
        window.Assignment = new(display, layout, temporary);
        FitDownloads(window, display);
        WindowPlacement.ClampTo(window, display);
        var bounds = WindowPlacement.PixelBounds(window); var scale = WindowPlacement.Scale(window);
        layout.OffsetX = (bounds.Left - display.Left) / scale; layout.OffsetY = (bounds.Top - display.Top) / scale;
        if (settings.MirrorDownloads)
        {
            // Turning mirroring off keeps the last moved copy as the single stack.
            var home = settings.DownloadsLayout;
            home.DisplayId = display.Id; home.OffsetX = layout.OffsetX; home.OffsetY = layout.OffsetY; home.Visible = layout.Visible;
        }
        else if (!temporary)
        {
            // Keep the corresponding mirror position current when moving in single mode.
            var record = settings.DownloadsLayouts.FirstOrDefault(l => DockPlanning.Same(l.DisplayId, display.Id));
            if (record is not null) { record.OffsetX = layout.OffsetX; record.OffsetY = layout.OffsetY; record.Visible = layout.Visible; }
        }
        _layoutSignature = LayoutSignature(); _session.Change(false); RecordMove(window, "Downloads"); DisplaysChanged?.Invoke();
    }
}
