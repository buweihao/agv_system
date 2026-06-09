namespace AgvDispatcher.Core.Enums
{
    public enum DispatchCommandState
    {
        Created,
        Sent,
        Accepted,
        Executing,
        Completed,
        Rejected,
        Failed,
        Cancelled,
        Timeout
    }
}
