using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class IndependentProbe
{
    private sealed class Displays : IDisplaySource
    {
        public IReadOnlyList<DisplayArea> Current = Array.Empty<DisplayArea>();
        public IReadOnlyList<DisplayArea> GetDisplays() => Current;
        public string? TargetDisplay(IntPtr _) => Current.FirstOrDefault()?.Id;
    }
    private static string[] Ids(DockWindow view) => view.Pins.Items.Cast<ItemRow>().Select(r => r.Item.Id).ToArray();
    private static Button Pin(DockWindow view, string id) => UiProbe.Find<Button>(view.Pins, b => b.Tag as string != "Keep" && b.DataContext is ItemRow row && row.Item.Id == id);
    public static void Run(MainWindow oldLibrary, DockWindow oldDock, string root, string output, Action<bool, string> check)
    {
        oldLibrary.Hide(); oldDock.HideDock();
        var store = new StateStore(Path.Combine(root, "independent-ui")); var state = store.Load();
        state.Settings.AnimationsEnabled = false; state.Settings.CollapseWhenIdle = false;
        using var session = new ShelfSession(store, state);
        var a = ShelfItem.FromText("Main only"); var b = ShelfItem.FromText("Window B"); var c = ShelfItem.FromText("Window C");
        session.Add(new[] { a, b, c }); var main = state.ActivePanelId;
        var first = PanelSets.Create(state, "References", new[] { b.Id });
        var second = PanelSets.Create(state, "Assets", new[] { c.Id }); PanelSets.Activate(state, main); session.Change();
        var library = new MainWindow(session);
        var area = WindowPlacement.WorkArea(oldDock); var scale = WindowPlacement.Scale(oldDock);
        var display = new DisplayArea("independent-test", "Test display", area.Left * scale, area.Top * scale, area.Width * scale, area.Height * scale, true);
        var source = new Displays { Current = new[] { display } };
        using var coordinator = new DockCoordinator(session, library, displays: source);
        try
        {
            coordinator.OpenPanelWindow(first.Id); coordinator.OpenPanelWindow(second.Id); UiProbe.Pump();
            var one = coordinator.IndependentViews.Single(v => v.PanelId == first.Id);
            var two = coordinator.IndependentViews.Single(v => v.PanelId == second.Id);
            var home = coordinator.Views.Single(v => v.PanelId is null);
            first.Window!.Layout.OffsetX = 35; first.Window.Layout.OffsetY = 90;
            second.Window!.Layout.OffsetX = 420; second.Window.Layout.OffsetY = 230; session.Change(); UiProbe.Pump();
            check(coordinator.Views.Count == 3 && Ids(one).SequenceEqual(new[] { b.Id }) && Ids(two).SequenceEqual(new[] { c.Id }) && state.ActivePanelId == main,
                "Two independent native dock HWNDs show different sets alongside the unchanged main dock");
            var initialWidth = WindowPlacement.PixelBounds(one).Width;
            Clipboard.SetText("Saved into References"); UiProbe.Click(one.QuickSave); UiProbe.Pump();
            var added = state.Items.Single(i => i.Content == "Saved into References");
            check(Ids(one).Contains(added.Id) && !Ids(two).Contains(added.Id) && !Ids(home).Contains(added.Id) && !added.IsPinned,
                "Native Save in an independent window adds only to its own set");
            session.TogglePin(new[] { added }, first.Id); UiProbe.Pump();
            check(!Ids(one).Contains(added.Id) && state.Items.Contains(added), "Scoped removal from a panel keeps the shared Library payload");
            check(WindowPlacement.PixelBounds(one).Width == initialWidth,
                "Removing a newly added clip shrinks the native panel back to its content width");
            var originalMain = Ids(home);
            var target = Pin(two, c.Id); var after = target.PointToScreen(new Point(target.ActualWidth - 2, target.ActualHeight / 2));
            UiProbe.DragTo(Pin(one, b.Id), after);
            check(Ids(two).SequenceEqual(new[] { c.Id, b.Id }) && Ids(one).SequenceEqual(new[] { b.Id }) && Ids(home).SequenceEqual(originalMain) && state.Trash.Count == 0,
                "A real cross-set internal drop adds ordered target membership and keeps source/main clips");
            var orderedWidth = WindowPlacement.PixelBounds(two).Width;
            var bInTwo = Pin(two, b.Id); var beforeC = Pin(two, c.Id).PointToScreen(new Point(1, target.ActualHeight / 2));
            UiProbe.DragTo(bInTwo, beforeC);
            check(Ids(two).SequenceEqual(new[] { b.Id, c.Id }) && Ids(one).SequenceEqual(new[] { b.Id }),
                "Independent native reorder changes only the requested set");
            check(WindowPlacement.PixelBounds(two).Width == orderedWidth,
                "Finishing an implicit drag removes temporary selection width from the panel");
            UiProbe.Hover(one.QuickSave);
            UiProbe.CaptureWindow(one, Path.Combine(output, "independent-references-screen.png"));
            UiProbe.Hover(two.QuickSave);
            UiProbe.CaptureWindow(two, Path.Combine(output, "independent-assets-screen.png"));
            var priorSecond = WindowPlacement.PixelBounds(two);
            UiProbe.Drag(one, new Point(3, one.ActualHeight / 2), 25, 35);
            check(WindowPlacement.PixelBounds(two) == priorSecond, "Moving one independent window leaves the other window in place");
            var savedX = first.Window.Layout.OffsetX; var savedY = first.Window.Layout.OffsetY;
            coordinator.ShowLibrary(one); UiProbe.Pump();
            UiProbe.Click((Button)library.FindName("PinnedFilter"));
            var list = (ListBox)library.FindName("ItemList");
            check(list.Items.Cast<ItemRow>().Select(r => r.Item.Id).SequenceEqual(new[] { b.Id }),
                "Library invoked from an independent window scopes its panel filter to that set");
            library.Hide();
            UiProbe.RightClick(one.QuickSave);
            UiProbe.Click(one.ContextMenu.Items.OfType<MenuItem>().Single(m => m.Tag as string == "CloseWindow")); UiProbe.Pump();
            check(coordinator.IndependentViews.Count == 1 && first.Window.ClosedUtc is not null && first.ClosedUtc is null && state.Items.Count == 4,
                "Close window removes only its HWND and retains a restorable named set and all payloads");
            session.TrySave();
            var reopened = new StateStore(store.Root).Load();
            check(reopened.Panels.Single(p => p.Id == first.Id).Window!.ClosedUtc is not null &&
                reopened.Panels.Single(p => p.Id == first.Id).Window!.Layout.OffsetX == savedX,
                "Closed independent-window position survives a saved-state restart");
            library.OpenPanels(); UiProbe.Pump(); var manager = Application.Current.Windows.OfType<PanelsWindow>().Single();
            UiProbe.Click(UiProbe.Find<Button>(manager, v => v.Content as string == "Recently closed"));
            var panels = (ListBox)manager.FindName("PanelList");
            panels.SelectedItem = panels.Items.Cast<PanelsWindow.Row>().Single(r => r.Panel.Id == first.Id);
            UiProbe.Click((Button)manager.FindName("OpenButton")); UiProbe.Pump();
            one = coordinator.IndependentViews.Single(v => v.PanelId == first.Id);
            check(one.IsShown && first.Window.Layout.OffsetX == savedX && first.Window.Layout.OffsetY == savedY && state.ActivePanelId == main &&
                ((PanelsWindow.Row)panels.SelectedItem).Panel.Id == first.Id,
                "Recently closed restores the actual window at its saved position without switching the main dock");
            manager.Close();
            coordinator.HideAll(); check(coordinator.Views.All(v => !v.IsShown), "Global Hide includes independent windows");
            coordinator.ShowAll(); check(coordinator.Views.All(v => v.IsShown), "Global Show restores open independent windows");
            source.Current = Array.Empty<DisplayArea>(); coordinator.RefreshDisplays();
            source.Current = new[] { display }; coordinator.RefreshDisplays(); coordinator.RefreshDisplays();
            check(coordinator.IndependentViews.Count == 2 && coordinator.Views.Count == 3, "Repeated display loss/reconnect restores independent windows without duplicates");
            one = coordinator.IndependentViews.Single(v => v.PanelId == first.Id);
            one.Close(); check(UiProbe.Until(() => coordinator.IndependentViews.Count == 1), "Native Close defers disposal safely and records independent-window recovery");
        }
        finally
        {
            PopupDismiss.CloseAll(); foreach (var manager in Application.Current.Windows.OfType<PanelsWindow>().ToArray()) manager.Close();
            library.Hide(); IconAssets.Initialize(root); oldDock.ShowDock(); oldLibrary.ShowShelf();
        }
    }
}
