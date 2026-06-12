namespace AgvDispatcher.Core.Models
{
    public class MapLocationAlias
    {
        public string AliasId { get; set; } = string.Empty;

        public string MapId { get; set; } = string.Empty;

        public string NodeId { get; set; } = string.Empty;

        public string AliasType { get; set; } = string.Empty;

        public string AliasValue { get; set; } = string.Empty;

        public string? Brand { get; set; }

        public bool IsEnabled { get; set; } = true;

        public string? Remark { get; set; }
    }
}
