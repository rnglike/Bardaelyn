using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Bardaelyn.Rules;

namespace Bardaelyn.State;

public sealed class StateMonitor
{
    private readonly ICondition condition;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;

    private readonly MovementTracker movementTracker = new();
    private readonly IdleTracker idleTracker = new();

    public StateMonitor(ICondition condition, IObjectTable objectTable, ITargetManager targetManager)
    {
        this.condition = condition;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
    }

    public BgmStateSnapshot Capture(DateTime now)
    {
        var player = objectTable.LocalPlayer;
        var position = player?.Position ?? Vector3.Zero;
        var movement = movementTracker.Update(player?.Position, now);
        var moved = movement.IsMoving;

        var isMounted = player != null &&
                        (condition[ConditionFlag.Mounted] || condition[ConditionFlag.RidingPillion]);
        var isInCombat = condition[ConditionFlag.InCombat];
        var isInDuty = condition[ConditionFlag.BoundByDuty] ||
                       condition[ConditionFlag.BoundByDuty56] ||
                       condition[ConditionFlag.BoundByDuty95];
        var isInCutscene = condition[ConditionFlag.OccupiedInCutSceneEvent] ||
                           condition[ConditionFlag.WatchingCutscene] ||
                           condition[ConditionFlag.WatchingCutscene78];

        var hpPercent = 100f;
        if (player is Dalamud.Game.ClientState.Objects.Types.IBattleChara battle)
        {
            hpPercent = battle.MaxHp <= 0
                ? 100f
                : battle.CurrentHp * 100f / battle.MaxHp;
        }

        var isIdle = player != null && !moved && !isInCombat && !isInDuty && !isInCutscene;
        var idleForSeconds = idleTracker.Update(isIdle, now);

        return new BgmStateSnapshot
        {
            HasPlayer = player != null,
            IsMounted = isMounted,
            IsMoving = moved,
            IsIdle = isIdle,
            IsInCombat = isInCombat,
            IsInDuty = isInDuty,
            IsInCutscene = isInCutscene,
            HasTarget = targetManager.Target != null,
            IsSwimming = player != null && condition[ConditionFlag.Swimming],
            IsUnderwater = player != null && condition[ConditionFlag.Diving],
            EorzeaMinute = player != null ? EorzeaPeriods.MinuteAt(now) : null,
            HpPercent = hpPercent,
            IdleForSeconds = idleForSeconds,
            Position = position
        };
    }
}
