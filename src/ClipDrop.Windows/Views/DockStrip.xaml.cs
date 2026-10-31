using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClipDrop.Core;
using ClipDrop.Windows.Services;
namespace ClipDrop.Windows.Views;

// The exact same dock surface renders in the floating window and Settings preview.
public partial class DockStrip : UserControl
{
    public event RoutedEventHandler? SaveRequested, LibraryRequested, PinRequested, PinLoaded, ImageLoaded, KeepRequested, SelectionRequested, DoneRequested;
    public event MouseButtonEventHandler? PinDown, PinUp, PinMenu;
    public event MouseEventHandler? PinMove;
    private readonly ObservableCollection<ItemRow> _rows = new();
    private double _slot = 40;
    private ItemRow? _cue;
    private long _lastScroll;
    private string _surfaceSignature = "";
    private int _surfaceRevision;
    public bool IsCompact { get; private set; }
    public DockStrip()
    {
        InitializeComponent(); PinnedItems.ItemsSource = _rows;
        Body.SizeChanged += (_, _) => SurfaceSize(false);
    }
    private double CompactWidth => (double)Resources["DockSize"] + ((Thickness)Resources["DockGap"]).Left * 2 + Body.Padding.Left + Body.Padding.Right + 2;
    private void SurfaceSize(bool animate)
    {
        if (!Resources.Contains("DockSize")) return;
        var width = IsCompact ? CompactWidth : Body.ActualWidth;
        if (width <= 0) return;
        var previous = double.IsNaN(Surface.Width) ? Surface.ActualWidth : Surface.Width;
        var revision = ++_surfaceRevision; Surface.Height = Body.ActualHeight;
        if (animate && Motion.Allowed && Math.Abs(previous - width) > 1)
        {
            var resize = new DoubleAnimation(previous, width, TimeSpan.FromMilliseconds(Motion.DurationMs)) {
                FillBehavior = FillBehavior.HoldEnd, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            resize.Completed += (_, _) => { if (revision == _surfaceRevision) { Surface.Width = width; Surface.BeginAnimation(WidthProperty, null); } };
            Surface.BeginAnimation(WidthProperty, resize, HandoffBehavior.SnapshotAndReplace);
        }
        else { Surface.Width = width; Surface.BeginAnimation(WidthProperty, null); }
    }
    public void SetCompact(bool compact)
    {
        if (IsCompact == compact) return;
        IsCompact = compact;
        Stage.Background = compact ? null : new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        Details.IsHitTestVisible = !compact;
        if (compact)
            Motion.Conceal(Details, () => { if (IsCompact) { Details.Visibility = Visibility.Hidden; SurfaceSize(true); } });
        else
        {
            SurfaceSize(true); Motion.Disclose(Details, new Point(0, .5));
            Details.Visibility = Visibility.Visible;
        }
    }

    public void Apply(ShelfSettings settings)
    {
        Appearance.Apply(settings, this, true);
        if (!Motion.Allowed && Surface.HasAnimatedProperties) SurfaceSize(false);
        var signature = string.Join("|", settings.DockButtonSize, settings.DockSpacing, settings.DockRadius, settings.DockOpacity,
            ((SolidColorBrush)FindResource("Surface")).Color, ((SolidColorBrush)FindResource("Ink")).Color);
        if (_surfaceSignature == signature) return;
        _surfaceSignature = signature;
        Resources["DockSize"] = Appearance.Bound(settings.DockButtonSize, 24, 72, 36);
        _slot = (double)Resources["DockSize"] + Appearance.Bound(settings.DockSpacing, 0, 24, 4);
        Resources["DockSlot"] = _slot;
        Resources["DockGap"] = new Thickness(Appearance.Bound(settings.DockSpacing, 0, 24, 4) / 2);
        Resources["DockRadius"] = new CornerRadius(Appearance.Bound(settings.DockRadius, 0, 36, 11));
        var bg = ((SolidColorBrush)FindResource("Surface")).Color;
        bg.A = (byte)Math.Round(255 * Appearance.Bound(settings.DockOpacity, 0, 100, 100) / 100);
        var background = new SolidColorBrush(bg); background.Freeze(); Resources["DockBackground"] = background;
        var ink = ((SolidColorBrush)FindResource("Ink")).Color; ink.A = 28;
        var outline = new SolidColorBrush(ink); outline.Freeze(); Resources["DockOutline"] = outline;
        SurfaceSize(false);
    }
    public void SetPins(IEnumerable<ShelfItem> pins, int capacity)
        => SetTiles(pins.Select(i => new DockTile(i.Id, null, new[] { i })), capacity);
    public void SetTiles(IEnumerable<DockTile> pins, int capacity)
    {
        var all = pins.ToArray();
        var ids = all.Select(i => i.Key).ToHashSet();
        for (var i = _rows.Count - 1; i >= 0; i--) if (!ids.Contains(_rows[i].Key)) _rows.RemoveAt(i);
        var inserted = false;
        var lookup = _rows.ToDictionary(r => r.Key);
        for (var i = 0; i < all.Length; i++)
        {
            if (i < _rows.Count && _rows[i].Key == all[i].Key) { _rows[i].SetTile(all[i]); continue; }
            lookup.TryGetValue(all[i].Key, out var existing);
            if (existing is not null) { existing.SetTile(all[i]); _rows.Move(_rows.IndexOf(existing), i); }
            else { var row = new ItemRow(all[i].Items[0]); row.SetTile(all[i]); _rows.Insert(i, row); inserted = true; }
        }
        PinnedItems.Width = Math.Min(all.Length, capacity) * _slot;
        PinnedItems.Height = _slot + 2;
        if (inserted && _rows.Count > capacity)
            Dispatcher.BeginInvoke(() => { if (_rows.Count > 0) PinnedItems.ScrollIntoView(_rows[^1]); }, DispatcherPriority.Loaded);
        EmptyHint.Visibility = all.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Divider.Visibility = all.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        OverflowButton.Visibility = all.Length > capacity ? Visibility.Visible : Visibility.Collapsed;
        OverflowButton.Content = "+" + (all.Length - capacity);
    }
    public void SetSelection(IReadOnlySet<string> selected, bool mode)
    {
        foreach (var row in _rows) row.Selected = selected.Contains(row.Key);
        SelectedButton.Content = selected.Count + " selected";
        SelectedButton.Visibility = selected.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DoneButton.Visibility = mode || selected.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Text = mode && _rows.Count == 0 ? "No clips to select" : "Drop here";
    }
    public void ShowInsertion(int? index)
    {
        var row = index is int position && _rows.Count > 0 ? _rows[Math.Clamp(position, 0, _rows.Count - 1)] : null;
        var mark = index >= _rows.Count ? 2 : 1;
        if (_cue == row && (row is null || row.DropMark == mark)) return;
        if (_cue is not null) _cue.DropMark = 0;
        _cue = row; if (row is not null) row.DropMark = mark;
    }
    public int? InsertionAt(Point point)
    {
        if (_rows.Count == 0)
        {
            var hint = PinnedItems.TranslatePoint(point, EmptyHint);
            return hint.X >= -8 && hint.X <= EmptyHint.ActualWidth + 8 && hint.Y >= -12 && hint.Y <= EmptyHint.ActualHeight + 12 ? 0 : null;
        }
        if (point.X < 0 || point.Y < 0 || point.X > PinnedItems.ActualWidth || point.Y > PinnedItems.ActualHeight) return null;
        if (FindScroll(PinnedItems) is { ScrollableWidth: > 0 } scroll && Environment.TickCount64 - _lastScroll > 90)
        {
            var direction = point.X < 18 ? -1 : point.X > PinnedItems.ActualWidth - 18 ? 1 : 0;
            if (direction != 0) { scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset + direction * _slot); _lastScroll = Environment.TickCount64; }
        }
        var hit = PinnedItems.InputHitTest(point) as DependencyObject;
        while (hit is not null && hit is not ListBoxItem) hit = VisualTreeHelper.GetParent(hit);
        if (hit is ListBoxItem { DataContext: ItemRow row } container)
        {
            var local = PinnedItems.TranslatePoint(point, container);
            return _rows.IndexOf(row) + (local.X >= container.ActualWidth / 2 ? 1 : 0);
        }
        var offset = FindScroll(PinnedItems)?.HorizontalOffset ?? 0;
        return Math.Clamp((int)Math.Round((offset + point.X) / _slot), 0, _rows.Count);
    }
    public int PinIndex(int slot) => _rows.Take(slot).Sum(r => r.Members.Length);
    public ItemRow? CenterAt(Point point)
    {
        var hit = PinnedItems.InputHitTest(point) as DependencyObject;
        while (hit is not null && hit is not ListBoxItem) hit = VisualTreeHelper.GetParent(hit);
        if (hit is not ListBoxItem { DataContext: ItemRow row } container) return null;
        var local = PinnedItems.TranslatePoint(point, container);
        return local.Y >= 0 && local.Y <= container.ActualHeight && local.X >= container.ActualWidth * .28 && local.X <= container.ActualWidth * .72 ? row : null;
    }
    public void ShowStackTarget(ItemRow? target)
    {
        if (target is null) return;
        if (ReferenceEquals(_cue, target) && target.DropMark == 3) return;
        ShowInsertion(null); _cue = target; target.DropMark = 3;
    }
    private static ScrollViewer? FindScroll(DependencyObject node)
    {
        if (node is ScrollViewer viewer) return viewer;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) if (FindScroll(VisualTreeHelper.GetChild(node, i)) is ScrollViewer child) return child;
        return null;
    }
    private void OnPinWheel(object sender, MouseWheelEventArgs e)
    {
        if (FindScroll(PinnedItems) is { ScrollableWidth: > 0 } scroll) { scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - Math.Sign(e.Delta) * _slot * 2); e.Handled = true; }
    }
    private void OnKeep(object sender, RoutedEventArgs e) => KeepRequested?.Invoke(sender, e);
    private void OnSelection(object sender, RoutedEventArgs e) => SelectionRequested?.Invoke(sender, e);
    private void OnSelectionRight(object sender, MouseButtonEventArgs e) { e.Handled = true; SelectionRequested?.Invoke(sender, new RoutedEventArgs()); }
    private void OnDone(object sender, RoutedEventArgs e) => DoneRequested?.Invoke(sender, e);
    private void OnSave(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(sender, e);
    private void OnLibrary(object sender, RoutedEventArgs e) => LibraryRequested?.Invoke(sender, e);
    public void SetPanelName(string name, bool independent = false)
    {
        Body.ToolTip = name + " · double-click free space for Library · right-click for options · drag free space or Save to move";
        SaveButton.ToolTip = "Save to " + name + " · click to save · drag to move · right-click for options";
        PanelTitle.Text = name; PanelTitle.ToolTip = name;
        PanelTitle.Visibility = independent ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnPin(object sender, RoutedEventArgs e) => PinRequested?.Invoke(sender, e);
    private void OnPinLoaded(object sender, RoutedEventArgs e) => PinLoaded?.Invoke(sender, e);
    private void OnImageContext(object sender, DependencyPropertyChangedEventArgs e) { if (sender is Image { IsLoaded: true }) ImageLoaded?.Invoke(sender, new RoutedEventArgs()); }
    private void OnImageLoaded(object sender, RoutedEventArgs e) => ImageLoaded?.Invoke(sender, e);
    private void OnPinMenu(object sender, MouseButtonEventArgs e) => PinMenu?.Invoke(sender, e);
    private void OnPinDown(object sender, MouseButtonEventArgs e) => PinDown?.Invoke(sender, e);
    private void OnPinUp(object sender, MouseButtonEventArgs e) => PinUp?.Invoke(sender, e);
    private void OnPinMove(object sender, MouseEventArgs e) => PinMove?.Invoke(sender, e);
}
