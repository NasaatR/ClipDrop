using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

public sealed class SettingsWindow : Window
{
    private sealed record Preset(string Label, AppearanceStyle? Style = null, string? SavedId = null)
    { public override string ToString() => Label; }
    private readonly ShelfSettings _settings;
    private readonly Action _changed;
    private readonly List<Action> _syncControls = new();
    private readonly StackPanel _panel = new() { Margin = new Thickness(18, 0, 18, 20) };
    private readonly ComboBox _presets = new();
    private readonly TextBox _presetName = new() { MaxLength = 40, ToolTip = "Name for your saved style" };
    private readonly Button _deleteStyle = new() { Content = "Delete", ToolTip = "Delete the selected saved style" };
    private readonly TextBlock _notice = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) };
    private bool _sync;

    public SettingsWindow(ShelfSettings settings, Action changed, Func<bool> save, Func<string> report, string dataRoot)
    {
        _settings = settings; _changed = changed;
        Title = "ClipDrop settings"; Width = 440; Height = 690; MinWidth = 400; MinHeight = 430;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var layout = new DockPanel(); Content = layout;
        var heading = new StackPanel { Margin = new Thickness(18, 14, 18, 12) };
        DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
        heading.Children.Add(new TextBlock { Text = "Settings", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        var nav = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1 }; heading.Children.Add(nav);
        var scroll = new ScrollViewer { Content = _panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var footer = new TextBlock { Text = $"ClipDrop {Diagnostics.Version} · Made by Nasaat", FontSize = 11, Margin = new Thickness(18, 8, 18, 12) };
        footer.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        layout.Children.Add(scroll);
        TextBlock Section(string name)
        {
            var title = new TextBlock { Text = name, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 8) };
            _panel.Children.Add(title);
            var jump = new Button { Content = name, Padding = new Thickness(9, 5, 9, 5), MinHeight = 28 };
            jump.Click += (_, _) => title.BringIntoView(); nav.Children.Add(jump); return title;
        }
        Section("Style");
        _presets.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Style preset");
        _presets.SelectionChanged += (_, _) => {
            _deleteStyle.IsEnabled = (_presets.SelectedItem as Preset)?.SavedId is not null;
            if (_sync || _presets.SelectedItem is not Preset { Style: { } style }) return;
            ApplyStyle(style);
        };
        Row("Preset", _presets);
        var names = new Grid(); names.ColumnDefinitions.Add(new ColumnDefinition()); names.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); names.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _presetName.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Style name");
        names.Children.Add(_presetName);
        var saveStyle = new Button { Content = "Save", ToolTip = "Save current appearance as a named style" };
        Grid.SetColumn(saveStyle, 1); names.Children.Add(saveStyle); Grid.SetColumn(_deleteStyle, 2); names.Children.Add(_deleteStyle);
        Row("Save as", names);
        saveStyle.Click += (_, _) => {
            var name = _presetName.Text.Trim();
            if (name.Length == 0) { _notice.Text = "Enter a name to save this style."; _presetName.Focus(); return; }
            var existing = settings.SavedStyles.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is null && settings.SavedStyles.Count >= 20) { _notice.Text = "20 styles saved. Reuse a name to update one."; return; }
            existing ??= new SavedStyle { Name = name };
            if (!settings.SavedStyles.Contains(existing)) settings.SavedStyles.Add(existing);
            existing.Style = settings.Snapshot(); existing.Name = name;
            RebuildPresets(existing.Id); _notice.Text = "Style saved"; _changed();
        };
        _deleteStyle.Click += (_, _) => {
            if (_presets.SelectedItem is not Preset { SavedId: { } id }) return;
            settings.SavedStyles.RemoveAll(s => s.Id == id); RebuildPresets(); _notice.Text = "Style deleted · current appearance kept"; _changed();
        };
        _notice.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _panel.Children.Add(_notice);
        Choice("App theme", () => settings.Theme, new[] { "Dark", "Light", "Midnight" }, new[] { "Graphite", "Paper", "Midnight" }, value => settings.Theme = value);
        ColorField("Accent", () => settings.AccentColor, value => settings.AccentColor = value);
        Check("Animations", () => settings.AnimationsEnabled, value => settings.AnimationsEnabled = value, false);
        Hint("Windows animation preferences are respected. Colors and changes preview live.");
        Section("Dock");
        Choice("Palette", () => settings.DockPalette, new[] { "Shared", "Dark", "Light", "Midnight" }, new[] { "App theme", "Graphite", "Paper", "Midnight" }, value => settings.DockPalette = value);
        ColorField("Background", () => settings.DockColor, value => settings.DockColor = value);
        Range("Button size", () => settings.DockButtonSize, 30, 48, value => settings.DockButtonSize = value);
        Range("Spacing", () => settings.DockSpacing, 0, 12, value => settings.DockSpacing = value);
        Range("Corners", () => settings.DockRadius, 0, 24, value => settings.DockRadius = value);
        Range("Opacity", () => settings.DockOpacity, 0, 100, value => settings.DockOpacity = value);
        Hint("Opacity affects the dock background. Buttons and content stay readable.");
        Section("Library");
        Check("Follow dock", () => settings.LibraryFollowsDock, value => settings.LibraryFollowsDock = value, false);
        Hint("Move Library to detach it. Use its link button to attach again.");
        Choice("Palette", () => settings.LibraryPalette, new[] { "Shared", "Dark", "Light", "Midnight" }, new[] { "App theme", "Graphite", "Paper", "Midnight" }, value => settings.LibraryPalette = value);
        ColorField("Background", () => settings.LibraryColor, value => settings.LibraryColor = value);
        Range("Text size", () => settings.FontSize, 11, 18, value => settings.FontSize = value);
        Range("Row spacing", () => settings.RowSpacing, 0, 12, value => settings.RowSpacing = value);
        Range("Row corners", () => settings.CornerRadius, 0, 16, value => settings.CornerRadius = value);
        var reset = new Button { Content = "Reset appearance", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
        reset.Click += (_, _) => { ApplyStyle(new AppearanceStyle()); RebuildPresets(); _notice.Text = "Appearance reset · saved styles kept"; }; _panel.Children.Add(reset);
        Section("App");
        Check("Always on top", () => settings.AlwaysOnTop, value => settings.AlwaysOnTop = value, false);
        Check("Start hidden in tray", () => settings.StartHidden, value => settings.StartHidden = value, false);
        Check("Alt+F4 hides to tray", () => settings.CloseToTray, value => settings.CloseToTray = value, false);
        Check("Shake to show (experimental)", () => settings.ShakeToShow, value => settings.ShakeToShow = value, false);
        Hint("Shake detection watches left-button movement.\nCtrl+Alt+Space: dock · Ctrl+Alt+L: Library");
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; _panel.Children.Add(actions);
        var retry = new Button { Content = "Save now" };
        retry.Click += (_, _) => _notice.Text = save() ? "Library saved" : "Save failed. Changes remain open; copy diagnostics.";
        actions.Children.Add(retry);
        var diagnostics = new Button { Content = "Copy diagnostics" };
        diagnostics.Click += (_, _) => { try { Clipboard.SetText(report()); diagnostics.Content = "Copied"; } catch (System.Runtime.InteropServices.ExternalException) { diagnostics.Content = "Clipboard busy · retry"; } }; actions.Children.Add(diagnostics);
        var folder = new Button { Content = "Data folder" };
        folder.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(dataRoot) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "ClipDrop"); } }; actions.Children.Add(folder);
        Hint("Assets and exports remain on disk for reliable transfers.\nNo clipboard monitoring, accounts or network requests.");
        RebuildPresets();
    }
    private void ApplyStyle(AppearanceStyle style)
    {
        _sync = true;
        try { style.CopyTo(_settings); _changed(); foreach (var sync in _syncControls) sync(); }
        finally { _sync = false; }
    }
    private void Change(bool appearance = true)
    {
        if (_sync) return;
        if (appearance) { _sync = true; _presets.SelectedIndex = 0; _sync = false; }
        _changed();
    }
    private void RebuildPresets(string? selected = null)
    {
        _sync = true;
        var choices = new List<Preset> { new("Custom") };
        choices.AddRange(AppearanceStyle.BuiltIns.Select(p => new Preset(p.Name, p.Style)));
        choices.AddRange(_settings.SavedStyles.Select(p => new Preset(p.Name + " · saved", p.Style, p.Id)));
        _presets.ItemsSource = choices; _presets.SelectedItem = choices.FirstOrDefault(p => p.SavedId == selected && selected is not null) ?? choices[0];
        _deleteStyle.IsEnabled = selected is not null; _sync = false;
    }
    private void Row(string label, FrameworkElement control)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) }); row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(control, 1); row.Children.Add(control); _panel.Children.Add(row);
    }
    private void Hint(string text)
    {
        var hint = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 5, 0, 5) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _panel.Children.Add(hint);
    }
    private void Check(string label, Func<bool> read, Action<bool> write, bool appearance)
    {
        var check = new CheckBox { Content = label, IsChecked = read(), Margin = new Thickness(0, 5, 0, 5) };
        check.Click += (_, _) => { write(check.IsChecked == true); Change(appearance); }; _panel.Children.Add(check);
        _syncControls.Add(() => check.IsChecked = read());
    }
    private void Choice(string label, Func<string> read, string[] values, string[] labels, Action<string> write)
    {
        var combo = new ComboBox { ItemsSource = labels, SelectedIndex = Math.Max(0, Array.IndexOf(values, read())) };
        System.Windows.Automation.AutomationProperties.SetName(combo, label);
        combo.SelectionChanged += (_, _) => { if (!_sync && combo.SelectedIndex >= 0) { write(values[combo.SelectedIndex]); Change(); } };
        Row(label, combo); _syncControls.Add(() => combo.SelectedIndex = Math.Max(0, Array.IndexOf(values, read())));
    }
    private void Range(string name, Func<double> read, double min, double max, Action<double> write)
    {
        var panel = new DockPanel(); var number = new TextBlock { Width = 30, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(number, Dock.Right); panel.Children.Add(number);
        var slider = new Slider { Minimum = min, Maximum = max, IsSnapToTickEnabled = true, TickFrequency = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        void Sync() { slider.Value = Appearance.Bound(read(), min, max, min); number.Text = slider.Value.ToString("0"); }
        Sync(); slider.ValueChanged += (_, _) => { number.Text = slider.Value.ToString("0"); if (!_sync) { write(slider.Value); Change(); } };
        System.Windows.Automation.AutomationProperties.SetName(slider, name); panel.Children.Add(slider); Row(name, panel); _syncControls.Add(Sync);
    }
    private void ColorField(string name, Func<string> read, Action<string> write)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new TextBox { Text = read(), ToolTip = "#RRGGBB · leave empty for automatic", MaxLength = 12 };
        System.Windows.Automation.AutomationProperties.SetName(text, name + " color"); grid.Children.Add(text);
        var clear = new Button { Content = "Auto", ToolTip = "Use palette color", Padding = new Thickness(8, 4, 8, 4) }; Grid.SetColumn(clear, 1); grid.Children.Add(clear);
        void Commit()
        {
            if (_sync) return;
            if (!AppearanceStyle.TryColor(text.Text, out var color)) { text.BorderBrush = Brushes.IndianRed; text.ToolTip = "Enter #RRGGBB, or clear for automatic."; return; }
            text.SetResourceReference(BorderBrushProperty, "Stroke"); text.ToolTip = "#RRGGBB · leave empty for automatic";
            text.Text = color; if (read() == color) return; write(color); Change();
        }
        text.LostKeyboardFocus += (_, _) => Commit();
        text.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { Commit(); e.Handled = true; } };
        clear.Click += (_, _) => { text.Clear(); Commit(); };
        _syncControls.Add(() => { text.Text = read(); text.SetResourceReference(BorderBrushProperty, "Stroke"); }); Row(name, grid);
    }
}
