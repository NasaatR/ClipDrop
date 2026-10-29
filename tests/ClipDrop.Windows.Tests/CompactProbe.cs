using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class CompactProbe
{
    public static void Run(MainWindow oldLibrary, DockWindow oldDock, string root, string output, Action<bool, string> check)
    {
        oldLibrary.Hide(); oldDock.HideDock();
        var store = new StateStore(Path.Combine(root, "compact")); var state = store.Load();
        state.Settings.AnimationsEnabled = false; state.Settings.CollapseWhenIdle = true;
        state.Settings.DockLeft = 60; state.Settings.DockTop = 80;
        using var session = new ShelfSession(store, state);
        session.Add(Enumerable.Range(0, 5).Select(i => ShelfItem.FromText("Compact clip " + i)));
        var hit = 0; var behind = new Button { Content = "Underlying app", Background = Brushes.SteelBlue };
        behind.Click += (_, _) => hit++;
        var backdrop = new Window { Content = behind, Width = 750, Height = 300, Left = 30, Top = 35, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        backdrop.Show();
        var dock = new DockWindow(session, () => { }, () => { }); dock.ShowDock();
        var strip = (DockStrip)dock.FindName("Strip");
        var source = new Button { Content = "Drop source" };
        var area = WindowPlacement.WorkArea(dock);
        var sourceWindow = new Window { Content = source, Width = 220, Height = 120, Left = area.Right - 250, Top = area.Top + 220, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        Point? origin = null;
        source.PreviewMouseLeftButtonDown += (_, e) => origin = e.GetPosition(source);
        source.PreviewMouseMove += (_, e) => {
            if (origin is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(source);
            if (Math.Abs(p.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            origin = null; source.ReleaseMouseCapture();
            var data = new DataObject(); data.SetText("Dropped on compact capsule");
            DragDrop.DoDragDrop(source, data, DragDropEffects.Copy);
        };
        try
        {
            UiProbe.Hover(dock.QuickSave); UiProbe.Pump(); var bounds = WindowPlacement.PixelBounds(dock);
            var savePoint = dock.QuickSave.PointToScreen(new Point(dock.QuickSave.ActualWidth / 2, dock.QuickSave.ActualHeight / 2));
            UiProbe.MoveTo(new Point(area.Right - 5, area.Bottom - 5));
            check(UiProbe.Until(() => strip.IsCompact), "Away from the pointer the dock becomes a compact Save capsule");
            check(UiProbe.Until(() => strip.FindName("Surface") is Border { ActualWidth: < 90 }) && WindowPlacement.PixelBounds(dock) == bounds,
                "Compact mode reduces the painted surface without resizing or moving the native window");
            UiProbe.CaptureWindow(dock, Path.Combine(output, "dock-compact-screen.png"));
            var blank = dock.PointToScreen(new Point(dock.ActualWidth - 25, dock.ActualHeight / 2));
            UiProbe.ClickAt(blank);
            check(hit == 1 && strip.IsCompact, "A real click through the collapsed content area reaches the underlying app without expanding the dock");
            UiProbe.MoveTo(savePoint);
            check(!strip.IsCompact && dock.QuickSave.PointToScreen(new Point(dock.QuickSave.ActualWidth / 2, dock.QuickSave.ActualHeight / 2)) == savePoint,
                "Hover expands immediately while keeping Save under the pointer");
            UiProbe.CaptureWindow(dock, Path.Combine(output, "dock-expanded-screen.png"));
            UiProbe.RightClick(dock.QuickSave);
            UiProbe.MoveTo(new Point(area.Right - 5, area.Bottom - 5)); UiProbe.Pump(800);
            check(!strip.IsCompact && dock.ContextMenu.IsOpen, "An open menu holds the dock expanded while the pointer is away");
            UiProbe.ClickAt(new Point(area.Right - 5, area.Bottom - 5));
            check(UiProbe.Until(() => strip.IsCompact), "Outside menu dismissal resumes delayed compact mode");
            sourceWindow.Show(); sourceWindow.Activate(); UiProbe.Pump();
            UiProbe.DragTo(source, savePoint);
            check(state.Items.Any(i => i.Content == "Dropped on compact capsule") && state.Items.Count == 6,
                "A native supported drop on the compact capsule expands and saves the payload");
            state.Settings.CollapseWhenIdle = false; session.Change();
            UiProbe.MoveTo(new Point(area.Right - 5, area.Bottom - 5)); UiProbe.Pump(850);
            check(!strip.IsCompact, "Disabling collapse keeps the whole dock available away from the pointer");
            state.Settings.CollapseWhenIdle = true; session.Change();
            check(UiProbe.Until(() => strip.IsCompact), "Re-enabling collapse applies live without reopening the dock");
            var settings = new SettingsWindow(state.Settings, () => session.Change(), session.TrySave, session.Report, store.Root);
            settings.Show();
            UiProbe.Find<CheckBox>(settings, b => b.Content as string == "Preview compact state").IsChecked = true; UiProbe.Pump();
            check(settings.Preview.IsCompact, "Settings renders the actual compact dock in its live preview");
            settings.Close();
        }
        finally
        {
            PopupDismiss.CloseAll(); dock.DisposeView(); backdrop.Close(); sourceWindow.Close();
            IconAssets.Initialize(root); oldDock.ShowDock(); oldLibrary.ShowShelf();
        }
    }
}
