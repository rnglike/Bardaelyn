using System.Numerics;
using SamplePlugin.State;
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
}
