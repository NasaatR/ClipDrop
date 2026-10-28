using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;
using Forms = System.Windows.Forms;

internal static class PresenceProbe
{
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
    private static void ScreenShot(Window window, string path)
    {
        var rect = WindowPlacement.PixelBounds(window);
        using var bitmap = new System.Drawing.Bitmap((int)rect.Width, (int)rect.Height);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen((int)rect.Left, (int)rect.Top, 0, 0, bitmap.Size, System.Drawing.CopyPixelOperation.SourceCopy);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    private static void TrayMessage(DesktopIntegration integration, int mouseMessage)
    {
        // Exercise NotifyIcon's real HWND callback route, without assuming an
        // Explorer taskbar layout/overflow location on the hosted runner.
        var tray = (Forms.NotifyIcon)typeof(DesktopIntegration).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(integration)!;
        var native = (Forms.NativeWindow)typeof(Forms.NotifyIcon).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!;
        SendMessage(native.Handle, 0x0800, IntPtr.Zero, new IntPtr(mouseMessage)); UiProbe.Pump();
    }
    public static void Run(MainWindow library, DockWindow original, string root, string output, Action<bool, string> check, Action<FrameworkElement, string> render)
    {
        original.HideDock(); library.Hide();
        var store = new StateStore(Path.Combine(root, "presence")); var state = store.Load(); state.Settings.AnimationsEnabled = false;
        state.Settings.DockLeft = 40; state.Settings.DockTop = 50;
        using var session = new ShelfSession(store, state);
        var clip = ShelfItem.FromText("Menu target"); session.Add(new[] { clip });
        var backdrop = new Window { Width = 800, Height = 350, Left = 10, Top = 10, Background = new SolidColorBrush(Color.FromRgb(75, 110, 160)), WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false };
        backdrop.Show();
        var dock = new DockWindow(session, () => { }, () => { }); dock.ShowDock();
        var strip = (DockStrip)dock.FindName("Strip");
        try
        {
            var bounds = WindowPlacement.WorkArea(dock);
            UiProbe.MoveTo(new Point(bounds.Right - 5, bounds.Bottom - 5)); UiProbe.Pump(1150);
            check(Math.Abs(dock.Opacity - .2) < .001, "Dock becomes twenty-percent visible when pointer and interactions leave");
            ScreenShot(dock, Path.Combine(output, "dock-idle-screen.png"));
            UiProbe.Hover(dock.QuickSave); UiProbe.Pump(120);
            check(dock.Opacity == 1, "A real pointer hover restores the entire dock to full visibility");
            ScreenShot(dock, Path.Combine(output, "dock-hover-screen.png"));
            var button = UiProbe.Find<Button>(dock.Pins, b => b.Tag as string != "Keep" && b.DataContext is ItemRow);
            UiProbe.RightClick(button); var menu = button.ContextMenu!;
            check(menu.IsOpen && menu.ActualHeight < 140 && menu.Items.OfType<MenuItem>().Count() == 4, "The opened clip menu is compact and omits disabled secondary actions");
            render(menu, Path.Combine(output, "clip-menu-compact.png"));
            var more = menu.Items.OfType<MenuItem>().Single(i => i.Tag as string == "More");
            UiProbe.Click(more); check(menu.IsOpen && menu.Items.OfType<MenuItem>().Any(i => i.Tag as string == "Preview"), "More expands secondary clip actions without closing the menu");
            UiProbe.MoveTo(new Point(bounds.Right - 5, bounds.Bottom - 5)); UiProbe.Pump(350);
            check(dock.Opacity == 1, "An open menu holds the dock visible after pointer leave");
            UiProbe.ClickAt(new Point(bounds.Right - 5, bounds.Bottom - 5)); UiProbe.Pump(300);
            check(!menu.IsOpen && !PopupDismiss.HasObserver && Math.Abs(dock.Opacity - .2) < .001, "Outside click dismisses the expanded menu and resumes idle fading");
            state.Settings.IdleDockOpacity = 35; session.Change();
            check(Math.Abs(dock.Opacity - .35) < .001 && !dock.HasAnimatedProperties, "Idle setting updates live without animations when motion is disabled");
            var settings = new SettingsWindow(state.Settings, () => session.Change(), session.TrySave, session.Report, store.Root);
            settings.Show();
            var idle = UiProbe.Find<CheckBox>(settings, c => c.Content as string == "Preview idle visibility"); idle.IsChecked = true;
            check(Math.Abs(settings.Preview.Opacity - .35) < .001, "Settings previews the actual dock at its configured idle visibility");
            settings.Close();
            session.Remove(new() { clip });
            UiProbe.RightClickAt(dock.PointToScreen(new Point(30, dock.ActualHeight / 2)));
            var background = dock.ContextMenu;
            check(background.IsOpen && background.Items.OfType<MenuItem>().Count() == 3, "Dock background offers a compact hide/undo/library menu");
            UiProbe.Click(background.Items.OfType<MenuItem>().Single(i => i.Tag as string == "Undo"));
            check(state.Items.Contains(clip) && state.Trash.Count == 0, "Undo from the dock restores the last deletion without opening Library");
            UiProbe.Click((Button)strip.FindName("HideButton")); check(!dock.IsVisible, "Small dock hide control hides the panel");
            using (var integration = new DesktopIntegration(dock.Toggle, dock.ShowDock, () => { }, () => { }, () => { }, () => { }, dock.ShowDock, dock.HideDock))
            {
                TrayMessage(integration, 0x0201); TrayMessage(integration, 0x0202);
                check(dock.IsShown, "A single left tray callback restores a hidden dock");
                TrayMessage(integration, 0x0201); TrayMessage(integration, 0x0202);
                check(!dock.IsVisible, "The next single tray callback hides the dock consistently");
                TrayMessage(integration, 0x0201); TrayMessage(integration, 0x0202); TrayMessage(integration, 0x0203); TrayMessage(integration, 0x0202);
                check(dock.IsShown, "A native double-click sequence does not toggle the dock twice");
            }
            dock.WindowState = WindowState.Minimized; dock.Toggle(); UiProbe.Pump();
            check(dock.IsShown && dock.WindowState == WindowState.Normal, "Showing a minimized dock restores it directly without an extra hide cycle");
            UiProbe.RightClick(UiProbe.Find<Button>(dock.Pins, b => b.Tag as string != "Keep" && b.DataContext is ItemRow));
            dock.HideDock(); check(!PopupDismiss.HasObserver, "Hiding a dock dismisses popups and releases temporary observers");
        }
        finally { PopupDismiss.CloseAll(); dock.DisposeView(); backdrop.Close(); IconAssets.Initialize(root); original.ShowDock(); library.ShowShelf(); }
    }
}
