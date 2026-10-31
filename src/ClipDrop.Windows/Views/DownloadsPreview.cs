using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

// Persistent, synthetic 1:1 sample. Never watches folders, launches files or
// participates in clipboard/OLE; actual row visuals are shared with Downloads.
public sealed class DownloadsPreview : Border
{
    private readonly Border _surface = new() { CornerRadius = new CornerRadius(12), Padding = new Thickness(8), BorderThickness = new Thickness(1) };
    private readonly ListBox _files = new() { IsTabStop = false, SelectionMode = SelectionMode.Extended, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly Border _icon = new() { Name = "PreviewDownloadsIcon", CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
    private readonly AppIcon _glyph = new() { Kind = "Downloads" };
    private readonly TextBlock _summary = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 5) };
    private readonly TextBlock _feedback = new() { Text = "Sample files · folder button previews the action", FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly ScrollViewer _viewport = new() { Name = "DownloadsSampleViewport", HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 260 };
    private DownloadsSize _size;
    public ListBox Files => _files;
    public string Summary => _summary.Text;
    public DownloadsPreview()
    {
        Padding = new Thickness(10, 6, 10, 8); Margin = new Thickness(18, 0, 18, 8); CornerRadius = new CornerRadius(9);
        SetResourceReference(BackgroundProperty, "Raised");
        _surface.SetResourceReference(BackgroundProperty, "Surface"); _surface.SetResourceReference(BorderBrushProperty, "Stroke");
        _icon.SetResourceReference(BackgroundProperty, "Surface"); _icon.SetResourceReference(BorderBrushProperty, "Stroke"); _icon.Child = _glyph;
        _summary.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _feedback.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var root = new StackPanel(); Child = root;
        root.Children.Add(_summary);
        // Only constrain the preview viewport, not the 1:1 row width. At a small
        // window or extreme width the preview can scroll instead of clipping.
        var sample = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
        var body = new StackPanel(); body.Children.Add(new TextBlock { Text = "Downloads", FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(3, 0, 0, 5) });
        _files.SetResourceReference(ItemsControl.ItemContainerStyleProperty, "DownloadRowStyle");
        var template = new DataTemplate(); template.VisualTree = new FrameworkElementFactory(typeof(DownloadRowView)); _files.ItemTemplate = template;
        var names = new[] { "Moodboard — reference.webp", "Project layout.fig", "Product presentation.pdf", "Motion study.mp4", "Source images.zip", "Notes.txt" };
        _files.ItemsSource = names.Select((name, i) => new DownloadsWindow.Row(new DownloadFile(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ClipDrop-preview", name), name, 184320 + i * 50000, DateTimeOffset.Now.AddMinutes(-i * 6)), preview: true)).ToArray();
        _files.AddHandler(DownloadRowView.ShowFolderRequestedEvent, new RoutedEventHandler((_, e) => { e.Handled = true; _feedback.Text = "Preview only · a real row opens its containing folder"; }));
        body.Children.Add(_files); _surface.Child = body; sample.Children.Add(_surface); sample.Children.Add(_icon);
        _viewport.Content = sample; root.Children.Add(_viewport); root.Children.Add(_feedback);
    }
    public void Fit(double settingsHeight)
    {
        _viewport.MaxHeight = Math.Clamp(settingsHeight - 400, 80, 260); FitSample();
    }
    private void FitSample()
    {
        if (_size.RowStride <= 0) return;
        // Keep the icon visible beside the sample journey whenever one row fits;
        // only the smallest extreme needs vertical preview scrolling.
        var rows = Math.Clamp((int)Math.Floor((_viewport.MaxHeight - _size.IconSize - 45) / _size.RowStride), 1, 3);
        _files.Height = rows * _size.RowStride;
    }
    public void Apply(ShelfSettings settings)
    {
        Appearance.Apply(settings, this, true);
        var size = _size = DownloadsPresentation.Apply(this, settings);
        _surface.Width = size.Width - 20; // Floating Details has 10-DIP side margins.
        FitSample();
        _icon.Width = _icon.Height = size.IconSize; _glyph.Width = _glyph.Height = Math.Round(size.IconSize * .52);
        _summary.Text = $"Live Downloads preview · 1:1 sample · up to {size.Rows} rows · {size.Width:0} × {size.Height:0} px";
    }
}
