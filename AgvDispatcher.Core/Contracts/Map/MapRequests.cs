using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Map
{
    public sealed class GetMapSnapshotRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new();

        public string? ExpectedMapVersion { get; init; }
    }

    public sealed class GetMapNodeRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new();

        public string NodeId { get; init; } = string.Empty;
    }

    public sealed class GetMapEdgeRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new();

        public string EdgeId { get; init; } = string.Empty;
    }

    public sealed class GetOutgoingEdgesRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new();

        public string NodeId { get; init; } = string.Empty;
    }

    public sealed class GetNodesByTypeRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new();

        public MapNodeType NodeType { get; init; } = MapNodeType.Unknown;
    }

    public sealed class GetVendorNodeCodeRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new();

        public string SystemNodeId { get; init; } = string.Empty;

        public string VendorCode { get; init; } = string.Empty;
    }

    public sealed class GetSystemNodeIdRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new();

        public string VendorNodeCode { get; init; } = string.Empty;

        public string VendorCode { get; init; } = string.Empty;
    }
}
