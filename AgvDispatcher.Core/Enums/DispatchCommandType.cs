namespace AgvDispatcher.Core.Enums
{
    public enum DispatchCommandType
    {
        AssignTask,
        MoveToNode,
        Pause,
        Resume,
        CancelTask,
        ReturnHome,
        GoCharge,
        StopCharge,
        EmergencyStop,
        ResetFault,
        LockTrafficArea,
        ReleaseTrafficArea
    }
}
