namespace Dalamud.Configuration
{
    public interface IPluginConfiguration
    {
    }
}

namespace SamplePlugin
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
