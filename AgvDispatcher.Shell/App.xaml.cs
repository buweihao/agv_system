using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Infrastructure.Sqlite;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using System.IO;
using System.Windows;

namespace AgvDispatcher.Shell;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        // 确保在解析 MainWindow（及其引发的模块加载、ViewModel 构造）之前初始化数据库
        Container.Resolve<LocalPersistenceInitializer>().Initialize();
        
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        LocalPersistenceRegistration.RegisterLocalPersistence(containerRegistry);
    }

    protected override IModuleCatalog CreateModuleCatalog()
    {
        var modulePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules");
        if (!Directory.Exists(modulePath))
        {
            Directory.CreateDirectory(modulePath);
        }

        return new DirectoryModuleCatalog { ModulePath = modulePath };
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Container.Resolve<IOperationLogService>().WriteLog(new AgvDispatcher.Core.Models.OperationLog
        {
            Category = "System",
            Action = "Started",
            Message = "AGV dispatcher shell started.",
            Operator = Environment.UserName,
            SourceId = Environment.MachineName
        });

        // 恢复意外中断的任务
        Container.Resolve<ITaskRecoveryService>().RecoverInterruptedTasks();

        Container.Resolve<IVehicleAdapterManager>().StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        var regionManager = Container.Resolve<Prism.Navigation.Regions.IRegionManager>();
        var eventAggregator = Container.Resolve<Prism.Events.IEventAggregator>();

        regionManager.RequestNavigate("MainWorkspaceRegion", "MonitorLayoutView");
        eventAggregator.GetEvent<AgvDispatcher.Core.Events.NavigationTitleEvent>().Publish("运行监控");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Container.Resolve<IOperationLogService>().WriteLog(new AgvDispatcher.Core.Models.OperationLog
        {
            Category = "System",
            Action = "Stopped",
            Message = "AGV dispatcher shell stopped.",
            Operator = Environment.UserName,
            SourceId = Environment.MachineName
        });

        Container.Resolve<IVehicleAdapterManager>().StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        base.OnExit(e);
    }
}
