namespace AgvDispatcher.Core.Enums
{
    public enum DispatchCommandType
    {
        AssignTask,
        CompleteTask,
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
