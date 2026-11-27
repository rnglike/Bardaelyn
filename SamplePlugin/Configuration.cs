using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using SamplePlugin.Rules;

namespace SamplePlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;
    public float DefaultVolume { get; set; } = 1f;
    public float FadeDurationSeconds { get; set; } = 2f;
    public float? FadeInSeconds { get; set; }
    public float? FadeOutSeconds { get; set; }
    public EorzeaPeriods TimePeriods { get; set; } = new();
    public float UpdateIntervalSeconds { get; set; } = 0.25f;
    public float MusicStopDelaySeconds { get; set; } = 4f;
    public float LowHpThresholdPercent { get; set; } = 30f;
    public CycleSettings GlobalCycle { get; set; } = new();

    public List<BgmRule> Rules { get; set; } = [];
    public List<SettingsProfile>? Profiles { get; set; }
    public string? ActiveProfileName { get; set; }

    public void Save()
    {
        Normalize();
        SaveActiveProfile();
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public void Normalize()
    {
        DefaultVolume = ClampFinite(DefaultVolume, 1f, 0f, 1f);
        FadeDurationSeconds = ClampFinite(FadeDurationSeconds, 2f, 0f, 60f);
        FadeInSeconds = ClampFinite(FadeInSeconds ?? FadeDurationSeconds, FadeDurationSeconds, 0f, 60f);
        FadeOutSeconds = ClampFinite(FadeOutSeconds ?? FadeDurationSeconds, FadeDurationSeconds, 0f, 60f);
        TimePeriods ??= new EorzeaPeriods();
        TimePeriods.Normalize();
        UpdateIntervalSeconds = ClampFinite(UpdateIntervalSeconds, 0.25f, 0.05f, 2f);
        MusicStopDelaySeconds = ClampFinite(MusicStopDelaySeconds, 4f, 0f, 30f);
        LowHpThresholdPercent = ClampFinite(LowHpThresholdPercent, 30f, 1f, 100f);
        GlobalCycle ??= new CycleSettings();
        GlobalCycle.Normalize();
        Rules ??= [];

        for (var index = Rules.Count - 1; index >= 0; index--)
        {
            var rule = Rules[index];
            if (rule is null)
            {
                Rules.RemoveAt(index);
                continue;
            }

            rule.Name = string.IsNullOrWhiteSpace(rule.Name)
                ? $"Rule {index + 1}"
                : rule.Name.Trim();
            if (rule.Name.Length > 64)
            {
                rule.Name = rule.Name[..64];
            }

            rule.TargetVolume = ClampFinite(rule.TargetVolume, 0.5f, 0f, 1f);
            rule.ActivateAfterSeconds = ClampFinite(rule.ActivateAfterSeconds, 0f, 0f, 3600f);
            rule.DeactivateAfterSeconds = ClampFinite(rule.DeactivateAfterSeconds, 3f, 0f, 3600f);
            rule.FadeInSeconds = ClampFinite(rule.FadeInSeconds, 2f, 0f, 60f);
            rule.FadeOutSeconds = ClampFinite(rule.FadeOutSeconds, 2f, 0f, 60f);
            rule.TimePeriods ??= [];
            rule.TimePeriods = rule.TimePeriods.Where(period => Enum.IsDefined(period)).Distinct().ToList();
            if (Version < 3)
            {
                rule.LowHpThresholdPercent = LowHpThresholdPercent;
            }
            rule.LowHpThresholdPercent = ClampFinite(rule.LowHpThresholdPercent, 30f, 1f, 100f);
            rule.Priority = Math.Clamp(rule.Priority, -100000, 100000);
            rule.Cycle ??= new CycleSettings();
            rule.Cycle.Normalize();
            rule.Conditions ??= [];
            var validConditions = rule.Conditions
                .Where(condition => Enum.IsDefined(condition))
                .Distinct()
                .ToList();
            rule.Conditions = validConditions;
            rule.ExcludedConditions ??= [];
            rule.ExcludedConditions = rule.ExcludedConditions
                .Where(condition => Enum.IsDefined(condition) && !rule.Conditions.Contains(condition))
                .Distinct()
                .ToList();
        }

        if (Version < 2)
        {
            Rules = Rules.OrderByDescending(rule => rule.Priority).ToList();
        }

        Version = Math.Max(3, Version);
        if (Profiles is null)
        {
            ActiveProfileName = "Current settings";
            Profiles = [SettingsProfile.Capture(ActiveProfileName, this)];
        }
    }

    public void SaveActiveProfile()
    {
        if (Profiles is null || ActiveProfileName is null)
        {
            throw new InvalidOperationException("The active profile is not initialized.");
        }
        var index = Profiles.FindIndex(profile => profile.Name == ActiveProfileName);
        if (index < 0)
        {
            throw new InvalidOperationException("The active profile could not be found.");
        }
        Profiles[index] = SettingsProfile.Capture(ActiveProfileName, this);
    }

    public void CreateProfile(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > 64 ||
            Profiles is null || Profiles.Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Profile names must be unique and contain 1 to 64 characters.", nameof(name));
        }
        SaveActiveProfile();
        Profiles.Add(SettingsProfile.Capture(name, this));
        ActiveProfileName = name;
    }

    public void SwitchProfile(string name)
    {
        if (Profiles is null)
        {
            throw new InvalidOperationException("Profiles are not initialized.");
        }
        var target = Profiles.Find(profile => profile.Name == name)
            ?? throw new ArgumentException("Profile not found.", nameof(name));
        if (name == ActiveProfileName)
        {
            return;
        }
        SaveActiveProfile();
        target.Apply(this);
        ActiveProfileName = target.Name;
        Normalize();
    }

    public void MovePriorityEntry(int index, int direction)
    {
        var target = index + direction;
        if ((direction != -1 && direction != 1) || index < 0 || index >= Rules.Count ||
            target < 0 || target >= Rules.Count)
        {
            return;
        }

        (Rules[index], Rules[target]) = (Rules[target], Rules[index]);
    }

    public void RemoveRuleAt(int index)
    {
        if (index < 0 || index >= Rules.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        Rules.RemoveAt(index);
    }

    public RuleSettings GetRuleSettings() => new()
    {
        DefaultVolume = DefaultVolume,
        TimePeriods = TimePeriods,
        Rules = Rules
    };

    private static float ClampFinite(float value, float fallback, float minimum, float maximum) =>
        float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
