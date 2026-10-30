using System;
using System.Linq;
using System.Windows;
using ClipDrop.Core;
using ClipDrop.Windows.Views;

namespace ClipDrop.Windows.Services;

public sealed partial class DockCoordinator
{
    private DownloadsFeed? _downloadsFeed;
    public DownloadsWindow? DownloadsView { get; private set; }
    private string DownloadsSignature()
    {
        var s = _session.State.Settings; var l = s.DownloadsLayout;
        return $"|downloads:{s.DownloadsEnabled}:{s.EffectiveDownloadsSource}:{s.DownloadsFolder}:{l.DisplayId}:{l.Visible}:{l.OffsetX}:{l.OffsetY}";
    }
    public void OpenDownloads()
    {
        var settings = _session.State.Settings;
        settings.DownloadsEnabled = true; settings.DownloadsLayout.Visible = true;
        _session.Change(); DownloadsView?.ShowStack();
    }
    public void CloseDownloads()
    { _session.State.Settings.DownloadsEnabled = false; _session.Change(); }
    private void ReconcileDownloads()
    {
        var settings = _session.State.Settings;
        if (!settings.DownloadsEnabled || Displays.Count == 0)
        {
            DownloadsView?.DisposeView(); DownloadsView = null;
            _downloadsFeed?.Dispose(); _downloadsFeed = null; return;
        }
        var chrome = settings.EffectiveDownloadsSource == "Chrome";
        var folder = chrome ? ChromeConnection.CatalogFolder : settings.DownloadsFolder.Length > 0 ? settings.DownloadsFolder : DownloadsFeed.DefaultFolder();
        if (_downloadsFeed is null || _downloadsFeed.Folder != folder || _downloadsFeed.IsChrome != chrome)
        {
            DownloadsView?.DisposeView(); DownloadsView = null; _downloadsFeed?.Dispose();
            _downloadsFeed = new DownloadsFeed(folder, _library.Dispatcher, chrome);
        }
        var layout = settings.DownloadsLayout;
        if (layout.DisplayId.Length == 0) { layout.DisplayId = Target()?.Assignment?.Display.Id ?? Displays[0].Id; _session.Change(false); }
        var display = Displays.FirstOrDefault(d => DockPlanning.Same(d.Id, layout.DisplayId));
        var temporary = display is null;
        display ??= Displays.FirstOrDefault(d => d.Primary) ?? Displays[0];
        var assigned = temporary ? new DockLayout { DisplayId = display.Id, Visible = layout.Visible } : layout;
        if (DownloadsView is null)
        {
            var view = new DownloadsWindow(_session, _downloadsFeed!, _library.OpenSettings, CloseDownloads);
            DownloadsView = view;
            view.GeometryCommitted += () => SaveDownloadsGeometry(view);
        }
        else if (temporary && DownloadsView.Assignment is { Temporary: true } old)
        { assigned.OffsetX = old.Layout.OffsetX; assigned.OffsetY = old.Layout.OffsetY; }
        var window = DownloadsView;
        window.Assignment = new(display, assigned, temporary);
        window.Height = Math.Max(180, Math.Min(508, display.Height / WindowPlacement.Scale(window)));
        window.Width = Math.Max(240, Math.Min(344, display.Width / WindowPlacement.Scale(window)));
        if (assigned.OffsetX is null || assigned.OffsetY is null)
        {
            assigned.OffsetX = Math.Max(0, display.Width / WindowPlacement.Scale(window) - window.Width - 16);
            assigned.OffsetY = Math.Max(0, display.Height / WindowPlacement.Scale(window) - window.Height - 16);
            if (!temporary) _session.Change(false);
        }
        WindowPlacement.PlaceOnDisplay(window, display, assigned);
        if (_startHidden) { layout.Visible = assigned.Visible = false; }
        if (!_startHidden && layout.Visible) window.ShowStack(); else window.HideStack();
        WindowPlacement.PlaceOnDisplay(window, display, assigned);
    }
    private void SaveDownloadsGeometry(DownloadsWindow window)
    {
        if (_sync || _disposed || window.Assignment is not { } old) return;
        var bounds = WindowPlacement.PixelBounds(window);
        var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        var display = Displays.FirstOrDefault(d => new Rect(d.Left, d.Top, d.Width, d.Height).Contains(center)) ?? old.Display;
        var layout = _session.State.Settings.DownloadsLayout;
        // Moving a fallback on the same remaining screen preserves the missing
        // screen record; explicitly crossing to another screen retargets it.
        if (old.Temporary && DockPlanning.Same(display.Id, old.Display.Id)) layout = old.Layout;
        else layout.DisplayId = display.Id;
        window.Assignment = new(display, layout, ReferenceEquals(layout, old.Layout) && old.Temporary);
        WindowPlacement.ClampTo(window, display); bounds = WindowPlacement.PixelBounds(window);
        var scale = WindowPlacement.Scale(window);
        layout.OffsetX = (bounds.Left - display.Left) / scale; layout.OffsetY = (bounds.Top - display.Top) / scale;
        _session.Change(false); _layoutSignature = LayoutSignature();
    }
}
