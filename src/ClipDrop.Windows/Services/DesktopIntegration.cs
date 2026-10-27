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
    private readonly System.Drawing.Icon _icon;
    private readonly DispatcherTimer _shake;
    private readonly Action _show;
    private int _lastX, _direction, _turns;
    private DateTime _lastTurn, _lastShow;
    public bool HotkeyAvailable { get; }
    public bool LibraryHotkeyAvailable { get; }
    private bool _shakeEnabled;
    public bool ShakeEnabled
    {
        get => _shakeEnabled;
        set { _shakeEnabled = value; if (value) _shake.Start(); else _shake.Stop(); }
    }

    public DesktopIntegration(Window owner, Action toggle, Action show, Action library, Action save, Action settings, Action quit)
    {
        _show = show;
        _source = HwndSource.FromHwnd(new WindowInteropHelper(owner).EnsureHandle());
        _source.AddHook(Hook);
        Toggle = toggle;
        Library = library;
        HotkeyAvailable = RegisterHotKey(_source.Handle, 1, 0x0002 | 0x0001 | 0x4000, 0x20);
        LibraryHotkeyAvailable = RegisterHotKey(_source.Handle, 2, 0x0002 | 0x0001 | 0x4000, 0x4C);
        using (var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ClipDrop.ico")).Stream)
        using (var original = new System.Drawing.Icon(stream)) _icon = (System.Drawing.Icon)original.Clone();
        _tray = new Forms.NotifyIcon { Text = "ClipDrop · Ctrl+Alt+Space", Icon = _icon, Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show dock", null, (_, _) => show());
        menu.Items.Add("Library", null, (_, _) => library());
        menu.Items.Add("Save clipboard", null, (_, _) => save());
        menu.Items.Add("Settings", null, (_, _) => settings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit ClipDrop", null, (_, _) => quit());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => show();
        _shake = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _shake.Tick += (_, _) => Tick();
    }

    private Action Toggle { get; }
    private Action Library { get; }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (message == 0x0312) { if (wparam.ToInt32() == 1) { Toggle(); handled = true; } else if (wparam.ToInt32() == 2) { Library(); handled = true; } }
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
        UnregisterHotKey(_source.Handle, 2);
        _source.RemoveHook(Hook);
        _tray.Visible = false;
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose(); _icon.Dispose();
    }
}
