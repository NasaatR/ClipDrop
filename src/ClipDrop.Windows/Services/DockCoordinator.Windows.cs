using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public sealed partial class DockCoordinator
{
    private readonly Dictionary<string, DockWindow> _panelViews = new();
    public IReadOnlyCollection<DockWindow> IndependentViews => _panelViews.Values;
    private string WindowSignature() => "|" + string.Join(";", _session.State.Panels.Where(p => p.Window is not null).Select(p => {
        var w = p.Window!; var l = w.Layout;
        return $"{p.Id}:{p.ClosedUtc}:{w.IsOpen}:{w.ClosedUtc}:{l.DisplayId}:{l.Visible}:{l.OffsetX}:{l.OffsetY}";
    }));
    public void OpenPanelWindow(string id)
    {
        if (Displays.Count == 0) { _session.Notify("No display available"); return; }
        var target = Target()?.Assignment?.Display ?? Displays.FirstOrDefault(d => d.Primary) ?? Displays[0];
        _session.PanelAction(() => PanelSets.OpenWindow(_session.State, id, target.Id), "Panel window opened");
        if (_panelViews.TryGetValue(id, out var view)) view.ShowDock();
    }
    public void ClosePanelWindow(string id) =>
        _session.PanelAction(() => PanelSets.CloseWindow(_session.State, id), "Window closed · restore from Recently closed");
    private void ReconcileWindows()
    {
        var plans = _session.State.Panels.Select(p => (Panel: p, Assignment: DockPlanning.PanelWindow(p, Displays)))
            .Where(p => p.Assignment is not null).ToArray();
        var wanted = plans.Select(p => p.Panel.Id).ToHashSet();
        foreach (var id in _panelViews.Keys.Where(id => !wanted.Contains(id)).ToArray())
        {
            var old = _panelViews[id]; _panelViews.Remove(id); _library.DetachDock(old); old.DisposeView();
        }
        var ordinal = 0;
        foreach (var (panel, planned) in plans)
        {
            var assignment = planned!;
            if (!_panelViews.TryGetValue(panel.Id, out var view))
            {
                DockWindow? created = null;
                created = new DockWindow(_session, () => ShowLibrary(created!), _library.OpenSettings,
                    item => { ShowLibrary(created!); _library.EditItem(item); }, managed: true, panelId: panel.Id);
                view = created; _panelViews.Add(panel.Id, view);
                view.CloseWindowRequested = _ => ClosePanelWindow(panel.Id);
                view.OpenPanelsRequested = clips => { _library.AttachDock(created!); _session.ShowPanels(clips); };
                view.GeometryCommitted += SaveGeometry;
                view.IsVisibleChanged += (_, _) => SaveVisibility(created);
                view.SizeChanged += (_, _) => QueuePlacement(created);
                if (assignment.Layout.OffsetX is null && assignment.Layout.OffsetY is null)
                {
                    assignment.Layout.OffsetX = 24 + ordinal * 32;
                    assignment.Layout.OffsetY = 80 + ordinal * 60;
                    _session.Change(false);
                }
            }
            if (assignment.Temporary && view.Assignment is { Temporary: true } previous)
            {
                assignment.Layout.OffsetX = previous.Layout.OffsetX;
                assignment.Layout.OffsetY = previous.Layout.OffsetY;
            }
            if (_startHidden) { assignment.Layout.Visible = false; panel.Window!.Layout.Visible = false; }
            view.Assignment = assignment; view.RefreshDock(); Place(view);
            if (!_startHidden && panel.Window!.Layout.Visible) view.ShowDock(); else view.HideDock();
            Place(view); ordinal++;
        }
    }
    private void RetargetWindowAfterMove(DockWindow view)
    {
        if (view.PanelId is not string id || view.Assignment is not { } old) return;
        var bounds = WindowPlacement.PixelBounds(view); var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        bool Contains(DisplayArea d) => new Rect(d.Left, d.Top, d.Width, d.Height).Contains(center);
        var display = Displays.FirstOrDefault(d => d.Id == old.Display.Id && Contains(d)) ?? Displays.FirstOrDefault(Contains);
        if (display is null || DockPlanning.Same(display.Id, old.Display.Id)) return;
        var layout = _session.State.Panels.Single(p => p.Id == id).Window!.Layout;
        layout.DisplayId = display.Id;
        view.Assignment = new DockAssignment(display, layout);
    }
}
