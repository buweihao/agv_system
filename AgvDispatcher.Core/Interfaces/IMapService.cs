using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    /// <summary>
    /// 地图查询与路径规划服务。
    /// 
    /// 该接口主要提供给调度模块、监控模块、地图展示模块使用。
    /// 它负责读取地图静态数据，并基于地图数据进行路径查询、可达性判断和距离计算。
    /// 
    /// 注意：
    /// 1. 该接口偏“查询/规划”，不建议承担地图编辑保存职责。
    /// 2. 地图节点、路径边的新增、修改、删除建议放到 IMapManagementService 或 IMapRepository。
    /// 3. 动态交通管制、路径占用、区域锁定建议后续单独放到 ITrafficControlService 或 IRouteReservationService。
    /// </summary>
    public interface IMapService
    {
        /// <summary>
        /// 获取当前地图中的全部节点。
        /// 
        /// 需求说明：
        /// 调度系统需要知道地图上有哪些可用点位，例如取货点、放货点、充电点、等待点、路口点等。
        /// 地图展示界面也需要通过该接口加载所有节点并显示在画布上。
        /// 
        /// 使用场景：
        /// 1. 地图可视化展示。
        /// 2. 创建任务时选择起点、终点。
        /// 3. 调度评分时判断任务点位是否存在。
        /// 4. 路径规划前加载节点集合。
        /// </summary>
        /// <returns>当前地图中的所有 MapNode。</returns>
        IReadOnlyList<MapNode> GetNodes();

        /// <summary>
        /// 获取当前地图中的全部路径边。
        /// 
        /// 需求说明：
        /// 路径边表示 AGV 可以行驶的路线，例如 A 点到 B 点之间是否可通行、是否单向、距离多少、是否锁定。
        /// 调度系统和路径规划算法需要根据边数据构建路网。
        /// 
        /// 使用场景：
        /// 1. 地图可视化展示节点之间的连线。
        /// 2. 路径规划时构建图结构。
        /// 3. 判断某条路径是否被禁用、关闭或锁定。
        /// 4. 后续交通管制模块判断边是否可占用。
        /// </summary>
        /// <returns>当前地图中的所有 MapEdge。</returns>
        IReadOnlyList<MapEdge> GetEdges();

        /// <summary>
        /// 根据节点编号获取单个地图节点。
        /// 
        /// 需求说明：
        /// 调度系统经常需要根据任务中的 SourceNodeId、TargetNodeId 查询对应节点，
        /// 用于判断点位是否存在、点位类型是否正确、点位是否允许某类车辆进入。
        /// 
        /// 使用场景：
        /// 1. 创建任务时校验起点和终点。
        /// 2. 调度派发前读取节点属性，例如 NodeType、AreaCode、AllowedBrands。
        /// 3. 地图界面点击某个节点后显示详细信息。
        /// </summary>
        /// <param name="nodeId">系统内部节点编号，例如 A1、B2、Charge-1。</param>
        /// <returns>找到则返回 MapNode；找不到则返回 null。</returns>
        MapNode? GetNode(string nodeId);

        /// <summary>
        /// 根据路径边编号获取单条路径边。
        /// 
        /// 需求说明：
        /// 调度或地图界面有时需要查询某一条边的详细信息，例如方向、长度、是否启用、是否锁定、允许品牌等。
        /// 
        /// 使用场景：
        /// 1. 地图编辑界面选中一条路径边后显示属性。
        /// 2. 路径规划结果中根据 EdgeId 查询边详情。
        /// 3. 后续交通管制模块查看某条边是否被锁定或占用。
        /// </summary>
        /// <param name="edgeId">路径边编号。</param>
        /// <returns>找到则返回 MapEdge；找不到则返回 null。</returns>
        MapEdge? GetEdge(string edgeId);

        /// <summary>
        /// 判断指定节点是否存在。
        /// 
        /// 需求说明：
        /// 调度系统在创建任务、导入任务、派发任务前，需要快速判断 SourceNodeId 和 TargetNodeId 是否为合法地图点位。
        /// 
        /// 使用场景：
        /// 1. 创建任务前校验起点、终点是否合法。
        /// 2. 导入任务模板时校验点位是否存在。
        /// 3. 厂商回传点位转换后校验是否能映射到系统节点。
        /// </summary>
        /// <param name="nodeId">系统内部节点编号。</param>
        /// <returns>存在返回 true；不存在返回 false。</returns>
        bool NodeExists(string nodeId);

        /// <summary>
        /// 根据起点和终点规划路径。
        /// 
        /// 需求说明：
        /// 这是最基础的路径规划接口。
        /// 调度模块需要知道从任务起点到任务终点是否可达，以及可达时应该经过哪些节点和路径边。
        /// 
        /// 使用场景：
        /// 1. 派发任务前判断 source 到 target 是否可达。
        /// 2. 地图界面进行路径预览。
        /// 3. 调度记录中保存本次任务规划路径。
        /// 
        /// 注意：
        /// 该方法只接收起点和终点，不包含车辆品牌、车辆能力、避让锁定边等复杂条件。
        /// 如果需要考虑车辆约束，应使用 FindPlannedPath(PathPlanningRequest request)。
        /// </summary>
        /// <param name="startNodeId">起点节点编号。</param>
        /// <param name="endNodeId">终点节点编号。</param>
        /// <returns>
        /// 返回 PlannedPath。
        /// 如果可达，IsAvailable 为 true，并包含 Nodes、Edges、TotalLength。
        /// 如果不可达，IsAvailable 为 false，并通过 Message 说明原因。
        /// </returns>
        PlannedPath FindPlannedPath(string startNodeId, string endNodeId);

        /// <summary>
        /// 根据路径规划请求规划路径。
        /// 
        /// 需求说明：
        /// 这是增强版路径规划接口。
        /// 用于后续正式调度场景，可以在路径规划时考虑车辆品牌、车辆能力、是否避开锁定边、是否避开禁用边等条件。
        /// 
        /// 使用场景：
        /// 1. 不同品牌 AGV 有不同可行驶区域。
        /// 2. 某些路径边只允许特定品牌或特定能力车辆通过。
        /// 3. 规划路径时需要避开已经锁定、禁用或关闭的路径。
        /// 4. 调度评分时计算某台车到任务起点的真实可行路径。
        /// 
        /// 示例：
        /// Okapi 车辆只能走 Brand=Okapi 允许的边；
        /// 叉车类 AGV 才能进入需要 Fork 能力的节点。
        /// </summary>
        /// <param name="request">路径规划请求，包含起点、终点、车辆约束、避让策略等参数。</param>
        /// <returns>
        /// 返回符合请求条件的 PlannedPath。
        /// 如果没有符合条件的路径，则 IsAvailable 为 false。
        /// </returns>
        PlannedPath FindPlannedPath(PathPlanningRequest request);

        /// <summary>
        /// 判断起点到终点是否存在可用路径。
        /// 
        /// 需求说明：
        /// 调度系统在派发任务前，不一定需要完整路径详情，有时只需要判断任务路线是否可达。
        /// 
        /// 使用场景：
        /// 1. 创建任务时判断 SourceNodeId 到 TargetNodeId 是否可达。
        /// 2. 派发任务前快速拒绝不可达任务。
        /// 3. 任务模板保存前校验路线合法性。
        /// 
        /// 注意：
        /// 该方法通常可以内部调用 FindPlannedPath(startNodeId, endNodeId).IsAvailable。
        /// </summary>
        /// <param name="startNodeId">起点节点编号。</param>
        /// <param name="endNodeId">终点节点编号。</param>
        /// <returns>可达返回 true；不可达返回 false。</returns>
        bool IsPathAvailable(string startNodeId, string endNodeId);

        /// <summary>
        /// 获取起点到终点的路径距离。
        /// 
        /// 需求说明：
        /// 调度评分时需要比较不同车辆到任务起点的距离，距离越短，通常优先级越高。
        /// 该接口用于给调度评分模块提供路径距离或路径成本。
        /// 
        /// 使用场景：
        /// 1. 车辆选择评分：计算车辆当前位置到任务起点的距离。
        /// 2. 任务预计耗时：根据路径距离和车辆速度估算时间。
        /// 3. 多条路线比较：选择距离更短或成本更低的路线。
        /// 
        /// 注意：
        /// 如果路径不可达，建议返回 double.PositiveInfinity 或约定的最大值，
        /// 调用方据此判断该路线不可用。
        /// </summary>
        /// <param name="startNodeId">起点节点编号。</param>
        /// <param name="endNodeId">终点节点编号。</param>
        /// <returns>路径总距离；不可达时返回正无穷或约定最大值。</returns>
        double GetPathDistance(string startNodeId, string endNodeId);

        /// <summary>
        /// 查找从指定节点出发可以到达的所有节点。
        /// 
        /// 需求说明：
        /// 调度系统有时需要知道某台车当前位置能够到达哪些点位，
        /// 例如寻找可达的等待点、充电点、取货点，或者分析地图连通性。
        /// 
        /// 使用场景：
        /// 1. 给空闲车辆寻找最近等待点。
        /// 2. 低电量车辆寻找可达充电点。
        /// 3. 地图校验时判断是否存在孤立区域。
        /// 4. 调度失败时分析车辆是否被困在某个不可达区域。
        /// 
        /// 注意：
        /// 该接口通常需要基于当前启用节点和可通行边进行图遍历。
        /// </summary>
        /// <param name="startNodeId">起始节点编号。</param>
        /// <returns>从 startNodeId 出发可以到达的节点列表。</returns>
        IReadOnlyList<MapNode> FindReachableNodes(string startNodeId);

        /// <summary>
        /// 按节点类型查找地图节点。
        /// 
        /// 需求说明：
        /// 调度系统经常需要按业务类型查询点位，例如全部取货点、全部放货点、全部充电点、全部等待点。
        /// 
        /// 使用场景：
        /// 1. 创建任务时只允许用户选择 Pickup 作为起点，Dropoff 作为终点。
        /// 2. 低电量调度时查找 Charge 类型节点。
        /// 3. 空闲车辆调度时查找 Waiting 类型节点。
        /// 4. 地图界面按类型筛选显示节点。
        /// </summary>
        /// <param name="nodeType">节点类型，例如 Pickup、Dropoff、Charge、Waiting。</param>
        /// <returns>指定类型的节点列表。</returns>
        IReadOnlyList<MapNode> FindNodesByType(MapNodeType nodeType);
    }
}
