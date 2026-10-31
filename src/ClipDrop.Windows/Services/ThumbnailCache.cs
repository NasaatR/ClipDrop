using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ClipDrop.Core;
namespace ClipDrop.Windows.Services;

public sealed class ThumbnailCache
{
    public static ThumbnailCache Shared { get; } = new();
    private sealed record Entry(BitmapSource? Image, DateTimeOffset Expires);
    private readonly ConcurrentDictionary<string, Entry> _cache = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<BitmapSource?>>> _pending = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _stamps = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _images = new(2);
    public async Task<BitmapSource?> Get(string path)
    {
        // Shared windows/recycled rows validate a path through one in-flight stat.
        // Do not cache stat results over time: a replaced file must be detected.
        var lookup = _stamps.GetOrAdd(path, key => new Lazy<Task<string?>>(() => Task.Run(() => {
            try { var file = new FileInfo(key); return file.Exists ? $"{key}|{file.Length}|{file.LastWriteTimeUtc.Ticks}" : null; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
        }), LazyThreadSafetyMode.ExecutionAndPublication));
        string? stamp;
        try { stamp = await lookup.Value; }
        finally { ((ICollection<KeyValuePair<string, Lazy<Task<string?>>>>)_stamps).Remove(new(path, lookup)); }
        if (stamp is null) return null;
        if (_cache.TryGetValue(stamp, out var cached) && cached.Expires > DateTimeOffset.UtcNow) return cached.Image;
        var pending = _pending.GetOrAdd(stamp, _ => new Lazy<Task<BitmapSource?>>(() => Load(path), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            var image = await pending.Value;
            if (_cache.Count >= 128) _cache.Clear();
            _cache[stamp] = new Entry(image, image is null ? DateTimeOffset.UtcNow.AddSeconds(10) : DateTimeOffset.MaxValue);
            return image;
        }
        finally { ((ICollection<KeyValuePair<string, Lazy<Task<BitmapSource?>>>>)_pending).Remove(new(stamp, pending)); }
    }
    private async Task<BitmapSource?> Load(string path)
    {
        if (MediaFiles.IsVideo(path)) return await ShellThumbnails.Get(path);
        await _images.WaitAsync();
        try
        {
            return await Task.Run(() => {
                try
                {
                    if (new FileInfo(path).Length > ImageFiles.MaximumBytes) return null;
                    using var input = File.OpenRead(path);
                    return ImageFiles.Load(ImageFiles.ReadStream(input), 160);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or System.Runtime.InteropServices.COMException or ArgumentException or UnauthorizedAccessException)
                { return null; }
            });
        }
        finally { _images.Release(); }
    }
}
