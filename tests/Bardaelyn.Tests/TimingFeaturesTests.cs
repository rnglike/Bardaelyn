using SamplePlugin;
using SamplePlugin.Rules;
using SamplePlugin.State;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class TimingFeaturesTests
{
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

    [Fact]
    public void LegacyFadesMigrateForEveryProfileAndCopiesAreIndependent()
    {
        var config = new Configuration
        {
            Version = 3, FadeDurationSeconds = 4,
            ActiveProfileName = "A",
            Profiles = [new SettingsProfile { Name = "A", FadeDurationSeconds = 4 },
                        new SettingsProfile { Name = "B", FadeDurationSeconds = 7 }]
        };
        config.Normalize();
        Assert.Equal(4, config.FadeInSeconds);
        config.TimePeriods.MorningStart = 400;
        config.SwitchProfile("B");
        Assert.Equal(7, config.FadeInSeconds);
        Assert.Equal(7, config.FadeOutSeconds);
        Assert.Equal(360, config.TimePeriods.MorningStart);
        config.FadeOutSeconds = 3;
        config.TimePeriods.MorningStart = 500;
        config.Rules.Add(new BgmRule { TimePeriods = [EorzeaPeriod.Night], UseGlobalFades = false, FadeInSeconds = 9 });
        config.SwitchProfile("A");
        Assert.Equal(400, config.TimePeriods.MorningStart);
        config.SwitchProfile("B");
        Assert.Equal(3, config.FadeOutSeconds);
        Assert.Equal(500, config.TimePeriods.MorningStart);
        Assert.Equal(EorzeaPeriod.Night, Assert.Single(Assert.Single(config.Rules).TimePeriods));
        Assert.Equal(9, config.Rules[0].FadeInSeconds);
    }

    [Fact]
    public void FadeSelectionUsesIncomingThenOutgoingAndKeepsTransitionAcrossSamples()
    {
        var selector = new FadeSelector();
        var first = new BgmRule { UseGlobalFades = false, FadeInSeconds = 5, FadeOutSeconds = 7 };
        var second = new BgmRule { UseGlobalFades = false, FadeInSeconds = 0, FadeOutSeconds = 3 };
        Assert.Equal(5, selector.Select(first, 0.4f, 2, 4));
        Assert.Equal(7, selector.Select(second, 0.8f, 2, 4));
        Assert.Equal(7, selector.Select(second, 0.8f, 2, 4));
        Assert.Equal(3, selector.Select(second, 0.2f, 2, 4));
        Assert.Equal(3, selector.Release(0, 4));
        Assert.Equal(3, selector.Release(0, 4));
        Assert.Equal(2, selector.Select(null, 1, 2, 4));
    }

    [Fact]
    public void ZeroFadesAreImmediateAndCyclesUseDirection()
    {
        var selector = new FadeSelector();
        var rule = new BgmRule { UseGlobalFades = false, FadeInSeconds = 0, FadeOutSeconds = 0 };
        Assert.Equal(0, selector.Select(rule, 0.5f, 0, 0));
        Assert.Equal(0, selector.Select(rule, 0.1f, 0, 0));
        Assert.Equal(0, selector.Select(rule, 0.5f, 0, 0));
    }
}
