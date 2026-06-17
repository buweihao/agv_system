namespace AgvDispatcher.Core.Contracts.Planning.Enums
{
    /// <summary>
    /// 车辆运动方向。
    /// </summary>
    public enum MoveDirection
    {
        /// <summary>
        /// 未知方向。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 向前行驶。
        /// </summary>
        Forward = 1,

        /// <summary>
        /// 向后行驶（倒车）。
        /// </summary>
        Backward = 2,

        /// <summary>
        /// 向左平移或左转。
        /// </summary>
        Left = 3,

        /// <summary>
        /// 向右平移或右转。
        /// </summary>
        Right = 4,

        /// <summary>
        /// 原地掉头。
        /// </summary>
        TurnAround = 5
    }
}
