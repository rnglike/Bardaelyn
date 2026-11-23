using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("BGM Controller Settings###BgmControllerSettings")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse;

        Size = new Vector2(420, 560);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var enabled = configuration.Enabled;
        if (ImGui.Checkbox("Enable BGM control", ref enabled))
        {
            configuration.Enabled = enabled;
            configuration.Save();
        }

        var defaultVolume = configuration.DefaultVolume * 100f;
        if (ImGui.SliderFloat("Default volume", ref defaultVolume, 0f, 100f))
        {
            configuration.DefaultVolume = defaultVolume / 100f;
            configuration.Save();
        }

        var fade = configuration.FadeDurationSeconds;
        if (ImGui.SliderFloat("Fade duration (seconds)", ref fade, 0f, 15f))
        {
            configuration.FadeDurationSeconds = fade;
            configuration.Save();
        }

        ImGui.Separator();
        ImGui.Text("Built-in low HP rule");
        var lowHpEnabled = configuration.LowHpRuleEnabled;
        if (ImGui.Checkbox("Enable", ref lowHpEnabled))
        {
            configuration.LowHpRuleEnabled = lowHpEnabled;
            configuration.Save();
        }

        var threshold = configuration.LowHpThresholdPercent;
        if (ImGui.SliderFloat("HP threshold", ref threshold, 1f, 100f))
        {
            configuration.LowHpThresholdPercent = threshold;
            configuration.Save();
        }

        var lowHpVolume = configuration.LowHpVolume * 100f;
        if (ImGui.SliderFloat("Low HP volume", ref lowHpVolume, 0f, 100f))
        {
            configuration.LowHpVolume = lowHpVolume / 100f;
            configuration.Save();
        }

        var lowHpPriority = configuration.LowHpPriority;
        if (ImGui.InputInt("Low HP priority", ref lowHpPriority))
        {
            configuration.LowHpPriority = lowHpPriority;
            configuration.Save();
        }

        var updateInterval = configuration.UpdateIntervalSeconds;
        if (ImGui.SliderFloat("State update interval", ref updateInterval, 0.05f, 2f))
        {
            configuration.UpdateIntervalSeconds = updateInterval;
            configuration.Save();
        }

        ImGui.Separator();
        ImGui.Text("Custom rules");

        for (var i = configuration.Rules.Count - 1; i >= 0; i--)
        {
            var rule = configuration.Rules[i];
            ImGui.PushID(i);
            var ruleEnabled = rule.Enabled;
            if (ImGui.Checkbox("Enabled", ref ruleEnabled))
            {
                rule.Enabled = ruleEnabled;
                configuration.Save();
            }

            ImGui.SameLine();
            var name = rule.Name;
            if (ImGui.InputText("Name", ref name, 128))
            {
                rule.Name = name;
                configuration.Save();
            }

            var priority = rule.Priority;
            if (ImGui.InputInt("Priority", ref priority))
            {
                rule.Priority = priority;
                configuration.Save();
            }

            var volume = rule.TargetVolume * 100f;
            if (ImGui.SliderFloat("Rule volume", ref volume, 0f, 100f))
            {
                rule.TargetVolume = volume / 100f;
                configuration.Save();
            }

            var activation = rule.ActivateAfterSeconds;
            if (ImGui.SliderFloat("Activate after", ref activation, 0f, 900f))
            {
                rule.ActivateAfterSeconds = activation;
                configuration.Save();
            }

            var deactivation = rule.DeactivateAfterSeconds;
            if (ImGui.SliderFloat("Deactivate after", ref deactivation, 0f, 900f))
            {
                rule.DeactivateAfterSeconds = deactivation;
                configuration.Save();
            }

            var requireAll = rule.RequireAll;
            if (ImGui.Checkbox("Require all conditions", ref requireAll))
            {
                rule.RequireAll = requireAll;
                configuration.Save();
            }

            foreach (var condition in Enum.GetValues<RuleCondition>())
            {
                var selected = rule.Conditions.Contains(condition);
                if (ImGui.Checkbox(condition.ToString(), ref selected))
                {
                    if (selected)
                    {
                        rule.Conditions.Add(condition);
                    }
                    else
                    {
                        rule.Conditions.Remove(condition);
                    }

                    configuration.Save();
                }
            }

            if (ImGui.Button("Remove rule"))
            {
                configuration.Rules.RemoveAt(i);
                configuration.Save();
            }

            ImGui.PopID();
            ImGui.Separator();
        }

        if (ImGui.Button("Add rule"))
        {
            configuration.Rules.Add(new BgmRule());
            configuration.Save();
        }
    }
}
