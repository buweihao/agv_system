using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class TaskCreateRequest
    {
        public string TaskType { get; set; } = string.Empty;

        public string TemplateId { get; set; } = string.Empty;

        public string SourceNodeId { get; set; } = string.Empty;

        public string TargetNodeId { get; set; } = string.Empty;

        public TaskPriority Priority { get; set; } = TaskPriority.Normal;

        public string CargoCode { get; set; } = string.Empty;

        public string CargoName { get; set; } = string.Empty;

        public double CargoWeight { get; set; }

        public DateTime? PlannedStartAt { get; set; }

        public DateTime? DeadlineAt { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public Dictionary<string, string> Attributes { get; set; } = new();
    }
}
