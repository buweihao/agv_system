using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface ISignalService
    {
        IReadOnlyList<SignalPoint> GetSignals();

        IReadOnlyList<SignalLogEntry> GetSignalLogs();

        void AddSignalLog(SignalLogEntry log);
    }
}
