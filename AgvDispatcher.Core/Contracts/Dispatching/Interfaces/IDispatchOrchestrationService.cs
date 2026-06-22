using System.Threading;
using System.Threading.Tasks;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Models;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Dispatching.Results;

namespace AgvDispatcher.Core.Contracts.Dispatching.Interfaces
{
    /// <summary>
    /// Orchestrates task dispatch across vehicle selection, map and traffic queries,
    /// path planning, route reservation, and vehicle command delivery boundaries.
    /// </summary>
    /// <remarks>
    /// Implementations coordinate other modules but do not own their domain logic.
    /// </remarks>
    public interface IDispatchOrchestrationService
    {
        /// <summary>
        /// Starts a dispatch execution for a task.
        /// </summary>
        Task<AgvResult<StartDispatchTaskResultDto>> StartTaskAsync(
            StartDispatchTaskRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retries the first rolling-window lock for an existing execution that is waiting for traffic.
        /// </summary>
        Task<AgvResult<RetryWaitingDispatchResultDto>> RetryWaitingTaskAsync(
            RetryWaitingDispatchRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Advances a running route by releasing passed resources and optionally acquiring the next window.
        /// </summary>
        Task<AgvResult<AdvanceDispatchRouteResultDto>> AdvanceRouteAsync(
            AdvanceDispatchRouteRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Completes a running dispatch execution and releases its remaining route reservation.
        /// </summary>
        Task<AgvResult> CompleteTaskAsync(
            CompleteDispatchTaskRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a dispatch execution and optionally releases its reservation and sends a vehicle command.
        /// </summary>
        Task<AgvResult> CancelTaskAsync(
            CancelDispatchTaskRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current dispatch execution for a task or execution identifier.
        /// </summary>
        Task<AgvResult<DispatchExecutionDto>> GetExecutionAsync(
            GetDispatchExecutionRequest request,
            CancellationToken cancellationToken = default);
    }
}
