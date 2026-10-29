using System;
using System.IO;
using System.Linq;
using System.Threading;
using ClipDrop.Core;

internal static class DownloadsChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var folder = Path.Combine(root, "downloads"); Directory.CreateDirectory(folder);
        var stamp = DateTimeOffset.UtcNow.AddMinutes(1);
        for (var i = 0; i < 65; i++)
        {
            var file = Path.Combine(folder, $"asset-{i:D2}.png"); File.WriteAllText(file, "original " + i);
            File.SetLastWriteTimeUtc(file, stamp.AddSeconds(i).UtcDateTime);
        }
        foreach (var name in new[] { "partial.CRDOWNLOAD", "partial.part", "hidden.tmp", ".hidden", "empty.txt" })
            File.WriteAllText(Path.Combine(folder, name), name == "empty.txt" ? "" : "temporary");
        Directory.CreateDirectory(Path.Combine(folder, "nested")); File.WriteAllText(Path.Combine(folder, "nested", "outside.txt"), "outside");
        var snapshot = RecentDownloads.Scan(folder, now: stamp.AddMinutes(5));
        check(snapshot.Files.Length == 40 && snapshot.Files[0].Name == "asset-64.png" && snapshot.Files[^1].Name == "asset-25.png",
            "Downloads keeps the newest bounded set in descending time order");
        check(snapshot.Files.All(f => !RecentDownloads.IsTemporary(f.Name) && f.Length > 0 && f.Name != "outside.txt"),
            "Downloads excludes temporary, empty and nested files");
        var fresh = Path.Combine(root, "fresh-downloads"); Directory.CreateDirectory(fresh);
        var path = Path.Combine(fresh, "new.txt"); File.WriteAllText(path, "unchanged payload");
        check(RecentDownloads.Scan(fresh).Files.Length == 0 && RecentDownloads.Scan(fresh).Settling,
            "A freshly created final-name file waits for its settling interval");
        var ready = RecentDownloads.Scan(fresh, now: DateTimeOffset.UtcNow.AddSeconds(5));
        check(ready.Files.Single().Name == "new.txt" && File.ReadAllText(path) == "unchanged payload",
            "Ready downloads retain their original bytes and path");
        var temporary = Path.Combine(fresh, "finished.pdf.part"); File.WriteAllText(temporary, "download bytes");
        check(RecentDownloads.Scan(fresh, now: stamp).Files.Length == 1, "An unfinished download stays absent beside ready files");
        File.Move(temporary, Path.Combine(fresh, "finished.pdf"));
        check(RecentDownloads.Scan(fresh, now: stamp).Files.Any(f => f.Name == "finished.pdf"), "A completed temporary-file rename appears as the final filename");
        File.Delete(Path.Combine(fresh, "finished.pdf"));
        File.Delete(path);
        check(RecentDownloads.Scan(fresh, now: stamp).Files.Length == 0, "Removing a source file removes it from the next downloads snapshot");
        check(RecentDownloads.Scan(Path.Combine(root, "not-there")).Notice is not null,
            "An unavailable downloads folder returns a recoverable visible state");
        var cancelled = new CancellationToken(true); var stopped = false;
        try { RecentDownloads.Scan(folder, cancelled, stamp.AddMinutes(5)); } catch (OperationCanceledException) { stopped = true; }
        check(stopped, "Downloads scanning obeys cancellation without mutating files");
        var store = new StateStore(Path.Combine(root, "downloads-settings")); var state = store.Load();
        check(!state.Settings.DownloadsEnabled && state.Settings.DownloadsFolder == "", "Downloads is opt-in and defaults to the Windows Downloads location");
        state.Settings.DownloadsEnabled = true; state.Settings.DownloadsFolder = folder;
        state.Settings.DownloadsLayout = new() { DisplayId = "screen-b", OffsetX = 25, OffsetY = 66, Visible = false };
        new AppearanceStyle().CopyTo(state.Settings); store.Save(state);
        var saved = new StateStore(store.Root).Load();
        check(saved.Settings.DownloadsEnabled && saved.Settings.DownloadsFolder == folder && !saved.Settings.DownloadsLayout.Visible && saved.Settings.DownloadsLayout.OffsetY == 66,
            "Downloads folder, enabled state and layout survive style changes and restart");
    }
}
