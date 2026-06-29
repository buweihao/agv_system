using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Reservations.Requests
{
    /// <summary>
    /// Requests active route reservations for read-only diagnostics.
    /// </summary>
    public sealed class GetRouteReservationsRequest : IAgvRequest
    {
        /// <summary>Gets the request context.</summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>Gets an optional task identifier filter.</summary>
        public string? TaskId { get; init; }

        /// <summary>Gets an optional vehicle identifier filter.</summary>
        public string? VehicleId { get; init; }
    }
}
