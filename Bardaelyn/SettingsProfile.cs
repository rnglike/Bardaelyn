using System;
using System.Collections.Generic;
using System.Linq;
using Bardaelyn.Rules;

namespace Bardaelyn;

[Serializable]
public sealed class SettingsProfile
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public float DefaultVolume { get; set; }
    public float FadeDurationSeconds { get; set; }
    public float? FadeInSeconds { get; set; }
    public float? FadeOutSeconds { get; set; }
    public EorzeaPeriods TimePeriods { get; set; } = new();
    public float UpdateIntervalSeconds { get; set; }
    public float MusicStopDelaySeconds { get; set; }
    public float LowHpThresholdPercent { get; set; } = 30f;
    public CycleSettings GlobalCycle { get; set; } = new();
    public List<BgmRule> Rules { get; set; } = [];

    public static SettingsProfile Capture(string name, Configuration config) => new()
    {
        Name = name,
        Enabled = config.Enabled,
        DefaultVolume = config.DefaultVolume,
        FadeDurationSeconds = config.FadeDurationSeconds,
        FadeInSeconds = config.FadeInSeconds,
        FadeOutSeconds = config.FadeOutSeconds,
        TimePeriods = config.TimePeriods.Copy(),
        UpdateIntervalSeconds = config.UpdateIntervalSeconds,
        MusicStopDelaySeconds = config.MusicStopDelaySeconds,
        LowHpThresholdPercent = config.LowHpThresholdPercent,
        GlobalCycle = CopyCycle(config.GlobalCycle),
        Rules = config.Rules.Select(CopyRule).ToList()
    };

    public void Apply(Configuration config)
    {
        config.Enabled = Enabled;
        config.DefaultVolume = DefaultVolume;
        config.FadeDurationSeconds = FadeDurationSeconds;
        config.FadeInSeconds = FadeInSeconds;
        config.FadeOutSeconds = FadeOutSeconds;
        config.TimePeriods = TimePeriods?.Copy() ?? new EorzeaPeriods();
        config.UpdateIntervalSeconds = UpdateIntervalSeconds;
        config.MusicStopDelaySeconds = MusicStopDelaySeconds;
        config.LowHpThresholdPercent = LowHpThresholdPercent;
        config.GlobalCycle = CopyCycle(GlobalCycle);
        config.Rules = Rules.Select(CopyRule).ToList();
    }

    private static CycleSettings CopyCycle(CycleSettings cycle) => new()
    {
        Enabled = cycle.Enabled, RandomDurations = cycle.RandomDurations,
        UseQuietProbability = cycle.UseQuietProbability, QuietChancePercent = cycle.QuietChancePercent,
        AttemptIntervalSeconds = cycle.AttemptIntervalSeconds,
        StartQuietOnNaturalGap = cycle.StartQuietOnNaturalGap, QuietVolume = cycle.QuietVolume,
        QuietMinSeconds = cycle.QuietMinSeconds, QuietMaxSeconds = cycle.QuietMaxSeconds,
        MusicMinSeconds = cycle.MusicMinSeconds, MusicMaxSeconds = cycle.MusicMaxSeconds
    };

    private static BgmRule CopyRule(BgmRule rule) => new()
    {
        Name = rule.Name, Enabled = rule.Enabled, Priority = rule.Priority,
        TargetVolume = rule.TargetVolume, ActivateAfterSeconds = rule.ActivateAfterSeconds,
        DeactivateAfterSeconds = rule.DeactivateAfterSeconds, RequireAll = rule.RequireAll,
        LowHpThresholdPercent = rule.LowHpThresholdPercent,
        UseGlobalFades = rule.UseGlobalFades, FadeInSeconds = rule.FadeInSeconds, FadeOutSeconds = rule.FadeOutSeconds,
        TimePeriods = rule.TimePeriods is null ? [] : [.. rule.TimePeriods],
        Conditions = [.. rule.Conditions], ExcludedConditions = [.. rule.ExcludedConditions],
        InterruptSilence = rule.InterruptSilence, Cycle = CopyCycle(rule.Cycle)
    };
}
