using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private readonly RowCollection _rows = new();
    private readonly System.Collections.Generic.Dictionary<string, ItemRow> _rowCache = new();
    private bool _refreshing;
    private double _rowSpacing = double.NaN;
    private readonly System.Windows.Threading.DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly ThumbnailCache _thumbnails = ThumbnailCache.Shared;
    private Point? _origin;
    private ItemRow? _pendingSingle;
    private bool _dragging;
    private Point? _batchOrigin;
    private DockWindow? _dock;
    public DockCoordinator? Docks { get; set; }
    private bool _placing;
    private string _filter = "All";
    private string? PanelContext => _dock?.PanelId is string id && Session.State.Panels.Any(p => p.Id == id && p.ClosedUtc is null) ? id : null;
    private bool _updatingGroups;
    private ShelfItem? _editing;
    public Diagnostics DiagnosticLog => Session.Diagnostics;
    public MainWindow(StateStore store, ShelfState state, bool integration = true) : this(new ShelfSession(store, state)) { }
    public MainWindow(ShelfSession session)
    {
        Session = session;
        InitializeComponent();
        if (ContextMenu is not null)
        {
            PopupDismiss.Attach(ContextMenu, this);
            ContextMenu.Opened += (_, _) => {
                var saved = Session.State.Panels.SelectMany(p => p.ClipIds).ToHashSet();
                ClearOption.IsEnabled = Session.State.Items.Any(i => !i.IsPinned && !saved.Contains(i.Id));
            };
        }
        _ = new WindowDrag(this, () => { Session.State.Settings.LibraryFollowsDock = false; UpdateAttachment(); }, QueueGeometrySave);
        ItemList.ItemsSource = _rows;
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); Refresh(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) _searchDelay.Stop(); };
        Session.Changed += Refresh;
        Session.StatusChanged += SetStatus;
        Session.PanelsRequested += OpenPanels;
        Loaded += (_, _) => {
            WindowPlacement.Restore(this, Session.State.Settings.Left, Session.State.Settings.Top,
                Session.State.Settings.Width, Session.State.Settings.Height);
            LocationChanged += (_, _) => QueueGeometrySave();
            SizeChanged += (_, _) => { PositionNearDock(); QueueGeometrySave(); };
        };
        Closing += OnClosing;
        DragLeave += (_, e) => {
            var point = e.GetPosition(this);
            if (point.X < 0 || point.Y < 0 || point.X > ActualWidth || point.Y > ActualHeight)
                DropCue.Visibility = Visibility.Collapsed;
        };
        ApplyAppearance(); Refresh();
    }
    public void AttachDock(DockWindow dock)
    {
        if (ReferenceEquals(_dock, dock)) return;
        if (_dock is not null) DetachDock(_dock);
        _dock = dock;
        dock.LocationChanged += DockMoved; dock.SizeChanged += DockResized; dock.IsVisibleChanged += DockVisibility;
        PositionNearDock(); if (IsVisible) Refresh();
    }
    public void DetachDock(DockWindow dock)
    {
        if (!ReferenceEquals(_dock, dock)) return;
        dock.LocationChanged -= DockMoved; dock.SizeChanged -= DockResized; dock.IsVisibleChanged -= DockVisibility; _dock = null;
        if (IsVisible) Refresh();
    }
    private void DockMoved(object? sender, EventArgs e) => PositionNearDock();
    private void DockResized(object sender, SizeChangedEventArgs e) => PositionNearDock();
    private void DockVisibility(object sender, DependencyPropertyChangedEventArgs e) => PositionNearDock();
    private void PositionNearDock()
    {
        if (_placing || !IsVisible || _dock?.IsVisible != true || !Session.State.Settings.LibraryFollowsDock) return;
        _placing = true;
        try { UpdateLayout(); WindowPlacement.Near(this, _dock); }
        finally { _placing = false; }
    }
    private void UpdateAttachment()
    {
        var follows = Session.State.Settings.LibraryFollowsDock;
        AttachOption.IsChecked = follows;
    }
    private void OnAttach(object sender, RoutedEventArgs e)
    {
        Session.State.Settings.LibraryFollowsDock = !Session.State.Settings.LibraryFollowsDock;
        Session.Change(); PositionNearDock();
    }
    public void ShowShelf()
    {
        Session.Paster?.CaptureTarget(); var opening = !IsVisible; Show(); Refresh(); PositionNearDock(); Activate();
        if (opening) Motion.Reveal(LibrarySurface);
    }
    public void OpenSettings()
    {
        var existing = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing is not null) { existing.Activate(); return; }
        Session.Paster?.CaptureTarget();
        var settings = new SettingsWindow(Session.State.Settings, Session.ChangeSettings, () => TrySave(), Session.Report, Session.Root, Docks, Session.BackupSnapshot) { Owner = IsVisible ? this : null, WindowStartupLocation = IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen };
        void StorageStatus(string _) => settings.SetStorageStatus(Session.StorageStatus);
        Session.StatusChanged += StorageStatus; settings.Closed += (_, _) => Session.StatusChanged -= StorageStatus;
        settings.SetStorageStatus(Session.StorageStatus); settings.Show();
    }
    public void OpenPanels(System.Collections.Generic.IReadOnlyList<ShelfItem>? clips = null)
    {
        Session.Paster?.CaptureTarget();
        var window = Application.Current.Windows.OfType<PanelsWindow>().FirstOrDefault();
        if (window is null)
        {
            window = new PanelsWindow(Session, Docks) { Owner = IsVisible ? this : null,
                WindowStartupLocation = IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen };
            window.SetSource(PanelContext); window.OfferSelection(clips); window.Show();
        }
        else { window.SetSource(PanelContext); window.OfferSelection(clips); window.Activate(); }
    }
    private void OnPanels(object sender, RoutedEventArgs e) => OpenPanels(Selected());
    private static double Bound(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    public void ApplyAppearance()
    {
        var settings = Session.State.Settings;
        Appearance.Apply(settings, this);
        UpdateAttachment(); PositionNearDock();
        Topmost = settings.AlwaysOnTop; TopOption.IsChecked = Topmost;
        FontSize = Bound(settings.FontSize, 10, 24, 13);
        var spacing = Bound(settings.RowSpacing, 0, 24, 4);
        if (_rowSpacing != spacing)
        {
            _rowSpacing = spacing;
            var style = new Style(typeof(ListBoxItem), (Style)Application.Current.FindResource(typeof(ListBoxItem)));
            style.Setters.Add(new Setter(MarginProperty, new Thickness(0, 0, 0, spacing))); style.Seal();
            ItemList.ItemContainerStyle = style;
        }
    }
    private void Refresh()
    {
        if (ItemList is null || IsLoaded && !IsVisible) return;
        ApplyAppearance();
        var selected = Selected().Select(i => i.Id).ToHashSet();
        var query = SearchBox.Text.Trim();
        _updatingGroups = true;
        var group = GroupFilter.SelectedItem as string ?? "All groups";
        var groups = new[] { "All groups" }.Concat(Session.State.Items.Concat(Session.State.Trash.Select(t => t.Item)).Select(i => i.Group).Where(g => g.Length > 0).Distinct().OrderBy(g => g)).ToArray();
        if (GroupFilter.ItemsSource is not string[] currentGroups || !currentGroups.SequenceEqual(groups))
        { GroupFilter.ItemsSource = groups; GroupFilter.SelectedItem = groups.Contains(group) ? group : "All groups"; }
        _updatingGroups = false;
        group = GroupFilter.SelectedItem as string ?? "All groups";
        var inPanel = Session.Pins(PanelContext).Select(i => i.Id).ToHashSet();
        PinnedFilter.ToolTip = "Clips in " + Session.PanelName(PanelContext);
        var source = _filter == "Trash" ? Session.State.Trash.OrderByDescending(t => t.DeletedUtc).Select(t => t.Item) : Session.State.Items.AsEnumerable();
        var available = Session.State.Items.Concat(Session.State.Trash.Select(t => t.Item)).Select(i => i.Id).ToHashSet();
        foreach (var id in _rowCache.Keys.Where(id => !available.Contains(id)).ToArray()) _rowCache.Remove(id);
        var next = new System.Collections.Generic.List<ItemRow>();
        foreach (var item in source.Where(i => (_filter != "Pinned" || inPanel.Contains(i.Id)) && (group == "All groups" || i.Group == group)).OrderByDescending(i => inPanel.Contains(i.Id)).ThenByDescending(i => i.CreatedUtc))
            if (query.Length == 0 || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Content.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Group.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                if (!_rowCache.TryGetValue(item.Id, out var row)) _rowCache[item.Id] = row = new ItemRow(item);
                else row.Refresh();
                next.Add(row);
            }
        _refreshing = true;
        try
        {
            _rows.Reconcile(next.ToArray());
            foreach (var row in _rows.Where(r => selected.Contains(r.Item.Id) && !ItemList.SelectedItems.Contains(r))) ItemList.SelectedItems.Add(row);
        }
        finally { _refreshing = false; }
        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = _filter == "Trash" ? "Trash is empty" : query.Length > 0 ? "No matching items" : "Your library is ready";
        TrashFilter.Content = "Trash " + Session.State.Trash.Count;
        foreach (var button in new[] { AllFilter, PinnedFilter, TrashFilter }) button.SetResourceReference(BackgroundProperty, (string)button.Tag == _filter ? "Selected" : "Raised");
        RestoreButton.Visibility = _filter == "Trash" ? Visibility.Visible : Visibility.Collapsed;
        if (_editing is not null && Session.State.Items.All(i => i.Id != _editing.Id)) { EditPanel.Visibility = Visibility.Collapsed; _editing = null; }
        CountLabel.Text = Session.State.Items.Count.ToString(); UpdateSelection();
    }
    private System.Collections.Generic.List<ShelfItem> Selected() => ItemList?.SelectedItems.Cast<ItemRow>().Select(r => r.Item).ToList() ?? new();
    private void UpdateSelection()
    {
        var selected = Selected();
        ActiveActions.Visibility = _filter != "Trash" && selected.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        PasteButton.IsEnabled = selected.Count > 0;
        DragSelectedButton.Content = "Drag " + selected.Count;
        RestoreButton.IsEnabled = selected.Count > 0;
    }
    private void OnSelection(object sender, SelectionChangedEventArgs e) { if (!_refreshing) UpdateSelection(); }
    private void OnSearch(object sender, TextChangedEventArgs e) { _searchDelay.Stop(); _searchDelay.Start(); }
    private void QueueGeometrySave()
    {
        if (WindowState != WindowState.Normal) return;
        var settings = Session.State.Settings;
        settings.Width = Width; settings.Height = Height; settings.Left = Left; settings.Top = Top;
        Session.Change(false);
    }
    public bool TrySave() => Session.TrySave();
    private void SetStatus(string text) => StatusLabel.Text = text;
    private void OnPaste(object sender, RoutedEventArgs e) => Collect(() => Session.SaveClipboard(false));
    private void Collect(Action capture)
    {
        var before = Session.State.Items.Select(i => i.Id).ToHashSet();
        capture();
        var added = Session.State.Items.Where(i => !before.Contains(i.Id)).Select(i => i.Id).ToArray();
        if (added.Length == 0) return;
        _filter = "All"; SearchBox.Clear(); GroupFilter.SelectedItem = "All groups"; Refresh();
        var rows = _rows.ToDictionary(r => r.Item.Id); _refreshing = true;
        try
        {
            ItemList.SelectedItems.Clear();
            foreach (var id in added) if (rows.TryGetValue(id, out var row)) ItemList.SelectedItems.Add(row);
        }
        finally { _refreshing = false; UpdateSelection(); }
        if (ItemList.SelectedItem is not null) ItemList.ScrollIntoView(ItemList.SelectedItem);
    }
    private void OnPasteSelected(object sender, RoutedEventArgs e) => Session.Paste(Selected());
    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Add files to ClipDrop", Filter = "All files|*.*" };
        if (picker.ShowDialog(this) != true) return;
        try { Collect(() => Session.Add(picker.FileNames.Select(ShelfItem.FromPath).ToList(), false)); }
        catch (Exception ex) { Session.Error("Add files", ex); }
    }
    private void OnDragOver(object sender, DragEventArgs e)
    {
        var supported = Session.OwnDrag(e.Data) is not null || TransferService.Supports(e.Data);
        e.Effects = supported ? DragDropEffects.Copy : DragDropEffects.None;
        DropCue.Visibility = supported ? Visibility.Visible : Visibility.Collapsed; e.Handled = true;
    }
    private void OnDrop(object sender, DragEventArgs e)
    {
        DropCue.Visibility = Visibility.Collapsed; e.Handled = true; e.Effects = DragDropEffects.None;
        if (Session.OwnDrag(e.Data) is { } own)
        {
            own.InternalHandled = true; e.Effects = DragDropEffects.Copy;
            Session.Notify("Already in Library · clips kept"); return;
        }
        if (!TransferService.Supports(e.Data)) return;
        try { Collect(() => { if (Session.Capture(e.Data, false)) e.Effects = DragDropEffects.Copy; }); }
        catch (Exception ex) { Session.Error("Drop", ex); }
    }
    private static ListBoxItem? Container(DependencyObject? node)
    {
        while (node is not null && node is not ListBoxItem) node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        return node as ListBoxItem;
    }
    private void OnItemDown(object sender, MouseButtonEventArgs e)
    {
        if (IsSelector(e.OriginalSource as DependencyObject)) { _origin = null; _pendingSingle = null; return; }
        if (_filter == "Trash" || Container(e.OriginalSource as DependencyObject) is null) { _origin = null; _pendingSingle = null; return; }
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
        StartDrag(ItemList);
    }
    private void StartDrag(DependencyObject source)
    {
        var selected = Selected().ToArray(); if (_dragging || selected.Length == 0 || _filter == "Trash") return;
        try
        {
            _dragging = true;
            using var transfer = Session.BeginDrag(selected);
            var result = DragDrop.DoDragDrop(source, transfer.Data, DragDropEffects.Copy);
            Session.CompleteDrag(transfer, result);
        }
        catch (Exception ex) { Session.Error("Drag", ex); }
        finally { _dragging = false; DropCue.Visibility = Visibility.Collapsed; }
    }
    private static bool IsSelector(DependencyObject? source)
    {
        while (source is not null && source is not ListBoxItem)
        { if (source is ToggleButton) return true; source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source); }
        return false;
    }
    private void OnBatchDown(object sender, MouseButtonEventArgs e) => _batchOrigin = e.GetPosition(this);
    private void OnBatchUp(object sender, MouseButtonEventArgs e) => _batchOrigin = null;
    private void OnBatchMove(object sender, MouseEventArgs e)
    {
        if (_batchOrigin is not Point start || e.LeftButton != MouseButtonState.Pressed || _dragging) return;
        var point = e.GetPosition(this);
        if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _batchOrigin = null; ((Button)sender).ReleaseMouseCapture(); StartDrag((Button)sender);
    }
    private void OnDragHint(object sender, RoutedEventArgs e) => Session.SetStatus("Drag this button or any selected clip into your app");
    private void OnSelectAll(object sender, RoutedEventArgs e) => ItemList.SelectAll();
    private void OnLibraryMenu(object sender, RoutedEventArgs e)
    {
        ClipMenu.Show(ContextMenu, (FrameworkElement)sender, true);
    }
    private void OnSelectionMenu(object sender, RoutedEventArgs e)
    {
        var selected = Selected(); if (selected.Count == 0) return;
        Session.Paster?.CaptureTarget();
        ClipMenu.Show(ClipMenu.Create(Session, selected, this, false, _filter == "Trash", EditItem, PanelContext), (FrameworkElement)sender, true);
    }
    public void EditItem(ShelfItem item)
    {
        ShowShelf(); _filter = "All"; SearchBox.Clear(); GroupFilter.SelectedItem = "All groups"; Refresh();
        var row = _rows.FirstOrDefault(r => r.Item.Id == item.Id);
        if (row is null) return;
        ItemList.SelectedItems.Clear(); ItemList.SelectedItem = row; ItemList.ScrollIntoView(row); OnEdit(this, new RoutedEventArgs());
    }
    private void OnItemRightDown(object sender, MouseButtonEventArgs e)
    {
        if (Container(e.OriginalSource as DependencyObject) is not { DataContext: ItemRow row } container) return;
        if (!container.IsSelected) { ItemList.SelectedItems.Clear(); ItemList.SelectedItem = row; }
    }
    private void OnClipMenu(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        var selected = Selected(); if (selected.Count == 0) return;
        Session.Paster?.CaptureTarget();
        var target = ItemList.ItemContainerGenerator.ContainerFromItem(ItemList.SelectedItem) as FrameworkElement ?? ItemList;
        ItemList.ContextMenu = ClipMenu.Create(Session, selected, this, false, _filter == "Trash", EditItem, PanelContext);
        ClipMenu.Show(ItemList.ContextMenu, target, e.CursorLeft < 0);
    }
    private void OnFilter(object sender, RoutedEventArgs e)
    {
        _filter = sender is Button { Tag: string filter } ? filter : "All";
        EditPanel.Visibility = Visibility.Collapsed; _editing = null; Refresh();
    }
    private void OnGroupFilter(object sender, SelectionChangedEventArgs e) { if (!_updatingGroups && IsLoaded) Refresh(); }
    private void OnRestore(object sender, RoutedEventArgs e) => Session.RestoreTrash(Selected());
    private void OnEdit(object sender, RoutedEventArgs e)
    {
        if (_filter == "Trash" || Selected().Count != 1) return;
        _editing = Selected()[0]; NameEditor.Text = _editing.Title; GroupEditor.Text = _editing.Group;
        EditPanel.Visibility = Visibility.Visible; NameEditor.Focus(); NameEditor.SelectAll();
    }
    private void OnSaveEdit(object sender, RoutedEventArgs e)
    {
        if (_editing is null || string.IsNullOrWhiteSpace(NameEditor.Text)) return;
        Session.Rename(_editing, NameEditor.Text, GroupEditor.Text); EditPanel.Visibility = Visibility.Collapsed; _editing = null;
    }
    private void OnCancelEdit(object sender, RoutedEventArgs e) { EditPanel.Visibility = Visibility.Collapsed; _editing = null; }
    private void OnCopy(object sender, RoutedEventArgs e) => Session.Copy(Selected());
    private void OnCopyText(object sender, RoutedEventArgs e) => Session.Copy(Selected(), true);
    private void OnPin(object sender, RoutedEventArgs e) => Session.TogglePin(Selected());
    private void OnRemove(object sender, RoutedEventArgs e) { if (_filter != "Trash") Session.Remove(Selected()); }
    private void OnClear(object sender, RoutedEventArgs e)
    {
        var saved = Session.State.Panels.SelectMany(p => p.ClipIds).ToHashSet();
        Session.Remove(Session.State.Items.Where(i => !i.IsPinned && !saved.Contains(i.Id)).ToList());
    }
    private void OnOpen(object sender, MouseButtonEventArgs e)
    {
        if (IsSelector(e.OriginalSource as DependencyObject)) return;
        if (Container(e.OriginalSource as DependencyObject) is not { DataContext: ItemRow row }) return;
        var item = row.Item;
        try
        {
            if (item.Kind is ItemKind.Text or ItemKind.Clipboard || MediaFiles.IsVideo(item.FilePath)) { new PreviewWindow(item) { Owner = this }.Show(); return; }
            var target = item.FilePath ?? item.Content.Trim();
            if (item.Kind == ItemKind.Link && (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))) return;
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) { Session.Error("Open", ex); }
    }
    private void OnThumbnailContext(object sender, DependencyPropertyChangedEventArgs e) { if (sender is Image { IsLoaded: true }) OnThumbnailLoaded(sender, new RoutedEventArgs()); }
    private async void OnThumbnailLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image { DataContext: ItemRow availability }) _ = availability.CheckAvailability();
        if (sender is Image cleared) cleared.Source = null;
        if (sender is Image image && image.DataContext is ItemRow { Item.FilePath: string path } row && (row.Item.Kind == ItemKind.Image || row.IsVideo))
        {
            image.Source = null; var thumbnail = await _thumbnails.Get(path);
            if (image.DataContext is ItemRow current && current.Item.FilePath == path) image.Source = thumbnail;
        }
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (_editing is not null && e.Key is Key.Enter or Key.Escape)
        { if (e.Key == Key.Enter) OnSaveEdit(this, e); else OnCancelEdit(this, e); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.V && Keyboard.FocusedElement is not TextBox) { Collect(() => Session.SaveClipboard(false)); e.Handled = true; }
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
        else if (e.Key == Key.F2 && Keyboard.FocusedElement is not TextBox) { OnEdit(this, e); e.Handled = true; }
        else if (e.Key == Key.Enter && Keyboard.FocusedElement is not TextBox) { if (_filter == "Trash") Session.RestoreTrash(Selected()); else Session.Paste(Selected()); e.Handled = true; }
        else if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox) { OnRemove(this, e); e.Handled = true; }
        else if (e.Key == Key.Escape) { if (SearchBox.Text.Length > 0) SearchBox.Clear(); else Hide(); e.Handled = true; }
        else if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBox && Selected().Count == 1)
        { new PreviewWindow(Selected()[0]) { Owner = this }.Show(); e.Handled = true; }
    }
    private void OnSettings(object sender, RoutedEventArgs e) => OpenSettings();
    private void OnToggleTop(object sender, RoutedEventArgs e) { Session.State.Settings.AlwaysOnTop = !Session.State.Settings.AlwaysOnTop; Session.Change(); }
    private void OnHide(object sender, RoutedEventArgs e) => Hide();
    private void OnQuit(object sender, RoutedEventArgs e) => Session.RequestQuit();
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (Application.Current.Dispatcher.HasShutdownStarted || (Application.Current as App)?.IsQuitting == true) return;
        e.Cancel = true; Hide();
        if (!Session.State.Settings.CloseToTray) Session.RequestQuit();
    }
}
