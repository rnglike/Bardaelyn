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
    public float UpdateIntervalSeconds { get; set; } = 0.25f;
    public float LowHpThresholdPercent { get; set; } = 30f;
    public bool LowHpRuleEnabled { get; set; } = true;
    public float LowHpVolume { get; set; } = 1f;
    public int LowHpPriority { get; set; } = 100;

    public List<BgmRule> Rules { get; set; } =
    [
        new BgmRule
        {
            Name = "Ducked while mounted",
            Enabled = true,
            Priority = 50,
            TargetVolume = 0.15f,
            ActivateAfterSeconds = 120f,
            DeactivateAfterSeconds = 5f,
            RequireAll = true,
            Conditions =
            [
                RuleCondition.Mounted,
                RuleCondition.Moving
            ]
        },
        new BgmRule
        {
            Name = "Muted while idle",
            Enabled = true,
            Priority = 40,
            TargetVolume = 0.10f,
            ActivateAfterSeconds = 300f,
            DeactivateAfterSeconds = 5f,
            RequireAll = true,
            Conditions =
            [
                RuleCondition.Idle
            ]
        }
    ];

    public void Save()
    {
        Normalize();
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public void Normalize()
    {
        Version = Math.Max(1, Version);
        DefaultVolume = ClampFinite(DefaultVolume, 1f, 0f, 1f);
        FadeDurationSeconds = ClampFinite(FadeDurationSeconds, 2f, 0f, 60f);
        UpdateIntervalSeconds = ClampFinite(UpdateIntervalSeconds, 0.25f, 0.05f, 2f);
        LowHpThresholdPercent = ClampFinite(LowHpThresholdPercent, 30f, 1f, 100f);
        LowHpVolume = ClampFinite(LowHpVolume, 1f, 0f, 1f);
        LowHpPriority = Math.Clamp(LowHpPriority, -100000, 100000);
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
            rule.Priority = Math.Clamp(rule.Priority, -100000, 100000);
            rule.Conditions ??= [];
            var validConditions = rule.Conditions
                .Where(condition => Enum.IsDefined(condition))
                .Distinct()
                .ToList();
            rule.Conditions = validConditions;
        }
    }

    public RuleSettings GetRuleSettings() => new()
    {
        DefaultVolume = DefaultVolume,
        LowHpThresholdPercent = LowHpThresholdPercent,
        LowHpRuleEnabled = LowHpRuleEnabled,
        LowHpVolume = LowHpVolume,
        LowHpPriority = LowHpPriority,
        Rules = Rules
    };

    private static float ClampFinite(float value, float fallback, float minimum, float maximum) =>
        float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
