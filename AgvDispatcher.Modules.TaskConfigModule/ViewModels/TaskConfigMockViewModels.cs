using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskConfigModule.ViewModels
{
    public class TaskConfigTemplateViewModel : TaskConfigTopPanelViewModel
    {
        public TaskConfigTemplateViewModel(ITaskConfigService taskConfigService)
            : base(taskConfigService)
        {
        }
    }

    public class WorkflowModel
    {
        public string WorkflowName { get; set; } = string.Empty;
        public string TriggerCondition { get; set; } = string.Empty;
        public int NodeCount { get; set; }
        public string Creator { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class TaskConfigWorkflowViewModel : BindableBase
    {
        public ObservableCollection<WorkflowModel> DataList { get; set; }

        public TaskConfigWorkflowViewModel(ITaskConfigService taskConfigService)
        {
            DataList = new ObservableCollection<WorkflowModel>(
                taskConfigService.GetWorkflows().Select(ToWorkflowModel));
        }

        private static WorkflowModel ToWorkflowModel(WorkflowConfig config)
        {
            return new WorkflowModel
            {
                WorkflowName = config.WorkflowName,
                TriggerCondition = config.TriggerCondition,
                NodeCount = config.NodeCount,
                Creator = config.Creator,
                IsEnabled = config.IsEnabled
            };
        }
    }

    public class PriorityModel
    {
        public string TaskType { get; set; } = string.Empty;
        public int BasePriority { get; set; }
        public string DynamicRule { get; set; } = string.Empty;
        public string AllowPreempt { get; set; } = string.Empty;
    }

    public class TaskConfigPriorityViewModel : BindableBase
    {
        public ObservableCollection<PriorityModel> DataList { get; set; }

        public TaskConfigPriorityViewModel(ITaskConfigService taskConfigService)
        {
            DataList = new ObservableCollection<PriorityModel>(
                taskConfigService.GetPriorities().Select(ToPriorityModel));
        }

        private static PriorityModel ToPriorityModel(PriorityConfig config)
        {
            return new PriorityModel
            {
                TaskType = config.TaskType,
                BasePriority = config.BasePriority,
                DynamicRule = config.DynamicRule,
                AllowPreempt = config.AllowPreempt
            };
        }
    }

    public class ParameterModel
    {
        public string ParamKey { get; set; } = string.Empty;
        public string ParamName { get; set; } = string.Empty;
        public string ParamValue { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class TaskConfigParameterViewModel : BindableBase
    {
        public ObservableCollection<ParameterModel> DataList { get; set; }

        public TaskConfigParameterViewModel(ITaskConfigService taskConfigService)
        {
            DataList = new ObservableCollection<ParameterModel>(
                taskConfigService.GetParameters().Select(ToParameterModel));
        }

        private static ParameterModel ToParameterModel(ParameterConfig config)
        {
            return new ParameterModel
            {
                ParamKey = config.ParamKey,
                ParamName = config.ParamName,
                ParamValue = config.ParamValue,
                DataType = config.DataType,
                Description = config.Description
            };
        }
    }

    public class ScheduleModel
    {
        public string ScheduleName { get; set; } = string.Empty;
        public string TargetTemplate { get; set; } = string.Empty;
        public string CronExpr { get; set; } = string.Empty;
        public string NextRunTime { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class TaskConfigScheduleViewModel : BindableBase
    {
        public ObservableCollection<ScheduleModel> DataList { get; set; }

        public TaskConfigScheduleViewModel(ITaskConfigService taskConfigService)
        {
            DataList = new ObservableCollection<ScheduleModel>(
                taskConfigService.GetSchedules().Select(ToScheduleModel));
        }

        private static ScheduleModel ToScheduleModel(ScheduleConfig config)
        {
            return new ScheduleModel
            {
                ScheduleName = config.ScheduleName,
                TargetTemplate = config.TargetTemplate,
                CronExpr = config.CronExpr,
                NextRunTime = config.NextRunTime,
                IsEnabled = config.IsEnabled
            };
        }
    }

    public class RuleModel
    {
        public string RuleName { get; set; } = string.Empty;
        public string EventSource { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class TaskConfigRuleViewModel : BindableBase
    {
        public ObservableCollection<RuleModel> DataList { get; set; }

        public TaskConfigRuleViewModel(ITaskConfigService taskConfigService)
        {
            DataList = new ObservableCollection<RuleModel>(
                taskConfigService.GetRules().Select(ToRuleModel));
        }

        private static RuleModel ToRuleModel(RuleConfig config)
        {
            return new RuleModel
            {
                RuleName = config.RuleName,
                EventSource = config.EventSource,
                Condition = config.Condition,
                Action = config.Action,
                IsEnabled = config.IsEnabled
            };
        }
    }

    public class StrategyModel
    {
        public string StrategyName { get; set; } = string.Empty;
        public string StrategyType { get; set; } = string.Empty;
        public string TargetGroup { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class TaskConfigStrategyViewModel : BindableBase
    {
        public ObservableCollection<StrategyModel> DataList { get; set; }

        public TaskConfigStrategyViewModel(ITaskConfigService taskConfigService)
        {
            DataList = new ObservableCollection<StrategyModel>(
                taskConfigService.GetStrategies().Select(ToStrategyModel));
        }

        private static StrategyModel ToStrategyModel(StrategyConfig config)
        {
            return new StrategyModel
            {
                StrategyName = config.StrategyName,
                StrategyType = config.StrategyType,
                TargetGroup = config.TargetGroup,
                Description = config.Description,
                IsEnabled = config.IsEnabled
            };
        }
    }
}
