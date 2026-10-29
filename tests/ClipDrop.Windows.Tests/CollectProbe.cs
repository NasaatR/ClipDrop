using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class CollectProbe
{
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T value) yield return value;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    public static void Run(MainWindow previousLibrary, DockWindow previousDock, string root, string output, Action<bool, string> check, Action<FrameworkElement, string> render)
    {
        previousLibrary.Hide(); previousDock.HideDock();
        var store = new StateStore(Path.Combine(root, "collect")); var state = store.Load();
        state.Settings.AnimationsEnabled = false; state.Settings.LibraryFollowsDock = false;
        state.Settings.DockLeft = 40; state.Settings.DockTop = 35; state.Settings.Left = 40; state.Settings.Top = 150;
        using var session = new ShelfSession(store, state);
        var held = ShelfItem.FromText("Temporary dock reference"); session.Add(new[] { held });
        var library = new MainWindow(session); var dock = new DockWindow(session, library.ShowShelf, library.OpenSettings); dock.ShowDock();
        var list = (ListBox)library.FindName("ItemList");
        var files = new[] { Path.Combine(store.Root, "first.txt"), Path.Combine(store.Root, "second.txt") };
        File.WriteAllText(files[0], "first original"); File.WriteAllText(files[1], "second original");
        var data = new DataObject(); var paths = new StringCollection(); paths.AddRange(files); data.SetFileDropList(paths);
        var source = new Button { Content = "Native multi-file source / target", Padding = new Thickness(12) };
        var receiver = new Window { Content = source, Width = 310, Height = 180, AllowDrop = true, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        var area = WindowPlacement.WorkArea(dock); receiver.Left = area.Right - 330; receiver.Top = area.Top + 180;
        Point? origin = null; var received = Array.Empty<string>(); var drops = 0;
        source.PreviewMouseLeftButtonDown += (_, e) => origin = e.GetPosition(source);
        source.PreviewMouseMove += (_, e) => {
            if (origin is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(source);
            if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            origin = null; source.ReleaseMouseCapture(); DragDrop.DoDragDrop(source, data, DragDropEffects.Copy);
        };
        receiver.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        receiver.Drop += (_, e) => { received = (string[])e.Data.GetData(DataFormats.FileDrop); drops++; e.Effects = DragDropEffects.Copy; e.Handled = true; };
        try
        {
            UiProbe.Pump();
            check(Descendants<Button>(dock).Where(b => b.IsVisible && b.DataContext is not ItemRow).SequenceEqual(new[] { dock.QuickSave }) &&
                Descendants<AppIcon>(dock).Where(i => i.DataContext is not ItemRow).All(i => i.Kind == "Save"),
                "The resting dock exposes only Save and clip content, without a move grip or permanent utility buttons");
            render((FrameworkElement)dock.Content, Path.Combine(output, "dock-save-only.png"));
            UiProbe.RightClick(dock.QuickSave);
            check(dock.ContextMenu.IsOpen && dock.ContextMenu.Items.OfType<MenuItem>().Any(i => i.Tag as string == "Settings"), "Right-click Save opens Library, panels and settings without extra toolbar icons");
            render(dock.ContextMenu, Path.Combine(output, "dock-actions.png"));
            UiProbe.Click(dock.ContextMenu.Items.OfType<MenuItem>().Single(i => i.Tag as string == "Library"));
            check(library.IsVisible, "Library opens from the actual Save context menu");
            library.Hide();
            UiProbe.DoubleClickAt(dock.PointToScreen(new Point(3, dock.ActualHeight / 2)));
            check(library.IsVisible, "Double-clicking free dock space opens Library without invoking Save or paste");
            Clipboard.SetText("Direct library clipboard capture");
            UiProbe.Click(UiProbe.Find<Button>(library, b => AutomationProperties.GetName(b) == "Save clipboard"));
            check(state.Items.Single(i => i.Content == "Direct library clipboard capture").IsPinned == false && dock.Pins.Items.Count == 1,
                "Library clipboard capture preserves text while leaving the dock membership unchanged");
            receiver.Show(); receiver.Activate(); UiProbe.Pump();
            var inside = list.PointToScreen(new Point(list.ActualWidth / 2, list.ActualHeight / 2));
            UiProbe.DragTo(source, inside);
            var clips = files.Select(path => state.Items.Single(i => i.FilePath == path)).ToArray();
            check(clips.All(i => !i.IsPinned) && dock.Pins.Items.Count == 1 && state.Items.Count == 4,
                "A native multi-file drop saves directly to Library without filling the dock");
            session.TogglePin(new[] { clips[0] }); session.Capture(data, false);
            check(clips[0].IsPinned && state.Items.Count == 4, "Re-saving into Library preserves existing dock membership and deduplicates files");
            session.TogglePin(new[] { clips[0] }); clips[1].KeepAfterUse = true; session.Change();
            library.Activate(); UiProbe.Pump(); list.SelectedItems.Clear();
            ToggleButton Selector(ShelfItem clip)
            {
                var row = list.Items.Cast<ItemRow>().Single(r => r.Item.Id == clip.Id); list.ScrollIntoView(row); UiProbe.Pump();
                return UiProbe.Find<ToggleButton>((ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(row), _ => true);
            }
            UiProbe.Click(Selector(clips[0])); UiProbe.Click(Selector(clips[1]));
            check(list.SelectedItems.Count == 2 && state.Trash.Count == 0, "Two real checkbox clicks select a group without Ctrl, paste or deletion");
            UiProbe.Click(Selector(clips[0]));
            check(list.SelectedItems.Count == 1 && ((ItemRow)list.SelectedItem).Item.Id == clips[1].Id, "Checkbox toggling removes one member without clearing the remaining selection");
            UiProbe.Click(Selector(clips[0]));
            var drag = (Button)library.FindName("DragSelectedButton");
            check(drag.IsVisible && drag.Content as string == "Drag 2", "Selection shows one counted group-drag action");
            render((FrameworkElement)library.Content, Path.Combine(output, "library-collect-selected.png"));
            library.Width = 320; library.Height = 360; state.Settings.Theme = "Light"; session.Change(); UiProbe.Pump();
            render((FrameworkElement)library.Content, Path.Combine(output, "library-collect-narrow-light.png"));
            state.Settings.Theme = "Dark"; session.Change();
            var target = source.PointToScreen(new Point(source.ActualWidth / 2, source.ActualHeight / 2));
            UiProbe.DragTo(drag, target, true);
            check(drops == 0 && clips.All(state.Items.Contains), "Cancelling a real Library group drag retains every selected clip");
            UiProbe.DragTo(drag, target);
            check(drops == 1 && received.ToHashSet().SetEquals(files) && state.Trash.Single().Item.Id == clips[0].Id && state.Items.Contains(clips[1]),
                "A native group drop receives both original files and archives only the unprotected clip");
            check(File.ReadAllText(files[0]) == "first original" && File.ReadAllText(files[1]) == "second original", "Batch transfer leaves both original file contents unchanged");
            var before = state.Trash.Count; library.ShowShelf(); UiProbe.Pump();
            var pin = UiProbe.Find<Button>(dock.Pins, b => b.DataContext is ItemRow row && row.Item.Id == held.Id && b.Tag as string != "Keep");
            inside = list.PointToScreen(new Point(list.ActualWidth / 2, list.ActualHeight / 2)); UiProbe.DragTo(pin, inside);
            check(state.Items.Contains(held) && held.IsPinned && state.Trash.Count == before && state.Items.Count(i => i.Id == held.Id) == 1,
                "An internal dock-to-Library drop acknowledges the existing clip without archiving or reimporting it");
        }
        finally
        {
            PopupDismiss.CloseAll(); dock.DisposeView(); library.Hide(); receiver.Close();
            IconAssets.Initialize(root); previousDock.ShowDock(); previousLibrary.ShowShelf();
        }
    }
}
