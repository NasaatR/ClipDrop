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
        public bool? Animated;
        public int Revision;
        public HoverState(FrameworkElement icon, FrameworkElement wash)
        {
            Icon = icon; Wash = wash; var group = new TransformGroup(); group.Children.Add(Scale); group.Children.Add(Move);
            icon.RenderTransform = group; icon.RenderTransformOrigin = new Point(.5, .5);
        }
        public void Reset()
        {
            Revision++; Active = false; Animated = null;
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
        { if (!active) return; state = new(icon, wash); HoverStates.Add(icon, state); Hovers.Add(new(state)); if (Hovers.Count > 512) Hovers.RemoveAll(r => !r.TryGetTarget(out _)); }
        var allowed = Allowed;
        if (state.Active == active && state.Animated == allowed) return;
        state.Active = active; state.Animated = allowed; var revision = ++state.Revision;
        // Pointer feedback is independent of the slower panel disclosure duration.
        // Ease-out responds on the first frame; fast exit prevents trailing rows.
        var duration = TimeSpan.FromMilliseconds(active ? 90 : 55);
        void Paint(DependencyObject target, DependencyProperty property, double value)
        {
            var from = (double)target.GetValue(property);
            void Base() { target.SetValue(property, value); if (target is Animatable a) a.BeginAnimation(property, null); else ((FrameworkElement)target).BeginAnimation(property, null); }
            if (!allowed || Math.Abs(from - value) < .0001) { Base(); return; }
            var animation = new DoubleAnimation(from, value, duration) { FillBehavior = FillBehavior.HoldEnd, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            animation.Completed += (_, _) => { if (state.Revision == revision) Base(); };
            if (target is Animatable animatable) animatable.BeginAnimation(property, animation); else ((FrameworkElement)target).BeginAnimation(property, animation);
        }
        Paint(state.Scale, ScaleTransform.ScaleXProperty, active && allowed ? 1.16 : 1);
        Paint(state.Scale, ScaleTransform.ScaleYProperty, active && allowed ? 1.16 : 1);
        Paint(state.Move, TranslateTransform.YProperty, active && allowed ? -1 : 0);
        Paint(wash, UIElement.OpacityProperty, active ? .8 : 0);
    }
}
