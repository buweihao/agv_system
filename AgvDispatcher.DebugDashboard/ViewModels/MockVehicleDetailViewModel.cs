using System.Collections.ObjectModel;
using System.Windows;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;
using AgvDispatcher.DebugDashboard.Services;
using Prism.Commands;
using Prism.Mvvm;

namespace AgvDispatcher.DebugDashboard.ViewModels;

public sealed class MockVehicleDetailViewModel : BindableBase, IDisposable
{
    private readonly IMockSimulationService _mockSimulationService;
    private readonly IMockScenarioController _mockScenarioController;
    private VehicleStatusSnapshot? _snapshot;
    private string _vehicleId;
    private string _brand = string.Empty;
    private RobotState _state = RobotState.Offline;
    private VehicleLoadState _loadState = VehicleLoadState.Unknown;
    private double _batteryLevel;
    private string _location = string.Empty;
    private string _currentTaskId = string.Empty;
    private bool _isOnline;
    private bool _isCharging;
    private bool _hasAlarm;
    private string _activeAlarmCode = string.Empty;
    private string _activeAlarmMessage = string.Empty;
    private DateTime? _reportedAt;
    private string _statusMessage = "Ready";

    public MockVehicleDetailViewModel(
        string vehicleId,
        IMockSimulationService mockSimulationService,
        IMockScenarioController mockScenarioController)
    {
        _vehicleId = vehicleId;
        _mockSimulationService = mockSimulationService;
        _mockScenarioController = mockScenarioController;
        RobotStates = Enum.GetValues<RobotState>();
        LoadStates = Enum.GetValues<VehicleLoadState>();

        ApplyCommand = new DelegateCommand(Apply);
        SetOnlineCommand = new DelegateCommand(() => Update(new MockVehicleUpdate { VehicleId = VehicleId, IsOnline = true }));
        SetOfflineCommand = new DelegateCommand(() => Update(new MockVehicleUpdate { VehicleId = VehicleId, IsOnline = false }));
        SetRunningCommand = new DelegateCommand(() => Update(new MockVehicleUpdate { VehicleId = VehicleId, State = RobotState.Running, IsOnline = true }));
        SetIdleCommand = new DelegateCommand(() => Update(new MockVehicleUpdate { VehicleId = VehicleId, State = RobotState.Idle, IsOnline = true }));
        SetFaultCommand = new DelegateCommand(() => Update(new MockVehicleUpdate
        {
            VehicleId = VehicleId,
            State = RobotState.Fault,
            IsOnline = true,
            HasAlarm = true,
            ActiveAlarmCode = string.IsNullOrWhiteSpace(ActiveAlarmCode) ? "MOCK_FAULT" : ActiveAlarmCode,
            ActiveAlarmMessage = string.IsNullOrWhiteSpace(ActiveAlarmMessage) ? "Mock fault" : ActiveAlarmMessage
        }));
        ClearFaultCommand = new DelegateCommand(() => Update(new MockVehicleUpdate { VehicleId = VehicleId, HasAlarm = false, State = RobotState.Idle, IsOnline = true }));
        ArriveNextNodeCommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.ArriveNextNodeAsync(VehicleId)).ConfigureAwait(true));
        RetryWaitingTaskCommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.RetryWaitingTaskAsync(VehicleId)).ConfigureAwait(true));
        ReleaseCurrentOccupancyCommand = new DelegateCommand(async () => await RunScenarioActionAsync(
            () => _mockScenarioController.ReleaseCurrentOccupancyAsync(VehicleId)).ConfigureAwait(true));

        _mockSimulationService.VehiclesChanged += OnVehiclesChanged;
        RefreshFromService();
    }

    public IReadOnlyList<RobotState> RobotStates { get; }

    public IReadOnlyList<VehicleLoadState> LoadStates { get; }

    public ObservableCollection<TelemetryRow> TelemetryRows { get; } = new();

    public DelegateCommand ApplyCommand { get; }

    public DelegateCommand SetOnlineCommand { get; }

    public DelegateCommand SetOfflineCommand { get; }

    public DelegateCommand SetRunningCommand { get; }

    public DelegateCommand SetIdleCommand { get; }

    public DelegateCommand SetFaultCommand { get; }

    public DelegateCommand ClearFaultCommand { get; }

    public DelegateCommand ArriveNextNodeCommand { get; }

    public DelegateCommand RetryWaitingTaskCommand { get; }

    public DelegateCommand ReleaseCurrentOccupancyCommand { get; }

    public string VehicleId
    {
        get => _vehicleId;
        private set => SetProperty(ref _vehicleId, value);
    }

    public string Brand
    {
        get => _brand;
        set => SetProperty(ref _brand, value ?? string.Empty);
    }

    public RobotState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    public VehicleLoadState LoadState
    {
        get => _loadState;
        set => SetProperty(ref _loadState, value);
    }

    public double BatteryLevel
    {
        get => _batteryLevel;
        set => SetProperty(ref _batteryLevel, value);
    }

    public string Location
    {
        get => _location;
        set => SetProperty(ref _location, value ?? string.Empty);
    }

    public string CurrentTaskId
    {
        get => _currentTaskId;
        set => SetProperty(ref _currentTaskId, value ?? string.Empty);
    }

    public bool IsOnline
    {
        get => _isOnline;
        set => SetProperty(ref _isOnline, value);
    }

    public bool IsCharging
    {
        get => _isCharging;
        set => SetProperty(ref _isCharging, value);
    }

    public bool HasAlarm
    {
        get => _hasAlarm;
        set => SetProperty(ref _hasAlarm, value);
    }

    public string ActiveAlarmCode
    {
        get => _activeAlarmCode;
        set => SetProperty(ref _activeAlarmCode, value ?? string.Empty);
    }

    public string ActiveAlarmMessage
    {
        get => _activeAlarmMessage;
        set => SetProperty(ref _activeAlarmMessage, value ?? string.Empty);
    }

    public DateTime? ReportedAt
    {
        get => _reportedAt;
        private set => SetProperty(ref _reportedAt, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public void Dispose()
    {
        _mockSimulationService.VehiclesChanged -= OnVehiclesChanged;
    }

    private void Apply()
    {
        Update(new MockVehicleUpdate
        {
            VehicleId = VehicleId,
            Brand = Brand,
            State = State,
            LoadState = LoadState,
            BatteryLevel = BatteryLevel,
            Location = Location,
            CurrentTaskId = CurrentTaskId,
            IsOnline = IsOnline,
            IsCharging = IsCharging,
            HasAlarm = HasAlarm,
            ActiveAlarmCode = ActiveAlarmCode,
            ActiveAlarmMessage = ActiveAlarmMessage,
            Telemetry = _snapshot?.Telemetry is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(_snapshot.Telemetry, StringComparer.OrdinalIgnoreCase)
        });
    }

    private void Update(MockVehicleUpdate update)
    {
        _mockSimulationService.UpsertVehicle(update);
        StatusMessage = $"Updated at {DateTime.Now:HH:mm:ss}";
    }

    private async Task RunScenarioActionAsync(Func<Task<MockScenarioOperationResult>> action)
    {
        try
        {
            var result = await action().ConfigureAwait(true);
            StatusMessage = result.Message;
            RefreshFromService();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void OnVehiclesChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            RefreshFromService();
            return;
        }

        dispatcher.BeginInvoke(RefreshFromService);
    }

    private void RefreshFromService()
    {
        var snapshot = _mockSimulationService.GetVehicle(VehicleId);
        if (snapshot is null)
        {
            StatusMessage = "Vehicle not found";
            return;
        }

        _snapshot = snapshot;
        VehicleId = snapshot.VehicleId;
        Brand = snapshot.Brand;
        State = snapshot.State;
        LoadState = snapshot.LoadState;
        BatteryLevel = snapshot.BatteryLevel;
        Location = snapshot.Location;
        CurrentTaskId = snapshot.CurrentTaskId ?? string.Empty;
        IsOnline = snapshot.IsOnline;
        IsCharging = snapshot.IsCharging;
        HasAlarm = snapshot.HasAlarm;
        ActiveAlarmCode = snapshot.ActiveAlarmCode ?? string.Empty;
        ActiveAlarmMessage = snapshot.ActiveAlarmMessage ?? string.Empty;
        ReportedAt = snapshot.ReportedAt;

        TelemetryRows.Clear();
        foreach (var row in snapshot.Telemetry.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            TelemetryRows.Add(new TelemetryRow { Key = row.Key, Value = row.Value });
        }
    }
}

public sealed class TelemetryRow
{
    public string Key { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}
