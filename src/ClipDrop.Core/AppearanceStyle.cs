using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipDrop.Core;

// Visual preferences only: applying a style never changes behavior, data or window positions.
public class AppearanceStyle
{
    public Dictionary<string, string> PngIcons { get; set; } = new();
    public string Theme { get; set; } = "Dark";
    public string DockPalette { get; set; } = "Shared";
    public string LibraryPalette { get; set; } = "Shared";
    public string DockColor { get; set; } = "";
    public string LibraryColor { get; set; } = "";
    public string AccentColor { get; set; } = "";
    public double FontSize { get; set; } = 13;
    public double RowSpacing { get; set; } = 4;
    public double CornerRadius { get; set; } = 8;
    public double DockButtonSize { get; set; } = 36;
    public double DockSpacing { get; set; } = 4;
    public double DockRadius { get; set; } = 11;
    public double DockOpacity { get; set; } = 100;
    public double IdleDockOpacity { get; set; } = 20;

    public AppearanceStyle Snapshot()
    {
        var copy = new AppearanceStyle(); CopyTo(copy); return copy;
    }
    public void CopyTo(AppearanceStyle target)
    {
        target.PngIcons = new Dictionary<string, string>(PngIcons);
        target.Theme = Theme; target.DockPalette = DockPalette; target.LibraryPalette = LibraryPalette;
        target.DockColor = DockColor; target.LibraryColor = LibraryColor; target.AccentColor = AccentColor;
        target.FontSize = FontSize; target.RowSpacing = RowSpacing; target.CornerRadius = CornerRadius;
        target.DockButtonSize = DockButtonSize; target.DockSpacing = DockSpacing;
        target.DockRadius = DockRadius; target.DockOpacity = DockOpacity; target.IdleDockOpacity = IdleDockOpacity;
    }
    public bool EquivalentTo(AppearanceStyle other) =>
        PngIcons.Count == other.PngIcons.Count && PngIcons.All(p => other.PngIcons.TryGetValue(p.Key, out var hash) && hash == p.Value) &&
        (Theme, DockPalette, LibraryPalette, DockColor, LibraryColor, AccentColor, FontSize, RowSpacing, CornerRadius, DockButtonSize, DockSpacing, DockRadius, DockOpacity, IdleDockOpacity) ==
        (other.Theme, other.DockPalette, other.LibraryPalette, other.DockColor, other.LibraryColor, other.AccentColor, other.FontSize, other.RowSpacing, other.CornerRadius, other.DockButtonSize, other.DockSpacing, other.DockRadius, other.DockOpacity, other.IdleDockOpacity);
    public static bool TryColor(string? input, out string color)
    {
        color = (input ?? "").Trim().ToUpperInvariant();
        if (color.Length == 0) return true;
        if (color.Length != 7 || color[0] != '#') return false;
        foreach (var c in color.AsSpan(1)) if (!Uri.IsHexDigit(c)) return false;
        return true;
    }
    public static IReadOnlyList<(string Name, AppearanceStyle Style)> BuiltIns { get; } = new[] {
        ("Graphite", new AppearanceStyle()),
        ("Paper", new AppearanceStyle { Theme = "Light" }),
        ("Midnight", new AppearanceStyle { Theme = "Midnight", DockRadius = 14, DockSpacing = 5 }),
        ("Compact", new AppearanceStyle { DockButtonSize = 30, DockSpacing = 0, DockRadius = 7, RowSpacing = 0, CornerRadius = 4, FontSize = 12 })
    };
}

public sealed class SavedStyle
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public AppearanceStyle Style { get; set; } = new();
}
