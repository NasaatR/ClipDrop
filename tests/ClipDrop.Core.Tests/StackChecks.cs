using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClipDrop.Core;

internal static class StackChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var state = new ShelfState(); var panel = PanelSets.Active(state);
        var items = new[] { "a", "b", "c", "d" }.Select(ShelfItem.FromText).ToArray(); state.Items.AddRange(items);
        for (var i = 0; i < items.Length; i++) { items[i].IsPinned = true; items[i].PinOrder = i + 1; }
        PanelSets.CaptureActive(state);
        PanelStacks.Combine(state, panel.Id, items[1].Id, new[] { items[3].Id, items[0].Id });
        var stack = panel.Stacks.Single(); var stackId = stack.Id;
        check(panel.ClipIds.SequenceEqual(new[] { items[1].Id, items[3].Id, items[0].Id, items[2].Id }) && state.Items.Count == 4,
            "Stacking preserves target position, incoming order and every original payload");
        var tiles = PanelStacks.Tiles(panel, PanelSets.Items(state));
        check(tiles.Length == 2 && tiles[0].StackId == stackId && tiles[0].Items.Length == 3 && tiles[1].Items[0] == items[2],
            "A flat stack projects as one ordered tile alongside standalone clips");
        check(!PanelStacks.Combine(state, panel.Id, items[1].Id, new[] { items[0].Id, items[1].Id }) && stack.ClipIds.Count == 3,
            "Dropping a stack into itself is a duplicate-free no-op");
        PanelSets.Move(state, panel.Id, stack.ClipIds.ToArray(), 4);
        check(panel.ClipIds[0] == items[2].Id && panel.Stacks.Single().Id == stackId,
            "Moving a complete stack preserves its identity and internal order");
        LibraryOperations.Remove(state, new[] { items[1].Id }); PanelSets.CaptureActive(state);
        check(stack.ClipIds.Contains(items[1].Id) && PanelStacks.Tiles(panel, PanelSets.Items(state)).Last().Items.Length == 2,
            "Trash hides a stack member while retaining its recovery membership");
        LibraryOperations.UndoRemove(state); PanelSets.ApplyActive(state);
        check(PanelStacks.Tiles(panel, PanelSets.Items(state)).Last().Items[0] == items[1] && panel.Stacks.Single().Id == stackId,
            "Trash restore recovers a stack member at its original internal slot");
        check(PanelSets.Move(state, panel.Id, new[] { items[0].Id }, 4) && !stack.ClipIds.Contains(items[0].Id),
            "Moving one member out of a stack persists detachment even at an unchanged numeric slot");
        var snapshot = PanelSets.Create(state, "Snapshot", panel.ClipIds, panel.Id);
        check(snapshot.Stacks.Single().Id != stackId && snapshot.Stacks.Single().ClipIds.SequenceEqual(stack.ClipIds),
            "Saving a panel copies stack structure with independent group identities");
        PanelSets.Activate(state, panel.Id);
        var other = PanelSets.Create(state, "Other", new[] { items[2].Id, items[0].Id }); PanelSets.Activate(state, panel.Id);
        var mainOrder = panel.ClipIds.ToArray();
        PanelStacks.Combine(state, other.Id, items[2].Id, new[] { items[0].Id });
        check(panel.ClipIds.SequenceEqual(mainOrder) && other.Stacks.Count == 1 && state.ActivePanelId == panel.Id,
            "Stacking in an independent set leaves the main panel projection untouched");
        var allBefore = state.Items.Select(i => i.Id).ToArray(); PanelStacks.Split(state, other.Id, other.Stacks[0].Id);
        check(other.Stacks.Count == 0 && other.ClipIds.Count == 2 && state.Items.Select(i => i.Id).SequenceEqual(allBefore) && state.Trash.Count == 0,
            "Splitting a stack changes only its grouping, without deleting or archiving clips");
        var store = new StateStore(Path.Combine(root, "stacks")); store.Load(); store.Save(state);
        var saved = new StateStore(store.Root).Load();
        check(saved.Panels.Single(p => p.Id == panel.Id).Stacks.Single().Id == stackId,
            "Stack identities and membership survive persistence and restart");
        var legacy = JsonNode.Parse(JsonSerializer.Serialize(state))!; legacy["SchemaVersion"] = 6;
        foreach (var p in legacy["Panels"]!.AsArray()) p!.AsObject().Remove("Stacks");
        var oldRoot = Path.Combine(root, "stacks-schema6"); Directory.CreateDirectory(oldRoot);
        var bytes = System.Text.Encoding.UTF8.GetBytes(legacy.ToJsonString()); File.WriteAllBytes(Path.Combine(oldRoot, "shelf.json"), bytes);
        var oldStore = new StateStore(oldRoot); var migrated = oldStore.Load(); oldStore.Save(migrated); oldStore.Save(migrated);
        check(migrated.SchemaVersion == 7 && migrated.Panels.All(p => p.Stacks.Count == 0) && migrated.ActivePanelId == state.ActivePanelId &&
            File.ReadAllBytes(Path.Combine(oldRoot, "shelf.json.schema-6-before-v7.bak")).SequenceEqual(bytes),
            "Schema-6 migration preserves existing panels and exact original bytes before introducing stacks");
        var badRoot = Path.Combine(root, "bad-stacks"); Directory.CreateDirectory(badRoot);
        var bad = JsonNode.Parse(JsonSerializer.Serialize(state))!;
        bad["Panels"]![0]!["Stacks"]![0]!["ClipIds"]!.AsArray().Add(Guid.NewGuid().ToString("N"));
        var damaged = bad.ToJsonString(); File.WriteAllText(Path.Combine(badRoot, "shelf.json"), damaged);
        var rejected = false; try { new StateStore(badRoot).Load(); } catch (InvalidDataException) { rejected = true; }
        check(rejected && File.ReadAllText(Path.Combine(badRoot, "shelf.json")) == damaged,
            "Malformed stack references are rejected while preserving the original metadata");
        check(MediaFiles.IsImage("photo.WEBP") && MediaFiles.IsImage("photo.avif") && !MediaFiles.IsImage("photo.webp.txt"),
            "Common raster types including WebP and AVIF are classified as images without extension substring mistakes");
        var html = "SourceURL:https://www.pinterest.com/pin/123/\r\n<html><a href='https://www.pinterest.com/pin/123/'><img src='https://i.pinimg.com/236x/small.webp' srcset='https://i.pinimg.com/236x/small.webp 236w, https://i.pinimg.com/originals/full.webp?x=1&amp;y=2 1200w'></a></html>";
        check(ImageSourceHints.Find(html, "https://www.pinterest.com/pin/123/")?.AbsoluteUri == "https://i.pinimg.com/originals/full.webp?x=1&y=2",
            "Browser HTML chooses the actual largest image source rather than the surrounding Pinterest page link");
        check(ImageSourceHints.Find(null, "https://www.pinterest.com/pin/123/") is null && ImageSourceHints.Find(null, "https://cdn.example.com/a.webp?width=900") is not null,
            "Page-only URLs remain links while direct image URLs are recognized");
        check(ImageSourceHints.Find("<img src='javascript:alert(1)'>", null) is null && !ImageSourceHints.Allowed(new Uri("https://user:pass@example.com/image.png")),
            "Image source hints reject executable URLs and embedded credentials");
        check(new[] { "127.0.0.1", "10.0.0.1", "192.168.0.1", "169.254.1.1", "::1", "fc00::1", "::ffff:127.0.0.1" }.All(a => !ImageSourceHints.PublicAddress(IPAddress.Parse(a))) &&
            ImageSourceHints.PublicAddress(IPAddress.Parse("8.8.8.8")) && ImageSourceHints.PublicAddress(IPAddress.Parse("2606:4700:4700::1111")),
            "Image connections reject private, loopback and mapped local addresses while allowing public addresses");
    }
}
