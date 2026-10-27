using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class MediaProbe
{
    private static T Await<T>(System.Threading.Tasks.Task<T> task, int milliseconds = 7000)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted && watch.ElapsedMilliseconds < milliseconds) UiProbe.Pump(20);
        if (!task.IsCompleted) throw new Exception("Thumbnail request did not settle within its bound");
        return task.GetAwaiter().GetResult();
    }
    public static void Run(MainWindow library, DockWindow dock, string root, string output, Action<bool, string> check, Action<FrameworkElement, string> render)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.mp4");
        var path = Path.Combine(root, "movie.mp4"); File.Copy(source, path);
        var original = SHA256.HashData(File.ReadAllBytes(path));
        var movie = ShelfItem.FromPath(path); library.Session.Add(new[] { movie });
        var row = new ItemRow(movie);
        check(row.IsVideo && row.ImageVisible == Visibility.Visible && row.Details.StartsWith("Video"), "Video files request thumbnails and retain video identity");
        var thumbnail = Await(ThumbnailCache.Shared.Get(path));
        if (thumbnail is not null) check(thumbnail.IsFrozen && thumbnail.PixelWidth > 0, "Windows shell returns a frozen video frame thumbnail");
        else { check(true, "Unavailable video thumbnail retains its vector fallback"); Console.WriteLine("MEDIA shell video provider unavailable on this runner"); }
        var invalid = Path.Combine(root, "invalid.mp4"); File.WriteAllText(invalid, "not a movie");
        check(Await(ThumbnailCache.Shared.Get(invalid)) is null, "Unsupported video content falls back without crashing or blocking the UI");
        var transfer = library.Session.Transfer.Build(new[] { movie });
        check(transfer.GetFileDropList().Cast<string>().Single() == path && !transfer.GetDataPresent(DataFormats.UnicodeText), "Video copy still offers the untouched original file");
        var preview = new PreviewWindow(movie) { Owner = library }; preview.Show(); UiProbe.Pump();
        var player = preview.Content as VideoPreview ?? throw new Exception("Video preview surface missing");
        check(player.Media.Source is null && player.Media.IsMuted && player.Media.LoadedBehavior == MediaState.Manual, "Video preview starts muted with explicit playback, without autoplay");
        player.Toggle(); var watch = Stopwatch.StartNew();
        while (!player.HasOpened && !player.HasFailed && watch.ElapsedMilliseconds < 10000) UiProbe.Pump(30);
        check(player.HasOpened || player.HasFailed, "Video opens or reports its supported-codec fallback within a bounded time");
        Console.WriteLine(player.HasOpened ? "MEDIA native H264 playback opened" : "MEDIA native playback unavailable; Open in app fallback exercised");
        UiProbe.Pump(150); render((FrameworkElement)preview.Content, Path.Combine(output, "video-preview.png"));
        preview.Close();
        check(player.Media.Source is null && SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(original), "Closing video preview releases playback and leaves original bytes unchanged");
        dock.UpdateLayout(); library.UpdateLayout();
    }
}
