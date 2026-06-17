using System.Collections.Generic;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Interfaces
{
    /// <summary>
    /// 地图静态查询服务。
    /// </summary>
    /// <remarks>
    /// 该服务是其他模块读取“当前已发布地图”的统一入口。
    /// 
    /// 只负责静态地图查询，例如：
    /// 1. 查询当前地图快照；
    /// 2. 查询点位、路线；
    /// 3. 判断点位或路线是否存在；
    /// 4. 查询点位出边；
    /// 5. 查询点位类型；
    /// 6. 查询系统点位与厂商点位之间的映射关系。
    /// 
    /// 不负责：
    /// 1. 地图编辑；
    /// 2. 地图保存；
    /// 3. 地图发布；
    /// 4. 路径规划；
    /// 5. 交通管制；
    /// 6. 点位占用；
    /// 7. 路径锁定；
    /// 8. 区域占用。
    /// 
    /// 地图编辑、保存、发布应放在 IMapManagementService。
    /// 路径规划应放在 IPathPlanner。
    /// 动态交通资源占用应放在 ITrafficControlService。
    /// </remarks>
    public interface IMapService
    {
        /// <summary>
        /// 获取当前正在运行的地图快照。
        /// </summary>
        /// <remarks>
        /// MapSnapshot 应该是一个相对完整、不可随意修改的地图视图，
        /// 通常包含点位、路线、区域、版本号、发布时间等信息。
        /// 
        /// 路径规划、交通管制、监控展示等模块可以基于该快照进行读取。
        /// 建议 MapSnapshot 尽量设计成只读对象，避免外部模块修改地图数据。
        /// </remarks>
        /// <returns>当前已发布地图快照。</returns>
        MapSnapshot GetCurrentMap();

        /// <summary>
        /// 获取当前地图中的所有点位。
        /// </summary>
        /// <remarks>
        /// 点位包括取货点、放货点、等待点、充电点、路口点、限制点等。
        /// 
        /// 该接口只返回静态点位配置，不包含车辆当前是否占用该点位。
        /// 点位占用状态应从 ITrafficControlService 获取。
        /// </remarks>
        /// <returns>当前地图中的点位列表。</returns>
        IReadOnlyList<MapNode> GetNodes();

        /// <summary>
        /// 获取当前地图中的所有路线。
        /// </summary>
        /// <remarks>
        /// 路线表示点位之间的可通行关系。
        /// 
        /// 该接口只返回静态路线配置，例如起点、终点、方向、距离、启用状态等。
        /// 路线是否被其他车辆锁定，应从 ITrafficControlService 获取。
        /// </remarks>
        /// <returns>当前地图中的路线列表。</returns>
        IReadOnlyList<MapEdge> GetEdges();

        /// <summary>
        /// 根据点位编号获取点位信息。
        /// </summary>
        /// <param name="nodeId">系统内部点位编号。</param>
        /// <returns>
        /// 如果点位存在，返回对应的 MapNode；
        /// 如果点位不存在，返回 null。
        /// </returns>
        MapNode? GetNode(string nodeId);

        /// <summary>
        /// 根据路线编号获取路线信息。
        /// </summary>
        /// <param name="edgeId">系统内部路线编号。</param>
        /// <returns>
        /// 如果路线存在，返回对应的 MapEdge；
        /// 如果路线不存在，返回 null。
        /// </returns>
        MapEdge? GetEdge(string edgeId);

        /// <summary>
        /// 判断指定点位是否存在于当前地图中。
        /// </summary>
        /// <remarks>
        /// 常用于任务创建、任务派发、厂商点位转换前的基础校验。
        /// </remarks>
        /// <param name="nodeId">系统内部点位编号。</param>
        /// <returns>存在返回 true；不存在返回 false。</returns>
        bool NodeExists(string nodeId);

        /// <summary>
        /// 判断指定路线是否存在于当前地图中。
        /// </summary>
        /// <param name="edgeId">系统内部路线编号。</param>
        /// <returns>存在返回 true；不存在返回 false。</returns>
        bool EdgeExists(string edgeId);

        /// <summary>
        /// 获取从指定点位出发的所有出边。
        /// </summary>
        /// <remarks>
        /// 主要供路径规划模块使用。
        /// 
        /// 例如路径规划从 A 点开始计算时，
        /// 可以通过该接口获取 A 点能够直接到达的下一批路线。
        /// 
        /// 该接口只考虑地图静态连通关系。
        /// 某条边当前是否被锁定，应由调度模块从 ITrafficControlService 获取动态阻塞信息后，
        /// 再传给路径规划模块进行过滤。
        /// </remarks>
        /// <param name="nodeId">起始点位编号。</param>
        /// <returns>从该点位出发的路线列表。</returns>
        IReadOnlyList<MapEdge> GetOutgoingEdges(string nodeId);

        /// <summary>
        /// 按点位类型查询点位。
        /// </summary>
        /// <remarks>
        /// 常用于查找某一类功能点，例如：
        /// 1. 所有充电点；
        /// 2. 所有等待点；
        /// 3. 所有取货点；
        /// 4. 所有放货点。
        /// 
        /// 注意：该接口只负责按类型查询。
        /// 至于“最近的充电点”“当前可用的等待点”，不建议放在这里。
        /// 最近点计算应放在 IPathPlanner；
        /// 当前是否可用应结合 ITrafficControlService 判断。
        /// </remarks>
        /// <param name="nodeType">点位类型。</param>
        /// <returns>符合该类型的点位列表。</returns>
        IReadOnlyList<MapNode> GetNodesByType(MapNodeType nodeType);

        /// <summary>
        /// 根据系统内部点位编号，获取指定厂商对应的点位编号。
        /// </summary>
        /// <remarks>
        /// 系统内部通常使用统一点位编号。
        /// 不同 AGV 厂商可能有自己的点位编号。
        /// 
        /// 例如：
        /// 系统点位：STATION_A
        /// 海康点位：HK_001
        /// 仙工点位：SEER_A01
        /// 
        /// 厂商适配模块在下发任务前，可以通过该接口把系统点位转换成厂商点位。
        /// </remarks>
        /// <param name="systemNodeId">系统内部点位编号。</param>
        /// <param name="vendorCode">厂商编码，例如 HikRobot、Seer、Okapi。</param>
        /// <returns>
        /// 如果存在映射，返回厂商点位编号；
        /// 如果不存在映射，返回 null。
        /// </returns>
        string? GetVendorNodeCode(string systemNodeId, string vendorCode);

        /// <summary>
        /// 根据厂商点位编号，反查系统内部点位编号。
        /// </summary>
        /// <remarks>
        /// 常用于厂商状态回调、车辆位置同步。
        /// 
        /// 例如厂商上报车辆当前在 HK_001，
        /// 系统需要将 HK_001 转换成内部点位 STATION_A，
        /// 然后再用于监控显示、交通管制、任务状态判断。
        /// </remarks>
        /// <param name="vendorNodeCode">厂商点位编号。</param>
        /// <param name="vendorCode">厂商编码。</param>
        /// <returns>
        /// 如果存在映射，返回系统内部点位编号；
        /// 如果不存在映射，返回 null。
        /// </returns>
        string? GetSystemNodeId(string vendorNodeCode, string vendorCode);
    }
}
