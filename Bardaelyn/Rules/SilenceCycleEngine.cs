using System;
using System.Collections.Generic;
using System.Linq;

namespace Bardaelyn.Rules;

public sealed class CycleDecision
{
    public float TargetVolume { get; init; }
    public string Status { get; init; } = "Off";
}

public sealed class SilenceCycleEngine
{
    private readonly Func<double> random;
    private readonly Dictionary<BgmRule, CycleState> ruleCycles = [];
    private CycleState? globalCycle;
    private CycleState? runningCycle;
    private DateTime? lastUpdate;
    private bool? lastMusicPlaying;
    private double positiveSeconds;

    public SilenceCycleEngine(Func<double>? random = null)
    {
        this.random = random ?? Random.Shared.NextDouble;
    }

    public CycleDecision Evaluate(RuleDecision decision, CycleSettings globalSettings, DateTime now,
        int? musicSoundCount = null, float stopDelaySeconds = 4f)
    {
        bool? musicPlaying = musicSoundCount >= 0 ? musicSoundCount > 0 : null;
        if (lastUpdate is not null && runningCycle is not null && now > lastUpdate.Value)
        {
            runningCycle.Advance((now - lastUpdate.Value).TotalSeconds, lastMusicPlaying, random);
        }

        if (lastUpdate is not null && now > lastUpdate.Value && lastMusicPlaying == true)
        {
            positiveSeconds += (now - lastUpdate.Value).TotalSeconds;
        }
        if (lastMusicPlaying == true && musicPlaying == false && runningCycle is not null)
        {
            var delay = float.IsFinite(stopDelaySeconds) ? Math.Clamp(stopDelaySeconds, 0f, 30f) : 0f;
            runningCycle.OnMusicStopped(Math.Min(delay, positiveSeconds), random);
        }
        if (musicPlaying != true || lastMusicPlaying != true)
        {
            positiveSeconds = 0;
        }

        lastUpdate = now;
        lastMusicPlaying = musicPlaying;
        if (!globalSettings.Enabled)
        {
            globalCycle = null;
        }

        foreach (var rule in ruleCycles.Keys
                     .Where(rule => !decision.ActiveRules.Contains(rule) || !rule.Cycle.Enabled)
                     .ToArray())
        {
            ruleCycles.Remove(rule);
        }

        var previousCycle = runningCycle;
        runningCycle = null;
        if (decision.InterruptSilence)
        {
            positiveSeconds = 0;
            var status = globalCycle is not null && globalSettings.Enabled
                ? $"Interrupted; default cycle paused: {globalCycle.PhaseStatus}"
                : "Interrupted";
            return new CycleDecision { TargetVolume = decision.TargetVolume, Status = status };
        }

        var selected = decision.SelectedRule;
        if (selected is not null && selected.Cycle.Enabled)
        {
            if (!ruleCycles.TryGetValue(selected, out var cycle))
            {
                cycle = new CycleState(selected.Cycle, random);
                ruleCycles[selected] = cycle;
            }

            runningCycle = cycle;
        }
        else if (globalSettings.Enabled)
        {
            globalCycle ??= new CycleState(globalSettings, random);
            runningCycle = globalCycle;
        }

        if (!ReferenceEquals(previousCycle, runningCycle))
        {
            positiveSeconds = 0;
        }

        if (runningCycle is null)
        {
            return new CycleDecision { TargetVolume = decision.TargetVolume };
        }

        runningCycle.SyncMode(random);
        runningCycle.CheckImmediateAttempt(musicPlaying, random);
        var playbackStatus = musicPlaying is null
            ? " [music detection unavailable; elapsed-time fallback]"
            : !runningCycle.IsQuiet && musicPlaying == false ? " [waiting for music]" : "";
        return new CycleDecision
        {
            TargetVolume = decision.TargetVolume * (runningCycle.IsQuiet ? runningCycle.Settings.QuietVolume : 1f),
            Status = $"{(ReferenceEquals(runningCycle, globalCycle) ? "Global" : "Rule")}: " +
                     $"{runningCycle.PhaseStatus}{playbackStatus}"
        };
    }

    public void Suspend()
    {
        lastUpdate = null;
        runningCycle = null;
        lastMusicPlaying = null;
        positiveSeconds = 0;
    }

    private sealed class CycleState
    {
        public CycleSettings Settings { get; }
        public bool IsQuiet { get; private set; } = true;
        public double RemainingSeconds { get; private set; }
        public string PhaseStatus => $"{(IsQuiet ? "quiet" : "music")} ({RemainingSeconds:0}s remaining)";
        private double phaseDuration;
        private bool immediateAttemptPending;
        private bool probabilityMode;

        public CycleState(CycleSettings settings, Func<double> random)
        {
            Settings = settings;
            probabilityMode = settings.UseQuietProbability;
            RemainingSeconds = phaseDuration = NextDuration(random);
        }

        public void Advance(double elapsed, bool? musicPlaying, Func<double> random)
        {
            SyncMode(random);
            CheckImmediateAttempt(musicPlaying, random);
            while (elapsed > 0)
            {
                if (!IsQuiet && musicPlaying == false)
                {
                    return;
                }
                if (!IsQuiet && probabilityMode && Settings.QuietChancePercent <= 0 &&
                    elapsed >= RemainingSeconds)
                {
                    elapsed -= RemainingSeconds;
                    RemainingSeconds = phaseDuration = Settings.AttemptIntervalSeconds;
                    RemainingSeconds -= elapsed % phaseDuration;
                    return;
                }
                if (elapsed < RemainingSeconds)
                {
                    RemainingSeconds -= elapsed;
                    return;
                }
                elapsed -= RemainingSeconds;
                if (!IsQuiet && probabilityMode && random() * 100 >= Settings.QuietChancePercent)
                {
                    RemainingSeconds = phaseDuration = Settings.AttemptIntervalSeconds;
                    continue;
                }
                IsQuiet = !IsQuiet;
                if (!IsQuiet && probabilityMode)
                {
                    RemainingSeconds = phaseDuration = Settings.AttemptIntervalSeconds;
                    immediateAttemptPending = true;
                    CheckImmediateAttempt(musicPlaying, random);
                }
                else
                {
                    RemainingSeconds = phaseDuration = NextDuration(random);
                }
            }
        }

        public void SyncMode(Func<double> random)
        {
            if (probabilityMode != Settings.UseQuietProbability)
            {
                probabilityMode = Settings.UseQuietProbability;
                if (!IsQuiet)
                {
                    immediateAttemptPending = probabilityMode;
                    RemainingSeconds = phaseDuration = probabilityMode
                        ? Settings.AttemptIntervalSeconds : NextDuration(random);
                }
            }
            if (!probabilityMode) immediateAttemptPending = false;
        }

        public void CheckImmediateAttempt(bool? musicPlaying, Func<double> random)
        {
            if (!immediateAttemptPending || IsQuiet || musicPlaying == false)
            {
                return;
            }
            immediateAttemptPending = false;
            if (random() * 100 < Settings.QuietChancePercent)
            {
                IsQuiet = true;
                RemainingSeconds = phaseDuration = NextDuration(random);
            }
        }

        public void OnMusicStopped(double estimatedDelay, Func<double> random)
        {
            if (IsQuiet)
            {
                return;
            }

            var corrected = Math.Min(estimatedDelay, phaseDuration - RemainingSeconds);
            if (Settings.StartQuietOnNaturalGap)
            {
                IsQuiet = true;
                RemainingSeconds = phaseDuration = NextDuration(random);
                Advance(corrected, false, random);
            }
            else
            {
                RemainingSeconds += corrected;
            }
        }

        private double NextDuration(Func<double> random)
        {
            var minimum = IsQuiet ? Settings.QuietMinSeconds : Settings.MusicMinSeconds;
            var maximum = IsQuiet ? Settings.QuietMaxSeconds : Settings.MusicMaxSeconds;
            return Settings.RandomDurations ? minimum + (maximum - minimum) * random() : minimum;
        }
    }
}
