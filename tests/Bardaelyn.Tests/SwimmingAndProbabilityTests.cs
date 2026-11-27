using SamplePlugin.Rules;
using SamplePlugin.State;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class SwimmingAndProbabilityTests
{
    private static readonly DateTime Start = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(RuleCondition.Swimming, true, false)]
    [InlineData(RuleCondition.Underwater, false, true)]
    public void WaterConditionsMatchIndependently(RuleCondition condition, bool swimming, bool underwater)
    {
        var rule = new BgmRule { Name = "Water", Conditions = [condition], DeactivateAfterSeconds = 0 };
        var engine = new RuleEngine(() => new RuleSettings { Rules = [rule] });
        Assert.Equal("Water", engine.Evaluate(new BgmStateSnapshot { IsSwimming = swimming, IsUnderwater = underwater }, Start).RuleName);
        Assert.Equal("Default", engine.Evaluate(new BgmStateSnapshot { IsSwimming = !swimming, IsUnderwater = !underwater }, Start.AddSeconds(1)).RuleName);
    }

    [Fact]
    public void ProbabilityAttemptsImmediatelyAndThenAtMusicTimeIntervals()
    {
        var rolls = new Queue<double>([0.8, 0.1]);
        var engine = new SilenceCycleEngine(() => rolls.Dequeue());
        var cycle = new CycleSettings
        {
            Enabled = true, UseQuietProbability = true, QuietChancePercent = 30,
            AttemptIntervalSeconds = 5, QuietMinSeconds = 10
        };
        var decision = new RuleDecision { TargetVolume = 1 };
        Assert.Contains("quiet (10s", engine.Evaluate(decision, cycle, Start, 1).Status);
        Assert.Contains("music (5s", engine.Evaluate(decision, cycle, Start.AddSeconds(10), 1).Status);
        Assert.Contains("quiet (10s", engine.Evaluate(decision, cycle, Start.AddSeconds(15), 1).Status);
        Assert.Empty(rolls);
    }

    [Fact]
    public void ImmediateAttemptWaitsForMusic()
    {
        var calls = 0;
        var engine = new SilenceCycleEngine(() => { calls++; return 0; });
        var cycle = new CycleSettings { Enabled = true, UseQuietProbability = true, QuietChancePercent = 100, QuietMinSeconds = 10 };
        var decision = new RuleDecision { TargetVolume = 1 };
        engine.Evaluate(decision, cycle, Start, 0);
        Assert.Contains("music", engine.Evaluate(decision, cycle, Start.AddSeconds(10), 0).Status);
        Assert.Equal(0, calls);
        Assert.Contains("quiet", engine.Evaluate(decision, cycle, Start.AddSeconds(20), 1).Status);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void FailedAttemptsPauseWhenOverriddenAndResumeWithRemainingTime()
    {
        var engine = new SilenceCycleEngine(() => 0.9);
        var cycle = new CycleSettings { Enabled = true, UseQuietProbability = true, QuietChancePercent = 30,
            AttemptIntervalSeconds = 10, QuietMinSeconds = 10 };
        var normal = new RuleDecision { TargetVolume = 1 };
        var interrupt = new RuleDecision { TargetVolume = 1, InterruptSilence = true };
        engine.Evaluate(normal, cycle, Start, 1);
        engine.Evaluate(normal, cycle, Start.AddSeconds(10), 1);
        Assert.Contains("music (7s", engine.Evaluate(interrupt, cycle, Start.AddSeconds(13), 1).Status);
        engine.Evaluate(interrupt, cycle, Start.AddSeconds(100), 1);
        Assert.Contains("music (7s", engine.Evaluate(normal, cycle, Start.AddSeconds(101), 1).Status);
    }

    [Fact]
    public void SwitchingToProbabilityAtSameTimestampMakesImmediateAttempt()
    {
        var engine = new SilenceCycleEngine(() => 0);
        var cycle = new CycleSettings { Enabled = true, QuietMinSeconds = 10, MusicMinSeconds = 20,
            QuietChancePercent = 100 };
        var decision = new RuleDecision { TargetVolume = 1 };
        engine.Evaluate(decision, cycle, Start, 1);
        Assert.Contains("music", engine.Evaluate(decision, cycle, Start.AddSeconds(10), 1).Status);
        cycle.UseQuietProbability = true;
        Assert.Contains("quiet", engine.Evaluate(decision, cycle, Start.AddSeconds(10), 1).Status);
    }

    [Fact]
    public void ZeroChanceStaysInMusicAcrossAttemptsAndUnavailableDetection()
    {
        var engine = new SilenceCycleEngine(() => 0);
        var cycle = new CycleSettings { Enabled = true, UseQuietProbability = true, QuietChancePercent = 0,
            AttemptIntervalSeconds = 5, QuietMinSeconds = 10 };
        var decision = new RuleDecision { TargetVolume = 1 };
        engine.Evaluate(decision, cycle, Start, null);
        Assert.Contains("music (5s", engine.Evaluate(decision, cycle, Start.AddSeconds(110), null).Status);
    }

    [Fact]
    public void SuccessfulAttemptChoosesRandomQuietDuration()
    {
        var rolls = new Queue<double>([0.5, 0.1, 0.75]);
        var engine = new SilenceCycleEngine(() => rolls.Dequeue());
        var cycle = new CycleSettings { Enabled = true, RandomDurations = true, UseQuietProbability = true,
            QuietChancePercent = 100, QuietMinSeconds = 10, QuietMaxSeconds = 20 };
        var decision = new RuleDecision { TargetVolume = 1 };
        Assert.Contains("quiet (15s", engine.Evaluate(decision, cycle, Start, 1).Status);
        Assert.Contains("quiet (18s", engine.Evaluate(decision, cycle, Start.AddSeconds(15), 1).Status);
        Assert.Empty(rolls);
    }

    [Fact]
    public void StopCorrectionRestoresOnlyTimeSinceMostRecentAttempt()
    {
        var engine = new SilenceCycleEngine(() => 0.9);
        var cycle = new CycleSettings { Enabled = true, UseQuietProbability = true, QuietChancePercent = 20,
            AttemptIntervalSeconds = 5, QuietMinSeconds = 10 };
        var decision = new RuleDecision { TargetVolume = 1 };
        engine.Evaluate(decision, cycle, Start, 1);
        engine.Evaluate(decision, cycle, Start.AddSeconds(10), 1);
        var stopped = engine.Evaluate(decision, cycle, Start.AddSeconds(17), 0, 4);
        Assert.Contains("music (5s", stopped.Status);
        Assert.Contains("waiting for music", stopped.Status);
    }

    [Fact]
    public void ProbabilityNaturalGapStartsQuietAndCreditsDetectedDelay()
    {
        var engine = new SilenceCycleEngine(() => 0.9);
        var cycle = new CycleSettings { Enabled = true, UseQuietProbability = true, QuietChancePercent = 20,
            AttemptIntervalSeconds = 10, QuietMinSeconds = 10, StartQuietOnNaturalGap = true };
        var decision = new RuleDecision { TargetVolume = 1 };
        engine.Evaluate(decision, cycle, Start, 1);
        engine.Evaluate(decision, cycle, Start.AddSeconds(10), 1);
        var stopped = engine.Evaluate(decision, cycle, Start.AddSeconds(16), 0, 4);
        Assert.Contains("quiet (6s", stopped.Status);
    }

    [Fact]
    public void ZeroChanceHandlesVeryLongElapsedTime()
    {
        var engine = new SilenceCycleEngine(() => 0.5);
        var cycle = new CycleSettings { Enabled = true, UseQuietProbability = true,
            QuietChancePercent = 0, AttemptIntervalSeconds = 1, QuietMinSeconds = 10 };
        var decision = new RuleDecision { TargetVolume = 1 };
        engine.Evaluate(decision, cycle, Start, null);
        Assert.Contains("music (1s", engine.Evaluate(decision, cycle, Start.AddSeconds(1_000_000_010), null).Status);
    }
}
