using System;

namespace AgvDispatcher.Core.Enums
{
    [Flags]
    public enum VehicleCommandCapability
    {
        None = 0,
        AssignTask = 1 << 0,
        MoveToNode = 1 << 1,
        Pause = 1 << 2,
        Resume = 1 << 3,
        CancelTask = 1 << 4,
        EmergencyStop = 1 << 5,
        ResetFault = 1 << 6,
        GoCharge = 1 << 7,
        StopCharge = 1 << 8
    }
}
