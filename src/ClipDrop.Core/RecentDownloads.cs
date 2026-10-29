using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ClipDrop.Core;

public sealed record DownloadFile(string Path, string Name, long Length, DateTimeOffset AddedUtc);
public sealed record DownloadSnapshot(DownloadFile[] Files, string? Notice = null, bool Settling = false);

// A view of one folder, never an import, browser-history reader or file mutator.
public static class RecentDownloads
{
    public const int Maximum = 40;
    private static readonly string[] TemporarySuffixes = { ".crdownload", ".part", ".partial", ".tmp", ".download", ".opdownload" };
    public static bool IsTemporary(string name) => name.StartsWith('.') ||
        TemporarySuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    public static DownloadSnapshot Scan(string folder, CancellationToken cancellation = default, DateTimeOffset? now = null)
    {
        var clock = now ?? DateTimeOffset.UtcNow;
        var ready = new SortedSet<DownloadFile>(Comparer<DownloadFile>.Create((a, b) => {
            var stamp = a.AddedUtc.CompareTo(b.AddedUtc);
            return stamp != 0 ? stamp : StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path);
        }));
        var settling = false; var visited = 0; var limited = false;
        try
        {
            if (!System.IO.Path.IsPathFullyQualified(folder) || !Directory.Exists(folder))
                return new(Array.Empty<DownloadFile>(), "Folder unavailable · choose a folder in Settings");
            var options = new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint };
            foreach (var path in Directory.EnumerateFiles(folder, "*", options))
            {
                cancellation.ThrowIfCancellationRequested();
                if (++visited > 50000) { limited = true; break; }
                if (IsTemporary(System.IO.Path.GetFileName(path))) continue;
                try
                {
                    var info = new FileInfo(path);
                    if (!info.Exists || info.Length == 0 || File.Exists(path + ".part") || File.Exists(path + ".crdownload")) continue;
                    var added = new DateTimeOffset(info.CreationTimeUtc > info.LastWriteTimeUtc ? info.CreationTimeUtc : info.LastWriteTimeUtc);
                    if (clock - added < TimeSpan.FromMilliseconds(800)) { settling = true; continue; }
                    var entry = new DownloadFile(path, info.Name, info.Length, added);
                    ready.Add(entry); if (ready.Count > Maximum) ready.Remove(ready.Min!);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            // Check only the small newest set. Shared reads reject active Windows
            // writers without reading the payload or locking it after this scan.
            var files = ready.Reverse().Where(file => {
                cancellation.ThrowIfCancellationRequested();
                try { using var input = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read); return input.Length == file.Length; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
            }).ToArray();
            return new(files, limited ? "Large folder · showing recent files from the scanned entries" : null, settling);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(Array.Empty<DownloadFile>(), "Folder unavailable · retry or choose another folder"); }
    }
}
