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
                settings.Docks.Add(new DockLayout { DisplayId = display.Id, Enabled = true,
                    Visible = settings.Docks.Count == 0 || settings.Docks.Any(l => l.Visible) });
        var visible = settings.Docks.FirstOrDefault(l => Same(l.DisplayId, settings.HomeDisplayId))?.Visible ?? settings.Docks.Any(l => l.Visible);
        var result = displays.Select(d => {
                var layout = settings.Docks.FirstOrDefault(l => Same(l.DisplayId, d.Id));
                return new DockAssignment(d, layout ?? new DockLayout { DisplayId = d.Id, Enabled = true, Visible = visible }, layout is null);
            })
            .Where(a => settings.MirrorDocks ? a.Layout.Enabled || Same(a.Display.Id, settings.HomeDisplayId) : Same(a.Display.Id, settings.HomeDisplayId)).ToArray();
        // A temporary fallback never overwrites the missing monitor's offsets/visibility.
        return result.Length > 0 ? result : new[] { new DockAssignment(primary, new DockLayout { DisplayId = primary.Id, Visible = visible }, true) };
    }
    public static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    public static IReadOnlyList<DockAssignment> Downloads(ShelfSettings settings, IReadOnlyList<DisplayArea> displays)
    {
        if (displays.Count == 0) return Array.Empty<DockAssignment>();
        var home = settings.DownloadsLayout;
        var primary = displays.FirstOrDefault(d => d.Primary) ?? displays[0];
        if (home.DisplayId.Length == 0) home.DisplayId = primary.Id;
        if (!settings.MirrorDownloads)
        {
            var found = displays.FirstOrDefault(d => Same(d.Id, home.DisplayId));
            return found is not null ? new[] { new DockAssignment(found, home) } :
                new[] { new DockAssignment(primary, new DockLayout { DisplayId = primary.Id, Visible = home.Visible }, true) };
        }
        foreach (var display in displays)
            if (settings.DownloadsLayouts.All(l => !Same(l.DisplayId, display.Id)) && settings.DownloadsLayouts.Count < 64)
                settings.DownloadsLayouts.Add(new DockLayout { DisplayId = display.Id, Enabled = true, Visible = home.Visible,
                    OffsetX = Same(display.Id, home.DisplayId) ? home.OffsetX : null,
                    OffsetY = Same(display.Id, home.DisplayId) ? home.OffsetY : null });
        return displays.Select(d => {
            var layout = settings.DownloadsLayouts.FirstOrDefault(l => Same(l.DisplayId, d.Id));
            return new DockAssignment(d, layout ?? new DockLayout { DisplayId = d.Id, Visible = home.Visible }, layout is null);
        }).ToArray();
    }
    // Match the native largest-intersection rule; ties retain the current screen.
    // For a gap/off-screen rectangle, choose the nearest work area rather than
    // requiring the center to lie inside one (vertically staggered monitors).
    public static DisplayArea? DisplayForBounds(IReadOnlyList<DisplayArea> displays, string previous,
        double left, double top, double width, double height)
    {
        double Overlap(DisplayArea d) => Math.Max(0, Math.Min(left + width, d.Left + d.Width) - Math.Max(left, d.Left)) *
            Math.Max(0, Math.Min(top + height, d.Top + d.Height) - Math.Max(top, d.Top));
        double Distance(DisplayArea d)
        {
            var x = Math.Max(0, Math.Max(d.Left - left - width, left - d.Left - d.Width));
            var y = Math.Max(0, Math.Max(d.Top - top - height, top - d.Top - d.Height));
            return x * x + y * y;
        }
        return displays.OrderByDescending(Overlap).ThenBy(Distance).ThenByDescending(d => Same(d.Id, previous)).FirstOrDefault();
    }
    public static PixelPosition Position(DisplayArea area, DockLayout layout, double widthPixels, double heightPixels, double scale)
    {
        scale = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var x = layout.OffsetX is double left && double.IsFinite(left) ? area.Left + left * scale : area.Left + (area.Width - widthPixels) / 2;
        var y = layout.OffsetY is double top && double.IsFinite(top) ? area.Top + top * scale : area.Top + area.Height - heightPixels - 24 * scale;
        return new(Math.Clamp(x, area.Left, area.Left + Math.Max(0, area.Width - widthPixels)),
            Math.Clamp(y, area.Top, area.Top + Math.Max(0, area.Height - heightPixels)));
    }
}
