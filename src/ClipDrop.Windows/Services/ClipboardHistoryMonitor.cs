using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClipDrop.Windows.Services;

// A single event-driven listener per session. No polling, COM worker or startup
// capture; pause/dispose cancels queued capture and removes the native listener.
public sealed class ClipboardHistoryMonitor : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr window);
    private readonly HwndSource _window;
    private readonly DispatcherTimer _delay = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly Func<bool> _capture;
    private int _attempts;
    private bool _disposed;
    public ClipboardHistoryMonitor(Func<bool> capture)
    {
        _capture = capture;
        _window = new HwndSource(new HwndSourceParameters("ClipDrop history listener") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 });
        _window.AddHook(Message);
        if (!AddClipboardFormatListener(_window.Handle))
        { var error = Marshal.GetLastWin32Error(); _window.Dispose(); throw new Win32Exception(error); }
        _delay.Tick += Capture;
    }
    private IntPtr Message(IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        if (!_disposed && message == 0x031D) { _attempts = 0; _delay.Stop(); _delay.Start(); }
        return IntPtr.Zero;
    }
    private void Capture(object? sender, EventArgs e)
    {
        _delay.Stop();
        if (_disposed) return;
        if (!_capture() && ++_attempts < 3) _delay.Start();
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _delay.Stop(); _delay.Tick -= Capture;
        RemoveClipboardFormatListener(_window.Handle); _window.RemoveHook(Message); _window.Dispose();
    }
}
