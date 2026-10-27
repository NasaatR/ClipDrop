using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClipDrop.Core;
using Forms = System.Windows.Forms;
namespace ClipDrop.Windows.Services;
public static class WindowPlacement
{
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    private static IntPtr Handle(Window window) => new WindowInteropHelper(window).EnsureHandle();
    public static double Scale(Window window) { var dpi = GetDpiForWindow(Handle(window)); return dpi == 0 ? 1 : dpi / 96d; }
    public static Rect PixelBounds(Window window)
    {
        if (!GetWindowRect(Handle(window), out var r)) return new Rect(0, 0, Math.Max(1, window.ActualWidth), Math.Max(1, window.ActualHeight));
        return new Rect(r.Left, r.Top, Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top));
    }
    public static void MovePixels(Window window, double x, double y) => SetWindowPos(Handle(window), IntPtr.Zero, (int)Math.Round(x), (int)Math.Round(y), 0, 0, 0x1 | 0x4 | 0x10);
    private static Rect PhysicalArea(Window window)
    {
        var area = Forms.Screen.FromHandle(Handle(window)).WorkingArea;
        return new Rect(area.Left, area.Top, area.Width, area.Height);
    }
    // Kept for legacy one-window sizing/tests. Multi-monitor placement uses physical
    // origins and local DIP offsets, never a globally scaled desktop coordinate.
    public static Rect WorkArea(Window window)
    {
        var area = PhysicalArea(window); var scale = Scale(window);
        return new Rect(area.Left / scale, area.Top / scale, area.Width / scale, area.Height / scale);
    }
    public static void PlaceOnDisplay(Window window, DisplayArea display, DockLayout layout)
    {
        var bounds = PixelBounds(window);
        var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        if (!new Rect(display.Left, display.Top, display.Width, display.Height).Contains(center))
        {
            MovePixels(window, display.Left + 16, display.Top + 16); // WPF processes WM_DPICHANGED synchronously.
            window.UpdateLayout();
        }
        window.UpdateLayout(); bounds = PixelBounds(window);
        var position = DockPlanning.Position(display, layout, bounds.Width, bounds.Height, Scale(window));
        MovePixels(window, position.X, position.Y);
    }
    public static void ClampTo(Window window, DisplayArea display)
    {
        var bounds = PixelBounds(window);
        MovePixels(window, Math.Clamp(bounds.Left, display.Left, display.Left + Math.Max(0, display.Width - bounds.Width)),
            Math.Clamp(bounds.Top, display.Top, display.Top + Math.Max(0, display.Height - bounds.Height)));
    }
    public static void Restore(Window window, double? left, double? top, double width, double height)
    {
        window.Width = Appearance.Bound(width, 320, 900, 376);
        window.Height = Appearance.Bound(height, 300, 1000, 500);
        if (left is double x && top is double y && double.IsFinite(x) && double.IsFinite(y)) { window.Left = x; window.Top = y; }
        var area = WorkArea(window);
        window.Width = Math.Max(window.MinWidth, Math.Min(window.Width, area.Width));
        window.Height = Math.Max(window.MinHeight, Math.Min(window.Height, area.Height));
        Clamp(window);
    }
    public static void Clamp(Window window)
    {
        var area = PhysicalArea(window); var bounds = PixelBounds(window);
        MovePixels(window, Math.Clamp(bounds.Left, area.Left, Math.Max(area.Left, area.Right - bounds.Width)),
            Math.Clamp(bounds.Top, area.Top, Math.Max(area.Top, area.Bottom - bounds.Height)));
    }
    public static void Near(Window window, Window anchor)
    {
        var area = PhysicalArea(anchor); var a = PixelBounds(anchor);
        MovePixels(window, a.Left, a.Top); window.UpdateLayout();
        var bounds = PixelBounds(window); var gap = 10 * Scale(anchor);
        var above = a.Top - bounds.Height - gap; var below = a.Bottom + gap;
        var preferred = above >= area.Top ? above : below + bounds.Height <= area.Bottom ? below : above;
        MovePixels(window, Math.Clamp(a.Left + (a.Width - bounds.Width) / 2, area.Left, Math.Max(area.Left, area.Right - bounds.Width)),
            Math.Clamp(preferred, area.Top, Math.Max(area.Top, area.Bottom - bounds.Height)));
    }
}
