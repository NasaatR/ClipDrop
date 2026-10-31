using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class DownloadUiProbe
{
    public static void Run(MainWindow library, DockWindow original, string root, string output, Action<bool, string> check)
    {
        library.Hide(); original.HideDock();
        var folder = Path.Combine(root, "row-action files — пример"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "A very long design reference — пример.fig"); File.WriteAllText(path, "original file");
        var store = new StateStore(Path.Combine(root, "downloads-preview")); var state = store.Load();
        state.Settings.AnimationsEnabled = false;
        using var session = new ShelfSession(store, state);
        using var feed = new DownloadsFeed(folder, library.Dispatcher);
        ProcessStartInfo? launched = null; var launches = 0;
        var stack = new DownloadsWindow(session, feed, () => { }, () => { }, start => { launched = start; launches++; }) { Left = 40, Top = 40, Width = 272, Height = 514 };
        SettingsWindow? settings = null;
        try
        {
            var start = FileNavigation.ContainingFolder(path);
            check(start.UseShellExecute && start.FileName == folder && start.Verb.Length == 0 && start.ArgumentList.Count == 0,
                "Show in Explorer delegates only the parent folder to the default shell handler instead of launching explorer.exe or the download");
            var explicitExplorer = FileNavigation.ContainingFolder(path, true);
            check(!explicitExplorer.UseShellExecute && Path.GetFileName(explicitExplorer.FileName) == "explorer.exe" && explicitExplorer.ArgumentList.SequenceEqual(new[] { "/select,", path }),
                "Explicit Windows Explorer selection is a separate opt-in route with one intact Unicode path argument");
            var missing = false; try { FileNavigation.ContainingFolder(Path.Combine(folder, "gone.fig")); } catch (FileNotFoundException) { missing = true; }
            check(missing, "Reveal refuses a missing download rather than opening or executing a different target");
            stack.ShowStack(); check(UiProbe.Until(() => stack.DownloadList.Items.Count == 1, 6000), "The row-action fixture receives one real ready download");
            UiProbe.Hover(stack.StackButton); stack.UpdateLayout();
            var row = UiProbe.Find<ListBoxItem>(stack.DownloadList, _ => true);
            var button = UiProbe.Find<Button>(row, b => b.Name == "FolderButton");
            var title = UiProbe.Find<TextBlock>(row, t => t.Text == Path.GetFileName(path)); var titleWidth = title.ActualWidth;
            UiProbe.Hover(UiProbe.Find<TextBlock>(stack, t => t.Text == "Downloads"));
            check(button.Opacity == 0 && !button.IsHitTestVisible && row.Margin.Bottom == 4, "Unhovered downloads keep the folder action hidden and use the new row spacing");
            UiProbe.Hover(row);
            check(button.Opacity == 1 && button.IsHitTestVisible && title.ActualWidth == titleWidth, "Hover reveals the folder button without reflowing the filename");
            UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-row-folder-action.png"));
            var native = WindowPlacement.PixelBounds(stack); UiProbe.Click(button);
            check(launches == 1 && launched is { UseShellExecute: true } && launched.FileName == folder && stack.IsCompact && WindowPlacement.PixelBounds(stack) == native && stack.DownloadList.SelectedItems.Count == 0 && state.Items.Count == 0,
                "The real folder-button click opens the clicked row's parent without selecting, importing, transferring or moving the panel");
            UiProbe.Hover(stack.StackButton); UiProbe.Hover(row); native = WindowPlacement.PixelBounds(stack);
            UiProbe.DragTo(button, button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2)) + new Vector(65, 0));
            check(launches == 1 && WindowPlacement.PixelBounds(stack) == native && File.ReadAllText(path) == "original file",
                "Dragging off a row action neither starts a panel/file drag nor invokes its click");
            UiProbe.Hover(stack.StackButton); UiProbe.RightClick(row);
            UiProbe.Click(stack.DownloadList.ContextMenu!.Items.OfType<MenuItem>().Single(m => m.Header as string == "Show in Explorer"));
            check(launches == 2 && launched is { UseShellExecute: true } && launched.FileName == folder, "Contextual Show in Explorer uses the same default-manager route as the row button");
            state.Settings.DownloadsAlwaysShowFolder = true; session.ChangeSettings(); UiProbe.Hover(stack.StackButton);
            UiProbe.Hover(UiProbe.Find<TextBlock>(stack, t => t.Text == "Downloads"));
            check(button.Opacity == 1 && button.IsHitTestVisible, "The always-visible folder option updates the real row immediately");
            state.Settings.DownloadsAlwaysShowFolder = false; session.ChangeSettings();
            UiProbe.Click(row); stack.Activate(); button.Focus(); UiProbe.Pump();
            check(button.Opacity == 1 && button.IsKeyboardFocusWithin && stack.DownloadList.SelectedItems.Count == 1, "Keyboard focus reveals the folder action while a row is selected");
            UiProbe.PressKey(0x0D);
            check(launches == 3 && launched is { UseShellExecute: true } && launched.FileName == folder && stack.IsCompact,
                "Enter on the focused folder button reveals its folder instead of opening the selected download");
            settings = new SettingsWindow(state.Settings, () => { session.ChangeSettings(); }, session.TrySave, session.Report, store.Root);
            settings.ShowSection("Downloads"); settings.Show(); UiProbe.Pump();
            stack.HideStack();
            var preview = settings.DownloadPreview;
            var previewRow = UiProbe.Find<ListBoxItem>(preview.Files, _ => true);
            check(preview.IsVisible && ReferenceEquals(preview.Files.ItemContainerStyle, stack.DownloadList.ItemContainerStyle) && UiProbe.Find<DownloadRowView>(preview, _ => true).IsVisible,
                "Downloads Settings keeps a persistent sample built from the same row component and style as the floating list");
            var spacing = UiProbe.Find<NumberField>(settings, f => f.Label == "Space between rows"); spacing.Input.Text = "12"; spacing.Commit(); UiProbe.Pump();
            check(previewRow.Margin.Bottom == 12 && preview.Summary.Contains("594") && SurfaceSizing.Downloads(state.Settings).Height == 594,
                "Changing row spacing updates shared preview spacing and full panel geometry together");
            var time = UiProbe.Find<CheckBox>(settings, c => c.Content as string == "Show download time");
            time.BringIntoView(); UiProbe.Pump(); UiProbe.Click(time);
            check(!state.Settings.DownloadsShowTime && UiProbe.Find<TextBlock>(preview, t => t.Name == "Timestamp").Visibility == Visibility.Collapsed,
                "Timestamp visibility updates the preview directly from the saved setting");
            var rootScroll = UiProbe.Find<ScrollViewer>(settings, s => s.Name == "SettingsControls" && s.ScrollableHeight > 0);
            var position = preview.PointToScreen(new Point()); rootScroll.ScrollToEnd(); UiProbe.Pump();
            UiProbe.Hover(UiProbe.Find<Button>(settings, b => b.Content as string == "App")); UiProbe.Pump(450);
            check(preview.IsVisible && preview.PointToScreen(new Point()) == position && !stack.IsVisible,
                "The Downloads preview stays visible at its pinned position while controls scroll and the floating panel is hidden");
            var sample = (DownloadsWindow.Row)preview.Files.Items[0]; UiProbe.Hover(previewRow);
            UiProbe.Click(UiProbe.Find<Button>(previewRow, b => b.Name == "FolderButton"));
            check(launches == 3 && sample.IsPreview && !File.Exists(sample.File.Path) && state.Items.Count == 0,
                "The synthetic preview button gives feedback without reading sample paths or launching applications");
            UiProbe.CaptureWindow(settings, Path.Combine(output, "downloads-settings-preview-dark.png"));
            settings.Width = 560; settings.Height = 600; state.Settings.Theme = "Light"; state.Settings.DockPalette = "Light";
            state.Settings.DownloadsWidth = 480; state.Settings.DownloadsRowHeight = 52; state.Settings.DownloadsIconSize = 64;
            state.Settings.DownloadsAlwaysShowFolder = true; session.ChangeSettings(); preview.Apply(state.Settings); UiProbe.Pump();
            UiProbe.CaptureWindow(settings, Path.Combine(output, "downloads-settings-preview-narrow-light.png"));
            var narrowRow = UiProbe.Find<ListBoxItem>(preview.Files, r => r.IsVisible && r.ActualHeight == 52);
            check(preview.IsVisible && preview.Files.ActualHeight == 64 && UiProbe.Find<ScrollViewer>(preview, s => s.Name == "DownloadsSampleViewport").ScrollableHeight == 0 && UiProbe.Find<Button>(narrowRow, b => b.Name == "FolderButton").ActualHeight == 28,
                "The narrow extreme preview fits a complete row, capsule and usable folder action without clipping");
            settings.Height = 480; UiProbe.Pump();
            check(rootScroll.ViewportHeight >= 100 && preview.IsVisible,
                "At minimum Settings height and maximum row sizes the pinned preview still leaves a usable control viewport");
            UiProbe.CaptureWindow(settings, Path.Combine(output, "downloads-settings-preview-minimum.png"));
            settings.Height = 600; UiProbe.Pump();
            state.Settings.DownloadsRevealMode = "WindowsExplorer"; session.ChangeSettings();
            var reset = UiProbe.Find<Button>(settings, b => b.Content as string == "Reset Downloads layout"); reset.BringIntoView(); UiProbe.Pump(); UiProbe.Click(reset);
            check(state.Settings.DownloadsRowSpacing == 4 && state.Settings.DownloadsShowTime && !state.Settings.DownloadsAlwaysShowFolder && state.Settings.DownloadsRevealMode == "WindowsExplorer",
                "Reset restores row presentation while preserving the explicit file-manager preference");
            settings.ShowSection("Style"); check(!preview.IsVisible, "Downloads preview only occupies Settings space in its own section");
            settings.Close(); settings = null; session.TrySave();
            var loaded = new StateStore(store.Root).Load(); check(loaded.Settings.DownloadsRevealMode == "WindowsExplorer" && loaded.Settings.DownloadsRowSpacing == 4, "Downloads reveal and spacing preferences survive closing Settings and restarting storage");
        }
        finally { settings?.Close(); stack.DisposeView(); IconAssets.Initialize(root); original.ShowDock(); library.ShowShelf(); }
    }
}
