using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IAlarmService
    {
        IReadOnlyList<AlarmEvent> GetActiveAlarms();

        IReadOnlyList<AlarmEvent> GetAlarmHistory();

        IReadOnlyList<AlarmEvent> QueryAlarms(AlarmQuery query);

        AlarmEvent RaiseAlarm(AlarmEvent alarm);

        void AcknowledgeAlarm(string alarmId, string acknowledgedBy);

        void ClearAlarm(string alarmId, string? reason = null);
    }
}
