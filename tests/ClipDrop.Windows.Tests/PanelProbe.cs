using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class PanelProbe
{
    private sealed class Displays : IDisplaySource
    {
        public IReadOnlyList<DisplayArea> Current = Array.Empty<DisplayArea>();
        public string? Target;
        public IReadOnlyList<DisplayArea> GetDisplays() => Current;
        public string? TargetDisplay(IntPtr externalWindow) => Target;
    }
    public static void Run(MainWindow library, DockWindow dock, string root, string output, Action<bool, string> check, Action<FrameworkElement, string> render)
    {
        IconAssets.Initialize(root);
        var session = library.Session; session.State.Settings.AnimationsEnabled = false;
        var first = session.State.Items.First(i => i.IsPinned);
        var before = UiProbe.Find<Button>(dock.Pins, b => b.DataContext is ItemRow r && r.Item.Id == first.Id);
        var added = ShelfItem.FromText("Appended at the right"); session.Add(new[] { added }); UiProbe.Pump();
        check(((ItemRow)dock.Pins.Items[dock.Pins.Items.Count - 1]).Item.Id == added.Id,
            "New pins append on the right rather than shifting earlier pins");
        var kept = UiProbe.Find<Button>(dock.Pins, b => b.DataContext is ItemRow r && r.Item.Id == first.Id);
        check(ReferenceEquals(before, kept), "Appending pins preserves existing WPF button instances");
        session.Add(new[] { ShelfItem.FromText(added.Content) });
        check(session.State.Items.Count(i => i.Content == added.Content) == 1, "Re-saving a duplicate does not move or duplicate the pin");
        session.TogglePin(new[] { first }); session.TogglePin(new[] { first });
        check(((ItemRow)dock.Pins.Items[dock.Pins.Items.Count - 1]).Item.Id == first.Id, "Re-pinning an existing clip appends it at the right");
        var emoji = UiProbe.Find<AppIcon>(dock.QuickSave, i => i.Kind == "Save");
        check(UiProbe.Find<Image>(emoji, _ => true).Source is DrawingImage || UiProbe.Find<TextBlock>(emoji, t => t.Text == "📥").Visibility == Visibility.Visible, "Dock actions use system emoji by default");
        var colorEmoji = UiProbe.Find<Image>(emoji, _ => true).Source as DrawingImage;
        check(colorEmoji?.Drawing is DrawingGroup { Children.Count: > 1 }, "Installed Windows emoji renders its actual colored font layers");
        var png = Path.Combine(root, "custom-icon.png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { 255, 0, 0, 255, 0, 0, 255, 255, 0, 255, 0, 255, 255, 255, 255, 0 }, 8)));
        using (var file = File.Create(png)) encoder.Save(file);
        var hash = IconAssets.Import(png); File.Delete(png); session.State.Settings.PngIcons["Save"] = hash; session.Change(); UiProbe.Pump();
        check(UiProbe.Find<Image>(emoji, _ => true).Source is BitmapSource { IsFrozen: true } && UiProbe.Find<TextBlock>(emoji, _ => true).Visibility == Visibility.Collapsed,
            "A managed custom PNG replaces emoji without depending on its original file");
        session.TrySave(); check(new StateStore(root).Load().Settings.PngIcons["Save"] == hash, "Custom PNG choice survives storage roundtrip");
        session.State.Settings.PngIcons["Save"] = new string('F', 64); session.Change(); UiProbe.Pump();
        check(UiProbe.Find<Image>(emoji, _ => true).Source is DrawingImage || UiProbe.Find<TextBlock>(emoji, _ => true).Visibility == Visibility.Visible, "Missing custom PNG falls back to the emoji");
        session.State.Settings.PngIcons.Clear(); session.Change();
        var invalid = Path.Combine(root, "invalid.png"); File.WriteAllText(invalid, "not a png");
        var rejected = false; try { IconAssets.Import(invalid); } catch (Exception ex) when (ex is FileFormatException or NotSupportedException or InvalidDataException) { rejected = true; }
        check(rejected && IconAssets.Get("../../escape") is null, "Invalid PNG and managed icon path traversal are rejected");
        render((FrameworkElement)dock.Content, Path.Combine(output, "dock-emoji.png"));
        dock.Hide(); library.Hide();
        var area = WindowPlacement.WorkArea(dock); var scale = WindowPlacement.Scale(dock);
        // Real HWNDs on one runner display, with two logical test assignments.
        // This tests coordinator behavior, not physical mixed-DPI hardware.
        var a = new DisplayArea("test-a", "Test A", area.Left * scale, area.Top * scale, area.Width * scale, area.Height * scale, true);
        var b = a with { Id = "test-b", Label = "Test B", Primary = false };
        var source = new Displays { Current = new[] { a, b }, Target = b.Id };
        var settings = session.State.Settings; settings.HomeDisplayId = a.Id; settings.Docks.Clear(); settings.MirrorDocks = true;
        DockPlanning.Plan(settings, source.Current); settings.Docks.Single(d => d.DisplayId == b.Id).Enabled = true;
        using (var coordinator = new DockCoordinator(session, library, displays: source))
        {
            UiProbe.Pump(); check(coordinator.Views.Count == 2 && coordinator.Views.All(v => ReferenceEquals(v.Session, session)),
                "Two real dock HWNDs share one session through logical monitor assignments");
            var one = coordinator.Views.Single(v => v.Assignment!.Display.Id == a.Id);
            var two = coordinator.Views.Single(v => v.Assignment!.Display.Id == b.Id);
            check(WindowPlacement.PixelBounds(one).Left >= a.Left && WindowPlacement.PixelBounds(two).Right <= b.Left + b.Width + 1,
                "Native dock placement stays within its assigned physical work area");
            one.Hide(); UiProbe.Pump(); check(!one.IsVisible && two.IsVisible, "Hiding one dock keeps the second dock visible");
            coordinator.ToggleTarget(); check(!two.IsVisible, "Monitor shortcut routing toggles the target display's dock");
            coordinator.ShowAll(); coordinator.ShowLibrary(one); coordinator.ShowLibrary(two); UiProbe.Pump();
            var libraryBefore = WindowPlacement.PixelBounds(library).Location;
            WindowPlacement.MovePixels(one, WindowPlacement.PixelBounds(one).Left + 20, WindowPlacement.PixelBounds(one).Top - 20); UiProbe.Pump();
            check(WindowPlacement.PixelBounds(library).Location == libraryBefore, "Library detaches event subscriptions from its previous dock anchor");
            var mirrored = ShelfItem.FromText("Shared mirrored clip"); session.Add(new[] { mirrored }); UiProbe.Pump();
            check(coordinator.Views.All(v => v.Pins.Items.Cast<ItemRow>().Any(r => r.Item.Id == mirrored.Id)), "A save from the shared session updates both dock views once");
            session.Remove(new List<ShelfItem> { mirrored });
            check(session.State.Trash.Count(t => t.Item.Id == mirrored.Id) == 1 && coordinator.Views.All(v => v.Pins.Items.Cast<ItemRow>().All(r => r.Item.Id != mirrored.Id)),
                "Removing a mirrored clip creates one Trash entry and updates both views");
            settings.MirrorDocks = false; settings.HomeDisplayId = b.Id; session.Change();
            source.Current = new[] { a }; coordinator.RefreshDisplays();
            check(coordinator.Views.Count == 1 && coordinator.Views.Single().Assignment!.Temporary && settings.Docks.Any(d => d.DisplayId == b.Id),
                "Disconnect replaces the missing dock with one fallback while preserving its layout");
            source.Current = new[] { b, a }; coordinator.RefreshDisplays(); coordinator.RefreshDisplays();
            check(coordinator.Views.Count == 1 && coordinator.Views.Single().Assignment!.Display.Id == b.Id && !coordinator.Views.Single().Assignment!.Temporary,
                "Repeated reconnect restores the enabled home dock without duplicate HWNDs");
            var screenSettings = new SettingsWindow(settings, () => session.Change(), session.TrySave, session.Report, session.Root, coordinator);
            screenSettings.Show(); UiProbe.Pump();
            var screens = UiProbe.Find<MonitorSettings>(screenSettings, _ => true); screens.BringIntoView(); UiProbe.Pump();
            render((FrameworkElement)screenSettings.Content, Path.Combine(output, "settings-screens.png")); screenSettings.Close();
        }
        settings.HomeDisplayId = ""; settings.Docks.Clear(); settings.MirrorDocks = false; library.Docks = null; library.AttachDock(dock);
        session.Remove(new List<ShelfItem> { added }); session.UndoRemove(); dock.ShowDock(); library.ShowShelf();
    }
}
