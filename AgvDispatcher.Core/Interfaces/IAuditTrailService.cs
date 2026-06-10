using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IAuditTrailService
    {
        void Record(OperationLog log);

        void RecordVehicleStatusUpdated(VehicleStatusSnapshot snapshot);

        void RecordDispatchResult(DispatchResult result, string action);

        void RecordAlarmRaised(AlarmEvent alarm);

        void RecordAlarmAcknowledged(AlarmEvent alarm, string acknowledgedBy);

        void RecordAlarmCleared(AlarmEvent alarm, string? reason);
    }
}
