using Bardaelyn.Rules;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class RuleConflictAnalyzerTests
{
    // Detects when a general combat rule covers a narrower combat-and-HP rule.
    [Fact]
    public void DetectsCombatCoveringCombatAndLowHp()
    {
        var battle = new BgmRule { Name = "Battle", Conditions = [RuleCondition.InCombat] };
        var heat = new BgmRule { Name = "Heat", Conditions = [RuleCondition.InCombat, RuleCondition.LowHp] };
        Assert.Same(battle, RuleConflictAnalyzer.FindCoveringRule([battle, heat], 1));
        Assert.Null(RuleConflictAnalyzer.FindCoveringRule([heat, battle], 1));
    }

    // Accounts for exclusions, OR matching, and disabled rules in coverage.
    [Fact]
    public void HonorsExclusionsAnyConditionsAndDisabledRules()
    {
        var broad = new BgmRule
        {
            Conditions = [RuleCondition.InCombat, RuleCondition.InDuty], RequireAll = false,
            ExcludedConditions = [RuleCondition.Mounted]
        };
        var narrow = new BgmRule { Conditions = [RuleCondition.InCombat] };
        Assert.Null(RuleConflictAnalyzer.FindCoveringRule([broad, narrow], 1));
        narrow.ExcludedConditions.Add(RuleCondition.Mounted);
        Assert.Same(broad, RuleConflictAnalyzer.FindCoveringRule([broad, narrow], 1));
        broad.Enabled = false;
        Assert.Null(RuleConflictAnalyzer.FindCoveringRule([broad, narrow], 1));
    }

    // Compares HP thresholds before reporting coverage.
    [Fact]
    public void AccountsForDifferentHpThresholds()
    {
        var upper = new BgmRule { Conditions = [RuleCondition.LowHp], LowHpThresholdPercent = 20 };
        var lower = new BgmRule { Conditions = [RuleCondition.LowHp], LowHpThresholdPercent = 30 };
        Assert.Null(RuleConflictAnalyzer.FindCoveringRule([upper, lower], 1));
        upper.LowHpThresholdPercent = 40;
        Assert.Same(upper, RuleConflictAnalyzer.FindCoveringRule([upper, lower], 1));
    }

    // Detects possible idle/water overlap without reporting impossible idle/combat overlap.
    [Fact]
    public void FindsFeasibleOverlapsBetweenDifferentConditions()
    {
        var idle = new BgmRule { Conditions = [RuleCondition.Idle] };
        var water = new BgmRule { Conditions = [RuleCondition.Underwater] };
        var combat = new BgmRule { Conditions = [RuleCondition.InCombat] };
        Assert.Same(idle, RuleConflictAnalyzer.FindOverlappingRule([idle, water], 1));
        Assert.Null(RuleConflictAnalyzer.FindOverlappingRule([combat, idle], 1));
        water.ExcludedConditions.Add(RuleCondition.Idle);
        Assert.Null(RuleConflictAnalyzer.FindOverlappingRule([idle, water], 1));
    }
}
