using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClipDrop.Core;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;
using Forms = System.Windows.Forms;

namespace ClipDrop.Windows;

public partial class MainWindow : Window
{
    private readonly StateStore _store;
    private readonly ShelfState _state;
    private readonly TransferService _transfer;
    private readonly ObservableCollection<ItemRow> _rows = new();
    private readonly ThumbnailCache _thumbnails = new();
    private readonly DispatcherTimer _save;
    private DesktopIntegration? _desktop;
    private Point? _origin;
    private ItemRow? _pendingSingle;
    private bool _dragging, _quit, _dirty;
    private string _storageStatus = "Saved";
    private readonly Stack<List<ShelfItem>> _removed = new();
    public Diagnostics DiagnosticLog { get; } = new();

    public MainWindow(StateStore store, ShelfState state, bool integration = true)
    {
        _store = store; _state = state;
        _transfer = new TransferService(new AssetStore(Path.Combine(store.Root, "assets")));
        InitializeComponent();
        ItemList.ItemsSource = _rows;
        _save = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _save.Tick += (_, _) => TrySave();
        Loaded += (_, _) => {
            RestorePosition();
            LocationChanged += (_, _) => QueueGeometrySave();
            SizeChanged += (_, _) => QueueGeometrySave();
            if (integration)
            {
                _desktop = new DesktopIntegration(this, ToggleShelf, ShowShelf, () => { ShowShelf(); Paste(); }, Quit);
                _desktop.ShakeEnabled = _state.Settings.ShakeToShow;
                if (!_desktop.HotkeyAvailable) SetStatus("Shortcut in use. Open ClipDrop from the tray.");
            }
            if (_store.RecoveryNotice is not null) SetStatus(_store.RecoveryNotice);
        };
        Closing += OnClosing;
        DragLeave += (_, _) => DropCue.Visibility = Visibility.Collapsed;
        ApplyAppearance();
        Refresh();
    }

    public void ShowShelf() { Show(); Activate(); Refresh(); }
    private void ToggleShelf() { if (IsVisible && IsActive) Hide(); else ShowShelf(); }

    private void RestorePosition()
    {
        Width = Bound(_state.Settings.Width, 320, 900, 376);
        Height = Bound(_state.Settings.Height, 300, 1000, 500);
        if (_state.Settings.Left is double left && _state.Settings.Top is double top && double.IsFinite(left) && double.IsFinite(top))
        { Left = left; Top = top; }
        // Convert monitor physical pixels to WPF device-independent units.
        var work = Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var start = transform.Transform(new Point(work.Left, work.Top));
        var end = transform.Transform(new Point(work.Right, work.Bottom));
        Width = Math.Min(Width, end.X - start.X);
        Height = Math.Min(Height, end.Y - start.Y);
        Left = Math.Clamp(Left, start.X, Math.Max(start.X, end.X - Width));
        Top = Math.Clamp(Top, start.Y, Math.Max(start.Y, end.Y - Height));
    }

    private static double Bound(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    public void ApplyAppearance()
    {
        var settings = _state.Settings;
        Topmost = settings.AlwaysOnTop;
        TopIcon.Opacity = Topmost ? 1 : 0.35;
        FontSize = Bound(settings.FontSize, 11, 18, 13);
        Application.Current.Resources["ItemRadius"] = new CornerRadius(Bound(settings.CornerRadius, 0, 16, 8));
        var light = settings.Theme == "Light";
        foreach (var pair in new Dictionary<string, string> {
            ["Surface"] = light ? "#F5F5F7" : "#18191C", ["Raised"] = light ? "#E7E8EC" : "#23252A",
            ["Hover"] = light ? "#D9DBE1" : "#303239", ["Stroke"] = light ? "#858B97" : "#41444C",
            ["Ink"] = light ? "#17191D" : "#F2F3F5", ["Muted"] = light ? "#515764" : "#AEB2BC",
            ["Selected"] = light ? "#CDD1DA" : "#3B404B" })
            Application.Current.Resources[pair.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pair.Value));
        var style = new Style(typeof(ListBoxItem), (Style)Application.Current.FindResource(typeof(ListBoxItem)));
        style.Setters.Add(new Setter(MarginProperty, new Thickness(0, 0, 0, Bound(settings.RowSpacing, 0, 12, 4))));
        ItemList.ItemContainerStyle = style;
        if (_desktop is not null) _desktop.ShakeEnabled = settings.ShakeToShow;
    }

    private void Refresh()
    {
        if (ItemList is null) return;
        var selected = Selected().Select(i => i.Id).ToHashSet();
        var query = SearchBox.Text.Trim();
        _rows.Clear();
        foreach (var item in _state.Items.OrderByDescending(i => i.IsPinned).ThenByDescending(i => i.CreatedUtc))
            if (query.Length == 0 || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                _rows.Add(new ItemRow(item));
        foreach (var row in _rows.Where(r => selected.Contains(r.Item.Id))) ItemList.SelectedItems.Add(row);
        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = query.Length > 0 ? "No matching items" : "Drop something here";
        CountLabel.Text = _state.Items.Count.ToString();
        UpdateSelection();
    }

    private List<ShelfItem> Selected() => ItemList?.SelectedItems.Cast<ItemRow>().Select(r => r.Item).ToList() ?? new();
    private void UpdateSelection()
    {
        var selected = Selected();
        CopyButton.IsEnabled = PinButton.IsEnabled = RemoveButton.IsEnabled = selected.Count > 0;
        PinButton.Content = selected.Count > 0 && selected.All(i => i.IsPinned) ? "Unpin" : "Pin";
    }
    private void OnSelection(object sender, SelectionChangedEventArgs e) => UpdateSelection();
    private void OnSearch(object sender, TextChangedEventArgs e) => Refresh();
    private void Changed()
    {
        _dirty = true; _storageStatus = "Saving"; SetStatus("Saving…");
        _save.Stop(); _save.Start(); Refresh();
    }
    private void QueueGeometrySave()
    {
        if (WindowState != WindowState.Normal) return;
        _state.Settings.Width = Width; _state.Settings.Height = Height;
        _state.Settings.Left = Left; _state.Settings.Top = Top;
        _dirty = true; _storageStatus = "Saving";
        _save.Stop(); _save.Start();
    }
    public bool TrySave()
    {
        _save.Stop();
        if (!_dirty) return true;
        try { _store.Save(_state); _dirty = false; _storageStatus = "Saved"; SetStatus("Saved"); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { DiagnosticLog.Record("Save shelf", ex); _storageStatus = "Save failed; unsaved changes retained"; SetStatus("Save failed — open Settings to retry or copy diagnostics"); return false; }
    }
    private void SetStatus(string message) => StatusLabel.Text = message;
    private void Error(string operation, Exception ex)
    { DiagnosticLog.Record(operation, ex); SetStatus(operation + " failed — " + SafeMessage(ex)); }
    private static string SafeMessage(Exception ex) => ex is FileNotFoundException ? "original file unavailable" :
        ex is ExternalException ? "clipboard or application is busy; try again" : "try again or copy diagnostics from Settings";

    private void Add(IEnumerable<ShelfItem> items)
    {
        var count = 0;
        foreach (var item in items)
        {
            if (_state.Items.Count >= 5000) { SetStatus("Shelf limit reached (5,000 items). Remove unneeded cards."); break; }
            if (!_state.Items.Any(i => i.SameContent(item))) { _state.Items.Add(item); count++; }
        }
        if (count > 0) Changed(); else SetStatus("No new items to add");
    }
    private void Paste()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null) { SetStatus("Clipboard is empty"); return; }
            var items = _transfer.Import(data);
            if (items.Count == 0) { SetStatus("No supported files, image or text in clipboard"); return; }
            Add(items);
        }
        catch (Exception ex) { Error("Paste", ex); }
    }
    private void OnPaste(object sender, RoutedEventArgs e) => Paste();
    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Add files to ClipDrop", Filter = "All files|*.*" };
        if (picker.ShowDialog(this) != true) return;
        try { Add(picker.FileNames.Select(ShelfItem.FromPath).ToList()); }
        catch (Exception ex) { Error("Add files", ex); }
    }
    private void OnDragOver(object sender, DragEventArgs e)
    {
        var supported = e.Data.GetFormats(false).Any(f => f is "FileDrop" or "PNG" or "Bitmap" or "UnicodeText" or "Text" or "FileGroupDescriptorW");
        if (e.Data.GetDataPresent("NasaatR.ClipDrop.Internal")) supported = false;
        e.Effects = supported ? DragDropEffects.Copy : DragDropEffects.None;
        DropCue.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }
    private void OnDrop(object sender, DragEventArgs e)
    {
        DropCue.Visibility = Visibility.Collapsed;
        e.Handled = true; e.Effects = DragDropEffects.None;
        if (e.Data.GetDataPresent("NasaatR.ClipDrop.Internal")) return;
        try { var items = _transfer.Import(e.Data); if (items.Count == 0) SetStatus("This content has no supported transfer format"); else { Add(items); e.Effects = DragDropEffects.Copy; } }
        catch (Exception ex) { Error("Drop", ex); }
    }
    private static ListBoxItem? Container(DependencyObject? node)
    {
        while (node is not null && node is not ListBoxItem) node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        return node as ListBoxItem;
    }
    private void OnItemDown(object sender, MouseButtonEventArgs e)
    {
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
        var selected = Selected();
        if (selected.Count == 0) return;
        try
        {
            _dragging = true;
            var data = _transfer.Build(selected);
            data.SetData("NasaatR.ClipDrop.Internal", "1");
            // Copy only: a target must never move/delete the user's originals through this shelf.
            var result = DragDrop.DoDragDrop(ItemList, data, DragDropEffects.Copy);
            SetStatus(result == DragDropEffects.None ? "Transfer cancelled — items kept" : "Transfer finished — items kept");
        }
        catch (Exception ex) { Error("Drag", ex); }
        finally { _dragging = false; DropCue.Visibility = Visibility.Collapsed; }
    }
    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetDataObject(_transfer.Build(Selected()), true); SetStatus("Copied"); }
        catch (Exception ex) { Error("Copy", ex); }
    }
    private void OnCopyText(object sender, RoutedEventArgs e)
    {
        var selection = Selected();
        if (selection.Count == 0) return;
        try { Clipboard.SetText(string.Join(Environment.NewLine, selection.Select(i => i.FilePath ?? i.Content))); SetStatus("Text copied"); }
        catch (Exception ex) { Error("Copy text", ex); }
    }
    private void OnPin(object sender, RoutedEventArgs e)
    { var items = Selected(); var pin = !items.All(i => i.IsPinned); foreach (var item in items) item.IsPinned = pin; if (items.Count > 0) Changed(); }
    private void Remove(List<ShelfItem> items)
    {
        if (items.Count == 0) return;
        _removed.Push(items); while (_removed.Count > 20) { var keep = _removed.Take(20).Reverse().ToList(); _removed.Clear(); foreach (var batch in keep) _removed.Push(batch); }
        foreach (var item in items) _state.Items.Remove(item);
        Changed();
    }
    private void OnRemove(object sender, RoutedEventArgs e) => Remove(Selected());
    private void OnClear(object sender, RoutedEventArgs e) => Remove(_state.Items.Where(i => !i.IsPinned).ToList());
    private void UndoRemove()
    { if (_removed.TryPop(out var items)) { _state.Items.AddRange(items.Where(i => _state.Items.All(existing => existing.Id != i.Id))); Changed(); } }
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
        catch (Exception ex) { Error("Open", ex); }
    }
    private async void OnThumbnailLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image && image.DataContext is ItemRow { Item.FilePath: string path })
        {
            image.Source = null;
            var thumbnail = await _thumbnails.Get(path);
            if (image.DataContext is ItemRow current && current.Item.FilePath == path) image.Source = thumbnail;
        }
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.V && Keyboard.FocusedElement is not TextBox) { Paste(); e.Handled = true; }
            else if (e.Key == Key.F) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
            else if (e.Key == Key.O) { OnAddFiles(this, e); e.Handled = true; }
            else if (Keyboard.FocusedElement is not TextBox)
            {
                if (e.Key == Key.C) OnCopy(this, e);
                else if (e.Key == Key.A) ItemList.SelectAll();
                else if (e.Key == Key.Z) UndoRemove();
                else return;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox) { OnRemove(this, e); e.Handled = true; }
        else if (e.Key == Key.Escape) { if (SearchBox.Text.Length > 0) SearchBox.Clear(); else Hide(); e.Handled = true; }
        else if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBox && Selected().Count == 1)
        { new PreviewWindow(Selected()[0]) { Owner = this }.Show(); e.Handled = true; }
    }
    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var existing = OwnedWindows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing is not null) { existing.Activate(); return; }
        new SettingsWindow(_state.Settings, () => { ApplyAppearance(); Changed(); }, () => TrySave(),
            () => DiagnosticLog.Report(_state.Items.Count, _storageStatus), _store.Root) { Owner = this }.Show();
    }
    private void OnToggleTop(object sender, RoutedEventArgs e) { _state.Settings.AlwaysOnTop = !_state.Settings.AlwaysOnTop; ApplyAppearance(); Changed(); }
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
    private void OnQuit(object sender, RoutedEventArgs e) => Quit();
    private void Quit() { _quit = true; Close(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_quit && _state.Settings.CloseToTray) { e.Cancel = true; Hide(); return; }
        _state.Settings.Width = Width; _state.Settings.Height = Height; _state.Settings.Left = Left; _state.Settings.Top = Top;
        _dirty = true;
        if (!TrySave()) { e.Cancel = true; _quit = false; ShowShelf(); return; }
        _desktop?.Dispose(); _save.Stop(); Application.Current.Shutdown();
    }
}
