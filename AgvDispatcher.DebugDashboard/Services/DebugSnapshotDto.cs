using AgvDispatcher.Core.Contracts.Dispatching.Models;
using AgvDispatcher.Core.Contracts.Reservations.Models;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Okapi;

namespace AgvDispatcher.DebugDashboard.Services;

public sealed class DebugSnapshotDto
{
    public DateTimeOffset GeneratedAt { get; init; }

    public IReadOnlyList<TaskOrder> Tasks { get; init; } = Array.Empty<TaskOrder>();

    public IReadOnlyList<Vehicle> Vehicles { get; init; } = Array.Empty<Vehicle>();

    public IReadOnlyList<VehicleStatus> VehicleStatuses { get; init; } = Array.Empty<VehicleStatus>();

    public IReadOnlyList<TrafficResourceStatusDto> TrafficResources { get; init; } = Array.Empty<TrafficResourceStatusDto>();

    public IReadOnlyList<RouteReservationDto> RouteReservations { get; init; } = Array.Empty<RouteReservationDto>();

    public IReadOnlyList<DispatchExecutionDto> DispatchExecutions { get; init; } = Array.Empty<DispatchExecutionDto>();

    public IReadOnlyList<OkapiProtocolTraceRecord> OkapiProtocolRecords { get; init; } = Array.Empty<OkapiProtocolTraceRecord>();

    public IReadOnlyList<string> DiagnosticMessages { get; init; } = Array.Empty<string>();
}
