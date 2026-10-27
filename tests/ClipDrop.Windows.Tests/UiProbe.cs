using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class UiProbe
{
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public Union Data; }
    [StructLayout(LayoutKind.Explicit)] private struct Union { [FieldOffset(0)] public Mouse Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    public static void Pump(int ms = 80)
    {
        var watch = Stopwatch.StartNew();
        do { Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); Thread.Sleep(10); } while (watch.ElapsedMilliseconds < ms);
    }
    public static T Find<T>(DependencyObject node, Func<T, bool> predicate) where T : DependencyObject
    {
        T? Search(DependencyObject current)
        {
            if (current is T found && predicate(found)) return found;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
                if (Search(VisualTreeHelper.GetChild(current, i)) is T child) return child;
            return null;
        }
        return Search(node) ?? throw new Exception("UI control not found: " + typeof(T).Name);
    }
    public static void Drag(Window window, Point point, int dx, int dy)
    {
        var from = window.PointToScreen(point);
        SetCursorPos((int)from.X, (int)from.Y); Pump();
        void Mouse(uint flags)
        {
            if (SendInput(1, new[] { new Input { Type = 0, Data = new Union { Mouse = new Mouse { Flags = flags } } } }, Marshal.SizeOf<Input>()) != 1) throw new Exception("Mouse input blocked");
        }
        Mouse(2); Pump();
        try { SetCursorPos((int)from.X + dx, (int)from.Y + dy); Pump(); }
        finally { Mouse(4); Pump(); }
    }
    public static void Placement(MainWindow library, DockWindow dock, Action<bool, string> check)
    {
        library.AttachDock(dock); library.ShowShelf(); Pump();
        var area = WindowPlacement.WorkArea(dock);
        check(library.Left >= area.Left && library.Top >= area.Top && library.Left + library.ActualWidth <= area.Right + 1 && library.Top + library.ActualHeight <= area.Bottom + 1,
            "Attached Library opens beside the dock within the working area");
        var beforeDock = dock.Left; var beforeLibrary = library.Left;
        Drag(dock, new Point(3, dock.ActualHeight / 2), 35, -25);
        check(Math.Abs(dock.Left - beforeDock) > 20 && Math.Abs(library.Left - beforeLibrary) > 20 && library.Session.State.Settings.LibraryFollowsDock,
            "Dragging dock background moves the attached Library too");
        var before = library.Left;
        Drag(library, new Point(80, library.ActualHeight - 115), 25, -25);
        check(Math.Abs(library.Left - before) > 15 && !library.Session.State.Settings.LibraryFollowsDock,
            "Dragging empty Library background detaches and moves it");
        ((Button)library.FindName("AttachButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(library.Session.State.Settings.LibraryFollowsDock, "Link button reattaches Library");
        check(!WindowDrag.CanStart((TextBox)library.FindName("SearchBox")) && !WindowDrag.CanStart(dock.QuickSave),
            "Background movement excludes search editors and action buttons");
    }
    public static void Library(MainWindow library, Action<bool, string> check)
    {
        var item = library.Session.State.Items.First();
        library.Session.Rename(item, "Reusable note", "Design");
        var groups = (ComboBox)library.FindName("GroupFilter");
        check(item.Title == "Reusable note" && groups.Items.Cast<string>().Contains("Design"), "Renaming and grouping updates Library filters");
        library.Session.Remove(new System.Collections.Generic.List<ClipDrop.Core.ShelfItem> { item });
        ((Button)library.FindName("TrashFilter")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var list = (ListBox)library.FindName("ItemList");
        check(list.Items.Count == 1 && library.Session.State.Trash.Count == 1, "Deleted item remains available in the Trash view");
        list.SelectedIndex = 0; ((Button)library.FindName("RestoreButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(library.Session.State.Trash.Count == 0 && library.Session.State.Items.Any(i => i.Id == item.Id && i.Group == "Design"), "Library Restore returns the original item and group");
        ((Button)library.FindName("AllFilter")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    public static void Styles(SettingsWindow settings, MainWindow library, DockWindow dock, Action<bool, string> check)
    {
        var state = library.Session.State.Settings;
        var picker = Find<ComboBox>(settings, c => AutomationProperties.GetName(c) == "Style preset");
        state.AnimationsEnabled = false;
        picker.SelectedIndex = 3;
        check(state.Theme == "Midnight" && !state.AnimationsEnabled, "Preset picker applies Midnight without enabling motion");
        Find<Slider>(settings, c => AutomationProperties.GetName(c) == "Spacing").Value = 0;
        Find<Slider>(settings, c => AutomationProperties.GetName(c) == "Opacity").Value = 0;
        Find<TextBox>(settings, c => AutomationProperties.GetName(c) == "Style name").Text = "QA zero spacing";
        Find<Button>(settings, c => c.ToolTip as string == "Save current appearance as a named style").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(state.SavedStyles.Count == 1 && state.SavedStyles[0].Style.DockOpacity == 0, "Settings saves current appearance as a named style");
        picker.SelectedIndex = 1; picker.SelectedIndex = picker.Items.Count - 1;
        check(state.DockSpacing == 0 && state.DockOpacity == 0, "Saved style restores zero spacing and background opacity through UI");
        picker.SelectedIndex = 1;
        state.DockPalette = "Light"; state.LibraryPalette = "Midnight"; library.Session.Change();
        check(((SolidColorBrush)dock.FindResource("Surface")).Color != ((SolidColorBrush)library.FindResource("Surface")).Color,
            "Dock and Library resolve independent live palettes");
        check(settings.Background is SolidColorBrush background && background.Color == ((SolidColorBrush)Application.Current.FindResource("Surface")).Color && settings.Foreground is SolidColorBrush foreground && foreground.Color == ((SolidColorBrush)Application.Current.FindResource("Ink")).Color,
            "Settings window background and foreground follow live app theme together");
        var button = dock.QuickSave; Motion.Reveal(button); Motion.Arrive(button);
        check(!button.HasAnimatedProperties && button.Opacity == 1 && button.RenderTransform is ScaleTransform scale && !scale.HasAnimatedProperties,
            "Disabled motion leaves controls visible with no running entrance animations");
    }
}
