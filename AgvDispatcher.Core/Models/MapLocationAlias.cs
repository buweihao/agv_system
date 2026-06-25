namespace AgvDispatcher.Core.Models
{
    /// <summary>
    /// 旧版地图位置别名模型。
    /// 跨模块接口请参考 AgvDispatcher.Core.Contracts.Map 中的相关 DTO 模型。
    /// </summary>
    public class MapLocationAlias
    {
        public string AliasId { get; set; } = string.Empty;

        public string MapId { get; set; } = string.Empty;

        public string MapVersion { get; set; } = "v1";

        public string NodeId { get; set; } = string.Empty;

        public string AliasType { get; set; } = string.Empty;

        public string AliasValue { get; set; } = string.Empty;

        public string? Brand { get; set; }

        public bool IsEnabled { get; set; } = true;

        public string? Remark { get; set; }
    }
}
