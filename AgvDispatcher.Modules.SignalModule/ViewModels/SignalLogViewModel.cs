using System.Collections.ObjectModel;
using AgvDispatcher.Modules.SignalModule.Models;
using AgvDispatcher.Modules.SignalModule.Services;
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

        public SignalLogViewModel()
        {
            SignalLowBatteryLogStore.EnsureSeeded();
            LogList = SignalLowBatteryLogStore.Logs;
        }
    }
}
