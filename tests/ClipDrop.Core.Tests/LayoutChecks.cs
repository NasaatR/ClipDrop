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
        check(settings.MirrorDownloads,
            "Downloads defaults to linked copies on every attached display");
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
        check(migrated.SchemaVersion == ShelfState.CurrentSchema && migrated.Items.OrderBy(i => i.PinOrder).First().Content == "Recent" && migrated.Settings.DockOpacity == 0,
            "Schema-2 upgrade preserves existing visual pin order and zero-valued preferences");
        var added = ShelfItem.FromText("Append right"); added.IsPinned = true; added.PinOrder = PinOrdering.Next(migrated); migrated.Items.Add(added);
        check(migrated.Items.OrderBy(i => i.PinOrder).Last().Id == added.Id, "A newly saved pin appends after migrated pins");
        DockPlanning.Plan(migrated.Settings, new[] { a, b }); migrated.Settings.Docks[1].OffsetX = 42;
        migrated.Settings.PngIcons["Save"] = new string('A', 64); store.Save(migrated); store.Save(migrated);
        check(File.ReadAllBytes(path + ".schema-2-before-v" + ShelfState.CurrentSchema + ".bak").SequenceEqual(original), "Repeated saves preserve the immutable original schema-2 migration backup");
        var reloaded = new StateStore(migrationRoot).Load();
        check(reloaded.Settings.Docks[1].OffsetX == 42 && reloaded.Settings.PngIcons["Save"] == new string('A', 64) && reloaded.Items.Last().PinOrder == added.PinOrder,
            "Monitor offsets, managed PNG references and explicit pin order survive restart");
        var style = reloaded.Settings.Snapshot(); style.PngIcons.Clear();
        check(reloaded.Settings.PngIcons.Count == 1, "Style snapshots deep-copy PNG icon choices");
        check(DockPlanning.DisplayForBounds(new[] { a, b }, a.Id, -700, 40, 900, 100)?.Id == b.Id,
            "A moved window uses the largest intersection on a negative-origin display");
        var right = a with { Id = "right", Left = 1920, Primary = false };
        check(DockPlanning.DisplayForBounds(new[] { a, right }, a.Id, 1870, 30, 100, 40)?.Id == a.Id &&
            DockPlanning.DisplayForBounds(new[] { right, a }, right.Id, 1870, 30, 100, 40)?.Id == right.Id,
            "An equal boundary overlap retains the current screen regardless of enumeration order");
        check(DockPlanning.DisplayForBounds(new[] { a, right }, a.Id, 4000, -400, 200, 100)?.Id == right.Id &&
            DockPlanning.DisplayForBounds(Array.Empty<DisplayArea>(), a.Id, 0, 0, 100, 100) is null,
            "Off-screen and empty-topology move targets are bounded and deterministic");
        var downloads = new ShelfSettings { DownloadsLayout = new() { DisplayId = b.Id, OffsetX = 0, OffsetY = 66, Visible = false } };
        var copies = DockPlanning.Downloads(downloads, new[] { a, b });
        check(copies.Count == 2 && downloads.DownloadsLayouts.Count == 2 && copies.All(c => !c.Layout.Visible) &&
            copies.Single(c => c.Display.Id == b.Id).Layout.OffsetX == 0,
            "Downloads mirroring seeds the legacy anchor and explicit zero/hidden state without losing either display");
        var bRecord = copies.Single(c => c.Display.Id == b.Id).Layout;
        bRecord.OffsetX = 25; bRecord.OffsetY = 125;
        DockPlanning.Downloads(downloads, new[] { a });
        var back = DockPlanning.Downloads(downloads, new[] { b, a });
        check(ReferenceEquals(bRecord, back.Single(c => c.Display.Id == b.Id).Layout) && bRecord.OffsetY == 125 && downloads.DownloadsLayouts.Count == 2,
            "Downloads reconnect/reordering restores the same independent layout without duplicates");
        downloads.MirrorDownloads = false;
        var single = DockPlanning.Downloads(downloads, new[] { a, b });
        var fallback = DockPlanning.Downloads(downloads, new[] { a }).Single();
        check(single.Count == 1 && ReferenceEquals(single[0].Layout, downloads.DownloadsLayout) && fallback.Temporary &&
            downloads.DownloadsLayout.DisplayId == b.Id && downloads.DownloadsLayout.OffsetY == 66,
            "Single Downloads mode and missing-screen fallback retain the saved home without replacing it");
        check(DockPlanning.Downloads(downloads, Array.Empty<DisplayArea>()).Count == 0 && downloads.DownloadsLayouts.Count == 2,
            "A transient empty topology retains all Downloads positions");
        var roundtrip = new ShelfState { Settings = downloads };
        var savedDownloads = StateStore.ReadSnapshot(JsonSerializer.SerializeToUtf8Bytes(roundtrip));
        check(!savedDownloads.Settings.MirrorDownloads && savedDownloads.Settings.DownloadsLayout.OffsetX == 0 &&
            savedDownloads.Settings.DownloadsLayouts.Single(l => l.DisplayId == b.Id).OffsetY == 125,
            "Explicit Downloads mirroring false and per-screen offsets survive restart");
        var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(roundtrip))!;
        legacyJson["Settings"]!.AsObject().Remove("MirrorDownloads"); legacyJson["Settings"]!.AsObject().Remove("DownloadsLayouts");
        var upgraded = StateStore.ReadSnapshot(System.Text.Encoding.UTF8.GetBytes(legacyJson.ToJsonString()));
        check(upgraded.Settings.MirrorDownloads && upgraded.Settings.DownloadsLayouts.Count == 0 &&
            upgraded.Settings.DownloadsLayout.DisplayId == b.Id && upgraded.Settings.DownloadsLayout.OffsetX == 0,
            "An actual older schema-7 snapshot gains Downloads mirroring while retaining its original anchor");
        foreach (var bad in new[] { "null", "[{\"DisplayId\":\"x\"},{\"DisplayId\":\"X\"}]", "[{\"DisplayId\":\"x\",\"OffsetX\":100001}]" })
        {
            legacyJson["Settings"]!["DownloadsLayouts"] = System.Text.Json.Nodes.JsonNode.Parse(bad);
            var rejectedLayout = false;
            try { StateStore.ReadSnapshot(System.Text.Encoding.UTF8.GetBytes(legacyJson.ToJsonString())); }
            catch (InvalidDataException) { rejectedLayout = true; }
            check(rejectedLayout, "Invalid mirrored Downloads metadata is rejected: " + bad);
        }
        var enabledNew = new ShelfSettings { MirrorDocks = true, HomeDisplayId = a.Id };
        check(DockPlanning.Plan(enabledNew, new[] { a, right }).Count == 2,
            "A newly attached monitor joins enabled clip mirroring without another hidden opt-in");
        var hidden = new ShelfSettings { HomeDisplayId = b.Id, Docks = new() { new() { DisplayId = b.Id, Visible = false } } };
        check(!DockPlanning.Plan(hidden, new[] { a }).Single().Layout.Visible,
            "A hidden single panel stays hidden when restarted without its saved home screen");
        var full = new ShelfSettings { HomeDisplayId = a.Id };
        full.Docks.AddRange(Enumerable.Range(0, 64).Select(i => new DockLayout { DisplayId = "offline-" + i, Visible = false }));
        var overflow = DockPlanning.Plan(full, new[] { a, b });
        check(overflow.Single().Temporary && !overflow.Single().Layout.Visible && full.Docks.Count == 64,
            "An unrecorded screen at the layout limit uses a hidden temporary assignment without losing saved records");
        full.DownloadsLayouts.AddRange(full.Docks.Select(l => new DockLayout { DisplayId = l.DisplayId }));
        check(DockPlanning.Downloads(full, new[] { a, b }).All(l => l.Temporary) && full.DownloadsLayouts.Count == 64,
            "Overflow Downloads views are explicitly temporary so refresh can retain their live positions");
    }
}
