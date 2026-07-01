using System;

namespace AgvDispatcher.Core.Events
{
    public sealed class ActiveMapChangedEvent : IDomainEvent
    {
        public string? OldMapId { get; init; }

        public string? OldMapVersion { get; init; }

        public string NewMapId { get; init; } = string.Empty;

        public string NewMapVersion { get; init; } = string.Empty;

        public DateTime ChangedAt { get; init; } = DateTime.Now;

        public string? OperatorId { get; init; }

        public string? Reason { get; init; }

        public DateTime OccurredAt { get; init; } = DateTime.Now;
    }
}
