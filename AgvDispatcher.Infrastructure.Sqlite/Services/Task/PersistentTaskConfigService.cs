using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentTaskConfigService : ITaskConfigService
    {
        private readonly ITaskTemplateRepository _templates;
        private readonly ISystemParameterRepository _parameters;

        public PersistentTaskConfigService(ITaskTemplateRepository templates, ISystemParameterRepository parameters)
        {
            _templates = templates;
            _parameters = parameters;
        }

        public IReadOnlyList<TaskTemplateConfig> GetTaskTemplates()
        {
            return _templates.GetAllAsync().GetAwaiter().GetResult();
        }

        public IReadOnlyList<TaskStepConfig> GetTaskSteps()
        {
            return new[]
            {
                new TaskStepConfig { Step = "1", NodeType = "Start", NodeName = "Accept Task", ActionConfig = "Assign AGV", ParamConfig = "Idle first", Timeout = "60" },
                new TaskStepConfig { Step = "2", NodeType = "Move", NodeName = "Go To Source", ActionConfig = "Move", ParamConfig = "Target=Source", Timeout = "120" },
                new TaskStepConfig { Step = "3", NodeType = "End", NodeName = "Complete Task", ActionConfig = "Update State", ParamConfig = "Completed", Timeout = "-" }
            };
        }

        public IReadOnlyList<WorkflowConfig> GetWorkflows()
        {
            return new[]
            {
                new WorkflowConfig { WorkflowName = "Standard Inbound", TriggerCondition = "Scanner input", NodeCount = 5, Creator = "Admin", IsEnabled = true },
                new WorkflowConfig { WorkflowName = "Line Replenishment", TriggerCondition = "PLC signal", NodeCount = 3, Creator = "Engineer", IsEnabled = true }
            };
        }

        public IReadOnlyList<PriorityConfig> GetPriorities()
        {
            return new[]
            {
                new PriorityConfig { TaskType = "Charge", BasePriority = 99, DynamicRule = "Battery < 10%", AllowPreempt = "Allow" },
                new PriorityConfig { TaskType = "Transfer", BasePriority = 50, DynamicRule = "Wait > 10min +10", AllowPreempt = "Deny" }
            };
        }

        public IReadOnlyList<ParameterConfig> GetParameters()
        {
            return _parameters.GetAllAsync().GetAwaiter().GetResult();
        }

        public IReadOnlyList<ScheduleConfig> GetSchedules()
        {
            return new[]
            {
                new ScheduleConfig { ScheduleName = "Night Patrol", TargetTemplate = "Night Patrol", CronExpr = "0 0 2 * * ?", NextRunTime = "2024-01-13 02:00:00", IsEnabled = true },
                new ScheduleConfig { ScheduleName = "Morning Replenishment", TargetTemplate = "Material Transfer", CronExpr = "0 30 7 * * ?", NextRunTime = "2024-01-13 07:30:00", IsEnabled = true }
            };
        }

        public IReadOnlyList<RuleConfig> GetRules()
        {
            return new[]
            {
                new RuleConfig { RuleName = "Low Battery Recharge", EventSource = "AGV Status", Condition = "Battery < 20%", Action = "Create charge task", IsEnabled = true },
                new RuleConfig { RuleName = "Door Linkage", EventSource = "Door Controller", Condition = "DoorStatus == Open", Action = "Release AGV", IsEnabled = true }
            };
        }

        public IReadOnlyList<StrategyConfig> GetStrategies()
        {
            return new[]
            {
                new StrategyConfig { StrategyName = "Nearest Vehicle", StrategyType = "Assignment", TargetGroup = "All AGV", Description = "Assign the nearest idle vehicle.", IsEnabled = true },
                new StrategyConfig { StrategyName = "Battery Balance", StrategyType = "Dispatch", TargetGroup = "Transfer AGV", Description = "Prefer vehicles with higher battery levels.", IsEnabled = false }
            };
        }
    }
}
