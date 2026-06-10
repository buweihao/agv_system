using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentAlarmService : IAlarmService
    {
        private readonly IAlarmRepository _alarms;

        public PersistentAlarmService(IAlarmRepository alarms)
        {
            _alarms = alarms;
        }

        public IReadOnlyList<AlarmEvent> GetActiveAlarms()
        {
            return _alarms.GetActiveAsync().GetAwaiter().GetResult();
        }

        public IReadOnlyList<AlarmEvent> GetAlarmHistory()
        {
            return _alarms.GetAllAsync().GetAwaiter().GetResult();
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
        }
    }
}
