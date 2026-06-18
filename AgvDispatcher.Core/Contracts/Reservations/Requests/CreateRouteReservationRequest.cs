using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Planning.Results;
using AgvDispatcher.Core.Contracts.Reservations.Enums;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    public sealed class CreateRouteReservationRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new RequestContext();

        public string TaskId { get; init; } = string.Empty;

        public string VehicleId { get; init; } = string.Empty;

        public string PlanId { get; init; } = string.Empty;

        public string MapId { get; init; } = string.Empty;

        public string MapVersion { get; init; } = string.Empty;

        public IReadOnlyList<PathSegmentDto> Segments { get; init; } = Array.Empty<PathSegmentDto>();

        public int RollingWindowSize { get; init; }

        public RouteReservationPolicy ReservationPolicy { get; init; } = RouteReservationPolicy.RollingWindow;
    }
}
