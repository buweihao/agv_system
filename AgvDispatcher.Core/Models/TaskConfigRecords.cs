namespace AgvDispatcher.Core.Models
{
    public class TaskTemplateConfig
    {
        public string TemplateName { get; set; } = string.Empty;
        public string TemplateType { get; set; } = string.Empty;
        public string Scene { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string UpdateTime { get; set; } = string.Empty;
    }

    public class TaskStepConfig
    {
        public string Step { get; set; } = string.Empty;
        public string NodeType { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string ActionConfig { get; set; } = string.Empty;
        public string ParamConfig { get; set; } = string.Empty;
        public string Timeout { get; set; } = string.Empty;
    }

    public class WorkflowConfig
    {
        public string WorkflowName { get; set; } = string.Empty;
        public string TriggerCondition { get; set; } = string.Empty;
        public int NodeCount { get; set; }
        public string Creator { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class PriorityConfig
    {
        public string TaskType { get; set; } = string.Empty;
        public int BasePriority { get; set; }
        public string DynamicRule { get; set; } = string.Empty;
        public string AllowPreempt { get; set; } = string.Empty;
    }

    public class ParameterConfig
    {
        public string ParamKey { get; set; } = string.Empty;
        public string ParamName { get; set; } = string.Empty;
        public string ParamValue { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ScheduleConfig
    {
        public string ScheduleName { get; set; } = string.Empty;
        public string TargetTemplate { get; set; } = string.Empty;
        public string CronExpr { get; set; } = string.Empty;
        public string NextRunTime { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class RuleConfig
    {
        public string RuleName { get; set; } = string.Empty;
        public string EventSource { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class StrategyConfig
    {
        public string StrategyName { get; set; } = string.Empty;
        public string StrategyType { get; set; } = string.Empty;
        public string TargetGroup { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }
}
