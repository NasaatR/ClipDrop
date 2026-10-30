using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using ClipDrop.Core;

internal static class BackupChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var store = new StateStore(Path.Combine(root, "backup-data")); var state = store.Load();
        var assets = new AssetStore(Path.Combine(store.Root, "assets"));
        var image = new ShelfItem { Kind = ItemKind.Image, OwnedAsset = true, FilePath = assets.WriteImage(new byte[] { 9, 8, 7 }, ".webp"), Title = "Original encoded image" };
        var raw = Encoding.UTF8.GetBytes("opaque\0Привет 👋\r\n"); var hash = assets.WriteBlob(raw);
        var text = ShelfItem.FromText("  Unsaved\r\nПример  "); text.Clipboard = new() { Identity = "opaque-identity", Formats = new() { new() { Name = "Custom format", Hash = hash, Length = raw.Length } } };
        var removed = ShelfItem.FromText("recover me"); removed.Clipboard = text.Clipboard;
        var external = new ShelfItem { Kind = ItemKind.File, Title = "Moved external file", FilePath = Path.Combine(root, "missing-original.psd") };
        state.Items.AddRange(new[] { image, text, removed, external });
        foreach (var item in state.Items) item.IsPinned = true;
        state.Settings.DockOpacity = 0; state.Settings.AlwaysOnTop = false;
        PanelSets.CaptureActive(state); PanelStacks.Combine(state, state.ActivePanelId, image.Id, new[] { text.Id });
        LibraryOperations.Remove(state, new[] { removed.Id });
        var icon = new byte[] { 4, 5, 6 }; var iconHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(icon));
        Directory.CreateDirectory(Path.Combine(store.Root, "icons")); File.WriteAllBytes(Path.Combine(store.Root, "icons", iconHash + ".png"), icon);
        state.Settings.SavedStyles.Add(new() { Name = "Custom", Style = new AppearanceStyle { PngIcons = new() { ["Save"] = iconHash } } });
        File.WriteAllText(Path.Combine(assets.Root, "orphan.bin"), "unreferenced");
        var output = Path.Combine(root, "library.clipdrop.zip"); var snapshot = store.CaptureSnapshot(state); var capturedText = text.Content;
        text.Content = "Edited after the snapshot was captured";
        var result = BackupArchive.Create(store.Root, snapshot, output);
        check(result.Clips == 3 && result.Trashed == 1 && result.Panels == 1 && result.Assets == 3,
            "Backup captures current unsaved clips, Trash, stack metadata and deduplicated owned/clipboard/style assets");
        using (var archive = ZipFile.OpenRead(output))
        {
            using var metadata = new MemoryStream(); archive.GetEntry("shelf.json")!.Open().CopyTo(metadata);
            var restored = StateStore.ReadSnapshot(metadata.ToArray());
            check(restored.Items.Single(i => i.Id == text.Id).Content == capturedText && restored.Trash.Single().Item.Id == removed.Id &&
                restored.Panels.Single().Stacks.Single().ClipIds.Count == 2 && restored.Settings.DockOpacity == 0 && !restored.Settings.AlwaysOnTop,
                "Archived metadata preserves Unicode/whitespace, Trash recovery, stacks and explicit zero/false preferences");
            using var payload = new MemoryStream(); archive.GetEntry("assets/clipboard/" + hash + ".bin")!.Open().CopyTo(payload);
            check(payload.ToArray().SequenceEqual(raw) && archive.Entries.All(e => !e.FullName.Contains("orphan")),
                "Backup preserves opaque clipboard bytes once and excludes unreferenced caches");
            check(restored.Items.Single(i => i.Id == external.Id).FilePath == external.FilePath && archive.Entries.Count == 5,
                "Missing external originals remain references without blocking export or being copied into the archive");
        }
        check(BackupArchive.Verify(output) == result && !File.Exists(Path.Combine(store.Root, "shelf.json")),
            "A verified backup does not silently save or rewrite source shelf metadata");
        var before = File.ReadAllBytes(output); var cancelled = false;
        var overwriteRejected = false;
        try { BackupArchive.Create(store.Root, snapshot, output); } catch (IOException) { overwriteRejected = true; }
        check(overwriteRejected && File.ReadAllBytes(output).SequenceEqual(before), "Backup publication never overwrites an existing file, including paths reached through an alias");
        try { BackupArchive.Create(store.Root, snapshot, output, cancellation: new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
        check(cancelled && File.ReadAllBytes(output).SequenceEqual(before), "Cancelling backup preserves an existing destination byte for byte");
        var failedOutput = Path.Combine(root, "failed-backup.clipdrop.zip");
        using (var stop = new CancellationTokenSource())
        {
            cancelled = false;
            try { BackupArchive.Create(store.Root, snapshot, failedOutput, progress => { if (progress.Stage == "Verifying") stop.Cancel(); }, stop.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            check(cancelled && !File.Exists(failedOutput) && File.ReadAllBytes(output).SequenceEqual(before), "Cancelling after archive creation but before verification never publishes over an existing backup");
        }
        File.Delete(image.FilePath!); var rejected = false;
        try { BackupArchive.Create(store.Root, snapshot, failedOutput); } catch (IOException) { rejected = true; }
        check(rejected && File.ReadAllBytes(output).SequenceEqual(before) && !Directory.GetFiles(root, "*.backup-tmp").Any(),
            "Missing owned payload fails export honestly, cleans its temporary file and keeps an existing backup");
        File.WriteAllBytes(image.FilePath!, new byte[] { 9, 8, 7 });
        File.WriteAllText(Path.Combine(assets.Root, "clipboard", hash + ".bin"), "changed"); rejected = false;
        try { BackupArchive.Create(store.Root, snapshot, failedOutput); } catch (InvalidDataException) { rejected = true; }
        check(rejected && File.ReadAllBytes(output).SequenceEqual(before), "Corrupted raw clipboard content cannot become a supposedly valid backup");
        File.WriteAllBytes(Path.Combine(assets.Root, "clipboard", hash + ".bin"), raw);
        var damaged = Path.Combine(root, "tampered.clipdrop.zip"); File.WriteAllBytes(damaged, before);
        using (var archive = ZipFile.Open(damaged, ZipArchiveMode.Update))
        { var entry = archive.GetEntry("assets/clipboard/" + hash + ".bin")!; entry.Delete(); using var stream = archive.CreateEntry("assets/clipboard/" + hash + ".bin").Open(); stream.Write(new byte[raw.Length]); }
        rejected = false; try { BackupArchive.Verify(damaged); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Backup verification checks actual decompressed payload hashes rather than trusting ZIP metadata");
        using (var archive = ZipFile.Open(damaged, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("manifest.json")!; System.Text.Json.Nodes.JsonNode manifest;
            using (var source = entry.Open()) manifest = System.Text.Json.Nodes.JsonNode.Parse(source)!;
            foreach (var item in manifest["Entries"]!.AsArray())
                if (item!["Name"]!.GetValue<string>() == "assets/clipboard/" + hash + ".bin") item["Sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[raw.Length]));
            entry.Delete(); using var target = archive.CreateEntry("manifest.json").Open(); JsonSerializer.Serialize(target, manifest);
        }
        rejected = false; try { BackupArchive.Verify(damaged); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Even a matching archive hash cannot override the snapshot's original clipboard identity hash");
        File.WriteAllBytes(damaged, before);
        using (var archive = ZipFile.Open(damaged, ZipArchiveMode.Update)) { using var stream = archive.CreateEntry("../escape.txt").Open(); stream.WriteByte(1); }
        rejected = false; try { BackupArchive.Verify(damaged); } catch (InvalidDataException) { rejected = true; }
        check(rejected && !File.Exists(Path.Combine(root, "escape.txt")), "Backup verification rejects unexpected/traversal entries and never extracts them");
        rejected = false; try { BackupArchive.Create(store.Root, snapshot, image.FilePath!); } catch (InvalidDataException) { rejected = true; }
        check(rejected && File.ReadAllBytes(image.FilePath!).SequenceEqual(new byte[] { 9, 8, 7 }), "A backup destination cannot overwrite a managed source payload");
        var changed = StateStore.ReadSnapshot(snapshot); changed.Items.Single(i => i.Id == image.Id).FilePath = Path.Combine(root, "not-owned.webp");
        rejected = false; try { BackupArchive.Create(store.Root, store.CaptureSnapshot(changed), failedOutput); } catch (InvalidDataException) { rejected = true; }
        check(rejected && File.ReadAllBytes(output).SequenceEqual(before), "An owned reference outside the data assets directory is rejected before publication");
        if (!OperatingSystem.IsWindows())
        {
            var alias = Path.Combine(root, "outside-alias"); Directory.CreateSymbolicLink(alias, store.Root);
            var metadataPath = Path.Combine(store.Root, "shelf.json"); File.WriteAllBytes(metadataPath, snapshot); rejected = false;
            try { BackupArchive.Create(store.Root, snapshot, Path.Combine(alias, "shelf.json")); } catch (IOException) { rejected = true; }
            check(rejected && File.ReadAllBytes(metadataPath).SequenceEqual(snapshot), "A directory alias cannot turn backup export into an overwrite of live shelf metadata");
            Directory.Delete(alias); File.Delete(metadataPath);
        }
    }
}
