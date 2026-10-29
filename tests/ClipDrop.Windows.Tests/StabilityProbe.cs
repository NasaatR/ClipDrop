using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        search.Text = "note 4999"; UiProbe.Pump(180);
        check(list.Items.Count == 1 && ((ItemRow)list.Items[0]).Item.Content == "Large library note 4999", "Debounced search retains the final query on a 5000-clip library");
        search.Clear(); UiProbe.Pump(180);
        check(list.Items.Count == 5000 && ReferenceEquals(list.Items[0], rows[0]), "Filtering and clearing search reuse existing Library rows");
        render((FrameworkElement)library.Content, Path.Combine(output, "library-5000.png"));
        library.Hide(); session.TrySave();
    }
}
