using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClipDrop.Windows.Services;

public static partial class Motion
{
    private sealed class HoverState
    {
        public readonly FrameworkElement Icon, Wash;
        public readonly ScaleTransform Scale = new(1, 1);
        public readonly TranslateTransform Move = new();
        public bool Active;
        public int Revision;
        public HoverState(FrameworkElement icon, FrameworkElement wash)
        {
            Icon = icon; Wash = wash; var group = new TransformGroup(); group.Children.Add(Scale); group.Children.Add(Move);
            icon.RenderTransform = group; icon.RenderTransformOrigin = new Point(.5, .5);
        }
        public void Reset()
        {
            Revision++; Active = false;
            Set(Scale, ScaleTransform.ScaleXProperty, 1); Set(Scale, ScaleTransform.ScaleYProperty, 1);
            Set(Move, TranslateTransform.YProperty, 0); Wash.Opacity = 0; Wash.BeginAnimation(UIElement.OpacityProperty, null);
        }
        private static void Set(Animatable target, DependencyProperty property, double value)
        { target.SetValue(property, value); target.BeginAnimation(property, null); }
    }
    private static readonly ConditionalWeakTable<FrameworkElement, HoverState> HoverStates = new();
    private static readonly List<WeakReference<HoverState>> Hovers = new();
    private static void StopRowHovers()
    { Hovers.RemoveAll(reference => { if (!reference.TryGetTarget(out var state)) return true; state.Reset(); return false; }); }
    public static void ResetHover(FrameworkElement icon)
    { if (HoverStates.TryGetValue(icon, out var state)) state.Reset(); }
    public static void RowHover(FrameworkElement icon, FrameworkElement wash, bool active)
    {
        if (!HoverStates.TryGetValue(icon, out var state))
        { state = new(icon, wash); HoverStates.Add(icon, state); Hovers.Add(new(state)); if (Hovers.Count > 512) Hovers.RemoveAll(r => !r.TryGetTarget(out _)); }
        if (state.Active == active && Allowed) return;
        state.Active = active; var revision = ++state.Revision;
        var duration = TimeSpan.FromMilliseconds(Math.Clamp(DurationMs * .65, 100, 220));
        void Paint(DependencyObject target, DependencyProperty property, double value)
        {
            var from = (double)target.GetValue(property);
            void Base() { target.SetValue(property, value); if (target is Animatable a) a.BeginAnimation(property, null); else ((FrameworkElement)target).BeginAnimation(property, null); }
            if (!Allowed || Math.Abs(from - value) < .0001) { Base(); return; }
            var animation = new DoubleAnimation(from, value, duration) { FillBehavior = FillBehavior.HoldEnd, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            animation.Completed += (_, _) => { if (state.Revision == revision) Base(); };
            if (target is Animatable animatable) animatable.BeginAnimation(property, animation); else ((FrameworkElement)target).BeginAnimation(property, animation);
        }
        Paint(state.Scale, ScaleTransform.ScaleXProperty, active && Allowed ? 1.08 : 1);
        Paint(state.Scale, ScaleTransform.ScaleYProperty, active && Allowed ? 1.08 : 1);
        Paint(state.Move, TranslateTransform.YProperty, active && Allowed ? -.5 : 0);
        Paint(wash, UIElement.OpacityProperty, active ? .65 : 0);
    }
}
