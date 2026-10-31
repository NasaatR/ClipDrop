using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

public static partial class Motion
{
    private sealed class Disclosure
    {
        public readonly ScaleTransform Scale = new(1, 1);
        public readonly TranslateTransform Move = new();
        public readonly TransformGroup Group;
        public bool Active;
        public int Revision;
        public Disclosure() { Group = new(); Group.Children.Add(Scale); Group.Children.Add(Move); }
        public void Stop()
        {
            Active = false; Revision++;
            Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            Move.BeginAnimation(TranslateTransform.YProperty, null);
            Scale.ScaleX = Scale.ScaleY = 1; Move.Y = 0;
        }
    }
    private static readonly ConditionalWeakTable<FrameworkElement, Disclosure> DisclosureStates = new();
    private static readonly List<WeakReference<FrameworkElement>> Disclosures = new();
    public static int DurationMs { get; private set; } = 240;
    private static bool _spring = true;
    public static void Configure(ShelfSettings settings)
    {
        var duration = (int)Appearance.Bound(settings.MotionDuration, 100, 600, 240);
        var spring = settings.MotionStyle != "Soft";
        if (DurationMs != duration || _spring != spring) StopDisclosures();
        DurationMs = duration; _spring = spring;
    }
    private static void StopDisclosures()
    {
        Disclosures.RemoveAll(reference => {
            if (!reference.TryGetTarget(out var element)) return true;
            if (DisclosureStates.TryGetValue(element, out var state)) state.Stop();
            element.BeginAnimation(UIElement.OpacityProperty, null); element.Opacity = 1; return false;
        });
    }
    public static void Disclose(FrameworkElement surface, Point origin)
    {
        var state = DisclosureStates.GetOrCreateValue(surface);
        var previous = surface.Opacity;
        var interrupted = state.Active;
        var scale = interrupted ? state.Scale.ScaleX : .965;
        var travel = interrupted ? state.Move.Y : 7;
        state.Stop(); surface.BeginAnimation(UIElement.OpacityProperty, null); surface.Opacity = 1;
        surface.RenderTransform = state.Group; surface.RenderTransformOrigin = origin;
        if (!Allowed) return;
        state.Active = true; var revision = state.Revision;
        Disclosures.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, surface));
        Disclosures.Add(new(surface));
        var duration = TimeSpan.FromMilliseconds(DurationMs);
        var fade = new DoubleAnimation(interrupted ? previous : .35, 1, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        fade.Completed += (_, _) => {
            if (state.Revision != revision) return;
            state.Stop(); surface.BeginAnimation(UIElement.OpacityProperty, null); surface.Opacity = 1;
        };
        surface.BeginAnimation(UIElement.OpacityProperty, fade);
        state.Move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(travel, 0, duration) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            var animation = new DoubleAnimationUsingKeyFrames { Duration = duration, FillBehavior = FillBehavior.Stop };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(scale, KeyTime.FromPercent(0)));
            if (_spring) animation.KeyFrames.Add(new SplineDoubleKeyFrame(1.006, KeyTime.FromPercent(.72), new KeySpline(.16, 1, .3, 1)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(1, KeyTime.FromPercent(1), new KeySpline(.22, .75, .3, 1)));
            state.Scale.BeginAnimation(property, animation);
        }
    }
}
