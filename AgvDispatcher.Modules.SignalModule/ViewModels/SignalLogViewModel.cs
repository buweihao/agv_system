using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.SignalModule.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.SignalModule.ViewModels
{
    public class SignalLogViewModel : BindableBase
    {
        private ObservableCollection<SignalLogModel> _logList = new();

        public ObservableCollection<SignalLogModel> LogList
        {
            get => _logList;
            set => SetProperty(ref _logList, value);
        }

        public SignalLogViewModel(ISignalService signalService)
        {
            LogList = new ObservableCollection<SignalLogModel>(
                signalService.GetSignalLogs().Select(ToSignalLogModel));
        }

        private static SignalLogModel ToSignalLogModel(SignalLogEntry log)
        {
            return new SignalLogModel
            {
                LogTime = log.LogTime,
                SignalName = log.SignalName,
                SignalId = log.SignalId,
                SignalType = log.SignalType,
                StateChange = log.StateChange,
                TriggerValue = log.TriggerValue,
                Device = log.Device,
                ResponseTime = log.ResponseTime,
                Result = log.Result
            };
        }
    }
}
