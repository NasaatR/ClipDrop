using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClipDrop.Core;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

// Shared picker: quick theme-aware palette, exact HEX and the owned Windows
// color dialog. Invalid drafts never reach the appearance model.
public sealed class ColorFieldControl : Grid
{
    private readonly Func<string> _read;
    private readonly Action<string> _write;
    private readonly Func<Color> _fallback;
    private readonly Border _sample = new() { Width = 18, Height = 18, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1) };
    private readonly TextBox _input;
    private readonly Button _picker;
    private ContextMenu? _menu;
    public ColorFieldControl(string name, Func<string> read, Action<string> write, Func<Color> fallback)
    {
        _read = read; _write = write; _fallback = fallback;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); ColumnDefinitions.Add(new ColumnDefinition()); ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _sample.SetResourceReference(Border.BorderBrushProperty, "Stroke");
        _picker = new Button { Content = _sample, Width = 30, MinHeight = 28, Padding = new Thickness(3), Margin = new Thickness(0, 0, 5, 0), ToolTip = "Choose a color" };
        _picker.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, name + " color picker"); Children.Add(_picker);
        _input = new TextBox { MaxLength = 7, MinWidth = 72, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 3, 6, 3), ToolTip = "#RRGGBB · empty uses the theme" };
        _input.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, name + " color"); SetColumn(_input, 1); Children.Add(_input);
        var automatic = new Button { Content = "Auto", Padding = new Thickness(6, 3, 6, 3), MinHeight = 28, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Use the inherited theme color" };
        automatic.Click += (_, _) => Set(""); SetColumn(automatic, 2); Children.Add(automatic);
        _input.LostKeyboardFocus += (_, _) => Commit();
        _input.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } else if (e.Key == Key.Escape) { Sync(true); e.Handled = true; } };
        _picker.Click += (_, _) => OpenPalette(name);
        Unloaded += (_, _) => { if (_menu is not null) _menu.IsOpen = false; };
        Sync(true);
    }
    public void Sync(bool force = false)
    {
        if (force || !_input.IsKeyboardFocusWithin) { _input.Text = _read() ?? ""; _input.SetResourceReference(Control.BorderBrushProperty, "Stroke"); }
        var color = AppearanceStyle.TryColor(_read(), out var value) && value.Length > 0 ? (Color)ColorConverter.ConvertFromString(value) : _fallback();
        if (_sample.Background is not SolidColorBrush existing || existing.Color != color)
        { var brush = new SolidColorBrush(color); brush.Freeze(); _sample.Background = brush; }
    }
    private void Commit()
    {
        if (!AppearanceStyle.TryColor(_input.Text, out var color)) { _input.BorderBrush = Brushes.IndianRed; _input.ToolTip = "Enter #RRGGBB, or clear for automatic."; return; }
        Set(color);
    }
    private void Set(string color)
    {
        if (_read() != color) _write(color);
        _input.ToolTip = "#RRGGBB · empty uses the theme"; Sync(true);
    }
    private void OpenPalette(string name)
    {
        if (_menu is not null) _menu.IsOpen = false;
        var menu = _menu = new ContextMenu { PlacementTarget = _picker, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        var swatches = new WrapPanel { Width = 160 };
        foreach (var hex in new[] { "#1E1E1E", "#F5F5F7", "#141C2B", "#334155", "#2563EB", "#7C3AED", "#E11D48", "#F59E0B", "#16A34A", "#06B6D4" })
        {
            var face = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), BorderThickness = new Thickness(1) };
            face.SetResourceReference(Border.BorderBrushProperty, "Stroke");
            var swatch = new Button { Content = face, Width = 30, Height = 30, MinHeight = 0, Padding = new Thickness(3), Margin = new Thickness(1), ToolTip = hex };
            swatch.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Use " + hex);
            swatch.Click += (_, _) => { menu.IsOpen = false; Set(hex); }; swatches.Children.Add(swatch);
        }
        menu.Items.Add(new MenuItem { Header = swatches, Padding = new Thickness(0), Focusable = false, StaysOpenOnClick = true });
        var auto = new MenuItem { Header = "Use theme color" }; auto.Click += (_, _) => Set(""); menu.Items.Add(auto);
        var more = new MenuItem { Header = "More colors…" }; more.Click += (_, _) => { menu.IsOpen = false; Dispatcher.BeginInvoke(new Action(() => ChooseNative())); }; menu.Items.Add(more);
        if (Window.GetWindow(this) is { } owner) PopupDismiss.Attach(menu, owner);
        _picker.ContextMenu = menu; menu.IsOpen = true;
    }
    private sealed class DialogOwner(IntPtr handle) : System.Windows.Forms.IWin32Window { public IntPtr Handle => handle; }
    private void ChooseNative()
    {
        if (!IsLoaded || Window.GetWindow(this) is not { } owner) return;
        var color = (_sample.Background as SolidColorBrush)?.Color ?? _fallback();
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true, Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B) };
        if (dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(owner).Handle)) == System.Windows.Forms.DialogResult.OK)
            Set($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
    }
}
