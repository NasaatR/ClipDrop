using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClipDrop.Core;

namespace ClipDrop.Windows.Services;

// One native animation clock moves the entire preview. Dependency-property
// storage is required so WPF's Freezable timeline clone retains the transition.
internal sealed class HoverMatrixAnimation : MatrixAnimationBase
{
    public static readonly DependencyProperty TransitionProperty = DependencyProperty.Register(
        nameof(Transition), typeof(HoverTransition), typeof(HoverMatrixAnimation), new PropertyMetadata(default(HoverTransition)));
    public HoverTransition Transition { get => (HoverTransition)GetValue(TransitionProperty); set => SetValue(TransitionProperty, value); }
    public static Matrix MatrixAt(double progress) => new(1 + .12 * progress, 0, 0, 1 + .12 * progress, 0, -progress);
    protected override Freezable CreateInstanceCore() => new HoverMatrixAnimation();
    protected override Matrix GetCurrentValueCore(Matrix defaultOriginValue, Matrix defaultDestinationValue, AnimationClock animationClock)
        => MatrixAt(Transition.Sample(animationClock.CurrentTime?.TotalSeconds ?? 0).Position);
}
