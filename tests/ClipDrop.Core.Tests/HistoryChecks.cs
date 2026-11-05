using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO.Compression;
using System.Security.Cryptography;
using ClipDrop.Core;

internal static class HistoryChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var folder = Path.Combine(root, "history"); var store = new StateStore(folder); var state = store.Load();
        var a = ShelfItem.FromText("Saved note — Привет"); a.IsPinned = true;
        var b = ShelfItem.FromText("Auto copy"); b.HistoryOnly = true;
        state.Items.AddRange(new[] { a, b }); ClipHistory.Record(a, false); ClipHistory.Record(b, true);
        PanelSets.CaptureActive(state); LibraryOperations.ArchiveUsed(state, new[] { a.Id });
        check(ClipHistory.Entries(state).Count() == 2 && state.Trash.Single().Item == a,
            "Used saved clips remain in History together with active auto-captures");
        ClipHistory.AddToPanel(state, new[] { a.Id, b.Id }); store.Save(state);
        var loaded = new StateStore(folder).Load();
        check(loaded.Trash.Count == 0 && loaded.Items.All(i => i.IsPinned && i.InHistory && !i.HistoryOnly),
            "History re-add restores Trash and promotes auto-captures while persisting panel membership");
        check(ClipHistory.Forget(state, new[] { a.Id, a.Id }) == 1 && state.Items.Contains(a) && a.IsPinned && !a.InHistory,
            "Removing selected history is idempotent and keeps saved clips/panels intact");
        ClipHistory.Record(a, false); check(a.InHistory && ClipHistory.Entries(state).Count() == 2,
            "Explicit recapture returns a cleared saved clip to History without duplicate payloads");
        var isolated = ShelfItem.FromText("Unused auto capture"); isolated.HistoryOnly = true; state.Items.Add(isolated);
        check(ClipHistory.Forget(state, new[] { isolated.Id }) == 1 && !state.Items.Contains(isolated),
            "Clearing unused auto-history frees its metadata without touching saved clips");
        var other = new PanelSet { Name = "Other" }; state.Panels.Add(other);
        LibraryOperations.Remove(state, new[] { a.Id }); ClipHistory.AddToPanel(state, new[] { a.Id }, other.Id);
        check(other.ClipIds.Contains(a.Id) && !state.Trash.Any(t => t.Item.Id == a.Id), "History can restore into an independent target panel");
        ClipHistory.Forget(state, ClipHistory.Entries(state).Select(i => i.Id).ToArray()); store.Save(state);
        var cleared = new StateStore(folder).Load();
        check(!ClipHistory.Entries(cleared).Any() && cleared.Items.Count == 2 && cleared.Panels.Single(p => p.Id == other.Id).ClipIds.Contains(a.Id),
            "Clear History survives restart while preserving all saved library/panel references");
        var old = JsonNode.Parse(store.CaptureSnapshot(state))!; old["SchemaVersion"] = 7;
        foreach (var item in old["Items"]!.AsArray()) { item!.AsObject().Remove("InHistory"); item.AsObject().Remove("HistoryOnly"); item.AsObject().Remove("LastSavedUtc"); }
        old["Settings"]!.AsObject().Remove("AutoSaveClipboard");
        var oldBytes = Encoding.UTF8.GetBytes(old.ToJsonString()); File.WriteAllBytes(Path.Combine(folder, "shelf.json"), oldBytes);
        var upgradeStore = new StateStore(folder); var upgraded = upgradeStore.Load(); upgradeStore.Save(upgraded); upgradeStore.Save(upgraded);
        check(upgraded.SchemaVersion == 8 && upgraded.Items.All(i => i.InHistory && !i.HistoryOnly) && !upgraded.Settings.AutoSaveClipboard &&
            File.ReadAllBytes(Path.Combine(folder, "shelf.json.schema-7-before-v8.bak")).SequenceEqual(oldBytes),
            "Schema-7 upgrade includes old clips in History, keeps auto capture off and preserves immutable original bytes");
        var bad = JsonNode.Parse(upgradeStore.CaptureSnapshot(upgraded))!; bad["Items"]![0]!.AsObject().Remove("InHistory");
        var rejected = false; try { StateStore.ReadSnapshot(Encoding.UTF8.GetBytes(bad.ToJsonString())); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Current schema rejects incomplete history metadata instead of silently restoring cleared history");
        // Preserve an actual schema-7 archive across the new read-time migration.
        var backup = Path.Combine(root, "history-legacy.zip"); BackupArchive.Create(folder, upgradeStore.CaptureSnapshot(upgraded), backup);
        using (var zip = ZipFile.Open(backup, ZipArchiveMode.Update))
        {
            var metadata = zip.GetEntry("shelf.json")!; metadata.Delete();
            using (var output = zip.CreateEntry("shelf.json").Open()) output.Write(oldBytes);
            var manifestEntry = zip.GetEntry("manifest.json")!; JsonNode manifest;
            using (var input = manifestEntry.Open()) manifest = JsonNode.Parse(input)!;
            manifestEntry.Delete(); manifest["DataSchema"] = 7;
            var entry = manifest["Entries"]!.AsArray().Single(e => e!["Name"]!.GetValue<string>() == "shelf.json")!;
            entry["Length"] = oldBytes.Length; entry["Sha256"] = Convert.ToHexString(SHA256.HashData(oldBytes));
            using var header = zip.CreateEntry("manifest.json").Open(); JsonSerializer.Serialize(header, manifest);
        }
        check(BackupArchive.Verify(backup).Clips == 2, "Verified legacy backup keeps its original manifest schema while metadata migrates for reading");
    }
}
