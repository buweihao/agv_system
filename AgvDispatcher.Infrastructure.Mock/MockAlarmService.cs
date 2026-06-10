using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockAlarmService : IAlarmService
    {
        private readonly List<AlarmEvent> _alarms = MockData.CreateAlarms(DateTime.Now).ToList();

        public IReadOnlyList<AlarmEvent> GetActiveAlarms()
        {
            return _alarms.Where(alarm => alarm.State == AlarmState.Active).ToArray();
        }

        public IReadOnlyList<AlarmEvent> GetAlarmHistory()
        {
            return _alarms.OrderByDescending(alarm => alarm.OccurredAt).ToArray();
        }

        public IReadOnlyList<AlarmEvent> QueryAlarms(AlarmQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var alarms = _alarms.AsEnumerable();

            if (query.Severity is not null)
            {
                alarms = alarms.Where(alarm => alarm.Severity == query.Severity.Value);
            }

            if (query.State is not null)
            {
                alarms = alarms.Where(alarm => alarm.State == query.State.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.AlarmCode))
            {
                alarms = alarms.Where(alarm => alarm.AlarmCode == query.AlarmCode);
            }

            if (!string.IsNullOrWhiteSpace(query.SourceType))
            {
                alarms = alarms.Where(alarm => alarm.SourceType == query.SourceType);
            }

            if (!string.IsNullOrWhiteSpace(query.SourceId))
            {
                alarms = alarms.Where(alarm => alarm.SourceId == query.SourceId);
            }

            if (!string.IsNullOrWhiteSpace(query.VehicleId))
            {
                alarms = alarms.Where(alarm => alarm.VehicleId == query.VehicleId);
            }

            if (!string.IsNullOrWhiteSpace(query.TaskId))
            {
                alarms = alarms.Where(alarm => alarm.TaskId == query.TaskId);
            }

            if (query.From is not null)
            {
                alarms = alarms.Where(alarm => alarm.OccurredAt >= query.From.Value);
            }

            if (query.To is not null)
            {
                alarms = alarms.Where(alarm => alarm.OccurredAt <= query.To.Value);
            }

            return alarms
                .OrderByDescending(alarm => alarm.OccurredAt)
                .Skip(Math.Max(query.Skip, 0))
                .Take(query.Take <= 0 ? 100 : query.Take)
                .ToArray();
        }

        public AlarmEvent RaiseAlarm(AlarmEvent alarm)
        {
            ArgumentNullException.ThrowIfNull(alarm);

            if (string.IsNullOrWhiteSpace(alarm.AlarmId))
            {
                alarm.AlarmId = $"ALM-{DateTime.Now:HHmmssfff}";
            }

            if (alarm.OccurredAt == default)
            {
                alarm.OccurredAt = DateTime.Now;
            }

            alarm.State = AlarmState.Active;
            _alarms.Insert(0, alarm);
            return alarm;
        }

        public void AcknowledgeAlarm(string alarmId, string acknowledgedBy)
        {
            var alarm = FindAlarm(alarmId);
            if (alarm is null)
            {
                return;
            }

            alarm.State = AlarmState.Acknowledged;
            alarm.AcknowledgedBy = acknowledgedBy;
            alarm.AcknowledgedAt = DateTime.Now;
        }

        public void ClearAlarm(string alarmId, string? reason = null)
        {
            var alarm = FindAlarm(alarmId);
            if (alarm is null)
            {
                return;
            }

            alarm.State = AlarmState.Cleared;
            alarm.ClearReason = reason;
            alarm.ClearedAt = DateTime.Now;
        }

        private AlarmEvent? FindAlarm(string alarmId)
        {
            return _alarms.FirstOrDefault(alarm =>
                string.Equals(alarm.AlarmId, alarmId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
