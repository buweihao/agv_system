using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class TaskOrder
    {
        public string TaskId { get; set; } = string.Empty;

        public string TaskNo { get; set; } = string.Empty;

        public string TemplateId { get; set; } = string.Empty;

        public string TaskType { get; set; } = string.Empty;

        public TaskPriority Priority { get; set; } = TaskPriority.Normal;

        public TaskState State { get; set; } = TaskState.Pending;

        public string SourceNodeId { get; set; } = string.Empty;

        public string TargetNodeId { get; set; } = string.Empty;

        public string? CurrentNodeId { get; set; }

        public string? AssignedVehicleId { get; set; }

        public string CargoCode { get; set; } = string.Empty;

        public string CargoName { get; set; } = string.Empty;

        public double CargoWeight { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? PlannedStartAt { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? FinishedAt { get; set; }

        public DateTime? DeadlineAt { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public string? CancelReason { get; set; }

        public string? FailureReason { get; set; }

        public int ProgressPercent { get; set; }

        public VehicleCapability RequiredCapabilities { get; set; } = VehicleCapability.None;

        public string AllowedBrands { get; set; } = string.Empty;

        public string ForbiddenBrands { get; set; } = string.Empty;

        public double? MinBatteryRequired { get; set; }

        public Dictionary<string, string> Attributes { get; set; } = new();
    }
}
