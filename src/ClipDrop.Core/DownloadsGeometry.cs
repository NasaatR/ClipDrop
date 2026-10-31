using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipDrop.Core;

public readonly record struct DownloadsPlacement(double Left, double Top, double Width, double Height, double AnchorX, double AnchorY, bool OpensDown, bool IconOnLeft);

public static class DownloadsGeometry
{
    public static PixelPosition LegacyAnchor(ShelfSettings settings, DockLayout layout, double width, double height)
    {
        var size = SurfaceSizing.Downloads(settings);
        var w = Math.Min(size.Width, width); var h = Math.Min(size.Height, height);
        var x = layout.OffsetX ?? Math.Max(0, width - w - 16);
        var y = layout.OffsetY ?? Math.Max(0, height - h - 16);
        return new(Math.Clamp(x, 0, Math.Max(0, width - w)) + w - 10 - size.IconSize / 2,
            Math.Clamp(y, 0, Math.Max(0, height - h)) + h - 10 - size.IconSize / 2);
    }
    public static DownloadsPlacement Place(ShelfSettings settings, double width, double height, PixelPosition anchor)
    {
        width = Math.Max(1, width); height = Math.Max(1, height);
        var size = SurfaceSizing.Downloads(settings); var edge = size.IconSize / 2 + 10;
        var x = Math.Clamp(double.IsFinite(anchor.X) ? anchor.X : width - edge - 16, Math.Min(edge, width / 2), Math.Max(width / 2, width - edge));
        var y = Math.Clamp(double.IsFinite(anchor.Y) ? anchor.Y : height - edge - 16, Math.Min(edge, height / 2), Math.Max(height / 2, height - edge));
        var down = settings.DownloadsDirection == "Below" || settings.DownloadsDirection != "Above" && y < height / 2;
        var room = down ? height - y + edge : y + edge;
        var other = down ? y + edge : height - y + edge;
        if (room < Math.Min(size.Height, size.IconSize + 120) && other > room) down = !down;
        var left = x < width / 2;
        var w = Math.Max(1, Math.Min(Math.Min(size.Width, width), left ? width - x + edge : x + edge));
        var h = Math.Max(1, Math.Min(Math.Min(size.Height, height), down ? height - y + edge : y + edge));
        return new(Math.Clamp(left ? x - edge : x + edge - w, 0, width - w), Math.Clamp(down ? y - edge : y + edge - h, 0, height - h), w, h, x, y, down, left);
    }
    // The shared catalog keeps completion order; only the visual projection flips.
    public static DownloadFile[] Order(IEnumerable<DownloadFile> files, bool opensDown) =>
        (opensDown ? files.OrderByDescending(f => f.AddedUtc).ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase) :
        files.OrderBy(f => f.AddedUtc).ThenByDescending(f => f.Path, StringComparer.OrdinalIgnoreCase)).ToArray();
}
