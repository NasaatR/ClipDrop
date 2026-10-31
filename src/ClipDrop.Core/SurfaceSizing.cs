using System;

namespace ClipDrop.Core;

public readonly record struct DownloadsSize(double Width, double Height, int Rows, double RowHeight, double IconSize)
{
    public double CapsuleHeight => IconSize + 20;
}

public static class SurfaceSizing
{
    private static double Bound(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    public static DownloadsSize Downloads(ShelfSettings settings)
    {
        var rows = (int)Math.Round(Bound(settings.DownloadsRows, 4, 20, 10));
        var row = Bound(settings.DownloadsRowHeight, 28, 52, 34);
        var icon = Bound(settings.DownloadsIconSize, 32, 64, 44);
        return new(Bound(settings.DownloadsWidth, 220, 480, 272), rows * row + icon + 90, rows, row, icon);
    }
}
