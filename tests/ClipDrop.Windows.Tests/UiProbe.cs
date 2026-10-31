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
    [StructLayout(LayoutKind.Explicit)] private struct Union { [FieldOffset(0)] public Mouse Mouse; [FieldOffset(0)] public KeyInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    public static void Pump(int ms = 80)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
    }
    public static bool Until(Func<bool> condition, int timeoutMs = 3000)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < timeoutMs) Pump(20);
        return condition();
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
    public static void MoveTo(Point point) { SetCursorPos((int)point.X, (int)point.Y); Pump(); }
    public static void ClickAt(Point point) { MoveTo(point); MouseEvent(2); Pump(30); MouseEvent(4); Pump(); }
    public static void DoubleClickAt(Point point)
    { MoveTo(point); MouseEvent(2); MouseEvent(4); Pump(35); MouseEvent(2); MouseEvent(4); Pump(); }
    public static void CaptureWindow(Window window, string path)
    {
        window.Activate(); Pump(120);
        var rect = WindowPlacement.PixelBounds(window);
        using var bitmap = new System.Drawing.Bitmap((int)rect.Width, (int)rect.Height);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen((int)rect.Left, (int)rect.Top, 0, 0, bitmap.Size, System.Drawing.CopyPixelOperation.SourceCopy);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    public static void Escape() { KeyEvent(0x1B, false); Pump(30); KeyEvent(0x1B, true); Pump(); }
    public static void PressKey(ushort key) { KeyEvent(key, false); Pump(20); KeyEvent(key, true); Pump(); }
    public static void RightClickAt(Point point)
    { MoveTo(point); MouseEvent(8); MouseEvent(16); Pump(); }
    public static void RightClick(FrameworkElement target)
    {
        var point = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
        SetCursorPos((int)point.X, (int)point.Y); Pump(30);
        var clicks = new[] { new Input { Type = 0, Data = new Union { Mouse = new Mouse { Flags = 8 } } }, new Input { Type = 0, Data = new Union { Mouse = new Mouse { Flags = 16 } } } };
        if (SendInput(2, clicks, Marshal.SizeOf<Input>()) != 2) throw new Exception("Right mouse input blocked");
        Pump();
    }
    private static void MouseEvent(uint flags)
    {
        if (SendInput(1, new[] { new Input { Data = new Union { Mouse = new Mouse { Flags = flags } } } }, Marshal.SizeOf<Input>()) != 1) throw new Exception("Mouse input blocked");
    }
    private static void KeyEvent(ushort key, bool up)
    {
        if (SendInput(1, new[] { new Input { Type = 1, Data = new Union { Keyboard = new KeyInput { Key = key, Flags = up ? 2u : 0 } } } }, Marshal.SizeOf<Input>()) != 1) throw new Exception("Key input blocked");
    }
    public static void Hover(FrameworkElement target)
    {
        var point = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
        SetCursorPos((int)point.X, (int)point.Y); Pump();
    }
    public static void Click(FrameworkElement target, ModifierKeys modifiers = ModifierKeys.None)
    {
        Hover(target);
        try
        {
            if (modifiers.HasFlag(ModifierKeys.Control)) KeyEvent(0x11, false);
            if (modifiers.HasFlag(ModifierKeys.Shift)) KeyEvent(0x10, false);
            MouseEvent(2); Pump(30); MouseEvent(4); Pump();
        }
        finally
        {
            if (modifiers.HasFlag(ModifierKeys.Shift)) KeyEvent(0x10, true);
            if (modifiers.HasFlag(ModifierKeys.Control)) KeyEvent(0x11, true);
        }
    }
    public static void DragTo(FrameworkElement source, Point destination, bool cancel = false)
    {
        var start = source.PointToScreen(new Point(source.ActualWidth / 2, source.ActualHeight / 2));
        DragFrom(start, destination, cancel);
    }
    public static void DragFrom(Point start, Point destination, bool cancel = false)
    {
        Exception? error = null; var done = false;
        var worker = new Thread(() => {
            try
            {
                SetCursorPos((int)start.X, (int)start.Y); Thread.Sleep(80); MouseEvent(2); Thread.Sleep(80);
                for (var step = 1; step <= 12; step++)
                { SetCursorPos((int)(start.X + (destination.X - start.X) * step / 12), (int)(start.Y + (destination.Y - start.Y) * step / 12)); Thread.Sleep(45); }
                Thread.Sleep(100);
                if (cancel) { KeyEvent(0x1B, false); KeyEvent(0x1B, true); }
            }
            catch (Exception ex) { error = ex; }
            finally { try { MouseEvent(4); } catch (Exception ex) { error ??= ex; } System.Threading.Volatile.Write(ref done, true); }
        }) { IsBackground = true };
        worker.Start(); var watch = Stopwatch.StartNew();
        while (!System.Threading.Volatile.Read(ref done) && watch.ElapsedMilliseconds < 8000) Pump(20);
        if (!done) throw new Exception("Native drag injection timed out");
        Pump(200); if (error is not null) throw error;
    }
    public static void ContextActions(MainWindow library, DockWindow dock, string output, Action<bool, string> check, Action<FrameworkElement, string> render)
    {
        var list = (ListBox)library.FindName("ItemList"); library.Activate(); list.UpdateLayout();
        list.SelectedIndex = 1;
        var target = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
        var row = (ItemRow)target.DataContext;
        RightClick(target);
        var menu = list.ContextMenu ?? throw new Exception("Clip menu missing");
        check(menu.IsOpen && list.SelectedItems.Count == 1 && ((ItemRow)list.SelectedItem).Item.Id == row.Item.Id,
            "Real Library right click selects the clicked clip before opening its actions");
        render(menu, System.IO.Path.Combine(output, "clip-menu.png"));
        var other = list.Items.Cast<ItemRow>().First(r => r.Item.Id != row.Item.Id);
        list.SelectedItem = other;
        ((MenuItem)menu.Items.Cast<object>().OfType<MenuItem>().Single(i => i.Tag as string == "Copy")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Pump();
        check(Clipboard.GetText() == row.Item.Content, "Context Copy stays bound to the clicked clip even if selection changes");
        var pin = Find<Button>(dock.Pins, b => b.DataContext is ItemRow r && r.Item.Id == row.Item.Id);
        RightClick(pin);
        var pinMenu = pin.ContextMenu ?? throw new Exception("Dock clip menu missing");
        check(pinMenu.IsOpen, "Dock pins expose their own context menu");
        pinMenu.Items.OfType<MenuItem>().Single(i => i.Tag as string == "Trash").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump();
        check(library.Session.State.Trash.Single().Item.Id == row.Item.Id && library.Session.State.Items.Any(i => i.Id == other.Item.Id),
            "Dock context removal moves only the targeted clip to persistent Trash");
        library.Session.UndoRemove();
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
        ((MenuItem)library.FindName("AttachOption")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(library.Session.State.Settings.LibraryFollowsDock, "Follow dock menu command reattaches Library");
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
        settings.ShowSection("Dock");
        foreach (var name in new[] { "Spacing", "Opacity" })
        {
            var number = Find<NumberField>(settings, c => c.Label == name);
            number.Input.Text = "0"; number.Commit();
        }
        settings.ShowSection("Style");
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
