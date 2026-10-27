using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

public sealed class MonitorSettings : StackPanel, IDisposable
{
    private readonly ShelfSettings _settings;
    private readonly DockCoordinator _docks;
    private readonly Action _changed;
    public MonitorSettings(ShelfSettings settings, DockCoordinator docks, Action changed)
    { _settings = settings; _docks = docks; _changed = changed; docks.DisplaysChanged += Refresh; Refresh(); }
    private void Commit() { _changed(); Refresh(); }
    private void Refresh()
    {
        Children.Clear(); var displays = _docks.Displays;
        if (displays.Count == 0) { Children.Add(new TextBlock { Text = "No attached display detected. Use Refresh displays." }); }
        else
        {
            var minX = displays.Min(d => d.Left); var minY = displays.Min(d => d.Top);
            var width = displays.Max(d => d.Left + d.Width) - minX; var height = displays.Max(d => d.Top + d.Height) - minY;
            var scale = Math.Min(400 / width, 100 / height);
            var map = new Canvas { Width = width * scale, Height = height * scale, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 12) };
            foreach (var display in displays)
            {
                var box = new Border { Width = Math.Max(30, display.Width * scale - 3), Height = Math.Max(24, display.Height * scale - 3), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), ToolTip = display.Label,
                    Child = new TextBlock { Text = display.Label.Split('·')[0].Trim(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                box.SetResourceReference(Border.BackgroundProperty, "Control"); box.SetResourceReference(Border.BorderBrushProperty, "Stroke");
                Canvas.SetLeft(box, (display.Left - minX) * scale); Canvas.SetTop(box, (display.Top - minY) * scale); map.Children.Add(box);
            }
            Children.Add(map);
            Children.Add(new TextBlock { Text = "Main dock on", Margin = new Thickness(0, 0, 0, 4) });
            var home = new ComboBox { ItemsSource = displays, DisplayMemberPath = nameof(DisplayArea.Label), SelectedItem = displays.FirstOrDefault(d => DockPlanning.Same(d.Id, _settings.HomeDisplayId)), MinHeight = 32 };
            System.Windows.Automation.AutomationProperties.SetName(home, "Main dock display"); Children.Add(home);
            home.SelectionChanged += (_, _) => {
                if (home.SelectedItem is not DisplayArea selected) return;
                _settings.HomeDisplayId = selected.Id;
                var record = _settings.Docks.FirstOrDefault(d => DockPlanning.Same(d.DisplayId, selected.Id)); if (record is not null) { record.Enabled = true; record.Visible = true; }
                Commit();
            };
            var mirror = new CheckBox { Content = "Duplicate dock on selected displays (preview)", IsChecked = _settings.MirrorDocks, Margin = new Thickness(0, 10, 0, 6) };
            System.Windows.Automation.AutomationProperties.SetName(mirror, "Duplicate dock on selected displays"); Children.Add(mirror);
            mirror.Click += (_, _) => { _settings.MirrorDocks = mirror.IsChecked == true; Commit(); };
            foreach (var display in displays)
            {
                var record = _settings.Docks.FirstOrDefault(d => DockPlanning.Same(d.DisplayId, display.Id)); if (record is null) continue;
                var isHome = DockPlanning.Same(display.Id, _settings.HomeDisplayId);
                var check = new CheckBox { Content = display.Label + $" · {display.Width:N0} × {display.Height:N0}", IsChecked = isHome || record.Enabled, IsEnabled = _settings.MirrorDocks && !isHome, Margin = new Thickness(0, 4, 0, 4) };
                Children.Add(check); check.Click += (_, _) => { record.Enabled = check.IsChecked == true; if (record.Enabled) record.Visible = true; Commit(); };
            }
        }
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) }; Children.Add(actions);
        void ActionButton(string label, Action action) { var button = new Button { Content = label }; button.Click += (_, _) => action(); actions.Children.Add(button); }
        ActionButton("Show all", _docks.ShowAll); ActionButton("Hide all", _docks.HideAll); ActionButton("Refresh displays", _docks.RefreshDisplays);
        ActionButton("Reset positions", () => { foreach (var layout in _settings.Docks) { layout.OffsetX = null; layout.OffsetY = null; } Commit(); });
        var hint = new TextBlock { Text = "Linked clips · independent positions and visibility. Ctrl+Alt+Space targets the active app's display. Drag stays on the assigned display. Reconnect restores enabled displays. Physical mixed-DPI testing is still needed.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 5, 0, 0) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Children.Add(hint);
        var offline = _settings.Docks.Count(d => !displays.Any(s => DockPlanning.Same(s.Id, d.DisplayId)));
        if (offline > 0) Children.Add(new TextBlock { Text = $"{offline} disconnected display layout(s) kept", FontSize = 11, Margin = new Thickness(0, 5, 0, 0) });
    }
    public void Dispose() => _docks.DisplaysChanged -= Refresh;
}
