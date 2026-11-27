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
}
