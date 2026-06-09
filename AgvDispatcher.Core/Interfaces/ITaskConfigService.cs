using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface ITaskConfigService
    {
        IReadOnlyList<TaskTemplateConfig> GetTaskTemplates();

        IReadOnlyList<TaskStepConfig> GetTaskSteps();

        IReadOnlyList<WorkflowConfig> GetWorkflows();

        IReadOnlyList<PriorityConfig> GetPriorities();

        IReadOnlyList<ParameterConfig> GetParameters();

        IReadOnlyList<ScheduleConfig> GetSchedules();

        IReadOnlyList<RuleConfig> GetRules();

        IReadOnlyList<StrategyConfig> GetStrategies();
    }
}
