using System;
using ClipDrop.Core;

internal static class HoverTransitionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var entering = new HoverTransition(0, 0, 1);
        check(entering.Sample(0) == new MotionSample(0, 0) && entering.Sample(.016).Position is > .04 and < .16,
            "Hover starts immediately with a small first-frame movement instead of a near-instant scale jump");
        check(entering.Sample(.08).Position is > .65 and < .85 && entering.Sample(.2).Position > .98 && entering.Sample(.32) == new MotionSample(1, 0),
            "Hover provides visible early feedback, a soft settling tail and an exact idle endpoint");
        var sample = entering.Sample(.047); var reversed = entering.Retarget(.047, 0);
        check(reversed.Sample(0) == sample && reversed.Sample(.001).Position > sample.Position,
            "Reversal preserves both position and forward momentum before smoothly turning back");
        var transition = entering; var bounded = true; var continuous = true;
        for (var i = 0; i < 200; i++)
        {
            var elapsed = .005 + i % 13 * .003;
            var before = transition.Sample(elapsed);
            transition = transition.Retarget(elapsed, i % 2);
            continuous &= transition.Sample(0) == before;
            for (var step = 0; step <= 10; step++)
            { var value = transition.Sample(step * .032); bounded &= value.Position is >= -1e-10 and <= 1.0000000001 && double.IsFinite(value.Velocity); }
        }
        check(continuous && bounded, "Hundreds of interrupted hover transitions remain continuous, finite and inside icon bounds");
        var exiting = new HoverTransition(1, 0, 0);
        check(exiting.Sample(.08).Position < .3 && exiting.Sample(.2).Position < .02 && exiting.Sample(.32) == new MotionSample(0, 0),
            "Exit loses most emphasis promptly while the remaining motion settles without an abrupt reset");
    }
}
