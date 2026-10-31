using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

public partial class DownloadsWindow : Window
{
    public sealed class Row
    {
        public DownloadFile File { get; }
        public ItemRow RowItem { get; }
        public string Time => File.AddedUtc.ToLocalTime().ToString(File.AddedUtc.ToLocalTime().Date == DateTime.Today ? "HH:mm" : "d MMM", CultureInfo.InvariantCulture);
        public string Details => File.Path + "\n" + (File.Length >= 1048576 ? (File.Length / 1048576d).ToString("0.#") + " MB" : File.Length >= 1024 ? (File.Length / 1024d).ToString("0.#") + " KB" : File.Length + " B") + " · " + File.AddedUtc.ToLocalTime().ToString("g");
        public Row(DownloadFile file)
        {
            File = file;
            RowItem = new(new ShelfItem { Title = file.Name, FilePath = file.Path,
                Kind = MediaFiles.IsImage(file.Path) ? ItemKind.Image : ItemKind.File });
        }
    }
    private readonly ShelfSession _session;
    private readonly DownloadsFeed _feed;
    private readonly Action _settings, _close;
    private readonly ObservableCollection<Row> _rows = new();
    private readonly ClipSelection _selection = new();
    private readonly DockPresence _presence;
    private FlyoutDismiss? _outside;
    private bool _disposing, _busy, _moving, _refreshing, _immediateDismiss;
    private Point? _origin;
    private FrameworkElement? _source;
    private Row? _pressedRow;
    private int _pressClicks;
    private ModifierKeys _pressModifiers;
    private ShelfItem[]? _dragItems;
    public ListBox DownloadList => Files;
    public Button StackButton => Capsule;
    public string SummaryText => Summary.Text;
    public bool IsCompact { get; private set; }
    public bool IsMoving => _moving;
    public event Action? MoveCompleted;
    public bool HasDismissObserver => _outside?.IsObserving == true;
    public DockAssignment? Assignment { get; set; }
    public event Action? GeometryCommitted;
    public DownloadsWindow(ShelfSession session, DownloadsFeed feed, Action settings, Action close)
    {
        _session = session; _feed = feed; _settings = settings; _close = close;
        InitializeComponent(); Files.ItemsSource = _rows;
        _presence = new DockPresence(this, () => session.State.Settings, compact => {
            IsCompact = compact; Details.IsHitTestVisible = !compact;
            HoverBridge.Visibility = compact ? Visibility.Hidden : Visibility.Visible;
            if (compact)
            {
                _selection.Clear(); SyncSelection();
                Motion.Conceal(Details, () => { if (IsCompact) Details.Visibility = Visibility.Hidden; }, _immediateDismiss);
            }
            else { Motion.Disclose(Details, new Point(1, 1)); Details.Visibility = Visibility.Visible; }
            WatchDismiss();
        }, transient: true);
        PopupDismiss.Attach(ContextMenu, this); ContextMenuOpening += (_, _) => Menu();
        SourceInitialized += (_, _) => NativePastePlatform.MakeNonActivating(this);
        _ = new WindowDrag(this, () => { _moving = true; Presence(); }, () => {
            try { GeometryCommitted?.Invoke(); }
            finally { _moving = false; Presence(); MoveCompleted?.Invoke(); }
        }, _ => true);
        MouseEnter += (_, _) => _feed.RequestRefresh();
        IsVisibleChanged += (_, _) => WatchDismiss();
        LostMouseCapture += (_, _) => { if (!_busy && _origin.HasValue && _source?.IsMouseCaptured != true) ClearGesture(); };
        SizeChanged += (_, _) => FitDetails();
        Closing += (_, e) => { if (!_disposing) { e.Cancel = true; Dispatcher.BeginInvoke(_close); } };
        _session.Changed += AppearanceChanged; _feed.Changed += Refresh;
        AppearanceChanged(); Refresh(); Menu();
    }
    private void WatchDismiss()
    {
        if (!_disposing && IsVisible && !IsCompact)
            _outside ??= new FlyoutDismiss(this, ContainsPointer, () => _busy || _moving || _origin.HasValue || PopupDismiss.IsOpenFor(this), Dismiss);
        else { _outside?.Dispose(); _outside = null; }
    }
    private bool ContainsPointer(Point screen)
    {
        bool Hit(FrameworkElement element) => element.IsVisible && element.ActualWidth > 0 && element.ActualHeight > 0 &&
            new Rect(element.PointToScreen(new Point()), element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight))).Contains(screen);
        return Hit(Details) || Hit(CapsuleShell) || Hit(HoverBridge);
    }
    private void AppearanceChanged()
    {
        if (_disposing) return;
        Appearance.Apply(_session.State.Settings, this, true);
        var size = SurfaceSizing.Downloads(_session.State.Settings);
        Resources["DownloadsRowHeight"] = size.RowHeight;
        Resources["DownloadsIconSize"] = size.IconSize;
        Resources["DownloadsCapsuleHeight"] = new GridLength(size.CapsuleHeight);
        Resources["DownloadsPreviewSize"] = Math.Min(30, size.RowHeight - 10);
        Resources["DownloadsPreviewSlot"] = new GridLength(Math.Min(30, size.RowHeight - 10) + 7);
        Resources["DownloadsGlyphSize"] = Math.Round(size.IconSize * .52);
        Topmost = _session.State.Settings.AlwaysOnTop; _presence.Refresh(); FitDetails();
    }
    private void Presence() => _presence.Busy(_busy || _moving || _origin.HasValue);
    public void ShowStack() { Show(); WindowState = WindowState.Normal; _presence.Reveal(); _feed.RequestRefresh(); }
    public void HideStack() { PopupDismiss.CloseFor(this); ClearGesture(); Hide(); }
    private void Dismiss()
    {
        if (_busy || _moving) return;
        PopupDismiss.CloseFor(this); ClearGesture(); _immediateDismiss = true;
        try { _presence.Dismiss(); Motion.Conceal(Details, () => Details.Visibility = Visibility.Hidden, true); }
        finally { _immediateDismiss = false; }
    }
    private void OnDismiss(object sender, RoutedEventArgs e) => Dismiss();
    private void Refresh()
    {
        if (_disposing || _busy || _origin.HasValue) return; // Never mutate an OLE source or a pressed row.
        var snapshot = _feed.Snapshot; var desired = snapshot.Files;
        _refreshing = true;
        try
        {
            var paths = desired.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var i = _rows.Count - 1; i >= 0; i--) if (!paths.Contains(_rows[i].File.Path)) _rows.RemoveAt(i);
            for (var i = 0; i < desired.Length; i++)
            {
                var row = _rows.FirstOrDefault(r => string.Equals(r.File.Path, desired[i].Path, StringComparison.OrdinalIgnoreCase));
                if (row is not null && row.File != desired[i]) { _rows.Remove(row); row = null; }
                if (row is null) _rows.Insert(i, new Row(desired[i]));
                else if (_rows.IndexOf(row) != i) _rows.Move(_rows.IndexOf(row), i);
            }
            _selection.Trim(_rows.Select(r => r.File.Path)); SyncSelection();
        }
        finally { _refreshing = false; }
        Summary.Text = snapshot.Notice ?? (_rows.Count == 0 ? (_feed.IsChrome ? "No ready Chrome downloads · open the companion to sync" : "No ready files yet") : "Drag any row · Ctrl/Shift for a group · right-click for actions");
        Notice.Visibility = snapshot.Notice is not null || _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SourceButton.Content = _feed.IsChrome ? "Connect Chrome" : "Open folder";
        SourceButton.Visibility = Notice.Visibility == Visibility.Visible && (snapshot.Notice is not null || _feed.IsChrome) ? Visibility.Visible : Visibility.Collapsed;
        UpdateCount(); FitDetails();
    }
    private ShelfItem[] Selected() => _rows.Where(r => _selection.Ids.Contains(r.File.Path)).Select(r => r.RowItem.Item).ToArray();
    private void UpdateCount() => Count.Text = _selection.Ids.Count > 1 ? $"{_selection.Ids.Count} selected" : $"{_rows.Count} files";
    private void SyncSelection()
    {
        var refreshing = _refreshing; _refreshing = true;
        try
        {
            foreach (var row in Files.SelectedItems.Cast<Row>().ToArray()) if (!_selection.Ids.Contains(row.File.Path)) Files.SelectedItems.Remove(row);
            foreach (var row in _rows) if (_selection.Ids.Contains(row.File.Path) && !Files.SelectedItems.Contains(row)) Files.SelectedItems.Add(row);
        }
        finally { _refreshing = refreshing; }
        UpdateCount();
    }
    private void FitDetails()
    {
        var notice = Notice.Visibility == Visibility.Visible ? (SourceButton.Visibility == Visibility.Visible ? 86 : 44) : 0;
        var size = SurfaceSizing.Downloads(_session.State.Settings);
        Details.Height = Math.Max(80, Math.Min(58 + Math.Max(1, Math.Min(size.Rows, _rows.Count)) * size.RowHeight + notice, Math.Max(80, Height - size.IconSize - 33)));
    }
    private void OnSelection(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing) return;
        // Keyboard navigation remains a normal ListBox interaction.
        _selection.Clear();
        foreach (var row in Files.SelectedItems.Cast<Row>()) _selection.Click(_rows.Select(r => r.File.Path).ToArray(), row.File.Path, true, false, true);
        UpdateCount();
    }
    private void OnCapsule(object sender, RoutedEventArgs e) => _presence.Reveal();
    private void OnSource(object sender, RoutedEventArgs e) => Run(() => {
        Dismiss();
        if (_feed.IsChrome) { ChromeConnection.Connect(); return; }
        if (!Path.IsPathFullyQualified(_feed.Folder) || !Directory.Exists(_feed.Folder)) throw new DirectoryNotFoundException();
        Process.Start(new ProcessStartInfo(_feed.Folder) { UseShellExecute = true });
    });
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { _session.Error("Downloads", ex); _feed.RequestRefresh(); }
    }
    private void Copy(ShelfItem[] items) { if (items.Length > 0 && _session.Copy(items)) Dismiss(); }
    private void Save(ShelfItem[] items)
    {
        if (items.Length == 0) return;
        Run(() => {
            foreach (var item in items) if (!File.Exists(item.FilePath)) throw new FileNotFoundException();
            var added = _session.Add(items, pin: false);
            if (_session.TrySave()) { _session.Notify(added > 0 ? $"Saved {added} to Library" : "Already in Library"); Dismiss(); }
        });
    }
    private void Open(ShelfItem item) => Run(() => { if (!File.Exists(item.FilePath)) throw new FileNotFoundException(); Dismiss(); Process.Start(new ProcessStartInfo(item.FilePath!) { UseShellExecute = true }); });
    private void OnRowDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject original || ItemsControl.ContainerFromElement(Files, original) is not ListBoxItem { DataContext: Row row } container) return;
        e.Handled = true;
        _pressModifiers = Keyboard.Modifiers; _pressedRow = row; _pressClicks = e.ClickCount;
        if (_pressModifiers.HasFlag(ModifierKeys.Control) || _pressModifiers.HasFlag(ModifierKeys.Shift))
        {
            _selection.Click(_rows.Select(r => r.File.Path).ToArray(), row.File.Path, _pressModifiers.HasFlag(ModifierKeys.Control), _pressModifiers.HasFlag(ModifierKeys.Shift), false);
            SyncSelection();
        }
        _origin = e.GetPosition(this); _source = container;
        _dragItems = _selection.Ids.Contains(row.File.Path) ? Selected() : new[] { row.RowItem.Item };
        if (!_source.CaptureMouse()) { ClearGesture(); return; }
        Presence();
    }
    private void ClearGesture()
    {
        var source = _source; _origin = null; _source = null; _pressedRow = null; _dragItems = null; _pressClicks = 0;
        source?.ReleaseMouseCapture(); Presence();
    }
    private void OnRowUp(object sender, MouseButtonEventArgs e)
    {
        var open = _origin.HasValue && _pressClicks == 2 && _pressModifiers == ModifierKeys.None ? _pressedRow?.RowItem.Item : null;
        if (_origin.HasValue && _pressedRow is Row row && _pressModifiers == ModifierKeys.None) { _selection.Single(row.File.Path); SyncSelection(); }
        ClearGesture();
        if (open is not null) Open(open); else Refresh();
    }
    private void OnRowMove(object sender, MouseEventArgs e)
    {
        if (_origin is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var clips = _dragItems ?? Array.Empty<ShelfItem>();
        _busy = true; ClearGesture(); Presence();
        try
        {
            if (clips.Length > 0) DragDrop.DoDragDrop(Files, _session.Transfer.BuildDrag(clips, internalSource: false), DragDropEffects.Copy);
        }
        catch (Exception ex) { _session.Error("Downloads drag", ex); _feed.RequestRefresh(); }
        finally { _busy = false; Presence(); Refresh(); Dismiss(); }
        e.Handled = true;
    }
    private void Menu()
    {
        var menu = ContextMenu; menu.Items.Clear(); Appearance.Apply(_session.State.Settings, menu, true);
        void Add(string name, Action action)
        { var item = new MenuItem { Header = name, FontSize = 11.5, Padding = new Thickness(6, 3, 6, 3) }; item.Click += (_, _) => { menu.IsOpen = false; Dispatcher.BeginInvoke(action); }; menu.Items.Add(item); }
        Add(_feed.IsChrome ? "Connect Chrome" : "Open folder", () => OnSource(this, new()));
        Add("Refresh", _feed.RequestRefresh);
        Add("Select all", () => { foreach (var row in _rows) if (!_selection.Ids.Contains(row.File.Path)) _selection.Click(_rows.Select(r => r.File.Path).ToArray(), row.File.Path, true, false, true); SyncSelection(); });
        Add("Settings", () => { Dismiss(); _settings(); }); Add("Close downloads stack", _close);
    }
    private void OnRowMenu(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || ItemsControl.ContainerFromElement(Files, source) is not ListBoxItem { DataContext: Row row }) return;
        e.Handled = true; ClearGesture();
        if (!_selection.Ids.Contains(row.File.Path)) { _selection.Single(row.File.Path); SyncSelection(); }
        var clips = Selected(); var menu = new ContextMenu(); Appearance.Apply(_session.State.Settings, menu, true);
        void Add(string label, Action action) { var item = new MenuItem { Header = label, FontSize = 11.5, Padding = new Thickness(6, 3, 6, 3) }; item.Click += (_, _) => { menu.IsOpen = false; action(); }; menu.Items.Add(item); }
        if (clips.Length == 1) Add("Open", () => Open(clips[0]));
        Add(clips.Length > 1 ? $"Copy {clips.Length}" : "Copy", () => Copy(clips)); Add("Save to Library", () => Save(clips));
        if (clips.Length == 1) Add("Show in Explorer", () => Run(() => { Dismiss(); Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", clips[0].FilePath! } }); }));
        Files.ContextMenu = menu; PopupDismiss.Attach(menu, this); menu.PlacementTarget = Files; menu.IsOpen = true;
    }
    private async void OnThumbnail(object sender, RoutedEventArgs e)
    {
        if (sender is not Image image) return; image.Source = null;
        if (image.DataContext is not Row row || row.RowItem.ImageVisible != Visibility.Visible) return;
        var bitmap = await ThumbnailCache.Shared.Get(row.File.Path);
        if (image.DataContext is Row current && current.File == row.File) image.Source = bitmap;
    }
    private void OnThumbnailContext(object sender, DependencyPropertyChangedEventArgs e) { if (sender is Image { IsLoaded: true }) OnThumbnail(sender, new()); }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; }
        else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Copy(Selected()); e.Handled = true; }
        else if (e.Key == Key.Enter && Selected() is { Length: 1 } items) { Open(items[0]); e.Handled = true; }
    }
    public void DisposeView()
    {
        if (_disposing) return; _disposing = true; PopupDismiss.CloseFor(this); ClearGesture(); _outside?.Dispose(); _outside = null;
        _session.Changed -= AppearanceChanged; _feed.Changed -= Refresh; _presence.Dispose(); Close();
    }
}
