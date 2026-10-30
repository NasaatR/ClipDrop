using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class ImageStackProbe
{
    // Original synthetic 3x2 lossless WebP, encoded by Pillow. Transparent,
    // half-transparent and opaque pixels are independent decoder fixtures.
    private static readonly byte[] WebP = Convert.FromBase64String("UklGRjYAAABXRUJQVlA4TCkAAAAvAkAAEBcwMgKBJLb9EYYRCCTRDTa5gKDouuUC3jsoBBAARRRNRP9jAAA=");
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Reply = (_, _) => Task.FromResult(Response());
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Interlocked.Increment(ref Calls); return Reply(request, token); }
    }
    private static HttpResponseMessage Response()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(WebP) };
        response.Content.Headers.ContentType = new("image/webp"); return response;
    }
    private static T Await<T>(Task<T> task)
    { if (!UiProbe.Until(() => task.IsCompleted, 8000)) throw new Exception("Image task did not finish"); return task.GetAwaiter().GetResult(); }
    private static Button Pin(DockWindow dock, string id) => UiProbe.Find<Button>(dock.Pins, b => b.Tag as string != "Keep" && b.DataContext is ItemRow row && row.Members.Any(i => i.Id == id));
    private static bool HasPreview(DockWindow dock, string id) => dock.Pins.Items.Cast<ItemRow>().Any(r => r.Members.Any(i => i.Id == id)) &&
        UiProbe.Find<Image>(dock.Pins, i => i.Name == "Thumbnail" && i.DataContext is ItemRow r && r.Members.Any(c => c.Id == id)).Source is not null;
    public static void Run(MainWindow oldLibrary, DockWindow oldDock, string root, string output, Action<bool, string> check, Action<FrameworkElement, string> render)
    {
        var decoded = ImageFiles.Load(WebP); var pixels = new byte[24]; decoded.CopyPixels(pixels, 12, 0);
        check(decoded.IsFrozen && decoded.PixelWidth == 3 && decoded.PixelHeight == 2 && pixels[3] == 0 && pixels[7] is >= 127 and <= 129,
            "Bundled WebP decoding preserves dimensions and transparent/half-transparent alpha without an OS codec");
        check(ImageFiles.Load(WebP, 2).PixelWidth <= 2 && ImageFiles.Extension(WebP) == ".webp",
            "WebP thumbnails are bounded and original encoding is identified from bytes");
        var invalidPng = new DataObject(); invalidPng.SetData("PNG", new MemoryStream(new byte[] { 1, 2, 3 }));
        invalidPng.SetData("HTML Format", "<img src='https://images.example.com/real.webp'>");
        var fallbackAssets = new AssetStore(Path.Combine(root, "format-fallback"));
        check(new TransferService(fallbackAssets).Import(invalidPng).Single().SourceUrl == "https://images.example.com/real.webp",
            "An invalid advertised PNG cannot hide an available browser image-source representation");
        var shortcut = Path.Combine(root, "browser-image.url"); File.WriteAllText(shortcut, "[InternetShortcut]\r\nURL=https://www.pinterest.com/pin/123/\r\n");
        var linkFile = new DataObject(); linkFile.SetFileDropList(new System.Collections.Specialized.StringCollection { shortcut });
        linkFile.SetData("HTML Format", "<img src='https://images.example.com/real.webp'>");
        var formats = new TransferService(fallbackAssets);
        check(formats.Import(linkFile, browserDrop: true).Single().SourceUrl == "https://images.example.com/real.webp" && formats.Import(linkFile).Single().FilePath == shortcut,
            "An explicit browser image drop prefers its image source over a shortcut while clipboard file import stays intact");
        var unsupported = Path.Combine(root, "unsupported-owned.webp"); File.WriteAllBytes(unsupported, new byte[] { 1, 2, 3 });
        var fallback = new TransferService(fallbackAssets).BuildDrag(new[] { new ShelfItem { Kind = ItemKind.Image, OwnedAsset = true, FilePath = unsupported } });
        check(fallback.GetFileDropList().Cast<string>().Single() == unsupported && File.ReadAllBytes(unsupported).SequenceEqual(new byte[] { 1, 2, 3 }),
            "An undecodable owned image still transfers as its intact original file");
        check(Await(ThumbnailCache.Shared.Get(unsupported)) is null,
            "Invalid image preview data returns a bounded fallback instead of faulting the UI callback");
        using (var blocked = new HttpClient(new Handler()))
        {
            var refused = false; try { Await(new BrowserImageLoader(blocked).Load(new Uri("http://127.0.0.1/a.webp"), default)); } catch (HttpRequestException) { refused = true; }
            check(refused, "Image retrieval rejects literal local endpoints before dispatch");
        }
        var redirect = new Handler { Reply = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("http://127.0.0.1/a.webp") } }) };
        using (var client = new HttpClient(redirect))
        {
            var refused = false; try { Await(new BrowserImageLoader(client).Load(new Uri("https://images.example.com/a.webp"), default)); } catch (HttpRequestException) { refused = true; }
            check(refused && redirect.Calls == 1, "Image redirects cannot bypass the public-address boundary");
        }
        oldLibrary.Hide(); oldDock.HideDock();
        var folder = Path.Combine(root, "webp-stack"); Directory.CreateDirectory(folder);
        var webp = Path.Combine(folder, "reference.webp"); File.WriteAllBytes(webp, WebP);
        var png = Path.Combine(folder, "target.png"); File.WriteAllBytes(png, ImageFiles.Encode(decoded));
        var store = new StateStore(Path.Combine(root, "image-stack-ui")); var state = store.Load();
        state.Settings.AnimationsEnabled = false; state.Settings.CollapseWhenIdle = false; state.Settings.DockLeft = 45; state.Settings.DockTop = 70;
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler { Reply = (_, token) => response.Task.WaitAsync(token) };
        using var http = new HttpClient(handler); using var session = new ShelfSession(store, state, new BrowserImageLoader(http));
        var first = ShelfItem.FromPath(png); first.KeepAfterUse = true; var second = ShelfItem.FromPath(webp);
        session.Add(new[] { first, second }); var dock = new DockWindow(session, () => { }, () => { }); dock.ShowDock();
        var source = new Button { Content = "Browser image source" }; IDataObject browserData = new DataObject(); Point? origin = null;
        source.PreviewMouseLeftButtonDown += (_, e) => origin = e.GetPosition(source);
        source.PreviewMouseMove += (_, e) => {
            if (origin is not Point start || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
            var point = e.GetPosition(source); if (Math.Abs(point.X - start.X) < 5 && Math.Abs(point.Y - start.Y) < 5) return;
            origin = null; source.ReleaseMouseCapture(); DragDrop.DoDragDrop(source, browserData, DragDropEffects.Copy);
        };
        var area = WindowPlacement.WorkArea(dock);
        var sourceWindow = new Window { Content = source, Width = 230, Height = 120, Left = area.Right - 260, Top = area.Top + 220, ShowInTaskbar = false };
        var receiver = new Border { Background = Brushes.SteelBlue, AllowDrop = true }; string[]? received = null;
        receiver.DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        receiver.Drop += (_, e) => { received = e.Data.GetData(DataFormats.FileDrop) as string[]; e.Effects = DragDropEffects.Copy; e.Handled = true; };
        var receiverWindow = new Window { Content = receiver, Width = 230, Height = 130, Left = area.Right - 260, Top = area.Top + 400, ShowInTaskbar = false };
        try
        {
            check(UiProbe.Until(() => HasPreview(dock, second.Id)), "A local dropped WebP renders a real dock thumbnail");
            UiProbe.Hover(Pin(dock, second.Id)); UiProbe.CaptureWindow(dock, Path.Combine(output, "dock-webp-screen.png"));
            var target = Pin(dock, first.Id); var center = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
            UiProbe.DragTo(Pin(dock, second.Id), center);
            check(dock.Pins.Items.Count == 1 && ((ItemRow)dock.Pins.Items[0]).Members.Length == 2 && state.Items.Count == 2 && state.Trash.Count == 0,
                "A real center drop combines clips into one counted stack without duplicating payloads");
            check(first.KeepAfterUse && second.KeepAfterUse, "A native center drop onto a kept clip protects the complete new stack");
            Clipboard.SetText("Opening a stack must not paste"); var card = Pin(dock, first.Id); UiProbe.Click(card);
            check(card.ContextMenu?.IsOpen == true && card.ContextMenu.Items.OfType<MenuItem>().Count(m => (m.Tag as string)?.StartsWith("StackMember:") == true) == 2 && Clipboard.GetText() == "Opening a stack must not paste",
                "Clicking a stack opens its captured member list without changing the clipboard or consuming clips");
            render(card.ContextMenu!, Path.Combine(output, "stack-open-menu.png"));
            UiProbe.Click(card.ContextMenu!.Items.OfType<MenuItem>().Single(m => m.Tag as string == "SplitStack"));
            check(dock.Pins.Items.Count == 2 && state.Items.Count == 2 && state.Trash.Count == 0,
                "The opened stack menu splits clips back into separate cards");
            target = Pin(dock, first.Id); var edge = target.PointToScreen(new Point(1, target.ActualHeight / 2));
            UiProbe.DragTo(Pin(dock, second.Id), edge);
            check(dock.Pins.Items.Count == 2 && PanelSets.Active(state).Stacks.Count == 0 && session.Pins()[0].Id == second.Id,
                "A real edge drop inserts a separate clip instead of stacking");
            target = Pin(dock, first.Id); center = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
            UiProbe.DragTo(Pin(dock, second.Id), center); var stackId = PanelSets.Active(state).Stacks.Single().Id;
            UiProbe.Hover(Pin(dock, first.Id)); UiProbe.CaptureWindow(dock, Path.Combine(output, "dock-stack-screen.png"));
            receiverWindow.Show();
            UiProbe.DragTo(Pin(dock, first.Id), receiver.PointToScreen(new Point(110, 70)));
            check(received is { Length: 2 } && state.Items.Count == 2 && state.Trash.Count == 0,
                "Using a stack created from a kept target retains every inherited protected member");
            first.KeepAfterUse = false; session.Change();
            UiProbe.DragTo(Pin(dock, first.Id), receiver.PointToScreen(new Point(110, 70)));
            check(received is { Length: 2 } && received.Contains(webp) && received.Contains(png) && state.Items.Single().Id == second.Id && state.Trash.Single().Item.Id == first.Id,
                "Dragging a stack transfers both original files and archives only its unprotected member");
            session.UndoRemove();
            check(PanelSets.Active(state).Stacks.Single().Id == stackId && ((ItemRow)dock.Pins.Items[0]).Members.Length == 2 && File.ReadAllBytes(webp).SequenceEqual(WebP),
                "Undo restores the same stack and source WebP bytes remain untouched");
            var html = new DataObject(); html.SetText("https://www.pinterest.com/pin/123/");
            html.SetData("HTML Format", "<img src='https://images.example.com/original.webp'>"); browserData = html; sourceWindow.Show();
            UiProbe.DragTo(source, dock.QuickSave.PointToScreen(new Point(18, 18)));
            check(UiProbe.Until(() => handler.Calls == 1) && session.PendingImageCount == 1,
                "A native HTML-image drop captures the real source URL and starts one background retrieval");
            var pending = state.Items.Single(i => i.SourceUrl is not null); var rowBefore = dock.Pins.Items.Cast<ItemRow>().Single(r => r.Item.Id == pending.Id);
            session.Capture(html); check(handler.Calls == 1 && state.Items.Count == 3, "Repeated drops of a pending image reuse its clip and request");
            response.SetResult(Response());
            check(UiProbe.Until(() => pending.Kind == ItemKind.Image && pending.FilePath is not null && HasPreview(dock, pending.Id), 6000) &&
                ReferenceEquals(rowBefore, dock.Pins.Items.Cast<ItemRow>().Single(r => r.Item.Id == pending.Id)),
                "Background retrieval upgrades the same tile to a real WebP image and refreshes its preview");
            check(Path.GetExtension(pending.FilePath) == ".webp" && File.ReadAllBytes(pending.FilePath!).SequenceEqual(WebP) && session.Copy(new[] { pending }) && Clipboard.ContainsImage() && Clipboard.GetDataObject()!.GetDataPresent("PNG"),
                "A fetched image keeps original WebP bytes and copies as image formats instead of URL text");
            var pageOnly = new DataObject(); pageOnly.SetText("https://www.pinterest.com/pin/456/"); session.Capture(pageOnly);
            check(state.Items.Single(i => i.Content == "https://www.pinterest.com/pin/456/").Kind == ItemKind.Link && session.PendingImageCount == 0,
                "A Pinterest page-only drop stays a link without scraping or speculative image requests");
            state.Settings.FetchDroppedImages = false;
            var noFetch = new DataObject(); noFetch.SetText("https://images.example.com/disabled.webp"); session.Capture(noFetch);
            check(handler.Calls == 1 && state.Items.Single(i => i.Content.EndsWith("disabled.webp")).Kind == ItemKind.Link,
                "Disabling fetch applies live and keeps dropped image URLs as ordinary links");
            state.Settings.FetchDroppedImages = true;
            handler.Reply = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            var denied = new DataObject(); denied.SetText("https://images.example.com/denied.webp"); session.Capture(denied);
            check(UiProbe.Until(() => session.PendingImageCount == 0) && state.Items.Single(i => i.Content.EndsWith("denied.webp")).Kind == ItemKind.Link,
                "An unavailable or authenticated image retains its recoverable source link");
            var held = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            handler.Reply = (_, token) => held.Task.WaitAsync(token);
            var late = new DataObject(); late.SetText("https://images.example.com/late.webp"); session.Capture(late);
            var removed = state.Items.Single(i => i.Content.EndsWith("late.webp")); session.Remove(new() { removed }); held.SetResult(Response());
            check(UiProbe.Until(() => session.PendingImageCount == 0) && state.Trash.Any(t => t.Item == removed && t.Item.Kind == ItemKind.Link) && !state.Items.Contains(removed),
                "A late image completion cannot resurrect a clip removed while retrieval was pending");
            state.Settings.Theme = "Light"; state.Settings.DockPalette = "Light"; session.Change(); UiProbe.Hover(Pin(dock, first.Id));
            UiProbe.CaptureWindow(dock, Path.Combine(output, "dock-stack-light-screen.png"));
        }
        finally
        {
            PopupDismiss.CloseAll(); dock.DisposeView(); sourceWindow.Close(); receiverWindow.Close();
            IconAssets.Initialize(root); oldDock.ShowDock(); oldLibrary.ShowShelf();
        }
    }
}
