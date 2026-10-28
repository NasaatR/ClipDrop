using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClipDrop.Windows.Services;

// A non-activating dock may leave the same external HWND in foreground. Its next
// click still needs to close the popup. Observe pointer-downs only while a menu is
// open; never consume/replay input, install in another process or record content.
public sealed class PopupDismiss : IDisposable
{
    private static PopupDismiss? _active;
    public static event Action<Window>? Changed;
    public static bool IsOpenFor(Window owner) => _active is { _closed: false } active && ReferenceEquals(active._owner, owner);
    public static bool HasObserver => _active?._mouseHook != IntPtr.Zero && _active is not null;
    private readonly ContextMenu _menu;
    private readonly Window _owner;
    private readonly MouseHook _mouse;
    private readonly WinEvent _foreground;
    private IntPtr _mouseHook, _foregroundHook;
    private bool _closed, _queued;
    private delegate IntPtr MouseHook(int code, IntPtr message, IntPtr data);
    private delegate void WinEvent(IntPtr hook, uint kind, IntPtr hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int kind, MouseHook callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [StructLayout(LayoutKind.Sequential)] private struct MousePacket { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    public static void Attach(ContextMenu menu, Window owner)
    {
        menu.StaysOpen = false;
        menu.Opened += (_, _) => { CloseAll(); _active = new PopupDismiss(menu, owner); Changed?.Invoke(owner); };
        menu.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { menu.IsOpen = false; e.Handled = true; } };
    }
    public static void CloseAll() { if (_active is { } current) { current._menu.IsOpen = false; current.Dispose(); } }
    public static void CloseFor(Window owner) { if (IsOpenFor(owner)) CloseAll(); }
    private IntPtr MenuHandle => (PresentationSource.FromVisual(_menu) as HwndSource)?.Handle ?? IntPtr.Zero;
    private PopupDismiss(ContextMenu menu, Window owner)
    {
        _menu = menu; _owner = owner;
        _mouse = ObserveMouse;
        _foreground = (_, _, hwnd, _, _, _, _) => {
            if (hwnd != IntPtr.Zero && hwnd != MenuHandle && hwnd != new WindowInteropHelper(owner).Handle) QueueClose();
        };
        _mouseHook = SetWindowsHookEx(14, _mouse, GetModuleHandle(null), 0);
        _foregroundHook = SetWinEventHook(3, 3, IntPtr.Zero, _foreground, 0, 0, 0);
        menu.Closed += OnClosed; owner.IsVisibleChanged += OwnerVisibility; owner.LocationChanged += OwnerMoved;
    }
    private IntPtr ObserveMouse(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && !_closed && message.ToInt32() is 0x0201 or 0x0204 or 0x0207 or 0x020B)
        {
            var point = Marshal.PtrToStructure<MousePacket>(data);
            if (!GetWindowRect(MenuHandle, out var bounds) || point.X < bounds.Left || point.X >= bounds.Right || point.Y < bounds.Top || point.Y >= bounds.Bottom) QueueClose();
        }
        return CallNextHookEx(_mouseHook, code, message, data);
    }
    private void QueueClose()
    {
        if (_closed || _queued) return; _queued = true;
        _menu.Dispatcher.BeginInvoke(() => { if (!_closed) _menu.IsOpen = false; }, DispatcherPriority.Input);
    }
    private void OnClosed(object? sender, RoutedEventArgs e) => Dispose();
    private void OwnerVisibility(object sender, DependencyPropertyChangedEventArgs e) { if (!_owner.IsVisible) _menu.IsOpen = false; }
    private void OwnerMoved(object? sender, EventArgs e) => _menu.IsOpen = false;
    public void Dispose()
    {
        if (_closed) return; _closed = true;
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
        _mouseHook = _foregroundHook = IntPtr.Zero;
        _menu.Closed -= OnClosed; _owner.IsVisibleChanged -= OwnerVisibility; _owner.LocationChanged -= OwnerMoved;
        if (ReferenceEquals(_active, this)) _active = null;
        Changed?.Invoke(_owner); GC.KeepAlive(_mouse); GC.KeepAlive(_foreground);
    }
}
