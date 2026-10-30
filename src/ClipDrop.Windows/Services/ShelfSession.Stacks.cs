using System;
using System.Collections.Generic;
using System.Linq;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public sealed partial class ShelfSession
{
    private readonly Dictionary<string, DockTile[]> _tileCache = new();
    public DockTile[] DockTiles(string? panelId = null)
    {
        var pins = Pins(panelId); var id = panelId ?? State.ActivePanelId;
        if (_tileCache.TryGetValue(id, out var cached)) return cached;
        return _tileCache[id] = PanelStacks.Tiles(State.Panels.Single(p => p.Id == id), pins);
    }
    public void Stack(IReadOnlyList<ShelfItem> items, string targetId, string? panelId = null)
    {
        try
        {
            if (PanelStacks.Combine(State, panelId ?? State.ActivePanelId, targetId, items.Select(i => i.Id)))
            { Change(); Notify("Clips stacked · click to open · drag to transfer together"); }
        }
        catch (InvalidOperationException ex) { Error("Stack", ex); }
    }
    public void SplitStack(string id, string? panelId = null)
    {
        try { if (PanelStacks.Split(State, panelId ?? State.ActivePanelId, id)) { Change(); Notify("Stack split · clips kept"); } }
        catch (InvalidOperationException ex) { Error("Split stack", ex); }
    }
}
