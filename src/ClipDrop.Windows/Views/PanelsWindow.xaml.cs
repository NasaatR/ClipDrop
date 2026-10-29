using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

public partial class PanelsWindow : Window
{
    public sealed record Row(PanelSet Panel, string Details, string ActiveLabel)
    { public string Name => Panel.Name; }
    private readonly ShelfSession _session;
    private readonly DockCoordinator? _docks;
    private string? _sourcePanel;
    private bool _closed, _refreshing;
    private string[] _pending = Array.Empty<string>();
    public PanelsWindow(ShelfSession session, DockCoordinator? docks = null)
    {
        _session = session; _docks = docks; InitializeComponent();
        WindowButton.Visibility = docks is null ? Visibility.Collapsed : Visibility.Visible;
        _ = new WindowDrag(this, () => { }, () => { });
        _session.Changed += Refresh;
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(() => {
            if (IsVisible && PanelList.SelectedItem is not null) PanelList.ScrollIntoView(PanelList.SelectedItem);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
        Closed += (_, _) => _session.Changed -= Refresh;
        Refresh();
    }
    public void SetSource(string? panelId) => _sourcePanel = panelId;
    public void OfferSelection(IReadOnlyList<ShelfItem>? clips)
    {
        _pending = clips?.Select(i => i.Id).Distinct().ToArray() ?? Array.Empty<string>();
        SaveSelection.Visibility = _pending.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        SaveSelection.Content = $"Save {_pending.Length} clips";
    }
    private Row[] Selected() => PanelList.SelectedItems.Cast<Row>().ToArray();
    private string[] SelectedIds() => Selected().Select(r => r.Panel.Id).ToArray();
    private string NewName(string prefix) => Enumerable.Range(1, 101).Select(i => prefix + " " + i).First(n => _session.State.Panels.All(p => p.Name != n));
    private void Refresh()
    {
        Appearance.Apply(_session.State.Settings, this); Topmost = _session.State.Settings.AlwaysOnTop;
        var selected = SelectedIds().ToHashSet(); var live = _session.State.Items.Select(i => i.Id).ToHashSet();
        var panels = _session.State.Panels.Where(p => _closed ? p.ClosedUtc is not null || p.Window?.ClosedUtc is not null : p.ClosedUtc is null);
        if (_closed) panels = panels.OrderByDescending(p => p.ClosedUtc ?? p.Window?.ClosedUtc);
        var rows = panels.Select(p => {
            var count = p.ClipIds.Count(live.Contains); var trash = p.ClipIds.Count - count;
            var labels = new List<string>();
            if (p.Id == _session.State.ActivePanelId) labels.Add("Main");
            if (p.Window?.IsOpen == true) labels.Add("Window");
            else if (p.Window?.ClosedUtc is not null && p.ClosedUtc is null) labels.Add("Window closed");
            return new Row(p, $"{count} {(count == 1 ? "clip" : "clips")}" + (trash > 0 ? $" · {trash} in Trash" : ""), string.Join(" · ", labels));
        }).ToArray();
        _refreshing = true;
        try
        {
            PanelList.ItemsSource = rows;
            foreach (var row in rows.Where(r => selected.Contains(r.Panel.Id))) PanelList.SelectedItems.Add(row);
            if (PanelList.SelectedItems.Count == 0 && rows.Length > 0) PanelList.SelectedItem = rows.FirstOrDefault(r => r.Panel.Id == _session.State.ActivePanelId) ?? rows[0];
        }
        finally { _refreshing = false; }
        PanelCount.Text = rows.Length.ToString(); EmptyHint.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        OpenFilter.SetResourceReference(BackgroundProperty, _closed ? "Raised" : "Selected");
        ClosedFilter.SetResourceReference(BackgroundProperty, _closed ? "Selected" : "Raised");
        OpenButton.Content = _closed ? "Restore & open" : "Use in dock";
        ClosePanelButton.Visibility = MergeButton.Visibility = _closed ? Visibility.Collapsed : Visibility.Visible;
        UpdateActions();
    }
    private void UpdateActions()
    {
        var selected = Selected();
        OpenButton.IsEnabled = RenameButton.IsEnabled = WindowButton.IsEnabled = selected.Length == 1;
        if (_closed && selected is [var closed] && closed.Panel.Window is not null && _docks is not null)
            OpenButton.Content = "Restore window";
        else OpenButton.Content = _closed ? "Restore & open" : "Use in dock";
        ClosePanelButton.IsEnabled = selected.Length > 0; MergeButton.IsEnabled = selected.Length > 1;
        NameEditor.Text = selected.Length == 1 ? selected[0].Name : selected.Length > 1 ? "Combined" : "";
        Hint.Text = selected.Length > 1 ? $"{selected.Length} panels · combine keeps the source panels" : "Ctrl/Shift to select several · closing keeps your clips";
    }
    private void OnSelection(object sender, SelectionChangedEventArgs e) { if (!_refreshing) UpdateActions(); }
    private void OnOpenFilter(object sender, RoutedEventArgs e) { _closed = false; Refresh(); }
    private void OnClosedFilter(object sender, RoutedEventArgs e) { _closed = true; Refresh(); }
    private void OnNew(object sender, RoutedEventArgs e)
    { _closed = false; _session.PanelAction(() => PanelSets.Create(_session.State, NewName("Panel")), "Empty panel created"); SelectActive(); }
    private void SelectActive()
        => SelectPanel(_session.State.ActivePanelId);
    private void SelectPanel(string id)
    {
        var active = PanelList.Items.Cast<Row>().FirstOrDefault(r => r.Panel.Id == id);
        if (active is not null) { PanelList.SelectedItems.Clear(); PanelList.SelectedItem = active; PanelList.ScrollIntoView(active); }
    }
    private void OnSnapshot(object sender, RoutedEventArgs e)
    {
        var ids = _session.Pins(_sourcePanel).Select(i => i.Id).ToArray();
        _closed = false; _session.PanelAction(() => PanelSets.Create(_session.State, NewName("Saved dock"), ids), "Dock saved as a panel");
        SelectActive();
    }
    private void OnSaveSelection(object sender, RoutedEventArgs e)
    {
        var ids = _pending.Where(id => _session.State.Items.Any(i => i.Id == id)).ToArray();
        if (ids.Length == 0) { _session.Notify("Selected clips are no longer in Library"); return; }
        _closed = false; _session.PanelAction(() => PanelSets.Create(_session.State, NewName("Selection"), ids), "Selection saved as a panel");
        SelectActive();
        OfferSelection(null);
    }
    private void OnOpen(object sender, RoutedEventArgs e)
    {
        if (Selected() is not [var row]) return;
        if (_closed && row.Panel.Window is not null && _docks is not null) _docks.OpenPanelWindow(row.Panel.Id);
        else _session.PanelAction(() => { if (_closed) PanelSets.Restore(_session.State, row.Panel.Id); else PanelSets.Activate(_session.State, row.Panel.Id); }, "Panel opened");
        // Keep the manager available for arranging several sets in sequence.
        _closed = false; Refresh(); SelectPanel(row.Panel.Id);
    }
    private void OnWindow(object sender, RoutedEventArgs e)
    {
        if (_docks is null || Selected() is not [var row]) return;
        _docks.OpenPanelWindow(row.Panel.Id);
        _closed = false; Refresh(); SelectPanel(row.Panel.Id);
    }
    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(PanelList, source) is ListBoxItem)
            OnOpen(sender, e);
    }
    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (Selected() is not [var row]) return;
        var name = NameEditor.Text; _session.PanelAction(() => PanelSets.Rename(_session.State, row.Panel.Id, name), "Panel renamed");
    }
    private void OnMerge(object sender, RoutedEventArgs e)
    {
        // List order is stable even if Ctrl selections were clicked in reverse.
        var ids = SelectedIds().ToHashSet(); var ordered = PanelList.Items.Cast<Row>().Where(r => ids.Contains(r.Panel.Id)).Select(r => r.Panel.Id).ToArray();
        var name = string.IsNullOrWhiteSpace(NameEditor.Text) ? "Combined" : NameEditor.Text;
        _session.PanelAction(() => PanelSets.Merge(_session.State, ordered, name), "Panels combined · source panels kept");
        SelectActive();
    }
    private void OnClosePanel(object sender, RoutedEventArgs e)
    { var ids = SelectedIds(); _session.PanelAction(() => PanelSets.Close(_session.State, ids), "Panel closed · restore from Recently closed"); }
    private void OnDismiss(object sender, RoutedEventArgs e) => Close();
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if (e.Key == Key.Enter) { if (Keyboard.FocusedElement == NameEditor) OnRename(sender, e); else OnOpen(sender, e); e.Handled = true; }
    }
}
