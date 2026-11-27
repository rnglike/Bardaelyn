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

    public float Select(BgmRule? rule, float target)
    {
        var incomingIn = rule?.FadeInSeconds ?? 0f;
        var incomingOut = rule?.FadeOutSeconds ?? 0f;
        if (!hasTarget || releasing || !ReferenceEquals(rule, previousRule))
        {
            transitionSeconds = incomingIn > 0 ? incomingIn : hasTarget ? previousOut : 0f;
        }
        else if (Math.Abs(target - previousTarget) > 0.0001f)
        {
            var primary = target > previousTarget ? incomingIn : incomingOut;
            var secondary = target > previousTarget ? previousOut : incomingIn;
            transitionSeconds = primary > 0 ? primary : secondary;
        }
        previousRule = rule;
        previousOut = incomingOut;
        previousTarget = target;
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
