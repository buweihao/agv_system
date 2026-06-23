namespace AgvDispatcher.DebugDashboard.Services;

public interface IDebugSnapshotService
{
    Task<DebugSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
