using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClipDrop.Core;

public sealed record BrowserDownload(string Path, DateTimeOffset CompletedUtc);
public sealed class BrowserDownloadMessage
{
    public int Version { get; set; }
    public string Type { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public long Revision { get; set; }
    public DateTimeOffset CapturedUtc { get; set; }
    public BrowserDownload[] Files { get; set; } = Array.Empty<BrowserDownload>();
}

// A separate, disposable catalog. Never opens Chrome's History database, writes
// shelf.json, imports payloads or records browsing/source URLs.
public static class BrowserDownloads
{
    public const string HostName = "com.nasaat.clipdrop.downloads";
    public const string ExtensionId = "iedbmkchlbjjabmopniipnffmdjdpmmn";
    public const string Origin = "chrome-extension://" + ExtensionId + "/";
    public const int MaximumBytes = 512 * 1024, MaximumEntries = 200, MaximumProfiles = 16;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };

    public static void Validate(BrowserDownloadMessage message)
    {
        if (message.Version != 1 || message.Type != "downloads" || !Guid.TryParseExact(message.ProfileId, "D", out _) ||
            message.Revision <= 0 || message.CapturedUtc < DateTimeOffset.UnixEpoch || message.CapturedUtc > DateTimeOffset.UtcNow.AddMinutes(5) ||
            message.Files is null || message.Files.Length > MaximumEntries || message.Files.Any(f => f is null ||
                string.IsNullOrWhiteSpace(f.Path) || f.Path.Length > 4096 || f.Path.Any(char.IsControl) ||
                f.CompletedUtc < DateTimeOffset.UnixEpoch || f.CompletedUtc > message.CapturedUtc.AddMinutes(5)))
            throw new InvalidDataException("Invalid browser downloads message.");
    }

    public static async Task<byte[]> ReadFrame(Stream input, CancellationToken cancellation)
    {
        var header = new byte[4]; await input.ReadExactlyAsync(header, cancellation);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length is 0 or > MaximumBytes) throw new InvalidDataException("Browser message exceeds its limit.");
        var bytes = new byte[length]; await input.ReadExactlyAsync(bytes, cancellation); return bytes;
    }
    public static async Task WriteFrame(Stream output, ReadOnlyMemory<byte> bytes, CancellationToken cancellation)
    {
        if (bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Browser message exceeds its limit.");
        var header = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)bytes.Length);
        await output.WriteAsync(header, cancellation); await output.WriteAsync(bytes, cancellation); await output.FlushAsync(cancellation);
    }
    public static BrowserDownloadMessage Decode(byte[] bytes)
    {
        if (bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Browser message exceeds its limit.");
        var message = JsonSerializer.Deserialize<BrowserDownloadMessage>(bytes, Json) ?? throw new InvalidDataException("Missing browser message.");
        Validate(message); return message;
    }
    public static bool Save(string folder, byte[] bytes)
    {
        var message = Decode(bytes); Directory.CreateDirectory(folder);
        var path = System.IO.Path.Combine(folder, message.ProfileId.ToLowerInvariant() + ".json");
        if (File.Exists(path))
        {
            // Never replace a newer snapshot after a delayed native process.
            try
            {
                var previous = Decode(ReadBounded(path));
                if (previous.Revision >= message.Revision) return false;
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException) { /* disposable catalog: a valid fresh sync repairs it */ }
        }
        else if (Directory.EnumerateFiles(folder, "*.json").Take(MaximumProfiles).Count() >= MaximumProfiles)
            throw new InvalidDataException("Too many connected browser profiles.");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return true;
    }
    private static byte[] ReadBounded(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length is 0 or > MaximumBytes) throw new InvalidDataException("Browser catalog exceeds its limit.");
        var bytes = new byte[stream.Length]; stream.ReadExactly(bytes); return bytes;
    }
    public static DownloadSnapshot Scan(string folder, CancellationToken cancellation = default)
    {
        if (!Directory.Exists(folder)) return new(Array.Empty<DownloadFile>(), "Connect Chrome to show downloads from every folder");
        var profiles = 0; var damaged = false;
        var candidates = new Dictionary<string, BrowserDownload>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.json").Take(MaximumProfiles))
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    var message = Decode(ReadBounded(path));
                    if (!string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), message.ProfileId, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Browser profile mismatch.");
                    profiles++;
                    foreach (var entry in message.Files)
                        if (!candidates.TryGetValue(entry.Path, out var previous) || previous.CompletedUtc < entry.CompletedUtc) candidates[entry.Path] = entry;
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { damaged = true; }
            }
            var ready = new List<DownloadFile>();
            foreach (var file in candidates.Values.OrderByDescending(f => f.CompletedUtc).ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
            {
                cancellation.ThrowIfCancellationRequested();
                if (ready.Count == RecentDownloads.Maximum) break;
                if (!System.IO.Path.IsPathFullyQualified(file.Path) || file.Path.StartsWith(@"\\.\", StringComparison.Ordinal) ||
                    file.Path.StartsWith(@"\\?\", StringComparison.Ordinal) || RecentDownloads.IsTemporary(System.IO.Path.GetFileName(file.Path))) continue;
                try
                {
                    var info = new FileInfo(file.Path);
                    if (!info.Exists || (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.System)) != 0) continue;
                    // Chrome says completed; verify availability now, including a
                    // zero-byte completed file, without reading its contents.
                    using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    ready.Add(new(file.Path, info.Name, stream.Length, file.CompletedUtc));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
            }
            return new(ready.ToArray(), damaged ? "Some Chrome data is unavailable · open the companion and Sync now" :
                profiles == 0 ? "Connect Chrome to show downloads from every folder" : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new(Array.Empty<DownloadFile>(), "Chrome data unavailable · open the companion and Sync now"); }
    }
}
