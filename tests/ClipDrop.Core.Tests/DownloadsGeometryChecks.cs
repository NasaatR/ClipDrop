using System;
using System.Linq;
using System.Text.Json;
using ClipDrop.Core;

internal static class DownloadsGeometryChecks
{
    public static void Run(Action<bool, string> check)
    {
        var settings = new ShelfSettings();
        var bottom = DownloadsGeometry.Place(settings, 1200, 900, new(1100, 820));
        var top = DownloadsGeometry.Place(settings, 1200, 900, new(1100, 70));
        check(!bottom.OpensDown && top.OpensDown && bottom.AnchorY == 820 && top.AnchorY == 70,
            "Automatic Downloads placement opens toward free space while preserving the capsule center");
        settings.DownloadsWidth = 480; settings.DownloadsRows = 20; settings.DownloadsRowSpacing = 12;
        var resized = DownloadsGeometry.Place(settings, 1200, 900, new(1100, 820));
        check(resized.AnchorX == bottom.AnchorX && resized.AnchorY == bottom.AnchorY && resized.Left + resized.Width - 32 == 1100 && resized.Top + resized.Height - 32 == 820,
            "Changing panel width, rows and spacing keeps the bottom capsule at the exact same center");
        foreach (var center in new[] { new PixelPosition(40, 50), new PixelPosition(1160, 850), new PixelPosition(400, 450), new PixelPosition(-100, 2000) })
        {
            var fit = DownloadsGeometry.Place(settings, 1200, 900, center);
            check(fit.Left >= 0 && fit.Top >= 0 && fit.Left + fit.Width <= 1200 && fit.Top + fit.Height <= 900 && fit.Width > 0 && fit.Height > 0,
                "Fitted Downloads stays inside its work area for anchor " + center);
        }
        settings.DownloadsDirection = "Above";
        check(!DownloadsGeometry.Place(settings, 1200, 900, new(1000, 500)).OpensDown && DownloadsGeometry.Place(settings, 1200, 900, new(1000, 50)).OpensDown,
            "Explicit Downloads direction is honored where it fits and falls back when that edge has no useful room");
        var layout = new DockLayout { DisplayId = "home", OffsetX = 700, OffsetY = 200 };
        var legacy = DownloadsGeometry.LegacyAnchor(new ShelfSettings(), layout, 1200, 900);
        check(legacy.X == 940 && legacy.Y == 682 && layout.AnchorX is null,
            "Legacy whole-window offsets can be projected to the old capsule center without rewriting source metadata");
        var state = new ShelfState(); state.Settings.DownloadsLayout = new() { DisplayId = "home", AnchorX = 0, AnchorY = 70, OffsetX = 15, OffsetY = 20 }; state.Settings.DownloadsDirection = "Below";
        var copy = StateStore.ReadSnapshot(JsonSerializer.SerializeToUtf8Bytes(state));
        check(copy.Settings.DownloadsLayout.AnchorX == 0 && copy.Settings.DownloadsLayout.AnchorY == 70 && copy.Settings.DownloadsDirection == "Below",
            "Explicit zero capsule anchors and opening preferences survive state roundtrip");
        var plan = DockPlanning.Downloads(copy.Settings, new[] { new DisplayArea("home", "Home", 0, 0, 1200, 900, true) });
        check(plan.Single().Layout.AnchorY == 70, "New mirror records inherit the existing home capsule anchor");
        state.Settings.DownloadsLayout.AnchorX = 100001; var rejected = false;
        try { StateStore.ReadSnapshot(JsonSerializer.SerializeToUtf8Bytes(state)); } catch (System.IO.InvalidDataException) { rejected = true; }
        check(rejected, "Invalid capsule anchors are rejected before any state can be overwritten");
        var files = Enumerable.Range(0, 4).Select(i => new DownloadFile("D:\\" + i, "file", 1, DateTimeOffset.UnixEpoch.AddMinutes(i))).ToArray();
        check(DownloadsGeometry.Order(files, true).First().Path.EndsWith("3") && DownloadsGeometry.Order(files, false).Last().Path.EndsWith("3") && files[0].Path.EndsWith("0"),
            "Visual ordering keeps the newest file adjacent to either capsule orientation without changing the catalog");
        check(ReleaseVersion.Label("1.0.0") == "1.0" && ReleaseVersion.Next("1.0.0") == "1.1.0" && ReleaseVersion.Next("1.9.0") == "2.0.0" && Version.Parse("1.0.0") > Version.Parse("0.19.0"),
            "Personal release labels and one-digit rollover stay understandable and numerically upgrade from legacy releases");
    }
}
