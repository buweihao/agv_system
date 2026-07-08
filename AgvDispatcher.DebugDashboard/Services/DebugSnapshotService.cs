using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Interfaces;
using AgvDispatcher.Core.Contracts.Dispatching.Models;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Models;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Contracts.Traffic.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Mock.Simulation;
using AgvDispatcher.Infrastructure.Okapi;

namespace AgvDispatcher.DebugDashboard.Services;

public sealed class DebugSnapshotService : IDebugSnapshotService
{
    private readonly ITaskService _taskService;
    private readonly IVehicleService _vehicleService;
    private readonly ITrafficControlService _trafficControlService;
    private readonly IRouteReservationService _routeReservationService;
    private readonly IDispatchOrchestrationService _dispatchOrchestrationService;
    private readonly IOkapiProtocolTraceStore? _okapiProtocolTraceStore;
    private readonly IMockFleetSimulationEngine? _mockFleetSimulationEngine;

    public DebugSnapshotService(
        ITaskService taskService,
        IVehicleService vehicleService,
        ITrafficControlService trafficControlService,
        IRouteReservationService routeReservationService,
        IDispatchOrchestrationService dispatchOrchestrationService,
        IOkapiProtocolTraceStore? okapiProtocolTraceStore = null)
        : this(
            taskService,
            vehicleService,
            trafficControlService,
            routeReservationService,
            dispatchOrchestrationService,
            okapiProtocolTraceStore,
            null)
    {
    }

    public DebugSnapshotService(
        ITaskService taskService,
        IVehicleService vehicleService,
        ITrafficControlService trafficControlService,
        IRouteReservationService routeReservationService,
        IDispatchOrchestrationService dispatchOrchestrationService,
        IMockFleetSimulationEngine mockFleetSimulationEngine,
        IOkapiProtocolTraceStore? okapiProtocolTraceStore = null)
        : this(
            taskService,
            vehicleService,
            trafficControlService,
            routeReservationService,
            dispatchOrchestrationService,
            okapiProtocolTraceStore,
            mockFleetSimulationEngine)
    {
    }

    private DebugSnapshotService(
        ITaskService taskService,
        IVehicleService vehicleService,
        ITrafficControlService trafficControlService,
        IRouteReservationService routeReservationService,
        IDispatchOrchestrationService dispatchOrchestrationService,
        IOkapiProtocolTraceStore? okapiProtocolTraceStore,
        IMockFleetSimulationEngine? mockFleetSimulationEngine)
    {
        _taskService = taskService;
        _vehicleService = vehicleService;
        _trafficControlService = trafficControlService;
        _routeReservationService = routeReservationService;
        _dispatchOrchestrationService = dispatchOrchestrationService;
        _okapiProtocolTraceStore = okapiProtocolTraceStore;
        _mockFleetSimulationEngine = mockFleetSimulationEngine;
    }

    public async Task<DebugSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = new RequestContext { SourceModule = nameof(AgvDispatcher.DebugDashboard) };
        var diagnostics = new List<string>();

        var tasks = ReadSection("Tasks", _taskService.GetTasks, Array.Empty<TaskOrder>(), diagnostics);
        var vehicles = ReadSection("Vehicles", _vehicleService.GetVehicles, Array.Empty<Vehicle>(), diagnostics);
        var vehicleStatuses = ReadSection("Vehicle statuses", _vehicleService.GetVehicleStatuses, Array.Empty<VehicleStatus>(), diagnostics);
        var trafficResources = await ReadResultSectionAsync(
                "Traffic resources",
                () => _trafficControlService.GetTrafficSnapshotAsync(context, cancellationToken),
                result => result.Resources,
                Array.Empty<TrafficResourceStatusDto>(),
                diagnostics)
            .ConfigureAwait(false);
        var routeReservations = await ReadResultSectionAsync(
                "Route reservations",
                () => _routeReservationService.GetActiveReservationsAsync(
                    new GetRouteReservationsRequest { Context = context },
                    cancellationToken),
                result => result,
                Array.Empty<RouteReservationDto>(),
                diagnostics)
            .ConfigureAwait(false);
        var dispatchExecutions = await ReadResultSectionAsync(
                "Dispatch executions",
                () => _dispatchOrchestrationService.GetActiveExecutionsAsync(
                    new GetDispatchExecutionsRequest { Context = context },
                    cancellationToken),
                result => result,
                Array.Empty<DispatchExecutionDto>(),
                diagnostics)
            .ConfigureAwait(false);
        var okapiRecords = ReadSection(
            "Okapi protocol records",
            () => _okapiProtocolTraceStore?.GetLatest(200) ?? Array.Empty<OkapiProtocolTraceRecord>(),
            Array.Empty<OkapiProtocolTraceRecord>(),
            diagnostics);
        var mockSimulation = ReadMockSimulationSnapshot(diagnostics);

        return new DebugSnapshotDto
        {
            GeneratedAt = DateTimeOffset.Now,
            Tasks = tasks,
            Vehicles = vehicles,
            VehicleStatuses = vehicleStatuses,
            TrafficResources = trafficResources,
            RouteReservations = routeReservations,
            DispatchExecutions = dispatchExecutions,
            OkapiProtocolRecords = okapiRecords,
            MockSimulation = mockSimulation,
            DiagnosticMessages = diagnostics
        };
    }

    private MockSimulationSnapshotDto CreateMockSimulationSnapshot()
    {
        if (_mockFleetSimulationEngine is null)
        {
            return new MockSimulationSnapshotDto();
        }

        var scenario = _mockFleetSimulationEngine.CurrentScenario;
        return new MockSimulationSnapshotDto
        {
            ScenarioId = scenario?.ScenarioId ?? string.Empty,
            Name = scenario?.Name ?? string.Empty,
            Tick = _mockFleetSimulationEngine.CurrentTick,
            Options = scenario is null ? string.Empty : FormatOptions(scenario.Options),
            Vehicles = _mockFleetSimulationEngine.GetVehicleStates(),
            RecentEvents = _mockFleetSimulationEngine.GetRecentEvents(100)
        };
    }

    private static string FormatOptions(MockSimulationOptions options) =>
        $"RollingWindowSize={options.RollingWindowSize}; " +
        $"WaitTimeout={options.WaitTimeout:g}; " +
        $"RetryInterval={options.RetryInterval:g}; " +
        $"MaxRetryCount={options.MaxRetryCount}; " +
        $"OnTimeoutPolicy={options.OnTimeoutPolicy}; " +
        $"ReplanOnLockedResource={options.ReplanOnLockedResource}; " +
        $"ReplanOnBlockedResource={options.ReplanOnBlockedResource}; " +
        $"PreferWaitingOverReplan={options.PreferWaitingOverReplan}; " +
        $"MaxReplanCount={options.MaxReplanCount}; " +
        $"AutoStartTasks={options.AutoStartTasks}";

    private MockSimulationSnapshotDto ReadMockSimulationSnapshot(ICollection<string> diagnostics)
    {
        try
        {
            return CreateMockSimulationSnapshot();
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Mock simulation: {ex.Message}");
            return new MockSimulationSnapshotDto();
        }
    }

    private static IReadOnlyList<T> ReadSection<T>(
        string name,
        Func<IReadOnlyList<T>> read,
        IReadOnlyList<T> fallback,
        ICollection<string> diagnostics)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            diagnostics.Add($"{name}: {ex.Message}");
            return fallback;
        }
    }

    private static async Task<IReadOnlyList<T>> ReadResultSectionAsync<TResult, T>(
        string name,
        Func<Task<AgvResult<TResult>>> read,
        Func<TResult, IReadOnlyList<T>> select,
        IReadOnlyList<T> fallback,
        ICollection<string> diagnostics)
    {
        try
        {
            var result = await read().ConfigureAwait(false);
            if (result.Success && result.Data is not null)
            {
                return select(result.Data);
            }

            diagnostics.Add($"{name}: {result.Message}");
        }
        catch (Exception ex)
        {
            diagnostics.Add($"{name}: {ex.Message}");
        }

        return fallback;
    }
}
