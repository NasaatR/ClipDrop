using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class UsageProbe
{
    private sealed class Platform : IPastePlatform
    {
        public IntPtr Foreground { get; set; } = new(1);
        public uint Sent = 4;
        public bool Valid(PasteTarget _) => true;
        public bool OwnWindow(IntPtr _) => false;
        public bool KeysHeld => false;
        public void Focus(PasteTarget _) { }
        public uint SendPaste(PasteTarget _) => Sent;
        public void ReleaseInjectedKeys(uint _) { }
    }
    private static void Await(Task task)
    { var start = DateTime.UtcNow; while (!task.IsCompleted && (DateTime.UtcNow - start).TotalSeconds < 4) UiProbe.Pump(20); task.GetAwaiter().GetResult(); }
    private static Button Pin(DockWindow dock, ShelfItem item) => UiProbe.Find<Button>(dock.Pins, b => b.DataContext is ItemRow row && row.Item.Id == item.Id && b.Tag as string != "Keep");
    private static string[] Order(DockWindow dock) => dock.Pins.Items.Cast<ItemRow>().Select(r => r.Item.Id).ToArray();
    public static void Run(MainWindow library, DockWindow originalDock, string root, string output, Action<bool, string> check, Action<FrameworkElement, string> render)
    {
        library.Hide(); originalDock.Hide();
        var store = new StateStore(Path.Combine(root, "usage")); var state = store.Load(); state.Settings.AnimationsEnabled = false; state.Settings.CollapseWhenIdle = false;
        state.Settings.DockLeft = 30; state.Settings.DockTop = 30;
        using var session = new ShelfSession(store, state);
        var a = ShelfItem.FromText("One use A"); var b = ShelfItem.FromText("Kept B"); var c = ShelfItem.FromText("External drop C"); var d = ShelfItem.FromText("Cancel D"); b.KeepAfterUse = true;
        session.Add(new[] { a, b, c, d });
        Await(session.PasteAsync(new[] { a })); check(state.Items.Contains(a) && state.Trash.Count == 0, "Clipboard-only paste fallback leaves one-use clips in the dock");
        check(session.Copy(new[] { a }) && state.Items.Contains(a), "Explicit Copy never consumes a clip");
        var target = new PasteTarget(new IntPtr(1), 10, 20);
        using (var blocked = new DesktopPaste(new Platform { Foreground = new IntPtr(2) }, () => target))
        { session.Paster = blocked; Await(session.PasteAsync(new[] { a })); }
        check(state.Items.Contains(a), "Changed destination preserves unprotected clips after copying");
        using (var partial = new DesktopPaste(new Platform { Sent = 1 }, () => target))
        { session.Paster = partial; Await(session.PasteAsync(new[] { a })); }
        check(state.Items.Contains(a), "Partial paste input leaves the clip available for manual recovery");
        using (var sent = new DesktopPaste(new Platform(), () => target))
        { session.Paster = sent; Await(session.PasteAsync(new[] { a, b })); }
        session.Paster = null;
        check(!state.Items.Contains(a) && state.Items.Contains(b) && state.Trash.Single().Item == a, "A dispatched group paste archives only the unprotected member");
        check(new StateStore(store.Root).Load().Trash.Single().Item.Id == a.Id, "Automatic removal is persisted immediately in recoverable Trash");
        var dock = new DockWindow(session, () => { }, () => { }); dock.ShowDock();
        var mirror = new DockWindow(session, () => { }, () => { }); mirror.ShowDock(); mirror.Top += 90;
        var dropCount = 0; var received = "";
        var dropArea = new Border { Background = Brushes.SlateGray, Child = new TextBlock { Text = "Native OLE copy target", Margin = new Thickness(20) } };
        var receiver = new Window { Title = "ClipDrop OLE probe", Content = dropArea, Width = 280, Height = 180, AllowDrop = true, WindowStartupLocation = WindowStartupLocation.Manual };
        var area = WindowPlacement.WorkArea(dock); receiver.Left = area.Right - 300; receiver.Top = area.Top + 250;
        receiver.DragOver += (_, e) => { e.Effects = (e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.FileDrop)) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        receiver.Drop += (_, e) => { received = e.Data.GetDataPresent(DataFormats.FileDrop) ? ((string[])e.Data.GetData(DataFormats.FileDrop)).Single() : e.Data.GetData(DataFormats.UnicodeText) as string ?? ""; dropCount++; e.Effects = DragDropEffects.Copy; e.Handled = true; };
        receiver.Show(); receiver.Activate(); UiProbe.Pump();
        var destination = dropArea.PointToScreen(new Point(dropArea.ActualWidth / 2, dropArea.ActualHeight / 2));
        try
        {
            UiProbe.DragTo(Pin(dock, c), destination);
            check(received == c.Content && state.Trash.Any(t => t.Item.Id == c.Id) && !Order(mirror).Contains(c.Id), "A real OLE Copy drop receives text and archives the source clip on both dock views");
            UiProbe.DragTo(Pin(dock, b), destination);
            check(received == b.Content && state.Items.Contains(b), "An accepted external drag keeps a protected clip");
            var count = dropCount; UiProbe.DragTo(Pin(dock, d), destination, true);
            check(dropCount == count && state.Items.Contains(d), "Esc cancels a native OLE drag without consuming the clip");
            var filePath = Path.Combine(store.Root, "original.txt"); File.WriteAllText(filePath, "original file bytes");
            var fileClip = ShelfItem.FromPath(filePath); session.Add(new[] { fileClip }); UiProbe.Pump();
            UiProbe.DragTo(Pin(dock, fileClip), destination);
            check(received == filePath && state.Trash.Any(t => t.Item.Id == fileClip.Id) && File.ReadAllText(filePath) == "original file bytes",
                "Native file drop offers the original TXT file and archives only its card");
            var e = ShelfItem.FromText("Group E"); var f = ShelfItem.FromText("Group F"); var g = ShelfItem.FromText("Group G"); session.Add(new[] { e, f, g }); UiProbe.Pump();
            var ePin = Pin(dock, e); UiProbe.Hover(ePin);
            var keep = UiProbe.Find<Button>(dock.Pins, button => button.Tag as string == "Keep" && button.DataContext is ItemRow row && row.Item.Id == e.Id);
            Clipboard.SetText("Selection must not paste"); UiProbe.Click(keep);
            check(e.KeepAfterUse && Clipboard.GetText() == "Selection must not paste" && mirror.Pins.Items.Cast<ItemRow>().Single(r => r.Item.Id == e.Id).KeepAfterUse,
                "Corner keep button changes protection on both views without invoking paste");
            render((FrameworkElement)dock.Content, Path.Combine(output, "dock-kept.png"));
            UiProbe.Click(Pin(dock, d), ModifierKeys.Control); UiProbe.Click(Pin(dock, g), ModifierKeys.Shift);
            check(dock.Pins.Items.Cast<ItemRow>().Count(r => r.Selected) == 4 && Clipboard.GetText() == "Selection must not paste", "Native Ctrl/Shift-click selects a range without copying or pasting");
            var strip = (DockStrip)dock.FindName("Strip");
            UiProbe.Click((Button)strip.FindName("DoneButton"));
            UiProbe.RightClick(dock.QuickSave); UiProbe.Click(dock.ContextMenu.Items.OfType<MenuItem>().Single(i => i.Tag as string == "Select")); UiProbe.Click(Pin(dock, d)); UiProbe.Click(Pin(dock, e));
            check(dock.Pins.Items.Cast<ItemRow>().Count(r => r.Selected) == 2 && Clipboard.GetText() == "Selection must not paste", "Selection mode lets plain clicks choose multiple clips without insertion");
            render((FrameworkElement)dock.Content, Path.Combine(output, "dock-selection.png"));
            var trashBefore = state.Trash.Count;
            var end = Pin(dock, g); var afterG = end.PointToScreen(new Point(end.ActualWidth - 1, end.ActualHeight / 2));
            UiProbe.DragTo(Pin(dock, d), afterG);
            check(Order(dock).SequenceEqual(new[] { b.Id, f.Id, g.Id, d.Id, e.Id }) && Order(mirror).SequenceEqual(Order(dock)) && state.Trash.Count == trashBefore,
                "Real internal OLE drag reorders a selected group on both docks without archiving any member");
            UiProbe.Click((Button)strip.FindName("SelectedButton"));
            UiProbe.Click(((Button)strip.FindName("SelectedButton")).ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Tag as string == "Trash"));
            var deleted = state.Trash.Skip(trashBefore).ToArray();
            check(deleted.Select(t => t.Item.Id).ToHashSet().SetEquals(new[] { d.Id, e.Id }) && deleted.Select(t => t.Batch).Distinct().Count() == 1,
                "One batch-delete action removes the selected group, including explicitly selected kept clips");
            session.UndoRemove(); check(state.Items.Contains(d) && state.Items.Contains(e) && e.KeepAfterUse, "Undo restores the batch and its protection flags");
            session.TrySave(); var loaded = new StateStore(store.Root).Load();
            check(loaded.Items.Single(i => i.Id == e.Id).KeepAfterUse, "Corner protection survives saving and restart");
            foreach (var item in state.Items) item.IsPinned = false; session.Change(); UiProbe.Pump();
            var hint = (TextBlock)strip.FindName("EmptyHint");
            var atHint = hint.TranslatePoint(new Point(hint.ActualWidth / 2, hint.ActualHeight / 2), dock.Pins);
            check(strip.InsertionAt(atHint) == 0, "An empty dock exposes its visible drop hint as insertion slot zero");
        }
        finally { dock.DisposeView(); mirror.DisposeView(); receiver.Close(); IconAssets.Initialize(root); originalDock.ShowDock(); library.ShowShelf(); }
    }
}
