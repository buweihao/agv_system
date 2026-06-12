using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.DataQueryModule.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.DataQueryModule.ViewModels
{
    public class DataQueryCenterPanelViewModel : BindableBase
    {
        private ObservableCollection<DataQueryModel> _dataList = new();
        public ObservableCollection<DataQueryModel> DataList
        {
            get => _dataList;
            set => SetProperty(ref _dataList, value);
        }

        private readonly IDataQueryService _dataQueryService;

        public DelegateCommand RefreshCommand { get; }

        public DataQueryCenterPanelViewModel(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
            RefreshCommand = new DelegateCommand(LoadData);
            LoadData();
        }

        private void LoadData()
        {
            DataList.Clear();
            var records = _dataQueryService.GetTaskRunRecords();
            foreach (var record in records)
            {
                DataList.Add(ToDataQueryModel(record));
            }
        }

        private static DataQueryModel ToDataQueryModel(TaskRunRecord record)
        {
            return new DataQueryModel
            {
                Seq = record.Seq,
                QueryTime = record.QueryTime,
                AgvId = record.AgvId,
                TaskId = record.TaskId,
                TaskType = record.TaskType,
                StartPoint = record.StartPoint,
                EndPoint = record.EndPoint,
                Status = record.Status,
                Duration = record.Duration,
                Distance = record.Distance,
                AvgSpeed = record.AvgSpeed
            };
        }
    }
}
