using System;

namespace AgvDispatcher.Core.Events
{
    /// <summary>
    /// 领域事件基础接口。
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>
        /// 事件发生时间。
        /// </summary>
        DateTime OccurredAt { get; }
    }
}
