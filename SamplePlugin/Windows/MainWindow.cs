using System.Numerics;
using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin)
        : base("BGM Controller##BgmController", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text($"Active rule: {plugin.LastDecision.RuleName}");
        ImGui.Text($"Target BGM: {plugin.LastDecision.TargetVolume * 100f:0}%");
        ImGui.Text($"Current BGM: {plugin.CurrentBgmVolume * 100f:0}%");

        var state = plugin.LastState;
        if (state == null)
        {
            ImGui.Text("Waiting for state sample...");
            return;
        }

        ImGui.Text($"Mounted: {state.IsMounted}");
        ImGui.Text($"Moving: {state.IsMoving}");
        ImGui.Text($"Idle: {state.IsIdle}");
        ImGui.Text($"Combat: {state.IsInCombat}");
        ImGui.Text($"Duty: {state.IsInDuty}");
        ImGui.Text($"Cutscene: {state.IsInCutscene}");
        ImGui.Text($"HP: {state.HpPercent:0.0}%");

        if (ImGui.Button("Open Settings"))
        {
            plugin.ToggleConfigUi();
        }
    }
}
