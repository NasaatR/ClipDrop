using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ClipDrop.Core;
namespace ClipDrop.Windows.Services;
public static class Appearance
{
    private sealed class Applied { public string Signature = ""; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ResourceDictionary, Applied> AppliedStyles = new();
    public static double Bound(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    public static void Apply(ShelfSettings settings)
    {
        Motion.Enabled = settings.AnimationsEnabled;
        ApplyTo(Application.Current.Resources, settings, settings.Theme, "");
        foreach (var role in Views.AppIcon.Roles)
        {
            settings.PngIcons.TryGetValue(role.Key, out var hash);
            var key = "IconImage." + role.Key; var image = IconAssets.Get(hash);
            if (!ReferenceEquals(Application.Current.Resources[key], image)) Application.Current.Resources[key] = image;
        }
    }
    public static void Apply(ShelfSettings settings, FrameworkElement surface, bool dock = false)
    {
        Apply(settings);
        var palette = dock ? settings.DockPalette : settings.LibraryPalette;
        ApplyTo(surface.Resources, settings, palette == "Shared" ? settings.Theme : palette, dock ? settings.DockColor : settings.LibraryColor);
    }
    public static Color Mix(Color a, Color b, double weight) => Color.FromRgb(
        (byte)Math.Round(a.R * (1 - weight) + b.R * weight), (byte)Math.Round(a.G * (1 - weight) + b.G * weight), (byte)Math.Round(a.B * (1 - weight) + b.B * weight));
    public static double Luminance(Color color)
    {
        static double Channel(byte value) { var v = value / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
        return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
    }
    private static Color Read(string value) => (Color)ColorConverter.ConvertFromString(value);
    private static void ApplyTo(ResourceDictionary resources, ShelfSettings settings, string palette, string custom)
    {
        var signature = string.Join("|", palette, custom, settings.AccentColor, settings.CornerRadius);
        var applied = AppliedStyles.GetOrCreateValue(resources);
        if (applied.Signature == signature) return;
        applied.Signature = signature;
        var bg = Read(palette == "Light" ? "#F5F5F7" : palette == "Midnight" ? "#141C2B" : "#1E1E1E");
        if (AppearanceStyle.TryColor(custom, out var normalized) && normalized.Length > 0) bg = Read(normalized);
        var light = Luminance(bg) > .179;
        var ink = Read(light ? "#000000" : "#FFFFFF");
        var accent = Read(light ? "#25252D" : "#FFFFFF");
        if (AppearanceStyle.TryColor(settings.AccentColor, out normalized) && normalized.Length > 0) accent = Read(normalized);
        // A custom accent always keeps a readable foreground on its own button.
        var onAccent = Read(Luminance(accent) > .179 ? "#101115" : "#FFFFFF");
        double Contrast(Color a, Color b) => (Math.Max(Luminance(a), Luminance(b)) + .05) / (Math.Min(Luminance(a), Luminance(b)) + .05);
        Color Background(Color value)
        {
            var away = Read(light ? "#FFFFFF" : "#000000");
            for (var i = 0; i < 20 && Contrast(value, ink) < 4.5; i++) value = Mix(value, away, .12);
            return value;
        }
        var raised = Background(Mix(bg, ink, .055));
        var muted = Mix(bg, ink, .70);
        for (var i = 0; i < 20 && Contrast(muted, bg) < 4.5; i++) muted = Mix(muted, ink, .12);
        foreach (var pair in new Dictionary<string, Color> {
            ["Surface"] = bg, ["Raised"] = raised, ["Hover"] = Background(Mix(bg, ink, light ? .12 : .13)),
            ["Control"] = Background(Mix(raised, ink, .06)), ["Stroke"] = Mix(bg, ink, .16), ["Ink"] = ink, ["Muted"] = muted,
            ["Selected"] = Background(Mix(raised, accent, .22)), ["Accent"] = accent, ["OnAccent"] = onAccent })
        {
            var brush = new SolidColorBrush(pair.Value); brush.Freeze(); resources[pair.Key] = brush;
        }
        resources["ItemRadius"] = new CornerRadius(Bound(settings.CornerRadius, 0, 16, 8));
    }
}
