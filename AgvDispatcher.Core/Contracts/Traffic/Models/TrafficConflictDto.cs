using AgvDispatcher.Core.Contracts.Traffic.Enums;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Describes one conflict found during a traffic availability or acquisition check.
    /// 描述在交通可用性或获取检查期间发现的一个冲突。
    /// </summary>
    public sealed class TrafficConflictDto
    {
        /// <summary>
        /// Gets the conflicted resource.
        /// 获取发生冲突的资源。
        /// </summary>
        public TrafficResourceKey Resource { get; init; } = new TrafficResourceKey();

        /// <summary>
        /// Gets the current state that caused the conflict.
        /// 获取导致冲突的当前状态。
        /// </summary>
        public TrafficResourceState CurrentState { get; init; } = TrafficResourceState.Unknown;

        /// <summary>
        /// Gets the conflicting AGV identifier, if known.
        /// 获取冲突的 AGV 标识符（如果已知）。
        /// </summary>
        public string? ConflictAgvId { get; init; }

        /// <summary>
        /// Gets the conflicting task identifier, if known.
        /// 获取冲突的任务标识符（如果已知）。
        /// </summary>
        public string? ConflictTaskId { get; init; }

        /// <summary>
        /// Gets the conflicting reservation identifier, if known.
        /// 获取冲突的预留标识符（如果已知）。
        /// </summary>
        public string? ReservationId { get; init; }

        /// <summary>
        /// Gets the traffic-specific failure code.
        /// 获取特定于交通的失败代码。
        /// </summary>
        public TrafficFailureCode FailureCode { get; init; } = TrafficFailureCode.None;

        /// <summary>
        /// Gets an optional conflict message.
        /// 获取可选的冲突消息。
        /// </summary>
        public string? Message { get; init; }
    }
}
