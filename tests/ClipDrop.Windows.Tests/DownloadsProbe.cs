using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class DownloadsProbe
{
    public static void Run(MainWindow oldLibrary, DockWindow oldDock, string root, string output, Action<bool, string> check)
    {
        oldLibrary.Hide(); oldDock.HideDock();
        var folder = Path.Combine(root, "browser-downloads"); Directory.CreateDirectory(folder);
        var a = Path.Combine(folder, "Design reference with a long name — пример.png");
        File.WriteAllBytes(a, ImageFiles.Encode(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 120, 70, 220, 255 }, 4)));
        var b = Path.Combine(folder, "Layout.fig"); File.WriteAllText(b, "original Figma file bytes");
        File.WriteAllText(Path.Combine(folder, "unfinished.crdownload"), "partial");
        var store = new StateStore(Path.Combine(root, "downloads-ui")); var state = store.Load();
        state.Settings.AnimationsEnabled = false; state.Settings.DownloadsFolder = folder;
        using var session = new ShelfSession(store, state); var library = new MainWindow(session);
        using var coordinator = new DockCoordinator(session, library);
        try
        {
            check(coordinator.DownloadsView is null, "Disabled downloads creates no stack or folder watcher");
            coordinator.OpenDownloads(); var stack = coordinator.DownloadsView!;
            check(UiProbe.Until(() => stack.DownloadList.Items.Count == 2, 6000), "Downloads opens with ready files and omits the browser partial file");
            var rows = stack.DownloadList.Items.Cast<DownloadsWindow.Row>().ToArray();
            check(state.Items.Count == 0 && state.Trash.Count == 0, "Scanning downloads does not import or archive Library clips");
            using (var writing = new FileStream(b, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                check(!RecentDownloads.Scan(folder, now: DateTimeOffset.UtcNow.AddSeconds(5)).Files.Any(f => f.Path == b),
                    "A file held by an active Windows writer is omitted from ready downloads");
            UiProbe.Hover(stack.StackButton); UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-expanded-screen.png"));
            check(((Border)stack.FindName("Details")).ActualHeight < 220, "A short downloads list uses a compact content-sized surface");
            var receiver = new Border { Background = Brushes.SteelBlue, AllowDrop = true };
            var backgroundClicks = 0; receiver.MouseLeftButtonDown += (_, _) => backgroundClicks++;
            string[]? received = null;
            receiver.DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            receiver.Drop += (_, e) => { received = e.Data.GetData(DataFormats.FileDrop) as string[]; e.Effects = DragDropEffects.Copy; e.Handled = true; };
            var target = new Window { Content = receiver, Width = 250, Height = 160, Left = 30, Top = 80, ShowInTaskbar = false };
            target.Show();
            try
            {
                received = null;
                var direct = UiProbe.Find<ListBoxItem>(stack.DownloadList, c => ReferenceEquals(c.DataContext, rows[0]));
                UiProbe.DragTo(direct, receiver.PointToScreen(new Point(110, 70)));
                check(received is { Length: 1 } && received[0] == rows[0].File.Path && stack.DownloadList.SelectedItems.Count == 0,
                    "An unselected downloads row drags its original file directly without a preliminary click or selection");
                received = null;
                UiProbe.DragTo(direct, receiver.PointToScreen(new Point(110, 70)), cancel: true);
                check(received is null && state.Items.Count == 0 && state.Trash.Count == 0,
                    "Cancelling a direct downloads drag keeps originals and does not create or archive Library clips");
                foreach (var row in rows)
                {
                    var box = UiProbe.Find<ToggleButton>(stack.DownloadList, c => ReferenceEquals(c.DataContext, row)); UiProbe.Click(box);
                    Console.WriteLine($"Downloads selector: checked={box.IsChecked}, selected={stack.DownloadList.SelectedItems.Count}, list-height={stack.DownloadList.ActualHeight}, compact={stack.IsCompact}");
                }
                if (stack.DownloadList.SelectedItems.Count != 2) UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-selection-failure.png"));
                check(stack.DownloadList.SelectedItems.Count == 2, "Downloads checkboxes select a file group without keyboard modifiers");
                var destination = receiver.PointToScreen(new Point(110, 70));
                UiProbe.DragTo(stack.DragButton, destination);
                check(received is { Length: 2 } && received.Contains(a) && received.Contains(b) && state.Trash.Count == 0 && File.ReadAllText(b) == "original Figma file bytes",
                    "Native downloads group drag exposes original FileDrop paths and keeps files and Trash unchanged");
                UiProbe.Click(UiProbe.Find<Button>(stack, v => v.Content as string == "Copy"));
                check(Clipboard.GetFileDropList().Count == 2, "Downloads Copy retains the file clipboard representation");
                UiProbe.Click(UiProbe.Find<Button>(stack, v => v.Content as string == "Save to Library"));
                check(state.Items.Count == 2 && state.Items.All(i => !i.IsPinned) && state.Trash.Count == 0,
                    "Saving downloads to Library is explicit and does not append them to the dock");
                var window = coordinator.Views.First(); window.ShowDock();
                var save = window.QuickSave.PointToScreen(new Point(18, 18)); UiProbe.DragTo(stack.DragButton, save);
                check(window.Pins.Items.Count == 2 && state.Items.Count == 2 && state.Trash.Count == 0,
                    "A native downloads-to-dock drop imports the group once without internal reorder tokens or use cleanup");
                var fresh = Path.Combine(folder, "Newest.psd"); File.WriteAllText(fresh + ".crdownload", "photoshop bytes");
                File.Move(fresh + ".crdownload", fresh);
                check(UiProbe.Until(() => stack.DownloadList.Items.Count == 3, 6000), "The folder watcher adds a completed new file without manual refresh");
                var before = stack.DownloadList.Items.Cast<DownloadsWindow.Row>().Single(r => r.File.Path == b);
                File.Delete(fresh);
                check(UiProbe.Until(() => stack.DownloadList.Items.Count == 2, 6000) && ReferenceEquals(before, stack.DownloadList.Items.Cast<DownloadsWindow.Row>().Single(r => r.File.Path == b)),
                    "Watcher removal retains unchanged download row instances and selection");
                var area = WindowPlacement.WorkArea(stack); var native = WindowPlacement.PixelBounds(stack);
                UiProbe.MoveTo(new Point(area.Left + 4, area.Bottom - 4));
                check(UiProbe.Until(() => stack.IsCompact) && WindowPlacement.PixelBounds(stack) == native,
                    "Downloads collapses to its capsule without moving its native anchor");
                UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-compact-screen.png"));
                WindowPlacement.MovePixels(target, native.Left + 20, native.Top + 20); target.Activate(); UiProbe.Pump();
                var through = receiver.PointToScreen(new Point(40, 45));
                UiProbe.ClickAt(through);
                check(backgroundClicks == 1 && stack.IsCompact, "Clicks through the compact downloads region reach the underlying application");
                UiProbe.Hover(stack.StackButton); check(!stack.IsCompact, "Hover immediately reveals the downloads list");
                UiProbe.ClickAt(through);
                check(backgroundClicks == 2, "Unused space above a short expanded downloads list also passes real clicks through");
                UiProbe.Click(stack.StackButton); UiProbe.MoveTo(new Point(area.Left + 4, area.Bottom - 4)); UiProbe.Pump(900);
                check(!stack.IsCompact, "Clicking the capsule keeps downloads open away from the pointer");
                UiProbe.Click(stack.StackButton); UiProbe.MoveTo(new Point(area.Left + 4, area.Bottom - 4));
                check(UiProbe.Until(() => stack.IsCompact), "A second capsule click resumes automatic hover collapse");
                UiProbe.RightClick(stack.StackButton);
                UiProbe.ClickAt(new Point(area.Left + 4, area.Bottom - 4));
                check(!stack.ContextMenu.IsOpen, "Downloads context menu dismisses on a real outside click");
                coordinator.HideAll(); check(!stack.IsVisible, "Global hide includes the downloads capsule");
                coordinator.ShowAll(); check(stack.IsVisible, "Global show restores an enabled downloads stack");
                state.Settings.Theme = "Light"; state.Settings.DockPalette = "Light"; session.Change();
                UiProbe.Hover(stack.StackButton); UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-light-screen.png"));
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
        var store = new StateStore(Path.Combine(root, "chrome-download-ui")); var state = store.Load();
        state.Settings.AnimationsEnabled = false; state.Settings.CollapseWhenIdle = false;
        using var session = new ShelfSession(store, state); using var feed = new DownloadsFeed(catalog, oldLibrary.Dispatcher, chrome: true);
        var stack = new DownloadsWindow(session, feed, () => { }, () => { }) { Left = 410, Top = 50 };
        var receiver = new Border { Background = Brushes.SteelBlue, AllowDrop = true };
        string[]? received = null;
        receiver.DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        receiver.Drop += (_, e) => { received = e.Data.GetData(DataFormats.FileDrop) as string[]; e.Effects = DragDropEffects.Copy; e.Handled = true; };
        var target = new Window { Content = receiver, Width = 250, Height = 160, Left = 30, Top = 80, ShowInTaskbar = false };
        try
        {
            stack.ShowStack(); target.Show();
            check(UiProbe.Until(() => stack.SummaryText.Contains("Connect Chrome"), 5000), "Chrome mode shows connection setup instead of misleading folder-only results");
            var now = DateTimeOffset.UtcNow; var message = new BrowserDownloadMessage { Version = 1, Type = "downloads", ProfileId = Guid.NewGuid().ToString("D"), Revision = 1, CapturedUtc = now,
                Files = new[] { new BrowserDownload(a, now.AddSeconds(-1)), new BrowserDownload(b, now) } };
            BrowserDownloads.Save(catalog, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message, BrowserDownloads.Json));
            check(UiProbe.Until(() => stack.DownloadList.Items.Count == 2, 5000), "A Chrome snapshot updates the floating view across different Save As folders without scanning them");
            var rows = stack.DownloadList.Items.Cast<DownloadsWindow.Row>().ToArray();
            check(rows[0].File.Path == b && state.Items.Count == 0 && state.Trash.Count == 0,
                "Chrome view follows actual completion order while leaving Library and Trash untouched");
            UiProbe.DragTo(UiProbe.Find<ListBoxItem>(stack.DownloadList, c => c.DataContext is DownloadsWindow.Row r && r.File.Path == a), receiver.PointToScreen(new Point(110, 70)));
            check(received is { Length: 1 } && received[0] == a && File.Exists(a) && stack.DownloadList.SelectedItems.Count == 0,
                "A Chrome download outside Downloads drags directly as a native original FileDrop without selection");
            var thumbnail = UiProbe.Find<Image>(stack.DownloadList, i => i.Name == "Thumbnail" && i.DataContext is DownloadsWindow.Row r && r.File.Path == a);
            check(UiProbe.Until(() => thumbnail.Source is not null), "Chrome WebP download rows show the decoded image preview");
            UiProbe.Hover(stack.StackButton); UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-chrome-screen.png"));
            message.Revision++; message.Files = Array.Empty<BrowserDownload>();
            BrowserDownloads.Save(catalog, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message, BrowserDownloads.Json));
            check(UiProbe.Until(() => stack.DownloadList.Items.Count == 0, 5000) && File.ReadAllText(b) == "figma bytes",
                "Pausing Chrome sharing clears its visible catalog without removing downloaded originals");
        }
        finally { stack.DisposeView(); target.Close(); IconAssets.Initialize(root); oldDock.ShowDock(); oldLibrary.ShowShelf(); }
    }
}
