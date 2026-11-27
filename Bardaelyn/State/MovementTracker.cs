using System;
using System.Numerics;

namespace Bardaelyn.State;

public readonly record struct MovementState(bool IsMoving, float IdleForSeconds);

public sealed class IdleTracker
{
    private DateTime? idleSince;

    public float Update(bool isIdle, DateTime now)
    {
        if (!isIdle)
        {
            idleSince = null;
            return 0f;
        }

        idleSince ??= now;
        return Math.Max(0f, (float)(now - idleSince.Value).TotalSeconds);
    }
}

public sealed class MovementTracker
{
    private const float MinimumMovementDistance = 0.04f;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MovementGracePeriod = TimeSpan.FromMilliseconds(750);

    private Vector3 lastPosition;
    private DateTime lastSampleTime;
    private DateTime? lastMovedAt;
    private DateTime? stationarySince;

    public MovementState Update(Vector3? position, DateTime now)
    {
        if (position is null)
        {
            Reset();
            return new MovementState(false, 0f);
        }

        if (lastSampleTime == default)
        {
            lastPosition = position.Value;
            lastSampleTime = now;
            stationarySince = now;
            return new MovementState(false, 0f);
        }

        if (now - lastSampleTime >= SampleInterval)
        {
            var distance = Vector3.Distance(lastPosition, position.Value);
            if (distance >= MinimumMovementDistance)
            {
                lastMovedAt = now;
                stationarySince = null;
            }
            else if (stationarySince == null)
            {
                stationarySince = now;
            }

            lastPosition = position.Value;
            lastSampleTime = now;
        }

        var isMoving = lastMovedAt != null && now - lastMovedAt.Value <= MovementGracePeriod;
        var idleForSeconds = !isMoving && stationarySince != null
            ? Math.Max(0f, (float)(now - stationarySince.Value).TotalSeconds)
            : 0f;
        return new MovementState(isMoving, idleForSeconds);
    }

    private void Reset()
    {
        lastPosition = Vector3.Zero;
        lastSampleTime = default;
        lastMovedAt = null;
        stationarySince = null;
    }
}
