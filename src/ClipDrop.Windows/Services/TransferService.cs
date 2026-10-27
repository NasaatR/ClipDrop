using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public sealed class TransferService
{
    private readonly AssetStore _assets;
    public TransferService(AssetStore assets) { _assets = assets; }
    public static bool Supports(IDataObject data) => !data.GetDataPresent("NasaatR.ClipDrop.Internal") &&
        data.GetFormats(false).Any(f => f is "FileDrop" or "PNG" or "Bitmap" or "UnicodeText" or "Text" or "FileGroupDescriptorW");

    public DataObject BuildDrag(IReadOnlyList<ShelfItem> items)
    {
        var data = Build(items);
        var paths = items.Where(i => i.FilePath is not null).Select(i => i.FilePath!).ToArray();
        foreach (var path in paths) if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("An original file is unavailable.");
        if (paths.Length > 0) { var files = new StringCollection(); files.AddRange(paths); data.SetFileDropList(files); }
        data.SetData("NasaatR.ClipDrop.Internal", "1"); return data;
    }

    public List<ShelfItem> Import(IDataObject data)
    {
        var result = new List<ShelfItem>();
        if (data.GetDataPresent("NasaatR.ClipDrop.TextCard", false) &&
            data.GetData(DataFormats.UnicodeText) is string ownText && !string.IsNullOrEmpty(ownText))
            return new List<ShelfItem> { ShelfItem.FromText(ownText) };
        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            foreach (var path in paths) result.Add(ShelfItem.FromPath(path));
            return result;
        }
        // Prefer original encoded PNG bytes; the Bitmap fallback can lose alpha in some source apps.
        if (data.GetDataPresent("PNG", false))
        {
            var value = data.GetData("PNG", false);
            var bytes = value switch { byte[] b => b, Stream s => ImageFiles.ReadStream(s), _ => null };
            if (bytes is not null)
            {
                ImageFiles.Load(bytes);
                return new List<ShelfItem> { CacheImage(bytes) };
            }
        }
        if (data.GetDataPresent(DataFormats.Bitmap) && data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
            return new List<ShelfItem> { CacheImage(ImageFiles.Encode(bitmap)) };

        var virtualFiles = VirtualFileReader.Read(data, _assets.Root);
        if (virtualFiles.Count > 0) return virtualFiles;

        var text = data.GetData(DataFormats.UnicodeText) as string
            ?? data.GetData(DataFormats.Text) as string;
        if (!string.IsNullOrEmpty(text)) result.Add(ShelfItem.FromText(text));
        return result;
    }

    private ShelfItem CacheImage(byte[] bytes) => new() {
        Kind = ItemKind.Image, Title = "Image " + DateTime.Now.ToString("HH-mm-ss"),
        FilePath = _assets.WriteImage(bytes), OwnedAsset = true
    };

    public DataObject Build(IReadOnlyList<ShelfItem> items)
    {
        if (items.Count == 0) throw new InvalidOperationException("Select an item first.");
        var data = new DataObject();
        var files = new StringCollection();
        foreach (var item in items.Where(i => i.FilePath is not null && (!i.OwnedAsset || items.Count > 1)))
        {
            var path = item.FilePath!;
            if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("An original file is unavailable. Restore it or remove its card.");
            files.Add(path);
        }
        if (files.Count > 0)
        {
            data.SetFileDropList(files);
            data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(1)), false);
        }
        if (items.Count == 1)
        {
            var item = items[0];
            if (item.Kind is ItemKind.Text or ItemKind.Link)
            {
                data.SetText(item.Content, TextDataFormat.UnicodeText);

                if (item.Kind == ItemKind.Link)
                {
                    data.SetData("UniformResourceLocatorW", new MemoryStream(System.Text.Encoding.Unicode.GetBytes(item.Content.Trim() + "\0")), false);
                }
            }
            if (item.Kind == ItemKind.Image && item.OwnedAsset && item.FilePath is not null)
            {
                var info = new FileInfo(item.FilePath);
                // Large files still transfer by path without loading their full contents.
                if (info.Length <= ImageFiles.MaximumBytes)
                {
                    var bytes = File.ReadAllBytes(item.FilePath);
                    try
                    {
                        var image = ImageFiles.Load(bytes);
                        var png = Path.GetExtension(item.FilePath).Equals(".png", StringComparison.OrdinalIgnoreCase)
                            ? bytes : ImageFiles.Encode(image);
                        data.SetData("PNG", new MemoryStream(png, false), false);
                        data.SetImage(image);
                    }
                    catch (Exception ex) when (ex is NotSupportedException or ArgumentException or FileFormatException or InvalidDataException)
                    {
                        // An undecodable/oversized image still transfers intact as its original file.
                    }
                }
            }
        }
        else if (items.Any(i => i.Kind is ItemKind.Text or ItemKind.Link))
            data.SetText(string.Join(Environment.NewLine, items.Where(i => i.Kind is ItemKind.Text or ItemKind.Link).Select(i => i.Content)));
        if (items.Count == 1 && items[0].Clipboard is { } snapshot)
            foreach (var format in snapshot.Formats.Where(f => f.StandardId == 0))
                data.SetData(format.Name, new MemoryStream(_assets.ReadBlob(format), false), false);
        if (data.GetFormats(false).Length == 0) throw new InvalidOperationException("Select one clipboard item to preserve its source formats.");
        return data;
    }
}
