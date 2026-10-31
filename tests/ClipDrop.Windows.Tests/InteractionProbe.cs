using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class InteractionProbe
{
    public static void Run(MainWindow oldLibrary, DockWindow oldDock, string root, string output, Action<bool, string> check)
    {
        oldLibrary.Hide(); oldDock.HideDock();
        var store = new StateStore(Path.Combine(root, "interaction")); var state = store.Load();
        state.Settings.AnimationsEnabled = false; state.Settings.CollapseWhenIdle = false; state.Settings.MirrorDownloads = false;
        using var session = new ShelfSession(store, state); var library = new MainWindow(session);
        using var coordinator = new DockCoordinator(session, library);
        var dock = coordinator.Views.Single();
        try
        {
            var area = WindowPlacement.WorkArea(dock);
            WindowPlacement.MovePixels(dock, area.Left + 80, area.Top + 100); UiProbe.Pump();
            Clipboard.SetText("Only save on release — not while dragging");
            var before = WindowPlacement.PixelBounds(dock);
            var point = dock.QuickSave.PointToScreen(new Point(dock.QuickSave.ActualWidth / 2, dock.QuickSave.ActualHeight / 2));
            UiProbe.DragTo(dock.QuickSave, point + new Vector(70, 45));
            var moved = WindowPlacement.PixelBounds(dock);
            check(moved.Left > before.Left + 40 && moved.Top > before.Top + 20 && state.Items.Count == 0,
                "Dragging the Save button moves the main panel without saving clipboard contents");
            UiProbe.Click(dock.QuickSave);
            check(state.Items.Count == 1 && state.Items[0].Content.Contains("Only save on release"),
                "A normal Save click still captures the clipboard exactly once after a button drag");
            Clipboard.SetText("A small pointer jitter is still one click");
            point = dock.QuickSave.PointToScreen(new Point(dock.QuickSave.ActualWidth / 2, dock.QuickSave.ActualHeight / 2));
            before = WindowPlacement.PixelBounds(dock);
            UiProbe.DragFrom(point, point + new Vector(1, 1));
            check(state.Items.Count == 2 && WindowPlacement.PixelBounds(dock).Location == before.Location,
                "Sub-threshold movement on Save remains a click rather than a panel move");
            Clipboard.SetText("Do not save the next dragged press");
            point = dock.QuickSave.PointToScreen(new Point(dock.QuickSave.ActualWidth / 2, dock.QuickSave.ActualHeight / 2));
            UiProbe.DragTo(dock.QuickSave, point + new Vector(40, 25));
            check(state.Items.Count == 2, "A repeated Save-button drag cannot accidentally dispatch another capture");
            var folder = Path.Combine(root, "interaction-downloads"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "A long useful downloaded file name — reference.fig"), "original bytes");
            state.Settings.DownloadsSource = "Folder"; state.Settings.DownloadsFolder = folder; coordinator.OpenDownloads();
            var stack = coordinator.DownloadsView!;
            check(UiProbe.Until(() => stack.DownloadList.Items.Count == 1, 5000), "The compact redesigned Downloads view receives the shared file catalog");
            UiProbe.Hover(stack.StackButton);
            var shell = (Border)stack.FindName("CapsuleShell");
            check(stack.Width == 272 && stack.StackButton.ActualWidth == 44 && stack.StackButton.ActualHeight == 44 && shell.Padding == new Thickness(0) && shell.BorderThickness == new Thickness(0) && shell.Background is null,
                "Downloads has one 44-DIP icon surface and a compact 272-DIP list without an outer box");
            var clicks = 0; stack.StackButton.Click += (_, _) => clicks++;
            before = WindowPlacement.PixelBounds(stack);
            point = stack.StackButton.PointToScreen(new Point(22, 22));
            UiProbe.DragTo(stack.StackButton, point + new Vector(-70, -45));
            moved = WindowPlacement.PixelBounds(stack);
            check(moved.Left < before.Left - 40 && moved.Top < before.Top - 20 && clicks == 0,
                "Dragging directly on the Downloads icon moves the window and cancels its click action");
            UiProbe.Click(stack.StackButton);
            check(clicks == 1 && !stack.IsCompact, "The Downloads icon still opens its list with one normal click");
            var settings = new SettingsWindow(state.Settings, () => session.Change(), session.TrySave, session.Report, store.Root, coordinator);
            settings.Show(); UiProbe.Pump();
            try
            {
                var nav = UiProbe.Find<Button>(settings, b => AutomationProperties.GetName(b) == "Settings: Downloads");
                UiProbe.Click(nav); UiProbe.Pump();
                check(settings.SelectedSection == "Downloads" && !settings.Preview.IsVisible,
                    "Settings navigation shows only the selected section instead of a long scrolling page");
                var width = UiProbe.Find<NumberField>(settings, f => f.Label == "Downloads width");
                UiProbe.Click(width.Input); width.Input.Text = "248"; UiProbe.PressKey(0x0D);
                check(state.Settings.DownloadsWidth == 248 && stack.Width == 248,
                    "Typing an exact width and pressing Enter updates the actual Downloads window");
                UiProbe.Click(UiProbe.Find<Button>(width, b => AutomationProperties.GetName(b) == "Downloads width increase"));
                check(state.Settings.DownloadsWidth == 258 && width.Input.Text == "258", "Number controls support a precise plus step without dragging a slider");
                UiProbe.Click(width.Input); width.Input.Text = "invalid"; UiProbe.PressKey(0x0D);
                check(state.Settings.DownloadsWidth == 258 && width.Input.BorderBrush == Brushes.IndianRed,
                    "An invalid numeric draft stays visible without writing corrupt settings");
                UiProbe.PressKey(0x1B);
                check(width.Input.Text == "258", "Escape restores the stored number without closing Settings");
                width.Input.Text = "300"; UiProbe.PressKey(0x26);
                check(state.Settings.DownloadsWidth == 310 && stack.Width == 310, "Keyboard steps continue from a valid typed draft instead of discarding it");
                var rows = UiProbe.Find<NumberField>(settings, f => f.Label == "Visible rows"); rows.Input.Text = "6"; rows.Commit();
                var height = UiProbe.Find<NumberField>(settings, f => f.Label == "Download row height"); height.Input.Text = "28"; height.Commit();
                var icon = UiProbe.Find<NumberField>(settings, f => f.Label == "Downloads icon size"); icon.Input.Text = "36"; icon.Commit(); UiProbe.Pump();
                check(stack.StackButton.ActualWidth == 36 && UiProbe.Find<ListBoxItem>(stack.DownloadList, _ => true).ActualHeight == 28 && stack.Height == 294,
                    "Row density, row count and icon size update live with coherent native geometry");
                UiProbe.CaptureWindow(settings, Path.Combine(output, "settings-downloads-precise.png"));
                settings.Width = 560; settings.Height = 540; state.Settings.Theme = "Light"; session.Change(); UiProbe.Pump();
                UiProbe.CaptureWindow(settings, Path.Combine(output, "settings-downloads-narrow-light.png"));
                check(width.Input.ActualWidth >= 60 && settings.SelectedSection == "Downloads", "Precise settings remain usable in the narrow light-theme window");
                UiProbe.Click(UiProbe.Find<Button>(settings, b => b.Content as string == "Reset Downloads layout")); UiProbe.Pump();
                check(stack.Width == 272 && state.Settings.DownloadsRows == 10 && stack.StackButton.ActualWidth == 44,
                    "Reset Downloads restores compact defaults without changing source or duplication mode");
                settings.ShowSection("Style"); settings.Width = 820; settings.Height = 700; UiProbe.Pump();
                UiProbe.CaptureWindow(settings, Path.Combine(output, "settings-style-motion.png"));
            }
            finally { settings.Close(); }
            ClientAnimationProbe.Run(() => {
            state.Settings.AnimationsEnabled = true; state.Settings.MotionDuration = 300; session.Change();
            if (Environment.GetEnvironmentVariable("CI") == "true") check(Motion.Allowed, "CI exercises enabled disclosure motion rather than skipping positive checks");
            UiProbe.MoveTo(new Point(area.Left + 5, area.Top + 5));
            check(UiProbe.Until(() => stack.IsCompact, 1800), "The icon returns to its compact state before the hover reveal");
            before = WindowPlacement.PixelBounds(stack);
            UiProbe.Hover(stack.StackButton);
            var details = (Border)stack.FindName("Details");
            check(!stack.IsCompact && WindowPlacement.PixelBounds(stack) == before, "Hover reveal leaves native window geometry and its drag anchor stable");
            if (Motion.Allowed)
            {
                check(details.RenderTransform is TransformGroup group && group.Children.OfType<ScaleTransform>().Any(t => t.HasAnimatedProperties),
                    "Hover reveal uses the configured scale-and-rise motion instead of an opacity-only appearance");
                UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-motion-in-progress.png"));
                UiProbe.Pump(380);
                check(details.Opacity == 1 && details.RenderTransform is TransformGroup settled && settled.Children.OfType<ScaleTransform>().All(t => t.ScaleX == 1 && t.ScaleY == 1) && settled.Children.OfType<TranslateTransform>().All(t => t.Y == 0),
                    "The spring reveal settles at exact full size and position within its configured duration");
                state.Settings.MotionStyle = "Soft"; session.Change();
                UiProbe.MoveTo(new Point(area.Left + 5, area.Top + 5)); UiProbe.Until(() => stack.IsCompact, 1800); UiProbe.Hover(stack.StackButton); UiProbe.Pump(350);
                UiProbe.MoveTo(new Point(area.Left + 5, area.Top + 5)); UiProbe.Until(() => stack.IsCompact, 1800); UiProbe.Hover(stack.StackButton);
                check(details.Opacity < .999 && details.RenderTransform is TransformGroup repeated && repeated.Children.OfType<ScaleTransform>().Any(t => t.ScaleX < 1),
                    "Soft motion replays after a completed reveal instead of mistaking stopped clocks for an interrupted animation");
            }
            state.Settings.AnimationsEnabled = false; session.Change(); UiProbe.Pump();
            check(details.Opacity == 1 && details.RenderTransform is TransformGroup stopped && stopped.Children.All(t => !t.HasAnimatedProperties),
                "Turning motion off immediately stops active reveal transforms and leaves content readable");
            UiProbe.CaptureWindow(stack, Path.Combine(output, "downloads-single-icon-narrow.png"));
            }, check);
            session.TrySave(); var reloaded = new StateStore(store.Root).Load();
            check(reloaded.Settings.DownloadsWidth == 272 && reloaded.Settings.DownloadsRowHeight == 34 && reloaded.Settings.MotionDuration == 300,
                "Layout reset and custom motion duration survive storage restart");
        }
        finally { library.Hide(); IconAssets.Initialize(root); oldDock.ShowDock(); oldLibrary.ShowShelf(); }
    }
}
