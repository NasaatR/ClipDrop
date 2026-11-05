using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class HistoryProbe
{
    public static void Run(MainWindow previous, DockWindow previousDock, string root, string output, Action<bool, string> check)
    {
        previous.Hide(); previousDock.HideDock();
        var store = new StateStore(Path.Combine(root, "history-native")); var state = store.Load();
        state.Settings.AnimationsEnabled = false; state.Settings.CollapseWhenIdle = false; state.Settings.LibraryFollowsDock = false;
        state.Settings.DockLeft = 40; state.Settings.DockTop = 40; state.Settings.Left = 40; state.Settings.Top = 150;
        using var session = new ShelfSession(store, state);
        var library = new MainWindow(session); var dock = new DockWindow(session, library.ShowShelf, library.OpenSettings);
        try
        {
            dock.ShowDock(); UiProbe.Pump();
            Clipboard.SetText("First saved clip"); UiProbe.Click(dock.QuickSave);
            var first = state.Items.Single(); first.KeepAfterUse = true; session.Change();
            Clipboard.SetText("Second saved clip"); UiProbe.RightClick(dock.QuickSave);
            check(!dock.ContextMenu.IsOpen && state.Panels.Single().Stacks.Single().ClipIds.Count == 2 && state.Items.All(i => i.KeepAfterUse),
                "A real Save right-click appends to the last clip and inherits its keep-after-use protection");
            Clipboard.SetText("Third saved clip"); UiProbe.RightClick(dock.QuickSave);
            check(state.Panels.Single().Stacks.Single().ClipIds.Count == 3 && session.DockTiles().Length == 1,
                "Repeated right-click saves extend one flat collection instead of creating new tiles");
            Clipboard.SetText("Separate new clip"); UiProbe.Click(dock.QuickSave);
            check(session.DockTiles().Length == 2, "Left Save starts a separate clip after collecting a stack");
            Clipboard.SetText("Append to separate"); UiProbe.RightClick(dock.QuickSave);
            check(state.Panels.Single().Stacks.Count == 2 && state.Panels.Single().Stacks.Last().ClipIds.Count == 2,
                "Right Save targets the latest tile after a normal left Save");
            var used = state.Items.Where(i => !i.KeepAfterUse).ToArray();
            using (var drag = session.BeginDrag(used)) session.CompleteDrag(drag, DragDropEffects.Copy);
            library.ShowShelf();
            UiProbe.Click((Button)library.FindName("HistoryFilter"));
            var list = (ListBox)library.FindName("ItemList");
            check(list.Items.Count == 5 && state.Trash.Count == 2 && list.Items.Cast<ItemRow>().Any(r => r.Item.Id == used[0].Id),
                "History includes used clips after panel cleanup alongside current saved clips");
            session.AddHistoryToPanel(used); check(state.Trash.Count == 0 && session.Pins().Length == 5,
                "History restores used clips to the panel without duplicating their identities");
            var automatic = (CheckBox)library.FindName("HistoryCapture");
            check(automatic.IsChecked == false, "Clipboard monitoring stays off until explicitly enabled in History");
            UiProbe.Click(automatic); Clipboard.SetText("Auto copy — Привет 👋");
            check(UiProbe.Until(() => state.Items.Any(i => i.Content == "Auto copy — Привет 👋"), 1800), "Native clipboard notifications capture a new Unicode copy into History");
            var auto = state.Items.Single(i => i.Content == "Auto copy — Привет 👋");
            check(auto.HistoryOnly && !auto.IsPinned && session.Pins().Length == 5 && list.Items.Count == 6,
                "Automatic copies appear in History without adding dock or Library tiles");
            var time = auto.LastSavedUtc; session.Copy(new[] { auto }); UiProbe.Pump(300);
            check(auto.LastSavedUtc == time, "ClipDrop's own Copy is excluded from automatic recapture and reorder loops");
            var privateCopy = new DataObject(); privateCopy.SetText("Marked private copy"); privateCopy.SetData("ExcludeClipboardContentFromMonitorProcessing", new byte[] { 1 });
            Clipboard.SetDataObject(privateCopy, true); UiProbe.Pump(300);
            check(!ClipHistory.Entries(state).Any(i => i.Content == "Marked private copy"), "Automatic history respects the Windows private-clipboard exclusion format");
            UiProbe.Click(automatic); Clipboard.SetText("Paused copy"); UiProbe.Pump(250);
            check(!state.Settings.AutoSaveClipboard && state.Items.All(i => i.Content != "Paused copy"), "Pause removes the native listener and retains existing History");
            list.SelectedItems.Clear(); list.SelectedItems.Add(list.Items.Cast<ItemRow>().Single(r => r.Item.Id == first.Id)); list.SelectedItems.Add(list.Items.Cast<ItemRow>().Single(r => r.Item.Id == auto.Id)); list.Focus();
            UiProbe.PressKey(0x2E);
            check(!first.InHistory && state.Items.Contains(first) && state.Items.All(i => i.Id != auto.Id) && list.Items.Count == 4,
                "Delete removes the selected history group while preserving saved clips and freeing unused auto-history");
            UiProbe.CaptureWindow(library, Path.Combine(output, "history-dark.png"));
            UiProbe.Click((Button)library.FindName("ClearHistoryButton"));
            var clear = ((Button)library.FindName("ClearHistoryButton")).ContextMenu;
            check(clear.IsOpen && clear.Items.OfType<MenuItem>().Single().Header.ToString()!.Contains("4"), "Clear History asks for a scoped confirmation in a compact themed menu");
            var fresh = ShelfItem.FromText("New while confirmation open"); session.Add(new[] { fresh });
            UiProbe.Click(clear.Items.OfType<MenuItem>().Single());
            check(ClipHistory.Entries(state).Single().Id == fresh.Id && state.Items.Count == 6 && session.Pins().Length == 6,
                "Clear History preserves saved content and a new capture made after confirmation opened");
            ((TextBox)library.FindName("SearchBox")).Text = "confirmation"; UiProbe.Pump(160);
            check(list.Items.Count == 1, "History search filters saved text through the existing debounced search");
            state.Settings.Theme = "Light"; session.ChangeSettings(); library.Width = 320; UiProbe.Pump();
            UiProbe.CaptureWindow(library, Path.Combine(output, "history-narrow-light.png"));
            session.TrySave(); var loaded = new StateStore(store.Root).Load();
            check(!loaded.Settings.AutoSaveClipboard && ClipHistory.Entries(loaded).Single().Id == fresh.Id && loaded.Items.Count == 6,
                "History clearing, pause and saved collections survive a storage restart");
        }
        finally { library.Hide(); dock.DisposeView(); IconAssets.Initialize(root); previousDock.ShowDock(); previous.ShowShelf(); }
    }
}
