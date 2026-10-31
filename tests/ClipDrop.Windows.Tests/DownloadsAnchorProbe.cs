using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class DownloadsAnchorProbe
{
    public static void Run(MainWindow oldLibrary, DockWindow oldDock, string root, string output, Action<bool, string> check)
    {
        oldLibrary.Hide(); oldDock.HideDock();
        var folder = Path.Combine(root, "anchored-downloads"); Directory.CreateDirectory(folder);
        var now = DateTime.UtcNow;
        string FileAt(string name, DateTime timestamp)
        {
            var path = Path.Combine(folder, name); File.WriteAllText(path, "original");
            File.SetCreationTimeUtc(path, timestamp); File.SetLastWriteTimeUtc(path, timestamp); return path;
        }
        var files = Enumerable.Range(0, 24).Select(i => FileAt($"Reference-{i:D2}.txt", now.AddMinutes(-30).AddSeconds(i))).ToArray();
        var store = new StateStore(Path.Combine(root, "anchored-session")); var state = store.Load();
        state.Settings.DownloadsFolder = folder; state.Settings.DownloadsSource = "Folder"; state.Settings.AnimationsEnabled = false;
        using var session = new ShelfSession(store, state); var library = new MainWindow(session);
        using var coordinator = new DockCoordinator(session, library);
        try
        {
            coordinator.OpenDownloads(); var stack = coordinator.DownloadsView!;
            check(UiProbe.Until(() => stack.DownloadList.Items.Count == 24, 6000), "Anchored Downloads receives a catalog large enough to scroll");
            UiProbe.Hover(stack.StackButton); var center = stack.CapsuleCenterPixels();
            state.Settings.DownloadsWidth = 420; state.Settings.DownloadsRows = 14; state.Settings.DownloadsRowHeight = 40; state.Settings.DownloadsRowSpacing = 6; session.ChangeSettings(); UiProbe.Pump();
            check((stack.CapsuleCenterPixels() - center).Length <= 1.5,
                "Changing panel width, row limit, density and spacing does not move the physical Downloads icon");
            state.Settings.MirrorDownloads = false; session.ChangeSettings(); UiProbe.Pump();
            check((stack.CapsuleCenterPixels() - center).Length <= 1.5, "A legacy mirrored home anchor survives resizing followed by switching to single mode");
            state.Settings.MirrorDownloads = true; session.ChangeSettings(); UiProbe.Pump();
            var area = stack.Assignment!.Display;
            var bottom = new Point(area.Left + area.Width - 90, area.Top + area.Height - 60);
            UiProbe.DragTo(stack.StackButton, bottom); UiProbe.Hover(stack.StackButton);
            check(!stack.OpensDown && (stack.CapsuleCenterPixels() - bottom).Length <= 2 && ((DownloadsWindow.Row)stack.DownloadList.Items[^1]).File.Path == files[^1],
                "Dragging the icon near the bottom opens up with the newest file at the bottom next to it");
            var scroll = UiProbe.Find<ScrollViewer>(stack.DownloadList, _ => true); UiProbe.Pump();
            check(scroll.ScrollableHeight - scroll.VerticalOffset <= 2, "Opening an upward list starts at its newest end");
            state.Settings.DownloadsRows = 6; session.ChangeSettings(); UiProbe.Pump();
            check(scroll.ScrollableHeight - scroll.VerticalOffset <= 2, "Shrinking the visible panel preserves a recent-end scroll position");
            scroll.ScrollToTop(); UiProbe.Pump();
            var added = FileAt("New arrival.txt", now.AddSeconds(-5));
            check(UiProbe.Until(() => stack.DownloadList.Items.Count == 25, 6000) && scroll.VerticalOffset <= 2,
                "A new download does not yank the view away while the user browses older files");
            scroll.ScrollToBottom(); UiProbe.Pump(); var latest = FileAt("Newest arrival.txt", now.AddSeconds(-3));
            check(UiProbe.Until(() => stack.DownloadList.Items.Count == 26, 6000) && UiProbe.Until(() => scroll.ScrollableHeight - scroll.VerticalOffset <= 2),
                "A user already at the recent end follows new downloads automatically");
            check(((DownloadsWindow.Row)stack.DownloadList.Items[^1]).File.Path == latest, "The newest arrival appends nearest the bottom icon");
            UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-anchor-bottom.png"));
            UiProbe.Click(UiProbe.Find<Button>(stack, b => b.Content as string == "Close")); stack.ShowStack();
            var thumb = UiProbe.Find<Thumb>(scroll, _ => true); thumb.CaptureMouse(); scroll.ScrollToTop(); UiProbe.Pump();
            check(scroll.VerticalOffset <= 2 && thumb.IsMouseCaptured, "A captured scrollbar owns scrolling even when a recent-end restore was queued just before capture");
            thumb.ReleaseMouseCapture();

            var top = new Point(area.Left + 80, area.Top + 70);
            UiProbe.DragTo(stack.StackButton, top); UiProbe.Hover(stack.StackButton);
            check(stack.OpensDown && (stack.CapsuleCenterPixels() - top).Length <= 2 && ((DownloadsWindow.Row)stack.DownloadList.Items[0]).File.Path == latest,
                "The same icon moves to the top edge and opens downward with the newest file first");
            UiProbe.Pump(); check(scroll.VerticalOffset <= 2, "A direction flip brings the new recent edge into view");
            center = stack.CapsuleCenterPixels(); state.Settings.DownloadsWidth = 300; state.Settings.DownloadsRows = 8; session.ChangeSettings(); UiProbe.Pump();
            check((stack.CapsuleCenterPixels() - center).Length <= 1.5 && stack.Height <= area.Height / WindowPlacement.Scale(stack),
                "Top-left capsule anchoring survives further panel sizing without leaving the work area");
            state.Settings.AnimationsEnabled = true; session.ChangeSettings();
            var row = UiProbe.Find<ListBoxItem>(stack.DownloadList, r => r.DataContext is DownloadsWindow.Row f && f.File.Path == latest);
            UiProbe.Hover(UiProbe.Find<TextBlock>(stack, t => t.Text == "Downloads")); UiProbe.Pump(250); UiProbe.Hover(row);
            var graphic = UiProbe.Find<FrameworkElement>(row, e => e.Name == "PreviewGraphic");
            if (Motion.Allowed)
            {
                check(graphic.RenderTransform is TransformGroup group && group.Children.Any(t => t.HasAnimatedProperties), "Row hover animates the icon's render transform without changing its layout slot");
                UiProbe.Pump(260);
                check(graphic.RenderTransform is TransformGroup settled && settled.Children.OfType<ScaleTransform>().Single().ScaleX == 1.16 && (stack.CapsuleCenterPixels() - center).Length <= 1.5,
                    "Hover settles at a bounded lift/scale while the capsule stays fixed");
            }
            state.Settings.AnimationsEnabled = false; session.ChangeSettings(); UiProbe.Pump();
            check(graphic.RenderTransform is TransformGroup stopped && stopped.Children.All(t => !t.HasAnimatedProperties) && stopped.Children.OfType<ScaleTransform>().Single().ScaleX == 1,
                "Disabling animation immediately removes active row clocks and restores exact icon size");
            UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-anchor-top.png"));
            var screens = new MonitorSettings(state.Settings, coordinator, session.ChangeSettings);
            var screenEditor = new Window { Content = screens, Width = 540, Height = 480, ShowInTaskbar = false };
            try
            {
                screenEditor.Show(); UiProbe.Pump(); UiProbe.Click(UiProbe.Find<Button>(screens, b => b.Content as string == "Reset positions"));
                check((stack.CapsuleCenterPixels() - top).Length > 50 && state.Settings.DownloadsLayout.AnchorY is not null,
                    "Reset positions discards capsule anchors as well as legacy window offsets");
            }
            finally { screenEditor.Close(); }
            session.TrySave(); var loaded = new StateStore(store.Root).Load();
            check(loaded.Settings.DownloadsLayouts.Any(l => l.AnchorX is not null && l.AnchorY is not null) && File.ReadAllText(added) == "original",
                "Capsule anchors persist alongside legacy offsets and downloaded originals remain intact");
        }
        finally { library.Hide(); IconAssets.Initialize(root); oldDock.ShowDock(); oldLibrary.ShowShelf(); }
    }
}
