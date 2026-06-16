using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    /// <summary>
    /// 地图服务接口。
    /// <para>
    /// 提供 AGV 调度系统中地图拓扑（节点 <see cref="MapNode"/> 与边 <see cref="MapEdge"/>）的查询能力，
    /// 以及基于该拓扑的路径规划与可达性判断。是调度引擎、任务派发、监控可视化等上层模块
    /// 获取地图数据和计算行驶路线的统一入口。
    /// </para>
    /// <para>
    /// 实现类（如持久化实现 <c>PersistentMapService</c>）通常会从仓储加载地图数据，
    /// 并委托 <see cref="IPathPlanningService"/>（如 Dijkstra 算法）完成路径计算。
    /// </para>
    /// </summary>
    public interface IMapService
    {
        /// <summary>
        /// 获取地图中的全部节点。
        /// </summary>
        /// <returns>
        /// 当前地图所有 <see cref="MapNode"/> 的只读列表；若地图为空则返回空列表（而非 null）。
        /// 节点包含坐标、类型（站点/充电点等）、品牌/能力限制及启用状态等信息。
        /// </returns>
        IReadOnlyList<MapNode> GetNodes();

        /// <summary>
        /// 获取地图中的全部边（路径）。
        /// </summary>
        /// <returns>
        /// 当前地图所有 <see cref="MapEdge"/> 的只读列表；若无边则返回空列表（而非 null）。
        /// 每条边表示连接两个节点的有向通路，可携带方向、品牌/能力限制及启用状态。
        /// </returns>
        IReadOnlyList<MapEdge> GetEdges();

        /// <summary>
        /// 根据节点编号获取单个节点。
        /// </summary>
        /// <param name="nodeId">节点唯一编号（<see cref="MapNode.NodeId"/>，如 <c>A1</c>、<c>CHG-01</c>）。</param>
        /// <returns>匹配的 <see cref="MapNode"/>；若不存在该编号的节点则返回 <c>null</c>。</returns>
        MapNode? GetNode(string nodeId);

        /// <summary>
        /// 计算从起点到终点的最优路径，仅返回途经节点序列。
        /// </summary>
        /// <param name="startNodeId">起始节点编号。</param>
        /// <param name="endNodeId">目标节点编号。</param>
        /// <returns>
        /// 从起点到终点按顺序排列的节点只读列表（含起点与终点）；
        /// 若起止点不存在或两点之间不可达，则返回空列表。
        /// </returns>
        /// <remarks>
        /// 这是 <see cref="FindPlannedPath"/> 的轻量版本，只关心“走哪些点”，
        /// 不返回距离、边详情等附加信息。需要完整路径信息时请使用 <see cref="FindPlannedPath"/>。
        /// </remarks>
        IReadOnlyList<MapNode> FindPath(string startNodeId, string endNodeId);

        /// <summary>
        /// 计算从起点到终点的完整规划路径。
        /// </summary>
        /// <param name="startNodeId">起始节点编号。</param>
        /// <param name="endNodeId">目标节点编号。</param>
        /// <returns>
        /// 一个 <see cref="PlannedPath"/> 对象，包含途经节点、途经边、总里程（<see cref="PlannedPath.TotalLength"/>）、
        /// 是否可达（<see cref="PlannedPath.IsAvailable"/>）以及说明信息（<see cref="PlannedPath.Message"/>）。
        /// 当不可达时返回 <see cref="PlannedPath.Unavailable"/> 形式的结果，而非 null。
        /// </returns>
        /// <remarks>
        /// 调度引擎据此评估任务路线是否成立、计算行驶成本并下发指令；
        /// 路径计算会考虑节点/边的启用状态、品牌与能力限制（被禁用或不满足约束的节点/边视为不可通行）。
        /// </remarks>
        PlannedPath FindPlannedPath(string startNodeId, string endNodeId);

        /// <summary>
        /// 判断从起点到终点是否存在可行路径。
        /// </summary>
        /// <param name="startNodeId">起始节点编号。</param>
        /// <param name="endNodeId">目标节点编号。</param>
        /// <returns>存在可达路径返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        /// <remarks>
        /// 用于派发前的快速可达性校验（例如 <c>AdapterDispatchService</c> 在分配任务前判断路线是否成立），
        /// 比完整路径计算更轻量；如需路径细节请改用 <see cref="FindPlannedPath"/>。
        /// </remarks>
        bool IsPathAvailable(string startNodeId, string endNodeId);
    }
}
