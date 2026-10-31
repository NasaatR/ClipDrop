using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class HoverProbe
{
    private static double Scale(FrameworkElement icon) => icon.RenderTransform is TransformGroup group ? group.Children.OfType<ScaleTransform>().Single().ScaleX : 1;
    private static bool Running(FrameworkElement icon) => icon.RenderTransform is TransformGroup group && group.Children.Any(t => t.HasAnimatedProperties);
    public static void Run(MainWindow library, DockWindow original, string root, string output, Action<bool, string> check)
    {
        library.Hide(); original.HideDock();
        var settings = new ShelfSettings { AnimationsEnabled = true, MotionDuration = 600, DownloadsRowHeight = 28 };
        var list = new ListBox { Background = Brushes.Transparent };
        list.SetResourceReference(ItemsControl.ItemContainerStyleProperty, "DownloadRowStyle");
        var template = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(DownloadRowView)) }; list.ItemTemplate = template;
        VirtualizingPanel.SetIsVirtualizing(list, true); VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling); VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);
        list.ItemsSource = Enumerable.Range(0, 80).Select(i => new DownloadsWindow.Row(new DownloadFile(Path.Combine(root, $"hover-{i}.webp"), $"Reference {i} — пример.webp", 1, DateTimeOffset.UtcNow), true)).ToArray();
        var header = new TextBlock { Text = "Downloads hover response", Height = 28 };
        var panel = new DockPanel(); DockPanel.SetDock(header, Dock.Top); panel.Children.Add(header); panel.Children.Add(list);
        var host = new Window { Content = panel, Width = 330, Height = 360, Left = 80, Top = 80, Topmost = true, ShowInTaskbar = false };
        Appearance.Apply(settings, host, true); DownloadsPresentation.Apply(host, settings);
        try
        {
            UiProbe.MoveTo(new Point(5, 5)); host.Show(); host.Activate(); UiProbe.Hover(header);
            var row = UiProbe.Find<ListBoxItem>(list, r => list.ItemContainerGenerator.IndexFromContainer(r) == 2);
            var icon = UiProbe.Find<FrameworkElement>(row, e => e.Name == "PreviewGraphic");
            check(icon.RenderTransform is not TransformGroup, "Inactive download rows do not allocate hover transform/clock state");
            var entered = new Stopwatch(); row.MouseEnter += (_, _) => entered.Restart();
            UiProbe.MoveTo(row.PointToScreen(new Point(row.ActualWidth / 2, row.ActualHeight / 2)), 1);
            check(UiProbe.Until(() => row.IsMouseOver, 500), "A real pointer enters the hover response fixture");
            if (Motion.Allowed)
            {
                check(UiProbe.Until(() => Scale(icon) >= 1.06, 200) && entered.ElapsedMilliseconds <= 100,
                    "Hover reaches visible feedback within100ms even when panel disclosure is configured to600ms");
                UiProbe.Pump(140);
                var bounds = icon.TransformToAncestor(row).TransformBounds(new Rect(icon.RenderSize));
                check(Scale(icon) == 1.16 && !Running(icon) && bounds.Top >= 0 && bounds.Bottom <= row.ActualHeight && bounds.Left >= 0,
                    "The stronger hover settles at1.16scale without clipping a minimum-height row");
                UiProbe.CaptureWindow(host, Path.Combine(output, "downloads-hover-emphasis.png"));
                UiProbe.MoveTo(header.PointToScreen(new Point(80, 12)), 1); var leaving = Stopwatch.StartNew();
                check(UiProbe.Until(() => Scale(icon) == 1 && !Running(icon), 300) && leaving.ElapsedMilliseconds <= 120,
                    "Leaving a row clears its transform quickly instead of leaving a long animated trail");
                UiProbe.Click(row);
                UiProbe.Hover(header); UiProbe.Pump(120);
                check(Scale(icon) == 1.16, "Keyboard hover fallback remains available when the pointer leaves the list");
                var next = UiProbe.Find<ListBoxItem>(list, r => list.ItemContainerGenerator.IndexFromContainer(r) == 3); UiProbe.Hover(next); UiProbe.Pump(120);
                check(row.IsKeyboardFocusWithin && Scale(icon) == 1,
                    "Keyboard focus does not hold a second icon hover while the pointer moves through other rows");
                for (var index = 4; index < 8; index++)
                {
                    var sweep = UiProbe.Find<ListBoxItem>(list, r => list.ItemContainerGenerator.IndexFromContainer(r) == index);
                    UiProbe.MoveTo(sweep.PointToScreen(new Point(sweep.ActualWidth / 2, sweep.ActualHeight / 2)), 12);
                }
                UiProbe.Hover(header); UiProbe.Pump(100);
                check(Enumerable.Range(0, 8).Select(i => list.ItemContainerGenerator.ContainerFromIndex(i)).OfType<ListBoxItem>().Where(r => !r.IsKeyboardFocusWithin)
                    .Select(r => UiProbe.Find<FrameworkElement>(r, e => e.Name == "PreviewGraphic")).All(i => Scale(i) == 1 && !Running(i)),
                    "A rapid pointer sweep leaves no stale transforms or clocks on previous rows");
                UiProbe.Hover(row); UiProbe.Pump(120);
                var view = UiProbe.Find<DownloadRowView>(row, _ => true);
                view.DataContext = new DownloadsWindow.Row(new DownloadFile(Path.Combine(root, "recycled.webp"), "Recycled example", 1, DateTimeOffset.UtcNow), true);
                check(Scale(icon) == 1.16 && !Running(icon), "Rebinding a hovered recycled row keeps the pointer effect instead of restarting from neutral");
                var scroll = UiProbe.Find<ScrollViewer>(list, _ => true); scroll.ScrollToVerticalOffset(32); UiProbe.Pump(160);
                var hovered = Enumerable.Range(0, 15).Select(i => list.ItemContainerGenerator.ContainerFromIndex(i)).OfType<ListBoxItem>().Where(r => r.IsMouseOver).ToArray();
                check(hovered.Length == 1 && Scale(UiProbe.Find<FrameworkElement>(hovered[0], e => e.Name == "PreviewGraphic")) == 1.16,
                    "Scrolling under a stationary pointer keeps exactly one correctly emphasized row");
            }
            Motion.Enabled = false; UiProbe.Pump();
            check(!Running(icon) && Scale(icon) == 1, "Disabling motion immediately clears fast hover clocks and restores neutral scale");
            var path = Path.Combine(root, "thumbnail-freshness.png");
            File.WriteAllBytes(path, ImageFiles.Encode(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 40, 80, 120, 255 }, 4)));
            var loads = Enumerable.Range(0, 16).Select(_ => ThumbnailCache.Shared.Get(path)).ToArray(); var batch = Task.WhenAll(loads);
            check(UiProbe.Until(() => batch.IsCompleted, 5000) && batch.GetAwaiter().GetResult().All(b => b is not null && ReferenceEquals(b, loads[0].Result)),
                "Concurrent thumbnail requests share the decoded image without blocking the UI");
            var before = loads[0].Result;
            File.WriteAllBytes(path, ImageFiles.Encode(BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 80, 120, 40, 255, 30, 60, 90, 255 }, 8)));
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2)); var changed = ThumbnailCache.Shared.Get(path);
            check(UiProbe.Until(() => changed.IsCompleted, 5000) && changed.Result is { PixelWidth: 2 } && !ReferenceEquals(changed.Result, before),
                "A replaced thumbnail path is revalidated immediately without a stale time-based shortcut");
            File.Delete(path); var deleted = ThumbnailCache.Shared.Get(path);
            check(UiProbe.Until(() => deleted.IsCompleted, 5000) && deleted.Result is null, "A deleted original does not retain a falsely available cached thumbnail");
        }
        finally { host.Close(); IconAssets.Initialize(root); original.ShowDock(); library.ShowShelf(); }
    }
}
