using System.Numerics;
using Bardaelyn.State;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class StateMonitorTests
{
    private static readonly DateTime Start = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    // Excludes stationary time during each combat, duty, and cutscene flag.
    [Theory]
    [InlineData(ConditionFlag.InCombat)]
    [InlineData(ConditionFlag.BoundByDuty)]
    [InlineData(ConditionFlag.BoundByDuty56)]
    [InlineData(ConditionFlag.BoundByDuty95)]
    [InlineData(ConditionFlag.OccupiedInCutSceneEvent)]
    [InlineData(ConditionFlag.WatchingCutscene)]
    [InlineData(ConditionFlag.WatchingCutscene78)]
    public void IdleDurationExcludesStationaryTimeInBlockedStates(ConditionFlag flag)
    {
        var conditions = new Conditions();
        var objects = new Objects { LocalPlayer = new Player() };
        var monitor = new StateMonitor(conditions, objects, new Targets());

        conditions.Active.Add(flag);
        Assert.False(monitor.Capture(Start).IsIdle);
        Assert.Equal(0f, monitor.Capture(Start.AddSeconds(10)).IdleForSeconds);
        conditions.Active.Remove(flag);
        var firstIdle = monitor.Capture(Start.AddSeconds(20));
        Assert.True(firstIdle.IsIdle);
        Assert.Equal(0f, firstIdle.IdleForSeconds);
        Assert.Equal(3f, monitor.Capture(Start.AddSeconds(23)).IdleForSeconds);

        conditions.Active.Add(flag);
        Assert.Equal(0f, monitor.Capture(Start.AddSeconds(24)).IdleForSeconds);
        conditions.Active.Remove(flag);
        Assert.Equal(0f, monitor.Capture(Start.AddSeconds(30)).IdleForSeconds);
    }

    // Resets idle duration when the player disappears and returns.
    [Fact]
    public void LosingThePlayerResetsIdleDuration()
    {
        var objects = new Objects { LocalPlayer = new Player() };
        var monitor = new StateMonitor(new Conditions(), objects, new Targets());
        monitor.Capture(Start);
        Assert.Equal(5f, monitor.Capture(Start.AddSeconds(5)).IdleForSeconds);
        objects.LocalPlayer = null;
        var missing = monitor.Capture(Start.AddSeconds(10));
        Assert.False(missing.IsIdle);
        Assert.Equal(0f, missing.IdleForSeconds);
        objects.LocalPlayer = new Player();
        Assert.Equal(0f, monitor.Capture(Start.AddSeconds(20)).IdleForSeconds);
    }

    private sealed class Conditions : ICondition
    {
        public HashSet<ConditionFlag> Active { get; } = [];
        public bool this[ConditionFlag flag] => Active.Contains(flag);
    }

    private sealed class Objects : IObjectTable
    {
        public IPlayerCharacter? LocalPlayer { get; set; }
    }

    private sealed class Player : IPlayerCharacter
    {
        public Vector3 Position => Vector3.Zero;
    }

    private sealed class Targets : ITargetManager
    {
        public object? Target => null;
    }
}
