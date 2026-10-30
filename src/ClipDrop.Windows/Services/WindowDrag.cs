using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ClipDrop.Windows.Services;

// Move from any non-interactive background; leave OLE cards, editors and resize grips alone.
// Mouse capture avoids the activation side effect of native caption dragging on a dock.
public sealed class WindowDrag
{
    private readonly Window _window;
    private readonly Action _started;
    private readonly Action _completed;
    private Point? _start;
    private Point _grab;
    private double _startScale;
    public bool IsMoving { get; private set; }
    public WindowDrag(Window window, Action started, Action completed)
    {
        _window = window; _started = started; _completed = completed;
        window.PreviewMouseLeftButtonDown += Down;
        window.PreviewMouseMove += Move;
        window.PreviewMouseLeftButtonUp += (_, _) => Finish();
        window.LostMouseCapture += (_, _) => { if (!window.IsMouseCaptured) Finish(); };
        window.IsVisibleChanged += (_, _) => { if (!window.IsVisible) Finish(); };
    }
    public static bool CanStart(DependencyObject? source)
    {
        while (source is not null && source is not Window)
        {
            if (source is ButtonBase or TextBoxBase or PasswordBox or ComboBox or ListBoxItem or RangeBase or Thumb or ResizeGrip or MenuItem or Hyperlink) return false;
            source = source is Visual or Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return source is Window;
    }
    private void Down(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1 || !CanStart(e.OriginalSource as DependencyObject)) return;
        if (!WindowPlacement.TryCursorPoint(out var cursor)) return;
        _start = cursor; _startScale = WindowPlacement.Scale(_window);
        var bounds = WindowPlacement.PixelBounds(_window);
        _grab = new Point((cursor.X - bounds.Left) / _startScale, (cursor.Y - bounds.Top) / _startScale);
        if (!_window.CaptureMouse()) _start = null;
        else e.Handled = true;
    }
    private void Move(object sender, MouseEventArgs e)
    {
        if (_start is not Point start) return;
        if (e.LeftButton != MouseButtonState.Pressed) { Finish(); return; }
        if (!WindowPlacement.TryCursorPoint(out var current)) return;
        var delta = current - start;
        if (!IsMoving)
        {
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance * _startScale && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance * _startScale) return;
            IsMoving = true; _started();
        }
        var scale = WindowPlacement.Scale(_window);
        WindowPlacement.MovePixels(_window, current.X - _grab.X * scale, current.Y - _grab.Y * scale);
        // SetWindowPos can synchronously change WPF's DPI/size at a boundary.
        // Keep the same DIP grab point under the physical pointer after that change.
        var nextScale = WindowPlacement.Scale(_window);
        if (nextScale != scale)
            WindowPlacement.MovePixels(_window, current.X - _grab.X * nextScale, current.Y - _grab.Y * nextScale);
        e.Handled = true;
    }
    private void Finish()
    {
        if (_start is null) return;
        _start = null;
        var moved = IsMoving; IsMoving = false;
        if (_window.IsMouseCaptured) _window.ReleaseMouseCapture();
        if (moved) { WindowPlacement.Clamp(_window); _completed(); }
    }
}
