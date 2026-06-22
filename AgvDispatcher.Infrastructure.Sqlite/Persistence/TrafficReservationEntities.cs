namespace AgvDispatcher.Infrastructure.Sqlite.Persistence;

public sealed class TrafficResourceLockEntity
{
    public long Id { get; set; }
    public int ResourceType { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public int State { get; set; }
    public string? OccupiedByAgvId { get; set; }
    public string? ReservedByAgvId { get; set; }
    public string? TaskId { get; set; }
    public string? RouteReservationId { get; set; }
    public string? TrafficReservationId { get; set; }
    public int? LockMode { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? ExpireAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long ConcurrencyToken { get; set; }
}

public sealed class RouteReservationEntity
{
    public string ReservationId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string MapVersion { get; set; } = string.Empty;
    public int RollingWindowSize { get; set; }
    public int ReservationPolicy { get; set; }
    public int State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? LastFailureReason { get; set; }
    public List<RouteReservationSegmentEntity> Segments { get; set; } = new();
}

public sealed class RouteReservationSegmentEntity
{
    public long Id { get; set; }
    public string ReservationId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string EdgeId { get; set; } = string.Empty;
    public double Distance { get; set; }
    public double Cost { get; set; }
    public bool IsReserved { get; set; }
    public bool IsLocked { get; set; }
    public bool IsReleased { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string TrafficReservationIdsJson { get; set; } = "[]";
    public string ResourcesJson { get; set; } = "[]";
    public RouteReservationEntity? Reservation { get; set; }
}

public sealed class RouteReservationEventEntity
{
    public string EventId { get; set; } = string.Empty;
    public string ReservationId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? SnapshotJson { get; set; }
}
