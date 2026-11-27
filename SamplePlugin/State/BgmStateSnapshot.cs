using System.Numerics;

namespace SamplePlugin.State;

public sealed class BgmStateSnapshot
{
    public bool HasPlayer { get; init; }
    public bool IsMounted { get; init; }
    public bool IsMoving { get; init; }
    public bool IsIdle { get; init; }
    public bool IsInCombat { get; init; }
    public bool IsInDuty { get; init; }
    public bool IsInCutscene { get; init; }
    public bool HasTarget { get; init; }
    public float HpPercent { get; init; }
    public float IdleForSeconds { get; init; }
    public Vector3 Position { get; init; }
}
