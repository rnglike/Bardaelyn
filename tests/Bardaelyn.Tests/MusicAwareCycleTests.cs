using Bardaelyn;
using Bardaelyn.Rules;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class MusicAwareCycleTests
{
    private static readonly DateTime Start = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly SilenceCycleEngine engine = new();
    private readonly RuleDecision decision = new() { TargetVolume = 1f };
    private readonly CycleSettings settings = new()
    {
        Enabled = true, QuietMinSeconds = 10, QuietMaxSeconds = 10,
        MusicMinSeconds = 20, MusicMaxSeconds = 20
    };

    [Fact]
    public void PausesMusicWhenSilentAndResumesForAnyPositiveCount()
    {
        Evaluate(0, 0);
        Assert.Contains("music (20s", Evaluate(10, 0).Status);
        Assert.Contains("waiting for music", Evaluate(20, 0).Status);
        Assert.Contains("music (20s", Evaluate(21, 2).Status);
        Assert.Contains("music (15s", Evaluate(26, 2).Status);
        Assert.Contains("music (13s", Evaluate(28, 1).Status);
    }

    [Fact]
    public void SeparateModeCorrectsDelayedStopAndKeepsQuietSeparate()
    {
        Evaluate(0, 1);
        Evaluate(10, 1);
        Assert.Contains("music (14s", Evaluate(16, 1).Status);
        var stopped = Evaluate(20, 0);
        Assert.Contains("music (14s", stopped.Status);
        Assert.Contains("music (14s", Evaluate(50, 0).Status);
        Evaluate(51, 1);
        Assert.Contains("quiet (10s", Evaluate(65, 1).Status);
    }

    [Fact]
    public void NaturalGapStartsQuietAndCreditsEstimatedDelayOnlyOnce()
    {
        settings.StartQuietOnNaturalGap = true;
        Evaluate(0, 1);
        Evaluate(10, 1);
        var stopped = Evaluate(16, 0);
        Assert.Contains("quiet (6s", stopped.Status);
        Assert.Contains("quiet (3s", Evaluate(19, 0).Status);
        Assert.Contains("music (20s", Evaluate(22, 0).Status);
        Assert.Contains("music (20s", Evaluate(100, 0).Status);
    }

    [Fact]
    public void ShortObservedMusicIsNotOvercorrected()
    {
        settings.StartQuietOnNaturalGap = true;
        Evaluate(0, 0);
        Evaluate(10, 0);
        Evaluate(11, 1);
        var stopped = Evaluate(13, 0);
        Assert.Contains("quiet (8s", stopped.Status);
    }

    [Fact]
    public void UnavailableFallsBackToElapsedTimeAndDoesNotInventStop()
    {
        settings.StartQuietOnNaturalGap = true;
        Evaluate(0, 1);
        Evaluate(10, 1);
        Assert.Contains("unavailable", Evaluate(15, null).Status);
        Assert.Contains("music (10s", Evaluate(20, null).Status);
        Assert.Contains("music (5s", Evaluate(25, 0).Status);
        Assert.Contains("music (5s", Evaluate(30, 0).Status);
    }

    [Fact]
    public void QuietToMusicTransitionPausesUnusedElapsedTimeWhenNoMusic()
    {
        Evaluate(0, 0);
        Assert.Contains("music (20s", Evaluate(30, 0).Status);
    }

    [Fact]
    public void SuspendPreventsCorrectionForUnobservedTime()
    {
        Evaluate(0, 1);
        Evaluate(10, 1);
        engine.Suspend();
        Assert.Contains("music (20s", Evaluate(100, 0).Status);
    }

    [Fact]
    public void NaturalGapCorrectionIsLimitedToCurrentMusicPhase()
    {
        settings.StartQuietOnNaturalGap = true;
        Evaluate(0, 1);
        Evaluate(9, 1);
        var result = Evaluate(11, 0);
        Assert.Contains("quiet (9s", result.Status);
    }

    [Fact]
    public void DelayCanBeDisabled()
    {
        engine.Evaluate(decision, settings, Start, 1, 0f);
        engine.Evaluate(decision, settings, Start.AddSeconds(10), 1, 0f);
        var result = engine.Evaluate(decision, settings, Start.AddSeconds(15), 0, 0f);
        Assert.Contains("music (15s", result.Status);
    }

    [Fact]
    public void InterruptedCycleRemainsPausedDespiteMusicStop()
    {
        Evaluate(0, 1);
        Evaluate(10, 1);
        var interrupt = new RuleDecision { TargetVolume = 1, InterruptSilence = true };
        Assert.Contains("default cycle paused: music (18s remaining)", engine.Evaluate(interrupt, settings, Start.AddSeconds(12), 1).Status);
        engine.Evaluate(interrupt, settings, Start.AddSeconds(20), 0);
        var result = Evaluate(50, 0);
        Assert.Contains("music (18s", result.Status);
        Assert.Contains("waiting for music", result.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GlobalCycleResumesSamePhaseAndRemainingTimeAfterLongInterruption(bool quiet)
    {
        Evaluate(0, 1);
        if (!quiet)
        {
            Evaluate(10, 1);
        }
        var pauseAt = quiet ? 4 : 14;
        var interrupt = new RuleDecision { TargetVolume = 0.5f, InterruptSilence = true };
        var paused = engine.Evaluate(interrupt, settings, Start.AddSeconds(pauseAt), 1);
        Assert.Contains(quiet ? "quiet (6s remaining)" : "music (16s remaining)", paused.Status);
        engine.Evaluate(interrupt, settings, Start.AddSeconds(pauseAt + 100), 1);
        var resumed = Evaluate(pauseAt + 101, 1);
        Assert.Contains(quiet ? "Global: quiet (6s remaining)" : "Global: music (16s remaining)", resumed.Status);
        Assert.Equal(quiet ? 0f : 1f, resumed.TargetVolume);
    }

    [Fact]
    public void StopCorrectionDoesNotCountTimeSpentInOverridingRule()
    {
        Evaluate(0, 1);
        Evaluate(10, 1);
        var interrupt = new RuleDecision { TargetVolume = 1, InterruptSilence = true };
        engine.Evaluate(interrupt, settings, Start.AddSeconds(11), 1);
        engine.Evaluate(interrupt, settings, Start.AddSeconds(100), 1);
        Evaluate(101, 1);
        var stopped = Evaluate(102, 0);
        Assert.Contains("music (19s remaining)", stopped.Status);
    }

    [Fact]
    public void ExistingSettingsRetainCycleValuesAndDefaultToSeparateMode()
    {
        const string json = """
          {"Version":3,"GlobalCycle":{"Enabled":true,"QuietMinSeconds":11,"MusicMinSeconds":22},
           "Rules":[{"Name":"kept","Cycle":{"Enabled":true,"QuietVolume":0.4}}]}
          """;
        var config = System.Text.Json.JsonSerializer.Deserialize<Configuration>(json)!;
        config.Normalize();
        Assert.Equal(4f, config.MusicStopDelaySeconds);
        Assert.False(config.GlobalCycle.StartQuietOnNaturalGap);
        Assert.Equal(11f, config.GlobalCycle.QuietMinSeconds);
        Assert.Equal(22f, config.GlobalCycle.MusicMinSeconds);
        Assert.Equal("kept", Assert.Single(config.Rules).Name);
        Assert.Equal(0.4f, config.Rules[0].Cycle.QuietVolume);
    }

    private CycleDecision Evaluate(int seconds, int? count) =>
        engine.Evaluate(decision, settings, Start.AddSeconds(seconds), count, 4f);
}
