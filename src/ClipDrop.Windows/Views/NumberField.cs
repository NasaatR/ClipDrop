using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClipDrop.Windows.Services;

namespace ClipDrop.Windows.Views;

// Smooth pointer rail plus exact entry. The rail follows every input event;
// expensive application previews are coalesced and always flushed on release.
public sealed class NumberField : Grid
{
    private readonly Func<double> _read;
    private readonly Action<double> _write;
    private readonly double _min, _max, _step;
    private readonly string _help;
    private readonly DispatcherTimer _preview = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private bool _syncing;
    private double? _pending;
    public string Label { get; }
    public TextBox Input { get; }
    public ScrubSlider Rail { get; }
    public NumberField(string label, Func<double> read, double min, double max, Action<double> write, double step = 1, string unit = "")
    {
        Label = label; _read = read; _write = write; _min = min; _max = max; _step = step;
        _help = $"{min:0}–{max:0} {unit} · drag anywhere on the rail · Enter applies, Escape restores";
        ColumnDefinitions.Add(new ColumnDefinition()); ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        Rail = new ScrubSlider { Minimum = min, Maximum = max, SmallChange = step, LargeChange = Math.Max(step, (max - min) / 10), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), ToolTip = _help };
        Input = new TextBox { VerticalContentAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, MinWidth = 60, ToolTip = _help };
        System.Windows.Automation.AutomationProperties.SetName(Input, label + " value");
        System.Windows.Automation.AutomationProperties.SetName(Rail, label + " slider");
        Children.Add(Rail); SetColumn(Input, 1); Children.Add(Input);
        _preview.Tick += (_, _) => Flush();
        Rail.ValueChanged += (_, _) => {
            if (_syncing) return;
            if (!Input.IsKeyboardFocusWithin) { Input.Text = Math.Round(Rail.Value).ToString("0", CultureInfo.CurrentCulture); Input.SetResourceReference(Control.BorderBrushProperty, "Stroke"); }
            _pending = Math.Clamp(Math.Round(Rail.Value), _min, _max);
            if (!_preview.IsEnabled) _preview.Start();
        };
        Rail.ScrubCompleted += Flush;
        Rail.LostKeyboardFocus += (_, _) => Flush();
        Rail.PreviewMouseWheel += (_, e) => { if (Rail.IsKeyboardFocused) { Rail.Value = Math.Clamp(Rail.Value + Math.Sign(e.Delta) * step, min, max); e.Handled = true; } };
        Input.LostKeyboardFocus += (_, _) => Commit();
        Input.PreviewKeyDown += (_, e) => {
            if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { Sync(); e.Handled = true; }
            else if (e.Key is Key.Up or Key.Down) { Step(e.Key == Key.Up ? 1 : -1); e.Handled = true; }
        };
        Input.PreviewMouseWheel += (_, e) => { if (Input.IsKeyboardFocusWithin) { Step(Math.Sign(e.Delta)); e.Handled = true; } };
        Unloaded += (_, _) => Flush(); Sync();
    }
    public void Sync()
    {
        if (Rail.IsScrubbing) return;
        _preview.Stop(); _pending = null; _syncing = true;
        try { Rail.Value = Appearance.Bound(_read(), _min, _max, _min); Input.Text = Rail.Value.ToString("0.##", CultureInfo.CurrentCulture); }
        finally { _syncing = false; }
        Input.SetResourceReference(Control.BorderBrushProperty, "Stroke"); Input.ToolTip = _help;
    }
    public void Flush()
    {
        _preview.Stop(); if (_pending is not double value) return; _pending = null;
        if (_read() != value) _write(value);
        // Do not round the thumb back to the model while the pointer is captured.
        if (!Rail.IsScrubbing) Sync();
    }
    public bool Commit()
    {
        if (!TryDraft(out var value)) { Input.BorderBrush = Brushes.IndianRed; return false; }
        _preview.Stop(); _pending = null; value = Math.Clamp(Math.Round(value), _min, _max);
        if (_read() != value) _write(value); Sync(); return true;
    }
    private void Step(int direction)
    {
        var value = TryDraft(out var draft) ? Math.Round(draft) : Appearance.Bound(_read(), _min, _max, _min);
        var next = Math.Clamp(value + direction * _step, _min, _max);
        _preview.Stop(); _pending = null; if (next != _read()) _write(next); Sync();
    }
    private bool TryDraft(out double value) =>
        (double.TryParse(Input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) || double.TryParse(Input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && double.IsFinite(value) && value >= _min && value <= _max;
}
