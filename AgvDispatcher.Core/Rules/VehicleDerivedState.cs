using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Rules
{
    public static class VehicleDerivedState
    {
        public static bool IsLowBattery(VehicleStatusSnapshot snapshot)
        {
            return VehicleStatusRules.IsLowBattery(snapshot.BatteryLevel);
        }

        public static bool IsOffline(VehicleStatusSnapshot snapshot)
        {
            return snapshot.State == RobotState.Offline;
        }
    }
}
