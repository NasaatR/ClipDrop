using System;

namespace ClipDrop.Core;

public readonly record struct MotionSample(double Position, double Velocity);

// Analytic critically damped response. Retarget from the sampled position AND
// velocity, not from rest: rapid pointer reversals retain continuous motion.
public readonly record struct HoverTransition(double Position, double Velocity, double Target)
{
    public const double DurationSeconds = .32;
    private const double Frequency = 32;
    public MotionSample Sample(double seconds)
    {
        if (seconds >= DurationSeconds) return new(Target, 0);
        if (seconds <= 0) return new(Position, Velocity);
        var offset = Position - Target;
        var momentum = Velocity + Frequency * offset;
        var decay = Math.Exp(-Frequency * seconds);
        return new(Target + (offset + momentum * seconds) * decay,
            (Velocity - Frequency * momentum * seconds) * decay);
    }
    public HoverTransition Retarget(double seconds, double target)
    { var sample = Sample(seconds); return new(sample.Position, sample.Velocity, target); }
}
