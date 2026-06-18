using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    public sealed class ReleaseRouteReservationRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new RequestContext();

        public string ReservationId { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;
    }
}
