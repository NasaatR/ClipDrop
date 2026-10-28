using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace ClipDrop.Windows.Services;

public sealed record PasteTarget(IntPtr Window, uint Process, uint Thread);
public enum PasteOutcome { Requested, NoTarget, TargetChanged, FocusDenied, KeysHeld, InputBlocked }

public interface IPastePlatform
{
    bool Valid(PasteTarget target);
    bool OwnWindow(IntPtr window);
    IntPtr Foreground { get; }
    bool KeysHeld { get; }
    void Focus(PasteTarget target);
    uint SendPaste(PasteTarget target);
    void ReleaseInjectedKeys(uint sent);
}

public sealed class DesktopPaste : IDisposable
{
    private readonly IPastePlatform _platform;
    private readonly NativePastePlatform? _native;
    private readonly Func<PasteTarget?>? _capture;
    public DesktopPaste() { _native = new NativePastePlatform(); _platform = _native; }
    public DesktopPaste(IPastePlatform platform, Func<PasteTarget?>? capture = null) { _platform = platform; _capture = capture; }
    public PasteTarget? CaptureTarget() => _native?.CaptureTarget() ?? _capture?.Invoke();
    public async Task<PasteOutcome> Paste(PasteTarget? target)
    {
        if (target is null || !_platform.Valid(target)) return PasteOutcome.NoTarget;
        // A different external window taking focus cancels this request; no paste into a new app.
        bool Changed() => _platform.Foreground != IntPtr.Zero && _platform.Foreground != target.Window && !_platform.OwnWindow(_platform.Foreground);
        if (Changed()) return PasteOutcome.TargetChanged;
        if (_platform.Foreground != target.Window) _platform.Focus(target);
        for (var attempt = 0; attempt < 12; attempt++)
        {
            if (!_platform.Valid(target)) return PasteOutcome.NoTarget;
            if (Changed()) return PasteOutcome.TargetChanged;
            if (_platform.Foreground == target.Window && !_platform.KeysHeld) break;
            await Task.Delay(20);
        }
        if (_platform.Foreground != target.Window) return PasteOutcome.FocusDenied;
        if (_platform.KeysHeld) return PasteOutcome.KeysHeld;
        if (!_platform.Valid(target)) return PasteOutcome.NoTarget;
        // No queued retry: duplicate paste is a document mutation.
        var sent = _platform.SendPaste(target);
        if (sent != 4) { _platform.ReleaseInjectedKeys(sent); return PasteOutcome.InputBlocked; }
        return PasteOutcome.Requested;
    }
    public void Dispose() => _native?.Dispose();
}

public sealed class NativePastePlatform : IPastePlatform, IDisposable
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint minimum, uint maximum, IntPtr module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    private delegate void WinEvent(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time);
    private readonly WinEvent _callback;
    private readonly IntPtr _hook;
    private readonly uint _ownProcess = (uint)Environment.ProcessId;
    private PasteTarget? _previous;

    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public Keyboard Keyboard; [FieldOffset(0)] public Mouse Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    private static Input Key(ushort code, bool up = false) => new() { Type = 1, Data = new InputUnion { Keyboard = new Keyboard { Key = code, Flags = up ? 2u : 0 } } };

    public NativePastePlatform()
    {
        _callback = (_, _, window, _, _, _, _) => Observe(window);
        // Foreground notifications only, out-of-context, skip this process. No key/clipboard hook.
        _hook = SetWinEventHook(3, 3, IntPtr.Zero, _callback, 0, 0, 2);
        Observe(Foreground);
    }

    private void Observe(IntPtr window)
    {
        if (window == IntPtr.Zero || !IsWindow(window)) return;
        var thread = GetWindowThreadProcessId(window, out var process);
        if (process == 0 || process == _ownProcess || thread == 0) return;
        _previous = new PasteTarget(window, process, thread);
    }
    public PasteTarget? CaptureTarget() { Observe(Foreground); return _previous; }
    public IntPtr Foreground => GetForegroundWindow();
    public bool OwnWindow(IntPtr window) { GetWindowThreadProcessId(window, out var process); return process == _ownProcess; }
    public bool Valid(PasteTarget target) => IsWindow(target.Window) && IsWindowVisible(target.Window) && !IsIconic(target.Window)
        && GetWindowThreadProcessId(target.Window, out var process) == target.Thread && process == target.Process;
    public bool KeysHeld
    {
        get { foreach (var key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, 0x01 }) if ((GetAsyncKeyState(key) & 0x8000) != 0) return true; return false; }
    }
    public void Focus(PasteTarget target) => SetForegroundWindow(target.Window);
    public uint SendPaste(PasteTarget target)
    {
        if (Foreground != target.Window || !Valid(target) || KeysHeld) return 0;
        return SendInput(4, new[] { Key(0x11), Key(0x56), Key(0x56, true), Key(0x11, true) }, Marshal.SizeOf<Input>());
    }
    public void ReleaseInjectedKeys(uint sent)
    {
        if (sent == 0) return;
        // Only undo keys inserted by this request, never release a user's pre-held modifier.
        var keys = sent >= 2 ? new[] { Key(0x56, true), Key(0x11, true) } : new[] { Key(0x11, true) };
        SendInput((uint)keys.Length, keys, Marshal.SizeOf<Input>());
    }

    public static void MakeNonActivating(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = GetWindowLongPtr(hwnd, -20).ToInt64();
        SetWindowLongPtr(hwnd, -20, new IntPtr(style | 0x08000000L | 0x80L));
        HwndSource.FromHwnd(hwnd).AddHook((IntPtr h, int message, IntPtr wp, IntPtr lp, ref bool handled) => {
            if (message == 0x0021) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE, preserve mouse click.
            return IntPtr.Zero;
        });
    }
    public void Dispose() { if (_hook != IntPtr.Zero) UnhookWinEvent(_hook); GC.KeepAlive(_callback); }
}
