using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Infrastructure.Sqlite.Services;
using Xunit;

namespace AgvDispatcher.Tests.Dispatching;

public sealed class DispatchTrafficConstraintMapperTests
{
    [Fact]
    public void Occupied_NodeAndEdge_ShouldBecomeOccupiedConstraint()
    {
        var snapshot = Snapshot(
            Status(TrafficResourceType.Node, "N2", TrafficResourceState.Occupied),
            Status(TrafficResourceType.Edge, "E2", TrafficResourceState.Occupied));

        var constraint = DispatchTrafficConstraintMapper.Map(snapshot, "TASK-001", "AGV-001");

        Assert.Contains("N2", constraint.OccupiedNodeIds!);
        Assert.Contains("E2", constraint.OccupiedEdgeIds!);
        Assert.False(constraint.AvoidOccupiedResources);
    }

    [Fact]
    public void ReservedAndLocked_ShouldBecomeReservedConstraint()
    {
        var snapshot = Snapshot(
            Status(TrafficResourceType.Node, "N3", TrafficResourceState.Reserved),
            Status(TrafficResourceType.Edge, "E3", TrafficResourceState.Locked));

        var constraint = DispatchTrafficConstraintMapper.Map(snapshot, "TASK-001", "AGV-001");

        Assert.Contains("N3", constraint.ReservedNodeIds!);
        Assert.Contains("E3", constraint.ReservedEdgeIds!);
        Assert.False(constraint.AvoidReservedResources);
    }

    [Fact]
    public void BlockedAndDisabled_ShouldBecomeForbiddenConstraint()
    {
        var snapshot = Snapshot(
            Status(TrafficResourceType.Node, "N4", TrafficResourceState.Blocked),
            Status(TrafficResourceType.Edge, "E4", TrafficResourceState.Disabled));

        var constraint = DispatchTrafficConstraintMapper.Map(snapshot, "TASK-001", "AGV-001");

        Assert.Contains("N4", constraint.ForbiddenNodeIds!);
        Assert.Contains("E4", constraint.ForbiddenEdgeIds!);
    }

    [Fact]
    public void OwnTaskOrOwnVehicleResources_ShouldBeIgnored()
    {
        var snapshot = Snapshot(
            Status(TrafficResourceType.Node, "N2", TrafficResourceState.Occupied, taskId: "TASK-001"),
            Status(TrafficResourceType.Node, "N3", TrafficResourceState.Reserved, reservedBy: "AGV-001"),
            Status(TrafficResourceType.Edge, "E2", TrafficResourceState.Locked, reservedBy: "AGV-001"));

        var constraint = DispatchTrafficConstraintMapper.Map(snapshot, "TASK-001", "AGV-001");

        Assert.DoesNotContain("N2", constraint.OccupiedNodeIds!);
        Assert.DoesNotContain("N3", constraint.ReservedNodeIds!);
        Assert.DoesNotContain("E2", constraint.ReservedEdgeIds!);
        Assert.DoesNotContain("N2", constraint.ForbiddenNodeIds!);
        Assert.DoesNotContain("N3", constraint.ForbiddenNodeIds!);
        Assert.DoesNotContain("E2", constraint.ForbiddenEdgeIds!);
    }

    private static TrafficSnapshotDto Snapshot(params TrafficResourceStatusDto[] resources) => new()
    {
        MapVersion = "1.0",
        Resources = resources
    };

    private static TrafficResourceStatusDto Status(
        TrafficResourceType type,
        string id,
        TrafficResourceState state,
        string? taskId = null,
        string? reservedBy = null) => new()
    {
        Resource = new TrafficResourceKey { ResourceType = type, ResourceId = id },
        State = state,
        TaskId = taskId,
        OccupiedByAgvId = state == TrafficResourceState.Occupied ? reservedBy : null,
        ReservedByAgvId = state is TrafficResourceState.Reserved or TrafficResourceState.Locked ? reservedBy : null
    };
}
