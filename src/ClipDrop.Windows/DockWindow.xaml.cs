using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

namespace ClipDrop.Windows;

public partial class DockWindow : Window
{
    public ClipDrop.Core.DockAssignment? Assignment { get; set; }
    public event Action<DockWindow>? GeometryCommitted;
    private readonly bool _managed;
    private bool _disposing;
    public ShelfSession Session { get; }
    public Button QuickSave => Strip.SaveButton;
    public ItemsControl Pins => Strip.PinnedItems;
    private Border DockBody => Strip.Body;
    private readonly Action _library;
    private readonly Action _settings;
    private readonly Action<ClipDrop.Core.ShelfItem>? _edit;
    private readonly ThumbnailCache _thumbnails = ThumbnailCache.Shared;
    private readonly HashSet<string> _knownPins = new();
    private readonly HashSet<string> _arrivingPins = new();
    private Point? _origin;
    private Button? _dragButton;
    private bool _suppressClick;
    private readonly ToolTip _feedback = new() { Placement = PlacementMode.Top, StaysOpen = true };
    private readonly DispatcherTimer _feedbackTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    public DockWindow(ShelfSession session, Action library, Action settings, Action<ClipDrop.Core.ShelfItem>? edit = null, bool managed = false)
    {
        _managed = managed; Session = session; _library = library; _settings = settings; _edit = edit;
        InitializeComponent();
        Strip.SaveRequested += OnSave; Strip.SettingsRequested += OnSettings; Strip.LibraryRequested += OnLibrary;
        Strip.PinRequested += OnUseItem; Strip.PinLoaded += OnPinLoaded; Strip.ImageLoaded += OnThumbnailLoaded;
        Strip.PinDown += OnItemDown; Strip.PinUp += OnItemUp; Strip.PinMove += OnItemMove; Strip.PinMenu += OnPinMenu;
        _ = new WindowDrag(this, () => { _feedback.IsOpen = false; }, () => {
            if (_managed) GeometryCommitted?.Invoke(this);
            else { Session.State.Settings.DockLeft = Left; Session.State.Settings.DockTop = Top; Session.Change(false); }
        });
        SourceInitialized += (_, _) => NativePastePlatform.MakeNonActivating(this);
        Loaded += (_, _) => {
            if (_managed) return;
            var saved = Session.State.Settings;
            if (saved.DockLeft is double x && saved.DockTop is double y && double.IsFinite(x) && double.IsFinite(y)) { Left = x; Top = y; }
            else { var area = WindowPlacement.WorkArea(this); Left = area.Left + (area.Width - ActualWidth) / 2; Top = area.Bottom - ActualHeight - 24; }
            WindowPlacement.Clamp(this);
        };
        SizeChanged += (_, _) => { if (IsLoaded && !_managed) WindowPlacement.Clamp(this); };
        Closing += (_, e) => { if (!_disposing && !Dispatcher.HasShutdownStarted && (Application.Current as App)?.IsQuitting != true) { e.Cancel = true; Hide(); } };
        DragLeave += (_, e) => {
            var point = e.GetPosition(this);
            if (point.X < 0 || point.Y < 0 || point.X > ActualWidth || point.Y > ActualHeight) ApplySurface(false);
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { _feedback.IsOpen = false; _feedbackTimer.Stop(); } };
        Session.Changed += Refresh; Session.Notification += ShowFeedback;
        _feedback.PlacementTarget = DockBody;
        _feedbackTimer.Tick += (_, _) => { _feedback.IsOpen = false; _feedbackTimer.Stop(); };
        Refresh();
    }
    public void ShowDock() { Session.Paster?.CaptureTarget(); var opening = !IsVisible; Show(); WindowPlacement.Clamp(this); if (opening) Motion.Reveal(DockBody); }
    public void Toggle() { if (IsVisible) Hide(); else ShowDock(); }
    public void RefreshDock() => Refresh();
    public void DisposeView()
    {
        _disposing = true; Session.Changed -= Refresh; Session.Notification -= ShowFeedback;
        _feedbackTimer.Stop(); _feedback.IsOpen = false; Close();
    }
    private void Refresh()
    {
        // A view can be closed while Session.Changed is iterating its delegate snapshot.
        if (_disposing) return;
        Appearance.Apply(Session.State.Settings, this, true);
        var settings = Session.State.Settings; Strip.Apply(settings);
        Topmost = settings.AlwaysOnTop;
        FontSize = Appearance.Bound(settings.FontSize, 11, 18, 13);
        var pins = Session.State.Items.Where(i => i.IsPinned).OrderBy(i => i.PinOrder).ToArray();
        var available = Assignment is { } a ? a.Display.Width / WindowPlacement.Scale(this) : IsLoaded ? WindowPlacement.WorkArea(this).Width : SystemParameters.WorkArea.Width;
        var size = Appearance.Bound(settings.DockButtonSize, 30, 48, 36) + Appearance.Bound(settings.DockSpacing, 0, 12, 4);
        var fixedWidth = 77 + 3 * size;
        var slots = Math.Max(1, (int)((available - fixedWidth) / size));
        if (pins.Length > slots) slots--;
        var capacity = Math.Max(1, Math.Min(8, slots));
        _arrivingPins.Clear();
        foreach (var pin in pins) if (!_knownPins.Contains(pin.Id)) _arrivingPins.Add(pin.Id);
        _knownPins.Clear(); foreach (var pin in pins) _knownPins.Add(pin.Id);
        Strip.SetPins(pins, capacity);
        ApplySurface(false);
    }
    private void ApplySurface(bool dropping)
    {
        var key = dropping ? "Selected" : "Surface";
        var color = ((SolidColorBrush)Strip.FindResource(key)).Color;
        color.A = dropping ? (byte)255 : (byte)Math.Round(255 * Appearance.Bound(Session.State.Settings.DockOpacity, 0, 100, 100) / 100);
        DockBody.Background = new SolidColorBrush(color);
        DockBody.BorderBrush = (Brush)Strip.FindResource(dropping ? "Ink" : "DockOutline");
    }
    private void ShowFeedback(string message)
    {
        if (!IsVisible) return;
        _feedback.Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 };
        _feedback.IsOpen = false; _feedback.IsOpen = true; _feedbackTimer.Stop(); _feedbackTimer.Start();
    }
    private void OnSave(object sender, RoutedEventArgs e) => Session.SaveClipboard();
    private void OnLibrary(object sender, RoutedEventArgs e) => _library();
    private void OnSettings(object sender, RoutedEventArgs e) => _settings();
    private void OnHide(object sender, RoutedEventArgs e) { _feedback.IsOpen = false; Hide(); }
    private void OnUseItem(object sender, RoutedEventArgs e)
    {
        if (_suppressClick) { _suppressClick = false; return; }
        if (sender is Button { DataContext: ItemRow row }) Session.Paste(new[] { row.Item });
    }
    private void OnPinMenu(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { DataContext: ItemRow row } button) return;
        e.Handled = true; _origin = null; _dragButton = null; _feedback.IsOpen = false;
        Session.Paster?.CaptureTarget();
        button.ContextMenu = ClipMenu.Create(Session, new[] { row.Item }, this, true, edit: _edit);
        ClipMenu.Show(button.ContextMenu, button);
    }
    private void OnPinLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ItemRow row } button && _arrivingPins.Remove(row.Item.Id)) Motion.Arrive(button);
    }
    private void OnItemDown(object sender, MouseButtonEventArgs e)
    { _origin = e.GetPosition(this); _dragButton = sender as Button; _suppressClick = false; }
    private void OnItemUp(object sender, MouseButtonEventArgs e) { _origin = null; _dragButton = null; }
    private void OnItemMove(object sender, MouseEventArgs e)
    {
        if (_origin is not Point start || _dragButton?.DataContext is not ItemRow row || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(this);
        if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var button = _dragButton; _origin = null; _dragButton = null; _suppressClick = true;
        // Release the button's capture so ending an OLE drag cannot also invoke click-paste.
        button.ReleaseMouseCapture();
        try
        {
            var result = DragDrop.DoDragDrop(button, Session.Transfer.BuildDrag(new[] { row.Item }), DragDropEffects.Copy);
            Session.Notify(result == DragDropEffects.None ? "Transfer cancelled · item kept" : "Transfer finished · item kept");
        }
        catch (Exception ex) { Session.Error("Drag", ex); }
        finally { ApplySurface(false); }
    }
    private void OnDragOver(object sender, DragEventArgs e)
    { var supported = TransferService.Supports(e.Data); ApplySurface(supported); e.Effects = supported ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private void OnDrop(object sender, DragEventArgs e)
    {
        ApplySurface(false); e.Handled = true; e.Effects = DragDropEffects.None;
        if (!TransferService.Supports(e.Data)) return;
        try { if (Session.Capture(e.Data)) e.Effects = DragDropEffects.Copy; }
        catch (Exception ex) { Session.Error("Drop", ex); }
    }
    private async void OnThumbnailLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image cleared) cleared.Source = null;
        if (sender is Image image && image.DataContext is ItemRow { Item.FilePath: string path } row && (row.Item.Kind == ClipDrop.Core.ItemKind.Image || row.IsVideo))
        { image.Source = null; var bitmap = await _thumbnails.Get(path); if (image.DataContext is ItemRow current && current.Item.FilePath == path) image.Source = bitmap; }
    }
}
