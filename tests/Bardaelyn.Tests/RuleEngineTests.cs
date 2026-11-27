using Bardaelyn.Rules;
using Bardaelyn.State;
using Bardaelyn;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class RuleEngineTests
{
    private static readonly DateTime Start = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void UsesDefaultWhenNoRuleMatches()
    {
        var engine = CreateEngine(new RuleSettings
        {
            DefaultVolume = 0.8f,
            Rules = [Rule("Mounted", RuleCondition.Mounted)]
        });

        var result = engine.Evaluate(State(), Start);

        Assert.Equal("Default", result.RuleName);
        Assert.Equal(0.8f, result.TargetVolume);
    }

    [Fact]
    public void RequiresEveryConditionWhenRequireAllIsTrue()
    {
        var engine = CreateEngine(new RuleSettings
        {
            Rules =
            [
                new BgmRule
                {
                    Name = "Mounted and moving",
                    TargetVolume = 0.2f,
                    RequireAll = true,
                    Conditions = [RuleCondition.Mounted, RuleCondition.Moving]
                }
            ]
        });

        var result = engine.Evaluate(State(isMounted: true), Start);

        Assert.Equal("Default", result.RuleName);
    }

    [Fact]
    public void MatchesAnyConditionWhenRequireAllIsFalse()
    {
        var engine = CreateEngine(new RuleSettings
        {
            Rules =
            [
                new BgmRule
                {
                    Name = "Mounted or moving",
                    TargetVolume = 0.2f,
                    RequireAll = false,
                    Conditions = [RuleCondition.Mounted, RuleCondition.Moving]
                }
            ]
        });

        var result = engine.Evaluate(State(isMounted: true), Start);

        Assert.Equal("Mounted or moving", result.RuleName);
        Assert.Equal(0.2f, result.TargetVolume);
    }

    [Fact]
    public void ActivatesOnlyAfterConfiguredDelay()
    {
        var engine = CreateEngine(new RuleSettings
        {
            Rules = [Rule("Mounted", RuleCondition.Mounted, activateAfter: 10f)]
        });

        Assert.Equal("Default", engine.Evaluate(State(isMounted: true), Start).RuleName);
        Assert.Equal("Default", engine.Evaluate(State(isMounted: true), Start.AddSeconds(9.9)).RuleName);
        Assert.Equal("Mounted", engine.Evaluate(State(isMounted: true), Start.AddSeconds(10)).RuleName);
    }

    [Fact]
    public void HoldsSelectedRuleDuringDeactivationDelay()
    {
        var engine = CreateEngine(new RuleSettings
        {
            Rules = [Rule("Mounted", RuleCondition.Mounted, deactivateAfter: 3f)]
        });

        Assert.Equal("Mounted", engine.Evaluate(State(isMounted: true), Start).RuleName);
        Assert.Equal("Mounted", engine.Evaluate(State(), Start.AddSeconds(2)).RuleName);
        Assert.Equal("Default", engine.Evaluate(State(), Start.AddSeconds(5.1)).RuleName);
    }

    [Fact]
    public void FirstMatchingRuleWinsRegardlessOfOldPriority()
    {
        var engine = CreateEngine(new RuleSettings
        {
            Rules =
            [
                Rule("Low priority", RuleCondition.Mounted, priority: 10, targetVolume: 0.1f),
                Rule("High priority", RuleCondition.Mounted, priority: 20, targetVolume: 0.8f)
            ]
        });

        var result = engine.Evaluate(State(isMounted: true), Start);

        Assert.Equal("Low priority", result.RuleName);
        Assert.Equal(0.1f, result.TargetVolume);
    }

    [Fact]
    public void SameNamedRulesKeepIndependentActivationState()
    {
        var engine = CreateEngine(new RuleSettings
        {
            Rules =
            [
                Rule("Same name", RuleCondition.Mounted, priority: 10, targetVolume: 0.1f),
                Rule("Same name", RuleCondition.InCombat, priority: 10, targetVolume: 0.8f)
            ]
        });

        Assert.Equal("Same name", engine.Evaluate(
            State(isMounted: true, isInCombat: true),
            Start).RuleName);

        var result = engine.Evaluate(
            State(isMounted: false, isInCombat: true),
            Start.AddSeconds(1));

        Assert.Equal(0.8f, result.TargetVolume);
    }

    [Fact]
    public void LowHpConditionUsesRuleThreshold()
    {
        var engine = CreateEngine(new RuleSettings
        {
            Rules = [new BgmRule { Name = "Low HP", Conditions = [RuleCondition.LowHp], LowHpThresholdPercent = 30f, TargetVolume = 1f }]
        });

        var result = engine.Evaluate(State(isMounted: true, hpPercent: 20f), Start);

        Assert.Equal("Low HP", result.RuleName);
        Assert.Equal(1f, result.TargetVolume);
    }

    [Fact]
    public void LowHpConditionDoesNotOverrideEarlierRule()
    {
        var engine = CreateEngine(new RuleSettings
        {
            Rules = [Rule("Critical state", RuleCondition.Mounted, priority: 101, targetVolume: 0.15f),
                new BgmRule { Name = "Low HP", Conditions = [RuleCondition.LowHp], LowHpThresholdPercent = 30f }]
        });

        var result = engine.Evaluate(State(isMounted: true, hpPercent: 20f), Start);

        Assert.Equal("Critical state", result.RuleName);
        Assert.Equal(0.15f, result.TargetVolume);
    }

    [Fact]
    public void EmptyConditionsNeverMatch()
    {
        var engine = CreateEngine(new RuleSettings
        {
            Rules = [new BgmRule { Name = "Empty", TargetVolume = 0f, Conditions = [] }]
        });

        var result = engine.Evaluate(State(), Start);

        Assert.Equal("Default", result.RuleName);
    }

    [Fact]
    public void UsesLatestSettingsOnEachEvaluation()
    {
        var current = new RuleSettings { DefaultVolume = 0.4f };
        var engine = CreateEngine(() => current);

        current = new RuleSettings { DefaultVolume = 0.7f };
        var result = engine.Evaluate(State(), Start);

        Assert.Equal(0.7f, result.TargetVolume);
    }

    [Fact]
    public void ConfigurationNormalizationClampsAndRepairsInvalidValues()
    {
        var configuration = new Configuration
        {
            DefaultVolume = float.NaN,
            FadeDurationSeconds = float.PositiveInfinity,
            UpdateIntervalSeconds = -10f,
            LowHpThresholdPercent = 120f,
            Rules =
            [
                new BgmRule
                {
                    Name = " ",
                    TargetVolume = float.NaN,
                    ActivateAfterSeconds = -1f,
                    DeactivateAfterSeconds = float.PositiveInfinity,
                    Conditions = [(RuleCondition)999, RuleCondition.Mounted, RuleCondition.Mounted]
                }
            ]
        };

        configuration.Normalize();

        Assert.Equal(1f, configuration.DefaultVolume);
        Assert.Equal(2f, configuration.FadeDurationSeconds);
        Assert.Equal(0.05f, configuration.UpdateIntervalSeconds);
        Assert.Equal(100f, configuration.LowHpThresholdPercent);
        Assert.Equal("Rule 1", configuration.Rules[0].Name);
        Assert.Equal(0.5f, configuration.Rules[0].TargetVolume);
        Assert.Equal(0f, configuration.Rules[0].ActivateAfterSeconds);
        Assert.Equal(3f, configuration.Rules[0].DeactivateAfterSeconds);
        Assert.Equal([RuleCondition.Mounted], configuration.Rules[0].Conditions);
    }

    [Fact]
    public void ConfigurationNormalizationRepairsNullRuleList()
    {
        var configuration = new Configuration { Rules = null! };

        configuration.Normalize();

        Assert.Empty(configuration.Rules);
    }

    private static RuleEngine CreateEngine(RuleSettings settings) =>
        CreateEngine(() => settings);

    private static RuleEngine CreateEngine(Func<RuleSettings> getSettings) =>
        new(getSettings);

    private static BgmRule Rule(
        string name,
        RuleCondition condition,
        int priority = 0,
        float targetVolume = 0.25f,
        float activateAfter = 0f,
        float deactivateAfter = 0f) =>
        new()
        {
            Name = name,
            Priority = priority,
            TargetVolume = targetVolume,
            ActivateAfterSeconds = activateAfter,
            DeactivateAfterSeconds = deactivateAfter,
            Conditions = [condition]
        };

    private static BgmStateSnapshot State(
        bool isMounted = false,
        bool isMoving = false,
        bool isInCombat = false,
        float hpPercent = 100f) =>
        new()
        {
            HasPlayer = true,
            IsMounted = isMounted,
            IsMoving = isMoving,
            IsInCombat = isInCombat,
            HpPercent = hpPercent
        };
}
