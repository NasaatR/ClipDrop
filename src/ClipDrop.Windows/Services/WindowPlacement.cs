using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
namespace ClipDrop.Windows.Services;
public static class WindowPlacement
{
    public static Rect WorkArea(Window window)
    {
        var area = Forms.Screen.FromHandle(new WindowInteropHelper(window).EnsureHandle()).WorkingArea;
        var transform = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return new Rect(transform.Transform(new Point(area.Left, area.Top)), transform.Transform(new Point(area.Right, area.Bottom)));
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
        var area = WorkArea(window);
        var width = double.IsFinite(window.Width) ? window.Width : window.ActualWidth;
        var height = double.IsFinite(window.Height) ? window.Height : window.ActualHeight;
        var left = double.IsFinite(window.Left) ? window.Left : area.Left + 20;
        var top = double.IsFinite(window.Top) ? window.Top : area.Top + 20;
        window.Left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - width));
        window.Top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - height));
    }
    public static void Near(Window window, Window anchor)
    {
        var area = WorkArea(anchor);
        var width = window.ActualWidth;
        var height = window.ActualHeight;
        var above = anchor.Top - height - 10;
        var below = anchor.Top + anchor.ActualHeight + 10;
        window.Left = Math.Clamp(anchor.Left + (anchor.ActualWidth - width) / 2, area.Left, Math.Max(area.Left, area.Right - width));
        var preferred = above >= area.Top ? above : below + height <= area.Bottom ? below : above;
        window.Top = Math.Clamp(preferred, area.Top, Math.Max(area.Top, area.Bottom - height));
    }
}
