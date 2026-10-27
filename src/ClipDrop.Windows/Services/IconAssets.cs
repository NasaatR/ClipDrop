using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;

namespace ClipDrop.Windows.Services;

public static class IconAssets
{
    private static string _root = "";
    private static readonly Dictionary<string, BitmapSource?> Cache = new();
    public static void Initialize(string dataRoot)
    {
        var root = Path.Combine(dataRoot, "icons");
        if (_root == root) return;
        _root = root; Cache.Clear();
    }
    public static string Import(string file)
    {
        if (_root.Length == 0) throw new InvalidOperationException("Open a library before importing icons.");
        if (new FileInfo(file).Length > 5 * 1024 * 1024) throw new InvalidDataException("Choose a PNG smaller than 5 MB.");
        using var input = File.OpenRead(file);
        var bitmap = Decode(input) ?? throw new InvalidDataException("Choose a valid PNG up to 2048 × 2048 pixels.");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output);
        var bytes = output.ToArray(); var hash = Convert.ToHexString(SHA256.HashData(bytes));
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, hash + ".png");
        if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
        Cache.Remove(hash); return hash;
    }
    private static BitmapSource? Decode(Stream stream)
    {
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
        if (decoder is not PngBitmapDecoder || decoder.Frames.Count == 0) return null;
        var frame = decoder.Frames[0];
        if (frame.PixelWidth is < 1 or > 2048 || frame.PixelHeight is < 1 or > 2048) return null;
        stream.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream; image.DecodePixelWidth = Math.Min(128, frame.PixelWidth); image.EndInit(); image.Freeze(); return image;
    }
    public static BitmapSource? Get(string? hash)
    {
        if (hash is null || hash.Length != 64 || !hash.All(Uri.IsHexDigit) || _root.Length == 0) return null;
        if (Cache.TryGetValue(hash, out var cached)) return cached;
        BitmapSource? image = null;
        try
        {
            var path = Path.Combine(_root, hash + ".png");
            if (new FileInfo(path).Length <= 5 * 1024 * 1024) { using var file = File.OpenRead(path); image = Decode(file); }
        }
        catch (Exception ex) when (ex is FileFormatException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException) { }
        if (Cache.Count >= 64) Cache.Remove(Cache.Keys.First());
        Cache[hash] = image; return image;
    }
}
