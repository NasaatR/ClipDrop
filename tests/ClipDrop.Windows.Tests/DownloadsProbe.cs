using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class DownloadsProbe
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    private static ListBoxItem Row(DownloadsWindow stack, string path)
    {
        UiProbe.Hover(stack.StackButton); stack.DownloadList.UpdateLayout();
        return UiProbe.Find<ListBoxItem>(stack.DownloadList, c => c.DataContext is DownloadsWindow.Row r && r.File.Path == path);
    }
    private static bool HasSelector(DependencyObject node)
    {
        if (node is ToggleButton) return true;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) if (HasSelector(VisualTreeHelper.GetChild(node, i))) return true;
        return false;
    }
    private static void Group(DownloadsWindow stack, string a, string b)
    { UiProbe.Click(Row(stack, a)); UiProbe.Click(Row(stack, b), ModifierKeys.Control); }
    private static void Command(DownloadsWindow stack, string path, string label)
    {
        UiProbe.RightClick(Row(stack, path));
        UiProbe.Click(stack.DownloadList.ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Header as string == label));
    }
    public static void Run(MainWindow oldLibrary, DockWindow oldDock, string root, string output, Action<bool, string> check)
    {
        oldLibrary.Hide(); oldDock.HideDock();
        var folder = Path.Combine(root, "browser-downloads"); Directory.CreateDirectory(folder);
        var a = Path.Combine(folder, "Design reference with a long name — пример.png");
        File.WriteAllBytes(a, ImageFiles.Encode(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 120, 70, 220, 255 }, 4)));
        var b = Path.Combine(folder, "Layout.fig"); File.WriteAllText(b, "original Figma file bytes");
        File.WriteAllText(Path.Combine(folder, "unfinished.crdownload"), "partial");
        var store = new StateStore(Path.Combine(root, "downloads-ui")); var state = store.Load();
        state.Settings.AnimationsEnabled = false; state.Settings.DownloadsFolder = folder; state.Settings.CollapseWhenIdle = false;
        using var session = new ShelfSession(store, state); var library = new MainWindow(session);
        using var coordinator = new DockCoordinator(session, library);
        try
        {
            check(coordinator.DownloadsView is null, "Disabled downloads creates no stack or folder watcher");
            coordinator.OpenDownloads(); var stack = coordinator.DownloadsView!;
            check(UiProbe.Until(() => stack.DownloadList.Items.Count == 2, 6000), "Downloads opens with ready files and omits the browser partial file");
            UiProbe.Hover(stack.StackButton);
            check(!HasSelector(stack.DownloadList) && Row(stack, a).ActualHeight == 34,
                "Downloads uses compact 34-DIP rows without a checkbox column");
            check(state.Items.Count == 0 && state.Trash.Count == 0, "Scanning downloads does not import or archive Library clips");
            using (var writing = new FileStream(b, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                check(!RecentDownloads.Scan(folder, now: DateTimeOffset.UtcNow.AddSeconds(5)).Files.Any(f => f.Path == b),
                    "A file held by an active Windows writer is omitted from ready downloads");
            UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-expanded-screen.png"));
            check(((Border)stack.FindName("Details")).ActualHeight < 150, "Two ready downloads use a short single-line list without a reserved action footer");
            var receiver = new Border { Background = Brushes.SteelBlue, AllowDrop = true };
            var backgroundClicks = 0; receiver.MouseLeftButtonDown += (_, _) => backgroundClicks++;
            string[]? received = null;
            receiver.DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            receiver.Drop += (_, e) => { received = e.Data.GetData(DataFormats.FileDrop) as string[]; e.Effects = DragDropEffects.Copy; e.Handled = true; };
            var target = new Window { Content = receiver, Width = 250, Height = 160, Left = 30, Top = 80, ShowInTaskbar = false };
            target.Show();
            try
            {
                var destination = receiver.PointToScreen(new Point(110, 70));
                UiProbe.DragTo(Row(stack, a), destination);
                check(received is { Length: 1 } && received[0] == a && stack.DownloadList.SelectedItems.Count == 0 && stack.IsCompact,
                    "An unselected downloads row drags its original file immediately and dismisses the list afterward");
                received = null; UiProbe.DragTo(Row(stack, a), destination, cancel: true);
                check(received is null && state.Items.Count == 0 && state.Trash.Count == 0 && File.Exists(a),
                    "Cancelling a direct downloads drag keeps originals and does not create or archive Library clips");
                Group(stack, a, b);
                check(stack.DownloadList.SelectedItems.Count == 2, "Ctrl-click selects a downloads group without permanent selection controls");
                UiProbe.DragTo(Row(stack, a), destination);
                check(received is { Length: 2 } && received.Contains(a) && received.Contains(b) && state.Trash.Count == 0 && File.ReadAllText(b) == "original Figma file bytes",
                    "Native downloads group drag exposes original FileDrop paths and keeps files and Trash unchanged");
                Group(stack, a, b); Command(stack, a, "Copy 2");
                check(Clipboard.GetFileDropList().Count == 2 && stack.IsCompact, "Contextual Downloads Copy keeps native file formats and dismisses the list");
                Group(stack, a, b); Command(stack, a, "Save to Library");
                check(state.Items.Count == 2 && state.Items.All(i => !i.IsPinned) && state.Trash.Count == 0,
                    "Contextual saving downloads to Library is explicit and does not append them to the dock");
                var window = coordinator.Views.First(); window.ShowDock();
                Group(stack, a, b); UiProbe.DragTo(Row(stack, a), window.QuickSave.PointToScreen(new Point(18, 18)));
                check(window.Pins.Items.Count == 2 && state.Items.Count == 2 && state.Trash.Count == 0,
                    "A native downloads-to-dock drop imports the group once without internal reorder tokens or use cleanup");
                var fresh = Path.Combine(folder, "Newest.psd"); File.WriteAllText(fresh + ".crdownload", "photoshop bytes"); File.Move(fresh + ".crdownload", fresh);
                check(UiProbe.Until(() => stack.DownloadList.Items.Count == 3, 6000), "The folder watcher adds a completed new file without manual refresh");
                var before = stack.DownloadList.Items.Cast<DownloadsWindow.Row>().Single(r => r.File.Path == b);
                File.Delete(fresh);
                check(UiProbe.Until(() => stack.DownloadList.Items.Count == 2, 6000) && ReferenceEquals(before, stack.DownloadList.Items.Cast<DownloadsWindow.Row>().Single(r => r.File.Path == b)),
                    "Watcher removal retains unchanged download row instances");
                for (var i = 0; i < 18; i++) File.WriteAllText(Path.Combine(folder, $"Reference-{i:D2}.txt"), "ready");
                check(UiProbe.Until(() => stack.DownloadList.Items.Count == 20, 6000), "A larger recent-download catalog updates without rebuilding unchanged rows");
                UiProbe.Hover(stack.StackButton); stack.UpdateLayout();
                check(stack.DownloadList.ActualHeight >= 340 && !HasSelector(stack.DownloadList), "The normal Downloads window fits at least ten compact rows without scrolling");
                UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-dense-screen.png"));
                state.Settings.Theme = "Light"; state.Settings.DockPalette = "Light"; session.Change(); UiProbe.Hover(stack.StackButton);
                UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-light-screen.png"));
                for (var i = 0; i < 18; i++) File.Delete(Path.Combine(folder, $"Reference-{i:D2}.txt"));
                check(UiProbe.Until(() => stack.DownloadList.Items.Count == 2, 6000), "Removing recent files returns to the short fitted Downloads surface");
                var area = WindowPlacement.WorkArea(stack); var native = WindowPlacement.PixelBounds(stack);
                UiProbe.Hover(stack.StackButton); UiProbe.MoveTo(new Point(area.Left + 4, area.Bottom - 4));
                check(UiProbe.Until(() => stack.IsCompact, 1000) && !stack.HasDismissObserver && WindowPlacement.PixelBounds(stack) == native,
                    "Downloads collapses promptly and releases its observer even when the main dock collapse setting is off");
                UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-compact-screen.png"));
                WindowPlacement.MovePixels(target, native.Left + 20, native.Top + 20); target.Activate(); UiProbe.Pump();
                var through = receiver.PointToScreen(new Point(40, 45)); var clicksBefore = backgroundClicks;
                UiProbe.ClickAt(through);
                check(backgroundClicks == clicksBefore + 1 && stack.IsCompact, "Clicks through the compact downloads region reach the underlying application");
                UiProbe.Hover(stack.StackButton); check(!stack.IsCompact && stack.HasDismissObserver, "Hover reveals the list and installs only its temporary outside observer");
                var foreground = GetForegroundWindow(); clicksBefore = backgroundClicks; UiProbe.ClickAt(through);
                check(UiProbe.Until(() => stack.IsCompact, 700) && GetForegroundWindow() == foreground && backgroundClicks == clicksBefore + 1,
                    "An outside click in the already-foreground app closes Downloads and passes through without waiting for deactivation");
                for (var i = 0; i < 2; i++)
                {
                    UiProbe.Click(stack.StackButton); UiProbe.MoveTo(new Point(area.Left + 4, area.Bottom - 4));
                    check(UiProbe.Until(() => stack.IsCompact, 1100), "Clicking the Downloads capsule never latches it open after pointer leave");
                }
                UiProbe.Hover(stack.StackButton); UiProbe.Click(UiProbe.Find<Button>(stack, v => v.Content as string == "Close"));
                check(stack.IsCompact && !stack.HasDismissObserver, "The visible Close action dismisses the list immediately without disabling the capsule");
                UiProbe.RightClick(stack.StackButton); UiProbe.MoveTo(new Point(area.Left + 4, area.Bottom - 4)); UiProbe.Pump(450);
                check(stack.ContextMenu.IsOpen && !stack.IsCompact, "An open Downloads menu holds the list rather than closing underneath it");
                UiProbe.ClickAt(new Point(area.Left + 4, area.Bottom - 4));
                check(UiProbe.Until(() => !stack.ContextMenu.IsOpen && stack.IsCompact, 1000), "Outside click closes the Downloads menu and restores compact presence");
                coordinator.HideAll(); check(!stack.IsVisible && !stack.HasDismissObserver, "Global hide includes Downloads and releases its observer");
                coordinator.ShowAll(); check(stack.IsVisible, "Global show restores an enabled downloads stack");
                coordinator.CloseDownloads(); check(coordinator.DownloadsView is null && File.Exists(a) && File.Exists(b),
                    "Closing downloads disposes the view/feed and preserves all source files");
                state.Settings.DownloadsFolder = Path.Combine(root, "unavailable-downloads"); coordinator.OpenDownloads(); stack = coordinator.DownloadsView!;
                check(UiProbe.Until(() => stack.SummaryText.Contains("unavailable"), 6000), "An unavailable custom downloads folder has a visible recoverable state");
                coordinator.CloseDownloads();
            }
            finally { target.Close(); }
        }
        finally { library.Hide(); IconAssets.Initialize(root); oldDock.ShowDock(); oldLibrary.ShowShelf(); }
    }

    public static void Chrome(MainWindow oldLibrary, DockWindow oldDock, string root, string output, Action<bool, string> check)
    {
        oldLibrary.Hide(); oldDock.HideDock();
        var catalog = Path.Combine(root, "chrome-ui-catalog");
        var a = Path.Combine(root, "save-as-project", "Chrome reference — пример.webp"); Directory.CreateDirectory(Path.GetDirectoryName(a)!);
        File.WriteAllBytes(a, Convert.FromBase64String("UklGRjYAAABXRUJQVlA4TCkAAAAvAkAAEBcwMgKBJLb9EYYRCCTRDTa5gKDouuUC3jsoBBAARRRNRP9jAAA="));
        var b = Path.Combine(root, "another-save-location", "Layout.fig"); Directory.CreateDirectory(Path.GetDirectoryName(b)!); File.WriteAllText(b, "figma bytes");
        var store = new StateStore(Path.Combine(root, "chrome-download-ui")); var state = store.Load(); state.Settings.AnimationsEnabled = false;
        using var session = new ShelfSession(store, state); using var feed = new DownloadsFeed(catalog, oldLibrary.Dispatcher, chrome: true);
        var stack = new DownloadsWindow(session, feed, () => { }, () => { }) { Left = 410, Top = 50 };
        var receiver = new Border { Background = Brushes.SteelBlue, AllowDrop = true }; string[]? received = null;
        receiver.DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        receiver.Drop += (_, e) => { received = e.Data.GetData(DataFormats.FileDrop) as string[]; e.Effects = DragDropEffects.Copy; e.Handled = true; };
        var target = new Window { Content = receiver, Width = 250, Height = 160, Left = 30, Top = 80, ShowInTaskbar = false };
        try
        {
            stack.ShowStack(); target.Show();
            check(UiProbe.Until(() => stack.SummaryText.Contains("Connect Chrome"), 5000), "Chrome mode shows connection setup instead of misleading folder-only results");
            UiProbe.Hover(stack.StackButton); UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-connect-screen.png"));
            var now = DateTimeOffset.UtcNow; var message = new BrowserDownloadMessage { Version = 1, Type = "downloads", ProfileId = Guid.NewGuid().ToString("D"), Revision = 1, CapturedUtc = now,
                Files = new[] { new BrowserDownload(a, now.AddSeconds(-1)), new BrowserDownload(b, now) } };
            BrowserDownloads.Save(catalog, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message, BrowserDownloads.Json));
            check(UiProbe.Until(() => stack.DownloadList.Items.Count == 2, 5000), "A Chrome snapshot updates the floating view across different Save As folders without scanning them");
            check(stack.DownloadList.Items.Cast<DownloadsWindow.Row>().First().File.Path == b && state.Items.Count == 0 && state.Trash.Count == 0,
                "Chrome view follows actual completion order while leaving Library and Trash untouched");
            UiProbe.DragTo(Row(stack, a), receiver.PointToScreen(new Point(110, 70)));
            check(received is { Length: 1 } && received[0] == a && File.Exists(a) && stack.DownloadList.SelectedItems.Count == 0,
                "A Chrome download outside Downloads drags directly as a native original FileDrop without selection");
            UiProbe.Hover(stack.StackButton);
            var thumbnail = UiProbe.Find<Image>(stack.DownloadList, i => i.Name == "Thumbnail" && i.DataContext is DownloadsWindow.Row r && r.File.Path == a);
            check(UiProbe.Until(() => thumbnail.Source is not null), "Chrome WebP download rows show the decoded image preview");
            UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-chrome-screen.png"));
            message.Revision++; message.Files = Array.Empty<BrowserDownload>();
            BrowserDownloads.Save(catalog, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message, BrowserDownloads.Json));
            check(UiProbe.Until(() => stack.DownloadList.Items.Count == 0, 5000) && File.ReadAllText(b) == "figma bytes",
                "Pausing Chrome sharing clears its visible catalog without removing downloaded originals");
        }
        finally { stack.DisposeView(); target.Close(); IconAssets.Initialize(root); oldDock.ShowDock(); oldLibrary.ShowShelf(); }
    }
}
