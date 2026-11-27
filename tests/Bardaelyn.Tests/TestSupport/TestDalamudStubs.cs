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

namespace Dalamud.Game.ClientState.Conditions
{
    public enum ConditionFlag
    {
        Mounted, RidingPillion, InCombat, BoundByDuty, BoundByDuty56, BoundByDuty95,
        OccupiedInCutSceneEvent, WatchingCutscene, WatchingCutscene78, Swimming, Diving
    }
}

namespace Dalamud.Game.ClientState.Objects.Types
{
    public interface IPlayerCharacter
    {
        System.Numerics.Vector3 Position { get; }
    }

    public interface IBattleChara
    {
        uint MaxHp { get; }
        uint CurrentHp { get; }
    }
}

namespace Dalamud.Plugin.Services
{
    public interface ICondition
    {
        bool this[Dalamud.Game.ClientState.Conditions.ConditionFlag flag] { get; }
    }

    public interface IObjectTable
    {
        Dalamud.Game.ClientState.Objects.Types.IPlayerCharacter? LocalPlayer { get; }
    }

    public interface ITargetManager
    {
        object? Target { get; }
    }

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
