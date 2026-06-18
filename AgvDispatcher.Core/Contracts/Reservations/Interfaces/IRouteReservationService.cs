using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Reservations.Models;
using AgvDispatcher.Core.Contracts.Reservations.Requests;

namespace AgvDispatcher.Core.Contracts.Reservations.Interfaces
{
    /// <summary>
    /// Coordinates route reservation and rolling-window locking over traffic resources.
    /// Implementations adapt route segments to calls on ITrafficControlService; they do
    /// not plan paths, dispatch vehicles, control vehicles, or own task lifecycles.
    /// 协调路线上交通资源的预留和滚动窗口锁定。
    /// 实现类将路线段适配为对 ITrafficControlService 的调用；它们不负责路径规划、车辆调度、车辆控制或拥有任务的生命周期。
    /// </summary>
    public interface IRouteReservationService
    {
        /// <summary>
        /// Creates a new route reservation.
        /// 创建一个新的路线预留。
        /// </summary>
        Task<AgvResult<RouteReservationDto>> CreateReservationAsync(
            CreateRouteReservationRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Acquires the next rolling window of locked resources for an active route.
        /// 获取活动路线的下一个滚动资源锁定窗口。
        /// </summary>
        Task<AgvResult<RouteRollingLockResultDto>> AcquireNextWindowAsync(
            AcquireNextRouteWindowRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases traffic resources that the vehicle has already passed.
        /// 释放车辆已通过的交通资源。
        /// </summary>
        Task<AgvResult<RouteRollingReleaseResultDto>> ReleasePassedResourcesAsync(
            ReleasePassedRouteResourcesRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases the entire route reservation and all associated resources.
        /// 释放整个路线预留及所有相关资源。
        /// </summary>
        Task<AgvResult> ReleaseReservationAsync(
            ReleaseRouteReservationRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current state of a route reservation.
        /// 获取路线预留的当前状态。
        /// </summary>
        Task<AgvResult<RouteReservationDto>> GetReservationAsync(
            GetRouteReservationRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all active route reservations for a specific task.
        /// 获取特定任务的所有活动路线预留。
        /// </summary>
        Task<AgvResult<IReadOnlyList<RouteReservationDto>>> GetReservationsByTaskAsync(
            GetTaskRouteReservationsRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all active route reservations for a specific vehicle.
        /// 获取特定车辆的所有活动路线预留。
        /// </summary>
        Task<AgvResult<IReadOnlyList<RouteReservationDto>>> GetReservationsByVehicleAsync(
            GetVehicleRouteReservationsRequest request,
            CancellationToken cancellationToken = default);
    }
}
