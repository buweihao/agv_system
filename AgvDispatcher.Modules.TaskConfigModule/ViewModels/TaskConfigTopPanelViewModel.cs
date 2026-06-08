using System.Collections.ObjectModel;
using AgvDispatcher.Modules.TaskConfigModule.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskConfigModule.ViewModels
{
    public class TaskConfigTopPanelViewModel : BindableBase
    {
        private ObservableCollection<TaskTemplateModel> _templateList = new();
        public ObservableCollection<TaskTemplateModel> TemplateList
        {
            get => _templateList;
            set => SetProperty(ref _templateList, value);
        }

        public TaskConfigTopPanelViewModel()
        {
            // 6 mock data rows
            TemplateList.Add(new TaskTemplateModel { TemplateName = "产线物料搬运", TemplateType = "搬运任务", Scene = "车间内物流", Priority = "高", Description = "用于生产线之间的物料自动转运", IsEnabled = true, UpdateTime = "2024-01-12 10:20" });
            TemplateList.Add(new TaskTemplateModel { TemplateName = "成品入库搬运", TemplateType = "搬运任务", Scene = "仓储物流", Priority = "中", Description = "成品打包后的入库转运", IsEnabled = true, UpdateTime = "2024-01-11 15:30" });
            TemplateList.Add(new TaskTemplateModel { TemplateName = "夜间自动巡检", TemplateType = "巡检任务", Scene = "厂区安防", Priority = "低", Description = "夜间全厂区的自动安防巡检", IsEnabled = false, UpdateTime = "2024-01-10 09:15" });
            TemplateList.Add(new TaskTemplateModel { TemplateName = "设备异常处理", TemplateType = "其他任务", Scene = "设备维护", Priority = "高", Description = "当设备发出异常告警时前往确认", IsEnabled = true, UpdateTime = "2024-01-09 14:00" });
            TemplateList.Add(new TaskTemplateModel { TemplateName = "原料自动补货", TemplateType = "物料任务", Scene = "车间内物流", Priority = "中", Description = "根据线边仓库存自动触发补料", IsEnabled = true, UpdateTime = "2024-01-08 11:45" });
            TemplateList.Add(new TaskTemplateModel { TemplateName = "电量过低回充", TemplateType = "充电任务", Scene = "系统任务", Priority = "高", Description = "AGV电量低于20%自动回充电桩", IsEnabled = true, UpdateTime = "2024-01-05 16:20" });
        }
    }
}
