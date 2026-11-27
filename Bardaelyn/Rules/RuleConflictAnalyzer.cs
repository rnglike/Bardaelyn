using System;
using System.Collections.Generic;
using System.Linq;
using Bardaelyn.State;

namespace Bardaelyn.Rules;

public static class RuleConflictAnalyzer
{
    public static BgmRule? FindCoveringRule(IReadOnlyList<BgmRule> rules, int index)
    {
        var target = rules[index];
        if (!target.Enabled || (target.Conditions.Count == 0 && target.ExcludedConditions.Count == 0))
        {
            return null;
        }

        for (var i = 0; i < index; i++)
        {
            var candidate = rules[i];
            if (candidate.Enabled && Covers(candidate, target))
            {
                return candidate;
            }
        }
        return null;
    }

    public static BgmRule? FindOverlappingRule(IReadOnlyList<BgmRule> rules, int index)
    {
        var target = rules[index];
        if (!target.Enabled || (target.Conditions.Count == 0 && target.ExcludedConditions.Count == 0))
            return null;
        for (var i = 0; i < index; i++)
        {
            if (rules[i].Enabled && Compare(rules[i], target, false))
                return rules[i];
        }
        return null;
    }

    private static bool Covers(BgmRule candidate, BgmRule target)
        => Compare(candidate, target, true);

    private static bool Compare(BgmRule candidate, BgmRule target, bool requireCoverage)
    {
        if (candidate.Conditions.Contains(RuleCondition.EorzeaTime) ||
            candidate.ExcludedConditions.Contains(RuleCondition.EorzeaTime) ||
            target.Conditions.Contains(RuleCondition.EorzeaTime) ||
            target.ExcludedConditions.Contains(RuleCondition.EorzeaTime))
        {
            return false;
        }
        var boundaries = new[] { 0f, 100f, candidate.LowHpThresholdPercent, target.LowHpThresholdPercent }
            .Select(value => Math.Clamp(value, 0f, 100f)).Distinct().OrderBy(value => value).ToArray();
        var hpValues = boundaries.Concat(boundaries.Zip(boundaries.Skip(1), (a, b) => (a + b) / 2f));
        bool Uses(RuleCondition condition) =>
            candidate.Conditions.Contains(condition) || candidate.ExcludedConditions.Contains(condition) ||
            target.Conditions.Contains(condition) || target.ExcludedConditions.Contains(condition);
        var combinations = Uses(RuleCondition.Underwater) ? 512 : Uses(RuleCondition.Swimming) ? 256 : 128;
        var found = false;
        foreach (var hp in hpValues)
        {
            for (var flags = 0; flags < combinations; flags++)
            {
                var state = new BgmStateSnapshot
                {
                    IsMounted = (flags & 1) != 0, IsMoving = (flags & 2) != 0,
                    IsIdle = (flags & 4) != 0, IsInCombat = (flags & 8) != 0,
                    IsInDuty = (flags & 16) != 0, IsInCutscene = (flags & 32) != 0,
                    HasTarget = (flags & 64) != 0, IsSwimming = (flags & 128) != 0,
                    IsUnderwater = (flags & 256) != 0, HpPercent = hp
                };
                if (!requireCoverage && state.IsIdle &&
                    (state.IsMoving || state.IsInCombat || state.IsInDuty || state.IsInCutscene))
                    continue;
                if (!RuleEngine.Matches(target, state))
                {
                    continue;
                }
                found = true;
                var candidateMatches = RuleEngine.Matches(candidate, state);
                if (!requireCoverage && candidateMatches)
                    return true;
                if (requireCoverage && !candidateMatches)
                {
                    return false;
                }
            }
        }
        return requireCoverage && found;
    }
}
