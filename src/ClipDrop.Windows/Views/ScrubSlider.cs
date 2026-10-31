using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ClipDrop.Windows.Views;

// Capture the entire rail, not only the small visible thumb. Absolute geometry
// avoids combining a new Value with a thumb layout from the previous frame.
public sealed class ScrubSlider : Slider
{
    private double _startValue, _grabOffset;
    public bool IsScrubbing { get; private set; }
    public event Action? ScrubCompleted;
    public ScrubSlider()
    {
        Style = (Style)Application.Current.FindResource(typeof(Slider));
        IsMoveToPointEnabled = true; IsSnapToTickEnabled = false;
    }
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsEnabled) return;
        Focus(); ApplyTemplate();
        if (GetTemplateChild("PART_Track") is not Track track) { base.OnPreviewMouseLeftButtonDown(e); return; }
        _startValue = Value; _grabOffset = 0;
        var point = e.GetPosition(track);
        if (track.Thumb is { } thumb)
        {
            var left = thumb.TranslatePoint(new Point(), track).X;
            if (point.X >= left && point.X <= left + thumb.ActualWidth) _grabOffset = point.X - left - thumb.ActualWidth / 2;
        }
        if (!CaptureMouse()) return;
        IsScrubbing = true; FromPoint(point, track); e.Handled = true;
    }
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        if (!IsScrubbing) { base.OnPreviewMouseMove(e); return; }
        if (e.LeftButton != MouseButtonState.Pressed) { Finish(); return; }
        if (GetTemplateChild("PART_Track") is Track track) FromPoint(e.GetPosition(track), track);
        e.Handled = true;
    }
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!IsScrubbing) { base.OnPreviewMouseLeftButtonUp(e); return; }
        if (GetTemplateChild("PART_Track") is Track track) FromPoint(e.GetPosition(track), track);
        Finish(); e.Handled = true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); if (!IsMouseCaptured) Finish(); }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (IsScrubbing && e.Key == Key.Escape) { Value = _startValue; Finish(); e.Handled = true; return; }
        base.OnPreviewKeyDown(e);
    }
    private void FromPoint(Point point, Track track)
    {
        var thumb = track.Thumb?.ActualWidth ?? 28;
        var length = track.ActualWidth - thumb;
        if (length <= 0 || Maximum <= Minimum) return;
        var fraction = Math.Clamp((point.X - thumb / 2 - _grabOffset) / length, 0, 1);
        if (IsDirectionReversed) fraction = 1 - fraction;
        SetCurrentValue(ValueProperty, Minimum + fraction * (Maximum - Minimum));
    }
    private void Finish()
    {
        if (!IsScrubbing) return;
        IsScrubbing = false; if (IsMouseCaptured) ReleaseMouseCapture(); ScrubCompleted?.Invoke();
    }
}
