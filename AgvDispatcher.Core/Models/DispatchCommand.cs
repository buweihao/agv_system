using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class DispatchCommand
    {
        public string CommandId { get; set; } = string.Empty;

        public DispatchCommandType CommandType { get; set; }

        public DispatchCommandState State { get; set; } = DispatchCommandState.Created;

        public string VehicleId { get; set; } = string.Empty;

        public string? TaskId { get; set; }

        public string? TargetNodeId { get; set; }

        public string? SourceNodeId { get; set; }

        public string? CorrelationId { get; set; }

        public int Priority { get; set; }

        public string IssuedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? SentAt { get; set; }

        public DateTime? AcceptedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public int RetryCount { get; set; }

        public int MaxRetryCount { get; set; } = 3;

        public string PayloadJson { get; set; } = string.Empty;

        public string? ResultCode { get; set; }

        public string? ResultMessage { get; set; }

        public string? AreaId { get; set; }

        public int? RequestControlType { get; set; }

        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}
