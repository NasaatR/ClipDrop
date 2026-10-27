using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(ShelfSettings settings, Action changed, Func<bool> save, Func<string> report, string dataRoot)
    {
        Title = "ClipDrop settings"; Width = 370; Height = 620; MinWidth = 340; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(20) };
        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.SetResourceReference(BackgroundProperty, "Surface");
        Content = scroll;
        panel.Children.Add(new TextBlock { Text = "Settings", FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        void Check(string label, bool value, Action<bool> update)
        {
            var check = new CheckBox { Content = label, IsChecked = value };
            check.Click += (_, _) => { update(check.IsChecked == true); changed(); };
            panel.Children.Add(check);
        }
        Check("Always on top", settings.AlwaysOnTop, value => settings.AlwaysOnTop = value);
        Check("Start hidden in the tray", settings.StartHidden, value => settings.StartHidden = value);
        Check("Alt+F4 hides to tray", settings.CloseToTray, value => settings.CloseToTray = value);
        Check("Shake to show (experimental)", settings.ShakeToShow, value => settings.ShakeToShow = value);
        var hint = new TextBlock { Text = "Shake detection watches left-button movement.\nThe global shortcut is Ctrl+Alt+Space.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 2, 0, 12) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(hint);
        panel.Children.Add(new TextBlock { Text = "Appearance", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 8) });
        var themes = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var name in new[] { "Dark", "Light" })
        {
            var radio = new RadioButton { Content = name, GroupName = "Theme", IsChecked = settings.Theme == name, Margin = new Thickness(0, 0, 20, 0) };
            radio.SetResourceReference(ForegroundProperty, "Ink");
            radio.Checked += (_, _) => { settings.Theme = name; changed(); };
            themes.Children.Add(radio);
        }
        panel.Children.Add(themes);
        void Range(string name, double value, double minimum, double maximum, Action<double> update)
        {
            var label = new TextBlock { Text = $"{name}  {value:0}", Margin = new Thickness(0, 14, 0, 6) };
            var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum, IsSnapToTickEnabled = true, TickFrequency = 1 };
            slider.ValueChanged += (_, _) => { update(slider.Value); label.Text = $"{name}  {slider.Value:0}"; changed(); };
            System.Windows.Automation.AutomationProperties.SetName(slider, name);
            panel.Children.Add(label); panel.Children.Add(slider);
        }
        Range("Text size", settings.FontSize, 11, 18, value => settings.FontSize = value);
        Range("Row spacing", settings.RowSpacing, 0, 12, value => settings.RowSpacing = value);
        Range("Row corners", settings.CornerRadius, 0, 16, value => settings.CornerRadius = value);
        var reset = new Button { Content = "Reset appearance", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 16, 0, 8) };
        reset.Click += (_, _) => { settings.Theme = "Dark"; settings.FontSize = 13; settings.RowSpacing = 4; settings.CornerRadius = 8; changed(); Close(); };
        panel.Children.Add(reset);
        var retry = new Button { Content = "Save now / retry", HorizontalAlignment = HorizontalAlignment.Left };
        retry.Click += (_, _) => MessageBox.Show(save() ? "The current shelf is saved." : "Saving failed. Your changes remain open. Copy diagnostics before closing ClipDrop.", "ClipDrop");
        panel.Children.Add(retry);
        var diagnostics = new Button { Content = "Copy diagnostics", HorizontalAlignment = HorizontalAlignment.Left };
        diagnostics.Click += (_, _) => {
            try { Clipboard.SetText(report()); diagnostics.Content = "Diagnostics copied"; }
            catch (System.Runtime.InteropServices.ExternalException) { diagnostics.Content = "Clipboard busy — retry"; }
        };
        panel.Children.Add(diagnostics);
        var folder = new Button { Content = "Open data folder", HorizontalAlignment = HorizontalAlignment.Left };
        folder.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(dataRoot) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "ClipDrop"); } };
        panel.Children.Add(folder);
        var privacy = new TextBlock { Text = "Assets and export files stay on disk for reliable transfers.\nNo clipboard monitoring, accounts or network requests.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 12, 0, 0) };
        privacy.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(privacy);
        var credit = new TextBlock { Text = $"ClipDrop {Diagnostics.Version}\nMade by Nasaat", FontSize = 11, Margin = new Thickness(0, 16, 0, 0) };
        credit.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(credit);
    }
}
