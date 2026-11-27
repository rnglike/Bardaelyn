using Bardaelyn;
using Bardaelyn.Rules;
using Bardaelyn.State;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class SilenceCycleTests
{
    private static readonly DateTime Start = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StartsQuietAndAlternatesAtExactBoundaries()
    {
        var cycles = new SilenceCycleEngine();
        var settings = Settings();
        var decision = Decision(0.5f);

        Assert.Equal(0.1f, cycles.Evaluate(decision, settings, Start).TargetVolume, 4);
        Assert.Equal(0.1f, cycles.Evaluate(decision, settings, Start.AddSeconds(9)).TargetVolume, 4);
        Assert.Equal(0.5f, cycles.Evaluate(decision, settings, Start.AddSeconds(10)).TargetVolume, 4);
        Assert.Equal(0.1f, cycles.Evaluate(decision, settings, Start.AddSeconds(30)).TargetVolume, 4);
        Assert.Contains("9s", cycles.Evaluate(decision, settings, Start.AddSeconds(31)).Status);
    }

    [Fact]
    public void RandomRangesAreChosenAtTheStartOfEachPhase()
    {
        var cycles = new SilenceCycleEngine(() => 0.5);
        var settings = Settings();
        settings.RandomDurations = true;
        settings.QuietMaxSeconds = 20;
        settings.MusicMaxSeconds = 40;
        var decision = Decision();

        Assert.Contains("15s", cycles.Evaluate(decision, settings, Start).Status);
        Assert.Contains("30s", cycles.Evaluate(decision, settings, Start.AddSeconds(15)).Status);
        Assert.Equal(1f, cycles.Evaluate(decision, settings, Start.AddSeconds(44)).TargetVolume);
        Assert.Equal(0.2f, cycles.Evaluate(decision, settings, Start.AddSeconds(45)).TargetVolume, 4);
    }

    [Fact]
    public void RuleCycleOverridesAndPausesGlobalCycle()
    {
        var cycles = new SilenceCycleEngine();
        var global = Settings();
        var rule = Rule("Mounted", 10, RuleCondition.Mounted);
        rule.Cycle = Settings();
        rule.Cycle.QuietMinSeconds = 3;
        var engine = Engine(rule);

        cycles.Evaluate(engine.Evaluate(State(), Start), global, Start);
        var ruleStart = cycles.Evaluate(engine.Evaluate(State(mounted: true), Start.AddSeconds(4)), global, Start.AddSeconds(4));
        Assert.Contains("Rule: quiet (3s", ruleStart.Status);
        var ruleMusic = cycles.Evaluate(engine.Evaluate(State(mounted: true), Start.AddSeconds(7)), global, Start.AddSeconds(7));
        Assert.Contains("Rule: music", ruleMusic.Status);
        var resumed = cycles.Evaluate(engine.Evaluate(State(), Start.AddSeconds(8)), global, Start.AddSeconds(8));
        Assert.Contains("Global: quiet (6s", resumed.Status);
        Assert.Contains("Global: music", cycles.Evaluate(engine.Evaluate(State(), Start.AddSeconds(14)), global, Start.AddSeconds(14)).Status);
    }

    [Fact]
    public void WinningInterruptPausesRuleCycleAndResumesItIfStillActive()
    {
        var cycles = new SilenceCycleEngine();
        var global = Settings();
        var mounted = Rule("Mounted", 10, RuleCondition.Mounted);
        mounted.Cycle = Settings();
        var combat = Rule("Combat", 20, RuleCondition.InCombat);
        combat.InterruptSilence = true;
        var engine = Engine(combat, mounted);

        cycles.Evaluate(engine.Evaluate(State(mounted: true), Start), global, Start);
        var interrupted = cycles.Evaluate(engine.Evaluate(State(mounted: true, combat: true), Start.AddSeconds(4)), global, Start.AddSeconds(4));
        Assert.Equal("Interrupted", interrupted.Status);
        Assert.Equal(0.5f, interrupted.TargetVolume);
        var resumed = cycles.Evaluate(engine.Evaluate(State(mounted: true), Start.AddSeconds(104)), global, Start.AddSeconds(104));
        Assert.Contains("Rule: quiet (6s", resumed.Status);
        Assert.Contains("Rule: music", cycles.Evaluate(engine.Evaluate(State(mounted: true), Start.AddSeconds(110)), global, Start.AddSeconds(110)).Status);
    }

    [Fact]
    public void LowerPriorityInterruptCannotBypassWinningCycle()
    {
        var cycles = new SilenceCycleEngine();
        var mounted = Rule("Mounted", 20, RuleCondition.Mounted);
        mounted.Cycle = Settings();
        var combat = Rule("Combat", 10, RuleCondition.InCombat);
        combat.InterruptSilence = true;
        var decision = Engine(mounted, combat).Evaluate(State(mounted: true, combat: true), Start);

        var result = cycles.Evaluate(decision, Settings(), Start);

        Assert.Equal("Mounted", decision.RuleName);
        Assert.Contains("Rule: quiet", result.Status);
        Assert.Equal(0.1f, result.TargetVolume, 4);
    }

    [Fact]
    public void RuleCycleRestartsAfterRuleBecomesInactive()
    {
        var cycles = new SilenceCycleEngine();
        var rule = Rule("Mounted", 10, RuleCondition.Mounted);
        rule.Cycle = Settings();
        var engine = Engine(rule);
        var global = new CycleSettings();

        cycles.Evaluate(engine.Evaluate(State(mounted: true), Start), global, Start);
        cycles.Evaluate(engine.Evaluate(State(mounted: true), Start.AddSeconds(5)), global, Start.AddSeconds(5));
        cycles.Evaluate(engine.Evaluate(State(), Start.AddSeconds(6)), global, Start.AddSeconds(6));
        var restarted = cycles.Evaluate(engine.Evaluate(State(mounted: true), Start.AddSeconds(7)), global, Start.AddSeconds(7));

        Assert.Contains("Rule: quiet (10s", restarted.Status);
    }

    [Fact]
    public void SuspendPausesElapsedTimeAndDisablingCycleResetsIt()
    {
        var cycles = new SilenceCycleEngine();
        var settings = Settings();
        var decision = Decision();
        cycles.Evaluate(decision, settings, Start);
        cycles.Evaluate(decision, settings, Start.AddSeconds(4));
        cycles.Suspend();
        Assert.Contains("6s", cycles.Evaluate(decision, settings, Start.AddSeconds(100)).Status);
        settings.Enabled = false;
        Assert.Equal("Off", cycles.Evaluate(decision, settings, Start.AddSeconds(101)).Status);
        settings.Enabled = true;
        Assert.Contains("10s", cycles.Evaluate(decision, settings, Start.AddSeconds(102)).Status);
    }

    [Fact]
    public void LowHpInterruptionCompetesByPriority()
    {
        var rule = Rule("Mounted", 10, RuleCondition.Mounted);
        var lowHp = new BgmRule
        {
            Name = "Low HP", Conditions = [RuleCondition.LowHp], LowHpThresholdPercent = 30,
            InterruptSilence = true
        };
        var settings = new RuleSettings { Rules = [lowHp, rule] };
        var decision = new RuleEngine(() => settings).Evaluate(State(mounted: true, hp: 20), Start);
        var result = new SilenceCycleEngine().Evaluate(decision, Settings(), Start);

        Assert.Equal("Low HP", decision.RuleName);
        Assert.Equal("Interrupted", result.Status);
    }

    [Fact]
    public void GlobalCyclePausesDuringInterruptAndAppliesToOrdinaryRules()
    {
        var cycles = new SilenceCycleEngine();
        var settings = Settings();
        var ordinary = Rule("Mounted", 10, RuleCondition.Mounted);
        var interrupt = Rule("Combat", 20, RuleCondition.InCombat);
        interrupt.InterruptSilence = true;
        var engine = Engine(ordinary, interrupt);
        cycles.Evaluate(engine.Evaluate(State(), Start), settings, Start);
        var mounted = cycles.Evaluate(engine.Evaluate(State(mounted: true), Start.AddSeconds(3)), settings, Start.AddSeconds(3));
        Assert.Equal(0.1f, mounted.TargetVolume, 4);
        Assert.Contains("7s", mounted.Status);
        Assert.Contains("Interrupted; default cycle paused: quiet (5s remaining)", cycles.Evaluate(engine.Evaluate(State(combat: true), Start.AddSeconds(5)), settings, Start.AddSeconds(5)).Status);
        var resumed = cycles.Evaluate(engine.Evaluate(State(), Start.AddSeconds(50)), settings, Start.AddSeconds(50));
        Assert.Contains("5s", resumed.Status);
    }

    [Fact]
    public void RuleCycleStartsAfterActivationDelay()
    {
        var cycles = new SilenceCycleEngine();
        var rule = Rule("Mounted", 10, RuleCondition.Mounted);
        rule.ActivateAfterSeconds = 5;
        rule.Cycle = Settings();
        var engine = Engine(rule);
        var global = new CycleSettings();
        Assert.Equal("Off", cycles.Evaluate(engine.Evaluate(State(mounted: true), Start), global, Start).Status);
        Assert.Equal("Off", cycles.Evaluate(engine.Evaluate(State(mounted: true), Start.AddSeconds(4)), global, Start.AddSeconds(4)).Status);
        Assert.Contains("Rule: quiet (10s", cycles.Evaluate(engine.Evaluate(State(mounted: true), Start.AddSeconds(5)), global, Start.AddSeconds(5)).Status);
    }

    [Fact]
    public void CycleSettingsAreNormalizedIncludingNullSettings()
    {
        var configuration = new Configuration
        {
            GlobalCycle = new CycleSettings
            {
                QuietVolume = float.NaN, QuietMinSeconds = -1, QuietMaxSeconds = 0,
                MusicMinSeconds = 700, MusicMaxSeconds = 20
            },
            Rules = [new BgmRule { Cycle = null! }]
        };
        configuration.Normalize();
        Assert.Equal(0, configuration.GlobalCycle.QuietVolume);
        Assert.Equal(1, configuration.GlobalCycle.QuietMinSeconds);
        Assert.Equal(1, configuration.GlobalCycle.QuietMaxSeconds);
        Assert.Equal(700, configuration.GlobalCycle.MusicMaxSeconds);
        Assert.NotNull(configuration.Rules[0].Cycle);
    }

    private static CycleSettings Settings() => new()
    {
        Enabled = true, QuietVolume = 0.2f, QuietMinSeconds = 10,
        QuietMaxSeconds = 10, MusicMinSeconds = 20, MusicMaxSeconds = 20
    };

    private static RuleDecision Decision(float volume = 1f) => new() { TargetVolume = volume };
    private static BgmRule Rule(string name, int priority, RuleCondition condition) => new()
    {
        Name = name, Priority = priority, TargetVolume = 0.5f,
        DeactivateAfterSeconds = 0, Conditions = [condition]
    };
    private static RuleEngine Engine(params BgmRule[] rules) =>
        new(() => new RuleSettings { Rules = rules });
    private static BgmStateSnapshot State(bool mounted = false, bool combat = false, float hp = 100f) =>
        new() { HasPlayer = true, IsMounted = mounted, IsInCombat = combat, HpPercent = hp };
}
