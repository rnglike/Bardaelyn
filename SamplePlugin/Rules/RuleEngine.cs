using System;
using System.Collections.Generic;
using System.Linq;
using SamplePlugin.State;

namespace SamplePlugin.Rules;

public sealed class RuleDecision
{
    public float TargetVolume { get; init; }
    public string RuleName { get; init; } = "Default";
}

public sealed class RuleEngine
{
    private readonly Configuration configuration;
    private readonly Dictionary<BgmRule, RuleTiming> timings = [];

    public RuleDecision LastDecision { get; private set; } = new()
    {
        TargetVolume = 1f,
        RuleName = "Default"
    };

    public RuleEngine(Configuration configuration)
    {
        this.configuration = configuration;
        LastDecision = new RuleDecision
        {
            TargetVolume = configuration.DefaultVolume,
            RuleName = "Default"
        };
    }

    public RuleDecision Evaluate(BgmStateSnapshot state, DateTime now)
    {
        foreach (var rule in configuration.Rules)
        {
            if (!timings.TryGetValue(rule, out var timing))
            {
                timing = new RuleTiming();
                timings[rule] = timing;
            }

            var rawMatch = rule.Enabled &&
                           Matches(rule, state, configuration.LowHpThresholdPercent);

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
        foreach (var rule in configuration.Rules
                     .Where(rule => rule.Enabled)
                     .OrderByDescending(rule => rule.Priority))
        {
            if (!timings.TryGetValue(rule, out var timing))
            {
                continue;
            }

            if (timing.MatchSince is not null)
            {
                var requiredSeconds = rule.ActivateAfterSeconds;
                if (now - timing.MatchSince.Value >= TimeSpan.FromSeconds(requiredSeconds))
                {
                    selected = rule;
                    break;
                }
            }
            else if (timing.LastSelected &&
                     timing.NoMatchSince is not null &&
                     now - timing.NoMatchSince.Value <=
                     TimeSpan.FromSeconds(rule.DeactivateAfterSeconds))
            {
                selected = rule;
                break;
            }
        }

        if (configuration.LowHpRuleEnabled &&
            state.HpPercent <= configuration.LowHpThresholdPercent)
        {
            selected = configuration.LowHpPriority >= (selected?.Priority ?? int.MinValue)
                ? new BgmRule
                {
                    Name = "Low HP",
                    Priority = configuration.LowHpPriority,
                    TargetVolume = configuration.LowHpVolume
                }
                : selected;
        }

        foreach (var rule in configuration.Rules)
        {
            if (timings.TryGetValue(rule, out var timing))
            {
                timing.LastSelected = selected?.Name == rule.Name;
            }
        }

        LastDecision = selected is null
            ? new RuleDecision
            {
                TargetVolume = configuration.DefaultVolume,
                RuleName = "Default"
            }
            : new RuleDecision
            {
                TargetVolume = Math.Clamp(selected.TargetVolume, 0f, 1f),
                RuleName = selected.Name
            };

        return LastDecision;
    }

    private static bool Matches(BgmRule rule, BgmStateSnapshot state, float lowHpThreshold)
    {
        if (rule.Conditions.Count == 0)
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
            RuleCondition.LowHp => state.HpPercent <= lowHpThreshold,
            _ => false
        };

        return rule.RequireAll
            ? rule.Conditions.All(ConditionMatches)
            : rule.Conditions.Any(ConditionMatches);
    }

    private sealed class RuleTiming
    {
        public DateTime? MatchSince { get; set; }
        public DateTime? NoMatchSince { get; set; }
        public bool LastSelected { get; set; }
    }
}
