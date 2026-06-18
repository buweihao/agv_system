using AgvDispatcher.Core.Contracts.Traffic.Enums;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Identifies one traffic-controlled runtime resource.
    /// 标识一个受交通控制的运行时资源。
    /// </summary>
    public sealed class TrafficResourceKey
    {
        /// <summary>
        /// Gets the type of the traffic resource.
        /// 获取交通资源的类型。
        /// </summary>
        public TrafficResourceType ResourceType { get; init; }

        /// <summary>
        /// Gets the resource identifier within its type.
        /// 获取资源在其类型中的标识符。
        /// </summary>
        public string ResourceId { get; init; } = string.Empty;
    }
}
