using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipDrop.Core;

// Payloads belong to Library/Trash. Sets hold references; IsPinned/PinOrder are
// the active set's projection, retained for the existing dock/transfer contracts.
public static class PanelSets
{
    public const int Maximum = 100;
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
    public static PanelSet Create(ShelfState state, string name, IEnumerable<string>? ids = null)
    {
        var title = Name(name);
        if (state.Panels.Count >= Maximum) throw new InvalidOperationException("Panel limit reached (100 including recently closed).");
        var clips = ids?.Distinct().ToList() ?? new List<string>();
        var available = state.Items.Select(i => i.Id).Concat(state.Trash.Select(t => t.Item.Id)).ToHashSet();
        if (clips.Any(id => !available.Contains(id))) throw new InvalidOperationException("A clip is no longer available.");
        CaptureActive(state);
        var panel = new PanelSet { Name = title, ClipIds = clips }; state.Panels.Add(panel);
        state.ActivePanelId = panel.Id; ApplyActive(state); return panel;
    }
    public static PanelSet Merge(ShelfState state, IEnumerable<string> ids, string name)
    {
        var requested = ids.Distinct().ToArray();
        if (requested.Length < 2 || requested.Any(id => state.Panels.All(p => p.Id != id))) throw new InvalidOperationException("Select at least two available panels.");
        Name(name); if (state.Panels.Count >= Maximum) throw new InvalidOperationException("Panel limit reached (100 including recently closed).");
        CaptureActive(state);
        return Create(state, name, requested.SelectMany(id => state.Panels.Single(p => p.Id == id).ClipIds));
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
        var now = DateTimeOffset.UtcNow; foreach (var panel in closing) panel.ClosedUtc = now;
        if (remaining is null) { remaining = new PanelSet { Name = "Main" }; state.Panels.Add(remaining); }
        if (closing.Any(p => p.Id == state.ActivePanelId)) { state.ActivePanelId = remaining.Id; ApplyActive(state); }
    }
    public static void Restore(ShelfState state, string id)
    {
        var panel = state.Panels.SingleOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Panel unavailable.");
        panel.ClosedUtc = null; Activate(state, id);
    }
}
