using System;

namespace AgvDispatcher.Core.Events
{
    /// <summary>
    /// 地图发布事件。
    /// </summary>
    /// <remarks>
    /// 当一张地图经过编辑、校验并发布为当前运行地图后触发。
    /// 
    /// 注意：
    /// 这个事件代表“运行地图已经切换”，不是普通编辑草稿保存。
    /// 
    /// 订阅方可以包括：
    /// 1. IMapService：刷新当前地图快照；
    /// 2. IPathPlanner：清空或重建路径图缓存；
    /// 3. ITrafficControlService：重建可管制资源；
    /// 4. 监控模块：刷新地图显示；
    /// 5. 日志模块：记录地图发布操作。
    /// </remarks>
    public sealed class MapPublishedEvent : IDomainEvent
    {
        /// <summary>
        /// 地图编号。
        /// </summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>
        /// 地图版本号。
        /// </summary>
        public long MapVersion { get; init; }

        /// <summary>
        /// 发布时间。
        /// </summary>
        public DateTime PublishedAt { get; init; }

        /// <summary>
        /// 发布人。
        /// 可以是操作员 ID，也可以是系统账号。
        /// </summary>
        public string? OperatorId { get; init; }

        /// <summary>
        /// 事件发生时间。
        /// </summary>
        public DateTime OccurredAt { get; init; } = DateTime.Now;
    }
}
