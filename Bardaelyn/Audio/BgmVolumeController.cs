using System;
using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Sound;

namespace Bardaelyn.Audio;

public sealed class BgmVolumeController
{
    private float targetVolume = 1f;
    private float currentVolume;
    private float originalVolume = 1f;
    private float targetRatio = 1f;
    private float fadeDurationSeconds = 2f;
    private bool isControlling;
    private bool isReleasing;
    private readonly IPluginLog? log;
    private readonly Func<float, bool>? applyVolume;
    private bool soundManagerWarningLogged;

    public BgmVolumeController(IPluginLog log)
    {
        this.log = log;
    }

    internal BgmVolumeController(Func<float, bool> applyVolume)
    {
        this.applyVolume = applyVolume;
    }

    public float TargetVolume => targetVolume;
    public float CurrentVolume => currentVolume;
    public string ActiveRule { get; set; } = "Default";

    public bool Initialize(IGameConfig gameConfig)
    {
        if (!gameConfig.TryGet(SystemConfigOption.SoundBgm, out uint configuredVolume))
        {
            return false;
        }

        InitializeVolume(configuredVolume / 100f);
        return true;
    }

    internal void InitializeVolume(float configuredVolume)
    {
        originalVolume = Math.Clamp(configuredVolume, 0f, 1f);
        currentVolume = originalVolume;
        targetVolume = originalVolume;
    }

    public void UpdateBaseline(float configuredVolume)
    {
        var newBaseline = Math.Clamp(configuredVolume, 0f, 1f);
        if (Math.Abs(newBaseline - originalVolume) < 0.0001f)
        {
            return;
        }

        originalVolume = newBaseline;
        if (!isControlling)
        {
            currentVolume = newBaseline;
            targetVolume = newBaseline;
        }
        else
        {
            targetVolume = isReleasing ? newBaseline : newBaseline * targetRatio;
        }
    }

    public void SetTarget(float volumeRatio, float fadeDurationSeconds)
    {
        targetRatio = Math.Clamp(volumeRatio, 0f, 1f);
        targetVolume = originalVolume * targetRatio;
        this.fadeDurationSeconds = Math.Max(0f, fadeDurationSeconds);
        isControlling = true;
        isReleasing = false;
    }

    public void Release(float fadeDurationSeconds)
    {
        if (!isControlling)
        {
            return;
        }

        targetVolume = originalVolume;
        targetRatio = 1f;
        this.fadeDurationSeconds = Math.Max(0f, fadeDurationSeconds);
        isReleasing = true;
    }

    public void Update(TimeSpan elapsed)
    {
        if (!isControlling)
        {
            return;
        }

        if (fadeDurationSeconds <= 0f)
        {
            currentVolume = targetVolume;
        }
        else
        {
            var maxDelta = elapsed.TotalSeconds / fadeDurationSeconds;
            currentVolume = MoveToward(currentVolume, targetVolume, (float)maxDelta);
        }

        var applied = Apply(currentVolume);

        if (applied && isReleasing && Math.Abs(currentVolume - originalVolume) < 0.0001f)
        {
            currentVolume = originalVolume;
            isControlling = false;
            isReleasing = false;
        }
    }

    public void Restore()
    {
        if (!isControlling)
        {
            return;
        }

        targetVolume = originalVolume;
        targetRatio = 1f;
        currentVolume = originalVolume;
        if (Apply(originalVolume))
        {
            isControlling = false;
            isReleasing = false;
        }
    }

    private static float MoveToward(float from, float to, float delta)
    {
        if (Math.Abs(to - from) <= delta)
        {
            return to;
        }

        return from + Math.Sign(to - from) * delta;
    }

    private unsafe bool Apply(float volume)
    {
        if (applyVolume is not null)
        {
            return applyVolume(Math.Clamp(volume, 0f, 1f));
        }
        var manager = SoundManager.Instance();
        if (manager == null)
        {
            if (!soundManagerWarningLogged)
            {
                log!.Warning("The game's sound manager is unavailable; BGM volume cannot be changed.");
                soundManagerWarningLogged = true;
            }

            return false;
        }

        soundManagerWarningLogged = false;
        manager->SetVolume(SoundBus.Music, Math.Clamp(volume, 0f, 1f));
        return true;
    }
}
