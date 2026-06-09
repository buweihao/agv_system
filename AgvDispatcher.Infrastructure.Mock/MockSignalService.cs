using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Events;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockSignalService : ISignalService
    {
        private readonly List<SignalPoint> _signals = MockData.CreateSignals(DateTime.Now).ToList();
        private readonly List<SignalLogEntry> _logs = MockData.CreateSignalLogs(DateTime.Now).ToList();

        public MockSignalService(IEventAggregator eventAggregator)
        {
            eventAggregator
                .GetEvent<RobotLowBatteryEvent>()
                .Subscribe(AddLowBatteryLog, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);
        }

        public IReadOnlyList<SignalPoint> GetSignals()
        {
            return _signals.ToArray();
        }

        public IReadOnlyList<SignalLogEntry> GetSignalLogs()
        {
            return _logs.ToArray();
        }

        public void AddSignalLog(SignalLogEntry log)
        {
            ArgumentNullException.ThrowIfNull(log);
            _logs.Insert(0, log);
        }

        private void AddLowBatteryLog(RobotBatteryAlert alert)
        {
            AddSignalLog(new SignalLogEntry
            {
                LogTime = alert.OccurredAt == default ? DateTime.Now.ToString("HH:mm:ss.fff") : alert.OccurredAt.ToString("HH:mm:ss.fff"),
                SignalName = "AGV低电量联动",
                SignalId = $"LOW_BATTERY_{alert.VehicleId}",
                SignalType = "联动事件",
                StateChange = "正常 -> 告警",
                TriggerValue = $"{alert.BatteryLevel:0.#}% <= {alert.Threshold:0.#}%",
                Device = string.IsNullOrWhiteSpace(alert.VehicleId) ? alert.Brand : alert.VehicleId,
                ResponseTime = "0ms",
                Result = "已记录"
            });
        }
    }
}
