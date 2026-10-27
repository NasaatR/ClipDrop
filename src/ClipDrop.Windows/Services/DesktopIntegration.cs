using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ClipDrop.Windows.Services;

public sealed class DesktopIntegration : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out PointNative point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RectNative rect);
    [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RectNative { public int Left; public int Top; public int Right; public int Bottom; }
    private readonly HwndSource _source;
    private readonly Forms.NotifyIcon _tray;
    private readonly DispatcherTimer _shake;
    private readonly Action _show;
    private int _lastX, _direction, _turns;
    private DateTime _lastTurn, _lastShow;
    public bool HotkeyAvailable { get; }
    public bool ShakeEnabled { get; set; }

    public DesktopIntegration(Window owner, Action toggle, Action show, Action paste, Action quit)
    {
        _show = show;
        _source = HwndSource.FromHwnd(new WindowInteropHelper(owner).EnsureHandle());
        _source.AddHook(Hook);
        Toggle = toggle;
        HotkeyAvailable = RegisterHotKey(_source.Handle, 1, 0x0002 | 0x0001 | 0x4000, 0x20);
        _tray = new Forms.NotifyIcon { Text = "ClipDrop · Ctrl+Alt+Space", Icon = System.Drawing.SystemIcons.Application, Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show shelf", null, (_, _) => show());
        menu.Items.Add("Paste into shelf", null, (_, _) => paste());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit ClipDrop", null, (_, _) => quit());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => show();
        _shake = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _shake.Tick += (_, _) => Tick();
        _shake.Start();
    }

    private Action Toggle { get; }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (message == 0x0312 && wparam.ToInt32() == 1) { Toggle(); handled = true; }
        return IntPtr.Zero;
    }

    private void Tick()
    {
        if (!ShakeEnabled || (GetAsyncKeyState(1) & 0x8000) == 0)
        { _turns = 0; _direction = 0; return; }
        if (!GetCursorPos(out var point)) return;
        var delta = point.X - _lastX;
        _lastX = point.X;
        if (Math.Abs(delta) < 22) return;
        var now = DateTime.UtcNow;
        if ((now - _lastTurn).TotalMilliseconds > 650) _turns = 0;
        if (_direction != 0 && Math.Sign(delta) != _direction) { _turns++; _lastTurn = now; }
        _direction = Math.Sign(delta);
        if (_turns >= 4 && (now - _lastShow).TotalSeconds > 2 && !ForegroundIsFullscreen())
        { _turns = 0; _lastShow = now; _show(); }
    }

    private static bool ForegroundIsFullscreen()
    {
        var hwnd = GetForegroundWindow();
        if (!GetWindowRect(hwnd, out var rect)) return false;
        var screen = Forms.Screen.FromHandle(hwnd).Bounds;
        return rect.Left <= screen.Left && rect.Top <= screen.Top && rect.Right >= screen.Right && rect.Bottom >= screen.Bottom;
    }

    public void Dispose()
    {
        _shake.Stop();
        UnregisterHotKey(_source.Handle, 1);
        _source.RemoveHook(Hook);
        _tray.Visible = false;
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();
    }
}
