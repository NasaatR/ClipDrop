using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

namespace ClipDrop.Windows;

// The existing shelf becomes the focused library. The session owns all mutable data.
public partial class MainWindow : Window
{
    public ShelfSession Session { get; }
    private readonly ObservableCollection<ItemRow> _rows = new();
    private readonly ThumbnailCache _thumbnails = new();
    private Point? _origin;
    private ItemRow? _pendingSingle;
    private bool _dragging;
    public Diagnostics DiagnosticLog => Session.Diagnostics;
    public MainWindow(StateStore store, ShelfState state, bool integration = true) : this(new ShelfSession(store, state)) { }
    public MainWindow(ShelfSession session)
    {
        Session = session;
        InitializeComponent();
        ItemList.ItemsSource = _rows;
        Session.Changed += Refresh;
        Session.StatusChanged += SetStatus;
        Loaded += (_, _) => {
            WindowPlacement.Restore(this, Session.State.Settings.Left, Session.State.Settings.Top,
                Session.State.Settings.Width, Session.State.Settings.Height);
            LocationChanged += (_, _) => QueueGeometrySave();
            SizeChanged += (_, _) => QueueGeometrySave();
        };
        Closing += OnClosing;
        DragLeave += (_, e) => {
            var point = e.GetPosition(this);
            if (point.X < 0 || point.Y < 0 || point.X > ActualWidth || point.Y > ActualHeight)
                DropCue.Visibility = Visibility.Collapsed;
        };
        ApplyAppearance(); Refresh();
    }
    public void ShowShelf() { Session.Paster?.CaptureTarget(); Show(); Activate(); Refresh(); }
    public void OpenSettings()
    {
        var existing = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing is not null) { existing.Activate(); return; }
        Session.Paster?.CaptureTarget();
        new SettingsWindow(Session.State.Settings, () => Session.Change(), () => TrySave(), Session.Report, Session.Root) { Owner = IsVisible ? this : null, WindowStartupLocation = IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen }.Show();
    }
    private static double Bound(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    public void ApplyAppearance()
    {
        var settings = Session.State.Settings;
        Appearance.Apply(settings);
        Topmost = settings.AlwaysOnTop; TopIcon.Opacity = Topmost ? 1 : 0.35;
        FontSize = Bound(settings.FontSize, 11, 18, 13);
        var style = new Style(typeof(ListBoxItem), (Style)Application.Current.FindResource(typeof(ListBoxItem)));
        style.Setters.Add(new Setter(MarginProperty, new Thickness(0, 0, 0, Bound(settings.RowSpacing, 0, 12, 4))));
        ItemList.ItemContainerStyle = style;
    }
    private void Refresh()
    {
        if (ItemList is null) return;
        ApplyAppearance();
        var selected = Selected().Select(i => i.Id).ToHashSet();
        var query = SearchBox.Text.Trim();
        _rows.Clear();
        foreach (var item in Session.State.Items.OrderByDescending(i => i.IsPinned).ThenByDescending(i => i.CreatedUtc))
            if (query.Length == 0 || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                _rows.Add(new ItemRow(item));
        foreach (var row in _rows.Where(r => selected.Contains(r.Item.Id))) ItemList.SelectedItems.Add(row);
        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = query.Length > 0 ? "No matching items" : "Your library is ready";
        CountLabel.Text = Session.State.Items.Count.ToString(); UpdateSelection();
    }
    private System.Collections.Generic.List<ShelfItem> Selected() => ItemList?.SelectedItems.Cast<ItemRow>().Select(r => r.Item).ToList() ?? new();
    private void UpdateSelection()
    {
        var selected = Selected();
        CopyButton.IsEnabled = PinButton.IsEnabled = RemoveButton.IsEnabled = PasteButton.IsEnabled = selected.Count > 0;
        PinButton.Content = selected.Count > 0 && selected.All(i => i.IsPinned) ? "Unpin" : "Pin";
    }
    private void OnSelection(object sender, SelectionChangedEventArgs e) => UpdateSelection();
    private void OnSearch(object sender, TextChangedEventArgs e) => Refresh();
    private void QueueGeometrySave()
    {
        if (WindowState != WindowState.Normal) return;
        var settings = Session.State.Settings;
        settings.Width = Width; settings.Height = Height; settings.Left = Left; settings.Top = Top;
        Session.Change(false);
    }
    public bool TrySave() => Session.TrySave();
    private void SetStatus(string text) => StatusLabel.Text = text;
    private void OnPaste(object sender, RoutedEventArgs e) => Session.SaveClipboard();
    private void OnPasteSelected(object sender, RoutedEventArgs e) => Session.Paste(Selected());
    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Add files to ClipDrop", Filter = "All files|*.*" };
        if (picker.ShowDialog(this) != true) return;
        try { Session.Add(picker.FileNames.Select(ShelfItem.FromPath).ToList()); }
        catch (Exception ex) { Session.Error("Add files", ex); }
    }
    private void OnDragOver(object sender, DragEventArgs e)
    {
        var supported = TransferService.Supports(e.Data);
        e.Effects = supported ? DragDropEffects.Copy : DragDropEffects.None;
        DropCue.Visibility = supported ? Visibility.Visible : Visibility.Collapsed; e.Handled = true;
    }
    private void OnDrop(object sender, DragEventArgs e)
    {
        DropCue.Visibility = Visibility.Collapsed; e.Handled = true; e.Effects = DragDropEffects.None;
        if (!TransferService.Supports(e.Data)) return;
        try { if (Session.Capture(e.Data)) e.Effects = DragDropEffects.Copy; }
        catch (Exception ex) { Session.Error("Drop", ex); }
    }
    private static ListBoxItem? Container(DependencyObject? node)
    {
        while (node is not null && node is not ListBoxItem) node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        return node as ListBoxItem;
    }
    private void OnItemDown(object sender, MouseButtonEventArgs e)
    {
        if (Container(e.OriginalSource as DependencyObject) is null) { _origin = null; _pendingSingle = null; return; }
        _origin = e.GetPosition(ItemList); _pendingSingle = null;
        if (Container(e.OriginalSource as DependencyObject) is { IsSelected: true, DataContext: ItemRow row } && ItemList.SelectedItems.Count > 1 && Keyboard.Modifiers == ModifierKeys.None)
        { _pendingSingle = row; e.Handled = true; }
    }
    private void OnItemUp(object sender, MouseButtonEventArgs e)
    {
        if (_pendingSingle is not null) { ItemList.SelectedItems.Clear(); ItemList.SelectedItem = _pendingSingle; }
        _origin = null; _pendingSingle = null;
    }
    private void OnItemMove(object sender, MouseEventArgs e)
    {
        if (_dragging || e.LeftButton != MouseButtonState.Pressed || _origin is not Point start) return;
        var current = e.GetPosition(ItemList);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _origin = null; _pendingSingle = null;
        var selected = Selected(); if (selected.Count == 0) return;
        try
        {
            _dragging = true;
            var result = DragDrop.DoDragDrop(ItemList, Session.Transfer.BuildDrag(selected), DragDropEffects.Copy);
            Session.Notify(result == DragDropEffects.None ? "Transfer cancelled · items kept" : "Transfer finished · items kept");
        }
        catch (Exception ex) { Session.Error("Drag", ex); }
        finally { _dragging = false; DropCue.Visibility = Visibility.Collapsed; }
    }
    private void OnCopy(object sender, RoutedEventArgs e) => Session.Copy(Selected());
    private void OnCopyText(object sender, RoutedEventArgs e) => Session.Copy(Selected(), true);
    private void OnPin(object sender, RoutedEventArgs e) => Session.TogglePin(Selected());
    private void OnRemove(object sender, RoutedEventArgs e) => Session.Remove(Selected());
    private void OnClear(object sender, RoutedEventArgs e) => Session.Remove(Session.State.Items.Where(i => !i.IsPinned).ToList());
    private void OnOpen(object sender, MouseButtonEventArgs e)
    {
        if (Container(e.OriginalSource as DependencyObject) is not { DataContext: ItemRow row }) return;
        var item = row.Item;
        try
        {
            if (item.Kind == ItemKind.Text) { new PreviewWindow(item) { Owner = this }.Show(); return; }
            var target = item.FilePath ?? item.Content.Trim();
            if (item.Kind == ItemKind.Link && (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))) return;
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) { Session.Error("Open", ex); }
    }
    private async void OnThumbnailLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image && image.DataContext is ItemRow { Item.FilePath: string path })
        {
            image.Source = null; var thumbnail = await _thumbnails.Get(path);
            if (image.DataContext is ItemRow current && current.Item.FilePath == path) image.Source = thumbnail;
        }
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.V && Keyboard.FocusedElement is not TextBox) { Session.SaveClipboard(); e.Handled = true; }
            else if (e.Key == Key.F) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
            else if (e.Key == Key.O) { OnAddFiles(this, e); e.Handled = true; }
            else if (Keyboard.FocusedElement is not TextBox)
            {
                if (e.Key == Key.C) OnCopy(this, e);
                else if (e.Key == Key.A) ItemList.SelectAll();
                else if (e.Key == Key.Z) Session.UndoRemove();
                else return;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter && Keyboard.FocusedElement is not TextBox) { Session.Paste(Selected()); e.Handled = true; }
        else if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox) { OnRemove(this, e); e.Handled = true; }
        else if (e.Key == Key.Escape) { if (SearchBox.Text.Length > 0) SearchBox.Clear(); else Hide(); e.Handled = true; }
        else if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBox && Selected().Count == 1)
        { new PreviewWindow(Selected()[0]) { Owner = this }.Show(); e.Handled = true; }
    }
    private void OnSettings(object sender, RoutedEventArgs e) => OpenSettings();
    private void OnToggleTop(object sender, RoutedEventArgs e) { Session.State.Settings.AlwaysOnTop = !Session.State.Settings.AlwaysOnTop; Session.Change(); }
    private void OnTitleDrag(object sender, MouseButtonEventArgs e)
    {
        var node = e.OriginalSource as DependencyObject;
        while (node is not null && !ReferenceEquals(node, sender))
        {
            if (node is Button) return;
            node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        if (e.ChangedButton == MouseButton.Left) { try { DragMove(); } catch (InvalidOperationException) { } }
    }
    private void OnHide(object sender, RoutedEventArgs e) => Hide();
    private void OnQuit(object sender, RoutedEventArgs e) => Session.RequestQuit();
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (Application.Current.Dispatcher.HasShutdownStarted || (Application.Current as App)?.IsQuitting == true) return;
        e.Cancel = true; Hide();
        if (!Session.State.Settings.CloseToTray) Session.RequestQuit();
    }
}
