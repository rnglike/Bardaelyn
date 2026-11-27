using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Bardaelyn.Rules;

namespace Bardaelyn.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;
    private BgmRule? pendingRemoval;
    private readonly Dictionary<BgmRule, int> ruleIds = [];
    private int nextRuleId;
    private string newProfileName = "";
    private string? pendingProfile;
    private string? profileError;

    public ConfigWindow(Plugin plugin) : base("Bardaelyn Settings###BgmControllerSettings")
    {
        Size = new Vector2(700, 700);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 350),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawProfiles();
        ImGui.Separator();
        var enabled = configuration.Enabled;
        if (ImGui.Checkbox("Enable BGM control", ref enabled))
        {
            configuration.Enabled = enabled;
            configuration.Save();
        }

        ImGui.TextWrapped("Use the sliders or enter a value in the input box and press Enter.");
        var defaultVolume = configuration.DefaultVolume * 100f;
        if (Number("Default volume (% of game setting)", ref defaultVolume, 0f, 100f, "%.1f"))
        {
            configuration.DefaultVolume = defaultVolume / 100f;
            configuration.Save();
        }
        var exactFade = configuration.ExactFadeDuration;
        if (ImGui.Checkbox("Fade over the specified duration", ref exactFade))
        {
            configuration.ExactFadeDuration = exactFade;
            configuration.Save();
        }
        Help("Off: fade time describes a full-scale volume change. On: each new target takes the specified time from the current volume; changing the target restarts the fade.");

        if (ImGui.Button("Configure Eorzea time periods"))
        {
            plugin.ToggleTimePeriodsUi();
        }

        if (ImGui.CollapsingHeader("Advanced timing"))
        {
            var interval = configuration.UpdateIntervalSeconds;
            if (Number("State update interval (seconds)", ref interval, 0.05f, 2f, "%.2f"))
            {
                configuration.UpdateIntervalSeconds = interval;
                configuration.Save();
            }

            var stopDelay = configuration.MusicStopDelaySeconds;
            if (Number("Estimated music stop detection delay (seconds)", ref stopDelay, 0f, 30f, "%.1f"))
            {
                configuration.MusicStopDelaySeconds = stopDelay;
                configuration.Save();
            }
            ImGui.TextWrapped("The estimate corrects detected stops but cannot reverse a phase that has already ended.");
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Default quiet cycle"))
        {
            ImGui.PushID("globalCycle");
            if (DrawCycleSettings(configuration.GlobalCycle))
            {
                configuration.Save();
            }
            ImGui.PopID();
        }

        ImGui.Separator();
        ImGui.TextWrapped("Priority order: the first matching enabled rule wins. Use Up and Down to reorder.");
        for (var position = 0; position < configuration.Rules.Count; position++)
        {
            var rule = configuration.Rules[position];

            if (!string.IsNullOrWhiteSpace(rule.Group) &&
                (position == 0 || !string.Equals(configuration.Rules[position - 1].Group, rule.Group, StringComparison.Ordinal)))
            {
                ImGui.Spacing();
                ImGui.TextWrapped($"Group: {rule.Group}");
                ImGui.Separator();
            }
            if (!ruleIds.ContainsKey(rule))
            {
                ruleIds.Add(rule, nextRuleId++);
            }
            ImGui.PushID($"rule{ruleIds[rule]}");
            ImGui.Spacing();
            var panelStart = ImGui.GetCursorScreenPos();
            var panelWidth = ImGui.GetContentRegionAvail().X;
            ImGui.BeginGroup();
            ImGui.Dummy(new Vector2(0f, 4f));
            ImGui.Indent(8f);
            var moved = false;
            if (position > 0 && ImGui.Button("Up"))
            {
                configuration.MovePriorityEntry(position, -1);
                moved = true;
            }
            if (position > 0)
            {
                ImGui.SameLine();
            }
            if (position < configuration.Rules.Count - 1 && ImGui.Button("Down"))
            {
                configuration.MovePriorityEntry(position, 1);
                moved = true;
            }
            if (moved)
            {
                configuration.Save();
                EndRulePanel(panelStart, panelWidth);
                ImGui.PopID();
                break;
            }

            if (configuration.Rules.Count > 1)
            {
                ImGui.SameLine();
            }
            if (ImGui.Button("Duplicate"))
            {
                var copy = SettingsProfile.CopyRule(rule);
                copy.Name = rule.Name.Length > 57 ? rule.Name[..57] + " copy" : rule.Name + " copy";
                configuration.Rules.Insert(position + 1, copy);
                configuration.Save();
                EndRulePanel(panelStart, panelWidth);
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.Button("Remove"))
            {
                pendingRemoval = rule;
            }
            ImGui.SameLine();
            var title = $"{position + 1}. {rule.Name}{(rule.Enabled ? "" : " (disabled)")}";
            var expanded = ImGui.CollapsingHeader($"{title}###details");
            ImGui.TextWrapped(RuleStatus(rule));
            if (!expanded)
            {
                var included = rule.Conditions.Count == 0 ? "none" : string.Join(rule.RequireAll ? " AND " : " OR ", rule.Conditions.Select(ConditionLabel));
                var excluded = rule.ExcludedConditions.Count == 0 ? "" : $"; exclude {string.Join(", ", rule.ExcludedConditions.Select(ConditionLabel))}";
                var delay = rule.SeparateDelays
                    ? $"on {rule.ActivateAfterSeconds:0.#}s / off {rule.DeactivateAfterSeconds:0.#}s"
                    : $"{rule.ActivateAfterSeconds:0.#}s delay";
                var fade = rule.SeparateFades
                    ? $"fade in {rule.FadeInSeconds:0.#}s / out {rule.FadeOutSeconds:0.#}s"
                    : $"{rule.FadeInSeconds:0.#}s fade";
                ImGui.TextWrapped($"{included}{excluded} | {rule.TargetVolume * 100f:0.#}% volume | {delay} | {fade} | {CycleModeLabel(rule)}");
            }
            var covering = RuleConflictAnalyzer.FindCoveringRule(configuration.Rules, position);
            if (covering is not null)
            {
                ImGui.TextWrapped($"Priority warning: \"{covering.Name}\" is higher and matches whenever this rule matches, so it may prevent this rule from winning. Delays can change the outcome.");
            }
            else if (expanded && RuleConflictAnalyzer.FindOverlappingRule(configuration.Rules, position) is { } overlap)
            {
                ImGui.TextWrapped($"Possible overlap: \"{overlap.Name}\" is higher and may win when both rules match. Delays can change the outcome.");
            }
            if (ReferenceEquals(pendingRemoval, rule))
            {
                ImGui.TextWrapped($"Remove rule \"{rule.Name}\"?");
                if (ImGui.Button("Confirm removal"))
                {
                    configuration.RemoveRuleAt(position);
                    ruleIds.Remove(rule);
                    pendingRemoval = null;
                    configuration.Save();
                    EndRulePanel(panelStart, panelWidth);
                    ImGui.PopID();
                    break;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    pendingRemoval = null;
                }
            }
            if (expanded)
            {
                DrawRule(rule);
            }
            EndRulePanel(panelStart, panelWidth);
            ImGui.Spacing();
            ImGui.PopID();
        }

        if (ImGui.Button("Add rule"))
        {
            configuration.Rules.Add(new BgmRule { InterruptSilence = true });
            configuration.Save();
        }
    }

    private static void EndRulePanel(Vector2 start, float width)
    {
        ImGui.Unindent(8f);
        ImGui.Dummy(new Vector2(0f, 4f));
        ImGui.EndGroup();
        var bottom = ImGui.GetItemRectMax().Y;
        ImGui.GetWindowDrawList().AddRect(start, new Vector2(start.X + width, bottom), ImGui.GetColorU32(ImGuiCol.Border));
    }

    private string RuleStatus(BgmRule rule)
    {
        if (!rule.Enabled) return "Status: Disabled";
        if (!configuration.Enabled) return "Status: BGM control disabled";
        if (plugin.LastState is not { HasPlayer: true } state) return "Status: Waiting for player state";
        var decision = plugin.LastDecision;
        if (ReferenceEquals(decision.SelectedRule, rule))
            return "Status: Active (last evaluation)";
        if (decision.ActiveRules.Contains(rule) && decision.SelectedRule is { } winner)
            return $"Status: Overridden by \"{winner.Name}\" (last evaluation)";
        if (decision.ActivationRemainingSeconds.TryGetValue(rule, out var remaining))
            return $"Status: Waiting {remaining:0.0}s for activation (last evaluation)";
        if (RuleEngine.Matches(rule, state, configuration.TimePeriods))
            return "Status: Matching; awaiting evaluation or activation delay";
        return "Status: Not matching (last sampled state)";
    }

    private void DrawProfiles()
    {
        var profiles = configuration.Profiles;
        if (profiles is null || configuration.ActiveProfileName is null)
        {
            ImGui.TextUnformatted("Profiles are unavailable: configuration is not initialized.");
            return;
        }
        ImGui.Text($"Active profile: {configuration.ActiveProfileName}");
        if (ImGui.BeginCombo("Switch profile", configuration.ActiveProfileName))
        {
            foreach (var profile in profiles)
            {
                if (ImGui.Selectable(profile.Name, profile.Name == configuration.ActiveProfileName) &&
                    profile.Name != configuration.ActiveProfileName)
                {
                    pendingProfile = profile.Name;
                }
            }
            ImGui.EndCombo();
        }
        if (pendingProfile is not null)
        {
            ImGui.TextWrapped($"Save current profile and switch to \"{pendingProfile}\"?");
            if (ImGui.Button("Confirm switch"))
            {
                plugin.SwitchProfile(pendingProfile);
                pendingProfile = null;
                pendingRemoval = null;
                ruleIds.Clear();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel switch"))
            {
                pendingProfile = null;
            }
        }
        ImGui.InputText("New profile name", ref newProfileName, 65);
        if (ImGui.Button("Create profile from current settings"))
        {
            var name = newProfileName.Trim();
            if (name.Length == 0 || name.Length > 64 ||
                profiles.Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                profileError = "Choose a unique profile name (1 to 64 characters).";
            }
            else
            {
                configuration.CreateProfile(name);
                configuration.Save();
                newProfileName = "";
                profileError = null;
                pendingProfile = null;
            }
        }
        if (profileError is not null)
        {
            ImGui.TextWrapped(profileError);
        }
    }

    private void DrawRule(BgmRule rule)
    {
        var enabled = rule.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            rule.Enabled = enabled;
            configuration.Save();
        }

        var name = rule.Name;
        if (ImGui.InputText("Name (press Enter)", ref name, 64, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            rule.Name = name;
            configuration.Save();
        }
        var group = rule.Group;
        if (ImGui.InputText("Group label (optional)", ref group, 64, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            rule.Group = group;
            configuration.Save();
        }
        Help("Labels group adjacent rules visually only. Rule order still determines priority.");

        ImGui.Separator();
        ImGui.TextUnformatted("Conditions");
        Help("Included conditions match all or any, as selected. Excluded conditions must always be absent. A rule with no conditions never matches.");
        var requireAll = rule.RequireAll;
        if (ImGui.Checkbox("Require all included conditions", ref requireAll))
        {
            rule.RequireAll = requireAll;
            configuration.Save();
        }

        if (ImGui.BeginTable("conditions", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Include");
            DrawConditionList("include", rule.Conditions, rule.ExcludedConditions);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Exclude");
            DrawConditionList("exclude", rule.ExcludedConditions, rule.Conditions);
            ImGui.EndTable();
        }
        if (rule.Conditions.Count == 0 && rule.ExcludedConditions.Count == 0)
        {
            ImGui.TextWrapped("Add at least one condition; an empty rule never matches.");
        }

        if (rule.Conditions.Contains(RuleCondition.LowHp) || rule.ExcludedConditions.Contains(RuleCondition.LowHp))
        {
            var threshold = rule.LowHpThresholdPercent;
            if (Number("Low HP threshold (%)", ref threshold, 1f, 100f, "%.1f"))
            {
                rule.LowHpThresholdPercent = threshold;
                configuration.Save();
            }
        }

        if (rule.Conditions.Contains(RuleCondition.EorzeaTime) || rule.ExcludedConditions.Contains(RuleCondition.EorzeaTime))
        {
            ImGui.TextUnformatted("Selected Eorzea periods (any):");
            foreach (var period in Enum.GetValues<EorzeaPeriod>())
            {
                var selected = rule.TimePeriods.Contains(period);
                if (ImGui.Checkbox(period.ToString(), ref selected))
                {
                    if (selected) rule.TimePeriods.Add(period);
                    else rule.TimePeriods.Remove(period);
                    configuration.Save();
                }
            }
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Volume");
        var volume = rule.TargetVolume * 100f;
        if (Number("Volume (% of game setting)", ref volume, 0f, 100f, "%.1f"))
        {
            rule.TargetVolume = volume / 100f;
            configuration.Save();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Timing");
        DrawRuleTiming(rule);

        ImGui.Separator();
        ImGui.TextUnformatted("Quiet cycle");
        Help("Ignoring cycles pauses the default cycle while this rule wins. A custom cycle also pauses the default cycle.");
        if (ImGui.BeginCombo("Quiet cycle behavior", CycleModeLabel(rule)))
        {
            if (ImGui.Selectable("Ignore quiet cycles", rule.InterruptSilence))
            {
                rule.InterruptSilence = true;
                configuration.Save();
            }
            if (ImGui.Selectable("Follow default cycle", !rule.InterruptSilence && !rule.Cycle.Enabled))
            {
                rule.InterruptSilence = false;
                rule.Cycle.Enabled = false;
                configuration.Save();
            }
            if (ImGui.Selectable("Use a custom cycle", !rule.InterruptSilence && rule.Cycle.Enabled))
            {
                rule.InterruptSilence = false;
                rule.Cycle.Enabled = true;
                configuration.Save();
            }
            ImGui.EndCombo();
        }

        if (!rule.InterruptSilence && rule.Cycle.Enabled)
        {
            ImGui.PushID("cycle");
            if (ImGui.CollapsingHeader("Rule quiet cycle") && DrawCycleSettings(rule.Cycle, false))
            {
                configuration.Save();
            }
            ImGui.PopID();
        }

    }

    private void DrawRuleTiming(BgmRule rule)
    {
        ImGui.TextUnformatted("Activation and deactivation");
        Help("Activation delay is how long conditions must match before the rule can take effect. Deactivation delay keeps the selected rule active after it stops matching; a higher-priority rule can take over. When combining separate delays, the activation value is used for both.");
        var separateDelays = rule.SeparateDelays;
        if (ImGui.Checkbox("Separate activation and deactivation delays", ref separateDelays))
        {
            rule.SeparateDelays = separateDelays;
            if (!separateDelays)
            {
                rule.DeactivateAfterSeconds = rule.ActivateAfterSeconds;
            }
            configuration.Save();
        }
        var activation = rule.ActivateAfterSeconds;
        if (Number(separateDelays ? "Activate after (seconds)" : "Activation and deactivation delay (seconds)", ref activation, 0f, 3600f, "%.1f"))
        {
            rule.ActivateAfterSeconds = activation;
            if (!separateDelays)
            {
                rule.DeactivateAfterSeconds = activation;
            }
            configuration.Save();
        }
        if (separateDelays)
        {
            var deactivation = rule.DeactivateAfterSeconds;
            if (Number("Deactivate after (seconds)", ref deactivation, 0f, 3600f, "%.1f"))
            {
                rule.DeactivateAfterSeconds = deactivation;
                configuration.Save();
            }
        }

        ImGui.TextUnformatted("Fades");
        Help("Fades control the speed of volume changes. Durations describe a full-scale change, so smaller changes finish sooner. On a rule change, a nonzero incoming fade-in takes priority; otherwise the outgoing fade-out is used. When combining separate fades, the fade-in value is used for both.");
        var separateFades = rule.SeparateFades;
        if (ImGui.Checkbox("Separate fade in and fade out", ref separateFades))
        {
            rule.SeparateFades = separateFades;
            if (!separateFades)
            {
                rule.FadeOutSeconds = rule.FadeInSeconds;
            }
            configuration.Save();
        }
        var fadeIn = rule.FadeInSeconds;
        if (Number(separateFades ? "Rule fade in (seconds)" : "Rule fade (seconds)", ref fadeIn, 0f, 60f, "%.1f"))
        {
            rule.FadeInSeconds = fadeIn;
            if (!separateFades)
            {
                rule.FadeOutSeconds = fadeIn;
            }
            configuration.Save();
        }
        if (separateFades)
        {
            var fadeOut = rule.FadeOutSeconds;
            if (Number("Rule fade out (seconds)", ref fadeOut, 0f, 60f, "%.1f"))
            {
                rule.FadeOutSeconds = fadeOut;
                configuration.Save();
            }
        }
    }

    private void DrawConditionList(string id, List<RuleCondition> selected, List<RuleCondition> opposite)
    {
        ImGui.PushID(id);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##add", "Add condition"))
        {
            foreach (var condition in Enum.GetValues<RuleCondition>())
            {
                if (!selected.Contains(condition) && ImGui.Selectable(ConditionLabel(condition)))
                {
                    opposite.Remove(condition);
                    selected.Add(condition);
                    configuration.Save();
                }
            }
            ImGui.EndCombo();
        }

        if (selected.Count == 0)
        {
            ImGui.TextDisabled("No conditions added");
        }
        foreach (var condition in selected.ToArray())
        {
            ImGui.PushID(condition.ToString());
            if (ImGui.Button("Remove"))
            {
                selected.Remove(condition);
                configuration.Save();
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(ConditionLabel(condition));
            ImGui.PopID();
        }
        ImGui.PopID();
    }

    internal static string ConditionLabel(RuleCondition condition) => condition switch
    {
        RuleCondition.Mounted => "Mounted",
        RuleCondition.Moving => "Moving",
        RuleCondition.Idle => "Idle",
        RuleCondition.InCombat => "In combat",
        RuleCondition.InDuty => "In duty",
        RuleCondition.InCutscene => "In cutscene",
        RuleCondition.HasTarget => "Has target",
        RuleCondition.LowHp => "Low HP",
        RuleCondition.Swimming => "Swimming",
        RuleCondition.Underwater => "Underwater",
        RuleCondition.EorzeaTime => "Eorzea time",
        _ => condition.ToString()
    };

    private static string CycleModeLabel(BgmRule rule) =>
        rule.InterruptSilence ? "Ignore quiet cycles" :
        rule.Cycle.Enabled ? "Use a custom cycle" : "Follow default cycle";

    private static void Help(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    private static bool DrawCycleSettings(CycleSettings cycle, bool showEnable = true)
    {
        var changed = false;
        var enabled = cycle.Enabled;
        if (showEnable && ImGui.Checkbox("Enable cycle", ref enabled))
        {
            cycle.Enabled = enabled;
            changed = true;
        }
        if (!cycle.Enabled)
        {
            return changed;
        }
        if (showEnable)
        {
            var useFade = cycle.UseDefaultCycleFade;
            if (ImGui.Checkbox("Fade default cycle transitions when no rule is active", ref useFade))
            {
                cycle.UseDefaultCycleFade = useFade;
                changed = true;
            }
            if (useFade)
            {
                var fade = cycle.DefaultCycleFadeSeconds;
                if (Number("Default cycle fade (seconds)", ref fade, 0f, 60f, "%.1f"))
                {
                    cycle.DefaultCycleFadeSeconds = fade;
                    changed = true;
                }
            }
        }
        var quietRange = cycle.RandomDurations ? $"{cycle.QuietMinSeconds:0.#}-{cycle.QuietMaxSeconds:0.#}s" : $"{cycle.QuietMinSeconds:0.#}s";
        var musicDescription = cycle.UseQuietProbability
            ? $"{cycle.QuietChancePercent:0.#}% quiet chance after {cycle.MinimumMusicSeconds:0.#}s, then every {cycle.AttemptIntervalSeconds:0.#}s"
            : $"music for {(cycle.RandomDurations ? $"{cycle.MusicMinSeconds:0.#}-{cycle.MusicMaxSeconds:0.#}s" : $"{cycle.MusicMinSeconds:0.#}s")}";
        ImGui.TextWrapped($"Cycle: quiet for {quietRange} at {cycle.QuietVolume * 100f:0.#}% of active volume, then {musicDescription}.");
        Help("The cycle starts quiet. Probability attempts use detected music time; a natural gap may start quiet sooner when enabled.");

        var random = cycle.RandomDurations;
        if (ImGui.Checkbox("Randomize durations", ref random))
        {
            cycle.RandomDurations = random;
            changed = true;
        }
        Help("Randomizes quiet duration and, in interval mode, music duration.");

        ImGui.TextUnformatted("Music phase");
        var probabilityMode = cycle.UseQuietProbability;
        if (ImGui.Checkbox("Use probability instead of music intervals", ref probabilityMode))
        {
            cycle.UseQuietProbability = probabilityMode;
            changed = true;
        }
        if (probabilityMode)
        {
            var minimumMusic = cycle.MinimumMusicSeconds;
            if (Number("Minimum music before first attempt (seconds)", ref minimumMusic, 0f, 3600f, "%.1f"))
            {
                cycle.MinimumMusicSeconds = minimumMusic;
                changed = true;
            }
            var chance = cycle.QuietChancePercent;
            if (Number("Quiet event chance (%)", ref chance, 0f, 100f, "%.1f"))
            {
                cycle.QuietChancePercent = chance;
                changed = true;
            }
            var attemptInterval = cycle.AttemptIntervalSeconds;
            if (Number("Time between attempts (seconds)", ref attemptInterval, 1f, 3600f, "%.1f"))
            {
                cycle.AttemptIntervalSeconds = attemptInterval;
                changed = true;
            }
            ImGui.TextUnformatted("Probability attempts");
            Help("The first attempt occurs after the minimum detected music time (immediately at zero); later attempts use detected music time. Natural gaps can start quiet before the minimum expires.");
        }
        else
        {
            var musicMin = cycle.MusicMinSeconds;
            if (Number(random ? "Music minimum (seconds)" : "Music duration (seconds)", ref musicMin, 1f, 3600f, "%.1f"))
            {
                cycle.MusicMinSeconds = musicMin;
                changed = true;
            }
            if (random)
            {
                var musicMax = cycle.MusicMaxSeconds;
                if (Number("Music maximum (seconds)", ref musicMax, 1f, 3600f, "%.1f"))
                {
                    cycle.MusicMaxSeconds = musicMax;
                    changed = true;
                }
            }
        }

        var countNaturalGap = cycle.StartQuietOnNaturalGap;
        if (ImGui.Checkbox("Start quiet period when the game goes quiet", ref countNaturalGap))
        {
            cycle.StartQuietOnNaturalGap = countNaturalGap;
            changed = true;
        }
        ImGui.TextUnformatted("Natural gaps");
        Help("When off, natural gaps stay separate from quiet periods. Music time pauses while no music is detected.");

        ImGui.TextUnformatted("Quiet phase");
        var quietVolume = cycle.QuietVolume * 100f;
        if (Number("Quiet volume (% of active volume)", ref quietVolume, 0f, 100f, "%.1f"))
        {
            cycle.QuietVolume = quietVolume / 100f;
            changed = true;
        }
        var quietMin = cycle.QuietMinSeconds;
        if (Number(random ? "Quiet minimum (seconds)" : "Quiet duration (seconds)", ref quietMin, 1f, 3600f, "%.1f"))
        {
            cycle.QuietMinSeconds = quietMin;
            changed = true;
        }
        if (random)
        {
            var quietMax = cycle.QuietMaxSeconds;
            if (Number("Quiet maximum (seconds)", ref quietMax, 1f, 3600f, "%.1f"))
            {
                cycle.QuietMaxSeconds = quietMax;
                changed = true;
            }
        }
        return changed;
    }

    private static bool Number(string label, ref float value, float min, float max, string format)
    {
        ImGui.PushID(label);
        ImGui.TextUnformatted(label);
        var previous = value;
        ImGui.SetNextItemWidth(200f);
        var changed = ImGui.SliderFloat("##slider", ref value, min, max, format);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputFloat("##input", ref value, 0f, 0f, format, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            changed = true;
        }
        if (changed)
        {
            if (!float.IsFinite(value))
            {
                value = previous;
                changed = false;
                ImGui.TextUnformatted("Enter a finite number.");
            }
            else
            {
                value = Math.Clamp(value, min, max);
            }
        }
        ImGui.PopID();
        return changed;
    }
}
