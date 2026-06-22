using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Dispatching.Requests
{
    /// <summary>
    /// Requests another first-window lock attempt for a dispatch execution waiting on traffic.
    /// </summary>
    public sealed class RetryWaitingDispatchRequest : IAgvRequest
    {
        /// <summary>Gets the request context.</summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>Gets the task whose existing dispatch execution should be retried.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets a value indicating whether the vehicle command should be sent after locking.</summary>
        public bool SendVehicleCommand { get; init; } = true;
    }
}
