using System;
using FFXIVClientStructs.FFXIV.Client.Sound;
using FFXIVClientStructs.SQEX.CDev.Engine.Sd.Driver;

namespace Bardaelyn.Audio;

public static class MusicBusReader
{
    public static unsafe int? ReadCount()
    {
        Bus* bus;
        try
        {
            bus = Bus.GetById((int)SoundBus.Music);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (bus == null || bus->Id != (int)SoundBus.Music || bus->Parent != null || bus->SoundCount < 0)
        {
            return null;
        }

        return bus->SoundCount;
    }
}
