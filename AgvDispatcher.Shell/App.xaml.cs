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
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        LocalPersistenceRegistration.RegisterLocalPersistence(containerRegistry);
    }

    protected override IModuleCatalog CreateModuleCatalog()
    {
        var modulePath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Modules");
        if (!Directory.Exists(modulePath))
        {
            Directory.CreateDirectory(modulePath);
        }
        return new DirectoryModuleCatalog() { ModulePath = modulePath };
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Container.Resolve<LocalPersistenceInitializer>().Initialize();

        var regionManager = Container.Resolve<Prism.Navigation.Regions.IRegionManager>();
        var eventAggregator = Container.Resolve<Prism.Events.IEventAggregator>();

        // 默认导航到运行监控
        regionManager.RequestNavigate("MainWorkspaceRegion", "MonitorLayoutView");
        eventAggregator.GetEvent<AgvDispatcher.Core.Events.NavigationTitleEvent>().Publish("运行监控");
    }
}
