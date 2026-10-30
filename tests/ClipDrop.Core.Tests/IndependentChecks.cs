using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClipDrop.Core;

internal static class IndependentChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var state = new ShelfState();
        var clips = new[] { ShelfItem.FromText("main a"), ShelfItem.FromText("main b"), ShelfItem.FromText("other c"), ShelfItem.FromText("other d") };
        state.Items.AddRange(clips); clips[0].IsPinned = clips[1].IsPinned = true; clips[0].PinOrder = 1; clips[1].PinOrder = 2;
        var main = PanelSets.Active(state); PanelSets.CaptureActive(state);
        var other = PanelSets.Create(state, "Other", clips.Skip(2).Select(i => i.Id)); PanelSets.Activate(state, main.Id);
        PanelSets.OpenWindow(state, other.Id, "screen-b");
        check(state.ActivePanelId == main.Id && PanelSets.Items(state, other.Id).Select(i => i.Id).SequenceEqual(new[] { clips[2].Id, clips[3].Id }),
            "Opening an independent window retains its set membership without switching the main dock");
        PanelSets.SetMembership(state, other.Id, new[] { clips[0].Id }, true);
        PanelSets.SetMembership(state, other.Id, new[] { clips[2].Id }, false);
        PanelSets.Move(state, other.Id, new[] { clips[0].Id }, 0);
        check(PanelSets.Items(state, other.Id).Select(i => i.Id).SequenceEqual(new[] { clips[0].Id, clips[3].Id }) &&
            PanelSets.Items(state).Select(i => i.Id).SequenceEqual(new[] { clips[0].Id, clips[1].Id }) && !clips[3].IsPinned,
            "Independent membership and reorder leave the main set and pin projection unchanged");
        LibraryOperations.Remove(state, new[] { clips[3].Id }); PanelSets.CaptureActive(state);
        check(PanelSets.Items(state, other.Id).Length == 1 && other.ClipIds.Contains(clips[3].Id), "Shared Trash removes an independent clip from view while retaining its membership");
        LibraryOperations.UndoRemove(state); PanelSets.ApplyActive(state);
        check(PanelSets.Items(state, other.Id).Select(i => i.Id).SequenceEqual(new[] { clips[0].Id, clips[3].Id }) && !clips[3].IsPinned,
            "Trash restoration recovers independent order without adding the clip to the main set");
        other.Window!.Layout.OffsetX = 42; other.Window.Layout.OffsetY = 55; other.Window.Layout.Visible = false;
        PanelSets.CloseWindow(state, other.Id);
        check(other.ClosedUtc is null && other.Window.ClosedUtc is not null && !other.Window.IsOpen && state.Items.Count == 4 && state.ActivePanelId == main.Id,
            "Closing a window preserves its saved set, clips, main identity and restore record");
        PanelSets.OpenWindow(state, other.Id, "screen-a");
        check(other.Window.IsOpen && other.Window.ClosedUtc is null && other.Window.Layout.DisplayId == "screen-b" && other.Window.Layout.OffsetX == 42 && other.Window.Layout.Visible,
            "Reopening a window reuses its original monitor and position and makes it visible");
        var a = new DisplayArea("screen-a", "Main", 0, 0, 1920, 1040, true);
        var b = new DisplayArea("screen-b", "Left", -2560, -200, 2560, 1400);
        var fallback = DockPlanning.PanelWindow(other, new[] { a })!;
        var restored = DockPlanning.PanelWindow(other, new[] { b, a })!;
        check(fallback.Temporary && fallback.Display.Id == a.Id && !restored.Temporary && ReferenceEquals(restored.Layout, other.Window.Layout) && other.Window.Layout.OffsetX == 42,
            "Missing-display fallback preserves the independent layout for reconnect");
        var position = DockPlanning.Position(b, restored.Layout, 600, 100, 1.5);
        check(position.X == -2497 && position.Y == -117.5, "Independent placement projects stored DIP offsets on negative mixed-DPI origins");
        var folder = Path.Combine(root, "independent"); var store = new StateStore(folder); store.Load();
        state.Settings.CollapseWhenIdle = false; store.Save(state); var reopened = new StateStore(folder).Load();
        check(reopened.Panels.Single(p => p.Id == other.Id).Window?.IsOpen == true && reopened.Panels.Single(p => p.Id == other.Id).Window!.Layout.OffsetY == 55 && !reopened.Settings.CollapseWhenIdle,
            "Independent window layout and an explicit disabled collapse preference survive restart");
        new AppearanceStyle().CopyTo(reopened.Settings);
        check(!reopened.Settings.CollapseWhenIdle, "Appearance presets do not reset the dock collapse behavior");
        PanelSets.Close(state, new[] { other.Id });
        check(other.ClosedUtc is not null && !other.Window.IsOpen && other.Window.ClosedUtc is not null && state.Items.Count == 4,
            "Closing a logical set closes its window while preserving payloads and layout");
        PanelSets.OpenWindow(state, other.Id, a.Id);
        check(other.ClosedUtc is null && state.ActivePanelId == main.Id, "Restoring a closed independent window does not switch the main set");
        for (var i = 0; i < 11; i++) { var panel = new PanelSet { Name = "Window " + i }; state.Panels.Add(panel); PanelSets.OpenWindow(state, panel.Id, a.Id); }
        var extra = new PanelSet { Name = "Thirteenth" }; state.Panels.Add(extra);
        var limited = false; try { PanelSets.OpenWindow(state, extra.Id, a.Id); } catch (InvalidOperationException) { limited = true; }
        PanelSets.OpenWindow(state, other.Id, a.Id);
        check(limited && state.Panels.Count(p => p.Window?.IsOpen == true) == 12 && extra.Window is null,
            "The independent-window limit rejects a new view without changing existing windows");
        var legacyRoot = Path.Combine(root, "independent-schema5"); Directory.CreateDirectory(legacyRoot);
        state.SchemaVersion = 5;
        var node = JsonNode.Parse(JsonSerializer.Serialize(state))!;
        foreach (var panel in node["Panels"]!.AsArray()) panel!.AsObject().Remove("Window");
        node["Settings"]!.AsObject().Remove("CollapseWhenIdle");
        var before = System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()); File.WriteAllBytes(Path.Combine(legacyRoot, "shelf.json"), before);
        var oldStore = new StateStore(legacyRoot); var migrated = oldStore.Load(); oldStore.Save(migrated); oldStore.Save(migrated);
        check(migrated.SchemaVersion == ShelfState.CurrentSchema && migrated.ActivePanelId == main.Id && migrated.Panels.All(p => p.Window is null) &&
            migrated.Panels.Single(p => p.Id == other.Id).ClipIds.SequenceEqual(other.ClipIds) && migrated.Settings.CollapseWhenIdle,
            "Schema-5 migration keeps named memberships and introduces collapse without spawning windows");
        check(File.ReadAllBytes(Path.Combine(legacyRoot, "shelf.json.schema-5-before-v" + ShelfState.CurrentSchema + ".bak")).SequenceEqual(before),
            "Schema migration saves preserve exact pre-upgrade schema-5 bytes");
    }
}
