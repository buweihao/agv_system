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
    /// </summary>
    public interface ITrafficControlService
    {
        /// <summary>
        /// Gets the current traffic snapshot for route planning and dispatch decisions.
        /// </summary>
        Task<AgvResult<TrafficSnapshotDto>> GetTrafficSnapshotAsync(
            RequestContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current status of a single traffic resource.
        /// </summary>
        Task<AgvResult<TrafficResourceStatusDto>> GetResourceStatusAsync(
            TrafficResourceKey resource,
            RequestContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current statuses of multiple traffic resources.
        /// </summary>
        Task<AgvResult<IReadOnlyList<TrafficResourceStatusDto>>> GetResourceStatusesAsync(
            IReadOnlyList<TrafficResourceKey> resources,
            RequestContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether the requested resources are currently available.
        /// </summary>
        Task<AgvResult<TrafficAvailabilityResultDto>> CheckAvailabilityAsync(
            TrafficAvailabilityRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to reserve, occupy, lock, or block the requested resources.
        /// </summary>
        Task<AgvResult<TrafficReservationDto>> TryAcquireAsync(
            TrafficAcquireRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases resources previously reserved, occupied, or locked by an AGV or task.
        /// </summary>
        Task<AgvResult> ReleaseAsync(
            TrafficReleaseRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the runtime occupancy reported by an AGV.
        /// </summary>
        Task<AgvResult> UpdateAgvOccupancyAsync(
            AgvOccupancyUpdateRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually blocks traffic resources for operation control or maintenance.
        /// </summary>
        Task<AgvResult<TrafficBlockDto>> BlockResourcesAsync(
            TrafficBlockRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a manual block from traffic resources.
        /// </summary>
        Task<AgvResult> UnblockResourcesAsync(
            TrafficUnblockRequest request,
            CancellationToken cancellationToken = default);
    }
}
