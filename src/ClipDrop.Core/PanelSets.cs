using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipDrop.Core;

// Payloads belong to Library/Trash. Sets hold references; IsPinned/PinOrder are
// the active set's projection, retained for the existing dock/transfer contracts.
public static class PanelSets
{
    public const int Maximum = 100;
    public const int MaximumWindows = 12;
    public static PanelSet Active(ShelfState state) => state.Panels.Single(p => p.Id == state.ActivePanelId && p.ClosedUtc is null);
    public static void Migrate(ShelfState state)
    {
        var main = new PanelSet { Name = "Main", ClipIds = state.Items.Concat(state.Trash.Select(t => t.Item)).Where(i => i.IsPinned).OrderBy(i => i.PinOrder).Select(i => i.Id).ToList() };
        state.Panels = new() { main }; state.ActivePanelId = main.Id;
    }
    public static void CaptureActive(ShelfState state)
    {
        var active = Active(state);
        var pins = state.Items.Where(i => i.IsPinned).OrderBy(i => i.PinOrder).Select(i => i.Id).ToList();
        // Keep deleted references at their previous slots, so restoring Trash
        // recovers membership without resurrecting a manually unpinned clip.
        if (state.Trash.Count > 0)
        {
            var deleted = state.Trash.Select(t => t.Item.Id).ToHashSet();
            for (var i = 0; i < active.ClipIds.Count; i++)
                if (deleted.Contains(active.ClipIds[i])) pins.Insert(Math.Min(i, pins.Count), active.ClipIds[i]);
        }
        if (!active.ClipIds.SequenceEqual(pins)) active.ClipIds = pins;
        PanelStacks.Prune(active);
        if (PanelStacks.Normalize(active)) ApplyActive(state);
    }
    public static ShelfItem[] Items(ShelfState state, string? panelId = null)
    {
        if (panelId is null || panelId == state.ActivePanelId)
            return state.Items.Where(i => i.IsPinned).OrderBy(i => i.PinOrder).ToArray();
        var panel = Find(state, panelId);
        var live = state.Items.ToDictionary(i => i.Id);
        return panel.ClipIds.Where(live.ContainsKey).Select(id => live[id]).ToArray();
    }
    private static PanelSet Find(ShelfState state, string id) =>
        state.Panels.SingleOrDefault(p => p.Id == id && p.ClosedUtc is null) ?? throw new InvalidOperationException("Panel unavailable.");
    public static bool SetMembership(ShelfState state, string panelId, IEnumerable<string> ids, bool add)
    {
        var panel = Find(state, panelId);
        var requested = ids.Distinct().ToArray(); var live = state.Items.Select(i => i.Id).ToHashSet();
        if (requested.Any(id => !live.Contains(id))) throw new InvalidOperationException("A clip is no longer in Library.");
        CaptureActive(state);
        var next = panel.ClipIds.ToList();
        if (add) { var known = next.ToHashSet(); foreach (var id in requested) if (known.Add(id)) next.Add(id); }
        else { var selected = requested.ToHashSet(); next.RemoveAll(selected.Contains); }
        if (panel.ClipIds.SequenceEqual(next)) return false;
        panel.ClipIds = next;
        PanelStacks.Prune(panel);
        PanelStacks.Normalize(panel);
        if (panelId == state.ActivePanelId) ApplyActive(state);
        return true;
    }
    public static bool Move(ShelfState state, string panelId, IEnumerable<string> ids, int insertionIndex)
    {
        var panel = Find(state, panelId);
        var selectedIds = ids.Distinct().ToArray();
        var liveIds = state.Items.Select(i => i.Id).ToHashSet();
        if (selectedIds.Any(id => !liveIds.Contains(id))) throw new InvalidOperationException("A clip is no longer in Library.");
        var detached = PanelStacks.DetachPartial(state, panel, selectedIds.ToHashSet());
        if (panelId == state.ActivePanelId)
        {
            var changed = PinOrdering.Move(state, selectedIds, insertionIndex); CaptureActive(state); return changed || detached;
        }
        var requested = selectedIds; var selected = requested.ToHashSet();
        var live = state.Items.Select(i => i.Id).ToHashSet();
        if (requested.Any(id => !live.Contains(id))) throw new InvalidOperationException("A clip is no longer in Library.");
        var before = panel.ClipIds.Where(live.Contains).ToList();
        var moving = before.Where(selected.Contains).Concat(requested.Where(id => !before.Contains(id))).ToArray();
        var slot = Math.Clamp(insertionIndex, 0, before.Count);
        var adjusted = slot - before.Take(slot).Count(selected.Contains);
        var next = before.Where(id => !selected.Contains(id)).ToList(); next.InsertRange(adjusted, moving);
        if (before.SequenceEqual(next)) return detached;
        var deleted = state.Trash.Select(t => t.Item.Id).ToHashSet();
        for (var i = 0; i < panel.ClipIds.Count; i++)
            if (deleted.Contains(panel.ClipIds[i])) next.Insert(Math.Min(i, next.Count), panel.ClipIds[i]);
        panel.ClipIds = next; PanelStacks.Normalize(panel); return true;
    }
    public static void OpenWindow(ShelfState state, string id, string displayId)
    {
        var panel = state.Panels.SingleOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Panel unavailable.");
        if (panel.Window?.IsOpen != true && state.Panels.Count(p => p.Window?.IsOpen == true) >= MaximumWindows)
            throw new InvalidOperationException("Up to 12 independent panel windows can be open.");
        if (string.IsNullOrWhiteSpace(displayId) || displayId.Length > 1024) throw new InvalidOperationException("Display unavailable.");
        panel.ClosedUtc = null; panel.Window ??= new PanelWindowState();
        panel.Window.IsOpen = true; panel.Window.ClosedUtc = null; panel.Window.Layout.Visible = true;
        if (panel.Window.Layout.DisplayId.Length == 0) panel.Window.Layout.DisplayId = displayId;
    }
    public static void CloseWindow(ShelfState state, string id)
    {
        var panel = state.Panels.SingleOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Panel unavailable.");
        if (panel.Window is not { IsOpen: true } window) return;
        window.IsOpen = false; window.ClosedUtc = DateTimeOffset.UtcNow;
    }
    public static void ApplyActive(ShelfState state)
    {
        var order = Active(state).ClipIds.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => (long)p.index + 1);
        foreach (var item in state.Items)
        { item.IsPinned = order.TryGetValue(item.Id, out var index); if (item.IsPinned) item.PinOrder = index; }
    }
    public static void Activate(ShelfState state, string id)
    {
        if (!state.Panels.Any(p => p.Id == id && p.ClosedUtc is null)) throw new InvalidOperationException("Choose an open panel.");
        CaptureActive(state); state.ActivePanelId = id; ApplyActive(state);
    }
    private static string Name(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 60) throw new InvalidOperationException("Use a panel name up to 60 characters.");
        return name;
    }
    public static PanelSet Create(ShelfState state, string name, IEnumerable<string>? ids = null, string? sourcePanelId = null)
    {
        var title = Name(name);
        if (state.Panels.Count >= Maximum) throw new InvalidOperationException("Panel limit reached (100 including recently closed).");
        var clips = ids?.Distinct().ToList() ?? new List<string>();
        var available = state.Items.Select(i => i.Id).Concat(state.Trash.Select(t => t.Item.Id)).ToHashSet();
        if (clips.Any(id => !available.Contains(id))) throw new InvalidOperationException("A clip is no longer available.");
        CaptureActive(state);
        var source = state.Panels.FirstOrDefault(p => p.Id == (sourcePanelId ?? state.ActivePanelId));
        var panel = new PanelSet { Name = title, ClipIds = clips }; state.Panels.Add(panel);
        if (source is not null) PanelStacks.Copy(new[] { source }, panel);
        state.ActivePanelId = panel.Id; ApplyActive(state); return panel;
    }
    public static PanelSet Merge(ShelfState state, IEnumerable<string> ids, string name)
    {
        var requested = ids.Distinct().ToArray();
        if (requested.Length < 2 || requested.Any(id => state.Panels.All(p => p.Id != id))) throw new InvalidOperationException("Select at least two available panels.");
        Name(name); if (state.Panels.Count >= Maximum) throw new InvalidOperationException("Panel limit reached (100 including recently closed).");
        CaptureActive(state);
        var sources = requested.Select(id => state.Panels.Single(p => p.Id == id)).ToArray();
        var merged = Create(state, name, sources.SelectMany(p => p.ClipIds));
        PanelStacks.Copy(sources, merged); ApplyActive(state); return merged;
    }
    public static void Rename(ShelfState state, string id, string name)
    {
        var title = Name(name);
        var panel = state.Panels.SingleOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Panel unavailable.");
        panel.Name = title;
    }
    public static void Close(ShelfState state, IEnumerable<string> ids)
    {
        var selected = ids.ToHashSet(); var closing = state.Panels.Where(p => p.ClosedUtc is null && selected.Contains(p.Id)).ToArray();
        if (closing.Length == 0) return;
        var remaining = state.Panels.FirstOrDefault(p => p.ClosedUtc is null && !selected.Contains(p.Id));
        if (remaining is null && state.Panels.Count >= Maximum) throw new InvalidOperationException("Keep one panel open at the panel limit.");
        CaptureActive(state);
        var now = DateTimeOffset.UtcNow; foreach (var panel in closing) { panel.ClosedUtc = now; CloseWindow(state, panel.Id); }
        if (remaining is null) { remaining = new PanelSet { Name = "Main" }; state.Panels.Add(remaining); }
        if (closing.Any(p => p.Id == state.ActivePanelId)) { state.ActivePanelId = remaining.Id; ApplyActive(state); }
    }
    public static void Restore(ShelfState state, string id)
    {
        var panel = state.Panels.SingleOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Panel unavailable.");
        panel.ClosedUtc = null; Activate(state, id);
    }
}
