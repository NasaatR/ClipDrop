using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClipDrop.Core;

internal static class PanelSetChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var state = new ShelfState();
        var a = ShelfItem.FromText("alpha"); var b = ShelfItem.FromText("beta"); var c = ShelfItem.FromText("gamma");
        a.IsPinned = b.IsPinned = true; a.PinOrder = 1; b.PinOrder = 2; state.Items.AddRange(new[] { a, b, c });
        var main = PanelSets.Active(state); PanelSets.CaptureActive(state);
        var other = PanelSets.Create(state, "Project B", new[] { b.Id, c.Id, b.Id });
        check(state.Items.Count == 3 && other.ClipIds.SequenceEqual(new[] { b.Id, c.Id }) && !a.IsPinned && b.IsPinned && c.IsPinned,
            "Creating a set shares original clips with independent ordered membership and no duplicate IDs");
        PanelSets.Activate(state, main.Id);
        check(a.IsPinned && b.IsPinned && !c.IsPinned && a.PinOrder < b.PinOrder, "Switching sets restores each set's order without deleting Library clips");
        var combined = PanelSets.Merge(state, new[] { main.Id, other.Id }, "Combined");
        check(combined.ClipIds.SequenceEqual(new[] { a.Id, b.Id, c.Id }) && main.ClosedUtc is null && other.ClosedUtc is null && state.Items.Count == 3,
            "Combining sets preserves first-seen order, original payloads and both source sets");
        LibraryOperations.Remove(state, new[] { b.Id }); PanelSets.CaptureActive(state);
        check(combined.ClipIds.SequenceEqual(new[] { a.Id, b.Id, c.Id }) && state.Trash.Single().Item == b,
            "Deleting a clip retains its set membership for later Trash recovery");
        PanelSets.Activate(state, other.Id); LibraryOperations.UndoRemove(state); PanelSets.ApplyActive(state);
        check(!a.IsPinned && b.IsPinned && c.IsPinned && b.PinOrder < c.PinOrder, "Trash restore obeys the active set membership and original relative order");
        c.IsPinned = false; PanelSets.CaptureActive(state); PanelSets.Activate(state, combined.Id); PanelSets.Activate(state, other.Id);
        check(other.ClipIds.SequenceEqual(new[] { b.Id }) && c.IsPinned == false && combined.ClipIds.Contains(c.Id), "Unpinning affects only the active set");
        PanelSets.Close(state, new[] { other.Id });
        check(other.ClosedUtc is not null && PanelSets.Active(state).Id != other.Id && state.Items.Count == 3, "Closing the active set chooses a reachable open set without moving clips to Trash");
        PanelSets.Restore(state, other.Id);
        check(other.ClosedUtc is null && PanelSets.Active(state) == other && b.IsPinned && !a.IsPinned, "Restoring a closed set reopens its exact membership");
        var folder = Path.Combine(root, "panels"); var store = new StateStore(folder); store.Load();
        PanelSets.Close(state, new[] { main.Id }); store.Save(state);
        var reopened = new StateStore(folder).Load();
        check(reopened.Panels.Count == 3 && reopened.Panels.Single(p => p.Id == main.Id).ClosedUtc is not null && PanelSets.Active(reopened).Id == other.Id,
            "Panel names, active identity and recently closed sets survive restart");
        PanelSets.Close(reopened, reopened.Panels.Where(p => p.ClosedUtc is null).Select(p => p.Id).ToArray());
        check(reopened.Panels.Count == 4 && PanelSets.Active(reopened).ClosedUtc is null && PanelSets.Active(reopened).ClipIds.Count == 0 && reopened.Items.Count == 3,
            "Closing every open set leaves a new empty panel and keeps all original clips");
        var before = reopened.Panels.Count;
        var rejected = false; try { PanelSets.Merge(reopened, new[] { other.Id, "missing" }, "Bad"); } catch (InvalidOperationException) { rejected = true; }
        check(rejected && reopened.Panels.Count == before, "An invalid merge is rejected before changing source sets");
        var legacyRoot = Path.Combine(root, "panels-schema4"); Directory.CreateDirectory(legacyRoot);
        var legacy = new ShelfState { SchemaVersion = 4 }; a.IsPinned = b.IsPinned = true; c.IsPinned = false; a.PinOrder = 9; b.PinOrder = 2;
        legacy.Items.AddRange(new[] { a, b, c }); legacy.Settings.DockOpacity = 0;
        var node = JsonNode.Parse(JsonSerializer.Serialize(legacy))!; node.AsObject().Remove("Panels"); node.AsObject().Remove("ActivePanelId");
        var bytes = System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()); File.WriteAllBytes(Path.Combine(legacyRoot, "shelf.json"), bytes);
        var oldStore = new StateStore(legacyRoot); var migrated = oldStore.Load(); oldStore.Save(migrated); oldStore.Save(migrated);
        check(PanelSets.Active(migrated).Name == "Main" && PanelSets.Active(migrated).ClipIds.SequenceEqual(new[] { b.Id, a.Id }) && migrated.Settings.DockOpacity == 0,
            "Schema-4 pins migrate to Main in their existing visual order and retain appearance");
        check(File.ReadAllBytes(Path.Combine(legacyRoot, "shelf.json.schema-4-before-v5.bak")).SequenceEqual(bytes), "Schema-5 upgrade retains immutable original schema-4 bytes after repeated saves");
        var asyncRoot = Path.Combine(root, "async-save"); var asyncStore = new StateStore(asyncRoot); var asyncState = asyncStore.Load();
        asyncState.Items.Add(ShelfItem.FromText("before"));
        var pending = asyncStore.SaveAsync(asyncState); asyncState.Items[0].Content = "after"; pending.GetAwaiter().GetResult();
        check(new StateStore(asyncRoot).Load().Items[0].Content == "before", "Background save writes an immutable snapshot rather than observing later UI edits");
        asyncStore.SaveAsync(asyncState).GetAwaiter().GetResult();
        check(new StateStore(asyncRoot).Load().Items[0].Content == "after", "A later background save persists the newer content");
    }
}
