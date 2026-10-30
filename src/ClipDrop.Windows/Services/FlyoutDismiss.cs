using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClipDrop.Windows.Services;

// Installed only while this flyout is expanded. A nonactivating window cannot
// rely on Deactivated: the same external app may keep foreground throughout.
// Observe pointer-down/foreground changes, never consume input or record content.
public sealed class FlyoutDismiss : IDisposable
{
    private readonly Window _owner;
    private readonly Func<Point, bool> _inside;
    private readonly Func<bool> _held;
    private readonly Action _dismiss;
    private readonly MouseHook _mouse;
    private readonly WinEvent _foreground;
    private IntPtr _mouseHook, _foregroundHook;
    private bool _disposed, _queued, _requireOutside;
    private delegate IntPtr MouseHook(int code, IntPtr message, IntPtr data);
    private delegate void WinEvent(IntPtr hook, uint kind, IntPtr hwnd, int objectId, int childId, uint thread, uint time);
    [StructLayout(LayoutKind.Sequential)] private struct MousePacket { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SetWindowsHookEx(int kind, MouseHook callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    public bool IsObserving => !_disposed && _mouseHook != IntPtr.Zero;
    public FlyoutDismiss(Window owner, Func<Point, bool> inside, Func<bool> held, Action dismiss)
    {
        _owner = owner; _inside = inside; _held = held; _dismiss = dismiss;
        _mouse = (code, message, data) => {
            if (code >= 0 && !_disposed && message.ToInt32() is 0x0201 or 0x0204 or 0x0207 or 0x020B)
            {
                var point = Marshal.PtrToStructure<MousePacket>(data);
                if (!_held() && !_inside(new Point(point.X, point.Y))) Queue(requireOutside: false);
            }
            return CallNextHookEx(_mouseHook, code, message, data);
        };
        _foreground = (_, _, hwnd, _, _, _, _) => {
            // OUTOFCONTEXT foreground notifications are asynchronous. Do not
            // apply a stale activation change after the pointer has entered us.
            if (!_disposed && !_held() && hwnd != IntPtr.Zero && hwnd == GetForegroundWindow() &&
                hwnd != new WindowInteropHelper(owner).Handle && PointerOutside()) Queue(requireOutside: true);
        };
        _mouseHook = SetWindowsHookEx(14, _mouse, GetModuleHandle(null), 0);
        _foregroundHook = SetWinEventHook(3, 3, IntPtr.Zero, _foreground, 0, 0, 0);
    }
    private bool PointerOutside() => GetCursorPos(out var point) && !_inside(new Point(point.X, point.Y));
    private void Queue(bool requireOutside)
    {
        if (_disposed || _owner.Dispatcher.HasShutdownStarted) return;
        if (_queued) { _requireOutside &= requireOutside; return; }
        _queued = true; _requireOutside = requireOutside;
        _owner.Dispatcher.BeginInvoke(() => {
            var relevant = !_requireOutside || PointerOutside();
            _queued = false;
            if (!_disposed && !_held() && relevant) _dismiss();
        }, DispatcherPriority.Input);
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
        _mouseHook = _foregroundHook = IntPtr.Zero; GC.KeepAlive(_mouse); GC.KeepAlive(_foreground);
    }
}
