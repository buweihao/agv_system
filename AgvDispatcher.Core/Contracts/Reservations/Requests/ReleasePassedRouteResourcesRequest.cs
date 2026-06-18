using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    public sealed class ReleasePassedRouteResourcesRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new RequestContext();

        public string ReservationId { get; init; } = string.Empty;

        public string CurrentNodeId { get; init; } = string.Empty;

        public int PassedSegmentSequence { get; init; }
    }
}
