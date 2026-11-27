namespace Dalamud.Configuration
{
    public interface IPluginConfiguration
    {
    }
}

namespace Dalamud.Game.Config
{
    public enum SystemConfigOption { SoundBgm }
}

namespace Dalamud.Plugin.Services
{
    public interface IGameConfig
    {
        bool TryGet(Dalamud.Game.Config.SystemConfigOption option, out uint value);
    }

    public interface IPluginLog
    {
        void Warning(string message);
    }
}

namespace FFXIVClientStructs.FFXIV.Client.Sound
{
    public enum SoundBus { Music }
    public unsafe struct SoundManager
    {
        public static SoundManager* Instance() => null;
        public void SetVolume(SoundBus bus, float volume) { }
    }
}

namespace Bardaelyn
{
    public static class Plugin
    {
        internal static PluginInterfaceStub PluginInterface { get; } = new();
    }

    internal sealed class PluginInterfaceStub
    {
        public void SavePluginConfig<T>(T configuration)
        {
        }
    }
}
