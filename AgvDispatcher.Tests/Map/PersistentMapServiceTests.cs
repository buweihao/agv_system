#pragma warning disable CS0618
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Services;
using Xunit;
using ContractNodeType = AgvDispatcher.Core.Contracts.Map.MapNodeType;

namespace AgvDispatcher.Tests.Map;

public sealed class PersistentMapServiceTests
{
    [Fact]
    public void GetCurrentMap_WithStoredMap_ShouldReturnContractSnapshot()
    {
        var service = CreateService();

        var result = service.GetCurrentMap(new GetMapSnapshotRequest());

        Assert.True(result.Success);
        Assert.Equal("MAIN", result.Data!.MapId);
        Assert.StartsWith("db-", result.Data.Version);
        Assert.Equal(2, result.Data.Nodes.Count);
        Assert.Single(result.Data.Edges);
        Assert.Equal(ContractNodeType.PickPoint, result.Data.Nodes[0].NodeType);
        Assert.Equal(MapEdgeDirection.OneWay, result.Data.Edges[0].Direction);
        Assert.Equal("N2", result.Data.Edges[0].FromNodeId);
        Assert.Equal("N1", result.Data.Edges[0].ToNodeId);
        Assert.Equal("VENDOR-N1", result.Data.VendorNodeMappings.Single().VendorNodeCode);
    }

    [Fact]
    public void GetCurrentMap_WithWrongExpectedVersion_ShouldReturnVersionMismatch()
    {
        var service = CreateService();

        var first = service.GetCurrentMap(new GetMapSnapshotRequest());
        var repeated = service.GetCurrentMap(new GetMapSnapshotRequest());
        var mismatch = service.GetCurrentMap(new GetMapSnapshotRequest { ExpectedMapVersion = "wrong" });

        Assert.Equal(first.Data!.Version, repeated.Data!.Version);
        Assert.False(mismatch.Success);
        Assert.Equal(FailureCode.MapVersionMismatch, mismatch.Code);
    }

    [Fact]
    public void GetCurrentMap_WithEmptyRepository_ShouldReturnMapNotLoaded()
    {
        var service = new PersistentMapService(
            new FakeMapRepository(Array.Empty<MapNode>(), Array.Empty<MapEdge>()),
            new FakeAliasRepository(Array.Empty<MapLocationAlias>()),
            new FakePathPlanningService());

        var result = service.GetCurrentMap(new GetMapSnapshotRequest());

        Assert.False(result.Success);
        Assert.Equal(FailureCode.MapNotLoaded, result.Code);
    }

    private static PersistentMapService CreateService()
    {
        var nodes = new[]
        {
            new MapNode
            {
                NodeId = "N1",
                MapId = "MAIN",
                NodeCode = "N1",
                Name = "Pickup",
                NodeType = AgvDispatcher.Core.Enums.MapNodeType.Pickup,
                Position = new MapPosition { MapId = "MAIN", X = 1, Y = 2 },
                IsEnabled = true
            },
            new MapNode
            {
                NodeId = "N2",
                MapId = "MAIN",
                NodeCode = "N2",
                Name = "Dropoff",
                NodeType = AgvDispatcher.Core.Enums.MapNodeType.Dropoff,
                Position = new MapPosition { MapId = "MAIN", X = 3, Y = 4 },
                IsEnabled = true
            }
        };
        var edges = new[]
        {
            new MapEdge
            {
                EdgeId = "E1",
                MapId = "MAIN",
                FromNodeId = "N1",
                ToNodeId = "N2",
                Direction = EdgeDirection.ReverseOnly,
                Length = 1,
                IsEnabled = true
            }
        };
        var aliases = new[]
        {
            new MapLocationAlias
            {
                AliasId = "ALIAS-1",
                MapId = "MAIN",
                NodeId = "N1",
                Brand = "Seer",
                AliasType = "Vendor",
                AliasValue = "VENDOR-N1",
                IsEnabled = true
            }
        };
        return new PersistentMapService(
            new FakeMapRepository(nodes, edges),
            new FakeAliasRepository(aliases),
            new FakePathPlanningService());
    }

    private sealed class FakeMapRepository : IMapRepository
    {
        private readonly IReadOnlyList<MapNode> _nodes;
        private readonly IReadOnlyList<MapEdge> _edges;

        internal FakeMapRepository(IReadOnlyList<MapNode> nodes, IReadOnlyList<MapEdge> edges)
        {
            _nodes = nodes;
            _edges = edges;
        }

        public Task<IReadOnlyList<MapNode>> GetNodesAsync() => Task.FromResult(_nodes);
        public Task<IReadOnlyList<MapEdge>> GetEdgesAsync() => Task.FromResult(_edges);
        public Task SaveNodeAsync(MapNode node) => Task.CompletedTask;
        public Task SaveEdgeAsync(MapEdge edge) => Task.CompletedTask;
        public Task DeleteNodeAsync(string nodeId) => Task.CompletedTask;
        public Task DeleteEdgeAsync(string edgeId) => Task.CompletedTask;
    }

    private sealed class FakeAliasRepository : IMapLocationAliasRepository
    {
        private readonly IReadOnlyList<MapLocationAlias> _aliases;

        internal FakeAliasRepository(IReadOnlyList<MapLocationAlias> aliases)
        {
            _aliases = aliases;
        }

        public Task<IReadOnlyList<MapLocationAlias>> GetAllAsync() => Task.FromResult(_aliases);
        public Task SaveAsync(MapLocationAlias alias) => Task.CompletedTask;
        public Task DeleteAsync(string aliasId) => Task.CompletedTask;
    }

    private sealed class FakePathPlanningService : IPathPlanningService
    {
        public PlannedPath PlanPath(
            IReadOnlyList<MapNode> nodes,
            IReadOnlyList<MapEdge> edges,
            string startNodeId,
            string endNodeId) => new();
    }
}
#pragma warning restore CS0618
