using System.Collections.ObjectModel;
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

        public DataQueryCenterPanelViewModel()
        {
            // 10 mock data rows
            DataList.Add(new DataQueryModel { Seq = 1, QueryTime = "2024-01-12 10:20:30", AgvId = "AGV-001", TaskId = "TSK-001201", TaskType = "搬运任务", StartPoint = "A01-01", EndPoint = "C03-05", Status = "已完成", Duration = "00:15:20", Distance = "1.2", AvgSpeed = "1.3" });
            DataList.Add(new DataQueryModel { Seq = 2, QueryTime = "2024-01-12 10:25:10", AgvId = "AGV-002", TaskId = "TSK-001202", TaskType = "充电任务", StartPoint = "B02-01", EndPoint = "CHG-01", Status = "执行中", Duration = "00:05:10", Distance = "0.5", AvgSpeed = "1.6" });
            DataList.Add(new DataQueryModel { Seq = 3, QueryTime = "2024-01-12 10:30:00", AgvId = "AGV-003", TaskId = "TSK-001203", TaskType = "巡检任务", StartPoint = "C01-01", EndPoint = "A01-01", Status = "已完成", Duration = "00:45:00", Distance = "3.5", AvgSpeed = "1.3" });
            DataList.Add(new DataQueryModel { Seq = 4, QueryTime = "2024-01-12 10:45:30", AgvId = "AGV-001", TaskId = "TSK-001204", TaskType = "搬运任务", StartPoint = "C03-05", EndPoint = "B01-02", Status = "已完成", Duration = "00:12:40", Distance = "1.0", AvgSpeed = "1.3" });
            DataList.Add(new DataQueryModel { Seq = 5, QueryTime = "2024-01-12 11:00:20", AgvId = "AGV-004", TaskId = "TSK-001205", TaskType = "其他任务", StartPoint = "D01-01", EndPoint = "D01-10", Status = "执行中", Duration = "00:08:15", Distance = "0.8", AvgSpeed = "1.6" });
            DataList.Add(new DataQueryModel { Seq = 6, QueryTime = "2024-01-12 11:15:00", AgvId = "AGV-002", TaskId = "TSK-001206", TaskType = "搬运任务", StartPoint = "CHG-01", EndPoint = "A02-05", Status = "已完成", Duration = "00:18:30", Distance = "1.5", AvgSpeed = "1.35" });
            DataList.Add(new DataQueryModel { Seq = 7, QueryTime = "2024-01-12 11:30:45", AgvId = "AGV-005", TaskId = "TSK-001207", TaskType = "巡检任务", StartPoint = "A01-05", EndPoint = "C02-05", Status = "执行中", Duration = "00:25:10", Distance = "2.0", AvgSpeed = "1.32" });
            DataList.Add(new DataQueryModel { Seq = 8, QueryTime = "2024-01-12 11:45:10", AgvId = "AGV-001", TaskId = "TSK-001208", TaskType = "充电任务", StartPoint = "B01-02", EndPoint = "CHG-02", Status = "已完成", Duration = "00:06:50", Distance = "0.6", AvgSpeed = "1.46" });
            DataList.Add(new DataQueryModel { Seq = 9, QueryTime = "2024-01-12 12:00:30", AgvId = "AGV-003", TaskId = "TSK-001209", TaskType = "搬运任务", StartPoint = "A01-01", EndPoint = "D02-05", Status = "已完成", Duration = "00:22:15", Distance = "1.8", AvgSpeed = "1.34" });
            DataList.Add(new DataQueryModel { Seq = 10, QueryTime = "2024-01-12 12:15:00", AgvId = "AGV-004", TaskId = "TSK-001210", TaskType = "搬运任务", StartPoint = "D01-10", EndPoint = "C01-05", Status = "执行中", Duration = "00:10:45", Distance = "0.9", AvgSpeed = "1.39" });
        }
    }
}
