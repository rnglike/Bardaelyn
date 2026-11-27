using System;

namespace SamplePlugin.Rules;

public sealed class FadeSelector
{
    private BgmRule? previousRule;
    private float previousOut;
    private float previousTarget;
    private float transitionSeconds;
    private bool hasTarget;
    private bool releasing;

    public float Select(BgmRule? rule, float target, float globalIn, float globalOut)
    {
        var incomingIn = rule is null || rule.UseGlobalFades ? globalIn : rule.FadeInSeconds;
        var incomingOut = rule is null || rule.UseGlobalFades ? globalOut : rule.FadeOutSeconds;
        if (!hasTarget || releasing || !ReferenceEquals(rule, previousRule))
        {
            transitionSeconds = incomingIn > 0 ? incomingIn : hasTarget ? previousOut : globalOut;
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

    public float Release(float globalIn, float globalOut)
    {
        if (!releasing)
        {
            transitionSeconds = globalIn > 0 ? globalIn : hasTarget ? previousOut : globalOut;
            releasing = true;
        }
        return transitionSeconds;
    }
}
