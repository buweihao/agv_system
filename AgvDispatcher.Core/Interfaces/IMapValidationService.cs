namespace AgvDispatcher.Core.Interfaces
{
    /// <summary>
    /// 地图校验结果的严重级别。
    /// </summary>
    public enum MapValidationLevel
    {
        /// <summary>提示信息，不影响地图使用，仅供参考（如冗余配置、命名建议）。</summary>
        Info,

        /// <summary>警告，地图仍可使用，但存在潜在隐患（如孤立节点、单向不可达），建议修正。</summary>
        Warning,

        /// <summary>错误，存在严重问题会导致调度/路径规划失败（如边引用了不存在的节点），必须修正。</summary>
        Error
    }

    /// <summary>
    /// 单条地图校验结果。
    /// <para>描述某个地图对象（节点、边、充电桩、别名等）的一项校验发现。</para>
    /// </summary>
    public class MapValidationResult
    {
        /// <summary>校验结果的严重级别，见 <see cref="MapValidationLevel"/>。</summary>
        public MapValidationLevel Level { get; set; }

        /// <summary>问题所属的对象类型，如 <c>MapNode</c>、<c>MapEdge</c>、<c>ChargeStation</c>、<c>MapLocationAlias</c>。</summary>
        public string ObjectType { get; set; } = string.Empty;

        /// <summary>问题对象的标识（如节点编号、边编号）；若为全局性问题可为空。</summary>
        public string ObjectId { get; set; } = string.Empty;

        /// <summary>面向用户的问题描述信息，说明校验发现的具体内容及建议。</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 地图校验服务接口。
    /// <para>
    /// 对地图数据进行一致性与完整性校验，确保拓扑可用于调度与路径规划。
    /// 典型校验项包括：边引用的起止节点是否存在、节点是否孤立/不可达、
    /// 充电桩 <c>NodeId</c> 是否引用有效且类型为充电点的节点、车辆/任务引用的节点是否存在、
    /// 地图别名是否映射到有效节点、品牌与能力限制配置是否合理等
    /// （配置规范详见 <c>docs/map-config-standard.md</c>）。
    /// </para>
    /// <para>
    /// 校验结果以 <see cref="MapValidationResult"/> 列表返回，按 <see cref="MapValidationLevel"/> 区分严重程度，
    /// 供地图编辑/导入界面在保存或下发前提示用户。
    /// </para>
    /// </summary>
    public interface IMapValidationService
    {
        /// <summary>
        /// 校验当前系统中已持久化的地图数据。
        /// </summary>
        /// <returns>
        /// 校验结果只读列表；若全部通过则返回空列表（或仅含 <see cref="MapValidationLevel.Info"/> 级别项）。
        /// 内部会加载当前的节点、边、充电桩与别名等数据进行整体校验。
        /// </returns>
        Task<IReadOnlyList<MapValidationResult>> ValidateMapAsync();

        /// <summary>
        /// 校验一组给定的地图数据（尚未持久化）。
        /// </summary>
        /// <param name="nodes">待校验的地图节点集合。</param>
        /// <param name="edges">待校验的地图边集合。</param>
        /// <param name="chargeStations">待校验的充电桩集合，会检查其引用的节点是否有效。</param>
        /// <param name="aliases">待校验的地图别名集合，会检查其映射的节点是否存在。</param>
        /// <returns>
        /// 校验结果只读列表；若全部通过则返回空列表（或仅含 <see cref="MapValidationLevel.Info"/> 级别项）。
        /// </returns>
        /// <remarks>
        /// 适用于地图导入/编辑场景：在数据写入数据库之前，先对内存中的草稿数据做预校验，
        /// 从而在保存前向用户暴露错误，避免落库后才发现问题。
        /// </remarks>
        Task<IReadOnlyList<MapValidationResult>> ValidateMapDataAsync(IEnumerable<AgvDispatcher.Core.Models.MapNode> nodes, IEnumerable<AgvDispatcher.Core.Models.MapEdge> edges, IEnumerable<AgvDispatcher.Core.Models.ChargeStation> chargeStations, IEnumerable<AgvDispatcher.Core.Models.MapLocationAlias> aliases);
    }
}
