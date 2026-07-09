using AgvDispatcher.DebugDashboard;
using AgvDispatcher.DebugDashboard.Views;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Infrastructure.Sqlite.DependencyInjection;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace AgvDispatcher.Shell;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : PrismApplication
{
    private readonly DebugDashboardOptions _debugDashboardOptions = LoadDebugDashboardOptions();

    protected override Window CreateShell()
    {
        // 确保在解析 MainWindow（及其引发的模块加载、ViewModel 构造）之前初始化数据库
        Container.Resolve<LocalPersistenceInitializer>().Initialize();
        
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        LocalPersistenceRegistration.RegisterLocalPersistence(containerRegistry);
        DebugDashboardRegistration.RegisterDebugDashboard(containerRegistry, _debugDashboardOptions);
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

        if (_debugDashboardOptions.EnableMockSimulationDashboard)
        {
            var debugDashboardWindow = Container.Resolve<DebugDashboardWindow>();
            debugDashboardWindow.Owner = Current.MainWindow;
            debugDashboardWindow.Show();
        }
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

    private static DebugDashboardOptions LoadDebugDashboardOptions()
    {
        var configured = ReadConfiguredSwitch();
        if (configured.HasValue)
        {
            return new DebugDashboardOptions
            {
                EnableMockSimulationDashboard = configured.Value
            };
        }

        return new DebugDashboardOptions
        {
            EnableMockSimulationDashboard = IsDebugBuild() || IsDevelopmentProfile()
        };
    }

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    private static bool IsDevelopmentProfile()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return environment is not null &&
            (environment.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
             environment.Equals("Dev", StringComparison.OrdinalIgnoreCase) ||
             environment.Equals("Debug", StringComparison.OrdinalIgnoreCase));
    }

    private static bool? ReadConfiguredSwitch()
    {
        var environmentValue = Environment.GetEnvironmentVariable("AGV_ENABLE_MOCK_SIMULATION_DASHBOARD");
        if (TryParseBoolean(environmentValue, out var enabled))
        {
            return enabled;
        }

        environmentValue = Environment.GetEnvironmentVariable("EnableMockSimulationDashboard");
        if (TryParseBoolean(environmentValue, out enabled))
        {
            return enabled;
        }

        return ReadConfiguredSwitchFromJson($"appsettings.{GetEnvironmentName()}.json") ??
            ReadConfiguredSwitchFromJson("appsettings.json");
    }

    private static string GetEnvironmentName() =>
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
        string.Empty;

    private static bool? ReadConfiguredSwitchFromJson(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (TryReadBoolean(root, "EnableMockSimulationDashboard", out var enabled))
        {
            return enabled;
        }

        if (root.TryGetProperty("DebugDashboard", out var debugDashboard) &&
            TryReadBoolean(debugDashboard, "EnableMockSimulationDashboard", out enabled))
        {
            return enabled;
        }

        return null;
    }

    private static bool TryReadBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        return property.ValueKind == JsonValueKind.String &&
            TryParseBoolean(property.GetString(), out value);
    }

    private static bool TryParseBoolean(string? value, out bool result)
    {
        if (bool.TryParse(value, out result))
        {
            return true;
        }

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }
}
