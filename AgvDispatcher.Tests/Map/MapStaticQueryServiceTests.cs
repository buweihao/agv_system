using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Modules.MapModule.Services;
using Xunit;

namespace AgvDispatcher.Tests.Map;

public sealed class MapStaticQueryServiceTests
{
    private readonly IMapService _service = new MockMapService();

    [Fact]
    public void GetCurrentMap_ShouldReturnPublishedMapSnapshot()
    {
        var result = _service.GetCurrentMap(new GetMapSnapshotRequest());

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.MapId));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.MapName));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Version));
        Assert.NotNull(result.Data.Nodes);
        Assert.NotNull(result.Data.Edges);
    }

    [Fact]
    public void GetCurrentMap_WithWrongExpectedVersion_ShouldReturnVersionMismatch()
    {
        var result = _service.GetCurrentMap(new GetMapSnapshotRequest { ExpectedMapVersion = "wrong" });

        Assert.False(result.Success);
        Assert.Equal(FailureCode.MapVersionMismatch, result.Code);
    }

    [Fact]
    public void GetNodes_And_GetEdges_ShouldReturnStaticMapData()
    {
        var nodes = _service.GetNodes(new GetMapSnapshotRequest());
        var edges = _service.GetEdges(new GetMapSnapshotRequest());

        Assert.Equal(3, nodes.Data!.Count);
        Assert.Equal(2, edges.Data!.Count);
        Assert.All(edges.Data, edge =>
        {
            Assert.False(string.IsNullOrWhiteSpace(edge.FromNodeId));
            Assert.False(string.IsNullOrWhiteSpace(edge.ToNodeId));
            Assert.True(edge.Distance > 0);
            Assert.NotEqual(MapEdgeDirection.Unknown, edge.Direction);
        });
    }

    [Fact]
    public void GetNode_WhenNodeExists_ShouldReturnNode()
    {
        var result = _service.GetNode(new GetMapNodeRequest { NodeId = "N1" });

        Assert.True(result.Success);
        Assert.Equal("N1", result.Data!.NodeId);
        Assert.Equal("Point_A", result.Data.NodeCode);
        Assert.Equal(MapNodeType.PickPoint, result.Data.NodeType);
    }

    [Fact]
    public void GetNode_WhenNodeNotExists_ShouldReturnMapNodeNotFound()
    {
        var result = _service.GetNode(new GetMapNodeRequest { NodeId = "missing" });

        Assert.False(result.Success);
        Assert.Equal(FailureCode.MapNodeNotFound, result.Code);
    }

    [Fact]
    public void GetEdge_WhenEdgeExists_ShouldReturnEdge()
    {
        var result = _service.GetEdge(new GetMapEdgeRequest { EdgeId = "E1" });

        Assert.True(result.Success);
        Assert.Equal("E1", result.Data!.EdgeId);
    }

    [Fact]
    public void GetEdge_WhenEdgeNotExists_ShouldReturnMapEdgeNotFound()
    {
        var result = _service.GetEdge(new GetMapEdgeRequest { EdgeId = "missing" });

        Assert.False(result.Success);
        Assert.Equal(FailureCode.MapEdgeNotFound, result.Code);
    }

    [Fact]
    public void NodeExists_And_EdgeExists_ShouldReturnBoolean()
    {
        Assert.True(_service.NodeExists(new GetMapNodeRequest { NodeId = "N1" }).Data);
        Assert.False(_service.NodeExists(new GetMapNodeRequest { NodeId = "missing" }).Data);
        Assert.True(_service.EdgeExists(new GetMapEdgeRequest { EdgeId = "E1" }).Data);
        Assert.False(_service.EdgeExists(new GetMapEdgeRequest { EdgeId = "missing" }).Data);
    }

    [Fact]
    public void GetOutgoingEdges_ShouldRespectBidirectionalEdge()
    {
        var reverseBidirectional = _service.GetOutgoingEdges(new GetOutgoingEdgesRequest { NodeId = "N2" });
        var reverseOneWay = _service.GetOutgoingEdges(new GetOutgoingEdgesRequest { NodeId = "N3" });

        Assert.Contains(reverseBidirectional.Data!, edge => edge.EdgeId == "E1");
        Assert.DoesNotContain(reverseOneWay.Data!, edge => edge.EdgeId == "E2");
    }

    [Theory]
    [InlineData(MapNodeType.PickPoint)]
    [InlineData(MapNodeType.Normal)]
    [InlineData(MapNodeType.ChargeStation)]
    public void GetNodesByType_ShouldReturnMatchingNodesOnly(MapNodeType nodeType)
    {
        var result = _service.GetNodesByType(new GetNodesByTypeRequest { NodeType = nodeType });

        Assert.NotEmpty(result.Data!);
        Assert.All(result.Data!, node => Assert.Equal(nodeType, node.NodeType));
    }

    [Fact]
    public void VendorNodeMapping_ShouldSupportBothDirectionLookup()
    {
        var vendorCode = _service.GetVendorNodeCode(new GetVendorNodeCodeRequest
        {
            SystemNodeId = "N1",
            VendorCode = "Seer"
        });
        var systemId = _service.GetSystemNodeId(new GetSystemNodeIdRequest
        {
            VendorNodeCode = "SEER_A01",
            VendorCode = "Seer"
        });
        var missing = _service.GetVendorNodeCode(new GetVendorNodeCodeRequest
        {
            SystemNodeId = "missing",
            VendorCode = "Seer"
        });

        Assert.Equal("SEER_A01", vendorCode.Data);
        Assert.Equal("N1", systemId.Data);
        Assert.False(missing.Success);
        Assert.Equal(FailureCode.VendorNodeMappingNotFound, missing.Code);
    }
}
