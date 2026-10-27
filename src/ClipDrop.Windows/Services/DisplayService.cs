using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ClipDrop.Core;
using Forms = System.Windows.Forms;

namespace ClipDrop.Windows.Services;

public interface IDisplaySource
{
    IReadOnlyList<DisplayArea> GetDisplays();
    string? TargetDisplay(IntPtr externalWindow);
}

public sealed class DisplayService : IDisplaySource
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string device, uint number, ref DisplayDevice info, uint flags);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Registry;
    }
    private static (string Id, string Label) Identity(Forms.Screen screen)
    {
        // The interface name identifies a monitor across GDI display-number changes.
        var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
        if (EnumDisplayDevices(screen.DeviceName, 0, ref device, 1) && !string.IsNullOrWhiteSpace(device.Id))
            return (device.Id, device.Description);
        return (screen.DeviceName, "Display"); // Remote/virtual display drivers may omit an interface.
    }
    public IReadOnlyList<DisplayArea> GetDisplays() => Forms.Screen.AllScreens.Select((screen, index) => {
        var (id, label) = Identity(screen); var area = screen.WorkingArea;
        return new DisplayArea(id, $"{index + 1} · {label}" + (screen.Primary ? " · Main" : ""), area.Left, area.Top, area.Width, area.Height, screen.Primary);
    }).GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();
    public string? TargetDisplay(IntPtr externalWindow)
    {
        var screen = externalWindow != IntPtr.Zero ? Forms.Screen.FromHandle(externalWindow) : Forms.Screen.FromPoint(Forms.Cursor.Position);
        return Identity(screen).Id;
    }
}
