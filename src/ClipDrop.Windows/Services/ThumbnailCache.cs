using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ClipDrop.Windows.Services;

public sealed class ThumbnailCache
{
    private readonly ConcurrentDictionary<string, BitmapSource> _cache = new();
    private readonly SemaphoreSlim _workers = new(2);
    public async Task<BitmapSource?> Get(string path)
    {
        if (_cache.TryGetValue(path, out var cached)) return cached;
        await _workers.WaitAsync();
        try
        {
            return await Task.Run(() => {
                try
                {
                    if (!File.Exists(path) || new FileInfo(path).Length > ImageFiles.MaximumBytes) return null;
                    using var input = File.OpenRead(path);
                    var image = new BitmapImage();
                    image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 96; image.StreamSource = input; image.EndInit(); image.Freeze();
                    if (_cache.Count > 128) _cache.Clear();
                    _cache[path] = image;
                    return (BitmapSource)image;
                }
                catch (Exception ex) when (ex is IOException or NotSupportedException or System.Runtime.InteropServices.COMException or ArgumentException)
                { return null; }
            });
        }
        finally { _workers.Release(); }
    }
}
