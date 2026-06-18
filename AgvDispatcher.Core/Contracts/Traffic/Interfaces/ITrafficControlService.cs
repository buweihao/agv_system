using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;

namespace AgvDispatcher.Core.Contracts.Traffic.Interfaces
{
    /// <summary>
    /// Defines the runtime traffic resource state and control contract.
    /// 定义运行时交通资源状态和控制契约。
    /// </summary>
    public interface ITrafficControlService
    {
        /// <summary>
        /// Gets the current traffic snapshot for route planning and dispatch decisions.
        /// 获取当前交通快照，用于路线规划和调度决策。
        /// </summary>
        Task<AgvResult<TrafficSnapshotDto>> GetTrafficSnapshotAsync(
            RequestContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current status of a single traffic resource.
        /// 获取单个交通资源的当前状态。
        /// </summary>
        Task<AgvResult<TrafficResourceStatusDto>> GetResourceStatusAsync(
            TrafficResourceKey resource,
            RequestContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current statuses of multiple traffic resources.
        /// 获取多个交通资源的当前状态。
        /// </summary>
        Task<AgvResult<IReadOnlyList<TrafficResourceStatusDto>>> GetResourceStatusesAsync(
            IReadOnlyList<TrafficResourceKey> resources,
            RequestContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether the requested resources are currently available.
        /// 检查请求的资源当前是否可用。
        /// </summary>
        Task<AgvResult<TrafficAvailabilityResultDto>> CheckAvailabilityAsync(
            TrafficAvailabilityRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to reserve, occupy, lock, or block the requested resources.
        /// 尝试预留、占用、锁定或阻塞请求的资源。
        /// </summary>
        Task<AgvResult<TrafficReservationDto>> TryAcquireAsync(
            TrafficAcquireRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases resources previously reserved, occupied, or locked by an AGV or task.
        /// 释放之前由 AGV 或任务预留、占用或锁定的资源。
        /// </summary>
        Task<AgvResult> ReleaseAsync(
            TrafficReleaseRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the runtime occupancy reported by an AGV.
        /// 更新由 AGV 报告的运行时占用情况。
        /// </summary>
        Task<AgvResult> UpdateAgvOccupancyAsync(
            AgvOccupancyUpdateRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually blocks traffic resources for operation control or maintenance.
        /// 手动阻塞交通资源，用于操作控制或维护。
        /// </summary>
        Task<AgvResult<TrafficBlockDto>> BlockResourcesAsync(
            TrafficBlockRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a manual block from traffic resources.
        /// 从交通资源中移除手动阻塞。
        /// </summary>
        Task<AgvResult> UnblockResourcesAsync(
            TrafficUnblockRequest request,
            CancellationToken cancellationToken = default);
    }
}
