using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    /// <summary>
    /// 右侧面板使用的实时告警条目（用于列表绑定展示）。
    /// </summary>
    public class AlarmMessage
    {
        /// <summary>告警发生时间文本（HH:mm:ss）。</summary>
        public string Time { get; set; } = string.Empty;

        /// <summary>触发告警的设备/车辆标识。</summary>
        public string Device { get; set; } = string.Empty;

        /// <summary>告警描述信息。</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>告警严重程度对应的显示颜色（十六进制色值）。</summary>
        public string SeverityColor { get; set; } = "Red";
    }

    /// <summary>
    /// 运行监控页面"右侧统计面板"的 ViewModel。
    /// <para>
    /// 汇总运营与设备健康指标：任务完成率/今日完成数/今日里程/平均速度、设备健康评分与
    /// 健康/异常/离线设备数，以及环境信息（温湿度、信号强度）和最近实时告警。
    /// 在构造时从车辆服务、任务服务、告警服务一次性拉取数据计算（属性为只读快照）。
    /// </para>
    /// </summary>
    public class RightPanelViewModel : BindableBase
    {
        /// <summary>任务完成率（百分比，保留 1 位小数）。</summary>
        public double TaskCompletionRate { get; }

        /// <summary>今日已完成任务数。</summary>
        public int TodayCompletedTasks { get; }

        /// <summary>今日累计里程（km，当前为演示占位值）。</summary>
        public double TodayDistance { get; } = 128.6;

        /// <summary>全部车辆平均速度（m/s，保留 2 位小数）。</summary>
        public double AvgSpeed { get; }

        /// <summary>设备整体健康评分（健康设备占比，百分比）。</summary>
        public double HealthScore { get; }

        /// <summary>健康设备数（空闲或运行且无告警）。</summary>
        public int HealthyDeviceCount { get; }

        /// <summary>异常设备数（故障或存在告警）。</summary>
        public int AbnormalDeviceCount { get; }

        /// <summary>离线设备数（离线状态或不在线）。</summary>
        public int OfflineDeviceCount { get; }

        /// <summary>环境温度（℃，当前为演示占位值）。</summary>
        public double Temperature { get; } = 24.5;

        /// <summary>环境湿度（%，当前为演示占位值）。</summary>
        public int Humidity { get; } = 45;

        /// <summary>信号强度描述（当前为演示占位值）。</summary>
        public string SignalStrength { get; } = "优";

        /// <summary>最近的实时告警列表（取最新 5 条）。</summary>
        public ObservableCollection<AlarmMessage> RealtimeAlarms { get; }

        /// <summary>
        /// 构造函数，从各服务拉取数据并计算统计指标。
        /// </summary>
        /// <param name="vehicleService">车辆服务，提供车辆状态用于速度/健康统计。</param>
        /// <param name="taskService">任务服务，提供任务用于完成率统计。</param>
        /// <param name="alarmService">告警服务，提供活动告警用于实时告警列表。</param>
        public RightPanelViewModel(
            IVehicleService vehicleService,
            ITaskService taskService,
            IAlarmService alarmService)
        {
            var statuses = vehicleService.GetVehicleStatuses();
            var tasks = taskService.GetTasks();
            var completedTasks = tasks.Count(item => item.State == TaskState.Completed);

            // 任务完成情况
            TodayCompletedTasks = completedTasks;
            TaskCompletionRate = tasks.Count == 0 ? 0 : Math.Round(completedTasks * 100.0 / tasks.Count, 1);
            AvgSpeed = statuses.Count == 0 ? 0 : Math.Round(statuses.Average(item => item.Speed), 2);

            // 设备健康分类与评分
            HealthyDeviceCount = statuses.Count(item => (item.State is RobotState.Idle or RobotState.Running) && !item.HasAlarm);
            AbnormalDeviceCount = statuses.Count(item => item.State == RobotState.Fault || item.HasAlarm);
            OfflineDeviceCount = statuses.Count(item => item.State == RobotState.Offline || !item.IsOnline);
            HealthScore = statuses.Count == 0 ? 0 : Math.Round(HealthyDeviceCount * 100.0 / statuses.Count, 1);

            // 取最新 5 条活动告警并转换为展示模型
            RealtimeAlarms = new ObservableCollection<AlarmMessage>(
                alarmService.GetActiveAlarms()
                    .OrderByDescending(item => item.OccurredAt)
                    .Take(5)
                    .Select(ToAlarmMessage));
        }

        /// <summary>
        /// 将领域告警 <see cref="AlarmEvent"/> 转换为展示用的 <see cref="AlarmMessage"/>，
        /// 并按严重程度映射颜色。
        /// </summary>
        private static AlarmMessage ToAlarmMessage(AlarmEvent alarm)
        {
            return new AlarmMessage
            {
                Device = alarm.VehicleId ?? alarm.SourceId,
                Message = string.IsNullOrWhiteSpace(alarm.Description) ? alarm.Name : alarm.Description,
                Time = alarm.OccurredAt.ToString("HH:mm:ss"),
                SeverityColor = alarm.Severity switch
                {
                    AlarmSeverity.Critical => "#FF4500",
                    AlarmSeverity.Major => "#FF8C00",
                    AlarmSeverity.Warning => "#FFD700",
                    _ => "#1E90FF"
                }
            };
        }
    }
}
