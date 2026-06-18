using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    public sealed class GetVehicleRouteReservationsRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new RequestContext();

        public string VehicleId { get; init; } = string.Empty;
    }
}
