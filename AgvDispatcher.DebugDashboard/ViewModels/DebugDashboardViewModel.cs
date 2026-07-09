using System.Collections.ObjectModel;
using System.ComponentModel;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;
using AgvDispatcher.Core.Contracts.Dispatching.Models;
using AgvDispatcher.Core.Contracts.Reservations.Models;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;
using AgvDispatcher.DebugDashboard.Services;
using AgvDispatcher.Infrastructure.Mock.Simulation;
using AgvDispatcher.Infrastructure.Okapi;
using Prism.Commands;
using Prism.Mvvm;
using System.Windows.Data;
using System.Windows.Threading;

namespace AgvDispatcher.DebugDashboard.ViewModels;

public sealed class DebugDashboardViewModel : BindableBase, IDisposable
{
    private readonly IDebugSnapshotService _debugSnapshotService;
    private readonly IMockVehicleWindowService _mockVehicleWindowService;
    private readonly IMockScenarioController _mockScenarioController;
    private readonly IMockFleetSimulationEngine _mockFleetSimulationEngine;
    private readonly DispatcherTimer _timer;
    private DebugSnapshotDto _snapshot = new();
    private bool _isRefreshing;
    private bool _isAutoRefreshEnabled = true;
    private int _selectedRefreshIntervalSeconds = 2;
    private string _searchText = string.Empty;
    private string _statusMessage = "Ready";
    private DateTimeOffset? _lastRefreshTime;
    private TaskRow? _selectedTask;
    private RouteReservationRow? _selectedRouteReservation;
    private OkapiProtocolTraceRecord? _selectedOkapiRecord;
    private MockScenarioOption? _selectedScenario;
    private string _scenarioName = string.Empty;
    private string _scenarioVehicleAId = string.Empty;
    private string _scenarioVehicleBId = string.Empty;
    private string _scenarioTaskAId = string.Empty;
    private string _scenarioTaskBId = string.Empty;
    private string _scenarioRouteSummary = string.Empty;
    private string _scenarioManualBlockResource = string.Empty;
    private string _mockSimulationScenarioId = string.Empty;
    private string _mockSimulationName = string.Empty;
    private long _mockSimulationTick;
    private string _mockSimulationOptions = string.Empty;
    private MockSimulationScenarioDefinition? _selectedMockSimulationScenario;
    private MockSimulationVehicleRow? _selectedMockSimulationVehicle;
    private MockFaultPolicy _selectedMockFaultPolicy = MockFaultPolicy.HoldResources;
    private string _mockMapEdgeId = string.Empty;
    private TrafficResourceType _selectedMockTrafficResourceType = TrafficResourceType.Edge;
    private string _mockTrafficResourceId = string.Empty;
    private bool _mockReplanOnLockedResource;
    private bool _mockReplanOnBlockedResource = true;
    private bool _mockPreferWaitingOverReplan = true;
    private int _mockMaxReplanCount = 3;
    private double _mockWaitTimeoutSeconds = 10;
    private double _mockRetryIntervalSeconds = 1;
    private int _mockMaxRetryCount = 3;
    private string _mockSimulationEditableScenarioId = string.Empty;
    private string _selectedMockEventVehicleFilter = "All";
    private string _selectedMockEventTypeFilter = "All";
    private MockSimulationEventRow? _selectedMockSimulationEvent;
    private CancellationTokenSource? _mockSimulationRunCancellation;
    private bool _isMockSimulationRunActive;
    private bool _isMockSimulationRegressionActive;
    private bool _isScenarioManualBlockActive;

    public DebugDashboardViewModel(
        IDebugSnapshotService debugSnapshotService,
        IMockVehicleWindowService mockVehicleWindowService,
        IMockScenarioController mockScenarioController,
        IMockFleetSimulationEngine mockFleetSimulationEngine)
    {
        _debugSnapshotService = debugSnapshotService;
        _mockVehicleWindowService = mockVehicleWindowService;
        _mockScenarioController = mockScenarioController;
        _mockFleetSimulationEngine = mockFleetSimulationEngine;
        RefreshCommand = new DelegateCommand(async () => await RefreshAsync().ConfigureAwait(true), () => !IsRefreshing);
        OpenVehicleWindowCommand = new DelegateCommand<VehicleRow?>(OpenVehicleWindow);
        LoadMockSimulationScenarioCommand = new DelegateCommand(
            async () => await LoadMockSimulationScenarioAsync().ConfigureAwait(true),
            () => !IsRefreshing && !IsMockSimulationRunActive && !IsMockSimulationRegressionActive &&
                  SelectedMockSimulationScenario is not null);
        StartAllMockSimulationTasksCommand = new DelegateCommand(
            async () => await StartAllMockSimulationTasksAsync().ConfigureAwait(true),
            () => CanRunMockSimulationCommand());
        StartSelectedMockSimulationTaskCommand = new DelegateCommand(
            async () => await StartSelectedMockSimulationTaskAsync().ConfigureAwait(true),
            () => CanRunMockSimulationCommand() &&
                  SelectedMockSimulationVehicle is not null &&
                  !string.IsNullOrWhiteSpace(SelectedMockSimulationVehicle.CurrentTaskId));
        StepMockSimulationCommand = new DelegateCommand(
            async () => await StepMockSimulationAsync().ConfigureAwait(true),
            () => CanRunMockSimulationCommand());
        RunTenMockSimulationTicksCommand = new DelegateCommand(
            async () => await RunMockSimulationTicksAsync(10).ConfigureAwait(true),
            () => CanRunMockSimulationCommand());
        RunMockSimulationUntilIdleCommand = new DelegateCommand(
            async () => await RunMockSimulationUntilIdleAsync(200).ConfigureAwait(true),
            () => CanRunMockSimulationCommand());
        StopMockSimulationRunCommand = new DelegateCommand(
            StopMockSimulationRun,
            () => IsMockSimulationRunActive);
        CancelSelectedMockSimulationTaskCommand = new DelegateCommand(
            async () => await CancelSelectedMockSimulationTaskAsync().ConfigureAwait(true),
            () => CanRunSelectedMockVehicleCommand() &&
                  !string.IsNullOrWhiteSpace(SelectedMockSimulationVehicle?.CurrentTaskId));
        InjectMockSimulationFaultCommand = new DelegateCommand(
            async () => await InjectMockSimulationFaultAsync().ConfigureAwait(true),
            () => CanRunSelectedMockVehicleCommand());
        RecoverMockSimulationVehicleCommand = new DelegateCommand(
            async () => await RecoverMockSimulationVehicleAsync().ConfigureAwait(true),
            () => CanRunSelectedMockVehicleCommand());
        DisableMockMapEdgeCommand = new DelegateCommand(
            async () => await SetMockMapEdgeEnabledAsync(false).ConfigureAwait(true),
            () => CanRunMockSimulationCommand() && !string.IsNullOrWhiteSpace(MockMapEdgeId));
        EnableMockMapEdgeCommand = new DelegateCommand(
            async () => await SetMockMapEdgeEnabledAsync(true).ConfigureAwait(true),
            () => CanRunMockSimulationCommand() && !string.IsNullOrWhiteSpace(MockMapEdgeId));
        BlockMockTrafficResourceCommand = new DelegateCommand(
            async () => await SetMockTrafficResourceBlockedAsync(true).ConfigureAwait(true),
            () => CanRunMockSimulationCommand() && !string.IsNullOrWhiteSpace(MockTrafficResourceId));
        UnblockMockTrafficResourceCommand = new DelegateCommand(
            async () => await SetMockTrafficResourceBlockedAsync(false).ConfigureAwait(true),
            () => CanRunMockSimulationCommand() && !string.IsNullOrWhiteSpace(MockTrafficResourceId));
        ApplyMockSimulationOptionsCommand = new DelegateCommand(
            async () => await ApplyMockSimulationOptionsAsync().ConfigureAwait(true),
            () => CanRunMockSimulationCommand());
        RunMockEightScenarioRegressionCommand = new DelegateCommand(
            async () => await RunMockEightScenarioRegressionAsync().ConfigureAwait(true),
            () => CanRunMockRegressionCommand());
        RunMockReplanRegressionCommand = new DelegateCommand(
            async () => await RunMockReplanRegressionAsync().ConfigureAwait(true),
            () => CanRunMockRegressionCommand());
        InitializeScenarioCommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.InitializeScenarioAsync(SelectedScenario?.Key ?? string.Empty)).ConfigureAwait(true));
        StartVehicleACommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.StartVehicleAsync(MockScenarioVehicleSlot.VehicleA)).ConfigureAwait(true));
        StartVehicleBCommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.StartVehicleAsync(MockScenarioVehicleSlot.VehicleB)).ConfigureAwait(true));
        VehicleAArriveNextNodeCommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.ArriveNextNodeAsync(ScenarioVehicleAId)).ConfigureAwait(true));
        VehicleBRetryWaitingTaskCommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.RetryWaitingTaskAsync(ScenarioVehicleBId)).ConfigureAwait(true));
        ManualBlockScenarioCommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.BlockScenarioResourceAsync()).ConfigureAwait(true));
        ManualUnblockScenarioCommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.UnblockScenarioResourceAsync()).ConfigureAwait(true));
        ResetScenarioCommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.ResetScenarioAsync()).ConfigureAwait(true));
        ScenarioOptions = _mockScenarioController.GetScenarios();
        _selectedScenario = ScenarioOptions.FirstOrDefault();
        MockSimulationScenarioOptions = MockSimulationScenarioCatalog.GetDefinitions();
        _selectedMockSimulationScenario = MockSimulationScenarioOptions.FirstOrDefault();
        MockFaultPolicies = Enum.GetValues<MockFaultPolicy>();
        MockTrafficResourceTypes = new[] { TrafficResourceType.Edge, TrafficResourceType.Node };
        MockSimulationEventTypeFilters = new[]
        {
            "All",
            nameof(MockSimulationEventType.VehicleMoved),
            nameof(MockSimulationEventType.WaitingForTraffic),
            nameof(MockSimulationEventType.RouteInvalidated),
            nameof(MockSimulationEventType.TaskReplanned),
            nameof(MockSimulationEventType.ReplanFailed),
            nameof(MockSimulationEventType.ReplanSkipped),
            nameof(MockSimulationEventType.ResourceLocked),
            nameof(MockSimulationEventType.ResourceReleased)
        };
        MockSimulationEventTimelineView = CollectionViewSource.GetDefaultView(MockSimulationEvents);
        MockSimulationEventTimelineView.GroupDescriptions?.Add(
            new PropertyGroupDescription(nameof(MockSimulationEventRow.TickGroup)));
        MockSimulationEventTimelineView.SortDescriptions.Add(
            new SortDescription(nameof(MockSimulationEventRow.Tick), ListSortDirection.Descending));
        MockSimulationEventTimelineView.SortDescriptions.Add(
            new SortDescription(nameof(MockSimulationEventRow.Sequence), ListSortDirection.Ascending));
        RefreshIntervals = new[] { 1, 2, 5 };
        _mockScenarioController.ScenarioChanged += OnScenarioChanged;
        ApplyScenarioState(_mockScenarioController.CurrentState);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_selectedRefreshIntervalSeconds) };
        _timer.Tick += async (_, _) => await RefreshAsync().ConfigureAwait(true);
        _timer.Start();
        _ = RefreshAsync();
    }

    public DelegateCommand RefreshCommand { get; }

    public DelegateCommand<VehicleRow?> OpenVehicleWindowCommand { get; }

    public DelegateCommand LoadMockSimulationScenarioCommand { get; }

    public DelegateCommand StartAllMockSimulationTasksCommand { get; }

    public DelegateCommand StartSelectedMockSimulationTaskCommand { get; }

    public DelegateCommand StepMockSimulationCommand { get; }

    public DelegateCommand RunTenMockSimulationTicksCommand { get; }

    public DelegateCommand RunMockSimulationUntilIdleCommand { get; }

    public DelegateCommand StopMockSimulationRunCommand { get; }

    public DelegateCommand CancelSelectedMockSimulationTaskCommand { get; }

    public DelegateCommand InjectMockSimulationFaultCommand { get; }

    public DelegateCommand RecoverMockSimulationVehicleCommand { get; }

    public DelegateCommand DisableMockMapEdgeCommand { get; }

    public DelegateCommand EnableMockMapEdgeCommand { get; }

    public DelegateCommand BlockMockTrafficResourceCommand { get; }

    public DelegateCommand UnblockMockTrafficResourceCommand { get; }

    public DelegateCommand ApplyMockSimulationOptionsCommand { get; }

    public DelegateCommand RunMockEightScenarioRegressionCommand { get; }

    public DelegateCommand RunMockReplanRegressionCommand { get; }

    public DelegateCommand InitializeScenarioCommand { get; }

    public DelegateCommand StartVehicleACommand { get; }

    public DelegateCommand StartVehicleBCommand { get; }

    public DelegateCommand VehicleAArriveNextNodeCommand { get; }

    public DelegateCommand VehicleBRetryWaitingTaskCommand { get; }

    public DelegateCommand ManualBlockScenarioCommand { get; }

    public DelegateCommand ManualUnblockScenarioCommand { get; }

    public DelegateCommand ResetScenarioCommand { get; }

    public IReadOnlyList<int> RefreshIntervals { get; }

    public IReadOnlyList<MockScenarioOption> ScenarioOptions { get; }

    public IReadOnlyList<MockSimulationScenarioDefinition> MockSimulationScenarioOptions { get; }

    public IReadOnlyList<MockFaultPolicy> MockFaultPolicies { get; }

    public IReadOnlyList<TrafficResourceType> MockTrafficResourceTypes { get; }

    public IReadOnlyList<string> MockSimulationEventTypeFilters { get; }

    public ICollectionView MockSimulationEventTimelineView { get; }

    public ObservableCollection<TaskRow> Tasks { get; } = new();

    public ObservableCollection<VehicleRow> Vehicles { get; } = new();

    public ObservableCollection<TrafficResourceRow> TrafficResources { get; } = new();

    public ObservableCollection<RouteReservationRow> RouteReservations { get; } = new();

    public ObservableCollection<RouteSegmentRow> SelectedReservationSegments { get; } = new();

    public ObservableCollection<DispatchExecutionDto> SelectedTaskExecutions { get; } = new();

    public ObservableCollection<RouteReservationRow> SelectedTaskReservations { get; } = new();

    public ObservableCollection<OkapiProtocolTraceRecord> OkapiProtocolRecords { get; } = new();

    public ObservableCollection<MockScenarioStepLog> ScenarioLogs { get; } = new();

    public ObservableCollection<MockSimulationVehicleRow> MockSimulationVehicles { get; } = new();

    public ObservableCollection<MockSimulationEventRow> MockSimulationEvents { get; } = new();

    public ObservableCollection<string> MockSimulationEventVehicleFilters { get; } = new();

    public ObservableCollection<MockSimulationTaskRow> MockSimulationTasks { get; } = new();

    public ObservableCollection<MockSimulationRegressionResultRow> MockSimulationRegressionResults { get; } = new();

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public bool IsAutoRefreshEnabled
    {
        get => _isAutoRefreshEnabled;
        set
        {
            if (SetProperty(ref _isAutoRefreshEnabled, value))
            {
                UpdateTimer();
            }
        }
    }

    public int SelectedRefreshIntervalSeconds
    {
        get => _selectedRefreshIntervalSeconds;
        set
        {
            if (SetProperty(ref _selectedRefreshIntervalSeconds, value))
            {
                UpdateTimer();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplySnapshot();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public DateTimeOffset? LastRefreshTime
    {
        get => _lastRefreshTime;
        private set => SetProperty(ref _lastRefreshTime, value);
    }

    public TaskRow? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                ApplySelectedTaskDetails();
            }
        }
    }

    public RouteReservationRow? SelectedRouteReservation
    {
        get => _selectedRouteReservation;
        set
        {
            if (SetProperty(ref _selectedRouteReservation, value))
            {
                Replace(SelectedReservationSegments, value?.Segments ?? Array.Empty<RouteSegmentRow>());
            }
        }
    }

    public OkapiProtocolTraceRecord? SelectedOkapiRecord
    {
        get => _selectedOkapiRecord;
        set => SetProperty(ref _selectedOkapiRecord, value);
    }

    public MockScenarioOption? SelectedScenario
    {
        get => _selectedScenario;
        set => SetProperty(ref _selectedScenario, value);
    }

    public string ScenarioName
    {
        get => _scenarioName;
        private set => SetProperty(ref _scenarioName, value);
    }

    public string ScenarioVehicleAId
    {
        get => _scenarioVehicleAId;
        private set => SetProperty(ref _scenarioVehicleAId, value);
    }

    public string ScenarioVehicleBId
    {
        get => _scenarioVehicleBId;
        private set => SetProperty(ref _scenarioVehicleBId, value);
    }

    public string ScenarioTaskAId
    {
        get => _scenarioTaskAId;
        private set => SetProperty(ref _scenarioTaskAId, value);
    }

    public string ScenarioTaskBId
    {
        get => _scenarioTaskBId;
        private set => SetProperty(ref _scenarioTaskBId, value);
    }

    public string ScenarioRouteSummary
    {
        get => _scenarioRouteSummary;
        private set => SetProperty(ref _scenarioRouteSummary, value);
    }

    public string ScenarioManualBlockResource
    {
        get => _scenarioManualBlockResource;
        private set => SetProperty(ref _scenarioManualBlockResource, value);
    }

    public bool IsScenarioManualBlockActive
    {
        get => _isScenarioManualBlockActive;
        private set => SetProperty(ref _isScenarioManualBlockActive, value);
    }

    public string MockSimulationScenarioId
    {
        get => _mockSimulationScenarioId;
        private set => SetProperty(ref _mockSimulationScenarioId, value);
    }

    public string MockSimulationName
    {
        get => _mockSimulationName;
        private set => SetProperty(ref _mockSimulationName, value);
    }

    public long MockSimulationTick
    {
        get => _mockSimulationTick;
        private set => SetProperty(ref _mockSimulationTick, value);
    }

    public string MockSimulationOptions
    {
        get => _mockSimulationOptions;
        private set => SetProperty(ref _mockSimulationOptions, value);
    }

    public MockSimulationScenarioDefinition? SelectedMockSimulationScenario
    {
        get => _selectedMockSimulationScenario;
        set
        {
            if (SetProperty(ref _selectedMockSimulationScenario, value))
            {
                LoadMockSimulationScenarioCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public MockSimulationVehicleRow? SelectedMockSimulationVehicle
    {
        get => _selectedMockSimulationVehicle;
        set
        {
            if (SetProperty(ref _selectedMockSimulationVehicle, value))
            {
                RaiseMockSimulationCommandStates();
            }
        }
    }

    public MockFaultPolicy SelectedMockFaultPolicy
    {
        get => _selectedMockFaultPolicy;
        set => SetProperty(ref _selectedMockFaultPolicy, value);
    }

    public string MockMapEdgeId
    {
        get => _mockMapEdgeId;
        set
        {
            if (SetProperty(ref _mockMapEdgeId, value ?? string.Empty))
            {
                RaiseMockSimulationCommandStates();
            }
        }
    }

    public TrafficResourceType SelectedMockTrafficResourceType
    {
        get => _selectedMockTrafficResourceType;
        set => SetProperty(ref _selectedMockTrafficResourceType, value);
    }

    public string MockTrafficResourceId
    {
        get => _mockTrafficResourceId;
        set
        {
            if (SetProperty(ref _mockTrafficResourceId, value ?? string.Empty))
            {
                RaiseMockSimulationCommandStates();
            }
        }
    }

    public bool MockReplanOnLockedResource
    {
        get => _mockReplanOnLockedResource;
        set => SetProperty(ref _mockReplanOnLockedResource, value);
    }

    public bool MockReplanOnBlockedResource
    {
        get => _mockReplanOnBlockedResource;
        set => SetProperty(ref _mockReplanOnBlockedResource, value);
    }

    public bool MockPreferWaitingOverReplan
    {
        get => _mockPreferWaitingOverReplan;
        set => SetProperty(ref _mockPreferWaitingOverReplan, value);
    }

    public int MockMaxReplanCount
    {
        get => _mockMaxReplanCount;
        set => SetProperty(ref _mockMaxReplanCount, Math.Max(0, value));
    }

    public double MockWaitTimeoutSeconds
    {
        get => _mockWaitTimeoutSeconds;
        set => SetProperty(ref _mockWaitTimeoutSeconds, Math.Max(0, value));
    }

    public double MockRetryIntervalSeconds
    {
        get => _mockRetryIntervalSeconds;
        set => SetProperty(ref _mockRetryIntervalSeconds, Math.Max(0, value));
    }

    public int MockMaxRetryCount
    {
        get => _mockMaxRetryCount;
        set => SetProperty(ref _mockMaxRetryCount, Math.Max(0, value));
    }

    public string SelectedMockEventVehicleFilter
    {
        get => _selectedMockEventVehicleFilter;
        set
        {
            if (SetProperty(ref _selectedMockEventVehicleFilter, string.IsNullOrWhiteSpace(value) ? "All" : value))
            {
                ApplySnapshot();
            }
        }
    }

    public string SelectedMockEventTypeFilter
    {
        get => _selectedMockEventTypeFilter;
        set
        {
            if (SetProperty(ref _selectedMockEventTypeFilter, string.IsNullOrWhiteSpace(value) ? "All" : value))
            {
                ApplySnapshot();
            }
        }
    }

    public MockSimulationEventRow? SelectedMockSimulationEvent
    {
        get => _selectedMockSimulationEvent;
        set => SetProperty(ref _selectedMockSimulationEvent, value);
    }

    public bool IsMockSimulationRunActive
    {
        get => _isMockSimulationRunActive;
        private set
        {
            if (SetProperty(ref _isMockSimulationRunActive, value))
            {
                RaiseMockSimulationCommandStates();
            }
        }
    }

    public bool IsMockSimulationRegressionActive
    {
        get => _isMockSimulationRegressionActive;
        private set
        {
            if (SetProperty(ref _isMockSimulationRegressionActive, value))
            {
                RaiseMockSimulationCommandStates();
            }
        }
    }

    public int TaskCount { get; private set; }
    public int RunningTaskCount { get; private set; }
    public int WaitingForTrafficTaskCount { get; private set; }
    public int FaultVehicleCount { get; private set; }
    public int LockedResourceCount { get; private set; }
    public int OccupiedResourceCount { get; private set; }
    public int BlockedResourceCount { get; private set; }
    public int RecentOkapiErrorCount { get; private set; }

    public async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        RefreshCommand.RaiseCanExecuteChanged();
        RaiseMockSimulationCommandStates();
        try
        {
            _snapshot = await _debugSnapshotService.GetSnapshotAsync().ConfigureAwait(true);
            ApplySnapshot();
            LastRefreshTime = _snapshot.GeneratedAt;
            StatusMessage = _snapshot.DiagnosticMessages.Count == 0
                ? "Refresh completed"
                : $"Refresh completed with diagnostics: {string.Join("; ", _snapshot.DiagnosticMessages)}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsRefreshing = false;
            RefreshCommand.RaiseCanExecuteChanged();
            RaiseMockSimulationCommandStates();
        }
    }

    private async Task LoadMockSimulationScenarioAsync()
    {
        if (SelectedMockSimulationScenario is null)
        {
            return;
        }

        try
        {
            var scenario = MockSimulationScenarioCatalog.Create(SelectedMockSimulationScenario.Key);
            var result = await _mockFleetSimulationEngine.InitializeAsync(scenario).ConfigureAwait(true);
            _mockSimulationEditableScenarioId = string.Empty;
        StatusMessage = result.Succeeded
                ? $"仿真场景已加载：{MockDisplayNames.ToDisplayName(scenario.Name)}"
                : result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task StartAllMockSimulationTasksAsync()
    {
        try
        {
            var result = await _mockFleetSimulationEngine.StartAllAsync().ConfigureAwait(true);
            StatusMessage = result.Succeeded
                ? "仿真任务已全部启动。"
                : result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task StartSelectedMockSimulationTaskAsync()
    {
        var vehicle = SelectedMockSimulationVehicle;
        if (vehicle is null || string.IsNullOrWhiteSpace(vehicle.CurrentTaskId))
        {
            return;
        }

        try
        {
            var result = await _mockFleetSimulationEngine
                .StartTaskAsync(vehicle.VehicleId, vehicle.CurrentTaskId)
                .ConfigureAwait(true);
            StatusMessage = result.Succeeded
                ? $"仿真任务已启动：{vehicle.VehicleId}/{vehicle.CurrentTaskId}"
                : result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task StepMockSimulationAsync()
    {
        try
        {
            var result = await _mockFleetSimulationEngine.StepAsync().ConfigureAwait(true);
            StatusMessage = result.Succeeded
                ? $"仿真已推进：第 {result.Tick} tick"
                : result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task CancelSelectedMockSimulationTaskAsync()
    {
        var vehicle = SelectedMockSimulationVehicle;
        if (string.IsNullOrWhiteSpace(vehicle?.CurrentTaskId))
        {
            return;
        }

        try
        {
            var result = await _mockFleetSimulationEngine
                .CancelTaskAsync(vehicle.CurrentTaskId)
                .ConfigureAwait(true);
            StatusMessage = result.Succeeded
                ? $"仿真任务已取消：{vehicle.CurrentTaskId}"
                : result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task InjectMockSimulationFaultAsync()
    {
        var vehicle = SelectedMockSimulationVehicle;
        if (string.IsNullOrWhiteSpace(vehicle?.VehicleId))
        {
            return;
        }

        try
        {
            var result = await _mockFleetSimulationEngine
                .InjectFaultAsync(vehicle.VehicleId, SelectedMockFaultPolicy)
                .ConfigureAwait(true);
            StatusMessage = result.Succeeded
                ? $"已注入车辆故障：{vehicle.VehicleId}/{MockDisplayNames.ToDisplayName(SelectedMockFaultPolicy.ToString())}"
                : result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RecoverMockSimulationVehicleAsync()
    {
        var vehicle = SelectedMockSimulationVehicle;
        if (string.IsNullOrWhiteSpace(vehicle?.VehicleId))
        {
            return;
        }

        try
        {
            var result = await _mockFleetSimulationEngine
                .RecoverVehicleAsync(vehicle.VehicleId)
                .ConfigureAwait(true);
            StatusMessage = result.Succeeded
                ? $"仿真车辆已恢复：{vehicle.VehicleId}"
                : result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task SetMockMapEdgeEnabledAsync(bool enabled)
    {
        try
        {
            var result = await _mockFleetSimulationEngine
                .SetMapEdgeEnabledAsync(MockMapEdgeId.Trim(), enabled)
                .ConfigureAwait(true);
            StatusMessage = result.Succeeded
                ? result.Message
                : result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task SetMockTrafficResourceBlockedAsync(bool blocked)
    {
        try
        {
            var result = blocked
                ? await _mockFleetSimulationEngine
                    .BlockTrafficResourceAsync(SelectedMockTrafficResourceType, MockTrafficResourceId.Trim())
                    .ConfigureAwait(true)
                : await _mockFleetSimulationEngine
                    .UnblockTrafficResourceAsync(SelectedMockTrafficResourceType, MockTrafficResourceId.Trim())
                    .ConfigureAwait(true);
            StatusMessage = result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task ApplyMockSimulationOptionsAsync()
    {
        try
        {
            var current = _mockFleetSimulationEngine.CurrentScenario?.Options ?? new MockSimulationOptions();
            var result = await _mockFleetSimulationEngine.UpdateOptionsAsync(new MockSimulationOptions
            {
                RollingWindowSize = current.RollingWindowSize,
                WaitTimeout = TimeSpan.FromSeconds(MockWaitTimeoutSeconds),
                RetryInterval = TimeSpan.FromSeconds(MockRetryIntervalSeconds),
                MaxRetryCount = MockMaxRetryCount,
                ReplanOnLockedResource = MockReplanOnLockedResource,
                ReplanOnBlockedResource = MockReplanOnBlockedResource,
                PreferWaitingOverReplan = MockPreferWaitingOverReplan,
                MaxReplanCount = MockMaxReplanCount,
                OnTimeoutPolicy = current.OnTimeoutPolicy,
                AutoStartTasks = current.AutoStartTasks
            }).ConfigureAwait(true);
            StatusMessage = result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RunMockEightScenarioRegressionAsync()
    {
        await RunMockRegressionAsync(new Func<Task<MockSimulationRegressionResultRow>>[]
        {
            RunSameTargetRegressionAsync,
            RunNarrowAisleRegressionAsync,
            RunIntersectionRegressionAsync,
            RunLockedEdgeRegressionAsync,
            RunWaitingTimeoutRegressionAsync,
            RunCancelTaskRegressionAsync,
            RunFaultVehicleRegressionAsync,
            RunDisabledMapEdgeRegressionAsync
        }, "8 个核心场景回归完成。").ConfigureAwait(true);
    }

    private async Task RunMockReplanRegressionAsync()
    {
        await RunMockRegressionAsync(new Func<Task<MockSimulationRegressionResultRow>>[]
        {
            RunDisabledEdgeWithAlternativeRegressionAsync,
            RunDisabledEdgeWithoutAlternativeRegressionAsync,
            RunLockedEdgeReplanRegressionAsync,
            RunReplanLimitRegressionAsync
        }, "重规划回归完成。").ConfigureAwait(true);
    }

    private async Task RunMockRegressionAsync(
        IReadOnlyList<Func<Task<MockSimulationRegressionResultRow>>> cases,
        string completedMessage)
    {
        if (IsMockSimulationRegressionActive)
        {
            return;
        }

        IsMockSimulationRegressionActive = true;
        MockSimulationRegressionResults.Clear();
        var finalStatus = string.Empty;
        try
        {
            foreach (var runCase in cases)
            {
                var row = await runCase().ConfigureAwait(true);
                MockSimulationRegressionResults.Add(row);
                StatusMessage = $"仿真回归：{row.ScenarioNameDisplay} {row.ResultDisplay}。";
            }

            var failedCount = MockSimulationRegressionResults.Count(row =>
                string.Equals(row.Result, "Failed", StringComparison.OrdinalIgnoreCase));
            finalStatus = failedCount == 0
                ? completedMessage
                : $"{completedMessage} 失败：{failedCount}。";
            StatusMessage = finalStatus;
        }
        catch (Exception ex)
        {
            finalStatus = ex.Message;
            StatusMessage = finalStatus;
        }
        finally
        {
            IsMockSimulationRegressionActive = false;
            await RefreshAsync().ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(finalStatus))
            {
                StatusMessage = finalStatus;
            }
        }
    }

    private async Task<MockSimulationRegressionResultRow> RunSameTargetRegressionAsync()
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("SameTarget", events).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine.StartAllAsync().ConfigureAwait(true));
        await RunRegressionStepsAsync(events, 40, stopWhenIdle: true).ConfigureAwait(true);

        return BuildRegressionResult(
            "TwoVehiclesSameTarget",
            HasEvent(events, MockSimulationEventType.WaitingForTraffic) &&
            HasEvent(events, MockSimulationEventType.TaskCompleted) &&
            !HasEvent(events, MockSimulationEventType.SimulationFailed),
            "预期共享终点串行通行，且等待后最终完成。",
            events);
    }

    private async Task<MockSimulationRegressionResultRow> RunNarrowAisleRegressionAsync()
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("NarrowAisle", events).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine.StartAllAsync().ConfigureAwait(true));
        await RunRegressionStepsAsync(events, 40, stopWhenIdle: true).ConfigureAwait(true);

        return BuildRegressionResult(
            "TwoVehiclesOppositeDirectionNarrowAisle",
            HasEvent(events, MockSimulationEventType.WaitingForTraffic) &&
            !HasEvent(events, MockSimulationEventType.SimulationFailed),
            "预期窄道相向会车时至少一台车等待。",
            events);
    }

    private async Task<MockSimulationRegressionResultRow> RunIntersectionRegressionAsync()
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("Intersection", events).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine.StartAllAsync().ConfigureAwait(true));
        await RunRegressionStepsAsync(events, 30, stopWhenIdle: true).ConfigureAwait(true);

        return BuildRegressionResult(
            "IntersectionOccupied",
            HasEvent(events, MockSimulationEventType.WaitingForTraffic) &&
            !HasEvent(events, MockSimulationEventType.SimulationFailed),
            "预期路口被占用时另一台车等待。",
            events);
    }

    private async Task<MockSimulationRegressionResultRow> RunLockedEdgeRegressionAsync()
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("AlternativeRoute", events, options => new MockSimulationOptions
        {
            RollingWindowSize = options.RollingWindowSize,
            WaitTimeout = options.WaitTimeout,
            RetryInterval = options.RetryInterval,
            MaxRetryCount = options.MaxRetryCount,
            ReplanOnLockedResource = false,
            ReplanOnBlockedResource = options.ReplanOnBlockedResource,
            PreferWaitingOverReplan = true,
            MaxReplanCount = options.MaxReplanCount,
            OnTimeoutPolicy = options.OnTimeoutPolicy,
            AutoStartTasks = options.AutoStartTasks
        }).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine.StartAllAsync().ConfigureAwait(true));
        await RunRegressionStepsAsync(events, 15, stopWhenIdle: false).ConfigureAwait(true);

        return BuildRegressionResult(
            "LockedEdge",
            HasEvent(events, MockSimulationEventType.WaitingForTraffic) ||
            HasEvent(events, MockSimulationEventType.ResourceLocked),
            "预期边资源被锁后其他车辆等待或产生锁定事件。",
            events);
    }

    private async Task<MockSimulationRegressionResultRow> RunWaitingTimeoutRegressionAsync()
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("SameTarget", events, options => new MockSimulationOptions
        {
            RollingWindowSize = options.RollingWindowSize,
            WaitTimeout = TimeSpan.Zero,
            RetryInterval = TimeSpan.Zero,
            MaxRetryCount = 0,
            ReplanOnLockedResource = true,
            ReplanOnBlockedResource = true,
            PreferWaitingOverReplan = false,
            MaxReplanCount = 1,
            OnTimeoutPolicy = options.OnTimeoutPolicy,
            AutoStartTasks = options.AutoStartTasks
        }).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine.StartAllAsync().ConfigureAwait(true));
        await RunRegressionStepsAsync(events, 8, stopWhenIdle: false).ConfigureAwait(true);

        return BuildRegressionResult(
            "WaitingTimeout",
            HasAnyEvent(events,
                MockSimulationEventType.WaitingTimedOut,
                MockSimulationEventType.RetryAttempted,
                MockSimulationEventType.ReplanRequired,
                MockSimulationEventType.TaskReplanned,
                MockSimulationEventType.ReplanFailed,
                MockSimulationEventType.ReplanSkipped),
            "预期等待超时后重试或请求重规划。",
            events);
    }

    private async Task<MockSimulationRegressionResultRow> RunCancelTaskRegressionAsync()
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("Linear", events).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine.StartAllAsync().ConfigureAwait(true));
        var taskId = _mockFleetSimulationEngine.GetVehicleStates().FirstOrDefault()?.CurrentTaskId;
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            AddEvents(events, await _mockFleetSimulationEngine.CancelTaskAsync(taskId).ConfigureAwait(true));
        }

        return BuildRegressionResult(
            "CancelTask",
            HasEvent(events, MockSimulationEventType.TaskCanceled) &&
            _mockFleetSimulationEngine.GetVehicleStates().All(vehicle => vehicle.State is MockVehicleSimulationState.Idle
                or MockVehicleSimulationState.Canceled),
            "预期取消后任务释放，车辆回到空闲或终态。",
            events);
    }

    private async Task<MockSimulationRegressionResultRow> RunFaultVehicleRegressionAsync()
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("SameTarget", events).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine.StartAllAsync().ConfigureAwait(true));
        AddEvents(events, await _mockFleetSimulationEngine
            .InjectFaultAsync("AGV-MOCK-A", MockFaultPolicy.HoldResources)
            .ConfigureAwait(true));
        AddEvents(events, await _mockFleetSimulationEngine.StepAsync().ConfigureAwait(true));

        return BuildRegressionResult(
            "FaultVehicle",
            HasEvent(events, MockSimulationEventType.VehicleFaulted) &&
            _mockFleetSimulationEngine.GetVehicleStates().Any(vehicle =>
                string.Equals(vehicle.VehicleId, "AGV-MOCK-A", StringComparison.OrdinalIgnoreCase) &&
                vehicle.State == MockVehicleSimulationState.Fault),
            "预期故障注入后车辆进入故障状态。",
            events);
    }

    private async Task<MockSimulationRegressionResultRow> RunDisabledMapEdgeRegressionAsync()
    {
        return await RunDisabledEdgeWithAlternativeRegressionAsync("DisabledMapEdge").ConfigureAwait(true);
    }

    private async Task<MockSimulationRegressionResultRow> RunDisabledEdgeWithAlternativeRegressionAsync()
    {
        return await RunDisabledEdgeWithAlternativeRegressionAsync("DisabledMapEdge_WithAlternative")
            .ConfigureAwait(true);
    }

    private async Task<MockSimulationRegressionResultRow> RunDisabledEdgeWithAlternativeRegressionAsync(
        string scenarioName)
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("CurrentNodeReplan", events, options => new MockSimulationOptions
        {
            RollingWindowSize = options.RollingWindowSize,
            WaitTimeout = options.WaitTimeout,
            RetryInterval = options.RetryInterval,
            MaxRetryCount = options.MaxRetryCount,
            ReplanOnLockedResource = options.ReplanOnLockedResource,
            ReplanOnBlockedResource = true,
            PreferWaitingOverReplan = options.PreferWaitingOverReplan,
            MaxReplanCount = 3,
            OnTimeoutPolicy = options.OnTimeoutPolicy,
            AutoStartTasks = options.AutoStartTasks
        }).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine
            .StartTaskAsync("AGV-MOCK-A", "TASK-MOCK-A")
            .ConfigureAwait(true));
        AddEvents(events, await _mockFleetSimulationEngine
            .SetMapEdgeEnabledAsync("E-A-T", enabled: false)
            .ConfigureAwait(true));
        await RunRegressionStepsAsync(events, 8, stopWhenIdle: false).ConfigureAwait(true);

        return BuildRegressionResult(
            scenarioName,
            HasEvent(events, MockSimulationEventType.RouteInvalidated) &&
            HasEvent(events, MockSimulationEventType.TaskReplanned),
            "预期禁用当前路线边后触发重规划并绕行。",
            events);
    }

    private async Task<MockSimulationRegressionResultRow> RunDisabledEdgeWithoutAlternativeRegressionAsync()
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("Linear", events).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine.StartAllAsync().ConfigureAwait(true));
        AddEvents(events, await _mockFleetSimulationEngine
            .SetMapEdgeEnabledAsync("E2", enabled: false)
            .ConfigureAwait(true));
        await RunRegressionStepsAsync(events, 5, stopWhenIdle: false).ConfigureAwait(true);

        return BuildRegressionResult(
            "DisabledMapEdge_NoAlternative",
            HasEvent(events, MockSimulationEventType.RouteInvalidated) &&
            HasEvent(events, MockSimulationEventType.ReplanFailed),
            "预期路线被禁用且无替代路径时产生重规划失败。",
            events);
    }

    private async Task<MockSimulationRegressionResultRow> RunLockedEdgeReplanRegressionAsync()
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("AlternativeRoute", events, options => new MockSimulationOptions
        {
            RollingWindowSize = options.RollingWindowSize,
            WaitTimeout = TimeSpan.Zero,
            RetryInterval = TimeSpan.Zero,
            MaxRetryCount = 0,
            ReplanOnLockedResource = true,
            ReplanOnBlockedResource = true,
            PreferWaitingOverReplan = false,
            MaxReplanCount = 2,
            OnTimeoutPolicy = options.OnTimeoutPolicy,
            AutoStartTasks = options.AutoStartTasks
        }).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine.StartAllAsync().ConfigureAwait(true));
        await RunRegressionStepsAsync(events, 10, stopWhenIdle: false).ConfigureAwait(true);

        return BuildRegressionResult(
            "LockedEdge_WithAlternative",
            HasAnyEvent(events,
                MockSimulationEventType.ReplanRequired,
                MockSimulationEventType.TaskReplanned,
                MockSimulationEventType.WaitingTimedOut),
            "预期锁定资源策略触发重规划尝试。",
            events);
    }

    private async Task<MockSimulationRegressionResultRow> RunReplanLimitRegressionAsync()
    {
        var events = new List<MockSimulationEvent>();
        await InitializeRegressionScenarioAsync("CurrentNodeReplan", events, options => new MockSimulationOptions
        {
            RollingWindowSize = options.RollingWindowSize,
            WaitTimeout = options.WaitTimeout,
            RetryInterval = options.RetryInterval,
            MaxRetryCount = options.MaxRetryCount,
            ReplanOnLockedResource = options.ReplanOnLockedResource,
            ReplanOnBlockedResource = true,
            PreferWaitingOverReplan = options.PreferWaitingOverReplan,
            MaxReplanCount = 0,
            OnTimeoutPolicy = options.OnTimeoutPolicy,
            AutoStartTasks = options.AutoStartTasks
        }).ConfigureAwait(true);
        AddEvents(events, await _mockFleetSimulationEngine
            .StartTaskAsync("AGV-MOCK-A", "TASK-MOCK-A")
            .ConfigureAwait(true));
        AddEvents(events, await _mockFleetSimulationEngine
            .SetMapEdgeEnabledAsync("E-A-T", enabled: false)
            .ConfigureAwait(true));
        await RunRegressionStepsAsync(events, 5, stopWhenIdle: false).ConfigureAwait(true);

        return BuildRegressionResult(
            "ExceedsMaxReplan",
            HasEvent(events, MockSimulationEventType.ReplanSkipped),
            "预期达到最大重规划次数后跳过重规划。",
            events);
    }

    private async Task InitializeRegressionScenarioAsync(
        string scenarioKey,
        ICollection<MockSimulationEvent> events,
        Func<MockSimulationOptions, MockSimulationOptions>? configureOptions = null)
    {
        var scenario = MockSimulationScenarioCatalog.Create(scenarioKey);
        if (configureOptions is not null)
        {
            scenario = WithOptions(scenario, configureOptions(scenario.Options));
        }

        AddEvents(events, await _mockFleetSimulationEngine.InitializeAsync(scenario).ConfigureAwait(true));
    }

    private async Task RunRegressionStepsAsync(
        ICollection<MockSimulationEvent> events,
        int maxTicks,
        bool stopWhenIdle)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            AddEvents(events, await _mockFleetSimulationEngine.StepAsync().ConfigureAwait(true));
            if (stopWhenIdle && IsMockSimulationIdle())
            {
                return;
            }
        }
    }

    private static MockSimulationScenario WithOptions(
        MockSimulationScenario scenario,
        MockSimulationOptions options) => new()
    {
        ScenarioId = scenario.ScenarioId,
        Name = scenario.Name,
        MapSnapshot = scenario.MapSnapshot,
        Vehicles = scenario.Vehicles.Select(vehicle => vehicle.Clone()).ToArray(),
        Tasks = scenario.Tasks.Select(task => task.Clone()).ToArray(),
        Options = options
    };

    private static void AddEvents(ICollection<MockSimulationEvent> target, MockSimulationTickResult result)
    {
        foreach (var simulationEvent in result.Events)
        {
            target.Add(simulationEvent);
        }
    }

    private static MockSimulationRegressionResultRow BuildRegressionResult(
        string scenarioName,
        bool passed,
        string failedReason,
        IReadOnlyList<MockSimulationEvent> events) => new()
    {
        ScenarioName = scenarioName,
        Result = passed ? "Passed" : "Failed",
        FailedReason = passed ? string.Empty : failedReason,
        LastEvent = FormatLastEvent(events.LastOrDefault())
    };

    private static bool HasEvent(
        IEnumerable<MockSimulationEvent> events,
        MockSimulationEventType eventType) =>
        events.Any(simulationEvent => simulationEvent.EventType == eventType);

    private static bool HasAnyEvent(
        IEnumerable<MockSimulationEvent> events,
        params MockSimulationEventType[] eventTypes)
    {
        var set = eventTypes.ToHashSet();
        return events.Any(simulationEvent => set.Contains(simulationEvent.EventType));
    }

    private static string FormatLastEvent(MockSimulationEvent? simulationEvent)
    {
        if (simulationEvent is null)
        {
            return string.Empty;
        }

        var subject = simulationEvent.VehicleId ?? simulationEvent.TaskId ?? simulationEvent.ResourceId;
        var eventName = MockDisplayNames.ToDisplayName(simulationEvent.EventType.ToString());
        return string.IsNullOrWhiteSpace(subject)
            ? $"{eventName}: {simulationEvent.Message}"
            : $"{eventName} [{subject}]: {simulationEvent.Message}";
    }

    private async Task RunMockSimulationTicksAsync(int tickCount)
    {
        await RunMockSimulationLoopAsync(
            maxTicks: Math.Max(1, tickCount),
            stopWhenIdle: false,
            completedMessage: $"Mock simulation ran {Math.Max(1, tickCount)} ticks.").ConfigureAwait(true);
    }

    private async Task RunMockSimulationUntilIdleAsync(int maxTicks)
    {
        await RunMockSimulationLoopAsync(
            maxTicks: Math.Max(1, maxTicks),
            stopWhenIdle: true,
            completedMessage: "Mock simulation reached idle.").ConfigureAwait(true);
    }

    private async Task RunMockSimulationLoopAsync(int maxTicks, bool stopWhenIdle, string completedMessage)
    {
        if (IsMockSimulationRunActive)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _mockSimulationRunCancellation = cancellation;
        IsMockSimulationRunActive = true;
        var executedTicks = 0;
        try
        {
            for (var i = 0; i < maxTicks; i++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var result = await _mockFleetSimulationEngine.StepAsync(cancellation.Token).ConfigureAwait(true);
                executedTicks++;
            StatusMessage = result.Succeeded
                    ? $"仿真已推进：第 {result.Tick} tick"
                    : result.Message;
                await RefreshAsync().ConfigureAwait(true);

                if (stopWhenIdle && IsMockSimulationIdle())
                {
                    StatusMessage = $"{completedMessage} 已执行 tick：{executedTicks}。";
                    return;
                }

                await Task.Delay(120, cancellation.Token).ConfigureAwait(true);
            }

            StatusMessage = stopWhenIdle
                ? $"运行到空闲已达到最大 tick 限制：{maxTicks}。"
                : $"{completedMessage} 已执行 tick：{executedTicks}。";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"仿真运行已停止。已执行 tick：{executedTicks}。";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsMockSimulationRunActive = false;
            _mockSimulationRunCancellation = null;
            await RefreshAsync().ConfigureAwait(true);
        }
    }

    private void StopMockSimulationRun()
    {
        _mockSimulationRunCancellation?.Cancel();
    }

    private bool IsMockSimulationIdle()
    {
        var vehicles = _mockFleetSimulationEngine.GetVehicleStates();
        return vehicles.Count > 0 && vehicles.All(vehicle =>
            vehicle.State is MockVehicleSimulationState.Idle
                or MockVehicleSimulationState.Completed
                or MockVehicleSimulationState.Canceled
                or MockVehicleSimulationState.Failed
                or MockVehicleSimulationState.TimedOut
                or MockVehicleSimulationState.Fault);
    }

    public void Dispose()
    {
        _timer.Stop();
        _mockScenarioController.ScenarioChanged -= OnScenarioChanged;
    }

    private async Task RunScenarioActionAsync(Func<Task<MockScenarioOperationResult>> action)
    {
        try
        {
            var result = await action().ConfigureAwait(true);
            StatusMessage = result.Message;
            ApplyScenarioState(_mockScenarioController.CurrentState);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void OnScenarioChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyScenarioState(_mockScenarioController.CurrentState);
            return;
        }

        dispatcher.BeginInvoke(() => ApplyScenarioState(_mockScenarioController.CurrentState));
    }

    private void ApplyScenarioState(MockScenarioState state)
    {
        ScenarioName = state.ScenarioName;
        ScenarioVehicleAId = state.VehicleAId;
        ScenarioVehicleBId = state.VehicleBId;
        ScenarioTaskAId = state.TaskAId ?? string.Empty;
        ScenarioTaskBId = state.TaskBId ?? string.Empty;
        ScenarioRouteSummary = string.IsNullOrWhiteSpace(state.SourceA)
            ? "场景尚未初始化"
            : $"A车：{state.SourceA} -> {state.Target}；B车：{state.SourceB} -> {state.Target}";
        ScenarioManualBlockResource = state.ManualBlockResource is null
            ? string.Empty
            : $"{state.ManualBlockResource.ResourceType}:{state.ManualBlockResource.ResourceId}";
        IsScenarioManualBlockActive = state.IsManualBlockActive;
        Replace(ScenarioLogs, state.Logs);
    }

    private void UpdateTimer()
    {
        _timer.Stop();
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, SelectedRefreshIntervalSeconds));
        if (IsAutoRefreshEnabled)
        {
            _timer.Start();
        }
    }

    private void ApplySnapshot()
    {
        var query = SearchText.Trim();
        var statusByVehicleId = _snapshot.VehicleStatuses
            .GroupBy(status => status.VehicleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ReportedAt).First(), StringComparer.OrdinalIgnoreCase);
        var vehiclesById = _snapshot.Vehicles.ToDictionary(vehicle => vehicle.VehicleId, StringComparer.OrdinalIgnoreCase);

        var taskRows = _snapshot.Tasks.Select(ToTaskRow).Where(row => Matches(row, query)).ToArray();
        var vehicleRows = _snapshot.VehicleStatuses
            .Select(status => ToVehicleRow(status, vehiclesById.GetValueOrDefault(status.VehicleId)))
            .Concat(_snapshot.Vehicles.Where(vehicle => !statusByVehicleId.ContainsKey(vehicle.VehicleId))
                .Select(vehicle => ToVehicleRow(null, vehicle)))
            .Where(row => Matches(row, query))
            .ToArray();
        var trafficRows = _snapshot.TrafficResources.Select(ToTrafficResourceRow).Where(row => Matches(row, query)).ToArray();
        var reservationRows = _snapshot.RouteReservations.Select(ToRouteReservationRow).Where(row => Matches(row, query)).ToArray();
        var okapiRows = _snapshot.OkapiProtocolRecords.Where(row => Matches(row, query)).ToArray();
        var mockVehicleRows = _snapshot.MockSimulation.Vehicles
            .Select(ToMockSimulationVehicleRow)
            .Where(row => Matches(row, query))
            .ToArray();
        var allMockEventRows = _snapshot.MockSimulation.RecentEvents
            .Select((item, index) => ToMockSimulationEventRow(item, index))
            .ToArray();
        UpdateMockEventVehicleFilters(allMockEventRows);
        var mockEventRows = allMockEventRows
            .Where(row => MatchesMockEventFilters(row))
            .Where(row => Matches(row, query))
            .ToArray();
        var mockTaskRows = _snapshot.MockSimulation.Vehicles
            .Where(vehicle => !string.IsNullOrWhiteSpace(vehicle.CurrentTaskId))
            .Select(vehicle => ToMockSimulationTaskRow(vehicle, _snapshot.Tasks.FirstOrDefault(task =>
                string.Equals(task.TaskId, vehicle.CurrentTaskId, StringComparison.OrdinalIgnoreCase))))
            .Where(row => Matches(row, query))
            .ToArray();

        Replace(Tasks, taskRows);
        Replace(Vehicles, vehicleRows);
        Replace(TrafficResources, trafficRows);
        Replace(RouteReservations, reservationRows);
        Replace(OkapiProtocolRecords, okapiRows);
        Replace(MockSimulationVehicles, mockVehicleRows);
        Replace(MockSimulationEvents, mockEventRows);
        MockSimulationEventTimelineView.Refresh();
        SelectedMockSimulationEvent = SelectedMockSimulationEvent is null
            ? null
            : MockSimulationEvents.FirstOrDefault(row => row.Sequence == SelectedMockSimulationEvent.Sequence);
        Replace(MockSimulationTasks, mockTaskRows);
        MockSimulationScenarioId = _snapshot.MockSimulation.ScenarioId;
        MockSimulationName = _snapshot.MockSimulation.Name;
        MockSimulationTick = _snapshot.MockSimulation.Tick;
        MockSimulationOptions = _snapshot.MockSimulation.Options;
        SyncEditableMockSimulationOptions();
        SelectedMockSimulationVehicle = SelectedMockSimulationVehicle is null
            ? null
            : MockSimulationVehicles.FirstOrDefault(vehicle =>
                string.Equals(vehicle.VehicleId, SelectedMockSimulationVehicle.VehicleId, StringComparison.OrdinalIgnoreCase));
        RaiseMockSimulationCommandStates();

        UpdateSummary();
        ApplySelectedTaskDetails();
        if (SelectedRouteReservation is not null)
        {
            SelectedRouteReservation = RouteReservations.FirstOrDefault(row => row.ReservationId == SelectedRouteReservation.ReservationId);
        }
        else
        {
            Replace(SelectedReservationSegments, Array.Empty<RouteSegmentRow>());
        }
    }

    private void UpdateSummary()
    {
        TaskCount = _snapshot.Tasks.Count;
        RunningTaskCount = _snapshot.Tasks.Count(task => task.State == TaskState.Running);
        WaitingForTrafficTaskCount = _snapshot.DispatchExecutions.Count(execution => execution.State == DispatchExecutionState.WaitingForTraffic);
        FaultVehicleCount = _snapshot.VehicleStatuses.Count(status => status.State == RobotState.Fault || status.HasAlarm);
        LockedResourceCount = _snapshot.TrafficResources.Count(resource => resource.State == TrafficResourceState.Locked);
        OccupiedResourceCount = _snapshot.TrafficResources.Count(resource => resource.State == TrafficResourceState.Occupied);
        BlockedResourceCount = _snapshot.TrafficResources.Count(resource => resource.State == TrafficResourceState.Blocked);
        RecentOkapiErrorCount = _snapshot.OkapiProtocolRecords.Count(record =>
            record.Direction.Equals("Error", StringComparison.OrdinalIgnoreCase));

        RaisePropertyChanged(nameof(TaskCount));
        RaisePropertyChanged(nameof(RunningTaskCount));
        RaisePropertyChanged(nameof(WaitingForTrafficTaskCount));
        RaisePropertyChanged(nameof(FaultVehicleCount));
        RaisePropertyChanged(nameof(LockedResourceCount));
        RaisePropertyChanged(nameof(OccupiedResourceCount));
        RaisePropertyChanged(nameof(BlockedResourceCount));
        RaisePropertyChanged(nameof(RecentOkapiErrorCount));
    }

    private void ApplySelectedTaskDetails()
    {
        var taskId = SelectedTask?.TaskId;
        Replace(SelectedTaskExecutions, string.IsNullOrWhiteSpace(taskId)
            ? Array.Empty<DispatchExecutionDto>()
            : _snapshot.DispatchExecutions.Where(execution =>
                string.Equals(execution.TaskId, taskId, StringComparison.OrdinalIgnoreCase)));
        Replace(SelectedTaskReservations, string.IsNullOrWhiteSpace(taskId)
            ? Array.Empty<RouteReservationRow>()
            : _snapshot.RouteReservations
                .Where(reservation => string.Equals(reservation.TaskId, taskId, StringComparison.OrdinalIgnoreCase))
                .Select(ToRouteReservationRow));
    }

    private void OpenVehicleWindow(VehicleRow? vehicle)
    {
        if (vehicle is null || string.IsNullOrWhiteSpace(vehicle.VehicleId))
        {
            return;
        }

        _mockVehicleWindowService.ShowVehicle(vehicle.VehicleId);
    }

    private static TaskRow ToTaskRow(TaskOrder task) => new()
    {
        TaskId = task.TaskId,
        State = task.State.ToString(),
        StateDisplay = MockDisplayNames.ToDisplayName(task.State.ToString()),
        AssignedVehicleId = task.AssignedVehicleId,
        SourceNodeId = task.SourceNodeId,
        TargetNodeId = task.TargetNodeId,
        CurrentNodeId = task.CurrentNodeId,
        ProgressPercent = task.ProgressPercent,
        Priority = task.Priority.ToString(),
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.FinishedAt ?? task.StartedAt,
        FailureReason = task.FailureReason ?? task.CancelReason
    };

    private static VehicleRow ToVehicleRow(VehicleStatus? status, Vehicle? vehicle) => new()
    {
        VehicleId = status?.VehicleId ?? vehicle?.VehicleId ?? string.Empty,
        VehicleCode = vehicle?.VehicleCode ?? string.Empty,
        Name = vehicle?.Name ?? string.Empty,
        Brand = vehicle?.Brand ?? string.Empty,
        AdapterType = vehicle?.AdapterType ?? string.Empty,
        State = status?.State.ToString() ?? "Unknown",
        StateDisplay = MockDisplayNames.ToDisplayName(status?.State.ToString() ?? "Unknown"),
        IsOnline = status?.IsOnline ?? false,
        BatteryLevel = status?.BatteryLevel,
        Location = status?.LocationText ?? vehicle?.HomeNodeId ?? string.Empty,
        CurrentTaskId = status?.CurrentTaskId,
        HasAlarm = status?.HasAlarm ?? false,
        ActiveAlarmCode = status?.ActiveAlarmCode,
        ActiveAlarmMessage = status?.ActiveAlarmMessage,
        ReportedAt = status?.ReportedAt
    };

    private static TrafficResourceRow ToTrafficResourceRow(TrafficResourceStatusDto resource) => new()
    {
        ResourceType = resource.Resource.ResourceType.ToString(),
        ResourceTypeDisplay = MockDisplayNames.ToDisplayName(resource.Resource.ResourceType.ToString()),
        ResourceId = resource.Resource.ResourceId,
        State = resource.State.ToString(),
        StateDisplay = MockDisplayNames.ToDisplayName(resource.State.ToString()),
        OccupiedByAgvId = resource.OccupiedByAgvId,
        ReservedByAgvId = resource.ReservedByAgvId,
        TaskId = resource.TaskId,
        ReservationId = resource.ReservationId,
        Reason = resource.Reason,
        ExpireAt = resource.ExpireAt,
        UpdatedAt = resource.UpdatedAt
    };

    private static RouteReservationRow ToRouteReservationRow(RouteReservationDto reservation) => new()
    {
        ReservationId = reservation.ReservationId,
        TaskId = reservation.TaskId,
        VehicleId = reservation.VehicleId,
        PlanId = reservation.PlanId,
        MapId = reservation.MapId,
        MapVersion = reservation.MapVersion,
        State = reservation.State.ToString(),
        StateDisplay = MockDisplayNames.ToDisplayName(reservation.State.ToString()),
        RollingWindowSize = reservation.RollingWindowSize,
        SegmentCount = reservation.Segments.Count,
        CurrentWindow = reservation.CurrentWindow is null
            ? string.Empty
            : $"{reservation.CurrentWindow.StartSegmentSequence}-{reservation.CurrentWindow.EndSegmentSequence}",
        UpdatedAt = reservation.UpdatedAt,
        Segments = reservation.Segments.Select(segment => new RouteSegmentRow
        {
            Sequence = segment.Segment.Sequence,
            FromNodeId = segment.Segment.FromNodeId,
            ToNodeId = segment.Segment.ToNodeId,
            EdgeId = segment.Segment.EdgeId,
            IsLocked = segment.IsLocked,
            IsReleased = segment.IsReleased,
            TrafficReservationIds = string.Join(", ", segment.TrafficReservationIds)
        }).ToArray()
    };

    private static MockSimulationVehicleRow ToMockSimulationVehicleRow(AgvDispatcher.Infrastructure.Mock.Simulation.MockVehicleRuntimeState vehicle) => new()
    {
        VehicleId = vehicle.VehicleId,
        State = vehicle.State.ToString(),
        StateDisplay = MockDisplayNames.ToDisplayName(vehicle.State.ToString()),
        CurrentNodeId = vehicle.CurrentNodeId,
        CurrentEdgeId = vehicle.CurrentEdgeId,
        CurrentTaskId = vehicle.CurrentTaskId,
        ReservationId = vehicle.ReservationId,
        PlanId = vehicle.PlanId,
        WaitRetryCount = vehicle.WaitRetryCount,
        ReplanCount = vehicle.ReplanCount
    };

    private static MockSimulationEventRow ToMockSimulationEventRow(
        AgvDispatcher.Infrastructure.Mock.Simulation.MockSimulationEvent simulationEvent,
        int sequence) => new()
    {
        Sequence = sequence,
        Tick = simulationEvent.Tick,
        TickGroup = $"Tick {simulationEvent.Tick}",
        TickGroupDisplay = $"第 {simulationEvent.Tick} tick",
        EventType = simulationEvent.EventType.ToString(),
        EventTypeDisplay = MockDisplayNames.ToDisplayName(simulationEvent.EventType.ToString()),
        VehicleId = simulationEvent.VehicleId,
        TaskId = simulationEvent.TaskId,
        FromNodeId = simulationEvent.FromNodeId,
        ToNodeId = simulationEvent.ToNodeId,
        ResourceId = simulationEvent.ResourceId,
        OldPlanId = simulationEvent.OldPlanId,
        NewPlanId = simulationEvent.NewPlanId,
        OldReservationId = simulationEvent.OldReservationId,
        NewReservationId = simulationEvent.NewReservationId,
        Reason = simulationEvent.Reason,
        Message = simulationEvent.Message
    };

    private static MockSimulationTaskRow ToMockSimulationTaskRow(
        AgvDispatcher.Infrastructure.Mock.Simulation.MockVehicleRuntimeState vehicle,
        TaskOrder? task) => new()
    {
        VehicleId = vehicle.VehicleId,
        TaskId = vehicle.CurrentTaskId ?? string.Empty,
        State = task?.State.ToString() ?? "Unknown",
        StateDisplay = MockDisplayNames.ToDisplayName(task?.State.ToString() ?? "Unknown"),
        SourceNodeId = task?.SourceNodeId ?? string.Empty,
        TargetNodeId = task?.TargetNodeId ?? vehicle.TargetNodeId ?? string.Empty,
        ProgressPercent = task?.ProgressPercent ?? 0
    };

    private static bool Matches(TaskRow row, string query) =>
        IsEmpty(query) || Contains(row.TaskId, query) || Contains(row.AssignedVehicleId, query) ||
        Contains(row.SourceNodeId, query) || Contains(row.TargetNodeId, query);

    private static bool Matches(VehicleRow row, string query) =>
        IsEmpty(query) || Contains(row.VehicleId, query) || Contains(row.VehicleCode, query) ||
        Contains(row.Name, query) || Contains(row.CurrentTaskId, query);

    private static bool Matches(TrafficResourceRow row, string query) =>
        IsEmpty(query) || Contains(row.ResourceId, query) || Contains(row.TaskId, query) ||
        Contains(row.ReservationId, query) || Contains(row.OccupiedByAgvId, query) || Contains(row.ReservedByAgvId, query);

    private static bool Matches(RouteReservationRow row, string query) =>
        IsEmpty(query) || Contains(row.ReservationId, query) || Contains(row.TaskId, query) ||
        Contains(row.VehicleId, query) || Contains(row.PlanId, query);

    private static bool Matches(OkapiProtocolTraceRecord row, string query) =>
        IsEmpty(query) || Contains(row.TaskId, query) || Contains(row.VehicleId, query) ||
        Contains(row.CommandType, query) || Contains(row.Url, query);

    private static bool Matches(MockSimulationVehicleRow row, string query) =>
        IsEmpty(query) || Contains(row.VehicleId, query) || Contains(row.CurrentTaskId, query) ||
        Contains(row.ReservationId, query) || Contains(row.PlanId, query) || Contains(row.CurrentNodeId, query);

    private static bool Matches(MockSimulationEventRow row, string query) =>
        IsEmpty(query) || Contains(row.EventType, query) || Contains(row.EventTypeDisplay, query) ||
        Contains(row.VehicleId, query) ||
        Contains(row.TaskId, query) || Contains(row.ResourceId, query) || Contains(row.Reason, query) ||
        Contains(row.OldReservationId, query) || Contains(row.NewReservationId, query) ||
        Contains(row.OldPlanId, query) || Contains(row.NewPlanId, query) ||
        Contains(row.FromNodeId, query) || Contains(row.ToNodeId, query);

    private static bool Matches(MockSimulationTaskRow row, string query) =>
        IsEmpty(query) || Contains(row.TaskId, query) || Contains(row.VehicleId, query) ||
        Contains(row.State, query) || Contains(row.StateDisplay, query) ||
        Contains(row.SourceNodeId, query) || Contains(row.TargetNodeId, query);

    private static bool IsEmpty(string query) => string.IsNullOrWhiteSpace(query);

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private bool CanRunMockSimulationCommand() =>
        !IsRefreshing && !IsMockSimulationRunActive && !IsMockSimulationRegressionActive &&
        !string.IsNullOrWhiteSpace(MockSimulationScenarioId);

    private bool CanRunMockRegressionCommand() =>
        !IsRefreshing && !IsMockSimulationRunActive && !IsMockSimulationRegressionActive;

    private bool CanRunSelectedMockVehicleCommand() =>
        CanRunMockSimulationCommand() && SelectedMockSimulationVehicle is not null;

    private bool MatchesMockEventFilters(MockSimulationEventRow row)
    {
        var vehicleMatches = SelectedMockEventVehicleFilter == "All" ||
            string.Equals(row.VehicleId, SelectedMockEventVehicleFilter, StringComparison.OrdinalIgnoreCase);
        var typeMatches = SelectedMockEventTypeFilter == "All" ||
            string.Equals(row.EventType, SelectedMockEventTypeFilter, StringComparison.OrdinalIgnoreCase);
        return vehicleMatches && typeMatches;
    }

    private void UpdateMockEventVehicleFilters(IReadOnlyList<MockSimulationEventRow> events)
    {
        var values = events
            .Where(row => !string.IsNullOrWhiteSpace(row.VehicleId))
            .Select(row => row.VehicleId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Prepend("All")
            .ToArray();
        Replace(MockSimulationEventVehicleFilters, values);
        if (!MockSimulationEventVehicleFilters.Contains(SelectedMockEventVehicleFilter))
        {
            _selectedMockEventVehicleFilter = "All";
            RaisePropertyChanged(nameof(SelectedMockEventVehicleFilter));
        }
    }

    private void SyncEditableMockSimulationOptions()
    {
        if (string.IsNullOrWhiteSpace(MockSimulationScenarioId) ||
            string.Equals(_mockSimulationEditableScenarioId, MockSimulationScenarioId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var options = _mockFleetSimulationEngine.CurrentScenario?.Options;
        if (options is null)
        {
            return;
        }

        _mockSimulationEditableScenarioId = MockSimulationScenarioId;
        MockReplanOnLockedResource = options.ReplanOnLockedResource;
        MockReplanOnBlockedResource = options.ReplanOnBlockedResource;
        MockPreferWaitingOverReplan = options.PreferWaitingOverReplan;
        MockMaxReplanCount = options.MaxReplanCount;
        MockWaitTimeoutSeconds = options.WaitTimeout.TotalSeconds;
        MockRetryIntervalSeconds = options.RetryInterval.TotalSeconds;
        MockMaxRetryCount = options.MaxRetryCount;
    }

    private void RaiseMockSimulationCommandStates()
    {
        LoadMockSimulationScenarioCommand.RaiseCanExecuteChanged();
        StartAllMockSimulationTasksCommand.RaiseCanExecuteChanged();
        StartSelectedMockSimulationTaskCommand.RaiseCanExecuteChanged();
        StepMockSimulationCommand.RaiseCanExecuteChanged();
        RunTenMockSimulationTicksCommand.RaiseCanExecuteChanged();
        RunMockSimulationUntilIdleCommand.RaiseCanExecuteChanged();
        StopMockSimulationRunCommand.RaiseCanExecuteChanged();
        CancelSelectedMockSimulationTaskCommand.RaiseCanExecuteChanged();
        InjectMockSimulationFaultCommand.RaiseCanExecuteChanged();
        RecoverMockSimulationVehicleCommand.RaiseCanExecuteChanged();
        DisableMockMapEdgeCommand.RaiseCanExecuteChanged();
        EnableMockMapEdgeCommand.RaiseCanExecuteChanged();
        BlockMockTrafficResourceCommand.RaiseCanExecuteChanged();
        UnblockMockTrafficResourceCommand.RaiseCanExecuteChanged();
        ApplyMockSimulationOptionsCommand.RaiseCanExecuteChanged();
        RunMockEightScenarioRegressionCommand.RaiseCanExecuteChanged();
        RunMockReplanRegressionCommand.RaiseCanExecuteChanged();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}

public sealed class TaskRow
{
    public string TaskId { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string StateDisplay { get; init; } = string.Empty;
    public string? AssignedVehicleId { get; init; }
    public string SourceNodeId { get; init; } = string.Empty;
    public string TargetNodeId { get; init; } = string.Empty;
    public string? CurrentNodeId { get; init; }
    public int ProgressPercent { get; init; }
    public string Priority { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? FailureReason { get; init; }
}

public sealed class VehicleRow
{
    public string VehicleId { get; init; } = string.Empty;
    public string VehicleCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string AdapterType { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string StateDisplay { get; init; } = string.Empty;
    public bool IsOnline { get; init; }
    public double? BatteryLevel { get; init; }
    public string Location { get; init; } = string.Empty;
    public string? CurrentTaskId { get; init; }
    public bool HasAlarm { get; init; }
    public string? ActiveAlarmCode { get; init; }
    public string? ActiveAlarmMessage { get; init; }
    public DateTime? ReportedAt { get; init; }
}

public sealed class TrafficResourceRow
{
    public string ResourceType { get; init; } = string.Empty;
    public string ResourceTypeDisplay { get; init; } = string.Empty;
    public string ResourceId { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string StateDisplay { get; init; } = string.Empty;
    public string? OccupiedByAgvId { get; init; }
    public string? ReservedByAgvId { get; init; }
    public string? TaskId { get; init; }
    public string? ReservationId { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset? ExpireAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class RouteReservationRow
{
    public string ReservationId { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public string VehicleId { get; init; } = string.Empty;
    public string PlanId { get; init; } = string.Empty;
    public string MapId { get; init; } = string.Empty;
    public string MapVersion { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string StateDisplay { get; init; } = string.Empty;
    public int RollingWindowSize { get; init; }
    public int SegmentCount { get; init; }
    public string CurrentWindow { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyList<RouteSegmentRow> Segments { get; init; } = Array.Empty<RouteSegmentRow>();
}

public sealed class RouteSegmentRow
{
    public int Sequence { get; init; }
    public string FromNodeId { get; init; } = string.Empty;
    public string ToNodeId { get; init; } = string.Empty;
    public string EdgeId { get; init; } = string.Empty;
    public bool IsLocked { get; init; }
    public bool IsReleased { get; init; }
    public string TrafficReservationIds { get; init; } = string.Empty;
}

public sealed class MockSimulationVehicleRow
{
    public string VehicleId { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string StateDisplay { get; init; } = string.Empty;
    public string CurrentNodeId { get; init; } = string.Empty;
    public string? CurrentEdgeId { get; init; }
    public string? CurrentTaskId { get; init; }
    public string? ReservationId { get; init; }
    public string? PlanId { get; init; }
    public int WaitRetryCount { get; init; }
    public int ReplanCount { get; init; }
}

public sealed class MockSimulationEventRow
{
    public int Sequence { get; init; }
    public long Tick { get; init; }
    public string TickGroup { get; init; } = string.Empty;
    public string TickGroupDisplay { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string EventTypeDisplay { get; init; } = string.Empty;
    public string? VehicleId { get; init; }
    public string? TaskId { get; init; }
    public string? FromNodeId { get; init; }
    public string? ToNodeId { get; init; }
    public string? ResourceId { get; init; }
    public string? OldPlanId { get; init; }
    public string? NewPlanId { get; init; }
    public string? OldReservationId { get; init; }
    public string? NewReservationId { get; init; }
    public string? Reason { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class MockSimulationTaskRow
{
    public string VehicleId { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string StateDisplay { get; init; } = string.Empty;
    public string SourceNodeId { get; init; } = string.Empty;
    public string TargetNodeId { get; init; } = string.Empty;
    public int ProgressPercent { get; init; }
}

public sealed class MockSimulationRegressionResultRow
{
    public string ScenarioName { get; init; } = string.Empty;
    public string ScenarioNameDisplay => MockDisplayNames.ToDisplayName(ScenarioName);
    public string Result { get; init; } = string.Empty;
    public string ResultDisplay => MockDisplayNames.ToDisplayName(Result);
    public string FailedReason { get; init; } = string.Empty;
    public string LastEvent { get; init; } = string.Empty;
}

public static class MockDisplayNames
{
    public static string ToDisplayName(string? value) => value switch
    {
        null or "" => string.Empty,
        "All" => "全部",
        "SameTarget" => "两车同终点",
        "NarrowAisle" => "相向窄道",
        "Intersection" => "路口占用",
        "AlternativeRoute" => "可替代路线",
        "CurrentNodeReplan" => "当前位置重规划",
        "Linear" => "线性路线",
        "TwoVehiclesSameTarget" => "两车同终点",
        "TwoVehiclesOppositeDirectionNarrowAisle" => "两车相向进入窄道",
        "LockedEdge" => "路径边被锁",
        "WaitingTimeout" => "等待超时",
        "CancelTask" => "任务取消",
        "FaultVehicle" => "车辆故障",
        "DisabledMapEdge" => "地图禁用路线",
        "DisabledMapEdge_WithAlternative" => "禁用边后绕行",
        "DisabledMapEdge_NoAlternative" => "禁用边且无替代路径",
        "LockedEdge_WithAlternative" => "锁边后绕行",
        "ExceedsMaxReplan" => "超过最大重规划次数",
        "Passed" => "通过",
        "Failed" => "失败",
        "Idle" => "空闲",
        "Dispatching" => "调度中",
        "Running" => "运行中",
        "WaitingForTraffic" => "等待交通资源",
        "Replanning" => "重规划中",
        "Completed" => "已完成",
        "Canceled" => "已取消",
        "Cancelled" => "已取消",
        "TimedOut" => "已超时",
        "Fault" => "故障",
        "Offline" => "离线",
        "Pending" => "待执行",
        "Unknown" => "未知",
        "Locked" => "已锁定",
        "Occupied" => "已占用",
        "Blocked" => "已阻塞",
        "Free" => "空闲",
        "Released" => "已释放",
        "Active" => "活动中",
        "Waiting" => "等待中",
        "Edge" => "路径边",
        "Node" => "点位",
        "Area" => "区域",
        "Path" => "路径",
        "HoldResources" => "保持资源",
        "ReleaseReservation" => "释放预约",
        "FailTaskAndRelease" => "任务失败并释放",
        "None" => "无",
        "VehicleInitialized" => "车辆初始化",
        "TaskDispatchRequested" => "请求任务调度",
        "TaskDispatchStarted" => "任务调度启动",
        "VehicleMoved" => "车辆移动",
        "ResourceLocked" => "资源锁定",
        "ResourceReleased" => "资源释放",
        "WaitingTimedOut" => "等待超时",
        "ReplanRequired" => "需要重规划",
        "TaskCompleted" => "任务完成",
        "TaskCanceled" => "任务取消",
        "VehicleFaulted" => "车辆故障",
        "RetryAttempted" => "已尝试重试",
        "VehicleRecovered" => "车辆恢复",
        "TaskReplanned" => "任务已重规划",
        "RouteInvalidated" => "路线失效",
        "ReplanFailed" => "重规划失败",
        "ReplanSkipped" => "跳过重规划",
        "SimulationFailed" => "仿真失败",
        "NoOp" => "无操作",
        _ => value
    };
}
