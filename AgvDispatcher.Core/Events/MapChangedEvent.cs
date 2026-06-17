using System;

namespace AgvDispatcher.Core.Events
{
    /// <summary>
    /// 地图数据变更事件。
    /// </summary>
    /// <remarks>
    /// 当地图内容被编辑、修改或保存但尚未正式发布时触发。
    /// </remarks>
    public sealed class MapChangedEvent : IDomainEvent
    {
        /// <summary>
        /// 地图编号。
        /// </summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>
        /// 事件发生时间。
        /// </summary>
        public DateTime OccurredAt { get; init; } = DateTime.Now;
    }
}
