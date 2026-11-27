using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Bardaelyn.Rules;

namespace Bardaelyn.Windows;

public sealed class TimePeriodsWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public TimePeriodsWindow(Configuration configuration) : base("Eorzea Time Periods###BgmTimePeriods")
    {
        this.configuration = configuration;
        Size = new Vector2(430, 280);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextWrapped("Eorzea time, 24-hour clock. Starts are inclusive and ends exclusive. Equal endpoints mean an empty period.");
        var periods = configuration.TimePeriods;
        var morningStart = periods.MorningStart;
        var morningEnd = periods.MorningEnd;
        var afternoonStart = periods.AfternoonStart;
        var afternoonEnd = periods.AfternoonEnd;
        var nightStart = periods.NightStart;
        var nightEnd = periods.NightEnd;
        var changed = Period("Morning", ref morningStart, ref morningEnd);
        changed |= Period("Afternoon", ref afternoonStart, ref afternoonEnd);
        changed |= Period("Night", ref nightStart, ref nightEnd);
        if (changed)
        {
            periods.MorningStart = morningStart;
            periods.MorningEnd = morningEnd;
            periods.AfternoonStart = afternoonStart;
            periods.AfternoonEnd = afternoonEnd;
            periods.NightStart = nightStart;
            periods.NightEnd = nightEnd;
            configuration.Save();
        }
        var minute = EorzeaPeriods.MinuteAt(DateTime.UtcNow);
        ImGui.Text($"Current Eorzea time: {minute / 60:00}:{minute % 60:00}");
    }

    private static bool Period(string name, ref int start, ref int end)
    {
        ImGui.PushID(name);
        ImGui.TextUnformatted(name);
        var changed = Time("Start", ref start);
        ImGui.SameLine();
        changed |= Time("End", ref end);
        ImGui.PopID();
        return changed;
    }

    private static bool Time(string label, ref int value)
    {
        var hour = value / 60;
        var minute = value % 60;
        ImGui.PushID(label);
        ImGui.SetNextItemWidth(45);
        var changed = ImGui.InputInt("##hour", ref hour, 0, 0, "%d", ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        ImGui.TextUnformatted(":");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(45);
        changed |= ImGui.InputInt("##minute", ref minute, 0, 0, "%d", ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        if (changed) value = Math.Clamp(hour, 0, 23) * 60 + Math.Clamp(minute, 0, 59);
        ImGui.PopID();
        return changed;
    }
}
