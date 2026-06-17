using AgvDispatcher.Core.Enums;
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

        /// <summary>
        /// 根据边编号获取单条边（路径）。
        /// </summary>
        /// <param name="edgeId">边唯一编号（<see cref="MapEdge.EdgeId"/>）。</param>
        /// <returns>匹配的 <see cref="MapEdge"/>；若不存在该编号的边则返回 <c>null</c>。</returns>
        MapEdge? GetEdge(string edgeId);

        /// <summary>
        /// 判断指定编号的节点是否存在于当前地图。
        /// </summary>
        /// <param name="nodeId">节点编号。</param>
        /// <returns>存在返回 <c>true</c>；否则返回 <c>false</c>。编号比较不区分大小写。</returns>
        bool NodeExists(string nodeId);

        /// <summary>
        /// 计算从起点到终点的最优路径总里程。
        /// </summary>
        /// <param name="startNodeId">起始节点编号。</param>
        /// <param name="endNodeId">目标节点编号。</param>
        /// <returns>
        /// 可达时返回路径总里程（米，等同 <see cref="PlannedPath.TotalLength"/>）；
        /// 起止点不存在或不可达时返回 <c>-1</c>。
        /// </returns>
        double GetPathDistance(string startNodeId, string endNodeId);

        /// <summary>
        /// 查找从指定起点出发、沿当前可通行拓扑可到达的全部节点。
        /// </summary>
        /// <param name="startNodeId">起始节点编号。</param>
        /// <returns>
        /// 可达节点的只读列表（含起点本身）；起点不存在或被禁用时返回空列表。
        /// 遍历遵循边的启用/锁定/封闭状态与方向，与路径规划使用相同的可通行规则。
        /// </returns>
        IReadOnlyList<MapNode> FindReachableNodes(string startNodeId);

        /// <summary>
        /// 按节点类型筛选节点。
        /// </summary>
        /// <param name="nodeType">目标节点类型（如取货点 <see cref="MapNodeType.Pickup"/>、充电点 <see cref="MapNodeType.Charge"/>）。</param>
        /// <returns>该类型的全部节点只读列表；无匹配时返回空列表。</returns>
        IReadOnlyList<MapNode> FindNodesByType(MapNodeType nodeType);

        /// <summary>
        /// 按约束条件计算完整规划路径。
        /// </summary>
        /// <param name="request">
        /// 路径规划请求，除起止点外可携带车辆品牌、能力以及对禁用/锁定/封闭通路的处理策略
        /// （见 <see cref="PathPlanningRequest"/>）。
        /// </param>
        /// <returns>
        /// <see cref="PlannedPath"/>；不满足约束（品牌不允许、能力不足等）的节点与边会被排除，
        /// 不可达时返回 <see cref="PlannedPath.Unavailable"/> 形式的结果。
        /// </returns>
        /// <remarks>
        /// 这是 <see cref="FindPlannedPath(string,string)"/> 的约束感知版本，供需要按车辆品牌/能力
        /// 规划路线的场景使用；无约束场景仍可使用双参数重载。
        /// </remarks>
        PlannedPath FindPlannedPath(PathPlanningRequest request);

        /// <summary>
        /// 将厂商（外部系统）使用的点位别名解析为系统内部节点编号。
        /// </summary>
        /// <param name="aliasValue">厂商点位别名值（<see cref="MapLocationAlias.AliasValue"/>）。</param>
        /// <param name="brand">
        /// 车辆/厂商品牌；指定时优先匹配该品牌限定的别名，未命中再回退到全局（无品牌）别名。
        /// 为 <c>null</c> 或空时仅匹配全局别名。
        /// </param>
        /// <returns>解析得到的系统节点编号（<see cref="MapNode.NodeId"/>）；无匹配的启用别名时返回 <c>null</c>。</returns>
        string? ResolveNodeIdByAlias(string aliasValue, string? brand = null);

        /// <summary>
        /// 获取某系统节点对应的全部点位别名。
        /// </summary>
        /// <param name="nodeId">系统节点编号。</param>
        /// <returns>该节点的启用别名只读列表（可含不同品牌的多个映射）；无别名时返回空列表。</returns>
        IReadOnlyList<MapLocationAlias> GetAliasesForNode(string nodeId);
    }
}
