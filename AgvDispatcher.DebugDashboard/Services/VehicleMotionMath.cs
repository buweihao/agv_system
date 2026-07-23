using AgvDispatcher.Core.Models;

namespace AgvDispatcher.DebugDashboard.Services;

public static class VehicleMotionMath
{
    public static MapPosition Interpolate(MapPosition start, MapPosition end, double progress)
    {
        var value = Math.Clamp(progress, 0, 1);
        return new MapPosition
        {
            MapId = string.IsNullOrWhiteSpace(end.MapId) ? start.MapId : end.MapId,
            X = start.X + ((end.X - start.X) * value),
            Y = start.Y + ((end.Y - start.Y) * value),
            Z = start.Z + ((end.Z - start.Z) * value),
            Heading = CalculateHeading(start, end),
            NodeId = value >= 1 ? end.NodeId : start.NodeId,
            AreaCode = value >= 1 ? end.AreaCode : start.AreaCode
        };
    }

    public static double CalculateHeading(MapPosition start, MapPosition end)
    {
        var degrees = Math.Atan2(end.Y - start.Y, end.X - start.X) * 180.0 / Math.PI;
        return degrees < 0 ? degrees + 360 : degrees;
    }
}
