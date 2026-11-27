using System.Text.Json;
using Bardaelyn;
using Bardaelyn.Rules;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class ProfileTests
{
    // Creates the initial profile without changing existing settings.
    [Fact]
    public void ExistingSettingsBecomeInitialProfileWithoutChangingValues()
    {
        var config = new Configuration
        {
            Version = 3, DefaultVolume = 0.25f,
            GlobalCycle = new CycleSettings { Enabled = true, QuietMinSeconds = 25 },
            Rules = [new BgmRule { Name = "Battle", InterruptSilence = true, Conditions = [RuleCondition.InCombat] }]
        };
        config.Normalize();
        Assert.Equal("Current settings", config.ActiveProfileName);
        Assert.Equal(0.25f, Assert.Single(config.Profiles!).DefaultVolume);
        Assert.True(config.Rules[0].InterruptSilence);
        Assert.Equal(25f, config.GlobalCycle.QuietMinSeconds);
    }

    // Saves the outgoing profile and keeps nested settings independent.
    [Fact]
    public void SwitchingAutosavesAndCopiesNestedSettings()
    {
        var config = new Configuration { Version = 3, Rules = [new BgmRule { Name = "Battle" }] };
        config.Normalize();
        config.CreateProfile("Exploration");
        config.DefaultVolume = 0.2f;
        config.Rules[0].Conditions.Add(RuleCondition.Mounted);
        config.GlobalCycle.Enabled = true;
        config.SwitchProfile("Current settings");
        Assert.Equal(1f, config.DefaultVolume);
        Assert.Empty(config.Rules[0].Conditions);
        Assert.False(config.GlobalCycle.Enabled);
        config.Rules[0].Name = "Changed";
        config.SwitchProfile("Exploration");
        Assert.Equal(0.2f, config.DefaultVolume);
        Assert.Equal("Battle", config.Rules[0].Name);
        Assert.Equal([RuleCondition.Mounted], config.Rules[0].Conditions);
        Assert.True(config.GlobalCycle.Enabled);
        config.SwitchProfile("Current settings");
        Assert.Equal("Changed", config.Rules[0].Name);
    }

    // Preserves profiles through serialization and rejects invalid profile names.
    [Fact]
    public void ProfilesSurviveRoundTripAndRejectDuplicates()
    {
        var config = new Configuration();
        config.Normalize();
        config.CreateProfile("Other");
        config.DefaultVolume = 0.33f;
        config.SaveActiveProfile();
        var restored = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;
        restored.Normalize();
        Assert.Equal("Other", restored.ActiveProfileName);
        Assert.Equal(2, restored.Profiles!.Count);
        restored.SwitchProfile("Current settings");
        restored.SwitchProfile("Other");
        Assert.Equal(0.33f, restored.DefaultVolume);
        Assert.Throws<ArgumentException>(() => restored.CreateProfile("other"));
        Assert.Throws<ArgumentException>(() => restored.SwitchProfile("missing"));
        Assert.Equal(0.33f, restored.DefaultVolume);
    }

    // Copies groups, fade mode, and cycle options independently across profiles and duplicates.
    [Fact]
    public void CopiesNewOptionsAcrossProfilesAndDuplicateRules()
    {
        var config = new Configuration { Version = 5, ExactFadeDuration = true,
            GlobalCycle = new CycleSettings { UseDefaultCycleFade = true, DefaultCycleFadeSeconds = 7 },
            Rules = [new BgmRule { Group = "Travel", Conditions = [RuleCondition.Mounted],
                Cycle = new CycleSettings { MinimumMusicSeconds = 12 } }] };
        config.Normalize();
        var duplicate = SettingsProfile.CopyRule(config.Rules[0]);
        duplicate.Conditions.Add(RuleCondition.Moving);
        duplicate.Cycle.MinimumMusicSeconds = 22;
        Assert.Single(config.Rules[0].Conditions);
        Assert.Equal(12, config.Rules[0].Cycle.MinimumMusicSeconds);
        config.CreateProfile("Other");
        config.ExactFadeDuration = false;
        config.Rules[0].Group = "Other";
        config.GlobalCycle.DefaultCycleFadeSeconds = 3;
        config.SwitchProfile("Current settings");
        Assert.True(config.ExactFadeDuration);
        Assert.Equal("Travel", config.Rules[0].Group);
        Assert.True(config.GlobalCycle.UseDefaultCycleFade);
        Assert.Equal(7, config.GlobalCycle.DefaultCycleFadeSeconds);
        Assert.Equal(12, config.Rules[0].Cycle.MinimumMusicSeconds);
    }

    // Loads a version 5 profile without new options and preserves its rule and cycle tuning.
    [Fact]
    public void ExistingVersionFiveProfileKeepsSettingsWithNewOptionsDisabled()
    {
        const string json = """
            {"Version":5,"ActiveProfileName":"Current settings","DefaultVolume":0.5,
             "GlobalCycle":{"Enabled":true,"RandomDurations":true,"QuietMinSeconds":100,"QuietMaxSeconds":200,
                            "MusicMinSeconds":30,"MusicMaxSeconds":60,"StartQuietOnNaturalGap":true},
             "Rules":[{"Name":"First","Conditions":[3],"TargetVolume":0.75,"FadeInSeconds":10,"FadeOutSeconds":10},
                      {"Name":"Idle","Conditions":[2],"ActivateAfterSeconds":30,"DeactivateAfterSeconds":0,
                       "SeparateDelays":true,"FadeInSeconds":30,"FadeOutSeconds":10,"SeparateFades":true,
                       "Cycle":{"Enabled":true,"UseQuietProbability":true,"QuietChancePercent":50,
                                "AttemptIntervalSeconds":15,"QuietMinSeconds":200,"QuietMaxSeconds":400}}],
             "Profiles":[{"Name":"Current settings","Rules":[]}]}
            """;
        var config = JsonSerializer.Deserialize<Configuration>(json)!;
        config.Normalize();
        config.SaveActiveProfile();
        Assert.Equal(5, config.Version);
        Assert.Equal(0.5f, config.DefaultVolume);
        Assert.False(config.ExactFadeDuration);
        Assert.False(config.GlobalCycle.UseDefaultCycleFade);
        Assert.Equal(0, config.GlobalCycle.MinimumMusicSeconds);
        Assert.Equal(100, config.GlobalCycle.QuietMinSeconds);
        Assert.Equal(60, config.GlobalCycle.MusicMaxSeconds);
        Assert.Equal(["First", "Idle"], config.Rules.Select(rule => rule.Name));
        Assert.Equal(10, config.Rules[0].FadeInSeconds);
        Assert.Equal(30, config.Rules[1].ActivateAfterSeconds);
        Assert.Equal(0, config.Rules[1].DeactivateAfterSeconds);
        Assert.Equal(30, config.Rules[1].FadeInSeconds);
        Assert.Equal(10, config.Rules[1].FadeOutSeconds);
        Assert.Equal(50, config.Rules[1].Cycle.QuietChancePercent);
        Assert.Equal(0, config.Rules[1].Cycle.MinimumMusicSeconds);
        Assert.Equal(30, config.Profiles![0].Rules[1].FadeInSeconds);
    }
}
