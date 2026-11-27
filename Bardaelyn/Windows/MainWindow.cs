using System.Numerics;
using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using Bardaelyn.Audio;
using Bardaelyn.Rules;

namespace Bardaelyn.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("Bardaelyn##BgmController")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Size = new Vector2(450, 440);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text($"Active rule: {plugin.ActiveRuleName}");
        if (plugin.Configuration.Enabled && plugin.LastState is { HasPlayer: true })
        {
            var decision = plugin.LastDecision;
            var rules = plugin.Configuration.Rules;
            if (decision.SelectedRule is { } active)
            {
                var included = active.Conditions.Count == 0 ? "no included conditions" :
                    string.Join(active.RequireAll ? " AND " : " OR ", active.Conditions.Select(ConfigWindow.ConditionLabel));
                var excluded = active.ExcludedConditions.Count == 0 ? "" :
                    $"; excludes {string.Join(", ", active.ExcludedConditions.Select(ConfigWindow.ConditionLabel))}";
                ImGui.TextWrapped($"Rule conditions: {included}{excluded}.");
                if (!RuleEngine.Matches(active, plugin.LastState, plugin.Configuration.TimePeriods))
                {
                    ImGui.TextWrapped("The last sampled conditions no longer match; the selected rule may be held by its deactivation delay.");
                }
            }
            var winnerIndex = decision.SelectedRule is null ? rules.Count : rules.IndexOf(decision.SelectedRule);
            if (winnerIndex < 0) winnerIndex = rules.Count;
            for (var i = 0; i < winnerIndex; i++)
            {
                if (rules[i].Enabled && decision.ActivationRemainingSeconds.TryGetValue(rules[i], out var remaining))
                {
                    ImGui.TextWrapped($"{rules[i].Name} can become eligible in {remaining:0.0}s if conditions remain matched.");
                    break;
                }
            }
        }
        ImGui.Text($"Cycle: {plugin.CycleStatus}");
        ImGui.Text(plugin.MusicSoundCount is { } count
            ? $"Music detection: {count} registered sound(s)"
            : "Music detection: unavailable or not sampled");
        ImGui.Text($"Target BGM: {plugin.TargetBgmVolume * 100f:0}%");
        ImGui.Text($"Current BGM: {plugin.CurrentBgmVolume * 100f:0}%");

        if (ImGui.Button("Open Settings"))
        {
            plugin.ToggleConfigUi();
        }

        if (ImGui.CollapsingHeader("Player state"))
        {
            var state = plugin.LastState;
            if (state == null)
            {
                ImGui.TextUnformatted("Waiting for state sample...");
            }
            else
            {
                ImGui.Text($"Player loaded: {state.HasPlayer}");
                ImGui.Text($"Mounted: {state.IsMounted}");
                ImGui.Text($"Swimming: {state.IsSwimming}");
                ImGui.Text($"Underwater: {state.IsUnderwater}");
                ImGui.Text($"Moving: {state.IsMoving}");
                ImGui.Text($"Idle: {state.IsIdle}");
                ImGui.Text($"Idle for: {state.IdleForSeconds:0.0}s");
                ImGui.Text($"Combat: {state.IsInCombat}");
                ImGui.Text($"Duty: {state.IsInDuty}");
                ImGui.Text($"Cutscene: {state.IsInCutscene}");
                ImGui.Text($"HP: {state.HpPercent:0.0}%");
            }
        }

        if (ImGui.CollapsingHeader("Experimental BGM scene diagnostics"))
        {
            DrawBgmDiagnostics();
        }

        if (ImGui.CollapsingHeader("Experimental music bus diagnostics"))
        {
            DrawMusicBusDiagnostics();
        }
    }

    private static unsafe void DrawBgmDiagnostics()
    {
        ImGui.TextWrapped("Read-only game scene data; playing state does not necessarily mean audible music.");
        var bgm = BGMSystem.Instance();
        if (bgm == null)
        {
            ImGui.TextUnformatted("BGM system unavailable.");
            return;
        }

        var count = bgm->Scenes.LongCount;
        if (count < 0 || count > 128 || (count > 0 && bgm->Scenes.First == null))
        {
            ImGui.TextUnformatted("BGM scene data unavailable or invalid.");
            return;
        }
        ImGui.Text($"Situation: {bgm->CurrentSituationKind}; scenes: {count}");
        for (var index = 0; index < count; index++)
        {
            var scene = bgm->Scenes[index];
            ImGui.Text($"Scene {scene.SceneId}: {scene.PlayState}, BGM {scene.BgmId}, playing ID {scene.PlayingBgmId}");
        }
    }

    private static unsafe void DrawMusicBusDiagnostics()
    {
        ImGui.TextWrapped("Registered sounds are not necessarily playing or audible. A shared pool may include other buses.");
        var count = MusicBusReader.ReadCount();
        if (count is null)
        {
            ImGui.TextUnformatted("Music bus count unavailable; cycles fall back to elapsed time.");
            return;
        }

        ImGui.Text($"Music bus registered sounds: {count}");
    }
}
