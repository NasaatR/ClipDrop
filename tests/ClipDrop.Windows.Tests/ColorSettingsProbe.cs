using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows;
using ClipDrop.Windows.Views;

internal static class ColorSettingsProbe
{
    public static void Run(MainWindow library, string output, Action<bool, string> check)
    {
        var state = library.Session.State.Settings; var saved = state.Snapshot();
        var settings = new SettingsWindow(state, () => library.Session.Change(), () => true, () => "QA", library.Session.Root) { Owner = library };
        try
        {
            settings.Show(); settings.Activate(); UiProbe.Pump();
            var pick = UiProbe.Find<Button>(settings, b => AutomationProperties.GetName(b) == "Shared background color picker");
            UiProbe.ClickAt(pick.PointToScreen(new Point(pick.ActualWidth / 2, pick.ActualHeight / 2)));
            check(pick.ContextMenu is { IsOpen: true }, "Shared background swatch opens the real quick palette");
            var swatch = UiProbe.Find<Button>(pick.ContextMenu!, b => AutomationProperties.GetName(b) == "Use #2563EB");
            UiProbe.ClickAt(swatch.PointToScreen(new Point(swatch.ActualWidth / 2, swatch.ActualHeight / 2)));
            check(state.SharedBackgroundColor == "#2563EB" && !pick.ContextMenu!.IsOpen && settings.Background is SolidColorBrush bg && bg.Color == (Color)ColorConverter.ConvertFromString("#2563EB"),
                "Native palette click closes popup and updates the shared live surface");
            var input = UiProbe.Find<TextBox>(settings, t => AutomationProperties.GetName(t) == "Shared background color");
            input.Focus(); input.Text = "#GG0000"; UiProbe.PressKey(0x0D);
            check(state.SharedBackgroundColor == "#2563EB" && input.BorderBrush == Brushes.IndianRed, "Invalid HEX remains a draft without changing shared appearance");
            UiProbe.Escape(); check(input.Text == "#2563EB", "Escape restores the saved color after an invalid draft");
            state.DockPalette = "Light"; state.DockColor = "#F5F5F7"; state.LibraryPalette = "Midnight";
            UiProbe.Find<Button>(settings, b => b.Content as string == "Use shared colors everywhere").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(state.DockPalette == "Shared" && state.LibraryPalette == "Shared" && state.DockColor == "" && state.LibraryColor == "" &&
                ((SolidColorBrush)settings.Preview.FindResource("Surface")).Color == ((SolidColorBrush)settings.FindResource("Surface")).Color,
                "Shared colors action clears local overrides and updates dock preview immediately");
            var appTheme = UiProbe.Find<ComboBox>(settings, c => AutomationProperties.GetName(c) == "App theme"); appTheme.SelectedIndex = 1;
            check(state.Theme == "Light" && state.SharedBackgroundColor == "" && input.Text == "", "Choosing a theme clears its custom background and synchronizes HEX");
            input.Focus(); input.Text = "#334155"; UiProbe.PressKey(0x0D);
            check(state.SharedBackgroundColor == "#334155", "Exact HEX commits through keyboard input");
            settings.Width = 500; settings.Height = 540; UiProbe.Pump();
            settings.ShowSection("Dock"); UiProbe.Pump();
            var spacing = UiProbe.Find<NumberField>(settings, n => n.Label == "Spacing");
            check(spacing.ActualWidth >= 230 && spacing.IsVisible && settings.Preview.IsVisible, "Narrow settings keep full-width sliders and a persistent appearance preview");
            UiProbe.CaptureWindow(settings, Path.Combine(output, "settings-colors-narrow.png"));
            settings.Width = 760; settings.Height = 700; settings.ShowSection("Style"); UiProbe.Pump();
            UiProbe.CaptureWindow(settings, Path.Combine(output, "settings-shared-colors.png"));
            UiProbe.Find<Button>(settings, b => b.Content as string == "Reset shared colors").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(state.SharedBackgroundColor == "" && state.AccentColor == "" && state.Theme == "Light", "Reset shared colors preserves the selected theme");
        }
        finally { settings.Close(); saved.CopyTo(state); library.Session.Change(); }
    }
}
