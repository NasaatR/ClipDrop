using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public sealed partial class ShelfSession
{
    private readonly BrowserImageLoader _browserImages;
    private readonly CancellationTokenSource _imageStop = new();
    private readonly HashSet<string> _imageRequests = new();
    public int PendingImageCount => _imageRequests.Count;
    private async void FetchImage(ShelfItem pending)
    {
        if (pending.SourceUrl is not string source || !Uri.TryCreate(source, UriKind.Absolute, out var uri) || !_imageRequests.Add(pending.Id)) return;
        Notify("Fetching dropped image · link kept until ready");
        try
        {
            var image = await _browserImages.Load(uri, _imageStop.Token);
            if (_disposed || !State.Items.Contains(pending) || pending.Kind != ItemKind.Link || pending.Content != source) return;
            var assets = new AssetStore(Path.Combine(Root, "assets"));
            var path = await Task.Run(() => assets.WriteImage(image.Bytes, image.Extension), _imageStop.Token);
            if (_disposed || !State.Items.Contains(pending) || pending.Kind != ItemKind.Link || pending.Content != source) return;
            var originalTitle = ShelfItem.FromText(source).Title;
            if (pending.Title == originalTitle)
            {
                var name = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
                pending.Title = name.Length is > 0 and <= 120 ? name : "Dropped image";
            }
            pending.Kind = ItemKind.Image; pending.Content = ""; pending.FilePath = path; pending.OwnedAsset = true; pending.Clipboard = null;
            Change(); Notify("Image ready · original encoding kept");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (_disposed || _imageStop.IsCancellationRequested) return;
            Diagnostics.Record("Fetch dropped image", ex); Notify("Image unavailable · link kept. Save the file or use the browser's image copy action.");
        }
        finally { _imageRequests.Remove(pending.Id); }
    }
}
