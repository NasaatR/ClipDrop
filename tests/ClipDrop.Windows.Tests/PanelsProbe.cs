using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class PanelsProbe
{
    public static void Run(string root, string output, Action<bool, string> check, Action<FrameworkElement, string> render)
    {
        var store = new StateStore(Path.Combine(root, "panel-ui")); var state = store.Load(); state.Settings.AnimationsEnabled = false;
        using var session = new ShelfSession(store, state);
        var clips = new[] { ShelfItem.FromText("Panel alpha"), ShelfItem.FromText("Panel beta"), ShelfItem.FromText("Panel gamma") }; session.Add(clips);
        var main = PanelSets.Active(state);
        var library = new MainWindow(session); var dock = new DockWindow(session, library.ShowShelf, library.OpenSettings); dock.ShowDock(); UiProbe.Pump();
        UiProbe.Click(UiProbe.Find<Button>(dock, b => AutomationProperties.GetName(b) == "Saved panels"));
        var manager = Application.Current.Windows.OfType<PanelsWindow>().Single();
        check(manager.IsVisible, "Real dock panel button opens a single Saved panels manager");
        Button Button(string name) => UiProbe.Find<Button>(manager, b => b.Content as string == name);
        UiProbe.Click(Button("New empty"));
        check(dock.Pins.Items.Count == 0 && state.Items.Count == 3, "New empty panel clears only the dock membership and retains Library clips");
        manager.Close(); library.ShowShelf(); UiProbe.Pump();
        var items = (ListBox)library.FindName("ItemList"); items.SelectedItems.Clear();
        foreach (var row in items.Items.Cast<ItemRow>().Where(r => r.Item.Id == clips[1].Id || r.Item.Id == clips[2].Id)) items.SelectedItems.Add(row);
        UiProbe.Click(UiProbe.Find<Button>(library, b => AutomationProperties.GetName(b) == "Saved panels"));
        manager = Application.Current.Windows.OfType<PanelsWindow>().Single(); UiProbe.Pump();
        UiProbe.Click(UiProbe.Find<Button>(manager, b => AutomationProperties.GetName(b) == "Save selection as panel"));
        var selection = PanelSets.Active(state);
        check(selection.ClipIds.Count == 2 && selection.ClipIds.Contains(clips[1].Id) && selection.ClipIds.Contains(clips[2].Id) && dock.Pins.Items.Count == 2,
            "Library selection saves as an independent panel through the real manager action");
        var list = (ListBox)manager.FindName("PanelList");
        ListBoxItem Container(string id)
        {
            var row = list.Items.Cast<PanelsWindow.Row>().Single(r => r.Panel.Id == id);
            list.ScrollIntoView(row); UiProbe.Pump(); return (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(row);
        }
        UiProbe.Click(Container(main.Id)); UiProbe.Click(Container(selection.Id), ModifierKeys.Control);
        check(list.SelectedItems.Count == 2, "Ctrl-click selects multiple saved panels without opening them");
        UiProbe.Click(Button("Combine selected"));
        var combined = PanelSets.Active(state);
        check(combined.ClipIds.SequenceEqual(clips.Select(i => i.Id)) && state.Panels.Count == 4 && state.Items.Count == 3 && state.Trash.Count == 0,
            "Real Combine creates a deduplicated ordered panel while preserving source sets and payloads");
        check(((PanelsWindow.Row)list.SelectedItem).Panel.Id == combined.Id, "A newly combined panel becomes the visible active selection");
        ((TextBox)manager.FindName("NameEditor")).Text = "Combined project references with a longer saved panel name";
        UiProbe.Click(Button("Rename"));
        check(combined.Name.StartsWith("Combined project"), "Renaming a saved panel updates its persisted name");
        render((FrameworkElement)manager.Content, Path.Combine(output, "panels-combined.png"));
        UiProbe.Click(Button("Close panel")); UiProbe.Click(Button("Recently closed"));
        check(combined.ClosedUtc is not null && list.Items.Count == 1 && state.Trash.Count == 0, "Closing a panel moves it to Recently closed rather than clip Trash");
        session.TrySave(); var reopened = new StateStore(store.Root).Load();
        check(reopened.Panels.Single(p => p.Id == combined.Id).ClosedUtc is not null && reopened.Items.Count == 3, "Closed panel recovery remains available after saving and reopening state");
        render((FrameworkElement)manager.Content, Path.Combine(output, "panels-recently-closed.png"));
        UiProbe.Click(Button("Restore & open"));
        check(combined.ClosedUtc is null && state.ActivePanelId == combined.Id && dock.Pins.Items.Count == 3, "Real Restore reopens the closed panel and its original dock membership");
        var loose = ShelfItem.FromText("Outside every saved set"); session.Add(new[] { loose }, false);
        ((Button)library.FindName("ClearButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(state.Items.Count == 3 && state.Trash.Single().Item.Id == loose.Id, "Library Clear preserves clips in every open or recently closed saved set");
        manager.Width = 360; manager.Height = 360; UiProbe.Pump();
        render((FrameworkElement)manager.Content, Path.Combine(output, "panels-narrow.png"));
        manager.Close(); dock.DisposeView(); library.Hide(); session.TrySave();
    }
}
