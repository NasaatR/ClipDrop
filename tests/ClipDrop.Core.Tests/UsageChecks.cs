using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClipDrop.Core;

internal static class UsageChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var state = new ShelfState();
        var clips = Enumerable.Range(0, 5).Select(i => ShelfItem.FromText("clip " + i)).ToArray();
        for (var i = 0; i < clips.Length; i++) { clips[i].IsPinned = true; clips[i].PinOrder = i + 1; }
        state.Items.AddRange(clips); clips[1].KeepAfterUse = true;
        check(LibraryOperations.ArchiveUsed(state, new[] { clips[0].Id, clips[1].Id, clips[0].Id }) == 1 && state.Items.Contains(clips[1]) && state.Trash.Single().Item.Id == clips[0].Id,
            "Automatic use cleanup archives only unprotected clips once");
        check(LibraryOperations.UndoRemove(state) == 1 && state.Items.Contains(clips[0]), "Used clips remain recoverable through persistent Trash undo");
        LibraryOperations.Remove(state, new[] { clips[1].Id }); LibraryOperations.UndoRemove(state);
        check(clips[1].KeepAfterUse, "Explicit deletion is allowed for kept clips and restore preserves protection");
        PinOrdering.Move(state, new[] { clips[1].Id, clips[3].Id }, 5);
        var order = state.Items.Where(i => i.IsPinned).OrderBy(i => i.PinOrder).Select(i => i.Id).ToArray();
        check(order.SequenceEqual(new[] { clips[0].Id, clips[2].Id, clips[4].Id, clips[1].Id, clips[3].Id }), "Moving a noncontiguous group preserves its relative order and adjusts the insertion slot");
        check(!PinOrdering.Move(state, new[] { clips[1].Id, clips[3].Id }, 5), "Dropping a group into its existing slot is a no-op");
        var unpinned = ShelfItem.FromText("Library-only clip"); state.Items.Add(unpinned);
        PinOrdering.Move(state, new[] { unpinned.Id }, 0);
        check(unpinned.IsPinned && state.Items.Where(i => i.IsPinned).OrderBy(i => i.PinOrder).First() == unpinned, "Dragging a Library clip into the dock inserts it at the requested position");
        var selection = new ClipSelection(); var ids = clips.Select(i => i.Id).ToArray();
        selection.Click(ids, ids[1], true, false, false); selection.Click(ids, ids[3], false, true, false);
        check(selection.Ids.SetEquals(ids.Skip(1).Take(3)), "Shift-click selects the visible range from the selection anchor");
        selection.Click(ids, ids[2], true, false, false);
        check(selection.Ids.SetEquals(new[] { ids[1], ids[3] }), "Ctrl-click toggles a member without discarding the rest of the group");
        selection.Trim(new[] { ids[1] }); check(selection.Ids.SetEquals(new[] { ids[1] }), "Removing clips prunes transient selection");
        var folder = Path.Combine(root, "usage-schema3"); Directory.CreateDirectory(folder);
        state.SchemaVersion = 3; var before = JsonSerializer.SerializeToUtf8Bytes(state); File.WriteAllBytes(Path.Combine(folder, "shelf.json"), before);
        var store = new StateStore(folder); var loaded = store.Load(); store.Save(loaded); store.Save(loaded);
        var reopened = new StateStore(folder).Load();
        check(reopened.SchemaVersion == ShelfState.CurrentSchema && reopened.Items.Single(i => i.Id == clips[1].Id).KeepAfterUse && reopened.Items.Where(i => i.IsPinned).OrderBy(i => i.PinOrder).Select(i => i.Id).SequenceEqual(state.Items.Where(i => i.IsPinned).OrderBy(i => i.PinOrder).Select(i => i.Id)),
            "Schema-3 migration preserves ordered pins and keep-after-use flags through restart");
        check(File.ReadAllBytes(Path.Combine(folder, "shelf.json.schema-3-before-v" + ShelfState.CurrentSchema + ".bak")).SequenceEqual(before), "Schema migration keeps the immutable pre-upgrade bytes after repeated saves");
    }
}
