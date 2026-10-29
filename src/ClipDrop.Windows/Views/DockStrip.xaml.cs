using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ClipDrop.Core;
using ClipDrop.Windows.Services;
namespace ClipDrop.Windows.Views;

// The exact same dock surface renders in the floating window and Settings preview.
public partial class DockStrip : UserControl
{
    public event RoutedEventHandler? SaveRequested, SettingsRequested, LibraryRequested, PinRequested, PinLoaded, ImageLoaded, KeepRequested, SelectRequested, DeleteSelectedRequested, DoneRequested, HideRequested;
    public event MouseButtonEventHandler? PinDown, PinUp, PinMenu;
    public event RoutedEventHandler? PanelsRequested;
    public event MouseEventHandler? PinMove;
    private readonly ObservableCollection<ItemRow> _rows = new();
    private double _slot = 40;
    private ItemRow? _cue;
    private long _lastScroll;
    private string _surfaceSignature = "";
    public DockStrip() { InitializeComponent(); PinnedItems.ItemsSource = _rows; }
    public void Apply(ShelfSettings settings)
    {
        Appearance.Apply(settings, this, true);
        var signature = string.Join("|", settings.DockButtonSize, settings.DockSpacing, settings.DockRadius, settings.DockOpacity,
            ((SolidColorBrush)FindResource("Surface")).Color, ((SolidColorBrush)FindResource("Ink")).Color);
        if (_surfaceSignature == signature) return;
        _surfaceSignature = signature;
        Resources["DockSize"] = Appearance.Bound(settings.DockButtonSize, 30, 48, 36);
        _slot = (double)Resources["DockSize"] + Appearance.Bound(settings.DockSpacing, 0, 12, 4);
        Resources["DockSlot"] = _slot;
        Resources["DockGap"] = new Thickness(Appearance.Bound(settings.DockSpacing, 0, 12, 4) / 2);
        Resources["DockRadius"] = new CornerRadius(Appearance.Bound(settings.DockRadius, 0, 24, 11));
        var bg = ((SolidColorBrush)FindResource("Surface")).Color;
        bg.A = (byte)Math.Round(255 * Appearance.Bound(settings.DockOpacity, 0, 100, 100) / 100);
        var background = new SolidColorBrush(bg); background.Freeze(); Resources["DockBackground"] = background;
        var ink = ((SolidColorBrush)FindResource("Ink")).Color; ink.A = 28;
        var outline = new SolidColorBrush(ink); outline.Freeze(); Resources["DockOutline"] = outline;
    }
    public void SetPins(IEnumerable<ShelfItem> pins, int capacity)
    {
        var all = pins.ToArray();
        var ids = all.Select(i => i.Id).ToHashSet();
        for (var i = _rows.Count - 1; i >= 0; i--) if (!ids.Contains(_rows[i].Item.Id)) _rows.RemoveAt(i);
        var inserted = false;
        var lookup = _rows.ToDictionary(r => r.Item.Id);
        for (var i = 0; i < all.Length; i++)
        {
            if (i < _rows.Count && _rows[i].Item.Id == all[i].Id) { _rows[i].Refresh(); continue; }
            lookup.TryGetValue(all[i].Id, out var existing);
            if (existing is not null) _rows.Move(_rows.IndexOf(existing), i);
            else { _rows.Insert(i, new ItemRow(all[i])); inserted = true; }
        }
        PinnedItems.Width = Math.Min(all.Length, capacity) * _slot;
        PinnedItems.Height = _slot + 2;
        if (inserted && _rows.Count > capacity)
            Dispatcher.BeginInvoke(() => { if (_rows.Count > 0) PinnedItems.ScrollIntoView(_rows[^1]); }, DispatcherPriority.Loaded);
        EmptyHint.Visibility = all.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        OverflowButton.Visibility = all.Length > capacity ? Visibility.Visible : Visibility.Collapsed;
        OverflowButton.Content = "+" + (all.Length - capacity);
    }
    public void SetSelection(IReadOnlySet<string> selected, bool mode)
    {
        foreach (var row in _rows) row.Selected = selected.Contains(row.Item.Id);
        SelectedCount.Text = selected.Count.ToString();
        SelectionActions.Visibility = selected.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DoneButton.Visibility = mode || selected.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectButton.SetResourceReference(BackgroundProperty, mode ? "Selected" : "Raised");
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
    private void OnHide(object sender, RoutedEventArgs e) => HideRequested?.Invoke(sender, e);
    private void OnKeep(object sender, RoutedEventArgs e) => KeepRequested?.Invoke(sender, e);
    private void OnSelect(object sender, RoutedEventArgs e) => SelectRequested?.Invoke(sender, e);
    private void OnDeleteSelected(object sender, RoutedEventArgs e) => DeleteSelectedRequested?.Invoke(sender, e);
    private void OnDone(object sender, RoutedEventArgs e) => DoneRequested?.Invoke(sender, e);
    private void OnSave(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(sender, e);
    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(sender, e);
    private void OnLibrary(object sender, RoutedEventArgs e) => LibraryRequested?.Invoke(sender, e);
    private void OnPanels(object sender, RoutedEventArgs e) => PanelsRequested?.Invoke(sender, e);
    public void SetPanelName(string name) => PanelsButton.ToolTip = "Saved panels · " + name;
    private void OnPin(object sender, RoutedEventArgs e) => PinRequested?.Invoke(sender, e);
    private void OnPinLoaded(object sender, RoutedEventArgs e) => PinLoaded?.Invoke(sender, e);
    private void OnImageContext(object sender, DependencyPropertyChangedEventArgs e) { if (sender is Image { IsLoaded: true }) ImageLoaded?.Invoke(sender, new RoutedEventArgs()); }
    private void OnImageLoaded(object sender, RoutedEventArgs e) => ImageLoaded?.Invoke(sender, e);
    private void OnPinMenu(object sender, MouseButtonEventArgs e) => PinMenu?.Invoke(sender, e);
    private void OnPinDown(object sender, MouseButtonEventArgs e) => PinDown?.Invoke(sender, e);
    private void OnPinUp(object sender, MouseButtonEventArgs e) => PinUp?.Invoke(sender, e);
    private void OnPinMove(object sender, MouseEventArgs e) => PinMove?.Invoke(sender, e);
}
