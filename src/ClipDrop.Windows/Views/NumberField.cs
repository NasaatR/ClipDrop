using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

// Exact entry and keyboard/step controls; no per-pixel slider updates or draft saves.
public sealed class NumberField : Grid
{
    private readonly Func<double> _read;
    private readonly Action<double> _write;
    private readonly double _min, _max, _step;
    private readonly string _help;
    public string Label { get; }
    public TextBox Input { get; }
    public NumberField(string label, Func<double> read, double min, double max, Action<double> write, double step = 1, string unit = "")
    {
        Label = label; _read = read; _write = write; _min = min; _max = max; _step = step;
        _help = $"{min:0}–{max:0} {unit} · type a value, use ↑/↓ or −/+ · Enter applies, Escape restores";
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var less = new Button { Content = "−", Width = 34, Padding = new Thickness(0), Margin = new Thickness(0, 0, 5, 0), ToolTip = "Decrease " + label };
        Input = new TextBox { VerticalContentAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, MinWidth = 60, ToolTip = _help };
        var more = new Button { Content = "+", Width = 34, Padding = new Thickness(0), Margin = new Thickness(5, 0, 0, 0), ToolTip = "Increase " + label };
        System.Windows.Automation.AutomationProperties.SetName(Input, label + " value");
        System.Windows.Automation.AutomationProperties.SetName(less, label + " decrease");
        System.Windows.Automation.AutomationProperties.SetName(more, label + " increase");
        Children.Add(less); SetColumn(Input, 1); Children.Add(Input); SetColumn(more, 2); Children.Add(more);
        less.Click += (_, _) => Step(-1); more.Click += (_, _) => Step(1);
        Input.LostKeyboardFocus += (_, _) => Commit();
        Input.PreviewKeyDown += (_, e) => {
            if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { Sync(); e.Handled = true; }
            else if (e.Key is Key.Up or Key.Down) { Step(e.Key == Key.Up ? 1 : -1); e.Handled = true; }
        };
        Input.PreviewMouseWheel += (_, e) => { if (Input.IsKeyboardFocusWithin) { Step(Math.Sign(e.Delta)); e.Handled = true; } };
        Sync();
    }
    public void Sync()
    {
        Input.Text = Appearance.Bound(_read(), _min, _max, _min).ToString("0.##", CultureInfo.CurrentCulture);
        Input.SetResourceReference(Control.BorderBrushProperty, "Stroke"); Input.ToolTip = _help;
    }
    public bool Commit()
    {
        if (!TryDraft(out var value))
        { Input.BorderBrush = Brushes.IndianRed; Input.ToolTip = _help; return false; }
        // Integer controls use consistent rounding; no silent out-of-range writes.
        value = Math.Clamp(Math.Round(value), _min, _max);
        if (_read() != value) _write(value);
        Sync(); return true;
    }
    private void Step(int direction)
    {
        var value = TryDraft(out var draft) ? Math.Round(draft) : Appearance.Bound(_read(), _min, _max, _min);
        var next = Math.Clamp(value + direction * _step, _min, _max);
        if (next != _read()) _write(next);
        Sync();
    }
    private bool TryDraft(out double value) =>
        (double.TryParse(Input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
            double.TryParse(Input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && double.IsFinite(value) && value >= _min && value <= _max;
}
