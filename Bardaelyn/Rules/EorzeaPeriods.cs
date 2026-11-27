using System;

namespace Bardaelyn.Rules;

public enum EorzeaPeriod
{
    Morning,
    Afternoon,
    Night
}

[Serializable]
public sealed class EorzeaPeriods
{
    public int MorningStart { get; set; } = 360;
    public int MorningEnd { get; set; } = 720;
    public int AfternoonStart { get; set; } = 720;
    public int AfternoonEnd { get; set; } = 1080;
    public int NightStart { get; set; } = 1080;
    public int NightEnd { get; set; } = 360;

    public void Normalize()
    {
        MorningStart = Math.Clamp(MorningStart, 0, 1439);
        MorningEnd = Math.Clamp(MorningEnd, 0, 1439);
        AfternoonStart = Math.Clamp(AfternoonStart, 0, 1439);
        AfternoonEnd = Math.Clamp(AfternoonEnd, 0, 1439);
        NightStart = Math.Clamp(NightStart, 0, 1439);
        NightEnd = Math.Clamp(NightEnd, 0, 1439);
    }

    public bool Contains(EorzeaPeriod period, int minute)
    {
        var (start, end) = period switch
        {
            EorzeaPeriod.Morning => (MorningStart, MorningEnd),
            EorzeaPeriod.Afternoon => (AfternoonStart, AfternoonEnd),
            EorzeaPeriod.Night => (NightStart, NightEnd),
            _ => (-1, -1)
        };
        if (minute < 0 || minute >= 1440 || start < 0 || start == end) return false;
        return start < end ? minute >= start && minute < end : minute >= start || minute < end;
    }

    public static int MinuteAt(DateTime utc)
    {
        var milliseconds = new DateTimeOffset(utc.ToUniversalTime()).ToUnixTimeMilliseconds();
        var dayMilliseconds = ((milliseconds % 4_200_000) + 4_200_000) % 4_200_000;
        return (int)(dayMilliseconds * 60 / 175_000);
    }

    public EorzeaPeriods Copy() => new()
    {
        MorningStart = MorningStart, MorningEnd = MorningEnd,
        AfternoonStart = AfternoonStart, AfternoonEnd = AfternoonEnd,
        NightStart = NightStart, NightEnd = NightEnd
    };
}
