using System;
using System.Collections.Generic;
using System.Linq;
using Bardaelyn.State;

namespace Bardaelyn.Rules;

public sealed class RuleDecision
{
    public float TargetVolume { get; init; }
    public string RuleName { get; init; } = "Default";
    public BgmRule? SelectedRule { get; init; }
    public bool InterruptSilence { get; init; }
    public IReadOnlySet<BgmRule> ActiveRules { get; init; } = new HashSet<BgmRule>();
}

public sealed class RuleEngine
{
    private readonly Func<RuleSettings> getSettings;
    private readonly Dictionary<BgmRule, RuleTiming> timings = [];

    public RuleDecision LastDecision { get; private set; } = new()
    {
        TargetVolume = 1f,
        RuleName = "Default"
    };

    public RuleEngine(Func<RuleSettings> getSettings)
    {
        this.getSettings = getSettings;
        var settings = getSettings();
        LastDecision = new RuleDecision
        {
            TargetVolume = ClampFinite(settings.DefaultVolume, 1f),
            RuleName = "Default"
        };
    }

    public RuleDecision Evaluate(BgmStateSnapshot state, DateTime now)
    {
        var settings = getSettings();
        var activeRules = new HashSet<BgmRule>(settings.Rules);
        foreach (var staleRule in timings.Keys.Where(rule => !activeRules.Contains(rule)).ToArray())
        {
            timings.Remove(staleRule);
        }

        var defaultVolume = ClampFinite(settings.DefaultVolume, 1f);
        foreach (var rule in settings.Rules)
        {
            if (!timings.TryGetValue(rule, out var timing))
            {
                timing = new RuleTiming();
                timings[rule] = timing;
            }

            var rawMatch = rule.Enabled &&
                           Matches(rule, state, settings.TimePeriods);

            if (rawMatch)
            {
                timing.NoMatchSince = null;
                timing.MatchSince ??= now;
            }
            else
            {
                timing.MatchSince = null;
                timing.NoMatchSince ??= now;
            }
        }

        BgmRule? selected = null;
        var eligibleRules = new HashSet<BgmRule>();
        foreach (var rule in settings.Rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            if (!timings.TryGetValue(rule, out var timing))
            {
                continue;
            }

            if (timing.MatchSince is not null)
            {
                var requiredSeconds = ClampSeconds(rule.ActivateAfterSeconds);
                if (now - timing.MatchSince.Value >= TimeSpan.FromSeconds(requiredSeconds))
                {
                    eligibleRules.Add(rule);
                    selected ??= rule;
                }
            }
            else if (timing.LastSelected &&
                     timing.NoMatchSince is not null &&
                     now - timing.NoMatchSince.Value <
                     TimeSpan.FromSeconds(ClampSeconds(rule.DeactivateAfterSeconds)))
            {
                eligibleRules.Add(rule);
                selected ??= rule;
            }
        }

        foreach (var rule in settings.Rules)
        {
            if (timings.TryGetValue(rule, out var timing))
            {
                timing.LastSelected = ReferenceEquals(selected, rule);
            }
        }

        LastDecision = selected is null
            ? new RuleDecision
            {
                TargetVolume = defaultVolume,
                RuleName = "Default",
                ActiveRules = eligibleRules
            }
            : new RuleDecision
            {
                TargetVolume = ClampFinite(selected.TargetVolume, defaultVolume),
                RuleName = selected.Name,
                SelectedRule = selected,
                InterruptSilence = selected.InterruptSilence,
                ActiveRules = eligibleRules
            };

        return LastDecision;
    }

    private static float ClampFinite(float value, float fallback) =>
        float.IsFinite(value)
            ? Math.Clamp(value, 0f, 1f)
            : float.IsFinite(fallback) ? Math.Clamp(fallback, 0f, 1f) : 1f;

    private static float ClampPercent(float value, float fallback) =>
        float.IsFinite(value)
            ? Math.Clamp(value, 0f, 100f)
            : fallback;

    private static double ClampSeconds(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, 0f, 3600f) : 0f;

    internal static bool Matches(BgmRule rule, BgmStateSnapshot state, EorzeaPeriods? periods = null)
    {
        if (rule.Conditions.Count == 0 && rule.ExcludedConditions.Count == 0)
        {
            return false;
        }
        if (state.EorzeaMinute is null && rule.ExcludedConditions.Contains(RuleCondition.EorzeaTime))
        {
            return false;
        }

        bool ConditionMatches(RuleCondition condition) => condition switch
        {
            RuleCondition.Mounted => state.IsMounted,
            RuleCondition.Moving => state.IsMoving,
            RuleCondition.Idle => state.IsIdle,
            RuleCondition.InCombat => state.IsInCombat,
            RuleCondition.InDuty => state.IsInDuty,
            RuleCondition.InCutscene => state.IsInCutscene,
            RuleCondition.HasTarget => state.HasTarget,
            RuleCondition.LowHp => state.HpPercent <= ClampPercent(rule.LowHpThresholdPercent, 30f),
            RuleCondition.Swimming => state.IsSwimming,
            RuleCondition.Underwater => state.IsUnderwater,
            RuleCondition.EorzeaTime => state.EorzeaMinute is int minute &&
                periods is not null && rule.TimePeriods.Any(period => periods.Contains(period, minute)),
            _ => false
        };

        if (rule.ExcludedConditions.Any(ConditionMatches))
        {
            return false;
        }

        return rule.Conditions.Count == 0 || (rule.RequireAll
            ? rule.Conditions.All(ConditionMatches)
            : rule.Conditions.Any(ConditionMatches));
    }

    private sealed class RuleTiming
    {
        public DateTime? MatchSince { get; set; }
        public DateTime? NoMatchSince { get; set; }
        public bool LastSelected { get; set; }
    }
}
