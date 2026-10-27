using System;
using System.Collections.Concurrent;
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
    private readonly SemaphoreSlim _images = new(2);
    public async Task<BitmapSource?> Get(string path)
    {
        // Stat off the dispatcher; length/write time invalidates replaced files.
        var stamp = await Task.Run(() => {
            try { var file = new FileInfo(path); return file.Exists ? $"{path}|{file.Length}|{file.LastWriteTimeUtc.Ticks}" : null; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
        });
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
        finally { _pending.TryRemove(stamp, out _); }
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
                    var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 160; image.StreamSource = input; image.EndInit(); image.Freeze(); return (BitmapSource)image;
                }
                catch (Exception ex) when (ex is IOException or NotSupportedException or System.Runtime.InteropServices.COMException or ArgumentException or UnauthorizedAccessException)
                { return null; }
            });
        }
        finally { _images.Release(); }
    }
}
