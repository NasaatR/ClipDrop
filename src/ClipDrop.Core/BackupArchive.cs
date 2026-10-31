using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;

namespace ClipDrop.Core;

public sealed record BackupProgress(string Stage, int Files, int TotalFiles, long Bytes);
public sealed record BackupSummary(int Clips, int Trashed, int Panels, int Assets, long Bytes);

// Export/verify only. No extraction, live-library replacement or external-file
// copying. The explicit archive is a first increment toward previewed recovery.
public static partial class BackupArchive
{
    private const long MaximumAsset = 512L * 1024 * 1024, MaximumTotal = 8L * 1024 * 1024 * 1024;
    private const int MaximumMetadata = 256 * 1024 * 1024, MaximumManifest = 32 * 1024 * 1024, MaximumEntries = 50000;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, MaxDepth = 16 };
    private sealed record Entry(string Name, long Length, string Sha256);
    private sealed record Reference(string Name, string Path, long? Length = null, string? Hash = null);
    private sealed class Manifest
    {
        public int FormatVersion { get; set; }
        public int DataSchema { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
        public string SourceRoot { get; set; } = "";
        public List<Entry> Entries { get; set; } = new();
    }
    private static Reference[] References(string root, ShelfState state)
    {
        var references = new Dictionary<string, Reference>(StringComparer.OrdinalIgnoreCase);
        void Add(Reference reference)
        {
            if (references.TryGetValue(reference.Name, out var previous))
            {
                if (previous.Length.HasValue && reference.Length.HasValue && previous.Length != reference.Length ||
                    previous.Hash is not null && reference.Hash is not null && !previous.Hash.Equals(reference.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Conflicting managed asset references.");
                references[reference.Name] = reference with { Length = previous.Length ?? reference.Length, Hash = previous.Hash ?? reference.Hash };
            }
            else references.Add(reference.Name, reference);
        }
        foreach (var item in state.Items.Concat(state.Trash.Select(t => t.Item)))
        {
            if (item.OwnedAsset)
            {
                if (string.IsNullOrWhiteSpace(item.FilePath) || !Path.IsPathFullyQualified(item.FilePath)) throw new InvalidDataException("Owned payload has no managed path.");
                var relative = Path.GetRelativePath(root, Path.GetFullPath(item.FilePath)).Replace(Path.DirectorySeparatorChar, '/');
                if (!SafeName(relative) || !relative.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Owned payload is outside the managed asset directory.");
                Add(new(relative, Path.GetFullPath(item.FilePath)));
            }
            if (item.Clipboard is { } clipboard)
                foreach (var blob in clipboard.Formats)
                {
                    var name = "assets/clipboard/" + blob.Hash + ".bin";
                    Add(new(name, Path.Combine(root, "assets", "clipboard", blob.Hash + ".bin"), blob.Length, blob.Hash));
                }
        }
        foreach (var hash in state.Settings.SavedStyles.Select(s => s.Style).Prepend(state.Settings).SelectMany(s => s.PngIcons.Values).Distinct(StringComparer.OrdinalIgnoreCase))
            Add(new("icons/" + hash + ".png", Path.Combine(root, "icons", hash + ".png"), Hash: hash));
        if (references.Count > MaximumEntries - 1) throw new InvalidDataException("Too many managed assets for one backup.");
        return references.Values.OrderBy(r => r.Name, StringComparer.Ordinal).ToArray();
    }
    private static bool SafeName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 8192 &&
        !name.Contains('\\') && !name.Contains(':') && !name.Any(char.IsControl) &&
        name.Split('/').All(part => part.Length > 0 && part is not ("." or ".."));
    private static void CheckManagedPath(string root, string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("A managed payload is a symbolic link or redirected path.");
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase)) return;
        }
        throw new InvalidDataException("Managed path escaped its data directory.");
    }
    private static string CopyHash(Stream input, Stream? output, long length, CancellationToken cancellation)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920); long read = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                var count = input.Read(buffer, 0, buffer.Length); if (count == 0) break;
                read += count;
                if (read > length) throw new InvalidDataException("A backup payload changed or exceeds its declared size.");
                hash.AppendData(buffer, 0, count); output?.Write(buffer, 0, count);
            }
            if (read != length) throw new InvalidDataException("A backup payload is incomplete.");
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
    public static BackupSummary Create(string dataRoot, byte[] snapshot, string destination, Action<BackupProgress>? progress = null, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (snapshot.Length is 0 or > MaximumMetadata) throw new InvalidDataException("Library metadata exceeds the backup limit.");
        var state = StateStore.ReadSnapshot(snapshot);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)); var output = Path.GetFullPath(destination);
        var relativeOutput = Path.GetRelativePath(root, output).Replace(Path.DirectorySeparatorChar, '/');
        if (!Path.IsPathRooted(relativeOutput) && relativeOutput.Split('/')[0] != ".." ||
            state.Items.Concat(state.Trash.Select(t => t.Item)).Any(i => i.FilePath is not null && Path.IsPathFullyQualified(i.FilePath) && Path.GetFullPath(i.FilePath).Equals(output, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Choose a backup destination outside ClipDrop's data folder and original files.");
        // A fresh filename is intentional. Lexical path guards alone cannot
        // recognize every directory junction/alias of live metadata or originals.
        // The final no-overwrite move also closes a check/publication race.
        if (File.Exists(output) || Directory.Exists(output)) throw new IOException("A file already exists. Choose a new backup filename.");
        var references = References(root, state);
        var manifest = new Manifest { FormatVersion = 1, DataSchema = state.SchemaVersion, SourceRoot = root, CreatedUtc = DateTimeOffset.UtcNow };
        var temporary = output + "." + Guid.NewGuid().ToString("N") + ".backup-tmp";
        long total = snapshot.Length; var files = 0; var reported = Stopwatch.StartNew();
        progress?.Invoke(new("Creating", 0, references.Length, 0));
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
                {
                    using (var metadata = archive.CreateEntry("shelf.json", CompressionLevel.Fastest).Open()) metadata.Write(snapshot);
                    manifest.Entries.Add(new("shelf.json", snapshot.Length, Convert.ToHexString(SHA256.HashData(snapshot))));
                    foreach (var reference in references)
                    {
                        cancellation.ThrowIfCancellationRequested(); CheckManagedPath(root, reference.Path);
                        using var input = new FileStream(reference.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var length = input.Length;
                        if (length > MaximumAsset || reference.Length.HasValue && reference.Length != length || total + length > MaximumTotal)
                            throw new InvalidDataException("A managed asset changed or exceeds the backup size limit.");
                        using var target = archive.CreateEntry(reference.Name, CompressionLevel.Fastest).Open();
                        var hash = CopyHash(input, target, length, cancellation);
                        if (reference.Hash is not null && !hash.Equals(reference.Hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Managed clipboard or icon bytes no longer match their hash.");
                        manifest.Entries.Add(new(reference.Name, length, hash)); total += length; files++;
                        if (reported.ElapsedMilliseconds >= 100) { progress?.Invoke(new("Creating", files, references.Length, total)); reported.Restart(); }
                    }
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, Json);
                    if (bytes.Length > MaximumManifest) throw new InvalidDataException("Backup manifest exceeds its size limit.");
                    using var header = archive.CreateEntry("manifest.json", CompressionLevel.Fastest).Open(); header.Write(bytes);
                }
                file.Flush(true);
            }
            cancellation.ThrowIfCancellationRequested(); progress?.Invoke(new("Verifying", files, references.Length, total));
            var result = Verify(temporary, cancellation);
            cancellation.ThrowIfCancellationRequested(); File.Move(temporary, output, false);
            progress?.Invoke(new("Complete", files, references.Length, total)); return result;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static byte[] ReadEntry(ZipArchiveEntry entry, int maximum)
    {
        if (entry.Length <= 0 || entry.Length > maximum) throw new InvalidDataException("Backup metadata exceeds its size limit.");
        var bytes = new byte[(int)entry.Length]; using var input = entry.Open(); input.ReadExactly(bytes);
        if (input.ReadByte() != -1) throw new InvalidDataException("Backup metadata has an invalid length.");
        return bytes;
    }
    public static BackupSummary Verify(string path, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        PreflightDirectory(file, cancellation);
        file.Position = 0;
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        if (archive.Entries.Count is < 2 or > MaximumEntries + 1 || archive.Entries.Any(e => !SafeName(e.FullName)) ||
            archive.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != archive.Entries.Count)
            throw new InvalidDataException("Invalid or duplicate backup entry paths.");
        var header = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("Backup manifest is missing.");
        var manifest = JsonSerializer.Deserialize<Manifest>(ReadEntry(header, MaximumManifest), Json) ?? throw new InvalidDataException("Backup manifest is empty.");
        if (manifest.FormatVersion != 1) throw new NotSupportedException("This backup needs a newer ClipDrop.");
        if (manifest.SourceRoot is null || !Path.IsPathFullyQualified(manifest.SourceRoot) || manifest.Entries is null || manifest.Entries.Count is < 1 or > MaximumEntries ||
            manifest.Entries.Any(e => e is null || !SafeName(e.Name) || e.Name == "manifest.json" || e.Length < 0 || e.Length > MaximumAsset || e.Sha256 is null || e.Sha256.Length != 64 || !e.Sha256.All(Uri.IsHexDigit)) ||
            manifest.Entries.Select(e => e.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Entries.Count || manifest.Entries.Sum(e => e.Length) > MaximumTotal)
            throw new InvalidDataException("Invalid backup manifest.");
        var metadata = archive.GetEntry("shelf.json") ?? throw new InvalidDataException("Library metadata is missing.");
        var state = StateStore.ReadSnapshot(ReadEntry(metadata, MaximumMetadata));
        if (state.SchemaVersion != manifest.DataSchema) throw new InvalidDataException("Backup data schema does not match its manifest.");
        var references = References(manifest.SourceRoot, state).ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
        var names = references.Keys.Append("shelf.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (names.Count != manifest.Entries.Count || !names.SetEquals(manifest.Entries.Select(e => e.Name)) || archive.Entries.Count != manifest.Entries.Count + 1)
            throw new InvalidDataException("Backup payload membership is incomplete or unexpected.");
        foreach (var item in manifest.Entries)
        {
            cancellation.ThrowIfCancellationRequested();
            if (references.TryGetValue(item.Name, out var reference) &&
                (reference.Length.HasValue && reference.Length != item.Length || reference.Hash is not null && !reference.Hash.Equals(item.Sha256, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Backup payload metadata conflicts with its clipboard or icon reference.");
            var entry = archive.GetEntry(item.Name) ?? throw new InvalidDataException("A backup payload is missing.");
            if (entry.Length != item.Length) throw new InvalidDataException("A backup payload has an invalid length.");
            using var input = entry.Open(); var hash = CopyHash(input, null, item.Length, cancellation);
            if (!hash.Equals(item.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("A backup payload failed its checksum.");
        }
        return new(state.Items.Count, state.Trash.Count, state.Panels.Count, names.Count - 1, manifest.Entries.Sum(e => e.Length));
    }
}
