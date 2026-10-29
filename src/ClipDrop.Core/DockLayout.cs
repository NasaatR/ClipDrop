using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipDrop.Core;

public sealed class DockLayout
{
    public string DisplayId { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Visible { get; set; } = true;
    public double? OffsetX { get; set; }
    public double? OffsetY { get; set; }
}

public sealed record DisplayArea(string Id, string Label, double Left, double Top, double Width, double Height, bool Primary = false)
{ public override string ToString() => Label; }
public sealed record DockAssignment(DisplayArea Display, DockLayout Layout, bool Temporary = false);
public readonly record struct PixelPosition(double X, double Y);

public static class DockPlanning
{
    public static DockAssignment? PanelWindow(PanelSet panel, IReadOnlyList<DisplayArea> displays)
    {
        if (panel.ClosedUtc is not null || panel.Window is not { IsOpen: true } window || displays.Count == 0) return null;
        var display = displays.FirstOrDefault(d => Same(d.Id, window.Layout.DisplayId));
        if (display is not null) return new DockAssignment(display, window.Layout);
        var primary = displays.FirstOrDefault(d => d.Primary) ?? displays[0];
        return new DockAssignment(primary, new DockLayout { DisplayId = primary.Id, Enabled = true, Visible = window.Layout.Visible }, true);
    }
    public static IReadOnlyList<DockAssignment> Plan(ShelfSettings settings, IReadOnlyList<DisplayArea> displays)
    {
        if (displays.Count == 0) return Array.Empty<DockAssignment>();
        var primary = displays.FirstOrDefault(d => d.Primary) ?? displays[0];
        if (settings.HomeDisplayId.Length == 0) settings.HomeDisplayId = primary.Id;
        foreach (var display in displays)
            if (settings.Docks.All(d => !Same(d.DisplayId, display.Id)) && settings.Docks.Count < 64)
                settings.Docks.Add(new DockLayout { DisplayId = display.Id, Enabled = Same(display.Id, settings.HomeDisplayId) });
        var result = displays.Select(d => new DockAssignment(d, settings.Docks.FirstOrDefault(l => Same(l.DisplayId, d.Id)) ?? new DockLayout { DisplayId = d.Id }))
            .Where(a => settings.MirrorDocks ? a.Layout.Enabled || Same(a.Display.Id, settings.HomeDisplayId) : Same(a.Display.Id, settings.HomeDisplayId)).ToArray();
        // A temporary fallback never overwrites the missing monitor's offsets/visibility.
        return result.Length > 0 ? result : new[] { new DockAssignment(primary, new DockLayout { DisplayId = primary.Id }, true) };
    }
    public static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    public static PixelPosition Position(DisplayArea area, DockLayout layout, double widthPixels, double heightPixels, double scale)
    {
        scale = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var x = layout.OffsetX is double left && double.IsFinite(left) ? area.Left + left * scale : area.Left + (area.Width - widthPixels) / 2;
        var y = layout.OffsetY is double top && double.IsFinite(top) ? area.Top + top * scale : area.Top + area.Height - heightPixels - 24 * scale;
        return new(Math.Clamp(x, area.Left, area.Left + Math.Max(0, area.Width - widthPixels)),
            Math.Clamp(y, area.Top, area.Top + Math.Max(0, area.Height - heightPixels)));
    }
}
