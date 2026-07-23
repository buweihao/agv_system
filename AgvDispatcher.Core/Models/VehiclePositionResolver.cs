namespace AgvDispatcher.Core.Models;

/// <summary>
/// Resolves the position rendered for a vehicle. A reported continuous position
/// takes precedence over the logical node fallback.
/// </summary>
public static class VehiclePositionResolver
{
    public static MapPosition? Resolve(
        VehicleStatusSnapshot snapshot,
        MapPosition? logicalNodePosition)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Position?.HasValidCoordinates() == true)
        {
            return snapshot.Position.Clone();
        }

        return logicalNodePosition?.HasValidCoordinates() == true
            ? logicalNodePosition.Clone()
            : null;
    }
}
