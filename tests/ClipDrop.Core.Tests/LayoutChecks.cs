using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClipDrop.Core;

internal static class LayoutChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var a = new DisplayArea("monitor-a", "Main", 0, 0, 1920, 1040, true);
        var b = new DisplayArea("monitor-b", "Left", -2560, -200, 2560, 1400);
        var settings = new ShelfSettings();
        var initial = DockPlanning.Plan(settings, new[] { a, b });
        check(initial.Count == 1 && initial[0].Display.Id == a.Id && settings.Docks.Count == 2,
            "A new installation enables only the main dock, retaining separate monitor records");
        settings.MirrorDocks = true; settings.Docks.Single(d => d.DisplayId == b.Id).Enabled = true;
        var both = DockPlanning.Plan(settings, new[] { b, a });
        var layout = both.Single(d => d.Display.Id == b.Id).Layout;
        layout.OffsetX = 24; layout.OffsetY = 60; layout.Visible = false;
        var position = DockPlanning.Position(b, layout, 600, 120, 1.5);
        check(position.X == -2524 && position.Y == -110, "Mixed-DPI local offsets project correctly on a monitor with negative origins");
        var clamped = DockPlanning.Position(b, new DockLayout { OffsetX = 99999, OffsetY = -99999 }, 600, 120, 1.5);
        check(clamped.X == -600 && clamped.Y == -200, "Off-screen positions clamp to the assigned physical work area");
        settings.HomeDisplayId = b.Id; settings.MirrorDocks = false;
        var missing = DockPlanning.Plan(settings, new[] { a });
        check(missing.Single().Temporary && layout.OffsetX == 24 && !layout.Visible,
            "Disconnected home screen gets a reachable temporary fallback without overwriting its layout");
        var reconnected = DockPlanning.Plan(settings, new[] { a, b });
        check(reconnected.Single().Display.Id == b.Id && !reconnected[0].Temporary && !reconnected[0].Layout.Visible,
            "Reconnection restores the original assigned monitor and saved hidden state");
        settings.MirrorDocks = true;
        check(DockPlanning.Plan(settings, new[] { b, a }).Count == 2 && DockPlanning.Plan(settings, new[] { a, b }).Count == 2,
            "Repeated/reordered display discovery does not duplicate monitor records");
        check(DockPlanning.Plan(settings, Array.Empty<DisplayArea>()).Count == 0 && settings.Docks.Count == 2,
            "A transient empty display topology preserves valuable monitor records");
        var migrationRoot = Path.Combine(root, "schema2-monitors"); Directory.CreateDirectory(migrationRoot);
        var old = new ShelfState { SchemaVersion = 2 };
        old.Items.Add(ShelfItem.FromText("Older")); old.Items[0].CreatedUtc = DateTimeOffset.UtcNow.AddDays(-1); old.Items[0].IsPinned = true;
        old.Items.Add(ShelfItem.FromText("Recent")); old.Items[1].IsPinned = true;
        old.Settings.DockLeft = 300; old.Settings.DockOpacity = 0;
        var original = JsonSerializer.SerializeToUtf8Bytes(old); var path = Path.Combine(migrationRoot, "shelf.json"); File.WriteAllBytes(path, original);
        var store = new StateStore(migrationRoot); var migrated = store.Load();
        check(migrated.SchemaVersion == 3 && migrated.Items.OrderBy(i => i.PinOrder).First().Content == "Recent" && migrated.Settings.DockOpacity == 0,
            "Schema-2 upgrade preserves existing visual pin order and zero-valued preferences");
        var added = ShelfItem.FromText("Append right"); added.IsPinned = true; added.PinOrder = PinOrdering.Next(migrated); migrated.Items.Add(added);
        check(migrated.Items.OrderBy(i => i.PinOrder).Last().Id == added.Id, "A newly saved pin appends after migrated pins");
        DockPlanning.Plan(migrated.Settings, new[] { a, b }); migrated.Settings.Docks[1].OffsetX = 42;
        migrated.Settings.PngIcons["Save"] = new string('A', 64); store.Save(migrated); store.Save(migrated);
        check(File.ReadAllBytes(path + ".schema-2-before-v3.bak").SequenceEqual(original), "Repeated saves preserve the immutable original schema-2 migration backup");
        var reloaded = new StateStore(migrationRoot).Load();
        check(reloaded.Settings.Docks[1].OffsetX == 42 && reloaded.Settings.PngIcons["Save"] == new string('A', 64) && reloaded.Items.Last().PinOrder == added.PinOrder,
            "Monitor offsets, managed PNG references and explicit pin order survive restart");
        var style = reloaded.Settings.Snapshot(); style.PngIcons.Clear();
        check(reloaded.Settings.PngIcons.Count == 1, "Style snapshots deep-copy PNG icon choices");
    }
}
