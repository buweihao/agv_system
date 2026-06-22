using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using AgvDispatcher.Core.Events;
using System.ComponentModel;
using System.Windows.Data;

namespace AgvDispatcher.Modules.SystemConfigModule.ViewModels
{
    public class SystemParameterConfigViewModel : BindableBase
    {
        private readonly ISystemParameterRepository _parameterRepository;
        private readonly IEventAggregator _eventAggregator;
        private ParameterConfig? _selectedParameter;
        private EditableParameterConfig _currentParameter = new();
        private string _statusMessage = "请选择参数";
        private string _filterRestart = "全部";

        public ObservableCollection<ParameterConfig> Parameters { get; } = new();
        public ICollectionView ParametersView { get; }

        public ObservableCollection<string> RestartOptions { get; } = new() { "全部", "重启生效", "热加载" };

        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand SaveCommand { get; }

        public string FilterRestart
        {
            get => _filterRestart;
            set { if (SetProperty(ref _filterRestart, value)) ParametersView.Refresh(); }
        }

        public ParameterConfig? SelectedParameter
        {
            get => _selectedParameter;
            set
            {
                if (SetProperty(ref _selectedParameter, value) && value is not null)
                {
                    CurrentParameter = EditableParameterConfig.FromConfig(value);
                    StatusMessage = $"正在编辑 {value.ParamName}";
                }
            }
        }

        public EditableParameterConfig CurrentParameter
        {
            get => _currentParameter;
            set => SetProperty(ref _currentParameter, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int TotalCount => Parameters.Count;

        public SystemParameterConfigViewModel(ISystemParameterRepository parameterRepository, IEventAggregator eventAggregator)
        {
            _parameterRepository = parameterRepository;
            _eventAggregator = eventAggregator;

            ParametersView = CollectionViewSource.GetDefaultView(Parameters);
            ParametersView.Filter = FilterParameter;

            RefreshCommand = new DelegateCommand(LoadParameters);
            SaveCommand = new DelegateCommand(SaveParameter);

            LoadParameters();
        }

        private void LoadParameters()
        {
            Parameters.Clear();
            var items = _parameterRepository.GetAllAsync().GetAwaiter().GetResult();
            foreach (var item in items)
            {
                Parameters.Add(item);
            }
            ParametersView.Refresh();
            RaisePropertyChanged(nameof(TotalCount));

            if (Parameters.Count > 0)
            {
                SelectedParameter = Parameters[0];
            }
        }

        private void SaveParameter()
        {
            if (string.IsNullOrWhiteSpace(CurrentParameter.ParamKey))
            {
                StatusMessage = "参数键不能为空";
                return;
            }

            var parameter = CurrentParameter.ToConfig();
            if (string.Equals(
                    parameter.ParamKey,
                    "Dispatching:TrafficReservationMode",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(parameter.ParamValue, "InMemory", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(parameter.ParamValue, "Persistent", StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = "交通预约存储模式只能设置为 InMemory 或 Persistent";
                    return;
                }

                parameter.ParamValue = string.Equals(
                    parameter.ParamValue,
                    "Persistent",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Persistent"
                    : "InMemory";
                parameter.RequiresRestart = true;
            }

            _parameterRepository.SaveAsync(parameter).GetAwaiter().GetResult();
            
            _eventAggregator.GetEvent<SystemParameterChangedEvent>().Publish(parameter);

            StatusMessage = $"{parameter.ParamName} 已保存" + (parameter.RequiresRestart ? " (需重启系统生效)" : " (已热生效)");
            LoadParameters();
            SelectedParameter = Parameters.FirstOrDefault(item => item.ParamKey == parameter.ParamKey);
        }

        private bool FilterParameter(object obj)
        {
            if (obj is not ParameterConfig p) return false;

            if (FilterRestart == "重启生效" && !p.RequiresRestart)
                return false;
            if (FilterRestart == "热加载" && p.RequiresRestart)
                return false;

            return true;
        }
    }

    public class EditableParameterConfig : BindableBase
    {
        private string _paramKey = string.Empty;
        private string _paramName = string.Empty;
        private string _paramValue = string.Empty;
        private string _dataType = string.Empty;
        private string _description = string.Empty;
        private bool _requiresRestart;

        public string ParamKey { get => _paramKey; set => SetProperty(ref _paramKey, value); }
        public string ParamName { get => _paramName; set => SetProperty(ref _paramName, value); }
        public string ParamValue { get => _paramValue; set => SetProperty(ref _paramValue, value); }
        public string DataType { get => _dataType; set => SetProperty(ref _dataType, value); }
        public string Description { get => _description; set => SetProperty(ref _description, value); }
        public bool RequiresRestart { get => _requiresRestart; set => SetProperty(ref _requiresRestart, value); }

        public static EditableParameterConfig FromConfig(ParameterConfig config)
        {
            return new EditableParameterConfig
            {
                ParamKey = config.ParamKey,
                ParamName = config.ParamName,
                ParamValue = config.ParamValue,
                DataType = config.DataType,
                Description = config.Description,
                RequiresRestart = config.RequiresRestart
            };
        }

        public ParameterConfig ToConfig()
        {
            return new ParameterConfig
            {
                ParamKey = ParamKey.Trim(),
                ParamName = ParamName.Trim(),
                ParamValue = ParamValue.Trim(),
                DataType = DataType.Trim(),
                Description = Description.Trim(),
                RequiresRestart = RequiresRestart
            };
        }
    }
}
