namespace AgvDispatcher.Core.Models
{
    /// <summary>
    /// 旧版地图位置模型。
    /// 跨模块接口请参考 AgvDispatcher.Core.Contracts.Map 中的相关 DTO 模型。
    /// </summary>
    public class MapPosition
    {
        public string MapId { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public double Heading { get; set; }

        public string? NodeId { get; set; }

        public string? AreaCode { get; set; }

        /// <summary>
        /// Indicates that the coordinates belong to a known map coordinate system.
        /// MapId is the validity marker so that (0, 0) remains a valid position.
        /// </summary>
        public bool HasValidCoordinates() =>
            !string.IsNullOrWhiteSpace(MapId) &&
            double.IsFinite(X) &&
            double.IsFinite(Y);

        public MapPosition Clone() => new()
        {
            MapId = MapId,
            X = X,
            Y = Y,
            Z = Z,
            Heading = Heading,
            NodeId = NodeId,
            AreaCode = AreaCode
        };
    }
}
