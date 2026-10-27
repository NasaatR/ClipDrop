using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ClipDrop.Core;
namespace ClipDrop.Windows.Services;
public static class Appearance
{
    public static double Bound(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    public static void Apply(ShelfSettings settings)
    {
        var light = settings.Theme == "Light";
        foreach (var pair in new Dictionary<string, string> {
            ["Surface"] = light ? "#F5F5F7" : "#1E1E1E", ["Raised"] = light ? "#E7E7EA" : "#2B2B2B",
            ["Hover"] = light ? "#D9D9DF" : "#383838", ["Stroke"] = light ? "#85858E" : "#4B4B4B",
            ["Ink"] = light ? "#19191D" : "#F3F3F5", ["Muted"] = light ? "#55555F" : "#B7B7BF",
            ["Selected"] = light ? "#D0D0D8" : "#424242" })
            Application.Current.Resources[pair.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pair.Value));
        Application.Current.Resources["ItemRadius"] = new CornerRadius(Bound(settings.CornerRadius, 0, 16, 8));
    }
}
