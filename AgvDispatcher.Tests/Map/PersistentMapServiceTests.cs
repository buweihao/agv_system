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

    [Fact]
    public void IMapService_ShouldReadOnlyActiveMap()
    {
        var active = new MapVersionEntity { MapId = "MAIN", MapVersion = "v1", Name = "Active", State = MapState.Active, IsActive = true };
        var nodes = new[]
        {
            Node("N1", "v1"),
            Node("N2", "v1"),
            Node("N_NEW", "v2")
        };
        var edges = new[] { Edge("E1", "N1", "N2", "v1"), Edge("E2", "N2", "N_NEW", "v2") };
        var aliases = new[]
        {
            Alias("A1", "N1", "1001", "v1"),
            Alias("A2", "N1", "2001", "v2")
        };
        var service = new PersistentMapService(
            new FakeMapRepository(nodes, edges),
            new FakeMapVersionRepository(active),
            new FakeAliasRepository(aliases),
            new FakePathPlanningService());

        var result = service.GetCurrentMap(new GetMapSnapshotRequest());

        Assert.True(result.Success);
        Assert.Equal("v1", result.Data!.Version);
        Assert.DoesNotContain(result.Data.Nodes, node => node.NodeId == "N_NEW");
        Assert.Equal("1001", result.Data.VendorNodeMappings.Single().VendorNodeCode);
    }

    [Fact]
    public void GetCurrentMap_ShouldReturnSnapshotCopy()
    {
        var active = new MapVersionEntity { MapId = "MAIN", MapVersion = "v1", Name = "Active", State = MapState.Active, IsActive = true };
        var service = new PersistentMapService(
            new FakeMapRepository(new[] { Node("N1", "v1"), Node("N2", "v1") }, new[] { Edge("E1", "N1", "N2", "v1") }),
            new FakeMapVersionRepository(active),
            new FakeAliasRepository(Array.Empty<MapLocationAlias>()),
            new FakePathPlanningService());

        var first = service.GetCurrentMap(new GetMapSnapshotRequest()).Data!;
        var mutable = Assert.IsType<MapNodeDto[]>(first.Nodes);
        mutable[0] = new MapNodeDto { NodeId = "POLLUTED" };
        var second = service.GetCurrentMap(new GetMapSnapshotRequest()).Data!;

        Assert.DoesNotContain(second.Nodes, node => node.NodeId == "POLLUTED");
        Assert.Contains(second.Nodes, node => node.NodeId == "N1");
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

    private static MapNode Node(string nodeId, string mapVersion) => new()
    {
        NodeId = nodeId,
        MapId = "MAIN",
        MapVersion = mapVersion,
        NodeCode = nodeId,
        Name = nodeId,
        NodeType = AgvDispatcher.Core.Enums.MapNodeType.Normal,
        Position = new MapPosition { MapId = "MAIN", X = 1, Y = 2 },
        IsEnabled = true
    };

    private static MapEdge Edge(string edgeId, string fromNodeId, string toNodeId, string mapVersion) => new()
    {
        EdgeId = edgeId,
        MapId = "MAIN",
        MapVersion = mapVersion,
        FromNodeId = fromNodeId,
        ToNodeId = toNodeId,
        Direction = EdgeDirection.ForwardOnly,
        Length = 1,
        MaxSpeed = 1,
        IsEnabled = true
    };

    private static MapLocationAlias Alias(string aliasId, string nodeId, string aliasValue, string mapVersion) => new()
    {
        AliasId = aliasId,
        MapId = "MAIN",
        MapVersion = mapVersion,
        NodeId = nodeId,
        Brand = "Seer",
        AliasType = "Vendor",
        AliasValue = aliasValue,
        IsEnabled = true
    };

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
        public Task<IReadOnlyList<MapNode>> GetNodesAsync(string mapId, string mapVersion) =>
            Task.FromResult<IReadOnlyList<MapNode>>(_nodes.Where(node =>
                node.MapId == mapId && node.MapVersion == mapVersion).ToArray());
        public Task<IReadOnlyList<MapEdge>> GetEdgesAsync() => Task.FromResult(_edges);
        public Task<IReadOnlyList<MapEdge>> GetEdgesAsync(string mapId, string mapVersion) =>
            Task.FromResult<IReadOnlyList<MapEdge>>(_edges.Where(edge =>
                edge.MapId == mapId && edge.MapVersion == mapVersion).ToArray());
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
        public Task<IReadOnlyList<MapLocationAlias>> GetAllAsync(string mapId, string mapVersion) =>
            Task.FromResult<IReadOnlyList<MapLocationAlias>>(_aliases.Where(alias =>
                alias.MapId == mapId && alias.MapVersion == mapVersion).ToArray());
        public Task SaveAsync(MapLocationAlias alias) => Task.CompletedTask;
        public Task DeleteAsync(string aliasId) => Task.CompletedTask;
    }

    private sealed class FakeMapVersionRepository : IMapVersionRepository
    {
        private readonly MapVersionEntity? _active;

        internal FakeMapVersionRepository(MapVersionEntity? active)
        {
            _active = active;
        }

        public Task<MapVersionEntity?> GetActiveAsync() => Task.FromResult(_active);
        public Task<MapVersionEntity?> GetAsync(string mapId, string mapVersion) => Task.FromResult(_active);
        public Task<IReadOnlyList<MapVersionEntity>> GetAllAsync(string? mapId = null) =>
            Task.FromResult<IReadOnlyList<MapVersionEntity>>(_active is null ? Array.Empty<MapVersionEntity>() : new[] { _active });
        public Task SaveAsync(MapVersionEntity version) => Task.CompletedTask;
        public Task SetActiveAsync(string mapId, string mapVersion) => Task.CompletedTask;
        public Task DeleteAsync(string mapId, string mapVersion) => Task.CompletedTask;
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
