using Bardaelyn;
using Bardaelyn.Rules;
using Bardaelyn.State;
using System.Text.Json;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class TimingFeaturesTests
{
    // Converts real time around the Unix epoch to the expected Eorzea minute.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(175, 60)]
    [InlineData(2100, 720)]
    [InlineData(4200, 0)]
    [InlineData(-175, 1380)]
    public void EorzeaClockMatchesUnixEpoch(int seconds, int minute)
    {
        Assert.Equal(minute, EorzeaPeriods.MinuteAt(DateTime.UnixEpoch.AddSeconds(seconds)));
    }

    // Includes period starts and excludes ends, including overnight periods.
    [Theory]
    [InlineData(EorzeaPeriod.Morning, 359, false)]
    [InlineData(EorzeaPeriod.Morning, 360, true)]
    [InlineData(EorzeaPeriod.Morning, 720, false)]
    [InlineData(EorzeaPeriod.Night, 1080, true)]
    [InlineData(EorzeaPeriod.Night, 0, true)]
    [InlineData(EorzeaPeriod.Night, 359, true)]
    [InlineData(EorzeaPeriod.Night, 360, false)]
    public void PeriodBoundariesIncludeStartAndExcludeEnd(EorzeaPeriod period, int minute, bool matches)
    {
        Assert.Equal(matches, new EorzeaPeriods().Contains(period, minute));
    }

    // Combines time periods with AND, OR, and exclusion rule matching.
    [Fact]
    public void TimeConditionSupportsAnyAllAndExclusion()
    {
        var periods = new EorzeaPeriods();
        var rule = new BgmRule
        {
            Conditions = [RuleCondition.EorzeaTime, RuleCondition.Mounted],
            TimePeriods = [EorzeaPeriod.Morning, EorzeaPeriod.Night]
        };
        var state = new BgmStateSnapshot { EorzeaMinute = 360 };
        Assert.False(RuleEngine.Matches(rule, state, periods));
        rule.RequireAll = false;
        Assert.True(RuleEngine.Matches(rule, state, periods));
        rule.Conditions = [RuleCondition.EorzeaTime];
        Assert.True(RuleEngine.Matches(rule, state, periods));
        Assert.False(RuleEngine.Matches(rule, new BgmStateSnapshot { EorzeaMinute = 720 }, periods));
        rule.Conditions.Clear();
        rule.ExcludedConditions.Add(RuleCondition.EorzeaTime);
        Assert.False(RuleEngine.Matches(rule, state, periods));
        Assert.True(RuleEngine.Matches(rule, new BgmStateSnapshot { EorzeaMinute = 720 }, periods));
        Assert.False(RuleEngine.Matches(rule, new BgmStateSnapshot(), periods));
    }

    // Resets old rule timings in all profiles and keeps later split settings independent.
    [Fact]
    public void LegacyRuleTimingResetsForEveryProfileAndCopiesAreIndependent()
    {
        var config = new Configuration
        {
            Version = 3, FadeDurationSeconds = 4, FadeInSeconds = 5, FadeOutSeconds = 6,
            Rules = [new BgmRule { Name = "Active", ActivateAfterSeconds = 12, DeactivateAfterSeconds = 24,
                FadeInSeconds = 6, FadeOutSeconds = 8, SeparateDelays = true, SeparateFades = true }],
            ActiveProfileName = "A",
            Profiles = [new SettingsProfile { Name = "A", FadeDurationSeconds = 4 },
                        new SettingsProfile { Name = "B", FadeDurationSeconds = 7,
                            Rules = [new BgmRule { Name = "Inherited" },
                                     new BgmRule { Name = "Custom", UseGlobalFades = false, FadeInSeconds = 9, FadeOutSeconds = 3 }] }]
        };
        config.Normalize();
        Assert.Equal(0, config.Rules[0].ActivateAfterSeconds);
        Assert.Equal(0, config.Rules[0].DeactivateAfterSeconds);
        Assert.Equal(2, config.Rules[0].FadeInSeconds);
        Assert.Equal(2, config.Rules[0].FadeOutSeconds);
        Assert.False(config.Rules[0].SeparateDelays);
        Assert.False(config.Rules[0].SeparateFades);
        config.TimePeriods.MorningStart = 400;
        config.SwitchProfile("B");
        Assert.All(config.Rules, rule =>
        {
            Assert.Equal(0, rule.ActivateAfterSeconds);
            Assert.Equal(0, rule.DeactivateAfterSeconds);
            Assert.Equal(2, rule.FadeInSeconds);
            Assert.Equal(2, rule.FadeOutSeconds);
            Assert.False(rule.SeparateDelays);
            Assert.False(rule.SeparateFades);
        });
        Assert.Equal(360, config.TimePeriods.MorningStart);
        config.Rules[0].SeparateFades = true;
        config.Rules[0].FadeOutSeconds = 3;
        config.Rules[1].SeparateDelays = true;
        config.Rules[1].DeactivateAfterSeconds = 8;
        config.TimePeriods.MorningStart = 500;
        config.Rules[1].TimePeriods.Add(EorzeaPeriod.Night);
        config.SwitchProfile("A");
        Assert.Equal(400, config.TimePeriods.MorningStart);
        config.SwitchProfile("B");
        Assert.Equal(3, config.Rules[0].FadeOutSeconds);
        Assert.Equal(500, config.TimePeriods.MorningStart);
        Assert.Equal(EorzeaPeriod.Night, Assert.Single(config.Rules[1].TimePeriods));
        Assert.Equal(8, config.Rules[1].DeactivateAfterSeconds);
        Assert.True(config.Rules[0].SeparateFades);
        Assert.True(config.Rules[1].SeparateDelays);
        var restored = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;
        restored.Normalize();
        restored.SwitchProfile("A");
        Assert.Equal(2, restored.Rules[0].FadeInSeconds);
        Assert.Equal(2, restored.Rules[0].FadeOutSeconds);
        restored.SwitchProfile("B");
        Assert.Equal(3, restored.Rules[0].FadeOutSeconds);
    }

    // Resets inactive profile timings before saving without changing other rule fields.
    [Fact]
    public void InactiveProfileResetsRuleTimingsBeforeSaving()
    {
        const string json = """
            {"Version":3,"ActiveProfileName":"A","Rules":[],
             "Profiles":[{"Name":"A","Rules":[]},
                         {"Name":"B","FadeDurationSeconds":2,"FadeInSeconds":11,"FadeOutSeconds":13,
                          "Rules":[{"Name":"Inherited","ActivateAfterSeconds":7,"DeactivateAfterSeconds":9},
                                   {"Name":"Custom","UseGlobalFades":false,"SeparateDelays":true,"SeparateFades":true,
                                    "ActivateAfterSeconds":10,"DeactivateAfterSeconds":11,"FadeInSeconds":4,"FadeOutSeconds":5}]}]}
            """;
        var config = JsonSerializer.Deserialize<Configuration>(json)!;
        config.Normalize();
        config.SaveActiveProfile();
        var restored = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;
        restored.Normalize();
        restored.SwitchProfile("B");
        Assert.Equal(["Inherited", "Custom"], restored.Rules.Select(rule => rule.Name));
        Assert.All(restored.Rules, rule =>
        {
            Assert.Equal(0, rule.ActivateAfterSeconds);
            Assert.Equal(0, rule.DeactivateAfterSeconds);
            Assert.Equal(2, rule.FadeInSeconds);
            Assert.Equal(2, rule.FadeOutSeconds);
            Assert.False(rule.SeparateDelays);
            Assert.False(rule.SeparateFades);
        });
    }

    // Keeps combined values synchronized while retaining independent opt-in settings.
    [Fact]
    public void NormalizationSynchronizesOnlyCombinedTiming()
    {
        var combined = new BgmRule { ActivateAfterSeconds = 4, DeactivateAfterSeconds = 7,
            FadeInSeconds = 5, FadeOutSeconds = 8 };
        var separate = new BgmRule { ActivateAfterSeconds = 4, DeactivateAfterSeconds = 7,
            FadeInSeconds = 5, FadeOutSeconds = 8, SeparateDelays = true, SeparateFades = true };
        var config = new Configuration { Version = 5, Rules = [combined, separate] };
        config.Normalize();
        Assert.Equal(4, combined.DeactivateAfterSeconds);
        Assert.Equal(5, combined.FadeOutSeconds);
        Assert.Equal(7, separate.DeactivateAfterSeconds);
        Assert.Equal(8, separate.FadeOutSeconds);
        config.Normalize();
        Assert.Equal(7, separate.DeactivateAfterSeconds);
        Assert.Equal(8, separate.FadeOutSeconds);
    }

    // Selects incoming or outgoing fades and preserves an ongoing transition.
    [Fact]
    public void FadeSelectionUsesIncomingThenOutgoingAndKeepsTransitionAcrossSamples()
    {
        var selector = new FadeSelector();
        var first = new BgmRule { UseGlobalFades = false, FadeInSeconds = 5, FadeOutSeconds = 7 };
        var second = new BgmRule { UseGlobalFades = false, FadeInSeconds = 0, FadeOutSeconds = 3 };
        Assert.Equal(5, selector.Select(first, 0.4f));
        Assert.Equal(7, selector.Select(second, 0.8f));
        Assert.Equal(7, selector.Select(second, 0.8f));
        Assert.Equal(3, selector.Select(second, 0.2f));
        Assert.Equal(3, selector.Release());
        Assert.Equal(3, selector.Release());
        Assert.Equal(3, selector.Select(null, 1));
        Assert.Equal(0, selector.Select(null, 0.5f));
        Assert.Equal(0, selector.Release());
    }

    // Makes cycle transitions immediate when both directional fades are zero.
    [Fact]
    public void ZeroFadesAreImmediateAndCyclesUseDirection()
    {
        var selector = new FadeSelector();
        var rule = new BgmRule { UseGlobalFades = false, FadeInSeconds = 0, FadeOutSeconds = 0 };
        Assert.Equal(0, selector.Select(rule, 0.5f));
        Assert.Equal(0, selector.Select(rule, 0.1f));
        Assert.Equal(0, selector.Select(rule, 0.5f));
    }

    // Uses the outgoing rule's fade when returning to default, then applies default changes immediately.
    [Fact]
    public void DefaultAndReleaseUseOutgoingFadeOnlyWhenARuleWasActive()
    {
        var selector = new FadeSelector();
        Assert.Equal(0, selector.Select(null, 1));
        Assert.Equal(0, selector.Release());
        var rule = new BgmRule { FadeInSeconds = 4, FadeOutSeconds = 8 };
        Assert.Equal(4, selector.Select(rule, 0.4f));
        Assert.Equal(8, selector.Select(null, 1));
        Assert.Equal(8, selector.Select(null, 1));
        Assert.Equal(0, selector.Select(null, 0.2f));
        Assert.Equal(0, selector.Release());
        Assert.Equal(4, selector.Select(rule, 0.4f));
        Assert.Equal(8, selector.Release());
    }
}
