using Bardaelyn.Audio;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class AudioControllerTests
{
    [Fact]
    public void FadesAndScalesAgainstLiveGameSetting()
    {
        var applied = new List<float>();
        var controller = new BgmVolumeController(value => { applied.Add(value); return true; });
        controller.InitializeVolume(0.8f);
        controller.SetTarget(0.25f, 2f);
        controller.Update(TimeSpan.FromSeconds(0.5));
        Assert.Equal(0.55f, controller.CurrentVolume, 4);
        Assert.Equal(0.2f, controller.TargetVolume, 4);
        controller.UpdateBaseline(0.4f);
        Assert.Equal(0.1f, controller.TargetVolume, 4);
        controller.Update(TimeSpan.FromSeconds(2));
        Assert.Equal(0.1f, applied[^1], 4);
    }

    [Fact]
    public void ReleaseFadesToUpdatedBaselineWithoutJumping()
    {
        var controller = new BgmVolumeController(_ => true);
        controller.InitializeVolume(0.8f);
        controller.SetTarget(0.25f, 0);
        controller.Update(TimeSpan.Zero);
        controller.Release(2);
        controller.Update(TimeSpan.FromSeconds(0.2));
        Assert.Equal(0.3f, controller.CurrentVolume, 4);
        controller.UpdateBaseline(0.6f);
        Assert.Equal(0.3f, controller.CurrentVolume, 4);
        Assert.Equal(0.6f, controller.TargetVolume, 4);
        controller.Update(TimeSpan.FromSeconds(1));
        Assert.Equal(0.6f, controller.CurrentVolume, 4);
    }

    [Fact]
    public void ReleaseRetriesWhenApplicationFails()
    {
        var available = true;
        var attempts = 0;
        var controller = new BgmVolumeController(_ => { attempts++; return available; });
        controller.InitializeVolume(1);
        controller.SetTarget(0, 0);
        controller.Update(TimeSpan.Zero);
        available = false;
        controller.Release(0);
        controller.Update(TimeSpan.Zero);
        available = true;
        controller.Update(TimeSpan.Zero);
        Assert.Equal(3, attempts);
        controller.Update(TimeSpan.Zero);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public void RestoreAppliesLatestBaselineImmediately()
    {
        var applied = new List<float>();
        var controller = new BgmVolumeController(value => { applied.Add(value); return true; });
        controller.InitializeVolume(0.8f);
        controller.SetTarget(0, 0);
        controller.Update(TimeSpan.Zero);
        controller.UpdateBaseline(0.6f);
        controller.Restore();
        Assert.Equal(0.6f, applied[^1], 4);
        controller.Update(TimeSpan.FromSeconds(1));
        Assert.Equal(2, applied.Count);
    }
}
