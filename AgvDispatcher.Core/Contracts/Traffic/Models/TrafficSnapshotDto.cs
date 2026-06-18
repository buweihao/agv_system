using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Represents a consistent snapshot of runtime traffic resource states.
    /// 表示运行时交通资源状态的一致快照。
    /// </summary>
    public sealed class TrafficSnapshotDto
    {
        /// <summary>
        /// Gets the map version used by the traffic snapshot.
        /// 获取交通快照使用的地图版本。
        /// </summary>
        public string MapVersion { get; init; } = string.Empty;

        /// <summary>
        /// Gets the monotonically increasing traffic snapshot version.
        /// 获取单调递增的交通快照版本。
        /// </summary>
        public long Version { get; init; }

        /// <summary>
        /// Gets the time when the snapshot was generated.
        /// 获取生成快照的时间。
        /// </summary>
        public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the resource statuses included in the snapshot.
        /// 获取快照中包含的资源状态。
        /// </summary>
        public IReadOnlyList<TrafficResourceStatusDto> Resources { get; init; } = Array.Empty<TrafficResourceStatusDto>();
    }
}
