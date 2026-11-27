using System.Text.Json;
using Bardaelyn;
using Bardaelyn.Rules;
using Bardaelyn.State;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class PriorityAndExclusionTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NewConfigurationHasNoRulesOrBuiltInLowHpOverride()
    {
        var configuration = new Configuration();
        configuration.Normalize();
        Assert.Empty(configuration.Rules);
        var result = new RuleEngine(configuration.GetRuleSettings)
            .Evaluate(new BgmStateSnapshot { HasPlayer = true, HpPercent = 1 }, Now);
        Assert.Equal("Default", result.RuleName);
    }

    [Fact]
    public void LegacySettingsPreserveCustomRulesAndThresholdButRemoveBuiltInRule()
    {
        const string json = """
            {"Version":2,"LowHpThresholdPercent":45,"LowHpRuleEnabled":true,
             "LowHpPosition":0,"GlobalCycle":{"Enabled":true},
             "Rules":[{"Name":"included","Conditions":[7]},{"Name":"excluded","ExcludedConditions":[7]}]}
            """;
        var configuration = JsonSerializer.Deserialize<Configuration>(json)!;
        configuration.Normalize();
        Assert.Equal(3, configuration.Version);
        Assert.Equal(["included", "excluded"], configuration.Rules.Select(rule => rule.Name));
        Assert.All(configuration.Rules, rule => Assert.Equal(45f, rule.LowHpThresholdPercent));
        Assert.True(configuration.GlobalCycle.Enabled);
        configuration.Rules[0].LowHpThresholdPercent = 20;
        configuration.Normalize();
        Assert.Equal(20f, configuration.Rules[0].LowHpThresholdPercent);
        Assert.Equal(45f, configuration.Rules[1].LowHpThresholdPercent);
    }

    [Fact]
    public void RulesUseIndependentThresholdsForIncludedAndExcludedLowHp()
    {
        var low = new BgmRule { Name = "low", LowHpThresholdPercent = 20,
            Conditions = [RuleCondition.LowHp], DeactivateAfterSeconds = 0 };
        var notLow = new BgmRule { Name = "not low", LowHpThresholdPercent = 50,
            ExcludedConditions = [RuleCondition.LowHp], DeactivateAfterSeconds = 0 };
        var engine = new RuleEngine(() => new RuleSettings { Rules = [low, notLow] });
        Assert.Equal("low", engine.Evaluate(new BgmStateSnapshot { HpPercent = 20 }, Now).RuleName);
        Assert.Equal("Default", engine.Evaluate(new BgmStateSnapshot { HpPercent = 30 }, Now.AddSeconds(1)).RuleName);
        Assert.Equal("not low", engine.Evaluate(new BgmStateSnapshot { HpPercent = 51 }, Now.AddSeconds(2)).RuleName);
    }

    [Fact]
    public void MigrationPreservesNumericOrderAndCopiesSharedThreshold()
    {
        var configuration = new Configuration
        {
            Version = 1,
            LowHpThresholdPercent = 42,
            Rules =
            [
                Rule("low", 1), Rule("tie A", 50), Rule("high", 100), Rule("tie B", 50)
            ],
            GlobalCycle = new CycleSettings { Enabled = true, QuietMinSeconds = 42 }
        };

        configuration.Normalize();

        Assert.Equal(3, configuration.Version);
        Assert.Equal(["high", "tie A", "tie B", "low"], configuration.Rules.Select(rule => rule.Name));
        Assert.All(configuration.Rules, rule => Assert.Equal(42, rule.LowHpThresholdPercent));
        Assert.True(configuration.GlobalCycle.Enabled);
        Assert.Equal(42, configuration.GlobalCycle.QuietMinSeconds);
        configuration.MovePriorityEntry(0, 1);
        configuration.Normalize();
        Assert.Equal("tie A", configuration.Rules[0].Name);
    }

    [Fact]
    public void MovingEntriesPreservesRulesAndChangesSelectedPriority()
    {
        var configuration = new Configuration
        {
            Version = 3,
            Rules = [Rule("A", 1), Rule("B", 2)]
        };
        configuration.Normalize();
        var engine = new RuleEngine(configuration.GetRuleSettings);
        var state = new BgmStateSnapshot { HasPlayer = true, IsMounted = true, HpPercent = 20 };

        Assert.Equal("A", engine.Evaluate(state, Now).RuleName);
        configuration.MovePriorityEntry(1, -1);
        Assert.Equal(["B", "A"], configuration.Rules.Select(rule => rule.Name));
        Assert.Equal("B", engine.Evaluate(state, Now.AddSeconds(2)).RuleName);
    }

    [Fact]
    public void OrderedSettingsSurviveSerializationAndNormalization()
    {
        var config = new Configuration
        {
            Version = 3,
            Rules = [Rule("first", 1), Rule("second", 99)]
        };
        config.Rules[0].ExcludedConditions.Add(RuleCondition.InCombat);
        var restored = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;

        restored.Normalize();

        Assert.Equal(["first", "second"], restored.Rules.Select(rule => rule.Name));
        Assert.Equal([RuleCondition.InCombat], restored.Rules[0].ExcludedConditions);
    }

    [Fact]
    public void RemovingRulesKeepsRemainingRules()
    {
        var configuration = new Configuration
        {
            Version = 3,
            Rules = [Rule("A", 0), Rule("B", 0), Rule("C", 0)]
        };
        configuration.RemoveRuleAt(0);
        configuration.RemoveRuleAt(1);
        Assert.Equal("B", Assert.Single(configuration.Rules).Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => configuration.RemoveRuleAt(1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExclusionsVetoBothAllAndAnyMatches(bool requireAll)
    {
        var rule = Rule("test", 0);
        rule.RequireAll = requireAll;
        rule.ExcludedConditions.Add(RuleCondition.InCombat);
        var engine = new RuleEngine(() => new RuleSettings { Rules = [rule] });

        Assert.Equal("test", engine.Evaluate(new BgmStateSnapshot { IsMounted = true }, Now).RuleName);
        Assert.Equal("Default", engine.Evaluate(new BgmStateSnapshot { IsMounted = true, IsInCombat = true }, Now.AddSeconds(1)).RuleName);
    }

    [Fact]
    public void ExclusionOnlyRuleMatchesOnlyWhenExcludedStatesAreAbsent()
    {
        var rule = new BgmRule
        {
            Name = "Not combat", ExcludedConditions = [RuleCondition.InCombat],
            DeactivateAfterSeconds = 0
        };
        var engine = new RuleEngine(() => new RuleSettings { Rules = [rule] });
        Assert.Equal("Not combat", engine.Evaluate(new BgmStateSnapshot(), Now).RuleName);
        Assert.Equal("Default", engine.Evaluate(new BgmStateSnapshot { IsInCombat = true }, Now.AddSeconds(1)).RuleName);
    }

    [Fact]
    public void NormalizationRepairsExcludedConditions()
    {
        var configuration = new Configuration
        {
            Version = 2,
            Rules =
            [
                new BgmRule
                {
                    Conditions = [RuleCondition.Mounted],
                    ExcludedConditions = [RuleCondition.Mounted, RuleCondition.InCombat, RuleCondition.InCombat, (RuleCondition)999]
                },
                new BgmRule { ExcludedConditions = null! }
            ]
        };

        configuration.Normalize();

        Assert.Equal([RuleCondition.InCombat], configuration.Rules[0].ExcludedConditions);
        Assert.Empty(configuration.Rules[1].ExcludedConditions);
    }

    private static BgmRule Rule(string name, int priority) => new()
    {
        Name = name, Priority = priority, Conditions = [RuleCondition.Mounted],
        DeactivateAfterSeconds = 0
    };
}
