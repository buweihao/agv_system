using AgvDispatcher.Core.Models;
using Xunit;

namespace AgvDispatcher.Tests.Map;

public sealed class VehiclePositionResolverTests
{
    [Fact]
    public void ContinuousPosition_TakesPrecedenceOverLogicalNode()
    {
        var snapshot = new VehicleStatusSnapshot
        {
            VehicleId = "AGV-001",
            Position = new MapPosition { MapId = "MAIN", X = 0, Y = 0, Heading = 45 }
        };
        var node = new MapPosition { MapId = "MAIN", X = 100, Y = 200 };

        var result = VehiclePositionResolver.Resolve(snapshot, node);

        Assert.NotNull(result);
        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(45, result.Heading);
    }

    [Fact]
    public void InvalidContinuousPosition_FallsBackToLogicalNode()
    {
        var snapshot = new VehicleStatusSnapshot
        {
            VehicleId = "AGV-001",
            Position = new MapPosition { X = 5, Y = 6 }
        };
        var node = new MapPosition { MapId = "MAIN", X = 100, Y = 200 };

        var result = VehiclePositionResolver.Resolve(snapshot, node);

        Assert.NotNull(result);
        Assert.Equal(100, result.X);
        Assert.Equal(200, result.Y);
    }
}
