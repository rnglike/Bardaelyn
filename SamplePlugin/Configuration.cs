using Dalamud.Configuration;
using System;
using System.Collections.Generic;

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
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

[Serializable]
public enum RuleCondition
{
    Mounted,
    Moving,
    Idle,
    InCombat,
    InDuty,
    InCutscene,
    HasTarget,
    LowHp
}

[Serializable]
public sealed class BgmRule
{
    public string Name { get; set; } = "New rule";
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 50;
    public float TargetVolume { get; set; } = 0.5f;
    public float ActivateAfterSeconds { get; set; }
    public float DeactivateAfterSeconds { get; set; } = 3f;
    public bool RequireAll { get; set; } = true;
    public List<RuleCondition> Conditions { get; set; } = [];
}
