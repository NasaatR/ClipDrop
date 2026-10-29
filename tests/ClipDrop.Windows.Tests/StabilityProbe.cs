using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class StabilityProbe
{
    public static void Run(string root, string output, Action<bool, string> check, Action<FrameworkElement, string> render)
    {
        var store = new StateStore(Path.Combine(root, "large-library")); var state = store.Load();
        state.Settings.AnimationsEnabled = false; state.Settings.LibraryFollowsDock = false;
        state.Items.AddRange(Enumerable.Range(0, 5000).Select(i => ShelfItem.FromText("Large library note " + i)));
        using var session = new ShelfSession(store, state);
        var library = new MainWindow(session); library.ShowShelf(); UiProbe.Pump();
        var list = (ListBox)library.FindName("ItemList"); var search = (TextBox)library.FindName("SearchBox");
        var rows = list.Items.Cast<ItemRow>().ToArray();
        list.SelectedIndex = 2; var chosen = list.SelectedItem;
        var container = list.ItemContainerGenerator.ContainerFromItem(chosen);
        var changes = 0; ((System.Collections.Specialized.INotifyCollectionChanged)list.Items).CollectionChanged += (_, _) => changes++;
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 12; i++) session.Change();
        watch.Stop();
        check(changes == 0 && rows.Zip(list.Items.Cast<ItemRow>()).All(pair => ReferenceEquals(pair.First, pair.Second)) &&
            ReferenceEquals(list.SelectedItem, chosen) && ReferenceEquals(list.ItemContainerGenerator.ContainerFromItem(chosen), container),
            "Unchanged 5000-clip refresh keeps rows, containers and selection without collection resets");
        Console.WriteLine($"PERF 5000 clips, 12 unchanged refreshes: {watch.ElapsedMilliseconds} ms");
        search.Text = "note 4999";
        check(UiProbe.Until(() => list.Items.Count == 1 && ((ItemRow)list.Items[0]).Item.Content == "Large library note 4999"), "Debounced search retains the final query on a 5000-clip library");
        search.Clear();
        check(UiProbe.Until(() => list.Items.Count == 5000 && ReferenceEquals(list.Items[0], rows[0])), "Filtering and clearing search reuse existing Library rows");
        var row = rows[0]; var notices = new System.Collections.Generic.List<string?>(); row.PropertyChanged += (_, e) => notices.Add(e.PropertyName);
        row.Selected = true; row.DropMark = 1;
        check(notices.SequenceEqual(new[] { "Selected", "DropMark" }), "Selection and insertion cues do not re-evaluate metadata or file bindings");
        var fallback = new AppIcon { Kind = "Play", Width = 6, Height = 7 }; fallback.Resources["IconImage.Play"] = null;
        var host = new Window { Content = fallback, Width = 120, Height = 100, ShowInTaskbar = false }; host.Show(); UiProbe.Pump();
        var text = UiProbe.Find<TextBlock>(fallback, _ => true);
        var bounds = text.TransformToAncestor(fallback).TransformBounds(new Rect(0, 0, text.ActualWidth, text.ActualHeight));
        check(bounds.Left >= -.1 && bounds.Top >= -.1 && bounds.Right <= fallback.ActualWidth + .1 && bounds.Bottom <= fallback.ActualHeight + .1,
            "Even a tiny fallback emoji fits its full measured bounds without line-box clipping");
        host.Close();
        var tiny = new DockStrip(); state.Settings.DockSpacing = 0; state.Settings.DockButtonSize = 30;
        tiny.Apply(state.Settings); tiny.SetPins(state.Items.Take(4), 4);
        var iconHost = new Window { Content = tiny, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false }; iconHost.Show(); UiProbe.Pump();
        render(tiny, Path.Combine(output, "dock-minimum-spacing.png")); iconHost.Close();
        var needle = state.Items[0]; needle.Title = "First async snapshot"; session.Change(); UiProbe.Pump(360);
        needle.Title = "Newest edit during autosave"; session.Change(); UiProbe.Pump(360); session.TrySave();
        check(new StateStore(store.Root).Load().Items[0].Title == "Newest edit during autosave" && session.StorageStatus == "Saved", "A newer edit during background autosave survives the explicit final flush");
        render((FrameworkElement)library.Content, Path.Combine(output, "library-5000.png"));
        library.Hide(); session.TrySave();
    }
}
