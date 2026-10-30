using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClipDrop.Core;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

namespace ClipDrop.Windows;

public partial class DockWindow : Window
{
    public ClipDrop.Core.DockAssignment? Assignment { get; set; }
    public string? PanelId { get; }
    public Action<DockWindow>? CloseWindowRequested { get; set; }
    public Action<IReadOnlyList<ShelfItem>>? OpenPanelsRequested { get; set; }
    public Action? OpenDownloadsRequested { get; set; }
    public event Action<DockWindow>? GeometryCommitted;
    private readonly bool _managed;
    private readonly DockPresence _presence;
    private bool _transferring, _dropHover, _moving;
    private bool _disposing;
    public ShelfSession Session { get; }
    public Button QuickSave => Strip.SaveButton;
    public ItemsControl Pins => Strip.PinnedItems;
    private Border DockBody => Strip.Surface;
    private readonly Action _library;
    private readonly Action _settings;
    private readonly Action<ClipDrop.Core.ShelfItem>? _edit;
    private readonly ThumbnailCache _thumbnails = ThumbnailCache.Shared;
    private readonly HashSet<string> _knownPins = new();
    private readonly HashSet<string> _arrivingPins = new();
    private readonly ClipSelection _selection = new();
    private bool _selectMode;
    private string _panelId = "";
    private ModifierKeys _clickModifiers;
    private Point? _origin;
    private Button? _dragButton;
    private bool _suppressClick;
    private readonly ToolTip _feedback = new() { Placement = PlacementMode.Top, StaysOpen = true };
    private readonly DispatcherTimer _feedbackTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    public DockWindow(ShelfSession session, Action library, Action settings, Action<ClipDrop.Core.ShelfItem>? edit = null, bool managed = false, string? panelId = null)
    {
        _managed = managed; PanelId = panelId; Session = session; _library = library; _settings = settings; _edit = edit;
        InitializeComponent();
        PopupDismiss.Attach(ContextMenu, this);
        ContextMenuOpening += (_, _) => UpdateMenu();
        _presence = new DockPresence(this, () => Session.State.Settings, compact => Strip.SetCompact(compact));
        Strip.KeepRequested += (sender, _) => { if (sender is Button { DataContext: ItemRow row }) Session.ToggleKeep(row.Members); };
        Strip.DoneRequested += (_, _) => { _selectMode = false; _selection.Clear(); Refresh(); };
        Strip.SelectionRequested += (sender, _) => {
            if (sender is FrameworkElement target && SelectedClips() is { Length: > 0 } clips)
            {
                target.ContextMenu = ClipMenu.Create(Session, clips, this, true, edit: _edit, panelId: PanelId);
                ClipMenu.Show(target.ContextMenu, target, true);
            }
        };
        Strip.SaveRequested += OnSave; Strip.LibraryRequested += OnLibrary;
        Strip.PinRequested += OnUseItem; Strip.PinLoaded += OnPinLoaded; Strip.ImageLoaded += OnThumbnailLoaded;
        Strip.PinDown += OnItemDown; Strip.PinUp += OnItemUp; Strip.PinMove += OnItemMove; Strip.PinMenu += OnPinMenu;
        _ = new WindowDrag(this, () => { _feedback.IsOpen = false; _moving = true; UpdatePresence(); }, () => {
            _moving = false; UpdatePresence();
            if (_managed) GeometryCommitted?.Invoke(this);
            else { Session.State.Settings.DockLeft = Left; Session.State.Settings.DockTop = Top; Session.Change(false); }
        });
        PreviewMouseLeftButtonDown += (_, e) => {
            if (e.ClickCount != 2 || !WindowDrag.CanStart(e.OriginalSource as DependencyObject)) return;
            e.Handled = true; _library();
        };
        SourceInitialized += (_, _) => NativePastePlatform.MakeNonActivating(this);
        Loaded += (_, _) => {
            if (_managed) return;
            var saved = Session.State.Settings;
            if (saved.DockLeft is double x && saved.DockTop is double y && double.IsFinite(x) && double.IsFinite(y)) { Left = x; Top = y; }
            else { var area = WindowPlacement.WorkArea(this); Left = area.Left + (area.Width - ActualWidth) / 2; Top = area.Bottom - ActualHeight - 24; }
            WindowPlacement.Clamp(this);
        };
        SizeChanged += (_, _) => { if (IsLoaded && !_managed) WindowPlacement.Clamp(this); };
        Closing += (_, e) => {
            if (_disposing || Dispatcher.HasShutdownStarted || (Application.Current as App)?.IsQuitting == true) return;
            e.Cancel = true;
            if (PanelId is not null && CloseWindowRequested is not null)
                Dispatcher.BeginInvoke(() => { if (!_disposing) CloseWindowRequested?.Invoke(this); }, DispatcherPriority.Background);
            else HideDock();
        };
        DragLeave += (_, e) => {
            var point = e.GetPosition(this);
            if (point.X < 0 || point.Y < 0 || point.X > ActualWidth || point.Y > ActualHeight) { _dropHover = false; UpdatePresence(); ApplySurface(false); Strip.ShowInsertion(null); }
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { _feedback.IsOpen = false; _feedbackTimer.Stop(); } };
        Session.Changed += Refresh; Session.Notification += ShowFeedback;
        _feedback.PlacementTarget = DockBody;
        _feedbackTimer.Tick += (_, _) => { _feedback.IsOpen = false; _feedbackTimer.Stop(); };
        Refresh(); UpdateMenu();
    }
    public bool IsShown => IsVisible && WindowState != WindowState.Minimized;
    private void UpdatePresence() => _presence.Busy(_transferring || _dropHover || _moving || _selectMode || _selection.Ids.Count > 0);
    public void ShowDock()
    {
        if (_disposing) return;
        Session.Paster?.CaptureTarget(); PopupDismiss.CloseFor(this);
        var opening = !IsShown; WindowState = WindowState.Normal; Show(); WindowPlacement.Clamp(this);
        _presence.Reveal(); if (opening) Motion.Reveal(DockBody);
    }
    public void HideDock() { PopupDismiss.CloseFor(this); _feedback.IsOpen = false; Hide(); }
    public void Toggle() { if (IsShown) HideDock(); else ShowDock(); }
    public void RefreshDock() => Refresh();
    public void DisposeView()
    {
        _disposing = true; PopupDismiss.CloseFor(this); _presence.Dispose(); Session.Changed -= Refresh; Session.Notification -= ShowFeedback;
        _feedbackTimer.Stop(); _feedback.IsOpen = false; Close();
    }
    private void Refresh()
    {
        // A view can be closed while Session.Changed is iterating its delegate snapshot.
        if (_disposing) return;
        Appearance.Apply(Session.State.Settings, this, true);
        var settings = Session.State.Settings; Strip.Apply(settings); _presence.Refresh();
        if (_panelId != (PanelId ?? Session.State.ActivePanelId)) { _panelId = PanelId ?? Session.State.ActivePanelId; _selection.Clear(); _selectMode = false; }
        var name = Session.PanelName(PanelId); Strip.SetPanelName(name, PanelId is not null);
        Title = PanelId is null ? "ClipDrop Dock" : "ClipDrop · " + name;
        Topmost = settings.AlwaysOnTop;
        FontSize = Appearance.Bound(settings.FontSize, 11, 18, 13);
        var pins = Session.DockTiles(PanelId);
        var available = Assignment is { } a ? a.Display.Width / WindowPlacement.Scale(this) : IsLoaded ? WindowPlacement.WorkArea(this).Width : SystemParameters.WorkArea.Width;
        var size = Appearance.Bound(settings.DockButtonSize, 30, 48, 36) + Appearance.Bound(settings.DockSpacing, 0, 12, 4);
        _selection.Trim(pins.Select(i => i.Key));
        var fixedWidth = 64 + size + (PanelId is not null ? 100 : 0) + (_selection.Ids.Count > 0 ? 125 : _selectMode ? 50 : 0);
        var slots = Math.Max(1, (int)((available - fixedWidth) / size));
        if (pins.Length > slots) slots--;
        var capacity = Math.Max(1, Math.Min(12, slots));
        _arrivingPins.Clear();
        foreach (var pin in pins) if (!_knownPins.Contains(pin.Key)) _arrivingPins.Add(pin.Key);
        _knownPins.Clear(); foreach (var pin in pins) _knownPins.Add(pin.Key);
        Strip.SetTiles(pins, capacity);
        Strip.SetSelection(_selection.Ids, _selectMode);
        UpdatePresence(); ApplySurface(false);
    }
    private void ApplySurface(bool dropping)
    {
        var key = dropping ? "Selected" : "Surface";
        var color = ((SolidColorBrush)Strip.FindResource(key)).Color;
        color.A = dropping ? (byte)255 : (byte)Math.Round(255 * Appearance.Bound(Session.State.Settings.DockOpacity, 0, 100, 100) / 100);
        if (DockBody.Background is not SolidColorBrush current || current.Color != color)
        { var brush = new SolidColorBrush(color); brush.Freeze(); DockBody.Background = brush; }
        var outline = (Brush)Strip.FindResource(dropping ? "Ink" : "DockOutline");
        if (!ReferenceEquals(DockBody.BorderBrush, outline)) DockBody.BorderBrush = outline;
    }
    private void ShowFeedback(string message)
    {
        if (!IsVisible || !(IsMouseOver || _transferring || _dropHover || _moving || PopupDismiss.IsOpenFor(this))) return;
        _feedback.Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 };
        _feedback.IsOpen = false; _feedback.IsOpen = true; _feedbackTimer.Stop(); _feedbackTimer.Start();
    }
    private void OnSave(object sender, RoutedEventArgs e) => Session.SaveClipboard(true, PanelId);
    private void OnLibrary(object sender, RoutedEventArgs e) => _library();
    private void UpdateMenu()
    {
        var menu = ContextMenu; menu.Items.Clear(); menu.FontSize = 11.5; menu.MaxWidth = 280;
        Appearance.Apply(Session.State.Settings, menu, true);
        var selected = SelectedClips();
        void Add(string title, string id, Action action, bool enabled = true)
        {
            var item = new MenuItem { Header = title, Tag = id, IsEnabled = enabled, Padding = new Thickness(6, 3, 6, 3), FontSize = 11.5 };
            item.Click += (_, _) => { menu.IsOpen = false; Dispatcher.BeginInvoke(action, DispatcherPriority.Background); };
            menu.Items.Add(item);
        }
        Add("Library", "Library", _library);
        if (OpenDownloadsRequested is not null) Add("Downloads", "Downloads", OpenDownloadsRequested);
        Add("Saved panels", "Panels", () => { if (OpenPanelsRequested is not null) OpenPanelsRequested(selected); else Session.ShowPanels(selected); });
        Add(_selectMode ? "Finish selection" : "Select clips", "Select", () => {
            _selectMode = !_selectMode; if (!_selectMode) _selection.Clear(); Refresh();
        }, Pins.Items.Count > 0 || _selectMode);
        Add("Save clipboard to Library", "SaveLibrary", () => Session.SaveClipboard(false));
        if (Session.State.Trash.Count > 0) Add("Undo removal", "Undo", Session.UndoRemove);
        menu.Items.Add(new Separator());
        Add("Settings", "Settings", _settings);
        Add("Hide dock", "Hide", HideDock);
        if (PanelId is not null && CloseWindowRequested is not null) Add("Close window", "CloseWindow", () => CloseWindowRequested(this));
    }
    private void OnUseItem(object sender, RoutedEventArgs e)
    {
        if (_suppressClick) { _suppressClick = false; return; }
        if (sender is not Button { DataContext: ItemRow row }) return;
        var modifiers = _clickModifiers; _clickModifiers = ModifierKeys.None;
        if (_selectMode || (modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0)
        {
            _selection.Click(PinIds(), row.Key, modifiers.HasFlag(ModifierKeys.Control), modifiers.HasFlag(ModifierKeys.Shift), _selectMode);
            Refresh(); return;
        }
        if (row.IsStack) OpenStack(row, (Button)sender); else Session.Paste(row.Members);
    }
    private void OnPinMenu(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { DataContext: ItemRow row } button) return;
        e.Handled = true; _origin = null; _dragButton = null; _feedback.IsOpen = false;
        Session.Paster?.CaptureTarget();
        if (_selection.Ids.Count > 0 && !_selection.Ids.Contains(row.Key)) { _selection.Single(row.Key); Refresh(); }
        var targets = _selection.Ids.Contains(row.Key) ? SelectedClips() : row.Members;
        var scope = PanelId ?? Session.State.ActivePanelId;
        button.ContextMenu = ClipMenu.Create(Session, targets, this, true, edit: _edit, panelId: PanelId,
            splitStack: row.StackId is string id ? () => Session.SplitStack(id, scope) : null);
        ClipMenu.Show(button.ContextMenu, button);
    }
    private void OnPinLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ItemRow row } button && _arrivingPins.Remove(row.Key)) Motion.Arrive(button);
    }
    private void OnItemDown(object sender, MouseButtonEventArgs e)
    { _origin = e.GetPosition(this); _dragButton = sender as Button; _suppressClick = false; _clickModifiers = Keyboard.Modifiers; }
    private void OnItemUp(object sender, MouseButtonEventArgs e) { _origin = null; _dragButton = null; }
    private void OnItemMove(object sender, MouseEventArgs e)
    {
        if (_origin is not Point start || _dragButton?.DataContext is not ItemRow row || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(this);
        if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var implicitSelection = _selection.Ids.Count == 0 && !_selectMode && _clickModifiers == ModifierKeys.None;
        var button = _dragButton; _origin = null; _dragButton = null; _suppressClick = true;
        // Release the button's capture so ending an OLE drag cannot also invoke click-paste.
        button.ReleaseMouseCapture();
        try
        {
            if (!_selection.Ids.Contains(row.Key))
            {
                if ((_clickModifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0)
                    _selection.Click(PinIds(), row.Key, _clickModifiers.HasFlag(ModifierKeys.Control), _clickModifiers.HasFlag(ModifierKeys.Shift), false);
                else _selection.Single(row.Key);
                Strip.SetSelection(_selection.Ids, _selectMode);
            }
            _transferring = true; UpdatePresence();
            using var transfer = Session.BeginDrag(SelectedClips());
            var result = DragDrop.DoDragDrop(button, transfer.Data, DragDropEffects.Copy);
            Session.CompleteDrag(transfer, result);
        }
        catch (Exception ex) { Session.Error("Drag", ex); }
        finally { _transferring = false; _dropHover = false; UpdatePresence(); ApplySurface(false); Strip.ShowInsertion(null); if (implicitSelection) { _selection.Clear(); Refresh(); } }
    }
    private string[] PinIds() => Session.DockTiles(PanelId).Select(i => i.Key).ToArray();
    private ShelfItem[] SelectedClips() => Session.DockTiles(PanelId).Where(i => _selection.Ids.Contains(i.Key)).SelectMany(i => i.Items).ToArray();
    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (Session.OwnDrag(e.Data) is not null)
        {
            _dropHover = true; UpdatePresence(); Strip.UpdateLayout();
            var index = DropIndex(e);
            _dropHover = index is not null; UpdatePresence();
            var center = Strip.CenterAt(e.GetPosition(Strip.PinnedItems));
            if (center is not null) Strip.ShowStackTarget(center); else Strip.ShowInsertion(index);
            ApplySurface(index is not null && Strip.PinnedItems.Items.Count == 0); e.Effects = index is not null ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else
        {
            var supported = TransferService.Supports(e.Data); _dropHover = supported; UpdatePresence(); Strip.UpdateLayout();
            var center = supported ? Strip.CenterAt(e.GetPosition(Strip.PinnedItems)) : null;
            if (center is not null) Strip.ShowStackTarget(center); else Strip.ShowInsertion(supported ? DropIndex(e) : null);
            ApplySurface(supported && center is null); e.Effects = supported ? DragDropEffects.Copy : DragDropEffects.None;
        }
        e.Handled = true;
    }
    private int? DropIndex(DragEventArgs e)
    {
        var point = e.GetPosition(QuickSave);
        if (point.X >= 0 && point.Y >= 0 && point.X <= QuickSave.ActualWidth && point.Y <= QuickSave.ActualHeight)
            return Pins.Items.Count;
        return Strip.InsertionAt(e.GetPosition(Strip.PinnedItems));
    }
    private void OnDrop(object sender, DragEventArgs e)
    {
        _dropHover = false; UpdatePresence(); ApplySurface(false); Strip.ShowInsertion(null); e.Handled = true; e.Effects = DragDropEffects.None;
        if (Session.OwnDrag(e.Data) is { } own)
        {
            own.InternalHandled = true;
            if (DropIndex(e) is int index)
            {
                var center = Strip.CenterAt(e.GetPosition(Strip.PinnedItems));
                if (center is not null) Session.Stack(own.Items, center.Item.Id, PanelId);
                else Session.Reorder(own.Items, Strip.PinIndex(index), PanelId);
                e.Effects = DragDropEffects.Copy;
            }
            return;
        }
        if (!TransferService.Supports(e.Data)) return;
        try
        {
            var center = Strip.CenterAt(e.GetPosition(Strip.PinnedItems)); var index = DropIndex(e);
            if (Session.Capture(e.Data, true, PanelId, index is int slot ? Strip.PinIndex(slot) : null, center?.Item.Id)) e.Effects = DragDropEffects.Copy;
        }
        catch (Exception ex) { Session.Error("Drop", ex); }
    }
    private async void OnThumbnailLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image cleared) cleared.Source = null;
        if (sender is Image image && image.DataContext is ItemRow { Item.FilePath: string path } row && (row.IsImage || row.IsVideo))
        { image.Source = null; var bitmap = await _thumbnails.Get(path); if (image.DataContext is ItemRow current && current.Item.FilePath == path) image.Source = bitmap; }
    }
}
