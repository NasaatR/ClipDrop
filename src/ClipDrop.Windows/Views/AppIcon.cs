using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ClipDrop.Windows.Views;

// One replaceable icon contract in the dock, Library, menus and live Settings preview.
public sealed class AppIcon : Grid
{
    public static IReadOnlyList<(string Key, string Label, string Emoji)> Roles { get; } = new[] {
        ("Save", "Save clipboard", "📥"), ("Settings", "Settings", "⚙️"), ("Library", "Library", "🗂️"),
        ("Link", "Link / attach", "🔗"), ("Pin", "Pin", "📌"), ("Close", "Close", "❌"),
        ("Plus", "Add files", "➕"), ("Paste", "Paste", "📋"), ("Copy", "Copy", "📑"),
        ("Edit", "Rename", "✏️"), ("Trash", "Trash", "🗑️"), ("Text", "Text clip", "📝"),
        ("Image", "Image clip", "🖼️"), ("File", "File clip", "📄"), ("Folder", "Folder clip", "📁"),
        ("Clipboard", "App clipboard clip", "📋"), ("Video", "Video clip", "🎬"),
        ("Reveal", "Open in app", "↗️"), ("Restore", "Restore", "♻️"), ("Grip", "Move panel", "↔️"),
        ("Hide", "Hide dock", "👁️"), ("Keep", "Keep after use", "📌"), ("Select", "Select clips", "☑️"),
        ("Panels", "Saved panels", "🗃️"),
        ("Downloads", "Downloads stack", "📂"),
        ("Play", "Play", "▶️"), ("Pause", "Pause", "⏸️")
    };
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(AppIcon), new PropertyMetadata("File"));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    private readonly TextBlock _emoji = new() { FontFamily = new FontFamily("Segoe UI Emoji"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
    private readonly Image _image = new() { Stretch = Stretch.Uniform, Margin = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    private string? _role;
    public AppIcon()
    {
        Width = 20; Height = 20; IsHitTestVisible = false;
        // Fit the whole measured line box, including emoji ascenders/descenders.
        // FontSize derived from a tiny square can exceed it and be clipped by WPF.
        _emoji.FontSize = 32;
        Children.Add(new Viewbox { Child = _emoji, Margin = new Thickness(1), Stretch = Stretch.Uniform }); Children.Add(_image);
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed));
        var fallback = new DataTrigger { Binding = new Binding(nameof(Image.Source)) { Source = _image }, Value = null };
        fallback.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible)); style.Triggers.Add(fallback); style.Seal();
        _emoji.Style = style;
        Loaded += (_, _) => UpdateIcon();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == KindProperty && _emoji is not null) UpdateIcon();
    }
    private void UpdateIcon()
    {
        var role = "File";
        foreach (var entry in Roles) if (entry.Key == Kind) { role = entry.Key; break; }
        if (_role == role) return; _role = role;
        _emoji.Text = "📄";
        foreach (var entry in Roles) if (entry.Key == role) { _emoji.Text = entry.Emoji; break; }
        _image.SetResourceReference(Image.SourceProperty, "IconImage." + role);
    }
}
