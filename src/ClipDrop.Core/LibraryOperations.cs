using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClipDrop.Core;

public static class LibraryOperations
{
    public static int Remove(ShelfState state, IEnumerable<string> ids)
    {
        var selected = ids.ToHashSet(); var items = state.Items.Where(i => selected.Contains(i.Id)).ToArray();
        if (state.Trash.Count + items.Length > 50000) throw new InvalidOperationException("Trash is full; items were kept in Library.");
        var batch = Guid.NewGuid().ToString("N"); var now = DateTimeOffset.UtcNow;
        foreach (var item in items) { state.Items.Remove(item); state.Trash.Add(new TrashEntry { Item = item, Batch = batch, DeletedUtc = now }); }
        return items.Length;
    }
    public static int ArchiveUsed(ShelfState state, IEnumerable<string> ids)
    {
        var requested = ids.ToHashSet();
        return Remove(state, state.Items.Where(i => requested.Contains(i.Id) && !i.KeepAfterUse).Select(i => i.Id));
    }
    public static int Restore(ShelfState state, IEnumerable<string> ids)
    {
        var selected = ids.ToHashSet(); var entries = state.Trash.Where(t => selected.Contains(t.Item.Id)).ToArray();
        if (state.Items.Count + entries.Length > 5000) throw new InvalidOperationException("Library limit reached; items remain in Trash.");
        foreach (var entry in entries)
        {
            if (state.Items.Any(i => i.Id == entry.Item.Id)) throw new InvalidDataException("The item already exists.");
        }
        foreach (var entry in entries) { state.Trash.Remove(entry); state.Items.Add(entry.Item); }
        return entries.Length;
    }
    public static int UndoRemove(ShelfState state)
    {
        var batch = state.Trash.LastOrDefault()?.Batch;
        return batch is null ? 0 : Restore(state, state.Trash.Where(t => t.Batch == batch).Select(t => t.Item.Id));
    }
    public static void Rename(ShelfItem item, string title, string group)
    {
        title = title.Trim(); group = group.Trim();
        if (title.Length is < 1 or > 120 || group.Length > 40) throw new InvalidOperationException("Use a name up to 120 characters and group up to 40 characters.");
        item.Title = title; item.Group = group;
    }
}
