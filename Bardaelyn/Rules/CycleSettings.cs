using System;

namespace Bardaelyn.Rules;

[Serializable]
public sealed class CycleSettings
{
    public bool Enabled { get; set; }
    public bool RandomDurations { get; set; }
    public bool UseQuietProbability { get; set; }
    public float QuietChancePercent { get; set; } = 30f;
    public float AttemptIntervalSeconds { get; set; } = 60f;
    public bool StartQuietOnNaturalGap { get; set; }
    public float QuietVolume { get; set; }
    public float QuietMinSeconds { get; set; } = 180f;
    public float QuietMaxSeconds { get; set; } = 180f;
    public float MusicMinSeconds { get; set; } = 600f;
    public float MusicMaxSeconds { get; set; } = 600f;

    public void Normalize()
    {
        QuietVolume = Clamp(QuietVolume, 0f, 0f, 1f);
        QuietChancePercent = Clamp(QuietChancePercent, 30f, 0f, 100f);
        AttemptIntervalSeconds = Clamp(AttemptIntervalSeconds, 60f, 1f, 3600f);
        QuietMinSeconds = Clamp(QuietMinSeconds, 180f, 1f, 3600f);
        QuietMaxSeconds = Math.Max(QuietMinSeconds, Clamp(QuietMaxSeconds, 180f, 1f, 3600f));
        MusicMinSeconds = Clamp(MusicMinSeconds, 600f, 1f, 3600f);
        MusicMaxSeconds = Math.Max(MusicMinSeconds, Clamp(MusicMaxSeconds, 600f, 1f, 3600f));
    }

    private static float Clamp(float value, float fallback, float min, float max) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
