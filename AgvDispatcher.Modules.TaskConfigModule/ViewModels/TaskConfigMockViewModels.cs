using System.Collections.ObjectModel;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskConfigModule.ViewModels
{
    // TaskConfigTopPanelViewModel 已经定义了 TemplateList
    public class TaskConfigTemplateViewModel : TaskConfigTopPanelViewModel { }

    // ==========================================
    // 任务流程 (Workflow)
    // ==========================================
    public class WorkflowModel
    {
        public string WorkflowName { get; set; }
        public string TriggerCondition { get; set; }
        public int NodeCount { get; set; }
        public string Creator { get; set; }
        public bool IsEnabled { get; set; }
    }
    public class TaskConfigWorkflowViewModel : BindableBase
    {
        public ObservableCollection<WorkflowModel> DataList { get; set; } = new();
        public TaskConfigWorkflowViewModel()
        {
            DataList.Add(new WorkflowModel { WorkflowName = "标准入库流程", TriggerCondition = "扫码枪输入", NodeCount = 5, Creator = "Admin", IsEnabled = true });
            DataList.Add(new WorkflowModel { WorkflowName = "产线叫料流程", TriggerCondition = "机台信号触发", NodeCount = 3, Creator = "Engineer", IsEnabled = true });
            DataList.Add(new WorkflowModel { WorkflowName = "成品出库流程", TriggerCondition = "WMS下发", NodeCount = 4, Creator = "Admin", IsEnabled = false });
        }
    }

    // ==========================================
    // 任务优先级 (Priority)
    // ==========================================
    public class PriorityModel
    {
        public string TaskType { get; set; }
        public int BasePriority { get; set; }
        public string DynamicRule { get; set; }
        public string AllowPreempt { get; set; }
    }
    public class TaskConfigPriorityViewModel : BindableBase
    {
        public ObservableCollection<PriorityModel> DataList { get; set; } = new();
        public TaskConfigPriorityViewModel()
        {
            DataList.Add(new PriorityModel { TaskType = "充电任务", BasePriority = 99, DynamicRule = "电量<10%时最高", AllowPreempt = "允许抢占" });
            DataList.Add(new PriorityModel { TaskType = "设备告警响应", BasePriority = 90, DynamicRule = "无", AllowPreempt = "允许抢占" });
            DataList.Add(new PriorityModel { TaskType = "产线搬运", BasePriority = 50, DynamicRule = "等待时间>10分钟+10", AllowPreempt = "不允许" });
            DataList.Add(new PriorityModel { TaskType = "巡检任务", BasePriority = 20, DynamicRule = "无", AllowPreempt = "不允许" });
        }
    }

    // ==========================================
    // 任务参数 (Parameter)
    // ==========================================
    public class ParameterModel
    {
        public string ParamKey { get; set; }
        public string ParamName { get; set; }
        public string ParamValue { get; set; }
        public string DataType { get; set; }
        public string Description { get; set; }
    }
    public class TaskConfigParameterViewModel : BindableBase
    {
        public ObservableCollection<ParameterModel> DataList { get; set; } = new();
        public TaskConfigParameterViewModel()
        {
            DataList.Add(new ParameterModel { ParamKey = "MAX_WAIT_TIME", ParamName = "最大等待时间", ParamValue = "300", DataType = "Int(秒)", Description = "取货点允许等待的最大时间" });
            DataList.Add(new ParameterModel { ParamKey = "DEFAULT_SPEED", ParamName = "默认运行速度", ParamValue = "1.2", DataType = "Float(m/s)", Description = "任务下发时的默认速度" });
            DataList.Add(new ParameterModel { ParamKey = "RETRY_COUNT", ParamName = "任务重试次数", ParamValue = "3", DataType = "Int", Description = "对接失败后的自动重试次数" });
        }
    }

    // ==========================================
    // 定时任务 (Schedule)
    // ==========================================
    public class ScheduleModel
    {
        public string ScheduleName { get; set; }
        public string TargetTemplate { get; set; }
        public string CronExpr { get; set; }
        public string NextRunTime { get; set; }
        public bool IsEnabled { get; set; }
    }
    public class TaskConfigScheduleViewModel : BindableBase
    {
        public ObservableCollection<ScheduleModel> DataList { get; set; } = new();
        public TaskConfigScheduleViewModel()
        {
            DataList.Add(new ScheduleModel { ScheduleName = "夜间全厂巡检", TargetTemplate = "夜间自动巡检", CronExpr = "0 0 2 * * ?", NextRunTime = "2024-01-13 02:00:00", IsEnabled = true });
            DataList.Add(new ScheduleModel { ScheduleName = "早班原料预发", TargetTemplate = "原料自动补货", CronExpr = "0 30 7 * * ?", NextRunTime = "2024-01-13 07:30:00", IsEnabled = true });
            DataList.Add(new ScheduleModel { ScheduleName = "周五废料清理", TargetTemplate = "废料回收任务", CronExpr = "0 0 18 ? * FRI", NextRunTime = "2024-01-19 18:00:00", IsEnabled = false });
        }
    }

    // ==========================================
    // 触发规则 (Rule)
    // ==========================================
    public class RuleModel
    {
        public string RuleName { get; set; }
        public string EventSource { get; set; }
        public string Condition { get; set; }
        public string Action { get; set; }
        public bool IsEnabled { get; set; }
    }
    public class TaskConfigRuleViewModel : BindableBase
    {
        public ObservableCollection<RuleModel> DataList { get; set; } = new();
        public TaskConfigRuleViewModel()
        {
            DataList.Add(new RuleModel { RuleName = "低电量回充", EventSource = "AGV状态广播", Condition = "Battery < 20%", Action = "生成回充任务", IsEnabled = true });
            DataList.Add(new RuleModel { RuleName = "门禁联动", EventSource = "门禁控制器", Condition = "DoorStatus == Open", Action = "放行AGV", IsEnabled = true });
            DataList.Add(new RuleModel { RuleName = "机台空闲呼叫", EventSource = "PLC信号", Condition = "MachineStatus == Idle", Action = "生成搬运任务", IsEnabled = true });
        }
    }

    // ==========================================
    // 任务策略 (Strategy)
    // ==========================================
    public class StrategyModel
    {
        public string StrategyName { get; set; }
        public string StrategyType { get; set; }
        public string TargetGroup { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; }
    }
    public class TaskConfigStrategyViewModel : BindableBase
    {
        public ObservableCollection<StrategyModel> DataList { get; set; } = new();
        public TaskConfigStrategyViewModel()
        {
            DataList.Add(new StrategyModel { StrategyName = "就近分配策略", StrategyType = "分配策略", TargetGroup = "所有AGV", Description = "优先分配给距离起点最近的空闲AGV", IsEnabled = true });
            DataList.Add(new StrategyModel { StrategyName = "电量均衡策略", StrategyType = "调度策略", TargetGroup = "搬运AGV组", Description = "优先分配给电量较高的AGV以均衡消耗", IsEnabled = false });
            DataList.Add(new StrategyModel { StrategyName = "拥堵区域绕行", StrategyType = "路径策略", TargetGroup = "所有AGV", Description = "检测到区域拥堵时动态规划备用路线", IsEnabled = true });
        }
    }
}
