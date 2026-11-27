using System;
using System.Collections.Generic;

namespace Bardaelyn.Rules;

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
    LowHp,
    Swimming,
    Underwater,
    EorzeaTime
}

[Serializable]
public sealed class BgmRule
{
    public string Name { get; set; } = "New rule";
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 50;
    public float TargetVolume { get; set; } = 0.5f;
    public float ActivateAfterSeconds { get; set; }
    public float DeactivateAfterSeconds { get; set; }
    public bool SeparateDelays { get; set; }
    public bool RequireAll { get; set; } = true;
    public float LowHpThresholdPercent { get; set; } = 30f;
    public bool UseGlobalFades { get; set; } = true;
    public float FadeInSeconds { get; set; } = 2f;
    public float FadeOutSeconds { get; set; } = 2f;
    public bool SeparateFades { get; set; }
    public List<EorzeaPeriod> TimePeriods { get; set; } = [];
    public List<RuleCondition> Conditions { get; set; } = [];
    public List<RuleCondition> ExcludedConditions { get; set; } = [];
    public bool InterruptSilence { get; set; }
    public CycleSettings Cycle { get; set; } = new();
}

public sealed class RuleSettings
{
    public float DefaultVolume { get; init; } = 1f;
    public EorzeaPeriods TimePeriods { get; init; } = new();
    public IReadOnlyList<BgmRule> Rules { get; init; } = Array.Empty<BgmRule>();
}
