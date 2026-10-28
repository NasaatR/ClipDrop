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
        ("Play", "Play", "▶️"), ("Pause", "Pause", "⏸️")
    };
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(AppIcon), new PropertyMetadata("File"));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    private readonly TextBlock _emoji = new() { FontFamily = new FontFamily("Segoe UI Emoji"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
    private readonly Image _image = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    public AppIcon()
    {
        Width = 20; Height = 20; IsHitTestVisible = false;
        Children.Add(_emoji); Children.Add(_image);
        _emoji.FontSize = 16;
        Loaded += (_, _) => UpdateIcon();
        SizeChanged += (_, _) => _emoji.FontSize = Math.Max(8, Math.Min(ActualWidth, ActualHeight) * .80);
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
        _emoji.Text = "📄";
        foreach (var entry in Roles) if (entry.Key == role) { _emoji.Text = entry.Emoji; break; }
        _image.SetResourceReference(Image.SourceProperty, "IconImage." + role);
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed));
        var fallback = new DataTrigger { Binding = new Binding(nameof(Image.Source)) { Source = _image }, Value = null };
        fallback.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible)); style.Triggers.Add(fallback);
        _emoji.Style = style;
    }
}
