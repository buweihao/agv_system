using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    public sealed class GetTaskRouteReservationsRequest : IAgvRequest
    {
        public RequestContext Context { get; init; } = new RequestContext();

        public string TaskId { get; init; } = string.Empty;
    }
}
