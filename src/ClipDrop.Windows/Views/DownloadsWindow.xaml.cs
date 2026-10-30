using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace ClipDrop.Windows.Views;

public partial class DownloadsWindow : Window
{
    public sealed class Row
    {
        public DownloadFile File { get; }
        public ItemRow RowItem { get; }
        public string SelectName => "Select file: " + File.Name;
        public string Description => (File.Length >= 1024 * 1024 ? (File.Length / 1048576d).ToString("0.#") + " MB" : File.Length >= 1024 ? (File.Length / 1024d).ToString("0.#") + " KB" : File.Length + " B") + " · " + File.AddedUtc.ToLocalTime().ToString("g");
        public Row(DownloadFile file)
        {
            File = file;
            // The catalog already validated the path off the dispatcher. This
            // lightweight reference is revalidated at the explicit transfer.
            RowItem = new(new ShelfItem { Title = file.Name, FilePath = file.Path,
                Kind = MediaFiles.IsImage(file.Path) ? ItemKind.Image : ItemKind.File });
        }
    }
    private readonly ShelfSession _session;
    private readonly DownloadsFeed _feed;
    private readonly Action _settings, _close;
    private readonly ObservableCollection<Row> _rows = new();
    private readonly DockPresence _presence;
    private bool _disposing, _busy, _moving, _latched, _refreshing;
    private Point? _origin;
    private FrameworkElement? _source;
    public ListBox DownloadList => Files;
    public Button StackButton => Capsule;
    public Button DragButton => DragSelection;
    public string SummaryText => Summary.Text;
    public bool IsCompact { get; private set; }
    public DockAssignment? Assignment { get; set; }
    public event Action? GeometryCommitted;
    public DownloadsWindow(ShelfSession session, DownloadsFeed feed, Action settings, Action close)
    {
        _session = session; _feed = feed; _settings = settings; _close = close;
        InitializeComponent(); Files.ItemsSource = _rows;
        _presence = new DockPresence(this, () => session.State.Settings, compact => {
            IsCompact = compact; Details.Visibility = compact ? Visibility.Hidden : Visibility.Visible;
            HoverBridge.Visibility = compact ? Visibility.Hidden : Visibility.Visible;
            if (!compact) Motion.Reveal(Details);
        });
        PopupDismiss.Attach(ContextMenu, this); ContextMenuOpening += (_, _) => Menu();
        SourceInitialized += (_, _) => NativePastePlatform.MakeNonActivating(this);
        _ = new WindowDrag(this, () => { _moving = true; Presence(); }, () => { _moving = false; Presence(); GeometryCommitted?.Invoke(); });
        MouseEnter += (_, _) => _feed.RequestRefresh();
        SizeChanged += (_, _) => FitDetails();
        Closing += (_, e) => { if (!_disposing) { e.Cancel = true; Dispatcher.BeginInvoke(_close); } };
        _session.Changed += AppearanceChanged; _feed.Changed += Refresh;
        AppearanceChanged(); Refresh(); Menu();
    }
    private void AppearanceChanged()
    {
        if (_disposing) return;
        Appearance.Apply(_session.State.Settings, this, true);
        Topmost = _session.State.Settings.AlwaysOnTop; _presence.Refresh();
    }
    private void Presence() => _presence.Busy(_busy || _moving || _latched);
    public void ShowStack() { Show(); WindowState = WindowState.Normal; _presence.Reveal(); _feed.RequestRefresh(); }
    public void HideStack() { PopupDismiss.CloseFor(this); Latch(false); Hide(); }
    private void Refresh()
    {
        if (_disposing) return;
        if (_busy) return; // Keep an OLE source and its captured selection stable.
        var selected = Files.SelectedItems.Cast<Row>().Select(r => r.File.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            Files.SelectedItems.Clear();
            foreach (var row in _rows) { if (selected.Contains(row.File.Path)) Files.SelectedItems.Add(row); }
        }
        finally { _refreshing = false; }
        Summary.Text = snapshot.Notice ?? (_rows.Count == 0 ? "No ready files yet · new downloads appear here" : $"{_rows.Count} recent files · select and drag to any app");
        Summary.ToolTip = _feed.Folder; UpdateActions();
    }
    private ShelfItem[] Selected() => _rows.Where(r => Files.SelectedItems.Contains(r)).Select(r => r.RowItem.Item).ToArray();
    private void UpdateActions()
    {
        var count = Files.SelectedItems.Count;
        Actions.Visibility = count > 0 ? Visibility.Visible : Visibility.Hidden; DragSelection.Content = "Drag " + count;
        FitDetails();
    }
    private void FitDetails() => Details.Height = Math.Max(90, Math.Min(116 + Math.Max(1, Math.Min(5, _rows.Count)) * 50, Height - 75));
    private void OnSelection(object sender, SelectionChangedEventArgs e) { if (!_refreshing) UpdateActions(); }
    private void OnCapsule(object sender, RoutedEventArgs e) => Latch(!_latched);
    private void Latch(bool value)
    {
        _latched = value;
        Capsule.SetResourceReference(BackgroundProperty, _latched ? "Selected" : "Control");
        Capsule.ToolTip = _latched ? "Downloads · kept open · click to resume hover mode" : "Downloads · hover to reveal · click to keep open · drag the edge to move · right-click for options";
        Presence();
    }
    private void OnFolder(object sender, RoutedEventArgs e) => Run(() => {
        if (!Path.IsPathFullyQualified(_feed.Folder) || !Directory.Exists(_feed.Folder)) throw new DirectoryNotFoundException();
        Process.Start(new ProcessStartInfo(_feed.Folder) { UseShellExecute = true });
    });
    private void OnDragHint(object sender, RoutedEventArgs e) => Summary.Text = "Drag this button or a selected row to another app";
    private void Run(Action action) { try { action(); } catch (Exception ex) { _session.Error("Downloads", ex); Summary.Text = "File unavailable · refresh or open folder"; _feed.RequestRefresh(); } }
    private void OnCopy(object sender, RoutedEventArgs e) => Copy(Selected());
    private void Copy(ShelfItem[] items) { if (items.Length > 0 && _session.Copy(items)) Summary.Text = $"Copied {items.Length} · original files kept"; }
    private void OnSave(object sender, RoutedEventArgs e) => Save(Selected());
    private void Save(ShelfItem[] items)
    {
        if (items.Length == 0) return;
        Run(() => { foreach (var item in items) if (!File.Exists(item.FilePath)) throw new FileNotFoundException();
            var added = _session.Add(items, pin: false); Summary.Text = added > 0 ? $"Saved {added} to Library" : "Already in Library"; });
    }
    private void OnRowDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject original || FindCheck(original)) return;
        if (ItemsControl.ContainerFromElement(Files, original) is not ListBoxItem { DataContext: Row row }) return;
        _origin = e.GetPosition(this); _source = Files;
        // Prevent WPF from collapsing an existing group before the drag starts.
        if (Files.SelectedItems.Contains(row) && Files.SelectedItems.Count > 1 && Keyboard.Modifiers == ModifierKeys.None) e.Handled = true;
    }
    private static bool FindCheck(DependencyObject source)
    {
        while (source is not null && source is not ListBoxItem)
        { if (source is ToggleButton) return true; source = VisualTreeHelper.GetParent(source); }
        return false;
    }
    private void OnActionDown(object sender, MouseButtonEventArgs e) { _origin = e.GetPosition(this); _source = DragSelection; }
    private void OnRowUp(object sender, MouseButtonEventArgs e) { _origin = null; _source = null; }
    private void OnRowMove(object sender, MouseEventArgs e)
    {
        if (_origin is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _origin = null; _source?.ReleaseMouseCapture(); _source = null;
        var clips = Selected(); if (clips.Length == 0) return;
        _busy = true; Presence();
        try
        {
            var data = _session.Transfer.BuildDrag(clips, internalSource: false);
            DragDrop.DoDragDrop(Files, data, DragDropEffects.Copy);
        }
        catch (Exception ex) { _session.Error("Downloads drag", ex); _feed.RequestRefresh(); }
        finally { _busy = false; Presence(); Refresh(); }
        e.Handled = true;
    }
    private void Menu()
    {
        var menu = ContextMenu; menu.Items.Clear(); Appearance.Apply(_session.State.Settings, menu, true);
        void Add(string name, Action action)
        { var item = new MenuItem { Header = name, FontSize = 11.5, Padding = new Thickness(6, 3, 6, 3) }; item.Click += (_, _) => { menu.IsOpen = false; Dispatcher.BeginInvoke(action); }; menu.Items.Add(item); }
        Add("Open downloads folder", () => OnFolder(this, new())); Add("Refresh", _feed.RequestRefresh);
        Add("Settings", _settings); Add("Close downloads stack", _close);
    }
    private void OnRowMenu(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || ItemsControl.ContainerFromElement(Files, source) is not ListBoxItem { DataContext: Row row }) return;
        e.Handled = true; _origin = null;
        if (!Files.SelectedItems.Contains(row)) { Files.SelectedItems.Clear(); Files.SelectedItems.Add(row); }
        var clips = Selected(); var menu = new ContextMenu(); Appearance.Apply(_session.State.Settings, menu, true);
        void Add(string label, Action action) { var item = new MenuItem { Header = label, FontSize = 11.5, Padding = new Thickness(6, 3, 6, 3) }; item.Click += (_, _) => { menu.IsOpen = false; action(); }; menu.Items.Add(item); }
        Add("Copy", () => Copy(clips)); Add("Save to Library", () => Save(clips));
        if (clips.Length == 1) Add("Show in Explorer", () => Run(() => Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", clips[0].FilePath! } })));
        PopupDismiss.Attach(menu, this); menu.PlacementTarget = Files; menu.IsOpen = true;
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
        if (e.Key == Key.Escape) { Latch(false); Files.SelectedItems.Clear(); e.Handled = true; }
        else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Copy(Selected()); e.Handled = true; }
    }
    public void DisposeView()
    {
        if (_disposing) return; _disposing = true; PopupDismiss.CloseFor(this);
        _session.Changed -= AppearanceChanged; _feed.Changed -= Refresh; _presence.Dispose(); Close();
    }
}
