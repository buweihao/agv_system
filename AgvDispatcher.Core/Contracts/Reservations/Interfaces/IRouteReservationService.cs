using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Reservations.Models;
using AgvDispatcher.Core.Contracts.Reservations.Requests;

namespace AgvDispatcher.Core.Contracts.Reservations.Interfaces
{
    /// <summary>
    /// Coordinates route reservation and rolling-window locking over traffic resources.
    /// Implementations adapt route segments to calls on ITrafficControlService; they do
    /// not plan paths, dispatch vehicles, control vehicles, or own task lifecycles.
    /// </summary>
    public interface IRouteReservationService
    {
        Task<AgvResult<RouteReservationDto>> CreateReservationAsync(
            CreateRouteReservationRequest request,
            CancellationToken cancellationToken = default);

        Task<AgvResult<RouteRollingLockResultDto>> AcquireNextWindowAsync(
            AcquireNextRouteWindowRequest request,
            CancellationToken cancellationToken = default);

        Task<AgvResult<RouteRollingReleaseResultDto>> ReleasePassedResourcesAsync(
            ReleasePassedRouteResourcesRequest request,
            CancellationToken cancellationToken = default);

        Task<AgvResult> ReleaseReservationAsync(
            ReleaseRouteReservationRequest request,
            CancellationToken cancellationToken = default);

        Task<AgvResult<RouteReservationDto>> GetReservationAsync(
            GetRouteReservationRequest request,
            CancellationToken cancellationToken = default);

        Task<AgvResult<IReadOnlyList<RouteReservationDto>>> GetReservationsByTaskAsync(
            GetTaskRouteReservationsRequest request,
            CancellationToken cancellationToken = default);

        Task<AgvResult<IReadOnlyList<RouteReservationDto>>> GetReservationsByVehicleAsync(
            GetVehicleRouteReservationsRequest request,
            CancellationToken cancellationToken = default);
    }
}
