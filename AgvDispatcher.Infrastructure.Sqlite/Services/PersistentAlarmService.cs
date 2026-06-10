using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentAlarmService : IAlarmService
    {
        private readonly IAlarmRepository _alarms;
        private readonly IAuditTrailService _auditTrail;

        public PersistentAlarmService(IAlarmRepository alarms, IAuditTrailService auditTrail)
        {
            _alarms = alarms;
            _auditTrail = auditTrail;
        }

        public IReadOnlyList<AlarmEvent> GetActiveAlarms()
        {
            return _alarms.GetActiveAsync().GetAwaiter().GetResult();
        }

        public IReadOnlyList<AlarmEvent> GetAlarmHistory()
        {
            return _alarms.GetAllAsync().GetAwaiter().GetResult();
        }

        public IReadOnlyList<AlarmEvent> QueryAlarms(AlarmQuery query)
        {
            return _alarms.QueryAsync(query).GetAwaiter().GetResult();
        }

        public AlarmEvent RaiseAlarm(AlarmEvent alarm)
        {
            ArgumentNullException.ThrowIfNull(alarm);

            if (string.IsNullOrWhiteSpace(alarm.AlarmId))
            {
                alarm.AlarmId = $"ALM-{DateTime.Now:yyyyMMddHHmmssfff}";
            }

            if (alarm.OccurredAt == default)
            {
                alarm.OccurredAt = DateTime.Now;
            }

            alarm.State = AlarmState.Active;
            _alarms.SaveAsync(alarm).GetAwaiter().GetResult();
            _auditTrail.RecordAlarmRaised(alarm);
            return alarm;
        }

        public void AcknowledgeAlarm(string alarmId, string acknowledgedBy)
        {
            var alarm = _alarms.GetByIdAsync(alarmId).GetAwaiter().GetResult();
            if (alarm is null)
            {
                return;
            }

            alarm.State = AlarmState.Acknowledged;
            alarm.AcknowledgedBy = acknowledgedBy;
            alarm.AcknowledgedAt = DateTime.Now;
            _alarms.SaveAsync(alarm).GetAwaiter().GetResult();
            _auditTrail.RecordAlarmAcknowledged(alarm, acknowledgedBy);
        }

        public void ClearAlarm(string alarmId, string? reason = null)
        {
            var alarm = _alarms.GetByIdAsync(alarmId).GetAwaiter().GetResult();
            if (alarm is null)
            {
                return;
            }

            alarm.State = AlarmState.Cleared;
            alarm.ClearReason = reason;
            alarm.ClearedAt = DateTime.Now;
            _alarms.SaveAsync(alarm).GetAwaiter().GetResult();
            _auditTrail.RecordAlarmCleared(alarm, reason);
        }
    }
}
