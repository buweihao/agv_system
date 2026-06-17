using System;

namespace AgvDispatcher.Core.Contracts.Common
{
    public sealed class RequestContext
    {
        public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

        public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");

        public string SourceModule { get; init; } = string.Empty;

        public string? OperatorId { get; init; }

        public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.Now;

        public int TimeoutMs { get; init; } = 3000;
    }
}
