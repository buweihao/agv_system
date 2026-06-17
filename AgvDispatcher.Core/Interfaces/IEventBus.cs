using System.Threading.Tasks;
using AgvDispatcher.Core.Events;

namespace AgvDispatcher.Core.Interfaces
{
    /// <summary>
    /// 领域事件总线。
    /// 提供统一的接口来进行核心领域事件的发布和订阅机制封装。
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// 同步发布事件。
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="domainEvent">事件对象</param>
        void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;

        /// <summary>
        /// 异步发布事件。
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="domainEvent">事件对象</param>
        /// <returns>Task</returns>
        Task PublishAsync<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
    }
}
