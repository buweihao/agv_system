namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Identifies the category of a traffic-controlled resource.
    /// 标识受交通控制资源的类别。
    /// </summary>
    public enum TrafficResourceType
    {
        /// <summary>
        /// A map node resource.
        /// 地图节点资源。
        /// </summary>
        Node = 1,

        /// <summary>
        /// A map edge resource.
        /// 地图边资源。
        /// </summary>
        Edge = 2,

        /// <summary>
        /// A logical traffic zone resource.
        /// 逻辑交通区域资源。
        /// </summary>
        Zone = 3,

        /// <summary>
        /// A work station resource.
        /// 工作站资源。
        /// </summary>
        Station = 4,

        /// <summary>
        /// A charger resource.
        /// 充电桩资源。
        /// </summary>
        Charger = 5,

        /// <summary>
        /// An elevator resource.
        /// 电梯资源。
        /// </summary>
        Elevator = 6,

        /// <summary>
        /// A door resource.
        /// 门资源。
        /// </summary>
        Door = 7
    }
}
