using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockTaskConfigService : ITaskConfigService
    {
        public IReadOnlyList<TaskTemplateConfig> GetTaskTemplates()
        {
            return new[]
            {
                new TaskTemplateConfig { TemplateName = "产线物料搬运", TemplateType = "搬运任务", Scene = "车间内物流", Priority = "高", Description = "用于生产线之间的物料自动转运", IsEnabled = true, UpdateTime = "2024-01-12 10:20" },
                new TaskTemplateConfig { TemplateName = "成品入库搬运", TemplateType = "搬运任务", Scene = "仓储物流", Priority = "中", Description = "成品打包后的入库转运", IsEnabled = true, UpdateTime = "2024-01-11 15:30" },
                new TaskTemplateConfig { TemplateName = "夜间自动巡检", TemplateType = "巡检任务", Scene = "厂区安防", Priority = "低", Description = "夜间全厂区的自动安防巡检", IsEnabled = false, UpdateTime = "2024-01-10 09:15" },
                new TaskTemplateConfig { TemplateName = "设备异常处理", TemplateType = "其他任务", Scene = "设备维护", Priority = "高", Description = "当设备发出异常告警时前往确认", IsEnabled = true, UpdateTime = "2024-01-09 14:00" },
                new TaskTemplateConfig { TemplateName = "原料自动补货", TemplateType = "物料任务", Scene = "车间内物流", Priority = "中", Description = "根据线边仓库存自动触发补料", IsEnabled = true, UpdateTime = "2024-01-08 11:45" },
                new TaskTemplateConfig { TemplateName = "电量过低回充", TemplateType = "充电任务", Scene = "系统任务", Priority = "高", Description = "AGV电量低于20%自动回充电桩", IsEnabled = true, UpdateTime = "2024-01-05 16:20" }
            };
        }

        public IReadOnlyList<TaskStepConfig> GetTaskSteps()
        {
            return new[]
            {
                new TaskStepConfig { Step = "1", NodeType = "起点", NodeName = "开始", ActionConfig = "-", ParamConfig = "-", Timeout = "-" },
                new TaskStepConfig { Step = "2", NodeType = "任务节点", NodeName = "接收任务", ActionConfig = "分配AGV", ParamConfig = "空闲优先", Timeout = "60" },
                new TaskStepConfig { Step = "3", NodeType = "任务节点", NodeName = "前往取货点", ActionConfig = "移动", ParamConfig = "Target=A01", Timeout = "120" },
                new TaskStepConfig { Step = "4", NodeType = "任务节点", NodeName = "到达取货点", ActionConfig = "等待", ParamConfig = "-", Timeout = "10" },
                new TaskStepConfig { Step = "5", NodeType = "结束", NodeName = "任务完成", ActionConfig = "更新状态", ParamConfig = "Completed", Timeout = "-" }
            };
        }

        public IReadOnlyList<WorkflowConfig> GetWorkflows()
        {
            return new[]
            {
                new WorkflowConfig { WorkflowName = "标准入库流程", TriggerCondition = "扫码枪输入", NodeCount = 5, Creator = "Admin", IsEnabled = true },
                new WorkflowConfig { WorkflowName = "产线叫料流程", TriggerCondition = "机台信号触发", NodeCount = 3, Creator = "Engineer", IsEnabled = true },
                new WorkflowConfig { WorkflowName = "成品出库流程", TriggerCondition = "WMS下发", NodeCount = 4, Creator = "Admin", IsEnabled = false }
            };
        }

        public IReadOnlyList<PriorityConfig> GetPriorities()
        {
            return new[]
            {
                new PriorityConfig { TaskType = "充电任务", BasePriority = 99, DynamicRule = "电量<10%时最高", AllowPreempt = "允许抢占" },
                new PriorityConfig { TaskType = "设备告警响应", BasePriority = 90, DynamicRule = "无", AllowPreempt = "允许抢占" },
                new PriorityConfig { TaskType = "产线搬运", BasePriority = 50, DynamicRule = "等待时间>10分钟+10", AllowPreempt = "不允许" },
                new PriorityConfig { TaskType = "巡检任务", BasePriority = 20, DynamicRule = "无", AllowPreempt = "不允许" }
            };
        }

        public IReadOnlyList<ParameterConfig> GetParameters()
        {
            return new[]
            {
                new ParameterConfig { ParamKey = "MAX_WAIT_TIME", ParamName = "最大等待时间", ParamValue = "300", DataType = "Int(秒)", Description = "取货点允许等待的最大时间" },
                new ParameterConfig { ParamKey = "DEFAULT_SPEED", ParamName = "默认运行速度", ParamValue = "1.2", DataType = "Float(m/s)", Description = "任务下发时的默认速度" },
                new ParameterConfig { ParamKey = "RETRY_COUNT", ParamName = "任务重试次数", ParamValue = "3", DataType = "Int", Description = "对接失败后的自动重试次数" }
            };
        }

        public IReadOnlyList<ScheduleConfig> GetSchedules()
        {
            return new[]
            {
                new ScheduleConfig { ScheduleName = "夜间全厂巡检", TargetTemplate = "夜间自动巡检", CronExpr = "0 0 2 * * ?", NextRunTime = "2024-01-13 02:00:00", IsEnabled = true },
                new ScheduleConfig { ScheduleName = "早班原料预发", TargetTemplate = "原料自动补货", CronExpr = "0 30 7 * * ?", NextRunTime = "2024-01-13 07:30:00", IsEnabled = true },
                new ScheduleConfig { ScheduleName = "周五废料清理", TargetTemplate = "废料回收任务", CronExpr = "0 0 18 ? * FRI", NextRunTime = "2024-01-19 18:00:00", IsEnabled = false }
            };
        }

        public IReadOnlyList<RuleConfig> GetRules()
        {
            return new[]
            {
                new RuleConfig { RuleName = "低电量回充", EventSource = "AGV状态广播", Condition = "Battery < 20%", Action = "生成回充任务", IsEnabled = true },
                new RuleConfig { RuleName = "门禁联动", EventSource = "门禁控制器", Condition = "DoorStatus == Open", Action = "放行AGV", IsEnabled = true },
                new RuleConfig { RuleName = "机台空闲呼叫", EventSource = "PLC信号", Condition = "MachineStatus == Idle", Action = "生成搬运任务", IsEnabled = true }
            };
        }

        public IReadOnlyList<StrategyConfig> GetStrategies()
        {
            return new[]
            {
                new StrategyConfig { StrategyName = "就近分配策略", StrategyType = "分配策略", TargetGroup = "所有AGV", Description = "优先分配给距离起点最近的空闲AGV", IsEnabled = true },
                new StrategyConfig { StrategyName = "电量均衡策略", StrategyType = "调度策略", TargetGroup = "搬运AGV组", Description = "优先分配给电量较高的AGV以均衡消耗", IsEnabled = false },
                new StrategyConfig { StrategyName = "拥堵区域绕行", StrategyType = "路径策略", TargetGroup = "所有AGV", Description = "检测到区域拥堵时动态规划备用路线", IsEnabled = true }
            };
        }
    }
}
