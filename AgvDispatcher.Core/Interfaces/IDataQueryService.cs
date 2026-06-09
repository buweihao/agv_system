using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IDataQueryService
    {
        IReadOnlyList<TaskRunRecord> GetTaskRunRecords();

        IReadOnlyList<ChargeRecord> GetChargeRecords();

        IReadOnlyList<AlarmRecord> GetAlarmRecords();

        IReadOnlyList<InteractionRecord> GetInteractionRecords();

        IReadOnlyList<EnergyRecord> GetEnergyRecords();

        IReadOnlyList<DeviceLogRecord> GetDeviceLogRecords();
    }
}
