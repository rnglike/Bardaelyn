using System.Numerics;
using Bardaelyn.State;
using Xunit;

namespace Bardaelyn.Tests;

public sealed class MovementTrackerTests
{
    private static readonly DateTime Start = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IgnoresPositionJitterBelowMovementThreshold()
    {
        var tracker = new MovementTracker();

        tracker.Update(Vector3.Zero, Start);
        var state = tracker.Update(new Vector3(0.01f, 0f, 0f), Start.AddMilliseconds(100));

        Assert.False(state.IsMoving);
    }

    [Fact]
    public void DetectsMovementAndKeepsItBrieflyAfterTheLastPositionChange()
    {
        var tracker = new MovementTracker();

        tracker.Update(Vector3.Zero, Start);
        Assert.True(tracker.Update(new Vector3(0.1f, 0f, 0f), Start.AddMilliseconds(100)).IsMoving);
        Assert.True(tracker.Update(new Vector3(0.1f, 0f, 0f), Start.AddMilliseconds(700)).IsMoving);
        Assert.False(tracker.Update(new Vector3(0.1f, 0f, 0f), Start.AddMilliseconds(900)).IsMoving);
    }

    [Fact]
    public void ReportsStationaryDurationAfterMovementStops()
    {
        var tracker = new MovementTracker();

        tracker.Update(Vector3.Zero, Start);
        tracker.Update(new Vector3(0.1f, 0f, 0f), Start.AddMilliseconds(100));
        tracker.Update(new Vector3(0.1f, 0f, 0f), Start.AddMilliseconds(200));
        var state = tracker.Update(new Vector3(0.1f, 0f, 0f), Start.AddSeconds(1));

        Assert.False(state.IsMoving);
        Assert.InRange(state.IdleForSeconds, 0.7f, 0.9f);
    }

    [Fact]
    public void ResetsAfterPlayerIsUnavailable()
    {
        var tracker = new MovementTracker();

        tracker.Update(Vector3.Zero, Start);
        tracker.Update(new Vector3(0.1f, 0f, 0f), Start.AddMilliseconds(100));
        tracker.Update(null, Start.AddMilliseconds(200));

        var state = tracker.Update(Vector3.Zero, Start.AddMilliseconds(300));

        Assert.False(state.IsMoving);
        Assert.Equal(0f, state.IdleForSeconds);
    }

    [Fact]
    public void IdleDurationStartsWhenIdleBeginsAndResetsWhenInterrupted()
    {
        var tracker = new IdleTracker();

        Assert.Equal(0f, tracker.Update(false, Start));
        Assert.Equal(0f, tracker.Update(false, Start.AddSeconds(10)));
        Assert.Equal(0f, tracker.Update(true, Start.AddSeconds(20)));
        Assert.Equal(3f, tracker.Update(true, Start.AddSeconds(23)));
        Assert.Equal(0f, tracker.Update(false, Start.AddSeconds(24)));
        Assert.Equal(0f, tracker.Update(true, Start.AddSeconds(30)));
        Assert.Equal(2f, tracker.Update(true, Start.AddSeconds(32)));
    }

    [Fact]
    public void IdleDurationStartsAfterMovementGracePeriod()
    {
        var movement = new MovementTracker();
        var idle = new IdleTracker();

        movement.Update(Vector3.Zero, Start);
        Assert.Equal(0f, idle.Update(!movement.Update(new Vector3(0.1f, 0f, 0f), Start.AddMilliseconds(100)).IsMoving, Start.AddMilliseconds(100)));
        Assert.Equal(0f, idle.Update(!movement.Update(new Vector3(0.1f, 0f, 0f), Start.AddMilliseconds(700)).IsMoving, Start.AddMilliseconds(700)));
        Assert.Equal(0f, idle.Update(!movement.Update(new Vector3(0.1f, 0f, 0f), Start.AddMilliseconds(900)).IsMoving, Start.AddMilliseconds(900)));
        Assert.Equal(1f, idle.Update(!movement.Update(new Vector3(0.1f, 0f, 0f), Start.AddMilliseconds(1900)).IsMoving, Start.AddMilliseconds(1900)));
    }
}
