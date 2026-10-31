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
    private sealed class HoverState
    {
        public readonly MatrixTransform Transform = new();
        public HoverTransition Transition;
        public AnimationClock? Clock;
        public bool Active;
        public bool? Animated;
        public int Revision;
        public HoverState(FrameworkElement icon)
        { icon.RenderTransform = Transform; icon.RenderTransformOrigin = new Point(.5, .5); }
        public void Settle(double target)
        {
            Transition = new(target, 0, target);
            Transform.Matrix = HoverMatrixAnimation.MatrixAt(target);
            Transform.ApplyAnimationClock(MatrixTransform.MatrixProperty, null);
            Clock?.Controller?.Remove(); Clock = null;
        }
        public void Reset() { Revision++; Active = false; Animated = null; Settle(0); }
    }
    private static readonly ConditionalWeakTable<FrameworkElement, HoverState> HoverStates = new();
    private static readonly List<WeakReference<HoverState>> Hovers = new();
    private static void StopRowHovers()
    { Hovers.RemoveAll(reference => { if (!reference.TryGetTarget(out var state)) return true; state.Reset(); return false; }); }
    public static void ResetHover(FrameworkElement icon)
    { if (HoverStates.TryGetValue(icon, out var state)) state.Reset(); }
    public static void RowHover(FrameworkElement icon, bool active)
    {
        if (!HoverStates.TryGetValue(icon, out var state))
        { if (!active) return; state = new(icon); HoverStates.Add(icon, state); Hovers.Add(new(state)); if (Hovers.Count > 512) Hovers.RemoveAll(r => !r.TryGetTarget(out _)); }
        var allowed = Allowed;
        if (state.Active == active && state.Animated == allowed) return;
        state.Active = active; state.Animated = allowed; var revision = ++state.Revision;
        var target = active && allowed ? 1d : 0;
        if (!allowed) { state.Settle(0); return; }
        // Sample WPF's clock, not wall time: this is the exact currently painted
        // position/velocity, so retargeting does not jump ahead of the last frame.
        var next = state.Transition.Retarget(state.Clock?.CurrentTime?.TotalSeconds ?? 0, target);
        if (Math.Abs(next.Position - target) < .00001 && Math.Abs(next.Velocity) < .0001) { state.Settle(target); return; }
        var animation = new HoverMatrixAnimation { Transition = next, Duration = TimeSpan.FromSeconds(HoverTransition.DurationSeconds), FillBehavior = FillBehavior.HoldEnd };
        var clock = animation.CreateClock(true);
        clock.Completed += (_, _) => { if (state.Revision == revision && ReferenceEquals(state.Clock, clock)) state.Settle(target); };
        var previous = state.Clock;
        state.Transition = next; state.Clock = clock;
        state.Transform.ApplyAnimationClock(MatrixTransform.MatrixProperty, clock, HandoffBehavior.SnapshotAndReplace);
        previous?.Controller?.Remove();
    }
}
