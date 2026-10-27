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
    private StackPanel _section = null!;
    private WrapPanel? _fields;
    private readonly List<FrameworkElement> _fieldBoxes = new();
    public DockStrip Preview { get; } = new();

    public SettingsWindow(ShelfSettings settings, Action changed, Func<bool> save, Func<string> report, string dataRoot, DockCoordinator? docks = null)
    {
        _settings = settings; _changed = () => { changed(); Preview.Apply(settings); }; _section = _panel;
        Title = "ClipDrop settings"; Width = 820; Height = 780; MinWidth = 560; MinHeight = 480;
        SetResourceReference(BackgroundProperty, "Surface");
        SetResourceReference(ForegroundProperty, "Ink");
        var buttons = new Style(typeof(Button), (Style)Application.Current.FindResource(typeof(Button)));
        buttons.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("Control")));
        buttons.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("Stroke")));
        buttons.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        buttons.Setters.Add(new Setter(Control.FontSizeProperty, 12d)); Resources[typeof(Button)] = buttons;
        _notice.Text = "Changes apply instantly";
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var layout = new DockPanel(); Content = layout;
        layout.SetResourceReference(Panel.BackgroundProperty, "Surface");
        var heading = new StackPanel { Margin = new Thickness(18, 14, 18, 12) };
        DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
        heading.Children.Add(new TextBlock { Text = "ClipDrop " + Diagnostics.Version, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        _notice.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); heading.Children.Add(_notice);
        var nav = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1 }; heading.Children.Add(nav);
        var scroll = new ScrollViewer { Content = _panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var footer = new TextBlock { Text = "Made by Nasaat", FontSize = 11, Margin = new Thickness(18, 8, 18, 12) };
        footer.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        layout.Children.Add(scroll);
        TextBlock Section(string name)
        {
            _section = new StackPanel(); _fields = null;
            var block = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 10), Child = _section };
            block.SetResourceReference(Border.BackgroundProperty, "Raised");
            block.SetResourceReference(Border.BorderBrushProperty, "Stroke"); block.BorderThickness = new Thickness(1);
            _panel.Children.Add(block);
            var title = new TextBlock { Text = name, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 9) };
            _section.Children.Add(title);
            var jump = new Button { Content = name, Padding = new Thickness(9, 5, 9, 5), MinHeight = 28 };
            jump.Background = Brushes.Transparent; jump.BorderThickness = new Thickness(0);
            jump.Click += (_, _) => block.BringIntoView(); nav.Children.Add(jump); return title;
        }
        Section("Style");
        var previewStage = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 12) };
        previewStage.SetResourceReference(Border.BackgroundProperty, "Surface");
        var previewLayout = new StackPanel(); previewStage.Child = previewLayout;
        var previewLabel = new TextBlock { Text = "Your dock · updates instantly", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
        previewLabel.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); previewLayout.Children.Add(previewLabel);
        previewLayout.Children.Add(new Viewbox { Child = Preview, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Center, MaxHeight = 110 });
        Preview.Apply(settings); Preview.SetPins(new[] { ShelfItem.FromText("Saved note"), ShelfItem.FromText("https://example.com") }, 2);
        _section.Children.Add(previewStage);
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

        Choice("App theme", () => settings.Theme, new[] { "Dark", "Light", "Midnight" }, new[] { "Graphite", "Paper", "Midnight" }, value => settings.Theme = value);
        ColorField("Accent", () => settings.AccentColor, value => settings.AccentColor = value);
        Check("Animations", () => settings.AnimationsEnabled, value => settings.AnimationsEnabled = value, false);
        Hint("Windows animation preferences are respected. Colors and changes preview live.");
        Section("Icons");
        Hint("Emoji by default. Replace any role with your own PNG; image/video thumbnails remain real previews.");
        var iconRole = new ComboBox { ItemsSource = AppIcon.Roles.Select(r => r.Label).ToArray(), SelectedIndex = 0 };
        System.Windows.Automation.AutomationProperties.SetName(iconRole, "Icon role"); Row("Icon", iconRole);
        var iconPreview = new AppIcon { Kind = "Save", Width = 36, Height = 36 };
        var iconStatus = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 0, 0, 0), TextWrapping = TextWrapping.Wrap };
        var iconLine = new StackPanel { Orientation = Orientation.Horizontal }; iconLine.Children.Add(iconPreview); iconLine.Children.Add(iconStatus);
        Row("Preview", iconLine);
        string Role() => AppIcon.Roles[Math.Max(0, iconRole.SelectedIndex)].Key;
        void SyncIcon() { iconPreview.Kind = Role(); iconStatus.Text = settings.PngIcons.ContainsKey(Role()) ? "Custom PNG · saved in ClipDrop" : "Emoji"; }
        iconRole.SelectionChanged += (_, _) => SyncIcon(); _syncControls.Add(SyncIcon); SyncIcon();
        var iconActions = new WrapPanel(); _section.Children.Add(iconActions);
        var chooseIcon = new Button { Content = "Choose PNG…" }; iconActions.Children.Add(chooseIcon);
        chooseIcon.Click += (_, _) => {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "PNG image|*.png", Title = "Choose an icon · up to 2048 × 2048" };
            if (picker.ShowDialog(this) != true) return;
            try { settings.PngIcons[Role()] = IconAssets.Import(picker.FileName); Change(); SyncIcon(); _notice.Text = "PNG saved · original file can be moved"; }
            catch (Exception ex) when (ex is System.IO.FileFormatException or System.IO.IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
            { _notice.Text = "Icon not changed · " + ex.Message; }
        };
        var resetIcon = new Button { Content = "Use emoji" }; iconActions.Children.Add(resetIcon);
        resetIcon.Click += (_, _) => { settings.PngIcons.Remove(Role()); Change(); SyncIcon(); };
        Hint("PNG limits: 5 MB, 2048 × 2048. Saved styles include icon choices. Reset appearance returns to emoji.");
        Section("Dock");
        Choice("Palette", () => settings.DockPalette, new[] { "Shared", "Dark", "Light", "Midnight" }, new[] { "App theme", "Graphite", "Paper", "Midnight" }, value => settings.DockPalette = value);
        ColorField("Background", () => settings.DockColor, value => settings.DockColor = value);
        Range("Button size", () => settings.DockButtonSize, 30, 48, value => settings.DockButtonSize = value);
        Range("Spacing", () => settings.DockSpacing, 0, 12, value => settings.DockSpacing = value);
        Range("Corners", () => settings.DockRadius, 0, 24, value => settings.DockRadius = value);
        Range("Opacity", () => settings.DockOpacity, 0, 100, value => settings.DockOpacity = value);
        Hint("Opacity affects the dock background. Buttons and content stay readable.");
        if (docks is not null)
        {
            Section("Screens"); var monitorSettings = new MonitorSettings(settings, docks, () => Change(false));
            _section.Children.Add(monitorSettings); Closed += (_, _) => monitorSettings.Dispose();
        }
        Section("Library");
        Check("Follow dock", () => settings.LibraryFollowsDock, value => settings.LibraryFollowsDock = value, false);
        Hint("Move Library to detach it. Use its link button to attach again.");
        Choice("Palette", () => settings.LibraryPalette, new[] { "Shared", "Dark", "Light", "Midnight" }, new[] { "App theme", "Graphite", "Paper", "Midnight" }, value => settings.LibraryPalette = value);
        ColorField("Background", () => settings.LibraryColor, value => settings.LibraryColor = value);
        Range("Text size", () => settings.FontSize, 11, 18, value => settings.FontSize = value);
        Range("Row spacing", () => settings.RowSpacing, 0, 12, value => settings.RowSpacing = value);
        Range("Row corners", () => settings.CornerRadius, 0, 16, value => settings.CornerRadius = value);
        var reset = new Button { Content = "Reset appearance", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
        reset.Click += (_, _) => { ApplyStyle(new AppearanceStyle()); RebuildPresets(); _notice.Text = "Appearance reset · saved styles kept"; }; _section.Children.Add(reset);
        Section("App");
        Check("Always on top", () => settings.AlwaysOnTop, value => settings.AlwaysOnTop = value, false);
        Check("Start hidden in tray", () => settings.StartHidden, value => settings.StartHidden = value, false);
        Check("Alt+F4 hides to tray", () => settings.CloseToTray, value => settings.CloseToTray = value, false);
        Check("Shake to show (experimental)", () => settings.ShakeToShow, value => settings.ShakeToShow = value, false);
        Hint("Shake detection watches left-button movement.\nCtrl+Alt+Space: dock · Ctrl+Alt+L: Library");
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; _section.Children.Add(actions);
        var retry = new Button { Content = "Save now" };
        retry.Click += (_, _) => _notice.Text = save() ? "Library saved" : "Save failed. Changes remain open; copy diagnostics.";
        actions.Children.Add(retry);
        var diagnostics = new Button { Content = "Copy diagnostics" };
        diagnostics.Click += (_, _) => { try { Clipboard.SetText(report()); diagnostics.Content = "Copied"; } catch (System.Runtime.InteropServices.ExternalException) { diagnostics.Content = "Clipboard busy · retry"; } }; actions.Children.Add(diagnostics);
        var folder = new Button { Content = "Data folder" };
        folder.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(dataRoot) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "ClipDrop"); } }; actions.Children.Add(folder);
        Hint("Assets and exports remain on disk for reliable transfers.\nNo clipboard monitoring, accounts or network requests.");
        RebuildPresets();
        void FitFields()
        {
            var width = Math.Max(210, (scroll.ActualWidth - 36 - 28 - 24 - 10) / 2);
            foreach (var field in _fieldBoxes) field.Width = width;
        }
        SizeChanged += (_, _) => FitFields(); Loaded += (_, _) => {
            var area = WindowPlacement.WorkArea(this);
            Width = Math.Max(MinWidth, Math.Min(Width, area.Width)); Height = Math.Max(MinHeight, Math.Min(Height, area.Height));
            WindowPlacement.Clamp(this); FitFields();
        };
    }
    public void SetStorageStatus(string text) => _notice.Text = text;
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
        _presets.ItemsSource = choices; _presets.SelectedItem = choices.FirstOrDefault(p => p.SavedId == selected && selected is not null) ?? choices.Skip(1).Reverse().FirstOrDefault(p => p.Style?.EquivalentTo(_settings) == true) ?? choices[0];
        _deleteStyle.IsEnabled = selected is not null; _sync = false;
    }
    private void Row(string label, FrameworkElement control)
    {
        if (_fields is null) { _fields = new WrapPanel { Margin = new Thickness(-6, 0, -6, 0) }; _section.Children.Add(_fields); }
        var field = new StackPanel { Margin = new Thickness(6, 4, 6, 7), Width = 325 };
        field.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 5) });
        control.MinHeight = 32; field.Children.Add(control); _fields.Children.Add(field); _fieldBoxes.Add(field);
    }

    private void Hint(string text)
    {
        var hint = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 5, 0, 5) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _section.Children.Add(hint);
    }
    private void Check(string label, Func<bool> read, Action<bool> write, bool appearance)
    {
        var check = new CheckBox { Content = label, IsChecked = read(), Margin = new Thickness(0, 5, 0, 5) };
        check.Click += (_, _) => { write(check.IsChecked == true); Change(appearance); }; _section.Children.Add(check);
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
