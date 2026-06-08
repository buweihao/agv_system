namespace AgvDispatcher.Core.Rules
{
    public static class VehicleStatusRules
    {
        public const double LowBatteryThreshold = 20;

        public static bool IsLowBattery(double batteryLevel)
        {
            return batteryLevel < LowBatteryThreshold;
        }
    }
}
