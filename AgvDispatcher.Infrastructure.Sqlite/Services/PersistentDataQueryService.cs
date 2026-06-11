using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Mock;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentDataQueryService : IDataQueryService
    {
        private readonly IAlarmService _alarms;
        private readonly IOperationLogService _operationLogs;
        private readonly ITaskService _taskService;
        private readonly IChargeStationRepository _chargeStationRepository;

        public PersistentDataQueryService(
            IAlarmService alarms, 
            IOperationLogService operationLogs,
            ITaskService taskService,
            IChargeStationRepository chargeStationRepository)
        {
            _alarms = alarms;
            _operationLogs = operationLogs;
            _taskService = taskService;
            _chargeStationRepository = chargeStationRepository;
        }

        public IReadOnlyList<TaskRunRecord> GetTaskRunRecords()
        {
            var tasks = _taskService.GetTasks();

            return tasks.Select((t, index) => new TaskRunRecord
            {
                Seq = index + 1,
                QueryTime = t.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                AgvId = string.IsNullOrWhiteSpace(t.AssignedVehicleId) ? "-" : t.AssignedVehicleId,
                TaskId = t.TaskNo,
                TaskType = t.TaskType,
                StartPoint = t.SourceNodeId,
                EndPoint = t.TargetNodeId,
                Status = t.State.ToString(),
                Duration = t.FinishedAt.HasValue && t.StartedAt.HasValue 
                    ? $"{(t.FinishedAt.Value - t.StartedAt.Value).TotalMinutes:F1} min" 
                    : (t.StartedAt.HasValue ? $"{(DateTime.Now - t.StartedAt.Value).TotalMinutes:F1} min" : "-"),
                Distance = "-",
                AvgSpeed = "-"
            }).ToArray();
        }

        public IReadOnlyList<ChargeRecord> GetChargeRecords()
        {
            var sessions = _chargeStationRepository.GetSessionsAsync().GetAwaiter().GetResult();
            var records = new List<ChargeRecord>();
            int seq = 1;
            foreach (var r in sessions)
            {
                records.Add(new ChargeRecord
                {
                    Seq = seq++,
                    Time = r.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    AgvId = r.VehicleId,
                    ChargeStation = r.StationId,
                    StartBattery = $"{r.StartBatteryLevel:F1}%",
                    EndBattery = $"{r.EndBatteryLevel:F1}%",
                    ChargeDuration = r.EndTime.HasValue ? $"{(r.EndTime.Value - r.StartTime).TotalMinutes:F1} min" : "Charging",
                    ChargeAmount = $"{r.EnergyConsumedKwh:F2} kWh"
                });
            }
            return records;
        }

        public IReadOnlyList<AlarmRecord> GetAlarmRecords()
        {
            var alarmRecords = _alarms.QueryAlarms(new AlarmQuery { Take = 200 })
                .Select((alarm, index) => new AlarmRecord
                {
                    Seq = index + 1,
                    Time = alarm.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    AgvId = alarm.VehicleId ?? alarm.SourceId,
                    AlarmLevel = ToDisplayText(alarm.Severity),
                    AlarmCode = alarm.AlarmCode,
                    AlarmDesc = string.IsNullOrWhiteSpace(alarm.Description) ? alarm.Name : alarm.Description,
                    Status = ToDisplayText(alarm.State)
                })
                .ToArray();

            return alarmRecords;
        }

        public IReadOnlyList<InteractionRecord> GetInteractionRecords()
        {
            return Array.Empty<InteractionRecord>();
        }

        public IReadOnlyList<EnergyRecord> GetEnergyRecords()
        {
            return Array.Empty<EnergyRecord>();
        }

        public IReadOnlyList<DeviceLogRecord> GetDeviceLogRecords()
        {
            var logRecords = _operationLogs.QueryLogs(new OperationLogQuery { Take = 200 })
                .Select((log, index) => new DeviceLogRecord
                {
                    Seq = index + 1,
                    Time = log.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    DeviceType = log.Category,
                    DeviceId = log.VehicleId ?? log.TaskId ?? log.SourceId ?? "-",
                    LogLevel = ToLogLevel(log),
                    LogContent = $"{log.Action}: {log.Message}"
                })
                .ToArray();

            return logRecords;
        }

        private static string ToDisplayText(AlarmSeverity severity)
        {
            return severity switch
            {
                AlarmSeverity.Info => "提示",
                AlarmSeverity.Warning => "警告",
                AlarmSeverity.Major => "重要",
                AlarmSeverity.Critical => "严重",
                _ => severity.ToString()
            };
        }

        private static string ToDisplayText(AlarmState state)
        {
            return state switch
            {
                AlarmState.Active => "未处理",
                AlarmState.Acknowledged => "已确认",
                AlarmState.Cleared => "已恢复",
                AlarmState.Suppressed => "已抑制",
                _ => state.ToString()
            };
        }

        private static string ToLogLevel(OperationLog log)
        {
            if (log.Category == "Alarm")
            {
                return log.Action == "Raised" ? "WARN" : "INFO";
            }

            if (log.Metadata.TryGetValue("Succeeded", out var succeeded)
                && bool.TryParse(succeeded, out var parsed)
                && !parsed)
            {
                return "ERROR";
            }

            return "INFO";
        }
    }
}
