using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IPathPlanningService
    {
        PlannedPath PlanPath(
            IReadOnlyList<MapNode> nodes,
            IReadOnlyList<MapEdge> edges,
            string startNodeId,
            string endNodeId);

        /// <summary>
        /// 按约束条件规划路径：在起止点之外考虑车辆品牌、能力以及对禁用/锁定/封闭通路的处理策略。
        /// </summary>
        /// <param name="nodes">候选节点集合。</param>
        /// <param name="edges">候选边集合。</param>
        /// <param name="request">路径规划请求（见 <see cref="PathPlanningRequest"/>）。</param>
        /// <returns>满足约束的最优 <see cref="PlannedPath"/>；不可达时返回不可用结果。</returns>
        PlannedPath PlanPath(
            IReadOnlyList<MapNode> nodes,
            IReadOnlyList<MapEdge> edges,
            PathPlanningRequest request);
    }
}
