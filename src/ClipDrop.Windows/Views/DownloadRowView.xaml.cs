using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

public partial class DownloadRowView : UserControl
{
    public static readonly RoutedEvent ShowFolderRequestedEvent = EventManager.RegisterRoutedEvent(nameof(ShowFolderRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(DownloadRowView));
    public event RoutedEventHandler ShowFolderRequested { add => AddHandler(ShowFolderRequestedEvent, value); remove => RemoveHandler(ShowFolderRequestedEvent, value); }
    private int _thumbnailRevision;
    private string? _thumbnailPath;
    private ListBoxItem? _hoverParent;
    private ItemsControl? _hoverOwner;
    private static readonly ImageSource SampleImage = MakeSample();
    public DownloadRowView()
    {
        InitializeComponent(); Loaded += (_, _) => {
            DependencyObject? parent = this;
            while (parent is not null && parent is not ListBoxItem) parent = VisualTreeHelper.GetParent(parent);
            _hoverParent = parent as ListBoxItem;
            if (_hoverParent is not null) { _hoverParent.MouseEnter += HoverChanged; _hoverParent.MouseLeave += HoverChanged; _hoverParent.IsKeyboardFocusWithinChanged += FocusChanged; }
            _hoverOwner = _hoverParent is null ? null : ItemsControl.ItemsControlFromItemContainer(_hoverParent);
            if (_hoverOwner is not null) { _hoverOwner.MouseEnter += HoverChanged; _hoverOwner.MouseLeave += HoverChanged; }
            LoadThumbnail(); UpdateHover();
        };
        DataContextChanged += (_, _) => { if (IsLoaded) { LoadThumbnail(); UpdateHover(); } else Motion.ResetHover(PreviewGraphic); };
        Unloaded += (_, _) => {
            _thumbnailRevision++; _thumbnailPath = null; Thumbnail.Source = null; Motion.ResetHover(PreviewGraphic);
            if (_hoverOwner is not null) { _hoverOwner.MouseEnter -= HoverChanged; _hoverOwner.MouseLeave -= HoverChanged; _hoverOwner = null; }
            if (_hoverParent is not null) { _hoverParent.MouseEnter -= HoverChanged; _hoverParent.MouseLeave -= HoverChanged; _hoverParent.IsKeyboardFocusWithinChanged -= FocusChanged; _hoverParent = null; }
        };
    }
    private void HoverChanged(object sender, System.Windows.Input.MouseEventArgs e) => UpdateHover();
    private void FocusChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateHover();
    private void UpdateHover()
    {
        var active = _hoverParent is { } row && (row.IsMouseOver ||
            row.IsKeyboardFocusWithin && _hoverOwner?.IsMouseOver != true);
        Motion.RowHover(PreviewGraphic, HoverWash, active);
    }
    private void OnFolder(object sender, RoutedEventArgs e)
    { e.Handled = true; RaiseEvent(new RoutedEventArgs(ShowFolderRequestedEvent, this)); }
    private async void LoadThumbnail()
    {
        var revision = ++_thumbnailRevision;
        if (DataContext is not DownloadsWindow.Row row || row.RowItem.ImageVisible != Visibility.Visible)
        { _thumbnailPath = null; Thumbnail.Source = null; return; }
        // Revalidating the same path must not flash the fallback icon first.
        if (!string.Equals(_thumbnailPath, row.File.Path, StringComparison.OrdinalIgnoreCase)) Thumbnail.Source = null;
        _thumbnailPath = row.File.Path;
        if (row.IsPreview) { Thumbnail.Source = SampleImage; return; }
        var bitmap = await ThumbnailCache.Shared.Get(row.File.Path);
        if (IsLoaded && revision == _thumbnailRevision && ReferenceEquals(DataContext, row)) Thumbnail.Source = bitmap;
    }
    private static ImageSource MakeSample()
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(64, 85, 130)), null, new RectangleGeometry(new Rect(0, 0, 32, 32), 4, 4)));
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(169, 198, 216)), null, Geometry.Parse("M0,28 L12,10 L22,23 L27,16 L32,25 L32,32 L0,32Z")));
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(248, 210, 128)), null, new EllipseGeometry(new Point(24, 8), 3, 3)));
        var image = new DrawingImage(drawing); image.Freeze(); return image;
    }
}
