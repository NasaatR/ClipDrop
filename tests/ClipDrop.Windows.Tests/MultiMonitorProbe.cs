using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Input;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class MultiMonitorProbe
{
    private sealed class Displays : IDisplaySource
    {
        public IReadOnlyList<DisplayArea> Current = Array.Empty<DisplayArea>();
        public IReadOnlyList<DisplayArea> GetDisplays() => Current;
        public string? TargetDisplay(IntPtr window) => Current.FirstOrDefault()?.Id;
    }
    public static void Run(MainWindow oldLibrary, DockWindow oldDock, string root, string output, Action<bool, string> check)
    {
        oldLibrary.Hide(); oldDock.HideDock();
        var scale = WindowPlacement.Scale(oldDock);
        var area = WindowPlacement.WorkArea(oldDock);
        // Two non-overlapping logical work areas inside the runner's real display.
        // This reproduces retarget/clamp behavior with real HWNDs and input, not hardware DPI.
        var a = new DisplayArea("move-a", "Left screen", area.Left * scale, area.Top * scale, area.Width * scale / 2, area.Height * scale, true);
        var b = a with { Id = "move-b", Label = "Right screen", Left = a.Left + a.Width, Primary = false };
        var source = new Displays { Current = new[] { a, b } };
        var store = new StateStore(Path.Combine(root, "multi-monitor")); var state = store.Load();
        state.Settings.HomeDisplayId = a.Id; state.Settings.CollapseWhenIdle = false;
        state.Settings.AnimationsEnabled = false; state.Settings.IdleDockOpacity = 100;
        using var session = new ShelfSession(store, state);
        var library = new MainWindow(session);
        session.Add(new[] { ShelfItem.FromText("Shared multi-monitor clip") });
        using var coordinator = new DockCoordinator(session, library, displays: source);
        try
        {
            var dock = coordinator.Views.Single(); UiProbe.Pump();
            var free = UiProbe.Find<Border>(dock, border => border.Name == "Body");
            var start = free.PointToScreen(new Point(5, 5));
            // UiProbe.DragTo uses the element center: this tiny transparent background
            // hit target uses an explicit point without touching Save/the clip.
            UiProbe.DragFrom(start, new Point(b.Left + 90, b.Top + 110));
            check(state.Settings.HomeDisplayId == b.Id && ReferenceEquals(dock, coordinator.Views.Single()) &&
                dock.Assignment!.Display.Id == b.Id && WindowPlacement.PixelBounds(dock).Left >= b.Left,
                "Dragging the single main dock across a display boundary retargets the same HWND instead of snapping back");
            var resized = false;
            var duringMove = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            duringMove.Tick += (_, _) => {
                if (!dock.IsMoving || resized) return;
                resized = true; state.Settings.DockButtonSize = 48; session.Change(); coordinator.RefreshDisplays(); duringMove.Stop();
            };
            duringMove.Start();
            try { UiProbe.DragFrom(free.PointToScreen(new Point(5, 5)), new Point(a.Left + 100, a.Top + 120)); }
            finally { duringMove.Stop(); }
            check(resized && state.Settings.HomeDisplayId == a.Id && ReferenceEquals(dock, coordinator.Views.Single()) && !dock.IsMoving &&
                WindowPlacement.PixelBounds(dock).Right <= a.Left + a.Width + 1,
                "Size/topology reconciliation during a real drag waits for release and retains the new screen and same HWND");
            var expectedX = dock.Assignment!.Layout.OffsetX;
            coordinator.RefreshDisplays(); coordinator.RefreshDisplays(); UiProbe.Pump();
            check(dock.Assignment.Layout.OffsetX == expectedX && ReferenceEquals(dock, coordinator.Views.Single()),
                "Repeated display refresh preserves the moved dock anchor without replacing its HWND");
            var originalPanel = state.ActivePanelId;
            var named = PanelSets.Create(state, "Movable references", new[] { state.Items[0].Id });
            PanelSets.Activate(state, originalPanel); session.Change(); coordinator.OpenPanelWindow(named.Id); UiProbe.Pump();
            var independent = coordinator.IndependentViews.Single();
            UiProbe.DragFrom(UiProbe.Find<Border>(independent, border => border.Name == "Body").PointToScreen(new Point(5, 5)), new Point(b.Left + 80, b.Top + 200));
            check(named.Window!.Layout.DisplayId == b.Id && state.Settings.HomeDisplayId == a.Id && ReferenceEquals(independent, coordinator.IndependentViews.Single()),
                "Independent panel movement retargets only its own saved screen and keeps the main panel in place");
            coordinator.ClosePanelWindow(named.Id);
            state.Settings.MirrorDocks = true; state.Settings.Docks.Single(l => l.DisplayId == b.Id).Enabled = true; session.Change(); UiProbe.Pump();
            check(coordinator.Views.Count == 2 && coordinator.Views.All(v => v.Pins.Items.Count == 1),
                "Clip duplication shows the shared active set on both screens");
            var movingCopy = coordinator.Views.Single(v => v.Assignment!.Display.Id == a.Id);
            var retiredCopy = coordinator.Views.Single(v => v.Assignment!.Display.Id == b.Id);
            var toggled = false;
            var switchMode = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            switchMode.Tick += (_, _) => {
                if (!movingCopy.IsMoving || toggled) return;
                toggled = true; state.Settings.MirrorDocks = false; session.Change(); switchMode.Stop();
            };
            switchMode.Start();
            try { UiProbe.DragFrom(UiProbe.Find<Border>(movingCopy, border => border.Name == "Body").PointToScreen(new Point(5, 5)), new Point(b.Left + 100, b.Top + 160)); }
            finally { switchMode.Stop(); }
            check(toggled && coordinator.Views.Count == 1 && ReferenceEquals(movingCopy, coordinator.Views.Single()) &&
                !Application.Current.Windows.Cast<Window>().Contains(retiredCopy),
                "Disabling clip mirroring during a crossing move retires the destination copy without orphaning a native window");
            state.Settings.MirrorDocks = true; session.Change(); UiProbe.Pump();
            var folder = Path.Combine(root, "mirror-downloads"); Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "Shared download.png");
            File.WriteAllBytes(file, ImageFiles.Encode(System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 80, 120, 220, 255 }, 4)));
            state.Settings.DownloadsSource = "Folder"; state.Settings.DownloadsFolder = folder;
            coordinator.OpenDownloads();
            check(UiProbe.Until(() => coordinator.DownloadsViews.Count == 2 && coordinator.DownloadsViews.All(v => v.DownloadList.Items.Count == 1), 6000),
                "Downloads defaults to one linked populated window per connected display");
            var copies = coordinator.DownloadsViews.ToArray();
            check(copies.All(v => v.IsVisible) && copies.Select(v => v.Assignment!.Display.Id).Distinct().Count() == 2 &&
                copies.All(v => v.DownloadList.Items.Cast<DownloadsWindow.Row>().Single().File.Path == file),
                "Mirrored Downloads show the same original file with separate display assignments");
            var added = Path.Combine(folder, "Another ready file.fig"); File.WriteAllText(added, "original bytes");
            check(UiProbe.Until(() => copies.All(v => v.DownloadList.Items.Count == 2), 6000),
                "One file catalog update refreshes every linked Downloads view");
            var one = copies.Single(v => v.Assignment!.Display.Id == a.Id);
            var two = copies.Single(v => v.Assignment!.Display.Id == b.Id);
            UiProbe.Hover(one.StackButton); one.DownloadList.UpdateLayout();
            var row = UiProbe.Find<ListBoxItem>(one.DownloadList, r => r.DataContext is DownloadsWindow.Row item && item.File.Path == added);
            UiProbe.Click(row, ModifierKeys.Control);
            check(one.DownloadList.SelectedItems.Count == 1 && two.DownloadList.SelectedItems.Count == 0,
                "Downloads selection is local to each copy rather than leaking between screens");
            var receiver = new Border { Background = Brushes.SteelBlue, AllowDrop = true };
            string[]? received = null;
            receiver.DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            receiver.Drop += (_, e) => { received = e.Data.GetData(DataFormats.FileDrop) as string[]; e.Effects = DragDropEffects.Copy; e.Handled = true; };
            var target = new Window { Content = receiver, Width = 220, Height = 130, Left = 20, Top = 40, ShowInTaskbar = false };
            target.Show();
            try
            {
                UiProbe.Hover(two.StackButton); two.DownloadList.UpdateLayout();
                var transfer = UiProbe.Find<ListBoxItem>(two.DownloadList, r => r.DataContext is DownloadsWindow.Row item && item.File.Path == added);
                Console.WriteLine($"MIRROR transfer before: compact={two.IsCompact}; visible={transfer.IsVisible}; row={transfer.PointToScreen(new Point(10, 10))}; destination={receiver.PointToScreen(new Point(100, 65))}; count={two.DownloadList.Items.Count}");
                UiProbe.DragTo(transfer, receiver.PointToScreen(new Point(100, 65)));
                Console.WriteLine($"MIRROR transfer after: received={received?.Length ?? -1}; matches={received?.Contains(added)}; trash={state.Trash.Count}; status={session.Status}; compact={two.IsCompact}");
                check(received is { Length: 1 } && received[0] == added && state.Trash.Count == 0 && File.ReadAllText(added) == "original bytes",
                    "A direct native drag from the second Downloads copy exposes the original file and keeps the library and file intact");
            }
            finally { target.Close(); }
            coordinator.HideAll(); check(coordinator.DownloadsViews.All(v => !v.IsVisible), "Hide all includes every Downloads copy");
            source.Current = new[] { a }; coordinator.RefreshDisplays();
            check(coordinator.DownloadsViews.Count == 1 && !coordinator.DownloadsViews.Single().IsVisible && state.Settings.DownloadsLayouts.Count == 2,
                "Disconnect removes only the unavailable Downloads HWND and keeps its hidden layout");
            source.Current = new[] { b, a }; coordinator.RefreshDisplays(); coordinator.RefreshDisplays();
            check(coordinator.DownloadsViews.Count == 2 && coordinator.DownloadsViews.All(v => !v.IsVisible),
                "Reconnect never duplicates or unexpectedly reveals hidden Downloads copies");
            coordinator.ShowAll(); UiProbe.Pump();
            check(coordinator.DownloadsViews.All(v => v.IsVisible), "Show all restores every enabled linked Downloads copy");
            var movingDownloads = coordinator.DownloadsViews.Single(v => v.Assignment!.Display.Id == a.Id);
            var retiredDownloads = coordinator.DownloadsViews.Single(v => v.Assignment!.Display.Id == b.Id);
            var switchedDownloads = false;
            var switchDownloadsMode = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            switchDownloadsMode.Tick += (_, _) => {
                if (!movingDownloads.IsMoving || switchedDownloads) return;
                switchedDownloads = true; state.Settings.MirrorDownloads = false; session.Change(); switchDownloadsMode.Stop();
            };
            UiProbe.Hover(movingDownloads.StackButton);
            var movingCapsule = (Border)movingDownloads.FindName("CapsuleShell");
            switchDownloadsMode.Start();
            try { UiProbe.DragFrom(movingCapsule.PointToScreen(new Point(3, movingCapsule.ActualHeight / 2)), new Point(b.Left + b.Width - 100, b.Top + b.Height - 40)); }
            finally { switchDownloadsMode.Stop(); }
            check(switchedDownloads && coordinator.DownloadsViews.Count == 1 && ReferenceEquals(movingDownloads, coordinator.DownloadsView) &&
                !Application.Current.Windows.Cast<Window>().Contains(retiredDownloads),
                "Disabling Downloads mirroring during a crossing move closes the displaced copy instead of leaving an orphan HWND");
            state.Settings.MirrorDownloads = true; session.Change(); UiProbe.Pump();
            state.Settings.MirrorDownloads = false; session.Change(); UiProbe.Pump();
            check(coordinator.DownloadsViews.Count == 1 && coordinator.Views.Count == 2,
                "Turning off Downloads duplication leaves clip duplication independent");
            var singleStack = coordinator.DownloadsView!;
            var from = singleStack.Assignment!.Display;
            var to = from.Id == a.Id ? b : a;
            var shortTarget = to with { Top = to.Top + 100, Height = 260 * WindowPlacement.Scale(singleStack) };
            source.Current = new[] { from, shortTarget }; coordinator.RefreshDisplays();
            UiProbe.Hover(singleStack.StackButton);
            var capsule = (Border)singleStack.FindName("CapsuleShell");
            UiProbe.DragFrom(capsule.PointToScreen(new Point(3, capsule.ActualHeight / 2)), new Point(shortTarget.Left + shortTarget.Width - 110, shortTarget.Top + shortTarget.Height - 30));
            check(state.Settings.DownloadsLayout.DisplayId == to.Id && ReferenceEquals(singleStack, coordinator.DownloadsView),
                "Single Downloads still moves across screens using the same HWND");
            check(WindowPlacement.PixelBounds(singleStack).Bottom <= shortTarget.Top + shortTarget.Height + 1 && singleStack.Height <= 260,
                "Moving single Downloads to a shorter work area resizes the window so its bottom capsule remains reachable");
            source.Current = new[] { a, b }; coordinator.RefreshDisplays(); UiProbe.Pump();
            state.Settings.MirrorDocks = false; session.Change();
            check(coordinator.Views.Count == 1 && coordinator.DownloadsViews.Count == 1,
                "Both surfaces can independently use one movable window");
            var settingsWindow = new SettingsWindow(state.Settings, () => session.Change(), session.TrySave, session.Report, session.Root, coordinator);
            settingsWindow.Show(); UiProbe.Pump();
            var screens = UiProbe.Find<MonitorSettings>(settingsWindow, _ => true); screens.BringIntoView(); UiProbe.Pump();
            var mode = UiProbe.Find<ComboBox>(screens, c => System.Windows.Automation.AutomationProperties.GetName(c) == "Clip panel mode");
            mode.SelectedIndex = 1; UiProbe.Pump();
            check(coordinator.Views.Count == 2 && state.Settings.MirrorDocks, "The real Screens mode picker enables clip copies immediately");
            var mirrorDownloads = UiProbe.Find<CheckBox>(screens, c => System.Windows.Automation.AutomationProperties.GetName(c) == "Duplicate Downloads on every display");
            UiProbe.Click(mirrorDownloads); UiProbe.Pump();
            check(state.Settings.MirrorDownloads && coordinator.DownloadsViews.Count == 2,
                "The real Screens checkbox independently enables Downloads copies immediately");
            UiProbe.CaptureWindow(settingsWindow, Path.Combine(output, "settings-multiple-screens.png"));
            settingsWindow.Close();
            state.Settings.MirrorDownloads = false; session.Change();
            session.TrySave(); var saved = new StateStore(store.Root).Load();
            check(!saved.Settings.MirrorDownloads && saved.Settings.MirrorDocks && saved.Settings.DownloadsLayouts.Count == 2 &&
                saved.Settings.DownloadsLayout.DisplayId == to.Id && saved.Settings.Docks.Count == 2,
                "Restart retains independent mirror choices, the moved Downloads home and both screens' layouts");
            coordinator.CloseDownloads(); check(coordinator.DownloadsViews.Count == 0, "Closing Downloads disposes every linked window");
            state.Settings.MirrorDownloads = true; coordinator.OpenDownloads(); coordinator.CloseDownloads();
            check(coordinator.DownloadsViews.Count == 0, "Repeated enabling/closing does not retain hidden Downloads windows");
            state.Settings.MirrorDocks = false; state.Settings.HomeDisplayId = a.Id;
            state.Settings.Docks.Clear(); state.Settings.Docks.Add(new DockLayout { DisplayId = a.Id, Enabled = true });
            state.Settings.Docks.AddRange(Enumerable.Range(0, 63).Select(i => new DockLayout { DisplayId = "kept-offline-" + i }));
            session.Change(); UiProbe.Pump();
            var overflowDock = coordinator.Views.Single();
            UiProbe.DragFrom(UiProbe.Find<Border>(overflowDock, border => border.Name == "Body").PointToScreen(new Point(5, 5)), new Point(b.Left + 70, b.Top + 120));
            var liveOffset = overflowDock.Assignment!.Layout.OffsetX;
            coordinator.RefreshDisplays(); UiProbe.Pump();
            check(overflowDock.Assignment.Temporary && state.Settings.HomeDisplayId == b.Id &&
                ReferenceEquals(overflowDock, coordinator.Views.Single()) && overflowDock.Assignment.Layout.OffsetX == liveOffset && state.Settings.Docks.Count == 64,
                "At 64 retained layouts an unrecorded destination stays movable with a stable temporary anchor and no erased records");
            state.Settings.DownloadsLayouts.Clear();
            state.Settings.DownloadsLayouts.AddRange(Enumerable.Range(0, 64).Select(i => new DockLayout { DisplayId = "kept-downloads-" + i }));
            state.Settings.MirrorDownloads = true; coordinator.OpenDownloads(); UiProbe.Pump();
            var overflowDownloads = coordinator.DownloadsViews.Single(v => v.Assignment!.Display.Id == b.Id);
            UiProbe.Hover(overflowDownloads.StackButton);
            var edge = (Border)overflowDownloads.FindName("CapsuleShell");
            UiProbe.DragFrom(edge.PointToScreen(new Point(3, edge.ActualHeight / 2)), new Point(b.Left + b.Width - 80, b.Top + b.Height - 90));
            var liveDownloadsOffset = overflowDownloads.Assignment!.Layout.OffsetY;
            coordinator.RefreshDisplays(); UiProbe.Pump();
            check(overflowDownloads.Assignment.Temporary && overflowDownloads.Assignment.Layout.OffsetY == liveDownloadsOffset && state.Settings.DownloadsLayouts.Count == 64,
                "Temporary Downloads positions survive refresh at the metadata limit without erasing older display records");
            check(session.Report().Contains("Display move") && session.Report().Contains("scale=") &&
                !session.Report().Contains("Shared multi-monitor clip") && !session.Report().Contains(file),
                "Copyable diagnostics include bounded move geometry and DPI without clip contents or download paths");
            coordinator.CloseDownloads();
        }
        finally { library.Hide(); oldDock.ShowDock(); oldLibrary.ShowShelf(); }
    }
}
