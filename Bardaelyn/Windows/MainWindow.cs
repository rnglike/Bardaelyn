using System.Numerics;
using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using Bardaelyn.Audio;

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
