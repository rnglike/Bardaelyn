using System;
using System.Collections.Generic;

namespace SamplePlugin.Rules;

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

public sealed class RuleSettings
{
    public float DefaultVolume { get; init; } = 1f;
    public float LowHpThresholdPercent { get; init; } = 30f;
    public bool LowHpRuleEnabled { get; init; } = true;
    public float LowHpVolume { get; init; } = 1f;
    public int LowHpPriority { get; init; } = 100;
    public IReadOnlyList<BgmRule> Rules { get; init; } = Array.Empty<BgmRule>();
}
