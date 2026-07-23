using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Shell.Services;
using Prism.Events;
using Xunit;

namespace AgvDispatcher.Tests.Shell;

public sealed class VehicleStateStoreTests
{
    [Fact]
    public void UpsertAndRead_DoNotExposeMutablePositionReference()
    {
        var store = new VehicleStateStore(new EventAggregator());
        var source = new VehicleStatusSnapshot
        {
            VehicleId = "AGV-CLONE",
            State = RobotState.Running,
            Position = new MapPosition { MapId = "MAIN", X = 12, Y = 34 }
        };

        store.UpsertStatus(source);
        source.Position.X = 999;
        var firstRead = store.GetVehicle(source.VehicleId)!;
        firstRead.Position.Y = 888;
        var secondRead = store.GetVehicle(source.VehicleId)!;

        Assert.Equal(12, secondRead.Position.X);
        Assert.Equal(34, secondRead.Position.Y);
        Assert.NotSame(firstRead.Position, secondRead.Position);
    }
}
