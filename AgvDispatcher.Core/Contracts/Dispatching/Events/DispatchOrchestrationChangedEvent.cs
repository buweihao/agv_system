using System;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;

namespace AgvDispatcher.Core.Contracts.Dispatching.Events
{
    /// <summary>
    /// Describes an observable state or milestone change in dispatch orchestration.
    /// </summary>
    public sealed class DispatchOrchestrationChangedEvent
    {
        /// <summary>Gets the task identifier.</summary>
        public string TaskId { get; init; } = string.Empty;

        /// <summary>Gets the dispatch execution identifier, if one has been assigned.</summary>
        public string? ExecutionId { get; init; }

        /// <summary>Gets the selected vehicle identifier, if one has been assigned.</summary>
        public string? VehicleId { get; init; }

        /// <summary>Gets the dispatch execution state at the time of the event.</summary>
        public DispatchExecutionState State { get; init; }

        /// <summary>Gets the orchestration milestone represented by the event.</summary>
        public DispatchOrchestrationEventType EventType { get; init; }

        /// <summary>Gets an optional event message.</summary>
        public string? Message { get; init; }

        /// <summary>Gets the time at which the event occurred.</summary>
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.Now;
    }
}
