using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public sealed class DockPresence : IDisposable
{
    private readonly Window _window;
    private readonly Func<ShelfSettings> _settings;
    private readonly Action<bool> _compact;
    private readonly DispatcherTimer _collapse = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private bool _isCompact;
    private readonly DispatcherTimer _settle = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly DispatcherTimer _reveal = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private bool _busy, _pulse, _disposed, _dismissed;
    private readonly bool _transient;
    private double _target = -1;
    public DockPresence(Window window, Func<ShelfSettings> settings, Action<bool> compact, bool transient = false)
    {
        _window = window; _settings = settings; _compact = compact; _transient = transient;
        if (transient) { _collapse.Interval = TimeSpan.FromMilliseconds(300); _settle.Interval = TimeSpan.FromMilliseconds(90); _reveal.Interval = TimeSpan.FromMilliseconds(350); }
        window.MouseEnter += Enter; window.MouseLeave += Leave; window.IsVisibleChanged += Visibility;
        PopupDismiss.Changed += PopupChanged;
        _settle.Tick += (_, _) => { _settle.Stop(); Refresh(); };
        _reveal.Tick += (_, _) => { _reveal.Stop(); _pulse = false; Refresh(); };
        _collapse.Tick += (_, _) => {
            _collapse.Stop();
            if (_window.IsVisible && CollapseWhenIdle && !Active) SetCompact(true);
        };
        Refresh();
    }
    private bool CollapseWhenIdle => _transient || _settings().CollapseWhenIdle;
    private bool Active => !_dismissed && (_window.IsMouseOver || _busy || _pulse || PopupDismiss.IsOpenFor(_window));
    private void SetCompact(bool compact)
    { if (_isCompact == compact) return; _isCompact = compact; _compact(compact); }
    private void Enter(object sender, MouseEventArgs e) { _dismissed = false; _settle.Stop(); Refresh(); }
    private void Leave(object sender, MouseEventArgs e) { _settle.Stop(); _settle.Start(); }
    private void Visibility(object sender, DependencyPropertyChangedEventArgs e)
    { if (!_window.IsVisible) { _settle.Stop(); _reveal.Stop(); _collapse.Stop(); _pulse = false; _busy = false; } Refresh(); }
    private void PopupChanged(Window owner) { if (ReferenceEquals(owner, _window)) Refresh(); }
    public void Busy(bool active) { _busy = active; if (active) _dismissed = false; Refresh(); }
    public void Reveal()
    { if (_disposed) return; _dismissed = false; _pulse = true; _reveal.Stop(); _reveal.Start(); Refresh(); }
    public void Dismiss()
    {
        if (_disposed) return;
        _dismissed = true; _pulse = false; _collapse.Stop(); _reveal.Stop(); _settle.Stop(); SetCompact(true); Refresh();
    }
    public void Refresh()
    {
        if (_disposed) return;
        var active = Active;
        if (_dismissed && _window.IsVisible) { _collapse.Stop(); SetCompact(true); }
        else if (!_window.IsVisible || active || !CollapseWhenIdle) { _collapse.Stop(); SetCompact(false); }
        else if (!_isCompact && !_collapse.IsEnabled) _collapse.Start();
        var next = active ? 1 : Appearance.Bound(_settings().IdleDockOpacity, 5, 100, 20) / 100;
        if (_target == next && (_settings().AnimationsEnabled || !_window.HasAnimatedProperties)) return;
        _target = next; var from = _window.Opacity;
        _window.BeginAnimation(UIElement.OpacityProperty, null); _window.Opacity = next;
        if (_window.IsVisible && Motion.Allowed && Math.Abs(from - next) > .001)
            _window.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(from, next, TimeSpan.FromMilliseconds(next == 1 ? 90 : 160)) { FillBehavior = FillBehavior.Stop, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _settle.Stop(); _reveal.Stop(); _collapse.Stop();
        PopupDismiss.Changed -= PopupChanged;
        _window.MouseEnter -= Enter; _window.MouseLeave -= Leave; _window.IsVisibleChanged -= Visibility;
        _window.BeginAnimation(UIElement.OpacityProperty, null);
    }
}
