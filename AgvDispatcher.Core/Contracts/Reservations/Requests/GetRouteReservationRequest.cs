using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    public sealed class GetRouteReservationRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new RequestContext();

        public string ReservationId { get; init; } = string.Empty;
    }
}
