using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClipDrop.Windows.Services;

public static class Motion
{
    private static bool _enabled = true;
    private static readonly List<WeakReference<Button>> Buttons = new();
    private static readonly List<WeakReference<FrameworkElement>> Reveals = new();
    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value) return;
            Buttons.RemoveAll(reference => {
                if (!reference.TryGetTarget(out var button)) return true;
                if (button.RenderTransform is ScaleTransform scale) { scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); scale.ScaleX = scale.ScaleY = 1; }
                button.BeginAnimation(UIElement.OpacityProperty, null); return false;
            });
            Reveals.RemoveAll(reference => { if (!reference.TryGetTarget(out var surface)) return true; surface.BeginAnimation(UIElement.OpacityProperty, null); surface.Opacity = 1; return false; });
        }
    }
    public static bool Allowed => Enabled && SystemParameters.ClientAreaAnimation;
    public static readonly DependencyProperty FeedbackProperty = DependencyProperty.RegisterAttached("Feedback", typeof(bool), typeof(Motion), new PropertyMetadata(false, Changed));
    public static void SetFeedback(DependencyObject target, bool value) => target.SetValue(FeedbackProperty, value);
    public static bool GetFeedback(DependencyObject target) => (bool)target.GetValue(FeedbackProperty);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Button button || args.NewValue is not true) return;
        Buttons.Add(new WeakReference<Button>(button));
        if (Buttons.Count > 512) Buttons.RemoveAll(reference => !reference.TryGetTarget(out _));
        button.RenderTransformOrigin = new Point(.5, .5);
        button.RenderTransform = new ScaleTransform(1, 1);
        void Update()
        {
            if (button.RenderTransform is not ScaleTransform scale) return;
            var next = button.IsPressed ? .94 : button.IsMouseOver && button.IsEnabled ? 1.035 : 1;
            Animate(scale, ScaleTransform.ScaleXProperty, Allowed ? next : 1, 110);
            Animate(scale, ScaleTransform.ScaleYProperty, Allowed ? next : 1, 110);
        }
        button.MouseEnter += (_, _) => Update(); button.MouseLeave += (_, _) => Update();
        button.PreviewMouseLeftButtonDown += (_, _) => button.Dispatcher.BeginInvoke(new Action(Update));
        button.PreviewMouseLeftButtonUp += (_, _) => button.Dispatcher.BeginInvoke(new Action(Update));
        button.LostMouseCapture += (_, _) => Update(); button.IsEnabledChanged += (_, _) => Update();
        button.Unloaded += (_, _) => { if (button.RenderTransform is ScaleTransform scale) { scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); scale.ScaleX = scale.ScaleY = 1; } };
    }
    private static void Animate(Animatable target, DependencyProperty property, double value, int ms)
    {
        var from = (double)target.GetValue(property);
        target.BeginAnimation(property, null); target.SetValue(property, value);
        if (Allowed) target.BeginAnimation(property, new DoubleAnimation(from, value, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }
    public static void Reveal(FrameworkElement surface)
    {
        surface.BeginAnimation(UIElement.OpacityProperty, null);
        surface.Opacity = 1;
        if (!Allowed) return;
        Reveals.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, surface));
        Reveals.Add(new WeakReference<FrameworkElement>(surface));
        surface.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.45, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }
    public static void Arrive(Button button)
    {
        Reveal(button);
        if (!Allowed || button.RenderTransform is not ScaleTransform scale) return;
        scale.ScaleX = scale.ScaleY = 1;
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
            scale.BeginAnimation(property, new DoubleAnimation(.8, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }
}
