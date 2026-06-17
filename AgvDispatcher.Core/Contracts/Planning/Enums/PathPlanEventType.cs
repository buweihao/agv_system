namespace AgvDispatcher.Core.Contracts.Planning.Enums
{
    /// <summary>
    /// 路径规划相关事件类型。
    /// 主要用于日志、监控、审计与调试，业务流程不应强依赖于此。
    /// </summary>
    public enum PathPlanEventType
    {
        /// <summary>
        /// 收到路径规划请求。
        /// </summary>
        PathPlanRequested = 2001,

        /// <summary>
        /// 路径规划成功。
        /// </summary>
        PathPlanSucceeded = 2002,

        /// <summary>
        /// 路径规划失败。
        /// </summary>
        PathPlanFailed = 2003,

        /// <summary>
        /// 路径规划超时。
        /// </summary>
        PathPlanTimeout = 2004,

        /// <summary>
        /// 请求重新规划路径。
        /// </summary>
        PathReplanRequested = 2005,

        /// <summary>
        /// 生成了备选候选路径。
        /// </summary>
        AlternativePathGenerated = 2006
    }
}
