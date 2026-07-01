using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.MapManagement.Requests;
using AgvDispatcher.Core.Contracts.MapManagement.Results;
using AgvDispatcher.Core.Contracts.MapManagement.Interfaces;
using Xunit;

namespace AgvDispatcher.Tests.Map;

internal static class MapTestDataBuilder
{
    internal static readonly RequestContext Context = new()
    {
        SourceModule = "AgvDispatcher.Tests",
        OperatorId = "test-operator"
    };

    internal static MapSnapshotDto CreateMap(
        string mapId = "TEST-MAP",
        string version = "draft",
        bool duplicateNodeIds = false,
        bool duplicateEdgeIds = false) => new()
    {
        MapId = mapId,
        MapName = "Map module test map",
        Version = version,
        Nodes = new[]
        {
            Node("N1", MapNodeType.PickPoint),
            Node(duplicateNodeIds ? "N1" : "N2", MapNodeType.Normal),
            Node("N3", MapNodeType.Normal),
            Node("N4", MapNodeType.ChargeStation)
        },
        Edges = new[]
        {
            Edge("E1", "N1", "N2", MapEdgeDirection.Bidirectional),
            Edge(duplicateEdgeIds ? "E1" : "E2", "N2", "N3", MapEdgeDirection.OneWay),
            Edge("E3", "N3", "N4", MapEdgeDirection.OneWay)
        },
        Areas = new[]
        {
            new MapAreaDto
            {
                AreaId = "A1",
                AreaName = "Test area",
                AreaType = MapAreaType.WorkArea,
                BoundaryPoints = new[]
                {
                    new MapPointDto { X = 0, Y = 0 },
                    new MapPointDto { X = 10, Y = 0 },
                    new MapPointDto { X = 10, Y = 10 }
                }
            }
        },
        VendorNodeMappings = new[]
        {
            new VendorNodeMappingDto
            {
                VendorCode = "Seer",
                SystemNodeId = "N1",
                VendorNodeCode = "SEER_A01"
            }
        }
    };

    internal static async Task<MapDraftDto> CreateAndSaveDraftAsync(
        IMapManagementService service,
        MapSnapshotDto? map = null)
    {
        var created = await service.CreateDraftAsync(new CreateMapDraftRequest
        {
            Context = Context,
            MapName = "Map module test map"
        });
        var draft = Assert.IsType<MapDraftDto>(created.Data);
        var saved = await service.SaveDraftAsync(new SaveMapDraftRequest
        {
            Context = Context,
            DraftId = draft.DraftId,
            Map = map ?? CreateMap()
        });
        return Assert.IsType<MapDraftDto>(saved.Data);
    }

    private static MapNodeDto Node(string id, MapNodeType type) => new()
    {
        NodeId = id,
        NodeCode = id,
        NodeName = $"Node {id}",
        NodeType = type,
        AreaId = "A1"
    };

    private static MapEdgeDto Edge(string id, string from, string to, MapEdgeDirection direction) => new()
    {
        EdgeId = id,
        FromNodeId = from,
        ToNodeId = to,
        Distance = 10,
        Direction = direction,
        AreaId = "A1"
    };
}
