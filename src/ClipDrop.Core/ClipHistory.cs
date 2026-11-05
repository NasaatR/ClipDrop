using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipDrop.Core;

// History reuses canonical clips and their owned payloads, including Trash.
// No second image/blob copy and no automatic expiration of saved references.
public static class ClipHistory
{
    public static IEnumerable<ShelfItem> Entries(ShelfState state) => state.Items.Concat(state.Trash.Select(t => t.Item)).Where(i => i.InHistory);
    public static void Record(ShelfItem item, bool historyOnly, DateTimeOffset? time = null)
    {
        item.InHistory = true; item.LastSavedUtc = time ?? DateTimeOffset.UtcNow;
        if (!historyOnly) item.HistoryOnly = false;
    }
    public static int Forget(ShelfState state, IEnumerable<string> ids)
    {
        var selected = ids.ToHashSet(); var entries = Entries(state).Where(i => selected.Contains(i.Id)).ToArray();
        var saved = state.Panels.SelectMany(p => p.ClipIds).ToHashSet();
        foreach (var item in entries)
        {
            item.InHistory = false;
            // Only auto-captured, otherwise-unused records can be removed from
            // metadata. Existing Library/Trash/panel membership stays recoverable.
            if (item.HistoryOnly && !item.IsPinned && !saved.Contains(item.Id))
            { state.Items.Remove(item); state.Trash.RemoveAll(t => t.Item.Id == item.Id); }
        }
        return entries.Length;
    }
    public static ShelfItem[] AddToPanel(ShelfState state, IEnumerable<string> ids, string? panelId = null)
    {
        var selected = ids.ToHashSet(); var clips = Entries(state).Where(i => selected.Contains(i.Id)).ToArray();
        var target = panelId ?? state.ActivePanelId;
        if (!state.Panels.Any(p => p.Id == target && p.ClosedUtc is null)) throw new InvalidOperationException("Panel unavailable.");
        LibraryOperations.Restore(state, clips.Select(i => i.Id));
        PanelSets.ApplyActive(state);
        foreach (var item in clips) item.HistoryOnly = false;
        PanelSets.SetMembership(state, target, clips.Select(i => i.Id), true);
        return clips;
    }
}
