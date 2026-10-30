using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;

internal static class MultiMonitorProbe
{
    private sealed class Displays : IDisplaySource
    {
        public IReadOnlyList<DisplayArea> Current = Array.Empty<DisplayArea>();
        public IReadOnlyList<DisplayArea> GetDisplays() => Current;
        public string? TargetDisplay(IntPtr window) => Current.FirstOrDefault()?.Id;
    }
    public static void Run(MainWindow oldLibrary, DockWindow oldDock, string root, string output, Action<bool, string> check)
    {
        oldLibrary.Hide(); oldDock.HideDock();
        var pixels = WindowPlacement.PixelBounds(oldDock); var scale = WindowPlacement.Scale(oldDock);
        var area = WindowPlacement.WorkArea(oldDock);
        // Two non-overlapping logical work areas inside the runner's real display.
        // This reproduces retarget/clamp behavior with real HWNDs and input, not hardware DPI.
        var a = new DisplayArea("move-a", "Left screen", area.Left * scale, area.Top * scale, area.Width * scale / 2, area.Height * scale, true);
        var b = a with { Id = "move-b", Label = "Right screen", Left = a.Left + a.Width, Primary = false };
        var source = new Displays { Current = new[] { a, b } };
        var store = new StateStore(Path.Combine(root, "multi-monitor")); var state = store.Load();
        state.Settings.HomeDisplayId = a.Id; state.Settings.CollapseWhenIdle = false;
        state.Settings.AnimationsEnabled = false; state.Settings.IdleDockOpacity = 100;
        using var session = new ShelfSession(store, state);
        var library = new MainWindow(session);
        session.Add(new[] { ShelfItem.FromText("Shared multi-monitor clip") });
        using var coordinator = new DockCoordinator(session, library, displays: source);
        try
        {
            var dock = coordinator.Views.Single(); UiProbe.Pump();
            var free = UiProbe.Find<Border>(dock, border => border.Name == "Body");
            var start = free.PointToScreen(new Point(5, 5));
            // UiProbe.DragTo uses the element center: this tiny transparent background
            // hit target uses an explicit point without touching Save/the clip.
            UiProbe.DragFrom(start, new Point(b.Left + 90, b.Top + 110));
            check(state.Settings.HomeDisplayId == b.Id && ReferenceEquals(dock, coordinator.Views.Single()) &&
                dock.Assignment!.Display.Id == b.Id && WindowPlacement.PixelBounds(dock).Left >= b.Left,
                "Dragging the single main dock across a display boundary retargets the same HWND instead of snapping back");
        }
        finally { library.Hide(); oldDock.ShowDock(); oldLibrary.ShowShelf(); }
    }
}
