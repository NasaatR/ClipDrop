using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using ClipDrop.Core;

internal static class BrowserDownloadChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var folder = Path.Combine(root, "chrome-catalog");
        var a = Path.Combine(root, "outside-downloads", "Референс.webp"); Directory.CreateDirectory(Path.GetDirectoryName(a)!); File.WriteAllText(a, "original");
        var b = Path.Combine(root, "different-drive-folder", "empty.txt"); Directory.CreateDirectory(Path.GetDirectoryName(b)!); File.WriteAllBytes(b, Array.Empty<byte>());
        var now = DateTimeOffset.UtcNow.AddSeconds(-2); var id = Guid.NewGuid().ToString("D");
        var message = new BrowserDownloadMessage { Version = 1, Type = "downloads", ProfileId = id, Revision = 1, CapturedUtc = now,
            Files = new[] { new BrowserDownload(a, now.AddMinutes(-2)), new BrowserDownload(b, now.AddMinutes(-1)), new BrowserDownload(a, now) } };
        byte[] Bytes() => JsonSerializer.SerializeToUtf8Bytes(message, BrowserDownloads.Json);
        check(BrowserDownloads.Scan(folder).Notice!.Contains("Connect Chrome"), "A missing Chrome connection is visible without silently falling back to one folder");
        BrowserDownloads.Save(folder, Bytes()); var snapshot = BrowserDownloads.Scan(folder);
        check(snapshot.Files.Select(f => f.Path).SequenceEqual(new[] { a, b }) && snapshot.Files[0].AddedUtc == now && snapshot.Files[1].Length == 0,
            "Chrome downloads span arbitrary folders, deduplicate paths and sort by completion rather than file timestamps");
        check(File.ReadAllText(a) == "original" && Directory.GetFiles(folder).Length == 1,
            "Browser catalog updates store metadata only and leave originals intact");
        var stored = File.ReadAllBytes(Path.Combine(folder, id + ".json"));
        message.Revision = 1; message.Files = Array.Empty<BrowserDownload>();
        check(!BrowserDownloads.Save(folder, Bytes()) && File.ReadAllBytes(Path.Combine(folder, id + ".json")).SequenceEqual(stored),
            "Delayed or duplicate browser revisions cannot erase a newer download list");
        message.Revision = 2; message.ProfileId = Guid.NewGuid().ToString("D"); message.Files = new[] { new BrowserDownload(b, now.AddSeconds(-1)) };
        BrowserDownloads.Save(folder, Bytes()); message.ProfileId = id; message.Files = Array.Empty<BrowserDownload>(); BrowserDownloads.Save(folder, Bytes());
        check(BrowserDownloads.Scan(folder).Files.Single().Path == b, "Pausing or clearing one Chrome profile preserves downloads from another profile");
        File.Delete(b); check(BrowserDownloads.Scan(folder).Files.Length == 0, "Missing Chrome files are revalidated and omitted from direct transfers");
        message.Files = new[] { new BrowserDownload(a, now) }; message.Revision = 3; BrowserDownloads.Save(folder, Bytes());
        message.Version = 99; var rejected = false;
        try { BrowserDownloads.Save(folder, Bytes()); } catch (InvalidDataException) { rejected = true; }
        check(rejected && BrowserDownloads.Scan(folder).Files.Single().Path == a, "An unsupported browser protocol cannot overwrite a usable local snapshot");
        message.Version = 1; message.ProfileId = "../../escape"; rejected = false;
        try { BrowserDownloads.Save(folder, Bytes()); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Browser profile identifiers cannot escape the catalog directory");
        var unicode = Encoding.UTF8.GetBytes("{\"path\":\"C:\\\\Пример\\\\👋.png\"}");
        using var frame = new MemoryStream(); BrowserDownloads.WriteFrame(frame, unicode, CancellationToken.None).GetAwaiter().GetResult(); frame.Position = 0;
        check(BrowserDownloads.ReadFrame(frame, CancellationToken.None).GetAwaiter().GetResult().SequenceEqual(unicode),
            "Native messaging frames count UTF-8 bytes and preserve Unicode and binary length headers");
        using var tooLarge = new MemoryStream(); var header = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(header, BrowserDownloads.MaximumBytes + 1); tooLarge.Write(header); tooLarge.Position = 0;
        rejected = false; try { BrowserDownloads.ReadFrame(tooLarge, CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Oversized native frames are rejected before payload allocation");
        using var truncated = new MemoryStream(new byte[] { 9, 0, 0, 0, 1 }); rejected = false;
        try { BrowserDownloads.ReadFrame(truncated, CancellationToken.None).GetAwaiter().GetResult(); } catch (EndOfStreamException) { rejected = true; }
        check(rejected, "Truncated native messages cannot be treated as acknowledged snapshots");
        var preferences = new ShelfSettings(); check(preferences.EffectiveDownloadsSource == "Chrome", "Chrome is the default recent-download source");
        preferences.DownloadsFolder = root; check(preferences.EffectiveDownloadsSource == "Folder", "An existing custom watched folder remains selected on upgrade");
        preferences.DownloadsSource = "Chrome";
        var store = new StateStore(Path.Combine(root, "chrome-settings")); var state = store.Load(); state.Settings = preferences; store.Save(state);
        check(new StateStore(store.Root).Load().Settings.EffectiveDownloadsSource == "Chrome", "An explicit Chrome source survives restart alongside the retained custom folder");
    }
}
