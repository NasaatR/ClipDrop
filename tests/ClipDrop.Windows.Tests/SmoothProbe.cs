using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Services;
using ClipDrop.Windows.Views;

internal static class SmoothProbe
{
    public static void Run(MainWindow library, DockWindow original, string root, string output, Action<bool, string> check)
    {
        library.Hide(); original.HideDock();
        var store = new StateStore(Path.Combine(root, "smooth-input")); var state = store.Load();
        state.Settings.AnimationsEnabled = true; state.Settings.MotionDuration = 500; state.Settings.CollapseWhenIdle = false;
        using var session = new ShelfSession(store, state);
        var host = new Window { Width = 360, Height = 130, Left = 60, Top = 70, ShowInTaskbar = false, ShowActivated = false, Topmost = true, Content = new Border { Background = Brushes.SlateBlue } };
        host.Show(); Appearance.Apply(state.Settings);
        var area = WindowPlacement.WorkArea(host); UiProbe.MoveTo(new Point(area.Right - 5, area.Bottom - 5));
        using var presence = new DockPresence(host, () => state.Settings, _ => { });
        var descriptor = DependencyPropertyDescriptor.FromProperty(UIElement.OpacityProperty, typeof(Window)) ?? throw new InvalidOperationException("Opacity descriptor unavailable");
        var samples = new List<(double Time, double Opacity)>(); var clock = new Stopwatch();
        EventHandler sample = (_, _) => samples.Add((clock.Elapsed.TotalMilliseconds, host.Opacity));
        DockWindow? dock = null; SettingsWindow? settings = null;
        try
        {
            presence.Busy(true); UiProbe.Pump(500);
            if (Motion.Allowed)
            {
                clock.Start(); descriptor.AddValueChanged(host, sample);
                presence.Busy(false);
                check(UiProbe.Until(() => host.Opacity <= .201, 1800), "Idle fading reaches the configured opacity");
                descriptor.RemoveValueChanged(host, sample);
                check(samples.Count > 6 && samples.Zip(samples.Skip(1)).All(pair => pair.Second.Opacity <= pair.First.Opacity + .002) && samples.Where(s => s.Time < 300).All(s => s.Opacity > .25),
                    "Idle opacity decreases continuously without an immediate target flash or reverse jump");
                presence.Busy(true); UiProbe.Pump(500); presence.Busy(false); UiProbe.Pump(180);
                var interrupted = host.Opacity;
                samples.Clear(); descriptor.AddValueChanged(host, sample); presence.Busy(true);
                UiProbe.Pump(1000); descriptor.RemoveValueChanged(host, sample);
                check(host.Opacity == 1 && samples.All(s => s.Opacity >= interrupted - .002) && samples.Zip(samples.Skip(1)).All(pair => pair.Second.Opacity >= pair.First.Opacity - .002),
                    "Re-entering during an idle fade reverses from the displayed value without a stale completion jump");
                host.CaptureMouse(); presence.Busy(false); UiProbe.Pump(200);
                check(host.Opacity == 1, "A captured press keeps the panel visible even when the pointer is outside");
                host.ReleaseMouseCapture(); check(UiProbe.Until(() => host.Opacity <= .201, 1800), "Releasing outside capture resumes fading without another pointer event");
            }
            presence.Busy(true); UiProbe.Pump(400);
            state.Settings.CollapseWhenIdle = true; state.Settings.DockLeft = 80; state.Settings.DockTop = 230;
            session.Add(Enumerable.Range(0, 4).Select(i => ShelfItem.FromText("Smooth row " + i)));
            dock = new DockWindow(session, () => { }, () => { }); dock.ShowDock(); UiProbe.Hover(dock.QuickSave); UiProbe.Pump(550);
            var strip = (DockStrip)dock.FindName("Strip"); var details = (FrameworkElement)strip.FindName("Details");
            var native = WindowPlacement.PixelBounds(dock); double? hiddenOpacity = null;
            details.IsVisibleChanged += (_, _) => { if (!details.IsVisible && dock.IsVisible) hiddenOpacity = details.Opacity; };
            UiProbe.MoveTo(new Point(area.Right - 5, area.Bottom - 5));
            check(UiProbe.Until(() => strip.IsCompact, 1500), "Passive closing starts the compact transition without a second long delay");
            if (Motion.Allowed) check(details.IsVisible && !details.IsHitTestVisible, "Closing content fades while immediately releasing interaction");
            check(UiProbe.Until(() => !details.IsVisible, 1500) && (!Motion.Allowed || hiddenOpacity is double opacity && opacity <= .01) && WindowPlacement.PixelBounds(dock) == native,
                "Compact content is hidden only after it is transparent and native geometry stays stable");
            UiProbe.Hover(dock.QuickSave); UiProbe.Pump(90); UiProbe.MoveTo(new Point(area.Right - 5, area.Bottom - 5)); UiProbe.Pump(110); UiProbe.Hover(dock.QuickSave); UiProbe.Pump(650);
            check(!strip.IsCompact && details.IsVisible && details.IsHitTestVisible && details.Opacity == 1,
                "A quick return cancels pending conceal and keeps the reopened panel available");
            var cached = session.DockTiles(); state.Settings.DockOpacity = 84; session.ChangeSettings();
            check(ReferenceEquals(cached, session.DockTiles()), "Settings updates preserve the cached clip index and tile collection");
            session.Add(new[] { ShelfItem.FromText("A real content update") });
            check(!ReferenceEquals(cached, session.DockTiles()), "Actual content changes still invalidate the clip cache");
            settings = new SettingsWindow(state.Settings, session.ChangeSettings, session.TrySave, session.Report, store.Root);
            settings.ShowSection("Dock"); settings.Show(); UiProbe.Pump();
            var field = UiProbe.Find<NumberField>(settings, f => f.Label == "Opacity");
            check(field.Rail.ActualHeight >= 36 && field.Input.IsVisible, "Settings offer a large slider rail and exact numeric entry together");
            var values = new List<double>(); var movedWhileHeld = false;
            field.Rail.ValueChanged += (_, _) => { values.Add(field.Rail.Value); movedWhileHeld |= field.Rail.IsScrubbing && field.Rail.IsMouseCaptured; };
            var start = field.Rail.PointToScreen(new Point(14 + (field.Rail.ActualWidth - 28) * .15, 18));
            var end = field.Rail.PointToScreen(new Point(14 + (field.Rail.ActualWidth - 28) * .8, 18));
            UiProbe.DragFrom(start, end, steps: 30, frameDelayMs: 5);
            check(movedWhileHeld && values.Distinct().Count() >= 6 && Math.Abs(field.Rail.Value - 80) < 2 && state.Settings.DockOpacity is >= 78 and <= 82,
                "Pressing anywhere on the rail and dragging follows the pointer continuously and commits the release value");
            var local = 20d; var writes = 0;
            var burst = new NumberField("Coalescing", () => local, 0, 100, value => { local = value; writes++; });
            for (var i = 21; i <= 90; i++) burst.Rail.Value = i;
            check(writes == 0 && UiProbe.Until(() => writes > 0) && writes == 1 && local == 90,
                "Rapid rail changes coalesce expensive previews and retain the newest value");
            UiProbe.CaptureWindow(settings, Path.Combine(output, "settings-dock-smooth-sliders.png"));
            settings.ShowSection("Downloads"); settings.Width = 560; settings.Height = 540; state.Settings.Theme = "Light"; session.ChangeSettings(); UiProbe.Pump();
            var scroll = UiProbe.Find<ScrollViewer>(settings, s => s.Content is StackPanel && s.ScrollableHeight > 0);
            var bar = UiProbe.Find<ScrollBar>(scroll, s => s.Orientation == Orientation.Vertical && s.IsVisible);
            var thumb = UiProbe.Find<Thumb>(bar, _ => true);
            var oldOffset = scroll.VerticalOffset;
            UiProbe.DragTo(thumb, thumb.PointToScreen(new Point(thumb.ActualWidth / 2, thumb.ActualHeight / 2)) + new Vector(0, 60));
            check(scroll.VerticalOffset > oldOffset + 10, "The visible scrollbar thumb tracks a real vertical drag with bound range and viewport");
            UiProbe.CaptureWindow(settings, Path.Combine(output, "settings-sliders-narrow-light.png"));
            settings.Close(); settings = null;
            session.TrySave(); check(new StateStore(store.Root).Load().Settings.DockOpacity == state.Settings.DockOpacity,
                "The final slider value survives persistence after Settings closes");
        }
        finally
        {
            descriptor.RemoveValueChanged(host, sample); settings?.Close(); dock?.DisposeView(); host.Close();
            IconAssets.Initialize(root); original.ShowDock(); library.ShowShelf();
        }
    }
}
