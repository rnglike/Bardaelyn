using System;

namespace Bardaelyn.Rules;

public sealed class FadeSelector
{
    private BgmRule? previousRule;
    private float previousOut;
    private float previousTarget;
    private float transitionSeconds;
    private bool hasTarget;
    private bool releasing;
    private bool? previousDefaultCycleQuiet;

    public float Select(BgmRule? rule, float target, bool? defaultCycleQuiet = null, float? defaultCycleFadeSeconds = null)
    {
        var incomingIn = rule?.FadeInSeconds ?? 0f;
        var incomingOut = rule?.FadeOutSeconds ?? 0f;
        if (!hasTarget || releasing || !ReferenceEquals(rule, previousRule))
        {
            transitionSeconds = incomingIn > 0 ? incomingIn :
                !hasTarget && rule is null && defaultCycleQuiet is not null && defaultCycleFadeSeconds is not null
                    ? defaultCycleFadeSeconds.Value : hasTarget ? previousOut : 0f;
        }
        else if (Math.Abs(target - previousTarget) > 0.0001f)
        {
            if (rule is null && defaultCycleFadeSeconds is not null &&
                defaultCycleQuiet is not null &&
                defaultCycleQuiet != previousDefaultCycleQuiet)
            {
                transitionSeconds = defaultCycleFadeSeconds.Value;
            }
            else
            {
                var primary = target > previousTarget ? incomingIn : incomingOut;
                var secondary = target > previousTarget ? previousOut : incomingIn;
                transitionSeconds = primary > 0 ? primary : secondary;
            }
        }
        previousRule = rule;
        previousOut = incomingOut;
        previousTarget = target;
        previousDefaultCycleQuiet = defaultCycleQuiet;
        hasTarget = true;
        releasing = false;
        return transitionSeconds;
    }

    public float Release()
    {
        if (!releasing)
        {
            transitionSeconds = hasTarget ? previousOut : 0f;
            releasing = true;
        }
        return transitionSeconds;
    }
}
