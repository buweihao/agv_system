using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentAuditTrailService : IAuditTrailService
    {
        private readonly IOperationLogService _operationLogs;

        public PersistentAuditTrailService(IOperationLogService operationLogs)
        {
            _operationLogs = operationLogs;
        }

        public void Record(OperationLog log)
        {
            ArgumentNullException.ThrowIfNull(log);
            _operationLogs.WriteLog(log);
        }

        public void RecordVehicleStatusUpdated(VehicleStatusSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            Record(new OperationLog
            {
                Category = "Vehicle",
                Action = "StatusUpdated",
                Message = $"{snapshot.VehicleId} reported {snapshot.State}, battery {snapshot.BatteryLevel:0.#}%, location {snapshot.Location}.",
                Operator = snapshot.Brand,
                VehicleId = snapshot.VehicleId,
                TaskId = snapshot.CurrentTaskId,
                SourceId = snapshot.VehicleId,
                OccurredAt = snapshot.ReportedAt == default ? DateTime.Now : snapshot.ReportedAt,
                Metadata =
                {
                    ["State"] = snapshot.State.ToString(),
                    ["BatteryLevel"] = snapshot.BatteryLevel.ToString("0.#"),
                    ["Location"] = snapshot.Location,
                    ["Brand"] = snapshot.Brand
                }
            });
        }

        public void RecordDispatchResult(DispatchResult result, string action)
        {
            ArgumentNullException.ThrowIfNull(result);

            Record(new OperationLog
            {
                Category = "Dispatch",
                Action = action,
                Message = result.Message,
                Operator = "DispatchService",
                VehicleId = result.VehicleId,
                TaskId = result.TaskId,
                SourceId = result.CommandId,
                OccurredAt = result.OccurredAt == default ? DateTime.Now : result.OccurredAt,
                Metadata =
                {
                    ["Succeeded"] = result.Succeeded.ToString(),
                    ["Code"] = result.Code,
                    ["CommandId"] = result.CommandId ?? string.Empty
                }
            });
        }

        public void RecordAlarmRaised(AlarmEvent alarm)
        {
            ArgumentNullException.ThrowIfNull(alarm);

            Record(new OperationLog
            {
                Category = "Alarm",
                Action = "Raised",
                Message = $"{alarm.Severity} alarm {alarm.AlarmCode}: {alarm.Name}",
                Operator = "AlarmService",
                VehicleId = alarm.VehicleId,
                TaskId = alarm.TaskId,
                SourceId = alarm.AlarmId,
                OccurredAt = alarm.OccurredAt == default ? DateTime.Now : alarm.OccurredAt,
                Metadata =
                {
                    ["AlarmCode"] = alarm.AlarmCode,
                    ["Severity"] = alarm.Severity.ToString(),
                    ["State"] = alarm.State.ToString(),
                    ["SourceType"] = alarm.SourceType,
                    ["SourceId"] = alarm.SourceId
                }
            });
        }

        public void RecordAlarmAcknowledged(AlarmEvent alarm, string acknowledgedBy)
        {
            ArgumentNullException.ThrowIfNull(alarm);

            Record(new OperationLog
            {
                Category = "Alarm",
                Action = "Acknowledged",
                Message = $"{alarm.AlarmCode} acknowledged by {acknowledgedBy}.",
                Operator = acknowledgedBy,
                VehicleId = alarm.VehicleId,
                TaskId = alarm.TaskId,
                SourceId = alarm.AlarmId,
                Metadata =
                {
                    ["AlarmCode"] = alarm.AlarmCode,
                    ["Severity"] = alarm.Severity.ToString()
                }
            });
        }

        public void RecordAlarmCleared(AlarmEvent alarm, string? reason)
        {
            ArgumentNullException.ThrowIfNull(alarm);

            Record(new OperationLog
            {
                Category = "Alarm",
                Action = "Cleared",
                Message = string.IsNullOrWhiteSpace(reason)
                    ? $"{alarm.AlarmCode} cleared."
                    : $"{alarm.AlarmCode} cleared: {reason}",
                Operator = "AlarmService",
                VehicleId = alarm.VehicleId,
                TaskId = alarm.TaskId,
                SourceId = alarm.AlarmId,
                Metadata =
                {
                    ["AlarmCode"] = alarm.AlarmCode,
                    ["Reason"] = reason ?? string.Empty
                }
            });
        }
    }
}
