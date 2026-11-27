using Dalamud.Game.Command;
using Dalamud.Game.Config;
using System;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Bardaelyn.Windows;
using Bardaelyn.Audio;
using Bardaelyn.Rules;
using Bardaelyn.State;

namespace Bardaelyn;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    private const string CommandName = "/bgmcontrol";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("BgmController");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private TimePeriodsWindow TimePeriodsWindow { get; init; }
    private StateMonitor StateMonitor { get; init; }
    private RuleEngine RuleEngine { get; set; }
    private SilenceCycleEngine SilenceCycleEngine { get; set; } = new();
    private readonly FadeSelector fadeSelector = new();
    private BgmVolumeController VolumeController { get; init; }

    private DateTime lastUpdate;
    private DateTime lastStateUpdate;
    private bool gameConfigReadWarningLogged;
    private bool musicBusWarningLogged;
    public BgmStateSnapshot? LastState { get; private set; }
    public RuleDecision LastDecision => RuleEngine.LastDecision;
    public float CurrentBgmVolume => VolumeController.CurrentVolume;
    public float TargetBgmVolume => VolumeController.TargetVolume;
    public string ActiveRuleName => Configuration.Enabled
        ? LastDecision.RuleName
        : "Disabled";
    public string CycleStatus { get; private set; } = "Off";
    public int? MusicSoundCount { get; private set; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Normalize();
        Configuration.Save();
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        TimePeriodsWindow = new TimePeriodsWindow(Configuration);
        StateMonitor = new StateMonitor(Condition, ObjectTable, TargetManager);
        RuleEngine = new RuleEngine(() => Configuration.GetRuleSettings());
        VolumeController = new BgmVolumeController(Log);
        if (!VolumeController.Initialize(GameConfig))
        {
            Log.Warning("Could not read the user's BGM volume. BGM control will remain inactive until it is available.");
        }
        lastUpdate = DateTime.UtcNow;
        lastStateUpdate = DateTime.UtcNow;

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(TimePeriodsWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Bardaelyn window."
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Framework.Update += OnFrameworkUpdate;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [Bardaelyn] Bardaelyn loaded.
        Log.Information("Bardaelyn loaded.");
    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        Framework.Update -= OnFrameworkUpdate;

        VolumeController.Restore();
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        TimePeriodsWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - lastUpdate;
        lastUpdate = now;

        if (Configuration.Enabled)
        {
            if (!GameConfig.TryGet(SystemConfigOption.SoundBgm, out uint configuredVolume))
            {
                if (!gameConfigReadWarningLogged)
                {
                    Log.Warning("Could not read the user's BGM volume. Releasing BGM control.");
                    gameConfigReadWarningLogged = true;
                }

                VolumeController.Release(ReleaseFade(), Configuration.ExactFadeDuration);
                SilenceCycleEngine.Suspend();
                CycleStatus = "Paused (BGM setting unavailable)";
                VolumeController.Update(elapsed);
                return;
            }

            gameConfigReadWarningLogged = false;
            VolumeController.UpdateBaseline(configuredVolume / 100f);

            if (now - lastStateUpdate >=
                TimeSpan.FromSeconds(Math.Max(0.05f, Configuration.UpdateIntervalSeconds)))
            {
                lastStateUpdate = now;
                LastState = StateMonitor.Capture(now);
                var decision = RuleEngine.Evaluate(
                    LastState,
                    now);
                VolumeController.ActiveRule = decision.RuleName;
                if (!LastState.HasPlayer)
                {
                    SilenceCycleEngine.Suspend();
                    CycleStatus = "Paused (player unavailable)";
                    VolumeController.Release(ReleaseFade(), Configuration.ExactFadeDuration);
                    VolumeController.Update(elapsed);
                    return;
                }

                var musicCount = MusicBusReader.ReadCount();
                MusicSoundCount = musicCount;
                if (musicCount is null && !musicBusWarningLogged)
                {
                    Log.Warning("Music bus detection is unavailable; quiet cycles will use elapsed-time fallback.");
                    musicBusWarningLogged = true;
                }
                else if (musicCount is not null)
                {
                    musicBusWarningLogged = false;
                }
                var cycleDecision = SilenceCycleEngine.Evaluate(
                    decision, Configuration.GlobalCycle, now, musicCount, Configuration.MusicStopDelaySeconds);
                CycleStatus = cycleDecision.Status;
                VolumeController.SetTarget(
                    cycleDecision.TargetVolume,
                    fadeSelector.Select(decision.SelectedRule, cycleDecision.TargetVolume,
                        cycleDecision.DefaultCycleQuiet,
                        Configuration.GlobalCycle.UseDefaultCycleFade ? Configuration.GlobalCycle.DefaultCycleFadeSeconds : null),
                    Configuration.ExactFadeDuration);
            }
        }
        else
        {
            SilenceCycleEngine.Suspend();
            CycleStatus = "Paused (disabled)";
            if (GameConfig.TryGet(SystemConfigOption.SoundBgm, out uint configuredVolume))
            {
                gameConfigReadWarningLogged = false;
                VolumeController.UpdateBaseline(configuredVolume / 100f);
            }
            else if (!gameConfigReadWarningLogged)
            {
                Log.Warning("Could not read the user's BGM volume. Releasing BGM control.");
                gameConfigReadWarningLogged = true;
            }

            VolumeController.Release(ReleaseFade(), Configuration.ExactFadeDuration);
        }

        VolumeController.Update(elapsed);
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
    public void ToggleTimePeriodsUi() => TimePeriodsWindow.Toggle();

    private float ReleaseFade() => fadeSelector.Release();

    public void SwitchProfile(string name)
    {
        Configuration.SwitchProfile(name);
        Configuration.Save();
        RuleEngine = new RuleEngine(() => Configuration.GetRuleSettings());
        SilenceCycleEngine = new SilenceCycleEngine();
        lastStateUpdate = DateTime.MinValue;
        CycleStatus = "Waiting for state sample";
    }
}
