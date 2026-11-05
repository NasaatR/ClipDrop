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
    private readonly StackPanel _panel = new() { Margin = new Thickness(12, 0, 12, 12) };
    private readonly ComboBox _presets = new();
    private readonly TextBox _presetName = new() { MaxLength = 40, ToolTip = "Name for your saved style" };
    private readonly Button _deleteStyle = new() { Content = "Delete", ToolTip = "Delete the selected saved style" };
    private readonly TextBlock _notice = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) };
    private bool _sync;
    private bool _previewIdle;
    private bool _previewCompact;
    private StackPanel _section = null!;
    private readonly Dictionary<string, (Border Frame, Button Button)> _sections = new();
    private readonly ScrollViewer _scroll;
    public string SelectedSection { get; private set; } = "";
    private WrapPanel? _fields;
    private readonly List<ColorFieldControl> _colors = new();
    private readonly Dictionary<StackPanel, StackPanel> _help = new();
    private readonly Border _appearancePreview = new();
    private readonly Border _librarySample = new();
    private readonly TextBlock _libraryText = new();
    private readonly Border _libraryCard = new();
    private readonly List<FrameworkElement> _fieldBoxes = new();
    public DockStrip Preview { get; } = new();
    public DownloadsPreview DownloadPreview { get; } = new() { Visibility = Visibility.Collapsed };

    public SettingsWindow(ShelfSettings settings, Action changed, Func<bool> save, Func<string> report, string dataRoot, DockCoordinator? docks = null, Func<byte[]>? captureBackup = null)
    {
        _settings = settings; _changed = () => { changed(); Topmost = settings.AlwaysOnTop; Preview.Apply(settings); DownloadPreview.Apply(settings); UpdateLibraryPreview(); foreach (var color in _colors) color.Sync(); UpdatePreviewOpacity(); }; _section = _panel;
        Topmost = settings.AlwaysOnTop; Title = "ClipDrop settings"; Width = 760; Height = 700; MinWidth = 500; MinHeight = 460;
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
        var heading = new StackPanel { Margin = new Thickness(12, 10, 12, 8) };
        DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
        var titleLine = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
        titleLine.Children.Add(new TextBlock { Text = "ClipDrop " + Diagnostics.Version, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 14, 0) });
        _notice.Margin = new Thickness(0); _notice.VerticalAlignment = VerticalAlignment.Center;
        _notice.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); titleLine.Children.Add(_notice); heading.Children.Add(titleLine);
        var nav = new WrapPanel(); heading.Children.Add(nav);
        DownloadPreview.Apply(settings); DockPanel.SetDock(DownloadPreview, Dock.Top); layout.Children.Add(DownloadPreview);
        DockPanel.SetDock(_appearancePreview, Dock.Top); layout.Children.Add(_appearancePreview);
        var scroll = _scroll = new ScrollViewer { Name = "SettingsControls", Content = _panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var footer = new TextBlock { Text = "Made by Nasaat", FontSize = 11, Margin = new Thickness(18, 8, 18, 12) };
        footer.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        layout.Children.Add(scroll);
        TextBlock Section(string name)
        {
            if (_help.TryGetValue(_section, out var previousHelp) && previousHelp.Parent is Expander previousDetails)
            { _section.Children.Remove(previousDetails); _section.Children.Add(previousDetails); }
            _section = new StackPanel(); _fields = null;
            var block = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 8), Child = _section };
            block.SetResourceReference(Border.BackgroundProperty, "Raised");
            block.SetResourceReference(Border.BorderBrushProperty, "Stroke"); block.BorderThickness = new Thickness(1);
            _panel.Children.Add(block);
            var title = new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 9) };
            _section.Children.Add(title);
            var jump = new Button { Content = name, Padding = new Thickness(9, 3, 9, 3), MinHeight = 26 };
            jump.Background = Brushes.Transparent; jump.BorderThickness = new Thickness(0);
            System.Windows.Automation.AutomationProperties.SetName(jump, "Settings: " + name);
            jump.Click += (_, _) => ShowSection(name); nav.Children.Add(jump);
            _sections.Add(name, (block, jump));
            block.RequestBringIntoView += (_, _) => { if (SelectedSection != name) ShowSection(name); };
            return title;
        }
        Section("Style");
        var previewStage = _appearancePreview;
        previewStage.CornerRadius = new CornerRadius(9); previewStage.Padding = new Thickness(10, 6, 10, 6); previewStage.Margin = new Thickness(12, 0, 12, 8);
        previewStage.SetResourceReference(Border.BackgroundProperty, "Surface");
        var previewLayout = new StackPanel(); previewStage.Child = previewLayout;
        var previewLabel = new TextBlock { Text = "Live preview · dock & library", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
        previewLabel.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); previewLayout.Children.Add(previewLabel);
        var idlePreview = new CheckBox { Content = "Preview idle visibility", Margin = new Thickness(0, 4, 0, 0) };
        idlePreview.Checked += (_, _) => { _previewIdle = true; UpdatePreviewOpacity(); };
        idlePreview.Unchecked += (_, _) => { _previewIdle = false; UpdatePreviewOpacity(); };
        var compactPreview = new CheckBox { Content = "Preview compact state", Margin = new Thickness(0, 2, 0, 0) };
        compactPreview.Checked += (_, _) => { _previewCompact = true; UpdatePreviewOpacity(); };
        compactPreview.Unchecked += (_, _) => { _previewCompact = false; UpdatePreviewOpacity(); };
        var samples = new Grid(); samples.ColumnDefinitions.Add(new ColumnDefinition()); samples.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(126) });
        samples.Children.Add(new Viewbox { Child = Preview, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Center, MaxHeight = 64 });
        var libraryText = _libraryText; libraryText.Text = "Saved clip"; libraryText.TextWrapping = TextWrapping.Wrap; libraryText.VerticalAlignment = VerticalAlignment.Center; libraryText.Margin = new Thickness(8);
        libraryText.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
        var libraryCard = _libraryCard; libraryCard.Child = libraryText; libraryCard.Margin = new Thickness(6); libraryCard.SetResourceReference(Border.BackgroundProperty, "Raised");
        _librarySample.Child = libraryCard; _librarySample.CornerRadius = new CornerRadius(8); _librarySample.Margin = new Thickness(8, 4, 0, 4);
        _librarySample.SetResourceReference(Border.BackgroundProperty, "Surface"); UpdateLibraryPreview();
        Grid.SetColumn(_librarySample, 1); samples.Children.Add(_librarySample); previewLayout.Children.Add(samples);
        var previewModes = new WrapPanel(); idlePreview.Margin = new Thickness(0, 2, 16, 0); compactPreview.Margin = new Thickness(0, 2, 0, 0);
        previewModes.Children.Add(idlePreview); previewModes.Children.Add(compactPreview); previewLayout.Children.Add(previewModes);
        Preview.Apply(settings); Preview.SetPins(new[] { ShelfItem.FromText("Saved note"), ShelfItem.FromText("https://example.com") }, 2);

        Group("Theme & colors");
        Choice("App theme", () => settings.Theme, new[] { "Dark", "Light", "Midnight" }, new[] { "Graphite", "Paper", "Midnight" }, value => {
            settings.Theme = value; settings.SharedBackgroundColor = "";
            foreach (var color in _colors) color.Sync(true);
        });
        ColorField("Shared background", () => settings.SharedBackgroundColor, value => settings.SharedBackgroundColor = value);
        ColorField("Accent", () => settings.AccentColor, value => settings.AccentColor = value);
        var colorActions = new WrapPanel(); _section.Children.Add(colorActions);
        var shareColors = new Button { Content = "Use shared colors everywhere", ToolTip = "Make Dock and Library inherit the app theme and background. Layout and saved styles stay unchanged." }; colorActions.Children.Add(shareColors);
        shareColors.Click += (_, _) => { settings.UseSharedColors(); Change(); SyncFields(); _notice.Text = "Dock and Library now follow shared colors"; };
        var resetColors = new Button { Content = "Reset shared colors", ToolTip = "Clear the shared background and accent. Keep the chosen theme and layout." }; colorActions.Children.Add(resetColors);
        resetColors.Click += (_, _) => { settings.SharedBackgroundColor = settings.AccentColor = ""; Change(); SyncFields(); };
        Hint("Choose a swatch for quick colors, More colors for the Windows picker, or enter HEX. Auto inherits the selected theme. Changing App theme clears its custom background. Text contrast adjusts automatically. Dock and Library may keep their own overrides; Use shared colors everywhere removes those overrides.");
        Group("Saved styles");
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

        Group("Motion");
        Check("Animations", () => settings.AnimationsEnabled, value => settings.AnimationsEnabled = value, false);
        Choice("Motion", () => settings.MotionStyle, new[] { "Soft", "Spring" }, new[] { "Soft · calm easing", "Spring · gentle settle" }, value => settings.MotionStyle = value, false);
        Range("Animation duration", () => settings.MotionDuration, 100, 600, value => settings.MotionDuration = value, false, 10, "ms");
        Hint("This duration controls panel reveal and fade. Row hover feedback stays fast.");
        Hint("Windows animation preferences are respected. Colors and changes preview live.");
        var reset = new Button { Content = "Reset appearance", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
        reset.Click += (_, _) => { ApplyStyle(new AppearanceStyle()); RebuildPresets(); _notice.Text = "Appearance reset · saved styles kept"; }; _section.Children.Add(reset);
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
        Range("Maximum panel width", () => settings.DockMaxWidth, 240, 1200, value => settings.DockMaxWidth = value, false, 20, "px");
        Choice("Palette", () => settings.DockPalette, new[] { "Shared", "Dark", "Light", "Midnight" }, new[] { "App theme", "Graphite", "Paper", "Midnight" }, value => settings.DockPalette = value);
        ColorField("Background", () => settings.DockColor, value => settings.DockColor = value, Preview);
        Range("Button size", () => settings.DockButtonSize, 24, 72, value => settings.DockButtonSize = value);
        Range("Spacing", () => settings.DockSpacing, 0, 24, value => settings.DockSpacing = value);
        Range("Corners", () => settings.DockRadius, 0, 36, value => settings.DockRadius = value);
        Range("Opacity", () => settings.DockOpacity, 0, 100, value => settings.DockOpacity = value);
        Range("Idle visibility", () => settings.IdleDockOpacity, 5, 100, value => settings.IdleDockOpacity = value);
        Check("Collapse when idle", () => settings.CollapseWhenIdle, value => settings.CollapseWhenIdle = value, false);
        Check("Fetch image URLs on drop", () => settings.FetchDroppedImages, value => settings.FetchDroppedImages = value, false);
        Hint("Dropped image bytes/files are saved directly. A public image URL or HTML image source is fetched in the background; unavailable images stay as links. A Pinterest page link alone stays a link. No browser cookies or page scraping.");
        Hint("Idle visibility fades the whole dock away from the pointer. Hover, menus and transfers reveal it. Opacity changes only the background.");
        if (docks is not null)
        {
            Section("Screens"); var monitorSettings = new MonitorSettings(settings, docks, () => Change(false));
            _section.Children.Add(monitorSettings); Closed += (_, _) => monitorSettings.Dispose();
        }
        Section("Downloads");
        Range("Downloads width", () => settings.DownloadsWidth, 220, 480, value => settings.DownloadsWidth = value, false, 10, "px");
        Range("Visible rows", () => settings.DownloadsRows, 4, 20, value => settings.DownloadsRows = value, false);
        Range("Download row height", () => settings.DownloadsRowHeight, 28, 52, value => settings.DownloadsRowHeight = value, false, 2, "px");
        Range("Downloads icon size", () => settings.DownloadsIconSize, 32, 64, value => settings.DownloadsIconSize = value, false, 2, "px");
        Range("Space between rows", () => settings.DownloadsRowSpacing, 0, 12, value => settings.DownloadsRowSpacing = value, false, 1, "px");
        Check("Show download time", () => settings.DownloadsShowTime, value => settings.DownloadsShowTime = value, false);
        Check("Always show folder button", () => settings.DownloadsAlwaysShowFolder, value => settings.DownloadsAlwaysShowFolder = value, false);
        Choice("Show in Explorer", () => settings.DownloadsRevealMode, new[] { "Default", "WindowsExplorer" }, new[] { "Default file manager", "Windows Explorer · select file" }, value => settings.DownloadsRevealMode = value, false);
        Hint("Default file manager opens the containing folder using your Windows folder association, including configured File Pilot. Selecting a file is specific to the manager; the Explorer option selects it in Windows Explorer.");
        Choice("List opens", () => settings.DownloadsDirection, new[] { "Auto", "Above", "Below" }, new[] { "Auto · toward free space", "Above icon", "Below icon" }, value => settings.DownloadsDirection = value, false);
        Hint("Panel settings keep the icon center fixed. Auto follows its screen position; edge limits can override a direction. Newest files stay nearest the icon. Compare top/bottom placement in the preview.");
        var resetDownloads = new Button { Content = "Reset Downloads layout", HorizontalAlignment = HorizontalAlignment.Left };
        resetDownloads.Click += (_, _) => { settings.DownloadsWidth = 272; settings.DownloadsRows = 10; settings.DownloadsRowHeight = 34; settings.DownloadsIconSize = 44; settings.DownloadsRowSpacing = 4; settings.DownloadsShowTime = true; settings.DownloadsAlwaysShowFolder = false; Change(false); foreach (var sync in _syncControls) sync(); };
        _section.Children.Add(resetDownloads);
        Hint("One icon · click to open, drag it to move. Width and density update every copy; small screens still clamp the window. Values are logical pixels.");
        Check("Downloads stack", () => settings.DownloadsEnabled, value => settings.DownloadsEnabled = value, false);
        Hint("Downloads appears on every attached display by default. Change duplication independently in Settings → Screens; turn it off for one movable Downloads window.");
        Hint("Hover the Downloads capsule, then drag any file row directly to another app. Use Ctrl/Shift to select a group; right-click for Copy, Save to Library or Show in Explorer. Original files stay in place.");
        Choice("Source", () => settings.EffectiveDownloadsSource, new[] { "Chrome", "Folder" }, new[] { "Chrome · all save locations", "One watched folder" }, value => settings.DownloadsSource = value, false);
        var connectChrome = new Button { Content = "Connect Chrome", HorizontalAlignment = HorizontalAlignment.Left };
        _section.Children.Add(connectChrome);
        connectChrome.Click += (_, _) => {
            try
            {
                ChromeConnection.Connect(); settings.DownloadsSource = "Chrome"; Change(false);
                _sync = true;
                try { foreach (var sync in _syncControls) sync(); } finally { _sync = false; }
                _notice.Text = "Chrome connection registered · follow the opened instructions, then Sync now in the companion";
            }
            catch (Exception ex) { _notice.Text = "Could not connect Chrome: " + ex.Message; }
        };
        Hint("Chrome companion: one-time Load unpacked in each Chrome profile. Finished downloads appear from any folder, even while ClipDrop is closed. No browsing-history, page or cookie access. The popup can pause sharing and clear that profile's list.");
        var downloadPath = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6) };
        downloadPath.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); _section.Children.Add(downloadPath);
        void SyncDownloadsFolder() => downloadPath.Text = settings.DownloadsFolder.Length > 0 ? settings.DownloadsFolder : "Windows Downloads folder";
        SyncDownloadsFolder(); _syncControls.Add(SyncDownloadsFolder);
        var downloadActions = new WrapPanel(); _section.Children.Add(downloadActions);
        var chooseDownloads = new Button { Content = "Choose folder" }; downloadActions.Children.Add(chooseDownloads);
        chooseDownloads.Click += (_, _) => {
            var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Folder for recent downloads", Multiselect = false };
            if (picker.ShowDialog(this) != true) return;
            settings.DownloadsFolder = picker.FolderName; settings.DownloadsSource = "Folder"; Change(false); foreach (var sync in _syncControls) sync();
        };
        var defaultDownloads = new Button { Content = "Use Windows Downloads" }; downloadActions.Children.Add(defaultDownloads);
        defaultDownloads.Click += (_, _) => { settings.DownloadsFolder = ""; settings.DownloadsSource = "Folder"; Change(false); foreach (var sync in _syncControls) sync(); };
        if (docks is not null)
        {
            var showDownloads = new Button { Content = "Show stack" }; downloadActions.Children.Add(showDownloads);
            showDownloads.Click += (_, _) => { docks.OpenDownloads(); foreach (var sync in _syncControls) sync(); };
        }
        Hint("Folder mode watches only the selected folder and omits temporary, empty and actively written files. Chrome mode shows the newest 40 available files from up to 200 completed downloads per connected profile, ordered by completion time. Renamed/moved files need a fresh download or Chrome history update.");
        Section("Library");
        Check("Follow dock", () => settings.LibraryFollowsDock, value => settings.LibraryFollowsDock = value, false);
        Hint("Move Library to detach it. More → Follow dock attaches it again.");
        Choice("Palette", () => settings.LibraryPalette, new[] { "Shared", "Dark", "Light", "Midnight" }, new[] { "App theme", "Graphite", "Paper", "Midnight" }, value => settings.LibraryPalette = value);
        ColorField("Background", () => settings.LibraryColor, value => settings.LibraryColor = value, _librarySample);
        Range("Text size", () => settings.FontSize, 10, 24, value => settings.FontSize = value);
        Range("Row spacing", () => settings.RowSpacing, 0, 24, value => settings.RowSpacing = value);
        Range("Row corners", () => settings.CornerRadius, 0, 24, value => settings.CornerRadius = value);
        if (captureBackup is not null)
        {
            Section("Backups"); var backups = new BackupSection(dataRoot, captureBackup); _section.Children.Add(backups);
            Closed += (_, _) => backups.Dispose();
        }
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
        Hint("Assets and exports remain on disk for reliable transfers.\nClipboard auto-save is opt-in in Library → History. No accounts. Public image URLs can be fetched after an explicit drop; turn this off in Dock.");
        if (_help.TryGetValue(_section, out var lastHelp) && lastHelp.Parent is Expander lastDetails)
        { _section.Children.Remove(lastDetails); _section.Children.Add(lastDetails); }
        RebuildPresets(); ShowSection("Style");
        void FitFields()
        {
            var available = Math.Max(300, scroll.ActualWidth - 24 - 22 - 18);
            var columns = available >= 640 ? 2 : 1;
            var width = available / columns - 8;
            foreach (var field in _fieldBoxes) field.Width = width;
        }
        SizeChanged += (_, _) => { FitFields(); DownloadPreview.Fit(ActualHeight); }; Loaded += (_, _) => {
            var area = WindowPlacement.WorkArea(this);
            Width = Math.Max(MinWidth, Math.Min(Width, area.Width)); Height = Math.Max(MinHeight, Math.Min(Height, area.Height));
            WindowPlacement.Clamp(this); FitFields();
        };
    }
    private void UpdateLibraryPreview()
    {
        Appearance.Apply(_settings, _librarySample, false);
        _libraryText.FontSize = Appearance.Bound(_settings.FontSize, 10, 24, 13);
        _libraryCard.CornerRadius = new CornerRadius(Appearance.Bound(_settings.CornerRadius, 0, 24, 8));
        _libraryCard.Padding = new Thickness(0, Appearance.Bound(_settings.RowSpacing, 0, 24, 4) / 2, 0, Appearance.Bound(_settings.RowSpacing, 0, 24, 4) / 2);
    }
    private void UpdatePreviewOpacity()
    {
        Preview.Opacity = _previewIdle ? Appearance.Bound(_settings.IdleDockOpacity, 5, 100, 20) / 100 : 1;
        Preview.SetCompact(_previewCompact && _settings.CollapseWhenIdle);
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
    private void SyncFields()
    {
        _sync = true;
        try { foreach (var sync in _syncControls) sync(); } finally { _sync = false; }
    }
    private void Group(string title)
    {
        _section.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 9, 4, 3) });
    }
    private void Row(string label, FrameworkElement control)
    {
        // Start a new row group after headings/actions so visual order follows construction order.
        if (_fields is null || _section.Children.Count == 0 || _section.Children[_section.Children.Count - 1] != _fields)
        { _fields = new WrapPanel(); _section.Children.Add(_fields); }
        var field = new Grid { Margin = new Thickness(4, 3, 4, 3), Width = 330, MinHeight = 28 };
        if (label.Length > 0)
        {
            field.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) }); field.ColumnDefinitions.Add(new ColumnDefinition());
            field.Children.Add(new TextBlock { Text = label, FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            Grid.SetColumn(control, 1);
        }
        control.MinHeight = 28; field.Children.Add(control); _fields.Children.Add(field); _fieldBoxes.Add(field);
    }
    private void Hint(string text)
    {
        if (!_help.TryGetValue(_section, out var help))
        {
            help = new StackPanel(); _help.Add(_section, help);
            // Section creation moves this footer to the end once its controls are complete.
            var details = new Expander { Header = "Help & details", Content = help, Margin = new Thickness(4, 8, 4, 2), FontSize = 11 };
            _section.Children.Add(details);
        }
        var hint = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 4, 0, 4) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); help.Children.Add(hint);
    }
    private void Check(string label, Func<bool> read, Action<bool> write, bool appearance)
    {
        var check = new CheckBox { Content = label, IsChecked = read(), Margin = new Thickness(0, 5, 0, 5) };
        check.Click += (_, _) => { write(check.IsChecked == true); Change(appearance); }; Row("", check);
        _syncControls.Add(() => check.IsChecked = read());
    }
    private void Choice(string label, Func<string> read, string[] values, string[] labels, Action<string> write, bool appearance = true)
    {
        var combo = new ComboBox { ItemsSource = labels, SelectedIndex = Math.Max(0, Array.IndexOf(values, read())) };
        System.Windows.Automation.AutomationProperties.SetName(combo, label);
        combo.SelectionChanged += (_, _) => { if (!_sync && combo.SelectedIndex >= 0) { write(values[combo.SelectedIndex]); Change(appearance); } };
        Row(label, combo); _syncControls.Add(() => combo.SelectedIndex = Math.Max(0, Array.IndexOf(values, read())));
    }
    public void ShowSection(string name)
    {
        if (!_sections.ContainsKey(name)) return;
        SelectedSection = name;
        DownloadPreview.Visibility = name == "Downloads" ? Visibility.Visible : Visibility.Collapsed;
        _appearancePreview.Visibility = name is "Style" or "Dock" or "Icons" or "Library" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var pair in _sections)
        {
            var active = pair.Key == name;
            pair.Value.Frame.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            if (active) pair.Value.Button.SetResourceReference(Control.BackgroundProperty, "Selected");
            else pair.Value.Button.Background = Brushes.Transparent;
        }
        _scroll.ScrollToTop();
    }
    private void Range(string name, Func<double> read, double min, double max, Action<double> write, bool appearance = true, double step = 1, string unit = "")
    {
        var field = new NumberField(name, read, min, max, value => { write(value); Change(appearance); }, step, unit);
        Row(name + (unit.Length > 0 ? " · " + unit : ""), field); _syncControls.Add(field.Sync);
    }
    private void ColorField(string name, Func<string> read, Action<string> write, FrameworkElement? surface = null)
    {
        var field = new ColorFieldControl(name, read, value => { if (!_sync) { write(value); Change(); } },
            () => ((surface?.TryFindResource(name == "Accent" ? "Accent" : "Surface") ?? TryFindResource(name == "Accent" ? "Accent" : "Surface")) as SolidColorBrush)?.Color ?? Colors.Gray);
        _colors.Add(field); _syncControls.Add(() => field.Sync(true)); Row(name, field);
    }
}
