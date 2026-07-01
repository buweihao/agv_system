using System.Collections.ObjectModel;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;
using AgvDispatcher.Core.Contracts.Dispatching.Models;
using AgvDispatcher.Core.Contracts.Reservations.Models;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;
using AgvDispatcher.DebugDashboard.Services;
using AgvDispatcher.Infrastructure.Okapi;
using Prism.Commands;
using Prism.Mvvm;
using System.Windows.Threading;

namespace AgvDispatcher.DebugDashboard.ViewModels;

public sealed class DebugDashboardViewModel : BindableBase, IDisposable
{
    private readonly IDebugSnapshotService _debugSnapshotService;
    private readonly IMockVehicleWindowService _mockVehicleWindowService;
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

    public DebugDashboardViewModel(
        IDebugSnapshotService debugSnapshotService,
        IMockVehicleWindowService mockVehicleWindowService)
    {
        _debugSnapshotService = debugSnapshotService;
        _mockVehicleWindowService = mockVehicleWindowService;
        RefreshCommand = new DelegateCommand(async () => await RefreshAsync().ConfigureAwait(true), () => !IsRefreshing);
        OpenVehicleWindowCommand = new DelegateCommand<VehicleRow?>(OpenVehicleWindow);
        RefreshIntervals = new[] { 1, 2, 5 };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_selectedRefreshIntervalSeconds) };
        _timer.Tick += async (_, _) => await RefreshAsync().ConfigureAwait(true);
        _timer.Start();
        _ = RefreshAsync();
    }

    public DelegateCommand RefreshCommand { get; }

    public DelegateCommand<VehicleRow?> OpenVehicleWindowCommand { get; }

    public IReadOnlyList<int> RefreshIntervals { get; }

    public ObservableCollection<TaskRow> Tasks { get; } = new();

    public ObservableCollection<VehicleRow> Vehicles { get; } = new();

    public ObservableCollection<TrafficResourceRow> TrafficResources { get; } = new();

    public ObservableCollection<RouteReservationRow> RouteReservations { get; } = new();

    public ObservableCollection<RouteSegmentRow> SelectedReservationSegments { get; } = new();

    public ObservableCollection<DispatchExecutionDto> SelectedTaskExecutions { get; } = new();

    public ObservableCollection<RouteReservationRow> SelectedTaskReservations { get; } = new();

    public ObservableCollection<OkapiProtocolTraceRecord> OkapiProtocolRecords { get; } = new();

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
        }
    }

    public void Dispose()
    {
        _timer.Stop();
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

        Replace(Tasks, taskRows);
        Replace(Vehicles, vehicleRows);
        Replace(TrafficResources, trafficRows);
        Replace(RouteReservations, reservationRows);
        Replace(OkapiProtocolRecords, okapiRows);

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
        ResourceId = resource.Resource.ResourceId,
        State = resource.State.ToString(),
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
        RollingWindowSize = reservation.RollingWindowSize,
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

    private static bool IsEmpty(string query) => string.IsNullOrWhiteSpace(query);

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

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
    public string ResourceId { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
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
    public int RollingWindowSize { get; init; }
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
